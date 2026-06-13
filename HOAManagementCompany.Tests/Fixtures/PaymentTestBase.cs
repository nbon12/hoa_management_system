using System.Net.Http.Headers;
using System.Net.Http.Json;
using HOAManagementCompany.Infrastructure.Payments;
using Microsoft.Extensions.DependencyInjection;

namespace HOAManagementCompany.Tests.Fixtures;

/// <summary>
/// Base for Stripe-backed payment tests: swaps the network <see cref="IStripeGateway"/> for the
/// in-memory <see cref="FakeStripeGateway"/> (no external calls in CI) and provides resident auth.
/// </summary>
public abstract class PaymentTestBase : IntegrationTestBase
{
    // Initialized before the base constructor runs ConfigureTestServices (derived field initializers
    // run first in C#), so the same instance is registered and observable from tests.
    protected readonly FakeStripeGateway Stripe = new();

    protected PaymentTestBase(TestDatabaseFixture fixture) : base(fixture) { }

    protected override void ConfigureTestServices(IServiceCollection services)
    {
        var existing = services.Where(d => d.ServiceType == typeof(IStripeGateway)).ToList();
        foreach (var d in existing) services.Remove(d);
        services.AddSingleton<IStripeGateway>(Stripe);
    }

    protected override IEnumerable<KeyValuePair<string, string?>> ExtraConfiguration() =>
    [
        // Placeholder Stripe config — FakeStripeGateway replaces the real adapter so
        // these values are never sent to the network; non-empty strings satisfy options binding.
        new("Stripe:SecretKey", "sk_test_" + Guid.NewGuid().ToString("N")),
        new("Stripe:PublishableKey", "pk_test_" + Guid.NewGuid().ToString("N")),
        new("Stripe:WebhookSigningSecret", "whsec_" + Guid.NewGuid().ToString("N")),
        new("Jobs:SchedulerSharedSecret", Guid.NewGuid().ToString("N")),
    ];

    /// <summary>Authenticates as the seeded resident (property aaaaaaaa-…-0001) and sets the bearer token.</summary>
    protected async Task AuthenticateAsync()
    {
        var res = await Client.PostAsJsonAsync("/api/v1/auth/login",
            new { email = "resident@nekohoa.dev", password = "Password1!" });
        var body = await res.Content.ReadFromJsonAsync<Dictionary<string, object>>();
        Client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", body!["token"]!.ToString()!);
    }
}
