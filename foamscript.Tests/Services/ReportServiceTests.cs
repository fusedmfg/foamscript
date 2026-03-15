using Xunit;
using FluentAssertions;
using foamscript.Models;
using foamscript.Services;

namespace foamscript.Tests.Services
{
    public class ReportServiceTests : IDisposable
    {
        private readonly List<string> _tempDirs = new();

        public void Dispose()
        {
            foreach (var dir in _tempDirs)
            {
                if (Directory.Exists(dir))
                    Directory.Delete(dir, recursive: true);
            }
        }

        private string CreateCaseWithUFile(string uContent)
        {
            var studyDir = Path.Combine(Path.GetTempPath(), $"foamscript-test-report-{Guid.NewGuid()}");
            var caseDir = Path.Combine(studyDir, "AoA_0.0");
            Directory.CreateDirectory(Path.Combine(caseDir, "0"));
            Directory.CreateDirectory(Path.Combine(caseDir, "constant"));
            Directory.CreateDirectory(Path.Combine(caseDir, "system"));
            _tempDirs.Add(studyDir);

            File.WriteAllText(Path.Combine(caseDir, "0", "U"), uContent);

            // Minimal controlDict so CaseDiscovery finds this as a valid case
            File.WriteAllText(Path.Combine(caseDir, "system", "controlDict"),
                "FoamFile { version 2.0; format ascii; class dictionary; object controlDict; }\napplication simpleFoam;\nendTime 1000;");

            return studyDir;
        }

        // ── RPM Extraction ──────────────────────────────────────────────────────

        [Fact]
        public void ReadPhysicsConfig_OmegaConstant_ExtractsRpm()
        {
            // v2512 rotatingWallVelocity format: omega constant <value>
            var uContent = @"FoamFile { version 2.0; format ascii; class volVectorField; object U; }
dimensions [0 1 -1 0 0 0 0];
internalField uniform (20 0 0);
boundaryField
{
    disc
    {
        type rotatingWallVelocity;
        origin (0 0 0);
        axis (0 0 1);
        omega           constant 104.71975512;
    }
}";
            var studyDir = CreateCaseWithUFile(uContent);
            var config = ReportService.ReadPhysicsConfig(studyDir);

            config.Rpm.Should().NotBe("N/A");
            // 104.71975512 rad/s = 1000 RPM
            double.Parse(config.Rpm).Should().BeApproximately(1000, 1);
        }

        [Fact]
        public void ReadPhysicsConfig_OmegaWithoutConstant_ExtractsRpm()
        {
            // Legacy format: omega <value> (without constant keyword)
            var uContent = @"FoamFile { version 2.0; format ascii; class volVectorField; object U; }
dimensions [0 1 -1 0 0 0 0];
internalField uniform (20 0 0);
boundaryField
{
    disc
    {
        type rotatingWallVelocity;
        origin (0 0 0);
        axis (0 0 1);
        omega           104.71975512;
    }
}";
            var studyDir = CreateCaseWithUFile(uContent);
            var config = ReportService.ReadPhysicsConfig(studyDir);

            config.Rpm.Should().NotBe("N/A");
            double.Parse(config.Rpm).Should().BeApproximately(1000, 1);
        }

        [Fact]
        public void ReadPhysicsConfig_NoUFile_RpmIsNA()
        {
            var studyDir = Path.Combine(Path.GetTempPath(), $"foamscript-test-report-{Guid.NewGuid()}");
            var caseDir = Path.Combine(studyDir, "AoA_0.0");
            Directory.CreateDirectory(Path.Combine(caseDir, "constant"));
            Directory.CreateDirectory(Path.Combine(caseDir, "system"));
            _tempDirs.Add(studyDir);

            File.WriteAllText(Path.Combine(caseDir, "system", "controlDict"),
                "FoamFile { version 2.0; format ascii; class dictionary; object controlDict; }\napplication simpleFoam;\nendTime 1000;");

            var config = ReportService.ReadPhysicsConfig(studyDir);

            config.Rpm.Should().Be("N/A");
        }

