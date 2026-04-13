# Auto-Convert STEP/IGES in new-study Pipeline

**Date:** 2026-04-12
**Issue:** new-study rejects STEP/IGES files; convert command has invalid gmsh flags

## Problem

1. `new-study` rejects STEP/IGES files, requiring manual `convert` first — defeats the purpose of an orchestration tool
2. `StlConversionService` passes `-angle` flag to gmsh, which doesn't exist in gmsh 4.x
3. Config file already documents `inputUnits` and `meshSize` but `StudyConfig` doesn't deserialize them

## Design: Auto-Convert in ProcessGeometry (Option A)

### Changes

1. **Fix gmsh `-angle` bug** (`StlConversionService.cs:55`)
   - Remove the `-angle` CLI flag entirely — gmsh has no such option
   - Feature angle is a scripting/API concept, not a CLI flag

2. **Add `ModelSourceUnits` and `MeshSize` to `StudyConfig`**
   - `ModelSourceUnits`: string, default `"mm"` (most common CAD unit)
   - `MeshSize`: double, default `1.0`
   - These already exist in `study.example.jsonc` but aren't deserialized

3. **Auto-convert in `CaseService.ProcessGeometry()`**
   - When STEP/IGES detected: call `_geometryService.ConvertStepToStl()`
   - Output to `{geometryDir}/model.stl`
   - Use config's `modelSourceUnits` (default mm) and `meshSize` (default 1.0)
   - Print conversion status to console
   - On failure, return error with gmsh details
   - `GeometryService` is already injected into `CaseService` — no DI changes needed

4. **Update `study.example.jsonc`**
   - Ensure all config fields have documented defaults
   - Add any missing physics/domain fields

### Config File Contract

```jsonc
{
  "modelSource": "/path/to/model.step",  // .step, .stp, .iges, .igs, or .stl
  "modelSourceUnits": "mm",              // default: mm (ignored for .stl)
  "meshSize": 1.0,                       // gmsh mesh size factor (ignored for .stl)
  // ... rest of config
}
```

### Testing

- Unit test: STEP file triggers conversion call with correct args
- Unit test: STL file skips conversion (existing behavior)
- Unit test: conversion failure propagates error
- Existing tests: verify no regressions
