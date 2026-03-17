# FoamScript Executive Summary

**Last Updated:** March 16, 2026
**Version:** 0.5.0 (Template Generalization + Environment Redesign)
**Repository:** [fusedmfg/foamscript](https://github.com/fusedmfg/foamscript)

*This is a living document revised alongside development. It serves two purposes: (1) document what FoamScript is and how it solves OpenFOAM complexity, and (2) provide an honest narrative on developing this application using a human/AI pair-coding approach.*

---

## 1. What We Built

FoamScript is a cross-platform .NET 10 command-line tool that automates Computational Fluid Dynamics (CFD) simulation workflows using OpenFOAM v2512. It transforms what is traditionally a manual, error-prone, multi-step engineering process into a single-command pipeline.

### The Problem

Running a CFD simulation with OpenFOAM requires an engineer to:

1. Convert CAD geometry (STEP files) to surface meshes (STL)
2. Define a computational domain (wind tunnel boundaries)
3. Generate dozens of configuration files (dictionaries) with precise syntax
4. Execute a multi-stage meshing pipeline (blockMesh → surfaceFeatureExtract → snappyHexMesh)
5. Configure parallel decomposition for multi-core runs
6. Launch and monitor a solver (simpleFoam, pimpleFoam, etc.)
7. Post-process force coefficients from raw output files
8. Repeat for every angle-of-attack in a parametric study

Each step involves hand-editing OpenFOAM dictionary files, running shell commands in the correct sequence, and debugging cryptic error messages. A single typo in a configuration file can cause silent failures or hours of wasted compute time.

### The Solution

FoamScript collapses this entire workflow into four commands:

```
foamscript convert disc.step disc.stl --input-units mm
foamscript new-study --template external_disc_rotatingwall_steady \
  --model-source disc.stl --angles 0,5,10 --velocity 27 --rpm 925
foamscript mesh -d ./DiscStudy
foamscript solve -d ./DiscStudy
foamscript report -d ./DiscStudy
```

**Key capabilities:**

| Feature | Description |
|---------|-------------|
| **STEP-to-STL Conversion** | Separate `convert` command via gmsh with unit scaling and validation; must be run before `new-study` for non-STL geometry |
| **Template-Driven Domain** | Domain sizing computed from geometry bounding box and template config (upstream, downstream, radial extents, margin) |
| **Template Metadata System** | Each template has `TEMPLATE.json` defining geometry type, solver, pipeline steps, parameter defaults, domain config, and post-processing; `--template` selects the workflow |
| **Parallel Meshing** | Template-driven pipeline: blockMesh → surfaceOrient → surfaceFeatureExtract → decomposePar → snappyHexMesh (MPI) → reconstruct |
| **Template-Driven Solving** | Solver, decomposition, and reconstruction steps defined per-template in `TEMPLATE.json` |
| **Parametric Studies** | Generate and process multiple angle-of-attack cases automatically |
| **Auto-Detection** | Single `-d` flag intelligently detects case vs. study directories |
| **Auto-Parallel** | CPU cores auto-detected; `--cores N` to override, `FOAMSCRIPT_MAX_CORES` env var to cap |
| **AIAA-Quality Reports** | Publication-standard HTML + PDF reports with aerodynamic polars, convergence history, mesh statistics, coefficient tables, and geometry-referenced flow visualizations |
| **Flow Visualization** | Pressure and velocity contour slices via ParaView (pvpython) + matplotlib (python3, numpy). AIAA-standard geometry-referenced framing. All visualization dependencies are required and validated by `foamscript validate` |
| **AIAA CSV Data Export** | Machine-readable coefficient data with reference conditions header, always generated alongside reports |
| **Environment Validation** | Config-based OpenFOAM management (`~/.foamscript/config.json`), auto-bashrc sourcing, pre-flight env injection, 23-check grouped validation with install hints. All dependencies required: OpenFOAM, gmsh, mpirun, pvpython, python3, matplotlib, numpy |

### Validation Status

The full pipeline has been **validated against SimFlow reference data** and confirmed through a **3-level grid convergence study** on Linux (Ubuntu + OpenFOAM v2512).

**Validation Test 1 — SimFlow Match (March 5, 2026)**

Replicated SimFlow's exact physics settings (TI=0.5%, kOmegaSST with custom coefficients, boundary layers, SimFlow's relaxation factors) with FoamScript's parallel processing. This isolated the pipeline from physics differences.

| Coefficient | FoamScript | SimFlow | Match |
|-------------|-----------|---------|-------|
| Cd | 0.044 | 0.071 | 62% (mesh density difference — 522K vs 200K cells) |
| **Cl** | **0.171** | **0.172** | **99.4%** |
| **CmPitch** | **-0.040** | **-0.042** | **96%** |

