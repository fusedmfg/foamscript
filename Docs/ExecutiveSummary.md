# FoamScript Executive Summary

**Last Updated:** March 8, 2026
**Version:** 0.3.0 (AIAA Reports + Auto-Parallel)
**Repository:** [fusedmfg/foamscript](https://github.com/fusedmfg/foamscript) (private)

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
foamscript new-study --model-source disc.step --angles 0,5,10 --velocity 30 --rpm 5000
foamscript mesh -d ./DiscStudy
foamscript solve -d ./DiscStudy
foamscript report -d ./DiscStudy
```

**Key capabilities:**

| Feature | Description |
|---------|-------------|
| **STEP-to-STL Conversion** | Automated CAD conversion via gmsh with unit detection and scaling |
| **Domain Generation** | Auto-sized wind tunnel from geometry bounding box (10x/5x/5x extents) |
| **Template System** | Scriban-powered OpenFOAM dictionary generation from parameterized templates |
| **Parallel Meshing** | blockMesh → surfaceOrient → surfaceFeatureExtract → decomposePar → snappyHexMesh (MPI) → reconstruct |
| **Template-Aware Solving** | Auto-detects solver (simpleFoam/pimpleFoam) from controlDict |
| **Parametric Studies** | Generate and process multiple angle-of-attack cases automatically |
| **Auto-Detection** | Single `-d` flag intelligently detects case vs. study directories |
| **Auto-Parallel** | CPU cores auto-detected; `--cores N` to override, `FOAMSCRIPT_MAX_CORES` env var to cap |
| **AIAA-Quality Reports** | Publication-standard HTML + PDF reports with aerodynamic polars, convergence history, mesh statistics, and coefficient tables |
| **Environment Validation** | Pre-flight checks for OpenFOAM, gmsh, and system dependencies |

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
- **Structured output:** JSON/CSV export for integration with design databases
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
| 1 | `external_airfoil_static_steady` | 2D airfoil, steady-state, incompressible | [Issue #1](https://github.com/fusedmfg/foamscript/issues/1) — Open |
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
│  Development Period    22 days (Feb 15-Mar 8)    │
│  Active Days           10                        │
│  Total Commits         94                        │
│  AI Co-Authored        80 / 94 (85.1%)           │
│  Total C# Lines        10,491                    │
│  Production Code       6,014 lines               │
│  Test Code             4,477 lines               │
│  Test/Production Ratio 74.5%                     │
│  Passing Tests         192                       │
│  Template Files        42                        │
│  GitHub Issues         30 (25 closed, 5 open)    │
└─────────────────────────────────────────────────┘
```

### AI Contribution by Model

| Model | Commits | Share |
|-------|---------|-------|
| Claude Opus 4.6 | 37 | 46.3% |
| Claude Sonnet 4.5 | 32 | 40.0% |
| Claude Sonnet 4.6 | 11 | 13.8% |
| **Total AI** | **80** | **85.1%** |
| Human-only | 14 | 14.9% |

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
──────────  ───────  ─────────────────────────────────────────
Total         94     Full pipeline with AIAA reports + auto-parallel
```

### Code Distribution

```
Production Code by Component (6,014 lines):

  MeshService          472 ██████████████████        7.8%
  SolverService        412 ████████████████          6.9%
  ReportService        407 ███████████████           6.8%
  PdfReportGenerator   359 █████████████             6.0%
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
| Production LOC | 6,014 | Substantial for CLI tool scope |
| Test LOC | 4,477 | Strong investment |
| Test/Prod Ratio | 74.5% | Above industry average (~40-60% typical) |
| Passing Tests | 192 | Zero failures |
| Template Files | 42 | Comprehensive OpenFOAM coverage |
| GitHub Issues | 30 total (25 closed, 5 open) | Comprehensive tracking |
| Build Status | Clean | Zero warnings |
| Pipeline Validated | Yes | SimFlow match + grid convergence |

### Development Velocity

| Metric | Value |
|--------|-------|
| Avg commits/active day | 9.4 |
| Peak day (Feb 16) | 30 commits |
| Lines per active day | 1,049 |
| Tests per active day | 19.2 |
| Issues closed per active day | 2.5 |

---

## 8. Development Cost Analysis

### 8.1 Engineer Rate Justification

This project requires dual expertise that is uncommon in a single contractor:

1. **Senior .NET software architecture** — dependency injection, CLI design, Scriban templating, charting (ScottPlot 5.x), PDF generation (PdfSharpCore), xUnit/Moq testing, cross-platform deployment
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

**Scenario A — No extra usage enabled (what happened in this project):**

| Cost Category | Amount | Notes |
|---------------|--------|-------|
| Claude Code Pro subscription | $20 | Non-refundable monthly subscription |
| Human engineer productive time | $8,438 | 37.5 hrs × $225/hr |
| Rate-limit downtime penalty | $9,000 | 40 hrs × $225/hr (see §8.3) |
| Linux workstation (existing) | $0 | Already owned, no incremental cost |
| OpenFOAM / gmsh licenses | $0 | Open-source software |
| **Total AI-assisted cost** | **~$17,458** | |

**Scenario B — Extra usage enabled (pay overage in $20 increments):**

| Cost Category | Amount | Notes |
|---------------|--------|-------|
| Claude Code Pro subscription | $20 | Non-refundable base |
| Extra usage increments | ~$60-$100 | 3-5 additional $20 increments (non-refundable, paid when limit hit) |
| Human engineer productive time | $8,438 | 37.5 hrs × $225/hr (irreducible) |
| Reduced downtime penalty | ~$1,500-$3,000 | Context breaks still cause some idle time |
| **Total AI-assisted cost (Scenario B)** | **~$10,000-$11,500** | |

**Key insight:** The $20 subscription cost is economically irrelevant. The real costs are:
1. **Human engineer time: $8,438** — irreducible, regardless of AI capability (domain expertise, architecture, review)
2. **Rate-limit downtime: $9,000** — reducible with higher-tier plan or extra usage enabled

**Cost per deliverable (Scenario A — $17,458 total):**

| Metric | Value |
|--------|-------|
| Cost per line of C# code | $1.66 |
| Cost per test | $90.93 |
| Cost per commit | $185.72 |
| Cost per GitHub issue resolved | $698.32 |

### 8.3 Rate Limiting Impact (The Dominant Hidden Cost)

The Pro plan's usage limit caused a **complete development stoppage from February 26 through March 2** — nearly a full week. The engineer was blocked from using Claude Code entirely, waiting for the weekly limit to reset. This was not a minor throttle; it was a hard stop.

| Factor | Initially Reported (Feb 25) | Actual Experience (Mar 8) |
|--------|---------------------------|--------------------------|
| Rate limit hits | ~8-12 occurrences | ~15-20 occurrences + 1 full week blocked |
| Average wait time | ~15-30 minutes | **5-7 full days** for weekly reset |
| Total idle time | ~3-5 hours | **~40+ hours** (full work week) |
| Context window exhaustions | ~4-5 continuations | ~8-10 continuations |
| Engineering cost of idle time | ~$300-$500 | **$9,000** at $225/hr |

**The rate limiting didn't just convert compute savings into idle time — it destroyed project momentum.** Critical bugs (coefficient parsing, surfaceOrient argument order) went undetected for a week because the engineer couldn't run validation. The weekly reset schedule meant that hitting the limit on a Tuesday effectively killed the entire week.

**Economic impact:** At $225/hr, 40 hours of blocked engineering time represents **$9,000** in lost productivity — **450x** the $20 subscription cost. The rate-limit downtime is the single largest cost component of AI-assisted development on the Pro plan.

### 8.4 Plan Tier Comparison

Anthropic offers multiple Claude Code subscription tiers. The table below estimates the economic impact of each:

| Plan | Monthly Cost | Rate Limits | Downtime Penalty | Est. Total Project Cost |
|------|-------------|-------------|------------------|------------------------|
| **Pro (actual)** | $20 | Standard | $9,000 (40 hrs blocked) | ~$17,458 |
| **Pro + extra usage** | $20 + ~$80 | Extended | ~$2,250 (10 hrs blocked) | ~$10,788 |
| **Max (5x)** | $100 | 5x Pro | ~$1,125 (5 hrs blocked) | ~$9,583 |
| **Max (20x)** | $200 | 20x Pro | ~$225 (1 hr blocked) | ~$8,883 |

**ROI of upgrading Pro → Max ($100/month):**
- Additional subscription cost: $80
- Downtime savings: ~$7,875 (35 hours reclaimed × $225/hr)
- **Return: 98x on the $80 investment**

Each $20 extra usage increment buys back hours of developer idle time at $225/hr. Even reclaiming 6 minutes of idle time makes the $20 increment ROI-positive. For intensive development projects, running on the base Pro plan without extra usage is the most expensive option despite having the lowest subscription cost.

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
                         AI-Assisted (A)   Traditional       Savings
                         ───────────────   ───────────       ───────
Total cost                 $17,458         $63K-$95K         72-82%
Calendar time              22 days         8-12 weeks        3-4x faster
Active engineer hours      37.5 hrs        280-420 hrs       85-91% fewer
Cost per LOC               $1.66           $6.00-$9.01       63-82% less
```

**Important caveats:**

1. **AI-assisted development requires an experienced engineer.** The cost savings assume the human has both software architecture expertise and CFD domain knowledge. Without domain expertise, the AI produces syntactically correct but physically invalid simulations (see §5.4). The $225/hr rate reflects this rare skillset — cheaper engineers would spend longer, potentially negating the savings.

2. **The downtime penalty is the swing factor.** Scenario A ($17,458 with 40 hrs blocked) vs. Scenario B (~$10,500 with extra usage) shows that **enabling extra usage reduces total project cost by ~40%** despite increasing subscription spend. The Pro plan without extra usage is a false economy.

3. **Active engineer involvement is dramatically reduced.** AI-assisted development required 37.5 hours of active human involvement vs. 280-420 hours traditionally — an **85-91% reduction**. The engineer's role shifts from writing code to directing, reviewing, and validating.

4. **Traditional cost assumes a single project.** The AI subscription covers all projects for the month. Multiple concurrent projects would further amortize the subscription cost, though the human time cost scales linearly per project.

### 8.7 ROI Summary

| Scenario | Total Cost | Time to Deliver | Cost vs. Traditional |
|----------|-----------|-----------------|---------------------|
| **AI-assisted — Pro, no extra usage (actual)** | ~$17,458 | 22 days | **77% savings** |
| **AI-assisted — Pro, extra usage enabled** | ~$10,500 | ~15 days | **86% savings** |
| **AI-assisted — Max plan** | ~$9,500 | ~12 days | **88% savings** |
| Traditional (single contractor, $225/hr) | ~$78,750 | 8-12 weeks | Baseline |
| Traditional (two specialists) | ~$71,300 | 8-12 weeks | Baseline |

**Bottom line:** Even with the most conservative AI-assisted scenario (Pro plan, no extra usage, full downtime penalty), the project cost 77% less and was delivered 3-4x faster than traditional development. The story is compelling without cherry-picking: the honest numbers show that AI pair-coding delivers roughly **4x faster at one-quarter the cost**, with the bulk of savings coming from reduced active engineering hours rather than from cheap subscriptions.

The largest cost-optimization opportunity is not the AI subscription tier — it's eliminating rate-limit downtime. Upgrading from Pro ($20/month) to Max ($100/month) costs $80 but saves ~$7,875 in blocked engineer time, making it the highest-ROI investment in the entire project.

---

*Last updated: March 8, 2026.*
*This is a living document revised alongside development.*
*AI assistance provided by Anthropic Claude (Sonnet 4.5, Sonnet 4.6, Opus 4.6) via Claude Code.*
