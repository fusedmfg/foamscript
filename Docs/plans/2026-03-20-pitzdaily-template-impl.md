# pitzDaily Template Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Create foamscript's first geometry-free (T1) template — `internal_duct_static_steady` — based on OpenFOAM's `incompressible/simpleFoam/pitzDaily` tutorial, with E2E validation against the original tutorial results.

**Architecture:** The pitzDaily template requires making geometry input (--model-source) and angle sweep (--angles) optional in the pipeline. The template defines its own mesh via blockMeshDict (no STL). This is a foundational change that unlocks ~95 T1 tutorials. The template renders Scriban files for 0/, constant/, system/ directories, parameterizing flow conditions while keeping the backward-facing step geometry fixed.

**Tech Stack:** .NET 10, xUnit + Moq + FluentAssertions, Scriban templating, OpenFOAM v2512

**Validation Strategy:** Run the original pitzDaily tutorial on Linux, then run the foamscript-generated version. Compare residual convergence and velocity/pressure fields at key locations. Acceptance: reattachment length within 5% of reference (x/H ≈ 6.0 for kEpsilon at Re=47,000).

---

## Task 1: Add "none" Geometry Type to TemplateMetadata

Support templates that have no geometry requirement — no STL input, no bounding box, no reference dimension from geometry.

**Files:**
- Modify: `Models/TemplateMetadata.cs`
- Test: `foamscript.Tests/Services/TemplateMetadataServiceTests.cs`

**Step 1: Write the failing test**

Add a test that loads a TEMPLATE.json with `"geometry": { "type": "none" }` and verifies it deserializes correctly with no requiredStlFiles.

```csharp
[Fact]
public void LoadMetadata_GeometryTypeNone_LoadsWithoutStlRequirements()
{
    // Arrange
    var dir = CreateTempDir();
    var json = @"{
        ""name"": ""internal_duct_static_steady"",
        ""description"": ""Backward-facing step (pitzDaily)"",
        ""solver"": ""simpleFoam"",
        ""geometry"": { ""type"": ""none"" },
        ""reference"": { ""dimension"": ""fixed"", ""areaFormula"": ""none"" },
        ""rotation"": { ""enabled"": false },
        ""parameters"": {
            ""velocity"": { ""required"": false, ""default"": 10.0, ""description"": ""Inlet velocity (m/s)"" }
        },
        ""domain"": { ""upstream"": 0, ""downstream"": 0, ""radial"": 0 },
        ""meshPipeline"": [{ ""command"": ""blockMesh"", ""args"": ""-case {caseDir}"" }],
        ""solvePipeline"": [{ ""command"": ""simpleFoam"", ""args"": ""-case {caseDir}"" }],
        ""results"": { ""type"": ""fieldData"", ""dataFile"": """" }
    }";
    File.WriteAllText(Path.Combine(dir, "TEMPLATE.json"), json);

    // Act
    var metadata = _service.LoadMetadata(dir);

    // Assert
    metadata.Geometry.Type.Should().Be("none");
    metadata.RequiredStlFiles.Should().BeEmpty();
    metadata.Rotation.Enabled.Should().BeFalse();
}
```

**Step 2: Run test to verify it fails**

Run: `dotnet test foamscript.Tests --filter "LoadMetadata_GeometryTypeNone" -v n`
Expected: FAIL — RequiredStlFiles may throw NullReferenceException if requiredStlFiles is null in JSON

**Step 3: Update TemplateMetadata to handle null requiredStlFiles**

In `Models/TemplateMetadata.cs`, ensure `RequiredStlFiles` accessor returns empty array when `Geometry.RequiredStlFiles` is null:

```csharp
// In the backward-compat accessor (around line 60):
public string[] RequiredStlFiles => Geometry?.RequiredStlFiles ?? Array.Empty<string>();
```

**Step 4: Run test to verify it passes**

Run: `dotnet test foamscript.Tests --filter "LoadMetadata_GeometryTypeNone" -v n`
Expected: PASS

**Step 5: Commit**

```bash
git add Models/TemplateMetadata.cs foamscript.Tests/Services/TemplateMetadataServiceTests.cs
git commit -m "feat: support geometry type 'none' for templates without STL input"
```

---

## Task 2: Make --model-source and --angles Optional Based on Template

Templates with `geometry.type: "none"` don't need `--model-source`. Templates where `angles` is not a required parameter don't need `--angles`. The handler should check TEMPLATE.json before enforcing these.

