using foamscript.Models;

namespace foamscript.Services
{
    /// <summary>
    /// Service for domain geometry generation: rotor (cylinder) and tunnel (box) STL creation from geometry STL.
    /// </summary>
    public class DomainService
    {
        private readonly IProcessExecutor _processExecutor;
        private readonly LoggingService _loggingService;

        public DomainService(IProcessExecutor processExecutor, LoggingService loggingService)
        {
            _processExecutor = processExecutor;
            _loggingService = loggingService;
        }

        /// <summary>
        /// Generates rotor and tunnel domain geometries from a geometry STL file.
        /// Validation (e.g., PDGA size checks) is the caller's responsibility via TemplateMetadata.
        /// </summary>
        public DomainGenerationResult GenerateDomain(
            string discStlFile,
            string outputDirectory,
            double rotorRadiusScale,
            double rotorHeightScale,
            double tunnelUpstream,
            double tunnelDownstream,
            double tunnelRadial,
            int meshResolution)
        {
            var result = new DomainGenerationResult();

            // Check if STL exists
            var fileCheckResult = _processExecutor.Execute("test", $"-f {discStlFile}");
            if (fileCheckResult.ExitCode != 0)
            {
                result.IsSuccess = false;
                result.ErrorMessage = $"Disc STL file not found: {discStlFile}";
                return result;
            }

            // Parse STL and calculate bounding box
            // STL file is assumed to be in meters (converted by the convert command with --input-units)
            var boundingBox = CalculateBoundingBox(discStlFile);
            if (boundingBox == null)
            {
                result.IsSuccess = false;
                result.ErrorMessage = $"Failed to parse disc STL file: {discStlFile}";
                return result;
            }

            result.DiscBoundingBox = boundingBox;

            // Calculate rotor dimensions (cylinder around geometry)
            var discRadius = Math.Max(boundingBox.Width, boundingBox.Depth) / 2.0;
            var discHeight = boundingBox.Height;
            var discCenterX = (boundingBox.MinX + boundingBox.MaxX) / 2.0;
            var discCenterY = (boundingBox.MinY + boundingBox.MaxY) / 2.0;
            var discCenterZ = (boundingBox.MinZ + boundingBox.MaxZ) / 2.0;

            var rotorDimensions = new CylinderDimensions
            {
                Radius = discRadius * rotorRadiusScale,
                Height = discHeight * rotorHeightScale,
                CenterX = discCenterX,
                CenterY = discCenterY,
                CenterZ = discCenterZ
            };
            result.RotorDimensions = rotorDimensions;

            // Calculate tunnel dimensions (box around everything)
            // Use scaled diameter (discRadius is already from scaled bounding box)
            var scaledDiscDiameter = 2.0 * discRadius;
            var tunnelDimensions = new BoxDimensions
            {
                MinX = discCenterX - (tunnelUpstream * scaledDiscDiameter),
                MaxX = discCenterX + (tunnelDownstream * scaledDiscDiameter),
                MinY = discCenterY - (tunnelRadial * scaledDiscDiameter),
                MaxY = discCenterY + (tunnelRadial * scaledDiscDiameter),
                MinZ = discCenterZ - (tunnelRadial * scaledDiscDiameter),
                MaxZ = discCenterZ + (tunnelRadial * scaledDiscDiameter)
            };
            result.TunnelDimensions = tunnelDimensions;

            // Create output directory if it doesn't exist
            Directory.CreateDirectory(outputDirectory);

            // Generate and write rotor STL (cylinder)
            var rotorStlPath = Path.Combine(outputDirectory, "rotor.stl");
            var rotorMesh = GenerateCylinderMesh(rotorDimensions, meshResolution);
            WriteStlFile(rotorStlPath, "rotor", rotorMesh);
            result.RotorStlFile = rotorStlPath;

            // Generate and write tunnel STL (box)
            var tunnelStlPath = Path.Combine(outputDirectory, "tunnel.stl");
            var tunnelMesh = GenerateBoxMesh(tunnelDimensions);
            WriteStlFile(tunnelStlPath, "tunnel", tunnelMesh);
            result.TunnelStlFile = tunnelStlPath;

            result.IsSuccess = true;
            return result;
        }

