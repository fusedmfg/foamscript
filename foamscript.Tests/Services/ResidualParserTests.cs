using Xunit;
using FluentAssertions;
using foamscript.Services;
using foamscript.Models;

namespace foamscript.Tests.Services
{
    public class ResidualParserTests : IDisposable
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

        // ── ParseLogContent ─────────────────────────────────────────────────────

        [Fact]
        public void ParseLogContent_EmptyString_ReturnsEmptyList()
        {
            var entries = ResidualParser.ParseLogContent("");
            entries.Should().BeEmpty();
        }

        [Fact]
        public void ParseLogContent_NoResidualLines_ReturnsEmptyList()
        {
            var content = """
                Build  : _75ea6908-OpenFOAM-v2512
                Exec   : simpleFoam -case /path/to/case
                Date   : Mar 05 2026
                """;

            var entries = ResidualParser.ParseLogContent(content);
            entries.Should().BeEmpty();
        }

        [Fact]
        public void ParseLogContent_SingleIteration_ParsesFieldsCorrectly()
        {
            var content = """
                Time = 1

                smoothSolver:  Solving for Ux, Initial residual = 0.234567, Final residual = 0.012345, No Iterations 3
                smoothSolver:  Solving for Uy, Initial residual = 0.345678, Final residual = 0.023456, No Iterations 4
                smoothSolver:  Solving for Uz, Initial residual = 0.456789, Final residual = 0.034567, No Iterations 5
                GAMG:  Solving for p, Initial residual = 1.0, Final residual = 0.00456, No Iterations 8
                """;

            var entries = ResidualParser.ParseLogContent(content);

            entries.Should().HaveCount(4);
            entries[0].Iteration.Should().Be(1);
            entries[0].Field.Should().Be("Ux");
            entries[0].InitialResidual.Should().BeApproximately(0.234567, 1e-6);
            entries[0].FinalResidual.Should().BeApproximately(0.012345, 1e-6);

            entries[3].Field.Should().Be("p");
            entries[3].InitialResidual.Should().Be(1.0);
        }

        [Fact]
        public void ParseLogContent_MultipleIterations_TracksTimeSteps()
        {
            var content = """
                Time = 1

                smoothSolver:  Solving for Ux, Initial residual = 0.9, Final residual = 0.1, No Iterations 3
                GAMG:  Solving for p, Initial residual = 1.0, Final residual = 0.5, No Iterations 8

                Time = 2

                smoothSolver:  Solving for Ux, Initial residual = 0.5, Final residual = 0.05, No Iterations 3
                GAMG:  Solving for p, Initial residual = 0.6, Final residual = 0.1, No Iterations 8

                Time = 3

                smoothSolver:  Solving for Ux, Initial residual = 0.1, Final residual = 0.01, No Iterations 3
                GAMG:  Solving for p, Initial residual = 0.2, Final residual = 0.02, No Iterations 8
                """;

            var entries = ResidualParser.ParseLogContent(content);

            entries.Should().HaveCount(6);

            // Iteration 1
            entries[0].Iteration.Should().Be(1);
            entries[1].Iteration.Should().Be(1);

            // Iteration 2
            entries[2].Iteration.Should().Be(2);
            entries[3].Iteration.Should().Be(2);

            // Iteration 3
            entries[4].Iteration.Should().Be(3);
            entries[5].Iteration.Should().Be(3);

            // Convergence: Ux initial residual should decrease
            entries[0].InitialResidual.Should().BeGreaterThan(entries[2].InitialResidual);
            entries[2].InitialResidual.Should().BeGreaterThan(entries[4].InitialResidual);
        }

        [Fact]
        public void ParseLogContent_ScientificNotation_ParsesCorrectly()
        {
            var content = """
                Time = 500

                smoothSolver:  Solving for Ux, Initial residual = 3.45e-05, Final residual = 1.23e-06, No Iterations 2
                GAMG:  Solving for p, Initial residual = 2.1E-04, Final residual = 8.9E-07, No Iterations 5
                """;

            var entries = ResidualParser.ParseLogContent(content);

            entries.Should().HaveCount(2);
            entries[0].InitialResidual.Should().BeApproximately(3.45e-05, 1e-10);
            entries[0].FinalResidual.Should().BeApproximately(1.23e-06, 1e-10);
            entries[1].InitialResidual.Should().BeApproximately(2.1e-04, 1e-10);
        }

        [Fact]
        public void ParseLogContent_TurbulenceFields_Parsed()
        {
            var content = """
                Time = 1

                smoothSolver:  Solving for k, Initial residual = 0.123, Final residual = 0.001, No Iterations 3
                smoothSolver:  Solving for omega, Initial residual = 0.456, Final residual = 0.002, No Iterations 4
                """;

            var entries = ResidualParser.ParseLogContent(content);

            entries.Should().HaveCount(2);
            entries[0].Field.Should().Be("k");
            entries[1].Field.Should().Be("omega");
        }

