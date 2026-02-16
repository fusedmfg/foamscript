# FoamScript

C# CLI utility for OpenFOAM case management and automated CFD workflows for disc golf disc analysis.

## Quick Start

```bash
# Validate OpenFOAM environment
foamscript validate

# Create parametric study with angle of attack sweep
foamscript new-study \
  --project-name MyProject \
  --output-dir ~/studies \
  --template ~/disc_template \
  --model-source ~/my_disc.step \
  --angles -5,-2.5,0,2.5,5 \
  --velocity 20 \
  --rpm 1000 \
  --input-units mm \
  --cores 8
```

## Features

- **Integrated Workflow**: Single command creates complete study with geometry processing and multiple cases
- **Geometry Conversion**: STEP/IGES → STL with automatic unit scaling (mm, cm, in, ft → meters)
- **Domain Generation**: Automatic rotor and tunnel STL creation
- **Parametric Studies**: Angle of attack sweeps with automatic parameter calculation
- **Template-Based**: Centralized configuration with `constant/caseSettings`
- **Cross-Platform**: Developed on Windows, deployed on Linux

## Documentation

📖 **[Full Documentation](Docs/README.md)**

### Commands

- [validate](Docs/Commands.md#validate) - Validate OpenFOAM environment
- [convert](Docs/Commands.md#convert) - Convert STEP/IGES geometry to STL
- [generate-domain](Docs/Commands.md#generate-domain) - Generate rotor and tunnel domains
- [new-study](Docs/Commands.md#new-study) - Create OpenFOAM study with AoA sweep

## Installation

### Prerequisites

- OpenFOAM v2312+ (tested with v2512)
- .NET 10.0 SDK
- gmsh (for geometry conversion)

### Build

```bash
git clone <repository>
cd foamscript
dotnet build
dotnet publish -c Release
```

### Deploy

See [DEPLOY.md](DEPLOY.md) for deployment to Linux systems.

## Usage Examples

### Validate Environment

```bash
foamscript validate --verbose
```

### Convert Geometry

```bash
foamscript convert \
  --input disc.step \
  --output disc.stl \
  --input-units mm \
  --mesh-size 0.05 \
  --validate
```

### Create Complete Study

```bash
foamscript new-study \
  --project-name DiscAnalysis \
  --output-dir ~/studies \
  --template ~/disc_template \
  --model-source ~/my_disc.step \
  --angles -10,-5,-2.5,0,2.5,5,10 \
  --velocity 20 \
  --rpm 1000 \
  --input-units mm \
  --mesh-size 0.05 \
  --cores 8
```

## Development

### Run Tests

```bash
dotnet test
```

### Project Structure

```
foamscript/
├── Models/          # Command-line models
├── Services/        # Business logic
│   ├── AppService.cs
│   ├── CaseService.cs
│   ├── GeometryService.cs
│   ├── EnvironmentService.cs
│   └── MeshService.cs
├── Docs/            # Documentation
└── foamscript.Tests/
```

## License

[Add your license here]

## Contributing

See [Docs/README.md](Docs/README.md) for contribution guidelines.
