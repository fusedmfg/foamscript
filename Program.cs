using System.Reflection;
using CommandLine;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;
using foamscript.Services;
using foamscript.Models;

namespace foamscript
{
    /// <summary>
    /// The Program class serves as the entry point for the application. It is responsible for setting up the application environment, configuring logging, building the host, and running the main application service. It also includes error handling to ensure that any exceptions during startup or execution are logged appropriately, and that resources are released properly on exit.
    /// </summary>
    public class Program
    {
        /// <summary>
        /// Exit code to be returned when the application exits. Initialized to 0 (success) and can be set to a non-zero value in case of errors or specific exit conditions.
        /// </summary>
        private static int exitCode = 0;

        /// <summary>
        /// The main entry point of the application. It sets up the environment, configures logging, builds the host, and runs the main application service. It also includes error handling to log any exceptions that occur during startup or execution, and ensures that resources are properly released on exit.
        /// </summary>
        /// <param name="args"></param>
        static void Main(string[] args)
        {
            // Set a timestamp environment variable for use in logging and other operations.
            Environment.SetEnvironmentVariable("FOAMSCRIPT_TIMESTAMP", DateTime.Now.ToString("yyyyMMdd_HHmmss"));

            // Fail-safe logger configuration to ensure that any issues during startup are logged.
            Log.Logger = new LoggerConfiguration()
                .WriteTo.Console()
                .CreateBootstrapLogger();

            try
            {
                // Discover all types in the executing assembly that are decorated with the VerbAttribute, which indicates that they are command-line verbs for the application. This allows for dynamic command registration and handling based on the attributes defined in the code.
                var verbTypes = Assembly.GetExecutingAssembly().GetTypes()
                    .Where(t => t.GetCustomAttribute<VerbAttribute>() != null)
                    .ToArray();

                // Use the documented overload for dynamic type arrays
                var result = Parser.Default.ParseArguments(args, verbTypes);

                // Check the result of the command-line parsing. If the parsing fails (i.e., if the result is NotParsed), set the exit code to -1 to indicate an error and return immediately, preventing the application from starting with invalid arguments.
                if (result.Tag == ParserResultType.NotParsed)
                {
                    Log.Fatal("Failed to parse command-line arguments. Exiting application.");
                    exitCode = -1;
                    return;
                }

                // Application startup configuration.
                var builder = Host.CreateApplicationBuilder(args);

                // Serilog setup.
                builder.Services.AddSerilog(lc => lc
                    .ReadFrom.Configuration(builder.Configuration)
                    .WriteTo.Console());

                // Application service modules.
                builder.Services.AddSingleton<LoggingService>();
                builder.Services.AddSingleton<IProcessExecutor, ProcessExecutor>();
                builder.Services.AddTransient<EnvironmentService>();
                builder.Services.AddTransient<GeometryService>();
                builder.Services.AddTransient<TemplateService>();
                builder.Services.AddTransient<CaseService>();
                builder.Services.AddTransient<AppService>();
                builder.Services.AddTransient<MeshService>();

                // Build the host and initialize the main application class.
                using IHost host = builder.Build();
                var app = host.Services.GetRequiredService<AppService>();

                // Run the app.
                exitCode = result.MapResult(
                    (VerbModel model) => app.Run(model), // Pass the "instruction" to the "orchestrator"
                    errs => 1); 

            }
            catch (Exception ex)
            {
                Log.Fatal(ex, "The application terminated unexpectedly.");
            }
            finally
            {
                Log.CloseAndFlush();
                Environment.Exit(exitCode);
            }
        }
    }
}