**Files:**
- Modify: `Handlers/NewStudyHandler.cs`
- Test: `foamscript.Tests/Handlers/NewStudyHandlerTests.cs`

**Step 1: Write the failing test**

Add a test that creates a valid geometry-free template and verifies new-study succeeds without --model-source and --angles:

```csharp
[Fact]
public void Handle_GeometryNoneTemplate_SucceedsWithoutModelSourceAndAngles()
{
    // Arrange
    // Create a temp template dir with TEMPLATE.json that has geometry.type = "none"
    // and no required angles parameter
    var templateDir = CreateGeometryNoneTemplate();
    var model = new NewStudyModel
    {
        TemplatePath = templateDir,
        ProjectName = "PitzDailyTest",
        OutputDir = _tempOutputDir,
        Velocity = 10.0,
        // ModelSource intentionally null
        // Angles intentionally null
    };

    _mockCaseService
        .Setup(cs => cs.CreateStudy(It.IsAny<StudyConfig>(), It.IsAny<string>()))
        .Returns(new StudyResult { IsSuccess = true, StudyName = "PitzDailyTest", StudyDir = _tempOutputDir, Cases = new List<CaseInfo>() });

    // Act
    var result = _handler.Handle(model);

    // Assert
    result.Should().Be(0);
    _mockCaseService.Verify(cs => cs.CreateStudy(It.IsAny<StudyConfig>(), It.IsAny<string>()), Times.Once);
}
```

**Step 2: Run test to verify it fails**

Run: `dotnet test foamscript.Tests --filter "Handle_GeometryNoneTemplate_SucceedsWithoutModelSourceAndAngles" -v n`
Expected: FAIL — handler currently requires --model-source at line 42 and --angles at line 43

**Step 3: Modify NewStudyHandler to defer --model-source and --angles validation**

The key insight: we need to load the template metadata BEFORE validating required CLI args, so we know which args are actually required for this template. Refactor `Handle()`:

1. Move template resolution earlier (before CLI arg validation)
2. Load TEMPLATE.json to get geometry type and parameter requirements
3. Only require --model-source if `geometry.type != "none"`
4. Only require --angles if the template's parameters define angles as required

In `NewStudyHandler.cs`, modify the CLI validation block (lines 37-53):

```csharp
// Validate required CLI args — but first check template requirements
var missing = new List<string>();
if (string.IsNullOrEmpty(model.TemplatePath)) missing.Add("--template (-t)");
if (string.IsNullOrEmpty(model.ProjectName)) missing.Add("--project-name (-n)");
if (string.IsNullOrEmpty(model.OutputDir)) missing.Add("--output-dir (-o)");

// Defer --model-source and --angles validation until after template metadata is loaded
// (geometry-free templates don't need them)
```

Then after template metadata is loaded (around line 135), add conditional validation:

```csharp
// Check if template requires geometry input
if (model.ConfigFile == null && metadata.Geometry?.Type != "none")
{
    if (string.IsNullOrEmpty(model.ModelSource))
    {
        Console.WriteLine("✗ Missing required argument: --model-source (-s)");
        Console.WriteLine($"    Template '{metadata.Name}' requires STL geometry input.");
        return -1;
    }
}

// Check if template requires angles
if (model.ConfigFile == null && metadata.Parameters.TryGetValue("angles", out var anglesDef) && anglesDef.Required)
{
    if (string.IsNullOrEmpty(model.Angles))
    {
        Console.WriteLine("✗ Missing required argument: --angles (-a)");
        Console.WriteLine($"    Template '{metadata.Name}' requires angle of attack specification.");
        return -1;
    }
}
```

**Step 4: Also update LoadStudyConfig (JSON path) to not require modelSource/angles**

In `LoadStudyConfig()` (line 347-348), make modelSource and angles conditional:

```csharp
// Only require modelSource if we can determine the template needs it
// For JSON config, defer to CaseService which checks metadata
if (string.IsNullOrWhiteSpace(config.TemplateName)) missing.Add("templateName");
// modelSource and angles validation deferred to template-aware code
```

**Step 5: Run tests to verify**

Run: `dotnet test foamscript.Tests --filter "NewStudyHandler" -v n`
Expected: All tests PASS (existing + new)

**Step 6: Commit**

```bash
git add Handlers/NewStudyHandler.cs foamscript.Tests/Handlers/NewStudyHandlerTests.cs
git commit -m "feat: make --model-source and --angles optional based on template geometry type"
```

---

## Task 3: Make CaseService Handle Geometry-Free Templates

