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

        public MeshService(ILogger<LoggingService> logger, IProcessExecutor processExecutor, LoggingService loggingService)
        {
            _logger = logger;
            _processExecutor = processExecutor;
            _loggingService = loggingService;
        }

        /// <summary>
        /// Meshes an OpenFOAM case using blockMesh and snappyHexMesh.
        /// </summary>
        public MeshResult MeshCase(string caseDir, bool parallel, int cores, bool checkQuality, bool overwrite)
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

                // Step 1: Run blockMesh
                Console.WriteLine("Running blockMesh...");
                var blockMeshResult = _processExecutor.Execute("blockMesh", $"-case {caseDir}");

                if (blockMeshResult.ExitCode != 0)
                {
                    result.IsSuccess = false;
                    result.ErrorMessage = $"blockMesh failed with exit code {blockMeshResult.ExitCode}";

                    // Log detailed error output to file
                    _loggingService.LogError($"blockMesh failed with exit code {blockMeshResult.ExitCode}");
                    _loggingService.LogError($"blockMesh stdout:\n{blockMeshResult.Output}");
                    if (!string.IsNullOrEmpty(blockMeshResult.Error))
                    {
                        _loggingService.LogError($"blockMesh stderr:\n{blockMeshResult.Error}");
                    }

                    return result;
                }

                Console.WriteLine("✓ blockMesh completed successfully");

                // Step 2: Run snappyHexMesh
                if (parallel)
                {
                    // Decompose domain for parallel processing
                    Console.WriteLine($"Decomposing domain for {cores} processors...");
                    var decomposeResult = _processExecutor.Execute("decomposePar", $"-case {caseDir} -no-fields");

                    if (decomposeResult.ExitCode != 0)
                    {
                        result.IsSuccess = false;
                        result.ErrorMessage = $"decomposePar failed with exit code {decomposeResult.ExitCode}";

                        // Log detailed error output to file
                        _loggingService.LogError($"decomposePar failed with exit code {decomposeResult.ExitCode}");
                        _loggingService.LogError($"decomposePar stdout:\n{decomposeResult.Output}");
                        if (!string.IsNullOrEmpty(decomposeResult.Error))
                        {
                            _loggingService.LogError($"decomposePar stderr:\n{decomposeResult.Error}");
                        }

                        return result;
                    }

                    Console.WriteLine("✓ Domain decomposed successfully");

                    // Run snappyHexMesh in parallel
                    Console.WriteLine($"Running snappyHexMesh in parallel ({cores} cores)...");
                    var snappyArgs = $"-np {cores} snappyHexMesh -case {caseDir} -parallel";
                    if (overwrite) snappyArgs += " -overwrite";

                    var snappyResult = _processExecutor.Execute("mpirun", snappyArgs);

                    if (snappyResult.ExitCode != 0)
                    {
                        result.IsSuccess = false;
                        result.ErrorMessage = $"snappyHexMesh (parallel) failed with exit code {snappyResult.ExitCode}";

                        // Log detailed error output to file
                        _loggingService.LogError($"snappyHexMesh (parallel) failed with exit code {snappyResult.ExitCode}");
                        _loggingService.LogError($"snappyHexMesh stdout:\n{snappyResult.Output}");
                        if (!string.IsNullOrEmpty(snappyResult.Error))
                        {
                            _loggingService.LogError($"snappyHexMesh stderr:\n{snappyResult.Error}");
                        }

                        return result;
                    }

                    Console.WriteLine("✓ snappyHexMesh completed successfully");

                    // Reconstruct mesh
                    Console.WriteLine("Reconstructing mesh...");
                    var reconstructResult = _processExecutor.Execute("reconstructParMesh", $"-case {caseDir} -constant");

                    if (reconstructResult.ExitCode != 0)
                    {
                        result.Warnings.Add($"reconstructParMesh returned exit code {reconstructResult.ExitCode} (may be non-critical)");
                    }
                    else
                    {
                        Console.WriteLine("✓ Mesh reconstructed successfully");
                    }
                }
                else
                {
                    // Run snappyHexMesh in serial
                    Console.WriteLine("Running snappyHexMesh...");
                    var snappyArgs = $"-case {caseDir}";
                    if (overwrite) snappyArgs += " -overwrite";

                    var snappyResult = _processExecutor.Execute("snappyHexMesh", snappyArgs);

                    if (snappyResult.ExitCode != 0)
                    {
                        result.IsSuccess = false;
                        result.ErrorMessage = $"snappyHexMesh failed with exit code {snappyResult.ExitCode}";

                        // Log detailed error output to file
                        _loggingService.LogError($"snappyHexMesh failed with exit code {snappyResult.ExitCode}");
                        _loggingService.LogError($"snappyHexMesh stdout:\n{snappyResult.Output}");
                        if (!string.IsNullOrEmpty(snappyResult.Error))
                        {
                            _loggingService.LogError($"snappyHexMesh stderr:\n{snappyResult.Error}");
                        }

                        return result;
                    }

                    Console.WriteLine("✓ snappyHexMesh completed successfully");
                }

                // Step 3: Check mesh quality (optional)
                if (checkQuality)
                {
                    Console.WriteLine("Checking mesh quality...");
                    var checkMeshResult = _processExecutor.Execute("checkMesh", $"-case {caseDir}");

                    // Parse checkMesh output for mesh statistics
                    ParseCheckMeshOutput(checkMeshResult.Output, result);

                    if (checkMeshResult.ExitCode == 0)
                    {
                        result.MeshQualityPassed = true;
                        Console.WriteLine("✓ Mesh quality check passed");
                    }
                    else
                    {
                        result.MeshQualityPassed = false;
                        result.Warnings.Add("Mesh quality check failed - review mesh for issues");
                    }
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
        /// Meshes all cases in an OpenFOAM study.
        /// </summary>
        public StudyMeshResult MeshStudy(string studyDir, bool parallel, int cores, bool checkQuality, bool overwrite, bool continueOnError)
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
                var caseDirs = DiscoverCases(studyDir);

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
        /// Discovers all valid OpenFOAM case directories within a study directory.
        /// </summary>
        private List<string> DiscoverCases(string studyDir)
        {
            var caseDirs = new List<string>();

            // Get all subdirectories
            var subdirs = Directory.GetDirectories(studyDir);

            foreach (var subdir in subdirs)
            {
                // Skip geometry directory and other non-case directories
                var dirName = Path.GetFileName(subdir);
                if (dirName == "geometry" || dirName.StartsWith("."))
                    continue;

                // Check if it's a valid OpenFOAM case (has constant/ and system/)
                var constantDir = Path.Combine(subdir, "constant");
                var systemDir = Path.Combine(subdir, "system");

                if (Directory.Exists(constantDir) && Directory.Exists(systemDir))
                {
                    caseDirs.Add(subdir);
                }
            }

            // Sort case directories alphabetically for consistent ordering
            caseDirs.Sort();

            return caseDirs;
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