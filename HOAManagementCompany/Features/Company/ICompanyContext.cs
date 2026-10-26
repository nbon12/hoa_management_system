using System.Security.Claims;

namespace HOAManagementCompany.Features.Company;

// 030 R1: resolves "the company" for a request. Every company query filters by the id it returns,
// so a second company later changes only how this resolves (FR-001), and tests that create their
// own companies stay isolated from each other.
public interface ICompanyContext
{
    // The company of the caller's live Company Administrator grant, else of the communities where
    // they hold an effective Community Manager membership; null when they have neither.
    Task<Guid?> ForUserAsync(ClaimsPrincipal user, CancellationToken ct = default);

    // The company a community belongs to; null for a community that doesn't exist.
    Task<Guid?> ForCommunityAsync(Guid communityId, CancellationToken ct = default);
}
