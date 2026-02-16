using Xunit;
using FluentAssertions;
using Moq;
using foamscript.Services;
using foamscript.Models;

namespace foamscript.Tests.Services
{
    public class GeometryServiceTests
    {
        private readonly Mock<IProcessExecutor> _mockProcessExecutor;
        private readonly GeometryService _service;

        public GeometryServiceTests()
        {
            _mockProcessExecutor = new Mock<IProcessExecutor>();
            _service = new GeometryService(_mockProcessExecutor.Object);
        }

        [Fact]
        public void ConvertStepToStl_WhenConversionSucceeds_ReturnsSuccessResult()
        {
            // Arrange
            var inputFile = "/path/to/model.step";
            var outputFile = "/path/to/model.stl";

            _mockProcessExecutor
                .Setup(x => x.Execute("test", "-f /path/to/model.step"))
                .Returns(new ProcessResult { ExitCode = 0, Output = "" });

            _mockProcessExecutor
                .Setup(x => x.Execute("gmsh", It.Is<string>(args =>
                    args.Contains(inputFile) &&
                    args.Contains(outputFile) &&
                    args.Contains("-format stl"))))
                .Returns(new ProcessResult
                {
                    ExitCode = 0,
                    Output = "Info    : Done meshing 3D (Wall 0.5s, CPU 0.48s)\n" +
                             "Info    : 1234 nodes 2468 triangles\n" +
                             "Info    : Writing '/path/to/model.stl'..."
                });

            _mockProcessExecutor
                .Setup(x => x.Execute("test", "-f /path/to/model.stl"))
                .Returns(new ProcessResult { ExitCode = 0, Output = "" });

            // Act
            var result = _service.ConvertStepToStl(inputFile, outputFile);

            // Assert
            result.IsSuccess.Should().BeTrue();
            result.OutputFile.Should().Be(outputFile);
            result.ErrorMessage.Should().BeNull();
        }

        [Fact]
        public void ConvertStepToStl_WhenInputFileDoesNotExist_ReturnsFailure()
        {
            // Arrange
            var inputFile = "/path/to/missing.step";
            var outputFile = "/path/to/model.stl";

            _mockProcessExecutor
                .Setup(x => x.Execute("test", "-f /path/to/missing.step"))
                .Returns(new ProcessResult { ExitCode = 1, Output = "" });

            // Act
            var result = _service.ConvertStepToStl(inputFile, outputFile);

            // Assert
            result.IsSuccess.Should().BeFalse();
            result.ErrorMessage.Should().Contain("Input file not found");
            result.OutputFile.Should().BeNull();
        }

        [Fact]
        public void ConvertStepToStl_WhenGmshFails_ReturnsFailureWithErrorMessage()
        {
            // Arrange
            var inputFile = "/path/to/model.step";
            var outputFile = "/path/to/model.stl";

            _mockProcessExecutor
                .Setup(x => x.Execute("test", "-f /path/to/model.step"))
                .Returns(new ProcessResult { ExitCode = 0, Output = "" });

            _mockProcessExecutor
                .Setup(x => x.Execute("gmsh", It.IsAny<string>()))
                .Returns(new ProcessResult
                {
                    ExitCode = 1,
                    Output = "",
                    Error = "Error   : Could not parse file 'model.step'"
                });

            // Act
            var result = _service.ConvertStepToStl(inputFile, outputFile);

            // Assert
            result.IsSuccess.Should().BeFalse();
            result.ErrorMessage.Should().Contain("gmsh conversion failed");
            result.ErrorMessage.Should().Contain("Could not parse file");
        }

        [Fact]
        public void ConvertStepToStl_WhenOutputFileNotCreated_ReturnsFailure()
        {
            // Arrange
            var inputFile = "/path/to/model.step";
            var outputFile = "/path/to/model.stl";

            _mockProcessExecutor
                .Setup(x => x.Execute("test", "-f /path/to/model.step"))
                .Returns(new ProcessResult { ExitCode = 0, Output = "" });

            _mockProcessExecutor
                .Setup(x => x.Execute("gmsh", It.IsAny<string>()))
                .Returns(new ProcessResult { ExitCode = 0, Output = "Info    : Done meshing" });

            // Output file doesn't exist after gmsh runs
            _mockProcessExecutor
                .Setup(x => x.Execute("test", "-f /path/to/model.stl"))
                .Returns(new ProcessResult { ExitCode = 1, Output = "" });

            // Act
            var result = _service.ConvertStepToStl(inputFile, outputFile);

            // Assert
            result.IsSuccess.Should().BeFalse();
            result.ErrorMessage.Should().Contain("Output file was not created");
        }

