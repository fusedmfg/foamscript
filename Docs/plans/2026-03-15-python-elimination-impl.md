# Python Visualization Elimination — Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Replace pvpython + matplotlib visualization pipeline with OpenFOAM postProcess + ScottPlot heatmap rendering, eliminating all Python runtime dependencies.

**Architecture:** Two-phase replacement — OpenFOAM's `postProcess -func surfaces` extracts slice data as VTK (replaces pvpython), then a new C# `VtkSliceRenderer` parses the VTK file, interpolates to a regular grid, and renders via ScottPlot Heatmap (replaces matplotlib). The existing `SliceVisualizationResult` model and report embedding remain unchanged.

**Tech Stack:** OpenFOAM postProcess, VTK legacy format parsing, inverse distance weighting interpolation, ScottPlot 5.x Heatmap, .NET 10

---

### Task 1: VTK Legacy Format Parser

Parse VTK legacy text files produced by OpenFOAM's surface sampling. The format has POINTS, POLYGONS, and POINT_DATA sections with scalar/vector arrays.

**Files:**
- Create: `Services/VtkSliceParser.cs`
- Test: `foamscript.Tests/Services/VtkSliceParserTests.cs`

**Step 1: Write test for parsing points and scalar field from VTK text**

```csharp
// foamscript.Tests/Services/VtkSliceParserTests.cs
using FluentAssertions;
using foamscript.Services;

namespace foamscript.Tests.Services;

public class VtkSliceParserTests
{
    private const string SampleVtk = """
        # vtk DataFile Version 5.1
        vtk output
        ASCII
        DATASET POLYDATA
        POINTS 4 float
        0.0 0.0 0.0
        1.0 0.0 0.0
        1.0 0.0 1.0
        0.0 0.0 1.0
        POLYGONS 2 8
        3 0 1 2
        3 0 2 3
        POINT_DATA 4
        FIELD FieldData 2
        p 1 4 float
        100.0 200.0 150.0 120.0
        U 3 4 float
        10.0 0.0 0.0
        20.0 0.0 0.0
        15.0 0.0 0.0
        12.0 0.0 0.0
        """;

    [Fact]
    public void Parse_ExtractsPointCoordinates()
    {
        var result = VtkSliceParser.Parse(SampleVtk);

        result.Points.Should().HaveCount(4);
        result.Points[0].X.Should().BeApproximately(0.0, 1e-6);
        result.Points[0].Z.Should().BeApproximately(0.0, 1e-6);
        result.Points[2].X.Should().BeApproximately(1.0, 1e-6);
        result.Points[2].Z.Should().BeApproximately(1.0, 1e-6);
    }

    [Fact]
    public void Parse_ExtractsScalarField()
    {
        var result = VtkSliceParser.Parse(SampleVtk);

        result.ScalarFields.Should().ContainKey("p");
        result.ScalarFields["p"].Should().HaveCount(4);
        result.ScalarFields["p"][0].Should().BeApproximately(100.0, 1e-6);
    }

    [Fact]
    public void Parse_ExtractsVectorFieldAsMagnitude()
    {
        var result = VtkSliceParser.Parse(SampleVtk);

        // U is a 3-component vector; parser should compute magnitude
        result.ScalarFields.Should().ContainKey("U");
        result.ScalarFields["U"][0].Should().BeApproximately(10.0, 1e-6);
        result.ScalarFields["U"][1].Should().BeApproximately(20.0, 1e-6);
    }

    [Fact]
    public void Parse_EmptyInput_ReturnsEmptyResult()
    {
        var result = VtkSliceParser.Parse("");

        result.Points.Should().BeEmpty();
        result.ScalarFields.Should().BeEmpty();
    }
}
```

**Step 2: Run tests to verify they fail**

Run: `dotnet test --filter "VtkSliceParserTests" --no-build`
Expected: FAIL — `VtkSliceParser` class does not exist

**Step 3: Implement VtkSliceParser**