CaseService.CreateStudy() currently always calls ProcessGeometry() and requires angles. For geometry-free templates, skip geometry processing and support single-case creation without angle sweep.

**Files:**
- Modify: `Services/CaseService.cs`
- Test: `foamscript.Tests/Services/CaseServiceTests.cs`

**Step 1: Write failing tests**

```csharp
[Fact]
public void CreateStudy_GeometryNone_SkipsGeometryProcessing()
{
    // Arrange
    var templateDir = CreateMinimalGeometryNoneTemplate(); // geometry.type = "none"
    var config = CreateDefaultConfig();
    config.ModelSource = null; // no STL
    config.Angles = "0"; // single "angle" for case creation
    config.Velocity = 10.0;

    // Act
    var result = _caseService.CreateStudy(config, templateDir);

    // Assert
    result.IsSuccess.Should().BeTrue();
    result.Cases.Should().HaveCount(1);
    // No geometry directory should contain STL files
    var geomDir = Path.Combine(result.StudyDir!, "geometry");
    Directory.GetFiles(geomDir, "*.stl").Should().BeEmpty();
}

[Fact]
public void CreateStudy_GeometryNone_NullAngles_CreatesSingleCase()
{
    // Arrange — template with no angles parameter
    var templateDir = CreateMinimalGeometryNoneTemplate();
    var config = CreateDefaultConfig();
    config.ModelSource = null;
    config.Angles = null; // No angles at all
    config.Velocity = 10.0;

    // Act
    var result = _caseService.CreateStudy(config, templateDir);

    // Assert
    result.IsSuccess.Should().BeTrue();
    result.Cases.Should().HaveCount(1);
    result.Cases[0].AngleOfAttack.Should().Be(0);
}
```

**Step 2: Run tests to verify they fail**

Run: `dotnet test foamscript.Tests --filter "GeometryNone" -v n`
Expected: FAIL — CreateStudy calls ProcessGeometry which requires ModelSource file

**Step 3: Modify CreateStudy to skip geometry for "none" type**

In `CaseService.cs`, modify `CreateStudy()`:

```csharp
// Load template metadata
var metadata = _metadataService.LoadMetadata(templatePath);

// Parse angles (default to single 0° case if null/empty)
double[] angles;
if (string.IsNullOrEmpty(config.Angles))
{
    angles = new[] { 0.0 };
}
else
{
    angles = ParseAngles(config.Angles);
    if (angles.Length == 0)
    {
        result.IsSuccess = false;
        result.ErrorMessage = $"Invalid angles format: {config.Angles}";
        return result;
    }
}

// Create study directory
Directory.CreateDirectory(result.StudyDir);
var geometryDir = Path.Combine(result.StudyDir, "geometry");
Directory.CreateDirectory(geometryDir);

double refLength;
double aref;
BoundingBox? bbox = null;
double spanHalf = 0.0;

if (metadata.Geometry?.Type == "none")
{
    // Geometry-free template: use fixed reference values from template metadata
    refLength = metadata.Reference?.FixedLength ?? 1.0;
    aref = metadata.Reference?.FixedArea ?? 1.0;
}
else
{
    // Geometry-based template: process STL, extract bbox, calculate reference
    var geometryResult = ProcessGeometry(geometryDir, config, metadata);
    if (!geometryResult.Success)
    {
        result.IsSuccess = false;
        result.ErrorMessage = geometryResult.ErrorMessage;
        return result;
    }
    refLength = geometryResult.RefLength;
    bbox = geometryResult.BoundingBox!;
    aref = TemplateMetadataService.CalculateReferenceArea(refLength, bbox, metadata);

    // ... existing validation and spanHalf calculation
}
```

**Step 4: Add FixedLength/FixedArea to ReferenceConfig**

In `Models/TemplateMetadata.cs`, add to ReferenceConfig:

```csharp
public class ReferenceConfig
{
    public string Dimension { get; set; } = "";
    public string AreaFormula { get; set; } = "";
    public double? FixedLength { get; set; }
    public double? FixedArea { get; set; }
}
```

**Step 5: Run tests**

Run: `dotnet test foamscript.Tests -v n`
Expected: ALL PASS

**Step 6: Commit**

```bash
git add Services/CaseService.cs Models/TemplateMetadata.cs foamscript.Tests/Services/CaseServiceTests.cs
git commit -m "feat: skip geometry processing for templates with geometry type 'none'"
```

---

## Task 4: Update Display Output for Geometry-Free Templates

The handler's console output currently always prints "Model source:", "Angles:", "RPM:", and geometry parameters. These should be conditional.

