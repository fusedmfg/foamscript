namespace foamscript.Models
{
    /// <summary>
    /// A single residual entry from an OpenFOAM solver log.
    /// </summary>
    public class ResidualEntry
    {
        public int Iteration { get; set; }
        public string Field { get; set; } = string.Empty;
        public double InitialResidual { get; set; }
        public double FinalResidual { get; set; }
    }

    /// <summary>
    /// Aggregated residual convergence data for a single case.
    /// </summary>
    public class CaseResidualData
    {
        public string CaseName { get; set; } = string.Empty;
        public string CaseDir { get; set; } = string.Empty;
        public List<ResidualEntry> Entries { get; set; } = new();

        /// <summary>
        /// Groups residual entries by field name (e.g., Ux, Uy, p, k, omega).
        /// </summary>
        public Dictionary<string, List<ResidualEntry>> ByField()
        {
            return Entries
                .GroupBy(e => e.Field)
                .ToDictionary(g => g.Key, g => g.ToList());
        }
    }
}