```csharp
// Services/VtkSliceParser.cs
namespace foamscript.Services;

/// <summary>
/// Parses VTK legacy text format files produced by OpenFOAM surface sampling.
/// Extracts point coordinates (x, z from y=0 slice) and scalar/vector field data.
/// Vector fields are converted to magnitude.
/// </summary>
public static class VtkSliceParser
{
    public record SliceData
    {
        public List<(double X, double Z)> Points { get; init; } = new();
        public Dictionary<string, double[]> ScalarFields { get; init; } = new();
    }

    public static SliceData Parse(string vtkContent)
    {
        if (string.IsNullOrWhiteSpace(vtkContent))
            return new SliceData();

        var lines = vtkContent.Split('\n', StringSplitOptions.None);
        var points = new List<(double X, double Z)>();
        var fields = new Dictionary<string, double[]>();
        int i = 0;

        while (i < lines.Length)
        {
            var line = lines[i].Trim();

            // Parse POINTS section
            if (line.StartsWith("POINTS"))
            {
                var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                var nPoints = int.Parse(parts[1]);
                i++;
                points = ParsePoints(lines, ref i, nPoints);
                continue;
            }

            // Parse FIELD data inside POINT_DATA
            if (line.StartsWith("FIELD"))
            {
                var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                var nArrays = int.Parse(parts[2]);
                i++;

                for (int a = 0; a < nArrays && i < lines.Length; a++)
                {
                    var header = lines[i].Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
                    // header: name numComponents numTuples dataType
                    var name = header[0];
                    var numComponents = int.Parse(header[1]);
                    var numTuples = int.Parse(header[2]);
                    i++;

                    var values = ParseFloats(lines, ref i, numTuples * numComponents);

                    if (numComponents == 1)
                    {
                        fields[name] = values;
                    }
                    else if (numComponents == 3)
                    {
                        // Vector field — compute magnitude
                        var magnitudes = new double[numTuples];
                        for (int t = 0; t < numTuples; t++)
                        {
                            var vx = values[t * 3];
                            var vy = values[t * 3 + 1];
                            var vz = values[t * 3 + 2];
                            magnitudes[t] = Math.Sqrt(vx * vx + vy * vy + vz * vz);
                        }
                        fields[name] = magnitudes;
                    }
                }
                continue;
            }

            i++;
        }

        return new SliceData { Points = points, ScalarFields = fields };
    }

    private static List<(double X, double Z)> ParsePoints(string[] lines, ref int i, int nPoints)
    {
        var points = new List<(double, double)>(nPoints);
        var values = ParseFloats(lines, ref i, nPoints * 3);
        for (int p = 0; p < nPoints; p++)
        {
            var x = values[p * 3];       // X coordinate
            var z = values[p * 3 + 2];   // Z coordinate (skip Y — slice plane)
            points.Add((x, z));
        }
        return points;
    }

    private static double[] ParseFloats(string[] lines, ref int i, int count)
    {
        var result = new List<double>(count);
        while (result.Count < count && i < lines.Length)
        {
            var parts = lines[i].Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
            foreach (var part in parts)
            {
                if (double.TryParse(part, System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out var val))
                {
                    result.Add(val);
                }
            }
            i++;
        }
        return result.ToArray();
    }
}
```

**Step 4: Run tests to verify they pass**

Run: `dotnet test --filter "VtkSliceParserTests"`
Expected: PASS (4/4)

**Step 5: Commit**

```bash
git add Services/VtkSliceParser.cs foamscript.Tests/Services/VtkSliceParserTests.cs
git commit -m "feat: add VTK legacy format parser for slice data"
```

---

### Task 2: Grid Interpolation (IDW)

Interpolate irregular mesh data onto a regular grid using inverse distance weighting. This is needed because ScottPlot Heatmap requires a `double[,]` regular grid.

**Files:**
- Create: `Services/GridInterpolator.cs`
- Test: `foamscript.Tests/Services/GridInterpolatorTests.cs`

**Step 1: Write tests for grid interpolation**