**Files:**
- Modify: `Handlers/NewStudyHandler.cs`

**Step 1: Make display conditional**

Wrap geometry-specific output in conditionals:

```csharp
if (!string.IsNullOrEmpty(config.ModelSource))
    Console.WriteLine($"Model source:     {config.ModelSource}");
if (!string.IsNullOrEmpty(config.Angles))
    Console.WriteLine($"Angles:           {config.Angles}");
Console.WriteLine($"Velocity:         {config.Velocity} m/s");
if (config.Rpm > 0)
    Console.WriteLine($"RPM:              {config.Rpm}");
```

Similarly for the success output — skip AoA/Omega lines for geometry-free templates.

**Step 2: Run full test suite**

Run: `dotnet test foamscript.Tests -v n`
Expected: ALL PASS

**Step 3: Commit**

```bash
git add Handlers/NewStudyHandler.cs
git commit -m "feat: conditional display output for geometry-free templates"
```

---

## Task 5: Create pitzDaily Template Files

Create the template directory with all 14 Scriban-parameterized OpenFOAM files. The geometry (backward-facing step) is fixed in blockMeshDict. Parameterized values: inlet velocity, nu, turbulence model settings, iterations, write interval.

**Files:**
- Create: `Templates/internal_duct_static_steady/TEMPLATE.json`
- Create: `Templates/internal_duct_static_steady/0/U`
- Create: `Templates/internal_duct_static_steady/0/p`
- Create: `Templates/internal_duct_static_steady/0/k`
- Create: `Templates/internal_duct_static_steady/0/epsilon`
- Create: `Templates/internal_duct_static_steady/0/omega`
- Create: `Templates/internal_duct_static_steady/0/nut`
- Create: `Templates/internal_duct_static_steady/0/nuTilda`
- Create: `Templates/internal_duct_static_steady/constant/transportProperties`
- Create: `Templates/internal_duct_static_steady/constant/turbulenceProperties`
- Create: `Templates/internal_duct_static_steady/system/blockMeshDict`
- Create: `Templates/internal_duct_static_steady/system/controlDict`
- Create: `Templates/internal_duct_static_steady/system/fvSchemes`
- Create: `Templates/internal_duct_static_steady/system/fvSolution`
- Create: `Templates/internal_duct_static_steady/system/streamlines`

**Step 1: Create TEMPLATE.json**

This is the template metadata. Key differences from disc/airfoil:
- `geometry.type: "none"` — no STL input
- No rotation
- No angles parameter (not required)
- Mesh pipeline: just blockMesh (no snappyHexMesh)
- Results type: fieldData (no forceCoefficients)

```json
{
  "name": "internal_duct_static_steady",
  "description": "Steady-state backward-facing step (pitzDaily tutorial, simpleFoam + kEpsilon)",
  "solver": "simpleFoam",
  "geometry": {
    "type": "none"
  },
  "reference": {
    "dimension": "fixed",
    "areaFormula": "none",
    "fixedLength": 0.0254,
    "fixedArea": 0.0254
  },
  "rotation": {
    "enabled": false
  },
  "parameters": {
    "velocity": { "required": false, "default": 10.0, "description": "Inlet velocity magnitude (m/s)" },
    "nu": { "required": false, "default": 1e-5, "description": "Kinematic viscosity (m²/s)" },
    "turbulenceIntensity": { "required": false, "default": 0.05, "description": "Inlet turbulence intensity" },
    "maxIterations": { "required": false, "default": 2000, "description": "Maximum solver iterations" },
    "writeInterval": { "required": false, "default": 100, "description": "Write interval (iterations)" },
    "endTime": { "required": false, "default": 2000, "description": "Simulation end time (iterations for steady)" },
    "nOuterCorrectors": { "required": false, "default": 0, "description": "SIMPLE non-orthogonal correctors" },
    "mixingLengthRatio": { "required": false, "default": 0.07, "description": "Mixing length as fraction of step height" },
    "nuTildaMultiplier": { "required": false, "default": 3.0, "description": "nu-tilda initial value multiplier (x nu)" }
  },
  "domain": {
    "upstream": 0,
    "downstream": 0,
    "radial": 0,
    "margin": 1.0
  },
  "meshPipeline": [
    { "command": "blockMesh", "args": "-case {caseDir}" }
  ],
  "solvePipeline": [
    { "command": "decomposePar", "args": "-case {caseDir} -force", "parallelOnly": true },
    { "command": "simpleFoam", "args": "-case {caseDir}", "parallel": true },
    { "command": "reconstructPar", "args": "-case {caseDir} -latestTime", "parallelOnly": true }
  ],
  "results": {
    "type": "fieldData",
    "dataFile": ""
  }
}
```

