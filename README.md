# FoamScript

.NET 10 CLI tool for automating OpenFOAM CFD workflows — parametric studies from STEP geometry to AIAA-quality reports.

## Quick Start

```bash
# 1. Validate OpenFOAM environment (writes ~/.foamscript/config.json)
foamscript validate

# 2. Convert STEP geometry to STL in meters (CAD file is in mm → output is meters)
foamscript convert ~/models/disc.step ~/models/disc.stl --input-units mm

# 3. Create parametric study (requires STL in meters — use convert for STEP/IGES)
foamscript new-study \
  --template external_disc_rotatingwall_steady \
  --project-name DiscAnalysis \
  --output-dir ~/OpenFOAM/$USER-v2512/run \
  --model-source ~/models/disc.stl \
  --angles -5,0,5,10 \
  --velocity 27 --rpm 925

# 4. Mesh, solve, report (auto-detects CPU cores, runs parallel)
foamscript mesh -d ~/OpenFOAM/$USER-v2512/run/DiscAnalysis
foamscript solve -d ~/OpenFOAM/$USER-v2512/run/DiscAnalysis
foamscript report -d ~/OpenFOAM/$USER-v2512/run/DiscAnalysis
```

## Features

- **Full Pipeline**: STEP/IGES → STL → domain generation → templated case creation → meshing → solving → report generation
- **Template-Driven**: Each template (`TEMPLATE.json`) defines geometry type, mesh/solve steps, pre/post-processing hooks, and report layout — `--template` selects the workflow
- **Geometry Validation**: `new-study` checks STL bounding box against template rules and warns if dimensions suggest non-meter units (mm, cm, in) with a suggested `convert` command
- **Pre/Post-Processing Hooks**: Templates can define `preProcess` and `postProcess` script arrays that run before meshing and after solving — with token substitution and optional/required failure handling
- **AIAA-Quality Reports**: Publication-standard HTML + PDF + CSV reports with aerodynamic polars, convergence history, flow visualization, mesh statistics, and coefficient tables
- **Pre-Flight Environment Guard**: All OpenFOAM-dependent commands auto-verify the environment before running; `foamscript validate` checks 23 dependencies across 7 categories
- **Configurable Physics**: Turbulence intensity, viscosity, end time, refinement levels — all via CLI or JSON (AIAA defaults: TI 1%, PBiCGStab+DILU, refinement 5/6, 8 boundary layers)
- **Auto-Parallel**: Detects CPU cores automatically; `--cores N` to override, `FOAMSCRIPT_MAX_CORES` env var to cap
- **Parametric Studies**: Angle of attack sweeps with automatic velocity decomposition and turbulence parameter calculation
- **Flow Visualization**: pvpython slice extraction + matplotlib contour rendering with AIAA geometry-referenced framing

## Templates

| Template | Geometry | Description |
|----------|----------|-------------|
| `external_disc_rotatingwall_steady` | Disc | Rotating wall BC, simpleFoam steady-state |
| `external_airfoil_static_steady` | Airfoil | Static geometry, simpleFoam steady-state |
| `external_disc_rotating-ami_transient` | Disc | AMI rotating mesh, pimpleFoam transient |

Run `foamscript list-templates` to see all available templates with their metadata.

## Commands

| Command | Description |
|---------|-------------|
| `--version` | Print version and exit |
| `--help` | Print help and exit |
| `validate` | Auto-detect OpenFOAM, validate 23 dependencies across 7 groups, write `~/.foamscript/config.json` |
| `convert` | Convert STEP/IGES → STL in meters (prerequisite for `new-study` when starting from CAD files) |
| `new-study` | Full pipeline: geometry → domain → templated cases for AoA sweep (`--template` required) |
| `mesh` | Mesh a case or study directory (auto-detects cores, parallel by default) |
| `solve` | Solve a case or study directory (auto-detects cores, parallel by default) |
| `report` | Generate AIAA-quality HTML + PDF + CSV analysis report from a completed study |
| `list-templates` | List available OpenFOAM case templates with metadata |

See **[Commands.md](Docs/Commands.md)** for full reference with all options, JSON config format, template authoring guide, and examples.

## Installation

### Prerequisites

- OpenFOAM v2312+ (tested with v2512)
- .NET 10.0 SDK
- gmsh (for STEP/IGES → STL conversion)
- openmpi (`mpirun` for parallel execution)
- ParaView with pvpython (for flow visualization)
- python3 with matplotlib and numpy (for flow visualization rendering)

