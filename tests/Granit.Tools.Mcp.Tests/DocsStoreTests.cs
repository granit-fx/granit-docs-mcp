using Granit.Tools.Mcp.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Shouldly;

namespace Granit.Tools.Mcp.Tests;

public sealed class DocsStoreTests : IDisposable
{
    private readonly DocsStore _store;

    private const string SampleMarkdown = """
        <SYSTEM>This is the full developer documentation for Granit</SYSTEM>

        # Crypto-Shredding: GDPR Erasure Without Deleting a Single Row

        > Destroy the encryption key, not the data.

        Your DPO sends you a ticket. You open your ORM, write a DELETE.

        ## The impossible triangle

        GDPR Article 17 demands erasure. ISO 27001 demands immutable audit trails.

        ```csharp
        public sealed class Patient : AuditedAggregateRoot
        {
            [Encrypted(KeyIsolation = true)]
            public string MedicalNotes { get; private set; }
        }
        ```

        # Isolated DbContext Per Module

        Every module maintains its own DbContext containing only entities it owns.

        ## Why isolation matters

        This prevents cross-module table access and makes service extraction mechanical.

        # Getting Started with CRUD in 10 Minutes

        A quick-start guide to building your first Granit endpoint.

        ## Create the project

        ```csharp
        var builder = WebApplication.CreateBuilder(args);
        builder.AddGranit(g => g.AddEssentials());
        ```
        """;

    public DocsStoreTests()
    {
        GranitMcpConfig config = new(
            Microsoft.Extensions.Logging.LogLevel.Information,
            4,
            Path.Combine(Path.GetTempPath(), $"granit-mcp-test-{Guid.NewGuid():N}"),
            "https://granit-fx.dev/llms-full.txt",
            "unused",
            "unused");

        _store = new DocsStore(config, NullLogger<DocsStore>.Instance);
        _store.Index(SampleMarkdown);
    }

    [Fact]
    public void Index_ParsesArticlesByH1()
    {
        // 3 H1 headings in sample → 3 articles
        DocsStore store = _store;
        DocArticle? first = store.GetById("doc-0");
        DocArticle? second = store.GetById("doc-1");
        DocArticle? third = store.GetById("doc-2");

        first.ShouldNotBeNull();
        first.Title.ShouldBe("Crypto-Shredding: GDPR Erasure Without Deleting a Single Row");

        second.ShouldNotBeNull();
        second.Title.ShouldBe("Isolated DbContext Per Module");

        third.ShouldNotBeNull();
        third.Title.ShouldBe("Getting Started with CRUD in 10 Minutes");
    }

    [Fact]
    public void Index_SkipsSystemTag()
    {
        DocArticle? first = _store.GetById("doc-0");
        first.ShouldNotBeNull();
        first.Content.ShouldNotContain("<SYSTEM>");
    }

    [Fact]
    public void Index_InfersCategories()
    {
        DocArticle? crypto = _store.GetById("doc-0");
        DocArticle? guide = _store.GetById("doc-2");

        crypto.ShouldNotBeNull();
        crypto.Category.ShouldBe("compliance");

        guide.ShouldNotBeNull();
        guide.Category.ShouldBe("guide");
    }

    [Fact]
    public void Search_FindsMatchingArticles()
    {
        List<DocSearchResult> results = _store.Search("GDPR erasure");
        results.ShouldNotBeEmpty();
        results[0].Title.ShouldContain("Crypto-Shredding");
    }

    [Fact]
    public void Search_ReturnsEmptyForNoMatch()
    {
        List<DocSearchResult> results = _store.Search("xyznonexistent");
        results.ShouldBeEmpty();
    }

    [Fact]
    public void Search_RespectsLimit()
    {
        List<DocSearchResult> results = _store.Search("module", 1);
        results.Count.ShouldBeLessThanOrEqualTo(1);
    }

    [Fact]
    public void Search_IgnoresShortTerms()
    {
        List<DocSearchResult> results = _store.Search("a b c");
        results.ShouldBeEmpty();
    }

    [Fact]
    public void GetById_ReturnsNullForMissing()
    {
        DocArticle? result = _store.GetById("doc-999");
        result.ShouldBeNull();
    }

    [Fact]
    public void GetById_ReturnsFullContent()
    {
        DocArticle? article = _store.GetById("doc-0");
        article.ShouldNotBeNull();
        article.Content.ShouldContain("impossible triangle");
        article.Content.ShouldContain("Patient");
    }

    [Fact]
    public void ListByCategory_FiltersCorrectly()
    {
        List<DocSearchResult> compliance = _store.ListByCategory("compliance");
        compliance.ShouldNotBeEmpty();
        compliance.ShouldAllBe(r => r.Category == "compliance");
    }

    [Fact]
    public void IsReady_TrueAfterIndexing()
    {
        _store.IsReady.ShouldBeTrue();
    }

    [Fact]
    public void EnsureReadyOrStatus_ReturnsNullWhenReady()
    {
        _store.EnsureReadyOrStatus().ShouldBeNull();
    }

    public void Dispose()
    {
        _store.Dispose();
    }
}
