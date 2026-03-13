"""
FoamScript slice visualization renderer.
Phase 2 of two-phase visualization: renders contour plots from extracted data.
Run with: python3 render_slice.py <input_json> <output_dir>
"""
import sys, os, json
import matplotlib
matplotlib.use("Agg")
import matplotlib.pyplot as plt
import matplotlib.tri as mtri
import numpy as np

data_path = sys.argv[1]
output_dir = sys.argv[2]
os.makedirs(output_dir, exist_ok=True)

with open(data_path) as f:
    data = json.load(f)

x = np.array(data["x"])
z = np.array(data["z"])
triang = mtri.Triangulation(x, z)

# Remove degenerate triangles from Delaunay at domain edges
mask = mtri.TriAnalyzer(triang).get_flat_tri_mask(min_circle_ratio=0.01)
triang.set_mask(mask)

# Auto-zoom: refined mesh clusters points near the disc, so percentile
# bounds capture the interesting region without needing disc dimensions.
x_lo, x_hi = np.percentile(x, 5), np.percentile(x, 95)
z_lo, z_hi = np.percentile(z, 5), np.percentile(z, 95)
pad = 0.1 * max(x_hi - x_lo, z_hi - z_lo)

for field_name, field_data, cmap, title, unit in [
    ("p", data["p"], "RdBu_r", "Static Pressure", "Pa"),
    ("umag", data["umag"], "viridis", "Velocity Magnitude", "m/s"),
]:
    values = np.array(field_data)
    fig, ax = plt.subplots(1, 1, figsize=(16, 9))

    # Use percentile-based levels to handle outliers near boundaries
    levels = np.linspace(np.percentile(values, 1), np.percentile(values, 99), 64)
    tc = ax.tricontourf(triang, values, levels=levels, cmap=cmap, extend="both")

    cb = fig.colorbar(tc, ax=ax, shrink=0.8, aspect=30, pad=0.02)
    cb.set_label(f"{title} ({unit})", fontsize=12)
    cb.ax.tick_params(labelsize=10)

    ax.set_xlim(x_lo - pad, x_hi + pad)
    ax.set_ylim(z_lo - pad, z_hi + pad)
    ax.set_xlabel("x (m)", fontsize=12)
    ax.set_ylabel("z (m)", fontsize=12)
    ax.set_title(f"{title} \u2014 y=0 Slice", fontsize=14, fontweight="bold")
    ax.set_aspect("equal")
    ax.tick_params(labelsize=10)

    fig.tight_layout()
    out_path = os.path.join(output_dir, f"{field_name}_slice.png")
    fig.savefig(out_path, dpi=200, bbox_inches="tight", facecolor="white")
    plt.close(fig)
    print(f"Saved: {out_path}")
