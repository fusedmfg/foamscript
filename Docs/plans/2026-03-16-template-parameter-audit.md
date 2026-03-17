# Template Parameter Audit & Remediation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Eliminate hardcoded disc-golf-era assumptions from CaseService/StudyConfig so every template-variable value is either a true physics constant or template-driven via TEMPLATE.json.

**Architecture:** Move all template-specific defaults out of C# code into TEMPLATE.json. Make CLI parameters nullable with template fallback (same pattern already proven for refinement levels). Remove legacy rotor/AMI code. Decouple geometry conversion from `new-study` (the standalone `convert` command already exists). Each template's TEMPLATE.json becomes the single source of truth for its defaults.

**Tech Stack:** .NET 10, xUnit + Moq + FluentAssertions, System.Text.Json, CommandLine (CommandLineParser)

---

## Context: The Audit

CaseService and StudyConfig were built for a single disc-golf template. With multi-template support, hardcoded values fall into four categories:

| Category | Action | Examples |
|----------|--------|---------|
| **True constants** | Keep in C# | cmu=0.09, TKE coefficient 1.5, p=0, RPM→rad/s |
| **Template defaults** | Move to TEMPLATE.json | Nu, TI, mixing length ratio, span ratio, domain margin, feature level, nu_tilda multiplier, endTime, writeInterval, nOuterCorrectors |
| **Legacy rotor/AMI** | Delete | rotorRadius=0.75×refLength, meshMotionVelocity, RotorRadiusScale/HeightScale, MeshResolution |
| **Geometry conversion** | Decouple from new-study | InputUnits, MeshSize (already in standalone `convert` command) |

## Key Files

| File | Role |
|------|------|
| `Services/CaseService.cs` | Template context calculation — main refactor target |
| `Models/StudyConfig.cs` | Config model with hardcoded defaults |
| `Models/NewStudyModel.cs` | CLI argument definitions |
| `Handlers/NewStudyHandler.cs` | Template default application logic |
| `Models/TemplateMetadata.cs` | TEMPLATE.json schema (DomainConfig, ParameterDef) |
| `Templates/external_disc_rotatingwall_steady/TEMPLATE.json` | Disc template metadata |
| `Templates/external_airfoil_static_steady/TEMPLATE.json` | Airfoil template metadata |
| `foamscript.Tests/Unit/CaseServiceTests.cs` | Main test file for context calculation |
| `foamscript.Tests/Unit/NewStudyHandlerTests.cs` | Handler tests |

## Phase 1: Delete Legacy Rotor/AMI Code

