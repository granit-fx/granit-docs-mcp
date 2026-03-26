# ADR-003: Local .NET Tool with SQLite FTS5

> **Date:** 2026-03-23
> **Authors:** Jean-Francois Meyers
> **Status:** Proposed
> **Supersedes:** ADR-001, ADR-002

## Context

ADR-001 (2026-03-18) chose a pre-built JSON `search-index.json` served via a
Cloudflare Worker for documentation search. ADR-002 (2026-03-19) extended the
Worker with code navigation and NuGet tools, growing the tool count from 3
to 9.

Five weeks of production use have revealed structural weaknesses in the
current architecture that justify a re-evaluation.

### Problem 1: search-index.json is manually maintained

The `generate-search-index.mjs` script in `granit-dotnet/docs-site/scripts/`
must be run manually after `pnpm astro build`. It is **not** wired to a
pre-commit hook or CI step. Meanwhile, the docs site already generates
`llms-full.txt` automatically via the `starlight-llms-txt` plugin on every
build. The manual index is a stale-data risk that duplicates work the docs
pipeline already performs.

### Problem 2: TF-IDF quality ceiling

The current search implementation (~120 lines) uses basic substring matching
with length normalization. It has no stemming, no boolean operators, no phrase
matching, and no prefix search. Queries like "how to configure blob storage
retry" return suboptimal results because "configure" does not match
"configuration" and multi-word proximity is not scored.

### Problem 3: token-inefficient tool responses

`get_module_reference` returns the **entire** module content inline (up to
20 KB per call). `search_granit_docs` returns excerpts but includes full
metadata. There is no search/fetch separation: the LLM pays the token cost
for content it may not need. The Duende MCP implementation demonstrates a
proven pattern: search returns lightweight results (ID + title + snippet),
a separate `fetch` tool returns full content on demand.

### Problem 4: two repos, three index pipelines

Maintaining the Worker (`granit-mcp`) separately from three index generators
(`search-index.json` in granit-dotnet, `.mcp-code-index.json` via pre-commit,
`.mcp-front-index.json` via pre-commit) creates coordination overhead. A
schema change in the code index requires synchronized deploys across repos.

### Problem 5: no offline capability

The Cloudflare Worker requires internet access. Developers on trains, planes,
or behind restrictive proxies lose all MCP capabilities.

### Reference: Duende IdentityServer MCP

Duende Software released an open-source MCP server for their IdentityServer
documentation (MIT license, .NET 10). Key design decisions:

- **Local .NET tool** distributed via NuGet (`dotnet tool install`)
- **Dual transport**: stdio (for Claude Code/Cursor) + HTTP (for web clients)
- **SQLite FTS5** for full-text search with stemming, phrase queries, and
  prefix matching
- **llms.txt parsing**: fetches `llms-full.txt`, splits by H1 headings,
  stores each section as a searchable document in FTS5
- **Search/fetch pattern**: `search_docs` returns lightweight results
  (ID + title + snippet), `get_doc` returns full content by ID
- **Self-indexing at startup** with configurable periodic refresh

Source: <https://github.com/DuendeSoftware/products/tree/main/docs-mcp>

### Constraints

- Target audience: .NET 10 developers — .NET runtime is guaranteed
- "Zero setup" is NOT a requirement (developers already install dotnet tools)
- Branch-aware code indexes: MUST preserve
- Multi-repo support (granit-dotnet + granit-front): MUST preserve

## Decision

**Migrate to a local .NET 10 dotnet tool** that consolidates all MCP
capabilities into a single self-contained binary.

The tool will be distributed as `Granit.Tools.Mcp` on NuGet and configured as a
stdio MCP server in Claude Code / Cursor settings.

### Architecture

```text
Claude Code ──stdio──> Granit.Tools.Mcp (local .NET 10 tool)
                         |-- Docs tools ------> SQLite FTS5 (file-backed)
                         |                        ^ indexed from llms-full.txt
                         |-- Code tools ------> JSON indexes (GitHub raw, branch-aware)
                         |-- NuGet tools -----> api.nuget.org
                         +-- Branch tools ----> api.github.com
```

### Data sources

| Data | Source | Freshness |
| ---- | ------ | --------- |
| Docs | `granit-fx.dev/llms-full.txt` | Auto on build |
| .NET code | GitHub raw `{branch}` index | Pre-commit |
| Front code | GitHub raw `{branch}` index | Pre-commit |
| NuGet | `api.nuget.org` | Real-time |
| Branches | `api.github.com` | Real-time |

### Documentation indexing strategy

1. On startup, fetch `llms-full.txt` from CF Pages
2. Parse with Markdig — split by H1 headings (~30-40 articles)
3. Insert into SQLite FTS5 virtual table
   (`tokenize='unicode61'` for accent-safe matching):
   `id`, `title`, `content`, `category`
