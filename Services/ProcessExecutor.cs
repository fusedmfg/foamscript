using System.Diagnostics;
using foamscript.Models;

namespace foamscript.Services
{
    /// <summary>
    /// Executes shell processes and returns results.
    /// </summary>
    public class ProcessExecutor : IProcessExecutor
    {
        private Dictionary<string, string>? _environmentVariables;

        public void SetEnvironment(Dictionary<string, string> environmentVariables)
        {
            _environmentVariables = environmentVariables;
        }

        public ProcessResult Execute(string command, string arguments = "")
        {
            var processInfo = new ProcessStartInfo
            {
                FileName = command,
                Arguments = arguments,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            // Inject captured OpenFOAM environment variables
            if (_environmentVariables != null)
            {
                foreach (var (key, value) in _environmentVariables)
                {
                    processInfo.EnvironmentVariables[key] = value;
                }
            }

            using var process = new Process { StartInfo = processInfo };
            process.Start();

            string output = process.StandardOutput.ReadToEnd();
            string error = process.StandardError.ReadToEnd();

            process.WaitForExit();

            return new ProcessResult
            {
                ExitCode = process.ExitCode,
                Output = output,
                Error = error
            };
        }
    }
}
