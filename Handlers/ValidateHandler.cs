using foamscript.Models;
using foamscript.Services;

namespace foamscript.Handlers
{
    public class ValidateHandler : ICommandHandler<ValidateModel>
    {
        private readonly LoggingService _loggingService;
        private readonly EnvironmentService _environmentService;

        public ValidateHandler(LoggingService loggingService, EnvironmentService environmentService)
        {
            _loggingService = loggingService;
            _environmentService = environmentService;
        }

        public int Handle(ValidateModel model)
        {
            _loggingService.LogInformation("Validating OpenFOAM environment...");

            var result = _environmentService.ValidateEnvironment();

            if (!model.Quiet)
            {
                Console.WriteLine();
                Console.WriteLine("=== OpenFOAM Environment Validation ===");
                Console.WriteLine();
            }

            // Version check
            if (result.Version != null)
            {
                if (!model.Quiet)
                {
                    Console.WriteLine($"✓ OpenFOAM Version: {result.Version} detected");
                }
            }
            else
            {
                // Display the smart error message with auto-detection info
                Console.WriteLine($"✗ {result.ErrorMessage}");
                return -1;
            }

            // Environment variables
            if (model.Verbose && !model.Quiet)
            {
                Console.WriteLine();
                Console.WriteLine("Environment Variables:");
                foreach (var (key, value) in result.EnvironmentVariables)
                {
                    Console.WriteLine($"  ✓ {key}: {value}");
                }
                foreach (var missing in result.MissingVariables)
                {
                    Console.WriteLine($"  ✗ {missing}: NOT SET");
                }
            }
            else if (result.MissingVariables.Count > 0)
            {
                Console.WriteLine($"✗ Missing environment variables: {string.Join(", ", result.MissingVariables)}");
            }

            // Tools
            if (model.Verbose && !model.Quiet)
            {
                Console.WriteLine();
                Console.WriteLine("Required Tools:");
                foreach (var (tool, path) in result.AvailableTools)
                {
                    Console.WriteLine($"  ✓ {tool}: {path}");
                }
                foreach (var missing in result.MissingTools)
                {
                    Console.WriteLine($"  ✗ {missing}: NOT FOUND");
                }
            }
            else if (result.MissingTools.Count > 0)
            {
                Console.WriteLine($"✗ Missing tools: {string.Join(", ", result.MissingTools)}");
            }
            else if (!model.Quiet)
            {
                Console.WriteLine("✓ All required tools found");
            }

            // Optional tools (visualization)
            if (model.Verbose && !model.Quiet)
            {
                Console.WriteLine();
                Console.WriteLine("Optional Tools (flow visualization):");
                foreach (var (tool, path) in result.OptionalTools)
                {
                    Console.WriteLine($"  ✓ {tool}: {path}");
                }
                foreach (var missing in result.MissingOptionalTools)
                {
                    Console.WriteLine($"  ⚠ {missing}: NOT FOUND (report visualization will be skipped)");
                }
            }
            else if (result.MissingOptionalTools.Count > 0 && !model.Quiet)
            {
                Console.WriteLine($"⚠ Optional tools missing (flow visualization): {string.Join(", ", result.MissingOptionalTools)}");
            }
            else if (result.OptionalTools.Count > 0 && !model.Quiet)
            {
                Console.WriteLine("✓ Flow visualization tools found (pvpython, python3/matplotlib)");
            }

            // Summary
            if (!model.Quiet)
            {
                Console.WriteLine();
                if (result.IsValid)
                {
                    Console.WriteLine("✓ Summary: All checks passed. FoamScript is ready to use.");
                    _loggingService.LogInformation("Environment validation successful.");
                }
                else
                {
                    Console.WriteLine("✗ Summary: Some checks failed. Please fix the issues above.");
                    _loggingService.LogError("Environment validation failed.", null!);
                }
                Console.WriteLine();
            }

            return result.IsValid ? 0 : -1;
        }
    }
}
