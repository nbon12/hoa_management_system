using NetArchTest.Rules;
using Xunit;

namespace HOAManagementCompany.UnitTests.Architecture;

// <!-- REPOWISE:START domain=architecture -->
// Enforced dependency rules (015 US5, SC-005): Infrastructure never depends on Features; Domain
// depends on nothing above it; provider-SDK (Stripe) types are confined to Infrastructure.Payments;
// feature slices don't import each other's internals (shared kernel lives in Domain /
// Features.Common). These run as ordinary unit tests in the container-free tier, so a violation
// fails every PR build.
// <!-- REPOWISE:END -->

public class LayeringTests
{
    private static readonly System.Reflection.Assembly App = typeof(Program).Assembly;

    private const string Domain = "HOAManagementCompany.Domain";
    private const string Features = "HOAManagementCompany.Features";
    private const string Infrastructure = "HOAManagementCompany.Infrastructure";

    private static void AssertClean(TestResult result)
    {
        var offenders = result.FailingTypes?.Select(t => t.FullName ?? t.Name).OrderBy(n => n).ToList() ?? [];
        Assert.True(result.IsSuccessful, "Layering violation by:\n" + string.Join("\n", offenders));
    }

    [Fact]
    public void Infrastructure_MustNotDependOn_Features() =>
        AssertClean(Types.InAssembly(App)
            .That().ResideInNamespaceStartingWith(Infrastructure)
            .ShouldNot().HaveDependencyOnAny(Features)
            .GetResult());

    [Fact]
    public void Domain_MustNotDependOn_FeaturesOrInfrastructure() =>
        AssertClean(Types.InAssembly(App)
            .That().ResideInNamespaceStartingWith(Domain)
            .ShouldNot().HaveDependencyOnAny(Features, Infrastructure)
            .GetResult());

    [Fact]
    public void ProviderSdk_IsConfinedTo_InfrastructurePayments() =>
        // "Stripe." (with the dot) targets the SDK namespace (Stripe.Event, Stripe.Charge, …)
        // without false-positiving on our own Stripe-prefixed type names (StripeOptions, …).
        AssertClean(Types.InAssembly(App)
            .That().DoNotResideInNamespaceStartingWith("HOAManagementCompany.Infrastructure.Payments")
            .ShouldNot().HaveDependencyOnAny("Stripe.")
            .GetResult());

    [Theory]
    // Each feature slice must not reach into the others' internals. Shared kernel concepts live
    // in Domain (DomainException, MoneyPolicy) and Features.Common (error handling, claims).
    [InlineData("HOAManagementCompany.Features.Auth")]
    [InlineData("HOAManagementCompany.Features.Community")]
    [InlineData("HOAManagementCompany.Features.Property")]
    [InlineData("HOAManagementCompany.Features.Dashboard")]
    [InlineData("HOAManagementCompany.Features.Payments")]
    [InlineData("HOAManagementCompany.Features.DevTools")]
    public void FeatureSlices_MustNotDependOn_OtherSlices(string slice)
    {
        string[] slices =
        [
            "HOAManagementCompany.Features.Auth",
            "HOAManagementCompany.Features.Community",
            "HOAManagementCompany.Features.Property",
            "HOAManagementCompany.Features.Dashboard",
            "HOAManagementCompany.Features.Payments",
            "HOAManagementCompany.Features.DevTools",
        ];
        var others = slices.Where(s => s != slice).ToArray();

        AssertClean(Types.InAssembly(App)
            .That().ResideInNamespaceStartingWith(slice)
            .ShouldNot().HaveDependencyOnAny(others)
            .GetResult());
    }
}
