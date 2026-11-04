---
name: csi-structural-analysis
description: "Structural analysis and design in ETABS / SAP2000 / CSiBridge / SAFE through BURAK BIM: model, loads, combinations, analysis, results, design, verification."
metadata:
  discipline: "Structure / Analysis"
  product: "BURAK BIM 1.3"
---
# CSI structural analysis (ETABS, SAP2000, CSiBridge, SAFE)

1. **Connect:**
   - `csi_status` shows the program, file, version, units, counts, patterns and combinations.
   - If the API is not registered, run `csi_register_api` (the user approves an admin prompt once).
2. **Units:** state and set them first: `csi_units set=kN_m_C`. Every value in later calls uses these units.
3. **Model:**
   - `csi_sections action=add_material`, `add_rectangle`, `add_circle`. For library sections use `csi_call path=PropFrame.ImportProp`.
   - Joints with `csi_points action=add`; supports with `action=restrain preset=fixed|pinned`.
   - Frames with `csi_frames action=add segments=[[...]] section=...`; slabs and walls with `csi_areas action=add`.
4. **Loads:**
   - `csi_loads action=add_pattern` for Dead (selfWeight=1), SuperDead, Live, Wind and Quake.
   - Assign them with `point_load`, `frame_udl` and `area_uniform`.
   - Combinations per the code (for example 1.2D+1.6L) with `action=add_combo`.
5. **Analyze:** `csi_analyze` (give a path if the model is new). If it fails, read the analysis log and check stability (supports, releases).
6. **Results:**
   - `csi_results kind=base_reactions`: sum of reactions = sum of applied loads (equilibrium check).
   - `modal_periods`.
   - `story_drifts` (ETABS) against the code limit.
   - `frame_forces` for the critical members.
7. **Design:**
   - `csi_design type=steel|concrete action=start`, then `action=summary`.
   - List members with ratio > 1.0 and the governing combination.
   - Propose section changes; apply them only after approval (`csi_frames action=section`, after `csi_unlock`).
8. **Verify and report:** rerun after changes, `capture app=etabs`, and give a short report covering equilibrium, drift, period and the failing members fixed.

Any other OAPI function: `csi_members path=...` to see signatures, then `csi_call`.
