using foamscript.Models;
using System.Text.RegularExpressions;

namespace foamscript.Services
{
    /// <summary>
    /// Service for geometry conversion operations (STEP to STL).
    /// </summary>
    public class GeometryService
    {
        private readonly IProcessExecutor _processExecutor;

        public GeometryService(IProcessExecutor processExecutor)
        {
            _processExecutor = processExecutor;
        }

        /// <summary>
        /// Converts a STEP file to STL format using gmsh.
        /// </summary>
        /// <param name="inputFile">Path to input STEP file</param>
        /// <param name="outputFile">Path to output STL file</param>
        /// <param name="meshSize">Mesh size scaling factor (default 1.0)</param>
        /// <param name="featureAngle">Feature angle in degrees (null = no angle constraint)</param>
        /// <param name="inputUnits">Input file units (mm, cm, m, in, ft) - output will be scaled to meters</param>
        /// <returns>Conversion result with success status and details</returns>
        public GeometryConversionResult ConvertStepToStl(string inputFile, string outputFile, double meshSize = 1.0, double? featureAngle = null, string inputUnits = "m")
        {
            var result = new GeometryConversionResult();

            // Check if input file exists
            var fileCheckResult = _processExecutor.Execute("test", $"-f {inputFile}");
            if (fileCheckResult.ExitCode != 0)
            {
                result.IsSuccess = false;
                result.ErrorMessage = $"Input file not found: {inputFile}";
                return result;
            }

            // Calculate scale factor for unit conversion to meters
            var scaleFactor = GetUnitScaleFactor(inputUnits);

            ProcessResult gmshResult;

            // If scaling is needed, use gmsh script with Dilate to convert and scale in one step
            if (scaleFactor != 1.0)
            {
                // Convert to absolute paths for gmsh
                var absInputFile = Path.GetFullPath(inputFile);
                var absOutputFile = Path.GetFullPath(outputFile);

                // Build gmsh script for STEP conversion with scaling
                // - Geometry.OCCBoundsUseVisible: needed for OpenCASCADE kernel
                // - Dilate: scales geometry around origin
                // - Volume{:}: applies to all volumes (STEP solids)
                // - Mesh 2: generates 2D surface mesh
                var scriptParts = new List<string>
                {
                    "Geometry.OCCBoundsUseVisible = 1;",
                    $"Dilate {{{{0,0,0}}, {scaleFactor}}} {{ Volume{{:}}; }}",
                    $"Mesh.MeshSizeFactor = {meshSize};",
                };

                if (featureAngle.HasValue)
                {
                    scriptParts.Add($"Mesh.AngleToleranceFacetOverlap = {featureAngle.Value};");
                }

                scriptParts.Add("Mesh 2;");
                scriptParts.Add($"Save '{absOutputFile}';");

                var script = string.Join(" ", scriptParts);
                var gmshArgs = $"{absInputFile} -string \"{script}\" -0";

                Console.WriteLine($"DEBUG: Executing gmsh with args: {gmshArgs}");

                gmshResult = _processExecutor.Execute("gmsh", gmshArgs);
            }
            else
            {
                // No scaling needed, use standard gmsh conversion
                // -3: 3D meshing
                // -format stl: output format
                // -clscale: mesh size scaling factor
                // -angle: feature angle threshold (preserves sharp edges)
                // -o: output file
                var gmshArgs = $"-3 -format stl -clscale {meshSize}";

                if (featureAngle.HasValue)
                {
                    gmshArgs += $" -angle {featureAngle.Value}";
                }

                gmshArgs += $" -o {outputFile} {inputFile}";

                gmshResult = _processExecutor.Execute("gmsh", gmshArgs);
            }

            if (gmshResult.ExitCode != 0)
            {
                result.IsSuccess = false;
                result.ErrorMessage = $"gmsh conversion failed: {gmshResult.Error}";
                return result;
            }

            // Verify output file was created
            var outputCheckResult = _processExecutor.Execute("test", $"-f {outputFile}");
            if (outputCheckResult.ExitCode != 0)
            {
                result.IsSuccess = false;
                result.ErrorMessage = $"Output file was not created: {outputFile}";
                return result;
            }

            // Parse mesh statistics from gmsh output
            ParseMeshStatistics(gmshResult.Output, result);

            result.IsSuccess = true;
            result.OutputFile = outputFile;
            return result;
        }

