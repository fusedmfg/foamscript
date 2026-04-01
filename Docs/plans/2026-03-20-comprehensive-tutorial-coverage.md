# Plan: Comprehensive 1:1 OpenFOAM Tutorial Coverage

**Date:** 2026-03-20
**Status:** Draft — pending review
**Supersedes:** `2026-03-14-openfoam-tutorial-templates.md` (11-template curated subset)
**Goal:** Create one foamscript template for every OpenFOAM v2512 tutorial case, validating and expanding foamscript's architecture to cover the full breadth of OpenFOAM's capabilities.

---

## Capability Tiers

Each tutorial is assigned a tier based on what foamscript infrastructure it requires:

| Tier | Description | Architecture Impact |
|------|-------------|-------------------|
| **T1** | Works with current pipeline: blockMesh → solver. No STL, no parallel. | None — ready today |
| **T2** | Needs snappyHexMesh + surfaceFeatureExtract, or parallel decomposition. | Supported today (disc/airfoil templates use this) |
| **T3** | Needs new mesh utilities (topoSet, createPatch, createBaffles, extrudeMesh, setFields, mapFields). | Small extensions — add pipeline steps |
| **T4** | Needs multi-stage workflows (precursor → main run, or multiple solver passes). | New: sequential stage orchestration |
| **T5** | Needs multi-region setup (splitMeshRegions, per-region BCs, conjugate coupling). | New: multi-region case structure |
| **T6** | Needs dynamic mesh (dynamicMeshDict, AMI sliding interfaces, overset/chimera). | New: dynamic mesh support |
| **T7** | Needs specialized physics not yet in foamscript (Lagrangian particles, combustion chemistry, VOF phase initialization, DSMC, molecular dynamics). | New: physics-specific infrastructure |
| **T8** | Needs optimization loops (adjoint solver, shape morphing, design variables). | New: iterative optimization workflow |

**Dependency chain:** T1 → T2 → T3 → T4/T5/T6/T7/T8 (T4+ can be tackled in parallel once T3 is solid)

---

## Complete Tutorial Inventory (601 cases)

### 1. INCOMPRESSIBLE (143 cases)

#### 1.1 icoFoam — Laminar Incompressible (5 cases)
*Entry-level tutorials. Laminar flow, no turbulence model. Teaches basic OpenFOAM case structure.*

| # | Tutorial Path | 2D/3D | Mesh | Tier | Teaches |
|---|--------------|-------|------|------|---------|
| 1 | icoFoam/cavity/cavity | 2D | blockMesh | T1 | Lid-driven cavity — the "hello world" of CFD |
| 2 | icoFoam/cavity/cavityGrade | 2D | blockMesh+mapFields | T3 | Graded mesh, field mapping between meshes |
| 3 | icoFoam/cavity/cavityClipped | 2D | blockMesh+mapFields | T3 | Clipped geometry variant, field mapping |
| 4 | icoFoam/cavityMappingTest | 2D | blockMesh | T1 | Field mapping validation |
| 5 | icoFoam/elbow | 2D | pre-meshed (Fluent import) | T3 | External mesh import (fluentMeshToFoam) |

#### 1.2 simpleFoam — Steady RANS (20 cases)
*Core steady-state turbulent flow. This is foamscript's current strength.*

