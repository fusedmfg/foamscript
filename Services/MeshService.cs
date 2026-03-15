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

                // Step 1.5: Orient disc STL normals outward.
                // gmsh preserves BREP face orientation from STEP files. Shapr3D's Parasolid
                // kernel may produce inward-facing normals, which causes snappyHexMesh to
                // create boundary faces with reversed area vectors → all force coefficient
                // signs flip. surfaceOrient uses a known-outside point (0,0,-1) — below
                // the disc — to determine outward direction, then reorients all normals.
                Console.WriteLine("Orienting disc surface normals...");
                var discStl = Path.Combine(caseDir, "constant", "triSurface", "disc.stl");
                var orientResult = _processExecutor.Execute("surfaceOrient",
                    $"{discStl} \"(0 0 -1)\" {discStl}");

                if (orientResult.ExitCode != 0)
                {
                    result.Warnings.Add("surfaceOrient failed — disc normals may be incorrect");
                    _loggingService.LogError($"surfaceOrient failed: {orientResult.Output}");
                }
                else
                {
                    Console.WriteLine("✓ Disc surface normals oriented outward");
                }

                // Step 2: Extract surface features (generates .eMesh files for snappyHexMesh)
                Console.WriteLine("Extracting surface features...");
                var featureResult = _processExecutor.Execute("surfaceFeatureExtract", $"-case {caseDir}");

                if (featureResult.ExitCode != 0)
                {
                    result.IsSuccess = false;
                    result.ErrorMessage = $"surfaceFeatureExtract failed with exit code {featureResult.ExitCode}";

                    _loggingService.LogError($"surfaceFeatureExtract failed with exit code {featureResult.ExitCode}");
                    _loggingService.LogError($"surfaceFeatureExtract stdout:\n{featureResult.Output}");
                    if (!string.IsNullOrEmpty(featureResult.Error))
                    {
                        _loggingService.LogError($"surfaceFeatureExtract stderr:\n{featureResult.Error}");
                    }

                    return result;
                }

                Console.WriteLine("✓ Surface features extracted successfully");

                // Step 3: Run snappyHexMesh
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

                    // Copy triSurface files to each processor directory
                    DistributeTriSurface(caseDir, cores);

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

                // Step: Convert rotor wall patches to cyclicAMI (only for AMI templates)
                var createPatchPath = Path.Combine(caseDir, "system", "createPatchDict");
                if (File.Exists(createPatchPath))
                {
                    PatchBoundaryForAMI(caseDir);
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