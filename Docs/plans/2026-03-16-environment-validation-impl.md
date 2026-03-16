# Environment Validation Redesign — Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Make foamscript own its OpenFOAM environment via config + bashrc sourcing, so no command ever fails due to shell state. Full pre-flight checklist with install hints.

**Architecture:** Config file (`~/.foamscript/config.json`) stores bashrc path. `OpenFoamEnvironment` service sources it at startup and captures env vars. `ProcessExecutor` injects those vars into every spawned process. `foamscript validate` is the setup wizard that writes the config and checks all 20 dependencies.

**Tech Stack:** .NET 10, System.Text.Json, xUnit + Moq + FluentAssertions

---

### Task 1: Config Model (`FoamScriptConfig`)

**Files:**
- Create: `Models/FoamScriptConfig.cs`
- Test: `foamscript.Tests/Models/FoamScriptConfigTests.cs`

**Step 1: Write the test**

```csharp
// foamscript.Tests/Models/FoamScriptConfigTests.cs
using System.Text.Json;
using Xunit;
using FluentAssertions;
using foamscript.Models;

namespace foamscript.Tests.Models
{
    public class FoamScriptConfigTests
    {
        [Fact]
        public void RoundTrips_ThroughJson()
        {
            var config = new FoamScriptConfig
            {
                OpenFoamBashrc = "/usr/lib/openfoam/openfoam2512/etc/bashrc",
                OpenFoamVersion = "v2512",
                ConfiguredAt = new DateTime(2026, 3, 16, 20, 0, 0, DateTimeKind.Utc)
            };

            var json = JsonSerializer.Serialize(config, FoamScriptConfig.JsonOptions);
            var deserialized = JsonSerializer.Deserialize<FoamScriptConfig>(json, FoamScriptConfig.JsonOptions);

            deserialized!.OpenFoamBashrc.Should().Be(config.OpenFoamBashrc);
            deserialized.OpenFoamVersion.Should().Be(config.OpenFoamVersion);
            deserialized.ConfiguredAt.Should().Be(config.ConfiguredAt);
        }

        [Fact]
        public void ConfigPath_IsUnderUserHome()
        {
            FoamScriptConfig.ConfigPath.Should().Contain(".foamscript");
            FoamScriptConfig.ConfigPath.Should().EndWith("config.json");
        }
    }
}
```

**Step 2: Run test to verify it fails**

Run: `dotnet test --filter "FoamScriptConfigTests"`
Expected: FAIL — `FoamScriptConfig` does not exist

**Step 3: Write the model**

```csharp
// Models/FoamScriptConfig.cs
using System.Text.Json;
using System.Text.Json.Serialization;

namespace foamscript.Models
{
    public class FoamScriptConfig
    {
        [JsonPropertyName("openfoamBashrc")]
        public string OpenFoamBashrc { get; set; } = "";

        [JsonPropertyName("openfoamVersion")]
        public string OpenFoamVersion { get; set; } = "";

        [JsonPropertyName("configuredAt")]
        public DateTime ConfiguredAt { get; set; }

        public static string ConfigDir =>
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".foamscript");

        public static string ConfigPath =>
            Path.Combine(ConfigDir, "config.json");

        public static readonly JsonSerializerOptions JsonOptions = new()
        {
            WriteIndented = true
        };
    }
}
```

**Step 4: Run test to verify it passes**

Run: `dotnet test --filter "FoamScriptConfigTests"`
Expected: PASS (2 tests)

**Step 5: Commit**

```bash
git add Models/FoamScriptConfig.cs foamscript.Tests/Models/FoamScriptConfigTests.cs
git commit -m "feat: add FoamScriptConfig model for ~/.foamscript/config.json"
```

---

### Task 2: OpenFoamEnvironment Service — Config Loading + Env Capture

**Files:**
- Create: `Services/OpenFoamEnvironment.cs`
- Test: `foamscript.Tests/Services/OpenFoamEnvironmentTests.cs`

**Step 1: Write the tests**

