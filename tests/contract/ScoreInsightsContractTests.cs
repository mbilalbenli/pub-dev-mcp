using FluentAssertions;

namespace PubDevMcp.Tests.Contract;

public sealed class ScoreInsightsContractTests
{
    [Fact(DisplayName = "ScoreInsights MUST provide component breakdown")]
    public async Task ScoreInsightsMustProvideComponentBreakdownAsync()
    {
        var contract = await ContractTestHarness.ExecuteScoreInsightsContractAsync("http");

        contract.ComponentNotes.Should().ContainKey("popularity");
        contract.ComponentNotes.Should().ContainKey("likes");
        contract.ComponentNotes.Should().ContainKey("pubPoints");
    }

    [Fact(DisplayName = "ScoreInsights MUST report overall and component scores")]
    public async Task ScoreInsightsMustReportScoresAsync()
    {
        var contract = await ContractTestHarness.ExecuteScoreInsightsContractAsync("http");

        contract.OverallScore.Should().BeGreaterThan(0);
        contract.Popularity.Should().BeGreaterThan(0);
        contract.PubPoints.Should().BeGreaterThan(0);
    }
}