        [Fact]
        public void ReadPhysicsConfig_ZeroOmega_ExtractsZeroRpm()
        {
            // Non-rotating case: omega = 0
            var uContent = @"FoamFile { version 2.0; format ascii; class volVectorField; object U; }
dimensions [0 1 -1 0 0 0 0];
internalField uniform (20 0 0);
boundaryField
{
    disc
    {
        type rotatingWallVelocity;
        origin (0 0 0);
        axis (0 0 1);
        omega           constant 0;
    }
}";
            var studyDir = CreateCaseWithUFile(uContent);
            var config = ReportService.ReadPhysicsConfig(studyDir);

            config.Rpm.Should().NotBe("N/A");
            double.Parse(config.Rpm).Should().BeApproximately(0, 1);
        }

        // ── CSV Export ────────────────────────────────────────────────────────────

        [Fact]
        public void WriteCoefficientCsv_ProducesValidCsvWithHeaderAndData()
        {
            var csvPath = Path.Combine(Path.GetTempPath(), $"foamscript-test-csv-{Guid.NewGuid()}.csv");

            var summary = new ResultsSummary
            {
                StudyDir = "/tmp/test",
                IsSuccess = true,
                Cases = new List<CaseResult>
                {
                    new() { CaseName = "AoA_-5.0", AngleOfAttack = -5.0, Cd = 0.065, Cl = 0.189, CmPitch = -0.071, Converged = true },
                    new() { CaseName = "AoA_0.0", AngleOfAttack = 0.0, Cd = 0.062, Cl = 0.237, CmPitch = -0.060, Converged = true },
                    new() { CaseName = "AoA_5.0", AngleOfAttack = 5.0, Cd = 0.061, Cl = 0.285, CmPitch = -0.049, Converged = true }
                }
            };

            var config = new PhysicsConfig
            {
                Velocity = "27.0",
                Rpm = "925",
                Nu = "1.5E-05",
                SolverName = "simpleFoam",
                MaxIterations = "500",
                RefinementMin = "5",
                RefinementMax = "6",
                TurbulenceIntensity = "1.0"
            };

            try
            {
                ReportService.WriteCoefficientCsv(csvPath, "TestStudy", summary, config);

                File.Exists(csvPath).Should().BeTrue();
                var lines = File.ReadAllLines(csvPath);

                // Should have comment header lines + column header + 3 data rows
                var commentLines = lines.Where(l => l.StartsWith("#")).ToList();
                commentLines.Should().HaveCountGreaterThan(5);
                commentLines.Should().Contain(l => l.Contains("Velocity: 27.0 m/s"));
                commentLines.Should().Contain(l => l.Contains("RPM: 925"));

                var dataLines = lines.Where(l => !l.StartsWith("#")).ToList();
                dataLines[0].Should().Be("AoA_deg,Cd,Cl,CmPitch,Converged");
                dataLines.Should().HaveCount(4); // header + 3 data rows

                // Verify first data row
                var fields = dataLines[1].Split(',');
                fields[0].Should().Be("-5.0");
                double.Parse(fields[1]).Should().BeApproximately(0.065, 0.001);
                double.Parse(fields[2]).Should().BeApproximately(0.189, 0.001);
                fields[4].Should().Be("True");
            }
            finally
            {
                if (File.Exists(csvPath)) File.Delete(csvPath);
            }
        }

        // ── Velocity Extraction ─────────────────────────────────────────────────

        [Fact]
        public void ReadPhysicsConfig_ExtractsVelocityMagnitude()
        {
            // ux=17.32, uz=10.0 → magnitude ≈ 20.0
            var uContent = @"FoamFile { version 2.0; format ascii; class volVectorField; object U; }
dimensions [0 1 -1 0 0 0 0];
internalField uniform (17.32 0 10.0);
boundaryField { }";
            var studyDir = CreateCaseWithUFile(uContent);
            var config = ReportService.ReadPhysicsConfig(studyDir);

            config.Velocity.Should().NotBe("N/A");
            double.Parse(config.Velocity).Should().BeApproximately(20.0, 0.1);
        }
    }
}
