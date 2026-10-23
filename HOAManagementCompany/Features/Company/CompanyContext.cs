using System.Security.Claims;
using HOAManagementCompany.Domain.Entities;
using HOAManagementCompany.Domain.Enums;
using HOAManagementCompany.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace HOAManagementCompany.Features.Company;

// Scoped: answers are cached for the request (030 R1).
public sealed class CompanyContext(ApplicationDbContext db) : ICompanyContext
{
    private readonly Dictionary<string, Guid?> _byUser = [];
    private readonly Dictionary<Guid, Guid?> _byCommunity = [];

    public async Task<Guid?> ForUserAsync(ClaimsPrincipal user, CancellationToken ct = default)
    {
        var userId = user.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? user.FindFirst("sub")?.Value;
        if (string.IsNullOrEmpty(userId))
            return null;
        if (_byUser.TryGetValue(userId, out var cached))
            return cached;

        var companyId = await db.CompanyAdministratorGrants
            .Where(g => g.UserId == userId && g.RevokedAt == null)
            .OrderBy(g => g.GrantedAt)
            .Select(g => (Guid?)g.CompanyId)
            .FirstOrDefaultAsync(ct);

        if (companyId is null)
        {
            var today = DateOnly.FromDateTime(DateTime.UtcNow);
            companyId = await db.CommunityMemberships
                .Where(m => m.UserId == userId
                         && m.Role == CommunityRole.CommunityManager
                         && m.Status == MembershipStatus.Active
                         && (m.EndDate == null || m.EndDate >= today)
                         && m.Community.Status != CommunityStatus.Archived)
                .OrderBy(m => m.StartDate)
                .Select(m => (Guid?)m.Community.ManagementCompanyId)
                .FirstOrDefaultAsync(ct);
        }

        _byUser[userId] = companyId;
        return companyId;
    }

    public async Task<Guid?> ForCommunityAsync(Guid communityId, CancellationToken ct = default)
    {
        if (_byCommunity.TryGetValue(communityId, out var cached))
            return cached;
        var companyId = await db.Communities
            .Where(c => c.Id == communityId)
            .Select(c => (Guid?)c.ManagementCompanyId)
            .FirstOrDefaultAsync(ct);
        _byCommunity[communityId] = companyId;
        return companyId;
    }

    public async Task<ManagementCompany> GetDeploymentCompanyAsync(CancellationToken ct = default)
    {
        var id = await DeploymentCompany.IdAsync(db, ct);
        return await db.ManagementCompanies.FirstAsync(c => c.Id == id, ct);
    }
}
