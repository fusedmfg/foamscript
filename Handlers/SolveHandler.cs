using foamscript.Models;
using foamscript.Services;

namespace foamscript.Handlers
{
    public class SolveHandler : ICommandHandler<SolveModel>
    {
        private readonly LoggingService _loggingService;
        private readonly SolverService _solverService;

        public SolveHandler(LoggingService loggingService, SolverService solverService)
        {
            _loggingService = loggingService;
            _solverService = solverService;
        }

        public int Handle(SolveModel model)
        {
            if (CaseDiscovery.IsCase(model.Dir))
                return HandleCase(model);

            if (Directory.Exists(model.Dir))
                return HandleStudy(model);

            Console.WriteLine($"✗ Directory not found: {model.Dir}");
            return -1;
        }

        private int HandleCase(SolveModel model)
        {
            var parallel = model.Parallel || model.Cores > 1;

            _loggingService.LogInformation($"Solving case at {model.Dir}");

            Console.WriteLine();
            Console.WriteLine("=== Solver Execution ===");
            Console.WriteLine();
            Console.WriteLine($"Case directory: {model.Dir}");
            Console.WriteLine($"Parallel: {(parallel ? $"Yes ({model.Cores} cores)" : "No")}");
            Console.WriteLine();

            var result = _solverService.SolveCase(model.Dir, parallel, model.Cores);

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

        private int HandleStudy(SolveModel model)
        {
            var parallel = model.Parallel || model.Cores > 1;

            _loggingService.LogInformation($"Solving study at {model.Dir}");

            Console.WriteLine();
            Console.WriteLine("=== Study Solver Execution ===");
            Console.WriteLine();
            Console.WriteLine($"Study directory: {model.Dir}");
            Console.WriteLine($"Parallel: {(parallel ? $"Yes ({model.Cores} cores per case)" : "No")}");
            Console.WriteLine();

            var result = _solverService.SolveStudy(model.Dir, parallel, model.Cores, continueOnError: true);

            Console.WriteLine();
            Console.WriteLine("=== Study Solve Summary ===");
            Console.WriteLine();
            Console.WriteLine($"Total cases: {result.TotalCases}");
            Console.WriteLine($"✓ Successful: {result.SuccessfulCases}");
            if (result.FailedCases > 0)
                Console.WriteLine($"✗ Failed: {result.FailedCases}");
            Console.WriteLine();

            Console.WriteLine($"{"Case Name",-30} {"Status",-10} {"Cd",10} {"Cl",10} {"Cm",10}");
            Console.WriteLine(new string('-', 75));

            foreach (var cs in result.CaseSummaries)
            {
                var status = cs.Success ? "✓ OK" : "✗ Failed";
                var cd = cs.Cd.HasValue ? $"{cs.Cd.Value:F6}" : "N/A";
                var cl = cs.Cl.HasValue ? $"{cs.Cl.Value:F6}" : "N/A";
                var cm = cs.CmPitch.HasValue ? $"{cs.CmPitch.Value:F6}" : "N/A";
                Console.WriteLine($"{cs.CaseName,-30} {status,-10} {cd,10} {cl,10} {cm,10}");
            }

            Console.WriteLine(new string('-', 75));
            Console.WriteLine();

            if (result.IsSuccess)
            {
                _loggingService.LogInformation("Study solver execution completed successfully.");
                return 0;
            }
            else
            {
                Console.WriteLine($"✗ {result.ErrorMessage}");
                Console.WriteLine($"  See log file for details: {_loggingService.GetLogFilePath()}");
                _loggingService.LogError($"Study solver failed: {result.ErrorMessage}", null!);
                return -1;
            }
        }
    }
}
