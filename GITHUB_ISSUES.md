# GitHub Issues for Future Templates

Copy each section below into a new GitHub issue. Use the issue titles and labels as specified.

---

## Issue 1: Template: external_airfoil_static_steady (Priority 1)

**Labels**: `enhancement`, `template`, `priority-1`

### Description

**Name**: `external_airfoil_static_steady`

**Classification**:
- **Domain**: External aerodynamics
- **Feature**: Airfoil (2D or 3D wing section)
- **Motion**: Static (no mesh motion)
- **Solver**: Steady-state (simpleFoam)

### Use Cases
- Quick lift/drag coefficient analysis
- Airfoil design iteration
- Wing section performance evaluation
- Reynolds number studies
- No time-dependent effects needed

### Key Features
- RANS k-omega SST turbulence model
- Steady-state convergence (faster than transient)
- 2D option for rapid iteration
- 3D wing section capability
- Angle of attack sweeps without rotation

### Implementation Requirements

**Template Variables** (add to `CalculateTemplateContext`):
- `ux`, `uy` - Velocity components (for AoA)
- `p` - Reference pressure
- `k`, `omega_turbulence` - Turbulence parameters
- `nu` - Kinematic viscosity
- `mag_u_inf` - Velocity magnitude
- `chord_length` - Airfoil chord (reference length)
- `aref` - Reference area (chord × span for 2D, actual area for 3D)
- `cores` - Parallel decomposition
- No `omega_rotation` (static mesh)

**Key Differences from Current Template**:
- No `dynamicMeshDict` (static mesh)
- Use `simpleFoam` instead of `pimpleFoam`
- Steady-state convergence criteria in `controlDict`
- Different force coefficient calculation (per chord instead of disc diameter)
- Simpler boundary conditions (no rotating zones)

**Geometry Expectations**:
- Airfoil cross-section (2D) or wing section (3D)
- Domain: C-mesh or O-mesh around airfoil
- Boundaries: inlet, outlet, farfield (top/bottom or cylindrical), airfoil surface

### Testing Plan
- [ ] NACA 0012 airfoil validation (known lift/drag curves)
- [ ] Angle of attack sweep (-10° to 20°)
- [ ] Compare with experimental/published data
- [ ] Verify steady-state convergence behavior

### Priority
**Priority 1** - Most broadly applicable template for aerodynamics

