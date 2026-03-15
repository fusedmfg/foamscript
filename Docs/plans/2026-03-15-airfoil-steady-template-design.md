# Design: external_airfoil_static_steady Template (#1)

**Date:** 2026-03-15
**Status:** Approved
**Issue:** #1

## Summary

Steady-state incompressible RANS simulation of a static airfoil using simpleFoam + Spalart-Allmaras.
Based on OpenFOAM v2512 tutorials: `incompressible/simpleFoam/airFoil2D` (solver/BCs) and
`mesh/snappyHexMesh/aerofoilNACA0012_directionalRefinement` (meshing approach).

## Template Structure

```
Templates/external_airfoil_static_steady/
  TEMPLATE.json          — metadata for template registry
  TEMPLATE.md            — human-readable description
  0/
    U                    — freestreamVelocity inlet/outlet, noSlip airfoil, empty frontAndBack
    p                    — freestreamPressure inlet/outlet, zeroGradient airfoil, empty frontAndBack
    nut                  — freestream inlet/outlet, nutUSpaldingWallFunction airfoil
    nuTilda              — freestream inlet/outlet, fixedValue 0 airfoil
  constant/
    transportProperties  — Newtonian, nu parameterized
    turbulenceProperties — RAS, SpalartAllmaras
  system/
    blockMeshDict        — thin rectangular domain, parameterized extents
    controlDict          — simpleFoam, forceCoeffs (chord-based Aref)
    decomposeParDict     — scotch, parameterized cores
    fvSchemes            — steadyState, linearUpwind for U and nuTilda
    fvSolution           — SIMPLE, GAMG for p, smoothSolver for U/nuTilda
    snappyHexMeshDict    — airfoil surface refinement, wake box refinement, boundary layers
    surfaceFeatureExtractDict — airfoil feature edges
```

## Key Design Decisions

1. **Spalart-Allmaras** (not kOmegaSST) — matches airFoil2D tutorial, standard for airfoil RANS
2. **Freestream BCs** — freestreamVelocity/freestreamPressure at inlet/outlet (tutorial pattern)
3. **Thin spanwise domain** — single cell in Y with `empty` frontAndBack BCs (2D simulation)
4. **blockMesh + snappyHexMesh** — matches aerofoilNACA0012 tutorial meshing approach
5. **No rotation** — `requiresRotorZone: false`, uses `GenerateTunnelOnly()` in DomainService
6. **Wake refinement box** — directional refinement downstream of airfoil (from NACA0012 tutorial)
7. **Boundary layers** — enabled on airfoil surface (critical for accurate drag prediction)

## TEMPLATE.json

```json
{
  "name": "external_airfoil_static_steady",
  "description": "Steady-state 2D airfoil analysis (simpleFoam + Spalart-Allmaras)",
  "solver": "simpleFoam",
  "geometryType": "airfoil",
  "geometryStlName": "airfoil.stl",
  "referenceDimension": "chord",
  "referenceAreaFormula": "chord_span",
  "requiresRotorZone": false,
  "requiredStlFiles": ["airfoil.stl"]
}
```

## C# Code Changes

1. **TemplateMetadataService.CalculateReferenceDimension()** — add `"chord"` case:
   longest horizontal axis of bounding box (max of Width, Depth)
2. **TemplateMetadataService.CalculateReferenceArea()** — add `"chord_span"` formula:
   chord * span (for 2D: chord * Y-extent of domain)
3. **CaseService.CalculateTemplateContext()** — add `nuTilda` initial value calculation
   for Spalart-Allmaras (nuTilda ≈ 3-5 * nu, per tutorial default)

## Tutorial Defaults (from airFoil2D)

- Solver: simpleFoam
- Turbulence: Spalart-Allmaras
- nu: 1e-5 m²/s
- Velocity: ~26 m/s (tutorial uses 25.75 m/s at 8° AoA)
- Iterations: 500, writeInterval: 50
- Pressure solver: GAMG, relTol 0.1
- Relaxation: p=0.3, U=0.7, nuTilda=0.7
- Residual targets: p=1e-5, U=1e-5, nuTilda=1e-5

## Validation Plan

- Geometry: NACA0012.obj.gz from OpenFOAM tutorials/resources/geometry/
- Run: AoA=0 at default velocity, verify Cd/Cl convergence
- Expected: Cd ≈ 0.008-0.012 for NACA0012 at 0° AoA (Re ~170k with chord=1m, nu=1e-5, V=26m/s)
