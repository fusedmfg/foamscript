using Xunit;
using FluentAssertions;
using foamscript.Services;
using foamscript.Models;

namespace foamscript.Tests.Services
{
    public class ResultsServiceTests : IDisposable
    {
        private readonly ResultsService _service;
        private readonly List<string> _tempDirs = new();

        public ResultsServiceTests()
        {
            _service = new ResultsService();
        }

        public void Dispose()
        {
            foreach (var dir in _tempDirs)
            {
                if (Directory.Exists(dir))
                    Directory.Delete(dir, recursive: true);
            }
        }

        private string CreateStudyWithResults(params (string caseName, string coeffData)[] cases)
        {
            var studyDir = Path.Combine(Path.GetTempPath(), $"foamscript-test-results-{Guid.NewGuid()}");
            Directory.CreateDirectory(studyDir);
            _tempDirs.Add(studyDir);

            foreach (var (caseName, coeffData) in cases)
            {
                var caseDir = Path.Combine(studyDir, caseName);
                Directory.CreateDirectory(Path.Combine(caseDir, "constant"));
                Directory.CreateDirectory(Path.Combine(caseDir, "system"));

                if (coeffData != null)
                {
                    var postDir = Path.Combine(caseDir, "postProcessing", "forces", "0");
                    Directory.CreateDirectory(postDir);
                    File.WriteAllText(Path.Combine(postDir, "coefficient.dat"), coeffData);
                }
            }

            return studyDir;
        }

        // v2512 format: 13 columns — Time Cd Cd(f) Cd(r) Cl Cl(f) Cl(r) CmPitch CmRoll CmYaw Cs Cs(f) Cs(r)
        // Sub-columns use half-values so aggregate = Cd(f) + Cd(r), etc.
        private static string MakeCoeffLine(double time, double cd, double cl, double cm) =>
            $"{time:F4}\t{cd:F6}\t{cd / 2:F6}\t{cd / 2:F6}\t{cl:F6}\t{cl / 2:F6}\t{cl / 2:F6}\t{cm:F6}\t0.000100\t0.000050\t0.001000\t0.000500\t0.000500";

        private static string MakeCoeffFile(params (double time, double cd, double cl, double cm)[] entries)
        {
            var lines = new List<string> { "# Time        Cd            Cd(f)         Cd(r)         Cl            Cl(f)         Cl(r)         CmPitch       CmRoll        CmYaw         Cs            Cs(f)         Cs(r)" };
            foreach (var (time, cd, cl, cm) in entries)
                lines.Add(MakeCoeffLine(time, cd, cl, cm));
            return string.Join("\n", lines);
        }

        // ── ExtractResults — Input Validation ────────────────────────────────────

        [Fact]
        public void ExtractResults_NonExistentDir_ReturnsFailure()
        {
            var result = _service.ExtractResults("/nonexistent/study", 0.1);

            result.IsSuccess.Should().BeFalse();
            result.ErrorMessage.Should().Contain("not found");
        }

        [Fact]
        public void ExtractResults_NoCases_ReturnsFailure()
        {
            var studyDir = Path.Combine(Path.GetTempPath(), $"foamscript-test-{Guid.NewGuid()}");
            Directory.CreateDirectory(studyDir);
            _tempDirs.Add(studyDir);

            var result = _service.ExtractResults(studyDir, 0.1);

            result.IsSuccess.Should().BeFalse();
            result.ErrorMessage.Should().Contain("No valid OpenFOAM cases");
        }

        // ── ExtractResults — Success ─────────────────────────────────────────────

        [Fact]
        public void ExtractResults_WithData_ExtractsCoefficients()
        {
            var coeffData = MakeCoeffFile(
                (0.0, 0.045, 0.120, 0.012),
                (0.1, 0.046, 0.121, 0.013));

            var studyDir = CreateStudyWithResults(
                ("Study_0.0", coeffData),
                ("Study_5.0", coeffData));

            var result = _service.ExtractResults(studyDir, 0.1);

            result.IsSuccess.Should().BeTrue();
            result.Cases.Should().HaveCount(2);
            result.Cases[0].AngleOfAttack.Should().Be(0.0);
            result.Cases[1].AngleOfAttack.Should().Be(5.0);
            result.Cases[0].Cd.Should().NotBeNull();
            result.Cases[0].Cl.Should().NotBeNull();
        }

