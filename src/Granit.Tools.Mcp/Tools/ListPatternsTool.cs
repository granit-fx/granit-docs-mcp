using System.ComponentModel;
using Granit.Tools.Mcp.Services;
using ModelContextProtocol.Server;

namespace Granit.Tools.Mcp.Tools;

[McpServerToolType]
public static class ListPatternsTool
{
    [McpServerTool(Name = "docs_list_patterns")]
    [Description(
        "Lists all architecture patterns documented in the Granit " +
        "framework. Use docs_search or docs_get to read pattern details.")]
    public static string Execute(DocsStore store)
    {
        string? status = store.EnsureReadyOrStatus();
        if (status is not null)
        {
            return status;
        }

        List<DocSearchResult> patterns = store.ListByCategory("pattern");

        if (patterns.Count == 0)
        {
            return "No patterns found in the documentation index.";
        }

        IEnumerable<string> lines = patterns.Select(p =>
            $"- **{p.Title}** (`{p.Id}`)");

        return $"## Granit architecture patterns " +
               $"({patterns.Count})\n\n" +
               string.Join('\n', lines) +
               "\n\nUse `docs_get` with the ID to read full pattern content.";
    }
}
