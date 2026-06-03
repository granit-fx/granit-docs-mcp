using System.Net.Http.Headers;
using System.Text.Json;
using Granit.Tools.Mcp.Models;
using Microsoft.Extensions.Logging;

namespace Granit.Tools.Mcp.Services;

/// <summary>
/// Fetches and caches per-repo code indexes across providers (GitHub public raw,
/// GitHub Contents API for private repos, GitLab API v4 for self-hosted), with
/// branch-aware URL resolution. Access to private repos is governed entirely by
/// the caller's token — repos the token can't read are skipped gracefully.
/// </summary>
public sealed class CodeIndexClient(
    IHttpClientFactory httpFactory,
    GranitMcpConfig config,
    RepoRegistry registry,
    ILogger<CodeIndexClient> logger)
{
    private const string UserAgent = "granit-tools-mcp";

    private readonly Dictionary<string, CachedIndex> _cache = new();
    private readonly Lock _lock = new();

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    public static string ResolveBranch(string? branch) =>
        branch ?? GitBranchDetector.DetectBranch();

    /// <summary>
    /// Loads the code indexes for every configured repo matching <paramref name="repoId"/>
    /// (or all repos when null) on the given branch. Repos with no reachable/parsable
    /// index are omitted from the result.
    /// </summary>
    public async Task<IReadOnlyList<LoadedIndex>> GetIndexesAsync(
        string? repoId, string? branch, CancellationToken ct = default)
    {
        string resolvedBranch = ResolveBranch(branch);
        var repos = registry.Resolve(repoId).ToList();

        LoadedIndex?[] loaded = await Task.WhenAll(
            repos.Select(r => LoadAsync(r, resolvedBranch, ct)));

        return loaded.Where(x => x is not null).Select(x => x!).ToList();
    }

    /// <summary>
    /// Lists, per configured repo, the branches that have a committed index file —
    /// the valid values for the <c>branch</c> tool parameter.
    /// </summary>
    public async Task<List<BranchInfo>> ListBranchesAsync(
        string? repoId, CancellationToken ct = default)
    {
        var results = new List<BranchInfo>();
        foreach (RepoConfig repo in registry.Resolve(repoId))
        {
            results.AddRange(await ListRepoBranchesAsync(repo, ct));
        }

        return results;
    }

    private async Task<LoadedIndex?> LoadAsync(
        RepoConfig repo, string branch, CancellationToken ct)
    {
        object? data = await GetCachedAsync(repo, branch, ct);
        return data switch
        {
            CodeIndex code => new LoadedIndex(repo, code, null),
            FrontIndex front => new LoadedIndex(repo, null, front),
            _ => null,
        };
    }

    private async Task<object?> GetCachedAsync(
        RepoConfig repo, string branch, CancellationToken ct)
    {
        string key = $"{repo.Id}@{branch}";

        lock (_lock)
        {
            if (_cache.TryGetValue(key, out CachedIndex? cached) && !cached.IsExpired)
            {
                return cached.Data;
            }
        }

        try
        {
            object? data = await FetchAsync(repo, branch, ct);
            if (data is not null)
            {
                lock (_lock)
                {
                    _cache[key] = new CachedIndex(data, DateTime.UtcNow.AddHours(12));
                }
            }

            return data;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex,
                "Failed to fetch index for {Repo}@{Branch}", repo.Id, branch);

            lock (_lock)
            {
                return _cache.TryGetValue(key, out CachedIndex? stale) ? stale.Data : null;
            }
        }
    }

    private async Task<object?> FetchAsync(
        RepoConfig repo, string branch, CancellationToken ct)
    {
        using HttpClient http = httpFactory.CreateClient();
        http.Timeout = TimeSpan.FromSeconds(30);

        using HttpRequestMessage request = BuildIndexRequest(repo, branch);
        using HttpResponseMessage response = await http.SendAsync(request, ct);

        if (!response.IsSuccessStatusCode)
        {
            // 404/403 typically means the branch/file is absent or the caller's
            // token can't read this (private) repo — degrade quietly, don't throw.
            logger.LogDebug(
                "Index for {Repo}@{Branch} unavailable ({Status})",
                repo.Id, branch, (int)response.StatusCode);
            return null;
        }

        await using Stream stream = await response.Content.ReadAsStreamAsync(ct);
        return repo.Kind switch
        {
            RepoKind.Dotnet => await JsonSerializer.DeserializeAsync<CodeIndex>(
                stream, JsonOptions, ct),
            RepoKind.Front => await JsonSerializer.DeserializeAsync<FrontIndex>(
                stream, JsonOptions, ct),
            _ => null,
        };
    }

    private HttpRequestMessage BuildIndexRequest(RepoConfig repo, string branch)
    {
        switch (repo.Provider)
        {
            case RepoProvider.GitHub when repo.Private:
            {
                string url =
                    $"https://api.github.com/repos/{repo.Project}/contents/{repo.IndexPath}" +
                    $"?ref={Uri.EscapeDataString(branch)}";
                var request = new HttpRequestMessage(HttpMethod.Get, url);
                request.Headers.Add("Accept", "application/vnd.github.raw");
                request.Headers.Add("User-Agent", UserAgent);
                AddGitHubAuth(request);
                return request;
            }

            case RepoProvider.GitHub:
            {
                string url = repo.RawUrlTemplate is not null
                    ? repo.RawUrlTemplate.Replace("{branch}", branch, StringComparison.Ordinal)
                    : $"https://raw.githubusercontent.com/{repo.Project}/{branch}/{repo.IndexPath}";
                var request = new HttpRequestMessage(HttpMethod.Get, url);
                request.Headers.Add("User-Agent", UserAgent);
                return request;
            }

            case RepoProvider.GitLab:
            {
                string url = BuildGitLabRawUrl(repo, branch);
                var request = new HttpRequestMessage(HttpMethod.Get, url);
                request.Headers.Add("User-Agent", UserAgent);
                AddGitLabAuth(request);
                return request;
            }

            default:
                throw new InvalidOperationException(
                    $"Unsupported provider {repo.Provider}");
        }
    }

    private async Task<List<BranchInfo>> ListRepoBranchesAsync(
        RepoConfig repo, CancellationToken ct)
    {
        using HttpClient http = httpFactory.CreateClient();
        http.Timeout = TimeSpan.FromSeconds(30);

        try
        {
            using HttpRequestMessage listRequest = BuildBranchListRequest(repo);
            using HttpResponseMessage response = await http.SendAsync(listRequest, ct);
            if (!response.IsSuccessStatusCode)
            {
                return [];
            }

            List<GitBranch>? branches = await JsonSerializer.DeserializeAsync<List<GitBranch>>(
                await response.Content.ReadAsStreamAsync(ct), JsonOptions, ct);
            if (branches is null)
            {
                return [];
            }

            IEnumerable<Task<BranchInfo>> checks = branches.Select(async b =>
            {
                bool hasIndex = await HasIndexAsync(http, repo, b.Name, ct);
                return new BranchInfo(repo.Id, b.Name, hasIndex);
            });

            return (await Task.WhenAll(checks))
                .Where(b => b.HasIndex)
                .ToList();
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex,
                "Failed to list branches for {Repo}", repo.Id);
            return [];
        }
    }

    private HttpRequestMessage BuildBranchListRequest(RepoConfig repo)
    {
        if (repo.Provider == RepoProvider.GitLab)
        {
            string url =
                $"https://{repo.Host}/api/v4/projects/{Uri.EscapeDataString(repo.Project)}" +
                "/repository/branches?per_page=100";
            var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.Add("User-Agent", UserAgent);
            AddGitLabAuth(request);
            return request;
        }

        string ghUrl =
            $"https://api.github.com/repos/{repo.Project}/branches?per_page=100";
        var ghRequest = new HttpRequestMessage(HttpMethod.Get, ghUrl);
        ghRequest.Headers.Add("Accept", "application/vnd.github+json");
        ghRequest.Headers.Add("User-Agent", UserAgent);
        AddGitHubAuth(ghRequest);
        return ghRequest;
    }

    private async Task<bool> HasIndexAsync(
        HttpClient http, RepoConfig repo, string branch, CancellationToken ct)
    {
        string url;
        if (repo.Provider == RepoProvider.GitLab)
        {
            url = BuildGitLabRawUrl(repo, branch);
        }
        else
        {
            url =
                $"https://api.github.com/repos/{repo.Project}/contents/{repo.IndexPath}" +
                $"?ref={Uri.EscapeDataString(branch)}";
        }

        using var request = new HttpRequestMessage(HttpMethod.Head, url);
        request.Headers.Add("User-Agent", UserAgent);
        if (repo.Provider == RepoProvider.GitLab)
        {
            AddGitLabAuth(request);
        }
        else
        {
            request.Headers.Add("Accept", "application/vnd.github+json");
            AddGitHubAuth(request);
        }

        using HttpResponseMessage response = await http.SendAsync(request, ct);
        return response.IsSuccessStatusCode;
    }

    private static string BuildGitLabRawUrl(RepoConfig repo, string branch) =>
        $"https://{repo.Host}/api/v4/projects/{Uri.EscapeDataString(repo.Project)}" +
        $"/repository/files/{Uri.EscapeDataString(repo.IndexPath)}/raw" +
        $"?ref={Uri.EscapeDataString(branch)}";

    private void AddGitHubAuth(HttpRequestMessage request)
    {
        if (config.GitHubToken is not null)
        {
            request.Headers.Authorization =
                new AuthenticationHeaderValue("Bearer", config.GitHubToken);
        }
    }

    private void AddGitLabAuth(HttpRequestMessage request)
    {
        if (config.GitLabToken is not null)
        {
            request.Headers.Add("PRIVATE-TOKEN", config.GitLabToken);
        }
    }

    private sealed record CachedIndex(object Data, DateTime ExpiresAt)
    {
        public bool IsExpired => DateTime.UtcNow > ExpiresAt;
    }

    private sealed record GitBranch(
        [property: System.Text.Json.Serialization.JsonPropertyName("name")]
        string Name);
}

/// <summary>A repo's loaded index — exactly one of <see cref="Dotnet"/>/<see cref="Front"/> is set.</summary>
public sealed record LoadedIndex(RepoConfig Repo, CodeIndex? Dotnet, FrontIndex? Front);

public sealed record BranchInfo(string Repo, string Branch, bool HasIndex);
