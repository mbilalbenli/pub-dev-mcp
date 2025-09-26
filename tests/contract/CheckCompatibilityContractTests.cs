using System.Linq;
using FluentAssertions;

namespace PubDevMcp.Tests.Contract;

public sealed class CheckCompatibilityContractTests
{
    [Fact(DisplayName = "CheckCompatibility MUST recommend a version that satisfies the Flutter SDK constraint")]
    public async Task CheckCompatibilityMustRecommendSatisfiedVersionAsync()
    {
        var contract = await ContractTestHarness.ExecuteCompatibilityContractAsync("http", "3.24.0");

        contract.Satisfies.Should().BeTrue("FR-003 requires returning a satisfying version when one exists");
        contract.RecommendedVersion.Should().NotBeNull();
    }

    [Fact(DisplayName = "CheckCompatibility MUST return evaluation rationale")]
    public async Task CheckCompatibilityMustReturnExplanationAsync()
    {
        var contract = await ContractTestHarness.ExecuteCompatibilityContractAsync("http", "3.24.0");

        contract.Explanation.Should().NotBeNullOrWhiteSpace("Users need to understand why a version was selected or rejected");
        contract.EvaluatedVersions.Should().NotBeEmpty();
    contract.EvaluatedVersions.Count.Should().BeLessThanOrEqualTo(20, "Compatibility evaluation should remain bounded for performance");
    }
}
