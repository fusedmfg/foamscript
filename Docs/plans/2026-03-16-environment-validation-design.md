# Environment Validation Redesign

**Date:** 2026-03-16
**Status:** Approved
**Goal:** Ensure no foamscript command ever fails due to missing or misconfigured dependencies. Catch problems early, explain them clearly, and make OpenFOAM environment management transparent to the user.

---

## Problem

1. foamscript relies on the user's shell having OpenFOAM sourced. If they didn't `source bashrc`, commands fail with cryptic errors.
2. `ProcessExecutor` uses `UseShellExecute = false`, so spawned processes inherit the dotnet process's environment — not a bash shell's. The `EnvironmentService` checks tools via `bash -c` (which has its own env), creating a false positive: validate passes but commands fail.
3. Missing tools (simpleFoam, surfaceOrient, mpirun, pvpython) are not checked.
4. No install hints when tools are missing.
5. Visualization tools (pvpython, python3, matplotlib) were optional — but without them, reports are incomplete, undermining the "easy to use" value proposition.

## Design: Config + Pre-flight Guard

### 1. Config File (`~/.foamscript/config.json`)

```json
{
  "openfoamBashrc": "/usr/lib/openfoam/openfoam2512/etc/bashrc",
  "configuredAt": "2026-03-16T20:00:00Z",
  "openfoamVersion": "v2512"
}
```

- Stores the **path to bashrc**, not env vars — re-sourced every launch so upgrades are picked up automatically.
- `openfoamVersion` is informational (validate output, staleness detection).
- `configuredAt` enables "config is 6+ months old" warnings.
- Created/updated by `foamscript validate`.

### 2. Startup Env Injection (`OpenFoamEnvironment` service)

At startup, before any command except `validate`, `list-templates`, `--help`, `--version`:

1. Load `~/.foamscript/config.json`
2. Run `bash -c "source /path/to/bashrc && env"` to capture full post-source environment
3. Diff against current process env to extract OpenFOAM-added vars
4. Store as `Dictionary<string, string>`, injected into `ProcessExecutor` via DI

`ProcessExecutor.Execute()` merges these into `ProcessStartInfo.EnvironmentVariables` for every spawned process. This means blockMesh, simpleFoam, mpirun all see correct PATH and OpenFOAM vars regardless of the user's shell state.

**Error handling:**
- Config missing: "FoamScript is not configured. Run `foamscript validate` to set up."
- Bashrc path gone: "OpenFOAM bashrc not found at /path. Run `foamscript validate` to reconfigure."
- Source fails: "Failed to source OpenFOAM bashrc. Check that /path is a valid installation."

**Performance:** ~100-200ms for `bash -c "source bashrc && env"`. Sourced once per process, cached in memory.

### 3. Validate Command — Full Checklist

`foamscript validate` becomes the setup wizard and health check. All tools are **required** — no optional category.

**Grouped by pipeline stage:**

| Group | Tools | Install Hint |
|-------|-------|-------------|
| OpenFOAM | Installation detection, version, bashrc, env vars (WM_PROJECT_DIR, FOAM_APPBIN, FOAM_LIBBIN) | `source /path/to/bashrc` |
| Meshing | blockMesh, snappyHexMesh, surfaceFeatureExtract, surfaceOrient, surfaceCheck, decomposePar, reconstructParMesh, checkMesh | Included with OpenFOAM |
| Solvers | simpleFoam, pimpleFoam, reconstructPar | Included with OpenFOAM |
| Parallel | mpirun | `sudo apt install openmpi-bin` |
| Geometry | gmsh | `sudo apt install gmsh` |
| Visualization | pvpython, python3, matplotlib, numpy | `sudo apt install paraview` / `pip3 install matplotlib numpy` |

**Output format:**
```
=== FoamScript Environment Validation ===

OpenFOAM
  ✓ Installation: /usr/lib/openfoam/openfoam2512
  ✓ Version: v2512
  ...

Meshing Tools
  ✓ blockMesh
  ...

=== 20/20 checks passed. FoamScript is ready to use. ===
```

Failures show install hints:
```
  ✗ pvpython           NOT FOUND
    Install: sudo apt install paraview
```

On success, writes/updates `~/.foamscript/config.json`.

### 4. Pre-flight Guard for Other Commands

Lightweight check before command dispatch (not the full 20-check validation):

1. Config exists?
2. Bashrc path still exists on disk?
3. Source bashrc and capture env vars
4. Verify `WM_PROJECT_DIR` is set in captured env

Fails fast with actionable message pointing to `foamscript validate`.

## Files Changed

**New:**
- `Services/OpenFoamEnvironment.cs` — config loading, bashrc sourcing, env capture
- `Models/FoamScriptConfig.cs` — config file model
- `foamscript.Tests/Services/OpenFoamEnvironmentTests.cs`

**Modified:**
- `Services/ProcessExecutor.cs` — inject captured env vars into ProcessStartInfo
- `Services/EnvironmentService.cs` — rewrite: grouped checklist, install hints, all required
- `Handlers/ValidateHandler.cs` — rewrite: grouped output, pass/fail counts
- `Models/EnvironmentValidationResult.cs` — restructure for grouped results
- `Program.cs` — register OpenFoamEnvironment, add pre-flight guard
- `Services/VisualizationService.cs` — remove redundant which/import checks
- `foamscript.Tests/Services/EnvironmentServiceTests.cs` — rewrite for new structure

**Unchanged:** MeshService, SolverService, ReportService, all handlers except Validate — they benefit automatically from ProcessExecutor's env injection.

## Key Principle

foamscript owns its own OpenFOAM environment via config + bashrc sourcing. We don't re-invent OpenFOAM's env setup — we just source their bashrc programmatically so the user doesn't have to remember to do it manually.
