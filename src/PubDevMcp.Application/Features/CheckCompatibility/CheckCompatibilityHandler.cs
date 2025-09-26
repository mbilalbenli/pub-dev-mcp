using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using NuGet.Versioning;
using PubDevMcp.Application.Abstractions;
using PubDevMcp.Domain.Entities;

namespace PubDevMcp.Application.Features.CheckCompatibility;

public sealed record CheckCompatibilityQuery(string Package, string FlutterSdk, string? ProjectConstraint) : IRequest<CompatibilityResult>;

public sealed class CheckCompatibilityHandler : IRequestHandler<CheckCompatibilityQuery, CompatibilityResult>
{
    private readonly IPubDevApiClient _apiClient;

    public CheckCompatibilityHandler(IPubDevApiClient apiClient)
    {
        _apiClient = apiClient;
    }

    public async Task<CompatibilityResult> Handle(CheckCompatibilityQuery request, CancellationToken cancellationToken)
    {
        var compatibilityRequest = CompatibilityRequest.Create(request.Package, request.FlutterSdk, request.ProjectConstraint);
        var flutterSdkVersion = ParseVersion(compatibilityRequest.FlutterSdk);
        var projectConstraint = ParseProjectConstraint(compatibilityRequest.ProjectConstraint);

        var versions = await _apiClient.GetVersionHistoryAsync(compatibilityRequest.Package, cancellationToken).ConfigureAwait(false);
        var evaluated = versions.Take(20).ToArray();

        var recommended = evaluated
            .Where(version => SatisfiesSdkConstraint(version.SdkConstraint, flutterSdkVersion))
            .Where(version => SatisfiesProjectConstraint(version.Version, projectConstraint))
            .Where(version => !version.IsPrerelease)
            .OrderByDescending(version => version.Released)
            .ThenByDescending(version => ParseVersion(version.Version))
            .FirstOrDefault();

        var satisfies = recommended is not null;
        if (!satisfies)
        {
            var prereleaseFallback = evaluated
                .Where(version => SatisfiesSdkConstraint(version.SdkConstraint, flutterSdkVersion))
                .Where(version => SatisfiesProjectConstraint(version.Version, projectConstraint))
                .OrderByDescending(version => version.Released)
                .ThenByDescending(version => ParseVersion(version.Version))
                .FirstOrDefault();

            if (prereleaseFallback is not null)
            {
                recommended = prereleaseFallback;
                satisfies = true;
            }
        }

        var explanation = satisfies
            ? BuildSuccessExplanation(recommended!, compatibilityRequest, projectConstraint)
            : BuildFailureExplanation(compatibilityRequest, projectConstraint, evaluated.Length);

        return CompatibilityResult.Create(
            compatibilityRequest,
            recommended,
            satisfies,
            explanation,
            evaluated);
    }

    private static VersionRange? ParseProjectConstraint(string? constraint)
    {
        if (string.IsNullOrWhiteSpace(constraint) || constraint.Equals("any", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        if (VersionRange.TryParse(constraint.Trim(), out var parsed))
        {
            return parsed;
        }

        throw new InvalidOperationException($"Invalid project constraint '{constraint}'.");
    }

    private static NuGetVersion ParseVersion(string version)
    {
        if (!NuGetVersion.TryParse(version, out var parsed))
        {
            throw new InvalidOperationException($"Invalid semantic version '{version}'.");
        }

        return parsed;
    }

    private static bool SatisfiesSdkConstraint(string sdkConstraint, NuGetVersion flutterSdk)
    {
        if (string.IsNullOrWhiteSpace(sdkConstraint) || sdkConstraint.Equals("any", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        var clauses = sdkConstraint.Split("||", StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        foreach (var clause in clauses)
        {
            if (ClauseSatisfied(clause, flutterSdk))
            {
                return true;
            }
        }

        return false;
    }

    private static bool ClauseSatisfied(string clause, NuGetVersion flutterSdk)
    {
        var tokens = clause.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var comparisons = new List<Func<bool>>();

        foreach (var token in tokens)
        {
            if (token.Length > 0 && token[0] == '^')
            {
                var caretVersion = ParseVersion(token.Substring(1));
                var upperBound = caretVersion.Major > 0
                    ? new NuGetVersion(caretVersion.Major + 1, 0, 0)
                    : caretVersion.Minor > 0
                        ? new NuGetVersion(caretVersion.Major, caretVersion.Minor + 1, 0)
                        : new NuGetVersion(caretVersion.Major, caretVersion.Minor, caretVersion.Patch + 1);

                comparisons.Add(() => flutterSdk >= caretVersion && flutterSdk < upperBound);
            }
            else if (token.StartsWith(">=", StringComparison.Ordinal))
            {
                var lowerVersion = ParseVersion(token.Substring(2));
                comparisons.Add(() => flutterSdk >= lowerVersion);
            }
            else if (token.Length > 0 && token[0] == '>')
            {
                var lowerVersion = ParseVersion(token.Substring(1));
                comparisons.Add(() => flutterSdk > lowerVersion);
            }
            else if (token.StartsWith("<=", StringComparison.Ordinal))
            {
                var upperVersion = ParseVersion(token.Substring(2));
                comparisons.Add(() => flutterSdk <= upperVersion);
            }
            else if (token.Length > 0 && token[0] == '<')
            {
                var upperVersion = ParseVersion(token.Substring(1));
                comparisons.Add(() => flutterSdk < upperVersion);
            }
            else if (token.Length > 0 && token[0] == '=')
            {
                var targetVersion = ParseVersion(token.Substring(1));
                comparisons.Add(() => flutterSdk == targetVersion);
            }
            else
            {
                // Fallback: treat lone version as exact match
                var targetVersion = ParseVersion(token);
                comparisons.Add(() => flutterSdk == targetVersion);
            }
        }

        return comparisons.All(comparison => comparison());
    }

    private static bool SatisfiesProjectConstraint(string version, VersionRange? projectConstraint)
    {
        if (projectConstraint is null)
        {
            return true;
        }

        var parsed = ParseVersion(version);
        return projectConstraint.Satisfies(parsed);
    }

    private static string BuildSuccessExplanation(VersionDetail version, CompatibilityRequest request, VersionRange? projectConstraint)
    {
        var sdkPart = $"Flutter SDK constraint {request.FlutterSdk}";
        var projectPart = projectConstraint is null
            ? null
            : $"project constraint {request.ProjectConstraint}";

        var constraintSummary = projectPart is null ? sdkPart : $"{sdkPart} and {projectPart}";
        return $"Selected version {version.Version} satisfies {constraintSummary}.";
    }

    private static string BuildFailureExplanation(CompatibilityRequest request, VersionRange? projectConstraint, int evaluatedCount)
    {
        var sdkPart = $"Flutter SDK constraint {request.FlutterSdk}";
        var projectPart = projectConstraint is null
            ? null
            : $" and project constraint {request.ProjectConstraint}";

        return $"No available versions within the latest {evaluatedCount} releases satisfy {sdkPart}{projectPart}.";
    }
}
