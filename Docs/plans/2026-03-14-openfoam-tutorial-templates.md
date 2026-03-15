# Plan: OpenFOAM Tutorial Template Coverage

**Date:** 2026-03-14
**Status:** Draft — ready for execution in a new session
**Goal:** Create FoamScript templates that cover the breadth of OpenFOAM's tutorial cases, transforming FoamScript from a disc-golf-specific tool into a general-purpose OpenFOAM automation platform.

---

## Strategy

OpenFOAM v2512 ships ~130+ tutorial cases across 15 categories. We don't need a 1:1 template for every tutorial — we need templates organized by **use case pattern** that cover the physics combinations users actually need. Each template should be parameterized via Scriban so one template handles many variations.

### Template Naming Convention

```
{domain}_{geometry}_{motion}_{timeScheme}
```

Examples:
- `external_airfoil_static_steady` (simpleFoam, fixed airfoil)
- `external_body_rotating-wall_steady` (simpleFoam, rotatingWallVelocity BC)
- `internal_duct_static_steady` (simpleFoam, internal flow)
- `turbomachinery_propeller_rotating-ami_transient` (pimpleFoam, AMI sliding mesh)

---

## Phase 1: Core Incompressible Templates (Issues #1-#4)

These cover ~60% of typical CFD use cases and build directly on the existing disc template infrastructure.

### Template 1: `external_airfoil_static_steady`
**GitHub Issue:** #1
**OpenFOAM Reference:** `incompressible/simpleFoam/airFoil2D`
**Solver:** simpleFoam
**Turbulence:** kOmegaSST (RANS)
**Use Cases:** 2D airfoil analysis, wing section design, NACA profiles
**Key Differences from Disc Template:**
- 2D (single-cell spanwise with empty BC) vs 3D
- No rotation — pure external aero
- Airfoil geometry import (DAT/CSV coordinate files or STL)
- C-mesh or O-mesh topology (blockMeshDict changes significantly)
- forceCoeffs reference area = chord * span (not projected area)
**Parameterization:**
- AoA sweep (existing)
- Reynolds number / velocity
- Chord length
- Airfoil coordinates or STL
- Turbulence model selection (kOmegaSST, SpalartAllmaras, kEpsilon)
**Estimated Effort:** Medium — new blockMeshDict topology, airfoil coordinate handling, 2D empty BC pattern
**Template Files to Create:** ~35 (similar count to disc template, different blockMeshDict/snappyHexMeshDict)

### Template 2: `external_airfoil_static_transient`
**GitHub Issue:** #2
**OpenFOAM Reference:** `incompressible/pimpleFoam/LES/vortexShed`, `pimpleFoam/RAS/wingMotion`
**Solver:** pimpleFoam
**Turbulence:** kOmegaSST (RANS) or Smagorinsky (LES)
**Use Cases:** Vortex shedding, dynamic stall, flutter analysis, unsteady airfoil
**Key Differences from Template 1:**
- Transient: controlDict needs deltaT, endTime, adjustTimeStep, maxCo
- PIMPLE loop: nOuterCorrectors, nCorrectors, nNonOrthogonalCorrectors
- Time-varying output (writeInterval in seconds, not iterations)
- Force coefficients averaged over time window (already supported)
**Parameterization:**
- Everything from Template 1
- deltaT, endTime, maxCo
- PIMPLE corrector counts
- Write interval (time-based)
- LES vs RANS switch
**Estimated Effort:** Low-Medium — mostly controlDict/fvSchemes/fvSolution changes from Template 1
**Template Files to Create:** ~38 (adds time-stepping config)

