namespace foamscript.Services
{
    /// <summary>
    /// Shared utility for discovering valid OpenFOAM case directories within a study.
    /// Used by MeshService, SolverService, and ResultsService.
    /// </summary>
    public static class CaseDiscovery
    {
        /// <summary>
        /// Returns true if the directory is a valid OpenFOAM case (has constant/ and system/).
        /// </summary>
        public static bool IsCase(string dir)
        {
            return Directory.Exists(Path.Combine(dir, "constant"))
                && Directory.Exists(Path.Combine(dir, "system"));
        }

        /// <summary>
        /// Discovers all valid OpenFOAM case directories within a study directory.
        /// Skips geometry/, dot-directories, and directories without constant/ + system/.
        /// Returns results sorted alphabetically.
        /// </summary>
        public static List<string> DiscoverCases(string studyDir)
        {
            var caseDirs = new List<string>();

            var subdirs = Directory.GetDirectories(studyDir);

            foreach (var subdir in subdirs)
            {
                var dirName = Path.GetFileName(subdir);
                if (dirName == "geometry" || dirName.StartsWith("."))
                    continue;

                var constantDir = Path.Combine(subdir, "constant");
                var systemDir = Path.Combine(subdir, "system");

                if (Directory.Exists(constantDir) && Directory.Exists(systemDir))
                {
                    caseDirs.Add(subdir);
                }
            }

            caseDirs.Sort();
            return caseDirs;
        }

        /// <summary>
        /// Extracts the angle of attack from a case directory name.
        /// Expects format: {StudyName}_{angle} (e.g., "MyStudy_5.0" → 5.0)
        /// </summary>
        public static double? ParseAngleFromCaseName(string caseName)
        {
            var lastUnderscore = caseName.LastIndexOf('_');
            if (lastUnderscore < 0 || lastUnderscore >= caseName.Length - 1)
                return null;

            var angleStr = caseName[(lastUnderscore + 1)..];
            if (double.TryParse(angleStr, out var angle))
                return angle;

            return null;
        }
    }
}
