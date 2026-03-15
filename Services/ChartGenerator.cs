using foamscript.Models;
using ScottPlot;

namespace foamscript.Services
{
    /// <summary>
    /// Generates AIAA-styled aerodynamic charts using ScottPlot.
    /// Charts are rendered as SVG for HTML reports and PNG for PDF reports.
    /// </summary>
    public class ChartGenerator
    {
        private const int ChartWidth = 800;
        private const int ChartHeight = 500;
        private const int PdfDpi = 300;

        /// <summary>
        /// Generates a polar chart (coefficient vs AoA) as SVG string.
        /// </summary>
        public string GeneratePolarChartSvg(List<CaseResult> cases, string coefficientField, string title, string yLabel)
        {
            var plot = new Plot();
            ApplyAiaaStyle(plot);

            var validCases = cases.Where(c => c.Converged).OrderBy(c => c.AngleOfAttack).ToList();
            if (validCases.Count == 0)
                return GenerateEmptyChartSvg(title, "No converged data");

            var xValues = validCases.Select(c => c.AngleOfAttack).ToArray();
            var yValues = GetCoefficientValues(validCases, coefficientField);

            if (validCases.Count == 1)
            {
                // Single point: marker only with annotation
                var scatter = plot.Add.Scatter(xValues, yValues);
                scatter.LineWidth = 0;
                scatter.MarkerSize = 10;
                scatter.MarkerShape = MarkerShape.FilledCircle;
                scatter.Color = ScottPlot.Color.FromHex("#1f77b4");

                // Annotate with value
                var annotation = plot.Add.Annotation($"{yValues[0]:F4}");
                annotation.Alignment = Alignment.LowerLeft;
            }
            else
            {
                // Multi-point: line + markers
                var scatter = plot.Add.Scatter(xValues, yValues);
                scatter.LineWidth = 2;
                scatter.MarkerSize = 8;
                scatter.MarkerShape = MarkerShape.FilledCircle;
                scatter.Color = ScottPlot.Color.FromHex("#1f77b4");
            }

            plot.Title(title);
            plot.XLabel("Angle of Attack (\u00b0)");
            plot.YLabel(yLabel);

            return EnsureSvgViewBox(plot.GetSvgXml(ChartWidth, ChartHeight));
        }

        /// <summary>
        /// Generates a polar chart as PNG byte array (for PDF embedding).
        /// </summary>
        public byte[] GeneratePolarChartPng(List<CaseResult> cases, string coefficientField, string title, string yLabel)
        {
            var plot = new Plot();
            ApplyAiaaStyle(plot);

            var validCases = cases.Where(c => c.Converged).OrderBy(c => c.AngleOfAttack).ToList();
            if (validCases.Count == 0)
                return GenerateEmptyChartPng(title, "No converged data");

            var xValues = validCases.Select(c => c.AngleOfAttack).ToArray();
            var yValues = GetCoefficientValues(validCases, coefficientField);

            if (validCases.Count == 1)
            {
                var scatter = plot.Add.Scatter(xValues, yValues);
                scatter.LineWidth = 0;
                scatter.MarkerSize = 10;
                scatter.MarkerShape = MarkerShape.FilledCircle;
                scatter.Color = ScottPlot.Color.FromHex("#1f77b4");
            }
            else
            {
                var scatter = plot.Add.Scatter(xValues, yValues);
                scatter.LineWidth = 2;
                scatter.MarkerSize = 8;
                scatter.MarkerShape = MarkerShape.FilledCircle;
                scatter.Color = ScottPlot.Color.FromHex("#1f77b4");
            }

            plot.Title(title);
            plot.XLabel("Angle of Attack (\u00b0)");
            plot.YLabel(yLabel);

            return plot.GetImageBytes(ChartWidth * PdfDpi / 96, ChartHeight * PdfDpi / 96, ImageFormat.Png);
        }

