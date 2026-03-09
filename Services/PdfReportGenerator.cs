using PdfSharpCore.Drawing;
using PdfSharpCore.Pdf;
using foamscript.Models;

namespace foamscript.Services
{
    /// <summary>
    /// Generates AIAA-quality PDF analysis reports using PdfSharpCore.
    /// Embeds ScottPlot chart images and renders tables programmatically.
    /// </summary>
    public class PdfReportGenerator
    {
        private const double PageWidth = 612; // US Letter width in points
        private const double PageHeight = 792; // US Letter height in points
        private const double Margin = 54; // 0.75 inch margins
        private const double ContentWidth = PageWidth - 2 * Margin;

        private static readonly XFont TitleFont = new("Times New Roman", 20, XFontStyle.Bold);
        private static readonly XFont SubtitleFont = new("Times New Roman", 12, XFontStyle.Regular);
        private static readonly XFont HeadingFont = new("Times New Roman", 14, XFontStyle.Bold);
        private static readonly XFont SubheadingFont = new("Times New Roman", 11, XFontStyle.Bold);
        private static readonly XFont BodyFont = new("Times New Roman", 10, XFontStyle.Regular);
        private static readonly XFont TableHeaderFont = new("Times New Roman", 9, XFontStyle.Bold);
        private static readonly XFont TableCellFont = new("Courier New", 9, XFontStyle.Regular);
        private static readonly XFont FooterFont = new("Times New Roman", 8, XFontStyle.Regular);

        private static readonly XColor HeaderBg = XColor.FromArgb(245, 245, 245);
        private static readonly XColor BorderColor = XColor.FromArgb(200, 200, 200);
        private static readonly XColor TextColor = XColor.FromArgb(51, 51, 51);

        /// <summary>
        /// Generates and writes a PDF report to disk.
        /// </summary>
        public void WriteReport(PdfReportData data, string outputPath)
        {
            var document = new PdfDocument();
            document.Info.Title = $"{data.StudyName} - Aerodynamic Analysis Report";
            document.Info.Author = "FoamScript";

            double y = 0;
            var page = AddPage(document);
            var gfx = XGraphics.FromPdfPage(page);

            // Title page content
            y = Margin + 60;
            DrawCenteredText(gfx, data.StudyName, TitleFont, y);
            y += 30;
            DrawCenteredText(gfx, "Aerodynamic Analysis Report", SubtitleFont, y);
            y += 20;
            DrawCenteredText(gfx, $"Generated: {data.GenerationDate}  |  FoamScript {data.Version}", FooterFont, y);
            y += 50;

            // Section 1: Physics Configuration
            y = DrawSectionHeading(gfx, "1. Physics & Solver Configuration", y);
            y = DrawConfigTable(gfx, data, y);

            // Section 2: Mesh Summary
            y = CheckPageBreak(document, ref page, ref gfx, y, 200);
            y = DrawSectionHeading(gfx, "2. Mesh Summary", y);
            y = DrawMeshTable(gfx, data.MeshStats, y);

            // Section 3: Aerodynamic Polars
            y = CheckPageBreak(document, ref page, ref gfx, y, 300);
            y = DrawSectionHeading(gfx, "3. Aerodynamic Coefficient Polars", y);

            // Charts (2 per row, then drag polar full width)
            foreach (var chartPair in ChunkCharts(data.PolarCharts, 2))
            {
                y = CheckPageBreak(document, ref page, ref gfx, y, 280);
                y = DrawChartRow(gfx, chartPair, y);
            }

            if (data.DragPolarChart != null && data.DragPolarChart.Length > 0)
            {
                y = CheckPageBreak(document, ref page, ref gfx, y, 280);
                y = DrawChartCentered(gfx, data.DragPolarChart, y, ContentWidth * 0.7);
            }

            // Section 4: Coefficient Summary
            y = CheckPageBreak(document, ref page, ref gfx, y, 200);
            y = DrawSectionHeading(gfx, "4. Coefficient Summary", y);
            y = DrawCoeffTable(gfx, data.Cases, y);

            // Section 5: Convergence History
            foreach (var convergence in data.ConvergenceCharts)
            {
                y = CheckPageBreak(document, ref page, ref gfx, y, 300);
                if (convergence == data.ConvergenceCharts[0])
                    y = DrawSectionHeading(gfx, "5. Convergence History", y);

                y = DrawSubheading(gfx, convergence.CaseName, y);
                y = DrawChartCentered(gfx, convergence.ChartPng, y, ContentWidth * 0.85);
            }

            // Section 6: Flow Visualization (if available)
            if (data.PressureSlicePng != null || data.VelocitySlicePng != null)
            {
                y = CheckPageBreak(document, ref page, ref gfx, y, 300);
                y = DrawSectionHeading(gfx, "6. Flow Field Visualization", y);

                gfx.DrawString("Contour plots on the y=0 symmetry plane through the disc center.",
                    BodyFont, new XSolidBrush(XColor.FromArgb(85, 85, 85)), Margin, y);
                y += 18;

                if (data.PressureSlicePng != null)
                {
                    y = CheckPageBreak(document, ref page, ref gfx, y, 300);
                    y = DrawSubheading(gfx, "Static Pressure", y);
                    y = DrawChartCentered(gfx, data.PressureSlicePng, y, ContentWidth * 0.85);
                }

                if (data.VelocitySlicePng != null)
                {
                    y = CheckPageBreak(document, ref page, ref gfx, y, 300);
                    y = DrawSubheading(gfx, "Velocity Magnitude", y);
                    y = DrawChartCentered(gfx, data.VelocitySlicePng, y, ContentWidth * 0.85);
                }
            }

            // Footer on last page
            DrawFooter(gfx, data.Version);

            Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
            document.Save(outputPath);
        }

