using HOAManagementCompany.Infrastructure.Payments.Alerts;
using Microsoft.Extensions.DependencyInjection;

namespace HOAManagementCompany.Tests.Fixtures;

/// <summary>
/// Base for US3 alert tests: swaps the real Twilio/SendGrid providers for in-memory
/// <see cref="FakeAlertProvider"/>s (observable from tests, forceable to reject) on top of the
/// <see cref="PaymentTestBase"/> Stripe fake. No external SMS/email/Stripe calls in CI.
/// </summary>
public abstract class AlertTestBase : PaymentTestBase
{
    protected readonly FakeAlertProvider Sms = new("sms");
    protected readonly FakeAlertProvider Email = new("email");

    protected AlertTestBase(TestDatabaseFixture fixture) : base(fixture) { }

    protected override void ConfigureTestServices(IServiceCollection services)
    {
        base.ConfigureTestServices(services);   // swaps IStripeGateway → FakeStripeGateway.

        var existing = services.Where(d => d.ServiceType == typeof(IAlertProvider)).ToList();
        foreach (var d in existing) services.Remove(d);
        services.AddSingleton<IAlertProvider>(Sms);
        services.AddSingleton<IAlertProvider>(Email);
    }
}
