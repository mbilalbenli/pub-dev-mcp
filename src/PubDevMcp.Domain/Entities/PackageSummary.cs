using System;

namespace PubDevMcp.Domain.Entities;

public sealed record PackageSummary
{
    private PackageSummary(
        string name,
        string description,
        string publisher,
        int likes,
        int pubPoints,
        double popularity,
        VersionDetail? latestStable)
    {
        Name = name;
        Description = description;
        Publisher = publisher;
        Likes = likes;
        PubPoints = pubPoints;
        Popularity = popularity;
        LatestStable = latestStable;
    }

    public string Name { get; }

    public string Description { get; }

    public string Publisher { get; }

    public int Likes { get; }

    public int PubPoints { get; }

    public double Popularity { get; }

    public VersionDetail? LatestStable { get; }

    public static PackageSummary Create(
        string name,
        string description,
        string publisher,
        int likes,
        int pubPoints,
        double popularity,
        VersionDetail? latestStable)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(description);
        ArgumentException.ThrowIfNullOrWhiteSpace(publisher);

        if (likes < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(likes), likes, "Likes cannot be negative");
        }

        if (pubPoints < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(pubPoints), pubPoints, "Pub points cannot be negative");
        }

        if (popularity is < 0 or > 1)
        {
            throw new ArgumentOutOfRangeException(nameof(popularity), popularity, "Popularity must be between 0 and 1 inclusive");
        }

        return new PackageSummary(
            name.Trim(),
            description.Trim(),
            publisher.Trim(),
            likes,
            pubPoints,
            popularity,
            latestStable);
    }
}
