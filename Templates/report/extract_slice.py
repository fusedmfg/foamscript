"""
FoamScript slice data extraction via ParaView.
Phase 1 of two-phase visualization: extracts field data on a y=0 cutting plane.
Run with: pvpython --force-offscreen-rendering extract_slice.py <case_dir> <output_json>
"""
import sys, os, json
from paraview.simple import *
from paraview import servermanager

case_dir = sys.argv[1]
output_json = sys.argv[2]

foam_file = os.path.join(case_dir, "system", "controlDict")
reader = OpenFOAMReader(FileName=foam_file)
reader.MeshRegions = ["internalMesh"]
reader.CellArrays = ["p", "U"]

# Load latest time step
times = reader.TimestepValues
if times:
    reader.UpdatePipeline(time=max(times))
else:
    reader.UpdatePipeline()

# Slice at y=0 (XZ plane through disc center)
slice_filter = Slice(Input=reader)
slice_filter.SliceType = "Plane"
slice_filter.SliceType.Origin = [0.0, 0.0, 0.0]
slice_filter.SliceType.Normal = [0.0, 1.0, 0.0]
slice_filter.UpdatePipeline()

# Calculate velocity magnitude
calc = Calculator(Input=slice_filter)
calc.ResultArrayName = "Umag"
calc.Function = "mag(U)"
calc.UpdatePipeline()

# Merge multiblock dataset into single block
merge = MergeBlocks(Input=calc)
merge.UpdatePipeline()

data = servermanager.Fetch(merge)
n_points = data.GetNumberOfPoints()

x = [data.GetPoint(i)[0] for i in range(n_points)]
z = [data.GetPoint(i)[2] for i in range(n_points)]
p_array = data.GetPointData().GetArray("p")
u_array = data.GetPointData().GetArray("Umag")
p_vals = [p_array.GetValue(i) for i in range(n_points)]
umag_vals = [u_array.GetValue(i) for i in range(n_points)]

result = {"x": x, "z": z, "p": p_vals, "umag": umag_vals}
with open(output_json, "w") as f:
    json.dump(result, f)
print(f"Extracted {n_points} points")