The AMI approach for disc golf was abandoned. The rotor-specific code in CaseService is dead weight. If MRF/AMI returns for a propeller template (#3), it re-enters as template-driven values.

### Task 1.1: Remove Rotor/AMI from CaseService.CalculateTemplateContext

**Files:**
- Modify: `Services/CaseService.cs:286-310` (deltaT/rotor calculation block)
- Test: `foamscript.Tests/Unit/CaseServiceTests.cs`

**Step 1: Identify all rotor-specific code in CalculateTemplateContext**

Lines to remove from `CaseService.cs`:
- Lines 286-310: The entire `deltaT`, `maxDeltaT`, and rotor radius block (transient time-stepping for PIMPLE — neither the disc nor airfoil template uses transient solver currently)
- Line 297-305: `if (requiresRotorZone)` block with `rotorRadius = refLength * 0.75` and `meshMotionVelocity`
- Template context properties: `delta_t`, `max_delta_t` (not used by any current template)
- The `requiresRotorZone` parameter on `CalculateTemplateContext` (currently only controls deltaT calculation which is being removed)

Keep: `omega_rotation` in the context — it's used by the disc template's `0/U` for `rotatingWallVelocity` BC.

**Step 2: Write a test confirming rotor params are gone**

```csharp
[Fact]
public void CalculateTemplateContext_DoesNotContain_LegacyRotorProperties()
{
    var context = CaseService.CalculateTemplateContext(
        ux: 27.0, uz: 0.0, omegaRotation: 96.86,
        cores: 4, refLength: 0.21, aref: 0.0346,
        physics: new StudyPhysicsConfig(),
        domain: new StudyDomainConfig());

    // delta_t and max_delta_t were transient/rotor-specific — removed
    var dict = context.GetType().GetProperties()
        .ToDictionary(p => p.Name, p => p.GetValue(context));
    dict.Should().NotContainKey("delta_t");
    dict.Should().NotContainKey("max_delta_t");
}
```

**Step 3: Run tests to verify the new test fails (delta_t still exists)**

Run: `dotnet test --filter "DoesNotContain_LegacyRotorProperties" --verbosity normal`
Expected: FAIL

**Step 4: Remove the rotor/deltaT code from CalculateTemplateContext**

In `CaseService.cs`, remove:
- The `requiresRotorZone` parameter from `CalculateTemplateContext` signature
- Lines 286-310 (the entire deltaT/maxDeltaT/rotor block)
- `delta_t` and `max_delta_t` from the returned anonymous object
- Update the call site in `CreateCase` to stop passing `requiresRotorZone`

The method signature becomes:
```csharp
internal static object CalculateTemplateContext(double ux, double uz, double omegaRotation,
    int cores, double refLength, double aref,
    StudyPhysicsConfig physics, StudyDomainConfig domain,
    double spanHalf = 0.0)
```

**Step 5: Fix compilation errors in existing tests**

Update any test calling `CalculateTemplateContext` that passes `requiresRotorZone`. Search for all callers in test files.

**Step 6: Run all tests**

Run: `dotnet test --verbosity normal`
Expected: All pass (241+ tests)

**Step 7: Commit**

```
feat: remove legacy rotor/AMI deltaT code from CaseService
```

---

### Task 1.2: Remove Rotor Domain Parameters from StudyConfig and CLI

**Files:**
- Modify: `Models/StudyConfig.cs:58-77` (StudyDomainConfig)
- Modify: `Models/NewStudyModel.cs:80-98` (CLI options)
- Modify: `Handlers/NewStudyHandler.cs:79-87,219-224` (config mapping + display)
- Modify: `Services/CaseService.cs:183-195` (rotor branch in ProcessGeometry)
- Test: `foamscript.Tests/Unit/NewStudyHandlerTests.cs`

**Step 1: Write a test confirming rotor CLI options are gone**

```csharp
[Fact]
public void NewStudyModel_ShouldNot_HaveRotorProperties()
{
    var props = typeof(NewStudyModel).GetProperties();
    props.Should().NotContain(p => p.Name == "RotorRadiusScale");
    props.Should().NotContain(p => p.Name == "RotorHeightScale");
    props.Should().NotContain(p => p.Name == "MeshResolution");
}
```

**Step 2: Run test — should FAIL**

**Step 3: Remove from models**

From `StudyDomainConfig`, remove:
- `RotorRadiusScale` property
- `RotorHeightScale` property
- `MeshResolution` property

From `NewStudyModel`, remove:
- `--rotor-radius-scale` option (line 82-83)
- `--rotor-height-scale` option (line 85-86)
- `--mesh-resolution` option (line 97-98)

From `NewStudyHandler`, remove:
- Config mapping lines 81-82, 86 (RotorRadiusScale, RotorHeightScale, MeshResolution)
- Display lines 219-220, 224 (rotor scale display)

From `CaseService.ProcessGeometry`, simplify:
- Remove `if (metadata.RequiresRotorZone)` branch (lines 183-195)
- Keep only `GenerateTunnelOnly` path (lines 200-207)
- Remove `rotorRadiusScale`, `rotorHeightScale`, `meshResolution` params from domain usage

**Note:** Keep `RequiresRotorZone` in `TemplateMetadata` — it's still referenced in the disc template's TEMPLATE.json and may return for propeller templates. But CaseService no longer uses it for domain generation.

Actually — re-evaluate: the disc template has `"requiresRotorZone": true` and uses a `rotor.stl` file. The `GenerateDomain()` method creates the rotor cylinder STL. If we remove this, the disc template's mesh pipeline breaks (snappyHexMesh references rotor.stl). **Decision point: keep GenerateDomain for now, just remove the hardcoded deltaT/rotor calculations from CalculateTemplateContext. The domain generation rotor parameters should become template-driven but that's a larger task for when we refactor the disc template.**

**Revised Step 3:** Only remove from `CalculateTemplateContext` and CLI display. Keep `StudyDomainConfig` rotor fields for now — they feed `ProcessGeometry → GenerateDomain` which the disc template still needs. Mark them with `// TODO: Move to template-driven domain config` comments.

Remove from `NewStudyModel` CLI: `--rotor-radius-scale`, `--rotor-height-scale`, `--mesh-resolution` (these will become template-driven, not CLI flags).

Remove from `NewStudyHandler` display output: rotor-specific lines.

**Step 4: Fix compilation and run all tests**

Run: `dotnet test --verbosity normal`
Expected: All pass

**Step 5: Commit**

```
refactor: remove rotor CLI flags, mark domain rotor params for template migration
```

---

## Phase 2: Decouple Geometry Conversion from new-study

The standalone `convert` command (`Handlers/ConvertHandler.cs`) already handles STEP→STL with unit conversion. The `new-study` command should accept meter-scale STL (or STEP with embedded conversion for convenience), but `InputUnits` and `MeshSize` are conversion concerns, not study concerns.

### Task 2.1: Remove InputUnits and MeshSize from StudyConfig

**Files:**
- Modify: `Models/StudyConfig.cs:15-16`
- Modify: `Models/NewStudyModel.cs:42-46`
- Modify: `Handlers/NewStudyHandler.cs:64-65,215-216`
- Modify: `Services/CaseService.cs:156-158` (ProcessGeometry conversion call)
- Test: `foamscript.Tests/Unit/NewStudyHandlerTests.cs`

**Step 1: Write test**

```csharp
[Fact]
public void StudyConfig_ShouldNot_HaveConversionProperties()
{
    var props = typeof(StudyConfig).GetProperties();
    props.Should().NotContain(p => p.Name == "InputUnits");
    props.Should().NotContain(p => p.Name == "MeshSize");
}
```

**Step 2: Run test — should FAIL**

**Step 3: Refactor**

The `new-study` command still needs to handle STEP input for convenience (users shouldn't be forced to run two commands if they don't need unit conversion). But the conversion parameters should use sensible defaults hardcoded at the conversion site, not carried through StudyConfig.

In `CaseService.ProcessGeometry`:
- Keep the STEP→STL conversion path (lines 149-165)
- Use default `meshSize: 1.0` and `inputUnits: "m"` (same defaults as the `convert` command)
- Remove `config.MeshSize`, `config.InputUnits`, `config.FeatureAngle` references
- If a user needs non-meter STEP files, they run `foamscript convert` first, then pass the resulting STL to `new-study`

Remove from `StudyConfig`: `InputUnits`, `MeshSize`, `FeatureAngle`
Remove from `NewStudyModel`: `--input-units`, `--mesh-size`, `--feature-angle` options
Remove from `NewStudyHandler`: config mapping and display for these fields

**Step 4: Fix compilation, run all tests**

**Step 5: Commit**

```
refactor: decouple geometry conversion params from new-study (use convert command)
```

---

## Phase 3: Make Physics Parameters Template-Driven

This is the core refactoring. Every physics value that varies between templates moves from C# defaults to TEMPLATE.json defaults, using the nullable CLI pattern already proven for refinement levels.

### Task 3.1: Extend TEMPLATE.json Schema for New Parameters

**Files:**
- Modify: `Templates/external_disc_rotatingwall_steady/TEMPLATE.json`
- Modify: `Templates/external_airfoil_static_steady/TEMPLATE.json`
- Modify: `Models/TemplateMetadata.cs` (DomainConfig gets new fields)

**Step 1: Add new parameters to both TEMPLATE.json files**

Disc template additions:
```json
"parameters": {
    "velocity": { "required": true, "description": "Freestream velocity (m/s)" },
    "rpm": { "required": true, "description": "Rotational speed (RPM)" },
    "angles": { "required": true, "description": "Angle(s) of attack (degrees)" },
    "refinementMin": { "required": false, "default": 5, "description": "Minimum refinement level" },
    "refinementMax": { "required": false, "default": 6, "description": "Maximum refinement level" },
    "maxIterations": { "required": false, "default": 500, "description": "Maximum solver iterations" },
    "writeInterval": { "required": false, "default": 100, "description": "Write results every N iterations" },
    "nu": { "required": false, "default": 1.5e-5, "description": "Kinematic viscosity (m²/s)" },
    "turbulenceIntensity": { "required": false, "default": 0.01, "description": "Freestream turbulence intensity (fraction)" },
    "endTime": { "required": false, "default": 1.0, "description": "Simulation end time (seconds)" },
    "nOuterCorrectors": { "required": false, "default": 3, "description": "PIMPLE outer corrector iterations" }
}
```

Airfoil template additions:
```json
"parameters": {
    "velocity": { "required": true, "description": "Freestream velocity (m/s)" },
    "angles": { "required": true, "description": "Angle(s) of attack (degrees)" },
    "refinementMin": { "required": false, "default": 2, "description": "Minimum refinement level" },
    "refinementMax": { "required": false, "default": 2, "description": "Maximum refinement level" },
    "maxIterations": { "required": false, "default": 15000, "description": "Maximum solver iterations" },
    "writeInterval": { "required": false, "default": 1000, "description": "Write results every N iterations" },
    "nu": { "required": false, "default": 1.5e-5, "description": "Kinematic viscosity (m²/s)" },
    "turbulenceIntensity": { "required": false, "default": 0.01, "description": "Freestream turbulence intensity (fraction)" },
    "endTime": { "required": false, "default": 15000, "description": "Simulation end time (seconds)" }
}
```

**Step 2: Add domain config fields to TEMPLATE.json**

Both templates already have `domain.upstream/downstream/radial`. Add new template-specific domain fields:

```json
// DomainConfig additions
"domain": {
    "upstream": 5.0,
    "downstream": 10.0,
    "radial": 5.0,
    "margin": 1.1,
    "spanRatio": null,
    "cellsPerRef": 4,
    "featureLevel": null
}
```

- `margin`: multiplier for domain extents beyond tunnel STL (default 1.1 = 10%)
- `spanRatio`: for 2D cases, domain span = STL span × spanRatio (null = use radial, i.e. 3D)
- `cellsPerRef`: base mesh cells per reference length (default 4)
- `featureLevel`: feature edge refinement level (null = use refinementMax for backward compat)

Disc template: `margin: 1.1, spanRatio: null, cellsPerRef: 4, featureLevel: null`
Airfoil template: `margin: 1.1, spanRatio: 0.8, cellsPerRef: 4, featureLevel: 0`

**Step 3: Update DomainConfig model**

In `Models/TemplateMetadata.cs`, add to `DomainConfig`:
```csharp
[JsonPropertyName("margin")]
public double Margin { get; set; } = 1.1;

[JsonPropertyName("spanRatio")]
public double? SpanRatio { get; set; }

[JsonPropertyName("cellsPerRef")]
public int CellsPerRef { get; set; } = 4;

[JsonPropertyName("featureLevel")]
public int? FeatureLevel { get; set; }
```

**Step 4: Run tests — should all still pass (additive schema change)**

**Step 5: Commit**

```
feat: extend TEMPLATE.json schema with domain and physics parameter defaults
```

---

### Task 3.2: Make CLI Physics Parameters Nullable

**Files:**
- Modify: `Models/NewStudyModel.cs` (nu, TI, endTime, etc. become nullable)
- Modify: `Models/StudyConfig.cs` (matching nullable changes)
- Modify: `Handlers/NewStudyHandler.cs` (apply template defaults for new nullable params)

**Step 1: Write test for nullable pattern**

```csharp
[Fact]
public void NewStudyHandler_AppliesTemplateDefault_ForNu_WhenNotSpecified()
{
    // Setup template with nu default
    var metadata = new TemplateMetadata
    {
        Parameters = new Dictionary<string, ParameterDef>
        {
            ["nu"] = new ParameterDef { Required = false, Default = 1.5e-5 }
        }
    };
    // Verify the handler applies 1.5e-5 when CLI nu is null
    // (follows same pattern as refinementMin/Max tests)
}
```

**Step 2: Make the following nullable in NewStudyModel**

| Property | Current | New | Reason |
|----------|---------|-----|--------|
| `Nu` | `double = 1.5e-5` | `double?` | Template default |
| `TurbulenceIntensity` | `double = 0.01` | `double?` | Template default |
| `EndTime` | `double = 1.0` | `double?` | Template default |
| `NOuterCorrectors` | `int = 3` | `int?` | Template default |
| `MaxIterations` | `int = 500` | `int?` | Template default |
| `WriteInterval` | `int = 100` | `int?` | Template default |

**Step 3: Make matching changes in StudyPhysicsConfig**

```csharp
public double? Nu { get; set; }
public double? TurbulenceIntensity { get; set; }
public double? EndTime { get; set; }
public int? NOuterCorrectors { get; set; }
public int? MaxIterations { get; set; }
public int? WriteInterval { get; set; }
```

**Step 4: Extend template default application in NewStudyHandler**

Add cases to the existing switch block (lines 166-180):
```csharp
case "nu" when model.Nu == null:
    config.Physics.Nu = GetDoubleFromDefault(paramDef.Default);
    break;
case "turbulenceintensity" when model.TurbulenceIntensity == null:
    config.Physics.TurbulenceIntensity = GetDoubleFromDefault(paramDef.Default);
    break;
case "endtime" when model.EndTime == null:
    config.Physics.EndTime = GetDoubleFromDefault(paramDef.Default);
    break;
case "noutercorrectors" when model.NOuterCorrectors == null:
    config.Physics.NOuterCorrectors = GetIntFromDefault(paramDef.Default);
    break;
case "maxiterations" when model.MaxIterations == null:
    config.Physics.MaxIterations = GetIntFromDefault(paramDef.Default);
    break;
case "writeinterval" when model.WriteInterval == null:
    config.Physics.WriteInterval = GetIntFromDefault(paramDef.Default);
    break;
```

**Step 5: Add null-coalescing fallbacks in CaseService.CalculateTemplateContext**

For each nullable physics property, provide a last-resort fallback:
```csharp
turbulence_intensity = physics.TurbulenceIntensity ?? 0.01,
max_iterations = physics.MaxIterations ?? 500,
write_interval = physics.WriteInterval ?? 100,
nu_tilda = 3.0 * (physics.Nu ?? 1.5e-5),
```

These fallbacks should rarely hit — the template default application in the handler runs first. But they prevent NullReferenceException if both CLI and template are missing the value.

**Step 6: Fix all compilation errors, update existing tests**

Many existing tests construct `StudyPhysicsConfig` with non-nullable assumptions. Update them to use the new nullable types.

**Step 7: Run all tests**

**Step 8: Commit**

```
feat: make physics parameters nullable with template-driven defaults
```

---

### Task 3.3: Move Domain Calculation Parameters to Template Metadata

**Files:**
- Modify: `Services/CaseService.cs:268-344` (CalculateTemplateContext)
- Modify: `Services/CaseService.cs:98-104` (spanHalf calculation at call site)
- Modify: `Handlers/NewStudyHandler.cs` (pass template metadata to CaseService)
- Test: `foamscript.Tests/Unit/CaseServiceTests.cs`

**Step 1: Write tests for template-driven domain parameters**

```csharp
[Fact]
public void CalculateTemplateContext_UsesSpanRatio_FromDomainConfig()
{
    var domain = new StudyDomainConfig();
    var templateDomain = new DomainConfig
    {
        Upstream = 5.0, Downstream = 10.0, Radial = 5.0,
        SpanRatio = 0.8, Margin = 1.1, CellsPerRef = 4
    };
    var physics = new StudyPhysicsConfig { RefinementLevelMax = 2 };

    var context = CaseService.CalculateTemplateContext(
        ux: 20.0, uz: 0.0, omegaRotation: 0.0,
        cores: 1, refLength: 1.0, aref: 1.0,
        physics: physics, domain: domain,
        templateDomain: templateDomain,
        stlSpanHalf: 0.5);

    // domain_span_half should be 0.5 * 0.8 = 0.4 (not 0.5 * 1.1 = 0.55)
    var dict = GetContextDict(context);
    ((double)dict["domain_span_half"]).Should().BeApproximately(0.4, 0.001);
}

[Fact]
public void CalculateTemplateContext_UsesFeatureLevel_FromDomainConfig()
{
    var templateDomain = new DomainConfig
    {
        Upstream = 5.0, Downstream = 10.0, Radial = 5.0,
        FeatureLevel = 0
    };
    var physics = new StudyPhysicsConfig { RefinementLevelMax = 6 };

    var context = CaseService.CalculateTemplateContext(
        ux: 27.0, uz: 0.0, omegaRotation: 0.0,
        cores: 4, refLength: 0.21, aref: 0.0346,
        physics: physics, domain: new StudyDomainConfig(),
        templateDomain: templateDomain,
        stlSpanHalf: 0.0);

    var dict = GetContextDict(context);
    ((int)dict["feature_level"]).Should().Be(0); // Not 6!
}
```

**Step 2: Run tests — should FAIL**

**Step 3: Refactor CalculateTemplateContext**

New signature:
```csharp
internal static object CalculateTemplateContext(double ux, double uz, double omegaRotation,
    int cores, double refLength, double aref,
    StudyPhysicsConfig physics, StudyDomainConfig domain,
    DomainConfig templateDomain, double stlSpanHalf = 0.0)
```

Replace hardcoded values with template domain config:
```csharp
// Domain extents — margin from template (was hardcoded 1.1)
var margin = templateDomain.Margin;
var domainUpstream = templateDomain.Upstream * refLength * margin;
var domainDownstream = templateDomain.Downstream * refLength * margin;
var domainRadial = templateDomain.Radial * refLength * margin;

// Base mesh density — cells per reference length from template (was hardcoded 4)
var cellsPerRef = templateDomain.CellsPerRef;

// Feature level — from template, fallback to refinementMax (was always refinementMax)
var featureLevel = templateDomain.FeatureLevel ?? (physics.RefinementLevelMax ?? 0);

// Span half — from template spanRatio if set (was hardcoded 0.8/1.1)
var spanHalf = templateDomain.SpanRatio.HasValue && stlSpanHalf > 0
    ? stlSpanHalf * templateDomain.SpanRatio.Value
    : (stlSpanHalf > 0 ? stlSpanHalf : domainRadial);
```

And in the returned object:
```csharp
feature_level = featureLevel,
domain_span_half = spanHalf,
cells_per_ref = cellsPerRef,  // templates can use this in blockMeshDict
```

**Step 4: Update call sites**

In `CaseService.CreateStudy` (line 100-103), pass template metadata's domain config:
- Load metadata before the foreach loop (already done at line 53)
- Pass `metadata.Domain` (the `DomainConfig` from TEMPLATE.json) through to `CalculateTemplateContext`
- Pass `bbox.Height / 2.0` as raw `stlSpanHalf` — let the template's `spanRatio` handle the scaling

In `CaseService.CreateCase`, add `DomainConfig templateDomain` parameter.

**Step 5: Fix compilation errors, update tests**

**Step 6: Run all tests**

**Step 7: Commit**

```
feat: domain calculation params (margin, spanRatio, cellsPerRef, featureLevel) from template
```

---

### Task 3.4: Clean Up Turbulence Model Context Variables

**Files:**
- Modify: `Services/CaseService.cs` (CalculateTemplateContext)
- Modify: templates as needed

The `nu_tilda` and `mixing_length`/`omega_turbulence` calculations are turbulence-model-specific:
- Disc template uses k-omega SST → needs `k`, `omega_turbulence`
- Airfoil template uses Spalart-Allmaras → needs `nu_tilda`

Currently both are computed unconditionally. This is wasteful but not harmful — Scriban templates simply ignore variables they don't reference. **No action needed here** — computing unused variables costs nothing and avoids template-conditional complexity. Leave as-is.

The `3.0` multiplier for `nu_tilda = 3.0 * nu` is a standard Spalart-Allmaras initialization. The `0.07` mixing length ratio is standard for external aerodynamics. Both are reasonable to keep as C# constants with comments citing the source (e.g., NASA TMR, Spalart & Allmaras 1992).

**No changes in this task.** Document the decision.

**Step 1: Add comments to CaseService clarifying these are model constants**

```csharp
// Spalart-Allmaras initial condition: ~3× molecular viscosity (Spalart & Allmaras, 1992)
nu_tilda = 3.0 * (physics.Nu ?? 1.5e-5),
// Standard mixing length for external aero (AIAA best practice: 7% of reference length)
var mixingLength = 0.07 * refLength;
```

**Step 2: Commit**

```
docs: clarify turbulence model constants in CaseService
```

---

## Phase 4: Update Disc Template TEMPLATE.json

The disc template needs its TEMPLATE.json updated to carry defaults that were previously hardcoded in C# — particularly the domain config additions.

### Task 4.1: Update Disc Template Defaults

**Files:**
- Modify: `Templates/external_disc_rotatingwall_steady/TEMPLATE.json`

**Step 1: Update TEMPLATE.json**

```json
{
  "name": "external_disc_rotatingwall_steady",
  "description": "Steady-state MRF simulation of rotating disc (simpleFoam + rotatingWallVelocity BC)",
  "solver": "simpleFoam",
  "geometry": {
    "type": "disc",
    "stlName": "disc.stl",
    "requiredStlFiles": ["disc.stl", "tunnel.stl"],
    "surfaceOrient": { "outsidePoint": [0, 0, -1] }
  },
  "reference": {
    "dimension": "diameter",
    "areaFormula": "circular"
  },
  "rotation": {
    "enabled": true,
    "requiresRotorZone": true
  },
  "validation": {
    "minSize": 0.18,
    "maxSize": 0.35,
    "warningMessage": "Disc diameter outside expected PDGA range (0.21-0.30m). Ensure geometry is in meters."
  },
  "parameters": {
    "velocity": { "required": true, "description": "Freestream velocity (m/s)" },
    "rpm": { "required": true, "description": "Rotational speed (RPM)" },
    "angles": { "required": true, "description": "Angle(s) of attack (degrees)" },
    "refinementMin": { "required": false, "default": 5, "description": "Minimum refinement level" },
    "refinementMax": { "required": false, "default": 6, "description": "Maximum refinement level" },
    "maxIterations": { "required": false, "default": 500, "description": "Maximum solver iterations" },
    "writeInterval": { "required": false, "default": 100, "description": "Write results every N iterations" },
    "nu": { "required": false, "default": 1.5e-5, "description": "Kinematic viscosity (m²/s)" },
    "turbulenceIntensity": { "required": false, "default": 0.01, "description": "Freestream turbulence intensity (fraction)" },
    "endTime": { "required": false, "default": 1.0, "description": "Simulation end time (seconds)" },
    "nOuterCorrectors": { "required": false, "default": 3, "description": "PIMPLE outer corrector iterations" }
  },
  "domain": {
    "upstream": 5.0,
    "downstream": 10.0,
    "radial": 5.0,
    "margin": 1.1,
    "spanRatio": null,
    "cellsPerRef": 4,
    "featureLevel": null
  },
  "meshPipeline": [
    { "command": "blockMesh", "args": "-case {caseDir}" },
    { "command": "surfaceOrient", "args": "{geometryStlPath} \"({outsidePoint})\" {geometryStlPath}", "optional": true },
    { "command": "surfaceFeatureExtract", "args": "-case {caseDir}" },
    { "command": "decomposePar", "args": "-case {caseDir}", "parallelOnly": true },
    { "command": "snappyHexMesh", "args": "-case {caseDir} -overwrite", "parallel": true },
    { "command": "reconstructParMesh", "args": "-case {caseDir} -constant", "parallelOnly": true },
    { "command": "checkMesh", "args": "-case {caseDir}" }
  ],
  "solvePipeline": [
    { "command": "decomposePar", "args": "-case {caseDir} -force", "parallelOnly": true },
    { "command": "simpleFoam", "args": "-case {caseDir}", "parallel": true },
    { "command": "reconstructPar", "args": "-case {caseDir} -latestTime", "parallelOnly": true }
  ],
  "results": {
    "type": "forceCoefficients",
    "dataFile": "postProcessing/forces/0/coefficient.dat",
    "columns": { "Cd": 1, "Cl": 4, "CmPitch": 7 }
  },
  "report": {
    "template": "report/report.html",
    "standard": "AIAA"
  }
}
```

**Step 2: Update airfoil template similarly**

```json
"domain": {
    "upstream": 5.0,
    "downstream": 10.0,
    "radial": 5.0,
    "margin": 1.1,
    "spanRatio": 0.8,
    "cellsPerRef": 4,
    "featureLevel": 0
}
```

**Step 3: Build and run tests to verify JSON deserialization**

**Step 4: Commit**

```
feat: disc and airfoil templates carry full parameter defaults in TEMPLATE.json
```

---

## Phase 5: Update Documentation

### Task 5.1: Update Docs/Commands.md

**Files:**
- Modify: `Docs/Commands.md`

Document:
- `convert` command is the recommended way to handle non-meter geometry
- `new-study` no longer accepts `--input-units`, `--mesh-size`, `--feature-angle`, `--rotor-radius-scale`, `--rotor-height-scale`, `--mesh-resolution`
- New nullable parameters follow template defaults when not specified
- Show example workflow: `foamscript convert model.step model.stl -u mm` → `foamscript new-study -t airfoil -s model.stl ...`

### Task 5.2: Update ExecutiveSummary.md

**Files:**
- Modify: `Docs/ExecutiveSummary.md`

Update test count, document the template parameter audit refactoring.

### Task 5.3: Commit

```
docs: update Commands.md and ExecutiveSummary.md for parameter audit changes
```

---

## Phase 6: E2E Validation

### Task 6.1: Disc Template Regression

Run the existing disc golf E2E test on Linux to verify the disc template still produces correct results after all refactoring.

### Task 6.2: Airfoil Template Validation

Run the airfoil template E2E test with the corrected spanRatio (0.8) to verify snappyHexMesh now creates the airfoil patch correctly.

---

## Execution Order

| Phase | Tasks | Estimated Changes | Risk |
|-------|-------|-------------------|------|
| 1 | Delete legacy rotor/AMI | ~80 lines removed | Low (dead code) |
| 2 | Decouple geometry conversion | ~30 lines removed | Low (convert cmd exists) |
| 3 | Template-driven physics params | ~120 lines changed | Medium (many test updates) |
| 4 | Update template JSONs | ~40 lines added | Low (additive) |
| 5 | Documentation | N/A | None |
| 6 | E2E validation | N/A | Medium (may find gaps) |

**Build and test between every commit. Current baseline: 241 tests, 0 warnings.**
