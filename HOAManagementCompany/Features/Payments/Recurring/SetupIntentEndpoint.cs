using HOAManagementCompany.Infrastructure.Configuration;
using HOAManagementCompany.Features.Common;
using FastEndpoints;
using HOAManagementCompany.Features.Payments.Models;
using HOAManagementCompany.Infrastructure.Payments;
using HOAManagementCompany.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace HOAManagementCompany.Features.Payments.Recurring;

/// <summary>
/// POST /payments/recurring/setup-intent — creates (or reuses) the resident's Stripe customer and a
/// SetupIntent so the browser's Stripe Elements can vault a payment method on file (FR-009). Returns
/// the client secret + publishable key; no raw card/bank data ever transits the backend (SC-001).
/// </summary>
public class SetupIntentEndpoint(
    IStripeGateway gateway, ApplicationDbContext db, IOptions<StripeOptions> stripeOptions)
    : EndpointWithoutRequest<SetupIntentResponse>
{
    public override void Configure()
    {
        Post("/payments/recurring/setup-intent");
        // Throttle vaulting attempts alongside the one-time intent/confirm endpoints (FR-028).
        Description(x => x.WithName("CreateSetupIntent").WithTags("Payments").RequireRateLimiting("payments"));
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var propertyId = User.GetPropertyId();
        var owner = await db.Owners.FirstOrDefaultAsync(o => o.PropertyId == propertyId, ct);
        if (owner is null) { await SendNotFoundAsync(ct); return; }

        var customerId = await gateway.EnsureCustomerAsync(
            owner.StripeCustomerId, owner.Email, $"{owner.FirstName} {owner.LastName}".Trim(), ct);
        if (!string.Equals(owner.StripeCustomerId, customerId, StringComparison.Ordinal))
        {
            owner.StripeCustomerId = customerId;
            await db.SaveChangesAsync(ct);
        }

        var setup = await gateway.CreateSetupIntentAsync(customerId, ct);
        await SendOkAsync(new SetupIntentResponse(setup.Id, setup.ClientSecret, stripeOptions.Value.PublishableKey), ct);
    }
}