        /// <summary>
        /// Generates only the tunnel (box) domain without a rotor zone.
        /// Used by templates that do not require rotation (e.g., airfoil, duct).
        /// </summary>
        public DomainGenerationResult GenerateTunnelOnly(
            string stlFile,
            string outputDirectory,
            double tunnelUpstream,
            double tunnelDownstream,
            double tunnelRadial)
        {
            var result = new DomainGenerationResult();

            var fileCheckResult = _processExecutor.Execute("test", $"-f {stlFile}");
            if (fileCheckResult.ExitCode != 0)
            {
                result.IsSuccess = false;
                result.ErrorMessage = $"STL file not found: {stlFile}";
                return result;
            }

            var boundingBox = CalculateBoundingBox(stlFile);
            if (boundingBox == null)
            {
                result.IsSuccess = false;
                result.ErrorMessage = $"Failed to parse STL file: {stlFile}";
                return result;
            }

            result.DiscBoundingBox = boundingBox;

            var refDim = Math.Max(boundingBox.Width, boundingBox.Depth);
            var centerX = (boundingBox.MinX + boundingBox.MaxX) / 2.0;
            var centerY = (boundingBox.MinY + boundingBox.MaxY) / 2.0;
            var centerZ = (boundingBox.MinZ + boundingBox.MaxZ) / 2.0;

            var tunnelDimensions = new BoxDimensions
            {
                MinX = centerX - (tunnelUpstream * refDim),
                MaxX = centerX + (tunnelDownstream * refDim),
                MinY = centerY - (tunnelRadial * refDim),
                MaxY = centerY + (tunnelRadial * refDim),
                MinZ = centerZ - (tunnelRadial * refDim),
                MaxZ = centerZ + (tunnelRadial * refDim)
            };
            result.TunnelDimensions = tunnelDimensions;

            Directory.CreateDirectory(outputDirectory);

            var tunnelStlPath = Path.Combine(outputDirectory, "tunnel.stl");
            var tunnelMesh = GenerateBoxMesh(tunnelDimensions);
            WriteStlFile(tunnelStlPath, "tunnel", tunnelMesh);
            result.TunnelStlFile = tunnelStlPath;

            result.IsSuccess = true;
            return result;
        }

