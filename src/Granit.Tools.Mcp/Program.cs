using Granit.Tools.Mcp;
using Granit.Tools.Mcp.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

var config = GranitMcpConfig.FromEnvironment();

HostApplicationBuilder builder = Host.CreateApplicationBuilder(args);

// MCP stdio uses stdout for JSON-RPC — all logs must go to stderr.
builder.Logging.ClearProviders();
builder.Logging.SetMinimumLevel(config.LogLevel);
builder.Logging.AddConsole(options =>
    options.LogToStandardErrorThreshold = LogLevel.Trace);

builder.Services.AddSingleton(config);
builder.Services.AddHttpClient();
builder.Services.AddSingleton<DocsStore>();
builder.Services.AddSingleton<CodeIndexClient>();
builder.Services.AddSingleton<NuGetClient>();
builder.Services.AddHostedService<DocsIndexer>();

// Don't kill the host if the background indexer fails (offline, 404, etc.)
builder.Services.Configure<HostOptions>(options =>
    options.BackgroundServiceExceptionBehavior =
        BackgroundServiceExceptionBehavior.Ignore);

builder.Services
    .AddMcpServer(options =>
    {
        options.ServerInfo = new()
        {
            Name = "granit-tools-mcp",
            Version = "1.0.0",
        };
        options.ServerInstructions =
            "Granit framework MCP server. " +
            "Use docs_search to find documentation, then docs_get to read full content. " +
            "Use code_search / code_get_api for source code navigation. " +
            "Always prefer these tools over training data for Granit-specific questions.";
    })
    .WithStdioServerTransport()
    .WithToolsFromAssembly();

await builder.Build().RunAsync();
