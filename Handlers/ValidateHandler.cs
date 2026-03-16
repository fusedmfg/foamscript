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
                Console.WriteLine("=== FoamScript Environment Validation ===");
                Console.WriteLine();
            }

            foreach (var group in result.Groups)
            {
                if (!model.Quiet)
                {
                    Console.WriteLine(group.Name);
                }

                foreach (var check in group.Checks)
                {
                    if (check.Passed)
                    {
                        if (!model.Quiet)
                        {
                            Console.WriteLine($"  ✓ {check.Name,-25} {check.Detail}");
                        }
                    }
                    else
                    {
                        Console.WriteLine($"  ✗ {check.Name,-25} {check.Detail}");
                        if (check.InstallHint != null)
                        {
                            Console.WriteLine($"    Install: {check.InstallHint}");
                        }
                    }
                }

                if (!model.Quiet)
                {
                    Console.WriteLine();
                }
            }

            // Summary
            if (!model.Quiet)
            {
                if (result.IsValid)
                {
                    Console.WriteLine($"=== {result.PassedChecks}/{result.TotalChecks} checks passed. FoamScript is ready to use. ===");
                    _loggingService.LogInformation("Environment validation successful.");
                }
                else
                {
                    Console.WriteLine($"=== {result.PassedChecks}/{result.TotalChecks} checks passed. {result.TotalChecks - result.PassedChecks} issue(s) must be resolved. ===");
                    _loggingService.LogError("Environment validation failed.", null!);
                }
                Console.WriteLine();
            }

            return result.IsValid ? 0 : -1;
        }
    }
}