```csharp
// foamscript.Tests/Services/OpenFoamEnvironmentTests.cs
using System.Text.Json;
using Xunit;
using FluentAssertions;
using Moq;
using foamscript.Models;
using foamscript.Services;

namespace foamscript.Tests.Services
{
    public class OpenFoamEnvironmentTests
    {
        [Fact]
        public void CaptureEnvironment_ParsesEnvOutput_IntoDict()
        {
            var mockExecutor = new Mock<IProcessExecutor>();
            // Simulate "bash -c 'source bashrc && env'" output
            mockExecutor
                .Setup(x => x.Execute("bash", It.Is<string>(s => s.Contains("source") && s.Contains("env"))))
                .Returns(new ProcessResult
                {
                    ExitCode = 0,
                    Output = "WM_PROJECT_DIR=/opt/openfoam\nFOAM_APPBIN=/opt/openfoam/bin\nPATH=/opt/openfoam/bin:/usr/bin\nHOME=/home/user\n"
                });

            var env = new OpenFoamEnvironment(mockExecutor.Object);
            var result = env.CaptureEnvironment("/path/to/bashrc");

            result.Should().ContainKey("WM_PROJECT_DIR");
            result["WM_PROJECT_DIR"].Should().Be("/opt/openfoam");
            result.Should().ContainKey("FOAM_APPBIN");
            result.Should().ContainKey("PATH");
        }

        [Fact]
        public void CaptureEnvironment_WhenSourceFails_ReturnsNull()
        {
            var mockExecutor = new Mock<IProcessExecutor>();
            mockExecutor
                .Setup(x => x.Execute("bash", It.IsAny<string>()))
                .Returns(new ProcessResult { ExitCode = 1, Output = "", Error = "bash: no such file" });

            var env = new OpenFoamEnvironment(mockExecutor.Object);
            var result = env.CaptureEnvironment("/bad/path/bashrc");

            result.Should().BeNull();
        }

        [Fact]
        public void LoadConfig_WhenFileExists_ReturnsConfig()
        {
            var tempDir = Path.Combine(Path.GetTempPath(), $"foamscript_test_{Guid.NewGuid():N}");
            Directory.CreateDirectory(tempDir);
            var configPath = Path.Combine(tempDir, "config.json");

            var config = new FoamScriptConfig
            {
                OpenFoamBashrc = "/usr/lib/openfoam/openfoam2512/etc/bashrc",
                OpenFoamVersion = "v2512",
                ConfiguredAt = DateTime.UtcNow
            };
            File.WriteAllText(configPath, JsonSerializer.Serialize(config, FoamScriptConfig.JsonOptions));

            try
            {
                var loaded = OpenFoamEnvironment.LoadConfig(configPath);
                loaded.Should().NotBeNull();
                loaded!.OpenFoamBashrc.Should().Be("/usr/lib/openfoam/openfoam2512/etc/bashrc");
            }
            finally
            {
                Directory.Delete(tempDir, true);
            }
        }

        [Fact]
        public void LoadConfig_WhenFileMissing_ReturnsNull()
        {
            var result = OpenFoamEnvironment.LoadConfig("/nonexistent/config.json");
            result.Should().BeNull();
        }

        [Fact]
        public void SaveConfig_CreatesDirectoryAndWritesFile()
        {
            var tempDir = Path.Combine(Path.GetTempPath(), $"foamscript_test_{Guid.NewGuid():N}");
            var configPath = Path.Combine(tempDir, "config.json");

            try
            {
                var config = new FoamScriptConfig
                {
                    OpenFoamBashrc = "/path/to/bashrc",
                    OpenFoamVersion = "v2512",
                    ConfiguredAt = DateTime.UtcNow
                };

                OpenFoamEnvironment.SaveConfig(config, configPath);

                File.Exists(configPath).Should().BeTrue();
                var loaded = JsonSerializer.Deserialize<FoamScriptConfig>(
                    File.ReadAllText(configPath), FoamScriptConfig.JsonOptions);
                loaded!.OpenFoamBashrc.Should().Be("/path/to/bashrc");
            }
            finally
            {
                if (Directory.Exists(tempDir))
                    Directory.Delete(tempDir, true);
            }
        }
    }
}
```

**Step 2: Run tests to verify they fail**

Run: `dotnet test --filter "OpenFoamEnvironmentTests"`
Expected: FAIL — `OpenFoamEnvironment` does not exist

**Step 3: Write the service**

