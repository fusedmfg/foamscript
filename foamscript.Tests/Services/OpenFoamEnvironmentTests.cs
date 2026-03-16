using System.Text.Json;
using Xunit;
using FluentAssertions;
using Moq;
using foamscript.Models;
using foamscript.Services;

namespace foamscript.Tests.Services
{
    public class OpenFoamEnvironmentTests
    {
        [Fact]
        public void CaptureEnvironment_ParsesEnvOutput_IntoDict()
        {
            var mockExecutor = new Mock<IProcessExecutor>();
            mockExecutor
                .Setup(x => x.Execute("bash", It.Is<string>(s => s.Contains("source") && s.Contains("env"))))
                .Returns(new ProcessResult
                {
                    ExitCode = 0,
                    Output = "WM_PROJECT_DIR=/opt/openfoam\nFOAM_APPBIN=/opt/openfoam/bin\nPATH=/opt/openfoam/bin:/usr/bin\nHOME=/home/user\n"
                });

            var env = new OpenFoamEnvironment(mockExecutor.Object);
            var result = env.CaptureEnvironment("/path/to/bashrc");

            result.Should().ContainKey("WM_PROJECT_DIR");
            result!["WM_PROJECT_DIR"].Should().Be("/opt/openfoam");
            result.Should().ContainKey("FOAM_APPBIN");
            result.Should().ContainKey("PATH");
        }

        [Fact]
        public void CaptureEnvironment_WhenSourceFails_ReturnsNull()
        {
            var mockExecutor = new Mock<IProcessExecutor>();
            mockExecutor
                .Setup(x => x.Execute("bash", It.IsAny<string>()))
                .Returns(new ProcessResult { ExitCode = 1, Output = "", Error = "bash: no such file" });

            var env = new OpenFoamEnvironment(mockExecutor.Object);
            var result = env.CaptureEnvironment("/bad/path/bashrc");

            result.Should().BeNull();
        }

        [Fact]
        public void LoadConfig_WhenFileExists_ReturnsConfig()
        {
            var tempDir = Path.Combine(Path.GetTempPath(), $"foamscript_test_{Guid.NewGuid():N}");
            Directory.CreateDirectory(tempDir);
            var configPath = Path.Combine(tempDir, "config.json");

            var config = new FoamScriptConfig
            {
                OpenFoamBashrc = "/usr/lib/openfoam/openfoam2512/etc/bashrc",
                OpenFoamVersion = "v2512",
                ConfiguredAt = DateTime.UtcNow
            };
            File.WriteAllText(configPath, JsonSerializer.Serialize(config, FoamScriptConfig.JsonOptions));

            try
            {
                var loaded = OpenFoamEnvironment.LoadConfig(configPath);
                loaded.Should().NotBeNull();
                loaded!.OpenFoamBashrc.Should().Be("/usr/lib/openfoam/openfoam2512/etc/bashrc");
            }
            finally
            {
                Directory.Delete(tempDir, true);
            }
        }

        [Fact]
        public void LoadConfig_WhenFileMissing_ReturnsNull()
        {
            var result = OpenFoamEnvironment.LoadConfig("/nonexistent/config.json");
            result.Should().BeNull();
        }

        [Fact]
        public void SaveConfig_CreatesDirectoryAndWritesFile()
        {
            var tempDir = Path.Combine(Path.GetTempPath(), $"foamscript_test_{Guid.NewGuid():N}");
            var configPath = Path.Combine(tempDir, "config.json");

            try
            {
                var config = new FoamScriptConfig
                {
                    OpenFoamBashrc = "/path/to/bashrc",
                    OpenFoamVersion = "v2512",
                    ConfiguredAt = DateTime.UtcNow
                };

                OpenFoamEnvironment.SaveConfig(config, configPath);

                File.Exists(configPath).Should().BeTrue();
                var loaded = JsonSerializer.Deserialize<FoamScriptConfig>(
                    File.ReadAllText(configPath), FoamScriptConfig.JsonOptions);
                loaded!.OpenFoamBashrc.Should().Be("/path/to/bashrc");
            }
            finally
            {
                if (Directory.Exists(tempDir))
                    Directory.Delete(tempDir, true);
            }
        }
    }
}
