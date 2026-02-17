# FoamScript Template Catalog

This directory contains OpenFOAM case templates for various simulation types. Each template is tightly coupled to the FoamScript codebase through Scriban templating variables defined in `CaseService.CalculateTemplateContext()`.

## Template Naming Convention

Templates follow the naming format: **`{domain}_{feature}_{motion}_{solver-type}`**

- **Domain**: Application area (external, internal, turbomachinery, etc.)
- **Feature**: Primary object or phenomenon (disc, airfoil, propeller, pipe, etc.)
- **Motion**: Mesh motion type (static, rotating-ami, rotating-mrf, morphing, etc.)
- **Solver Type**: Time scheme (steady, transient) or specific solver characteristic

## Available Templates

### 1. external_disc_rotating-ami_transient ✅

**Classification:**
- **Domain**: External aerodynamics
- **Feature**: Rotating disc
- **Motion**: AMI (Arbitrary Mesh Interface) - full mesh rotation
- **Solver**: Transient (pimpleFoam)

**Use Cases:**
- Frisbee aerodynamics
- Propeller blade element analysis
- Rotating disc flow visualization
- Angle of attack sweeps with rotation

**Key Features:**
- RANS k-omega SST turbulence model
- Solid body rotation with cyclicAMI interfaces
- External tunnel domain with inlet/outlet
- Force coefficient calculation (drag, lift, moment)

**Documentation**: [external_disc_rotating-ami_transient/TEMPLATE.md](external_disc_rotating-ami_transient/TEMPLATE.md)

---

## Planned Templates

### 2. external_airfoil_static_steady (Priority 1)

**Classification:**
- **Domain**: External aerodynamics
- **Feature**: Airfoil (2D or 3D)
- **Motion**: Static (no mesh motion)
- **Solver**: Steady-state (simpleFoam)

**Use Cases:**
- Quick lift/drag coefficient analysis
- Airfoil design iteration
- Wing section performance
- No time-dependent effects needed

**Key Differences from Current:**
- No rotating mesh (simpler setup)
- Steady-state solver (faster convergence)
- 2D option for faster iteration

---

### 3. external_airfoil_static_transient (Priority 2)

**Classification:**
- **Domain**: External aerodynamics
- **Feature**: Airfoil
- **Motion**: Static
- **Solver**: Transient (pimpleFoam)

**Use Cases:**
- Vortex shedding analysis
- Dynamic stall investigation
- Time-varying forces
- Flow visualization

**Key Differences from Steady:**
- Captures unsteady flow phenomena
- Time history of forces
- Higher computational cost

---

### 4. turbomachinery_propeller_rotating-mrf_steady (Priority 3)

**Classification:**
- **Domain**: Turbomachinery
- **Feature**: Propeller
- **Motion**: MRF (Multiple Reference Frames) - frozen rotor
- **Solver**: Steady-state (simpleFoam with MRF)

**Use Cases:**
- Propeller thrust/torque analysis
- Multi-blade configurations
- Fast steady-state solutions
- Design optimization

**Key Differences from AMI:**
- Frozen rotor approximation (faster)
- No actual mesh rotation
- Assumes steady flow in rotating frame

---

### 5. turbomachinery_propeller_rotating-ami_transient (Priority 4)

**Classification:**
- **Domain**: Turbomachinery
- **Feature**: Propeller
- **Motion**: AMI (full rotation)
- **Solver**: Transient (pimpleFoam)

**Use Cases:**
- Unsteady propeller effects
- Blade-passage interactions
- High-fidelity propeller analysis
- Time-accurate solutions

**Key Differences from MRF:**
- Full mesh rotation
- Captures unsteady interactions
- Higher computational cost
- More accurate for unsteady flows

---

### 6. external_airfoil_compressible_transonic (Future)

**Classification:**
- **Domain**: External aerodynamics (compressible)
- **Feature**: Airfoil
- **Motion**: Static
- **Solver**: Transient or steady (rhoPimpleFoam/rhoSimpleFoam)

**Use Cases:**
- High-speed flow (Mach > 0.3)
- Shock wave analysis
- Transonic buffet
- Supersonic flow

**Key Differences:**
- Compressible flow solver
- Density-based calculations
- Shock capturing schemes

---

## Template Development Guidelines

When creating a new template:

1. **Create template directory** following naming convention
2. **Document in TEMPLATE.md** with:
   - Classification (domain, feature, motion, solver)
   - All template variables and their meanings
   - Use cases and applications
   - Key physics and modeling choices
3. **Update CalculateTemplateContext()** in CaseService.cs
4. **Add to this catalog** in the "Available Templates" section
5. **Create tests** if template-specific logic is complex
6. **Update default path logic** if this becomes a new default

## Template Selection Guide

**Choose your template based on:**

| Need | Recommended Template |
|------|---------------------|
| Rotating object aerodynamics | `external_disc_rotating-ami_transient` |
| Quick airfoil analysis | `external_airfoil_static_steady` |
| Unsteady airfoil flow | `external_airfoil_static_transient` |
| Propeller design (fast) | `turbomachinery_propeller_rotating-mrf_steady` |
| Propeller unsteady effects | `turbomachinery_propeller_rotating-ami_transient` |
| High-speed flow | `external_airfoil_compressible_transonic` |

## Contributing Templates

To propose a new template:

1. Identify the use case and classification
2. Check if existing template can be adapted
3. Document required template variables
4. Discuss on GitHub issues before implementation

## References

- [OpenFOAM Solvers Guide](https://www.openfoam.com/documentation/guides/latest/doc/guide-applications-solvers.html)
- [Turbulence Modeling](https://turbmodels.larc.nasa.gov/)
- [Scriban Documentation](https://github.com/scriban/scriban/tree/master/doc)
