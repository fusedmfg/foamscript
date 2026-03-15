# CLAUDE.md — FoamScript Project Instructions

## Project Overview

- .NET 10 CLI tool automating OpenFOAM v2512 CFD workflows
- Cross-platform (developed on Windows, runs on Linux)
- Branch: `develop` (PR target: `main`)
- Tests: xUnit + Moq + FluentAssertions

## Development Rules

- **TDD approach**: write tests alongside features
- **AIAA aerospace standards**: all defaults, report formatting, and visualization framing follow AIAA conventions
- Run `dotnet build` and `dotnet test` before committing
- For E2E validation, SSH to Linux box running OpenFOAM v2512
- Update `Docs/ExecutiveSummary.md` when features, tests, issues, or costs change

## Key Paths

| What | Path |
|------|------|
| Templates | `Templates/external_disc_rotatingwall_steady/` |
| Report scripts | `Templates/report/` (extract_slice.py, render_slice.py, report.html) |
| Services | `Services/` |
| Command handlers | `Handlers/` |
| Models | `Models/` |
| Tests | `foamscript.Tests/` |
| Executive report | `Docs/ExecutiveSummary.md` |
| CLI docs | `Docs/Commands.md` |
