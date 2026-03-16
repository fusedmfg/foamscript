using System.Text.Json;
using foamscript.Models;

namespace foamscript.Services
{
    /// <summary>
    /// Manages OpenFOAM environment: loads config, sources bashrc,
    /// captures env vars for injection into ProcessExecutor.
    /// </summary>
    public class OpenFoamEnvironment
    {
        private readonly IProcessExecutor _processExecutor;
        private Dictionary<string, string>? _capturedEnv;

        public OpenFoamEnvironment(IProcessExecutor processExecutor)
        {
            _processExecutor = processExecutor;
        }

        /// <summary>
        /// The captured OpenFOAM environment variables. Null until Initialize() is called.
        /// </summary>
        public Dictionary<string, string>? CapturedEnv => _capturedEnv;

        /// <summary>
        /// Initializes the OpenFOAM environment from the config file.
        /// Called at startup for commands that need OpenFOAM.
        /// Returns null on success, or an error message on failure.
        /// </summary>
        public string? Initialize(string? configPath = null)
        {
            configPath ??= FoamScriptConfig.ConfigPath;
            var config = LoadConfig(configPath);

            if (config == null)
                return "FoamScript is not configured. Run 'foamscript validate' to set up your environment.";

            if (!File.Exists(config.OpenFoamBashrc))
                return $"OpenFOAM bashrc not found at {config.OpenFoamBashrc}\n" +
                       "  The installation may have been moved or upgraded.\n" +
                       "  Run 'foamscript validate' to reconfigure.";

            _capturedEnv = CaptureEnvironment(config.OpenFoamBashrc);

            if (_capturedEnv == null)
                return $"Failed to source OpenFOAM bashrc at {config.OpenFoamBashrc}\n" +
                       "  Check that it is a valid OpenFOAM installation.";

            if (!_capturedEnv.ContainsKey("WM_PROJECT_DIR"))
                return $"OpenFOAM bashrc at {config.OpenFoamBashrc} did not set WM_PROJECT_DIR.\n" +
                       "  This may not be a valid OpenFOAM installation.\n" +
                       "  Run 'foamscript validate' to reconfigure.";

            return null; // Success
        }

        /// <summary>
        /// Sources the given bashrc and captures the resulting environment variables.
        /// Returns null if sourcing fails.
        /// </summary>
        public Dictionary<string, string>? CaptureEnvironment(string bashrcPath)
        {
            var result = _processExecutor.Execute("bash",
                $"-c \"source '{bashrcPath}' 2>/dev/null && env\"");

            if (result.ExitCode != 0)
                return null;

            var env = new Dictionary<string, string>();
            foreach (var line in result.Output.Split('\n', StringSplitOptions.RemoveEmptyEntries))
            {
                var eqIdx = line.IndexOf('=');
                if (eqIdx > 0)
                {
                    var key = line[..eqIdx];
                    var value = line[(eqIdx + 1)..];
                    env[key] = value;
                }
            }

            return env.Count > 0 ? env : null;
        }

        public static FoamScriptConfig? LoadConfig(string? configPath = null)
        {
            configPath ??= FoamScriptConfig.ConfigPath;
            if (!File.Exists(configPath))
                return null;

            try
            {
                var json = File.ReadAllText(configPath);
                return JsonSerializer.Deserialize<FoamScriptConfig>(json, FoamScriptConfig.JsonOptions);
            }
            catch
            {
                return null;
            }
        }

        public static void SaveConfig(FoamScriptConfig config, string? configPath = null)
        {
            configPath ??= FoamScriptConfig.ConfigPath;
            var dir = Path.GetDirectoryName(configPath)!;
            Directory.CreateDirectory(dir);
            File.WriteAllText(configPath, JsonSerializer.Serialize(config, FoamScriptConfig.JsonOptions));
        }
    }
}