        [Fact]
        public void ParseLogContent_V2512RealFormat_ParsesAllFields()
        {
            // Real v2512 log output format with PBiCGStab solver
            var content = """
                Time = 1

                PBiCGStab:  Solving for Ux, Initial residual = 1, Final residual = 0.000746783, No Iterations 1
                PBiCGStab:  Solving for Uy, Initial residual = 1, Final residual = 0.00222899, No Iterations 1
                PBiCGStab:  Solving for Uz, Initial residual = 1, Final residual = 0.000643912, No Iterations 1
                GAMG:  Solving for p, Initial residual = 1, Final residual = 0.00338457, No Iterations 4
                PBiCGStab:  Solving for k, Initial residual = 1, Final residual = 0.000171303, No Iterations 1
                PBiCGStab:  Solving for omega, Initial residual = 1, Final residual = 8.32296e-06, No Iterations 1
                """;

            var entries = ResidualParser.ParseLogContent(content);

            entries.Should().HaveCount(6);
            var fields = entries.Select(e => e.Field).ToList();
            fields.Should().Contain("Ux");
            fields.Should().Contain("Uy");
            fields.Should().Contain("Uz");
            fields.Should().Contain("p");
            fields.Should().Contain("k");
            fields.Should().Contain("omega");
        }

        // ── FindLogFile ─────────────────────────────────────────────────────────

        [Fact]
        public void FindLogFile_SimpleFoamLog_ReturnsPath()
        {
            var caseDir = Path.Combine(Path.GetTempPath(), $"foamscript-test-residual-{Guid.NewGuid()}");
            Directory.CreateDirectory(caseDir);
            _tempDirs.Add(caseDir);

            var logPath = Path.Combine(caseDir, "log.simpleFoam");
            File.WriteAllText(logPath, "some log content");

            var result = ResidualParser.FindLogFile(caseDir);

            result.Should().Be(logPath);
        }

        [Fact]
        public void FindLogFile_PimpleFoamLog_ReturnsPath()
        {
            var caseDir = Path.Combine(Path.GetTempPath(), $"foamscript-test-residual-{Guid.NewGuid()}");
            Directory.CreateDirectory(caseDir);
            _tempDirs.Add(caseDir);

            var logPath = Path.Combine(caseDir, "log.pimpleFoam");
            File.WriteAllText(logPath, "some log content");

            var result = ResidualParser.FindLogFile(caseDir);

            result.Should().Be(logPath);
        }

        [Fact]
        public void FindLogFile_NoLogFile_ReturnsNull()
        {
            var caseDir = Path.Combine(Path.GetTempPath(), $"foamscript-test-residual-{Guid.NewGuid()}");
            Directory.CreateDirectory(caseDir);
            _tempDirs.Add(caseDir);

            var result = ResidualParser.FindLogFile(caseDir);

            result.Should().BeNull();
        }

        // ── ParseLogFile ────────────────────────────────────────────────────────

        [Fact]
        public void ParseLogFile_NonExistentFile_ReturnsEmptyList()
        {
            var entries = ResidualParser.ParseLogFile("/nonexistent/log.simpleFoam");
            entries.Should().BeEmpty();
        }

        [Fact]
        public void ParseLogFile_ValidFile_ParsesEntries()
        {
            var caseDir = Path.Combine(Path.GetTempPath(), $"foamscript-test-residual-{Guid.NewGuid()}");
            Directory.CreateDirectory(caseDir);
            _tempDirs.Add(caseDir);

            var logContent = """
                Time = 1

                smoothSolver:  Solving for Ux, Initial residual = 0.5, Final residual = 0.01, No Iterations 3
                GAMG:  Solving for p, Initial residual = 1.0, Final residual = 0.1, No Iterations 5
                """;

            var logPath = Path.Combine(caseDir, "log.simpleFoam");
            File.WriteAllText(logPath, logContent);

            var entries = ResidualParser.ParseLogFile(logPath);

            entries.Should().HaveCount(2);
            entries[0].Field.Should().Be("Ux");
            entries[1].Field.Should().Be("p");
        }

        // ── CaseResidualData.ByField ────────────────────────────────────────────

        [Fact]
        public void CaseResidualData_ByField_GroupsCorrectly()
        {
            var data = new CaseResidualData
            {
                CaseName = "Test_0.0",
                Entries = new List<ResidualEntry>
                {
                    new() { Iteration = 1, Field = "Ux", InitialResidual = 0.9, FinalResidual = 0.1 },
                    new() { Iteration = 1, Field = "p", InitialResidual = 1.0, FinalResidual = 0.5 },
                    new() { Iteration = 2, Field = "Ux", InitialResidual = 0.5, FinalResidual = 0.05 },
                    new() { Iteration = 2, Field = "p", InitialResidual = 0.6, FinalResidual = 0.1 },
                }
            };

            var grouped = data.ByField();

            grouped.Should().HaveCount(2);
            grouped["Ux"].Should().HaveCount(2);
            grouped["p"].Should().HaveCount(2);
        }
    }
}
