using Xunit;
using FluentAssertions;
using Moq;
using foamscript.Services;
using foamscript.Models;

namespace foamscript.Tests.Services
{
    public class EnvironmentServiceTests
    {
        private readonly Mock<IProcessExecutor> _mockProcessExecutor;
        private readonly EnvironmentService _service;

        public EnvironmentServiceTests()
        {
            _mockProcessExecutor = new Mock<IProcessExecutor>();
            _service = new EnvironmentService(_mockProcessExecutor.Object);
        }

        [Fact]
        public void DetectOpenFOAMVersion_WhenVersionCommandSucceeds_ReturnsVersion()
        {
            // Arrange
            _mockProcessExecutor
                .Setup(x => x.Execute("bash", "-c \"echo $WM_PROJECT_VERSION\""))
                .Returns(new ProcessResult { ExitCode = 0, Output = "v2512\n" });

            // Act
            var result = _service.DetectOpenFOAMVersion();

            // Assert
            result.Should().Be("v2512");
        }

        [Fact]
        public void DetectOpenFOAMVersion_WhenVersionNotSet_ReturnsNull()
        {
            // Arrange
            _mockProcessExecutor
                .Setup(x => x.Execute("bash", "-c \"echo $WM_PROJECT_VERSION\""))
                .Returns(new ProcessResult { ExitCode = 0, Output = "\n" });

            // Act
            var result = _service.DetectOpenFOAMVersion();

            // Assert
            result.Should().BeNull();
        }

        [Fact]
        public void CheckEnvironmentVariable_WhenVariableExists_ReturnsTrue()
        {
            // Arrange
            _mockProcessExecutor
                .Setup(x => x.Execute("bash", "-c \"echo $WM_PROJECT_DIR\""))
                .Returns(new ProcessResult { ExitCode = 0, Output = "/opt/openfoam2512\n" });

            // Act
            var result = _service.CheckEnvironmentVariable("WM_PROJECT_DIR");

            // Assert
            result.IsValid.Should().BeTrue();
            result.Value.Should().Be("/opt/openfoam2512");
        }

        [Fact]
        public void CheckEnvironmentVariable_WhenVariableNotSet_ReturnsFalse()
        {
            // Arrange
            _mockProcessExecutor
                .Setup(x => x.Execute("bash", "-c \"echo $WM_PROJECT_DIR\""))
                .Returns(new ProcessResult { ExitCode = 0, Output = "\n" });

            // Act
            var result = _service.CheckEnvironmentVariable("WM_PROJECT_DIR");

            // Assert
            result.IsValid.Should().BeFalse();
            result.Value.Should().BeNull();
        }

        [Fact]
        public void CheckToolAvailable_WhenToolExists_ReturnsTrue()
        {
            // Arrange
            _mockProcessExecutor
                .Setup(x => x.Execute("which", "blockMesh"))
                .Returns(new ProcessResult { ExitCode = 0, Output = "/opt/openfoam2512/bin/blockMesh\n" });

            // Act
            var result = _service.CheckToolAvailable("blockMesh");

            // Assert
            result.IsAvailable.Should().BeTrue();
            result.Path.Should().Be("/opt/openfoam2512/bin/blockMesh");
        }

        [Fact]
        public void CheckToolAvailable_WhenToolNotFound_ReturnsFalse()
        {
            // Arrange
            _mockProcessExecutor
                .Setup(x => x.Execute("which", "blockMesh"))
                .Returns(new ProcessResult { ExitCode = 1, Output = "" });

            // Act
            var result = _service.CheckToolAvailable("blockMesh");

            // Assert
            result.IsAvailable.Should().BeFalse();
            result.Path.Should().BeNull();
        }

        [Fact]
        public void ValidateEnvironment_WhenAllChecksPass_ReturnsSuccessResult()
        {
            // Arrange
            SetupSuccessfulEnvironment();

            // Act
            var result = _service.ValidateEnvironment();

            // Assert
            result.IsValid.Should().BeTrue();
            result.Version.Should().Be("v2512");
            result.MissingTools.Should().BeEmpty();
            result.MissingVariables.Should().BeEmpty();
            result.OptionalTools.Should().ContainKey("pvpython");
            result.OptionalTools.Should().ContainKey("matplotlib");
        }

