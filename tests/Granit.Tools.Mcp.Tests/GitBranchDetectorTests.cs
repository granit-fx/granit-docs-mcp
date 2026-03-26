using Granit.Tools.Mcp.Services;
using Shouldly;

namespace Granit.Tools.Mcp.Tests;

public sealed class GitBranchDetectorTests
{
    [Fact]
    public void DetectBranch_ReturnsCurrentBranch()
    {
        // We're running inside a git repo, so this should return a branch name
        string branch = GitBranchDetector.DetectBranch();
        branch.ShouldNotBeNullOrWhiteSpace();
    }

    [Fact]
    public void DetectBranch_ReturnsDevelopForNonGitDir()
    {
        // Run from a temp dir that's not a git repo
        string tempDir = Path.Combine(Path.GetTempPath(), $"no-git-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);

        try
        {
            string originalDir = Directory.GetCurrentDirectory();
            Directory.SetCurrentDirectory(tempDir);

            try
            {
                string branch = GitBranchDetector.DetectBranch();
                branch.ShouldBe("develop");
            }
            finally
            {
                Directory.SetCurrentDirectory(originalDir);
            }
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }
}