**Step 2: Create 0/ boundary condition files**

Each file is a faithful copy of the pitzDaily tutorial with Scriban parameterization for inlet values that depend on velocity, nu, and turbulence intensity.

**0/U** — parameterize inlet velocity:
```
FoamFile { version 2.0; format ascii; class volVectorField; object U; }

dimensions      [0 1 -1 0 0 0 0];
internalField   uniform ({{ ux }} 0 0);

boundaryField
{
    inlet       { type fixedValue; value uniform ({{ ux }} 0 0); }
    outlet      { type zeroGradient; }
    upperWall   { type noSlip; }
    lowerWall   { type noSlip; }
    frontAndBack { type empty; }
}
```

**0/p** — no parameterization needed (outlet p=0 is standard):
```
FoamFile { version 2.0; format ascii; class volScalarField; object p; }

dimensions      [0 2 -2 0 0 0 0];
internalField   uniform 0;

boundaryField
{
    inlet       { type zeroGradient; }
    outlet      { type fixedValue; value uniform 0; }
    upperWall   { type zeroGradient; }
    lowerWall   { type zeroGradient; }
    frontAndBack { type empty; }
}
```

**0/k** — parameterize from turbulence intensity and velocity:
```
FoamFile { version 2.0; format ascii; class volScalarField; object k; }

dimensions      [0 2 -2 0 0 0 0];
internalField   uniform {{ k }};

boundaryField
{
    inlet       { type fixedValue; value uniform {{ k }}; }
    outlet      { type zeroGradient; }
    upperWall   { type kqRWallFunction; value uniform {{ k }}; }
    lowerWall   { type kqRWallFunction; value uniform {{ k }}; }
    frontAndBack { type empty; }
}
```

**0/epsilon** — parameterize (epsilon = cmu^0.75 * k^1.5 / mixingLength):
```
FoamFile { version 2.0; format ascii; class volScalarField; object epsilon; }

dimensions      [0 2 -3 0 0 0 0];
internalField   uniform {{ epsilon }};

boundaryField
{
    inlet       { type fixedValue; value uniform {{ epsilon }}; }
    outlet      { type zeroGradient; }
    upperWall   { type epsilonWallFunction; value uniform {{ epsilon }}; }
    lowerWall   { type epsilonWallFunction; value uniform {{ epsilon }}; }
    frontAndBack { type empty; }
}
```

Note: epsilon must be calculated in CalculateTemplateContext. Add: `epsilon = cmu^0.75 * k^1.5 / mixingLength`

**0/omega** — parameterize from template context:
```
FoamFile { version 2.0; format ascii; class volScalarField; object omega; }

dimensions      [0 0 -1 0 0 0 0];
internalField   uniform {{ omega_turbulence }};

boundaryField
{
    inlet       { type fixedValue; value uniform {{ omega_turbulence }}; }
    outlet      { type zeroGradient; }
    upperWall   { type omegaWallFunction; value uniform {{ omega_turbulence }}; }
    lowerWall   { type omegaWallFunction; value uniform {{ omega_turbulence }}; }
    frontAndBack { type empty; }
}
```

**0/nut** — no parameterization:
```
FoamFile { version 2.0; format ascii; class volScalarField; object nut; }

dimensions      [0 2 -1 0 0 0 0];
internalField   uniform 0;

boundaryField
{
    inlet       { type calculated; value uniform 0; }
    outlet      { type calculated; value uniform 0; }
    upperWall   { type nutkWallFunction; value uniform 0; }
    lowerWall   { type nutkWallFunction; value uniform 0; }
    frontAndBack { type empty; }
}
```

**0/nuTilda** — parameterize for Spalart-Allmaras option:
```
FoamFile { version 2.0; format ascii; class volScalarField; object nuTilda; }

dimensions      [0 2 -1 0 0 0 0];
internalField   uniform {{ nu_tilda }};

boundaryField
{
    inlet       { type fixedValue; value uniform {{ nu_tilda }}; }
    outlet      { type zeroGradient; }
    upperWall   { type zeroGradient; }
    lowerWall   { type zeroGradient; }
    frontAndBack { type empty; }
}
```

**Step 3: Create constant/ files**

**constant/transportProperties** — parameterize nu:
```
FoamFile { version 2.0; format ascii; class dictionary; object transportProperties; }

transportModel  Newtonian;
nu              {{ nu }};
```

