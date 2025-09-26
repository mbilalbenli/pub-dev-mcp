using System.Linq;
using FluentAssertions;

namespace PubDevMcp.Tests.Contract;

public sealed class ListVersionsContractTests
{
    [Fact(DisplayName = "ListVersions MUST return versions ordered newest to oldest")]
    public async Task ListVersionsMustReturnDescendingOrderAsync()
    {
        var contract = await ContractTestHarness.ExecuteListVersionsContractAsync("http", includePrerelease: false);

        var versions = contract.Versions.Select(v => v.Version).ToList();
        versions.Should().BeInDescendingOrder("Version history should be presented from newest to oldest");
    }

    [Fact(DisplayName = "ListVersions MUST include prereleases when requested")]
    public async Task ListVersionsMustSurfacePrereleasesWhenRequestedAsync()
    {
        var contract = await ContractTestHarness.ExecuteListVersionsContractAsync("http", includePrerelease: true);

        contract.Versions.Should().Contain(v => v.IsPrerelease, "Explicit prerelease requests must return prerelease entries");
    }
}
