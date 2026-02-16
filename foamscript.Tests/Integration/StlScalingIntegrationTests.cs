using Xunit;
using FluentAssertions;
using foamscript.Services;
using System.IO;
using Microsoft.Extensions.Logging;
using Moq;

namespace foamscript.Tests.Integration
{
    /// <summary>
    /// Integration tests for STL scaling that use real file I/O.
    /// These tests work cross-platform without requiring gmsh.
    /// </summary>
    public class StlScalingIntegrationTests : IDisposable
    {
        private readonly string _testDirectory;
        private readonly GeometryService _geometryService;

        public StlScalingIntegrationTests()
        {
            _testDirectory = Path.Combine(Path.GetTempPath(), $"foamscript_test_{Guid.NewGuid()}");
            Directory.CreateDirectory(_testDirectory);

            // Create a real process executor and logging service for integration tests
            var processExecutor = new ProcessExecutor();
            var mockLogger = new Mock<ILogger<LoggingService>>();
            var loggingService = new LoggingService(mockLogger.Object);
            _geometryService = new GeometryService(processExecutor, loggingService);
        }

        public void Dispose()
        {
            if (Directory.Exists(_testDirectory))
            {
                Directory.Delete(_testDirectory, true);
            }
        }

        [Fact]
        public void ScaleStlFile_WithMillimeterToMeter_ScalesCorrectly()
        {
            // Arrange: Create a 215mm diameter cylinder STL
            var stlFile = Path.Combine(_testDirectory, "test_mm.stl");
            GenerateSimpleCylinderStl(stlFile, diameter: 215.0, height: 18.0);

            // Act: Scale from mm to m (factor 0.001)
            var scaleFactor = 0.001;
            var scaleMethod = typeof(GeometryService).GetMethod("ScaleStlFile",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var result = (bool)scaleMethod!.Invoke(_geometryService, new object[] { stlFile, scaleFactor })!;

            // Assert: Scaling succeeded
            result.Should().BeTrue();

            // Verify dimensions by reading back the STL
            var boundingBox = CalculateBoundingBox(stlFile);
            var diameter = Math.Max(boundingBox.Width, boundingBox.Depth);

            // Should be ~0.215m (±0.001m tolerance for rounding)
            diameter.Should().BeApproximately(0.215, 0.001);
            boundingBox.Height.Should().BeApproximately(0.018, 0.001);
        }

        [Fact]
        public void ScaleStlFile_WithCentimeterToMeter_ScalesCorrectly()
        {
            // Arrange: Create a 21.5cm diameter cylinder STL
            var stlFile = Path.Combine(_testDirectory, "test_cm.stl");
            GenerateSimpleCylinderStl(stlFile, diameter: 21.5, height: 1.8);

            // Act: Scale from cm to m (factor 0.01)
            var scaleFactor = 0.01;
            var scaleMethod = typeof(GeometryService).GetMethod("ScaleStlFile",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var result = (bool)scaleMethod!.Invoke(_geometryService, new object[] { stlFile, scaleFactor })!;

            // Assert
            result.Should().BeTrue();

            var boundingBox = CalculateBoundingBox(stlFile);
            var diameter = Math.Max(boundingBox.Width, boundingBox.Depth);

            diameter.Should().BeApproximately(0.215, 0.001);
            boundingBox.Height.Should().BeApproximately(0.018, 0.001);
        }

        [Fact]
        public void ScaleStlFile_WithInchToMeter_ScalesCorrectly()
        {
            // Arrange: Create a 8.46 inch diameter cylinder STL (≈215mm)
            var stlFile = Path.Combine(_testDirectory, "test_in.stl");
            GenerateSimpleCylinderStl(stlFile, diameter: 8.46, height: 0.71);

            // Act: Scale from inches to m (factor 0.0254)
            var scaleFactor = 0.0254;
            var scaleMethod = typeof(GeometryService).GetMethod("ScaleStlFile",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var result = (bool)scaleMethod!.Invoke(_geometryService, new object[] { stlFile, scaleFactor })!;

            // Assert
            result.Should().BeTrue();

            var boundingBox = CalculateBoundingBox(stlFile);
            var diameter = Math.Max(boundingBox.Width, boundingBox.Depth);

            diameter.Should().BeApproximately(0.215, 0.001);
            boundingBox.Height.Should().BeApproximately(0.018, 0.001);
        }

        /// <summary>
        /// Generates a simple cylinder STL file for testing.
        /// Creates a low-poly cylinder with 8 sides centered at origin.
        /// </summary>
        private void GenerateSimpleCylinderStl(string filePath, double diameter, double height)
        {
            var radius = diameter / 2.0;
            var segments = 8; // Low poly for fast generation
            var halfHeight = height / 2.0;

            using var writer = new StreamWriter(filePath);
            writer.WriteLine("solid test_cylinder");

            // Generate triangles
            var angleStep = 2.0 * Math.PI / segments;

            for (int i = 0; i < segments; i++)
            {
                var angle1 = i * angleStep;
                var angle2 = ((i + 1) % segments) * angleStep;

                var x1 = radius * Math.Cos(angle1);
                var z1 = radius * Math.Sin(angle1);
                var x2 = radius * Math.Cos(angle2);
                var z2 = radius * Math.Sin(angle2);

                // Side faces (2 triangles per segment)
                WriteTriangle(writer, x1, -halfHeight, z1, x2, -halfHeight, z2, x1, halfHeight, z1);
                WriteTriangle(writer, x2, -halfHeight, z2, x2, halfHeight, z2, x1, halfHeight, z1);

                // Top cap
                WriteTriangle(writer, 0, halfHeight, 0, x1, halfHeight, z1, x2, halfHeight, z2);

                // Bottom cap
                WriteTriangle(writer, 0, -halfHeight, 0, x2, -halfHeight, z2, x1, -halfHeight, z1);
            }

            writer.WriteLine("endsolid test_cylinder");
        }

        private void WriteTriangle(StreamWriter writer, double x1, double y1, double z1,
                                   double x2, double y2, double z2,
                                   double x3, double y3, double z3)
        {
            // Calculate normal (simplified - assuming outward facing)
            var nx = 0.0;
            var ny = 1.0;
            var nz = 0.0;

            writer.WriteLine($"  facet normal {nx:E} {ny:E} {nz:E}");
            writer.WriteLine("    outer loop");
            writer.WriteLine($"      vertex {x1:E} {y1:E} {z1:E}");
            writer.WriteLine($"      vertex {x2:E} {y2:E} {z2:E}");
            writer.WriteLine($"      vertex {x3:E} {y3:E} {z3:E}");
            writer.WriteLine("    endloop");
            writer.WriteLine("  endfacet");
        }

        private BoundingBox CalculateBoundingBox(string stlFile)
        {
            var lines = File.ReadAllLines(stlFile);
            double minX = double.MaxValue, maxX = double.MinValue;
            double minY = double.MaxValue, maxY = double.MinValue;
            double minZ = double.MaxValue, maxZ = double.MinValue;

            foreach (var line in lines)
            {
                var trimmed = line.Trim();
                if (trimmed.StartsWith("vertex"))
                {
                    var parts = trimmed.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length >= 4 &&
                        double.TryParse(parts[1], out double x) &&
                        double.TryParse(parts[2], out double y) &&
                        double.TryParse(parts[3], out double z))
                    {
                        minX = Math.Min(minX, x);
                        maxX = Math.Max(maxX, x);
                        minY = Math.Min(minY, y);
                        maxY = Math.Max(maxY, y);
                        minZ = Math.Min(minZ, z);
                        maxZ = Math.Max(maxZ, z);
                    }
                }
            }

            return new BoundingBox
            {
                MinX = minX,
                MaxX = maxX,
                MinY = minY,
                MaxY = maxY,
                MinZ = minZ,
                MaxZ = maxZ,
                Width = maxX - minX,
                Height = maxY - minY,
                Depth = maxZ - minZ
            };
        }

        private class BoundingBox
        {
            public double MinX { get; set; }
            public double MaxX { get; set; }
            public double MinY { get; set; }
            public double MaxY { get; set; }
            public double MinZ { get; set; }
            public double MaxZ { get; set; }
            public double Width { get; set; }
            public double Height { get; set; }
            public double Depth { get; set; }
        }
    }
}