        [Fact]
        public void ConvertStepToStl_WithCustomMeshSize_PassesParametersToGmsh()
        {
            // Arrange
            var inputFile = "/path/to/model.step";
            var outputFile = "/path/to/model.stl";
            var meshSize = 0.5;

            _mockProcessExecutor
                .Setup(x => x.Execute("test", "-f /path/to/model.step"))
                .Returns(new ProcessResult { ExitCode = 0, Output = "" });

            _mockProcessExecutor
                .Setup(x => x.Execute("gmsh", It.Is<string>(args =>
                    args.Contains($"-clscale {meshSize}"))))
                .Returns(new ProcessResult { ExitCode = 0, Output = "Info    : Done meshing" });

            _mockProcessExecutor
                .Setup(x => x.Execute("test", "-f /path/to/model.stl"))
                .Returns(new ProcessResult { ExitCode = 0, Output = "" });

            // Act
            var result = _service.ConvertStepToStl(inputFile, outputFile, meshSize);

            // Assert
            result.IsSuccess.Should().BeTrue();
            _mockProcessExecutor.Verify(x => x.Execute("gmsh", It.Is<string>(args =>
                args.Contains($"-clscale {meshSize}"))), Times.Once);
        }

        [Fact]
        public void ConvertStepToStl_WithFeatureAngle_PassesAngleToGmsh()
        {
            // Arrange
            var inputFile = "/path/to/model.step";
            var outputFile = "/path/to/model.stl";
            var featureAngle = 30.0;

            _mockProcessExecutor
                .Setup(x => x.Execute("test", "-f /path/to/model.step"))
                .Returns(new ProcessResult { ExitCode = 0, Output = "" });

            _mockProcessExecutor
                .Setup(x => x.Execute("gmsh", It.Is<string>(args =>
                    args.Contains($"-angle {featureAngle}"))))
                .Returns(new ProcessResult { ExitCode = 0, Output = "Info    : Done meshing" });

            _mockProcessExecutor
                .Setup(x => x.Execute("test", "-f /path/to/model.stl"))
                .Returns(new ProcessResult { ExitCode = 0, Output = "" });

            // Act
            var result = _service.ConvertStepToStl(inputFile, outputFile, meshSize: 1.0, featureAngle: featureAngle);

            // Assert
            result.IsSuccess.Should().BeTrue();
            _mockProcessExecutor.Verify(x => x.Execute("gmsh", It.Is<string>(args =>
                args.Contains($"-angle {featureAngle}"))), Times.Once);
        }

        [Fact]
        public void ConvertStepToStl_WithoutFeatureAngle_DoesNotPassAngleToGmsh()
        {
            // Arrange
            var inputFile = "/path/to/model.step";
            var outputFile = "/path/to/model.stl";

            _mockProcessExecutor
                .Setup(x => x.Execute("test", "-f /path/to/model.step"))
                .Returns(new ProcessResult { ExitCode = 0, Output = "" });

            _mockProcessExecutor
                .Setup(x => x.Execute("gmsh", It.Is<string>(args =>
                    !args.Contains("-angle"))))
                .Returns(new ProcessResult { ExitCode = 0, Output = "Info    : Done meshing" });

            _mockProcessExecutor
                .Setup(x => x.Execute("test", "-f /path/to/model.stl"))
                .Returns(new ProcessResult { ExitCode = 0, Output = "" });

            // Act
            var result = _service.ConvertStepToStl(inputFile, outputFile);

            // Assert
            result.IsSuccess.Should().BeTrue();
            _mockProcessExecutor.Verify(x => x.Execute("gmsh", It.Is<string>(args =>
                !args.Contains("-angle"))), Times.Once);
        }

