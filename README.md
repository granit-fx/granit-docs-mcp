# granit-mcp

[![GitHub](https://img.shields.io/badge/github-repo-blue?logo=github)](https://github.com/granit-fx/granit-mcp)
[![Quality Gate Status](https://sonarcloud.io/api/project_badges/measure?project=granit-fx_granit-mcp&metric=alert_status)](https://sonarcloud.io/summary/new_code?id=granit-fx_granit-mcp)
[![License: Apache-2.0](https://img.shields.io/badge/License-Apache%202.0-blue.svg)](LICENSE)

Remote MCP server for the [Granit framework](https://granit-fx.dev) — gives AI
assistants (Claude Code, Cursor, Windsurf) structured access to documentation,
code navigation, and NuGet package metadata.

Powered by a **pre-built JSON search index** and the **NuGet public API**, running
on **Cloudflare Workers** with [Hono](https://hono.dev/) and the
[Model Context Protocol SDK](https://modelcontextprotocol.io/).

## Tools

### Documentation

| Tool | Description |
| ---- | ----------- |
| `search_granit_docs` | Full-text TF-IDF search across docs |
| `get_module_reference` | Complete reference for a module |
| `list_patterns` | Architecture patterns by platform |

### NuGet packages

| Tool | Description |
| ---- | ----------- |
| `list_packages` | Granit.\* packages with version/downloads |
| `get_package_info` | Versions, deps, frameworks, license |

### Code navigation

| Tool | Description |
| ---- | ----------- |
| `search_code` | Search symbols across .NET and TS |
| `get_public_api` | Public API of a type with signatures |
| `get_project_graph` | Project/package dependency graph |

## Use with Claude Code

```bash
claude mcp add granit-mcp --transport http https://mcp.granit-fx.dev/mcp
```

Or add to your project's `.mcp.json`:

```json
{
  "mcpServers": {
    "granit-mcp": {
      "type": "url",
      "url": "https://mcp.granit-fx.dev/mcp"
    }
  }
}
```

## Use with Cursor / Windsurf

Add the MCP server in **Settings > MCP Servers**:

- **Name:** `granit-mcp`
- **Type:** `http`
- **URL:** `https://mcp.granit-fx.dev/mcp`

## Local development

```bash
# Install dependencies
pnpm install

# Generate the docs search index
cd ../granit-dotnet/docs-site
node scripts/generate-search-index.mjs

# Serve the built docs locally (includes search-index.json)
python3 -m http.server 4322 -d dist &

# Generate the code index (from granit-dotnet repo root)
cd ..
node docs-site/scripts/generate-code-index.mjs
# Output: code-index.json at repo root

# Start local Worker
cd ../granit-docs-mcp
pnpm dev
```

Create `.dev.vars` to point to local indexes:

```ini
SEARCH_INDEX_URL=http://localhost:4322/search-index.json
CODE_INDEX_URL=file:///path/to/granit-dotnet/code-index.json
```

Test with the MCP Inspector:

```bash
npx @modelcontextprotocol/inspector http://localhost:8787/mcp
```

## Architecture

```text
AI Assistant
  |  MCP Streamable HTTP
  v
Cloudflare Worker (mcp.granit-fx.dev)
  |
  +-- Docs tools -----> search-index.json (CF Pages, 24h KV cache)
  +-- Code tools -----> code-index.json   (GitHub Release, 12h KV cache)
  |                     front-index.json  (GitHub Release, 12h KV cache)
  +-- NuGet tools ----> api.nuget.org     (public, 6-12h KV cache)
```

Complementary with the
[GitHub MCP Server](https://github.com/github/github-mcp-server) for file
browsing, commits, PRs, and issues.

### Data sources

| Source | Origin | Cache TTL |
| ------ | ------ | --------- |
| `search-index.json` | CF Pages (granit-fx.dev) | 24 h |
| `code-index.json` | GitHub Release (granit-dotnet) | 12 h |
| `front-index.json` | GitHub Release (granit-front) | 12 h |
| NuGet package list | NuGet Search API | 12 h |
| NuGet package info | NuGet Registration API | 6 h |

### Code index generation

The `generate-code-index.mjs` script (in `granit-dotnet/docs-site/scripts/`)
parses `.csproj` files and `.cs` source files with regex to extract:

- **Project graph** — all projects with their dependencies and target frameworks
- **Public symbols** — classes, interfaces, records, structs, enums
- **Public members** — methods (with signatures), properties, events

The script handles multi-line signatures and partial classes. It runs with
Node.js only (no .NET required). Output for granit-dotnet: ~1500 types,
~2700 members, 172 projects, ~730 KB.

### Search index categories

| Category | Source path | Count |
| -------- | ----------- | ----- |
| `module` | `/dotnet/{core,data,security,api,...}/` | ~69 |
| `pattern` | `/dotnet/architecture/patterns/` | ~56 |
| `adr` | `/dotnet/architecture/adr/` | ~26 |
| `guide` | `/dotnet/guides/` | ~25 |
| `frontend` | `/frontend/` | ~25 |
| `concept` | `/dotnet/concepts/` | ~12 |
| `community` | `/contributing/`, `/troubleshooting/` | ~13 |
| `getting-started` | `/dotnet/getting-started/` | ~8 |

## Deployment

Automatic on push to `main` or when triggered by `granit-dotnet` after a docs
deploy (`repository_dispatch: docs-deployed`).

### Required secrets

| Secret | Purpose |
| ------ | ------- |
| `CLOUDFLARE_API_TOKEN` | Wrangler deploy to Cloudflare Workers |
| `CLOUDFLARE_ACCOUNT_ID` | Cloudflare account identifier |

## ADRs

- [ADR-001](docs/adr/001-json-index-cloudflare-workers.md) —
  JSON index + Cloudflare Workers
- [ADR-002](docs/adr/002-granit-mcp-code-and-packages.md) —
  Code navigation & NuGet packages

## License

Apache-2.0
