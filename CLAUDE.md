# granit-tools-mcp

Local MCP server for the Granit framework, distributed as a .NET 10 dotnet tool.
Provides documentation search (SQLite FTS5), code navigation, and NuGet package
discovery via the Model Context Protocol.

## Stack

- **Runtime:** .NET 10 (`Microsoft.Extensions.Hosting`)
- **MCP SDK:** `ModelContextProtocol` (stdio transport)
- **Search:** SQLite FTS5 via `Microsoft.Data.Sqlite`
- **Markdown:** Markdig (parsing `llms-full.txt`)

## Architecture

```text
Claude Code ──stdio──> Granit.Tools.Mcp (local .NET 10 tool)
                         |-- Docs tools ------> SQLite FTS5
                         |                        ^ indexed from llms-full.txt
                         |-- Code tools ------> .mcp-*-index.json (GitHub raw)
                         |-- NuGet tools -----> api.nuget.org
                         +-- Branch tools ----> api.github.com
```

## Key files

| Path | Purpose |
| ---- | ------- |
| `src/Granit.Tools.Mcp/Program.cs` | Host setup, MCP transport |
| `src/Granit.Tools.Mcp/GranitMcpConfig.cs` | Env var configuration |
| `src/Granit.Tools.Mcp/Models/RepoConfig.cs` | Repo entry model (provider/kind) |
| `src/Granit.Tools.Mcp/Services/DocsStore.cs` | SQLite FTS5 index + search |
| `src/Granit.Tools.Mcp/Services/DocsIndexer.cs` | Background llms-full.txt fetcher |
| `src/Granit.Tools.Mcp/Services/RepoRegistry.cs` | Default + `repos.json` repo list |
| `src/Granit.Tools.Mcp/Services/CodeIndexClient.cs` | Multi-provider code index cache |
| `src/Granit.Tools.Mcp/Services/NuGetClient.cs` | NuGet API client |
| `src/Granit.Tools.Mcp/Services/GitBranchDetector.cs` | .git/HEAD branch detection |
| `src/Granit.Tools.Mcp/Tools/*.cs` | 9 MCP tool handlers |
| `tests/Granit.Tools.Mcp.Tests/*.cs` | xUnit tests (FTS5, config, repos, branch) |

## Building

```bash
dotnet build
dotnet pack -o nupkgs
dotnet tool install --global --add-source nupkgs Granit.Tools.Mcp
```

## Configuration

Environment variables with `GRANIT_MCP_` prefix:

| Variable | Default | Description |
| -------- | ------- | ----------- |
| `GRANIT_MCP_LOG_LEVEL` | Information | Log level |
| `GRANIT_MCP_REFRESH_HOURS` | 4 | Docs re-index interval |
| `GRANIT_MCP_DATA_DIR` | `~/.granit-mcp` | SQLite + logs |
| `GRANIT_MCP_DOCS_URL` | `granit-fx.dev/llms-full.txt` | Docs source |
| `GRANIT_MCP_CODE_INDEX_URL` | GitHub raw template | Built-in .NET index URL |
| `GRANIT_MCP_FRONT_INDEX_URL` | GitHub raw template | Built-in front index URL |
| `GRANIT_MCP_REPOS_FILE` | `~/.granit-mcp/repos.json` | Extra searchable repos |
| `GRANIT_MCP_GITHUB_TOKEN` | – | Bearer token (private GitHub + Packages) |
| `GRANIT_MCP_GITLAB_TOKEN` | – | `PRIVATE-TOKEN` for GitLab repos |
| `GRANIT_MCP_GITLAB_HOST` | – | Default host for GitLab `repos.json` entries |

### Additional repos (`repos.json`)

Built-in repos `dotnet` (granit-dotnet) and `front` (granit-front) are always
searchable. Add private GitHub or self-hosted GitLab repos via `repos.json`
(augments defaults; reuse an `id` to override one). Access is governed by the
caller's token — unreachable repos are skipped silently.

```jsonc
[
  { "id": "business", "kind": "dotnet", "provider": "github",
    "project": "granit-fx/granit-business", "private": true },
  { "id": "ops", "kind": "front", "provider": "gitlab",
    "host": "gitlab.example.com", "project": "infra/ops-console" }
]
```

| Field | Required | Notes |
| ----- | -------- | ----- |
| `id` | no | Selector for the `repo` tool param; defaults to last path segment |
| `kind` | yes | `dotnet` \| `front` — index schema |
| `provider` | no | `github` (default) \| `gitlab` |
| `project` | yes | GitHub `owner/name`; GitLab `group/subgroup/project` |
| `host` | gitlab | GitLab host; falls back to `GRANIT_MCP_GITLAB_HOST` |
| `indexPath` | no | Defaults to `.mcp-code-index.json` / `.mcp-front-index.json` |
| `private` | no | GitHub only: fetch via authenticated Contents API |

## Conventions

- **Transport:** stdio (stdout = JSON-RPC, logs → stderr)
- **Tools:** attribute-driven (`[McpServerToolType]` + `[McpServerTool]`)
- **Graceful degradation:** tools return status JSON during indexing
- **No secrets in code**
