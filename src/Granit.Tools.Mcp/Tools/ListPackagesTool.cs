using System.ComponentModel;
using Granit.Tools.Mcp.Services;
using ModelContextProtocol.Server;

namespace Granit.Tools.Mcp.Tools;

[McpServerToolType]
public static class ListPackagesTool
{
    [McpServerTool(Name = "nuget_list")]
    [Description(
        "Lists all published Granit NuGet packages with their latest version, " +
        "description, and download count. When GRANIT_MCP_GITHUB_TOKEN is set, " +
        "also includes pre-release packages from GitHub Packages.")]
    public static async Task<string> ExecuteAsync(
        NuGetClient nuget,
        CancellationToken ct = default)
    {
        List<PackageSummary> packages = await nuget.ListPackagesAsync(ct);

        if (packages.Count == 0)
        {
            return "No Granit packages found on NuGet.";
        }

        var sorted = packages.OrderBy(p => p.Id).ToList();
        IEnumerable<string> rows = sorted.Select(p =>
        {
            string dl = p.Downloads >= 1000
                ? $"{p.Downloads / 1000.0:F1}k"
                : p.Downloads.ToString(System.Globalization.CultureInfo.InvariantCulture);
            string desc = !string.IsNullOrEmpty(p.Description)
                ? p.Description : "No description";
            string source = p.Source != "nuget.org" ? $" [{p.Source}]" : "";
            return $"- **{p.Id}** v{p.Version} — {desc} ({dl} downloads){source}";
        });

        return $"## Granit NuGet packages ({sorted.Count})\n\n" +
               string.Join('\n', rows);
    }
}