        private static PdfPage AddPage(PdfDocument document)
        {
            var page = document.AddPage();
            page.Width = PageWidth;
            page.Height = PageHeight;
            return page;
        }

        private static double CheckPageBreak(PdfDocument doc, ref PdfPage page, ref XGraphics gfx, double y, double needed)
        {
            if (y + needed > PageHeight - Margin)
            {
                DrawFooter(gfx, "");
                gfx.Dispose();
                page = AddPage(doc);
                gfx = XGraphics.FromPdfPage(page);
                return Margin;
            }
            return y;
        }

        private static double DrawSectionHeading(XGraphics gfx, string text, double y)
        {
            y += 15;
            gfx.DrawString(text, HeadingFont, new XSolidBrush(TextColor), Margin, y);
            y += 5;
            gfx.DrawLine(new XPen(BorderColor, 0.5), Margin, y, PageWidth - Margin, y);
            y += 12;
            return y;
        }

        private static double DrawSubheading(XGraphics gfx, string text, double y)
        {
            gfx.DrawString(text, SubheadingFont, new XSolidBrush(TextColor), Margin, y);
            y += 16;
            return y;
        }

        private static void DrawCenteredText(XGraphics gfx, string text, XFont font, double y)
        {
            var size = gfx.MeasureString(text, font);
            gfx.DrawString(text, font, new XSolidBrush(TextColor), (PageWidth - size.Width) / 2, y);
        }

        private static double DrawConfigTable(XGraphics gfx, PdfReportData data, double y)
        {
            var rows = new[]
            {
                ("Freestream velocity", $"{data.Velocity} m/s"),
                ("RPM", data.Rpm),
                ("Turbulence intensity", $"{data.TurbulenceIntensity}%"),
                ("Kinematic viscosity", $"{data.Nu} m\u00b2/s"),
                ("Turbulence model", "k-\u03c9 SST"),
                ("Solver", $"{data.SolverName} (steady-state RANS)"),
                ("Max iterations", data.MaxIterations),
                ("Refinement levels", $"{data.RefinementMin}\u2013{data.RefinementMax}"),
                ("Boundary layers", "8 layers, expansion ratio 1.2"),
            };

            return DrawTwoColumnTable(gfx, "Parameter", "Value", rows, y);
        }

