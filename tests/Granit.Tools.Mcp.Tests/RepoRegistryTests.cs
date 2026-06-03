using Granit.Tools.Mcp;
using Granit.Tools.Mcp.Models;
using Granit.Tools.Mcp.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Shouldly;

namespace Granit.Tools.Mcp.Tests;

public sealed class RepoRegistryTests : IDisposable
{
    private readonly List<string> _tempFiles = [];

    [Fact]
    public void Defaults_IncludeDotnetAndFront_WhenNoFile()
    {
        var registry = new RepoRegistry(ConfigFor(MissingFilePath()), NullLogger<RepoRegistry>.Instance);

        registry.All.Select(r => r.Id).ShouldBe(["dotnet", "front"], ignoreOrder: true);

        RepoConfig dotnet = registry.All.Single(r => r.Id == "dotnet");
        dotnet.Kind.ShouldBe(RepoKind.Dotnet);
        dotnet.Provider.ShouldBe(RepoProvider.GitHub);
        dotnet.RawUrlTemplate.ShouldNotBeNull();
        dotnet.RawUrlTemplate.ShouldContain("{branch}");
    }

    [Fact]
    public void LoadsReposFromFile_AugmentingDefaults()
    {
        string path = WriteRepos("""
        [
          { "id": "business", "kind": "dotnet", "provider": "github",
            "project": "granit-fx/granit-business", "private": true },
          { "kind": "front", "provider": "gitlab",
            "host": "gitlab.example.com", "project": "infra/ops-console" }
        ]
        """);

        var registry = new RepoRegistry(ConfigFor(path), NullLogger<RepoRegistry>.Instance);

        registry.All.Select(r => r.Id)
            .ShouldBe(["dotnet", "front", "business", "ops-console"], ignoreOrder: true);

        RepoConfig business = registry.All.Single(r => r.Id == "business");
        business.Provider.ShouldBe(RepoProvider.GitHub);
        business.Private.ShouldBeTrue();
        business.IndexPath.ShouldBe(RepoRegistry.DotnetIndexFile);

        RepoConfig ops = registry.All.Single(r => r.Id == "ops-console");
        ops.Provider.ShouldBe(RepoProvider.GitLab);
        ops.Host.ShouldBe("gitlab.example.com");
        ops.IndexPath.ShouldBe(RepoRegistry.FrontIndexFile);
    }

    [Fact]
    public void FileEntry_OverridesDefault_ByReusingId()
    {
        string path = WriteRepos("""
        [
          { "id": "dotnet", "kind": "dotnet", "provider": "gitlab",
            "host": "gitlab.example.com", "project": "granit/dotnet" }
        ]
        """);

        var registry = new RepoRegistry(ConfigFor(path), NullLogger<RepoRegistry>.Instance);

        RepoConfig dotnet = registry.All.Single(r => r.Id == "dotnet");
        dotnet.Provider.ShouldBe(RepoProvider.GitLab);
        dotnet.RawUrlTemplate.ShouldBeNull();
    }

    [Fact]
    public void GitLabRepo_InheritsGlobalHost_WhenHostOmitted()
    {
        string path = WriteRepos("""
        [
          { "id": "internal", "kind": "dotnet", "provider": "gitlab",
            "project": "granit/backend/internal-api" }
        ]
        """);

        var registry = new RepoRegistry(
            ConfigFor(path, gitlabHost: "gitlab.internal"),
            NullLogger<RepoRegistry>.Instance);

        registry.All.Single(r => r.Id == "internal").Host.ShouldBe("gitlab.internal");
    }

    [Fact]
    public void GitLabRepo_WithoutAnyHost_IsSkipped()
    {
        string path = WriteRepos("""
        [
          { "id": "internal", "kind": "dotnet", "provider": "gitlab",
            "project": "granit/backend/internal-api" }
        ]
        """);

        var registry = new RepoRegistry(ConfigFor(path), NullLogger<RepoRegistry>.Instance);

        registry.All.ShouldNotContain(r => r.Id == "internal");
        registry.All.Select(r => r.Id).ShouldBe(["dotnet", "front"], ignoreOrder: true);
    }

    [Fact]
    public void InvalidKind_IsSkipped()
    {
        string path = WriteRepos("""
        [
          { "id": "weird", "kind": "python", "project": "granit-fx/granit-weird" }
        ]
        """);

        var registry = new RepoRegistry(ConfigFor(path), NullLogger<RepoRegistry>.Instance);

        registry.All.ShouldNotContain(r => r.Id == "weird");
    }

    [Fact]
    public void DerivesId_FromLastProjectSegment_WhenIdOmitted()
    {
        string path = WriteRepos("""
        [
          { "kind": "dotnet", "project": "granit-fx/granit-payments" }
        ]
        """);

        var registry = new RepoRegistry(ConfigFor(path), NullLogger<RepoRegistry>.Instance);

        registry.All.ShouldContain(r => r.Id == "granit-payments");
    }

    [Fact]
    public void Resolve_FiltersById_OrReturnsAll()
    {
        var registry = new RepoRegistry(ConfigFor(MissingFilePath()), NullLogger<RepoRegistry>.Instance);

        registry.Resolve("front").Select(r => r.Id).ShouldBe(["front"]);
        registry.Resolve("FRONT").Select(r => r.Id).ShouldBe(["front"]);
        registry.Resolve(null).Count().ShouldBe(2);
        registry.Resolve("nope").ShouldBeEmpty();
    }

    [Fact]
    public void MalformedFile_FallsBackToDefaults()
    {
        string path = WriteRepos("{ not valid json");

        var registry = new RepoRegistry(ConfigFor(path), NullLogger<RepoRegistry>.Instance);

        registry.All.Select(r => r.Id).ShouldBe(["dotnet", "front"], ignoreOrder: true);
    }

    private static GranitMcpConfig ConfigFor(string reposFile, string? gitlabHost = null) =>
        new(
            LogLevel.Information, 4, "/tmp",
            "https://granit-fx.dev/llms-full.txt",
            "https://raw.githubusercontent.com/granit-fx/granit-dotnet/{branch}/.mcp-code-index.json",
            "https://raw.githubusercontent.com/granit-fx/granit-front/{branch}/.mcp-front-index.json",
            GitHubToken: null, GitLabToken: null, GitLabHost: gitlabHost, ReposFile: reposFile);

    private static string MissingFilePath() =>
        Path.Combine(Path.GetTempPath(), $"granit-mcp-missing-{Guid.NewGuid():N}.json");

    private string WriteRepos(string json)
    {
        string path = Path.Combine(Path.GetTempPath(), $"granit-mcp-repos-{Guid.NewGuid():N}.json");
        File.WriteAllText(path, json);
        _tempFiles.Add(path);
        return path;
    }

    public void Dispose()
    {
        foreach (string path in _tempFiles)
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }
}
