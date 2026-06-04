# Architecture

## Overview

Granit Tools MCP is a local .NET 10 dotnet tool that speaks the
[Model Context Protocol](https://modelcontextprotocol.io) over stdio.
It acts as a bridge between AI assistants and external data sources:
the Granit documentation site, per-repo code indexes (GitHub and
self-hosted GitLab, public or private), and the NuGet registry.

```text
AI assistant ──stdio (JSON-RPC)──> granit-tools-mcp
                                     ├─ DocsStore ─────────> SQLite FTS5 (local)
                                     │    ↑ DocsIndexer        ↑ indexed from
                                     │    └─ periodic fetch ── granit-fx.dev/llms-full.txt
                                     │
                                     ├─ CodeIndexClient ───> per-repo .mcp-*-index.json
                                     │    └─ branch-aware      GitHub / GitLab
                                     │
                                     ├─ NuGetClient ───────> api.nuget.org
                                     │    └─ optional ────────> nuget.pkg.github.com
                                     │
                                     └─ GitBranchDetector ─> .git/HEAD (local)
```

## Transport

The server uses **stdio transport** — it reads JSON-RPC requests from
`stdin` and writes responses to `stdout`. This means:

- No network ports opened
- No authentication needed (the calling process owns the pipe)
- All diagnostic logs go to `stderr` (never `stdout`)
- The server runs for the lifetime of the AI assistant session

## Services

### DocsStore

A singleton that manages a SQLite database with an FTS5 virtual table.

**Indexing pipeline:**

1. `DocsIndexer` (a `BackgroundService`) fetches `llms-full.txt` via HTTP
2. The Markdown content is split by H1 headings into individual articles
3. Each article gets an auto-generated ID (`doc-1`, `doc-2`, ...) and an
   inferred category (module, guide, pattern, reference, compliance, general)
4. Articles are inserted into the FTS5 table inside a transaction
5. A `last_indexed` timestamp is stored in the `state` table

**Search pipeline:**

1. The user query is tokenized — terms shorter than 2 characters are dropped
2. Each remaining term is double-quoted and joined with `OR`
3. The query runs against the FTS5 `MATCH` operator
4. Results include a `snippet()` for context

**Category inference rules:**

| Category | Matched keywords in title/content |
| -------- | --------------------------------- |
| compliance | gdpr, compliance, iso 27001, crypto |
| module | module, granit. |
| guide | getting started, quick start, crud |
| pattern | pattern, architecture |
| reference | contains ` ```csharp ` or ` ```cs ` code blocks |
| general | fallback |

### DocsIndexer

A `BackgroundService` that runs the indexing pipeline on startup, then
refreshes on a configurable interval (`GRANIT_MCP_REFRESH_HOURS`).

**Startup behavior:**

1. Check if the SQLite database has a fresh index (age < refresh interval)
2. If fresh — mark the store as ready, skip the HTTP fetch
3. If stale or missing — fetch, parse, and index

**Error handling:**

- Network failures are logged and swallowed
- If fetch fails but a stale database exists, the store is marked ready
  with stale data (offline fallback)
- The host is configured with `BackgroundServiceExceptionBehavior.Ignore`
  so a crash in the indexer never kills the server

### RepoRegistry

Builds the set of searchable repositories: the two built-in defaults
(`granit-dotnet`, `granit-front`) plus any user-defined repos loaded from
`repos.json` (`GRANIT_MCP_REPOS_FILE`). User entries augment the defaults
and may override one by reusing its `id`. Each entry carries a `provider`
(`github`/`gitlab`), `kind` (`dotnet`/`front`), `project`, and optional
`host`/`indexPath`/`private`. Invalid entries are skipped with a warning.

### CodeIndexClient

Fetches each configured repo's `.mcp-*-index.json` and parses it per `kind`.
The fetch endpoint and auth depend on the repo's `provider`:

| Provider | Endpoint | Auth |
| -------- | -------- | ---- |
| GitHub (public) | `raw.githubusercontent.com` | none |
| GitHub (`private`) | Contents API `api.github.com` | `Bearer GRANIT_MCP_GITHUB_TOKEN` |
| GitLab | API v4 raw `https://{host}/api/v4/...` | `PRIVATE-TOKEN GRANIT_MCP_GITLAB_TOKEN` |

Access is governed by the caller's token — a repo that returns 403/404 is
skipped silently, so a team can share one `repos.json`.

**Branch resolution:**

1. If the caller passes an explicit `branch` parameter, use it
2. Otherwise, call `GitBranchDetector.DetectBranch()` to read `.git/HEAD`
3. If detection fails (not a git repo, detached HEAD), fall back to `develop`

**Caching:** In-memory dictionary keyed by `repo@branch`, 12-hour TTL.
Returns stale data on network error.

### NuGetClient

Queries two NuGet feeds:

| Feed | Auth | When |
| ---- | ---- | ---- |
| nuget.org | none | always |
| GitHub Packages | Bearer token | when token is set |

- nuget.org: `azuresearch-usnc.nuget.org/query`
- GitHub: `nuget.pkg.github.com/granit-fx/query`

**Package list merging:**

- Packages from both feeds are merged by ID
- If a package exists on both, the nuget.org entry is kept with source
  set to `nuget.org+github`
- GitHub-only packages get source `github`

**Version list merging** (for `nuget_get`):

- Versions from both feeds are merged by version string
- Duplicates are deduplicated (nuget.org version takes precedence)
- Each version carries its source indicator

### GitBranchDetector

A static utility that reads the current branch from `.git/HEAD`.

**Algorithm:**

1. Walk up from the current working directory looking for `.git`
2. If `.git` is a file (worktree), follow the `gitdir:` pointer
3. Read `HEAD` and parse `ref: refs/heads/{branch}`
4. Return the branch name, or `"develop"` on any failure

## Dependency injection

All services are registered in `Program.cs`:

```text
GranitMcpConfig   → Singleton (immutable record from env vars)
HttpClientFactory → Transient (via AddHttpClient)
DocsStore         → Singleton (owns SQLite connection)
RepoRegistry      → Singleton (defaults + repos.json)
CodeIndexClient   → Singleton (owns in-memory cache)
NuGetClient       → Singleton (owns in-memory cache)
DocsIndexer       → HostedService (BackgroundService)
```

MCP tool classes are static with method-injected dependencies — the MCP
SDK resolves parameters from the DI container automatically.

## Error handling philosophy

The server is designed to **never crash** and **always return something
useful**:

1. **Network errors** — return stale cached data when available
2. **Missing data** — return a helpful message suggesting the right tool
3. **Indexing in progress** — return a JSON status message
4. **Background service crash** — swallowed by the host, server keeps running
5. **Invalid input** — return guidance, not stack traces
