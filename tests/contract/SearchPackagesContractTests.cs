using FluentAssertions;

namespace PubDevMcp.Tests.Contract;

public sealed class SearchPackagesContractTests
{
    [Fact(DisplayName = "SearchPackages MUST cap results at 10 entries")]
    public async Task SearchPackagesMustEnforceTenResultLimitAsync()
    {
        var contract = await ContractTestHarness.ExecuteSearchPackagesContractAsync("http client");

        contract.Packages.Should().HaveCount(10, "FR-001 requires limiting search results to the top 10 entries");
    }

    [Fact(DisplayName = "SearchPackages MUST provide a moreResultsHint when truncating")]
    public async Task SearchPackagesMustProvideMoreResultsHintAsync()
    {
        var contract = await ContractTestHarness.ExecuteSearchPackagesContractAsync("http client");

        contract.MoreResultsHint.Should().NotBeNullOrWhiteSpace("Users need guidance when additional packages exist beyond the first page");
    }
}
