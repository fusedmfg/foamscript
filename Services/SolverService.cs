using foamscript.Models;
using System.Text.RegularExpressions;

namespace foamscript.Services
{
    /// <summary>
    /// Service for running OpenFOAM solvers on meshed cases.
    /// </summary>
    public class SolverService
    {
        private readonly IProcessExecutor _processExecutor;
        private readonly LoggingService _loggingService;

        public SolverService(IProcessExecutor processExecutor, LoggingService loggingService)
        {
            _processExecutor = processExecutor;
            _loggingService = loggingService;
        }

        /// <summary>
        /// Runs the OpenFOAM solver on a single meshed case.
        /// Automatically detects the solver (simpleFoam, pimpleFoam, etc.) from controlDict.
        /// </summary>
        public SolveResult SolveCase(string caseDir, bool parallel, int cores)
        {
            var result = new SolveResult { CaseDir = caseDir };

            try
            {
                // Validate case directory exists
                if (!Directory.Exists(caseDir))
                {
                    result.IsSuccess = false;
                    result.ErrorMessage = $"Case directory not found: {caseDir}";
                    return result;
                }

                // Validate mesh exists
                var polyMeshDir = Path.Combine(caseDir, "constant", "polyMesh");
                if (!Directory.Exists(polyMeshDir))
                {
                    result.IsSuccess = false;
                    result.ErrorMessage = "No mesh found (missing constant/polyMesh/). Run 'foamscript mesh' first.";
                    return result;
                }

                // Validate initial conditions exist
                var zeroDir = Path.Combine(caseDir, "0");
                if (!Directory.Exists(zeroDir))
                {
                    result.IsSuccess = false;
                    result.ErrorMessage = "No initial conditions found (missing 0/ directory).";
                    return result;
                }

                // Detect solver from controlDict (simpleFoam, pimpleFoam, etc.)
                var solverName = DetectSolver(caseDir);
                var isTransient = solverName == "pimpleFoam";

                if (parallel)
                {
                    // Clean old processor directories if present
                    CleanProcessorDirs(caseDir);

                    // Decompose — transient solvers use -force (time-varying fields)
                    Console.WriteLine($"Decomposing case for {cores} processors...");
                    var decomposeArgs = isTransient
                        ? $"-case {caseDir} -force"
                        : $"-case {caseDir}";
                    var decomposeResult = _processExecutor.Execute("decomposePar", decomposeArgs);

                    if (decomposeResult.ExitCode != 0)
                    {
                        result.IsSuccess = false;
                        result.ErrorMessage = $"decomposePar failed with exit code {decomposeResult.ExitCode}";
                        LogToolError("decomposePar", decomposeResult);
                        return result;
                    }

                    Console.WriteLine("✓ Domain decomposed successfully");

                    // Run solver in parallel
                    Console.WriteLine($"Running {solverName} in parallel ({cores} cores)...");
                    var solverArgs = $"-np {cores} {solverName} -case {caseDir} -parallel";
                    var solverResult = _processExecutor.Execute("mpirun", solverArgs);

                    if (solverResult.ExitCode != 0)
                    {
                        result.IsSuccess = false;
                        result.ErrorMessage = $"{solverName} (parallel) failed with exit code {solverResult.ExitCode}";
                        LogToolError($"{solverName} (parallel)", solverResult);
                        return result;
                    }

                    Console.WriteLine($"✓ {solverName} completed successfully");

                    // Reconstruct
                    Console.WriteLine("Reconstructing fields...");
                    var reconstructResult = _processExecutor.Execute("reconstructPar", $"-case {caseDir}");

                    if (reconstructResult.ExitCode != 0)
                    {
                        result.Warnings.Add($"reconstructPar returned exit code {reconstructResult.ExitCode} (may be non-critical)");
                    }
                    else
                    {
                        Console.WriteLine("✓ Fields reconstructed successfully");
                    }
                }
                else
                {
                    // Run solver in serial
                    Console.WriteLine($"Running {solverName}...");
                    var solverResult = _processExecutor.Execute(solverName, $"-case {caseDir}");

                    if (solverResult.ExitCode != 0)
                    {
                        result.IsSuccess = false;
                        result.ErrorMessage = $"{solverName} failed with exit code {solverResult.ExitCode}";
                        LogToolError(solverName, solverResult);
                        return result;
                    }

                    Console.WriteLine($"✓ {solverName} completed successfully");
                }

                // Try to extract force coefficients from postProcessing
                var coeffs = ParseForceCoeffs(caseDir);
                if (coeffs != null)
                {
                    result.Cd = coeffs.Value.Cd;
                    result.Cl = coeffs.Value.Cl;
                    result.CmPitch = coeffs.Value.CmPitch;
                    result.SimulationTime = coeffs.Value.LastTime;
                    result.Converged = true;
                }

                result.IsSuccess = true;
                return result;
            }
            catch (Exception ex)
            {
                result.IsSuccess = false;
                result.ErrorMessage = $"Solver failed: {ex.Message}";
                return result;
            }
        }

