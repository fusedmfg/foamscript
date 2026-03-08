# Design: `report` Command — AIAA-Quality Aerodynamic Analysis Reports

**Date:** 2026-03-08
**Status:** Approved
**Replaces:** `results` command (table/CSV/JSON console output)

---

## Context

FoamScript's `results` command currently extracts Cd/Cl/CmPitch from coefficient.dat and prints to console as table, CSV, or JSON. This is insufficient for AIAA-standard analysis deliverables which require publication-quality charts, convergence history, mesh statistics, and metadata — all in a self-contained document.

This design replaces `results` with a `report` command that generates HTML + PDF analysis reports with embedded aerodynamic polars, convergence plots, and summary tables.

---

## Architecture

```
foamscript report -d /path/to/study [--format html|pdf|both]
       |
       v
 ReportHandler (new)
       |
       +-> ResultsService.ExtractResults()      -> CaseResult[]    (existing)
       +-> ResidualParser.ParseSolverLog()       -> ResidualData[]  (new)
       +-> MeshService.GetMeshStats()            -> MeshStats       (extend)
       +-> StudyConfig metadata                  -> physics params  (existing)
       |
       v
 ReportService (new)
       |
       +-> ChartGenerator (new, ScottPlot)
       |     +- Cl vs AoA
       |     +- Cd vs AoA
       |     +- CmPitch vs AoA
       |     +- L/D vs AoA
       |     +- Cl vs Cd (drag polar)
       |     +- Residual convergence (per case)
       |
       +-> HtmlReportGenerator (new, Scriban)
       |     +- Self-contained .html with embedded SVG charts
       |
       +-> PdfReportGenerator (new, PdfSharpCore)
             +- Publication-quality .pdf
       |
       v
 Output: {study_dir}/report/
   +- {StudyName}_report.html
   +- {StudyName}_report.pdf
```

---

## Technology Stack

| Component | Library | License | Purpose |
|-----------|---------|---------|---------|
| Charts | ScottPlot 5.x | MIT | SVG/PNG chart generation |
| HTML | Scriban 6.x | BSD-2 | HTML template rendering (already in project) |
| PDF | PdfSharpCore | MIT | PDF document generation |
| Data | Existing services | — | ResultsService, SolverService parsing |

All dependencies are MIT/BSD licensed — no AGPL, no revenue-gated community editions.

---

## CLI Interface

### New: `report` command

```
foamscript report -d <study-dir> [options]

Options:
  -d, --dir          Study or case directory (required, auto-detects)
  -f, --format       Output format: html, pdf, both (default: both)
  -o, --output       Output directory (default: {study_dir}/report/)
  --average-window   Fraction of sim time to average coefficients (default: 0.1)
```

### Removed: `results` command

`ResultsHandler`, `ResultsModel`, and `ResultsService.FormatResults()` are removed. The data extraction logic in `ResultsService.ExtractResults()` is preserved and used by `ReportService`.

---

## Report Layout

### Section 1: Title / Header
- Study name, generation date, FoamScript version
- Model source filename, template name
- One-line summary: "{n} angle-of-attack cases at {velocity} m/s, {rpm} RPM"

### Section 2: Physics & Solver Configuration (table)
| Parameter | Value |
|-----------|-------|
| Freestream velocity | 20 m/s |
| RPM | 1000 |
| Turbulence intensity | 1% |
| Kinematic viscosity | 1.5e-5 m^2/s |
| Turbulence model | k-omega SST |
| Solver | simpleFoam (steady-state) |
| Max iterations | 1000 |
| Refinement levels | 4-5 |
| Boundary layers | 8 layers, expansion 1.2 |

### Section 3: Mesh Summary (table, per case)
| Case | Cells | Points | Faces | Quality |
|------|-------|--------|-------|---------|
| Study_-2.5 | 499,733 | ... | ... | Passed |
| Study_0.0 | 499,733 | ... | ... | Passed |
| Study_2.5 | 499,733 | ... | ... | Passed |

### Section 4: Aerodynamic Coefficient Polars (5 charts)
1. **Cl vs AoA** — Lift coefficient polar
2. **Cd vs AoA** — Drag coefficient polar
3. **CmPitch vs AoA** — Pitching moment polar
4. **L/D vs AoA** — Lift-to-drag efficiency
5. **Cl vs Cd** — Classic drag polar (Cl on Y, Cd on X)

For single-case studies: single-point markers with annotated values (no connecting lines).

### Section 5: Coefficient Summary Table
```
AoA (deg)    Cd        Cl        CmPitch    Cl/Cd    Status
-2.5         0.0573    0.2002    -0.0574    3.50     OK
 0.0         0.0574    0.2295    -0.0535    4.00     OK
 2.5         0.0577    0.2571    -0.0493    4.46     OK
```

### Section 6: Convergence History (1 chart per case)
- Log-scale Y-axis (residual magnitude)
- Linear X-axis (iteration number)
- Lines for: Ux, Uy, Uz, p, k, omega
- Demonstrates solution reached steady state

### Single-Case vs Multi-Case Handling
- **Multi-case**: Full polars with connected data points, convergence chart per case
- **Single-case**: Summary card with Cd/Cl/CmPitch/L/D values, convergence chart only

---

## AIAA Chart Styling

- White background, light gray gridlines
- Axis labels with units and coefficient notation (e.g., "C_L", "C_D")
- Filled circle markers at data points
- Connected by lines for multi-point studies
- Black & white compatible (marker shapes distinguish series, not just colors)
- 300 DPI for PDF, SVG for HTML
- Consistent font sizing (12pt axis labels, 10pt tick labels, 14pt titles)