        [Fact]
        public void ValidateStl_WhenFileIsValidAndWatertight_ReturnsValidResult()
        {
            // Arrange
            var stlFile = "/path/to/model.stl";
            var surfaceCheckOutput = @"
Surface has 0 illegal triangles.
Number of unconnected parts : 1
Number of zones (connected area with consistent normal) : 1
Surface is not self-intersecting
";

            _mockProcessExecutor
                .Setup(x => x.Execute("test", "-f /path/to/model.stl"))
                .Returns(new ProcessResult { ExitCode = 0, Output = "" });

            _mockProcessExecutor
                .Setup(x => x.Execute("surfaceCheck", It.Is<string>(args =>
                    args.Contains(stlFile) && args.Contains("-checkSelfIntersection"))))
                .Returns(new ProcessResult { ExitCode = 0, Output = surfaceCheckOutput });

            // Act
            var result = _service.ValidateStl(stlFile);

            // Assert
            result.IsValid.Should().BeTrue();
            result.IllegalTriangles.Should().Be(0);
            result.UnconnectedParts.Should().Be(1);
            result.NormalZones.Should().Be(1);
            result.IsSelfIntersecting.Should().BeFalse();
            result.IsWatertight.Should().BeTrue();
            result.IsManifold.Should().BeTrue();
        }

        [Fact]
        public void ValidateStl_WhenFileDoesNotExist_ReturnsFailure()
        {
            // Arrange
            var stlFile = "/path/to/missing.stl";

            _mockProcessExecutor
                .Setup(x => x.Execute("test", "-f /path/to/missing.stl"))
                .Returns(new ProcessResult { ExitCode = 1, Output = "" });

            // Act
            var result = _service.ValidateStl(stlFile);

            // Assert
            result.IsValid.Should().BeFalse();
            result.ErrorMessage.Should().Contain("File not found");
        }

        [Fact]
        public void ValidateStl_WhenSurfaceHasIllegalTriangles_ReturnsInvalid()
        {
            // Arrange
            var stlFile = "/path/to/model.stl";
            var surfaceCheckOutput = @"
Surface has 5 illegal triangles.
Number of unconnected parts : 1
Number of zones (connected area with consistent normal) : 1
";

            _mockProcessExecutor
                .Setup(x => x.Execute("test", "-f /path/to/model.stl"))
                .Returns(new ProcessResult { ExitCode = 0, Output = "" });

            _mockProcessExecutor
                .Setup(x => x.Execute("surfaceCheck", It.IsAny<string>()))
                .Returns(new ProcessResult { ExitCode = 0, Output = surfaceCheckOutput });

            // Act
            var result = _service.ValidateStl(stlFile);

            // Assert
            result.IsValid.Should().BeFalse();
            result.IllegalTriangles.Should().Be(5);
            result.ErrorMessage.Should().Contain("illegal triangles");
        }

        [Fact]
        public void ValidateStl_WhenSurfaceIsNotWatertight_ReturnsInvalid()
        {
            // Arrange
            var stlFile = "/path/to/model.stl";
            var surfaceCheckOutput = @"
Surface has 0 illegal triangles.
Number of edges                         : 15000
Number of edges connected to one face   : 120
Number of edges connected to >2 faces   : 0
Number of unconnected parts : 1
Number of zones (connected area with consistent normal) : 1
";

            _mockProcessExecutor
                .Setup(x => x.Execute("test", "-f /path/to/model.stl"))
                .Returns(new ProcessResult { ExitCode = 0, Output = "" });

            _mockProcessExecutor
                .Setup(x => x.Execute("surfaceCheck", It.IsAny<string>()))
                .Returns(new ProcessResult { ExitCode = 0, Output = surfaceCheckOutput });

            // Act
            var result = _service.ValidateStl(stlFile);

            // Assert
            result.IsValid.Should().BeFalse();
            result.OpenEdges.Should().Be(120);
            result.IsWatertight.Should().BeFalse();
            result.ErrorMessage.Should().Contain("not watertight");
        }

