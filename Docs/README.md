# FoamScript Documentation

Welcome to the FoamScript documentation!

## What is FoamScript?

FoamScript is a C# CLI utility that streamlines OpenFOAM CFD workflows, specifically designed for disc golf disc aerodynamic analysis. It automates the tedious parts of case setup, geometry processing, and parametric studies.

## Quick Start

```bash
# Validate your OpenFOAM environment
foamscript validate

# Create a parametric study with angle of attack sweep
foamscript new-study \
  --output-dir ~/disc_analysis \
  --template ~/disc_template \
  --model-source ~/my_disc.step \
  --angles -5,-2.5,0,2.5,5 \
  --velocity 20 \
  --rpm 1000 \
  --input-units mm \
  --cores 8
```

## Documentation

- **[Commands.md](Commands.md)** - Complete command reference with examples
  - [validate](Commands.md#validate) - Validate OpenFOAM environment
  - [convert](Commands.md#convert) - Convert STEP/IGES to STL
  - [generate-domain](Commands.md#generate-domain) - Generate rotor and tunnel domains
  - [new-study](Commands.md#new-study) - Create parametric study with AoA sweep

## Features

### Integrated Workflow
The `new-study` command provides a complete integrated workflow:
- ✓ Geometry conversion (STEP/IGES → STL) with unit scaling
- ✓ Domain generation (rotor and tunnel)
- ✓ Multiple case creation for angle of attack sweeps
- ✓ Automatic parameter calculation (velocity components, omega)
- ✓ Template-based case management

### Standalone Tools
Individual commands for manual workflows:
- `convert` - STEP/IGES to STL conversion with validation
- `generate-domain` - Rotor and tunnel domain generation
- `validate` - Environment validation

## Key Concepts

### Template-Based Configuration
FoamScript uses a template case with a centralized `constant/caseSettings` file that defines all study parameters. This template is copied for each angle of attack, with parameters automatically updated.

### Angle of Attack Sweep
Create multiple cases with different angles of attack in a single command. FoamScript automatically:
- Calculates velocity components: `Ux = V*cos(α)`, `Uy = V*sin(α)`
- Converts RPM to rad/s: `ω = RPM * 2π / 60`
- Updates case settings for each angle

### Geometry Processing
All geometry is processed in a centralized `geometry/` directory and copied to each case, ensuring consistency and efficiency.

## System Requirements

- **OpenFOAM**: v2312 or later (tested with v2512)
- **.NET**: 10.0 or later
- **gmsh**: For STEP/IGES conversion
- **OS**: Linux (Ubuntu 24.04 recommended), Windows with WSL

## Installation

See [main README](../README.md) for installation instructions.

## Contributing

When adding new features:
1. Add XML documentation comments to code
2. Add CommandLine help text to models
3. Update [Commands.md](Commands.md) with new command/option documentation
4. Include examples in documentation

## Support

For issues or questions:
- Check [Troubleshooting](Commands.md#troubleshooting) section
- Review command help: `foamscript <command> --help`
- File an issue on GitHub
