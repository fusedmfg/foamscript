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

            // Build gmsh command
            // -3: 3D meshing
            // -format stl: output format
            // -clscale: mesh size scaling factor
            // -scale: geometry scaling factor (for unit conversion)
            // -angle: feature angle threshold (preserves sharp edges)
            // -o: output file
            var gmshArgs = $"-3 -format stl -clscale {meshSize}";

            // Apply unit conversion scale if needed
            if (scaleFactor != 1.0)
            {
                gmshArgs += $" -scale {scaleFactor}";
            }

            if (featureAngle.HasValue)
            {
                gmshArgs += $" -angle {featureAngle.Value}";
            }

            gmshArgs += $" -o {outputFile} {inputFile}";

            // Execute gmsh
            var gmshResult = _processExecutor.Execute("gmsh", gmshArgs);

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
    }
}
