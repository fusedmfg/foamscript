using foamscript.Models;

namespace foamscript.Services
{
    /// <summary>
    /// Interface for executing shell processes.
    /// Allows mocking in tests.
    /// </summary>
    public interface IProcessExecutor
    {
        ProcessResult Execute(string command, string arguments = "");
        void SetEnvironment(Dictionary<string, string> environmentVariables);
    }
}
