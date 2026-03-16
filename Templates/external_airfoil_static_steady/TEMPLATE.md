# External Airfoil — Static Steady-State (simpleFoam)

Steady-state incompressible RANS simulation of a 2D airfoil using simpleFoam
with Spalart-Allmaras turbulence model and freestream boundary conditions.

Based on OpenFOAM v2512 tutorials:
- `incompressible/simpleFoam/airFoil2D` (solver settings, BCs)
- `mesh/snappyHexMesh/airfoilWithLayers` (meshing, domain setup)

## Use Cases
- Airfoil lift/drag coefficient analysis
- Angle of attack sweeps
- Wing section performance evaluation
- NACA profile characterization

## Key Features
- Spalart-Allmaras one-equation turbulence model
- Freestream boundary conditions (inlet/outlet)
- Quasi-2D simulation via symmetryPlane BCs (per airfoilWithLayers tutorial)
- Wake refinement region for improved downstream resolution
- Boundary layers on airfoil surface
