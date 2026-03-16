using CommandLine;

namespace foamscript.Models
{
    /// <summary>
    /// Command verb for validating OpenFOAM environment.
    /// Usage: foamscript validate
    /// </summary>
    [Verb("validate", HelpText = "Validate OpenFOAM environment, check all 23 dependencies, and write ~/.foamscript/config.json.")]
    public class ValidateModel : VerbModel
    {
        [Option('q', "quiet", Required = false, Default = false,
            HelpText = "Only show failures (exit code only, useful in scripts).")]
        public bool Quiet { get; set; }
    }
}