        [Fact]
        public void ValidateStl_WhenSurfaceHasNonManifoldEdges_ReturnsInvalid()
        {
            // Arrange
            var stlFile = "/path/to/model.stl";
            var surfaceCheckOutput = @"
Surface has 0 illegal triangles.
Number of edges                         : 15000
Number of edges connected to one face   : 0
Number of edges connected to >2 faces   : 15
Number of unconnected parts : 1
Number of zones (connected area with consistent normal) : 1
";

            _mockProcessExecutor
                .Setup(x => x.Execute("test", "-f /path/to/model.stl"))
                .Returns(new ProcessResult { ExitCode = 0, Output = "" });

            _mockProcessExecutor
                .Setup(x => x.Execute("surfaceCheck", It.IsAny<string>()))
                .Returns(new ProcessResult { ExitCode = 0, Output = surfaceCheckOutput });

            // Act
            var result = _service.ValidateStl(stlFile);

            // Assert
            result.IsValid.Should().BeFalse();
            result.NonManifoldEdges.Should().Be(15);
            result.IsManifold.Should().BeFalse();
            result.ErrorMessage.Should().Contain("non-manifold");
        }

        [Fact]
        public void ValidateStl_WhenSurfaceIsSelfIntersecting_ReturnsInvalid()
        {
            // Arrange
            var stlFile = "/path/to/model.stl";
            var surfaceCheckOutput = @"
Surface has 0 illegal triangles.
Number of unconnected parts : 1
Number of zones (connected area with consistent normal) : 1
Surface is self-intersecting at 42 locations
";

            _mockProcessExecutor
                .Setup(x => x.Execute("test", "-f /path/to/model.stl"))
                .Returns(new ProcessResult { ExitCode = 0, Output = "" });

            _mockProcessExecutor
                .Setup(x => x.Execute("surfaceCheck", It.IsAny<string>()))
                .Returns(new ProcessResult { ExitCode = 0, Output = surfaceCheckOutput });

            // Act
            var result = _service.ValidateStl(stlFile);

            // Assert
            result.IsValid.Should().BeFalse();
            result.IsSelfIntersecting.Should().BeTrue();
            result.SelfIntersectionCount.Should().Be(42);
            result.ErrorMessage.Should().Contain("self-intersecting");
        }

        [Fact]
        public void ValidateStl_WhenSurfaceHasMultipleUnconnectedParts_ReturnsInvalid()
        {
            // Arrange
            var stlFile = "/path/to/model.stl";
            var surfaceCheckOutput = @"
Surface has 0 illegal triangles.
Number of unconnected parts : 3
Number of zones (connected area with consistent normal) : 1
";

            _mockProcessExecutor
                .Setup(x => x.Execute("test", "-f /path/to/model.stl"))
                .Returns(new ProcessResult { ExitCode = 0, Output = "" });

            _mockProcessExecutor
                .Setup(x => x.Execute("surfaceCheck", It.IsAny<string>()))
                .Returns(new ProcessResult { ExitCode = 0, Output = surfaceCheckOutput });

            // Act
            var result = _service.ValidateStl(stlFile);

            // Assert
            result.IsValid.Should().BeFalse();
            result.UnconnectedParts.Should().Be(3);
            result.ErrorMessage.Should().Contain("unconnected parts");
        }

        [Fact]
        public void ValidateStl_WhenSurfaceHasMultipleNormalZones_ReturnsInvalid()
        {
            // Arrange
            var stlFile = "/path/to/model.stl";
            var surfaceCheckOutput = @"
Surface has 0 illegal triangles.
Number of unconnected parts : 1
Number of zones (connected area with consistent normal) : 5
";

            _mockProcessExecutor
                .Setup(x => x.Execute("test", "-f /path/to/model.stl"))
                .Returns(new ProcessResult { ExitCode = 0, Output = "" });

            _mockProcessExecutor
                .Setup(x => x.Execute("surfaceCheck", It.IsAny<string>()))
                .Returns(new ProcessResult { ExitCode = 0, Output = surfaceCheckOutput });

            // Act
            var result = _service.ValidateStl(stlFile);

            // Assert
            result.IsValid.Should().BeFalse();
            result.NormalZones.Should().Be(5);
            result.ErrorMessage.Should().Contain("normal zones");
        }