```csharp
// foamscript.Tests/Services/GridInterpolatorTests.cs
using FluentAssertions;
using foamscript.Services;

namespace foamscript.Tests.Services;

public class GridInterpolatorTests
{
    [Fact]
    public void Interpolate_UniformField_ProducesUniformGrid()
    {
        // All source points have value 42.0
        var points = new List<(double X, double Z)>
        {
            (0, 0), (1, 0), (0, 1), (1, 1)
        };
        var values = new double[] { 42.0, 42.0, 42.0, 42.0 };

        var grid = GridInterpolator.Interpolate(points, values,
            xMin: 0, xMax: 1, zMin: 0, zMax: 1, nx: 5, nz: 5);

        grid.GetLength(0).Should().Be(5);
        grid.GetLength(1).Should().Be(5);
        // Every cell should be ~42.0
        for (int row = 0; row < 5; row++)
            for (int col = 0; col < 5; col++)
                grid[row, col].Should().BeApproximately(42.0, 0.01);
    }

    [Fact]
    public void Interpolate_LinearGradient_InterpolatesSmoothly()
    {
        // Value = x coordinate (linear gradient in X)
        var points = new List<(double X, double Z)>
        {
            (0, 0), (10, 0), (0, 10), (10, 10),
            (5, 5)
        };
        var values = new double[] { 0, 10, 0, 10, 5 };

        var grid = GridInterpolator.Interpolate(points, values,
            xMin: 0, xMax: 10, zMin: 0, zMax: 10, nx: 11, nz: 11);

        // Center cell (row 5, col 5) should be close to 5.0
        grid[5, 5].Should().BeApproximately(5.0, 0.5);
        // Left edge should be lower than right edge
        grid[5, 0].Should().BeLessThan(grid[5, 10]);
    }

    [Fact]
    public void Interpolate_RespectsGridDimensions()
    {
        var points = new List<(double X, double Z)> { (0, 0) };
        var values = new double[] { 1.0 };

        var grid = GridInterpolator.Interpolate(points, values,
            xMin: 0, xMax: 1, zMin: 0, zMax: 1, nx: 100, nz: 50);

        grid.GetLength(0).Should().Be(50);  // rows = nz
        grid.GetLength(1).Should().Be(100); // cols = nx
    }
}
```

**Step 2: Run tests to verify they fail**

Run: `dotnet test --filter "GridInterpolatorTests" --no-build`
Expected: FAIL — `GridInterpolator` class does not exist

**Step 3: Implement GridInterpolator**

```csharp
// Services/GridInterpolator.cs
namespace foamscript.Services;

/// <summary>
/// Interpolates scattered (x, z, value) data onto a regular grid
/// using inverse distance weighting (IDW).
/// </summary>
public static class GridInterpolator
{
    /// <summary>
    /// Interpolates irregular point data to a regular NxM grid.
    /// Returns double[nz, nx] (rows = Z, cols = X) for ScottPlot Heatmap.
    /// </summary>
    public static double[,] Interpolate(
        List<(double X, double Z)> points,
        double[] values,
        double xMin, double xMax, double zMin, double zMax,
        int nx, int nz, double power = 2.0, int kNearest = 16)
    {
        var grid = new double[nz, nx];
        var dx = (xMax - xMin) / Math.Max(nx - 1, 1);
        var dz = (zMax - zMin) / Math.Max(nz - 1, 1);

        for (int row = 0; row < nz; row++)
        {
            var gz = zMax - row * dz; // Top row = zMax (image convention)
            for (int col = 0; col < nx; col++)
            {
                var gx = xMin + col * dx;
                grid[row, col] = IdwInterpolate(points, values, gx, gz, power, kNearest);
            }
        }

        return grid;
    }

    private static double IdwInterpolate(
        List<(double X, double Z)> points, double[] values,
        double gx, double gz, double power, int kNearest)
    {
        // Find k nearest points by distance
        var distances = new (double Dist, int Index)[points.Count];
        for (int i = 0; i < points.Count; i++)
        {
            var ddx = points[i].X - gx;
            var ddz = points[i].Z - gz;
            distances[i] = (ddx * ddx + ddz * ddz, i); // squared distance
        }

        // Partial sort for k nearest
        Array.Sort(distances, (a, b) => a.Dist.CompareTo(b.Dist));
        var k = Math.Min(kNearest, points.Count);

        // Check for exact hit (distance ~0)
        if (distances[0].Dist < 1e-20)
            return values[distances[0].Index];

        double weightSum = 0;
        double valueSum = 0;

        for (int i = 0; i < k; i++)
        {
            var dist = Math.Sqrt(distances[i].Dist);
            var w = 1.0 / Math.Pow(dist, power);
            weightSum += w;
            valueSum += w * values[distances[i].Index];
        }

        return valueSum / weightSum;
    }
}
```

**Step 4: Run tests to verify they pass**

Run: `dotnet test --filter "GridInterpolatorTests"`
Expected: PASS (3/3)

**Step 5: Commit**

```bash
git add Services/GridInterpolator.cs foamscript.Tests/Services/GridInterpolatorTests.cs
git commit -m "feat: add IDW grid interpolator for irregular mesh data"
```

---

### Task 3: ScottPlot Heatmap Slice Renderer

Combine the VTK parser + grid interpolator to render flow field heatmaps as PNG/SVG using ScottPlot.

