using Microsoft.Extensions.Logging;
using foamscript.Models;
using System.Text.RegularExpressions;

namespace foamscript.Services
{
    public class MeshService
    {
        private readonly ILogger<LoggingService> _logger;
        private readonly IProcessExecutor _processExecutor;
        private readonly LoggingService _loggingService;
        private readonly TemplateMetadataService _metadataService;

        public MeshService(ILogger<LoggingService> logger, IProcessExecutor processExecutor, LoggingService loggingService, TemplateMetadataService metadataService)
        {
            _logger = logger;
            _processExecutor = processExecutor;
            _loggingService = loggingService;
            _metadataService = metadataService;
        }

        /// <summary>
        /// Meshes an OpenFOAM case using a template-driven pipeline from TEMPLATE.json.
        /// </summary>
        public virtual MeshResult MeshCase(string caseDir, bool parallel, int cores, bool checkQuality, bool overwrite)
        {
            var result = new MeshResult
            {
                CaseDir = caseDir
            };

            try
            {
                // Validate case directory exists
                if (!Directory.Exists(caseDir))
                {
                    result.IsSuccess = false;
                    result.ErrorMessage = $"Case directory not found: {caseDir}";
                    return result;
                }

                // Validate required directories exist
                var constantDir = Path.Combine(caseDir, "constant");
                var systemDir = Path.Combine(caseDir, "system");

                if (!Directory.Exists(constantDir) || !Directory.Exists(systemDir))
                {
                    result.IsSuccess = false;
                    result.ErrorMessage = "Invalid OpenFOAM case structure (missing constant/ or system/ directory)";
                    return result;
                }

                // Load template metadata for pipeline definition
                var metadata = _metadataService.LoadMetadata(caseDir);

                // Execute pre-processing hooks from TEMPLATE.json
                foreach (var step in metadata.PreProcess)
                {
                    var resolvedArgs = ResolvePipelineTokens(step.Args, caseDir, metadata);
                    Console.WriteLine($"Running pre-process: {step.Command}...");

                    var stepResult = _processExecutor.Execute(step.Command, resolvedArgs);

                    if (stepResult.ExitCode != 0)
                    {
                        if (step.Optional)
                        {
                            result.Warnings.Add($"Pre-process {step.Command} failed (optional — continuing)");
                            _loggingService.LogError($"Pre-process {step.Command} failed: {stepResult.Output}");
                        }
                        else
                        {
                            result.IsSuccess = false;
                            result.ErrorMessage = $"Pre-process {step.Command} failed with exit code {stepResult.ExitCode}";
                            _loggingService.LogError($"{step.Command} stdout:\n{stepResult.Output}");
                            if (!string.IsNullOrEmpty(stepResult.Error))
                                _loggingService.LogError($"{step.Command} stderr:\n{stepResult.Error}");
                            return result;
                        }
                    }
                    else
                    {
                        Console.WriteLine($"✓ Pre-process {step.Command} completed");
                    }
                }

                // Execute mesh pipeline from TEMPLATE.json
                foreach (var step in metadata.MeshPipeline)
                {
                    // Skip parallel-only steps (decomposePar, reconstructParMesh) in serial mode
                    if (step.ParallelOnly && !parallel)
                        continue;

                    // Skip checkMesh if quality checking is not requested
                    if (step.Command == "checkMesh" && !checkQuality)
                        continue;

                    var resolvedArgs = ResolvePipelineTokens(step.Args, caseDir, metadata);
                    Console.WriteLine($"Running {step.Command}...");

                    ProcessResult stepResult;
                    if (step.Parallel && cores > 1)
                    {
                        stepResult = _processExecutor.Execute("mpirun", $"-np {cores} {step.Command} {resolvedArgs} -parallel");
                    }
                    else
                    {
                        stepResult = _processExecutor.Execute(step.Command, resolvedArgs);
                    }

                    if (stepResult.ExitCode != 0)
                    {
                        if (step.Optional)
                        {
                            result.Warnings.Add($"{step.Command} failed (optional step — continuing)");
                            _loggingService.LogError($"{step.Command} failed: {stepResult.Output}");
                        }
                        else if (step.Command == "checkMesh")
                        {
                            // checkMesh failure is non-fatal — parse output and add warning
                            ParseCheckMeshOutput(stepResult.Output, result);
                            result.MeshQualityPassed = false;
                            result.Warnings.Add("Mesh quality check failed - review mesh for issues");
                        }
                        else if (step.Command == "reconstructParMesh")
                        {
                            // reconstructParMesh failure is non-critical
                            result.Warnings.Add($"reconstructParMesh returned exit code {stepResult.ExitCode} (may be non-critical)");
                        }
                        else
                        {
                            result.IsSuccess = false;
                            result.ErrorMessage = $"{step.Command} failed with exit code {stepResult.ExitCode}";
                            _loggingService.LogError($"{step.Command} stdout:\n{stepResult.Output}");
                            if (!string.IsNullOrEmpty(stepResult.Error))
                                _loggingService.LogError($"{step.Command} stderr:\n{stepResult.Error}");
                            return result;
                        }
                    }
                    else
                    {
                        Console.WriteLine($"✓ {step.Command} completed successfully");

                        // After successful decomposePar, distribute triSurface files to processor dirs
                        if (step.Command == "decomposePar")
                        {
                            DistributeTriSurface(caseDir, cores);
                        }

                        // After successful checkMesh, parse output for mesh statistics
                        if (step.Command == "checkMesh")
                        {
                            ParseCheckMeshOutput(stepResult.Output, result);
                            result.MeshQualityPassed = true;
                        }
                    }
                }

                // Post-pipeline: convert rotor wall patches to cyclicAMI if needed
                var createPatchPath = Path.Combine(caseDir, "system", "createPatchDict");
                if (File.Exists(createPatchPath))
                {
                    PatchBoundaryForAMI(caseDir);
                }

                result.IsSuccess = true;
                return result;
            }
            catch (Exception ex)
            {
                result.IsSuccess = false;
                result.ErrorMessage = $"Meshing failed: {ex.Message}";
                return result;
            }
        }

