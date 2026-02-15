using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;

namespace foamscript
{
    public class Program
    {
        private static int exitCode = 0;

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
                // Application startup configuration.
                var builder = Host.CreateApplicationBuilder(args);

                // Serilog setup.
                builder.Services.AddSerilog(lc => lc
                    .ReadFrom.Configuration(builder.Configuration)
                    .WriteTo.Console());

                // Application service modules.
                builder.Services.AddSingleton<LoggingService>();
                builder.Services.AddTransient<App>();

                // Build the host and initialize the main application class.
                using IHost host = builder.Build();
                var app = host.Services.GetRequiredService<App>();

                // Run the app.
                exitCode = app.Run();           

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