# Template Generalization Design

**Date**: 2026-03-15
**Status**: Approved
**Issue**: Extends #34 (infrastructure refactoring)

## Problem

FoamScript's pipeline (mesh, solve, report) is hardcoded around disc-golf CFD assumptions. An audit found 20 disc-specific assumptions: 9 blocking (will crash for non-disc templates), 7 misleading (wrong output), 4 cosmetic. This prevents adding airfoil, duct, heat transfer, and other templates without per-template workarounds.

## Design Principles

1. **TEMPLATE.json is the single source of truth** — every template-specific behavior is declared there. No hardcoded defaults in C#.
2. **Every JSON field is CLI-overridable** — the user can override any template default from the command line.
3. **No silent fallbacks** — if TEMPLATE.json is missing, foamscript refuses to run with a clear error.
4. **Breaking changes are acceptable** — small community, early development cycle. Document and move forward.
5. **Template authors encode expertise** — the template gets the pipeline right once; end users just provide geometry + parameters.

## TEMPLATE.json Schema

Every template ships a complete TEMPLATE.json. No field is optional for the template author.

```json
{
  "name": "external_disc_rotatingwall_steady",
  "description": "Steady-state rotating disc analysis (simpleFoam + rotatingWallVelocity)",
  "solver": "simpleFoam",

  "geometry": {
    "type": "disc",
    "stlName": "disc.stl",
    "requiredStlFiles": ["disc.stl", "tunnel.stl"],
    "surfaceOrient": { "outsidePoint": [0, 0, -1] }
  },

  "reference": {
    "dimension": "diameter",
    "areaFormula": "circular"
  },

  "rotation": {
    "enabled": true,
    "requiresRotorZone": true
  },

  "validation": {
    "minSize": 0.18,
    "maxSize": 0.35,
    "warningMessage": "Geometry size outside expected range for disc template."
  },

  "parameters": {
    "velocity": { "required": true, "description": "Freestream velocity (m/s)" },
    "rpm": { "required": true, "description": "Rotational speed (RPM)" },
    "angles": { "required": true, "description": "Angle(s) of attack (degrees)" },
    "refinementMin": { "required": false, "default": 5, "description": "Minimum refinement level" },
    "refinementMax": { "required": false, "default": 6, "description": "Maximum refinement level" },
    "maxIterations": { "required": false, "default": 500, "description": "Maximum solver iterations" },
    "turbulenceIntensity": { "required": false, "default": 0.01, "description": "Turbulence intensity (0-1)" }
  },

  "domain": {
    "upstream": 5.0,
    "downstream": 10.0,
    "radial": 5.0
  },

  "meshPipeline": [
    { "command": "blockMesh", "args": "-case {caseDir}" },
    { "command": "surfaceOrient", "args": "{geometry.stlPath} \"(0 0 -1)\" {geometry.stlPath}", "optional": true },
    { "command": "surfaceFeatureExtract", "args": "-case {caseDir}" },
    { "command": "decomposePar", "args": "-case {caseDir}", "parallel": false },
    { "command": "snappyHexMesh", "args": "-case {caseDir} -overwrite", "parallel": true },
    { "command": "reconstructParMesh", "args": "-case {caseDir} -constant", "parallel": false },
    { "command": "checkMesh", "args": "-case {caseDir}", "parallel": false }
  ],

  "solvePipeline": [
    { "command": "decomposePar", "args": "-case {caseDir} -force", "parallel": false },
    { "command": "simpleFoam", "args": "-case {caseDir}", "parallel": true },
    { "command": "reconstructPar", "args": "-case {caseDir} -latestTime", "parallel": false }
  ],

  "results": {
    "type": "forceCoefficients",
    "dataFile": "postProcessing/forces/0/coefficient.dat",
    "columns": {
      "Cd": 1,
      "Cl": 4,
      "CmPitch": 7
    }
  },

  "report": {
    "template": "report/report.html",
    "standard": "AIAA",
    "postProcess": [
      { "function": "surfaces", "args": "-case {caseDir} -latestTime" }
    ]
  }
}
```

### Airfoil Example (differences)

```json
{
  "geometry": {
    "type": "airfoil",
    "stlName": "airfoil.stl",
    "requiredStlFiles": ["airfoil.stl"],
    "surfaceOrient": null
  },
  "rotation": { "enabled": false, "requiresRotorZone": false },
  "validation": null,
  "parameters": {
    "velocity": { "required": true },
    "rpm": null,
    "angles": { "required": true }
  }
}
```

## CLI Parameter Flow

1. User runs: `foamscript new-study --template <name> --velocity 26 --angles 0,5,10 ...`
2. foamscript loads TEMPLATE.json from the template directory
3. Validates all `"required": true` parameters were provided via CLI
4. If missing: error with clear message — `"Template 'external_disc_rotatingwall_steady' requires --rpm but it was not provided"`
5. Optional parameters not provided use the template's declared default

### Changes from current behavior

- `--template` becomes **required** (no default)
- `--velocity`, `--rpm`, etc. become nullable in C# model (no hardcoded defaults)
- Template `parameters` section declares required vs optional per template
- Universal physics constants (nu=1.5e-5, TI=0.01) remain available with sensible defaults because they're fluid properties, not geometry properties

## Mesh Pipeline

MeshService becomes a generic pipeline executor. It reads the `meshPipeline` array from TEMPLATE.json and runs each step in order.

- `{caseDir}` and `{geometry.stlPath}` tokens resolved at runtime
- `"parallel": true` → run via mpirun with the configured core count
- `"optional": true` → non-zero exit is a warning, not a failure
- No hardcoded steps in C# — the template controls the entire pipeline

