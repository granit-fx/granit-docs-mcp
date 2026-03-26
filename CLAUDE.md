# granit-tools-mcp

Local MCP server for the Granit framework. Distributed as a .NET 10
dotnet tool. Provides documentation search (SQLite FTS5), code
navigation, and NuGet package discovery via the Model Context Protocol.

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
| `src/Granit.Tools.Mcp/Services/DocsStore.cs` | SQLite FTS5 index + search |
| `src/Granit.Tools.Mcp/Services/DocsIndexer.cs` | Background llms-full.txt fetcher |
| `src/Granit.Tools.Mcp/Services/CodeIndexClient.cs` | Branch-aware code index cache |
| `src/Granit.Tools.Mcp/Services/NuGetClient.cs` | NuGet API client |
| `src/Granit.Tools.Mcp/Services/GitBranchDetector.cs` | .git/HEAD branch detection |
| `src/Granit.Tools.Mcp/Tools/*.cs` | 9 MCP tool handlers |
| `tests/Granit.Tools.Mcp.Tests/*.cs` | xUnit tests (FTS5, config, branch) |

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
| `GRANIT_MCP_CODE_INDEX_URL` | GitHub raw template | Code index |
| `GRANIT_MCP_FRONT_INDEX_URL` | GitHub raw template | Front index |

## Conventions

- **Transport:** stdio (stdout = JSON-RPC, logs → stderr)
- **Tools:** attribute-driven (`[McpServerToolType]` + `[McpServerTool]`)
- **Graceful degradation:** tools return status JSON during indexing
- **No secrets in code**
