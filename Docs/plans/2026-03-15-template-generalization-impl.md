# Template Generalization Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Remove all 20 hardcoded disc assumptions from foamscript so every template-specific behavior is driven by TEMPLATE.json.

**Architecture:** Expand TEMPLATE.json schema to include mesh/solve pipelines, parameter declarations, results extraction config, and report config. Refactor C# services from hardcoded logic to generic pipeline executors. Migrate existing disc report into template directory.

**Tech Stack:** .NET 10, System.Text.Json, Scriban, ScottPlot, xUnit + FluentAssertions

**Design doc:** `Docs/plans/2026-03-15-template-generalization-design.md`

---

## Task 1: Expand TemplateMetadata Model

**Files:**
- Modify: `Models/TemplateMetadata.cs`
- Test: `foamscript.Tests/Services/TemplateMetadataServiceTests.cs`

**Step 1: Write failing tests for new schema fields**

Add tests that deserialize TEMPLATE.json with the expanded schema (geometry, parameters, meshPipeline, solvePipeline, results, report sections). These will fail because the model doesn't have the new properties yet.

```csharp
[Fact]
public void LoadMetadata_ExpandedSchema_ParsesAllSections()
{
    var dir = CreateTempDir();
    File.WriteAllText(Path.Combine(dir, "TEMPLATE.json"), """
    {
      "name": "test_template",
      "description": "Test",
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
      "parameters": {
        "velocity": { "required": true, "description": "Freestream velocity (m/s)" },
        "rpm": { "required": true, "description": "Rotational speed" }
      },
      "domain": {
        "upstream": 5.0,
        "downstream": 10.0,
        "radial": 5.0
      },
      "meshPipeline": [
        { "command": "blockMesh", "args": "-case {caseDir}" },
        { "command": "snappyHexMesh", "args": "-case {caseDir} -overwrite", "parallel": true }
      ],
      "solvePipeline": [
        { "command": "decomposePar", "args": "-case {caseDir} -force" },
        { "command": "simpleFoam", "args": "-case {caseDir}", "parallel": true },
        { "command": "reconstructPar", "args": "-case {caseDir} -latestTime" }
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
    """);

    var metadata = _service.LoadMetadata(dir);

    metadata.Name.Should().Be("test_template");
    metadata.Geometry.Type.Should().Be("disc");
    metadata.Geometry.StlName.Should().Be("disc.stl");
    metadata.Geometry.SurfaceOrient.Should().NotBeNull();
    metadata.Geometry.SurfaceOrient!.OutsidePoint.Should().BeEquivalentTo(new[] { 0.0, 0, -1 });
    metadata.Reference.Dimension.Should().Be("diameter");
    metadata.Rotation.Enabled.Should().BeTrue();
    metadata.Parameters.Should().ContainKey("velocity");
    metadata.Parameters["velocity"].Required.Should().BeTrue();
    metadata.MeshPipeline.Should().HaveCount(2);
    metadata.MeshPipeline[1].Parallel.Should().BeTrue();
    metadata.SolvePipeline.Should().HaveCount(3);
    metadata.Results.DataFile.Should().Be("postProcessing/forces/0/coefficient.dat");
    metadata.Results.Columns.Should().ContainKey("Cd");
    metadata.Report.Template.Should().Be("report/report.html");
}

[Fact]
public void LoadMetadata_MissingFile_ThrowsFileNotFoundException()
{
    var dir = CreateTempDir();
    var act = () => _service.LoadMetadata(dir);
    act.Should().Throw<FileNotFoundException>()
        .WithMessage("*TEMPLATE.json*");
}

[Fact]
public void LoadMetadata_NullSurfaceOrient_ParsesAsNull()
{
    var dir = CreateTempDir();
    File.WriteAllText(Path.Combine(dir, "TEMPLATE.json"), """
    {
      "name": "airfoil_test",
      "solver": "simpleFoam",
      "geometry": {
        "type": "airfoil",
        "stlName": "airfoil.stl",
        "requiredStlFiles": ["airfoil.stl"],
        "surfaceOrient": null
      },
      "reference": { "dimension": "chord", "areaFormula": "rectangular" },
      "rotation": { "enabled": false, "requiresRotorZone": false },
      "parameters": {
        "velocity": { "required": true }
      },
      "domain": { "upstream": 5.0, "downstream": 10.0, "radial": 5.0 },
      "meshPipeline": [],
      "solvePipeline": [],
      "results": { "type": "forceCoefficients", "dataFile": "postProcessing/forces/0/coefficient.dat", "columns": { "Cd": 1, "Cl": 4 } },
      "report": { "template": "report/report.html" }
    }
    """);

    var metadata = _service.LoadMetadata(dir);
    metadata.Geometry.SurfaceOrient.Should().BeNull();
    metadata.Rotation.Enabled.Should().BeFalse();
}
```