### Template 3: `turbomachinery_propeller_rotating-mrf_steady`
**GitHub Issue:** #3
**OpenFOAM Reference:** `incompressible/simpleFoam/mixerVessel2D`, `simpleFoam/rotorDisk`
**Solver:** simpleFoam + MRF
**Turbulence:** kOmegaSST
**Use Cases:** Propeller/fan performance at fixed operating point, pump impeller
**Key Differences from Disc Template:**
- MRF zone (not rotatingWallVelocity BC) — needs MRFProperties dict
- Propeller geometry typically multi-blade with hub
- Thrust/torque coefficients (not lift/drag)
- forceCoeffs reference area = disc area (pi * r^2)
- May need cellZone selection for MRF region
**Parameterization:**
- RPM / advance ratio
- Freestream velocity
- Propeller diameter
- Number of blades (informational)
- MRF zone geometry (auto-generated cylinder)
**Estimated Effort:** Medium — MRF setup is the main new element, plus different post-processing
**Template Files to Create:** ~40

### Template 4: `turbomachinery_propeller_rotating-ami_transient`
**GitHub Issue:** #4
**OpenFOAM Reference:** `incompressible/pimpleFoam/RAS/propeller`, `pimpleFoam/RAS/axialTurbine_rotating_oneBlade`
**Solver:** pimpleFoam + AMI
**Turbulence:** kOmegaSST
**Use Cases:** Unsteady propeller loading, blade-passing effects, acoustic analysis
**Key Differences from Template 3:**
- AMI sliding mesh interface (dynamicMeshDict)
- Transient solver (PIMPLE)
- Time-accurate rotation: deltaT must resolve blade passing
- cellZone rotation via dynamicMeshDict, not MRF
- Higher computational cost — needs careful time step guidance
**Parameterization:**
- Everything from Template 3
- deltaT (auto-calculated from RPM and blade count for adequate resolution)
- endTime (number of revolutions)
- AMI interface tolerance
**Estimated Effort:** High — AMI setup is complex, we have prior experience from the disc AMI attempt
**Template Files to Create:** ~45

---

## Phase 2: Compressible Flow Templates (Issue #5 + new)

