using Xunit;

// Playwright + shared Postgres are flaky under parallel execution.
[assembly: CollectionBehavior(DisableTestParallelization = true)]