        /// <summary>
        /// Calculates bounding box of an STL file by parsing vertices.
        /// </summary>
        public BoundingBox? CalculateBoundingBox(string stlFile)
        {
            try
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
                        if (parts.Length >= 4)
                        {
                            if (double.TryParse(parts[1], out double x) &&
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
                }

                if (minX == double.MaxValue)
                {
                    return null; // No vertices found
                }

                return new BoundingBox
                {
                    MinX = minX,
                    MaxX = maxX,
                    MinY = minY,
                    MaxY = maxY,
                    MinZ = minZ,
                    MaxZ = maxZ
                };
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Failed to calculate bounding box: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Generates a closed cylinder mesh (for rotor).
        /// </summary>
        private List<Triangle> GenerateCylinderMesh(CylinderDimensions dims, int segments)
        {
            var triangles = new List<Triangle>();
            var angleStep = 2.0 * Math.PI / segments;
            var halfHeight = dims.Height / 2.0;

            // Generate cylinder side faces
            for (int i = 0; i < segments; i++)
            {
                var angle1 = i * angleStep;
                var angle2 = ((i + 1) % segments) * angleStep;

                var x1 = dims.CenterX + dims.Radius * Math.Cos(angle1);
                var z1 = dims.CenterZ + dims.Radius * Math.Sin(angle1);
                var x2 = dims.CenterX + dims.Radius * Math.Cos(angle2);
                var z2 = dims.CenterZ + dims.Radius * Math.Sin(angle2);

                var yTop = dims.CenterY + halfHeight;
                var yBottom = dims.CenterY - halfHeight;

                // Two triangles for each side face (outward-facing normals)
                triangles.Add(new Triangle(
                    new Vector3(x1, yBottom, z1),
                    new Vector3(x2, yBottom, z2),
                    new Vector3(x1, yTop, z1)
                ));
                triangles.Add(new Triangle(
                    new Vector3(x2, yBottom, z2),
                    new Vector3(x2, yTop, z2),
                    new Vector3(x1, yTop, z1)
                ));

                // Top cap triangle (normal points up: +Y)
                // Counter-clockwise when viewed from above
                triangles.Add(new Triangle(
                    new Vector3(dims.CenterX, yTop, dims.CenterZ),
                    new Vector3(x1, yTop, z1),
                    new Vector3(x2, yTop, z2)
                ));

                // Bottom cap triangle (normal points down: -Y)
                // Clockwise when viewed from below (counter-clockwise from above)
                triangles.Add(new Triangle(
                    new Vector3(dims.CenterX, yBottom, dims.CenterZ),
                    new Vector3(x2, yBottom, z2),
                    new Vector3(x1, yBottom, z1)
                ));
            }

            return triangles;
        }

        /// <summary>
        /// Generates a closed box mesh (for tunnel).
        /// </summary>
        private List<Triangle> GenerateBoxMesh(BoxDimensions dims)
        {
            var triangles = new List<Triangle>();

            // 8 corners of the box
            var vertices = new[]
            {
                new Vector3(dims.MinX, dims.MinY, dims.MinZ), // 0
                new Vector3(dims.MaxX, dims.MinY, dims.MinZ), // 1
                new Vector3(dims.MaxX, dims.MaxY, dims.MinZ), // 2
                new Vector3(dims.MinX, dims.MaxY, dims.MinZ), // 3
                new Vector3(dims.MinX, dims.MinY, dims.MaxZ), // 4
                new Vector3(dims.MaxX, dims.MinY, dims.MaxZ), // 5
                new Vector3(dims.MaxX, dims.MaxY, dims.MaxZ), // 6
                new Vector3(dims.MinX, dims.MaxY, dims.MaxZ)  // 7
            };

            // Front face (MinZ)
            triangles.Add(new Triangle(vertices[0], vertices[1], vertices[2]));
            triangles.Add(new Triangle(vertices[0], vertices[2], vertices[3]));

            // Back face (MaxZ)
            triangles.Add(new Triangle(vertices[4], vertices[6], vertices[5]));
            triangles.Add(new Triangle(vertices[4], vertices[7], vertices[6]));

            // Left face (MinX)
            triangles.Add(new Triangle(vertices[0], vertices[3], vertices[7]));
            triangles.Add(new Triangle(vertices[0], vertices[7], vertices[4]));

            // Right face (MaxX)
            triangles.Add(new Triangle(vertices[1], vertices[5], vertices[6]));
            triangles.Add(new Triangle(vertices[1], vertices[6], vertices[2]));

            // Bottom face (MinY)
            triangles.Add(new Triangle(vertices[0], vertices[4], vertices[5]));
            triangles.Add(new Triangle(vertices[0], vertices[5], vertices[1]));

            // Top face (MaxY)
            triangles.Add(new Triangle(vertices[3], vertices[2], vertices[6]));
            triangles.Add(new Triangle(vertices[3], vertices[6], vertices[7]));

            return triangles;
        }

        /// <summary>
        /// Writes triangles to an ASCII STL file.
        /// </summary>
        private void WriteStlFile(string filePath, string solidName, List<Triangle> triangles)
        {
            using var writer = new StreamWriter(filePath);
            writer.WriteLine($"solid {solidName}");

            foreach (var triangle in triangles)
            {
                var normal = triangle.CalculateNormal();
                writer.WriteLine($"  facet normal {normal.X:E} {normal.Y:E} {normal.Z:E}");
                writer.WriteLine("    outer loop");
                writer.WriteLine($"      vertex {triangle.V1.X:E} {triangle.V1.Y:E} {triangle.V1.Z:E}");
                writer.WriteLine($"      vertex {triangle.V2.X:E} {triangle.V2.Y:E} {triangle.V2.Z:E}");
                writer.WriteLine($"      vertex {triangle.V3.X:E} {triangle.V3.Y:E} {triangle.V3.Z:E}");
                writer.WriteLine("    endloop");
                writer.WriteLine("  endfacet");
            }

            writer.WriteLine($"endsolid {solidName}");
        }
    }

    /// <summary>
    /// 3D vector for mesh generation.
    /// </summary>
    internal class Vector3
    {
        public double X { get; set; }
        public double Y { get; set; }
        public double Z { get; set; }

        public Vector3(double x, double y, double z)
        {
            X = x;
            Y = y;
            Z = z;
        }
    }

    /// <summary>
    /// Triangle with three vertices.
    /// </summary>
    internal class Triangle
    {
        public Vector3 V1 { get; set; }
        public Vector3 V2 { get; set; }
        public Vector3 V3 { get; set; }

        public Triangle(Vector3 v1, Vector3 v2, Vector3 v3)
        {
            V1 = v1;
            V2 = v2;
            V3 = v3;
        }

        /// <summary>
        /// Calculates the normal vector of the triangle.
        /// </summary>
        public Vector3 CalculateNormal()
        {
            // Calculate two edges
            var edge1 = new Vector3(V2.X - V1.X, V2.Y - V1.Y, V2.Z - V1.Z);
            var edge2 = new Vector3(V3.X - V1.X, V3.Y - V1.Y, V3.Z - V1.Z);

            // Calculate cross product
            var nx = edge1.Y * edge2.Z - edge1.Z * edge2.Y;
            var ny = edge1.Z * edge2.X - edge1.X * edge2.Z;
            var nz = edge1.X * edge2.Y - edge1.Y * edge2.X;

            // Normalize
            var length = Math.Sqrt(nx * nx + ny * ny + nz * nz);
            if (length > 0)
            {
                nx /= length;
                ny /= length;
                nz /= length;
            }

            return new Vector3(nx, ny, nz);
        }
    }
}
