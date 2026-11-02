using System.Diagnostics;
using Xunit;

namespace HOAManagementCompany.Tests.Unit.Configuration;

/// <summary>
/// 026 US4-1 / FR-013 / SC-006: once SES replaced the old email provider, no package, option, adapter,
/// validator, config entry, secret or test reference to it may remain in the backend, the test project
/// or the CI workflows. Scans the files git tracks, i.e. what actually ships. Untracked and gitignored
/// files (a developer's local <c>appsettings.Secrets.json</c>, issue #216) and build output are not
/// part of the repo and are ignored.
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

        // Guard against a scan that silently finds nothing (e.g. a wrong path) and passes vacuously.
        Assert.Contains(SourceFiles(root), f => f.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase));

        var hits = FilesReferencingRemovedProvider(root);

        Assert.Empty(hits);
    }

    /// <summary>
    /// Issue #216: a file git doesn't track must not count, even when it mentions the removed provider.
    /// Creates an untracked file inside a searched directory and checks that the scan skips it.
    /// </summary>
    [SkippableFact]
    public void Untracked_files_are_not_scanned()
    {
        var root = FindRepoRoot();
        Skip.IfNot(IsGitWorkTree(root), "Not a git work tree; the scan falls back to every file on disk");

        var untracked = Path.Combine(root, "HOAManagementCompany.Tests", $"untracked-{Guid.NewGuid():N}.md");
        File.WriteAllText(untracked, $"{RemovedProvider}:ApiKey");
        try
        {
            var hits = FilesReferencingRemovedProvider(root);
            Assert.DoesNotContain(Path.GetRelativePath(root, untracked), hits);
        }
        finally
        {
            File.Delete(untracked);
        }
    }

    private static List<string> FilesReferencingRemovedProvider(string root) =>
        SourceFiles(root)
            .Where(f => TextExtensions.Contains(Path.GetExtension(f)) && !IsBuildOutput(root, f) && File.Exists(f))
            .Where(f => File.ReadAllText(f).Contains(RemovedProvider, StringComparison.OrdinalIgnoreCase))
            .Select(f => Path.GetRelativePath(root, f))
            .ToList();

    /// <summary>
    /// The tracked files under <see cref="SearchedDirectories"/>. Without git (e.g. a source export),
    /// falls back to every file on disk there.
    /// </summary>
    private static IEnumerable<string> SourceFiles(string root)
    {
        var tracked = GitLsFiles(root);
        if (tracked is not null)
            return tracked.Select(f => Path.GetFullPath(Path.Combine(root, f)));

        return SearchedDirectories
            .Select(d => Path.Combine(root, d))
            .Where(Directory.Exists)
            .SelectMany(d => Directory.EnumerateFiles(d, "*", SearchOption.AllDirectories));
    }

    private static bool IsGitWorkTree(string root) => GitLsFiles(root) is not null;

    private static List<string>? GitLsFiles(string root)
    {
        var psi = new ProcessStartInfo("git")
        {
            WorkingDirectory = root,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        foreach (var arg in new[] { "ls-files", "-z", "--" }.Concat(SearchedDirectories.Select(d => d.Replace('\\', '/'))))
            psi.ArgumentList.Add(arg);

        try
        {
            using var git = Process.Start(psi);
            if (git is null) return null;
            var stderr = git.StandardError.ReadToEndAsync();
            var output = git.StandardOutput.ReadToEnd();
            git.WaitForExit();
            _ = stderr.Result;
            if (git.ExitCode != 0) return null;
            return output.Split('\0', StringSplitOptions.RemoveEmptyEntries).ToList();
        }
        catch (System.ComponentModel.Win32Exception)
        {
            return null; // git not installed
        }
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