### References
- [simpleFoam Documentation](https://www.openfoam.com/documentation/guides/latest/doc/guide-applications-solvers-incompressible-simpleFoam.html)
- [NACA Airfoil Data](https://airfoiltools.com/)
- [Turbulence Modeling for Airfoils](https://turbmodels.larc.nasa.gov/)

---

## Issue 2: Template: external_airfoil_static_transient (Priority 2)

**Labels**: `enhancement`, `template`, `priority-2`

### Description

**Name**: `external_airfoil_static_transient`

**Classification**:
- **Domain**: External aerodynamics
- **Feature**: Airfoil
- **Motion**: Static
- **Solver**: Transient (pimpleFoam)

### Use Cases
- Vortex shedding analysis
- Dynamic stall investigation
- Time-varying lift/drag forces
- Flow visualization (vorticity, Q-criterion)
- Unsteady flow phenomena

### Key Features
- RANS k-omega SST or LES turbulence
- Transient solver (similar to current rotating disc)
- Time history of forces
- Flow field animation capability

### Implementation Requirements

**Template Variables**:
- Similar to `external_airfoil_static_steady` plus:
- `end_time` - Simulation duration
- `delta_t` - Time step size
- `write_interval` - Output frequency

**Key Differences from Steady Template**:
- Uses `pimpleFoam` instead of `simpleFoam`
- Transient time controls in `controlDict`
- Higher computational cost
- Captures vortex shedding and unsteady effects

### Testing Plan
- [ ] NACA 0012 at high angle of attack (dynamic stall)
- [ ] Vortex shedding frequency validation
- [ ] Compare time-averaged forces with steady-state

### Priority
**Priority 2** - Important for unsteady flow analysis

---

## Issue 3: Template: turbomachinery_propeller_rotating-mrf_steady (Priority 3)

**Labels**: `enhancement`, `template`, `priority-3`

### Description

**Name**: `turbomachinery_propeller_rotating-mrf_steady`

**Classification**:
- **Domain**: Turbomachinery
- **Feature**: Propeller
- **Motion**: MRF (Multiple Reference Frames) - frozen rotor
- **Solver**: Steady-state (simpleFoam with MRF)

### Use Cases
- Propeller thrust/torque analysis
- Multi-blade configurations
- Fast steady-state solutions
- Propeller design optimization
- Helicopter rotor analysis

### Key Features
- MRF zones for rotating regions
- Frozen rotor approximation (no actual mesh motion)
- Steady-state convergence
- Faster than full AMI rotation
- Multiple blade support

### Implementation Requirements

**Template Variables**:
- `ux`, `uy`, `uz` - Freestream velocity
- `omega_rotation` - Rotation speed (rad/s)
- `mrf_origin` - Rotation axis origin
- `mrf_axis` - Rotation axis direction
- `mrf_zone_name` - MRF cell zone name

**Key Differences from AMI Template**:
- Uses `MRFSimpleFoam` or `simpleFoam` with MRF zones
- No `dynamicMeshDict` (frozen rotor)
- MRF properties in `constant/MRFProperties`
- Assumes flow is steady in rotating frame

**Geometry Expectations**:
- Propeller blades
- MRF zone around blades
- Stationary outer domain

### Testing Plan
- [ ] Propeller thrust validation
- [ ] Compare with experimental data
- [ ] Multi-blade configuration

### Priority
**Priority 3** - Specialized turbomachinery application

---

## Issue 4: Template: turbomachinery_propeller_rotating-ami_transient (Priority 4)

**Labels**: `enhancement`, `template`, `priority-4`

### Description

**Name**: `turbomachinery_propeller_rotating-ami_transient`

**Classification**:
- **Domain**: Turbomachinery
- **Feature**: Propeller
- **Motion**: AMI (full rotation)
- **Solver**: Transient (pimpleFoam)

### Use Cases
- Unsteady propeller effects
- Blade-passage interactions
- High-fidelity propeller analysis
- Time-accurate solutions
- Propeller wake analysis

### Key Features
- Full mesh rotation with AMI interfaces
- Transient solver
- Highest fidelity for rotating machinery
- Captures all unsteady interactions

### Implementation Requirements

**Template Variables**:
- Very similar to current `external_disc_rotating-ami_transient`
- Modified for propeller geometry and multi-blade support

**Key Differences from Current Disc Template**:
- Multi-blade support (vs single disc)
- Different reference areas for force coefficients
- Propeller-specific boundary conditions

### Testing Plan
- [ ] Propeller thrust time history
- [ ] Blade-passage effects
- [ ] Wake structure validation

### Priority
**Priority 4** - Highest fidelity, highest cost

---

## Issue 5: Template: external_airfoil_compressible_transonic (Future)

**Labels**: `enhancement`, `template`, `future`

### Description

**Name**: `external_airfoil_compressible_transonic`

**Classification**:
- **Domain**: External aerodynamics (compressible)
- **Feature**: Airfoil
- **Motion**: Static
- **Solver**: Compressible (rhoPimpleFoam or rhoSimpleFoam)

### Use Cases
- High-speed flow (Mach > 0.3)
- Shock wave analysis
- Transonic buffet
- Supersonic flow
- Wave drag calculation

### Key Features
- Compressible flow solver
- Density-based calculations
- Shock capturing schemes
- Temperature effects

### Implementation Requirements

**Template Variables**:
- All from incompressible airfoil plus:
- `mach_number` - Freestream Mach number
- `temperature` - Freestream temperature
- `pressure` - Freestream pressure
- `gamma` - Specific heat ratio

**Key Differences from Incompressible**:
- Uses density-based solver (`rhoPimpleFoam` or `rhoSimpleFoam`)
- Energy equation
- Compressible turbulence models
- Different boundary conditions for pressure/temperature

### Testing Plan
- [ ] NACA 0012 transonic validation
- [ ] Shock location accuracy
- [ ] Wave drag prediction

### Priority
**Future** - Specialized high-speed applications

---

## Issue 6: Add template selection to CLI

**Labels**: `enhancement`, `cli`, `usability`

### Description

Add command-line interface for listing and selecting templates.

### Proposed Commands

**List available templates**:
```bash
foamscript list-templates
```

Output:
```
Available Templates:
  1. external_disc_rotating-ami_transient (default)
     - External rotating disc with AMI, transient solver
     - Use for: Frisbee aerodynamics, rotating discs

  Future templates planned in TEMPLATES.md
```

**Select template for new-study**:
```bash
foamscript new-study -n "MyStudy" -o "output" -s "model.step" -a "0,5,10" --template-name "external_disc_rotating-ami_transient"
```

Or use short path:
```bash
foamscript new-study ... -t "external_disc_rotating-ami_transient"
```

### Implementation
- Add `list-templates` verb to enumerate Templates/ directory
- Accept template name (without full path) in `-t` option
- Resolve short name to full path in AppService
- Display helpful error if template not found

### Testing
- [ ] List all available templates
- [ ] Select template by name
- [ ] Error handling for invalid template name
- [ ] Backward compatibility with full paths

---

## Issue 7: End-to-end testing for current rotating disc workflow

**Labels**: `testing`, `documentation`

### Description

Complete end-to-end testing of the `external_disc_rotating-ami_transient` template workflow.

### Test Scenarios

**1. Geometry Processing**
- [ ] STEP file input (with unit conversion)
- [ ] STL file input (pre-converted)
- [ ] Geometry validation (bounding box, diameter extraction)
- [ ] Domain generation (rotor, tunnel)

**2. Case Creation**
- [ ] Single angle of attack case
- [ ] Multi-angle AoA sweep
- [ ] Parameter calculation (turbulence, velocity components)
- [ ] Template processing (Scriban variable substitution)

**3. Meshing**
- [ ] blockMesh execution
- [ ] snappyHexMesh (serial and parallel)
- [ ] Mesh quality checks
- [ ] Reconstruction after parallel meshing

**4. Solver Execution** (if OpenFOAM available)
- [ ] Decompose for parallel run
- [ ] pimpleFoam execution
- [ ] Force coefficient output
- [ ] Visualization data

**5. Results Post-Processing**
- [ ] Force coefficient extraction
- [ ] Visualization in ParaView
- [ ] Convergence analysis

### Acceptance Criteria
- Complete workflow runs without errors
- Generated cases have correct parameters
- Mesh quality passes OpenFOAM checks
- Documentation updated with test results

---

Copy each section above into a new GitHub issue with the specified title and labels.