        [Fact]
        public void ExtractResults_SortsByAngle()
        {
            var coeffData = MakeCoeffFile((0.1, 0.045, 0.120, 0.012));

            var studyDir = CreateStudyWithResults(
                ("Study_10.0", coeffData),
                ("Study_-5.0", coeffData),
                ("Study_0.0", coeffData));

            var result = _service.ExtractResults(studyDir, 0.1);

            result.Cases[0].AngleOfAttack.Should().Be(-5.0);
            result.Cases[1].AngleOfAttack.Should().Be(0.0);
            result.Cases[2].AngleOfAttack.Should().Be(10.0);
        }

        [Fact]
        public void ExtractResults_MissingPostProcessing_MarksNotConverged()
        {
            var studyDir = CreateStudyWithResults(("Study_0.0", null!));

            var result = _service.ExtractResults(studyDir, 0.1);

            result.IsSuccess.Should().BeTrue();
            result.Cases.Should().HaveCount(1);
            result.Cases[0].Converged.Should().BeFalse();
            result.Cases[0].ErrorMessage.Should().Contain("No force coefficient data");
        }

        // ── FormatTable ──────────────────────────────────────────────────────────

        [Fact]
        public void FormatTable_ContainsHeaderAndData()
        {
            var summary = new ResultsSummary
            {
                IsSuccess = true,
                StudyDir = "/tmp/MyStudy",
                Cases = new List<CaseResult>
                {
                    new() { CaseName = "MyStudy_0.0", AngleOfAttack = 0.0, Cd = 0.045, Cl = 0.001, CmPitch = 0.001, Converged = true },
                    new() { CaseName = "MyStudy_5.0", AngleOfAttack = 5.0, Cd = 0.046, Cl = 0.125, CmPitch = 0.013, Converged = true }
                }
            };

            var output = ResultsService.FormatTable(summary);

            output.Should().Contain("Study Results: MyStudy");
            output.Should().Contain("AoA");
            output.Should().Contain("0.045");
            output.Should().Contain("0.125");
        }

        // ── FormatCsv ────────────────────────────────────────────────────────────

        [Fact]
        public void FormatCsv_ContainsHeaderAndData()
        {
            var summary = new ResultsSummary
            {
                IsSuccess = true,
                Cases = new List<CaseResult>
                {
                    new() { CaseName = "Study_0.0", AngleOfAttack = 0.0, Cd = 0.045, Cl = 0.001, CmPitch = 0.001, Converged = true }
                }
            };

            var csv = ResultsService.FormatCsv(summary);

            csv.Should().Contain("AoA,Cd,Cl,CmPitch,Converged");
            csv.Should().Contain("0.0,0.045000");
        }

        // ── FormatJson ───────────────────────────────────────────────────────────

        [Fact]
        public void FormatJson_ValidJson()
        {
            var summary = new ResultsSummary
            {
                IsSuccess = true,
                StudyDir = "/tmp/study",
                Cases = new List<CaseResult>
                {
                    new() { CaseName = "Study_0.0", AngleOfAttack = 0.0, Cd = 0.045, Cl = 0.001, CmPitch = 0.001, Converged = true }
                }
            };

            var json = ResultsService.FormatJson(summary);

            json.Should().Contain("\"isSuccess\": true");
            json.Should().Contain("\"cd\": 0.045");
        }

        // ── CaseDiscovery ────────────────────────────────────────────────────────

        [Fact]
        public void ParseAngleFromCaseName_ValidFormat_ReturnsAngle()
        {
            CaseDiscovery.ParseAngleFromCaseName("MyStudy_5.0").Should().Be(5.0);
            CaseDiscovery.ParseAngleFromCaseName("MyStudy_-5.0").Should().Be(-5.0);
            CaseDiscovery.ParseAngleFromCaseName("MyStudy_0.0").Should().Be(0.0);
            CaseDiscovery.ParseAngleFromCaseName("MyStudy_10.5").Should().Be(10.5);
        }

        [Fact]
        public void ParseAngleFromCaseName_InvalidFormat_ReturnsNull()
        {
            CaseDiscovery.ParseAngleFromCaseName("MyStudy").Should().BeNull();
            CaseDiscovery.ParseAngleFromCaseName("NoUnderscore").Should().BeNull();
            CaseDiscovery.ParseAngleFromCaseName("Study_abc").Should().BeNull();
        }

