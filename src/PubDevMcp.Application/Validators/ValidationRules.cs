using System;
using System.Text.RegularExpressions;
using NuGet.Versioning;

namespace PubDevMcp.Application.Validators;

internal static partial class ValidationRules
{
    internal const int SearchQueryMaxLength = 80;
    private static readonly Regex PackageNameRegex = PackageNameRegexFactory();
    private static readonly Regex PublisherIdRegex = PublisherIdRegexFactory();

    internal static bool IsValidPackageName(string value)
        => PackageNameRegex.IsMatch(value);

    internal static bool IsValidPublisherId(string value)
        => PublisherIdRegex.IsMatch(value);

    internal static bool IsValidSemanticVersion(string value)
        => NuGetVersion.TryParse(value, out _);

    internal static bool IsValidVersionRange(string value)
        => VersionRange.TryParse(value, out _);

    internal static string Normalize(string value)
        => value.Trim();

    internal static bool HasValue(string? value)
        => !string.IsNullOrWhiteSpace(value);

    internal static string? NormalizeOptional(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    internal static void EnsureValidPackageName(string value, string parameterName)
    {
        if (!IsValidPackageName(value))
        {
            throw new ArgumentException($"Value '{value}' is not a valid pub.dev package name.", parameterName);
        }
    }

    [GeneratedRegex("^[a-z0-9_]+$", RegexOptions.CultureInvariant | RegexOptions.NonBacktracking)]
    private static partial Regex PackageNameRegexFactory();

    [GeneratedRegex("^[a-z0-9._-]+$", RegexOptions.CultureInvariant | RegexOptions.NonBacktracking)]
    private static partial Regex PublisherIdRegexFactory();
}
