using System.Text.Json;
using foamscript.Models;

namespace foamscript.Services
{
    /// <summary>
    /// Loads template metadata from TEMPLATE.json files. Falls back to disc defaults
    /// when the file is missing, ensuring backward compatibility.
    /// </summary>
    public class TemplateMetadataService
    {
        private const string MetadataFileName = "TEMPLATE.json";

        /// <summary>
        /// Loads metadata from the TEMPLATE.json in the given template directory.
        /// Returns disc-compatible defaults if the file is missing or invalid.
        /// </summary>
        public TemplateMetadata LoadMetadata(string templatePath)
        {
            var jsonPath = Path.Combine(templatePath, MetadataFileName);

            if (!File.Exists(jsonPath))
            {
                return CreateDiscDefaults(templatePath);
            }

            try
            {
                var json = File.ReadAllText(jsonPath);
                var metadata = JsonSerializer.Deserialize<TemplateMetadata>(json);
                return metadata ?? CreateDiscDefaults(templatePath);
            }
            catch (JsonException)
            {
                return CreateDiscDefaults(templatePath);
            }
        }

        private static TemplateMetadata CreateDiscDefaults(string templatePath)
        {
            return new TemplateMetadata
            {
                Name = Path.GetFileName(templatePath),
                Description = "Template (no TEMPLATE.json found — using disc defaults)",
                Solver = "simpleFoam",
                GeometryType = "disc",
                GeometryStlName = "disc.stl",
                ReferenceDimension = "diameter",
                ReferenceAreaFormula = "circular",
                RequiresRotorZone = true,
                RequiredStlFiles = ["disc.stl", "tunnel.stl"],
                Validation = new GeometryValidation
                {
                    MinSize = 0.18,
                    MaxSize = 0.35,
                    WarningMessage = "Disc diameter outside expected PDGA range (0.21-0.30m). Ensure correct --input-units."
                }
            };
        }

        /// <summary>
        /// Calculates the reference dimension from a bounding box based on the metadata rule.
        /// </summary>
        public static double CalculateReferenceDimension(BoundingBox bbox, TemplateMetadata metadata)
        {
            return metadata.ReferenceDimension switch
            {
                "chord" => bbox.Width,    // X-extent for airfoils
                _       => Math.Max(bbox.Width, bbox.Depth)  // "diameter" — current disc behavior
            };
        }

        /// <summary>
        /// Calculates the reference area from a reference dimension based on the metadata rule.
        /// </summary>
        public static double CalculateReferenceArea(double refLength, BoundingBox bbox, TemplateMetadata metadata)
        {
            return metadata.ReferenceAreaFormula switch
            {
                "rectangular" => refLength * bbox.Height,    // chord * span (Y-extent) for airfoils
                _             => Math.PI * Math.Pow(refLength / 2.0, 2)  // "circular" — current disc behavior
            };
        }
    }
}
