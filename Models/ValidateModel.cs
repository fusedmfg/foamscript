using CommandLine;

namespace foamscript.Models
{
    /// <summary>
    /// Command verb for validating OpenFOAM environment.
    /// Usage: foamscript validate
    /// </summary>
    [Verb("validate", HelpText = "Validate OpenFOAM environment and tool availability.")]
    public class ValidateModel : VerbModel
    {
        [Option('v', "verbose", Required = false, Default = false,
            HelpText = "Show detailed information about all checks.")]
        public bool Verbose { get; set; }

        [Option('q', "quiet", Required = false, Default = false,
            HelpText = "Only show errors, suppress success messages.")]
        public bool Quiet { get; set; }
    }
}
