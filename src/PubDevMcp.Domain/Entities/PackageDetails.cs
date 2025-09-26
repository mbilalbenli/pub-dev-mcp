using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace PubDevMcp.Domain.Entities;

public sealed record PackageDetails
{
    private PackageDetails(
        string package,
        string description,
        string publisher,
        Uri? homepageUrl,
        Uri? repositoryUrl,
        Uri? issueTrackerUrl,
        VersionDetail latestStable,
        IReadOnlyList<string> topics)
    {
        Package = package;
        Description = description;
        Publisher = publisher;
        HomepageUrl = homepageUrl;
        RepositoryUrl = repositoryUrl;
        IssueTrackerUrl = issueTrackerUrl;
        LatestStable = latestStable;
        Topics = topics;
    }

    public string Package { get; }

    public string Description { get; }

    public string Publisher { get; }

    public Uri? HomepageUrl { get; }

    public Uri? RepositoryUrl { get; }

    public Uri? IssueTrackerUrl { get; }

    public VersionDetail LatestStable { get; }

    public IReadOnlyList<string> Topics { get; }

    public static PackageDetails Create(
        string package,
        string description,
        string publisher,
        Uri? homepageUrl,
        Uri? repositoryUrl,
        Uri? issueTrackerUrl,
        VersionDetail latestStable,
        IEnumerable<string> topics)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(package);
        ArgumentException.ThrowIfNullOrWhiteSpace(description);
        ArgumentException.ThrowIfNullOrWhiteSpace(publisher);
        ArgumentNullException.ThrowIfNull(latestStable);
        ArgumentNullException.ThrowIfNull(topics);

        ValidateAbsolute(homepageUrl, nameof(homepageUrl));
        ValidateAbsolute(repositoryUrl, nameof(repositoryUrl));
        ValidateAbsolute(issueTrackerUrl, nameof(issueTrackerUrl));

        var normalizedTopics = topics
            .Select(topic => topic?.Trim())
            .Where(static topic => !string.IsNullOrWhiteSpace(topic))
            .Cast<string>()
            .ToArray();

        return new PackageDetails(
            package.Trim(),
            description.Trim(),
            publisher.Trim(),
            homepageUrl,
            repositoryUrl,
            issueTrackerUrl,
            latestStable,
            new ReadOnlyCollection<string>(normalizedTopics));
    }

    private static void ValidateAbsolute(Uri? uri, string parameterName)
    {
        if (uri is not null && !uri.IsAbsoluteUri)
        {
            throw new ArgumentException("Uri must be absolute", parameterName);
        }
    }
}
