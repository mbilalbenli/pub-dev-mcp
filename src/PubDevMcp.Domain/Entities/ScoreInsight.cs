using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace PubDevMcp.Domain.Entities;

public sealed record ScoreInsight
{
    private ScoreInsight(
        string package,
        double overallScore,
        double popularity,
        int likes,
        int pubPoints,
        IReadOnlyDictionary<string, string> componentNotes,
        DateTimeOffset fetchedAt)
    {
        Package = package;
        OverallScore = overallScore;
        Popularity = popularity;
        Likes = likes;
        PubPoints = pubPoints;
        ComponentNotes = componentNotes;
        FetchedAt = fetchedAt;
    }

    public string Package { get; }

    public double OverallScore { get; }

    public double Popularity { get; }

    public int Likes { get; }

    public int PubPoints { get; }

    public IReadOnlyDictionary<string, string> ComponentNotes { get; }

    public DateTimeOffset FetchedAt { get; }

    public static ScoreInsight Create(
        string package,
        double overallScore,
        double popularity,
        int likes,
        int pubPoints,
        IDictionary<string, string> componentNotes,
        DateTimeOffset fetchedAt)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(package);
        ArgumentNullException.ThrowIfNull(componentNotes);

        if (overallScore < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(overallScore), overallScore, "Overall score must be non-negative");
        }

        if (popularity is < 0 or > 1)
        {
            throw new ArgumentOutOfRangeException(nameof(popularity), popularity, "Popularity must be between 0 and 1 inclusive");
        }

        if (likes < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(likes), likes, "Likes cannot be negative");
        }

        if (pubPoints < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(pubPoints), pubPoints, "Pub points cannot be negative");
        }

        var normalizedNotes = componentNotes
            .Where(kvp => !string.IsNullOrWhiteSpace(kvp.Key))
            .ToDictionary(
                kvp => kvp.Key.Trim(),
                kvp => kvp.Value?.Trim() ?? string.Empty,
                StringComparer.OrdinalIgnoreCase);

        var readOnlyNotes = new ReadOnlyDictionary<string, string>(normalizedNotes);

        return new ScoreInsight(
            package.Trim(),
            overallScore,
            popularity,
            likes,
            pubPoints,
            readOnlyNotes,
            fetchedAt);
    }
}
