using FluentAssertions;

namespace PubDevMcp.Tests.Contract;

public sealed class PublisherPackagesContractTests
{
    [Fact(DisplayName = "PublisherPackages MUST return packages owned by the publisher")]
    public async Task PublisherPackagesMustBePublisherScopedAsync()
    {
        var contract = await ContractTestHarness.ExecutePublisherPackagesContractAsync("dart-lang");

        contract.Packages.Should().NotBeEmpty();
        contract.Packages.Should().OnlyContain(p => p.Publisher == contract.Publisher);
    }

    [Fact(DisplayName = "PublisherPackages MUST respect the 10 result ceiling")]
    public async Task PublisherPackagesMustRespectResultLimitAsync()
    {
        var contract = await ContractTestHarness.ExecutePublisherPackagesContractAsync("dart-lang");

    contract.Packages.Count.Should().BeLessThanOrEqualTo(10, "Publisher listing must follow search pagination constraints");
    }
}