**Files:**
- Create: `Services/VtkSliceRenderer.cs`
- Test: `foamscript.Tests/Services/VtkSliceRendererTests.cs`

**Step 1: Write test for rendering a heatmap from parsed VTK data**

```csharp
// foamscript.Tests/Services/VtkSliceRendererTests.cs
using FluentAssertions;
using foamscript.Services;

namespace foamscript.Tests.Services;

public class VtkSliceRendererTests
{
    [Fact]
    public void RenderSlice_ProducesPngBytes()
    {
        var sliceData = new VtkSliceParser.SliceData
        {
            Points = new List<(double X, double Z)>
            {
                (0, 0), (1, 0), (0, 1), (1, 1), (0.5, 0.5)
            },
            ScalarFields = new Dictionary<string, double[]>
            {
                ["p"] = new[] { 100.0, 200.0, 150.0, 180.0, 160.0 },
                ["U"] = new[] { 10.0, 20.0, 15.0, 18.0, 16.0 }
            }
        };

        var result = VtkSliceRenderer.Render(sliceData);

        result.PressureSlicePng.Should().NotBeNull();
        result.PressureSlicePng!.Length.Should().BeGreaterThan(100); // Valid PNG
        result.VelocitySlicePng.Should().NotBeNull();
        result.VelocitySlicePng!.Length.Should().BeGreaterThan(100);
    }

    [Fact]
    public void RenderSlice_WithGeometryBounds_UsesAiaaFraming()
    {
        var sliceData = new VtkSliceParser.SliceData
        {
            Points = new List<(double X, double Z)>
            {
                (-1, -1), (2, -1), (-1, 1), (2, 1), (0.5, 0)
            },
            ScalarFields = new Dictionary<string, double[]>
            {
                ["p"] = new[] { 100.0, 200.0, 150.0, 180.0, 160.0 }
            }
        };

        var bounds = new Models.BoundingBox
        {
            MinX = -0.1, MaxX = 0.1, MinZ = -0.05, MaxZ = 0.05
        };

        // Should not throw; geometry bounds drive the view window
        var result = VtkSliceRenderer.Render(sliceData, geometryBounds: bounds);
        result.PressureSlicePng.Should().NotBeNull();
    }

    [Fact]
    public void RenderSlice_EmptyData_ReturnsNull()
    {
        var sliceData = new VtkSliceParser.SliceData();
        var result = VtkSliceRenderer.Render(sliceData);

        result.PressureSlicePng.Should().BeNull();
        result.VelocitySlicePng.Should().BeNull();
    }
}
```

**Step 2: Run tests to verify they fail**

Run: `dotnet test --filter "VtkSliceRendererTests" --no-build`
Expected: FAIL — `VtkSliceRenderer` class does not exist

**Step 3: Implement VtkSliceRenderer**