        [Fact]
        public void ValidateEnvironment_WhenOptionalToolsMissing_StillReturnsValid()
        {
            // Arrange — all required tools present, but pvpython/python3 missing
            SetupEnvironmentWithMissingOptionalTools();

            // Act
            var result = _service.ValidateEnvironment();

            // Assert — IsValid should still be true
            result.IsValid.Should().BeTrue();
            result.MissingOptionalTools.Should().Contain("pvpython");
            result.MissingOptionalTools.Should().Contain("python3");
        }

        [Fact]
        public void ValidateEnvironment_WhenOpenFOAMNotSourced_ReturnsFailureResult()
        {
            // Arrange - No version detected
            _mockProcessExecutor
                .Setup(x => x.Execute("bash", "-c \"echo $WM_PROJECT_VERSION\""))
                .Returns(new ProcessResult { ExitCode = 0, Output = "\n" });

            // And no installations found
            _mockProcessExecutor
                .Setup(x => x.Execute("bash", "-c \"find /usr/lib/openfoam /opt -maxdepth 3 -type f -path '*/etc/bashrc' 2>/dev/null | xargs dirname | xargs dirname || true\""))
                .Returns(new ProcessResult { ExitCode = 0, Output = "" });

            // Act
            var result = _service.ValidateEnvironment();

            // Assert
            result.IsValid.Should().BeFalse();
            result.Version.Should().BeNull();
            result.ErrorMessage.Should().Contain("OpenFOAM environment not detected");
            result.DetectedInstallations.Should().BeEmpty();
        }

        [Fact]
        public void ValidateEnvironment_WhenToolsMissing_ReturnsFailureWithMissingTools()
        {
            // Arrange
            SetupEnvironmentWithMissingTools();

            // Act
            var result = _service.ValidateEnvironment();

            // Assert
            result.IsValid.Should().BeFalse();
            result.MissingTools.Should().Contain("snappyHexMesh");
            result.MissingTools.Should().Contain("gmsh");
        }

        private void SetupSuccessfulEnvironment()
        {
            // Version
            _mockProcessExecutor
                .Setup(x => x.Execute("bash", "-c \"echo $WM_PROJECT_VERSION\""))
                .Returns(new ProcessResult { ExitCode = 0, Output = "v2512\n" });

            // Environment variables - specific setups
            _mockProcessExecutor
                .Setup(x => x.Execute("bash", "-c \"echo $WM_PROJECT_DIR\""))
                .Returns(new ProcessResult { ExitCode = 0, Output = "/opt/openfoam2512\n" });

            _mockProcessExecutor
                .Setup(x => x.Execute("bash", "-c \"echo $FOAM_APPBIN\""))
                .Returns(new ProcessResult { ExitCode = 0, Output = "/opt/openfoam2512/platforms/linux64GccDPInt32Opt/bin\n" });

            _mockProcessExecutor
                .Setup(x => x.Execute("bash", "-c \"echo $FOAM_LIBBIN\""))
                .Returns(new ProcessResult { ExitCode = 0, Output = "/opt/openfoam2512/platforms/linux64GccDPInt32Opt/lib\n" });

            // Tools
            string[] tools = { "blockMesh", "snappyHexMesh", "surfaceFeatureExtract",
                              "decomposePar", "pimpleFoam", "reconstructPar", "checkMesh", "gmsh" };

            foreach (var tool in tools)
            {
                _mockProcessExecutor
                    .Setup(x => x.Execute("which", tool))
                    .Returns(new ProcessResult { ExitCode = 0, Output = $"/usr/bin/{tool}\n" });
            }

            // Optional tools
            _mockProcessExecutor
                .Setup(x => x.Execute("which", "pvpython"))
                .Returns(new ProcessResult { ExitCode = 0, Output = "/usr/bin/pvpython\n" });
            _mockProcessExecutor
                .Setup(x => x.Execute("which", "python3"))
                .Returns(new ProcessResult { ExitCode = 0, Output = "/usr/bin/python3\n" });
            _mockProcessExecutor
                .Setup(x => x.Execute("python3", "-c \"import matplotlib\""))
                .Returns(new ProcessResult { ExitCode = 0, Output = "" });
            _mockProcessExecutor
                .Setup(x => x.Execute("python3", "-c \"import numpy\""))
                .Returns(new ProcessResult { ExitCode = 0, Output = "" });
        }

