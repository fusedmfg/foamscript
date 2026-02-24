using foamscript.Models;
using foamscript.Services;

namespace foamscript.Handlers
{
    public class ListTemplatesHandler : ICommandHandler<ListTemplatesModel>
    {
        private readonly LoggingService _loggingService;

        public ListTemplatesHandler(LoggingService loggingService)
        {
            _loggingService = loggingService;
        }

        public int Handle(ListTemplatesModel model)
        {
            _loggingService.LogInformation("Listing available templates");

            Console.WriteLine();
            Console.WriteLine("=== Available OpenFOAM Templates ===");
            Console.WriteLine();

            var templatesDir = GetTemplatesDirectory();

            if (!Directory.Exists(templatesDir))
            {
                Console.WriteLine("✗ Templates directory not found");
                Console.WriteLine($"  Expected location: {templatesDir}");
                return -1;
            }

            var templates = Directory.GetDirectories(templatesDir)
                .Select(d => Path.GetFileName(d))
                .Where(name => !string.IsNullOrEmpty(name))
                .OrderBy(name => name)
                .ToList();

            if (templates.Count == 0)
            {
                Console.WriteLine("No templates found");
                return 0;
            }

            foreach (var templateName in templates)
            {
                var templatePath = Path.Combine(templatesDir, templateName!);
                var templateMdPath = Path.Combine(templatePath, "TEMPLATE.md");

                Console.WriteLine($"• {templateName}");

                // Try to read the first line of TEMPLATE.md for description
                if (File.Exists(templateMdPath))
                {
                    try
                    {
                        var lines = File.ReadLines(templateMdPath).Take(10).ToList();
                        // Look for classification info
                        foreach (var line in lines)
                        {
                            if (line.StartsWith("- **Domain**:"))
                            {
                                Console.WriteLine($"  Domain: {line.Replace("- **Domain**:", "").Trim()}");
                            }
                            else if (line.StartsWith("- **Feature**:"))
                            {
                                Console.WriteLine($"  Feature: {line.Replace("- **Feature**:", "").Trim()}");
                            }
                            else if (line.StartsWith("- **Solver**:"))
                            {
                                Console.WriteLine($"  Solver: {line.Replace("- **Solver**:", "").Trim()}");
                                break; // Stop after solver line
                            }
                        }
                    }
                    catch
                    {
                        // If we can't read the template file, just show the name
                    }
                }
                Console.WriteLine();
            }

            Console.WriteLine($"Total templates available: {templates.Count}");
            Console.WriteLine();
            Console.WriteLine("To use a template:");
            Console.WriteLine($"  foamscript new-study -t \"{templates[0]}\" ...");
            Console.WriteLine();
            Console.WriteLine("For more information:");
            Console.WriteLine("  See Templates/TEMPLATES.md in the repository");
            Console.WriteLine();

            return 0;
        }

        internal static string GetTemplatesDirectory()
        {
            var appDir = AppContext.BaseDirectory;
            var templatesDir = Path.Combine(appDir, "..", "..", "..", "Templates");
            return Path.GetFullPath(templatesDir);
        }
    }
}
