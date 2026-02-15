namespace foamscript
{
    public class App
    {
        private readonly LoggingService _loggingService;

        public App(LoggingService loggingService)
        {
            _loggingService = loggingService;
        }

        public int Run()
        {
            try 
            {
                // Log the application start.
                _loggingService.LogInformation("Starting application...");

                // Write a message to the console.
                Console.WriteLine("Hello, World!");

                // Log the application end.
                _loggingService.LogInformation("Exiting application...");

                // Return a success exit code.
                return 0;
            }
            catch (Exception ex)
            {
                _loggingService.LogError("An error occurred during application execution: {Message}", ex);

                // Return a failure exit code.
                return -1;
            }            
        }
    }
}