        /// <summary>
        /// Runs the solver on all cases in an OpenFOAM study.
        /// </summary>
        public StudySolveResult SolveStudy(string studyDir, bool parallel, int cores, bool continueOnError)
        {
            var result = new StudySolveResult { StudyDir = studyDir };

            try
            {
                if (!Directory.Exists(studyDir))
                {
                    result.IsSuccess = false;
                    result.ErrorMessage = $"Study directory not found: {studyDir}";
                    return result;
                }

                var caseDirs = CaseDiscovery.DiscoverCases(studyDir);

                if (caseDirs.Count == 0)
                {
                    result.IsSuccess = false;
                    result.ErrorMessage = "No valid OpenFOAM cases found in study directory";
                    return result;
                }

                result.TotalCases = caseDirs.Count;

                Console.WriteLine($"Found {caseDirs.Count} case(s) to solve");
                Console.WriteLine();

                for (int i = 0; i < caseDirs.Count; i++)
                {
                    var caseDir = caseDirs[i];
                    var caseName = Path.GetFileName(caseDir);

                    Console.WriteLine($"[{i + 1}/{caseDirs.Count}] Solving case: {caseName}");
                    Console.WriteLine(new string('-', 60));

                    var solveResult = SolveCase(caseDir, parallel, cores);

                    var summary = new CaseSolveSummary
                    {
                        CaseName = caseName,
                        CaseDir = caseDir,
                        Success = solveResult.IsSuccess,
                        ErrorMessage = solveResult.ErrorMessage,
                        SimulationTime = solveResult.SimulationTime,
                        Converged = solveResult.Converged,
                        Cd = solveResult.Cd,
                        Cl = solveResult.Cl,
                        CmPitch = solveResult.CmPitch
                    };

                    result.CaseSummaries.Add(summary);

                    if (solveResult.IsSuccess)
                    {
                        result.SuccessfulCases++;
                        Console.WriteLine($"✓ Case {caseName} solved successfully");
                    }
                    else
                    {
                        result.FailedCases++;
                        Console.WriteLine($"✗ Case {caseName} failed: {solveResult.ErrorMessage}");

                        if (!continueOnError)
                        {
                            result.IsSuccess = false;
                            result.ErrorMessage = $"Solving aborted after failure in case: {caseName}";
                            return result;
                        }
                    }

                    Console.WriteLine();
                }

                result.IsSuccess = result.FailedCases == 0;
                if (result.FailedCases > 0)
                {
                    result.ErrorMessage = $"{result.FailedCases} of {result.TotalCases} cases failed to solve";
                }

                return result;
            }
            catch (Exception ex)
            {
                result.IsSuccess = false;
                result.ErrorMessage = $"Study solving failed: {ex.Message}";
                return result;
            }
        }

        /// <summary>
        /// Parses force coefficient data from postProcessing output.
        /// Returns time-averaged values from the last entries.
        /// </summary>
        internal static (double Cd, double Cl, double CmPitch, double LastTime)? ParseForceCoeffs(string caseDir)
        {
            // OpenFOAM forceCoeffs writes to postProcessing/forces/0/coefficient.dat
            var coeffFile = Path.Combine(caseDir, "postProcessing", "forces", "0", "coefficient.dat");
            if (!File.Exists(coeffFile))
                return null;

            return ParseForceCoeffsFile(coeffFile);
        }