```csharp
// Services/VtkSliceRenderer.cs
using foamscript.Models;
using ScottPlot;

namespace foamscript.Services;

/// <summary>
/// Renders flow field slice data as heatmap images using ScottPlot.
/// Replaces the Python matplotlib rendering pipeline.
/// </summary>
public static class VtkSliceRenderer
{
    private const int ImageWidth = 1600;
    private const int ImageHeight = 900;
    private const int GridNx = 300;
    private const int GridNz = 200;

    /// <summary>
    /// Renders pressure and velocity heatmaps from parsed VTK slice data.
    /// Returns SliceVisualizationResult with PNG bytes.
    /// </summary>
    public static SliceVisualizationResult Render(
        VtkSliceParser.SliceData sliceData,
        BoundingBox? geometryBounds = null)
    {
        var result = new SliceVisualizationResult();

        if (sliceData.Points.Count == 0)
            return result;

        // Calculate view bounds (AIAA framing or data-driven fallback)
        var (xMin, xMax, zMin, zMax) = CalculateViewBounds(sliceData.Points, geometryBounds);

        // Render pressure field
        if (sliceData.ScalarFields.TryGetValue("p", out var pValues))
        {
            result.PressureSlicePng = RenderField(
                sliceData.Points, pValues, xMin, xMax, zMin, zMax,
                "Static Pressure — y=0 Slice", "Pa",
                new ScottPlot.Colormaps.MellowRainbow());
        }

        // Render velocity magnitude field
        if (sliceData.ScalarFields.TryGetValue("U", out var uValues))
        {
            result.VelocitySlicePng = RenderField(
                sliceData.Points, uValues, xMin, xMax, zMin, zMax,
                "Velocity Magnitude — y=0 Slice", "m/s",
                new ScottPlot.Colormaps.Viridis());
        }

        return result;
    }

    private static byte[] RenderField(
        List<(double X, double Z)> points, double[] values,
        double xMin, double xMax, double zMin, double zMax,
        string title, string unit, IColormap colormap)
    {
        // Clamp values to 1st–99th percentile to handle boundary outliers
        var sorted = values.OrderBy(v => v).ToArray();
        var lo = sorted[(int)(sorted.Length * 0.01)];
        var hi = sorted[(int)(sorted.Length * 0.99)];
        var clamped = values.Select(v => Math.Clamp(v, lo, hi)).ToArray();

        var grid = GridInterpolator.Interpolate(points, clamped,
            xMin, xMax, zMin, zMax, GridNx, GridNz);

        var plot = new Plot();
        var hm = plot.Add.Heatmap(grid);
        hm.Colormap = colormap;
        hm.Position = new(xMin, xMax, zMin, zMax);

        plot.Title(title);
        plot.XLabel("x (m)");
        plot.YLabel("z (m)");

        // Match data aspect ratio
        plot.Axes.SetLimits(xMin, xMax, zMin, zMax);

        return plot.GetImageBytes(ImageWidth, ImageHeight, ImageFormat.Png);
    }

    /// <summary>
    /// AIAA-standard view framing: geometry-referenced bounds.
    /// Shows 1.5 char lengths upstream, 3 downstream (wake), 2 vertically.
    /// Falls back to data extent with padding if no geometry bounds.
    /// </summary>
    internal static (double xMin, double xMax, double zMin, double zMax)
        CalculateViewBounds(List<(double X, double Z)> points, BoundingBox? geoBounds)
    {
        if (geoBounds != null)
        {
            var charLen = Math.Max(
                geoBounds.MaxX - geoBounds.MinX,
                geoBounds.MaxZ - geoBounds.MinZ);
            var cx = (geoBounds.MinX + geoBounds.MaxX) / 2.0;
            var cz = (geoBounds.MinZ + geoBounds.MaxZ) / 2.0;
            return (
                cx - 1.5 * charLen,
                cx + 3.0 * charLen,
                cz - 2.0 * charLen,
                cz + 2.0 * charLen);
        }

        // Fallback: data extent with 10% padding
        var xs = points.Select(p => p.X).ToArray();
        var zs = points.Select(p => p.Z).ToArray();
        var padX = (xs.Max() - xs.Min()) * 0.1;
        var padZ = (zs.Max() - zs.Min()) * 0.1;
        return (xs.Min() - padX, xs.Max() + padX, zs.Min() - padZ, zs.Max() + padZ);
    }
}
```

**Step 4: Run tests to verify they pass**

Run: `dotnet test --filter "VtkSliceRendererTests"`
Expected: PASS (3/3)

**Step 5: Commit**

```bash
git add Services/VtkSliceRenderer.cs foamscript.Tests/Services/VtkSliceRendererTests.cs
git commit -m "feat: add ScottPlot heatmap slice renderer"
```

---

### Task 4: Rewrite VisualizationService

Replace the Python-based pipeline in `VisualizationService` with OpenFOAM `postProcess` + the new C# rendering pipeline.

**Files:**
- Modify: `Services/VisualizationService.cs` (full rewrite)
- Test: `foamscript.Tests/Services/VisualizationServiceTests.cs`

**Step 1: Write tests for the new VisualizationService**

