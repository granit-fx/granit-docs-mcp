using System.ComponentModel;
using Granit.Tools.Mcp.Models;
using Granit.Tools.Mcp.Services;
using ModelContextProtocol.Server;

namespace Granit.Tools.Mcp.Tools;

[McpServerToolType]
public static class SearchCodeTool
{
    [McpServerTool(Name = "code_search")]
    [Description(
        "Search across Granit source code symbols (types, methods, interfaces, enums). " +
        "Returns ranked matches with name, kind, project, file path, and signature. " +
        "Searches the configured code indexes — the built-in .NET (granit-dotnet) and " +
        "TypeScript (granit-front) repos plus any extra repos defined in repos.json.")]
    public static async Task<string> ExecuteAsync(
        CodeIndexClient client,
        [Description("Search query — type name, method name, or keywords")]
        string query,
        [Description("Restrict to a configured repo id (e.g. \"dotnet\", \"front\", or a repos.json id). Omit to search all.")]
        string? repo = null,
        [Description("Filter by symbol kind: class, interface, method, enum, record, function, type")]
        string? kind = null,
        [Description("Maximum results (default 10, max 20)")]
        int limit = 10,
        [Description("Git branch for the code index. Defaults to detected branch or develop.")]
        string? branch = null,
        CancellationToken ct = default)
    {
        limit = Math.Clamp(limit, 1, 20);
        string[] terms = Tokenize(query);
        if (terms.Length == 0)
        {
            return "Query too short.";
        }

        var results = new List<ScoredResult>();

        foreach (LoadedIndex loaded in await client.GetIndexesAsync(repo, branch, ct))
        {
            if (loaded.Dotnet is { } codeIndex)
            {
                results.AddRange(SearchDotnet(codeIndex, terms, kind, loaded.Repo.Id));
            }
            else if (loaded.Front is { } frontIndex)
            {
                results.AddRange(SearchFront(frontIndex, terms, kind, loaded.Repo.Id));
            }
        }

        if (results.Count == 0)
        {
            string hint = repo is not null ? $" in repo \"{repo}\"" : "";
            return $"No code results found for \"{query}\"{hint}.";
        }

        var top = results
            .OrderByDescending(r => r.Score)
            .Take(limit)
            .ToList();

        IEnumerable<string> formatted = top.Select((r, i) =>
        {
            var lines = new List<string>
            {
                $"### {i + 1}. {r.Name}",
                $"**Kind:** {r.Kind} · **Repo:** {r.Repo} · **Project:** {r.Project}",
            };
            if (r.Fqn is not null)
            {
                lines.Add($"**FQN:** {r.Fqn}");
            }

            if (r.File is not null)
            {
                lines.Add($"**File:** {r.File}");
            }

            if (r.Signature is not null)
            {
                lines.Add($"**Signature:** `{r.Signature}`");
            }

            return string.Join('\n', lines);
        });

        return $"## Code search for \"{query}\" ({top.Count} found)\n\n" +
               string.Join("\n\n---\n\n", formatted);
    }

    private static List<ScoredResult> SearchDotnet(
        CodeIndex index, string[] terms, string? kindFilter, string repoId)
    {
        var results = new List<ScoredResult>();

        foreach (CodeSymbol sym in index.Symbols)
        {
            if (kindFilter is not null && sym.Kind != kindFilter)
            {
                continue;
            }

            int score = ScoreSymbol(
                sym.Name, sym.Fqn,
                sym.Members.Select(m => m.Name).ToArray(), terms);
            if (score > 0)
            {
                results.Add(new ScoredResult(
                    sym.Name, sym.Fqn, sym.Kind, sym.Project,
                    sym.File, null, repoId, score));
            }

            foreach (CodeMember member in sym.Members)
            {
                if (kindFilter is not null && member.Kind != kindFilter)
                {
                    continue;
                }

                int memberScore = ScoreMember(
                    member.Name, sym.Name, member.Signature, terms);
                if (memberScore > 0)
                {
                    results.Add(new ScoredResult(
                        $"{sym.Name}.{member.Name}",
                        $"{sym.Fqn}.{member.Name}",
                        member.Kind, sym.Project,
                        sym.File, member.Signature,
                        repoId, memberScore));
                }
            }
        }

        return results;
    }

    private static List<ScoredResult> SearchFront(
        FrontIndex index, string[] terms, string? kindFilter, string repoId)
    {
        var results = new List<ScoredResult>();

        foreach (FrontPackage pkg in index.Packages)
        {
            foreach (FrontExport exp in pkg.Exports)
            {
                if (kindFilter is not null && exp.Kind != kindFilter)
                {
                    continue;
                }

                int score = ScoreExport(
                    exp.Name, pkg.Name, exp.Signature, terms);
                if (score > 0)
                {
                    results.Add(new ScoredResult(
                        exp.Name, $"{pkg.Name}/{exp.Name}",
                        exp.Kind, pkg.Name,
                        null, exp.Signature,
                        repoId, score));
                }
            }
        }

        return results;
    }

    private static string[] Tokenize(string query) =>
        query.ToLowerInvariant()
            .Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Where(t => t.Length >= 2)
            .ToArray();

    private static int CountHits(string text, string[] terms)
    {
        string lower = text.ToLowerInvariant();
        int count = 0;
        foreach (string term in terms)
        {
            int idx = 0;
            while ((idx = lower.IndexOf(term, idx, StringComparison.Ordinal)) != -1)
            {
                count++;
                idx += term.Length;
            }
        }
        return count;
    }

    private static int ScoreSymbol(
        string name, string fqn, string[] memberNames, string[] terms) =>
        CountHits(name, terms) * 5
        + CountHits(fqn, terms) * 3
        + CountHits(string.Join(' ', memberNames), terms);

    private static int ScoreMember(
        string name, string parentName, string signature, string[] terms) =>
        CountHits(name, terms) * 5
        + CountHits(parentName, terms) * 2
        + CountHits(signature, terms);

    private static int ScoreExport(
        string name, string packageName, string signature, string[] terms) =>
        CountHits(name, terms) * 5
        + CountHits(packageName, terms) * 2
        + CountHits(signature, terms);

    private sealed record ScoredResult(
        string Name, string? Fqn, string Kind, string Project,
        string? File, string? Signature, string Repo, int Score);
}
