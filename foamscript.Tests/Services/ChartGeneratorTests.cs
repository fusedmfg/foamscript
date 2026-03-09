using Xunit;
using FluentAssertions;
using foamscript.Services;
using foamscript.Models;

namespace foamscript.Tests.Services
{
    public class ChartGeneratorTests
    {
        private readonly ChartGenerator _generator = new();

        // ── Polar Charts ────────────────────────────────────────────────────────

        [Fact]
        public void GeneratePolarChartSvg_MultiplePoints_ReturnsSvg()
        {
            var cases = new List<CaseResult>
            {
                new() { CaseName = "S_-2.5", AngleOfAttack = -2.5, Cd = 0.055, Cl = -0.10, CmPitch = 0.01, Converged = true },
                new() { CaseName = "S_0.0", AngleOfAttack = 0.0, Cd = 0.057, Cl = 0.23, CmPitch = -0.05, Converged = true },
                new() { CaseName = "S_2.5", AngleOfAttack = 2.5, Cd = 0.056, Cl = 0.55, CmPitch = -0.12, Converged = true },
            };

            var svg = _generator.GeneratePolarChartSvg(cases, "Cl", "Lift Coefficient vs AoA", "C\u2097");

            svg.Should().NotBeNullOrEmpty();
            svg.Should().Contain("<svg");
            svg.Should().Contain("</svg>");
        }

        [Fact]
        public void GeneratePolarChartSvg_SinglePoint_ReturnsSvg()
        {
            var cases = new List<CaseResult>
            {
                new() { CaseName = "S_0.0", AngleOfAttack = 0.0, Cd = 0.057, Cl = 0.23, CmPitch = -0.05, Converged = true },
            };

            var svg = _generator.GeneratePolarChartSvg(cases, "Cl", "Lift Coefficient", "C\u2097");

            svg.Should().NotBeNullOrEmpty();
            svg.Should().Contain("<svg");
        }

        [Fact]
        public void GeneratePolarChartSvg_NoConvergedCases_ReturnsEmptyChart()
        {
            var cases = new List<CaseResult>
            {
                new() { CaseName = "S_0.0", AngleOfAttack = 0.0, Converged = false },
            };

            var svg = _generator.GeneratePolarChartSvg(cases, "Cl", "Lift Coefficient", "C\u2097");

            svg.Should().NotBeNullOrEmpty();
            svg.Should().Contain("<svg");
            svg.Should().Contain("No converged data");
        }

        [Fact]
        public void GeneratePolarChartPng_ReturnsValidPng()
        {
            var cases = new List<CaseResult>
            {
                new() { CaseName = "S_0.0", AngleOfAttack = 0.0, Cd = 0.057, Cl = 0.23, CmPitch = -0.05, Converged = true },
                new() { CaseName = "S_5.0", AngleOfAttack = 5.0, Cd = 0.060, Cl = 0.50, CmPitch = -0.10, Converged = true },
            };

            var png = _generator.GeneratePolarChartPng(cases, "Cd", "Drag Coefficient vs AoA", "C\u2084");

            png.Should().NotBeNullOrEmpty();
            png.Length.Should().BeGreaterThan(100); // PNG header is at least ~8 bytes
        }

        [Fact]
        public void GeneratePolarChartSvg_LdCoefficient_Works()
        {
            var cases = new List<CaseResult>
            {
                new() { CaseName = "S_0.0", AngleOfAttack = 0.0, Cd = 0.05, Cl = 0.20, Converged = true },
                new() { CaseName = "S_5.0", AngleOfAttack = 5.0, Cd = 0.06, Cl = 0.50, Converged = true },
            };

            var svg = _generator.GeneratePolarChartSvg(cases, "L/D", "L/D vs AoA", "L/D");

            svg.Should().NotBeNullOrEmpty();
            svg.Should().Contain("<svg");
        }

        // ── Drag Polar ──────────────────────────────────────────────────────────

        [Fact]
        public void GenerateDragPolarSvg_WithData_ReturnsSvg()
        {
            var cases = new List<CaseResult>
            {
                new() { CaseName = "S_-5.0", AngleOfAttack = -5.0, Cd = 0.060, Cl = -0.20, Converged = true },
                new() { CaseName = "S_0.0", AngleOfAttack = 0.0, Cd = 0.055, Cl = 0.23, Converged = true },
                new() { CaseName = "S_5.0", AngleOfAttack = 5.0, Cd = 0.060, Cl = 0.50, Converged = true },
            };

            var svg = _generator.GenerateDragPolarSvg(cases);

            svg.Should().NotBeNullOrEmpty();
            svg.Should().Contain("<svg");
            svg.Should().Contain("Drag Polar");
        }

        [Fact]
        public void GenerateDragPolarPng_WithData_ReturnsValidPng()
        {
            var cases = new List<CaseResult>
            {
                new() { CaseName = "S_0.0", AngleOfAttack = 0.0, Cd = 0.055, Cl = 0.23, Converged = true },
                new() { CaseName = "S_5.0", AngleOfAttack = 5.0, Cd = 0.060, Cl = 0.50, Converged = true },
            };

            var png = _generator.GenerateDragPolarPng(cases);

            png.Should().NotBeNullOrEmpty();
            png.Length.Should().BeGreaterThan(100);
        }

