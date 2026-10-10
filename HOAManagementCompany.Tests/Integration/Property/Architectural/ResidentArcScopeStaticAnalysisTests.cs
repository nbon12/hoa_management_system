using System.Text.RegularExpressions;
using Xunit;

namespace HOAManagementCompany.Tests.Integration.Property.Architectural;

/// <summary>
/// 029 T066 / FR-022–FR-023 — structural guarantees for the resident slice, read straight from source:
/// endpoints never touch the database directly (they go through services, which load every draft or
/// application through <c>ResidentArcScope</c>); the scope pins loads to the caller's active property; and
/// nothing in the slice consults the board resolver, so a board role can't widen resident access.
/// </summary>
public class ResidentArcScopeStaticAnalysisTests
{
    private static readonly DirectoryInfo Slice = Locate();

    [Fact]
    public void Endpoints_DependOnServices_NeverOnTheDbContext()
    {
        var endpointCtors = Slice.GetFiles("*Endpoints.cs")
            .SelectMany(f => Regex.Matches(File.ReadAllText(f.FullName), @"public class (\w+Endpoint)\(([^)]*)\)")
                .Select(m => (Name: m.Groups[1].Value, Ctor: m.Groups[2].Value)))
            .ToList();

        Assert.True(endpointCtors.Count >= 15, $"found only {endpointCtors.Count} endpoints — scan is looking in the wrong place");
        var offenders = endpointCtors.Where(e => e.Ctor.Contains("ApplicationDbContext")).Select(e => e.Name).ToList();
        Assert.True(offenders.Count == 0, "Resident ARC endpoints must not query the DbContext directly: " + string.Join(", ", offenders));
    }

    [Fact]
    public void Scope_PinsDraftsAndApplicationsToTheActiveProperty()
    {
        var scope = File.ReadAllText(Path.Combine(Slice.FullName, "ResidentArcScope.cs"));

        Assert.Contains("RequirePropertyId()", scope);
        Assert.Contains("d.Id == draftId && d.PropertyId == propertyId", scope);
        Assert.Contains("a.Id == applicationId && a.PropertyId == propertyId", scope);
    }

    [Fact]
    public void Slice_NeverConsultsTheBoardResolver()
    {
        var offenders = Slice.GetFiles("*.cs")
            .Where(f => File.ReadAllText(f.FullName) is var text
                        && (text.Contains("ICommunityScopeResolver") || text.Contains("CanAccessAsync")))
            .Select(f => f.Name)
            .ToList();

        Assert.Empty(offenders);
    }

    private static DirectoryInfo Locate()
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
        {
            var candidate = new DirectoryInfo(Path.Combine(dir.FullName, "HOAManagementCompany", "Features", "Property", "Architectural"));
            if (candidate.Exists)
                return candidate;
        }
        throw new DirectoryNotFoundException("Could not find HOAManagementCompany/Features/Property/Architectural above the test binary.");
    }
}