This supports future templates that need different pipelines (e.g., blockMesh-only for structured internal flow, setFields for multiphase, createBaffles for heat transfer).

## Solve Pipeline

Same pattern as mesh. SolverService reads the `solvePipeline` array and executes.

Current hardcoded pipeline (decomposePar → simpleFoam → reconstructPar) becomes the disc/airfoil template's declared pipeline. Future templates can add pre-steps (potentialFoam initialization, setFields) or use different solvers (pimpleFoam, interFoam).

## Results Extraction

Currently hardcoded to parse `postProcessing/forces/0/coefficient.dat` with fixed column indices. The new approach:

- Template declares `results.dataFile` path and `results.columns` mapping
- C# results parser becomes generic: read file, extract declared columns, report by name
- Works for any OpenFOAM postProcessing output (force coefficients, field averages, surface values)

Future non-aero templates can declare different result types:
```json
{
  "results": {
    "type": "fieldAverage",
    "dataFile": "postProcessing/fieldAverage/0/surfaceFieldValue.dat",
    "columns": { "pressureDrop": 1, "massFlowRate": 2 }
  }
}
```

## Report Generation

### Architecture

Each template ships its own Scriban report template in `report/report.html` alongside its OpenFOAM files. The C# ReportService becomes a generic rendering engine.

### Template directory structure

```
Templates/
  external_disc_rotatingwall_steady/
    0/ constant/ system/
    TEMPLATE.json
    report/
      report.html          ← disc-specific AIAA report
  external_airfoil_static_steady/
    0/ constant/ system/
    TEMPLATE.json
    report/
      report.html          ← airfoil-specific report
```

### What moves out of C#

Chart labels, column names, section headings, report layout — all into the Scriban template. The C# code provides data and renders.

### What stays in C#

- PDF generation (PDFsharp) — generic renderer
- CSV export — writes whatever columns `results` declared
- Convergence/residual parsing — universal OpenFOAM log format
- ScottPlot chart generation — generic, data-driven

### Visualization (Python elimination)

Current Python scripts (`extract_slice.py`, `render_slice.py`) are replaced by:
1. OpenFOAM's built-in `postProcess` function objects to export slice/surface data
2. ScottPlot (already a project dependency) to render charts in C#

This eliminates the undeclared Python/ParaView dependency. Templates declare postProcess steps in `report.postProcess`.

### Disc report migration

The existing disc-golf report (AIAA formatting, Cd/Cl/Cm tables, polar curves, convergence plots, flow visualization) is migrated into the disc template directory. No functionality is lost — it just lives inside the template instead of being hardcoded in C#.

## Domain Sizing

Help text changes from "in disc diameters" to "in reference lengths". The `domain` section in TEMPLATE.json declares upstream/downstream/radial multipliers. These are applied to the template's reference dimension (diameter for disc, chord for airfoil).

## Hardcoded Assumptions — Full Resolution

### Blocking (9 issues)

| Issue | Resolution |
|-------|-----------|
| Default template hardcoding | `--template` required, no default |
| Rotor zone assumption | `rotation.requiresRotorZone` in TEMPLATE.json |
| disc.stl surfaceOrient | `meshPipeline` declares orient step (or omits it) |
| Required STL files default | `geometry.requiredStlFiles` in TEMPLATE.json |
| Geometry STL filename default | `geometry.stlName` in TEMPLATE.json |
| Force coefficient extraction path | `results.dataFile` in TEMPLATE.json |
| Coefficient column indices | `results.columns` mapping in TEMPLATE.json |
| Reference dimension default | `reference.dimension` in TEMPLATE.json |
| Reference area formula default | `reference.areaFormula` in TEMPLATE.json |

### Misleading (7 issues)

| Issue | Resolution |
|-------|-----------|
| velocity=27/rpm=925 defaults | Nullable CLI flags; template `parameters` declares required/optional |
| Domain sizing "disc diameters" | Renamed to "reference lengths" in help text |
| Report CSV header (Cd/Cl/Cm) | Generated from `results.columns` dynamically |
| RPM in reports | Only included if `rotation.enabled` |
| Chart labels (aero-specific) | Moved to template's report Scriban file |
| PDGA validation range | Moved to template's `validation` section |
| Rotor deltaT comment | Removed; rotor logic guarded by `rotation.enabled` |

### Cosmetic (4 issues)

| Issue | Resolution |
|-------|-----------|
| `disc_diameter` template alias | Replaced with `ref_length` (already exists) |
| `discStlFile` parameter name | Renamed to `geometryStlFile` |
| rotor/rotor_slave patch names | Guarded by `rotation.enabled`; non-rotating templates skip |
| Turbulence intensity AIAA bias | Moved to template `parameters` defaults |

## Breaking Changes

These changes break backward compatibility for existing users:

1. `--template` flag is now required for `new-study`
2. `--velocity` and `--rpm` no longer have defaults — must be provided explicitly (or declared in template)
3. Existing disc templates require updated TEMPLATE.json with full schema
4. Report output may differ slightly as rendering moves to template-driven Scriban

Document these in release notes and CHANGELOG.

## Testing Strategy

1. Schema validation tests — TEMPLATE.json parsing, required field enforcement
2. Pipeline executor tests — mock command execution, token resolution
3. Parameter validation tests — required/optional enforcement, CLI override
4. Results parser tests — generic column extraction from various file formats
5. Report rendering tests — Scriban template rendering with dynamic data
6. E2E: disc template produces identical results to current behavior
7. E2E: airfoil template (already validated) works with new schema
8. Migration: existing disc studies produce equivalent reports
