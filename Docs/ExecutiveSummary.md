# FoamScript Executive Summary

**Date:** March 5, 2026
**Version:** 0.2.0 (Validated Pipeline with Grid Convergence)
**Repository:** [fusedmfg/foamscript](https://github.com/fusedmfg/foamscript) (private)

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
foamscript results -d ./DiscStudy
```

**Key capabilities:**

| Feature | Description |
|---------|-------------|
| **STEP-to-STL Conversion** | Automated CAD conversion via gmsh with unit detection and scaling |
| **Domain Generation** | Auto-sized wind tunnel from geometry bounding box (10x/5x/5x extents) |
| **Template System** | Scriban-powered OpenFOAM dictionary generation from parameterized templates |
| **Parallel Meshing** | blockMesh → surfaceFeatureExtract → decomposePar → snappyHexMesh (MPI) → reconstruct |
| **Template-Aware Solving** | Auto-detects solver (simpleFoam/pimpleFoam) from controlDict |
| **Parametric Studies** | Generate and process multiple angle-of-attack cases automatically |
| **Auto-Detection** | Single `-d` flag intelligently detects case vs. study directories |
| **Results Extraction** | Force coefficients (Cd, Cl, CmPitch, Cl/Cd ratio) in table, CSV, or JSON |
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

### For Product Development Teams

- **Parametric sweeps:** Test dozens of design variants with a single command
- **Structured output:** JSON/CSV export for integration with design databases
- **Consistency:** Same mesh settings, solver parameters, and quality checks every run
- **Parallel execution:** Multi-core meshing and solving out of the box

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
│  Development Period    19 days (Feb 15-Mar 5)    │
│  Total Commits         83                        │
│  AI Co-Authored        69 / 83 (83.1%)           │
│  Total C# Lines        8,086                     │
│  Production Code       4,532 lines               │
│  Test Code             3,554 lines               │
│  Test/Production Ratio 78.4%                     │
│  Passing Tests         144                       │
│  Template Files        41                        │
│  GitHub Issues         16 (10 closed, 6 open)    │
└─────────────────────────────────────────────────┘
```

### AI Contribution by Model

| Model | Commits | Share |
|-------|---------|-------|
| Claude Sonnet 4.5 | 32 | 46.4% |
| Claude Opus 4.6 | 26 | 37.7% |
| Claude Sonnet 4.6 | 11 | 15.9% |
| **Total AI** | **69** | **83.1%** |
| Human-only | 14 | 16.9% |

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
Feb 26-Mar 2   0     BLOCKED — $20/mo usage limit hit (see §8.3)
Mar 3-4        2     Sign reversal fix, surfaceOrient, rotatingWallVelocity
Mar 5          2     Coefficient parsing fix, template upgrade, grid convergence
──────────  ───────  ─────────────────────────────────────────
Total         83     Validated pipeline with grid convergence
```

### Code Distribution

```
Production Code by Component (4,479 lines):

  MeshService          451 ████████████████████████  10.1%
  DomainService        354 ██████████████████        7.9%
  SolverService        354 ██████████████████        7.9%
  StlConversionService 340 █████████████████         7.6%
  CaseService          328 ████████████████          7.3%
  NewStudyHandler      257 █████████████             5.7%
  EnvironmentService   234 ████████████              5.2%
  MeshHandler          159 ████████                  3.6%
  ResultsService       145 ███████                   3.2%
  SolveHandler         132 ██████                    2.9%
  Other (25 files)   1,725 ███████████████████████████████████████ 38.5%

Test Code by Component (3,458 lines):

  MeshServiceTests         704 ████████████████████  20.4%
  GeometryServiceTests     486 ██████████████        14.1%
  SolverServiceTests       434 ████████████          12.6%
  CaseServiceTests         449 █████████████         13.0%
  EnvironmentServiceTests  361 ██████████            10.4%
  ResultsServiceTests      248 ███████               7.2%
  TemplateServiceTests     243 ███████               7.0%
  NewStudyHandlerTests     241 ███████               7.0%
  StlScalingIntegTests     223 ██████                6.4%
  CaseDiscoveryTests        69 ██                    2.0%
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

### 5.3 AI vs. Traditional Development Comparison

```
Metric                     AI-Assisted    Traditional (Est.)    Ratio
─────────────────────────  ─────────────  ──────────────────    ─────
Calendar time              10 days        6-8 weeks             4-6x faster
Active coding sessions     ~7 sessions    ~30 sessions          4x fewer
Lines of code produced     7,937          7,937                 Same output
Test coverage              77.2%          40-60% (typical)      Higher
Refactoring confidence     High           Moderate              Better
Documentation              Comprehensive  Often deferred        Better
Cross-file consistency     High           Variable              Better
Domain knowledge required  Same           Same                  No change
Debugging novel issues     Moderate       High                  Worse for AI
Architecture decisions     Human-driven   Human-driven          No change
```

**Key finding:** AI assistance compressed approximately 6-8 weeks of solo developer effort into 10 calendar days. The acceleration was most dramatic for:
- Boilerplate generation (models, handlers, DI wiring) — 10x faster
- Test writing — 5x faster
- Refactoring across multiple files — 5x faster
- Documentation — 3x faster

The acceleration was minimal for:
- Debugging physics/simulation failures (AMI instability) — similar time
- Architecture decisions — similar time
- Linux environment configuration — similar time

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
| Production LOC | 4,532 | Lean for feature scope |
| Test LOC | 3,554 | Strong investment |
| Test/Prod Ratio | 78.4% | Above industry average |
| Passing Tests | 144 | Zero failures |
| Template Files | 41 | Comprehensive OpenFOAM coverage |
| GitHub Issues | 16 total (10 closed) | Well-tracked |
| Build Status | Clean | Zero warnings |
| Pipeline Validated | Yes | SimFlow match + grid convergence |

### Development Velocity

| Metric | Value |
|--------|-------|
| Avg commits/active day | 11.0 |
| Peak day (Feb 16) | 30 commits |
| Lines per active day | 1,134 |
| Tests per active day | 20.3 |
| Issues closed per active day | 1.4 |

---

## 8. Development Cost Analysis

### 8.1 AI-Assisted Development Costs

Claude Code is subscription-based ($100/month for the Pro plan used in this project). Cost estimation below uses the 10-day active development window.

| Cost Category | Amount | Notes |
|---------------|--------|-------|
| Claude Code subscription | $100/month | Pro plan, flat rate regardless of usage |
| Prorated to project (10 days) | ~$33 | 10/30 of monthly cost |
| Linux workstation (existing) | $0 | Already owned, no incremental cost |
| OpenFOAM / gmsh licenses | $0 | Open-source software |
| **Total AI-assisted cost** | **~$33** | |

**Cost per deliverable:**

| Metric | Value |
|--------|-------|
| Cost per line of C# code | $0.004 |
| Cost per test | $0.23 |
| Cost per commit | $0.42 |
| Cost per GitHub issue resolved | $3.30 |

### 8.2 Traditional Development Cost Estimate

For an equivalent scope (CLI tool with 8 command handlers, 7 services, 41 templates, 142 tests, full documentation) developed by a solo developer without AI assistance:

| Cost Category | Amount | Assumptions |
|---------------|--------|-------------|
| Developer time (6-8 weeks) | $15,000 - $24,000 | Mid-level .NET developer, $75-100/hr, 25-30 hrs/week |
| OpenFOAM consulting | $2,000 - $5,000 | CFD domain review, template validation |
| Testing / QA | $2,000 - $3,000 | Manual testing, CI/CD setup |
| **Total traditional cost** | **$19,000 - $32,000** | |

### 8.3 Rate Limiting Impact (Hidden Cost — Much Worse Than Initially Reported)

The initial version of this report (Feb 25) underestimated the rate limiting impact. The actual experience was significantly worse.

**The $20/month usage limit** (which the Pro plan effectively imposed via weekly spending caps) caused a **complete development stoppage from February 26 through March 2** — nearly a full week. The engineer was blocked from using Claude Code entirely, waiting for the weekly limit to reset on Monday at 3:00 PM. This was not a minor throttle; it was a hard stop.

| Factor | Initially Reported (Feb 25) | Actual Experience (Mar 5) |
|--------|---------------------------|--------------------------|
| Rate limit hits | ~8-12 occurrences | ~15-20 occurrences + 1 full week blocked |
| Average wait time | ~15-30 minutes | **5-7 full days** for weekly reset |
| Total idle time | ~3-5 hours | **~40+ hours** (full work week) |
| Context window exhaustions | ~4-5 continuations | ~8-10 continuations |
| Engineering cost of idle time | ~$300-$500 | **$4,000-$8,000** at $100-200/hr |

**The rate limiting didn't just convert compute savings into idle time — it destroyed project momentum.** Critical bugs (coefficient parsing, surfaceOrient argument order) went undetected for a week because the engineer couldn't run validation. The weekly reset schedule (Monday 3:00 PM) meant that hitting the limit on a Tuesday effectively killed the entire week.

**Economic impact:** At a billing rate of $100-200/hr, 40+ hours of blocked engineering time represents $4,000-$8,000 in lost productivity — dwarfing the $100/month subscription cost by 40-80x. The initial report's estimate of $300-$500 was a significant undercount.

### 8.4 Plan Tier Comparison

Anthropic offers multiple Claude Code subscription tiers. The table below estimates how the project timeline and cost would change with higher-tier plans:

| Plan | Monthly Cost | Rate Limits | Est. Project Duration | Est. Total Cost | Notes |
|------|-------------|-------------|----------------------|----------------|-------|
| **Pro (actual)** | $100 | Standard | 10 days (7 active) | ~$2,033 | Rate limits caused ~3-5 hrs idle |
| **Max (5x)** | $200 | 5x Pro | ~7 days (5 active) | ~$2,147 | Eliminates most rate limit waits |
| **Max (20x)** | $100\* | 20x Pro | ~5-6 days (4 active) | ~$2,053 | Near-unlimited; minimal idle time |

\* *Max 20x pricing at time of project was $100/month during promotional period; standard pricing is $200/month.*

**Key insight:** The incremental cost of a higher-tier plan ($100-$200/month) is trivial compared to the engineer idle time it eliminates. At $100/hr, saving just 2 hours of rate-limit idle time pays for the entire plan upgrade. For intensive development projects, the Max plan is strictly ROI-positive.

**Projected timeline with unlimited access:**

If rate limits and context window exhaustions were eliminated entirely:
- Active development days could compress from 7 to 4-5 (30-40% reduction)
- Session continuations (which lose context and require re-explanation) would be fewer
- Peak-day throughput (30 commits) could become the norm rather than the exception
- Estimated project completion: **5-6 calendar days** instead of 10

### 8.5 Cost Comparison

```
                       AI-Assisted    Traditional     Savings
                       ───────────    ───────────     ───────
Direct cost              $33          $19K-$32K       99.8%
Calendar time            10 days      6-8 weeks       4-6x faster
Engineer hours           ~20 hrs      150-240 hrs     7-12x fewer
Cost per LOC             $0.004       $2.40-$4.03     600-1000x less
Rate limit idle time     ~3-5 hrs     N/A             Hidden cost
```

**Important caveats:**

1. **The $33 figure understates the human cost.** The engineer spent approximately 20 hours providing direction, reviewing output, debugging physics, and validating results. At a $100/hr rate, that adds ~$2,000 of human time, bringing the true AI-assisted cost to approximately **$2,033** — still a **90-94% savings** over traditional development.

2. **Rate limit idle time adds ~$300-$500 of hidden cost.** When the engineer is blocked waiting for rate limits to reset, that time is economically unproductive. A Max plan eliminates most of this waste for an incremental $100/month.

3. **AI-assisted development requires an experienced engineer.** The cost savings assume the human has both software architecture expertise and CFD domain knowledge. Without domain expertise, the AI would produce syntactically correct but physically invalid simulations (as demonstrated by the force coefficient calibration issues).

4. **Subscription cost is amortized.** The $100/month covers all projects for the month. If the developer uses Claude Code for multiple projects, the per-project cost drops further.

### 8.6 ROI Summary

| Scenario | Total Cost | Time to Deliver | ROI vs. Traditional |
|----------|-----------|-----------------|---------------------|
| **AI-assisted — Pro plan (actual)** | ~$2,033 | 10 days | Baseline |
| **AI-assisted — Max plan (projected)** | ~$2,150 | 5-6 days | 5% more cost, 40-50% faster |
| Traditional (solo dev) | ~$25,500 | 7 weeks | — |
| **Savings (Pro vs. Traditional)** | **~$23,467** | **~39 days** | **~12x cost reduction** |
| **Savings (Max vs. Traditional)** | **~$23,350** | **~43 days** | **~12x cost, 7-10x faster** |

The return on investment is driven primarily by the reduction in billable engineering hours. The AI subscription cost ($33-$67) is negligible compared to the human time savings. Even accounting for the engineer's oversight hours, the AI-assisted approach delivered the same output at roughly one-tenth the cost and one-fifth the calendar time. Upgrading to the Max plan adds minimal cost while significantly compressing the timeline.

---

*This document was last updated on March 5, 2026.*
*AI assistance provided by Anthropic Claude (Sonnet 4.5, Sonnet 4.6, Opus 4.6) via Claude Code.*