```csharp
// foamscript.Tests/Services/VisualizationServiceTests.cs
using FluentAssertions;
using foamscript.Models;
using foamscript.Services;
using Moq;

namespace foamscript.Tests.Services;

public class VisualizationServiceTests
{
    private readonly Mock<IProcessExecutor> _mockExecutor = new();

    [Fact]
    public void GenerateSliceVisualization_PostProcessFails_ReturnsNull()
    {
        _mockExecutor
            .Setup(e => e.Execute("postProcess", It.IsAny<string>()))
            .Returns(new ProcessResult { ExitCode = 1, Output = "error" });

        var svc = new VisualizationService(_mockExecutor.Object);
        var result = svc.GenerateSliceVisualization("/fake/case", "/fake/output");

        result.Should().BeNull();
    }

    [Fact]
    public void GenerateSliceVisualization_WritesPostProcessDict()
    {
        // Verifies that the service writes a surfaceSampling dict to system/
        var caseDir = Path.Combine(Path.GetTempPath(), "viz_test_" + Guid.NewGuid());
        var systemDir = Path.Combine(caseDir, "system");
        Directory.CreateDirectory(systemDir);

        _mockExecutor
            .Setup(e => e.Execute("postProcess", It.IsAny<string>()))
            .Returns(new ProcessResult { ExitCode = 1 }); // Will fail, that's OK

        var svc = new VisualizationService(_mockExecutor.Object);
        svc.GenerateSliceVisualization(caseDir, "/fake/output");

        var dictPath = Path.Combine(systemDir, "surfaceSampling");
        File.Exists(dictPath).Should().BeTrue("service should write surfaceSampling dict");
        var content = File.ReadAllText(dictPath);
        content.Should().Contain("cuttingPlane");
        content.Should().Contain("(p U)");

        // Cleanup
        Directory.Delete(caseDir, true);
    }
}
```

**Step 2: Run tests to verify they fail**

Run: `dotnet test --filter "VisualizationServiceTests" --no-build`
Expected: FAIL — tests don't match new service behavior

**Step 3: Rewrite VisualizationService**

Replace the full contents of `Services/VisualizationService.cs` with the new OpenFOAM postProcess + C# rendering pipeline. Key changes:
- Remove `FindScriptDirectory()`, `pvpython`/`python3` checks
- Add `WriteSurfaceSamplingDict()` — writes the function object config to `system/surfaceSampling`
- Invoke `postProcess -case {caseDir} -func surfaceSampling -latestTime`
- Find the VTK output file in `postProcessing/surfaceSampling/<time>/`
- Parse with `VtkSliceParser`, render with `VtkSliceRenderer`
- Optionally read geometry bounds from STL via `DomainService.CalculateBoundingBox()`
- Clean up the dict file after use

**Step 4: Run tests to verify they pass**

Run: `dotnet test --filter "VisualizationServiceTests"`
Expected: PASS

**Step 5: Run full test suite**

Run: `dotnet test`
Expected: All tests pass (existing ReportService tests mock VisualizationService, so no breakage)

**Step 6: Commit**

```bash
git add Services/VisualizationService.cs foamscript.Tests/Services/VisualizationServiceTests.cs
git commit -m "feat: replace Python visualization with OpenFOAM postProcess + ScottPlot"
```

---

### Task 5: Delete Python Scripts

Remove the now-unused Python visualization scripts.

**Files:**
- Delete: `Templates/report/extract_slice.py`
- Delete: `Templates/report/render_slice.py`

**Step 1: Delete the files**

```bash
git rm Templates/report/extract_slice.py Templates/report/render_slice.py
```

**Step 2: Verify no remaining references to deleted scripts**

Search codebase for `extract_slice`, `render_slice`, `pvpython`, `matplotlib` — should find zero hits in C# files.

**Step 3: Run full test suite**

Run: `dotnet test`
Expected: All tests pass

**Step 4: Commit**

```bash
git commit -m "chore: remove Python visualization scripts (replaced by native pipeline)"
```

---

### Task 6: E2E Validation on Linux

Run the full disc pipeline on the Linux box to validate the new visualization path works end-to-end with real OpenFOAM data.

**Step 1: Push branch and sync to Linux**

```bash
git push -u origin feature/python-elimination
```
SSH to Linux, pull, build.

**Step 2: Run full pipeline**

```bash
foamscript new-study --project-name viz_test --output-dir . --model-source /home/trboyden/models/disc.step --angles 0 --velocity 27 --rpm 925 --cores 4 --template external_disc_rotatingwall_steady
foamscript mesh -d viz_test --cores 4
foamscript solve -d viz_test --cores 4
foamscript report -d viz_test
```

**Step 3: Verify report output**

- Check `viz_test/report/` contains HTML, PDF, CSV
- Open HTML report — verify Section 6 has pressure and velocity heatmaps
- Verify heatmaps show reasonable flow field (not blank or garbage)

**Step 4: Clean up test data**

```bash
rm -rf viz_test
```

---

### Task 7: Create PR and Merge

**Step 1: Create PR**

```bash
gh pr create --title "Remove Python visualization dependency" --body "..."
```

**Step 2: Merge after review**

```bash
gh pr merge --merge
```

**Step 3: Clean up branch**

```bash
git checkout develop && git pull && git branch -d feature/python-elimination
```
