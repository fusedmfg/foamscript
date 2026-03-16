using foamscript.Models;

namespace foamscript.Services
{
    /// <summary>
    /// Service for validating OpenFOAM environment and tool availability.
    /// Produces a grouped checklist with install hints for every failure.
    /// </summary>
    public class EnvironmentService
    {
        private readonly IProcessExecutor _processExecutor;
        private readonly OpenFoamEnvironment _openFoamEnvironment;

        private static readonly (string Group, string[] Tools, string InstallHint)[] ToolGroups = new[]
        {
            ("Meshing Tools", new[] { "blockMesh", "snappyHexMesh", "surfaceFeatureExtract",
                "surfaceOrient", "surfaceCheck", "decomposePar", "reconstructParMesh", "checkMesh" },
                "Included with OpenFOAM"),
            ("Solver Tools", new[] { "simpleFoam", "pimpleFoam", "reconstructPar" },
                "Included with OpenFOAM"),
            ("Parallel Execution", new[] { "mpirun" },
                "sudo apt install openmpi-bin"),
            ("Geometry Processing", new[] { "gmsh" },
                "sudo apt install gmsh"),
            ("Flow Visualization", new[] { "pvpython" },
                "sudo apt install paraview"),
        };

        private static readonly string[] RequiredEnvVars = new[]
        {
            "WM_PROJECT_DIR", "WM_PROJECT_VERSION", "FOAM_APPBIN", "FOAM_LIBBIN"
        };

        private static readonly (string Module, string InstallHint)[] PythonModules = new[]
        {
            ("matplotlib", "pip3 install matplotlib"),
            ("numpy", "pip3 install numpy"),
        };

        public EnvironmentService(IProcessExecutor processExecutor, OpenFoamEnvironment openFoamEnvironment)
        {
            _processExecutor = processExecutor;
            _openFoamEnvironment = openFoamEnvironment;
        }