Run `foamscript validate` after installation to verify all 23 dependencies and auto-configure the OpenFOAM environment. This writes `~/.foamscript/config.json` so subsequent commands automatically source the correct bashrc.

### Build & Test

```bash
dotnet build
dotnet test    # 271 tests
```

### Deploy to Linux

```bash
dotnet publish -c Release -r linux-x64 --self-contained
# Copy to Linux machine, or clone and build directly:
dotnet build -c Release
```

## Project Structure

```
foamscript/
├── Handlers/            # Command handlers (one per CLI verb)
│   ├── ICommandHandler.cs
│   ├── ValidateHandler.cs
│   ├── ConvertHandler.cs
│   ├── NewStudyHandler.cs
│   ├── MeshHandler.cs
│   ├── SolveHandler.cs
│   ├── ReportHandler.cs
│   └── ListTemplatesHandler.cs
├── Models/              # CLI models (CommandLineParser) + result/config POCOs
├── Services/            # Business logic
│   ├── AppService.cs              # CLI verb routing + pre-flight env guard
│   ├── CaseService.cs             # Study creation, template context calculation
│   ├── TemplateMetadataService.cs # TEMPLATE.json loading and validation
│   ├── StlConversionService.cs    # STEP→STL conversion, validation, scaling
│   ├── GeometryService.cs         # Facade over StlConversionService
│   ├── MeshService.cs             # Template-driven mesh pipeline execution
│   ├── SolverService.cs           # Template-driven solver execution + log persistence
│   ├── ResultsService.cs          # Metadata-driven coefficient extraction
│   ├── ReportService.cs           # Report orchestrator (data + charts + render)
│   ├── ChartGenerator.cs          # ScottPlot AIAA-styled charts (polars, convergence)
│   ├── HtmlReportGenerator.cs     # Scriban HTML report with embedded SVG charts
│   ├── PdfReportGenerator.cs      # PDFsharp PDF report with PNG charts
│   ├── VisualizationService.cs    # pvpython + matplotlib flow visualization
│   ├── ResidualParser.cs          # OpenFOAM solver log convergence parser
│   ├── CoreResolver.cs            # Auto-detect CPU cores + FOAMSCRIPT_MAX_CORES env var
│   ├── EnvironmentService.cs      # Grouped validation checklist (23 checks, 7 groups)
│   ├── OpenFoamEnvironment.cs     # Config loading, bashrc sourcing, env capture
│   └── TemplateService.cs         # Scriban template rendering
├── Templates/           # OpenFOAM case + report templates (Scriban)
│   ├── external_disc_rotatingwall_steady/   # Disc with rotating wall BC
│   ├── external_airfoil_static_steady/      # 2D airfoil steady-state
│   ├── external_disc_rotating-ami_transient/ # Disc with AMI transient
│   └── report/
│       ├── report.html            # Scriban HTML report template
│       ├── extract_slice.py       # pvpython slice data extraction
│       └── render_slice.py        # matplotlib contour plot rendering
├── Docs/Commands.md     # Full command reference
├── foamscript.Tests/    # xUnit + Moq + FluentAssertions (271 tests)
└── study.example.jsonc  # Example JSON config file
```

## Architecture

- **Template metadata system** — each template has a `TEMPLATE.json` defining geometry type, reference dimensions, pipeline steps, and post-processing; `TemplateMetadataService` loads and validates
- **Command handler pattern** — each CLI verb has a dedicated handler class (`ICommandHandler<T>`), keeping `AppService` as a thin dispatcher with pre-flight env guard
- **Environment management** — `OpenFoamEnvironment` sources bashrc, captures env vars, and injects them into all spawned processes via `ProcessExecutor.SetEnvironment()`
- **Dependency injection** via `Microsoft.Extensions.Hosting`
- **`IProcessExecutor` abstraction** wraps all external process calls — enables full unit test mocking without OpenFOAM installed
- **Result object pattern** — all service calls return typed results (`IsSuccess`, `ErrorMessage`)
- **Scriban templating** — OpenFOAM case files rendered at study creation with pre-calculated physics context

## License

MIT

## Contributing

When adding new features, update [Docs/Commands.md](Docs/Commands.md) with any new commands or options.