        /// <summary>
        /// Parses mesh statistics (node count, triangle count) from gmsh output.
        /// </summary>
        private void ParseMeshStatistics(string gmshOutput, GeometryConversionResult result)
        {
            // Example gmsh output: "Info    : 1234 nodes 2468 triangles"
            var nodeMatch = Regex.Match(gmshOutput, @"(\d+)\s+nodes");
            var triangleMatch = Regex.Match(gmshOutput, @"(\d+)\s+triangles");

            if (nodeMatch.Success && int.TryParse(nodeMatch.Groups[1].Value, out int nodes))
            {
                result.NodeCount = nodes;
            }

            if (triangleMatch.Success && int.TryParse(triangleMatch.Groups[1].Value, out int triangles))
            {
                result.TriangleCount = triangles;
            }
        }

        /// <summary>
        /// Validates an STL file for geometric and topological quality using OpenFOAM's surfaceCheck utility.
        /// </summary>
        /// <param name="stlFile">Path to the STL file to validate</param>
        /// <returns>Validation result with quality metrics and pass/fail status</returns>
        public StlValidationResult ValidateStl(string stlFile)
        {
            var result = new StlValidationResult();

            // Check if input file exists
            var fileCheckResult = _processExecutor.Execute("test", $"-f {stlFile}");
            if (fileCheckResult.ExitCode != 0)
            {
                result.IsValid = false;
                result.ErrorMessage = $"File not found: {stlFile}";
                return result;
            }

            // Run surfaceCheck with self-intersection check
            var surfaceCheckResult = _processExecutor.Execute("surfaceCheck", $"-checkSelfIntersection {stlFile}");

            if (surfaceCheckResult.ExitCode != 0)
            {
                result.IsValid = false;
                result.ErrorMessage = $"surfaceCheck failed: {surfaceCheckResult.Error}";
                return result;
            }

            // Parse surfaceCheck output
            ParseSurfaceCheckOutput(surfaceCheckResult.Output, result);

            // Determine overall validity
            var errors = new List<string>();

            if (result.IllegalTriangles > 0)
            {
                errors.Add($"{result.IllegalTriangles} illegal triangles");
            }

            if (result.OpenEdges > 0)
            {
                errors.Add($"not watertight ({result.OpenEdges} open edges)");
            }

            if (result.NonManifoldEdges > 0)
            {
                errors.Add($"{result.NonManifoldEdges} non-manifold edges");
            }

            if (result.UnconnectedParts != 1)
            {
                errors.Add($"{result.UnconnectedParts} unconnected parts (expected 1)");
            }

            if (result.NormalZones != 1)
            {
                errors.Add($"{result.NormalZones} normal zones (expected 1)");
            }

            if (result.IsSelfIntersecting)
            {
                errors.Add($"self-intersecting at {result.SelfIntersectionCount} locations");
            }

            if (errors.Count > 0)
            {
                result.IsValid = false;
                result.ErrorMessage = "Surface validation failed: " + string.Join(", ", errors);
            }
            else
            {
                result.IsValid = true;
            }

            return result;
        }