### Template 5: `external_airfoil_compressible_transonic`
**GitHub Issue:** #5
**OpenFOAM Reference:** `compressible/rhoSimpleFoam/aerofoilNACA0012`, `compressible/rhoCentralFoam/wedge15Ma5`
**Solver:** rhoSimpleFoam (steady) or rhoCentralFoam (transient/shock-capturing)
**Turbulence:** kOmegaSST
**Use Cases:** Transonic airfoil, shock-boundary layer interaction, high-speed aerodynamics
**Key Differences:**
- Compressible: needs thermophysicalProperties, energy equation
- Mach number as primary parameter (not just velocity)
- Temperature and pressure as freestream conditions
- Shock capturing schemes (vanLeer, Kurganov-Tadmor)
- Different fvSchemes (div schemes for compressible)
**Parameterization:**
- Mach number
- Freestream temperature, pressure
- AoA sweep
- Solver choice (rhoSimpleFoam vs rhoCentralFoam)
**Estimated Effort:** High — new physics (energy equation, thermophysical properties, compressible BCs)
**Template Files to Create:** ~45
**New Issue Required:** No (Issue #5 exists)

### Template 6: `external_body_compressible_supersonic`
**OpenFOAM Reference:** `compressible/rhoCentralFoam/obliqueShock`, `rhoCentralFoam/forwardStep`
**Solver:** rhoCentralFoam
**Use Cases:** Supersonic projectiles, re-entry vehicles, shock tube validation
**Key Differences from Template 5:**
- Central differencing schemes required for shock capturing
- No turbulence model needed for some cases (laminar supersonic)
- Potentially needs adaptive mesh refinement around shocks
**Estimated Effort:** Medium (builds on Template 5 infrastructure)

---

## Phase 3: Internal Flow & Heat Transfer Templates

### Template 7: `internal_duct_static_steady`
**OpenFOAM Reference:** `incompressible/simpleFoam/pitzDaily`, `simpleFoam/squareBend`
**Solver:** simpleFoam
**Use Cases:** Duct design, HVAC, manifold flow, backward-facing step validation
**Key Differences:**
- Internal flow: inlet/outlet BC pattern (not freestream)
- No geometry generation needed — user provides full domain STL
- Pressure drop as primary output metric
- Mass flow rate specification
**Parameterization:**
- Inlet velocity or mass flow rate
- Outlet pressure
- Turbulence intensity at inlet
**Estimated Effort:** Low — simplest template, great for validation

### Template 8: `internal_duct_static_steady_heated`
**OpenFOAM Reference:** `heatTransfer/buoyantSimpleFoam/hotRoom`, `buoyantSimpleFoam/circuitBoardCooling`
**Solver:** buoyantSimpleFoam
**Use Cases:** Electronics cooling, HVAC thermal analysis, heat exchanger design
**Key Differences:**
- Conjugate heat transfer or fixed-temperature walls
- Buoyancy effects (Boussinesq approximation)
- Temperature as additional field variable
- Nusselt number / heat transfer coefficient output
**Parameterization:**
- Wall temperatures or heat flux
- Inlet temperature
- Gravity vector
**Estimated Effort:** Medium — new solver, energy equation, thermal BCs

---

## Phase 4: Specialized Templates

### Template 9: `external_vehicle_static_steady`
**OpenFOAM Reference:** `incompressible/simpleFoam/motorBike`, `simpleFoam/simpleCar`
**Solver:** simpleFoam
**Use Cases:** Vehicle aerodynamics, drone bodies, external bluff body
**Key Differences from Disc Template:**
- No rotation
- Ground plane BC (moving wall)
- Much larger domain (car-scale vs disc-scale)
- Drag/downforce as primary metrics
- snappyHexMesh with refinement regions around body and wake
**Estimated Effort:** Medium — domain sizing and ground plane BC are the new elements

### Template 10: `multiphase_free-surface_transient`
**OpenFOAM Reference:** `multiphase/interFoam/laminar/damBreak`, `interIsoFoam/damBreak`
**Solver:** interFoam or interIsoFoam
**Use Cases:** Dam break, sloshing, wave interaction, hull hydrodynamics
**Key Differences:**
- VOF (Volume of Fluid) method — two-phase flow
- Needs alpha field (phase fraction)
- transportProperties defines two fluids
- Mesh refinement at free surface
**Estimated Effort:** High — fundamentally different physics model

### Template 11: `stressanalysis_solid_static`
**OpenFOAM Reference:** `stressAnalysis/solidDisplacementFoam/plateHole`
**Solver:** solidDisplacementFoam
**Use Cases:** Structural analysis, stress concentration, plate bending
**Key Differences:**
- Solid mechanics, not fluid dynamics
- Displacement field instead of velocity
- Mechanical properties (Young's modulus, Poisson's ratio)
- Stress/strain output
**Estimated Effort:** Medium — entirely different physics but simpler setup

---

## Implementation Order & Priority

| Priority | Template | Issue | Effort | Unlocks |
|----------|----------|-------|--------|---------|
| **1** | external_airfoil_static_steady | #1 | Medium | Airfoil community, NACA validation |
| **2** | internal_duct_static_steady | NEW | Low | Easiest template, validation benchmark |
| **3** | external_airfoil_static_transient | #2 | Low-Med | Unsteady aero, builds on #1 |
| **4** | turbomachinery_propeller_rotating-mrf_steady | #3 | Medium | Propeller/fan users |
| **5** | external_airfoil_compressible_transonic | #5 | High | Compressible flow, new solver |
| **6** | external_vehicle_static_steady | NEW | Medium | Vehicle aero, broadest appeal |
| **7** | internal_duct_static_steady_heated | NEW | Medium | Thermal analysis |
| **8** | turbomachinery_propeller_rotating-ami_transient | #4 | High | Unsteady turbomachinery |
| **9** | external_body_compressible_supersonic | NEW | Medium | High-speed aero |
| **10** | multiphase_free-surface_transient | NEW | High | Marine/hydraulic engineering |
| **11** | stressanalysis_solid_static | NEW | Medium | Structural analysis |

---

## Infrastructure Changes Required

### Before Template Work Begins

1. **Template discovery refactor** — current `list-templates` scans one directory. Need to support a template registry/catalog with metadata (solver, physics type, domain type, description).

2. **Geometry handling generalization** — current pipeline assumes disc (STEP→STL→rotor+tunnel). Templates need to declare their geometry requirements:
   - Disc template: STEP/STL → auto-generate rotor cylinder + tunnel box
   - Airfoil template: DAT/CSV coordinates → blockMesh C-grid OR STL → snappyHexMesh
   - Duct template: user-provided STL (no domain generation)
   - Vehicle template: STL → auto-generate tunnel box (no rotor)

3. **Post-processing generalization** — current forceCoeffs are hardcoded for disc (liftDir, dragDir, pitchAxis, reference area). Each template needs to declare:
   - Which coefficients to extract
   - Reference dimensions (chord, diameter, area)
   - Axis conventions
   - Report section customization

4. **Solver detection expansion** — current code handles simpleFoam/pimpleFoam. Need to add: rhoSimpleFoam, rhoCentralFoam, buoyantSimpleFoam, interFoam, solidDisplacementFoam.

5. **Template metadata file** — each template gets a `TEMPLATE.json` (or extend existing `TEMPLATE.md`) declaring:
   ```json
   {
     "name": "external_airfoil_static_steady",
     "solver": "simpleFoam",
     "physics": "incompressible",
     "domain": "external",
     "geometryType": "airfoil-coordinates",
     "requiredInputs": ["airfoilCoordinates", "chordLength", "angles", "velocity"],
     "postProcessing": {
       "forceCoeffs": { "liftDir": [0,0,1], "dragDir": [1,0,0], "referenceArea": "chord * span" }
     }
   }
   ```

### Per-Template Work Pattern

Each template follows this checklist:

1. **Study the OpenFOAM tutorial** — run the reference case, understand the physics, identify parameterizable values
2. **Create template directory** with Scriban-parameterized files (0/, constant/, system/)
3. **Write TEMPLATE.md** with metadata, parameter descriptions, validation notes
4. **Update geometry handling** if new geometry type needed
5. **Update post-processing** if new coefficient types needed
6. **Write tests** — template rendering tests, parameter validation tests
7. **E2E validation on Linux** — run the template end-to-end, compare against OpenFOAM tutorial results
8. **Update Commands.md and ExecutiveSummary.md**
9. **Create/close GitHub issue**

---

## Validation Strategy

Each template must be validated against its OpenFOAM tutorial reference case:

| Template | Validation Case | Key Metric | Acceptance |
|----------|----------------|------------|------------|
| Airfoil steady | NACA 0012 at Re=6M | Cl, Cd vs published data | Within 5% |
| Airfoil transient | Cylinder vortex shedding | Strouhal number | Within 3% |
| Propeller MRF | OpenFOAM propeller tutorial | Thrust coefficient | Within 5% |
| Compressible | NACA 0012 transonic | Shock position | Visual match |
| Duct | Pitz-Daily backward step | Reattachment length | Within 10% |
| Vehicle | motorBike tutorial | Cd | Within 5% |

---

## Estimated Total Effort

| Phase | Templates | Effort | Sessions (est.) |
|-------|-----------|--------|-----------------|
| Phase 1 | 4 (Issues #1-#4) | ~8-12 sessions | Weeks 1-3 |
| Phase 2 | 2 (compressible) | ~4-6 sessions | Weeks 4-5 |
| Phase 3 | 2 (internal + thermal) | ~3-4 sessions | Week 6 |
| Phase 4 | 3 (specialized) | ~6-8 sessions | Weeks 7-9 |
| Infrastructure | Refactoring | ~3-4 sessions | Before Phase 1 |
| **Total** | **11 templates** | **~24-34 sessions** | **~9-12 weeks** |

Infrastructure refactoring should happen first — it's a prerequisite for templates 2+ and will make each subsequent template faster to build.

---

## Session Execution Notes

- **Start each session by reading this plan and MEMORY.md**
- **One template per multi-session arc** — don't mix template work
- **Infrastructure changes first** — template registry, geometry generalization, post-processing generalization
- **Commit after each template is complete and validated** — don't batch
- **Update this plan** as templates are completed or priorities change
