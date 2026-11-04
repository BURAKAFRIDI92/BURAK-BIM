---
name: revit-structure-modeling
description: "Structural modelling in Revit: grids, columns, beams, braces, slabs, foundations, rebar, loads; analytical model and hand-off to ETABS/SAP2000."
metadata:
  discipline: "Revit / Structure"
  product: "BURAK BIM 1.3"
---
# Revit Structure modelling

1. **Datums:** use the same grids and levels as Architecture. Copy/Monitor them (`revit_copy_monitor_report`) when the architectural model is linked.
2. **Columns:** `revit_columns_at_grids` (type, base and top level, which grid intersections). `revit_column_grid_check` flags off-grid columns.
3. **Framing:**
   - Beams: `revit_beams_between_columns` (type, level, offset).
   - Braces: `revit_braces_trusses`.
   - Clean up with `revit_framing_join_ends` and `revit_beam_offsets`.
4. **Floors and slabs:** `revit_create kind=floor` with a structural floor type. Check slab edges with `revit_slab_edge_check`.
5. **Foundations:**
   - pad footings: `revit_footings_under_columns`;
   - strip footings: `revit_wall_foundations`;
   - raft: `revit_foundation_slab`.
6. **Rebar:** `revit_rebar_from_curves`, `revit_rebar_cover`, `revit_rebar_report`.
7. **Loads:** `revit_load_cases`, `revit_loads_report`. Apply loads with `script_run` (point, line and area loads).
8. **Quantities:** `revit_concrete_volume_by_level`, `revit_steel_tonnage`.
9. **Analysis hand-off:**
   - Export IFC (`revit_export` format=ifc) or use the analytical model.
   - In ETABS or SAP2000, rebuild or verify with `csi_points`, `csi_frames` and `csi_areas` at the same coordinates (watch the units).
   - Run the csi-structural-analysis skill.
   - Bring the results back as parameters with `revit_param_bulk_set`, for example utilisation ratios.
10. **Verify:** capture a structural 3D view and the framing plans; check `revit_warnings_grouped`.
