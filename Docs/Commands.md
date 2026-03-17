# FoamScript Command Reference

Complete reference for all FoamScript commands with explanations and examples.

## Table of Contents

- [validate](#validate) - Validate OpenFOAM environment
- [convert](#convert) - Convert STEP/IGES geometry to STL
- [new-study](#new-study) - Create OpenFOAM study with angle of attack sweep
- [mesh](#mesh) - Mesh a case or study directory
- [solve](#solve) - Run solver on a case or study directory
- [report](#report) - Generate AIAA-quality analysis report (HTML + PDF + CSV)
- [list-templates](#list-templates) - List available templates

---

## validate

Setup wizard and health check. Auto-detects OpenFOAM installation, sources the bashrc to capture environment variables, and validates all 23 dependencies grouped by pipeline stage. On success, writes `~/.foamscript/config.json` so all other commands can automatically source the OpenFOAM environment.

### Usage

```bash
foamscript validate [OPTIONS]
```

### Options

| Option | Short | Description | Default |
|--------|-------|-------------|---------|
| `--quiet` | `-q` | Only show failures (exit code only) | `false` |

### What It Checks (23 checks, 7 groups)

| Group | Checks | Install Hint |
|-------|--------|-------------|
| **OpenFOAM** | Installation detection, bashrc sourcing, version, WM_PROJECT_DIR, WM_PROJECT_VERSION, FOAM_APPBIN, FOAM_LIBBIN | `source /path/to/bashrc` |
| **Meshing Tools** | blockMesh, snappyHexMesh, surfaceFeatureExtract, surfaceOrient, surfaceCheck, decomposePar, reconstructParMesh, checkMesh | Included with OpenFOAM |
| **Solver Tools** | simpleFoam, pimpleFoam, reconstructPar | Included with OpenFOAM |
| **Parallel Execution** | mpirun | `sudo apt install openmpi-bin` |
| **Geometry Processing** | gmsh | `sudo apt install gmsh` |
| **Flow Visualization** | pvpython | `sudo apt install paraview` |
| **Python Libraries** | matplotlib, numpy | `pip3 install matplotlib numpy` |

All checks are **required** — any failure means FoamScript cannot run the full pipeline.

### Config File

On success, writes `~/.foamscript/config.json`:
```json
{
  "openfoamBashrc": "/usr/lib/openfoam/openfoam2512/etc/bashrc",
  "openfoamVersion": "v2512",
  "configuredAt": "2026-03-16T10:47:15Z"
}
```

This config is read at startup by all other commands (except `list-templates`) to source the OpenFOAM environment automatically. Users no longer need to manually run `source bashrc` before using FoamScript.

### Examples

**First-time setup:**
```bash
foamscript validate
```

**Quiet mode (useful in scripts):**
```bash
foamscript validate --quiet
if [ $? -eq 0 ]; then
    echo "Environment OK"
fi
```

### Exit Codes

- `0` - All checks passed, config written
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

- **Output is always in meters** (OpenFOAM convention) — verify converted dimensions match expected geometry size
- **Lower mesh-size = finer mesh** (more triangles, larger file, better accuracy)
- **Feature angle preserves sharp edges** (use 20-45° for most cases)
- **Important:** The `convert` command must be run before `new-study` if your geometry is in STEP/IGES format. The `new-study` command requires STL files in meters.

---

## new-study

Creates a complete OpenFOAM study with multiple cases for angle of attack sweep. This is the main command for setting up parametric studies. The `--template` flag selects the simulation type — each template defines its geometry type, solver, mesh/solve pipeline steps, and which parameters are required or optional.

Parameters can be supplied via CLI options or a JSON config file (see [JSON Config File](#json-config-file)).

### Usage

```bash
# CLI options
foamscript new-study --template <template> -n <name> -o <dir> -s <model> -a <angles> [OPTIONS]

# JSON config file
foamscript new-study --config study.json
```

### Required Options (when not using --config)

| Option | Short | Description |
|--------|-------|-------------|
| `--template` | `-t` | Template name (run `list-templates` to see available) |
| `--project-name` | `-n` | Project name (folder and case naming) |
| `--output-dir` | `-o` | Parent directory for the project folder |
| `--model-source` | `-s` | Path to STL geometry file (in meters). Run `convert` first for STEP/IGES files. |
| `--angles` | `-a` | Angles of attack in degrees, comma-separated |

### Study Options

| Option | Short | Description | Default |
|--------|-------|-------------|---------|
| `--config` | `-c` | Path to JSON config file (replaces all CLI options) | - |
| `--velocity` | `-v` | Freestream velocity magnitude (m/s) | Template-defined |
| `--rpm` | `-r` | Rotation speed (RPM) — only for rotating templates | Template-defined |
| `--feature-angle` | | Feature angle for edge preservation (degrees) | - |
| `--cores` | | Number of CPU cores (0 = auto-detect all available) | `0` |

### Physics Parameters

All physics parameters are optional. When omitted, defaults are read from the template's `TEMPLATE.json`.

| Option | Description | Default |
|--------|-------------|---------|
| `--nu` | Kinematic viscosity (m²/s) — air at ~20°C sea level | Template-defined |
| `--turbulence-intensity` | Freestream turbulence intensity (fraction, e.g. 0.01 = 1%) | Template-defined |
| `--max-iterations` | Maximum solver iterations (steady-state) | Template-defined |
| `--write-interval` | Write results every N iterations | Template-defined |
| `--end-time` | Simulation end time — transient only (seconds) | Template-defined |
| `--outer-correctors` | PIMPLE outer corrector iterations — transient only | Template-defined |
| `--refinement-min` | snappyHexMesh minimum refinement level | Template-defined |
| `--refinement-max` | snappyHexMesh maximum refinement level | Template-defined |

### Domain Geometry Parameters

Domain sizing is primarily controlled by the template's `TEMPLATE.json` (upstream, downstream, radial extents and margin). CLI overrides are available for tunnel extent parameters. All scales are relative to the geometry's reference length (diameter for discs, chord for airfoils).

| Option | Description | Default |
|--------|-------------|---------|
| `--tunnel-upstream` | Wind tunnel upstream extent (reference lengths) | `5.0` |
| `--tunnel-downstream` | Wind tunnel downstream extent (reference lengths) | `10.0` |
| `--tunnel-radial` | Wind tunnel radial extent (reference lengths) | `5.0` |

### How It Works

1. **Creates study directory structure**
2. **Loads template metadata** from `TEMPLATE.json` — determines geometry type, required STL files, pipeline steps, and parameter defaults
3. **Copies STL geometry** to study directory and checks bounding box against template validation rules — if dimensions suggest non-meter units (mm, cm, in), prints a warning with a suggested `foamscript convert` command (warning only, does not block execution)
4. **Applies template defaults** — any physics parameter not specified on the CLI is filled from `TEMPLATE.json`
5. **Computes domain extents** from geometry bounding box and template domain config (upstream, downstream, radial, margin)
6. **For each angle of attack:**
   - Calculates velocity components: `Ux = V·cos(α)`, `Uz = V·sin(α)`
   - For rotating templates: `ω = RPM × 2π/60`
   - Derives turbulence parameters (`k`, `ω_turb`) from physics config
   - Renders Scriban template files with all parameters
   - Copies required STL files to `constant/triSurface/`

### Directory Structure

The exact structure depends on the template. Example for a disc study with rotating wall BC:

```
{outputDir}/
└── {projectName}/
    ├── geometry/                   # Master geometry (copied once)
    │   └── disc.stl               # User-provided STL (in meters)
    ├── {projectName}_-5.0/        # Case for AoA = -5°
    │   ├── 0/                     # Initial conditions (U, p, k, omega, nut)
    │   ├── constant/
    │   │   ├── triSurface/        # STL files (per TEMPLATE.json requiredStlFiles)
    │   │   └── transportProperties
    │   └── system/                # blockMeshDict, snappyHexMeshDict, etc.
    ├── {projectName}_0.0/
    └── {projectName}_5.0/
```

### Examples

**Disc study — rotating wall BC, steady-state:**
```bash
# Convert STEP to STL first (if needed)
foamscript convert ~/my_disc.step ~/my_disc.stl --input-units mm

# Create study from STL
foamscript new-study \
  --template external_disc_rotatingwall_steady \
  -n DiscAnalysis \
  -o ~/studies \
  -s ~/my_disc.stl \
  -a -5,0,5,10 \
  --velocity 27 --rpm 925
```

**Airfoil study — static geometry, steady-state:**
```bash
foamscript convert ~/airfoil.step ~/airfoil.stl --input-units mm

foamscript new-study \
  --template external_airfoil_static_steady \
  -n AirfoilStudy \
  -o ~/studies \
  -s ~/airfoil.stl \
  -a -5,0,5,10 \
  --velocity 30
```

**AoA sweep with custom physics:**
```bash
foamscript new-study \
  --template external_disc_rotatingwall_steady \
  -n HighAlt \
  -o ~/studies \
  -s ~/disc.stl \
  -a -10,-5,0,5,10 \
  --velocity 15 --rpm 1200 \
  --nu 1.8e-5 \
  --turbulence-intensity 0.03 \
  --cores 8
```

**Custom mesh refinement:**
```bash
foamscript new-study \
  --template external_disc_rotating-ami_transient \
  -n HiFi \
  -o ~/studies \
  -s ~/disc.stl \
  -a 0,5 \
  --velocity 27 --rpm 925 \
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
  "modelSource": "~/my_disc.stl",
  "angles": "-5,0,5,10",
  "velocity": 27.0,
  "rpm": 925.0,
  "featureAngle": null,
  "cores": 0,
  "physics": {
    "nu": 1.5e-5,
    "turbulenceIntensity": 0.01,
    "maxIterations": 500,
    "writeInterval": 100,
    "endTime": 1.0,
    "nOuterCorrectors": 3,
    "refinementLevelMin": 5,
    "refinementLevelMax": 6
  },
  "domain": {
    "tunnelUpstream": 5.0,
    "tunnelDownstream": 10.0,
    "tunnelRadial": 5.0
  }
}
```

### Notes

- **Required fields**: `projectName`, `outputDir`, `templateName`, `modelSource`, `angles`
- **`modelSource` must be an STL file in meters** — run `foamscript convert` first if you have STEP/IGES geometry
- **All other fields are optional** — omitted physics/domain fields use defaults from the template's `TEMPLATE.json`
- **JSON keys are case-insensitive** — `projectName`, `ProjectName`, and `project_name` are all accepted
- The `physics` and `domain` sections can be omitted entirely if template defaults are acceptable
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

After solving, force coefficients are extracted from `postProcessing/forces/0/coefficient.dat` using dynamic header parsing and time-window averaging. The available coefficients depend on the OpenFOAM version and function object configuration.

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

- **Single case**: Displays simulation time, time-averaged force coefficients, and any warnings.
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
| `--average-window` | | Fraction of simulation to average over (0.1 = last 10%) | `0.1` |

### Report Contents

- **Aerodynamic polars**: Cl, Cd, CmPitch, and L/D vs angle of attack
- **Drag polar**: Cl vs Cd
- **Convergence history**: Residual plots from solver logs (Ux, Uy, Uz, p, k, omega)
- **Mesh statistics**: Cell counts per case
- **Physics configuration**: Velocity, RPM, turbulence intensity, viscosity, solver, refinement levels
- **Coefficient table**: Time-averaged Cd, Cl, CmPitch, Cl/Cd for all angles
- **AIAA CSV data export**: Machine-readable coefficient data with reference conditions header (always generated alongside reports)

### Examples

**Generate both HTML and PDF (default):**
```bash
foamscript report -d ~/studies/MyStudy
```

**HTML only:**
```bash
foamscript report -d ~/studies/MyStudy --format html
```

**Average over last 20% of simulation:**
```bash
foamscript report -d ~/studies/MyStudy --average-window 0.2
```

### Output

Reports are saved to `{study_dir}/report/`:
- `{StudyName}_report.html` — self-contained HTML with inline CSS and embedded SVG charts
- `{StudyName}_report.pdf` — publication-quality PDF with embedded PNG charts
- `{StudyName}_coefficients.csv` — AIAA-standard coefficient data with reference conditions header (always generated)

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

Shows each template name along with metadata from `TEMPLATE.json` (geometry type, solver, description). Templates without `TEMPLATE.json` fall back to `TEMPLATE.md` parsing.

---

## Common Workflows

### Complete Disc Study (STEP → Results)

```bash
# 1. Validate environment
foamscript validate

# 2. Convert STEP geometry to STL (meters)
foamscript convert ~/my_disc.step ~/my_disc.stl --input-units mm

# 3. Create parametric study with AoA sweep
foamscript new-study \
  --template external_disc_rotatingwall_steady \
  -n DiscAnalysis \
  -o ~/studies \
  -s ~/my_disc.stl \
  -a -10,-5,-2.5,0,2.5,5,10 \
  --velocity 27 --rpm 925

# 4. Mesh all cases (auto-detects cores, runs parallel)
foamscript mesh -d ~/studies/DiscAnalysis

# 5. Solve all cases
foamscript solve -d ~/studies/DiscAnalysis

# 6. Generate report
foamscript report -d ~/studies/DiscAnalysis
```

### Complete Airfoil Study

```bash
# Convert STEP to STL first
foamscript convert ~/naca0012.step ~/naca0012.stl --input-units mm

foamscript new-study \
  --template external_airfoil_static_steady \
  -n NACA0012 \
  -o ~/studies \
  -s ~/naca0012.stl \
  -a -5,-2.5,0,2.5,5,7.5,10 \
  --velocity 30

foamscript mesh -d ~/studies/NACA0012
foamscript solve -d ~/studies/NACA0012
foamscript report -d ~/studies/NACA0012
```

### Using a Config File for Repeatable Studies

```bash
# Convert geometry first
foamscript convert ~/my_disc.step ~/my_disc.stl --input-units mm

# Save study config as JSON
cat > ~/studies/disc_study.json << 'EOF'
{
  "projectName": "DiscAnalysis",
  "outputDir": "~/studies",
  "templateName": "external_disc_rotatingwall_steady",
  "modelSource": "~/my_disc.stl",
  "angles": "-10,-5,-2.5,0,2.5,5,10",
  "velocity": 27.0,
  "rpm": 925.0,
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

### Standalone Geometry Conversion

```bash
# Convert STEP to STL (positional arguments)
foamscript convert disc.step disc.stl \
  --input-units mm \
  --mesh-size 0.05 \
  --validate
```

---

## Tips and Best Practices

### Angle of Attack Arrays

- **Coarse sweep**: `-10,0,10` (quick exploration)
- **Standard sweep**: `-5,-2.5,0,2.5,5` (typical study)
- **Fine sweep**: `-10,-7.5,-5,-2.5,0,2.5,5,7.5,10` (publication quality)

### Parallel Execution

- **4 cores**: Development/testing
- **8 cores**: Standard workstation
- **16+ cores**: HPC clusters

### Geometry Units

The `new-study` command requires STL files already converted to **meters**. Use `foamscript convert` to handle unit conversion from STEP/IGES files:
- CAD files from SolidWorks/Fusion 360 are typically in **mm** — use `--input-units mm`
- CAD files from some tools may be in **inches** — use `--input-units in`
- STL files already in meters can be used directly with `new-study`

When in doubt, check the file in your CAD software before conversion, or use `foamscript convert ... --validate` to inspect dimensions.

---

## Troubleshooting

### "FoamScript is not configured"

Run `foamscript validate` to auto-detect and configure the OpenFOAM environment. This writes `~/.foamscript/config.json` which is read by all other commands.

### "OpenFOAM not found"

Run `foamscript validate` to diagnose. It will:
- Auto-detect OpenFOAM installations in `/usr/lib/openfoam` and `/opt`
- Source the bashrc and verify environment variables
- Show install hints for any missing dependencies

### "Geometry dimension outside expected range"

This warning appears if the template's validation rules detect unexpected geometry dimensions:
- Verify `--input-units` is correct
- Check source file units in CAD software
- Use `foamscript convert ... --validate` to inspect actual dimensions

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
- Verify OpenFOAM environment is configured (`foamscript validate`)

### Force coefficients are zero or unexpected

- Check that the simulation ran to completion (look at log output)
- Increase `--end-time` if the simulation needs more time to develop
- Try a larger `--average-window` (e.g., 0.2) if coefficients are noisy
- Verify the `forceCoeffs` function object is present in `system/controlDict`