4. Periodic re-fetch every 4 hours (configurable)
5. Gracefully degrade if offline: use last cached database

### Automatic branch detection

Running locally, the tool can read `.git/HEAD` from the current
working directory to detect the active branch. Code index tools
(`code_search`, `code_get_api`, `code_get_graph`) use this
branch by default, eliminating the need to pass `branch` manually.
Explicit `branch` parameter still overrides when provided.

### Logging

stdio is reserved for the MCP protocol. All diagnostic output
goes to `ILogger` configured on **stderr** (default) or to a
file (`~/.granit-mcp/logs/`).

### Tool redesign

| Current | New | Change |
| ------- | --- | ------ |
| `search_granit_docs` | `docs_search` | FTS5. Lightweight results. |
| `get_module_reference` | `docs_get` | Generic fetch by ID. |
| `list_patterns` | `docs_list_patterns` | FTS5 category filter. |
| `search_code` | `code_search` | Unchanged (JSON index). |
| `get_public_api` | `code_get_api` | Unchanged. |
| `get_project_graph` | `code_get_graph` | Unchanged. |
| `list_branches` | `code_list_branches` | Unchanged. |
| `list_packages` | `nuget_list` | Unchanged. |
| `get_package_info` | `nuget_get` | Unchanged. |

Total: 9 tools. The key change is splitting search from fetch for
documentation, reducing average token consumption by ~60-80% for docs queries.

### Transport

- **Primary**: stdio (for Claude Code `mcpServers` config and Cursor/Rider)
- No remote deployment — the tool runs on the developer's machine

### Distribution

```bash
dotnet tool install --global Granit.Tools.Mcp
```

Claude Code `~/.claude.json` configuration:

```json
{
  "mcpServers": {
    "granit": {
      "command": "granit-tools-mcp",
      "args": []
    }
  }
}
```

### Caching

- SQLite database stored in `~/.granit-mcp/cache.db`
- FTS5 table for docs
- Regular tables for code indexes (per branch) and NuGet data
- Code indexes cached per branch with configurable TTL (default: 12 hours)
- NuGet data cached with 12-hour TTL

### Tech stack

| Layer | Technology |
| ----- | ---------- |
| Runtime | .NET 10, ASP.NET Core |
| MCP SDK | `ModelContextProtocol` + `ModelContextProtocol.AspNetCore` |
| Database | SQLite with EF Core (`Microsoft.EntityFrameworkCore.Sqlite`) |
| Search | SQLite FTS5 (full-text search) |
| Markdown parsing | Markdig |
| Transport | stdio + HTTP (dual) |

## Alternatives considered

### Option A: Full local .NET tool (selected)

See Decision section above.

### Option B: Hybrid — local tool for docs, keep Worker for code/NuGet

Split responsibilities: local tool handles documentation (FTS5 on
`llms-full.txt`), Worker retains code navigation, NuGet, and branch tools.

- **Advantage**: minimal change to code/NuGet tools, Worker stays deployed
- **Disadvantage**: developers must configure TWO MCP servers, two codebases
  to maintain, two deployment pipelines, inconsistent caching strategies,
  partial offline support (docs offline, code/NuGet not)
- **Rejected because**: the coordination cost of two servers outweighs the
  migration savings. Code and NuGet tools are straightforward HTTP clients
  that port to .NET trivially.

### Option C: Keep Worker, replace search-index.json with llms-full.txt

Worker fetches `llms-full.txt` (2.9 MB) instead of `search-index.json`
(1.4 MB), parses markdown at runtime, runs TF-IDF on the parsed sections.

- **Advantage**: eliminates manual `generate-search-index.mjs` step, single
  codebase, no migration for existing users
- **Disadvantage**: 2.9 MB fetch on cold start, still TF-IDF with no stemming
  (Cloudflare Workers cannot run SQLite FTS5), still no offline capability,
  still token-inefficient responses, markdown parsing on every cold start
- **Rejected because**: fixes only Problem 1 (stale index) while leaving
  Problems 2-5 unresolved. The quality ceiling of TF-IDF in a stateless
  Worker is the fundamental limitation.

## Justification

<!-- markdownlint-disable MD013 -->

| Criterion | A: Local .NET | B: Hybrid | C: Worker | Current |
| --------- | ------------- | --------- | --------- | ------- |
| Search | Excellent (FTS5) | Partial | Limited | Limited |
| Tokens | High | Partial | Low | Low |
| Maintenance | 1 repo | 2 repos | 1 repo | 2 repos |
| Offline | Full | Docs only | None | None |
| Freshness | Auto | Auto + hook | Auto | Manual |
| Cold start | ~2 s | ~200 ms | ~500 ms | ~100 ms |
| Distribution | dotnet tool | Tool + Worker | wrangler | wrangler |
| Config | 1 server | 2 servers | 1 server | 1 server |
| Cost | Free | Free | Free | Free |

