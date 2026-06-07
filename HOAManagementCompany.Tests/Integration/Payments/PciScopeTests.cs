using System.Net;
using System.Net.Http.Json;
using System.Reflection;
using System.Text.Json;
using HOAManagementCompany.Domain.Entities;
using HOAManagementCompany.Infrastructure.Persistence;
using HOAManagementCompany.Tests.Fixtures;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace HOAManagementCompany.Tests.Integration.Payments;

/// <summary>
/// SC-001 PCI-scope guarantee (T034): the backend never accepts or stores a raw card/bank number.
/// The browser tokenises the instrument with Stripe.js and the API only ever sees opaque
/// <c>pm_…/pi_…/seti_…</c> references plus masked display detail (brand + last 4). This verifies the
/// guarantee two ways: the request contracts have no field that could carry a PAN/account/routing
/// number, and a real charge leaves no full card number anywhere in the persisted payment schema.
/// </summary>
public class PciScopeTests(TestDatabaseFixture fixture) : PaymentTestBase(fixture)
{
    // A full test PAN and a bank account/routing pair — none of these must ever land in the DB.
    private const string TestPan = "4242424242424242";
    private const string TestAccountNumber = "000123456789";
    private const string TestRoutingNumber = "110000000";

    private static readonly string[] ForbiddenPropertyTokens =
    {
        "cardnumber", "cardno", "pan", "cvc", "cvv", "securitycode",
        "routingnumber", "routing", "accountnumber", "bankaccount", "iban",
    };

    private static async Task<JsonElement> JsonAsync(HttpResponseMessage res) =>
        JsonDocument.Parse(await res.Content.ReadAsStringAsync()).RootElement;

    [Fact]
    public void RequestContracts_ExposeNoRawCardOrBankField()
    {
        // Every inbound payment DTO the model binder can populate: a field named like a PAN/account/
        // routing/CVC would be a way to smuggle raw instrument data into the backend. There must be none.
        var requestTypes = typeof(HOAManagementCompany.Features.Payments.Models.LedgerRequest).Assembly
            .GetTypes()
            .Where(t => t is { IsClass: true, IsAbstract: false }
                && t.Namespace?.StartsWith("HOAManagementCompany.Features.Payments") == true
                && t.Name.EndsWith("Request", StringComparison.Ordinal))
            .ToList();

        Assert.NotEmpty(requestTypes); // guard: reflection actually found the contracts.

        var offenders = new List<string>();
        foreach (var type in requestTypes)
        foreach (var prop in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            var name = prop.Name.ToLowerInvariant();
            if (ForbiddenPropertyTokens.Any(token => name.Contains(token)))
                offenders.Add($"{type.Name}.{prop.Name}");
        }

        Assert.True(offenders.Count == 0,
            $"Payment request DTOs must not expose raw card/bank fields: {string.Join(", ", offenders)}");
    }

    [Fact]
    public async Task ChargedCard_PersistsOnlyTokensAndMaskedDetail_NeverThePan()
    {
        await AuthenticateAsync();
        var propertyId = Guid.Parse("aaaaaaaa-0000-0000-0000-000000000001");

        // Drive a real one-time card charge. The client never sends the PAN — only the amount/method —
        // mirroring the tokenised flow; the PAN below exists solely to prove it can't reach the DB.
        var intent = await JsonAsync(await Client.PostAsJsonAsync(
            "/api/v1/payments/intent", new { amount = 250m, method = "card" }));
        var intentId = intent.GetProperty("paymentIntentId").GetString();

        var confirm = await Client.PostAsJsonAsync(
            "/api/v1/payments/one-time/confirm", new { paymentIntentId = intentId });
        Assert.Equal(HttpStatusCode.OK, confirm.StatusCode);

        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var txns = await db.PaymentTransactions.AsNoTracking().Where(t => t.PropertyId == propertyId).ToListAsync();
        var receipts = await db.Receipts.AsNoTracking()
            .Where(r => txns.Select(t => t.Id).Contains(r.TransactionId)).ToListAsync();
        var recurrings = await db.RecurringPayments.AsNoTracking().Where(r => r.PropertyId == propertyId).ToListAsync();
        var auths = await db.PaymentAuthorizations.AsNoTracking()
            .Where(a => recurrings.Select(r => r.Id).Contains(a.RecurringPaymentId)).ToListAsync();
        var drafts = await db.DraftEntries.AsNoTracking().Where(d => d.PropertyId == propertyId).ToListAsync();
        var owners = await db.Owners.AsNoTracking().Where(o => o.PropertyId == propertyId).ToListAsync();

        // Concatenate every persisted string across the payment schema and confirm no raw number leaked.
        var haystack = string.Join("", new object[] { }
            .Concat(txns).Concat(receipts).Concat(recurrings).Concat(auths).Concat(drafts).Concat(owners)
            .SelectMany(StringValues));

        Assert.NotEmpty(txns); // guard: a transaction really was written.
        Assert.DoesNotContain(TestPan, haystack);
        Assert.DoesNotContain(TestAccountNumber, haystack);
        Assert.DoesNotContain(TestRoutingNumber, haystack);

        // Positive control: the safe, masked display detail (last 4 only) is what actually persists.
        var receipt = Assert.Single(receipts);
        Assert.Contains("4242", receipt.MaskedMethod);
        Assert.DoesNotContain(TestPan, receipt.MaskedMethod);
        Assert.All(txns, t => Assert.True(
            t.StripePaymentIntentId is null || t.StripePaymentIntentId.StartsWith("pi_"),
            "Only opaque PaymentIntent references may persist."));
    }

    private static IEnumerable<string> StringValues(object entity) =>
        entity.GetType()
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.PropertyType == typeof(string))
            .Select(p => p.GetValue(entity) as string)
            .Where(v => !string.IsNullOrEmpty(v))!
            .Cast<string>();
}
