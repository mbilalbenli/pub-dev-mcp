using System;
using System.Collections.Generic;
using System.Linq;

namespace PubDevMcp.Domain.Entities;

public sealed record CompatibilityRequest
{
    private CompatibilityRequest(string package, string flutterSdk, string? projectConstraint)
    {
        Package = package;
        FlutterSdk = flutterSdk;
        ProjectConstraint = projectConstraint;
    }

    public string Package { get; }

    public string FlutterSdk { get; }

    public string? ProjectConstraint { get; }

    public static CompatibilityRequest Create(string package, string flutterSdk, string? projectConstraint)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(package);
        ArgumentException.ThrowIfNullOrWhiteSpace(flutterSdk);

        return new CompatibilityRequest(
            package.Trim(),
            flutterSdk.Trim(),
            string.IsNullOrWhiteSpace(projectConstraint) ? null : projectConstraint.Trim());
    }
}

public sealed record CompatibilityResult
{
    private CompatibilityResult(
        CompatibilityRequest request,
        VersionDetail? recommendedVersion,
        bool satisfies,
        string explanation,
        IReadOnlyList<VersionDetail> evaluatedVersions)
    {
        Request = request;
        RecommendedVersion = recommendedVersion;
        Satisfies = satisfies;
        Explanation = explanation;
        EvaluatedVersions = evaluatedVersions;
    }

    public CompatibilityRequest Request { get; }

    public VersionDetail? RecommendedVersion { get; }

    public bool Satisfies { get; }

    public string Explanation { get; }

    public IReadOnlyList<VersionDetail> EvaluatedVersions { get; }

    public static CompatibilityResult Create(
        CompatibilityRequest request,
        VersionDetail? recommendedVersion,
        bool satisfies,
        string explanation,
        IEnumerable<VersionDetail> evaluatedVersions)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfNullOrWhiteSpace(explanation);
        ArgumentNullException.ThrowIfNull(evaluatedVersions);

        var materialized = evaluatedVersions.Take(50).ToArray();
        if (materialized.Length == 0)
        {
            throw new ArgumentException("At least one evaluated version must be supplied", nameof(evaluatedVersions));
        }

        return new CompatibilityResult(
            request,
            recommendedVersion,
            satisfies,
            explanation.Trim(),
            Array.AsReadOnly(materialized));
    }

    public static CompatibilityResult None(CompatibilityRequest request, IEnumerable<VersionDetail> evaluatedVersions, string explanation)
        => Create(request, null, false, explanation, evaluatedVersions);
}
