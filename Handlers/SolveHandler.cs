using foamscript.Models;
using foamscript.Services;

namespace foamscript.Handlers
{
    public class SolveHandler : ICommandHandler<SolveCaseModel>
    {
        private readonly LoggingService _loggingService;
        private readonly SolverService _solverService;

        public SolveHandler(LoggingService loggingService, SolverService solverService)
        {
            _loggingService = loggingService;
            _solverService = solverService;
        }

        public int Handle(SolveCaseModel model)
        {
            _loggingService.LogInformation($"Solving case at {model.CaseDir}");

            Console.WriteLine();
            Console.WriteLine("=== Solver Execution ===");
            Console.WriteLine();
            Console.WriteLine($"Case directory: {model.CaseDir}");
            Console.WriteLine($"Parallel: {(model.Parallel ? $"Yes ({model.Cores} cores)" : "No")}");
            Console.WriteLine();

            var result = _solverService.SolveCase(model.CaseDir, model.Parallel, model.Cores);

            if (result.IsSuccess)
            {
                Console.WriteLine();
                Console.WriteLine("=== Solve Summary ===");
                Console.WriteLine();

                if (result.SimulationTime.HasValue)
                    Console.WriteLine($"Simulation time: {result.SimulationTime.Value:F4} s");

                if (result.Cd.HasValue)
                {
                    Console.WriteLine($"Force Coefficients (time-averaged):");
                    Console.WriteLine($"  Cd: {result.Cd.Value:F6}");
                    Console.WriteLine($"  Cl: {result.Cl?.ToString("F6") ?? "N/A"}");
                    Console.WriteLine($"  Cm: {result.CmPitch?.ToString("F6") ?? "N/A"}");
                }

                if (result.Warnings.Count > 0)
                {
                    Console.WriteLine();
                    Console.WriteLine("Warnings:");
                    foreach (var warning in result.Warnings)
                        Console.WriteLine($"  ⚠ {warning}");
                }

                Console.WriteLine();
                _loggingService.LogInformation("Solver execution completed successfully.");
                return 0;
            }
            else
            {
                Console.WriteLine($"✗ Solver execution failed: {result.ErrorMessage}");
                Console.WriteLine($"  See log file for details: {_loggingService.GetLogFilePath()}");
                _loggingService.LogError("Solver execution failed.", null!);
                Console.WriteLine();
                return -1;
            }
        }
    }
}
