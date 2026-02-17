using CommandLine;

namespace foamscript.Models
{
    /// <summary>
    /// Model for the 'list-templates' verb - displays available OpenFOAM case templates.
    /// </summary>
    [Verb("list-templates", HelpText = "List available OpenFOAM case templates with descriptions.")]
    public class ListTemplatesModel : VerbModel
    {
        // No additional parameters needed - just list all templates
    }
}
