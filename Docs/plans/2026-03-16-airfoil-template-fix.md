# Airfoil Template Fix Plan

**Date:** 2026-03-16
**Issue:** Airfoil template does not match OpenFOAM v2512 `airfoilWithLayers` tutorial
**Severity:** Blocking — template fails at production refinement levels

## Root Cause

Two compounding failures:

1. **Template files were not faithfully copied from the tutorial.** The snappyHexMeshDict, surfaceFeatureExtractDict, blockMeshDict, and boundary conditions were invented rather than parameterized from the tutorial source. Key divergences caused snappyHexMesh to fail during parallel morphing/snapping at refinement levels 5-6.

2. **CLI hardcoded defaults override TEMPLATE.json defaults.** `NewStudyModel.cs` defines `RefinementLevelMin=5` and `RefinementLevelMax=6` as non-nullable ints. The handler already has a pattern for applying template defaults (lines 162-173 for velocity/rpm), but refinement was never wired in because the CLI property is `int` (always has a value), not `int?` (nullable, signals "user didn't specify").

## Rules for This Fix

1. **Tutorial settings are the source of truth.** Every template file must be a parameterized copy of the tutorial. No "improvements," no "arguably better" alternatives. Match the tutorial.
2. **No new files without analysis.** Adding a file like `meshQualityDict` requires understanding its purpose, how it impacts the project, and whether it needs to be modeled/templated. Stop and analyze first.
3. **E2E validation before shipping.** Full pipeline on Linux: new-study → mesh → solve → report.
4. **Documentation updated alongside every change.**

## What Must Change

### Part A: Template-Driven Parameter Defaults (Architectural Fix)

**Goal:** When the user doesn't specify `--refinement-min`/`--refinement-max`, the template's TEMPLATE.json defaults should be used, not hardcoded CLI defaults. Fallback if neither CLI nor template specifies: 0 (OpenFOAM minimum).

**Files to change:**

1. **`Models/NewStudyModel.cs`** (lines 74-78)
   - Change `RefinementLevelMin` and `RefinementLevelMax` from `int` to `int?` (nullable)
   - Remove `Default = 5` and `Default = 6` from `[Option]` attributes
   - Remove `= 5` and `= 6` property initializers
   - Update HelpText to say "Template-defined" instead of hardcoded default

2. **`Models/StudyConfig.cs`** (lines 47-51)
   - Change `RefinementLevelMin` and `RefinementLevelMax` from `int` to `int?`
   - Remove `= 5` and `= 6` initializers

3. **`Handlers/NewStudyHandler.cs`** (lines 55-88, 162-173)
   - In the CLI config builder (line 76-77): pass `model.RefinementLevelMin` and `model.RefinementLevelMax` as nullable
   - In the template defaults switch (lines 165-173): add cases for `"refinementmin"` and `"refinementmax"` to apply TEMPLATE.json defaults when CLI value is null
   - Fallback to 0 if neither CLI nor template specifies
   - Update validation (line 100-101): handle nullable comparison

4. **`Services/CaseService.cs`** (lines 329-331)
   - The Scriban template variables `refinement_level_min`, `refinement_level_max`, `feature_level` come from `physics.RefinementLevelMin`/`Max`
   - Must handle nullable: by this point (after template defaults applied) values should be non-null, but use `?? 0` as final safety net

