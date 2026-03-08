# foamscript
C# CLI utility for OpenFOAM case management
=======
# FoamScript

.NET 10 CLI tool for automating OpenFOAM CFD case setup and meshing for disc golf disc aerodynamic analysis.

## Quick Start

```bash
# Validate OpenFOAM environment
foamscript validate

# Create parametric study with angle of attack sweep
foamscript new-study \
  -n DiscAnalysis \
  -o ~/OpenFOAM/$USER-v2512/run \
  -s ~/models/disc.step \
  -a -5,0,5,10 \
  --velocity 20 --rpm 1000 --input-units mm --cores 4

# Mesh all cases in parallel
foamscript mesh-study -d ~/OpenFOAM/$USER-v2512/run/DiscAnalysis --parallel --cores 4

# Solve all cases
foamscript solve-study -d ~/OpenFOAM/$USER-v2512/run/DiscAnalysis --parallel --cores 4

# Generate AIAA-quality analysis report (HTML + PDF)
foamscript report -d ~/OpenFOAM/$USER-v2512/run/DiscAnalysis

# Or use a JSON config file
foamscript new-study --config study.json
```

## Features

- **Full Pipeline**: STEP/IGES → STL → domain generation → Scriban-templated case creation → meshing → solving → report generation
- **AIAA-Quality Reports**: Publication-standard HTML + PDF reports with aerodynamic polars, drag polar, convergence history, mesh statistics, and coefficient tables
- **Configurable Physics**: Turbulence intensity, viscosity, end time, refinement levels — all via CLI or JSON (AIAA defaults: TI 1%, PBiCGStab+DILU, refinement 5/6, 8 boundary layers)
- **Configurable Domain**: Tunnel sizing, rotor scaling, mesh resolution — defaults follow CFD convention (5D upstream, 10D downstream, 5D radial)
- **Parallel Meshing**: blockMesh → surfaceFeatureExtract → decomposePar → snappyHexMesh (MPI) → reconstructParMesh
- **Parallel Solving**: decomposePar → simpleFoam (MPI) → reconstructPar
- **Post-Processing**: Force coefficient extraction (Cd, Cl, CmPitch) with time-window averaging and Cl/Cd ratio
- **Parametric Studies**: Angle of attack sweeps with automatic velocity decomposition and turbulence parameter calculation
- **Template System**: Scriban-rendered OpenFOAM case files; template naming: `{domain}_{feature}_{motion}_{solver-type}`

## Commands

| Command | Description |
|---------|-------------|
| `validate` | Validate OpenFOAM environment (tools, env vars) |
| `convert` | Convert STEP/IGES → STL via gmsh with unit scaling |
| `generate-domain` | Generate rotor cylinder + tunnel box STL from disc geometry |
| `new-study` | Full pipeline: geometry → domain → templated cases for AoA sweep |
| `mesh` | Run blockMesh + snappyHexMesh on a single case (serial or parallel) |
| `mesh-study` | Batch mesh all cases in a study directory |
| `solve` | Run solver on a single meshed case (serial or parallel) |
| `solve-study` | Batch solve all cases in a study directory |
| `report` | Generate AIAA-quality HTML + PDF analysis report from a completed study |
| `list-templates` | List available OpenFOAM case templates |

See **[Commands.md](Docs/Commands.md)** for full reference with all options, JSON config format, and examples.

## Installation

### Prerequisites

- OpenFOAM v2312+ (tested with v2512)
- .NET 10.0 SDK
- gmsh (for STEP/IGES → STL conversion)

### Build & Test

```bash
dotnet build
dotnet test    # 169 tests
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
│   ├── GenerateDomainHandler.cs
│   ├── NewStudyHandler.cs
│   ├── MeshHandler.cs
│   ├── SolveHandler.cs
│   ├── ReportHandler.cs
│   └── ListTemplatesHandler.cs
├── Models/              # CLI models (CommandLineParser) + result/config POCOs
├── Services/            # Business logic
│   ├── AppService.cs            # CLI verb routing (thin dispatcher)
│   ├── CaseService.cs           # Study creation, template context calculation
│   ├── StlConversionService.cs  # STEP→STL conversion, validation, scaling
│   ├── DomainService.cs         # Domain generation (rotor/tunnel STL)
│   ├── GeometryService.cs       # Facade over StlConversion + Domain services
│   ├── MeshService.cs           # blockMesh, snappyHexMesh, parallel workflow
│   ├── SolverService.cs         # Solver execution, force coefficient extraction, log persistence
│   ├── ResultsService.cs        # Results aggregation from coefficient.dat files
│   ├── ReportService.cs         # Report orchestrator (data collection + chart + render)
│   ├── ChartGenerator.cs        # ScottPlot AIAA-styled charts (polars, convergence)
│   ├── HtmlReportGenerator.cs   # Scriban HTML report with embedded SVG charts
│   ├── PdfReportGenerator.cs    # PdfSharpCore PDF report with PNG charts
│   ├── ResidualParser.cs        # OpenFOAM solver log convergence parser
│   ├── EnvironmentService.cs    # OpenFOAM environment validation
│   └── TemplateService.cs       # Scriban template rendering
├── Templates/           # OpenFOAM case + report templates (Scriban)
│   ├── external_disc_rotatingwall_steady/
│   └── report/report.html
├── Docs/Commands.md     # Full command reference
├── foamscript.Tests/    # xUnit + Moq + FluentAssertions (169 tests)
└── study.example.jsonc  # Example JSON config file
```

## Architecture

- **CommandLineParser** for declarative CLI verb/option parsing
- **Command handler pattern** — each CLI verb has a dedicated handler class (`ICommandHandler<T>`), keeping `AppService` as a thin dispatcher (~70 LOC)
- **Dependency injection** via `Microsoft.Extensions.Hosting`
- **Service layer split** — `StlConversionService` (STEP→STL), `DomainService` (geometry generation), `MeshService` (OpenFOAM meshing), `SolverService` (solver execution + log persistence), `ResultsService` (coefficient extraction), `ReportService` (AIAA report orchestration with `ChartGenerator`, `HtmlReportGenerator`, `PdfReportGenerator`)
- **`IProcessExecutor` abstraction** wraps all external process calls — enables full unit test mocking without OpenFOAM installed
- **Result object pattern** — all service calls return typed results (`IsSuccess`, `ErrorMessage`)
- **Scriban templating** — OpenFOAM files rendered at case creation with pre-calculated physics context

## License

MIT

## Contributing

When adding new features, update [Docs/Commands.md](Docs/Commands.md) with any new commands or options.