        // ── Residual Charts ─────────────────────────────────────────────────────

        [Fact]
        public void GenerateResidualChartSvg_WithData_ReturnsSvg()
        {
            var data = new CaseResidualData
            {
                CaseName = "Study_0.0",
                Entries = new List<ResidualEntry>
                {
                    new() { Iteration = 1, Field = "Ux", InitialResidual = 0.9, FinalResidual = 0.1 },
                    new() { Iteration = 2, Field = "Ux", InitialResidual = 0.5, FinalResidual = 0.05 },
                    new() { Iteration = 3, Field = "Ux", InitialResidual = 0.1, FinalResidual = 0.01 },
                    new() { Iteration = 1, Field = "p", InitialResidual = 1.0, FinalResidual = 0.5 },
                    new() { Iteration = 2, Field = "p", InitialResidual = 0.6, FinalResidual = 0.1 },
                    new() { Iteration = 3, Field = "p", InitialResidual = 0.2, FinalResidual = 0.02 },
                }
            };

            var svg = _generator.GenerateResidualChartSvg(data);

            svg.Should().NotBeNullOrEmpty();
            svg.Should().Contain("<svg");
            svg.Should().Contain("Convergence");
        }

        [Fact]
        public void GenerateResidualChartSvg_NoData_ReturnsEmptyChart()
        {
            var data = new CaseResidualData
            {
                CaseName = "Study_0.0",
                Entries = new List<ResidualEntry>()
            };

            var svg = _generator.GenerateResidualChartSvg(data);

            svg.Should().NotBeNullOrEmpty();
            svg.Should().Contain("No residual data");
        }

        [Fact]
        public void GenerateResidualChartPng_WithData_ReturnsValidPng()
        {
            var data = new CaseResidualData
            {
                CaseName = "Study_0.0",
                Entries = new List<ResidualEntry>
                {
                    new() { Iteration = 1, Field = "Ux", InitialResidual = 0.9, FinalResidual = 0.1 },
                    new() { Iteration = 100, Field = "Ux", InitialResidual = 1e-5, FinalResidual = 1e-6 },
                    new() { Iteration = 1, Field = "p", InitialResidual = 1.0, FinalResidual = 0.5 },
                    new() { Iteration = 100, Field = "p", InitialResidual = 1e-4, FinalResidual = 1e-5 },
                }
            };

            var png = _generator.GenerateResidualChartPng(data);

            png.Should().NotBeNullOrEmpty();
            png.Length.Should().BeGreaterThan(100);
        }

        [Fact]
        public void GenerateResidualChartSvg_VerySmallResiduals_HandlesLogTransform()
        {
            var data = new CaseResidualData
            {
                CaseName = "Study_0.0",
                Entries = new List<ResidualEntry>
                {
                    new() { Iteration = 1, Field = "Ux", InitialResidual = 1e-10, FinalResidual = 1e-12 },
                    new() { Iteration = 2, Field = "Ux", InitialResidual = 1e-15, FinalResidual = 1e-18 },
                }
            };

            // Should not throw even with very small residuals
            var svg = _generator.GenerateResidualChartSvg(data);
            svg.Should().NotBeNullOrEmpty();
        }

        // ── SVG ViewBox ──────────────────────────────────────────────────────────

        [Fact]
        public void GeneratePolarChartSvg_ContainsViewBox()
        {
            var cases = new List<CaseResult>
            {
                new() { CaseName = "S_0.0", AngleOfAttack = 0.0, Cd = 0.057, Cl = 0.23, CmPitch = -0.05, Converged = true },
            };

            var svg = _generator.GeneratePolarChartSvg(cases, "Cl", "Lift Coefficient", "C\u2097");

            svg.Should().Contain("viewBox=\"0 0 800 500\"");
        }

        [Fact]
        public void EnsureSvgViewBox_AddsMissingViewBox()
        {
            var svg = "<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"800\" height=\"500\"><rect/></svg>";
            var result = ChartGenerator.EnsureSvgViewBox(svg);

            result.Should().Contain("viewBox=\"0 0 800 500\"");
        }

        [Fact]
        public void EnsureSvgViewBox_PreservesExistingViewBox()
        {
            var svg = "<svg width=\"800\" height=\"500\" viewBox=\"0 0 800 500\"><rect/></svg>";
            var result = ChartGenerator.EnsureSvgViewBox(svg);

            result.Should().Be(svg);
        }

        [Fact]
        public void GenerateResidualChartSvg_ZeroResidual_ClampedToFloor()
        {
            var data = new CaseResidualData
            {
                CaseName = "Study_0.0",
                Entries = new List<ResidualEntry>
                {
                    new() { Iteration = 1, Field = "Ux", InitialResidual = 0.0, FinalResidual = 0.0 },
                    new() { Iteration = 2, Field = "Ux", InitialResidual = 0.1, FinalResidual = 0.01 },
                }
            };

            // Should not throw — zero residuals clamped to 1e-20
            var svg = _generator.GenerateResidualChartSvg(data);
            svg.Should().NotBeNullOrEmpty();
        }
    }
}