        private static double DrawTwoColumnTable(XGraphics gfx, string header1, string header2,
            (string, string)[] rows, double y)
        {
            double col1Width = ContentWidth * 0.45;
            double col2Width = ContentWidth * 0.55;
            double rowHeight = 18;

            // Header
            gfx.DrawRectangle(new XSolidBrush(HeaderBg), Margin, y, ContentWidth, rowHeight);
            gfx.DrawRectangle(new XPen(BorderColor, 0.5), Margin, y, ContentWidth, rowHeight);
            gfx.DrawString(header1, TableHeaderFont, XBrushes.Black, Margin + 4, y + 13);
            gfx.DrawString(header2, TableHeaderFont, XBrushes.Black, Margin + col1Width + 4, y + 13);
            y += rowHeight;

            // Data rows
            foreach (var (label, value) in rows)
            {
                gfx.DrawRectangle(new XPen(BorderColor, 0.5), Margin, y, ContentWidth, rowHeight);
                gfx.DrawString(label, BodyFont, XBrushes.Black, Margin + 4, y + 13);
                gfx.DrawString(value, TableCellFont, XBrushes.Black, Margin + col1Width + 4, y + 13);
                y += rowHeight;
            }

            y += 10;
            return y;
        }

        private static double DrawMeshTable(XGraphics gfx, List<PdfMeshStatEntry> meshStats, double y)
        {
            if (meshStats.Count == 0) return y;

            double rowHeight = 18;
            var colWidths = new[] { ContentWidth * 0.25, ContentWidth * 0.12, ContentWidth * 0.17,
                                    ContentWidth * 0.17, ContentWidth * 0.17, ContentWidth * 0.12 };
            var headers = new[] { "Case", "AoA (\u00b0)", "Cells", "Points", "Faces", "Quality" };

            // Header row
            double x = Margin;
            gfx.DrawRectangle(new XSolidBrush(HeaderBg), Margin, y, ContentWidth, rowHeight);
            gfx.DrawRectangle(new XPen(BorderColor, 0.5), Margin, y, ContentWidth, rowHeight);
            for (int i = 0; i < headers.Length; i++)
            {
                gfx.DrawString(headers[i], TableHeaderFont, XBrushes.Black, x + 4, y + 13);
                x += colWidths[i];
            }
            y += rowHeight;

            // Data rows
            foreach (var entry in meshStats)
            {
                x = Margin;
                gfx.DrawRectangle(new XPen(BorderColor, 0.5), Margin, y, ContentWidth, rowHeight);
                var values = new[] { entry.CaseName, entry.Aoa, entry.Cells, entry.Points, entry.Faces,
                                     entry.QualityPassed ? "Passed" : "Failed" };
                for (int i = 0; i < values.Length; i++)
                {
                    var font = i == 0 ? BodyFont : TableCellFont;
                    gfx.DrawString(values[i], font, XBrushes.Black, x + 4, y + 13);
                    x += colWidths[i];
                }
                y += rowHeight;
            }

            y += 10;
            return y;
        }

        private static double DrawCoeffTable(XGraphics gfx, List<CaseResultEntry> cases, double y)
        {
            if (cases.Count == 0) return y;

            double rowHeight = 18;
            var colWidths = new[] { ContentWidth * 0.12, ContentWidth * 0.17, ContentWidth * 0.17,
                                    ContentWidth * 0.20, ContentWidth * 0.17, ContentWidth * 0.17 };
            var headers = new[] { "AoA (\u00b0)", "C\u2084", "C\u2097", "Cm,pitch", "L/D", "Status" };

            // Header row
            double x = Margin;
            gfx.DrawRectangle(new XSolidBrush(HeaderBg), Margin, y, ContentWidth, rowHeight);
            gfx.DrawRectangle(new XPen(BorderColor, 0.5), Margin, y, ContentWidth, rowHeight);
            for (int i = 0; i < headers.Length; i++)
            {
                gfx.DrawString(headers[i], TableHeaderFont, XBrushes.Black, x + 4, y + 13);
                x += colWidths[i];
            }
            y += rowHeight;

            foreach (var c in cases)
            {
                x = Margin;
                gfx.DrawRectangle(new XPen(BorderColor, 0.5), Margin, y, ContentWidth, rowHeight);
                var values = new[] { c.Aoa, c.Cd, c.Cl, c.Cm, c.Ld, c.Converged ? "OK" : "FAIL" };
                for (int i = 0; i < values.Length; i++)
                {
                    gfx.DrawString(values[i], TableCellFont, XBrushes.Black, x + 4, y + 13);
                    x += colWidths[i];
                }
                y += rowHeight;
            }

            y += 10;
            return y;
        }

