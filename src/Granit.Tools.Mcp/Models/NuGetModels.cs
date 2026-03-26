using System.Text.Json.Serialization;

namespace Granit.Tools.Mcp.Models;

// NuGet Search API
public sealed record NuGetSearchResponse(
    [property: JsonPropertyName("totalHits")] int TotalHits,
    [property: JsonPropertyName("data")] List<NuGetSearchPackage> Data);

public sealed record NuGetSearchPackage(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("version")] string Version,
    [property: JsonPropertyName("description")] string? Description,
    [property: JsonPropertyName("totalDownloads")] long TotalDownloads,
    [property: JsonPropertyName("authors")] List<string> Authors,
    [property: JsonPropertyName("tags")] List<string> Tags);

// NuGet Registration API
public sealed record RegistrationIndex(
    [property: JsonPropertyName("count")] int Count,
    [property: JsonPropertyName("items")] List<RegistrationPage> Items);

public sealed record RegistrationPage(
    [property: JsonPropertyName("@id")] string Url,
    [property: JsonPropertyName("items")] List<RegistrationLeaf>? Items);

public sealed record RegistrationLeaf(
    [property: JsonPropertyName("catalogEntry")] CatalogEntry CatalogEntry);

public sealed record CatalogEntry(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("version")] string Version,
    [property: JsonPropertyName("description")] string? Description,
    [property: JsonPropertyName("authors")] string? Authors,
    [property: JsonPropertyName("licenseExpression")] string? LicenseExpression,
    [property: JsonPropertyName("licenseUrl")] string? LicenseUrl,
    [property: JsonPropertyName("projectUrl")] string? ProjectUrl,
    [property: JsonPropertyName("tags")] List<string>? Tags,
    [property: JsonPropertyName("dependencyGroups")] List<DependencyGroup>? DependencyGroups,
    [property: JsonPropertyName("listed")] bool? Listed,
    [property: JsonPropertyName("published")] string? Published);

public sealed record DependencyGroup(
    [property: JsonPropertyName("targetFramework")] string TargetFramework,
    [property: JsonPropertyName("dependencies")] List<NuGetDependency>? Dependencies);

public sealed record NuGetDependency(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("range")] string Range);