```csharp
// Services/OpenFoamEnvironment.cs
using System.Text.Json;
using foamscript.Models;

namespace foamscript.Services
{
    /// <summary>
    /// Manages OpenFOAM environment: loads config, sources bashrc,
    /// captures env vars for injection into ProcessExecutor.
    /// </summary>
    public class OpenFoamEnvironment
    {
        private readonly IProcessExecutor _processExecutor;
        private Dictionary<string, string>? _capturedEnv;

        public OpenFoamEnvironment(IProcessExecutor processExecutor)
        {
            _processExecutor = processExecutor;
        }

        /// <summary>
        /// The captured OpenFOAM environment variables. Null until Initialize() is called.
        /// </summary>
        public Dictionary<string, string>? CapturedEnv => _capturedEnv;

        /// <summary>
        /// Initializes the OpenFOAM environment from the config file.
        /// Called at startup for commands that need OpenFOAM.
        /// Returns null on success, or an error message on failure.
        /// </summary>
        public string? Initialize(string? configPath = null)
        {
            configPath ??= FoamScriptConfig.ConfigPath;
            var config = LoadConfig(configPath);

            if (config == null)
                return "FoamScript is not configured. Run 'foamscript validate' to set up your environment.";

            if (!File.Exists(config.OpenFoamBashrc))
                return $"OpenFOAM bashrc not found at {config.OpenFoamBashrc}\n" +
                       "  The installation may have been moved or upgraded.\n" +
                       "  Run 'foamscript validate' to reconfigure.";

            _capturedEnv = CaptureEnvironment(config.OpenFoamBashrc);

            if (_capturedEnv == null)
                return $"Failed to source OpenFOAM bashrc at {config.OpenFoamBashrc}\n" +
                       "  Check that it is a valid OpenFOAM installation.";

            if (!_capturedEnv.ContainsKey("WM_PROJECT_DIR"))
                return $"OpenFOAM bashrc at {config.OpenFoamBashrc} did not set WM_PROJECT_DIR.\n" +
                       "  This may not be a valid OpenFOAM installation.\n" +
                       "  Run 'foamscript validate' to reconfigure.";

            return null; // Success
        }

        /// <summary>
        /// Sources the given bashrc and captures the resulting environment variables.
        /// Returns null if sourcing fails.
        /// </summary>
        public Dictionary<string, string>? CaptureEnvironment(string bashrcPath)
        {
            var result = _processExecutor.Execute("bash",
                $"-c \"source '{bashrcPath}' 2>/dev/null && env\"");

            if (result.ExitCode != 0)
                return null;

            var env = new Dictionary<string, string>();
            foreach (var line in result.Output.Split('\n', StringSplitOptions.RemoveEmptyEntries))
            {
                var eqIdx = line.IndexOf('=');
                if (eqIdx > 0)
                {
                    var key = line[..eqIdx];
                    var value = line[(eqIdx + 1)..];
                    env[key] = value;
                }
            }

            return env.Count > 0 ? env : null;
        }

        public static FoamScriptConfig? LoadConfig(string? configPath = null)
        {
            configPath ??= FoamScriptConfig.ConfigPath;
            if (!File.Exists(configPath))
                return null;

            try
            {
                var json = File.ReadAllText(configPath);
                return JsonSerializer.Deserialize<FoamScriptConfig>(json, FoamScriptConfig.JsonOptions);
            }
            catch
            {
                return null;
            }
        }

        public static void SaveConfig(FoamScriptConfig config, string? configPath = null)
        {
            configPath ??= FoamScriptConfig.ConfigPath;
            var dir = Path.GetDirectoryName(configPath)!;
            Directory.CreateDirectory(dir);
            File.WriteAllText(configPath, JsonSerializer.Serialize(config, FoamScriptConfig.JsonOptions));
        }
    }
}
```

**Step 4: Run tests to verify they pass**

Run: `dotnet test --filter "OpenFoamEnvironmentTests"`
Expected: PASS (5 tests)

**Step 5: Commit**

```bash
git add Services/OpenFoamEnvironment.cs foamscript.Tests/Services/OpenFoamEnvironmentTests.cs
git commit -m "feat: add OpenFoamEnvironment service for config loading and env capture"
```

---

### Task 3: ProcessExecutor Env Injection

**Files:**
- Modify: `Services/IProcessExecutor.cs`
- Modify: `Services/ProcessExecutor.cs`

**Step 1: Write the test**

No new test file needed — this is a behavioral change. The existing mock-based tests don't exercise ProcessExecutor directly. Add a unit test to verify env vars are applied:

