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
- [Pre/Post-Processing Hooks](#prepost-processing-hooks) - Configure template script hooks
- [Creating Templates](#creating-templates) - Template authoring guide

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

Converts STEP or IGES CAD geometry files to STL format **in meters** — the required input format for `new-study`. Most CAD tools export STEP files in millimeters, inches, or other non-meter units; `convert` handles both the format conversion (STEP/IGES → STL via gmsh) and the unit scaling to meters in one step.

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
| `--validate` | | Run surfaceCheck validation after conversion | `false` |
| `--verbose` | `-v` | Show detailed gmsh output | `false` |

### How It Works

1. **Conversion**: Uses gmsh to convert STEP/IGES → STL surface mesh
2. **Unit Scaling**: Scales all coordinates from `--input-units` to meters — e.g., a disc that is 210 mm in the STEP file becomes 0.210 m in the output STL. This is critical because `new-study` and all downstream OpenFOAM operations assume geometry is in meters.
3. **Validation** (optional): Runs `surfaceCheck` to verify watertightness, manifold edges, and self-intersection

### Examples

**Convert disc from millimeters to meters:**
```bash
foamscript convert disc.step disc.stl \
  --input-units mm
```

**Convert with finer mesh:**
```bash
foamscript convert disc.step disc.stl \
  --input-units mm \
  --mesh-size 0.05
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

- **This is the prerequisite for `new-study`** when your geometry is STEP or IGES. The `new-study` command only accepts STL files and assumes they are already in meters. If you skip `convert` or use the wrong `--input-units`, your simulation domain and physics will be wrong (e.g., a 210 mm disc treated as 210 m).
- **Output is always in meters** (OpenFOAM convention) — verify converted dimensions match expected geometry size
- **`--input-units` must match your CAD file's units** — SolidWorks and Fusion 360 typically export in **mm**, some tools use **inches**. Check your CAD software's export settings if unsure.
- **Lower mesh-size = finer mesh** (more triangles, larger file, better accuracy)
- **Feature angle preserves sharp edges** (use 20-45° for most cases)
- **If you already have an STL in meters**, you can skip `convert` entirely and pass it directly to `new-study`. The `new-study` command will check the bounding box and warn if the dimensions look wrong for the template.

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
| `--model-source-units` | | Source geometry units: mm, cm, m, in, ft (STEP/IGES auto-convert) | `mm` |
| `--mesh-size` | | gmsh mesh size scaling for STEP/IGES conversion | `1.0` |
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
  "modelSource": "~/my_disc.step",
  "angles": "-5,0,5,10",
  "velocity": 27.0,
  "rpm": 925.0,
  "modelSourceUnits": "mm",
  "meshSize": 1.0,
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
- **`modelSource` accepts STL, STEP (.step/.stp), or IGES (.iges/.igs) files** — STEP/IGES files are auto-converted to STL using gmsh
- **`modelSourceUnits`**: units of the source geometry (default: `"mm"`). Ignored for STL files. Used during STEP/IGES → STL conversion to scale to meters.
- **`meshSize`**: gmsh mesh size scaling factor for STEP/IGES conversion (default: `1.0`). Ignored for STL files.
- **All other fields are optional** — omitted physics/domain fields use defaults from the template's `TEMPLATE.json`
- **JSON keys are case-insensitive** — `projectName`, `ProjectName`, and `project_name` are all accepted
- The `physics` and `domain` sections can be omitted entirely if template defaults are acceptable

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

If the template defines `preProcess` hooks in `TEMPLATE.json`, those run before the mesh pipeline. See [Pre/Post-Processing Hooks](#prepost-processing-hooks) for configuration details.

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

If the template defines `postProcess` hooks in `TEMPLATE.json`, those run after the solve pipeline completes. See [Pre/Post-Processing Hooks](#prepost-processing-hooks) for configuration details.

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

## Pre/Post-Processing Hooks

Templates can define `preProcess` and `postProcess` arrays in `TEMPLATE.json` to run custom scripts at specific points in the pipeline:

- **`preProcess`** — runs before the mesh pipeline (during `foamscript mesh`)
- **`postProcess`** — runs after the solve pipeline (during `foamscript solve`)

### Hook Schema

Each hook is a pipeline step with the same schema used by `meshPipeline` and `solvePipeline`:

```json
{
  "preProcess": [
    { "command": "surfaceCheck", "args": "{geometryStlPath}", "optional": true }
  ],
  "postProcess": [
    { "command": "postProcess", "args": "-case {caseDir} -func yPlus", "optional": false }
  ]
}
```

| Field | Type | Description |
|-------|------|-------------|
| `command` | string | Executable name (must be on PATH or in OpenFOAM bin) |
| `args` | string | Arguments with token substitution (see below) |
| `optional` | bool | If `true`, failure is logged but does not abort the pipeline. Default `false`. |
| `parallel` | bool | If `true` and cores > 1, runs with `mpirun -np <cores>`. Default `false`. |
| `parallelOnly` | bool | If `true`, step is skipped in serial mode (cores = 1). Default `false`. |

### Available Tokens

Tokens in `args` are replaced at runtime with actual paths:

| Token | Description | Example |
|-------|-------------|---------|
| `{caseDir}` | Absolute path to the current case directory | `/home/user/studies/Disc/Disc_0.0` |
| `{geometryDir}` | Absolute path to the study's `geometry/` folder | `/home/user/studies/Disc/geometry` |
| `{studyDir}` | Absolute path to the study root directory | `/home/user/studies/Disc` |
| `{geometryStlPath}` | Absolute path to the primary STL in `triSurface/` | `.../constant/triSurface/disc.stl` |
| `{cores}` | Number of CPU cores (postProcess only) | `8` |

### Failure Handling

- **Required hooks** (`optional: false`): If the command exits non-zero, the entire mesh or solve operation is aborted for that case.
- **Optional hooks** (`optional: true`): Failure is logged as a warning, and the pipeline continues normally.

### Example: Surface Validation Before Meshing

```json
{
  "preProcess": [
    {
      "command": "surfaceCheck",
      "args": "{geometryStlPath}",
      "optional": true
    }
  ]
}
```

This runs `surfaceCheck` on the STL before meshing each case. Since it's optional, a non-watertight STL warning won't block meshing.

### Example: Post-Solve y+ Calculation

```json
{
  "postProcess": [
    {
      "command": "postProcess",
      "args": "-case {caseDir} -func yPlus",
      "optional": false
    }
  ]
}
```

This runs the OpenFOAM `postProcess` utility to compute wall y+ values after solving. Since it's required, failure will mark the case solve as failed.

---

## Creating Templates

Templates are the core abstraction in FoamScript. Each template is a directory containing OpenFOAM case files (as Scriban templates) plus a `TEMPLATE.json` metadata file that tells FoamScript how to use them.

### Template Directory Structure

```
Templates/
└── my_template_name/
    ├── TEMPLATE.json              # Required — metadata, parameters, pipelines
    ├── TEMPLATE.md                # Optional — human-readable description
    ├── 0/                         # Initial condition templates (Scriban)
    │   ├── U
    │   ├── p
    │   ├── k
    │   ├── omega
    │   └── nut
    ├── constant/                  # Physical properties templates
    │   ├── transportProperties
    │   ├── turbulenceProperties
    │   └── MRFProperties          # Only if rotation.enabled
    ├── system/                    # Solver/mesh configuration templates
    │   ├── blockMeshDict
    │   ├── controlDict
    │   ├── decomposeParDict
    │   ├── fvSchemes
    │   ├── fvSolution
    │   ├── snappyHexMeshDict
    │   └── surfaceFeatureExtractDict
    └── report/                    # Optional — custom report template
        └── report.html
```

### Naming Convention

Template directory names follow the pattern: `{flow}_{geometry}_{motion}_{timeScheme}`

Examples:
- `external_disc_rotatingwall_steady` — external flow, disc, rotating wall BC, steady-state
- `external_airfoil_static_steady` — external flow, airfoil, no rotation, steady-state
- `external_disc_rotating-ami_transient` — external flow, disc, AMI sliding mesh, transient

### TEMPLATE.json Reference

The `TEMPLATE.json` file is the heart of a template. It defines everything FoamScript needs to create, mesh, solve, and report on a study.

#### Top-Level Fields

```json
{
  "name": "my_template_name",
  "description": "Human-readable description shown by list-templates",
  "solver": "simpleFoam"
}
```

| Field | Required | Description |
|-------|----------|-------------|
| `name` | Yes | Must match the directory name |
| `description` | Yes | Shown by `foamscript list-templates` |
| `solver` | Yes | OpenFOAM solver application (`simpleFoam`, `pimpleFoam`, etc.) |

#### Geometry Section

Defines what STL files the template expects and how to orient them.

```json
{
  "geometry": {
    "type": "disc",
    "stlName": "disc.stl",
    "requiredStlFiles": ["disc.stl"],
    "surfaceOrient": { "outsidePoint": [0, 0, -1] }
  }
}
```

| Field | Description |
|-------|-------------|
| `type` | Geometry category (`disc`, `airfoil`, etc.) — used for reference dimension logic |
| `stlName` | Primary STL filename (what the user-provided STL is renamed to) |
| `requiredStlFiles` | All STL files needed in `constant/triSurface/`. Simple templates need only the user's STL; AMI templates may need `rotor.stl` and `tunnel.stl` in addition. |
| `surfaceOrient` | If set, runs `surfaceOrient` with the given outside point to ensure consistent normals. Set to `null` to skip. |

#### Reference Section

Controls how force coefficients are normalized.

```json
{
  "reference": {
    "dimension": "diameter",
    "areaFormula": "circular"
  }
}
```

| Field | Values | Description |
|-------|--------|-------------|
| `dimension` | `diameter`, `chord` | Which geometry dimension to use as reference length |
| `areaFormula` | `circular`, `rectangular` | `circular` = π·r², `rectangular` = chord × span |

#### Rotation Section

```json
{
  "rotation": {
    "enabled": true
  }
}
```

When `enabled: true`, the `--rpm` parameter becomes required and angular velocity is computed for template variables.

#### Validation Section

Optional geometry size checking. When defined, `new-study` checks the STL bounding box and warns if dimensions fall outside the expected range.

```json
{
  "validation": {
    "minSize": 0.18,
    "maxSize": 0.35,
    "warningMessage": "Disc diameter outside expected PDGA range (0.21-0.30m). Ensure correct --input-units."
  }
}
```

| Field | Description |
|-------|-------------|
| `minSize` | Minimum expected max extent in meters |
| `maxSize` | Maximum expected max extent in meters |
| `warningMessage` | Custom warning shown when size is out of range |

If the STL max extent falls outside `[minSize, maxSize]`, FoamScript prints the warning along with a suggested `foamscript convert` command based on the likely source units (mm, cm, or inches). This is a **warning only** — it does not block study creation.

Set `validation` to `null` to disable size checking (useful for templates that accept arbitrary geometry sizes).

#### Parameters Section

Declares all tunable parameters with defaults and descriptions. These map to CLI options and are used to fill template defaults when CLI values are omitted.

```json
{
  "parameters": {
    "velocity": { "required": true, "description": "Freestream velocity (m/s)" },
    "rpm": { "required": true, "description": "Rotational speed (RPM)" },
    "angles": { "required": true, "description": "Angle(s) of attack (degrees)" },
    "refinementMin": { "required": false, "default": 5, "description": "Minimum refinement level" },
    "nu": { "required": false, "default": 1.5e-5, "description": "Kinematic viscosity (m²/s)" },
    "turbulenceIntensity": { "required": false, "default": 0.01, "description": "Freestream turbulence intensity" },
    "maxIterations": { "required": false, "default": 500, "description": "Maximum solver iterations" },
    "writeInterval": { "required": false, "default": 100, "description": "Write interval (iterations)" },
    "endTime": { "required": false, "default": 500, "description": "Simulation end time" },
    "nOuterCorrectors": { "required": false, "default": 1, "description": "SIMPLE/PIMPLE outer correctors" },
    "mixingLengthRatio": { "required": false, "default": 0.07, "description": "Mixing length ratio" },
    "nuTildaMultiplier": { "required": false, "default": 3.0, "description": "nu-tilda multiplier" }
  }
}
```

- **`required: true`** parameters must be provided on the CLI or in the JSON config file
- **`required: false`** parameters use their `default` value when omitted from the CLI
- `velocity`, `rpm`, and `angles` are the standard required parameters; `rpm` is only required when `rotation.enabled` is `true`

#### Domain Section

Controls how the computational domain is sized relative to the geometry.

```json
{
  "domain": {
    "upstream": 5.0,
    "downstream": 10.0,
    "radial": 5.0,
    "margin": 1.1,
    "spanRatio": null
  }
}
```

| Field | Description | Default |
|-------|-------------|---------|
| `upstream` | Upstream extent in reference lengths | - |
| `downstream` | Downstream extent in reference lengths | - |
| `radial` | Radial/lateral extent in reference lengths | - |
| `margin` | Domain sizing margin multiplier (e.g., 1.1 = 10% extra) | `1.1` |
| `spanRatio` | Span-to-reference-length ratio for 2D simulations. `null` = 3D. Values < 1.0 make STL protrude through symmetry planes for proper 2D meshing. | `null` |

#### Pipeline Sections

The `meshPipeline` and `solvePipeline` arrays define the OpenFOAM commands to run. Each step supports token substitution and parallel execution flags.

```json
{
  "meshPipeline": [
    { "command": "blockMesh", "args": "-case {caseDir}" },
    { "command": "surfaceOrient", "args": "{geometryStlPath} \"({outsidePoint})\" {geometryStlPath}", "optional": true },
    { "command": "surfaceFeatureExtract", "args": "-case {caseDir}" },
    { "command": "decomposePar", "args": "-case {caseDir}", "parallelOnly": true },
    { "command": "snappyHexMesh", "args": "-case {caseDir} -overwrite", "parallel": true },
    { "command": "reconstructParMesh", "args": "-case {caseDir} -constant", "parallelOnly": true },
    { "command": "checkMesh", "args": "-case {caseDir}" }
  ],
  "solvePipeline": [
    { "command": "decomposePar", "args": "-case {caseDir} -force", "parallelOnly": true },
    { "command": "simpleFoam", "args": "-case {caseDir}", "parallel": true },
    { "command": "reconstructPar", "args": "-case {caseDir} -latestTime", "parallelOnly": true }
  ]
}
```

Key flags:
- **`parallel: true`** — run with `mpirun -np <cores>` when cores > 1, or normally when cores = 1
- **`parallelOnly: true`** — skip entirely when running in serial (cores = 1)
- **`optional: true`** — failure doesn't abort the pipeline

#### Pre/Post-Processing Hooks

See [Pre/Post-Processing Hooks](#prepost-processing-hooks) above for full documentation. These are optional arrays that use the same pipeline step schema.

#### Results Section

Tells FoamScript where to find solver output and which columns contain which coefficients.

```json
{
  "results": {
    "type": "forceCoefficients",
    "dataFile": "postProcessing/forces/0/coefficient.dat",
    "columns": { "Cd": 1, "Cl": 4, "CmPitch": 7 }
  }
}
```

| Field | Description |
|-------|-------------|
| `type` | Results format (`forceCoefficients` is currently the only supported type) |
| `dataFile` | Relative path to coefficient data file within the case directory |
| `columns` | Map of coefficient names to column indices in the data file (0-indexed after the time column) |

#### Report Section

```json
{
  "report": {
    "template": "report/report.html",
    "standard": "AIAA"
  }
}
```

| Field | Description |
|-------|-------------|
| `template` | Path to the Scriban HTML report template (relative to template directory) |
| `standard` | Reporting standard for formatting and conventions |

### Scriban Template Variables

OpenFOAM case files in the template directory are rendered using [Scriban](https://github.com/scriban/scriban) syntax. FoamScript computes a template context for each case and passes it to the renderer.

#### Available Variables

| Variable | Source | Description |
|----------|--------|-------------|
| `ux` | Computed | Velocity X component: `V × cos(AoA)` |
| `uz` | Computed | Velocity Z component: `V × sin(AoA)` |
| `p` | Constant (0) | Reference pressure |
| `k` | Computed | Turbulent kinetic energy: `1.5 × (V × TI)²` |
| `omega_turbulence` | Computed | Specific dissipation rate from k and mixing length |
| `omega_rotation` | Computed | Disc angular velocity (rad/s): `RPM × 2π/60` |
| `mag_u_inf` | Input | Velocity magnitude for force coefficients |
| `disc_diameter` / `chord_length` | Measured | Reference length from STL bounding box |
| `aref` | Computed | Reference area (circular or rectangular per template) |
| `nu` | Config | Kinematic viscosity |
| `max_iterations` | Config | Maximum solver iterations |
| `write_interval` | Config | Write frequency |
| `end_time` | Config | Simulation end time |
| `n_outer_correctors` | Config | PIMPLE outer correctors |
| `refinement_level_min` | Config | snappyHexMesh min refinement |
| `refinement_level_max` | Config | snappyHexMesh max refinement |
| `feature_level` | Computed | Edge feature refinement level |
| `domain_upstream` | Computed | Domain upstream extent (meters) |
| `domain_downstream` | Computed | Domain downstream extent (meters) |
| `domain_radial` | Computed | Domain radial extent (meters) |
| `domain_span` | Computed | Domain span (meters, 2D templates only) |
| `cores` | Config | CPU core count for `decomposeParDict` |

#### Scriban Syntax Example

In an OpenFOAM template file (e.g., `0/U`):

```
internalField   uniform ({{ ux }} 0 {{ uz }});

boundaryField
{
    inlet
    {
        type            fixedValue;
        value           uniform ({{ ux }} 0 {{ uz }});
    }
}
```

### Checklist for Creating a New Template

1. **Start from an OpenFOAM tutorial** — templates should be faithful parameterized copies of official tutorials, not invented configurations
2. **Create the directory** under `Templates/` using the naming convention
3. **Write `TEMPLATE.json`** with all required sections (name, description, solver, geometry, reference, rotation, parameters, domain, meshPipeline, solvePipeline, results, report)
4. **Copy OpenFOAM case files** from the tutorial into `0/`, `constant/`, and `system/`
5. **Parameterize** hardcoded values using `{{ variable_name }}` Scriban syntax — all physics values that a user might want to change should be parameters
6. **Inline all values** — do not use OpenFOAM `#include` directives; all values must be present in the template files directly
7. **Add validation rules** if the template targets a specific geometry size range
8. **Add pre/post-process hooks** if the template needs custom scripts before meshing or after solving
9. **Test with `list-templates`** to verify TEMPLATE.json parses correctly
10. **E2E validate on Linux** — run the full pipeline (new-study → mesh → solve → report) to confirm the template produces correct results

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

- Increase `--mesh-size` (e.g. 2.0 for quick tests, lower values = finer mesh)
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
