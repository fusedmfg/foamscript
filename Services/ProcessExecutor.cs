using System.Diagnostics;
using foamscript.Models;

namespace foamscript.Services
{
    /// <summary>
    /// Executes shell processes and returns results.
    /// </summary>
    public class ProcessExecutor : IProcessExecutor
    {
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