        private void SetupEnvironmentWithMissingTools()
        {
            // Version exists
            _mockProcessExecutor
                .Setup(x => x.Execute("bash", "-c \"echo $WM_PROJECT_VERSION\""))
                .Returns(new ProcessResult { ExitCode = 0, Output = "v2512\n" });

            // Environment variables - all present
            _mockProcessExecutor
                .Setup(x => x.Execute("bash", "-c \"echo $WM_PROJECT_DIR\""))
                .Returns(new ProcessResult { ExitCode = 0, Output = "/opt/openfoam2512\n" });

            _mockProcessExecutor
                .Setup(x => x.Execute("bash", "-c \"echo $FOAM_APPBIN\""))
                .Returns(new ProcessResult { ExitCode = 0, Output = "/opt/openfoam2512/platforms/linux64GccDPInt32Opt/bin\n" });

            _mockProcessExecutor
                .Setup(x => x.Execute("bash", "-c \"echo $FOAM_LIBBIN\""))
                .Returns(new ProcessResult { ExitCode = 0, Output = "/opt/openfoam2512/platforms/linux64GccDPInt32Opt/lib\n" });

            // Setup all tools individually
            _mockProcessExecutor
                .Setup(x => x.Execute("which", "blockMesh"))
                .Returns(new ProcessResult { ExitCode = 0, Output = "/usr/bin/blockMesh\n" });

            _mockProcessExecutor
                .Setup(x => x.Execute("which", "snappyHexMesh"))
                .Returns(new ProcessResult { ExitCode = 1, Output = "" });

            _mockProcessExecutor
                .Setup(x => x.Execute("which", "surfaceFeatureExtract"))
                .Returns(new ProcessResult { ExitCode = 0, Output = "/usr/bin/surfaceFeatureExtract\n" });

            _mockProcessExecutor
                .Setup(x => x.Execute("which", "decomposePar"))
                .Returns(new ProcessResult { ExitCode = 0, Output = "/usr/bin/decomposePar\n" });

            _mockProcessExecutor
                .Setup(x => x.Execute("which", "pimpleFoam"))
                .Returns(new ProcessResult { ExitCode = 0, Output = "/usr/bin/pimpleFoam\n" });

            _mockProcessExecutor
                .Setup(x => x.Execute("which", "reconstructPar"))
                .Returns(new ProcessResult { ExitCode = 0, Output = "/usr/bin/reconstructPar\n" });

            _mockProcessExecutor
                .Setup(x => x.Execute("which", "checkMesh"))
                .Returns(new ProcessResult { ExitCode = 0, Output = "/usr/bin/checkMesh\n" });

            _mockProcessExecutor
                .Setup(x => x.Execute("which", "gmsh"))
                .Returns(new ProcessResult { ExitCode = 1, Output = "" });

            // Optional tools — not found (shouldn't affect IsValid)
            _mockProcessExecutor
                .Setup(x => x.Execute("which", "pvpython"))
                .Returns(new ProcessResult { ExitCode = 1, Output = "" });
            _mockProcessExecutor
                .Setup(x => x.Execute("which", "python3"))
                .Returns(new ProcessResult { ExitCode = 1, Output = "" });
        }

        private void SetupEnvironmentWithMissingOptionalTools()
        {
            // All required tools present
            SetupSuccessfulEnvironment();

            // Override optional tools to be missing
            _mockProcessExecutor
                .Setup(x => x.Execute("which", "pvpython"))
                .Returns(new ProcessResult { ExitCode = 1, Output = "" });
            _mockProcessExecutor
                .Setup(x => x.Execute("which", "python3"))
                .Returns(new ProcessResult { ExitCode = 1, Output = "" });
        }