```csharp
// Add to foamscript.Tests/Services/OpenFoamEnvironmentTests.cs

[Fact]
public void ProcessExecutor_SetsEnvironmentVariables_WhenProvided()
{
    // This tests the contract: ProcessExecutor should accept env vars
    var executor = new ProcessExecutor();
    executor.SetEnvironment(new Dictionary<string, string>
    {
        ["FOAMSCRIPT_TEST_VAR"] = "hello_from_test"
    });

    // bash -c 'echo $VAR' will read from the process env
    var result = executor.Execute("bash", "-c \"echo $FOAMSCRIPT_TEST_VAR\"");

    result.Output.Trim().Should().Be("hello_from_test");
}
```

**Step 2: Run test to verify it fails**

Run: `dotnet test --filter "ProcessExecutor_SetsEnvironmentVariables"`
Expected: FAIL — `SetEnvironment` does not exist

**Step 3: Modify ProcessExecutor**

Update `IProcessExecutor`:

```csharp
// Services/IProcessExecutor.cs
using foamscript.Models;

namespace foamscript.Services
{
    public interface IProcessExecutor
    {
        ProcessResult Execute(string command, string arguments = "");
        void SetEnvironment(Dictionary<string, string> environmentVariables);
    }
}
```

Update `ProcessExecutor`:

```csharp
// Services/ProcessExecutor.cs
using System.Diagnostics;
using foamscript.Models;

namespace foamscript.Services
{
    public class ProcessExecutor : IProcessExecutor
    {
        private Dictionary<string, string>? _environmentVariables;

        public void SetEnvironment(Dictionary<string, string> environmentVariables)
        {
            _environmentVariables = environmentVariables;
        }

        public ProcessResult Execute(string command, string arguments = "")
        {
            var processInfo = new ProcessStartInfo
            {
                FileName = command,
                Arguments = arguments,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            // Inject captured OpenFOAM environment variables
            if (_environmentVariables != null)
            {
                foreach (var (key, value) in _environmentVariables)
                {
                    processInfo.EnvironmentVariables[key] = value;
                }
            }

            using var process = new Process { StartInfo = processInfo };
            process.Start();

            string output = process.StandardOutput.ReadToEnd();
            string error = process.StandardError.ReadToEnd();

            process.WaitForExit();

            return new ProcessResult
            {
                ExitCode = process.ExitCode,
                Output = output,
                Error = error
            };
        }
    }
}
```

**Step 4: Fix mock setups**

All existing tests use `Mock<IProcessExecutor>`. Moq auto-stubs `SetEnvironment()` as a void method with no setup needed. Verify no test breakage:

Run: `dotnet test`
Expected: ALL PASS (235+ tests)

**Step 5: Commit**

```bash
git add Services/IProcessExecutor.cs Services/ProcessExecutor.cs foamscript.Tests/Services/OpenFoamEnvironmentTests.cs
git commit -m "feat: ProcessExecutor env injection via SetEnvironment()"
```

---

### Task 4: Pre-flight Guard in Program.cs + AppService

**Files:**
- Modify: `Program.cs` (register `OpenFoamEnvironment`)
- Modify: `Services/AppService.cs` (add pre-flight guard)

**Step 1: Modify Program.cs — register OpenFoamEnvironment as Singleton**

Add after the existing `IProcessExecutor` registration:

```csharp
builder.Services.AddSingleton<OpenFoamEnvironment>();
```

**Step 2: Modify AppService — add pre-flight guard**

Add `OpenFoamEnvironment` to AppService's constructor and call `Initialize()` before commands that need OpenFOAM:

```csharp
// Services/AppService.cs — updated Run() method
public int Run(VerbModel model)
{
    try
    {
        // Commands that don't need OpenFOAM environment
        if (model is ValidateModel m1)
            return _validateHandler.Handle(m1);
        if (model is ListTemplatesModel m2)
            return _listTemplatesHandler.Handle(m2);

        // Pre-flight: ensure OpenFOAM environment is loaded
        var envError = _openFoamEnvironment.Initialize();
        if (envError != null)
        {
            Console.Error.WriteLine($"✗ {envError}");
            return -1;
        }

        // Inject captured env into ProcessExecutor
        var processExecutor = _processExecutor as ProcessExecutor;
        if (processExecutor != null && _openFoamEnvironment.CapturedEnv != null)
        {
            processExecutor.SetEnvironment(_openFoamEnvironment.CapturedEnv);
        }

        return model switch
        {
            ConvertModel m => _convertHandler.Handle(m),
            NewStudyModel m => _newStudyHandler.Handle(m),
            MeshModel m => _meshHandler.Handle(m),
            SolveModel m => _solveHandler.Handle(m),
            ReportModel m => _reportHandler.Handle(m),
            _ => throw new NotImplementedException($"The verb of type {model.GetType().Name} is not implemented.")
        };
    }
    catch (Exception ex)
    {
        _loggingService.LogError("The application failed to execute the requested command.", ex);
        return -1;
    }
}
```

The constructor adds `OpenFoamEnvironment openFoamEnvironment` and `IProcessExecutor processExecutor` parameters.

**Step 3: Run tests**

Run: `dotnet build && dotnet test`
Expected: ALL PASS

**Step 4: Commit**

```bash
git add Program.cs Services/AppService.cs
git commit -m "feat: pre-flight guard sources OpenFOAM env before commands"
```

---

### Task 5: Rewrite EnvironmentService — Grouped Checklist

**Files:**
- Modify: `Models/EnvironmentValidationResult.cs`
- Modify: `Services/EnvironmentService.cs`
- Modify: `foamscript.Tests/Services/EnvironmentServiceTests.cs`

**Step 1: Rewrite the model**

Replace `EnvironmentValidationResult` with a grouped structure:

```csharp
// Models/EnvironmentValidationResult.cs
namespace foamscript.Models
{
    public class EnvironmentValidationResult
    {
        public bool IsValid { get; set; }
        public string? OpenFoamVersion { get; set; }
        public string? OpenFoamBashrc { get; set; }
        public string? DetectedBashrcPath { get; set; }
        public List<CheckGroup> Groups { get; set; } = new();
        public int TotalChecks => Groups.Sum(g => g.Checks.Count);
        public int PassedChecks => Groups.Sum(g => g.Checks.Count(c => c.Passed));
    }

    public class CheckGroup
    {
        public string Name { get; set; } = "";
        public List<CheckItem> Checks { get; set; } = new();
    }

    public class CheckItem
    {
        public bool Passed { get; set; }
        public string Name { get; set; } = "";
        public string? Detail { get; set; }       // e.g., path when found
        public string? InstallHint { get; set; }   // e.g., "sudo apt install gmsh"
    }
}
```

**Step 2: Rewrite EnvironmentService.ValidateEnvironment()**

The new implementation:
- Auto-detects OpenFOAM installation (existing logic)
- Sources bashrc and captures env (via `OpenFoamEnvironment.CaptureEnvironment`)
- Checks all tools **using the captured env** (not the system env) by looking for them in the captured PATH
- Groups checks by pipeline stage
- Every failure includes an install hint
- Writes `~/.foamscript/config.json` on success

Tool groups and install hints:

```csharp
private static readonly (string Name, string[] Tools, string InstallHint)[] ToolGroups = new[]
{
    ("Meshing Tools", new[] { "blockMesh", "snappyHexMesh", "surfaceFeatureExtract",
        "surfaceOrient", "surfaceCheck", "decomposePar", "reconstructParMesh", "checkMesh" },
        "Included with OpenFOAM"),
    ("Solver Tools", new[] { "simpleFoam", "pimpleFoam", "reconstructPar" },
        "Included with OpenFOAM"),
    ("Parallel Execution", new[] { "mpirun" },
        "sudo apt install openmpi-bin"),
    ("Geometry Processing", new[] { "gmsh" },
        "sudo apt install gmsh"),
    ("Flow Visualization", new[] { "pvpython" },
        "sudo apt install paraview"),
};
```

Python checks (matplotlib, numpy) use `Execute("python3", "-c \"import matplotlib\"")` with install hint `pip3 install matplotlib numpy`.

**Step 3: Rewrite tests**

Replace the existing EnvironmentServiceTests with tests for the new grouped structure. Key tests:
- All checks pass → `IsValid = true`, `TotalChecks == PassedChecks`
- Missing OpenFOAM → early return with detection info
- Missing tools → correct group, correct install hint
- Missing Python deps → correct install hints
- Config file written on success