        /// <summary>
        /// Resolves template tokens in pipeline step arguments.
        /// </summary>
        private static string ResolvePipelineTokens(string args, string caseDir, TemplateMetadata metadata)
        {
            var geometryStlPath = Path.Combine(caseDir, "constant", "triSurface", metadata.Geometry.StlName);
            var geometryDir = Path.Combine(caseDir, "constant", "triSurface");
            var studyDir = Path.GetDirectoryName(caseDir) ?? caseDir;
            var outsidePoint = metadata.Geometry.SurfaceOrient?.OutsidePoint is { Length: 3 } pt
                ? $"{pt[0]} {pt[1]} {pt[2]}"
                : "0 0 0";

            return args
                .Replace("{caseDir}", caseDir)
                .Replace("{geometryDir}", geometryDir)
                .Replace("{studyDir}", studyDir)
                .Replace("{geometryStlPath}", geometryStlPath)
                .Replace("{outsidePoint}", outsidePoint);
        }

        /// <summary>
        /// Meshes all cases in an OpenFOAM study.
        /// </summary>
        public virtual StudyMeshResult MeshStudy(string studyDir, bool parallel, int cores, bool checkQuality, bool overwrite, bool continueOnError)
        {
            var result = new StudyMeshResult
            {
                StudyDir = studyDir
            };

            try
            {
                // Validate study directory exists
                if (!Directory.Exists(studyDir))
                {
                    result.IsSuccess = false;
                    result.ErrorMessage = $"Study directory not found: {studyDir}";
                    return result;
                }

                // Discover case directories (directories that contain constant/ and system/)
                var caseDirs = CaseDiscovery.DiscoverCases(studyDir);

                if (caseDirs.Count == 0)
                {
                    result.IsSuccess = false;
                    result.ErrorMessage = "No valid OpenFOAM cases found in study directory";
                    return result;
                }

                result.TotalCases = caseDirs.Count;

                Console.WriteLine($"Found {caseDirs.Count} case(s) to mesh");
                Console.WriteLine();

                // Mesh each case
                for (int i = 0; i < caseDirs.Count; i++)
                {
                    var caseDir = caseDirs[i];
                    var caseName = Path.GetFileName(caseDir);

                    Console.WriteLine($"[{i + 1}/{caseDirs.Count}] Meshing case: {caseName}");
                    Console.WriteLine(new string('-', 60));

                    var meshResult = MeshCase(caseDir, parallel, cores, checkQuality, overwrite);

                    var summary = new CaseMeshSummary
                    {
                        CaseName = caseName,
                        CaseDir = caseDir,
                        Success = meshResult.IsSuccess,
                        ErrorMessage = meshResult.ErrorMessage,
                        CellCount = meshResult.CellCount,
                        MeshQualityPassed = meshResult.MeshQualityPassed
                    };

                    result.CaseSummaries.Add(summary);

                    if (meshResult.IsSuccess)
                    {
                        result.SuccessfulCases++;
                        Console.WriteLine($"✓ Case {caseName} meshed successfully");
                    }
                    else
                    {
                        result.FailedCases++;
                        Console.WriteLine($"✗ Case {caseName} failed: {meshResult.ErrorMessage}");

                        if (!continueOnError)
                        {
                            result.IsSuccess = false;
                            result.ErrorMessage = $"Meshing aborted after failure in case: {caseName}";
                            return result;
                        }
                    }

                    Console.WriteLine();
                }

                result.IsSuccess = result.FailedCases == 0;
                if (result.FailedCases > 0)
                {
                    result.ErrorMessage = $"{result.FailedCases} of {result.TotalCases} cases failed to mesh";
                }

                return result;
            }
            catch (Exception ex)
            {
                result.IsSuccess = false;
                result.ErrorMessage = $"Study meshing failed: {ex.Message}";
                return result;
            }
        }

