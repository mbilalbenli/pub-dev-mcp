using System;
using System.Collections.Generic;
using System.Linq;

namespace PubDevMcp.Domain.Entities;

public sealed record SearchResultSet
{
    private SearchResultSet(string query, IReadOnlyList<PackageSummary> packages, string? moreResultsHint)
    {
        Query = query;
        Packages = packages;
        MoreResultsHint = moreResultsHint;
    }

    public string Query { get; }

    public IReadOnlyList<PackageSummary> Packages { get; }

    public string? MoreResultsHint { get; }

    public static SearchResultSet Create(string query, IEnumerable<PackageSummary> packages, string? moreResultsHint)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(query);
        ArgumentNullException.ThrowIfNull(packages);

        var materialized = packages.Take(11).ToArray();
        if (materialized.Length > 10)
        {
            throw new ArgumentException("Search results cannot exceed 10 entries per FR-001", nameof(packages));
        }

        if (materialized.Length == 0)
        {
            throw new ArgumentException("At least one package must be present", nameof(packages));
        }

        return new SearchResultSet(
            query.Trim(),
            Array.AsReadOnly(materialized),
            string.IsNullOrWhiteSpace(moreResultsHint) ? null : moreResultsHint.Trim());
    }
}
