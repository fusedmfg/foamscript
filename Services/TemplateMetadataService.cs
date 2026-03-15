using System.Text.Json;
using foamscript.Models;

namespace foamscript.Services
{
    /// <summary>
    /// Loads template metadata from TEMPLATE.json files. Throws if the file is missing
    /// or contains invalid JSON — every template must have a valid TEMPLATE.json.
    /// </summary>
    public class TemplateMetadataService
    {
        private const string MetadataFileName = "TEMPLATE.json";

        /// <summary>
        /// Loads metadata from the TEMPLATE.json in the given template directory.
        /// Throws FileNotFoundException if the file is missing.
        /// Throws JsonException if the file contains invalid JSON.
        /// </summary>
        public TemplateMetadata LoadMetadata(string templatePath)
        {
            var jsonPath = Path.Combine(templatePath, MetadataFileName);

            if (!File.Exists(jsonPath))
            {
                throw new FileNotFoundException(
                    $"Template metadata file not found: {jsonPath}", jsonPath);
            }

            var json = File.ReadAllText(jsonPath);
            var metadata = JsonSerializer.Deserialize<TemplateMetadata>(json)
                ?? throw new JsonException("Deserialized TEMPLATE.json was null.");
            return metadata;
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
