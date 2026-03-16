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
        private readonly OpenFoamEnvironment _openFoamEnvironment;
        private readonly EnvironmentService _service;

        private const string BashrcPath = "/usr/lib/openfoam/openfoam2512/etc/bashrc";

        public EnvironmentServiceTests()
        {
            _mockProcessExecutor = new Mock<IProcessExecutor>();
            _openFoamEnvironment = new OpenFoamEnvironment(_mockProcessExecutor.Object);
            _service = new EnvironmentService(_mockProcessExecutor.Object, _openFoamEnvironment);
        }

        [Fact]
        public void ValidateEnvironment_WhenAllChecksPass_ReturnsValid()
        {
            SetupFullyValidEnvironment();

            var result = _service.ValidateEnvironment();

            result.IsValid.Should().BeTrue();
            result.TotalChecks.Should().Be(result.PassedChecks);
            result.OpenFoamVersion.Should().Be("v2512");
            result.Groups.Should().NotBeEmpty();
        }

        [Fact]
        public void ValidateEnvironment_WhenAllChecksPass_HasCorrectGroupCount()
        {
            SetupFullyValidEnvironment();

            var result = _service.ValidateEnvironment();

            // OpenFOAM, Meshing, Solver, Parallel, Geometry, Visualization, Python
            result.Groups.Should().HaveCount(7);
        }

        [Fact]
        public void ValidateEnvironment_WhenNoInstallationFound_ReturnsInvalidWithHint()
        {
            // No existing config
            // No installations found
            _mockProcessExecutor
                .Setup(x => x.Execute("bash", It.Is<string>(s => s.Contains("find /usr/lib/openfoam"))))
                .Returns(new ProcessResult { ExitCode = 0, Output = "" });

            var result = _service.ValidateEnvironment();

            result.IsValid.Should().BeFalse();
            result.Groups.Should().HaveCount(1);
            result.Groups[0].Name.Should().Be("OpenFOAM");
            result.Groups[0].Checks[0].Passed.Should().BeFalse();
            result.Groups[0].Checks[0].InstallHint.Should().Contain("openfoam.org");
        }

        [Fact]
        public void ValidateEnvironment_WhenBashrcSourceFails_ReturnsInvalidEarly()
        {
            // Installation detected
            _mockProcessExecutor
                .Setup(x => x.Execute("bash", It.Is<string>(s => s.Contains("find /usr/lib/openfoam"))))
                .Returns(new ProcessResult { ExitCode = 0, Output = "/usr/lib/openfoam/openfoam2512\n" });
            _mockProcessExecutor
                .Setup(x => x.Execute("bash", It.Is<string>(s => s.Contains("find /usr/lib/openfoam/openfoam2512") && s.Contains("bashrc"))))
                .Returns(new ProcessResult { ExitCode = 0, Output = BashrcPath + "\n" });

            // Source fails
            _mockProcessExecutor
                .Setup(x => x.Execute("bash", It.Is<string>(s => s.Contains("source") && s.Contains("env"))))
                .Returns(new ProcessResult { ExitCode = 1, Output = "", Error = "source failed" });

            var result = _service.ValidateEnvironment();

            result.IsValid.Should().BeFalse();
            var bashrcCheck = result.Groups[0].Checks.FirstOrDefault(c => c.Name == "Bashrc sourced");
            bashrcCheck.Should().NotBeNull();
            bashrcCheck!.Passed.Should().BeFalse();
        }

        [Fact]
        public void ValidateEnvironment_WhenToolMissing_ReturnsInvalidWithInstallHint()
        {
            SetupFullyValidEnvironment();

            // Override: gmsh not found
            _mockProcessExecutor
                .Setup(x => x.Execute("bash", It.Is<string>(s => s.Contains("which gmsh"))))
                .Returns(new ProcessResult { ExitCode = 1, Output = "" });

            var result = _service.ValidateEnvironment();

            result.IsValid.Should().BeFalse();
            var geometryGroup = result.Groups.First(g => g.Name == "Geometry Processing");
            var gmshCheck = geometryGroup.Checks.First(c => c.Name == "gmsh");
            gmshCheck.Passed.Should().BeFalse();
            gmshCheck.InstallHint.Should().Be("sudo apt install gmsh");
        }

        [Fact]
        public void ValidateEnvironment_WhenPvpythonMissing_ReturnsInvalid()
        {
            SetupFullyValidEnvironment();

            // Override: pvpython not found
            _mockProcessExecutor
                .Setup(x => x.Execute("bash", It.Is<string>(s => s.Contains("which pvpython"))))
                .Returns(new ProcessResult { ExitCode = 1, Output = "" });

            var result = _service.ValidateEnvironment();

            result.IsValid.Should().BeFalse();
            var vizGroup = result.Groups.First(g => g.Name == "Flow Visualization");
            var pvCheck = vizGroup.Checks.First(c => c.Name == "pvpython");
            pvCheck.Passed.Should().BeFalse();
            pvCheck.InstallHint.Should().Be("sudo apt install paraview");
        }

        [Fact]
        public void ValidateEnvironment_WhenMatplotlibMissing_ReturnsInvalid()
        {
            SetupFullyValidEnvironment();

            // Override: matplotlib import fails
            _mockProcessExecutor
                .Setup(x => x.Execute("bash", It.Is<string>(s => s.Contains("import matplotlib"))))
                .Returns(new ProcessResult { ExitCode = 1, Output = "" });

            var result = _service.ValidateEnvironment();

            result.IsValid.Should().BeFalse();
            var pyGroup = result.Groups.First(g => g.Name == "Python Libraries");
            var matCheck = pyGroup.Checks.First(c => c.Name == "matplotlib");
            matCheck.Passed.Should().BeFalse();
            matCheck.InstallHint.Should().Be("pip3 install matplotlib");
        }

        [Fact]
        public void ValidateEnvironment_PassedChecksCountsCorrectly()
        {
            SetupFullyValidEnvironment();

            // Override: 2 tools missing
            _mockProcessExecutor
                .Setup(x => x.Execute("bash", It.Is<string>(s => s.Contains("which gmsh"))))
                .Returns(new ProcessResult { ExitCode = 1, Output = "" });
            _mockProcessExecutor
                .Setup(x => x.Execute("bash", It.Is<string>(s => s.Contains("which pvpython"))))
                .Returns(new ProcessResult { ExitCode = 1, Output = "" });

            var result = _service.ValidateEnvironment();

            result.IsValid.Should().BeFalse();
            result.PassedChecks.Should().Be(result.TotalChecks - 2);
        }

        [Fact]
        public void DetectOpenFOAMInstallations_WhenInstallationsExist_ReturnsListOfPaths()
        {
            _mockProcessExecutor
                .Setup(x => x.Execute("bash", It.Is<string>(s => s.Contains("find /usr/lib/openfoam"))))
                .Returns(new ProcessResult
                {
                    ExitCode = 0,
                    Output = "/usr/lib/openfoam/openfoam2512\n/opt/openfoam11\n"
                });

            var result = _service.DetectOpenFOAMInstallations();

            result.Should().HaveCount(2);
            result.Should().Contain("/usr/lib/openfoam/openfoam2512");
            result.Should().Contain("/opt/openfoam11");
        }

        [Fact]
        public void DetectOpenFOAMInstallations_WhenNoInstallations_ReturnsEmptyList()
        {
            _mockProcessExecutor
                .Setup(x => x.Execute("bash", It.Is<string>(s => s.Contains("find /usr/lib/openfoam"))))
                .Returns(new ProcessResult { ExitCode = 0, Output = "" });

            var result = _service.DetectOpenFOAMInstallations();

            result.Should().BeEmpty();
        }

        [Fact]
        public void FindBashrcInInstallation_WhenBashrcExists_ReturnsPath()
        {
            _mockProcessExecutor
                .Setup(x => x.Execute("bash", It.Is<string>(s => s.Contains("find /usr/lib/openfoam/openfoam2512") && s.Contains("bashrc"))))
                .Returns(new ProcessResult
                {
                    ExitCode = 0,
                    Output = "/usr/lib/openfoam/openfoam2512/etc/bashrc\n"
                });

            var result = _service.FindBashrcInInstallation("/usr/lib/openfoam/openfoam2512");

            result.Should().Be("/usr/lib/openfoam/openfoam2512/etc/bashrc");
        }

        [Fact]
        public void FindBashrcInInstallation_WhenBashrcNotFound_ReturnsNull()
        {
            _mockProcessExecutor
                .Setup(x => x.Execute("bash", It.Is<string>(s => s.Contains("find /opt/openfoam11") && s.Contains("bashrc"))))
                .Returns(new ProcessResult { ExitCode = 0, Output = "" });

            var result = _service.FindBashrcInInstallation("/opt/openfoam11");

            result.Should().BeNull();
        }

        [Fact]
        public void CheckToolInEnvironment_WhenToolFound_ReturnsAvailable()
        {
            _mockProcessExecutor
                .Setup(x => x.Execute("bash", It.Is<string>(s => s.Contains("source") && s.Contains("which blockMesh"))))
                .Returns(new ProcessResult { ExitCode = 0, Output = "/opt/openfoam2512/bin/blockMesh\n" });

            var result = _service.CheckToolInEnvironment(BashrcPath, "blockMesh");

            result.IsAvailable.Should().BeTrue();
            result.Path.Should().Be("/opt/openfoam2512/bin/blockMesh");
        }

        [Fact]
        public void CheckToolInEnvironment_WhenToolNotFound_ReturnsUnavailable()
        {
            _mockProcessExecutor
                .Setup(x => x.Execute("bash", It.Is<string>(s => s.Contains("source") && s.Contains("which blockMesh"))))
                .Returns(new ProcessResult { ExitCode = 1, Output = "" });

            var result = _service.CheckToolInEnvironment(BashrcPath, "blockMesh");

            result.IsAvailable.Should().BeFalse();
        }

        /// <summary>
        /// Sets up mocks for a fully passing environment: installation detected, bashrc sourced,
        /// all env vars set, all tools found, all python modules available.
        /// </summary>
        private void SetupFullyValidEnvironment()
        {
            // Installation detection (auto-detect path)
            _mockProcessExecutor
                .Setup(x => x.Execute("bash", It.Is<string>(s => s.Contains("find /usr/lib/openfoam"))))
                .Returns(new ProcessResult { ExitCode = 0, Output = "/usr/lib/openfoam/openfoam2512\n" });
            _mockProcessExecutor
                .Setup(x => x.Execute("bash", It.Is<string>(s => s.Contains("find /usr/lib/openfoam/openfoam2512") && s.Contains("bashrc"))))
                .Returns(new ProcessResult { ExitCode = 0, Output = BashrcPath + "\n" });

            // Source bashrc + env capture
            _mockProcessExecutor
                .Setup(x => x.Execute("bash", It.Is<string>(s => s.Contains("source") && s.Contains("env"))))
                .Returns(new ProcessResult
                {
                    ExitCode = 0,
                    Output = "WM_PROJECT_DIR=/usr/lib/openfoam/openfoam2512\n" +
                             "WM_PROJECT_VERSION=v2512\n" +
                             "FOAM_APPBIN=/usr/lib/openfoam/openfoam2512/platforms/linux64GccDPInt32Opt/bin\n" +
                             "FOAM_LIBBIN=/usr/lib/openfoam/openfoam2512/platforms/linux64GccDPInt32Opt/lib\n" +
                             "PATH=/usr/lib/openfoam/openfoam2512/bin:/usr/bin\n"
                });

            // All tools found (match "source ... && which <tool>" pattern)
            string[] allTools = {
                "blockMesh", "snappyHexMesh", "surfaceFeatureExtract", "surfaceOrient",
                "surfaceCheck", "decomposePar", "reconstructParMesh", "checkMesh",
                "simpleFoam", "pimpleFoam", "reconstructPar",
                "mpirun", "gmsh", "pvpython"
            };

            foreach (var tool in allTools)
            {
                _mockProcessExecutor
                    .Setup(x => x.Execute("bash", It.Is<string>(s => s.Contains($"which {tool}"))))
                    .Returns(new ProcessResult { ExitCode = 0, Output = $"/usr/bin/{tool}\n" });
            }

            // Python modules
            _mockProcessExecutor
                .Setup(x => x.Execute("bash", It.Is<string>(s => s.Contains("import matplotlib"))))
                .Returns(new ProcessResult { ExitCode = 0, Output = "" });
            _mockProcessExecutor
                .Setup(x => x.Execute("bash", It.Is<string>(s => s.Contains("import numpy"))))
                .Returns(new ProcessResult { ExitCode = 0, Output = "" });
        }
    }
}
