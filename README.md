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

# Or use a JSON config file
foamscript new-study --config study.json
```

## Features

- **Full Pipeline**: STEP/IGES → STL → domain generation → Scriban-templated case creation → meshing
- **Configurable Physics**: Turbulence intensity, viscosity, end time, refinement levels — all via CLI or JSON
- **Configurable Domain**: Tunnel sizing, rotor scaling, mesh resolution — defaults follow CFD convention (5D upstream, 10D downstream, 5D radial)
- **Parallel Meshing**: blockMesh → surfaceFeatureExtract → decomposePar → snappyHexMesh (MPI) → reconstructParMesh
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
dotnet test    # 86 tests
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
├── Models/              # CLI models (CommandLineParser) + result/config POCOs
├── Services/            # Business logic
│   ├── AppService.cs    # CLI routing, JSON config loading, validation
│   ├── CaseService.cs   # Study creation, template context calculation
│   ├── GeometryService.cs  # STEP→STL, bounding box, domain generation
│   ├── MeshService.cs   # blockMesh, snappyHexMesh, parallel workflow
│   ├── EnvironmentService.cs  # OpenFOAM environment validation
│   └── TemplateService.cs  # Scriban template rendering
├── Templates/           # OpenFOAM case templates (Scriban)
│   └── external_disc_rotating-ami_transient/
├── Docs/Commands.md     # Full command reference
├── foamscript.Tests/    # xUnit + Moq + FluentAssertions (86 tests)
└── study.example.jsonc  # Example JSON config file
```

## Architecture

- **CommandLineParser** for declarative CLI verb/option parsing
- **Dependency injection** via `Microsoft.Extensions.Hosting`
- **`IProcessExecutor` abstraction** wraps all external process calls — enables full unit test mocking without OpenFOAM installed
- **Result object pattern** — all service calls return typed results (`IsSuccess`, `ErrorMessage`)
- **Scriban templating** — OpenFOAM files rendered at case creation with pre-calculated physics context

## License

MIT

## Contributing

When adding new features, update [Docs/Commands.md](Docs/Commands.md) with any new commands or options.