**constant/turbulenceProperties** — fixed (kEpsilon default per tutorial):
```
FoamFile { version 2.0; format ascii; class dictionary; object turbulenceProperties; }

simulationType  RAS;

RAS
{
    RASModel        kEpsilon;
    turbulence      on;
    printCoeffs     on;
}
```

**Step 4: Create system/ files**

**system/controlDict** — parameterize iterations and write interval:
```
FoamFile { version 2.0; format ascii; class dictionary; object controlDict; }

application     simpleFoam;
startFrom       startTime;
startTime       0;
stopAt          endTime;
endTime         {{ end_time }};
deltaT          1;
writeControl    timeStep;
writeInterval   {{ write_interval }};
purgeWrite      0;
writeFormat     ascii;
writePrecision  8;
writeCompression off;
timeFormat      general;
timePrecision   6;
runTimeModifiable true;

functions
{
    #includeFunc streamlines
}
```

**system/fvSchemes** — fixed (faithful to tutorial):
```
FoamFile { version 2.0; format ascii; class dictionary; object fvSchemes; }

ddtSchemes      { default steadyState; }

gradSchemes     { default Gauss linear; }

divSchemes
{
    default         none;
    div(phi,U)      bounded Gauss linearUpwind grad(U);
    div(phi,k)      bounded Gauss limitedLinear 1;
    div(phi,epsilon) bounded Gauss limitedLinear 1;
    div(phi,omega)  bounded Gauss limitedLinear 1;
    div(phi,nuTilda) bounded Gauss limitedLinear 1;
    div(phi,R)      bounded Gauss limitedLinear 1;
    div(R)          Gauss linear;
    div((nuEff*dev2(T(grad(U))))) Gauss linear;
}

laplacianSchemes { default Gauss linear corrected; }

interpolationSchemes { default linear; }

snGradSchemes   { default corrected; }

wallDist        { method meshWave; }
```

**system/fvSolution** — parameterize nNonOrthogonalCorrectors:
```
FoamFile { version 2.0; format ascii; class dictionary; object fvSolution; }

solvers
{
    p
    {
        solver          GAMG;
        tolerance       1e-06;
        relTol          0.1;
        smoother        GaussSeidel;
    }

    "(U|k|epsilon|omega|f|v2|nuTilda)"
    {
        solver          smoothSolver;
        smoother        symGaussSeidel;
        tolerance       1e-05;
        relTol          0.1;
    }
}

SIMPLE
{
    nNonOrthogonalCorrectors {{ n_outer_correctors }};
    consistent      yes;

    residualControl
    {
        p               1e-2;
        U               1e-3;
        "(k|epsilon|omega|f|v2|nuTilda)" 1e-3;
    }
}

relaxationFactors
{
    equations
    {
        U               0.9;
        ".*"            0.9;
    }
}
```

**system/blockMeshDict** — FIXED geometry (not parameterized), faithful to tutorial:
```
FoamFile { version 2.0; format ascii; class dictionary; object blockMeshDict; }

scale   0.001;

vertices
(
    (-20.6 0     -0.5)
    (-20.6 25.4  -0.5)
    (0     -25.4 -0.5)
    (0     0     -0.5)
    (0     25.4  -0.5)
    (206   -25.4 -0.5)
    (206   0     -0.5)
    (206   25.4  -0.5)
    (290   -25.4 -0.5)
    (290   0     -0.5)
    (290   25.4  -0.5)
    (-20.6 0     0.5)
    (-20.6 25.4  0.5)
    (0     -25.4 0.5)
    (0     0     0.5)
    (0     25.4  0.5)
    (206   -25.4 0.5)
    (206   0     0.5)
    (206   25.4  0.5)
    (290   -25.4 0.5)
    (290   0     0.5)
    (290   25.4  0.5)
);

blocks
(
    hex (0 3 4 1 11 14 15 12)    (18 30 1) simpleGrading (1 1 1)
    hex (2 5 6 3 13 16 17 14)    (180 27 1) simpleGrading (1 1 1)
    hex (3 6 7 4 14 17 18 15)    (180 30 1) simpleGrading (1 1 1)
    hex (5 8 9 6 16 19 20 17)    (25 27 1) simpleGrading (1 1 1)
    hex (6 9 10 7 17 20 21 18)   (25 30 1) simpleGrading (1 1 1)
);

boundary
(
    inlet
    {
        type patch;
        faces ((0 1 12 11));
    }
    outlet
    {
        type patch;
        faces
        (
            (8 9 20 19)
            (9 10 21 20)
        );
    }
    upperWall
    {
        type wall;
        faces
        (
            (1 4 15 12)
            (4 7 18 15)
            (7 10 21 18)
        );
    }
    lowerWall
    {
        type wall;
        faces
        (
            (0 3 14 11)
            (3 2 13 14)
            (2 5 16 13)
            (5 8 19 16)
        );
    }
    frontAndBack
    {
        type empty;
        faces
        (
            (0 3 4 1)
            (2 5 6 3)
            (3 6 7 4)
            (5 8 9 6)
            (6 9 10 7)
            (11 14 15 12)
            (13 16 17 14)
            (14 17 18 15)
            (16 19 20 17)
            (17 20 21 18)
        );
    }
);
```

