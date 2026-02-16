# FoamScript Command Reference

Complete reference for all FoamScript commands with explanations and examples.

## Table of Contents

- [validate](#validate) - Validate OpenFOAM environment
- [convert](#convert) - Convert STEP/IGES geometry to STL
- [generate-domain](#generate-domain) - Generate rotor and tunnel domains
- [new-study](#new-study) - Create OpenFOAM study with angle of attack sweep

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
foamscript convert [OPTIONS]
```

### Options

| Option | Short | Description | Default |
|--------|-------|-------------|---------|
| `--input` | `-i` | Input STEP/IGES file path (required) | - |
| `--output` | `-o` | Output STL file path (required) | - |
| `--input-units` | `-u` | Input file units: mm, cm, m, in, ft | `mm` |
| `--mesh-size` | `-m` | Mesh size scaling factor (lower = finer mesh) | `0.05` |
| `--feature-angle` | | Feature angle for edge preservation (degrees) | - |
| `--validate` | | Run surfaceCheck validation after conversion | `false` |

### How It Works

1. **Conversion**: Uses gmsh to convert STEP/IGES → STL
2. **Unit Scaling**: Automatically scales from input units to meters (OpenFOAM standard)
3. **Validation** (optional): Checks for watertightness, manifold edges, self-intersection

### Examples

**Convert disc from millimeters to meters:**
```bash
foamscript convert \
  --input disc.step \
  --output disc.stl \
  --input-units mm \
  --mesh-size 0.05
```

**Convert with edge preservation:**
```bash
foamscript convert \
  --input disc.step \
  --output disc.stl \
  --input-units mm \
  --mesh-size 0.05 \
  --feature-angle 30
```

**Convert and validate:**
```bash
foamscript convert \
  --input disc.step \
  --output disc.stl \
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
| `--output-dir` | `-o` | Output directory for rotor.stl and tunnel.stl (required) | - |
| `--rotor-radius` | | Rotor radius scale factor | `1.2` |
| `--rotor-height` | | Rotor height scale factor | `1.5` |
| `--tunnel-upstream` | | Tunnel upstream length (disc diameters) | `3.0` |
| `--tunnel-downstream` | | Tunnel downstream length (disc diameters) | `7.0` |
| `--tunnel-radial` | | Tunnel radial extent (disc diameters) | `4.0` |
| `--mesh-resolution` | | Mesh resolution (segments per cylinder) | `32` |
| `--validate` | | Run surfaceCheck validation on generated files | `false` |

### How It Works

1. **Analyzes disc geometry** to determine bounding box and center
2. **Generates rotor cylinder** around the disc (rotating region for MRF)
3. **Generates tunnel box** around the rotor (stationary far-field)

### Examples

**Generate with defaults:**
```bash
foamscript generate-domain \
  --disc disc.stl \
  --output-dir ./
```

**Custom tunnel dimensions (longer wake region):**
```bash
foamscript generate-domain \
  --disc disc.stl \
  --output-dir ./ \
  --tunnel-upstream 5 \
  --tunnel-downstream 10 \
  --tunnel-radial 6
```

**High-resolution mesh with validation:**
```bash
foamscript generate-domain \
  --disc disc.stl \
  --output-dir ./ \
  --mesh-resolution 64 \
  --validate
```

### Output

The command creates:
- `rotor.stl` - Cylindrical rotating region
- `tunnel.stl` - Box-shaped stationary region

And displays:
- Disc bounding box dimensions
- Rotor dimensions (radius, height)
- Tunnel extents
- Validation results (if `--validate` used)

### Notes

- **Rotor must enclose disc completely** (use scales > 1.0)
- **Tunnel must enclose rotor completely** (minimum 2-3 disc diameters)
- Typical CFD best practices:
  - Upstream: 3-5 disc diameters
  - Downstream: 7-10 disc diameters (longer for wake analysis)
  - Radial: 4-6 disc diameters

---

## new-study

Creates a complete OpenFOAM study with multiple cases for angle of attack sweep. This is the main command for setting up parametric studies.

### Usage

```bash
foamscript new-study [OPTIONS]
```

### Options

| Option | Short | Description | Default |
|--------|-------|-------------|---------|
| `--project-name` | `-n` | Project name (used for study folder and case naming) (required) | - |
| `--output-dir` | `-o` | Parent directory where project folder will be created (required) | - |
| `--template` | `-t` | Path to template case directory (required) | - |
| `--model-source` | `-s` | Path to source geometry file: STEP, IGES, or STL (required) | - |
| `--angles` | `-a` | Angles of attack in degrees, comma-separated (required) | - |
| `--velocity` | `-v` | Free stream velocity magnitude (m/s) | `20.0` |
| `--rpm` | `-r` | Disc rotation speed (RPM) | `1000` |
| `--input-units` | `-u` | Source file units: mm, cm, m, in, ft | `mm` |
| `--mesh-size` | `-m` | STL mesh size factor for STEP/IGES conversion | `0.05` |
| `--feature-angle` | | Feature angle for edge preservation (degrees) | - |
| `--cores` | | Number of CPU cores for parallel execution | `4` |

### How It Works

This command performs an **integrated workflow**:

1. **Creates study directory structure**
2. **Creates `geometry/` subdirectory** for master geometry files
3. **Copies model source file** to geometry directory
4. **Converts geometry** (if STEP/IGES):
   - Converts to STL with unit scaling
   - Applies feature angle preservation if specified
5. **Generates domain**:
   - Creates `rotor.stl` (rotating region)
   - Creates `tunnel.stl` (stationary region)
6. **Creates case for each angle**:
   - Copies template to `{studyName}_{angle}/`
   - Calculates velocity components: `Ux = V*cos(α)`, `Uy = V*sin(α)`
   - Converts RPM to rad/s: `ω = RPM * 2π / 60`
   - Updates `constant/caseSettings` with parameters
   - Copies STL files from `geometry/` to `constant/triSurface/`

### Directory Structure

```
~/studies/                          # Parent directory (from --output-dir)
└── MyProject/                      # Project folder (from --project-name)
    ├── geometry/                   # Master geometry files
    │   ├── my_disc.step           # Original source file
    │   ├── disc.stl               # Converted disc (meters)
    │   ├── rotor.stl              # Generated rotor
    │   └── tunnel.stl             # Generated tunnel
    ├── MyProject_-5.0/            # Case for AoA = -5°
    │   ├── 0/                     # Initial conditions
    │   ├── constant/
    │   │   ├── caseSettings       # Updated parameters
    │   │   └── triSurface/
    │   │       ├── disc.stl      # Copied from geometry/
    │   │       ├── rotor.stl     # Copied from geometry/
    │   │       └── tunnel.stl    # Copied from geometry/
    │   └── system/                # Solver settings
    ├── MyProject_0.0/             # Case for AoA = 0°
    └── MyProject_5.0/             # Case for AoA = 5°
```

### Examples

**Single angle (baseline case):**
```bash
foamscript new-study \
  --project-name MyProject \
  --output-dir ~/studies \
  --template ~/disc_template \
  --model-source ~/my_disc.step \
  --angles 0 \
  --velocity 20 \
  --rpm 1000 \
  --input-units mm \
  --cores 8
```

**Angle of attack sweep:**
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

**Using pre-converted STL:**
```bash
foamscript new-study \
  --project-name QuickTest \
  --output-dir ~/studies \
  --template ~/disc_template \
  --model-source ~/disc.stl \
  --angles -5,0,5 \
  --velocity 20 \
  --rpm 1000
```

**High-precision geometry conversion:**
```bash
foamscript new-study \
  --project-name HighPrecision \
  --output-dir ~/studies \
  --template ~/disc_template \
  --model-source ~/my_disc.step \
  --angles 0 \
  --velocity 20 \
  --rpm 1000 \
  --input-units mm \
  --mesh-size 0.02 \
  --feature-angle 30
```

### Parameters Updated in caseSettings

For each case, the following parameters in `constant/caseSettings` are automatically updated:

- `Ux` - X velocity component (m/s)
- `Uy` - Y velocity component (m/s)
- `constant_dynamicMeshDict_omega` - Rotation speed (rad/s)
- `system_decomposeParDict_numberOfSubdomains` - Number of cores

### Output

The command displays:
- Study parameters (directory, template, geometry)
- Geometry processing progress
- List of created cases with:
  - Angle of attack
  - Velocity components (Ux, Uy)
  - Rotation speed (omega in rad/s)

### Notes

- **Project name is used for folder and case naming** - `--project-name MyProject` creates `~/studies/MyProject/` with cases named `MyProject_{angle}`
- **Output directory is the parent folder** - `--output-dir ~/studies` means the project folder will be created inside `~/studies`
- **Template must have `constant/caseSettings` file** - This is the central configuration hub
- **Geometry is processed once** and copied to all cases (efficient for multi-case studies)
- **All geometry files are in meters** (OpenFOAM standard)
- For STEP/IGES files, conversion uses the same logic as the `convert` command

### Template Requirements

Your template case must have:
- `constant/caseSettings` - Central configuration file
- Standard OpenFOAM directory structure (`0/`, `constant/`, `system/`)
- `constant/triSurface/` directory (will be populated automatically)

### Next Steps After Creation

After creating a study, typical workflow:

1. **Navigate to a case:**
   ```bash
   cd ~/studies/MyProject/MyProject_0.0
   ```

2. **Run blockMesh:**
   ```bash
   blockMesh
   ```

3. **Run snappyHexMesh:**
   ```bash
   snappyHexMesh -overwrite
   ```

4. **Decompose for parallel:**
   ```bash
   decomposePar
   ```

5. **Run solver:**
   ```bash
   mpirun -np 8 simpleFoam -parallel
   ```

6. **Reconstruct and post-process:**
   ```bash
   reconstructPar
   paraFoam
   ```

---

## Common Workflows

### Complete Study Setup (STEP → Cases)

```bash
# 1. Validate environment
foamscript validate

# 2. Create parametric study with AoA sweep
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

# 3. Navigate to first case and run simulation
cd ~/studies/DiscAnalysis/DiscAnalysis_-10.0
blockMesh
snappyHexMesh -overwrite
decomposePar
mpirun -np 8 simpleFoam -parallel
reconstructPar
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

# 2. Generate domain
foamscript generate-domain \
  --disc disc.stl \
  --output-dir ./ \
  --validate
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

Ensure template path exists and has proper structure:
```bash
ls -la ~/disc_template/constant/caseSettings
```

### Conversion takes too long

- Increase `--mesh-size` (0.1 for quick tests)
- Remove `--feature-angle` if not needed
- Consider pre-converting geometry once and reusing STL

### Cases have incorrect velocity

Check that:
- `--velocity` is in m/s (not mph or other units)
- Angles are in degrees (not radians)
- Template's `caseSettings` has correct variable names
