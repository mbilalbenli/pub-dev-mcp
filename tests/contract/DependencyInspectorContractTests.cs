using System.Linq;
using FluentAssertions;

namespace PubDevMcp.Tests.Contract;

public sealed class DependencyInspectorContractTests
{
    [Fact(DisplayName = "DependencyInspector MUST provide full dependency tree")]
    public async Task DependencyInspectorMustProvideDependencyTreeAsync()
    {
        var contract = await ContractTestHarness.ExecuteDependencyInspectorContractAsync("http", version: null);

        contract.Nodes.Should().NotBeEmpty();
        contract.Nodes.Should().Contain(node => node.IsDirect);
    }

    [Fact(DisplayName = "DependencyInspector MUST surface constraint issues")]
    public async Task DependencyInspectorMustSurfaceConstraintIssuesAsync()
    {
        var contract = await ContractTestHarness.ExecuteDependencyInspectorContractAsync("http", version: null);

        contract.Issues.Should().NotBeNull();
    }
}
