using System;
using FluentAssertions;

namespace PubDevMcp.Tests.Contract;

public sealed class LatestVersionContractTests
{
    [Fact(DisplayName = "LatestVersion MUST exclude prerelease packages by default")]
    public async Task LatestVersionMustReturnStableReleaseAsync()
    {
        var contract = await ContractTestHarness.ExecuteLatestVersionContractAsync("http");

        contract.Latest.IsPrerelease.Should().BeFalse("FR-002 requires returning the newest stable release");
    }

    [Fact(DisplayName = "LatestVersion MUST include release date and release notes link")]
    public async Task LatestVersionMustIncludeReleaseMetadataAsync()
    {
        var contract = await ContractTestHarness.ExecuteLatestVersionContractAsync("http");

        contract.Latest.Released.Should().BeAfter(DateTimeOffset.UnixEpoch);
        contract.Latest.ReleaseNotesUrl.Should().NotBeNull("Latest version contract should surface release notes link when available");
    }
}
