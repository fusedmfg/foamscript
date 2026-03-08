# FoamScript Command Reference

Complete reference for all FoamScript commands with explanations and examples.

## Table of Contents

- [validate](#validate) - Validate OpenFOAM environment
- [convert](#convert) - Convert STEP/IGES geometry to STL
- [generate-domain](#generate-domain) - Generate rotor and tunnel domains
- [new-study](#new-study) - Create OpenFOAM study with angle of attack sweep
- [mesh](#mesh) - Mesh a case or study directory
- [solve](#solve) - Run solver on a case or study directory
- [report](#report) - Generate AIAA-quality analysis report (HTML + PDF)
- [list-templates](#list-templates) - List available templates

---

## validate

Validates that the OpenFOAM environment is properly configured and all required tools are available.

### Usage

```bash
foamscript validate [OPTIONS]
```

### Options

| Option | Short | Description | Default |
|--------|-------|-------------|---------|
| `--verbose` | `-v` | Show detailed environment information | `false` |
| `--quiet` | `-q` | Suppress all output (exit code only) | `false` |

### What It Checks

- **OpenFOAM Version**: Detects OpenFOAM installation and version
- **Environment Variables**: Checks critical variables like `WM_PROJECT_DIR`, `FOAM_RUN`
- **Required Tools**: Verifies availability of `blockMesh`, `snappyHexMesh`, `surfaceCheck`, `gmsh`

### Examples

**Basic validation:**
```bash
foamscript validate
```

**Verbose output (shows all environment variables and tool paths):**
```bash
foamscript validate --verbose
```

**Quiet mode (useful in scripts):**
```bash
foamscript validate --quiet
if [ $? -eq 0 ]; then
    echo "Environment OK"
fi
```

### Exit Codes

- `0` - All checks passed
- `-1` - One or more checks failed

---

## convert

Converts STEP or IGES CAD geometry files to STL format with unit conversion and optional validation.

### Usage

```bash
foamscript convert <input> <output> [OPTIONS]
```

### Options

| Option | Short | Description | Default |
|--------|-------|-------------|---------|
| `<input>` | | Input STEP/IGES file path (positional, required) | - |
| `<output>` | | Output STL file path (positional, required) | - |
| `--input-units` | `-u` | Input file units: mm, cm, m, in, ft | `m` |
| `--mesh-size` | `-s` | Mesh size scaling factor (lower = finer mesh) | `1.0` |
| `--feature-angle` | `-a` | Feature angle for edge preservation (degrees) | - |
| `--validate` | | Run surfaceCheck validation after conversion | `false` |
| `--verbose` | `-v` | Show detailed gmsh output | `false` |

### How It Works

1. **Conversion**: Uses gmsh to convert STEP/IGES → STL
2. **Unit Scaling**: Automatically scales from input units to meters (OpenFOAM standard)
3. **Validation** (optional): Checks for watertightness, manifold edges, self-intersection

### Examples

**Convert disc from millimeters to meters:**
```bash
foamscript convert disc.step disc.stl \
  --input-units mm
```

**Convert with edge preservation and finer mesh:**
```bash
foamscript convert disc.step disc.stl \
  --input-units mm \
  --mesh-size 0.05 \
  --feature-angle 30
```

**Convert and validate:**
```bash
foamscript convert disc.step disc.stl \
  --input-units mm \
  --validate
```

### Output

The command displays:
- Conversion progress
- Node and triangle counts
- Validation results (if `--validate` used)
- Output file path

### Notes

- **Output is always in meters** (OpenFOAM convention)
- **Lower mesh-size = finer mesh** (more triangles, larger file, better accuracy)
- **Feature angle preserves sharp edges** (use 20-45° for most cases)
- For PDGA disc golf discs (diameter ~21-30cm), typical dimensions after conversion should be 0.21-0.30m

---

## generate-domain

Generates rotor (rotating region) and tunnel (stationary region) STL files from a disc STL geometry.

### Usage

```bash
foamscript generate-domain [OPTIONS]
```

### Options

| Option | Short | Description | Default |
|--------|-------|-------------|---------|
| `--disc` | `-d` | Input disc STL file (required) | - |
| `--output-dir` | `-o` | Output directory for rotor.stl and tunnel.stl | `.` |
| `--rotor-radius-scale` | | Rotor AMI cylinder radius as multiple of disc radius | `1.25` |
| `--rotor-height-scale` | | Rotor AMI cylinder height as multiple of disc height | `1.5` |
| `--tunnel-upstream` | | Tunnel upstream extent (disc diameters) | `5.0` |
| `--tunnel-downstream` | | Tunnel downstream extent (disc diameters) | `10.0` |
| `--tunnel-radial` | | Tunnel radial extent (disc diameters) | `5.0` |
| `--mesh-resolution` | | Segments around generated cylinders | `32` |
| `--validate` | | Run surfaceCheck validation on generated files | `false` |

### How It Works

1. **Analyzes disc geometry** to determine bounding box and center
2. **Generates rotor cylinder** around the disc (AMI rotating region)
3. **Generates tunnel box** around the rotor (stationary far-field)

Defaults follow standard CFD conventions: 5D upstream, 10D downstream, 5D radial. The rotor cylinder provides ~25% clearance around the disc.

### Examples

**Generate with defaults:**
```bash
foamscript generate-domain disc.stl --output-dir ./
```

**Extended wake for wake analysis:**
```bash
foamscript generate-domain disc.stl \
  --output-dir ./ \
  --tunnel-downstream 15 \
  --validate
```

**High-resolution mesh:**
```bash
foamscript generate-domain disc.stl \
  --output-dir ./ \
  --mesh-resolution 64 \
  --validate
```

### Output

The command creates:
- `rotor.stl` - Cylindrical rotating region
- `tunnel.stl` - Box-shaped stationary region

And displays disc bounding box, rotor dimensions, tunnel extents, and optional validation results.

### Notes

- **Rotor must enclose disc completely** (use scales > 1.0)
- **Tunnel must enclose rotor completely**
- Standard CFD convention: 5D upstream, 10D downstream, 5D radial

---

## new-study

Creates a complete OpenFOAM study with multiple cases for angle of attack sweep. This is the main command for setting up parametric studies.

Parameters can be supplied via CLI options or a JSON config file (see [JSON Config File](#json-config-file)).

### Usage

```bash
# CLI options
foamscript new-study -n <name> -o <dir> -s <model> -a <angles> [OPTIONS]

# JSON config file
foamscript new-study --config study.json
```

### Required Options (when not using --config)

| Option | Short | Description |
|--------|-------|-------------|
| `--project-name` | `-n` | Project name (folder and case naming) |
| `--output-dir` | `-o` | Parent directory for the project folder |
| `--model-source` | `-s` | Source geometry file: STEP, IGES, or STL |
| `--angles` | `-a` | Angles of attack in degrees, comma-separated |

### Study Options

| Option | Short | Description | Default |
|--------|-------|-------------|---------|
| `--config` | `-c` | Path to JSON config file (replaces all CLI options) | - |
| `--template` | `-t` | Template name or path | `external_disc_rotatingwall_steady` |
| `--velocity` | `-v` | Free stream velocity magnitude (m/s) | `20.0` |
| `--rpm` | `-r` | Disc rotation speed (RPM) | `1000` |
| `--input-units` | `-u` | Source file units: mm, cm, m, in, ft | `mm` |
| `--mesh-size` | `-m` | STL mesh size factor for STEP/IGES conversion | `0.05` |
| `--feature-angle` | | Feature angle for edge preservation (degrees) | - |
| `--cores` | | Number of CPU cores for parallel execution | `4` |

### Physics Parameters

| Option | Description | Default |
|--------|-------------|---------|
| `--nu` | Kinematic viscosity (m²/s) — air at ~20°C sea level | `1.5e-5` |
| `--turbulence-intensity` | Freestream turbulence intensity (fraction, e.g. 0.01 = 1%) | `0.01` |
| `--max-iterations` | Maximum solver iterations (steady-state) | `1000` |
| `--write-interval` | Write results every N iterations | `100` |
| `--end-time` | Simulation end time — transient only (seconds) | `1.0` |
| `--outer-correctors` | PIMPLE outer corrector iterations — transient only | `3` |
| `--refinement-min` | snappyHexMesh minimum refinement level | `5` |
| `--refinement-max` | snappyHexMesh maximum refinement level | `6` |

### Domain Geometry Parameters

All scales are relative to the detected disc diameter.

| Option | Description | Default |
|--------|-------------|---------|
| `--rotor-radius-scale` | Rotor AMI cylinder radius (multiple of disc radius) | `1.25` |
| `--rotor-height-scale` | Rotor AMI cylinder height (multiple of disc height) | `1.5` |
| `--tunnel-upstream` | Wind tunnel upstream extent (disc diameters) | `5.0` |
| `--tunnel-downstream` | Wind tunnel downstream extent (disc diameters) | `10.0` |
| `--tunnel-radial` | Wind tunnel radial extent (disc diameters) | `5.0` |
| `--mesh-resolution` | Segments around generated cylinder geometries | `32` |

### How It Works

1. **Creates study directory structure**
2. **Processes geometry** (convert STEP/IGES → STL, scale to meters, validate)
3. **Generates domain** (`rotor.stl` AMI rotating region, `tunnel.stl` stationary far-field)
4. **For each angle of attack:**
   - Calculates `Ux = V·cos(α)`, `Uz = V·sin(α)`, `ω = RPM × 2π/60`
   - Derives turbulence parameters (`k`, `ω_turb`) from physics config
   - Renders Scriban template files with all parameters
   - Copies geometry STL files to `constant/triSurface/`

### Directory Structure

```
{outputDir}/
└── {projectName}/
    ├── geometry/                   # Master geometry (processed once)
    │   ├── disc.step              # Original source file
    │   ├── disc.stl               # Converted disc (meters)
    │   ├── rotor.stl              # Generated AMI cylinder
    │   └── tunnel.stl             # Generated wind tunnel box
    ├── {projectName}_-5.0/        # Case for AoA = -5°
    │   ├── 0/                     # Initial conditions (U, p, k, omega, nut)
    │   ├── constant/
    │   │   ├── triSurface/
    │   │   │   ├── disc.stl
    │   │   │   ├── rotor.stl
    │   │   │   └── tunnel.stl
    │   │   ├── dynamicMeshDict
    │   │   └── transportProperties
    │   └── system/                # blockMeshDict, snappyHexMeshDict, etc.
    ├── {projectName}_0.0/
    └── {projectName}_5.0/
```

### Examples

**Minimal — single angle with defaults:**
```bash
foamscript new-study \
  -n MyDisc \
  -o ~/studies \
  -s ~/my_disc.step \
  -a 0
```

**AoA sweep with custom velocity and RPM:**
```bash
foamscript new-study \
  -n DiscAnalysis \
  -o ~/studies \
  -s ~/my_disc.step \
  -a -10,-5,-2.5,0,2.5,5,10 \
  --velocity 15 \
  --rpm 1200 \
  --input-units mm \
  --cores 8
```

**Custom physics — high-altitude, lower viscosity simulation:**
```bash
foamscript new-study \
  -n HighAlt \
  -o ~/studies \
  -s ~/disc.stl \
  -a 0,5,10 \
  --nu 1.8e-5 \
  --turbulence-intensity 0.03
```

**Custom mesh refinement for high-fidelity study:**
```bash
foamscript new-study \
  -n HiFi \
  -o ~/studies \
  -s ~/disc.step \
  -a 0,5 \
  --refinement-min 4 \
  --refinement-max 6 \
  --end-time 2.0
```

**Using a JSON config file:**
```bash
foamscript new-study --config ~/studies/my_study.json
```

---

## JSON Config File

An alternative to specifying all CLI options is to provide a JSON config file with `--config`. This is useful for saving and repeating study setups.

### Format

```json
{
  "projectName": "MyStudy",
  "outputDir": "~/studies",
  "templateName": "external_disc_rotatingwall_steady",
  "modelSource": "~/my_disc.step",
  "angles": "-5,0,5,10",
  "velocity": 20.0,
  "rpm": 1000.0,
  "inputUnits": "mm",
  "meshSize": 0.05,
  "featureAngle": null,
  "cores": 4,
  "physics": {
    "nu": 1.5e-5,
    "turbulenceIntensity": 0.01,
    "endTime": 1.0,
    "nOuterCorrectors": 3,
    "refinementLevelMin": 5,
    "refinementLevelMax": 6
  },
  "domain": {
    "rotorRadiusScale": 1.25,
    "rotorHeightScale": 1.5,
    "tunnelUpstream": 5.0,
    "tunnelDownstream": 10.0,
    "tunnelRadial": 5.0,
    "meshResolution": 32
  }
}
```

### Notes

- **Required fields**: `projectName`, `outputDir`, `modelSource`, `angles`
- **All other fields are optional** — omitted physics/domain fields use the same defaults as the CLI options
- **JSON keys are case-insensitive** — `projectName`, `ProjectName`, and `project_name` are all accepted
- The `physics` and `domain` sections can be omitted entirely if defaults are acceptable
- `featureAngle` accepts `null` or a numeric value in degrees

---

## mesh

Generates the computational mesh for an OpenFOAM case or all cases in a study directory. Auto-detects whether the path is a single case (has `constant/` + `system/`) or a study directory containing multiple cases.

### Usage

```bash
foamscript mesh [OPTIONS]
```

### Options

| Option | Short | Description | Default |
|--------|-------|-------------|---------|
| `--dir` | `-d` | Path to case or study directory (required) | - |
| `--cores` | | Number of CPU cores (0 = auto-detect all available) | `0` |
| `--check-quality` | | Run checkMesh after meshing | `true` |
| `--overwrite` | | Overwrite existing mesh | `true` |

Parallel mode is enabled automatically when cores > 1. Set `FOAMSCRIPT_MAX_CORES` environment variable to cap auto-detected core count.

### Workflow

**Serial (1 core):**
1. `blockMesh` — background hex mesh
2. `surfaceFeatureExtract` — extract edge features for snapping
3. `snappyHexMesh -overwrite` — hex-dominant mesh with refinement

**Parallel (2+ cores):**
1. `blockMesh` — background hex mesh
2. `surfaceFeatureExtract` — extract edge features
3. `decomposePar -no-fields` — decompose mesh (no field data)
4. Distribute `triSurface/` files to processor directories
5. `mpirun -np <cores> snappyHexMesh -parallel -overwrite`
6. `reconstructParMesh -constant` — reassemble mesh

Optional: `checkMesh` for quality validation.

### Examples

**Mesh a study (auto-detects all CPU cores):**
```bash
foamscript mesh -d ~/studies/MyStudy
```

**Mesh with explicit core count:**
```bash
foamscript mesh -d ~/studies/MyStudy --cores 8
```

**Force serial for debugging:**
```bash
foamscript mesh -d ~/studies/MyStudy/MyStudy_0.0 --cores 1 --check-quality false
```

### Output

- **Single case**: Displays mesh statistics (cell/point/face counts), quality check results, and any warnings.
- **Study**: Displays a summary table showing each case name, status, cell count, and mesh quality result. All cases are always processed; any failures are reported in the summary.

---

## solve

Runs the OpenFOAM solver on a meshed case or all cases in a study directory. The solver is auto-detected from `system/controlDict` (e.g., simpleFoam for steady-state MRF, pimpleFoam for transient AMI).

### Usage

```bash
foamscript solve [OPTIONS]
```

### Options

| Option | Short | Description | Default |
|--------|-------|-------------|---------|
| `--dir` | `-d` | Path to meshed case or study directory (required) | - |
| `--cores` | | Number of CPU cores (0 = auto-detect all available) | `0` |

Parallel mode is enabled automatically when cores > 1. Set `FOAMSCRIPT_MAX_CORES` environment variable to cap auto-detected core count.

### Workflow

**Serial (1 core):**
1. `<solver> -case <dir>` — run detected solver

**Parallel (2+ cores):**
1. `decomposePar -case <dir>` — decompose with fields
2. `mpirun -np <cores> <solver> -case <dir> -parallel` — run solver
3. `reconstructPar -case <dir>` — reassemble time directories

After solving, force coefficients (Cd, Cl, CmPitch) are extracted from `postProcessing/forces/0/coefficient.dat` using time-window averaging.

### Examples

**Solve a study (auto-detects all CPU cores):**
```bash
foamscript solve -d ~/studies/MyStudy
```

**Solve with explicit core count:**
```bash
foamscript solve -d ~/studies/MyStudy --cores 8
```

**Force serial for debugging:**
```bash
foamscript solve -d ~/studies/MyStudy/MyStudy_0.0 --cores 1
```

### Output

- **Single case**: Displays simulation time, time-averaged force coefficients (Cd, Cl, Cm), and any warnings.
- **Study**: Displays a summary table showing each case name, status, and force coefficients. All cases are always processed; any failures are reported in the summary.

### Notes

- The case must be meshed before solving (run `mesh` first)
- Force coefficients are computed by the `forceCoeffs` function object in `controlDict`

---

## report

Generates publication-quality analysis reports from a completed case or study. Produces HTML (self-contained with embedded SVG charts) and/or PDF reports with aerodynamic polar charts, drag polar, convergence history, mesh statistics, physics configuration, and coefficient tables.

### Usage

```bash
foamscript report [OPTIONS]
```

### Options

| Option | Short | Description | Default |
|--------|-------|-------------|---------|
| `--dir` | `-d` | Path to case or study directory (required) | - |
| `--format` | `-f` | Output format: `html`, `pdf`, or `both` | `both` |
| `--output` | `-o` | Output directory | `{study_dir}/report/` |
| `--average-window` | | Fraction of simulation to average over (0.1 = last 10%) | `0.1` |

### Report Contents

- **Aerodynamic polars**: Cl, Cd, CmPitch, and L/D vs angle of attack
- **Drag polar**: Cl vs Cd
- **Convergence history**: Residual plots from solver logs (Ux, Uy, Uz, p, k, omega)
- **Mesh statistics**: Cell counts per case
- **Physics configuration**: Velocity, RPM, turbulence intensity, viscosity, solver, refinement levels
- **Coefficient table**: Time-averaged Cd, Cl, CmPitch, Cl/Cd for all angles

### Examples

**Generate both HTML and PDF (default):**
```bash
foamscript report -d ~/studies/MyStudy
```

**HTML only:**
```bash
foamscript report -d ~/studies/MyStudy --format html
```

**Custom output directory:**
```bash
foamscript report -d ~/studies/MyStudy -o ~/reports
```

**Average over last 20% of simulation:**
```bash
foamscript report -d ~/studies/MyStudy --average-window 0.2
```

### Output

Reports are saved to `{study_dir}/report/` by default:
- `{StudyName}_report.html` — self-contained HTML with inline CSS and embedded SVG charts
- `{StudyName}_report.pdf` — publication-quality PDF with embedded PNG charts

### Notes

- Cases must be solved before generating reports
- Convergence plots require solver log files (`log.simpleFoam`, etc.) in each case directory
- The `--average-window` controls what fraction of the simulation time is used for averaging coefficients
- Single-case and multi-case studies are both supported

---

## list-templates

Lists all available OpenFOAM case templates.

### Usage

```bash
foamscript list-templates
```

### Output

Shows each template name along with metadata from `TEMPLATE.md` (domain type, feature, solver).

---

## Common Workflows

### Complete Study (STEP → Results)

```bash
# 1. Validate environment
foamscript validate

# 2. Create parametric study with AoA sweep
foamscript new-study \
  -n DiscAnalysis \
  -o ~/studies \
  -s ~/my_disc.step \
  -a -10,-5,-2.5,0,2.5,5,10 \
  --velocity 20 \
  --rpm 1000 \
  --input-units mm \
  --cores 8

# 3. Mesh all cases (auto-detects cores, runs parallel)
foamscript mesh -d ~/studies/DiscAnalysis

# 4. Solve all cases
foamscript solve -d ~/studies/DiscAnalysis

# 5. Generate report
foamscript report -d ~/studies/DiscAnalysis
```

### Using a Config File for Repeatable Studies

```bash
# Save study config as JSON
cat > ~/studies/disc_study.json << 'EOF'
{
  "projectName": "DiscAnalysis",
  "outputDir": "~/studies",
  "modelSource": "~/my_disc.step",
  "angles": "-10,-5,-2.5,0,2.5,5,10",
  "velocity": 20.0,
  "rpm": 1000.0,
  "inputUnits": "mm",
  "cores": 8
}
EOF

# Run study from config
foamscript new-study --config ~/studies/disc_study.json

# Mesh, solve, report
foamscript mesh -d ~/studies/DiscAnalysis
foamscript solve -d ~/studies/DiscAnalysis
foamscript report -d ~/studies/DiscAnalysis
```

### Standalone Geometry Processing

```bash
# 1. Convert STEP to STL
foamscript convert \
  --input disc.step \
  --output disc.stl \
  --input-units mm \
  --mesh-size 0.05 \
  --validate

# 2. Generate domain STL files
foamscript generate-domain disc.stl --output-dir ./ --validate
```

---

## Tips and Best Practices

### Mesh Size Selection

- **Coarse (0.1)**: Quick preview, low accuracy
- **Medium (0.05)**: Standard for most studies
- **Fine (0.02-0.03)**: High accuracy, slower conversion
- **Very Fine (0.01)**: Research-grade, very slow

### Angle of Attack Arrays

- **Coarse sweep**: `-10,0,10` (quick exploration)
- **Standard sweep**: `-5,-2.5,0,2.5,5` (typical study)
- **Fine sweep**: `-10,-7.5,-5,-2.5,0,2.5,5,7.5,10` (publication quality)

### Parallel Execution

- **4 cores**: Development/testing
- **8 cores**: Standard workstation
- **16+ cores**: HPC clusters

### Geometry Units

Always specify correct input units:
- CAD files from SolidWorks/Fusion 360 are typically in **mm**
- CAD files from some tools may be in **inches**
- STL files from scanners may be in **mm** or **m**

When in doubt, check the file in your CAD software before conversion.

---

## Troubleshooting

### "OpenFOAM not found"

Run `foamscript validate --verbose` to diagnose:
- Check if OpenFOAM is installed
- Verify environment is sourced (run `. /opt/openfoam2512/etc/bashrc`)
- Check `$WM_PROJECT_DIR` environment variable

### "Disc diameter outside expected range"

This warning appears if converted geometry is not 0.21-0.30m:
- Verify `--input-units` is correct
- Check source file units in CAD software
- Use `--validate` to inspect actual dimensions

### "Template directory not found"

Ensure the template name is correct:
```bash
foamscript list-templates
```

### Conversion takes too long

- Increase `--mesh-size` (0.1 for quick tests)
- Remove `--feature-angle` if not needed
- Consider pre-converting geometry once and reusing STL

### Cases have incorrect velocity

Check that:
- `--velocity` is in m/s (not mph or other units)
- Angles are in degrees (not radians)
- Template's Scriban variables match what `CaseService` provides (see `TEMPLATE.md` in the template directory)

### Solver doesn't start

- Ensure the case is meshed first (`foamscript mesh -d <dir>`)
- Check that `constant/polyMesh/` exists in the case directory
- Verify OpenFOAM environment is sourced

### Force coefficients are zero or unexpected

- Check that the simulation ran to completion (look at log output)
- Increase `--end-time` if the simulation needs more time to develop
- Try a larger `--average-window` (e.g., 0.2) if coefficients are noisy
- Verify the `forceCoeffs` function object is present in `system/controlDict`
