# ADR-002: Granit MCP — Code Navigation & NuGet Packages

> **Date:** 2026-03-19
> **Authors:** Jean-Francois Meyers
> **Status:** Accepted
> **Supersedes:** Extends ADR-001

## Context

ADR-001 established a documentation-only MCP server (`granit-docs-mcp`)
with TF-IDF search over a pre-built JSON index. AI assistants can search
docs, look up modules, and list patterns — but they cannot explore the
Granit source code or discover published NuGet packages.

Developers using Claude Code alongside the Granit framework need:

- **Code navigation** — find types, inspect public APIs, understand
  project dependencies without reading entire `.cs`/`.ts` files
- **Package discovery** — list published Granit NuGet packages, check
  versions, dependencies, and target frameworks
- **Complementarity with GitHub MCP Server** — the official
  `github/github-mcp-server` already provides `get_file_contents`,
  `get_repository_tree`, and `list_commits`; the Granit MCP should
  not duplicate these capabilities

## Decision

Evolve `granit-docs-mcp` into **`granit-mcp`** — a unified MCP server
covering documentation, code navigation, and NuGet packages.

### Architecture

The core principle from ADR-001 is preserved: **pre-built indexes
generated at CI time, fetched and cached by the CF Worker, searched
in-Worker**.

Three index sources:

- **search-index.json** — granit-dotnet/docs-site,
  `generate-search-index.mjs` (existing), hosted on CF Pages
- **code-index.json** — granit-dotnet,
  `generate-code-index.mjs` (RoslynLens via MCP stdio),
  hosted as GitHub Release Asset
- **front-index.json** — granit-front,
  `generate-front-index.mjs` (ts-morph),
  hosted as GitHub Release Asset

NuGet package data is fetched at runtime from the public NuGet API
(no token required) and cached in KV.

### New tools

- **search\_code** — TF-IDF search over symbols
  (code-index + front-index)
- **get\_public\_api** — full public API surface of a type
  (code-index + front-index)
- **get\_project\_graph** — project/package dependency graph
  (code-index + front-index)
- **list\_packages** — all published Granit.\* NuGet packages
  (NuGet Search API)
- **get\_package\_info** — versions, deps, frameworks, license
  (NuGet Registration API)

### RoslynLens integration

RoslynLens is used **at build time only** — the CI pipeline in
granit-dotnet installs it as a global dotnet tool, spawns it via
MCP stdio, and calls `get_project_graph`, `find_symbols_batch`,
and `get_public_api_batch` to generate a semantic code index. This
provides Roslyn-level analysis (namespaces, signatures, member
types) without requiring .NET at runtime.

### Cache strategy

Single KV namespace (`CACHE`) with prefixed keys:

- `docs:index` — 24 h TTL (CF Pages)
- `code:index` — 12 h TTL (GitHub Release Asset)
- `front:index` — 12 h TTL (GitHub Release Asset)
- `nuget:package-list` — 12 h TTL (NuGet Search API)
- `nuget:pkg:{id}` — 6 h TTL (NuGet Registration API)

## Alternatives considered

### GitHub API for code search at runtime

The GitHub Code Search API (`GET /search/code`) has a 10 req/min
rate limit, making it impractical for interactive MCP tools serving
multiple users. Pre-built indexes eliminate this bottleneck entirely.

### Duplicating GitHub MCP Server tools

Tools like `get_file_contents` and `get_repository_tree` are already
available via the official GitHub MCP Server. Duplicating them would
add complexity and a PAT secret dependency with no added value.

### Embedding RoslynLens in the Worker

RoslynLens requires .NET 10, MSBuild, and a full solution —
incompatible with Cloudflare Workers. Build-time index generation
is the practical alternative.

## Consequences

- The Worker name changes from `granit-docs-mcp` to `granit-mcp`
  (same domain)
- KV binding renamed from `DOCS_CACHE` to `CACHE`
- Two new CI pipelines needed (granit-dotnet code-index,
  granit-front front-index)
- NuGet tools work immediately with no external dependencies
- Total tool count grows from 3 to 8
