# Airfoil Template Fix Plan

**Date:** 2026-03-16
**Issue:** Airfoil template does not match OpenFOAM v2512 `airfoilWithLayers` tutorial
**Severity:** Blocking — template fails at production refinement levels

## Root Cause

Two compounding failures:

1. **Template files were not faithfully copied from the tutorial.** The snappyHexMeshDict, surfaceFeatureExtractDict, blockMeshDict, and boundary conditions were invented rather than parameterized from the tutorial source. Key divergences caused snappyHexMesh to fail during parallel morphing/snapping at refinement levels 5-6.

2. **CLI hardcoded defaults override TEMPLATE.json defaults.** `NewStudyModel.cs` defines `RefinementLevelMin=5` and `RefinementLevelMax=6` as non-nullable ints. The handler already has a pattern for applying template defaults (lines 162-173 for velocity/rpm), but refinement was never wired in because the CLI property is `int` (always has a value), not `int?` (nullable, signals "user didn't specify").

## What Must Change

### Part A: Template-Driven Parameter Defaults (Architectural Fix)

**Goal:** When the user doesn't specify `--refinement-min`/`--refinement-max`, the template's TEMPLATE.json defaults should be used, not hardcoded CLI defaults.

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
   - Add fallback defaults if neither CLI nor template specifies (e.g., 2 as safe minimum)
   - Update validation (line 100-101): handle nullable comparison

4. **`Services/CaseService.cs`** (lines 329-331)
   - The Scriban template variables `refinement_level_min`, `refinement_level_max`, `feature_level` come from `physics.RefinementLevelMin`/`Max`
   - Must handle nullable: either require non-null by this point (after template defaults applied) or provide a fallback