        /// <summary>
        /// Auto-detects OpenFOAM installations in common directories.
        /// </summary>
        public List<string> DetectOpenFOAMInstallations()
        {
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
        /// Performs a complete grouped validation of the OpenFOAM environment.
        /// Auto-detects OpenFOAM, sources bashrc, checks all tools using the sourced env.
        /// Writes ~/.foamscript/config.json on success.
        /// </summary>
        public EnvironmentValidationResult ValidateEnvironment(string? configPath = null)
        {
            var result = new EnvironmentValidationResult();

            // --- OpenFOAM group: detect installation + source bashrc ---
            var openfoamGroup = new CheckGroup { Name = "OpenFOAM" };

            // Try to load existing config first, fall back to auto-detection
            var config = OpenFoamEnvironment.LoadConfig(configPath);
            string? bashrcPath = config?.OpenFoamBashrc;

            if (string.IsNullOrEmpty(bashrcPath) || !File.Exists(bashrcPath))
            {
                bashrcPath = null;
                // Auto-detect
                result.DetectedInstallations = DetectOpenFOAMInstallations();
                if (result.DetectedInstallations.Count > 0)
                {
                    bashrcPath = FindBashrcInInstallation(result.DetectedInstallations[0]);
                }
            }

            if (string.IsNullOrEmpty(bashrcPath))
            {
                openfoamGroup.Checks.Add(new CheckItem
                {
                    Passed = false,
                    Name = "Installation",
                    Detail = "NOT FOUND",
                    InstallHint = "Install OpenFOAM: https://openfoam.org/download/"
                });
                result.Groups.Add(openfoamGroup);
                result.IsValid = false;
                return result;
            }

            result.DetectedBashrcPath = bashrcPath;
            openfoamGroup.Checks.Add(new CheckItem
            {
                Passed = true,
                Name = "Installation",
                Detail = Path.GetDirectoryName(Path.GetDirectoryName(bashrcPath))
            });

            // Source bashrc and capture env
            var capturedEnv = _openFoamEnvironment.CaptureEnvironment(bashrcPath);
            if (capturedEnv == null)
            {
                openfoamGroup.Checks.Add(new CheckItem
                {
                    Passed = false,
                    Name = "Bashrc sourced",
                    Detail = $"Failed to source {bashrcPath}",
                    InstallHint = "Check that the bashrc file is a valid OpenFOAM installation"
                });
                result.Groups.Add(openfoamGroup);
                result.IsValid = false;
                return result;
            }

            openfoamGroup.Checks.Add(new CheckItem
            {
                Passed = true,
                Name = "Bashrc sourced",
                Detail = bashrcPath
            });

            // Version
            var version = capturedEnv.TryGetValue("WM_PROJECT_VERSION", out var v) ? v : null;
            result.OpenFoamVersion = version;
            openfoamGroup.Checks.Add(new CheckItem
            {
                Passed = version != null,
                Name = "Version",
                Detail = version ?? "NOT SET",
                InstallHint = version == null ? "WM_PROJECT_VERSION not set after sourcing bashrc" : null
            });

            // Required env vars
            foreach (var varName in RequiredEnvVars)
            {
                var hasVar = capturedEnv.TryGetValue(varName, out var val) && !string.IsNullOrEmpty(val);
                openfoamGroup.Checks.Add(new CheckItem
                {
                    Passed = hasVar,
                    Name = varName,
                    Detail = hasVar ? val : "NOT SET",
                    InstallHint = hasVar ? null : $"source {bashrcPath}"
                });
            }

            result.Groups.Add(openfoamGroup);

            // --- Tool groups: check each tool using sourced env ---
            foreach (var (groupName, tools, defaultHint) in ToolGroups)
            {
                var group = new CheckGroup { Name = groupName };
                foreach (var tool in tools)
                {
                    var check = CheckToolInEnvironment(bashrcPath, tool);
                    group.Checks.Add(new CheckItem
                    {
                        Passed = check.IsAvailable,
                        Name = tool,
                        Detail = check.IsAvailable ? check.Path : "NOT FOUND",
                        InstallHint = check.IsAvailable ? null : defaultHint
                    });
                }
                result.Groups.Add(group);
            }

            // --- Python modules group ---
            var pythonGroup = new CheckGroup { Name = "Python Libraries" };
            foreach (var (module, hint) in PythonModules)
            {
                var check = CheckPythonModuleInEnvironment(bashrcPath, module);
                pythonGroup.Checks.Add(new CheckItem
                {
                    Passed = check,
                    Name = module,
                    Detail = check ? "available" : "NOT FOUND",
                    InstallHint = check ? null : hint
                });
            }
            result.Groups.Add(pythonGroup);

            // --- Determine overall validity ---
            result.IsValid = result.TotalChecks == result.PassedChecks;

            // --- Write config on success ---
            if (result.IsValid)
            {
                OpenFoamEnvironment.SaveConfig(new FoamScriptConfig
                {
                    OpenFoamBashrc = bashrcPath,
                    OpenFoamVersion = version ?? "",
                    ConfiguredAt = DateTime.UtcNow
                });
            }

            return result;
        }

        /// <summary>
        /// Checks if a tool is available within the OpenFOAM-sourced environment.
        /// </summary>
        internal ToolCheck CheckToolInEnvironment(string bashrcPath, string toolName)
        {
            var result = _processExecutor.Execute("bash",
                $"-c \"source '{bashrcPath}' 2>/dev/null && which {toolName}\"");

            if (result.ExitCode == 0 && !string.IsNullOrWhiteSpace(result.Output))
            {
                return new ToolCheck { IsAvailable = true, Path = result.Output.Trim() };
            }

            return new ToolCheck { IsAvailable = false, Path = null };
        }

        /// <summary>
        /// Checks if a Python module is importable.
        /// </summary>
        internal bool CheckPythonModuleInEnvironment(string bashrcPath, string moduleName)
        {
            var result = _processExecutor.Execute("bash",
                $"-c \"source '{bashrcPath}' 2>/dev/null && python3 -c 'import {moduleName}'\"");

            return result.ExitCode == 0;
        }
    }

    /// <summary>
    /// Result of checking tool availability.
    /// </summary>
    public class ToolCheck
    {
        public bool IsAvailable { get; set; }
        public string? Path { get; set; }
    }
}