---

## New Components

### Services/ResidualParser.cs (new)
Parses OpenFOAM solver log files to extract per-iteration residuals.

**Input:** Solver log file (e.g., `log.simpleFoam` or captured stdout)

**Log format:**
```
smoothSolver:  Solving for Ux, Initial residual = 0.0234, Final residual = 0.00123, No Iterations 3
GAMG:  Solving for p, Initial residual = 0.156, Final residual = 0.00456, No Iterations 8
```

**Output:** `List<ResidualEntry>` where each entry has: iteration, fieldName, initialResidual, finalResidual

**Regex:** `@"Solving for (\w+), Initial residual = ([\d.e+-]+), Final residual = ([\d.e+-]+)"`

### Services/ChartGenerator.cs (new)
ScottPlot wrapper that generates all chart types with AIAA styling defaults.

**Methods:**
- `GeneratePolarChart(cases, field, xLabel, yLabel)` -> SVG/PNG
- `GenerateDragPolar(cases)` -> SVG/PNG
- `GenerateResidualChart(residuals, caseName)` -> SVG/PNG
- `ApplyAiaaStyle(plot)` — shared styling method

### Services/HtmlReportGenerator.cs (new)
Renders Scriban template with embedded SVG charts.

**Template:** `Templates/report/report.html` — Scriban template with chart placeholders

### Services/PdfReportGenerator.cs (new)
Composes PDF using PdfSharpCore with embedded chart PNGs.

### Services/ReportService.cs (new)
Orchestrator: collects data, generates charts, renders HTML/PDF.

### Handlers/ReportHandler.cs (new)
CLI handler for `report` verb.

### Models/ReportModel.cs (new)
CLI options: `--dir`, `--format`, `--output`, `--average-window`

### Models/ResidualData.cs (new)
Data model for parsed residual entries.

---

## Data Flow: Residual Parsing

**Problem:** Where are solver logs stored?

OpenFOAM writes solver output to stdout. The current `SolverService` captures stdout via `IProcessExecutor` but doesn't persist it. Options:

1. **Parse from `log.*` files** — If the solver was run with output redirected (common practice)
2. **Persist solver output** — Modify SolverService to save stdout to `log.{solver}` in the case directory
3. **Re-read from `postProcessing/`** — Residuals aren't in postProcessing by default

**Recommendation:** Option 2 — modify `SolverService.RunSolver()` to write captured stdout to `{caseDir}/log.{solver}` (e.g., `log.simpleFoam`). This is standard OpenFOAM practice and provides the residual data for the report command.

---

## Folder Nesting Fix

**Current issue:** `new-study --project-name Foo --output-dir /run/Foo` creates `/run/Foo/Foo/` (double nesting).

**Fix:** In `NewStudyHandler`, detect when `--output-dir` already ends with `--project-name` and use it directly instead of appending.

```csharp
var studyDir = Path.GetFileName(config.OutputDir) == config.ProjectName
    ? config.OutputDir
    : Path.Combine(config.OutputDir, config.ProjectName);
```

This is a separate concern from the report command but should be fixed in the same release.

---

## Files to Create

| File | Type |
|------|------|
| `Handlers/ReportHandler.cs` | New handler |
| `Models/ReportModel.cs` | New CLI model |
| `Services/ReportService.cs` | New orchestrator |
| `Services/ChartGenerator.cs` | New ScottPlot wrapper |
| `Services/HtmlReportGenerator.cs` | New HTML renderer |
| `Services/PdfReportGenerator.cs` | New PDF renderer |
| `Services/ResidualParser.cs` | New log parser |
| `Models/ResidualData.cs` | New data model |
| `Templates/report/report.html` | New Scriban HTML template |

## Files to Modify

| File | Change |
|------|--------|
| `foamscript.csproj` | Add ScottPlot, PdfSharpCore NuGet packages |
| `Services/SolverService.cs` | Save solver stdout to `log.{solver}` file |
| `Services/ResultsService.cs` | Remove FormatResults(); keep ExtractResults() |
| `Handlers/NewStudyHandler.cs` | Fix folder nesting (detect duplicate name) |
| `AppService.cs` | Register ReportHandler, remove ResultsHandler |
| `Program.cs` / DI setup | Register new services |

## Files to Remove

| File | Reason |
|------|--------|
| `Handlers/ResultsHandler.cs` | Replaced by ReportHandler |
| `Models/ResultsModel.cs` | Replaced by ReportModel |

---

## Testing Strategy

### New Tests
- `ResidualParserTests` — Parse real v2512 solver log format, edge cases
- `ChartGeneratorTests` — Verify chart generation doesn't throw, output is valid SVG/PNG
- `ReportServiceTests` — Integration: data collection -> report generation
- `ReportHandlerTests` — CLI argument parsing, auto-detection

### Existing Tests to Update
- `ResultsServiceTests` — Remove FormatResults tests, keep ExtractResults tests
- `SolverServiceTests` — Add test for log file persistence

---

## Verification

1. Build: `dotnet build --configuration Release`
2. Tests: `dotnet test` — all existing + new tests pass
3. E2E on Linux:
   - `foamscript report -d /run/E2E_Spread/E2E_Spread` — generates HTML + PDF
   - `foamscript report -d /run/E2E_Zero/E2E_Zero` — single-case report
   - Open HTML in browser, verify charts render correctly
   - Open PDF, verify publication quality
4. Verify solver log persistence: re-run `solve`, confirm `log.simpleFoam` created
