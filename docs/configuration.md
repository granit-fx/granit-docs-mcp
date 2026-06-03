# Configuration

All settings are read from environment variables prefixed with
`GRANIT_MCP_`. Every variable has a sensible default — zero configuration
is required for basic usage.

## Environment variables

### `GRANIT_MCP_LOG_LEVEL`

- **Type:** LogLevel — **Default:** `Information`
- Minimum log level. Valid values: `Trace`, `Debug`, `Information`,
  `Warning`, `Error`, `Critical`, `None`. Case-insensitive.

### `GRANIT_MCP_REFRESH_HOURS`

- **Type:** int — **Default:** `4`
- Hours between documentation re-index cycles.

### `GRANIT_MCP_DATA_DIR`

- **Type:** path — **Default:** `~/.granit-mcp`
- Directory for the SQLite database and logs.

### `GRANIT_MCP_DOCS_URL`

- **Type:** URL — **Default:** `https://granit-fx.dev/llms-full.txt`
- Documentation source URL.

### `GRANIT_MCP_CODE_INDEX_URL`

- **Type:** URL
- **Default:** GitHub raw URL with `{branch}` placeholder
- Template URL for the built-in `dotnet` (.NET) code index. Must contain
  `{branch}`.

### `GRANIT_MCP_FRONT_INDEX_URL`

- **Type:** URL
- **Default:** GitHub raw URL with `{branch}` placeholder
- Template URL for the built-in `front` (TypeScript) code index. Must
  contain `{branch}`.

### `GRANIT_MCP_REPOS_FILE`