        [Fact]
        public void DiscoverCases_SkipsGeometryAndDotDirs()
        {
            var studyDir = Path.Combine(Path.GetTempPath(), $"foamscript-test-discover-{Guid.NewGuid()}");
            Directory.CreateDirectory(studyDir);
            _tempDirs.Add(studyDir);

            // Valid case
            var validCase = Path.Combine(studyDir, "Study_0.0");
            Directory.CreateDirectory(Path.Combine(validCase, "constant"));
            Directory.CreateDirectory(Path.Combine(validCase, "system"));

            // Should be skipped
            var geometry = Path.Combine(studyDir, "geometry");
            Directory.CreateDirectory(geometry);
            var dotDir = Path.Combine(studyDir, ".hidden");
            Directory.CreateDirectory(dotDir);
            var noConstant = Path.Combine(studyDir, "incomplete");
            Directory.CreateDirectory(noConstant);

            var cases = CaseDiscovery.DiscoverCases(studyDir);

            cases.Should().HaveCount(1);
            Path.GetFileName(cases[0]).Should().Be("Study_0.0");
        }

        // ── Generic Coefficients Dictionary ──────────────────────────────────────

        [Fact]
        public void CaseResult_BackwardCompatAccessors_ReadWriteThroughDictionary()
        {
            var result = new CaseResult();
            result.Cd = 0.055;
            result.Cl = 0.170;
            result.CmPitch = -0.040;

            result.Coefficients["Cd"].Should().Be(0.055);
            result.Coefficients["Cl"].Should().Be(0.170);
            result.Coefficients["CmPitch"].Should().Be(-0.040);
        }

        [Fact]
        public void CaseResult_GenericCoefficients_AccessibleViaBackwardCompat()
        {
            var result = new CaseResult();
            result.Coefficients["Cd"] = 0.055;
            result.Coefficients["Cl"] = 0.170;
            result.Coefficients["CmPitch"] = -0.040;

            result.Cd.Should().Be(0.055);
            result.Cl.Should().Be(0.170);
            result.CmPitch.Should().Be(-0.040);
        }

        [Fact]
        public void CaseResult_CustomCoefficients_AppearInDictionaryOnly()
        {
            var result = new CaseResult();
            result.Coefficients["Cp"] = 1.23;
            result.Coefficients["Cf"] = 0.005;

            result.Coefficients.Should().ContainKey("Cp");
            result.Coefficients.Should().ContainKey("Cf");
            result.Coefficients["Cp"].Should().Be(1.23);
        }

        [Fact]
        public void FormatCsv_WithCustomCoefficients_IncludesAllKeys()
        {
            var summary = new ResultsSummary
            {
                IsSuccess = true,
                Cases = new List<CaseResult>
                {
                    new()
                    {
                        CaseName = "Study_0.0",
                        AngleOfAttack = 0.0,
                        Converged = true,
                        Coefficients = new Dictionary<string, double?>
                        {
                            ["Cd"] = 0.045,
                            ["Cl"] = 0.170,
                            ["Cp"] = 1.23
                        }
                    }
                }
            };

            var csv = ResultsService.FormatCsv(summary);

            csv.Should().Contain("AoA,Cd,Cl,Cp,Converged");
            csv.Should().Contain("0.0,0.045000,0.170000,1.230000,True");
        }

        [Fact]
        public void GetCoefficientKeys_EmptyCases_ReturnsFallbackKeys()
        {
            var summary = new ResultsSummary { Cases = new List<CaseResult>() };
            var keys = ResultsService.GetCoefficientKeys(summary);

            keys.Should().BeEquivalentTo(new[] { "Cd", "Cl", "CmPitch" });
        }

        [Fact]
        public void GetCoefficientKeys_MergesKeysFromAllCases()
        {
            var summary = new ResultsSummary
            {
                Cases = new List<CaseResult>
                {
                    new() { Coefficients = new Dictionary<string, double?> { ["Cd"] = 0.05, ["Cl"] = 0.17 } },
                    new() { Coefficients = new Dictionary<string, double?> { ["Cd"] = 0.06, ["Cl"] = 0.18, ["Cp"] = 1.0 } }
                }
            };

            var keys = ResultsService.GetCoefficientKeys(summary);
            keys.Should().BeEquivalentTo(new[] { "Cd", "Cl", "Cp" });
        }
    }
}