| # | Tutorial Path | 2D/3D | Mesh | Tier | Teaches |
|---|--------------|-------|------|------|---------|
| 6 | simpleFoam/pitzDaily | 2D | blockMesh | T1 | Backward-facing step, kEpsilon, internal flow |
| 7 | simpleFoam/pitzDaily_fused | 2D | blockMesh+decomposePar | T2 | Parallel variant of pitzDaily |
| 8 | simpleFoam/pitzDailyExptInlet | 2D | blockMesh | T1 | Experimental inlet profile (timeVaryingMappedFixed) |
| 9 | simpleFoam/backwardFacingStep2D | 2D | blockMesh | T1 | kOmegaSST variant of backward step |
| 10 | simpleFoam/airFoil2D | 2D | blockMesh+decomposePar | T2 | **DONE** — 2D airfoil external aero (template #1) |
| 11 | simpleFoam/T3A | 2D | blockMesh | T1 | Flat plate transition (kOmegaSSTLM) |
| 12 | simpleFoam/bump2D (multi-setup) | 2D | blockMesh | T1 | 2D bump channel, turbulence model comparison |
| 13 | simpleFoam/turbulentFlatPlate (multi-setup) | 2D | blockMesh | T1 | Turbulent flat plate BL validation |
| 14 | simpleFoam/simpleCar | 2D | blockMesh+createPatch+topoSet | T3 | Simplified car external aero |
| 15 | simpleFoam/rotatingCylinders | 2D | blockMesh | T1 | Rotating cylinders, laminar |
| 16 | simpleFoam/mixerVessel2D | 2D | blockMesh | T6 | 2D mixer with AMI/baffles |
| 17 | simpleFoam/motorBike | 3D | blockMesh+snappy+surfaceFeatureExtract | T2 | Flagship: complex 3D geometry, parallel |
| 18 | simpleFoam/pipeCyclic | 3D | blockMesh+topoSet+decomposePar | T3 | Cyclic pipe flow |
| 19 | simpleFoam/rotorDisk | 3D | blockMesh+snappy+surfaceFeatureExtract | T2 | Actuator disk model |
| 20 | simpleFoam/squareBend | 3D | blockMesh | T1 | 3D square bend duct |
| 21 | simpleFoam/turbineSiting | 3D | blockMesh+snappy+topoSet+decomposePar | T3 | Wind turbine siting over terrain |
| 22 | simpleFoam/windAroundBuildings | 3D | blockMesh | T1 | Wind around buildings, ABL inlet |
| 23 | lumpedPointMotion/bridge/steady | 3D | pre-meshed | T6 | FSI bridge (lumped-point motion) |
| 24 | lumpedPointMotion/building/steady | 3D | pre-meshed | T6 | FSI building (lumped-point motion) |
| 25 | pimpleFoam/RAS/wingMotion/wingMotion2D_simpleFoam | 3D | snappy+extrudeMesh+createPatch | T3 | Wing steady precursor for dynamic mesh |

#### 1.3 pimpleFoam — Transient (30 cases)

**Laminar (11):**

| # | Tutorial Path | 2D/3D | Mesh | Tier | Teaches |
|---|--------------|-------|------|------|---------|
| 26 | pimpleFoam/laminar/contactAngleCavity | 2D | blockMesh | T1 | Contact angle driven flow |
| 27 | pimpleFoam/laminar/contaminatedDroplet2D | 2D | blockMesh | T1 | Surface tension effects |
| 28 | pimpleFoam/laminar/cylinder2D | 2D | pre-meshed+decomposePar | T2 | Vortex shedding (classic) |
| 29 | pimpleFoam/laminar/movingCone | 2D | blockMesh | T6 | Dynamic mesh demo |
| 30 | pimpleFoam/laminar/planarContraction | 2D | blockMesh | T1 | Channel contraction |
| 31 | pimpleFoam/laminar/planarPoiseuille (multi) | 2D | blockMesh | T1 | Poiseuille flow validation |
| 32 | pimpleFoam/laminar/sloshing2D | 2D | blockMesh | T1 | Free surface sloshing |
| 33 | pimpleFoam/laminar/mixerVesselAMI2D | 2D | blockMesh+decomposePar | T6 | AMI rotation, 2D mixer |
| 34 | pimpleFoam/laminar/mixerVesselAMI2D-topologyChange | 3D | blockMesh+decomposePar | T6 | AMI with topology change |
| 35 | pimpleFoam/laminar/filmPanel0 | 3D | blockMesh | T7 | Thin film flow |
| 36 | pimpleFoam/laminar/inclinedPlaneFilm | 3D | blockMesh | T7 | Film flow on inclined plane |

**LES (10):**

| # | Tutorial Path | 2D/3D | Mesh | Tier | Teaches |
|---|--------------|-------|------|------|---------|
| 37 | pimpleFoam/LES/vortexShed | 2D | blockMesh+decomposePar | T2 | Vortex shedding DDES |
| 38 | pimpleFoam/LES/surfaceMountedCube/initChannel | 2D | blockMesh | T1 | Channel precursor for LES |
| 39 | pimpleFoam/LES/decayIsoTurb | 3D | blockMesh+decomposePar | T2 | Decaying isotropic turbulence |
| 40 | pimpleFoam/LES/periodicHill/steadyState | 3D | blockMesh+topoSet+decomposePar | T4 | Periodic hill RANS precursor |
| 41 | pimpleFoam/LES/periodicHill/transient | 3D | pre-meshed+runParallel | T4 | Periodic hill LES (needs precursor #40) |
| 42 | pimpleFoam/LES/periodicPlaneChannel | 3D | blockMesh+decomposePar+renumberMesh | T3 | Plane channel LES (WALE) |
| 43 | pimpleFoam/LES/planeChannel (multi) | 3D | blockMesh | T1 | Plane channel LES (Smagorinsky) |
| 44 | pimpleFoam/LES/surfaceMountedCube/fullCase | 3D | blockMesh+decomposePar | T4 | Surface-mounted cube LES (needs #38) |
| 45 | pimpleFoam/LES/NACA4412 | 3D | pre-meshed | T2 | NACA 4412 airfoil LES |
| 46 | pimpleFoam/LES/wallMountedHump (multi) | 3D | blockMesh+renumberMesh | T3 | Wall-mounted hump separation |

**RAS (13):**

| # | Tutorial Path | 2D/3D | Mesh | Tier | Teaches |
|---|--------------|-------|------|------|---------|
| 47 | pimpleFoam/RAS/pitzDaily | 2D | blockMesh | T1 | Transient backward step |
| 48 | pimpleFoam/RAS/ellipsekkLOmega | 2D | pre-meshed | T1 | kkLOmega transition model |
| 49 | pimpleFoam/RAS/oscillatingInletACMI2D | 3D | blockMesh+decomposePar | T6 | ACMI interface |
| 50 | pimpleFoam/RAS/oscillatingInletPeriodicAMI2D | 3D | blockMesh | T6 | Periodic AMI |
| 51 | pimpleFoam/RAS/TJunction | 3D | blockMesh | T1 | T-junction pipe flow |
| 52 | pimpleFoam/RAS/TJunctionFan | 3D | blockMesh+topoSet+createBaffles | T3 | T-junction with fan baffle |
| 53 | pimpleFoam/RAS/TJunctionSwitching | 3D | blockMesh | T1 | T-junction switching BC |
| 54 | pimpleFoam/RAS/TJunctionArrheniusBirdCarreauTransport | 3D | blockMesh | T1 | Non-Newtonian T-junction |
| 55 | pimpleFoam/RAS/propeller | 3D | snappy+decomposePar | T6 | Propeller AMI rotation |
| 56 | pimpleFoam/RAS/propeller1 | 3D | snappy+decomposePar | T6 | Propeller variant |
| 57 | pimpleFoam/RAS/rotatingFanInRoom | 3D | snappy+decomposePar | T6 | Fan in room (MRF/AMI) |
| 58 | pimpleFoam/RAS/axialTurbine_rotating_oneBlade | 3D | pre-meshed | T6 | Single-blade axial turbine |
| 59 | pimpleFoam/RAS/wingMotion/wingMotion2D_pimpleFoam | 3D | snappy+extrudeMesh+createPatch+mapFields | T6 | Wing pitching (6DoF) |

#### 1.4 pisoFoam (6 cases)

| # | Tutorial Path | 2D/3D | Mesh | Tier | Teaches |
|---|--------------|-------|------|------|---------|
| 60 | pisoFoam/laminar/porousBlockage | 2D | blockMesh+topoSet | T3 | Porous blockage |
| 61 | pisoFoam/RAS/cavity | 2D | blockMesh+topoSet | T3 | Turbulent cavity |
| 62 | pisoFoam/RAS/cavityCoupledU | 2D | blockMesh | T1 | Coupled velocity cavity |
| 63 | pisoFoam/LES/pitzDaily | 2D | blockMesh | T1 | Pitz-Daily LES |
| 64 | pisoFoam/LES/pitzDailyMapped | 2D | blockMesh | T1 | Pitz-Daily LES mapped inlet |
| 65 | pisoFoam/LES/motorBike | 3D | blockMesh+snappy+decomposePar+potentialFoam | T4 | Motorbike LES (multi-stage) |

#### 1.5 overPimpleDyMFoam — Overset Transient (5 cases)

| # | Tutorial Path | 2D/3D | Mesh | Tier | Teaches |
|---|--------------|-------|------|------|---------|
| 66 | overPimpleDyMFoam/cylinder/cylinderAndBackground | 2D | blockMesh+snappy | T6 | Overset cylinder in crossflow |
| 67 | overPimpleDyMFoam/cylinder/cylinderMesh | 3D | snappy | T6 | Cylinder mesh for overset |
| 68 | overPimpleDyMFoam/rotatingSquare | 2D | blockMesh | T6 | Rotating square overset |
| 69 | overPimpleDyMFoam/simpleRotor | 2D | blockMesh+decomposePar | T6 | Rotor overset |
| 70 | overPimpleDyMFoam/twoSimpleRotors | 2D | blockMesh+decomposePar | T6 | Two rotors overset |

#### 1.6 overSimpleFoam — Overset Steady (4 cases)

| # | Tutorial Path | 2D/3D | Mesh | Tier | Teaches |
|---|--------------|-------|------|------|---------|
| 71 | overSimpleFoam/aeroFoil/aeroFoil_overset | 3D | pre-meshed | T6 | Airfoil overset component |
| 72 | overSimpleFoam/aeroFoil/background_overset | 3D | decomposePar | T6 | Background for overset airfoil |
| 73 | overSimpleFoam/aeroFoil/aeroFoil_snappyHexMesh | 3D | snappy | T6 | Airfoil mesh generation |
| 74 | overSimpleFoam/aeroFoil/background_snappyHexMesh | 3D | snappy | T6 | Background mesh generation |

#### 1.7 porousSimpleFoam (3 cases)

| # | Tutorial Path | 2D/3D | Mesh | Tier | Teaches |
|---|--------------|-------|------|------|---------|
| 75 | porousSimpleFoam/angledDuct/explicit | 3D | blockMesh | T1 | Explicit porosity |
| 76 | porousSimpleFoam/angledDuct/implicit | 3D | blockMesh | T1 | Implicit porosity |
| 77 | porousSimpleFoam/straightDuctImplicit | 3D | pre-meshed | T1 | Straight duct porosity |

#### 1.8 SRF Solvers (2 cases)

| # | Tutorial Path | 2D/3D | Mesh | Tier | Teaches |
|---|--------------|-------|------|------|---------|
| 78 | SRFPimpleFoam/rotor2D | 3D | blockMesh | T1 | Single rotating frame rotor |
| 79 | SRFSimpleFoam/mixer | 3D | blockMesh | T1 | SRF mixer |

#### 1.9 Other Incompressible (3 cases)

| # | Tutorial Path | 2D/3D | Mesh | Tier | Teaches |
|---|--------------|-------|------|------|---------|
| 80 | nonNewtonianIcoFoam/offsetCylinder | 2D | blockMesh | T1 | Non-Newtonian flow |
| 81 | shallowWaterFoam/squareBump | 2D | blockMesh+setFields | T3 | Shallow water equations |
| 82 | adjointShapeOptimizationFoam/pitzDaily | 2D | blockMesh | T8 | Adjoint shape optimization |

#### 1.10 adjointOptimisationFoam (66 cases)

**Sensitivity Maps (10):**

| # | Tutorial Path | 2D/3D | Mesh | Tier | Teaches |
|---|--------------|-------|------|------|---------|
| 83 | sensitivityMaps/motorBike | 3D | snappy+surfaceFeatureExtract | T8 | Surface sensitivity on complex geometry |
| 84 | sensitivityMaps/naca0012/laminar/drag | 2D | pre-meshed | T8 | Drag sensitivity (laminar) |
| 85 | sensitivityMaps/naca0012/laminar/lift | 2D | pre-meshed | T8 | Lift sensitivity (laminar) |
| 86 | sensitivityMaps/naca0012/laminar/moment | 2D | pre-meshed | T8 | Moment sensitivity (laminar) |
| 87 | sensitivityMaps/naca0012/turbulent/liftFullSetup | 2D | pre-meshed | T8 | Lift sensitivity (SA, full setup) |
| 88 | sensitivityMaps/naca0012/turbulent/liftMinimumSetup | 2D | pre-meshed | T8 | Lift sensitivity (SA, minimum) |
| 89 | sensitivityMaps/sbend/laminar | 2D | pre-meshed | T8 | S-bend sensitivity (laminar) |
| 90 | sensitivityMaps/sbend/turbulent/highRe | 2D | pre-meshed | T8 | S-bend sensitivity (high Re) |
| 91 | sensitivityMaps/sbend/turbulent/lowRe/multiPoint | 2D | pre-meshed | T8 | Multi-point sensitivity |
| 92 | sensitivityMaps/sbend/turbulent/lowRe/singlePoint | 2D | pre-meshed | T8 | Single-point sensitivity |

**Shape Optimization (25):**

| # | Tutorial Path | 2D/3D | Mesh | Tier | Teaches |
|---|--------------|-------|------|------|---------|
| 93-97 | shapeOptimisation/fork-uneven/* (3) + naca0012/kOmegaSST + naca0012/laminar/drag | 2D | blockMesh/pre-meshed | T8 | Fork optimization, NACA drag opt |
| 98-107 | shapeOptimisation/naca0012/laminar/* (4) + sbend/laminar/* (6) | 2D | pre-meshed | T8 | Airfoil + S-bend shape optimization (BFGS, SD, SQP) |
| 108-117 | shapeOptimisation/sbend/turbulent/* (5) + motorBike + remaining | 2D/3D | pre-meshed/snappy | T8 | Turbulent optimization variants |

**Topology Optimization (31):**

| # | Tutorial Path | 2D/3D | Mesh | Tier | Teaches |
|---|--------------|-------|------|------|---------|
| 118-148 | topologyOptimisation/* (31 cases) | 2D/3D | blockMesh+topoSet+decomposePar | T8 | Porosity/levelSet topology optimization |

---

### 2. COMPRESSIBLE (40 cases)

#### 2.1 rhoCentralFoam — Density-Based Compressible (7 cases)

| # | Tutorial Path | 2D/3D | Mesh | Tier | Teaches |
|---|--------------|-------|------|------|---------|
| 149 | rhoCentralFoam/obliqueShock | 2D | blockMesh | T1 | Oblique shock wave validation |
| 150 | rhoCentralFoam/wedge15Ma5 | 2D | blockMesh | T1 | Mach 5 wedge, axisymmetric |
| 151 | rhoCentralFoam/shockTube | 2D | blockMesh+setFields | T3 | Sod shock tube (Riemann problem) |
| 152 | rhoCentralFoam/forwardStep | 2D | blockMesh | T1 | Mach 3 forward step |
| 153 | rhoCentralFoam/movingCone | 2D | blockMesh | T6 | Supersonic + dynamic mesh |
| 154 | rhoCentralFoam/LadenburgJet60psi | 3D | blockMesh | T1 | Underexpanded jet |
| 155 | rhoCentralFoam/biconic25-55Run35 | 3D | blockMesh+datToFoam | T3 | Hypersonic biconic (external mesh) |

#### 2.2 rhoSimpleFoam — Steady Compressible RANS (6 cases)

| # | Tutorial Path | 2D/3D | Mesh | Tier | Teaches |
|---|--------------|-------|------|------|---------|
| 156 | rhoSimpleFoam/aerofoilNACA0012 | 3D | blockMesh | T1 | Compressible NACA0012 |
| 157 | rhoSimpleFoam/angledDuctExplicitFixedCoeff | 3D | blockMesh | T1 | Compressible duct with porosity |
| 158 | rhoSimpleFoam/squareBend | 3D | blockMesh | T1 | Compressible square bend |
| 159 | rhoSimpleFoam/squareBendLiq | 3D | blockMesh | T1 | Compressible liquid bend |
| 160 | rhoSimpleFoam/squareBendLiqNoNewtonian | 3D | blockMesh | T1 | Non-Newtonian compressible |
| 161 | rhoSimpleFoam/gasMixing/injectorPipe | 3D | snappy | T2 | Gas species mixing |

#### 2.3 rhoPimpleFoam — Transient Compressible (14 cases)

| # | Tutorial Path | 2D/3D | Mesh | Tier | Teaches |
|---|--------------|-------|------|------|---------|
| 162 | rhoPimpleFoam/laminar/sineWaveDamping | 2D | blockMesh | T1 | Acoustic wave damping |
| 163 | rhoPimpleFoam/laminar/helmholtzResonance | 3D | blockMesh | T1 | Helmholtz resonator |
| 164 | rhoPimpleFoam/LES/pitzDaily | 2D | blockMesh | T1 | Compressible LES backward step |
| 165 | rhoPimpleFoam/RAS/cavity | 2D | blockMesh | T1 | Compressible cavity |
| 166 | rhoPimpleFoam/RAS/aerofoilNACA0012 | 3D | blockMesh+extrudeMesh | T3 | Transient compressible airfoil |
| 167 | rhoPimpleFoam/RAS/angledDuct | 3D | blockMesh | T1 | Compressible angled duct |
| 168 | rhoPimpleFoam/RAS/angledDuctLTS | 3D | blockMesh | T1 | Angled duct with LTS |
| 169 | rhoPimpleFoam/RAS/annularThermalMixer | 3D | snappy+createBaffles | T3 | Thermal mixer with baffles |
| 170 | rhoPimpleFoam/RAS/externalCoupledSquareBendLiq | 3D | blockMesh | T4 | External coupling |
| 171 | rhoPimpleFoam/RAS/mixerVessel2D | 2D | blockMesh (m4) | T6 | Compressible mixer AMI |
| 172 | rhoPimpleFoam/RAS/squareBendLiq | 3D | blockMesh | T1 | Compressible liquid bend |
| 173 | rhoPimpleFoam/RAS/TJunction | 3D | blockMesh | T1 | Compressible T-junction |
| 174 | rhoPimpleFoam/RAS/TJunctionAverage | 3D | blockMesh | T1 | T-junction with averaging |

#### 2.4 Other Compressible (13 cases)

| # | Tutorial Path | 2D/3D | Mesh | Tier | Teaches |
|---|--------------|-------|------|------|---------|
| 175 | sonicFoam/laminar/forwardStep | 2D | blockMesh | T1 | Sonic forward step |
| 176 | sonicFoam/laminar/shockTube | 2D | blockMesh+setFields | T3 | Sonic shock tube |
| 177 | sonicFoam/RAS/nacaAirfoil | 2D | star4ToFoam | T3 | Transonic airfoil (imported mesh) |
| 178 | sonicFoam/RAS/prism | 2D | blockMesh | T1 | Supersonic prism |
| 179 | sonicDyMFoam/movingCone | 2D | blockMesh | T6 | Sonic moving cone |
| 180 | sonicLiquidFoam/decompressionTank | 2D | blockMesh | T1 | Pressure vessel decompression |
| 181 | rhoPorousSimpleFoam/angledDuct/explicit | 3D | blockMesh | T1 | Compressible porous duct |
| 182 | rhoPorousSimpleFoam/angledDuct/implicit | 3D | blockMesh | T1 | Compressible porous duct |
| 183 | rhoPimpleAdiabaticFoam/rutlandVortex2D | 2D | blockMesh+decomposePar | T2 | Adiabatic vortex shedding |
| 184 | overRhoPimpleDyMFoam/twoSimpleRotors | 3D | blockMesh | T6 | Overset compressible rotors |
| 185 | overRhoSimpleFoam/hotCylinder | 3D | blockMesh+snappy | T6 | Overset heated cylinder |
| 186-187 | acousticFoam/obliqueAirJet (precursor+main) | 3D | snappy/blockMesh | T4 | Aeroacoustics (multi-stage) |

---

### 3. HEAT TRANSFER (32 cases)

#### 3.1 Boussinesq (incompressible buoyancy) (4 cases)

| # | Tutorial Path | 2D/3D | Mesh | Tier | Teaches |
|---|--------------|-------|------|------|---------|
| 188 | buoyantBoussinesqSimpleFoam/hotRoom | 3D | blockMesh+setFields | T3 | Steady natural convection |
| 189 | buoyantBoussinesqSimpleFoam/iglooWithFridges | 3D | blockMesh+snappy | T2 | Complex geometry buoyancy |
| 190 | buoyantBoussinesqPimpleFoam/BenardCells | 2D | blockMesh | T1 | Rayleigh-Benard convection |
| 191 | buoyantBoussinesqPimpleFoam/hotRoom | 3D | blockMesh+setFields | T3 | Transient buoyancy |

#### 3.2 Compressible Buoyancy (11 cases)

| # | Tutorial Path | 2D/3D | Mesh | Tier | Teaches |
|---|--------------|-------|------|------|---------|
| 192 | buoyantSimpleFoam/buoyantCavity | 3D | blockMesh | T1 | Differentially heated cavity |
| 193 | buoyantSimpleFoam/circuitBoardCooling | 2D | blockMesh+createBaffles | T3 | Electronics cooling |
| 194 | buoyantSimpleFoam/comfortHotRoom | 3D | blockMesh+topoSet+createPatch | T3 | Thermal comfort |
| 195 | buoyantSimpleFoam/hotRadiationRoom | 3D | blockMesh | T1 | Radiation (P1 model) |
| 196 | buoyantSimpleFoam/hotRadiationRoomFvDOM | 3D | blockMesh | T1 | Radiation (fvDOM) |
| 197 | buoyantSimpleFoam/roomWithThickCeiling | 2D | blockMesh+changeDictionary | T3 | Mapped BC conjugate-like |
| 198 | buoyantSimpleFoam/simpleCarSolarPanel | 3D | blockMesh+snappy+surfaceFeatureExtract | T2 | Solar heating on car |
| 199 | buoyantPimpleFoam/hotRoom | 3D | blockMesh+setFields | T3 | Transient compressible buoyancy |
| 200 | buoyantPimpleFoam/hotRoomWithThermalShell | 3D | blockMesh+makeFaMesh | T7 | Finite-area thermal shell |
| 201 | buoyantPimpleFoam/hotRoomWithThermalShell.multi-area | 3D | blockMesh+makeFaMesh | T7 | Multi-area thermal shell |
| 202 | buoyantPimpleFoam/thermocoupleTestCase | 3D | blockMesh | T1 | Thermocouple response |

#### 3.3 Conjugate Heat Transfer — Multi-Region (17 cases)

| # | Tutorial Path | 2D/3D | Mesh | Tier | Teaches |
|---|--------------|-------|------|------|---------|
| 203 | chtMultiRegionFoam/multiRegionHeater | 3D | blockMesh+splitMeshRegions | T5 | Classic CHT |
| 204 | chtMultiRegionFoam/snappyMultiRegionHeater | 3D | snappy+splitMeshRegions | T5 | CHT with snappy mesh |
| 205 | chtMultiRegionFoam/snappyMultiRegionHeaterImplicit | 3D | snappy+splitMeshRegions | T5 | Implicit CHT coupling |
| 206 | chtMultiRegionFoam/reverseBurner | 2D | blockMesh+splitMeshRegions+topoSet | T5 | 2D reverse burner CHT |
| 207 | chtMultiRegionFoam/externalCoupledHeater | 3D | blockMesh | T5 | External coupling CHT |
| 208 | chtMultiRegionFoam/externalSolarLoad | 3D | blockMesh+viewFactorsGen | T5 | Solar load with view factors |
| 209 | chtMultiRegionFoam/solarBeamWithTrees | 3D | blockMesh | T5 | Solar beam radiation |
| 210 | chtMultiRegionFoam/windshieldCondensation | 3D | blockMesh+topoSet+subsetMesh+splitMeshRegions | T5 | Windshield condensation |
| 211 | chtMultiRegionFoam/windshieldDefrost | 3D | blockMesh | T5 | Windshield defrost |
| 212 | chtMultiRegionSimpleFoam/cpuCabinet | 3D | snappy+splitMeshRegions | T5 | CPU cooling CHT |
| 213 | chtMultiRegionSimpleFoam/externalCoupledHeater | 3D | blockMesh | T5 | Steady external coupling |
| 214 | chtMultiRegionSimpleFoam/heatExchanger | 3D | other | T5 | Heat exchanger CHT |
| 215 | chtMultiRegionSimpleFoam/jouleHeatingSolid | 3D | other | T5 | Joule heating |
| 216 | chtMultiRegionSimpleFoam/multiRegionHeaterRadiation | 3D | blockMesh+viewFactorsGen | T5 | CHT + radiation |
| 217 | chtMultiRegionTwoPhaseEulerFoam/solidQuenching2D | 2D | blockMesh | T5+T7 | Two-phase quenching CHT |
| 218 | overBuoyantPimpleDyMFoam/movingBox | 2D | blockMesh | T6 | Overset buoyant |
| 219 | solidFoam/movingCone | 2D | blockMesh | T6 | Solid heat conduction |

---

### 4. STRESS ANALYSIS (2 cases)

| # | Tutorial Path | 2D/3D | Mesh | Tier | Teaches |
|---|--------------|-------|------|------|---------|
| 220 | solidDisplacementFoam/plateHole | 2D | blockMesh | T1 | Stress concentration (classic) |
| 221 | solidEquilibriumDisplacementFoam/beamEndLoad | 3D | blockMesh | T1 | Cantilever beam |

---

### 5. MULTIPHASE (131 cases)

#### 5.1 interFoam — VOF Two-Phase (50+ cases)

**Laminar:**

| # | Tutorial Path | 2D/3D | Mesh | Tier | Teaches |
|---|--------------|-------|------|------|---------|
| 222 | interFoam/laminar/damBreak/damBreak | 2D | blockMesh+setFields | T3 | Classic dam break |
| 223 | interFoam/laminar/damBreakPermeable | 2D | blockMesh+setFields | T3 | Dam break with porous baffle |
| 224 | interFoam/laminar/damBreakWithObstacle | 3D | blockMesh+setFields | T3 | 3D dam break |
| 225 | interFoam/laminar/capillaryRise | 2D | blockMesh | T7 | Capillary rise |
| 226 | interFoam/laminar/mixerVessel2D | 2D | blockMesh | T6+T7 | Two-phase mixer |
| 227 | interFoam/laminar/oscillatingBox | 3D | blockMesh | T7 | Free surface oscillation |
| 228 | interFoam/laminar/sloshingCylinder | 3D | blockMesh | T7 | Cylindrical sloshing |
| 229-234 | interFoam/laminar/sloshingTank* (6 variants) | 2D/3D | blockMesh | T7 | Sloshing (2D/3D, 3DoF/6DoF) |
| 235 | interFoam/laminar/testTubeMixer | 3D | blockMesh | T7 | Test tube rotation |
| 236-238 | interFoam/laminar/vofToLagrangian/* (3) | 3D | blockMesh | T7 | VOF→Lagrangian conversion |
| 239-252 | interFoam/laminar/waves/* (14 cases) | 2D/3D | blockMesh | T7 | Wave generation/propagation |

**RAS:**

| # | Tutorial Path | 2D/3D | Mesh | Tier | Teaches |
|---|--------------|-------|------|------|---------|
| 253 | interFoam/RAS/damBreak | 2D | blockMesh+setFields | T3+T7 | Turbulent dam break |
| 254 | interFoam/RAS/angledDuct | 3D | blockMesh | T7 | Two-phase duct |
| 255 | interFoam/RAS/DTCHull | 3D | snappy | T2+T7 | Ship hull resistance |
| 256 | interFoam/RAS/DTCHullMoving | 3D | snappy | T6+T7 | Ship hull with motion |
| 257 | interFoam/RAS/floatingObject | 3D | snappy | T6+T7 | Floating rigid body |
| 258 | interFoam/RAS/mixerVesselAMI | 3D | pre-meshed | T6+T7 | Two-phase mixer AMI |
| 259-264 | interFoam/RAS/* (remaining 6) | 2D/3D | various | T3-T7 | Various two-phase RAS |

**LES:**

| # | Tutorial Path | 2D/3D | Mesh | Tier | Teaches |
|---|--------------|-------|------|------|---------|
| 265 | interFoam/LES/nozzleFlow2D | 2D | blockMesh | T7 | Nozzle free surface LES |

#### 5.2 interIsoFoam (13 cases)

| # | Tutorial Path | 2D/3D | Mesh | Tier | Teaches |
|---|--------------|-------|------|------|---------|
| 266-278 | interIsoFoam/* (13 cases) | 2D/3D | blockMesh | T7 | isoAdvector: dam break, disc/sphere in vortex, sloshing, waves |

#### 5.3 Compressible Multiphase (15 cases)

| # | Tutorial Path | 2D/3D | Mesh | Tier | Teaches |
|---|--------------|-------|------|------|---------|
| 279-293 | compressibleInter*/compressibleMultiphase* (15) | 2D/3D | blockMesh | T7 | Compressible VOF, depth charges, climbing rod |

#### 5.4 Euler-Euler Multiphase (29 cases)

| # | Tutorial Path | 2D/3D | Mesh | Tier | Teaches |
|---|--------------|-------|------|------|---------|
| 294-322 | multiphaseEulerFoam/reactingMultiphaseEulerFoam/reactingTwoPhaseEulerFoam/twoPhaseEulerFoam (29) | 2D/3D | blockMesh | T7 | Euler-Euler: bubble columns, fluidised beds, boiling |

#### 5.5 Other Multiphase (24 cases)

| # | Tutorial Path | 2D/3D | Mesh | Tier | Teaches |
|---|--------------|-------|------|------|---------|
| 323-346 | driftFlux/interMixing/interPhaseChange/MPPIC/cavitating/overInter/potentialFreeSurface/twoLiquidMixing (24) | 2D/3D | various | T6-T7 | Drift flux, cavitation, overset multiphase, lock exchange |
| 347-352 | icoReactingMultiphaseInterFoam (7 cases) | 2D/3D | blockMesh | T7 | Reacting multiphase: evaporation, oxide formation, melting |

---

### 6. COMBUSTION (24 cases)

| # | Tutorial Path | 2D/3D | Mesh | Tier | Teaches |
|---|--------------|-------|------|------|---------|
| 353-357 | chemFoam/* (5 cases) | 0D | none | T7 | Chemistry validation (GRI, H2, ic8h18, nc7h16) |
| 358 | coldEngineFoam/freePiston | 3D | dynamic | T6+T7 | Cold engine flow |
| 359-364 | fireFoam/* (6 cases) | 2D/3D | blockMesh | T7 | Fire simulation (LES) |
| 365-366 | PDRFoam/* (2 cases) | 3D | blockMesh+PDRsetFields | T7 | Deflagration (PDR) |
| 367-374 | reactingFoam/* (8 cases) | 2D/3D | blockMesh | T7 | Reacting flow (laminar+RAS) |
| 375 | rhoReactingFoam/groundAbsorption | 3D | snappy | T7 | Acoustic + reacting |
| 376-378 | XiDyMFoam/XiEngineFoam/XiFoam (3 cases) | 2D/3D | dynamic/blockMesh | T6+T7 | Premixed combustion |

---

### 7. LAGRANGIAN (33 cases)

| # | Tutorial Path | 2D/3D | Mesh | Tier | Teaches |
|---|--------------|-------|------|------|---------|
| 379-411 | All lagrangian cases (33) | 2D/3D | various | T7 | Particle tracking: sprays, coal dust, reacting parcels, fluidised beds, cyclones |

---

### 8. BASIC (22 cases)

| # | Tutorial Path | 2D/3D | Mesh | Tier | Teaches |
|---|--------------|-------|------|------|---------|
| 412-415 | laplacianFoam/* (4+multiWorld) | 2D/3D | blockMesh | T1 | Heat conduction |
| 416-417 | potentialFoam/* (2 cases) | 2D | blockMesh | T1 | Potential flow |
| 418-419 | scalarTransportFoam/* (2 cases) | 2D | blockMesh | T1 | Scalar transport |
| 420-421 | overLaplacianDyMFoam/* (2 cases) | 2D | blockMesh | T6 | Overset heat transfer |
| 422-425 | overPotentialFoam/* (2+mesh cases) | 3D | blockMesh+snappy | T6 | Overset potential flow |
| 426-433 | chtMultiRegionFoam/2DImplicitCyclic + multiWorld variants | 2D/3D | various | T5 | Multi-region basics |

---

### 9. MESH (42 cases)

| # | Tutorial Path | 2D/3D | Mesh | Tier | Teaches |
|---|--------------|-------|------|------|---------|
| 434-440 | blockMesh/* (7 cases) | 3D | blockMesh | T1 | Mesh generation: pipe, sphere, spheroid |
| 441-444 | foamyHexMesh/* (4 cases) | 3D | foamyHexMesh | T3 | Alternative hex mesher |
| 445-447 | foamyQuadMesh/* (3 cases) | 2D | foamyQuadMesh | T3 | Quad mesh generation |
| 448-453 | moveDynamicMesh/* (7 cases) | 3D | blockMesh | T6 | Mesh motion demos |
| 454-467 | snappyHexMesh/* (14 cases) | 3D | snappy | T2 | SnappyHexMesh techniques |
| 468-475 | refineMesh/createPatch/extrudeMesh/stitchMesh/PDRblockMesh/polyDualMesh/parallel (8) | various | various | T3 | Mesh utilities |

---

### 10. DISCRETE METHODS (7 cases)

| # | Tutorial Path | 2D/3D | Mesh | Tier | Teaches |
|---|--------------|-------|------|------|---------|
| 476-479 | dsmcFoam/* (4 cases) | 2D/3D | blockMesh | T7 | DSMC rarefied gas |
| 480-482 | molecularDynamics/* (3 cases) | 3D | special | T7 | Molecular dynamics |

---

### 11. REMAINING CATEGORIES

| # | Category | Cases | Tier | Teaches |
|---|----------|-------|------|---------|
| 483 | DNS/dnsFoam/boxTurb16 | 1 | T1 | Direct numerical simulation |
| 484-485 | electromagnetics/* | 2 | T7 | Electrostatics, MHD |
| 486 | financial/financialFoam/europeanCall | 1 | T7 | Black-Scholes PDE |
| 487-489 | finiteArea/* | 3 | T7 | Surface transport |
| 490-494 | preProcessing/* | 5 | T3 | Utility workflows |
| 495-551 | verificationAndValidation/* | 57 | T1-T3 | V&V benchmarks |

---

## Tier Summary

| Tier | Count (approx) | Description | Architecture Work |
|------|---------------|-------------|-------------------|
| **T1** | ~95 | blockMesh → solver (no extras) | **None — ready now** |
| **T2** | ~30 | snappyHexMesh + parallel | **Supported today** |
| **T3** | ~55 | Extra mesh utilities (topoSet, createPatch, createBaffles, setFields, mapFields, extrudeMesh) | **Add pipeline steps** |
| **T4** | ~15 | Multi-stage workflows (precursor → main, external coupling) | **New: stage orchestration** |
| **T5** | ~20 | Multi-region (splitMeshRegions, per-region BCs) | **New: multi-region support** |
| **T6** | ~45 | Dynamic/overset mesh (AMI, dynamicMeshDict, overset) | **New: dynamic mesh** |
| **T7** | ~170 | Specialized physics (VOF, particles, combustion, DSMC, MD) | **New: physics modules** |
| **T8** | ~66 | Optimization loops (adjoint, shape morphing) | **New: optimization workflow** |
| **V&V** | ~57 | Verification & validation (cross-tier) | Depends on tier of each case |
| **Total** | **~601** | | |

---

## Recommended Execution Order

### Wave 1: Foundation (T1 + T2) — ~125 templates
Build all blockMesh-only and snappyHexMesh tutorials. This validates the core pipeline across every solver foamscript can already handle. Organized by progression track:

**1a. Internal flow fundamentals** (simpleFoam/pimpleFoam internal)
**1b. External aero expansion** (simpleFoam/pimpleFoam external)
**1c. Compressible flow** (rhoSimpleFoam, rhoCentralFoam, rhoPimpleFoam)
**1d. Basic solvers** (laplacianFoam, potentialFoam, scalarTransportFoam, icoFoam)
**1e. Stress analysis** (solidDisplacementFoam)

### Wave 2: Extended Pipeline (T3) — ~55 templates
Add support for topoSet, createPatch, createBaffles, setFields, mapFields, extrudeMesh as pipeline steps. This unlocks a large batch of tutorials that use these utilities.

### Wave 3: Multi-Region + Multi-Stage (T4 + T5) — ~35 templates
Add multi-region case structure (splitMeshRegions) and sequential stage orchestration. Unlocks conjugate heat transfer and precursor→main workflows.

### Wave 4: Dynamic Mesh + AMI (T6) — ~45 templates
Add AMI sliding interfaces, overset mesh support, dynamic mesh motion. Unlocks turbomachinery, moving bodies, overset cases.

### Wave 5: Specialized Physics (T7) — ~170 templates
Add VOF phase initialization, Lagrangian particles, combustion chemistry, DSMC, molecular dynamics. This is the largest wave but each physics type is somewhat independent.

### Wave 6: Optimization (T8) — ~66 templates
Add adjoint solver loops, shape morphing, topology optimization. This is the most architecturally different workflow.

### Wave 7: Mesh-Only + V&V — ~57+ templates
Mesh generation tutorials and verification/validation benchmarks. These serve as regression tests and meshing technique references.

---

## Architecture Gaps Identified

| Gap | Tutorials Blocked | Wave |
|-----|------------------|------|
| No-geometry templates (blockMesh defines geometry) | ~95 T1 cases | Wave 1 |
| No-angle-sweep templates (single case, no AoA) | Most non-aero cases | Wave 1 |
| Optional --model-source (many tutorials have no STL) | ~95 T1 cases | Wave 1 |
| Pipeline step extensibility (topoSet, createPatch, etc.) | ~55 T3 cases | Wave 2 |
| setFields for initial conditions | Dam break, shock tube, etc. | Wave 2 |
| Multi-region case structure | All CHT cases | Wave 3 |
| Multi-stage orchestration | Precursor→LES, acoustic | Wave 3 |
| AMI interface setup | Mixers, propellers | Wave 4 |
| Overset mesh assembly | Chimera cases | Wave 4 |
| Dynamic mesh configuration | Moving bodies | Wave 4 |
| VOF phase initialization | All multiphase | Wave 5 |
| Lagrangian particle injection | All particle cases | Wave 5 |
| Chemistry mechanism loading | All combustion | Wave 5 |
| Adjoint solver loop | All optimization | Wave 6 |
| Shape morphing / design variables | Shape optimization | Wave 6 |
| External mesh import (Fluent, Star-CD, datToFoam) | ~5 cases | Wave 2 |
| m4 macro processing for blockMeshDict | ~2 cases | Wave 2 |
| Pre-meshed geometry support | ~15 cases | Wave 1 |

---

## Next Immediate Step

**Start Wave 1a: Internal flow fundamentals** beginning with `simpleFoam/pitzDaily` (#36).

This requires resolving the first architecture gap: templates that don't need STL geometry input. pitzDaily uses only blockMesh — no --model-source, no geometry validation, no angles sweep.