**Step 2: Run tests to verify they fail**

Run: `dotnet test --filter "TemplateMetadataServiceTests"`
Expected: compilation errors — new properties don't exist yet.

**Step 3: Expand TemplateMetadata model**

Replace the flat properties in `Models/TemplateMetadata.cs` with nested objects:

```csharp
using System.Text.Json.Serialization;

namespace foamscript.Models
{
    public class TemplateMetadata
    {
        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("description")]
        public string Description { get; set; } = string.Empty;

        [JsonPropertyName("solver")]
        public string Solver { get; set; } = string.Empty;

        [JsonPropertyName("geometry")]
        public GeometryConfig Geometry { get; set; } = new();

        [JsonPropertyName("reference")]
        public ReferenceConfig Reference { get; set; } = new();

        [JsonPropertyName("rotation")]
        public RotationConfig Rotation { get; set; } = new();

        [JsonPropertyName("validation")]
        public GeometryValidation? Validation { get; set; }

        [JsonPropertyName("parameters")]
        public Dictionary<string, ParameterDef> Parameters { get; set; } = new();

        [JsonPropertyName("domain")]
        public DomainConfig Domain { get; set; } = new();

        [JsonPropertyName("meshPipeline")]
        public List<PipelineStep> MeshPipeline { get; set; } = new();

        [JsonPropertyName("solvePipeline")]
        public List<PipelineStep> SolvePipeline { get; set; } = new();

        [JsonPropertyName("results")]
        public ResultsConfig Results { get; set; } = new();

        [JsonPropertyName("report")]
        public ReportConfig Report { get; set; } = new();

        // Backward-compat accessors used by existing code during migration
        [JsonIgnore] public string GeometryType => Geometry.Type;
        [JsonIgnore] public string GeometryStlName => Geometry.StlName;
        [JsonIgnore] public string ReferenceDimension => Reference.Dimension;
        [JsonIgnore] public string ReferenceAreaFormula => Reference.AreaFormula;
        [JsonIgnore] public bool RequiresRotorZone => Rotation.RequiresRotorZone;
        [JsonIgnore] public string[] RequiredStlFiles => Geometry.RequiredStlFiles;
    }

    public class GeometryConfig
    {
        [JsonPropertyName("type")]
        public string Type { get; set; } = string.Empty;

        [JsonPropertyName("stlName")]
        public string StlName { get; set; } = string.Empty;

        [JsonPropertyName("requiredStlFiles")]
        public string[] RequiredStlFiles { get; set; } = [];

        [JsonPropertyName("surfaceOrient")]
        public SurfaceOrientConfig? SurfaceOrient { get; set; }
    }

    public class SurfaceOrientConfig
    {
        [JsonPropertyName("outsidePoint")]
        public double[] OutsidePoint { get; set; } = [];
    }

    public class ReferenceConfig
    {
        [JsonPropertyName("dimension")]
        public string Dimension { get; set; } = string.Empty;

        [JsonPropertyName("areaFormula")]
        public string AreaFormula { get; set; } = string.Empty;
    }

    public class RotationConfig
    {
        [JsonPropertyName("enabled")]
        public bool Enabled { get; set; }

        [JsonPropertyName("requiresRotorZone")]
        public bool RequiresRotorZone { get; set; }
    }

    public class ParameterDef
    {
        [JsonPropertyName("required")]
        public bool Required { get; set; }

        [JsonPropertyName("default")]
        public double? Default { get; set; }

        [JsonPropertyName("description")]
        public string? Description { get; set; }
    }

    public class DomainConfig
    {
        [JsonPropertyName("upstream")]
        public double Upstream { get; set; } = 5.0;

        [JsonPropertyName("downstream")]
        public double Downstream { get; set; } = 10.0;

        [JsonPropertyName("radial")]
        public double Radial { get; set; } = 5.0;
    }

    public class PipelineStep
    {
        [JsonPropertyName("command")]
        public string Command { get; set; } = string.Empty;

        [JsonPropertyName("args")]
        public string Args { get; set; } = string.Empty;

        [JsonPropertyName("parallel")]
        public bool Parallel { get; set; }

        [JsonPropertyName("optional")]
        public bool Optional { get; set; }
    }

    public class ResultsConfig
    {
        [JsonPropertyName("type")]
        public string Type { get; set; } = string.Empty;

        [JsonPropertyName("dataFile")]
        public string DataFile { get; set; } = string.Empty;

        [JsonPropertyName("columns")]
        public Dictionary<string, int> Columns { get; set; } = new();
    }

    public class ReportConfig
    {
        [JsonPropertyName("template")]
        public string Template { get; set; } = string.Empty;

        [JsonPropertyName("standard")]
        public string? Standard { get; set; }

        [JsonPropertyName("postProcess")]
        public List<PostProcessStep>? PostProcess { get; set; }
    }

    public class PostProcessStep
    {
        [JsonPropertyName("function")]
        public string Function { get; set; } = string.Empty;

        [JsonPropertyName("args")]
        public string Args { get; set; } = string.Empty;
    }

    public class GeometryValidation
    {
        [JsonPropertyName("minSize")]
        public double? MinSize { get; set; }

        [JsonPropertyName("maxSize")]
        public double? MaxSize { get; set; }

        [JsonPropertyName("warningMessage")]
        public string? WarningMessage { get; set; }
    }
}
```