5. **`Models/StudyConfig.cs` JSON deserialization** — ensure `refinementMin`/`refinementMax` in JSON config files still work (they're `int?` now, which JSON deserializer handles)

6. **Disc template `TEMPLATE.json`** — add `"refinementMin": {"default": 5}` and `"refinementMax": {"default": 6}` so disc behavior doesn't change

7. **Tests** — update `NewStudyHandlerTests`, `CaseServiceTests`, `SolverServiceTests` where `RefinementLevelMin`/`Max` are set to non-nullable ints. All tests that create `StudyPhysicsConfig` will need to supply values explicitly or test the nullable default path.

### Part B: Airfoil Template Files (Match Tutorial Exactly)

**Reference:** `/usr/lib/openfoam/openfoam2512/tutorials/mesh/snappyHexMesh/airfoilWithLayers/`

Every file must be compared line-by-line with the tutorial. Parameterize only user-configurable values (velocity, angles, domain size, refinement levels). Everything else matches the tutorial verbatim.

**Committed in e80fc19 — NEEDS RE-REVIEW:**
- `system/snappyHexMeshDict` — was rewritten to match tutorial settings, BUT it uses `#include "meshQualityDict"` which introduces a new file. This needs to be analyzed:
  - What is `meshQualityDict`? What does it control?
  - Do other templates need it? Should it be shared or per-template?
  - Should it be parameterized or static?
  - Does it need to be registered in the template system?
  - **Action:** Analyze the meshQualityDict pattern before committing to it. If snappyHexMeshDict can inline those values instead of using `#include`, that may be simpler and avoids the new-file question entirely.
- `system/meshQualityDict` — was added as a new file. **This was premature.** Must be analyzed as described above before keeping it.
- `TEMPLATE.json` — refinement defaults changed from 5/6 to 2/2 (correct)

**Files to fix:**

1. **`system/blockMeshDict`**
   - Tutorial uses a single `airFlow` patch for inlet/outlet/walls. **Match the tutorial** — use single `airFlow` patch.
   - Update all BC files (0/U, 0/p, 0/nuTilda, 0/nut) to use `airFlow` patch name instead of split patches.
   - Keep `symmetryPlane` front/back and 5 spanwise cells (matches tutorial).
   - Domain extents are parameterized (OK — intentional generalization).

2. **`system/surfaceFeatureExtractDict`**
   - Tutorial uses `aerofoil.stl` (British spelling), ours uses `airfoil.stl` — keep ours (matches our STL naming)
   - Match tutorial settings: `includedAngle 120`, `curvature true`, `geometricTestOnly yes`, `intersectionMethod none`

3. **`system/fvSchemes`**
   - Tutorial has a `geometry` block: `type highAspectRatio; minAspect 10; maxAspect 100;`
   - Match the tutorial — add the geometry block.

4. **`system/fvSolution`**
   - Tutorial has a `cellDisplacement` solver (GAMG, tolerance 1e-08, GaussSeidel smoother)
   - Match the tutorial — add cellDisplacement solver.

5. **`0/U`, `0/p`, `0/nuTilda`, `0/nut`**
   - Change patch names from split (inlet/outlet/lowerWall/upperWall) to single `airFlow` to match blockMeshDict and tutorial.

6. **`system/controlDict`**
   - Verify `purgeWrite`, `writeFormat`, `writePrecision` match tutorial values (2, binary, 15).

7. **`system/snappyHexMeshDict`**
   - Re-review the `#include "meshQualityDict"` pattern (see above).
   - Tutorial runs snappy in SERIAL (no `-parallel`). **Match the tutorial** — run snappy serial.

8. **`TEMPLATE.json` meshPipeline**
   - Match tutorial: `blockMesh → surfaceFeatureExtract → snappyHexMesh` (serial, no decomposePar before snappy)
   - decomposePar only in solvePipeline, not meshPipeline
   - `-overwrite` flag: tutorial does NOT use it. Match the tutorial.

### Part C: E2E Validation

After Parts A and B are complete:

1. Clean old test: `rm -rf /home/trboyden/OpenFOAM/trboyden-v2512/run/airfoil-e2e-test`
2. Create study: `dotnet run -- new-study --template external_airfoil_static_steady --project-name airfoil-e2e-test --output-dir ... --model-source /home/trboyden/models/NACA0012.stl --angles 0 --velocity 26`
   - **Do NOT specify --refinement-min/max** — template defaults (2,2) should apply automatically
3. Mesh: `dotnet run -- mesh -d .../airfoil-e2e-test`
   - Verify snappyHexMesh succeeds (running serial)
   - Verify checkMesh passes
4. Solve: `dotnet run -- solve -d .../airfoil-e2e-test`
   - Verify simpleFoam converges
5. Report: `dotnet run -- report -d .../airfoil-e2e-test`
   - Verify HTML + PDF + CSV generated
   - Verify coefficient data extracted (Cd, Cl, CmPitch)

### Part D: Documentation Updates

Per feedback rule — documentation must be updated alongside every code change:

1. **`Docs/Commands.md`** — update refinement-min/max defaults to say "Template-defined (fallback: 0)"
2. **`study.example.jsonc`** — update refinement comments
3. **`README.md`** — verify quick start examples still accurate
4. **CLI help text** — updated automatically when NewStudyModel.cs changes

## Execution Order

1. Part A (template-driven defaults) — architectural prerequisite
2. Part B (template files) — match tutorial exactly
3. Build + unit tests locally
4. Part C (E2E on Linux) — prove it works end-to-end
5. Part D (docs)
6. Commit, push, merge

## Files Changed Summary

| File | Change |
|------|--------|
| `Models/NewStudyModel.cs` | `int` → `int?` for refinement, remove hardcoded defaults |
| `Models/StudyConfig.cs` | `int` → `int?` for refinement |
| `Handlers/NewStudyHandler.cs` | Add refinement to template defaults switch, fallback 0 |
| `Services/CaseService.cs` | Handle nullable refinement with `?? 0` safety net |
| `Templates/external_airfoil_static_steady/system/blockMeshDict` | Single `airFlow` patch (match tutorial) |
| `Templates/external_airfoil_static_steady/system/surfaceFeatureExtractDict` | Match tutorial settings |
| `Templates/external_airfoil_static_steady/system/fvSchemes` | Add geometry block (match tutorial) |
| `Templates/external_airfoil_static_steady/system/fvSolution` | Add cellDisplacement solver (match tutorial) |
| `Templates/external_airfoil_static_steady/system/controlDict` | Verify matches tutorial |
| `Templates/external_airfoil_static_steady/system/snappyHexMeshDict` | Re-review meshQualityDict include |
| `Templates/external_airfoil_static_steady/0/*` | Patch names → `airFlow` (match tutorial) |
| `Templates/external_airfoil_static_steady/TEMPLATE.json` | meshPipeline: serial snappy, no -overwrite |
| `Templates/external_disc_rotatingwall_steady/TEMPLATE.json` | Add explicit refinement defaults 5/6 |
| Tests (multiple) | Update for nullable refinement |
| `Docs/Commands.md` | Update refinement default text |
| `study.example.jsonc` | Update refinement comments |

## Open Analysis Required

**meshQualityDict:** The tutorial's snappyHexMeshDict uses `#include "meshQualityDict"`. Before adopting this pattern:
- Determine if inlining the values directly in snappyHexMeshDict is viable (avoids new file)
- If the file is needed, analyze whether it should be shared across templates or per-template
- Determine if any values need parameterization
- Understand how this interacts with the template rendering pipeline (does Scriban process `#include` directives? No — OpenFOAM does at runtime. So the file just needs to exist in the case directory.)
