using Shouldly;

namespace Granit.Tools.Mcp.Tests;

public sealed class GranitMcpConfigTests
{
    [Fact]
    public void FromEnvironment_ReturnsDefaults()
    {
        var config = GranitMcpConfig.FromEnvironment();

        config.LogLevel.ShouldBe(Microsoft.Extensions.Logging.LogLevel.Information);
        config.RefreshHours.ShouldBe(4);
        config.DocsUrl.ShouldBe("https://granit-fx.dev/llms-full.txt");
        config.CodeIndexUrl.ShouldContain("{branch}");
        config.FrontIndexUrl.ShouldContain("{branch}");
        config.DataDir.ShouldEndWith(".granit-mcp");
    }

    [Fact]
    public void FromEnvironment_ReadsEnvVars()
    {
        Environment.SetEnvironmentVariable("GRANIT_MCP_REFRESH_HOURS", "8");
        Environment.SetEnvironmentVariable("GRANIT_MCP_LOG_LEVEL", "Debug");

        try
        {
            var config = GranitMcpConfig.FromEnvironment();

            config.RefreshHours.ShouldBe(8);
            config.LogLevel.ShouldBe(Microsoft.Extensions.Logging.LogLevel.Debug);
        }
        finally
        {
            Environment.SetEnvironmentVariable("GRANIT_MCP_REFRESH_HOURS", null);
            Environment.SetEnvironmentVariable("GRANIT_MCP_LOG_LEVEL", null);
        }
    }

    [Fact]
    public void FromEnvironment_InvalidIntFallsBackToDefault()
    {
        Environment.SetEnvironmentVariable("GRANIT_MCP_REFRESH_HOURS", "notanumber");

        try
        {
            var config = GranitMcpConfig.FromEnvironment();
            config.RefreshHours.ShouldBe(4);
        }
        finally
        {
            Environment.SetEnvironmentVariable("GRANIT_MCP_REFRESH_HOURS", null);
        }
    }
}