        /// <summary>
        /// Parses a forceCoeffs coefficient.dat file.
        /// Reads the header line to determine column indices dynamically,
        /// supporting both legacy and v2512+ formats.
        /// v2512 format: Time Cd Cd(f) Cd(r) Cl Cl(f) Cl(r) CmPitch CmRoll CmYaw Cs Cs(f) Cs(r)
        /// </summary>
        internal static (double Cd, double Cl, double CmPitch, double LastTime)? ParseForceCoeffsFile(string filePath, double averageWindow = 0.1)
        {
            try
            {
                var lines = File.ReadAllLines(filePath);
                var dataLines = new List<(double Time, double Cd, double Cl, double CmPitch)>();

                // Parse header to find column indices dynamically
                int cdCol = -1, clCol = -1, cmPitchCol = -1;
                foreach (var line in lines)
                {
                    if (line.TrimStart().StartsWith("# Time"))
                    {
                        var headers = line.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                        // headers[0] = "#", headers[1] = "Time", headers[2+] = column names
                        // Data line: parts[0] = Time, parts[1+] = data columns
                        // So data index = header index - 1
                        for (int i = 2; i < headers.Length; i++)
                        {
                            switch (headers[i])
                            {
                                case "Cd": cdCol = i - 1; break;
                                case "Cl": clCol = i - 1; break;
                                case "CmPitch": cmPitchCol = i - 1; break;
                            }
                        }
                        break;
                    }
                }

                // Fallback to v2512 indices if header parsing fails
                if (cdCol < 0) cdCol = 1;
                if (clCol < 0) clCol = 4;
                if (cmPitchCol < 0) cmPitchCol = 7;

                var minCols = Math.Max(Math.Max(cdCol, clCol), cmPitchCol) + 1;

                foreach (var line in lines)
                {
                    if (line.StartsWith("#") || string.IsNullOrWhiteSpace(line))
                        continue;

                    var parts = line.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length >= minCols &&
                        double.TryParse(parts[0], out var time) &&
                        double.TryParse(parts[cdCol], out var cd) &&
                        double.TryParse(parts[clCol], out var cl) &&
                        double.TryParse(parts[cmPitchCol], out var cmPitch))
                    {
                        dataLines.Add((time, cd, cl, cmPitch));
                    }
                }

                if (dataLines.Count == 0)
                    return null;

                // Average over the last averageWindow fraction of the data
                var totalTime = dataLines[^1].Time - dataLines[0].Time;
                var windowStart = dataLines[^1].Time - (totalTime * averageWindow);

                var windowData = dataLines.Where(d => d.Time >= windowStart).ToList();
                if (windowData.Count == 0)
                    windowData = dataLines; // Fall back to all data

                var avgCd = windowData.Average(d => d.Cd);
                var avgCl = windowData.Average(d => d.Cl);
                var avgCm = windowData.Average(d => d.CmPitch);

                return (avgCd, avgCl, avgCm, dataLines[^1].Time);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Failed to parse force coefficients: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Detects the solver application from the case's controlDict file.
        /// Falls back to "simpleFoam" if the application line cannot be parsed.
        /// </summary>
        internal static string DetectSolver(string caseDir)
        {
            var controlDictPath = Path.Combine(caseDir, "system", "controlDict");
            if (!File.Exists(controlDictPath))
                return "simpleFoam";

            var lines = File.ReadAllLines(controlDictPath);
            foreach (var line in lines)
            {
                var trimmed = line.Trim();
                if (trimmed.StartsWith("application"))
                {
                    // Parse "application     simpleFoam;" → "simpleFoam"
                    var match = Regex.Match(trimmed, @"application\s+(\w+)\s*;");
                    if (match.Success)
                        return match.Groups[1].Value;
                }
            }

            return "simpleFoam";
        }

        /// <summary>
        /// Removes old processor directories before re-decomposition.
        /// </summary>
        private static void CleanProcessorDirs(string caseDir)
        {
            var processorDirs = Directory.GetDirectories(caseDir, "processor*");
            foreach (var dir in processorDirs)
            {
                Directory.Delete(dir, recursive: true);
            }
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
    }
}
