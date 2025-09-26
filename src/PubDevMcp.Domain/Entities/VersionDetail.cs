using System;

namespace PubDevMcp.Domain.Entities;

public sealed record VersionDetail
{
    private VersionDetail(
        string version,
        DateTimeOffset released,
        string sdkConstraint,
        bool isPrerelease,
        Uri? releaseNotesUrl)
    {
        Version = version;
        Released = released;
        SdkConstraint = sdkConstraint;
        IsPrerelease = isPrerelease;
        ReleaseNotesUrl = releaseNotesUrl;
    }

    public string Version { get; }

    public DateTimeOffset Released { get; }

    public string SdkConstraint { get; }

    public bool IsPrerelease { get; }

    public Uri? ReleaseNotesUrl { get; }

    public static VersionDetail Create(
        string version,
        DateTimeOffset released,
        string sdkConstraint,
        bool isPrerelease,
        Uri? releaseNotesUrl)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(version);
        ArgumentException.ThrowIfNullOrWhiteSpace(sdkConstraint);

        if (releaseNotesUrl is not null && !releaseNotesUrl.IsAbsoluteUri)
        {
            throw new ArgumentException("Release notes URL must be absolute", nameof(releaseNotesUrl));
        }

        return new VersionDetail(
            version.Trim(),
            released,
            sdkConstraint.Trim(),
            isPrerelease,
            releaseNotesUrl);
    }

    public bool MatchesVersion(string version)
        => string.Equals(Version, version, StringComparison.OrdinalIgnoreCase);
}
