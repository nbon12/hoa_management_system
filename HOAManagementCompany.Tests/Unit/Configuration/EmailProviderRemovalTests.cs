using Xunit;

namespace HOAManagementCompany.Tests.Unit.Configuration;

/// <summary>
/// 026 US4-1 / FR-013 / SC-006: once SES replaced the old email provider, no package, option, adapter,
/// validator, config entry, secret or test reference to it may remain in the backend, the test project
/// or the CI workflows. Scans the checked-out source tree; build output is ignored.
/// </summary>
public class EmailProviderRemovalTests
{
    // Built from parts so this file never matches its own search.
    private static readonly string RemovedProvider = string.Concat("Send", "Grid");

    private static readonly string[] SearchedDirectories =
        ["HOAManagementCompany", "HOAManagementCompany.Tests", Path.Combine(".github", "workflows")];

    private static readonly HashSet<string> TextExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".cs", ".csproj", ".props", ".targets", ".json", ".example", ".yml", ".yaml", ".md", ".http", ".config",
    };

    [Fact]
    public void No_references_to_the_removed_email_provider_remain()
    {
        var root = FindRepoRoot();

        var hits = SearchedDirectories
            .Select(d => Path.Combine(root, d))
            .Where(Directory.Exists)
            .SelectMany(d => Directory.EnumerateFiles(d, "*", SearchOption.AllDirectories))
            .Where(f => TextExtensions.Contains(Path.GetExtension(f)) && !IsBuildOutput(root, f))
            .Where(f => File.ReadAllText(f).Contains(RemovedProvider, StringComparison.OrdinalIgnoreCase))
            .Select(f => Path.GetRelativePath(root, f))
            .ToList();

        Assert.Empty(hits);
    }

    private static bool IsBuildOutput(string root, string file) =>
        Path.GetRelativePath(root, file)
            .Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            .Any(part => part is "bin" or "obj" or "TestResults");

    private static string FindRepoRoot()
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
            if (File.Exists(Path.Combine(dir.FullName, "HOAManagementCompany.sln")))
                return dir.FullName;
        throw new InvalidOperationException("Could not locate the repository root (HOAManagementCompany.sln).");
    }
}
