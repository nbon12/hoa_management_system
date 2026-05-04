using Xunit;

namespace HOAManagementCompany.Tests;

/// <summary>
/// Runs browser tests one at a time to avoid flaky Identity/Blazor logins under parallel load.
/// </summary>
[CollectionDefinition("Playwright", DisableParallelization = true)]
public class PlaywrightCollectionDefinition;
