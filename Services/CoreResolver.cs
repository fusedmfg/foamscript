namespace foamscript.Services
{
    /// <summary>
    /// Resolves the number of CPU cores to use for parallel execution.
    /// Uses auto-detection from Environment.ProcessorCount, with optional
    /// FOAMSCRIPT_MAX_CORES environment variable to cap the count.
    /// </summary>
    public static class CoreResolver
    {
        public const string MaxCoresEnvVar = "FOAMSCRIPT_MAX_CORES";

        /// <summary>
        /// Resolves the effective core count from CLI input.
        /// - cores > 0: use the specified value directly
        /// - cores == 0: auto-detect using Environment.ProcessorCount, capped by FOAMSCRIPT_MAX_CORES
        /// Returns a tuple of (resolvedCores, parallel).
        /// </summary>
        public static (int cores, bool parallel) Resolve(int requestedCores)
        {
            var cores = requestedCores > 0 ? requestedCores : DetectCores();
            var parallel = cores > 1;
            return (cores, parallel);
        }

        /// <summary>
        /// Detects available cores, capped by FOAMSCRIPT_MAX_CORES if set.
        /// </summary>
        internal static int DetectCores()
        {
            var available = Environment.ProcessorCount;

            var maxCoresEnv = Environment.GetEnvironmentVariable(MaxCoresEnvVar);
            if (!string.IsNullOrEmpty(maxCoresEnv) && int.TryParse(maxCoresEnv, out var maxCores) && maxCores > 0)
            {
                return Math.Min(available, maxCores);
            }

            return available;
        }
    }
}