**system/streamlines** — fixed (reference to OpenFOAM function object):
```
FoamFile { version 2.0; format ascii; class dictionary; object streamlines; }

type            streamlines;
libs            (fieldFunctionObjects);
writeControl    writeTime;

// Seeding method
seedSampleSet
{
    type            uniform;
    axis            xyz;
    start           (-0.0205 0.001 0.00001);
    end             (-0.0205 0.0251 0.00001);
    nPoints         10;
}

cloud           particleTracks;
nSubCycle       5;
direction       forward;
lifeTime        10000;
trackLength     1e-3;

fields          (p k U);
```

**Step 5: Add epsilon to CalculateTemplateContext**

In `Services/CaseService.cs`, add epsilon calculation to the context object:

```csharp
var epsilon = Math.Pow(cmu, 0.75) * Math.Pow(k, 1.5) / mixingLength;

return new
{
    // ... existing fields ...
    epsilon = epsilon,
};
```

**Step 6: Commit**

```bash
git add Templates/internal_duct_static_steady/
git add Services/CaseService.cs
git commit -m "feat: add internal_duct_static_steady template (pitzDaily)"
```

---

## Task 6: Template Rendering Tests

Verify that the pitzDaily template renders correctly with default parameters.

**Files:**
- Test: `foamscript.Tests/Services/TemplateServiceTests.cs`

**Step 1: Write rendering test**

```csharp
[Fact]
public void ProcessTemplate_PitzDaily_RendersInletVelocity()
{
    // Arrange
    var templateDir = Path.Combine(GetTemplatesDir(), "internal_duct_static_steady");
    var (_, outputDir) = CreateTempDirs();
    var context = new
    {
        ux = 10.0, uz = 0.0, k = 0.00375, epsilon = 0.014855,
        omega_turbulence = 440.15, nu = 1e-5, nu_tilda = 3e-5,
        end_time = 2000, write_interval = 100, max_iterations = 2000,
        n_outer_correctors = 0,
        // geometry-free fields unused but present for compat
        ref_length = 0.0254, aref = 0.0254, mag_u_inf = 10.0,
        refinement_level_min = 0, refinement_level_max = 0,
        feature_level = 0, cores = 1,
        domain_upstream = 0.0, domain_downstream = 0.0,
        domain_radial = 0.0, domain_span_half = 0.0,
        omega_rotation = 0.0, disc_diameter = 0.0254,
        cmu = 0.09, turbulence_intensity = 0.05,
        mixing_length = 0.001778, p = 0
    };

    // Act
    _service.ProcessTemplate(templateDir, outputDir, context);

    // Assert
    var uFile = File.ReadAllText(Path.Combine(outputDir, "0", "U"));
    uFile.Should().Contain("uniform (10 0 0)");

    var transportFile = File.ReadAllText(Path.Combine(outputDir, "constant", "transportProperties"));
    transportFile.Should().Contain("1E-05");

    var controlDict = File.ReadAllText(Path.Combine(outputDir, "system", "controlDict"));
    controlDict.Should().Contain("endTime         2000");
}
```

**Step 2: Run test**

Run: `dotnet test foamscript.Tests --filter "PitzDaily_RendersInletVelocity" -v n`

**Step 3: Commit**

```bash
git add foamscript.Tests/Services/TemplateServiceTests.cs
git commit -m "test: add pitzDaily template rendering tests"
```

---

## Task 7: Build and Test Locally

Verify the full build succeeds and all tests pass with zero warnings.

**Step 1: Build**

Run: `dotnet build`
Expected: Build succeeded. 0 Warning(s). 0 Error(s).

**Step 2: Run all tests**

Run: `dotnet test foamscript.Tests -v n`
Expected: All tests pass (271 existing + new tests)

