namespace Granit.Tools.Mcp.Models;

/// <summary>Schema of a repo's code index — decides how to parse and present it.</summary>
public enum RepoKind
{
    /// <summary>.NET / C# index (<c>.mcp-code-index.json</c>): projects + symbols.</summary>
    Dotnet,

    /// <summary>TypeScript index (<c>.mcp-front-index.json</c>): packages + exports.</summary>
    Front,
}

/// <summary>Where a repo lives — decides the fetch endpoint and auth scheme.</summary>
public enum RepoProvider
{
    /// <summary>GitHub (public raw, or Contents API + bearer token for private).</summary>
    GitHub,

    /// <summary>GitLab (API v4 raw file endpoint + PRIVATE-TOKEN), incl. self-hosted.</summary>
    GitLab,
}

/// <summary>
/// A source repository whose code index can be searched. Built-in defaults
/// cover granit-dotnet/granit-front; users add more (including private GitHub
/// or self-hosted GitLab repos) via <c>repos.json</c>.
/// </summary>
/// <param name="Id">Selector used in the <c>repo</c> tool parameter (e.g. "business").</param>
/// <param name="Kind">Index schema — drives parsing and formatting.</param>
/// <param name="Provider">Hosting provider — drives the fetch endpoint and auth.</param>
/// <param name="Project">
/// GitHub: <c>owner/name</c>. GitLab: full project path <c>group/subgroup/project</c>.
/// </param>
/// <param name="IndexPath">Path of the committed index file inside the repo.</param>
/// <param name="Private">
/// GitHub only: fetch via the authenticated Contents API instead of public raw.
/// </param>
/// <param name="Host">
/// GitLab host (e.g. <c>gitlab.example.com</c>); null for GitHub.
/// </param>
/// <param name="RawUrlTemplate">
/// Optional full URL template with a <c>{branch}</c> placeholder. Honors the
/// legacy <c>GRANIT_MCP_CODE_INDEX_URL</c>/<c>FRONT_INDEX_URL</c> overrides for
/// the built-in GitHub defaults; null for everything else.
/// </param>
public sealed record RepoConfig(
    string Id,
    RepoKind Kind,
    RepoProvider Provider,
    string Project,
    string IndexPath,
    bool Private,
    string? Host = null,
    string? RawUrlTemplate = null);

/// <summary>
/// Tolerant DTO for a <c>repos.json</c> entry. Strings (not enums) so an invalid
/// value skips just that entry instead of failing the whole file.
/// </summary>
public sealed class RepoConfigDto
{
    public string? Id { get; set; }
    public string? Kind { get; set; }
    public string? Provider { get; set; }
    public string? Project { get; set; }
    public string? Host { get; set; }
    public string? IndexPath { get; set; }
    public bool? Private { get; set; }
}
