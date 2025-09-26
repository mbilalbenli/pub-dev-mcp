using System;

namespace PubDevMcp.Infrastructure.Options;

public sealed class PubDevApiOptions
{
    public Uri BaseAddress { get; set; } = new("https://pub.dev", UriKind.Absolute);

    public int SearchResultLimit { get; set; } = 10;

    public string UserAgent { get; set; } = "PubDev MCP Client/1.0";
}