        /// <summary>
        /// Generates the classic drag polar (Cl vs Cd) as SVG.
        /// </summary>
        public string GenerateDragPolarSvg(List<CaseResult> cases)
        {
            var plot = new Plot();
            ApplyAiaaStyle(plot);

            var validCases = cases.Where(c => c.Converged && c.Cd.HasValue && c.Cl.HasValue)
                .OrderBy(c => c.AngleOfAttack).ToList();
            if (validCases.Count == 0)
                return GenerateEmptyChartSvg("Drag Polar", "No converged data");

            var cdValues = validCases.Select(c => c.Cd!.Value).ToArray();
            var clValues = validCases.Select(c => c.Cl!.Value).ToArray();

            if (validCases.Count == 1)
            {
                var scatter = plot.Add.Scatter(cdValues, clValues);
                scatter.LineWidth = 0;
                scatter.MarkerSize = 10;
                scatter.MarkerShape = MarkerShape.FilledCircle;
                scatter.Color = ScottPlot.Color.FromHex("#d62728");
            }
            else
            {
                var scatter = plot.Add.Scatter(cdValues, clValues);
                scatter.LineWidth = 2;
                scatter.MarkerSize = 8;
                scatter.MarkerShape = MarkerShape.FilledSquare;
                scatter.Color = ScottPlot.Color.FromHex("#d62728");
            }

            plot.Title("Drag Polar");
            plot.XLabel("Drag Coefficient, C\u2084");
            plot.YLabel("Lift Coefficient, C\u2097");

            return EnsureSvgViewBox(plot.GetSvgXml(ChartWidth, ChartHeight));
        }

        /// <summary>
        /// Generates drag polar as PNG for PDF.
        /// </summary>
        public byte[] GenerateDragPolarPng(List<CaseResult> cases)
        {
            var plot = new Plot();
            ApplyAiaaStyle(plot);

            var validCases = cases.Where(c => c.Converged && c.Cd.HasValue && c.Cl.HasValue)
                .OrderBy(c => c.AngleOfAttack).ToList();
            if (validCases.Count == 0)
                return GenerateEmptyChartPng("Drag Polar", "No converged data");

            var cdValues = validCases.Select(c => c.Cd!.Value).ToArray();
            var clValues = validCases.Select(c => c.Cl!.Value).ToArray();

            var scatter = plot.Add.Scatter(cdValues, clValues);
            scatter.LineWidth = validCases.Count == 1 ? 0 : 2;
            scatter.MarkerSize = validCases.Count == 1 ? 10 : 8;
            scatter.MarkerShape = MarkerShape.FilledSquare;
            scatter.Color = ScottPlot.Color.FromHex("#d62728");

            plot.Title("Drag Polar");
            plot.XLabel("Drag Coefficient, C\u2084");
            plot.YLabel("Lift Coefficient, C\u2097");

            return plot.GetImageBytes(ChartWidth * PdfDpi / 96, ChartHeight * PdfDpi / 96, ImageFormat.Png);
        }

        /// <summary>
        /// Generates a residual convergence plot (log scale Y) as SVG.
        /// Residuals are log₁₀-transformed for plotting; tick labels display as 10⁻ⁿ.
        /// </summary>
        public string GenerateResidualChartSvg(CaseResidualData residualData)
        {
            var plot = new Plot();
            ApplyAiaaStyle(plot);

            var byField = residualData.ByField();
            if (byField.Count == 0)
                return GenerateEmptyChartSvg($"Convergence: {residualData.CaseName}", "No residual data");

            PopulateResidualPlot(plot, byField);
            ConfigureResidualAxes(plot, residualData.CaseName);

            return EnsureSvgViewBox(plot.GetSvgXml(ChartWidth, ChartHeight));
        }

        /// <summary>
        /// Generates a residual convergence plot as PNG for PDF.
        /// </summary>
        public byte[] GenerateResidualChartPng(CaseResidualData residualData)
        {
            var plot = new Plot();
            ApplyAiaaStyle(plot);

            var byField = residualData.ByField();
            if (byField.Count == 0)
                return GenerateEmptyChartPng($"Convergence: {residualData.CaseName}", "No residual data");

            PopulateResidualPlot(plot, byField);
            ConfigureResidualAxes(plot, residualData.CaseName);

            return plot.GetImageBytes(ChartWidth * PdfDpi / 96, ChartHeight * PdfDpi / 96, ImageFormat.Png);
        }

        /// <summary>
        /// Adds log₁₀-transformed residual scatter series to the plot.
        /// </summary>
        private static void PopulateResidualPlot(Plot plot, Dictionary<string, List<ResidualEntry>> byField)
        {
            var colors = GetFieldColors();
            int colorIndex = 0;

            foreach (var (field, entries) in byField)
            {
                if (entries.Count == 0) continue;

                var iterations = entries.Select(e => (double)e.Iteration).ToArray();
                // Log₁₀ transform: clamp to 1e-20 to avoid -∞
                var logResiduals = entries.Select(e => Math.Log10(Math.Max(e.InitialResidual, 1e-20))).ToArray();

                var scatter = plot.Add.Scatter(iterations, logResiduals);
                scatter.LineWidth = 1.5f;
                scatter.MarkerSize = 0;
                scatter.Color = colors[colorIndex % colors.Length];
                scatter.LegendText = field;

                colorIndex++;
            }
        }

