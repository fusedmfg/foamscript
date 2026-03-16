# External Airfoil — Static Steady-State (simpleFoam)

Steady-state incompressible RANS simulation of a 2D airfoil using simpleFoam
with Spalart-Allmaras turbulence model and freestream boundary conditions.

Based on OpenFOAM v2512 tutorials:
- `incompressible/simpleFoam/airFoil2D` (solver settings, BCs)
- `mesh/snappyHexMesh/aerofoilNACA0012_directionalRefinement` (meshing)

## Use Cases
- Airfoil lift/drag coefficient analysis
- Angle of attack sweeps
- Wing section performance evaluation
- NACA profile characterization

## Key Features
- Spalart-Allmaras one-equation turbulence model
- Freestream boundary conditions (inlet/outlet)
- 2D simulation via thin spanwise domain with empty BCs
- Wake refinement region for improved downstream resolution
- Boundary layers on airfoil surface
