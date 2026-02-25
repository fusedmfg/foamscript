# External Disc — MRF Steady-State (simpleFoam)

Steady-state incompressible RANS simulation of a rotating disc in a wind tunnel
using the Multiple Reference Frame (MRF) approach with k-omega SST turbulence.

## Solver
- **Application**: simpleFoam (steady-state SIMPLE algorithm)
- **Turbulence**: k-omega SST (RAS)
- **Rotation**: MRF (source terms in momentum equation, no mesh motion)

## Template Variables (Scriban)
| Variable | Source | Description |
|----------|--------|-------------|
| `ux`, `uy` | C# (from velocity + AoA) | Freestream velocity components (m/s) |
| `p` | C# (constant 0) | Reference pressure |
| `k` | C# (from velocity + TI) | Turbulent kinetic energy |
| `omega_turbulence` | C# (from k + disc diameter) | Specific dissipation rate |
| `omega_rotation` | C# (from RPM) | Disc spin rate (rad/s) |
| `mag_u_inf` | C# | Velocity magnitude for force coefficients |
| `disc_diameter` | C# | Reference length for force coefficients |
| `aref` | C# | Reference area (pi * r^2) |
| `max_iterations` | Config (default 1000) | SIMPLE iteration count |
| `write_interval` | Config (default 100) | Snapshot write frequency |
| `refinement_level_min` | Config (default 3) | snappyHexMesh min refinement |
| `refinement_level_max` | Config (default 4) | snappyHexMesh max refinement |
| `feature_level` | Config | Edge refinement level |
| `domain_upstream` | C# | Domain extent upstream (m) |
| `domain_downstream` | C# | Domain extent downstream (m) |
| `domain_radial` | C# | Domain radial extent (m) |
| `cores` | Config (default 4) | CPU cores for parallel |

## Output
- `postProcessing/forces/0/coefficient.dat` — Cd, Cl, CmPitch per iteration