        /// <summary>
        /// Configures axis labels and log-scale tick formatting for residual plots.
        /// </summary>
        private static void ConfigureResidualAxes(Plot plot, string caseName)
        {
            plot.Title($"Residual Convergence: {caseName}");
            plot.XLabel("Iteration");
            plot.YLabel("Residual");

            // Y-axis is in log₁₀ space: format ticks as 10⁻ⁿ
            var tickGen = new ScottPlot.TickGenerators.NumericAutomatic();
            tickGen.LabelFormatter = v => $"1e{v:F0}";
            plot.Axes.Left.TickGenerator = tickGen;

            plot.ShowLegend(Alignment.UpperRight);
        }

        /// <summary>
        /// Applies AIAA publication-quality styling to a plot.
        /// </summary>
        private static void ApplyAiaaStyle(Plot plot)
        {
            plot.FigureBackground.Color = ScottPlot.Color.FromHex("#ffffff");
            plot.DataBackground.Color = ScottPlot.Color.FromHex("#ffffff");

            // Grid styling — light gray, thin lines
            plot.Grid.MajorLineColor = ScottPlot.Color.FromHex("#e0e0e0");
            plot.Grid.MajorLineWidth = 0.5f;
            plot.Grid.MinorLineColor = ScottPlot.Color.FromHex("#f0f0f0");
            plot.Grid.MinorLineWidth = 0.3f;

            // Axis styling
            plot.Axes.Bottom.FrameLineStyle.Width = 1.5f;
            plot.Axes.Left.FrameLineStyle.Width = 1.5f;
            plot.Axes.Bottom.FrameLineStyle.Color = ScottPlot.Color.FromHex("#333333");
            plot.Axes.Left.FrameLineStyle.Color = ScottPlot.Color.FromHex("#333333");

            // Font sizes
            plot.Axes.Title.Label.FontSize = 16;
            plot.Axes.Bottom.Label.FontSize = 13;
            plot.Axes.Left.Label.FontSize = 13;
        }

        private static double[] GetCoefficientValues(List<CaseResult> cases, string field)
        {
            return field.ToLowerInvariant() switch
            {
                "cl" => cases.Select(c => c.Cl ?? 0).ToArray(),
                "cd" => cases.Select(c => c.Cd ?? 0).ToArray(),
                "cmpitch" or "cm" => cases.Select(c => c.CmPitch ?? 0).ToArray(),
                "ld" or "l/d" or "cl/cd" => cases.Select(c => c.Cd > 0 ? (c.Cl ?? 0) / c.Cd.Value : 0).ToArray(),
                _ => cases.Select(c => 0.0).ToArray()
            };
        }

        private static ScottPlot.Color[] GetFieldColors()
        {
            return new[]
            {
                ScottPlot.Color.FromHex("#1f77b4"), // Ux - blue
                ScottPlot.Color.FromHex("#ff7f0e"), // Uy - orange
                ScottPlot.Color.FromHex("#2ca02c"), // Uz - green
                ScottPlot.Color.FromHex("#d62728"), // p  - red
                ScottPlot.Color.FromHex("#9467bd"), // k  - purple
                ScottPlot.Color.FromHex("#8c564b"), // omega - brown
            };
        }

        private string GenerateEmptyChartSvg(string title, string message)
        {
            var plot = new Plot();
            ApplyAiaaStyle(plot);
            plot.Title(title);
            var annotation = plot.Add.Annotation(message);
            annotation.Alignment = Alignment.MiddleCenter;
            return EnsureSvgViewBox(plot.GetSvgXml(ChartWidth, ChartHeight));
        }

        private byte[] GenerateEmptyChartPng(string title, string message)
        {
            var plot = new Plot();
            ApplyAiaaStyle(plot);
            plot.Title(title);
            var annotation = plot.Add.Annotation(message);
            annotation.Alignment = Alignment.MiddleCenter;
            return plot.GetImageBytes(ChartWidth, ChartHeight, ImageFormat.Png);
        }

        /// <summary>
        /// Ensures the SVG has a viewBox attribute for proper responsive scaling.
        /// Without viewBox, browsers cannot scale SVG content when CSS constrains width/height.
        /// </summary>
        internal static string EnsureSvgViewBox(string svg)
        {
            if (string.IsNullOrEmpty(svg) || svg.Contains("viewBox"))
                return svg;

            // ScottPlot outputs: <svg xmlns="..." width="800" height="500">
            // Insert viewBox after height attribute
            var pattern = $"height=\"{ChartHeight}\"";
            var replacement = $"height=\"{ChartHeight}\" viewBox=\"0 0 {ChartWidth} {ChartHeight}\"";
            return svg.Replace(pattern, replacement);
        }
    }
}
