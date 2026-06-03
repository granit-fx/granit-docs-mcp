using System.ComponentModel;
using Granit.Tools.Mcp.Services;
using ModelContextProtocol.Server;

namespace Granit.Tools.Mcp.Tools;

[McpServerToolType]
public static class GetProjectGraphTool
{
    [McpServerTool(Name = "code_get_graph")]
    [Description(
        "Shows the project/package dependency graph for the Granit framework. " +
        "Lists .NET projects and/or TypeScript packages per configured repo.")]
    public static async Task<string> ExecuteAsync(
        CodeIndexClient client,
        [Description("Restrict to a configured repo id (e.g. \"dotnet\", \"front\", or a repos.json id). Omit to show all.")]
        string? repo = null,
        [Description("Git branch for the code index. Defaults to detected branch or develop.")]
        string? branch = null,
        CancellationToken ct = default)
    {
        var sections = new List<string>();

        foreach (LoadedIndex loaded in await client.GetIndexesAsync(repo, branch, ct))
        {
            if (loaded.Dotnet is { ProjectGraph.Count: > 0 } codeIndex)
            {
                var sorted = codeIndex.ProjectGraph
                    .OrderBy(p => p.Name)
                    .ToList();
                IEnumerable<string> lines = sorted.Select(p =>
                {
                    string deps = p.Deps.Count > 0
                        ? $"→ {string.Join(", ", p.Deps)}"
                        : "*(no dependencies)*";
                    return $"- **{p.Name}** ({p.Framework}) {deps}";
                });

                sections.Add(
                    $"### {loaded.Repo.Id} (.NET) — {sorted.Count} projects\n\n" +
                    string.Join('\n', lines));
            }
            else if (loaded.Front is { Packages.Count: > 0 } frontIndex)
            {
                var sorted = frontIndex.Packages
                    .OrderBy(p => p.Name)
                    .ToList();
                IEnumerable<string> lines = sorted.Select(p =>
                {
                    string desc = !string.IsNullOrEmpty(p.Description)
                        ? $" — {p.Description}" : "";
                    return $"- **{p.Name}**{desc}";
                });

                sections.Add(
                    $"### {loaded.Repo.Id} (TypeScript) — {sorted.Count} packages\n\n" +
                    string.Join('\n', lines));
            }
        }

        return sections.Count > 0
            ? $"## Granit project graph\n\n{string.Join("\n\n", sections)}"
            : "No project graph data available. " +
              "Code indexes may not be published yet.";
    }
}
