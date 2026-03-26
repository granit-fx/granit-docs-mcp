using System.Text.Json.Serialization;

namespace Granit.Tools.Mcp.Models;

public sealed record FrontIndex(
    [property: JsonPropertyName("repo")] string Repo,
    [property: JsonPropertyName("packages")] List<FrontPackage> Packages);

public sealed record FrontPackage(
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("description")] string Description,
    [property: JsonPropertyName("exports")] List<FrontExport> Exports);

public sealed record FrontExport(
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("kind")] string Kind,
    [property: JsonPropertyName("signature")] string Signature,
    [property: JsonPropertyName("members")] List<CodeMember>? Members);