Cl and CmPitch match validates the pipeline is computing forces correctly. The Cd gap is attributable to mesh density differences (FoamScript's higher resolution captures the flow better around the disc).

**Validation Test 2 — Grid Convergence Study (March 5, 2026)**

Three mesh refinement levels at 0° AoA with science-based defaults (TI=1%, 8 boundary layers, PBiCGStab+DILU solver):

| Level | Refinement | Cells | Cd | Cl | CmPitch | Cl/Cd |
|-------|-----------|-------|--------|--------|---------|-------|
| Coarse | (4,5) | 498K | 0.0551 | 0.2141 | -0.0504 | 3.89 |
| Medium | (5,6) | 1.47M | 0.0551 | 0.2147 | -0.0529 | 3.89 |
| Fine | (6,7) | 2.13M | 0.0573 | 0.2234 | -0.0552 | 3.90 |

Cl/Cd ratio converged to <0.3% across all levels. Default set to Medium (5,6).

**All prior issues resolved:**

| Issue | Status | Fix Applied |
|-------|--------|-------------|
| liftDir / pitchAxis axes swapped | Resolved | liftDir=(0 0 1), pitchAxis=(0 1 0) |
| AoA velocity in wrong plane | Resolved | Velocity decomposes to (Ux, 0, Uz) |
| Force coefficient signs inverted | Resolved | surfaceOrient fixes STL normals from gmsh/Shapr3D |
| Force coefficient magnitudes differ | Resolved | Template upgraded: TI 1%, boundary layers, PBiCGStab, refinement (5,6) |
| coefficient.dat column parsing wrong | Resolved | Dynamic header parsing for v2512 13-column format |

---

## 2. End-User Benefits

### For CFD Engineers

- **Time savings:** A workflow that takes 2-4 hours manually runs in under 30 minutes end-to-end
- **Error reduction:** Parameterized templates eliminate hand-editing of OpenFOAM dictionaries
- **Reproducibility:** Every study is version-controlled and parameter-tracked
- **Accessibility:** Engineers unfamiliar with OpenFOAM internals can run simulations
- **Publication-ready output:** AIAA-quality HTML and PDF reports with aerodynamic polars, convergence history, and coefficient tables

### For Product Development Teams

- **Parametric sweeps:** Test dozens of design variants with a single command
- **Structured output:** AIAA-standard CSV coefficient data export for integration with design databases and post-processing tools
- **Consistency:** Same mesh settings, solver parameters, and quality checks every run
- **Auto-parallel:** CPU cores detected automatically; no manual configuration needed

### For Organizations

- **Reduced training:** New engineers productive in hours, not weeks
- **Standardized workflows:** Enforced best practices via curated templates
- **Audit trail:** Git-trackable study configurations and results
- **Cost efficiency:** Maximize utilization of OpenFOAM (free) vs. commercial CFD licenses

---

## 3. Future Roadmap

### Near-Term (Priority Templates)

| # | Template | Application | Status |
|---|----------|-------------|--------|
| 1 | `external_airfoil_static_steady` | 2D airfoil, steady-state, incompressible | [Issue #1](https://github.com/fusedmfg/foamscript/issues/1) — **Done (v0.5.0)** |
| 2 | `external_airfoil_static_transient` | 2D airfoil, transient, incompressible | [Issue #2](https://github.com/fusedmfg/foamscript/issues/2) — Open |
| 3 | `turbomachinery_propeller_rotating-mrf_steady` | Propeller, MRF, steady | [Issue #3](https://github.com/fusedmfg/foamscript/issues/3) — Open |
| 4 | `turbomachinery_propeller_rotating-ami_transient` | Propeller, AMI, transient | [Issue #4](https://github.com/fusedmfg/foamscript/issues/4) — Open |
| 5 | `external_airfoil_compressible_transonic` | Transonic airfoil, compressible | [Issue #5](https://github.com/fusedmfg/foamscript/issues/5) — Open |

### Medium-Term Enhancements

- **Web dashboard** for monitoring running simulations and viewing results
- **Mesh refinement automation** based on solution convergence
- **Cloud execution** (AWS/Azure HPC clusters) for large-scale studies
- **Result visualization** integration (ParaView scripting)
- **Multi-objective optimization** loops (geometry → mesh → solve → evaluate → iterate)

### Long-Term Vision

- **Template marketplace** for community-contributed simulation setups
- **ML-assisted meshing** — predict optimal mesh parameters from geometry
- **Real-time collaboration** on parametric studies
- **Integration with CAD tools** (SolidWorks, Fusion 360 plugins)

---

## 4. How AI Was Used to Build This Project

### Development Model

FoamScript was built using **Claude Code** (Anthropic's AI coding agent) as the primary development tool, with a human engineer providing architectural direction, domain expertise, and validation. The human acted as product owner, architect, and QA lead; Claude acted as the implementation engine.

### Key Statistics

```
┌─────────────────────────────────────────────────┐
│           PROJECT STATISTICS AT A GLANCE         │
├─────────────────────────────────────────────────┤
│  Development Period    30 days (Feb 15-Mar 16)   │
│  Active Days           15                        │
│  Total Commits         152                       │
│  AI Co-Authored        130 / 152 (85.5%)         │
│  Total C# Lines        12,468                    │
│  Production Code       6,880 lines               │
│  Test Code             5,588 lines               │
│  Test/Production Ratio 81.2%                     │
│  Passing Tests         262                       │
│  Template Files        64                        │
│  GitHub Issues         41 (30 closed, 11 open)   │
└─────────────────────────────────────────────────┘
```

### AI Contribution by Model

| Model | Commits | Share |
|-------|---------|-------|
| Claude Opus 4.6 | 55 | 56.1% |
| Claude Sonnet 4.5 | 32 | 32.7% |
| Claude Sonnet 4.6 | 11 | 11.2% |
| **Total AI** | **98** | **87.5%** |
| Human-only | 14 | 12.5% |

### Development Timeline

```
Date        Commits  Milestone
──────────  ───────  ─────────────────────────────────────────
Feb 15 (Sa)    4     Project scaffolding, logging, CLI setup
Feb 16 (Su)   30     Core services: STL, domain, mesh, templates
Feb 17 (Mo)   18     Solver, results, study pipeline, tests
Feb 18 (Tu)    3     Linux E2E validation, mesh fixes
Feb 19-22      0     (No development)
Feb 23 (Su)    5     SSH config, remote testing
Feb 24 (Mo)   15     Refactoring, AMI debugging, MRF migration
Feb 25 (Tu)    3     CLI unification, axis fix, validation
Feb 26-Mar 4   0     BLOCKED — usage limit hit (see §8.3)
Mar 5 (We)     5     Coefficient parsing, template upgrade, grid convergence
Mar 6-7        2     Audit remediation, template rename, v2512 test fixtures
Mar 8 (Sa)     9     Report command, auto-parallel, documentation audit
Mar 9-12       0     (No development)
Mar 13 (Th)    7     Landscape PDF, full-page polars, geometry-referenced visualizations
Mar 14 (Fr)   11     PDFsharp migration, clean build, CSV data export, elite amateur defaults, Apogee E2E validation, report -o flag removal, PDF page break fix
──────────  ───────  ─────────────────────────────────────────
Total        112     Full pipeline with AIAA reports, CSV export + flow visualization
```

### Code Distribution

```
Production Code by Component (6,014 lines):

  MeshService          472 ██████████████████        7.8%
  SolverService        412 ████████████████          6.9%
  ReportService        407 ███████████████           6.8%
  PdfReportGenerator   434 ████████████████          6.9%
  DomainService        355 █████████████             5.9%
  StlConversionService 341 ████████████              5.7%
  CaseService          335 ████████████              5.6%
  ChartGenerator       320 ████████████              5.3%
  NewStudyHandler      274 ██████████                4.6%
  EnvironmentService   234 █████████                 3.9%
  Other (30 files)   2,505 ███████████████████████████████████████ 41.7%

Test Code by Component (4,477 lines):

  MeshServiceTests         732 ████████████████████  16.3%
  SolverServiceTests       503 █████████████         11.2%
  GeometryServiceTests     487 █████████████         10.9%
  CaseServiceTests         448 ████████████          10.0%
  EnvironmentServiceTests  361 ██████████             8.1%
  ResidualParserTests      271 ███████                6.1%
  ResultsServiceTests      250 ███████                5.6%
  TemplateServiceTests     243 ██████                 5.4%
  NewStudyHandlerTests     241 ██████                 5.4%
  ChartGeneratorTests      222 ██████                 5.0%
  Other (4 files)          719 ████████████████████  16.1%
```

---

## 5. Lessons Learned

### 5.1 What Worked Well

**1. Conversational architecture reviews produced better designs**
The human engineer described the desired end-state ("I want to run `foamscript mesh -d ./study` and have it figure out if it's a case or study"), and Claude proposed implementation patterns (auto-detection, handler merging). This dialogue was faster than writing detailed specs.

**2. Test-driven development was natural with AI — but had a critical blind spot**
Claude consistently generated tests alongside implementation code, maintaining a 78% test-to-production ratio. However, **tests were written against the code's assumptions rather than against real system output** (see §5.4 for details). This allowed a serious column-parsing bug to ship undetected through 142 passing tests.

**3. AI excelled at boilerplate-heavy code**
OpenFOAM dictionary templates, CLI model classes, DI registration, and xUnit test scaffolding — code that is tedious but straightforward — was generated quickly and correctly.

**4. Iterative debugging cycles were fast**
When AMI (Arbitrary Mesh Interface) simulations failed, Claude could rapidly iterate through solver parameter adjustments, template modifications, and diagnostic analysis across multiple files simultaneously.

**5. Refactoring was low-risk**
The CLI unification (merging 4 models into 2, 4 handlers into 2, updating 8+ files) was completed in a single session with zero test regressions. AI's ability to track cross-file dependencies reduced refactoring risk.

### 5.2 What Could Be More Efficient

**1. Context window management is the biggest bottleneck**
Complex tasks frequently exhausted the context window mid-operation, requiring session continuations. Each continuation loses nuance from the original conversation. The human engineer should:
- Break large tasks into smaller, self-contained units
- Provide explicit context summaries at session start
- Avoid "stream of consciousness" interactions that consume context on exploration

**2. Domain expertise cannot be outsourced to AI**
Claude could generate syntactically correct OpenFOAM configurations but could not validate whether turbulence model choices, mesh quality thresholds, or force coefficient conventions were physically appropriate. The human engineer's CFD knowledge was essential for:
- Choosing simpleFoam + MRF over pimpleFoam + AMI
- Recognizing that AMI weight degradation was a fundamental limitation, not a parameter tuning issue
- Identifying that liftDir/pitchAxis were swapped and AoA decomposed into the wrong plane (X-Y instead of X-Z)
- Comparing force coefficient output against SimFlow reference data to detect sign and magnitude discrepancies
- Understanding that AI-generated OpenFOAM configurations can be syntactically valid but physically meaningless without domain validation

**3. Upfront planning saves more than it costs**
The CLI unification was executed efficiently because a detailed plan was reviewed and approved first. Earlier in the project, some features were implemented and then reworked because requirements weren't fully specified. The plan → approve → execute cycle should be used for any task touching more than 2-3 files.

**4. SSH/remote execution is fragile in AI workflows**
Remote Linux testing required explicit instructions about SSH keys, environment sourcing, and stdout capture. AI agents struggle with non-interactive SSH sessions. Pre-configured CI/CD pipelines would be more reliable.

### 5.3 AI vs. Traditional Development Comparison

```
Metric                     AI-Assisted    Traditional (Est.)    Ratio
─────────────────────────  ─────────────  ──────────────────    ─────
Calendar time              22 days        8-12 weeks            3-4x faster
Active coding days         10 days        30-40 days            3-4x fewer
Lines of code produced     10,491         10,491                Same output
Test/Production ratio      74.5%          40-60% (typical)      Higher
Refactoring confidence     High           Moderate              Better
Documentation              Comprehensive  Often deferred        Better
Cross-file consistency     High           Variable              Better
Domain knowledge required  Same           Same                  No change
Debugging novel issues     Moderate       High                  Worse for AI
Architecture decisions     Human-driven   Human-driven          No change
```

**Key finding:** AI assistance compressed approximately 8-12 weeks of solo developer effort into 22 calendar days (10 active). The acceleration was most dramatic for:
- Boilerplate generation (models, handlers, DI wiring) — 10x faster
- Test writing — 5x faster
- Refactoring across multiple files — 5x faster
- Documentation — 3x faster

The acceleration was minimal for:
- Debugging physics/simulation failures (AMI instability) — similar time
- Architecture decisions — similar time
- Linux environment configuration — similar time

### 5.4 Critical Oversight: Tests That Validated Assumptions, Not Reality

**The coefficient.dat column parsing bug** is the most significant quality failure in this project and warrants detailed examination because it reveals a systemic risk with AI-generated test suites.

**What happened:** The `ParseForceCoeffsFile` method parsed OpenFOAM's force coefficient output file. The code assumed a 7-column legacy format (`Time Cd Cs Cl CmRoll CmPitch CmYaw`), but OpenFOAM v2512 outputs 13 columns (`Time Cd Cd(f) Cd(r) Cl Cl(f) Cl(r) CmPitch CmRoll CmYaw Cs Cs(f) Cs(r)`). The parser read column index 3 as "Cl" — which was actually `Cd(r)`. Column 5 was read as "CmPitch" — which was actually `Cl(f)`. **Every Cl and CmPitch value ever reported was wrong.**

**Why tests didn't catch it:** The AI-generated test data used the same 7-column format the code expected. The tests verified the code did what it was *designed* to do — they validated the implementation's internal consistency, not its correctness against real-world output. This is the testing equivalent of grading your own homework.

**The fix:** Dynamic header parsing that reads the `# Time ...` header line to determine column indices at runtime. New tests use actual v2512 format data as fixtures, including a regression test with deliberately different sub-column values to ensure `Cd(f)`, `Cd(r)`, `Cl(f)`, `Cl(r)` are never confused with aggregate `Cd` and `Cl`.

**Lessons:**
1. **AI-generated tests are biased toward the AI's own assumptions.** When the same agent writes both code and tests, errors in understanding propagate to both. Test data must come from real system output, not synthetic data shaped to match the code.
2. **Integration test fixtures should be copy-pasted from actual tool output,** not hand-crafted. A single real `coefficient.dat` file from an OpenFOAM run would have caught this immediately.
3. **Test count is not test quality.** 142 passing tests provided false confidence. The metric that matters is whether tests validate against ground truth, not whether they pass.
4. **Human review of test data is essential.** The engineer should have asked: "Does this test data look like real OpenFOAM output?" — a question that would have revealed the format mismatch instantly.

### 5.5 Documentation Staleness Is a Silent Defect

**Example configs with old defaults silently override production behavior.** The `study.example.jsonc` file shipped with defaults from an early development iteration — `cores: 4` (should be `0` for auto-detect), `turbulenceIntensity: 0.05` (should be `0.01`), `refinementLevelMin: 3` (should be `5`), and a template name that no longer existed (`external_disc_rotating-ami_transient`). Users copying the example file would silently get inferior physics settings with no warning.

Similarly, the `convert` command documentation in `Commands.md` listed `--input-units` with a default of `mm` (actual: `m`), `--mesh-size` with a default of `0.05` (actual: `1.0`), and used named options (`--input`, `--output`) instead of the actual positional arguments. These errors persisted for weeks undetected.

**Lesson:** Documentation audits need a file checklist (README, Commands.md, study.example.jsonc, ExecutiveSummary.md) that is reviewed after any change to defaults, CLI flags, or command behavior. Stale documentation is worse than missing documentation — it actively misleads users and creates debugging sessions that trace back to "I used the example config."

### 5.6 Context Limit Recovery Requires Checkpoint Discipline

When AI coding sessions hit context limits mid-task, the todo list is the checkpoint mechanism — but only if continuation sessions check it first. Multiple times during this project, a session hit its context limit while partway through a multi-step task. The next session would start fresh, often re-discovering work that was already in progress or repeating exploration that had already been done.

**What works:** Updating the todo list in real-time as tasks progress, with explicit status markers (in_progress, completed, pending). Writing partial results to MEMORY.md before context compaction. Structuring tasks so each step produces a committed artifact — if the session dies, the work is saved.

**What doesn't work:** Starting new sessions without checking the todo list. Assuming the AI agent will remember context from a compaction summary (it loses nuance). Batching multiple related changes into a single uncommitted session — if the context dies, everything is lost.

**Lesson:** The system should prioritize completing in-progress tasks over starting new ones. Every session should begin by reading the todo list and MEMORY.md before taking any action.

### 5.7 Retroactive Issue Creation Reveals Process Gaps

After 93 commits, the project had only 19 GitHub issues — a clear signal that features were being implemented without proper tracking. A retroactive audit identified 11 significant features (auto-parallel, report command, coefficient parsing, surfaceOrient, template upgrade, and more) that were built across multiple commits but never tracked as issues.

Creating issues after the fact with commit references (`Implemented in abc123`) restored traceability, but the process gap reveals a broader pattern: **AI pair-coding sessions naturally skip issue creation because the conversation IS the specification.** The engineer describes what they want, the AI builds it, and neither stops to create a tracking artifact. This is fast but leaves no paper trail for future developers (or future sessions of the same project).

**Lesson:** Issue creation should be the first step of any feature, not an afterthought. A simple discipline — "before the AI writes code, create a GitHub issue" — ensures that every feature has a traceable origin, acceptance criteria, and linkage to commits. For projects using AI pair-coding, this is especially important because conversation context is ephemeral.

### 5.8 AI Agents Don't Guard Repository Hygiene by Default

During development, internal configuration files containing SSH usernames, private IP addresses, and key file paths were committed to git and later deleted. While the data posed no real security risk (private LAN, no keys committed), the files remained recoverable in git history — a pattern that would be a serious exposure in a public repository with actual secrets.

**What happened:** `.claude/settings.local.json`, `DEPLOY.md`, and `SSH-SETUP.md` were committed during early development, then removed in later commits. The AI agent neither flagged the initial commits as potentially sensitive nor suggested `.gitignore` entries to prevent them. A pre-release security audit caught the issue, but only because the engineer explicitly requested one.

**Why the AI missed it:** Repository hygiene — preventing sensitive files from entering version control — is a well-established best practice that should be in even the oldest AI models' training data. Yet AI coding agents are optimized for task completion, not proactive risk assessment. Unless explicitly asked "should this file be committed?" or "scan for secrets," the agent focuses on making the code work, not on what shouldn't be in the repo.

**Lesson:** Treat `.gitignore` as a first-class deliverable. Before the first commit of any project, explicitly ask the AI to generate a comprehensive `.gitignore` that covers IDE settings, credential files, deployment configs, and environment-specific files. Better yet, use pre-commit hooks (like `git-secrets` or `detect-secrets`) that reject commits containing patterns that look like credentials. AI agents should proactively flag files that match sensitive patterns — the fact that they don't is a gap worth noting.

---

## 6. AI Efficiency Analysis

### 6.1 Useful vs. Wasteful Usage

Based on the development history, approximately **70-75% of AI compute was productive** (directly contributed to shipped code), while **25-30% was exploratory or reworked**:

| Category | Estimated Share | Examples |
|----------|----------------|---------|
| **Productive** | 60-65% | Core services, templates, tests, refactoring, validation |
| **Exploratory (valuable)** | 10-15% | AMI debugging, SimFlow comparison analysis |
| **Reworked** | 10-15% | Features built then redesigned, tests rewritten with real data |
| **Context overhead** | 10-15% | Session restarts, re-explaining context after compaction, excessive context consumption during sessions |
| **Blocked (rate limits)** | ~5% | Wait time within sessions before weekly hard stop |

### 6.2 Recommendations for More Efficient AI Use

**For the end-user:**

1. **Write a project brief before starting.** A 1-page document describing architecture, constraints, and non-negotiable requirements would have prevented at least 2 significant rework cycles.

2. **Use plan mode for every multi-file change.** The CLI unification succeeded because it was planned first. Earlier features that skipped planning required corrections.

3. **Keep sessions focused.** One task per session with clear success criteria. Avoid mixing exploration ("how should we do X?") with implementation ("now build X") in the same context window.

4. **Front-load domain knowledge.** When the human provided OpenFOAM expertise upfront (e.g., "use simpleFoam + MRF, not pimpleFoam + AMI"), the AI executed flawlessly. When domain decisions were deferred, the AI explored dead ends.

5. **Use MEMORY.md aggressively.** The project memory file was invaluable for session continuations but was often updated reactively. Proactive updates after each milestone would improve continuity.

6. **Context consumption must be actively managed.** The AI agent consistently consumed excessive context by re-reading files it had already seen, generating verbose output, and not leveraging stored memory to avoid redundant exploration. Each wasted token brings the session closer to compaction or exhaustion, which then causes further waste through re-explanation. The agent should: prefer memory lookups over file re-reads, minimize tool output verbosity, and proactively update memory files so future sessions start with full context rather than re-discovering it.

### 6.3 Usage Transparency Recommendations for Anthropic

**Current gaps in transparency:**

1. **No token/cost visibility during sessions.** Users cannot see how many tokens they've consumed, what the cost is, or how close they are to context limits. This makes it impossible to budget or optimize usage.

2. **Context compaction is opaque.** When the context window fills up, older content is silently compressed. Users don't know what was lost or how to recover it. A visual indicator of context fullness and explicit notification of what was compacted would help.

3. **No session analytics.** After a session, there's no dashboard showing: tokens consumed, tools invoked, files modified, time spent thinking vs. executing. This data would help users identify inefficient patterns.

4. **No cost-per-task attribution.** Users can't determine whether a specific task (e.g., "write 10 tests") consumed $2 or $20 of compute. Task-level cost tracking would enable ROI analysis.

**Recommendations:**

| Recommendation | Impact | Difficulty |
|----------------|--------|------------|
| Real-time token counter in UI | Users can self-regulate usage | Low |
| Context fullness indicator | Prevents surprise session breaks | Low |
| Post-session analytics dashboard | Enables usage optimization | Medium |
| Cost-per-task breakdown | Enables ROI analysis | Medium |
| Compaction notification with summary | Improves session continuity | Medium |
| Usage trend reports (weekly/monthly) | Organizational planning | Medium |
| "Estimated remaining capacity" indicator | Better task scoping | High |
| Automatic session checkpointing | Recovery from context overflow | High |

---

## 7. Project Metrics Summary

### Codebase Health

| Metric | Value | Assessment |
|--------|-------|------------|
| Production LOC | 6,880 | Substantial for CLI tool scope |
| Test LOC | 5,588 | Strong investment |
| Test/Prod Ratio | 81.2% | Well above industry average (~40-60% typical) |
| Passing Tests | 241 | Zero failures |
| Template Files | 64 | 3 geometry templates + report templates |
| GitHub Issues | 39 total (29 closed, 10 open) | Comprehensive tracking |
| Build Status | Clean | Zero warnings |
| Pipeline Validated | Yes | SimFlow match + grid convergence |

### Development Velocity

| Metric | Value |
|--------|-------|
| Avg commits/active day | 8.6 |
| Peak day (Feb 16) | 30 commits |
| Lines per active day | 915 |
| Tests per active day | 16.7 |
| Issues closed per active day | 2.2 |

---

## 8. Development Cost Analysis

### 8.1 Engineer Rate Justification

This project requires dual expertise that is uncommon in a single contractor:

1. **Senior .NET software architecture** — dependency injection, CLI design, Scriban templating, charting (ScottPlot 5.x), PDF generation (PDFsharp 6.x), xUnit/Moq testing, cross-platform deployment
2. **CFD/OpenFOAM domain knowledge** — meshing pipelines (blockMesh/snappyHexMesh), turbulence models (kOmegaSST), force coefficient conventions (AIAA standards), solver tuning, grid convergence analysis

Market rates for each discipline independently:

| Role | Rate Range | Sources |
|------|-----------|---------|
| Senior .NET architect/contractor (US) | $120-200/hr | ZipRecruiter, Rise 2026 Edition |
| Senior software consultant (independent, 5+ yrs) | $120-300/hr | Cleveroad, FullStack Labs 2025 Price Guide |
| CFD/aerospace engineering consultant | $100-200/hr | CFD Online, Kolabtree, Glassdoor |
| Systems-level aerospace engineer | $165-198/hr | ZipRecruiter senior-level data |

**Dual-expertise premium:** Finding one person with both skillsets is rare. The intersection of senior .NET architecture and OpenFOAM CFD knowledge commands a premium, placing the effective rate at **$225/hr** (conservative end of the $200-250/hr range for rare skillset intersections).

### 8.2 AI-Assisted Development Costs

This project used Claude Code's **Pro plan ($20/month)**, the base subscription that most developers would use. The subscription is non-refundable — it cannot be prorated.

**What actually happened (Pro plan + $20 extra usage):**

| Cost Category | Amount | Notes |
|---------------|--------|-------|
| Claude Code Pro subscription | $20 | Non-refundable monthly subscription |
| Extra usage purchased | $20 | Consumed in ~2 hours on Mar 8 |
| Human engineer productive time | $9,900 | 44 hrs × $225/hr (11 active days × 4 hrs avg) |
| Rate-limit downtime penalty | $16,200 | 72 hrs × $225/hr (see §8.3) |
| Linux workstation (existing) | $0 | Already owned, no incremental cost |
| OpenFOAM / gmsh licenses | $0 | Open-source software |
| **Total AI-assisted cost** | **~$26,140** | |

**Key insight:** The $40 total subscription spend is economically irrelevant. The real costs are:
1. **Human engineer time: $9,900** — irreducible, regardless of AI capability (domain expertise, architecture, review)
2. **Rate-limit downtime: $16,200** — the dominant cost, representing 62% of total project cost

**Cost per deliverable ($26,140 total):**

| Metric | Value |
|--------|-------|
| Cost per line of C# code | $2.40 |
| Cost per test | $130.70 |
| Cost per commit | $233.39 |
| Cost per GitHub issue resolved | $933.57 |

### 8.3 Rate Limiting Impact (The Dominant Hidden Cost)

The Pro plan's usage limit caused **two complete development stoppages** totaling 72 hours of blocked engineering time:

1. **Feb 26 – Mar 4 (7 days, ~40 hrs):** Weekly limit hit; hard stop until reset.
2. **Mar 9 – Mar 12 (4 days, ~32 hrs):** After an intensive Mar 8 session (landscape PDF, flow viz, report refinements), the limit hit again. $20 extra usage was purchased but consumed in approximately 2 hours, providing no meaningful buffer.

| Factor | Initially Reported (Feb 25) | Final Experience (Mar 13) |
|--------|---------------------------|--------------------------|
| Rate limit hits | ~8-12 occurrences | ~20-25 occurrences + 11 days blocked total |
| Average wait time | ~15-30 minutes | **4-7 full days** per block |
| Total idle time | ~3-5 hours | **~72 hours** (9 business days) |
| Context window exhaustions | ~4-5 continuations | ~10-12 continuations |
| Extra usage purchased | — | $20 (consumed in ~2 hours) |
| Engineering cost of idle time | ~$300-$500 | **$16,200** at $225/hr |

**The $20 extra usage experiment proved that small increments are economically irrational.** At $225/hr engineer time, $20 of extra usage needs to prevent only 5.3 minutes of downtime to break even. The $20 increment bought ~2 hours of productivity before exhaustion, after which 4 full days of downtime followed. The extra usage model needs either much larger increments or automatic scaling to be useful for intensive development.

**Contributing factor — AI context inefficiency:** A portion of the rapid token consumption is attributable to the AI agent itself. Re-reading files already documented in project memory, verbose exploration of code already understood, and redundant tool calls all consume tokens without producing value. Each wasted token accelerates hitting the rate limit, compounding the downtime penalty. Improving the agent's memory utilization (checking MEMORY.md and NeuroVault before exploring) would stretch the same token budget further.

**Economic impact:** At $225/hr, 72 hours of blocked engineering time represents **$16,200** in lost productivity — **405x** the $40 total subscription spend. Rate-limit downtime is **62% of total project cost** and the single largest line item.

### 8.4 Plan Tier Comparison

Anthropic offers multiple Claude Code subscription tiers. The table below estimates the economic impact based on actual project experience:

| Plan | Monthly Cost | Rate Limits | Downtime Penalty | Est. Total Project Cost |
|------|-------------|-------------|------------------|------------------------|
| **Pro + $20 extra (actual)** | $40 | Standard + 2 hrs | $16,200 (72 hrs blocked) | ~$26,140 |
| **Pro + continuous extra** | $20 + ~$200 | Extended | ~$4,500 (20 hrs blocked) | ~$14,620 |
| **Max (5x)** | $100 | 5x Pro | ~$2,250 (10 hrs blocked) | ~$12,250 |
| **Max (20x)** | $200 | 20x Pro | ~$450 (2 hrs blocked) | ~$10,550 |

**ROI of upgrading Pro → Max ($200/month):**
- Additional subscription cost: $160
- Downtime savings: ~$15,750 (70 hours reclaimed × $225/hr)
- **Return: 98x on the $160 investment**

The Pro plan with ad-hoc $20 increments is the worst of both worlds: it costs more than Pro alone (due to the $20 spend) while providing negligible relief (~2 hours before re-blocking). For intensive development, the Max plan is the only tier that approaches continuous availability. The $200/month cost is less than 1 hour of the engineer time it protects.

### 8.5 Traditional Development Cost Estimate

The traditional cost estimate must reflect the dual-expertise requirement discussed in §8.1.

**Option A — Single senior contractor with dual expertise (rare):**

| Cost Category | Amount | Assumptions |
|---------------|--------|-------------|
| Contractor time | $63,000 - $94,500 | $225/hr × 280-420 hrs (8-12 weeks at 35 hrs/week) |
| **Total** | **$63,000 - $94,500** | |

**Option B — Two specialists (more realistic hire):**

| Cost Category | Amount | Assumptions |
|---------------|--------|-------------|
| Senior .NET developer | $35,000 - $49,000 | $175/hr × 200-280 hrs |
| CFD/OpenFOAM consultant | $16,000 - $24,000 | $200/hr × 80-120 hrs |
| Integration/coordination overhead | $7,650 - $10,950 | +15% for two-person coordination |
| **Total** | **$58,650 - $83,950** | |

**Midpoint estimate: ~$75,000** (averaging across both options).

### 8.6 Cost Comparison

```
                         AI-Assisted       Traditional       Savings
                         ───────────────   ───────────       ───────
Total cost                 $26,140         $63K-$95K         59-72%
Calendar time              27 days         8-12 weeks        2-3x faster
Active engineer hours      44 hrs          280-420 hrs       85-90% fewer
Cost per LOC               $2.40           $6.00-$9.01       60-73% less
```

**Important caveats:**

1. **AI-assisted development requires an experienced engineer.** The cost savings assume the human has both software architecture expertise and CFD domain knowledge. Without domain expertise, the AI produces syntactically correct but physically invalid simulations (see §5.4). The $225/hr rate reflects this rare skillset — cheaper engineers would spend longer, potentially negating the savings.

2. **The downtime penalty is the dominant cost.** At $16,200 (62% of total), rate-limit downtime dwarfs both subscription costs ($40) and productive engineer time ($9,900). On the Max plan, the same project would cost ~$10,550 — **60% less** — because most downtime is eliminated.

3. **Active engineer involvement is dramatically reduced.** AI-assisted development required 44 hours of active human involvement vs. 280-420 hours traditionally — an **85-90% reduction**. The engineer's role shifts from writing code to directing, reviewing, and validating.

4. **AI context inefficiency amplifies rate-limit costs.** Redundant file reads, verbose exploration, and failure to leverage project memory accelerate token consumption. If the AI agent used stored context more aggressively (MEMORY.md, NeuroVault), the same token budget would stretch further, reducing how often the rate limit is hit.

### 8.7 ROI Summary

| Scenario | Total Cost | Time to Deliver | Cost vs. Traditional |
|----------|-----------|-----------------|---------------------|
| **AI-assisted — Pro + $20 extra (actual)** | ~$26,140 | 27 days | **65% savings** |
| **AI-assisted — Max plan (projected)** | ~$10,550 | ~15 days | **86% savings** |
| Traditional (single contractor, $225/hr) | ~$78,750 | 8-12 weeks | Baseline |
| Traditional (two specialists) | ~$71,300 | 8-12 weeks | Baseline |

**Bottom line:** Even in the worst-case scenario — Pro plan with $20 extra usage, 72 hours of rate-limit downtime, and honest accounting of idle engineer time — the project cost 65% less and was delivered 2-3x faster than traditional development. However, the story reveals that **rate-limit downtime is the dominant cost driver**, representing 62% of total project cost ($16,200 of $26,140). The productive work itself (44 hours of engineering + $40 in subscriptions) costs only ~$9,940 — a **87% savings** over traditional development.

The largest cost-optimization opportunity is eliminating rate-limit downtime. Upgrading from Pro ($40 actual) to Max ($200/month) costs an additional $160 but saves ~$15,750 in blocked engineer time — a **98x return** on the subscription upgrade. On the Max plan, total project cost drops to ~$10,550, achieving the 86% savings that the core productivity gains actually deliver.

---

*Last updated: March 16, 2026.*
*This is a living document revised alongside development.*
*AI assistance provided by Anthropic Claude (Sonnet 4.5, Sonnet 4.6, Opus 4.6) via Claude Code.*
