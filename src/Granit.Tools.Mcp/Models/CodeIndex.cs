using System.Text.Json.Serialization;

namespace Granit.Tools.Mcp.Models;

public sealed record CodeIndex(
    [property: JsonPropertyName("repo")] string Repo,
    [property: JsonPropertyName("projectGraph")] List<ProjectNode> ProjectGraph,
    [property: JsonPropertyName("symbols")] List<CodeSymbol> Symbols);

public sealed record ProjectNode(
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("deps")] List<string> Deps,
    [property: JsonPropertyName("framework")] string Framework);

public sealed record CodeSymbol(
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("fqn")] string Fqn,
    [property: JsonPropertyName("kind")] string Kind,
    [property: JsonPropertyName("project")] string Project,
    [property: JsonPropertyName("file")] string File,
    [property: JsonPropertyName("line")] int? Line,
    [property: JsonPropertyName("members")] List<CodeMember> Members);

public sealed record CodeMember(
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("kind")] string Kind,
    [property: JsonPropertyName("signature")] string Signature,
    [property: JsonPropertyName("returnType")] string? ReturnType);