<!-- markdownlint-enable MD013 -->

## Consequences

### Positive

- FTS5 provides stemming, phrase queries, prefix matching, and ranked results
  out of the box — replacing ~120 lines of custom TF-IDF
- Search/fetch separation reduces average token consumption by ~60-80% for
  documentation queries
- Single repository and single binary eliminates cross-repo coordination
- `llms-full.txt` is already auto-generated — no manual index step needed
- Offline-capable: developers can search docs and cached code indexes without
  internet
- .NET 10 dotnet tool distribution is familiar to the target audience
- SQLite database enables future enhancements (query history, usage analytics)

### Negative

- **Cold start overhead**: ~2 seconds for SQLite initialization and FTS5
  indexing on first query (mitigated by persistent database file)
- **No shared caching**: local cache only, first-run experience is slower

## Implementation notes

The `roslyn-lens` MCP server (`~/dev/jfmeyers/roslyn-lens`) already
implements the same architectural pattern in .NET 10. The following
patterns should be reused directly:

### Bootstrap (from `RoslynLens/Program.cs`)

```csharp
var builder = Host.CreateApplicationBuilder(args);

builder.Logging.ClearProviders();
builder.Logging.AddConsole(options =>
    options.LogToStandardErrorThreshold = LogLevel.Trace);

builder.Services.AddSingleton<GranitMcpConfig>();
builder.Services.AddSingleton<DocsIndexStore>();
builder.Services.AddHttpClient();
builder.Services.AddHostedService<DocsIndexer>();

builder.Services
    .AddMcpServer()
    .WithStdioServerTransport()
    .WithToolsFromAssembly();

await builder.Build().RunAsync();
```

### Tool registration (attribute-driven)

```csharp
[McpServerToolType]
public static class SearchDocsTool
{
    [McpServerTool(Name = "docs_search")]
    [Description("FTS5 search across Granit docs.")]
    public static async Task<string> ExecuteAsync(
        DocsIndexStore store,
        [Description("Search query")] string query,
        [Description("Max results")] int limit = 6,
        CancellationToken ct = default)
    {
        var status = store.EnsureReadyOrStatus();
        if (status is not null) return status;
        // ...
    }
}
```

### Background indexing (from `WorkspaceInitializer`)

Use `BackgroundService` to fetch and index `llms-full.txt` at
startup, then re-fetch periodically:

```csharp
public sealed class DocsIndexer(
    DocsIndexStore store,
    IHttpClientFactory http,
    ILogger<DocsIndexer> logger)
    : BackgroundService
{
    protected override async Task ExecuteAsync(
        CancellationToken ct)
    {
        await store.IndexAsync(http, ct);
        using var timer = new PeriodicTimer(
            TimeSpan.FromHours(4));
        while (await timer.WaitForNextTickAsync(ct))
            await store.IndexAsync(http, ct);
    }
}
```

### Graceful degradation (from `WorkspaceManager`)

Tools check readiness before executing. During initial indexing,
they return a JSON status instead of failing:

```json
{ "state": "Indexing", "message": "Building FTS5 index..." }
```

### Configuration (from `NavigatorConfig`)

Environment variables with `GRANIT_MCP_` prefix:

| Variable | Default | Description |
| -------- | ------- | ----------- |
| `GRANIT_MCP_LOG_LEVEL` | Information | Log level |
| `GRANIT_MCP_REFRESH_HOURS` | 4 | Docs re-index interval |
| `GRANIT_MCP_DATA_DIR` | `~/.granit-mcp` | SQLite + logs |

### What is new (not in roslyn-lens)

- **SQLite FTS5** — roslyn-lens uses in-memory Roslyn only.
  granit-mcp needs EF Core + SQLite for persistent doc index.
- **IHttpClientFactory** — roslyn-lens works on local files.
  granit-mcp fetches `llms-full.txt`, code indexes, and NuGet
  API over HTTP.
- **Persistent cache** — SQLite tables for code indexes and
  NuGet data, avoiding re-fetch on every restart.

## Re-evaluation conditions

This decision should be re-evaluated if:

- Cloudflare Workers gain SQLite/D1 with FTS5 support on the free tier
- The target audience expands beyond .NET developers
- MCP specification adds a standard search/resources primitive
- Documentation corpus exceeds 10 MB (may need chunking strategy)
- A managed MCP hosting service emerges with FTS5-quality search

## References

- ADR-001: JSON Index + Cloudflare Workers (2026-03-18)
- ADR-002: Granit MCP — Code Navigation & NuGet Packages (2026-03-19)
- Duende MCP: <https://github.com/DuendeSoftware/products/tree/main/docs-mcp>
- SQLite FTS5: <https://www.sqlite.org/fts5.html>
- MCP specification: <https://modelcontextprotocol.io/>
- llms.txt standard: <https://llmstxt.org/>