        private static double DrawChartRow(XGraphics gfx, List<byte[]> charts, double y)
        {
            double chartWidth = (ContentWidth - 10) / 2;
            double chartHeight = chartWidth * 0.625;

            for (int i = 0; i < charts.Count; i++)
            {
                double x = Margin + i * (chartWidth + 10);
                DrawChartImage(gfx, charts[i], x, y, chartWidth, chartHeight);
            }

            y += chartWidth * 0.625 + 10;
            return y;
        }

        private static double DrawChartCentered(XGraphics gfx, byte[] chartPng, double y, double width)
        {
            double height = width * 0.625;
            double x = Margin + (ContentWidth - width) / 2;
            DrawChartImage(gfx, chartPng, x, y, width, height);
            y += height + 10;
            return y;
        }

        private static void DrawChartImage(XGraphics gfx, byte[] pngData, double x, double y, double width, double height)
        {
            if (pngData == null || pngData.Length == 0) return;

            using var stream = new MemoryStream(pngData);
            var image = XImage.FromStream(() => new MemoryStream(pngData));
            gfx.DrawImage(image, x, y, width, height);
        }

        private static void DrawFooter(XGraphics gfx, string version)
        {
            var text = $"Generated by FoamScript {version} \u2014 AIAA-standard aerodynamic analysis report";
            var size = gfx.MeasureString(text, FooterFont);
            gfx.DrawString(text, FooterFont, new XSolidBrush(XColor.FromArgb(153, 153, 153)),
                (PageWidth - size.Width) / 2, PageHeight - 30);
        }

        private static List<List<byte[]>> ChunkCharts(List<byte[]> charts, int chunkSize)
        {
            var result = new List<List<byte[]>>();
            for (int i = 0; i < charts.Count; i += chunkSize)
                result.Add(charts.Skip(i).Take(chunkSize).ToList());
            return result;
        }
    }

    /// <summary>
    /// Data model for PDF report generation (uses PNG chart images instead of SVG).
    /// </summary>
    public class PdfReportData
    {
        public string StudyName { get; set; } = string.Empty;
        public string GenerationDate { get; set; } = string.Empty;
        public string Version { get; set; } = "0.3.0";

        // Physics config
        public string Velocity { get; set; } = string.Empty;
        public string Rpm { get; set; } = string.Empty;
        public string TurbulenceIntensity { get; set; } = string.Empty;
        public string Nu { get; set; } = string.Empty;
        public string SolverName { get; set; } = string.Empty;
        public string MaxIterations { get; set; } = string.Empty;
        public string RefinementMin { get; set; } = string.Empty;
        public string RefinementMax { get; set; } = string.Empty;

        public List<PdfMeshStatEntry> MeshStats { get; set; } = new();
        public List<byte[]> PolarCharts { get; set; } = new(); // Cl, Cd, Cm, L/D as PNG
        public byte[]? DragPolarChart { get; set; }
        public List<CaseResultEntry> Cases { get; set; } = new();
        public List<PdfConvergenceEntry> ConvergenceCharts { get; set; } = new();

        // Flow visualization (raw PNG bytes)
        public byte[]? PressureSlicePng { get; set; }
        public byte[]? VelocitySlicePng { get; set; }
    }

    public class PdfMeshStatEntry
    {
        public string CaseName { get; set; } = string.Empty;
        public string Aoa { get; set; } = string.Empty;
        public string Cells { get; set; } = "N/A";
        public string Points { get; set; } = "N/A";
        public string Faces { get; set; } = "N/A";
        public bool QualityPassed { get; set; }
    }

    public class PdfConvergenceEntry
    {
        public string CaseName { get; set; } = string.Empty;
        public byte[] ChartPng { get; set; } = Array.Empty<byte>();
    }
}
