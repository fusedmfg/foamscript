using foamscript.Models;

namespace foamscript.Services
{
    /// <summary>
    /// Service for validating OpenFOAM environment and tool availability.
    /// </summary>
    public class EnvironmentService
    {
        private readonly IProcessExecutor _processExecutor;

        private readonly string[] _requiredEnvironmentVariables = new[]
        {
            "WM_PROJECT_DIR",
            "WM_PROJECT_VERSION",
            "FOAM_APPBIN",
            "FOAM_LIBBIN"
        };

        private readonly string[] _requiredTools = new[]
        {
            "blockMesh",
            "snappyHexMesh",
            "surfaceFeatureExtract",
            "decomposePar",
            "pimpleFoam",
            "reconstructPar",
            "checkMesh",
            "gmsh"
        };

        public EnvironmentService(IProcessExecutor processExecutor)
        {
            _processExecutor = processExecutor;
        }

        /// <summary>
        /// Auto-detects OpenFOAM installations in common directories.
        /// Searches /usr/lib/openfoam and /opt for openfoam* directories with etc/bashrc.
        /// </summary>
        public List<string> DetectOpenFOAMInstallations()
        {
            // Search common installation paths for directories that have etc/bashrc
            var result = _processExecutor.Execute("bash",
                "-c \"find /usr/lib/openfoam /opt -maxdepth 3 -type f -path '*/etc/bashrc' 2>/dev/null | xargs dirname | xargs dirname || true\"");

            if (result.ExitCode == 0 && !string.IsNullOrWhiteSpace(result.Output))
            {
                return result.Output
                    .Split('\n', StringSplitOptions.RemoveEmptyEntries)
                    .Select(p => p.Trim())
                    .Where(p => !string.IsNullOrEmpty(p))
                    .Distinct()
                    .ToList();
            }

            return new List<string>();
        }

        /// <summary>
        /// Finds the bashrc file in a given OpenFOAM installation directory.
        /// </summary>
        public string? FindBashrcInInstallation(string installationPath)
        {
            var result = _processExecutor.Execute("bash",
                $"-c \"find {installationPath} -name bashrc -type f -path '*/etc/bashrc' 2>/dev/null | head -1\"");

            if (result.ExitCode == 0 && !string.IsNullOrWhiteSpace(result.Output))
            {
                return result.Output.Trim();
            }

            return null;
        }

        /// <summary>
        /// Detects the installed OpenFOAM version.
        /// </summary>
        public string? DetectOpenFOAMVersion()
        {
            var result = _processExecutor.Execute("bash", "-c \"echo $WM_PROJECT_VERSION\"");

            if (result.ExitCode == 0)
            {
                var version = result.Output.Trim();
                return string.IsNullOrEmpty(version) ? null : version;
            }

            return null;
        }

        /// <summary>
        /// Checks if an environment variable is set.
        /// </summary>
        public EnvironmentVariableCheck CheckEnvironmentVariable(string variableName)
        {
            var result = _processExecutor.Execute("bash", $"-c \"echo ${variableName}\"");

            if (result.ExitCode == 0)
            {
                var value = result.Output.Trim();
                return new EnvironmentVariableCheck
                {
                    IsValid = !string.IsNullOrEmpty(value),
                    Value = string.IsNullOrEmpty(value) ? null : value
                };
            }

            return new EnvironmentVariableCheck { IsValid = false, Value = null };
        }

        /// <summary>
        /// Checks if a command-line tool is available.
        /// </summary>
        public ToolCheck CheckToolAvailable(string toolName)
        {
            var result = _processExecutor.Execute("which", toolName);

            if (result.ExitCode == 0)
            {
                var path = result.Output.Trim();
                return new ToolCheck
                {
                    IsAvailable = true,
                    Path = path
                };
            }

            return new ToolCheck { IsAvailable = false, Path = null };
        }

        /// <summary>
        /// Performs a complete validation of the OpenFOAM environment.
        /// </summary>
        public EnvironmentValidationResult ValidateEnvironment()
        {
            var result = new EnvironmentValidationResult();

            // Check OpenFOAM version
            result.Version = DetectOpenFOAMVersion();
            if (result.Version == null)
            {
                result.IsValid = false;

                // Try to auto-detect installations to provide helpful error message
                result.DetectedInstallations = DetectOpenFOAMInstallations();

                if (result.DetectedInstallations.Count > 0)
                {
                    // OpenFOAM is installed but not sourced
                    var bashrcPath = FindBashrcInInstallation(result.DetectedInstallations[0]);
                    result.DetectedBashrcPath = bashrcPath;

                    var errorMsg = "OpenFOAM is installed but not sourced. Please run:\n";
                    if (bashrcPath != null)
                    {
                        errorMsg += $"  source {bashrcPath}";
                    }
                    else
                    {
                        errorMsg += $"  source {result.DetectedInstallations[0]}/etc/bashrc";
                    }

                    if (result.DetectedInstallations.Count > 1)
                    {
                        errorMsg += $"\n\nOther detected installations:";
                        foreach (var install in result.DetectedInstallations.Skip(1))
                        {
                            errorMsg += $"\n  - {install}";
                        }
                    }

                    result.ErrorMessage = errorMsg;
                }
                else
                {
                    // No installations found
                    result.ErrorMessage = "OpenFOAM environment not detected and no installations found.\n" +
                                         "Please install OpenFOAM or source the bashrc file:\n" +
                                         "  source /path/to/openfoam/etc/bashrc";
                }

                return result;
            }

            // Check required environment variables
            foreach (var varName in _requiredEnvironmentVariables)
            {
                var check = CheckEnvironmentVariable(varName);
                if (check.IsValid)
                {
                    result.EnvironmentVariables[varName] = check.Value!;
                }
                else
                {
                    result.MissingVariables.Add(varName);
                }
            }

            // Check required tools
            foreach (var tool in _requiredTools)
            {
                var check = CheckToolAvailable(tool);
                if (check.IsAvailable)
                {
                    result.AvailableTools[tool] = check.Path!;
                }
                else
                {
                    result.MissingTools.Add(tool);
                }
            }

            // Determine overall validity
            result.IsValid = result.MissingVariables.Count == 0 && result.MissingTools.Count == 0;

            if (!result.IsValid)
            {
                var errors = new List<string>();
                if (result.MissingVariables.Count > 0)
                {
                    errors.Add($"Missing environment variables: {string.Join(", ", result.MissingVariables)}");
                }
                if (result.MissingTools.Count > 0)
                {
                    errors.Add($"Missing tools: {string.Join(", ", result.MissingTools)}");
                }
                result.ErrorMessage = string.Join("\n", errors);
            }

            return result;
        }
    }
}
