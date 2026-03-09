using Xunit;
using FluentAssertions;
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