**Step 4: Update TemplateMetadataService — remove disc fallback, fail on missing**

Modify `Services/TemplateMetadataService.cs`:
- Remove `CreateDiscDefaults()` method entirely
- `LoadMetadata()` throws `FileNotFoundException` if TEMPLATE.json missing
- `LoadMetadata()` throws `JsonException` if TEMPLATE.json invalid (don't catch it)
- Update `CalculateReferenceDimension` and `CalculateReferenceArea` to use new property paths

```csharp
public TemplateMetadata LoadMetadata(string templatePath)
{
    var jsonPath = Path.Combine(templatePath, MetadataFileName);

    if (!File.Exists(jsonPath))
    {
        throw new FileNotFoundException(
            $"TEMPLATE.json not found in '{templatePath}'. Every template must include a TEMPLATE.json file.",
            jsonPath);
    }

    var json = File.ReadAllText(jsonPath);
    var metadata = JsonSerializer.Deserialize<TemplateMetadata>(json)
        ?? throw new JsonException($"Failed to deserialize TEMPLATE.json in '{templatePath}'.");
    return metadata;
}
```

**Step 5: Update existing tests**

Remove/update tests that expected disc fallback behavior:
- `LoadMetadata_MissingFile_ReturnsDiscDefaults` → `LoadMetadata_MissingFile_ThrowsFileNotFoundException`
- `LoadMetadata_InvalidJson_ReturnsDiscDefaults` → `LoadMetadata_InvalidJson_ThrowsJsonException`
- Update `LoadMetadata_ValidJson_ReturnsDeserializedMetadata` to use new schema structure
- Update `LoadMetadata_WithValidation_ParsesValidationBlock` to use new schema
- Update `LoadMetadata_DiscTemplate_MatchesExpectedValues` — disc TEMPLATE.json must be updated first (Task 2)

**Step 6: Run tests to verify they pass**

Run: `dotnet test --filter "TemplateMetadataServiceTests"`
Expected: all pass

**Step 7: Commit**

```bash
git add Models/TemplateMetadata.cs Services/TemplateMetadataService.cs foamscript.Tests/Services/TemplateMetadataServiceTests.cs
git commit -m "feat: expand TemplateMetadata schema with pipelines, parameters, results, report config

BREAKING: TEMPLATE.json is now required — missing file throws FileNotFoundException.
Removes hardcoded disc fallback defaults."
```

---

## Task 2: Update All TEMPLATE.json Files to New Schema

**Files:**
- Modify: `Templates/external_disc_rotatingwall_steady/TEMPLATE.json`
- Modify: `Templates/external_disc_rotating-ami_transient/TEMPLATE.json`
- Modify: `Templates/external_airfoil_static_steady/TEMPLATE.json`

**Step 1: Update disc steady TEMPLATE.json**

```json
{
  "name": "external_disc_rotatingwall_steady",
  "description": "Steady-state rotating disc analysis (simpleFoam + rotatingWallVelocity)",
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
    "warningMessage": "Disc diameter outside expected PDGA range (0.21-0.30m). Ensure correct --input-units."
  },
  "parameters": {
    "velocity": { "required": true, "description": "Freestream velocity (m/s)" },
    "rpm": { "required": true, "description": "Rotational speed (RPM)" },
    "angles": { "required": true, "description": "Angle(s) of attack (degrees)" },
    "refinementMin": { "required": false, "default": 5, "description": "Minimum refinement level" },
    "refinementMax": { "required": false, "default": 6, "description": "Maximum refinement level" },
    "maxIterations": { "required": false, "default": 500, "description": "Maximum solver iterations" }
  },
  "domain": {
    "upstream": 5.0,
    "downstream": 10.0,
    "radial": 5.0
  },
  "meshPipeline": [
    { "command": "blockMesh", "args": "-case {caseDir}" },
    { "command": "surfaceOrient", "args": "{geometryStlPath} \"({outsidePoint})\" {geometryStlPath}", "optional": true },
    { "command": "surfaceFeatureExtract", "args": "-case {caseDir}" },
    { "command": "decomposePar", "args": "-case {caseDir}" },
    { "command": "snappyHexMesh", "args": "-case {caseDir} -overwrite", "parallel": true },
    { "command": "reconstructParMesh", "args": "-case {caseDir} -constant" },
    { "command": "checkMesh", "args": "-case {caseDir}" }
  ],
  "solvePipeline": [
    { "command": "decomposePar", "args": "-case {caseDir} -force" },
    { "command": "simpleFoam", "args": "-case {caseDir}", "parallel": true },
    { "command": "reconstructPar", "args": "-case {caseDir} -latestTime" }
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

**Step 2: Update disc transient (AMI) TEMPLATE.json**

Same structure but with `"solver": "pimpleFoam"`, `"requiredStlFiles": ["disc.stl", "rotor.stl", "tunnel.stl"]`, and pimpleFoam in solvePipeline.

**Step 3: Update airfoil steady TEMPLATE.json**

```json
{
  "name": "external_airfoil_static_steady",
  "description": "Steady-state 2D airfoil analysis (simpleFoam + Spalart-Allmaras)",
  "solver": "simpleFoam",
  "geometry": {
    "type": "airfoil",
    "stlName": "airfoil.stl",
    "requiredStlFiles": ["airfoil.stl"],
    "surfaceOrient": null
  },
  "reference": {
    "dimension": "chord",
    "areaFormula": "rectangular"
  },
  "rotation": {
    "enabled": false,
    "requiresRotorZone": false
  },
  "validation": null,
  "parameters": {
    "velocity": { "required": true, "description": "Freestream velocity (m/s)" },
    "angles": { "required": true, "description": "Angle(s) of attack (degrees)" },
    "refinementMin": { "required": false, "default": 5, "description": "Minimum refinement level" },
    "refinementMax": { "required": false, "default": 6, "description": "Maximum refinement level" }
  },
  "domain": {
    "upstream": 5.0,
    "downstream": 10.0,
    "radial": 5.0
  },
  "meshPipeline": [
    { "command": "blockMesh", "args": "-case {caseDir}" },
    { "command": "surfaceFeatureExtract", "args": "-case {caseDir}" },
    { "command": "decomposePar", "args": "-case {caseDir}" },
    { "command": "snappyHexMesh", "args": "-case {caseDir} -overwrite", "parallel": true },
    { "command": "reconstructParMesh", "args": "-case {caseDir} -constant" },
    { "command": "checkMesh", "args": "-case {caseDir}" }
  ],
  "solvePipeline": [
    { "command": "decomposePar", "args": "-case {caseDir} -force" },
    { "command": "simpleFoam", "args": "-case {caseDir}", "parallel": true },
    { "command": "reconstructPar", "args": "-case {caseDir} -latestTime" }
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

**Step 4: Run all tests**

Run: `dotnet test`
Expected: all pass (TemplateMetadataServiceTests updated in Task 1, plus existing test that loads disc template)

**Step 5: Commit**

```bash
git add Templates/*/TEMPLATE.json
git commit -m "feat: update all TEMPLATE.json files to expanded schema

Adds meshPipeline, solvePipeline, results, report, parameters, and
nested geometry/reference/rotation sections to all three templates."
```

---

## Task 3: CLI Parameter Generalization

**Files:**
- Modify: `Models/NewStudyModel.cs`
- Modify: `Handlers/NewStudyHandler.cs`
- Test: `foamscript.Tests/Handlers/NewStudyHandlerTests.cs` (existing or new)

**Step 1: Write failing test for required --template**

```csharp
[Fact]
public void Handle_MissingTemplate_ReturnsError()
{
    // NewStudyModel with no --template should fail
    var model = new NewStudyModel { ProjectName = "test", OutputDir = "/tmp", ModelSource = "test.stl", Angles = "0" };
    // TemplatePath is null — handler should reject
}
```

**Step 2: Write failing test for template-driven required params**

```csharp
[Fact]
public void Handle_MissingRequiredParam_ReturnsError()
{
    // Template requires --velocity but it's not provided
    // Should produce error: "Template 'X' requires --velocity but it was not provided"
}
```

**Step 3: Make --template required (no default)**

In `Models/NewStudyModel.cs`:
- Change `--template` help text: remove "defaults to external_disc_rotatingwall_steady"
- In `Handlers/NewStudyHandler.cs` `ResolveTemplatePath()`: remove the null/empty fallback to disc template. Instead, throw an error.

```csharp
private string ResolveTemplatePath(string? templatePathOrName)
{
    if (string.IsNullOrEmpty(templatePathOrName))
    {
        throw new ArgumentException("--template is required. Use 'foamscript list-templates' to see available templates.");
    }
    // ... rest unchanged
}
```

**Step 4: Make velocity/rpm nullable, remove hardcoded defaults**

In `Models/NewStudyModel.cs`:
- `Velocity` → `double?` with `Default = null` (remove `Default = 27.0`)
- `Rpm` → `double?` with `Default = null` (remove `Default = 925.0`)
- Update help text: remove "elite amateur" references, use neutral descriptions

In `Handlers/NewStudyHandler.cs`:
- After loading TemplateMetadata, validate required parameters from `metadata.Parameters`
- For each parameter with `"required": true`, check if the CLI provided a value
- If CLI value is null and parameter has a `"default"` in TEMPLATE.json, use the default
- If CLI value is null and no default, error with clear message

**Step 5: Update domain help text**

In `Models/NewStudyModel.cs`:
- `--tunnel-upstream`: "in disc diameters" → "in reference lengths"
- `--tunnel-downstream`: same
- `--tunnel-radial`: same
- `--rotor-radius-scale`: "multiple of disc radius" → "multiple of geometry radius"

**Step 6: Run tests**

Run: `dotnet test`
Expected: all pass

**Step 7: Commit**

```bash
git add Models/NewStudyModel.cs Handlers/NewStudyHandler.cs foamscript.Tests/
git commit -m "feat: template-driven CLI parameters

BREAKING: --template is now required (no default).
BREAKING: --velocity and --rpm no longer have defaults.
Templates declare required/optional params in TEMPLATE.json."
```

---

## Task 4: Generic Mesh Pipeline Executor

**Files:**
- Modify: `Services/MeshService.cs`
- Test: `foamscript.Tests/Services/MeshServiceTests.cs`

**Step 1: Write failing test for pipeline execution**

```csharp
[Fact]
public void MeshCase_ExecutesPipelineFromMetadata()
{
    // Setup: mock process executor, template metadata with meshPipeline
    // Verify: each pipeline step is executed in order with resolved tokens
}

[Fact]
public void MeshCase_SkipsSurfaceOrientWhenNotInPipeline()
{
    // Setup: airfoil metadata with no surfaceOrient in pipeline
    // Verify: surfaceOrient is never called
}

[Fact]
public void MeshCase_OptionalStepFailureIsWarning()
{
    // Setup: pipeline step with optional=true that returns non-zero
    // Verify: result.Success is true, result.Warnings contains message
}
```

**Step 2: Implement generic pipeline executor in MeshService**

Replace the hardcoded blockMesh → surfaceOrient → surfaceFeatureExtract → ... sequence with:

```csharp
// Load metadata from TEMPLATE.json in case directory
var metadata = _metadataService.LoadMetadata(Path.Combine(caseDir));

foreach (var step in metadata.MeshPipeline)
{
    var resolvedArgs = ResolvePipelineTokens(step.Args, caseDir, metadata);
    Console.WriteLine($"Running {step.Command}...");

    ProcessResult stepResult;
    if (step.Parallel && cores > 1)
    {
        stepResult = _processExecutor.Execute("mpirun", $"-np {cores} {step.Command} {resolvedArgs} -parallel");
    }
    else
    {
        stepResult = _processExecutor.Execute(step.Command, resolvedArgs);
    }

    if (stepResult.ExitCode != 0)
    {
        if (step.Optional)
        {
            result.Warnings.Add($"{step.Command} failed (optional step)");
            _loggingService.LogError($"{step.Command} failed: {stepResult.Output}");
        }
        else
        {
            result.Success = false;
            result.ErrorMessage = $"{step.Command} failed with exit code {stepResult.ExitCode}";
            return result;
        }
    }
    else
    {
        Console.WriteLine($"✓ {step.Command} completed successfully");
    }
}
```

**Step 3: Implement token resolver**

```csharp
private string ResolvePipelineTokens(string args, string caseDir, TemplateMetadata metadata)
{
    var geometryStlPath = Path.Combine(caseDir, "constant", "triSurface", metadata.Geometry.StlName);
    var outsidePoint = metadata.Geometry.SurfaceOrient?.OutsidePoint is { } pt
        ? $"{pt[0]} {pt[1]} {pt[2]}"
        : "0 0 0";

    return args
        .Replace("{caseDir}", caseDir)
        .Replace("{geometryStlPath}", geometryStlPath)
        .Replace("{outsidePoint}", outsidePoint);
}
```

**Step 4: Remove hardcoded surfaceOrient, PatchBoundaryForAMI guard**

- Delete the surfaceOrient block (lines 73-92)
- Guard `PatchBoundaryForAMI` with `metadata.Rotation.Enabled`
- Delete the `DistributeTriSurface` hardcoded file list — derive from metadata

**Step 5: Run tests**

Run: `dotnet test`
Expected: all pass

**Step 6: Commit**

```bash
git add Services/MeshService.cs foamscript.Tests/Services/MeshServiceTests.cs
git commit -m "feat: generic mesh pipeline executor driven by TEMPLATE.json

Replaces hardcoded blockMesh→surfaceOrient→snappy sequence.
Each template declares its own mesh steps in meshPipeline."
```

---

## Task 5: Generic Solve Pipeline Executor

**Files:**
- Modify: `Services/SolverService.cs`
- Test: `foamscript.Tests/Services/SolverServiceTests.cs`

**Step 1: Write failing test**

```csharp
[Fact]
public void SolveCase_ExecutesSolvePipelineFromMetadata()
{
    // Setup: metadata with solvePipeline = [decomposePar, simpleFoam(parallel), reconstructPar]
    // Verify: each step executed in order
}
```

**Step 2: Implement generic solver pipeline**

Same pattern as mesh — read `metadata.SolvePipeline`, execute each step with token resolution. Replace the hardcoded decomposePar → solver → reconstructPar sequence.

**Step 3: Run tests, commit**

```bash
git commit -m "feat: generic solve pipeline executor driven by TEMPLATE.json"
```

---

## Task 6: Generic Results Extraction

**Files:**
- Modify: `Services/SolverService.cs` (ParseForceCoeffs)
- Modify: `Services/ResultsService.cs`
- Modify: `Services/ReportService.cs` (CSV export)
- Test: `foamscript.Tests/Services/ResultsServiceTests.cs`

**Step 1: Write failing test for generic column extraction**

```csharp
[Fact]
public void ExtractResults_UsesMetadataColumns()
{
    // Setup: metadata with results.columns = { "Cd": 1, "Cl": 4 }
    // Verify: extracted results contain Cd and Cl keys with correct values
}
```

**Step 2: Refactor results to use Dictionary<string, double> instead of fixed Cd/Cl/Cm**

The `CaseResult` model gets a `Dictionary<string, double?> Coefficients` property instead of separate `Cd`, `Cl`, `CmPitch` properties. The dictionary keys come from `metadata.Results.Columns`.

**Step 3: Update CSV export**

`WriteCoefficientCsv` in ReportService generates headers dynamically from the results column names instead of hardcoded `"AoA_deg,Cd,Cl,CmPitch,L_over_D,Converged"`.

**Step 4: Run tests, commit**

```bash
git commit -m "feat: generic results extraction using metadata column mapping

Results keys are now dynamic from TEMPLATE.json results.columns.
CSV export generates headers from metadata."
```

---

## Task 7: Template-Driven Report Generation

**Files:**
- Modify: `Services/ReportService.cs`
- Modify: `Services/HtmlReportGenerator.cs`
- Modify: `Services/PdfReportGenerator.cs`
- Modify: `Services/ChartGenerator.cs`
- Create: `Templates/external_disc_rotatingwall_steady/report/report.html` (copy from `Templates/report/report.html`)
- Create: `Templates/external_airfoil_static_steady/report/report.html` (copy initially, diverge later)
- Test: `foamscript.Tests/Services/ReportServiceTests.cs`

**Step 1: Copy existing report template into disc template directory**

```bash
mkdir -p Templates/external_disc_rotatingwall_steady/report
cp Templates/report/report.html Templates/external_disc_rotatingwall_steady/report/
# Same for AMI transient
mkdir -p Templates/external_disc_rotating-ami_transient/report
cp Templates/report/report.html Templates/external_disc_rotating-ami_transient/report/
# Same for airfoil (will diverge later)
mkdir -p Templates/external_airfoil_static_steady/report
cp Templates/report/report.html Templates/external_airfoil_static_steady/report/
```

**Step 2: Update HtmlReportGenerator to find template-specific report.html**

Change `FindTemplatePath()` to first look in the template directory (from study.json manifest), then fall back to the global `Templates/report/report.html`.

**Step 3: Make chart generation data-driven**

`ChartGenerator` already has a generic `GeneratePolarChartSvg(results, fieldName)` method. The `ReportService` needs to iterate over `metadata.Results.Columns` keys to generate one chart per coefficient instead of hardcoding Cl/Cd/Cm/LD charts.

**Step 4: Update PdfReportGenerator coefficient table**

Replace hardcoded headers at line 349 (`"Cᴅ"`, `"Cₗ"`, `"Cm,pitch"`) with dynamic headers from the results columns.

**Step 5: Remove Python visualization scripts**

Delete `Templates/report/extract_slice.py` and `Templates/report/render_slice.py`. Replace with OpenFOAM `postProcess` declarations in TEMPLATE.json and ScottPlot rendering in C#.

Note: This is the most complex sub-task and may need to be split further during execution. The Python elimination can be deferred to a follow-up if it blocks progress.

**Step 6: Run tests, commit**

```bash
git commit -m "feat: template-driven report generation

Each template ships its own report/report.html Scriban template.
Charts generated dynamically from results.columns metadata.
PDF coefficient table headers are data-driven."
```

---

## Task 8: Update Existing Callers and Clean Up

**Files:**
- Modify: `Services/CaseService.cs` — remove `disc_diameter` alias, update parameter names
- Modify: `Services/DomainService.cs` — rename `discStlFile` parameter
- Modify: `Handlers/ListTemplatesHandler.cs` — display expanded metadata
- Modify: `Services/ReportService.cs` — remove RPM display when rotation.enabled=false
- Delete: `Templates/report/extract_slice.py` (moved to postProcess)
- Delete: `Templates/report/render_slice.py` (moved to postProcess)

**Step 1: Clean up backward-compat aliases**

In `CaseService.CalculateTemplateContext()`:
- Remove `disc_diameter = refLength` alias (line 320)
- Existing templates already use `ref_length`

**Step 2: Rename misleading parameters**

In `DomainService`:
- Rename any `discStlFile` parameters to `geometryStlFile`

**Step 3: Conditional RPM display**

In report generators:
- Only show RPM row when `metadata.Rotation.Enabled == true`

**Step 4: Run full test suite**

Run: `dotnet test`
Expected: all 211+ tests pass, 0 warnings

**Step 5: Commit**

```bash
git commit -m "chore: clean up disc-specific naming and conditional displays

Removes disc_diameter alias, renames discStlFile, conditionally
shows RPM in reports based on rotation.enabled."
```

---

## Task 9: Build, Full Test, E2E Validation

**Step 1: Build**

Run: `dotnet build --configuration Release`
Expected: 0 warnings, 0 errors

**Step 2: Run full test suite**

Run: `dotnet test`
Expected: all tests pass

**Step 3: Push to Linux, E2E validate disc template**

Verify the disc template still produces identical results to pre-refactor:
```bash
foamscript new-study --template external_disc_rotatingwall_steady --project-name disc_regression --output-dir /run --model-source Apogee.step --velocity 27 --rpm 925 --angles 0
foamscript mesh -d /run/disc_regression
foamscript solve -d /run/disc_regression
foamscript report -d /run/disc_regression
```

**Step 4: E2E validate airfoil template**

```bash
foamscript new-study --template external_airfoil_static_steady --project-name airfoil_regression --output-dir /run --model-source NACA0012.stl --velocity 26 --angles 0 --refinement-min 3 --refinement-max 4
foamscript mesh -d /run/airfoil_regression
foamscript solve -d /run/airfoil_regression
foamscript report -d /run/airfoil_regression
```

**Step 5: Commit final state, update docs**

```bash
git commit -m "chore: template generalization complete — all E2E tests pass"
```

Update `Docs/ExecutiveSummary.md` with new template count, test count, and architecture changes.

---

## Task Summary

| Task | Description | Est. Complexity |
|------|-------------|----------------|
| 1 | Expand TemplateMetadata model + remove disc fallback | Medium |
| 2 | Update all TEMPLATE.json files to new schema | Low |
| 3 | CLI parameter generalization (--template required, nullable params) | Medium |
| 4 | Generic mesh pipeline executor | High |
| 5 | Generic solve pipeline executor | Medium |
| 6 | Generic results extraction | Medium |
| 7 | Template-driven report generation + Python elimination | High |
| 8 | Clean up backward-compat aliases and cosmetic issues | Low |
| 9 | Build, test, E2E validation on Linux | Medium |

**Total: 9 tasks, ~15-25 commits**

## Dependencies

```
Task 1 (model) ──→ Task 2 (JSON files) ──→ Task 3 (CLI)
                                         ──→ Task 4 (mesh pipeline)
                                         ──→ Task 5 (solve pipeline)
                                         ──→ Task 6 (results)
                                         ──→ Task 7 (report)
Tasks 3-7 ──→ Task 8 (cleanup) ──→ Task 9 (E2E)
```

Tasks 3-7 can be parallelized across agents since they touch different services.