- **Type:** path — **Default:** `~/.granit-mcp/repos.json`
- JSON file listing additional repositories to make searchable (private
  GitHub or self-hosted GitLab). See
  [Searching additional repositories](#searching-additional-repositories).

### `GRANIT_MCP_GITHUB_TOKEN`

- **Type:** string — **Default:** *(none)*
- GitHub token (`Bearer`) used both for the GitHub Packages NuGet feed and
  for fetching the index of **private GitHub** repos via the Contents API.
  A fine-grained PAT needs `Contents: read` (private repos) and/or
  `read:packages` (GitHub Packages). Optional.

### `GRANIT_MCP_GITLAB_TOKEN`

- **Type:** string — **Default:** *(none)*
- GitLab token sent as `PRIVATE-TOKEN` when fetching indexes from GitLab
  repos. A PAT with `read_api` (or `read_repository`) scope. Optional.

### `GRANIT_MCP_GITLAB_HOST`

- **Type:** host — **Default:** *(none)*
- Default GitLab host (e.g. `gitlab.example.com`) for `repos.json` entries
  that omit `host`. Scheme and trailing slash are stripped automatically.

## Passing environment variables

Set variables in the MCP server configuration of your AI assistant.

### Claude Code (`~/.claude.json`)

```json
{
  "mcpServers": {
    "granit-tools": {
      "type": "stdio",
      "command": "granit-tools-mcp",
      "args": [],
      "env": {
        "GRANIT_MCP_LOG_LEVEL": "Warning",
        "GRANIT_MCP_REFRESH_HOURS": "8",
        "GRANIT_MCP_GITHUB_TOKEN": "ghp_xxxxxxxxxxxx"
      }
    }
  }
}
```

### Shell (testing)

```bash
GRANIT_MCP_LOG_LEVEL=Debug granit-tools-mcp
```

## Searching additional repositories

The code tools (`code_search`, `code_get_api`, `code_get_graph`,
`code_list_branches`) always search two built-in repositories:

| id | Repo | Provider | Kind |
| -- | ---- | -------- | ---- |
| `dotnet` | `granit-fx/granit-dotnet` | GitHub (public) | .NET |
| `front` | `granit-fx/granit-front` | GitHub (public) | TypeScript |

To make **more** repositories searchable — including private GitHub repos
and repos on a self-hosted GitLab — list them in `repos.json` (path from
`GRANIT_MCP_REPOS_FILE`, default `~/.granit-mcp/repos.json`):

```jsonc
[
  // Private GitHub repo (Contents API + GRANIT_MCP_GITHUB_TOKEN)
  { "id": "business", "kind": "dotnet", "provider": "github",
    "project": "granit-fx/granit-business", "private": true },

  // Self-hosted GitLab repo, host inherited from GRANIT_MCP_GITLAB_HOST
  { "id": "internal-api", "kind": "dotnet", "provider": "gitlab",
    "project": "granit/backend/internal-api" },

  // GitLab repo with an explicit host
  { "id": "ops", "kind": "front", "provider": "gitlab",
    "host": "gitlab.example.com", "project": "infra/ops-console" }
]
```

A ready-to-edit template with inline comments lives at
[`repos.example.json`](../repos.example.json) in the repo root — copy it to
`~/.granit-mcp/repos.json`. Comments (`//`) and trailing commas are allowed.

### Entry fields

| Field | Required | Default | Notes |
| ----- | -------- | ------- | ----- |
| `id` | no | last segment of `project` | Selector for the `repo` tool parameter |
| `kind` | **yes** | — | `dotnet` or `front` — the index schema |
| `provider` | no | `github` | `github` or `gitlab` |
| `project` | **yes** | — | GitHub `owner/name`; GitLab `group/subgroup/project` |
| `host` | gitlab only | `GRANIT_MCP_GITLAB_HOST` | GitLab instance host |
| `indexPath` | no | `.mcp-code-index.json` / `.mcp-front-index.json` | Index file path in the repo |
| `private` | no | `false` | GitHub only — fetch via the authenticated Contents API |

### How fetching and auth work

| Provider | Index endpoint | Auth |
| -------- | -------------- | ---- |
| GitHub (public) | `raw.githubusercontent.com` | none |
| GitHub (`private: true`) | Contents API `api.github.com` | `Bearer GRANIT_MCP_GITHUB_TOKEN` |
| GitLab | API v4 raw file (`https://{host}/api/v4/...`) | `PRIVATE-TOKEN: GRANIT_MCP_GITLAB_TOKEN` |

Notes:

- **Access follows the token.** Configure the same `repos.json` for a whole
  team; each developer only sees repos their token can read. A repo that
  returns 403/404 is skipped silently.
- Entries **augment** the built-in defaults. Reuse `id` `dotnet` or `front`
  to override a default.
- Invalid entries (bad `kind`/`provider`, missing `project`, GitLab without a
  host) are skipped with a warning on stderr; the rest still load.
- A repo is only searchable once it has a committed index file
  (`.mcp-code-index.json` / `.mcp-front-index.json`) on the target branch —
  generated by the upstream indexer, not by this server.

## Data directory

The data directory (`~/.granit-mcp` by default) stores:

| File | Purpose | Size |
| ---- | ------- | ---- |
| `docs.db` | SQLite FTS5 index of all documentation articles | ~2-5 MB |
| `repos.json` | Optional list of extra searchable repos (you create it) | <1 KB |

The directory is created automatically on first startup if it does not
exist. It can be safely deleted — the server rebuilds the index on next
launch.

## GitHub Packages integration

By default, `nuget_list` and `nuget_get` only query the public
nuget.org feed. To also include pre-release packages published to
GitHub Packages:

1. Create a GitHub PAT with `read:packages` scope
2. Set `GRANIT_MCP_GITHUB_TOKEN` in your MCP server config

When the token is set:

- `nuget_list` merges results from both feeds
- `nuget_get` consolidates version lists from both feeds
- Source indicators (`[github]`, `[nuget.org+github]`) appear in output

When the token is **not** set, GitHub Packages is silently skipped —
all tools work normally with nuget.org only.

## Cache lifetimes

| Data source | Storage | TTL | Fallback |
| ----------- | ------- | --- | -------- |
| Documentation | SQLite | 4h (config.) | Stale DB |
| Code index (per repo + branch) | Memory | 12h | Stale |
| NuGet list | Memory | 12h | Stale |
| NuGet detail | Memory | 6h | *(none)* |
| Branch list | Transient | — | Empty |

All caches use graceful degradation — if a network request fails, the
server returns stale data when available rather than failing.

## Logging

The MCP stdio protocol uses `stdout` for JSON-RPC messages. **All logs
are emitted to `stderr`** to avoid interference with the protocol.

To see debug logs when troubleshooting, set `GRANIT_MCP_LOG_LEVEL=Debug`
and observe stderr output.
