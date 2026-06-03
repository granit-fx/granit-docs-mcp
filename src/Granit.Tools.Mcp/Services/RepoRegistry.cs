using System.Text.Json;
using Granit.Tools.Mcp.Models;
using Microsoft.Extensions.Logging;

namespace Granit.Tools.Mcp.Services;

/// <summary>
/// The set of repositories whose code indexes are searchable. Always includes
/// the built-in granit-dotnet/granit-front defaults; user-defined repos from
/// <c>repos.json</c> augment the list and may override a default by reusing its id.
/// </summary>
public sealed class RepoRegistry
{
    public const string DotnetIndexFile = ".mcp-code-index.json";
    public const string FrontIndexFile = ".mcp-front-index.json";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    private readonly List<RepoConfig> _repos;

    public RepoRegistry(GranitMcpConfig config, ILogger<RepoRegistry> logger)
    {
        var byId = new Dictionary<string, RepoConfig>(StringComparer.OrdinalIgnoreCase)
        {
            ["dotnet"] = new RepoConfig(
                "dotnet", RepoKind.Dotnet, RepoProvider.GitHub,
                "granit-fx/granit-dotnet", DotnetIndexFile, Private: false,
                Host: null, RawUrlTemplate: config.CodeIndexUrl),
            ["front"] = new RepoConfig(
                "front", RepoKind.Front, RepoProvider.GitHub,
                "granit-fx/granit-front", FrontIndexFile, Private: false,
                Host: null, RawUrlTemplate: config.FrontIndexUrl),
        };

        foreach (RepoConfig repo in LoadFromFile(config.ReposFile, config.GitLabHost, logger))
        {
            byId[repo.Id] = repo;
        }

        _repos = [.. byId.Values];
    }

    /// <summary>All configured repos, in registration order (defaults first).</summary>
    public IReadOnlyList<RepoConfig> All => _repos;

    /// <summary>
    /// Repos matching <paramref name="id"/> (case-insensitive), or all repos when
    /// <paramref name="id"/> is null.
    /// </summary>
    public IEnumerable<RepoConfig> Resolve(string? id) =>
        id is null
            ? _repos
            : _repos.Where(r => r.Id.Equals(id, StringComparison.OrdinalIgnoreCase));

    private static List<RepoConfig> LoadFromFile(
        string path, string? defaultGitLabHost, ILogger logger)
    {
        if (!File.Exists(path))
        {
            return [];
        }

        try
        {
            string json = File.ReadAllText(path);
            List<RepoConfigDto>? dtos = JsonSerializer.Deserialize<List<RepoConfigDto>>(
                json, JsonOptions);

            if (dtos is null)
            {
                return [];
            }

            var result = new List<RepoConfig>();
            foreach (RepoConfigDto dto in dtos)
            {
                RepoConfig? repo = MapDto(dto, defaultGitLabHost, logger);
                if (repo is not null)
                {
                    result.Add(repo);
                }
            }

            logger.LogInformation(
                "Loaded {Count} repo(s) from {Path}", result.Count, path);
            return result;
        }
        catch (Exception ex) when (ex is IOException or JsonException)
        {
            logger.LogWarning(ex,
                "Failed to read repos file {Path}; using built-in repos only", path);
            return [];
        }
    }

    private static RepoConfig? MapDto(
        RepoConfigDto dto, string? defaultGitLabHost, ILogger logger)
    {
        if (string.IsNullOrWhiteSpace(dto.Project))
        {
            logger.LogWarning("Repo entry is missing 'project'; skipped");
            return null;
        }

        string project = dto.Project.Trim().Trim('/');
        string id = !string.IsNullOrWhiteSpace(dto.Id) ? dto.Id.Trim() : DeriveId(project);

        if (!TryParseKind(dto.Kind, out RepoKind kind))
        {
            logger.LogWarning(
                "Repo '{Id}' has invalid kind '{Kind}' (expected dotnet|front); skipped",
                id, dto.Kind);
            return null;
        }

        if (!TryParseProvider(dto.Provider, out RepoProvider provider))
        {
            logger.LogWarning(
                "Repo '{Id}' has invalid provider '{Provider}' (expected github|gitlab); skipped",
                id, dto.Provider);
            return null;
        }

        string indexPath = !string.IsNullOrWhiteSpace(dto.IndexPath)
            ? dto.IndexPath.Trim()
            : kind == RepoKind.Dotnet ? DotnetIndexFile : FrontIndexFile;

        string? host = !string.IsNullOrWhiteSpace(dto.Host)
            ? dto.Host.Trim()
            : provider == RepoProvider.GitLab ? defaultGitLabHost : null;

        if (provider == RepoProvider.GitLab && string.IsNullOrWhiteSpace(host))
        {
            logger.LogWarning(
                "GitLab repo '{Id}' has no host (set 'host' or GRANIT_MCP_GITLAB_HOST); skipped",
                id);
            return null;
        }

        return new RepoConfig(
            id, kind, provider, project, indexPath,
            Private: dto.Private ?? false, Host: host, RawUrlTemplate: null);
    }

    /// <summary>Falls back to the last path segment when no explicit id is given.</summary>
    private static string DeriveId(string project)
    {
        int slash = project.LastIndexOf('/');
        return slash >= 0 && slash < project.Length - 1
            ? project[(slash + 1)..]
            : project;
    }

    private static bool TryParseKind(string? value, out RepoKind kind)
    {
        kind = RepoKind.Dotnet;
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        return Enum.TryParse(value.Trim(), ignoreCase: true, out kind);
    }

    private static bool TryParseProvider(string? value, out RepoProvider provider)
    {
        provider = RepoProvider.GitHub;
        if (string.IsNullOrWhiteSpace(value))
        {
            return true; // GitHub is the default when omitted
        }

        return Enum.TryParse(value.Trim(), ignoreCase: true, out provider);
    }
}
