using HOAManagementCompany.Features.Common;
using System.Text.RegularExpressions;
using FastEndpoints;
using FluentValidation;
using HOAManagementCompany.Domain.Entities;
using HOAManagementCompany.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace HOAManagementCompany.Features.Payments.Alerts;

/// <summary>Current alert opt-in matrix for the signed-in owner. Phone is the owner's own (FR-013).</summary>
public sealed record AlertPreferencesDto(bool SmsOptIn, bool EmailOptIn, string? AlertPhone);

/// <summary>Desired opt-in state. SMS requires a phone (in the request or already on file).</summary>
public sealed record UpdateAlertPreferencesRequest(bool SmsOptIn, bool EmailOptIn, string? AlertPhone);

/// <summary>
/// GET /payments/alert-preferences — returns the owner's current opt-in flags + alert phone.
/// Alerts default OFF (TCPA-safe); a brand-new owner reads back all-false.
/// </summary>
public class GetAlertPreferencesEndpoint(ApplicationDbContext db) : EndpointWithoutRequest<AlertPreferencesDto>
{
    public override void Configure()
    {
        Get("/payments/alert-preferences");
        Description(x => x.WithName("GetAlertPreferences").WithTags("Payments"));
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var propertyId = User.GetPropertyId();
        var owner = await db.Owners.FirstOrDefaultAsync(o => o.PropertyId == propertyId, ct);
        if (owner is null) { await SendNotFoundAsync(ct); return; }

        await SendOkAsync(new AlertPreferencesDto(owner.AlertSmsOptIn, owner.AlertEmailOptIn, owner.AlertPhone), ct);
    }
}

/// <summary>
/// PUT /payments/alert-preferences — updates the opt-in matrix. Every channel whose state changes
/// appends an immutable <see cref="AlertConsent"/> row (opt_in/opt_out) with the consent text and
/// source IP — the TCPA paper trail (FR-031). SMS may only be enabled once a phone is on file.
/// </summary>
public class UpdateAlertPreferencesEndpoint(ApplicationDbContext db)
    : Endpoint<UpdateAlertPreferencesRequest, AlertPreferencesDto>
{
    private const string SmsOptInText =
        "I agree to receive SMS payment alerts from NekoHOA. Msg & data rates may apply. Reply STOP to opt out.";
    private const string EmailOptInText = "I agree to receive email payment alerts from NekoHOA.";

    public override void Configure()
    {
        Put("/payments/alert-preferences");
        Description(x => x.WithName("UpdateAlertPreferences").WithTags("Payments"));
    }

    public override async Task HandleAsync(UpdateAlertPreferencesRequest req, CancellationToken ct)
    {
        var propertyId = User.GetPropertyId();
        var owner = await db.Owners.FirstOrDefaultAsync(o => o.PropertyId == propertyId, ct);
        if (owner is null) { await SendNotFoundAsync(ct); return; }

        var phone = string.IsNullOrWhiteSpace(req.AlertPhone) ? owner.AlertPhone : req.AlertPhone.Trim();
        if (req.SmsOptIn && string.IsNullOrWhiteSpace(phone))
        {
            AddError(r => r.AlertPhone, "A phone number is required to enable SMS alerts.");
            await SendErrorsAsync(422, ct);   // matches the global validation status (Program.cs Errors.StatusCode).
            return;
        }

        var ip = HttpContext.Connection.RemoteIpAddress?.ToString();

        if (req.SmsOptIn != owner.AlertSmsOptIn)
            AppendConsent(owner, "sms", req.SmsOptIn, ip);
        if (req.EmailOptIn != owner.AlertEmailOptIn)
            AppendConsent(owner, "email", req.EmailOptIn, ip);

        owner.AlertSmsOptIn = req.SmsOptIn;
        owner.AlertEmailOptIn = req.EmailOptIn;
        owner.AlertPhone = phone;
        owner.UpdatedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(ct);

        await SendOkAsync(new AlertPreferencesDto(owner.AlertSmsOptIn, owner.AlertEmailOptIn, owner.AlertPhone), ct);
    }

    private void AppendConsent(Owner owner, string channel, bool optIn, string? ip) =>
        db.AlertConsents.Add(new AlertConsent
        {
            OwnerId = owner.Id,
            Channel = channel,
            Action = optIn ? "opt_in" : "opt_out",
            ConsentText = optIn ? (channel == "sms" ? SmsOptInText : EmailOptInText) : $"Opted out of {channel} payment alerts.",
            SourceIp = ip,
        });
}

public partial class UpdateAlertPreferencesValidator : Validator<UpdateAlertPreferencesRequest>
{
    // E.164: leading '+', country digit 1–9, up to 15 digits total (FR-013, stored encrypted at rest).
    [GeneratedRegex(@"^\+[1-9]\d{1,14}$")]
    private static partial Regex E164();

    public UpdateAlertPreferencesValidator()
    {
        RuleFor(x => x.AlertPhone)
            .Must(p => string.IsNullOrWhiteSpace(p) || E164().IsMatch(p.Trim()))
            .WithMessage("alertPhone must be in E.164 format, e.g. +19195551234.");
    }
}