        [Fact]
        public void ConvertStepToStl_WithMillimeterUnits_SucceedsConversion()
        {
            // Arrange
            var inputFile = "/path/to/model.step";
            var outputFile = "/path/to/model.stl";

            _mockProcessExecutor
                .Setup(x => x.Execute("test", "-f /path/to/model.step"))
                .Returns(new ProcessResult { ExitCode = 0, Output = "" });

            _mockProcessExecutor
                .Setup(x => x.Execute("gmsh", It.IsAny<string>()))
                .Returns(new ProcessResult { ExitCode = 0, Output = "Info    : Done meshing" });

            _mockProcessExecutor
                .Setup(x => x.Execute("test", "-f /path/to/model.stl"))
                .Returns(new ProcessResult { ExitCode = 0, Output = "" });

            // Act
            var result = _service.ConvertStepToStl(inputFile, outputFile, meshSize: 1.0, featureAngle: null, inputUnits: "mm");

            // Assert - conversion succeeds (scaling via gmsh Dilate in one step)
            result.IsSuccess.Should().BeTrue();
        }

        [Fact]
        public void ConvertStepToStl_WithCentimeterUnits_SucceedsConversion()
        {
            // Arrange
            var inputFile = "/path/to/model.step";
            var outputFile = "/path/to/model.stl";

            _mockProcessExecutor
                .Setup(x => x.Execute("test", "-f /path/to/model.step"))
                .Returns(new ProcessResult { ExitCode = 0, Output = "" });

            _mockProcessExecutor
                .Setup(x => x.Execute("gmsh", It.IsAny<string>()))
                .Returns(new ProcessResult { ExitCode = 0, Output = "Info    : Done meshing" });

            _mockProcessExecutor
                .Setup(x => x.Execute("test", "-f /path/to/model.stl"))
                .Returns(new ProcessResult { ExitCode = 0, Output = "" });

            // Act
            var result = _service.ConvertStepToStl(inputFile, outputFile, meshSize: 1.0, featureAngle: null, inputUnits: "cm");

            // Assert - conversion succeeds (scaling via gmsh Dilate in one step)
            result.IsSuccess.Should().BeTrue();
        }

        [Fact]
        public void ConvertStepToStl_WithMeterUnits_SucceedsConversion()
        {
            // Arrange
            var inputFile = "/path/to/model.step";
            var outputFile = "/path/to/model.stl";

            _mockProcessExecutor
                .Setup(x => x.Execute("test", "-f /path/to/model.step"))
                .Returns(new ProcessResult { ExitCode = 0, Output = "" });

            _mockProcessExecutor
                .Setup(x => x.Execute("gmsh", It.IsAny<string>()))
                .Returns(new ProcessResult { ExitCode = 0, Output = "Info    : Done meshing" });

            _mockProcessExecutor
                .Setup(x => x.Execute("test", "-f /path/to/model.stl"))
                .Returns(new ProcessResult { ExitCode = 0, Output = "" });

            // Act
            var result = _service.ConvertStepToStl(inputFile, outputFile, meshSize: 1.0, featureAngle: null, inputUnits: "m");

            // Assert - conversion succeeds (no scaling needed for meters)
            result.IsSuccess.Should().BeTrue();
        }

        [Fact]
        public void ConvertStepToStl_WithInchUnits_SucceedsConversion()
        {
            // Arrange
            var inputFile = "/path/to/model.step";
            var outputFile = "/path/to/model.stl";

            _mockProcessExecutor
                .Setup(x => x.Execute("test", "-f /path/to/model.step"))
                .Returns(new ProcessResult { ExitCode = 0, Output = "" });

            _mockProcessExecutor
                .Setup(x => x.Execute("gmsh", It.IsAny<string>()))
                .Returns(new ProcessResult { ExitCode = 0, Output = "Info    : Done meshing" });

            _mockProcessExecutor
                .Setup(x => x.Execute("test", "-f /path/to/model.stl"))
                .Returns(new ProcessResult { ExitCode = 0, Output = "" });

            // Act
            var result = _service.ConvertStepToStl(inputFile, outputFile, meshSize: 1.0, featureAngle: null, inputUnits: "in");

            // Assert - conversion succeeds (scaling via gmsh Dilate in one step)
            result.IsSuccess.Should().BeTrue();
        }
    }
}
