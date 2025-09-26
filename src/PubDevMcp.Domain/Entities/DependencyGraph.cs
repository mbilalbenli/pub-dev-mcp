using System;
using System.Collections.Generic;
using System.Linq;

namespace PubDevMcp.Domain.Entities;

public sealed record DependencyNode
{
    private DependencyNode(
        string package,
        string requested,
        string resolved,
        bool isDirect,
        IReadOnlyList<DependencyNode> children)
    {
        Package = package;
        Requested = requested;
        Resolved = resolved;
        IsDirect = isDirect;
        Children = children;
    }

    public string Package { get; }

    public string Requested { get; }

    public string Resolved { get; }

    public bool IsDirect { get; }

    public IReadOnlyList<DependencyNode> Children { get; }

    public static DependencyNode Create(
        string package,
        string requested,
        string resolved,
        bool isDirect,
        IEnumerable<DependencyNode> children)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(package);
        ArgumentException.ThrowIfNullOrWhiteSpace(requested);
        ArgumentException.ThrowIfNullOrWhiteSpace(resolved);
        ArgumentNullException.ThrowIfNull(children);

        var materialized = children.ToArray();
        return new DependencyNode(
            package.Trim(),
            requested.Trim(),
            resolved.Trim(),
            isDirect,
            Array.AsReadOnly(materialized));
    }
}

public sealed record DependencyGraph
{
    private DependencyGraph(
        string rootPackage,
        string rootVersion,
        IReadOnlyList<DependencyNode> nodes,
        IReadOnlyList<string> issues)
    {
        RootPackage = rootPackage;
        RootVersion = rootVersion;
        Nodes = nodes;
        Issues = issues;
    }

    public string RootPackage { get; }

    public string RootVersion { get; }

    public IReadOnlyList<DependencyNode> Nodes { get; }

    public IReadOnlyList<string> Issues { get; }

    public static DependencyGraph Create(
        string rootPackage,
        string rootVersion,
        IEnumerable<DependencyNode> nodes,
        IEnumerable<string> issues)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rootPackage);
        ArgumentException.ThrowIfNullOrWhiteSpace(rootVersion);
        ArgumentNullException.ThrowIfNull(nodes);
        ArgumentNullException.ThrowIfNull(issues);

        var nodeArray = nodes.ToArray();
        if (nodeArray.Length == 0)
        {
            throw new ArgumentException("Graph must contain at least one node", nameof(nodes));
        }

        var issueArray = issues
            .Select(issue => issue?.Trim())
            .Where(static issue => !string.IsNullOrWhiteSpace(issue))
            .Cast<string>()
            .ToArray();

        return new DependencyGraph(
            rootPackage.Trim(),
            rootVersion.Trim(),
            Array.AsReadOnly(nodeArray),
            Array.AsReadOnly(issueArray));
    }
}
