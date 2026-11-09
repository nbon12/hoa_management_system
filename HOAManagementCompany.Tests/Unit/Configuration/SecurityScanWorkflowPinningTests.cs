using System.Text.RegularExpressions;
using Xunit;

namespace HOAManagementCompany.Tests.Unit.Configuration;

/// <summary>
/// Spec 011 (Trivy security scanning), User Story 3 / FR-008: every third-party action in the
/// scanning workflow is pinned to a full-length immutable commit SHA.
///
/// Covers:
///  - US3-AS1: Given the scanning workflow definition, When it is reviewed, Then every
///    third-party action reference is pinned to a full-length immutable commit SHA.
///  - US3-AS2: Given an upstream action retags a release, When our pipeline runs, Then it still
///    executes the exact pinned revision. GitHub resolves an @&lt;40-hex sha&gt; ref to that exact
///    commit, so a full-SHA pin is the static guarantee of this property.
///  - US3-IT: no reference uses a branch (@main/@master) or a floating tag (@v1, @v2).
/// </summary>
public class SecurityScanWorkflowPinningTests
{
    private static readonly Regex UsesLine =
        new(@"^\s*(?:-\s*)?uses:\s*(?<ref>[^\s#]+)", RegexOptions.Compiled);

    private static readonly Regex FullShaRef =
        new(@"^[^@\s]+@[0-9a-f]{40}$", RegexOptions.Compiled);

    private static string WorkflowPath =>
        Path.Combine(FindRepoRoot(), ".github", "workflows", "security-scan.yml");

    private static List<(int Line, string Ref)> ThirdPartyActionRefs()
    {
        var refs = new List<(int, string)>();
        var lines = File.ReadAllLines(WorkflowPath);
        for (var i = 0; i < lines.Length; i++)
        {
            var m = UsesLine.Match(lines[i]);
            if (!m.Success) continue;
            var r = m.Groups["ref"].Value.Trim('"', '\'');
            // Local actions (./path) and docker:// images are not third-party git refs.
            if (r.StartsWith("./") || r.StartsWith("docker://")) continue;
            refs.Add((i + 1, r));
        }
        return refs;
    }

    [Fact]
    public void ScanningWorkflow_HasThirdPartyActionReferences()
    {
        // Guards the pinning assertions below against passing vacuously.
        Assert.True(File.Exists(WorkflowPath), $"Missing {WorkflowPath}");
        Assert.NotEmpty(ThirdPartyActionRefs());
    }

    [Fact]
    public void ScanningWorkflow_EveryThirdPartyAction_IsPinnedToFullLengthCommitSha()
    {
        // US3-AS1, US3-AS2
        var unpinned = ThirdPartyActionRefs()
            .Where(r => !FullShaRef.IsMatch(r.Ref))
            .Select(r => $"line {r.Line}: {r.Ref}")
            .ToList();

        Assert.True(unpinned.Count == 0,
            "security-scan.yml has action refs not pinned to a 40-char commit SHA:\n" +
            string.Join("\n", unpinned));
    }

    [Fact]
    public void ScanningWorkflow_NoActionUsesBranchOrFloatingVersionTag()
    {
        // US3-IT
        var floating = new Regex(@"@(main|master|v\d+(\.\d+){0,2})$");
        var offenders = ThirdPartyActionRefs()
            .Where(r => floating.IsMatch(r.Ref))
            .Select(r => $"line {r.Line}: {r.Ref}")
            .ToList();

        Assert.True(offenders.Count == 0,
            "security-scan.yml references a branch or floating tag:\n" + string.Join("\n", offenders));
    }

    private static string FindRepoRoot()
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
            if (File.Exists(Path.Combine(dir.FullName, "HOAManagementCompany.sln")))
                return dir.FullName;
        throw new InvalidOperationException("Could not locate the repository root (HOAManagementCompany.sln).");
    }
}
