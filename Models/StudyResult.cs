namespace foamscript.Models
{
    /// <summary>
    /// Result of study creation operation.
    /// </summary>
    public class StudyResult
    {
        /// <summary>
        /// Whether the study creation was successful.
        /// </summary>
        public bool IsSuccess { get; set; }

        /// <summary>
        /// Error message if creation failed.
        /// </summary>
        public string? ErrorMessage { get; set; }

        /// <summary>
        /// Study name (derived from output directory).
        /// </summary>
        public string? StudyName { get; set; }

        /// <summary>
        /// Study directory path.
        /// </summary>
        public string? StudyDir { get; set; }

        /// <summary>
        /// List of created case directories.
        /// </summary>
        public List<CaseInfo> Cases { get; set; } = new List<CaseInfo>();
    }

    /// <summary>
    /// Information about a created case.
    /// </summary>
    public class CaseInfo
    {
        /// <summary>
        /// Angle of attack in degrees.
        /// </summary>
        public double AngleOfAttack { get; set; }

        /// <summary>
        /// Case directory path.
        /// </summary>
        public string CaseDir { get; set; } = string.Empty;

        /// <summary>
        /// Calculated X velocity component (m/s).
        /// </summary>
        public double Ux { get; set; }

        /// <summary>
        /// Calculated Z velocity component (m/s).
        /// </summary>
        public double Uz { get; set; }

        /// <summary>
        /// Rotation speed (rad/s).
        /// </summary>
        public double Omega { get; set; }
    }
}
