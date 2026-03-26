using System.ComponentModel;
using Granit.Tools.Mcp.Services;
using ModelContextProtocol.Server;

namespace Granit.Tools.Mcp.Tools;

[McpServerToolType]
public static class GetDocTool
{
    [McpServerTool(Name = "docs_get")]
    [Description(
        "Retrieves the full content of a documentation article by ID. " +
        "Use docs_search first to find the article ID.")]
    public static string Execute(
        DocsStore store,
        [Description("Article ID returned by docs_search (e.g. \"doc-3\")")]
        string id)
    {
        string? status = store.EnsureReadyOrStatus();
        if (status is not null)
        {
            return status;
        }

        DocArticle? article = store.GetById(id);
        if (article is null)
        {
            return $"Article \"{id}\" not found. " +
                   "Use docs_search to find valid IDs.";
        }

        return $"# {article.Title}\n\n" +
               $"**Category:** {article.Category}\n\n" +
               article.Content;
    }
}