**Step 4: Run tests**

Run: `dotnet test`
Expected: ALL PASS

**Step 5: Commit**

```bash
git add Models/EnvironmentValidationResult.cs Services/EnvironmentService.cs foamscript.Tests/Services/EnvironmentServiceTests.cs
git commit -m "feat: rewrite EnvironmentService with grouped checklist and install hints"
```

---

### Task 6: Rewrite ValidateHandler — Grouped Output

**Files:**
- Modify: `Handlers/ValidateHandler.cs`

**Step 1: Rewrite the handler**

The new handler iterates `result.Groups` and prints each with the approved format:

```
=== FoamScript Environment Validation ===

OpenFOAM
  ✓ Installation: /usr/lib/openfoam/openfoam2512
  ✓ Version: v2512
  ✓ Bashrc sourced successfully

Meshing Tools
  ✓ blockMesh          /path/to/blockMesh
  ✓ snappyHexMesh      /path/to/snappyHexMesh
  ...

=== 20/20 checks passed. FoamScript is ready to use. ===
```

Or on failure:
```
  ✗ pvpython           NOT FOUND
    Install: sudo apt install paraview

=== 18/20 checks passed. 2 issues must be resolved. ===
```

Remove the `--verbose` distinction — always show full paths. Keep `--quiet` for script use (only errors).

**Step 2: Run build and tests**

Run: `dotnet build && dotnet test`
Expected: ALL PASS

**Step 3: Commit**

```bash
git add Handlers/ValidateHandler.cs
git commit -m "feat: rewrite ValidateHandler with grouped output and pass/fail counts"
```

---

### Task 7: Clean Up VisualizationService

**Files:**
- Modify: `Services/VisualizationService.cs`

**Step 1: Remove redundant dependency checks**

The `GenerateSliceVisualization` method currently checks `which pvpython` and `python3 -c "import matplotlib"` at call time. These are now validated by `foamscript validate` and guaranteed by the pre-flight guard. Remove these checks — if we get to visualization, the tools are available.

Remove lines 43-58 (the `which pvpython` and `python3 -c "import matplotlib"` blocks). Keep the script-finding logic and the actual pvpython/python3 execution.

**Step 2: Run tests**

Run: `dotnet build && dotnet test`
Expected: ALL PASS

**Step 3: Commit**

```bash
git add Services/VisualizationService.cs
git commit -m "refactor: remove redundant tool checks from VisualizationService"
```

---

### Task 8: Integration Test on Linux

**Step 1: Push develop to remote**

```bash
git push origin develop
```

**Step 2: Pull and build on Linux**

```bash
ssh trboyden@192.168.1.223
cd /home/trboyden/foamscript && git pull origin develop
dotnet build --configuration Release
```

**Step 3: Run validate**

```bash
dotnet run --configuration Release -- validate
```

Expected: Full grouped checklist output with 20/20 checks passing. Config file written to `~/.foamscript/config.json`.

**Step 4: Run report on existing study**

```bash
dotnet run --configuration Release -- report -d /home/trboyden/OpenFOAM/trboyden-v2512/run/Apogee
```

Expected: Report generates successfully — the pre-flight guard sources the bashrc, ProcessExecutor has the env vars, all tools work.

**Step 5: Verify no-config error**

```bash
rm ~/.foamscript/config.json
dotnet run --configuration Release -- mesh -d /tmp/fake
```

Expected: "✗ FoamScript is not configured. Run 'foamscript validate' to set up your environment."

**Step 6: Commit any fixes, push**

---

### Task 9: Update Documentation

**Files:**
- Modify: `README.md` — update Installation section to mention `foamscript validate` as first step
- Modify: `Docs/Commands.md` — update `validate` command description
- Modify: `Docs/ExecutiveSummary.md` — update Environment Validation feature description

**Step 1: Update README**

Add to Quick Start section, before the existing commands:
```
# First time setup — validates and configures environment
foamscript validate
```

Update Prerequisites to note that `foamscript validate` checks all dependencies.

**Step 2: Update Commands.md validate section**

Document the new behavior: setup wizard, config file, grouped checklist.

**Step 3: Commit**

```bash
git add README.md Docs/Commands.md Docs/ExecutiveSummary.md
git commit -m "docs: update documentation for environment validation redesign"
```
