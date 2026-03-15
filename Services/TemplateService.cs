using Scriban;
using System.Text;

namespace foamscript.Services;

public class TemplateService
{
    private readonly LoggingService _loggingService;

    public TemplateService(LoggingService loggingService)
    {
        _loggingService = loggingService;
    }

    /// <summary>
    /// Process a template directory, replacing all Scriban template variables with actual values.
    /// </summary>
    /// <param name="templateDir">Source template directory</param>
    /// <param name="outputDir">Target output directory</param>
    /// <param name="context">Template context object containing all variables</param>
    public void ProcessTemplate(string templateDir, string outputDir, object context)
    {
        try
        {
            _loggingService.LogInformation($"Processing template from {templateDir} to {outputDir}");

            // Ensure output directory exists
            Directory.CreateDirectory(outputDir);

            // Process all files and subdirectories recursively
            ProcessDirectory(templateDir, outputDir, context);

            _loggingService.LogInformation("Template processing completed successfully");
        }
        catch (Exception ex)
        {
            _loggingService.LogError("Template processing failed", ex);
            throw;
        }
    }

    private void ProcessDirectory(string sourceDir, string targetDir, object context)
    {
        // Create target directory if it doesn't exist
        Directory.CreateDirectory(targetDir);

        // Process all files in current directory
        foreach (var sourceFile in Directory.GetFiles(sourceDir))
        {
            var fileName = Path.GetFileName(sourceFile);
            var targetFile = Path.Combine(targetDir, fileName);

            ProcessFile(sourceFile, targetFile, context);
        }

        // Recursively process subdirectories
        foreach (var sourceSubDir in Directory.GetDirectories(sourceDir))
        {
            var subDirName = Path.GetFileName(sourceSubDir);

            // Skip reference-only directories (not part of the OpenFOAM case)
            if (subDirName == "0.orig")
                continue;

            var targetSubDir = Path.Combine(targetDir, subDirName);

            ProcessDirectory(sourceSubDir, targetSubDir, context);
        }
    }

    private void ProcessFile(string sourceFile, string targetFile, object context)
    {
        try
        {
            // Read the template file
            var templateContent = File.ReadAllText(sourceFile);

            // Parse and render the template with Scriban
            var template = Template.Parse(templateContent);

            if (template.HasErrors)
            {
                var errors = string.Join(", ", template.Messages.Select(m => m.Message));
                _loggingService.LogError($"Template parsing errors in {sourceFile}: {errors}");
                // Copy file as-is if it has parsing errors (might not be a template file)
                File.Copy(sourceFile, targetFile, true);
                return;
            }

            var renderedContent = template.Render(context);

            // Write the processed content to the target file
            File.WriteAllText(targetFile, renderedContent);
        }
        catch (Exception ex)
        {
            _loggingService.LogError($"Error processing file {sourceFile}", ex);
            // Fall back to copying the file as-is
            File.Copy(sourceFile, targetFile, true);
        }
    }
}