5. **`Models/StudyConfig.cs` JSON deserialization** — ensure `refinementMin`/`refinementMax` in JSON config files still work (they're `int?` now, which JSON deserializer handles)

6. **Disc template `TEMPLATE.json`** — verify it declares `"refinementMin": {"default": 5}` and `"refinementMax": {"default": 6}` so disc behavior doesn't change

7. **Tests** — update `NewStudyHandlerTests`, `CaseServiceTests`, `SolverServiceTests` where `RefinementLevelMin`/`Max` are set to non-nullable ints. All tests that create `StudyPhysicsConfig` will need to supply values explicitly or test the nullable default path.

### Part B: Airfoil Template Files (Match Tutorial Exactly)

**Reference:** `/usr/lib/openfoam/openfoam2512/tutorials/mesh/snappyHexMesh/airfoilWithLayers/`

The following files need to be compared line-by-line with the tutorial and corrected. Parameterize only what is user-configurable (velocity, angles, domain size, refinement levels). Everything else should match the tutorial verbatim.

**Already fixed (committed in e80fc19):**
- `system/snappyHexMeshDict` — matched tutorial settings (refinement, snap, layers, quality)
- `system/meshQualityDict` — new file, required by `#include` in snappyHexMeshDict
- `TEMPLATE.json` — refinement defaults changed from 5/6 to 2/2

**Still need verification/fixes:**

1. **`system/blockMeshDict`** — PARTIALLY CORRECT
   - Already uses `symmetryPlane` front/back (matches tutorial)
   - Already uses 5 spanwise cells (matches tutorial)
   - Domain extents are parameterized (OK — this is intentional generalization)
   - **Issue:** Tutorial uses a single `airFlow` patch for inlet/outlet/walls. Ours splits into `inlet`, `outlet`, `lowerWall`, `upperWall`. This is arguably better for BC specification but **must be validated** that snappyHexMesh and the solver handle these patch names correctly. The snappyHexMeshDict `locationInMesh` and BC files must be consistent.
   - **Decision needed:** Keep our split patches (cleaner BCs) or match tutorial's single `airFlow` patch? Recommendation: keep split patches, they're cleaner and already work (the mesh passed with refinement 2,2).

2. **`system/surfaceFeatureExtractDict`**
   - Tutorial uses `aerofoil.stl` (British spelling), ours uses `airfoil.stl` — this is correct, our STL is named `airfoil.stl`
   - Tutorial: `includedAngle 120`, `curvature true`, `geometricTestOnly yes`, `intersectionMethod none`
   - Ours: `includedAngle 150`, no `curvature`, no `geometricTestOnly`, no `intersectionMethod`
   - **Fix:** Match tutorial settings: `includedAngle 120`, add `curvature true`, `geometricTestOnly yes`, `intersectionMethod none`

3. **`system/fvSchemes`** — NEEDS REVIEW
   - Tutorial has a `geometry` block: `type highAspectRatio; minAspect 10; maxAspect 100;`
   - This is for snappyHexMesh's high-aspect-ratio geometry scheme (the tutorial runs snappy with both `basic` and `highAspectRatio` modes)
   - Our fvSchemes likely doesn't have this geometry block
   - **Fix:** Add the geometry block. This may be critical for boundary layer quality.

4. **`system/fvSolution`** — NEEDS REVIEW
   - Tutorial has a `cellDisplacement` solver for snappyHexMesh morphing
   - Our fvSolution likely has solver settings for simpleFoam but may be missing the cellDisplacement solver
   - **Fix:** Ensure cellDisplacement solver is present (needed during snappyHexMesh snapping phase)

5. **`0/U`, `0/p`, `0/nuTilda`, `0/nut`** — PARTIALLY CORRECT
   - Already have `symmetryPlane` BCs for front/back (matches tutorial pattern)
   - Freestream BCs on inlet/outlet/walls are correct for external aerodynamics
   - **Verify:** patch names in BCs match blockMeshDict patch names and snappyHexMeshDict-generated patches

6. **`system/controlDict`**
   - Tutorial: `endTime 15000`, `writeInterval 5000`, `purgeWrite 2`, `writeFormat binary`, `writePrecision 15`
   - Ours: parameterized endTime/writeInterval (correct), but verify `purgeWrite`, `writeFormat`, `writePrecision` match tutorial values

7. **`system/snappyHexMeshDict`** — ALREADY FIXED (e80fc19)
   - Matched all tutorial settings
   - One remaining question: tutorial's Allrun runs snappy in SERIAL (no `-parallel`). Our meshPipeline in TEMPLATE.json doesn't include `decomposePar` before snappy or after — but the `MeshHandler` may add parallelism. **Verify** whether snappy runs serial or parallel for this template.

8. **`TEMPLATE.json` meshPipeline**
   - Current: `blockMesh → surfaceFeatureExtract → snappyHexMesh -overwrite → checkMesh`
   - Tutorial Allrun: `blockMesh → surfaceFeatureExtract → snappyHexMesh` (no -overwrite, no checkMesh, runs SERIAL)
   - **Issue:** The tutorial runs snappy in serial. Our pipeline might decompose first and run parallel. For a quasi-2D mesh, serial snappy is safer and simpler.
   - **Decision needed:** Should the airfoil meshPipeline run snappy in serial? If so, the pipeline should NOT include decomposePar before snappy. DecomposePar should only happen before the solver.

### Part C: E2E Validation

After Parts A and B are complete:

1. Clean old test: `rm -rf /home/trboyden/OpenFOAM/trboyden-v2512/run/airfoil-e2e-test`
2. Create study: `dotnet run -- new-study --template external_airfoil_static_steady --project-name airfoil-e2e-test --output-dir ... --model-source /home/trboyden/models/NACA0012.stl --angles 0 --velocity 26`
   - **Do NOT specify --refinement-min/max** — template defaults (2,2) should apply automatically
3. Mesh: `dotnet run -- mesh -d .../airfoil-e2e-test`
   - Verify snappyHexMesh succeeds
   - Verify checkMesh passes
4. Solve: `dotnet run -- solve -d .../airfoil-e2e-test`
   - Verify simpleFoam converges
5. Report: `dotnet run -- report -d .../airfoil-e2e-test`
   - Verify HTML + PDF + CSV generated
   - Verify coefficient data extracted (Cd, Cl, CmPitch)

### Part D: Documentation Updates

Per feedback rule — documentation must be updated alongside every code change:

1. **`Docs/Commands.md`** — update refinement-min/max defaults to say "Template-defined" instead of hardcoded values
2. **`study.example.jsonc`** — update refinement comments
3. **`README.md`** — verify quick start examples still accurate
4. **CLI help text** — updated automatically when NewStudyModel.cs changes

## Execution Order

1. Part A (template-driven defaults) — this is the architectural prerequisite
2. Part B (template files) — fix remaining divergences from tutorial
3. Build + unit tests locally
4. Part C (E2E on Linux) — prove it works end-to-end
5. Part D (docs)
6. Commit, push, merge

## Files Changed Summary

| File | Change |
|------|--------|
| `Models/NewStudyModel.cs` | `int` → `int?` for refinement, remove hardcoded defaults |
| `Models/StudyConfig.cs` | `int` → `int?` for refinement |
| `Handlers/NewStudyHandler.cs` | Add refinement to template defaults switch |
| `Services/CaseService.cs` | Handle nullable refinement |
| `Templates/external_airfoil_static_steady/system/surfaceFeatureExtractDict` | Match tutorial settings |
| `Templates/external_airfoil_static_steady/system/fvSchemes` | Add geometry block |
| `Templates/external_airfoil_static_steady/system/fvSolution` | Add cellDisplacement solver |
| `Templates/external_airfoil_static_steady/system/controlDict` | Verify matches tutorial |
| `Templates/external_airfoil_static_steady/TEMPLATE.json` | Verify meshPipeline (serial snappy?) |
| `Templates/external_disc_rotatingwall_steady/TEMPLATE.json` | Add explicit refinement defaults 5/6 |
| Tests (multiple) | Update for nullable refinement |
| `Docs/Commands.md` | Update refinement default text |
| `study.example.jsonc` | Update refinement comments |

## Key Decisions for Implementor

1. **Split patches vs single airFlow patch in blockMeshDict?** — Recommendation: keep split (cleaner). Already validated at refinement 2,2.
2. **Serial vs parallel snappyHexMesh for airfoil?** — Recommendation: serial. Tutorial runs serial. Avoids 2D parallel issues. Only decompose for the solver.
3. **Fallback refinement if neither CLI nor template specifies?** — Fallback to 0 (OpenFOAM's minimum allowed refinement level). This is the safest default — produces the coarsest mesh, runs fastest, and makes it obvious the user needs to configure refinement for their use case.
