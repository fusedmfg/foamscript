# Design: Python Visualization Elimination

## Problem

The report visualization pipeline requires two external runtime dependencies:

- **pvpython** (ParaView) for CFD data extraction (`extract_slice.py`)
- **python3 + matplotlib** for contour rendering (`render_slice.py`)

These must be installed on the target system and are fragile to version mismatches. Both tasks can be performed natively using tools already present: OpenFOAM's `postProcess` for extraction and ScottPlot (already a project dependency) for rendering.

## Approach

Replace the two-phase Python pipeline with OpenFOAM `postProcess` + C# VTK parsing + ScottPlot heatmap rendering.

| Current | Replacement |
|---------|-------------|
| `pvpython extract_slice.py` -> JSON | `postProcess -func surfaceSampling` -> VTK |
| `python3 render_slice.py` -> PNG | C# VTK parser -> grid interpolation -> ScottPlot Heatmap -> PNG/SVG |

## Phase 1: Data Extraction

### OpenFOAM `postProcess` with `surfaces` function object

During report generation (not during solve), write a temporary function object dictionary and invoke `postProcess`:

```
surfaceSampling
{
    type            surfaces;
    libs            (sampling);
    writeControl    writeTime;
    surfaceFormat   vtk;
    fields          (p U);
    surfaces
    {
        yNormal
        {
            type        cuttingPlane;
            point       (0 0 0);
            normal      (0 1 0);
            interpolate true;
        }
    }
}
```

Invocation:
```bash
postProcess -case <caseDir> -func surfaceSampling -latestTime
```

Output: `postProcessing/surfaceSampling/<time>/yNormal.vtp` (or legacy `.vtk`)

OpenFOAM reads its own mesh format natively — no ParaView needed. The `postProcess` utility is always installed with OpenFOAM.

## Phase 2: Rendering

### New C# class: VtkSliceRenderer

1. **VTK Legacy Parser** — reads POINTS, POLYGONS, POINT_DATA sections from text VTK files
2. **Grid Interpolation** — inverse distance weighting (IDW) from irregular mesh points to a uniform NxM grid (target: 300x200 for 16:9)
3. **ScottPlot Heatmap** — `plot.Add.Heatmap(double[,])` with colormap:
   - Pressure: diverging colormap (blue-white-red)
   - Velocity magnitude: sequential colormap (Turbo or Viridis)
4. **AIAA View Framing** — reads geometry bounds from STL for consistent view window sizing
5. **Output** — PNG bytes consumed by existing ReportService pipeline (no change to HTML/PDF embedding)

### Quality Trade-off

ScottPlot's Heatmap on an interpolated regular grid looks slightly different from matplotlib's `tricontourf` on the native triangulated mesh. At 300x200 resolution, the interpolated result is smooth and adequate for quick-look report summaries. Users who need publication-quality visualization use ParaView directly.

## File Changes

| File | Action |
|------|--------|
| `Services/VisualizationService.cs` | Rewrite: remove Python, use postProcess + VtkSliceRenderer |
| `Services/VtkSliceRenderer.cs` | NEW: VTK parser + grid interpolation + ScottPlot rendering |
| `Templates/report/extract_slice.py` | DELETE |
| `Templates/report/render_slice.py` | DELETE |
| `foamscript.Tests/Services/VtkSliceRendererTests.cs` | NEW: parser, interpolation, rendering tests |
| `foamscript.Tests/Services/VisualizationServiceTests.cs` | Update for new flow |

## Graceful Degradation

Unchanged from current behavior: if `postProcess` fails or no results exist, `HasVisualization = false` and the report renders without Section 6. No error thrown.

## Dependencies

- Removes: pvpython, python3, matplotlib, numpy
- Adds: nothing (ScottPlot already in project)
