using Microsoft.Extensions.Logging;

namespace Granit.Tools.Mcp;

public sealed record GranitMcpConfig(
    LogLevel LogLevel,
    int RefreshHours,
    string DataDir,
    string DocsUrl,
    string CodeIndexUrl,
    string FrontIndexUrl,
    string? GitHubToken,
    string? GitLabToken,
    string? GitLabHost,
    string ReposFile)
{
    private const string Prefix = "GRANIT_MCP_";

    public static GranitMcpConfig FromEnvironment()
    {
        LogLevel logLevel = ParseEnum(
            $"{Prefix}LOG_LEVEL", LogLevel.Information);
        int refreshHours = ParseInt(
            $"{Prefix}REFRESH_HOURS", 4);
        string dataDir = Environment.GetEnvironmentVariable(
            $"{Prefix}DATA_DIR")
            ?? Path.Combine(
                Environment.GetFolderPath(
                    Environment.SpecialFolder.UserProfile),
                ".granit-mcp");
        string docsUrl = Environment.GetEnvironmentVariable(
            $"{Prefix}DOCS_URL")
            ?? "https://granit-fx.dev/llms-full.txt";
        string codeIndexUrl = Environment.GetEnvironmentVariable(
            $"{Prefix}CODE_INDEX_URL")
            ?? "https://raw.githubusercontent.com/granit-fx/granit-dotnet/{branch}/.mcp-code-index.json";
        string frontIndexUrl = Environment.GetEnvironmentVariable(
            $"{Prefix}FRONT_INDEX_URL")
            ?? "https://raw.githubusercontent.com/granit-fx/granit-front/{branch}/.mcp-front-index.json";

        string? githubToken = Environment.GetEnvironmentVariable(
            $"{Prefix}GITHUB_TOKEN");
        string? gitlabToken = Environment.GetEnvironmentVariable(
            $"{Prefix}GITLAB_TOKEN");
        string? gitlabHost = NormalizeHost(
            Environment.GetEnvironmentVariable($"{Prefix}GITLAB_HOST"));
        string reposFile = Environment.GetEnvironmentVariable(
            $"{Prefix}REPOS_FILE")
            ?? Path.Combine(dataDir, "repos.json");

        return new GranitMcpConfig(
            logLevel, refreshHours, dataDir,
            docsUrl, codeIndexUrl, frontIndexUrl,
            githubToken, gitlabToken, gitlabHost, reposFile);
    }

    /// <summary>Strips scheme and trailing slash so the host can be slotted into URLs.</summary>
    private static string? NormalizeHost(string? host)
    {
        if (string.IsNullOrWhiteSpace(host))
        {
            return null;
        }

        string trimmed = host.Trim();
        int scheme = trimmed.IndexOf("://", StringComparison.Ordinal);
        if (scheme >= 0)
        {
            trimmed = trimmed[(scheme + 3)..];
        }

        return trimmed.TrimEnd('/');
    }

    private static T ParseEnum<T>(string key, T defaultValue)
        where T : struct, Enum
    {
        string? value = Environment.GetEnvironmentVariable(key);
        return Enum.TryParse<T>(value, ignoreCase: true, out T result)
            ? result
            : defaultValue;
    }

    private static int ParseInt(string key, int defaultValue)
    {
        string? value = Environment.GetEnvironmentVariable(key);
        return int.TryParse(value, out int result) ? result : defaultValue;
    }
}
