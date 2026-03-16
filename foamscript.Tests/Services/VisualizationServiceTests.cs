using FluentAssertions;
using foamscript.Models;
using foamscript.Services;
using Moq;

namespace foamscript.Tests.Services;

public class VisualizationServiceTests
{
    private readonly Mock<IProcessExecutor> _mockExecutor = new();

    [Fact]
    public void GenerateSliceVisualization_PostProcessFails_ReturnsNull()
    {
        _mockExecutor
            .Setup(e => e.Execute("postProcess", It.IsAny<string>()))
            .Returns(new ProcessResult { ExitCode = 1, Output = "error" });

        var caseDir = Path.Combine(Path.GetTempPath(), "viz_fail_" + Guid.NewGuid());
        Directory.CreateDirectory(Path.Combine(caseDir, "system"));

        try
        {
            var svc = new VisualizationService(_mockExecutor.Object);
            var result = svc.GenerateSliceVisualization(caseDir, "/fake/output");

            result.Should().BeNull();
        }
        finally
        {
            Directory.Delete(caseDir, true);
        }
    }

    [Fact]
    public void GenerateSliceVisualization_WritesPostProcessDict()
    {
        var caseDir = Path.Combine(Path.GetTempPath(), "viz_test_" + Guid.NewGuid());
        var systemDir = Path.Combine(caseDir, "system");
        Directory.CreateDirectory(systemDir);

        string? capturedContent = null;

        _mockExecutor
            .Setup(e => e.Execute("postProcess", It.IsAny<string>()))
            .Callback<string, string>((_, _) =>
            {
                // Capture the dict content while it exists (before finally cleanup)
                var dictPath = Path.Combine(systemDir, "surfaceSampling");
                if (File.Exists(dictPath))
                    capturedContent = File.ReadAllText(dictPath);
            })
            .Returns(new ProcessResult { ExitCode = 1 });

        try
        {
            var svc = new VisualizationService(_mockExecutor.Object);
            svc.GenerateSliceVisualization(caseDir, "/fake/output");

            capturedContent.Should().NotBeNull("service should write surfaceSampling dict before calling postProcess");
            capturedContent.Should().Contain("cuttingPlane");
            capturedContent.Should().Contain("(p U)");
            capturedContent.Should().Contain("surfaceFormat   raw", "should use raw format for simple text parsing");
        }
        finally
        {
            Directory.Delete(caseDir, true);
        }
    }

    [Fact]
    public void GenerateSliceVisualization_CaseDirMissing_ReturnsNull()
    {
        var svc = new VisualizationService(_mockExecutor.Object);
        var result = svc.GenerateSliceVisualization("/nonexistent/path", "/fake/output");

        result.Should().BeNull();
    }

    [Fact]
    public void GenerateSliceVisualization_CallsPostProcessWithCorrectArgs()
    {
        var caseDir = Path.Combine(Path.GetTempPath(), "viz_args_" + Guid.NewGuid());
        Directory.CreateDirectory(Path.Combine(caseDir, "system"));

        _mockExecutor
            .Setup(e => e.Execute("postProcess", It.IsAny<string>()))
            .Returns(new ProcessResult { ExitCode = 1 });

        try
        {
            var svc = new VisualizationService(_mockExecutor.Object);
            svc.GenerateSliceVisualization(caseDir, "/fake/output");

            _mockExecutor.Verify(e => e.Execute("postProcess",
                It.Is<string>(args =>
                    args.Contains($"-case {caseDir}") &&
                    args.Contains("-func surfaceSampling") &&
                    args.Contains("-latestTime"))),
                Times.Once);
        }
        finally
        {
            Directory.Delete(caseDir, true);
        }
    }

    [Fact]
    public void GenerateSliceVisualization_CleansSurfaceSamplingDict()
    {
        var caseDir = Path.Combine(Path.GetTempPath(), "viz_clean_" + Guid.NewGuid());
        var systemDir = Path.Combine(caseDir, "system");
        Directory.CreateDirectory(systemDir);

        _mockExecutor
            .Setup(e => e.Execute("postProcess", It.IsAny<string>()))
            .Returns(new ProcessResult { ExitCode = 1 });

        try
        {
            var svc = new VisualizationService(_mockExecutor.Object);
            svc.GenerateSliceVisualization(caseDir, "/fake/output");

            var dictPath = Path.Combine(systemDir, "surfaceSampling");
            File.Exists(dictPath).Should().BeFalse("service should clean up surfaceSampling dict after use");
        }
        finally
        {
            Directory.Delete(caseDir, true);
        }
    }
}
