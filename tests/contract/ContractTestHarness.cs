using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Threading.Tasks;

namespace PubDevMcp.Tests.Contract;

internal static class ContractTestHarness
{
    public static Task<SearchPackagesContractResult> ExecuteSearchPackagesContractAsync(string query)
        => throw new NotImplementedException("Search packages contract harness not implemented");

    public static Task<LatestVersionContractResult> ExecuteLatestVersionContractAsync(string package)
        => throw new NotImplementedException("Latest version contract harness not implemented");

    public static Task<CompatibilityContractResult> ExecuteCompatibilityContractAsync(string package, string flutterSdk)
        => throw new NotImplementedException("Compatibility contract harness not implemented");

    public static Task<ListVersionsContractResult> ExecuteListVersionsContractAsync(string package, bool includePrerelease)
        => throw new NotImplementedException("List versions contract harness not implemented");

    public static Task<PackageDetailsContractResult> ExecutePackageDetailsContractAsync(string package)
        => throw new NotImplementedException("Package details contract harness not implemented");

    public static Task<PublisherPackagesContractResult> ExecutePublisherPackagesContractAsync(string publisher)
        => throw new NotImplementedException("Publisher packages contract harness not implemented");

    public static Task<ScoreInsightsContractResult> ExecuteScoreInsightsContractAsync(string package)
        => throw new NotImplementedException("Score insights contract harness not implemented");

    public static Task<DependencyInspectorContractResult> ExecuteDependencyInspectorContractAsync(string package, string? version)
        => throw new NotImplementedException("Dependency inspector contract harness not implemented");
}

[SuppressMessage("Performance", "CA1812:Avoid uninstantiated internal classes", Justification = "Contract harness will populate search result model during implementation")]
internal sealed record SearchPackagesContractResult(string Query, IReadOnlyList<PackageSummaryContractModel> Packages, string? MoreResultsHint);

[SuppressMessage("Performance", "CA1812:Avoid uninstantiated internal classes", Justification = "Contract harness will materialize package summary model during implementation")]
internal sealed record PackageSummaryContractModel(
    string Name,
    string Description,
    string Publisher,
    int Likes,
    int PubPoints,
    double Popularity,
    VersionDetailContractModel LatestStable
);

[SuppressMessage("Performance", "CA1812:Avoid uninstantiated internal classes", Justification = "Contract harness will materialize version detail model during implementation")]
internal sealed record VersionDetailContractModel(
    string Version,
    DateTimeOffset Released,
    string SdkConstraint,
    bool IsPrerelease,
    Uri? ReleaseNotesUrl
);

[SuppressMessage("Performance", "CA1812:Avoid uninstantiated internal classes", Justification = "Contract harness will provide latest version contract model during implementation")]
internal sealed record LatestVersionContractResult(string Package, VersionDetailContractModel Latest);

[SuppressMessage("Performance", "CA1812:Avoid uninstantiated internal classes", Justification = "Contract harness will provide compatibility contract model during implementation")]
internal sealed record CompatibilityContractResult(
    string Package,
    string RequestedFlutterSdk,
    VersionDetailContractModel? RecommendedVersion,
    bool Satisfies,
    string Explanation,
    IReadOnlyList<VersionDetailContractModel> EvaluatedVersions
);

[SuppressMessage("Performance", "CA1812:Avoid uninstantiated internal classes", Justification = "Contract harness will provide list versions contract model during implementation")]
internal sealed record ListVersionsContractResult(
    string Package,
    bool IncludePrerelease,
    IReadOnlyList<VersionDetailContractModel> Versions
);

[SuppressMessage("Performance", "CA1812:Avoid uninstantiated internal classes", Justification = "Contract harness will provide package details contract model during implementation")]
internal sealed record PackageDetailsContractResult(
    string Package,
    string Description,
    string Publisher,
    string HomepageUrl,
    string RepositoryUrl,
    string IssueTrackerUrl,
    VersionDetailContractModel LatestStable,
    IReadOnlyList<string> Topics
);

[SuppressMessage("Performance", "CA1812:Avoid uninstantiated internal classes", Justification = "Contract harness will provide publisher packages contract model during implementation")]
internal sealed record PublisherPackagesContractResult(
    string Publisher,
    IReadOnlyList<PackageSummaryContractModel> Packages
);

[SuppressMessage("Performance", "CA1812:Avoid uninstantiated internal classes", Justification = "Contract harness will provide score insights contract model during implementation")]
internal sealed record ScoreInsightsContractResult(
    string Package,
    double OverallScore,
    double Popularity,
    int Likes,
    int PubPoints,
    IReadOnlyDictionary<string, string> ComponentNotes
);

[SuppressMessage("Performance", "CA1812:Avoid uninstantiated internal classes", Justification = "Contract harness will provide dependency inspector contract model during implementation")]
internal sealed record DependencyInspectorContractResult(
    string Package,
    string RootVersion,
    IReadOnlyList<DependencyNodeContractModel> Nodes,
    IReadOnlyList<string> Issues
);

[SuppressMessage("Performance", "CA1812:Avoid uninstantiated internal classes", Justification = "Contract harness will provide dependency node contract model during implementation")]
internal sealed record DependencyNodeContractModel(
    string Package,
    string Requested,
    string Resolved,
    bool IsDirect,
    IReadOnlyList<DependencyNodeContractModel> Children
);