        [Fact]
        public void DetectOpenFOAMInstallations_WhenInstallationsExist_ReturnsListOfPaths()
        {
            // Arrange
            _mockProcessExecutor
                .Setup(x => x.Execute("bash", "-c \"find /usr/lib/openfoam /opt -maxdepth 3 -type f -path '*/etc/bashrc' 2>/dev/null | xargs dirname | xargs dirname || true\""))
                .Returns(new ProcessResult
                {
                    ExitCode = 0,
                    Output = "/usr/lib/openfoam/openfoam2512\n/opt/openfoam11\n"
                });

            // Act
            var result = _service.DetectOpenFOAMInstallations();

            // Assert
            result.Should().HaveCount(2);
            result.Should().Contain("/usr/lib/openfoam/openfoam2512");
            result.Should().Contain("/opt/openfoam11");
        }

        [Fact]
        public void DetectOpenFOAMInstallations_WhenNoInstallations_ReturnsEmptyList()
        {
            // Arrange
            _mockProcessExecutor
                .Setup(x => x.Execute("bash", "-c \"find /usr/lib/openfoam /opt -maxdepth 3 -type f -path '*/etc/bashrc' 2>/dev/null | xargs dirname | xargs dirname || true\""))
                .Returns(new ProcessResult { ExitCode = 0, Output = "" });

            // Act
            var result = _service.DetectOpenFOAMInstallations();

            // Assert
            result.Should().BeEmpty();
        }

        [Fact]
        public void FindBashrcInInstallation_WhenBashrcExists_ReturnsPath()
        {
            // Arrange
            _mockProcessExecutor
                .Setup(x => x.Execute("bash", "-c \"find /usr/lib/openfoam/openfoam2512 -name bashrc -type f -path '*/etc/bashrc' 2>/dev/null | head -1\""))
                .Returns(new ProcessResult
                {
                    ExitCode = 0,
                    Output = "/usr/lib/openfoam/openfoam2512/etc/bashrc\n"
                });

            // Act
            var result = _service.FindBashrcInInstallation("/usr/lib/openfoam/openfoam2512");

            // Assert
            result.Should().Be("/usr/lib/openfoam/openfoam2512/etc/bashrc");
        }

        [Fact]
        public void FindBashrcInInstallation_WhenBashrcNotFound_ReturnsNull()
        {
            // Arrange
            _mockProcessExecutor
                .Setup(x => x.Execute("bash", "-c \"find /opt/openfoam11 -name bashrc -type f -path '*/etc/bashrc' 2>/dev/null | head -1\""))
                .Returns(new ProcessResult { ExitCode = 0, Output = "" });

            // Act
            var result = _service.FindBashrcInInstallation("/opt/openfoam11");

            // Assert
            result.Should().BeNull();
        }

        [Fact]
        public void ValidateEnvironment_WhenNotSourcedButInstallationDetected_ProvidesHelpfulErrorMessage()
        {
            // Arrange - Environment not sourced
            _mockProcessExecutor
                .Setup(x => x.Execute("bash", "-c \"echo $WM_PROJECT_VERSION\""))
                .Returns(new ProcessResult { ExitCode = 0, Output = "\n" });

            // But installation exists
            _mockProcessExecutor
                .Setup(x => x.Execute("bash", "-c \"find /usr/lib/openfoam /opt -maxdepth 3 -type f -path '*/etc/bashrc' 2>/dev/null | xargs dirname | xargs dirname || true\""))
                .Returns(new ProcessResult
                {
                    ExitCode = 0,
                    Output = "/usr/lib/openfoam/openfoam2512\n"
                });

            // And bashrc can be found
            _mockProcessExecutor
                .Setup(x => x.Execute("bash", "-c \"find /usr/lib/openfoam/openfoam2512 -name bashrc -type f -path '*/etc/bashrc' 2>/dev/null | head -1\""))
                .Returns(new ProcessResult
                {
                    ExitCode = 0,
                    Output = "/usr/lib/openfoam/openfoam2512/etc/bashrc\n"
                });

            // Act
            var result = _service.ValidateEnvironment();

            // Assert
            result.IsValid.Should().BeFalse();
            result.DetectedInstallations.Should().Contain("/usr/lib/openfoam/openfoam2512");
            result.DetectedBashrcPath.Should().Be("/usr/lib/openfoam/openfoam2512/etc/bashrc");
            result.ErrorMessage.Should().Contain("OpenFOAM is installed but not sourced");
            result.ErrorMessage.Should().Contain("source /usr/lib/openfoam/openfoam2512/etc/bashrc");
        }
    }
}