        /// <summary>
        /// Parses surfaceCheck output to extract validation metrics.
        /// </summary>
        private void ParseSurfaceCheckOutput(string output, StlValidationResult result)
        {
            // Parse illegal triangles: "Surface has 5 illegal triangles."
            var illegalTrianglesMatch = Regex.Match(output, @"Surface has (\d+) illegal triangles");
            if (illegalTrianglesMatch.Success && int.TryParse(illegalTrianglesMatch.Groups[1].Value, out int illegalTriangles))
            {
                result.IllegalTriangles = illegalTriangles;
            }

            // Parse unconnected parts: "Number of unconnected parts : 1"
            var unconnectedPartsMatch = Regex.Match(output, @"Number of unconnected parts\s*:\s*(\d+)");
            if (unconnectedPartsMatch.Success && int.TryParse(unconnectedPartsMatch.Groups[1].Value, out int unconnectedParts))
            {
                result.UnconnectedParts = unconnectedParts;
            }

            // Parse normal zones: "Number of zones (connected area with consistent normal) : 1"
            var normalZonesMatch = Regex.Match(output, @"Number of zones.*?:\s*(\d+)");
            if (normalZonesMatch.Success && int.TryParse(normalZonesMatch.Groups[1].Value, out int normalZones))
            {
                result.NormalZones = normalZones;
            }

            // Parse open edges: "Number of edges connected to one face   : 120"
            var openEdgesMatch = Regex.Match(output, @"Number of edges connected to one face\s*:\s*(\d+)");
            if (openEdgesMatch.Success && int.TryParse(openEdgesMatch.Groups[1].Value, out int openEdges))
            {
                result.OpenEdges = openEdges;
            }

            // Parse non-manifold edges: "Number of edges connected to >2 faces   : 15"
            var nonManifoldEdgesMatch = Regex.Match(output, @"Number of edges connected to >2 faces\s*:\s*(\d+)");
            if (nonManifoldEdgesMatch.Success && int.TryParse(nonManifoldEdgesMatch.Groups[1].Value, out int nonManifoldEdges))
            {
                result.NonManifoldEdges = nonManifoldEdges;
            }

            // Parse self-intersection: "Surface is self-intersecting at 42 locations" or "Surface is not self-intersecting"
            var selfIntersectingMatch = Regex.Match(output, @"Surface is self-intersecting at (\d+) locations");
            if (selfIntersectingMatch.Success)
            {
                result.IsSelfIntersecting = true;
                if (int.TryParse(selfIntersectingMatch.Groups[1].Value, out int selfIntersectionCount))
                {
                    result.SelfIntersectionCount = selfIntersectionCount;
                }
            }
            else if (output.Contains("not self-intersecting"))
            {
                result.IsSelfIntersecting = false;
                result.SelfIntersectionCount = 0;
            }
        }

        /// <summary>
        /// Gets the scale factor to convert from input units to meters (OpenFOAM standard).
        /// </summary>
        private double GetUnitScaleFactor(string inputUnits)
        {
            return inputUnits.ToLower() switch
            {
                "mm" => 0.001,      // millimeters to meters
                "cm" => 0.01,       // centimeters to meters
                "m" => 1.0,         // meters to meters (no conversion)
                "in" => 0.0254,     // inches to meters
                "ft" => 0.3048,     // feet to meters
                _ => 1.0            // default: no conversion
            };
        }

        /// <summary>
        /// Generates rotor and tunnel domain geometries from a disc STL file.
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

            // Check if disc STL exists
            var fileCheckResult = _processExecutor.Execute("test", $"-f {discStlFile}");
            if (fileCheckResult.ExitCode != 0)
            {
                result.IsSuccess = false;
                result.ErrorMessage = $"Disc STL file not found: {discStlFile}";
                return result;
            }

            // Parse disc STL and calculate bounding box
            // STL file is assumed to be in meters (converted by the convert command with --input-units)
            var boundingBox = CalculateBoundingBox(discStlFile);
            if (boundingBox == null)
            {
                result.IsSuccess = false;
                result.ErrorMessage = $"Failed to parse disc STL file: {discStlFile}";
                return result;
            }

            // Validate disc dimensions are reasonable (PDGA disc: 21-30cm diameter)
            var discDiameter = Math.Max(boundingBox.Width, boundingBox.Depth);
            if (discDiameter < 0.18 || discDiameter > 0.35)
            {
                Console.WriteLine($"⚠ Warning: Disc diameter {discDiameter:F4}m is outside expected PDGA range (0.21-0.30m).");
                Console.WriteLine($"  Ensure you used correct --input-units during conversion.");
            }

            result.DiscBoundingBox = boundingBox;

            // Calculate rotor dimensions (cylinder around disc)
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
        /// Calculates bounding box of an STL file by parsing vertices.
        /// </summary>
        private BoundingBox? CalculateBoundingBox(string stlFile)
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
            catch
            {
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
