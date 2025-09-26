using FluentAssertions;

namespace PubDevMcp.Tests.Contract;

public sealed class PackageDetailsContractTests
{
    [Fact(DisplayName = "PackageDetails MUST include core metadata fields")]
    public async Task PackageDetailsMustIncludeCoreMetadataAsync()
    {
        var contract = await ContractTestHarness.ExecutePackageDetailsContractAsync("http");

        contract.Description.Should().NotBeNullOrWhiteSpace();
        contract.Publisher.Should().NotBeNullOrWhiteSpace();
        contract.LatestStable.Should().NotBeNull();
    }

    [Fact(DisplayName = "PackageDetails MUST expose repository and issue tracker URLs")]
    public async Task PackageDetailsMustExposeReferenceLinksAsync()
    {
        var contract = await ContractTestHarness.ExecutePackageDetailsContractAsync("http");

        contract.RepositoryUrl.Should().StartWith("https://");
        contract.IssueTrackerUrl.Should().StartWith("https://");
        contract.Topics.Should().NotBeEmpty();
    }
}