**Step 3: Commit any fixes**

---

## Task 8: E2E Validation — Run Original pitzDaily Tutorial

SSH to Linux and run the original OpenFOAM pitzDaily tutorial to establish baseline results.

**Step 1: Copy tutorial and run**

```bash
SSH_CMD="ssh -i \$HOME/.ssh/id_ed25519_fusiondiscgolf -o StrictHostKeyChecking=no -o IdentitiesOnly=yes trboyden@192.168.1.223"

$SSH_CMD 'source /usr/lib/openfoam/openfoam2512/etc/bashrc && \
    cd /home/trboyden/OpenFOAM/trboyden-v2512/run && \
    rm -rf pitzDaily_reference && \
    cp -r $FOAM_TUTORIALS/incompressible/simpleFoam/pitzDaily pitzDaily_reference && \
    cd pitzDaily_reference && \
    blockMesh && \
    simpleFoam'
```

**Step 2: Extract baseline results**

After convergence, extract:
- Final residuals (from log)
- Velocity profile at x/H = 4 (reattachment region) using postProcess -func sample
- Pressure drop between inlet and outlet

**Step 3: Record baseline**

Note: pitzDaily with kEpsilon at Re≈47,000 should show:
- Convergence in ~1000-1500 iterations
- Reattachment length x/H ≈ 6.0 (±0.5)
- Final residuals: p < 1e-4, U < 1e-5

---

## Task 9: E2E Validation — Run foamscript-Generated pitzDaily

Build foamscript on Linux, create a study using the new template, mesh + solve, compare results.

**Step 1: Build foamscript on Linux**

```bash
$SSH_CMD 'cd /home/trboyden/foamscript && \
    git pull && \
    dotnet build --configuration Release'
```

**Step 2: Create study using new template**

```bash
$SSH_CMD 'source /usr/lib/openfoam/openfoam2512/etc/bashrc && \
    cd /home/trboyden/OpenFOAM/trboyden-v2512/run && \
    dotnet /home/trboyden/foamscript/bin/Release/net10.0/foamscript.dll new-study \
        -t internal_duct_static_steady \
        -n pitzDaily_foamscript \
        -o .'
```

**Step 3: Mesh and solve**

```bash
$SSH_CMD 'source /usr/lib/openfoam/openfoam2512/etc/bashrc && \
    cd /home/trboyden/OpenFOAM/trboyden-v2512/run && \
    dotnet /home/trboyden/foamscript/bin/Release/net10.0/foamscript.dll mesh \
        -d pitzDaily_foamscript && \
    dotnet /home/trboyden/foamscript/bin/Release/net10.0/foamscript.dll solve \
        -d pitzDaily_foamscript'
```

**Step 4: Compare results**

Compare foamscript-generated vs reference:
- **Residual convergence**: Both should converge to same levels within same iteration count (±10%)
- **Velocity at outlet**: Sample U at outlet plane — should match within 2%
- **Pressure drop**: Compare inlet-outlet pressure difference — should match within 5%

Acceptance criteria:
- Residuals converge to same order of magnitude
- Velocity field at outlet matches within 2%
- No OpenFOAM errors or warnings that reference doesn't also have

**Step 5: Document results**

Record comparison in commit message or PR description.

---

## Task 10: Update Documentation

**Files:**
- Modify: `Docs/Commands.md` — add template to list
- Modify: `Docs/ExecutiveSummary.md` — update test count, template count, features

**Step 1: Update Commands.md**

Add `internal_duct_static_steady` to the template listing with its description and parameters.

**Step 2: Update ExecutiveSummary.md**

Update metrics: test count, commit count, template count, open/closed issues.

**Step 3: Close GitHub issue #36**

```bash
gh issue close 36 --comment "Implemented internal_duct_static_steady template based on pitzDaily tutorial. E2E validated against original tutorial — [results summary]."
```

**Step 4: Final commit**

```bash
git add Docs/Commands.md Docs/ExecutiveSummary.md
git commit -m "docs: add internal_duct_static_steady template documentation"
```

---

## Summary of Architecture Changes

This implementation introduces three foundational changes for T1 template support:

1. **`geometry.type: "none"`** — Templates can declare they don't need STL geometry input
2. **Optional --model-source / --angles** — Handler checks template metadata before requiring these CLI args
3. **CaseService geometry bypass** — CreateStudy skips ProcessGeometry for geometry-free templates, uses fixed reference values

These changes unlock ~95 additional tutorials that define geometry entirely in blockMeshDict.