        /// <summary>
        /// Patches the constant/polyMesh/boundary file to convert rotor/rotor_slave
        /// from wall type to cyclicAMI. Required for AMI mesh interface rotation.
        /// createPatch utility doesn't reliably work in all OpenFOAM versions.
        /// </summary>
        private static void PatchBoundaryForAMI(string caseDir)
        {
            var boundaryPath = Path.Combine(caseDir, "constant", "polyMesh", "boundary");
            if (!File.Exists(boundaryPath))
                return;

            var content = File.ReadAllText(boundaryPath);

            // Only patch if rotor patches exist as wall type
            if (!content.Contains("rotor") || !content.Contains("rotor_slave"))
                return;

            var lines = File.ReadAllLines(boundaryPath).ToList();
            var output = new List<string>();

            for (int i = 0; i < lines.Count; i++)
            {
                var trimmed = lines[i].Trim();

                // Detect rotor or rotor_slave patch block
                if ((trimmed == "rotor" || trimmed == "rotor_slave") && i + 1 < lines.Count && lines[i + 1].Trim() == "{")
                {
                    var patchName = trimmed;
                    var neighbourPatch = patchName == "rotor" ? "rotor_slave" : "rotor";

                    output.Add(lines[i]); // patch name
                    output.Add(lines[i + 1]); // {
                    i += 2;

                    // Write cyclicAMI entries
                    output.Add("        type            cyclicAMI;");
                    output.Add($"        neighbourPatch  {neighbourPatch};");
                    output.Add("        transform       noOrdering;");
                    output.Add("        matchTolerance  0.0001;");

                    // Skip old type and inGroups lines, keep nFaces and startFace
                    while (i < lines.Count)
                    {
                        var innerTrimmed = lines[i].Trim();
                        if (innerTrimmed.StartsWith("nFaces") || innerTrimmed.StartsWith("startFace"))
                        {
                            output.Add(lines[i]);
                        }
                        else if (innerTrimmed == "}")
                        {
                            output.Add(lines[i]);
                            break;
                        }
                        // Skip type, inGroups lines
                        i++;
                    }
                }
                else
                {
                    output.Add(lines[i]);
                }
            }

            File.WriteAllLines(boundaryPath, output);
            Console.WriteLine("✓ Rotor patches converted to cyclicAMI");
        }

        private void LogToolError(string toolName, ProcessResult result)
        {
            _loggingService.LogError($"{toolName} failed with exit code {result.ExitCode}");
            _loggingService.LogError($"{toolName} stdout:\n{result.Output}");
            if (!string.IsNullOrEmpty(result.Error))
            {
                _loggingService.LogError($"{toolName} stderr:\n{result.Error}");
            }
        }

        /// <summary>
        /// Copies constant/triSurface files (STL + eMesh) to each processor directory.
        /// Required for parallel snappyHexMesh which reads geometry from processor dirs.
        /// </summary>
        private static void DistributeTriSurface(string caseDir, int cores)
        {
            var triSurfaceDir = Path.Combine(caseDir, "constant", "triSurface");
            if (!Directory.Exists(triSurfaceDir))
                return;

            var files = Directory.GetFiles(triSurfaceDir);

            for (int i = 0; i < cores; i++)
            {
                var procTriDir = Path.Combine(caseDir, $"processor{i}", "constant", "triSurface");
                Directory.CreateDirectory(procTriDir);

                foreach (var file in files)
                {
                    var dest = Path.Combine(procTriDir, Path.GetFileName(file));
                    File.Copy(file, dest, overwrite: true);
                }
            }
        }

        /// <summary>
        /// Parses checkMesh output to extract mesh statistics.
        /// </summary>
        private void ParseCheckMeshOutput(string output, MeshResult result)
        {
            // Extract cell count: "    cells:            123456"
            var cellMatch = Regex.Match(output, @"cells:\s+(\d+)");
            if (cellMatch.Success && int.TryParse(cellMatch.Groups[1].Value, out var cellCount))
            {
                result.CellCount = cellCount;
            }

            // Extract point count: "    points:           234567"
            var pointMatch = Regex.Match(output, @"points:\s+(\d+)");
            if (pointMatch.Success && int.TryParse(pointMatch.Groups[1].Value, out var pointCount))
            {
                result.PointCount = pointCount;
            }

            // Extract face count: "    faces:            345678"
            var faceMatch = Regex.Match(output, @"faces:\s+(\d+)");
            if (faceMatch.Success && int.TryParse(faceMatch.Groups[1].Value, out var faceCount))
            {
                result.FaceCount = faceCount;
            }
        }
    }
}
