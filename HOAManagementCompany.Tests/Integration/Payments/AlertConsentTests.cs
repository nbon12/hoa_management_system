using System.Net;
using System.Net.Http.Json;
using HOAManagementCompany.Features.Payments.Alerts;
using HOAManagementCompany.Infrastructure.Persistence;
using HOAManagementCompany.Tests.Fixtures;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace HOAManagementCompany.Tests.Integration.Payments;

/// <summary>
/// US3 alert-preferences endpoints (FR-013, FR-031): GET/PUT the opt-in matrix, with every state
/// change writing an append-only <see cref="Domain.Entities.AlertConsent"/> row (the TCPA paper
/// trail) and SMS gated on a phone being on file.
/// </summary>
public class AlertConsentTests(TestDatabaseFixture fixture) : AlertTestBase(fixture)
{
    private static readonly Guid PropertyId = Guid.Parse("aaaaaaaa-0000-0000-0000-000000000001");

    private async Task<(Guid ownerId, int consentsBefore)> ResetOwnerAsync(string? phone = null)
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var owner = await db.Owners.FirstAsync(o => o.PropertyId == PropertyId);
        owner.AlertSmsOptIn = false;
        owner.AlertEmailOptIn = false;
        owner.AlertPhone = phone;
        await db.SaveChangesAsync();
        var consents = await db.AlertConsents.CountAsync(c => c.OwnerId == owner.Id);
        return (owner.Id, consents);
    }

    [Fact]
    public async Task PutOptIn_SetsFlags_AppendsConsent_AndGetReflects()
    {
        var (ownerId, before) = await ResetOwnerAsync();
        await AuthenticateAsync();

        var put = await Client.PutAsJsonAsync("/api/v1/payments/alert-preferences",
            new UpdateAlertPreferencesRequest(SmsOptIn: true, EmailOptIn: true, AlertPhone: "+19195550123"));
        Assert.Equal(HttpStatusCode.OK, put.StatusCode);

        var dto = await (await Client.GetAsync("/api/v1/payments/alert-preferences"))
            .Content.ReadFromJsonAsync<AlertPreferencesDto>();
        Assert.True(dto!.SmsOptIn);
        Assert.True(dto.EmailOptIn);
        Assert.Equal("+19195550123", dto.AlertPhone);

        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var consents = await db.AlertConsents.Where(c => c.OwnerId == ownerId).ToListAsync();
        Assert.Equal(before + 2, consents.Count);   // one opt_in per channel
        Assert.Contains(consents, c => c.Channel == "sms" && c.Action == "opt_in");
        Assert.Contains(consents, c => c.Channel == "email" && c.Action == "opt_in");
        Assert.All(consents.Where(c => c.Action == "opt_in"), c => Assert.False(string.IsNullOrWhiteSpace(c.ConsentText)));
    }

    [Fact]
    public async Task PutOptOut_AppendsOptOutConsent_HistoryIsPreserved()
    {
        var (ownerId, _) = await ResetOwnerAsync(phone: "+19195550123");
        await AuthenticateAsync();

        await Client.PutAsJsonAsync("/api/v1/payments/alert-preferences",
            new UpdateAlertPreferencesRequest(true, true, "+19195550123"));
        var afterOptIn = await CountConsentsAsync(ownerId);

        var put = await Client.PutAsJsonAsync("/api/v1/payments/alert-preferences",
            new UpdateAlertPreferencesRequest(false, false, "+19195550123"));
        Assert.Equal(HttpStatusCode.OK, put.StatusCode);

        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var consents = await db.AlertConsents.Where(c => c.OwnerId == ownerId).ToListAsync();
        Assert.Equal(afterOptIn + 2, consents.Count);   // opt-out is appended, never overwrites opt-in
        Assert.Contains(consents, c => c.Channel == "sms" && c.Action == "opt_out");
        Assert.Contains(consents, c => c.Channel == "email" && c.Action == "opt_out");
    }

    [Fact]
    public async Task PutSmsOptIn_WithoutPhone_IsRejected()
    {
        await ResetOwnerAsync(phone: null);
        await AuthenticateAsync();

        var put = await Client.PutAsJsonAsync("/api/v1/payments/alert-preferences",
            new UpdateAlertPreferencesRequest(SmsOptIn: true, EmailOptIn: false, AlertPhone: null));

        Assert.Equal(HttpStatusCode.UnprocessableEntity, put.StatusCode);
    }

    [Fact]
    public async Task PutInvalidPhone_IsRejected()
    {
        await ResetOwnerAsync();
        await AuthenticateAsync();

        var put = await Client.PutAsJsonAsync("/api/v1/payments/alert-preferences",
            new UpdateAlertPreferencesRequest(SmsOptIn: true, EmailOptIn: false, AlertPhone: "555-1234"));

        Assert.Equal(HttpStatusCode.UnprocessableEntity, put.StatusCode);
    }

    private async Task<int> CountConsentsAsync(Guid ownerId)
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        return await db.AlertConsents.CountAsync(c => c.OwnerId == ownerId);
    }
}
