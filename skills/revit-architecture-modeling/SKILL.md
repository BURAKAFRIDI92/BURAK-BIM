---
name: revit-architecture-modeling
description: "Architectural modelling in Revit from brief or CAD: levels, grids, walls, floors, roofs, doors/windows, rooms, finishes - with checks."
metadata:
  discipline: "Revit / Architecture"
  product: "BURAK BIM 1.3"
---
# Revit Architecture modelling

## 0. Inputs
Find out what the design is based on:
- a brief (areas, heights);
- CAD drawings: `revit_cad_link`, then read them with `revit_cad_files`;
- a PDF: `pdf_text` for room schedules;
- a point cloud: use the scan-to-bim skill instead.

Confirm units, level heights and wall types with the user.

## 1. Datums
- **Levels:** `revit_levels_series` (count + floor-to-floor height). Rename them to the project names.
- **Grids:** `revit_grid_system` (spacings in X and Y). Check with `revit_capture` on the Level 1 plan.

## 2. Walls
- **Exterior first:** `revit_wall_polyline` (closed outline, type, base and top level).
- **Interior:** `revit_wall_polyline` per run.
- **From CAD lines:** `revit_lines_to_walls`.
- **Cleanup:** `revit_wall_join_ends`, then check `revit_unjoined_walls` and `revit_overlapping_walls`.
- **Repeat floors:** `revit_copy_to_levels` for typical floors.

## 3. Floors, ceilings, roofs
- `revit_rooms_auto_place` first, then `revit_floor_from_room` and `revit_ceiling_from_room`. Or use `revit_create kind=floor` with a boundary.
- Roofs: `revit_roof_by_footprint_slope`.

## 4. Openings
- Doors and windows: `revit_door_place` and `revit_window_place` (host wall, offset, sill height).
- Check door swing and fire rating with `revit_door_from_to_rooms` and `revit_door_fire_rating_check`.

## 5. Rooms and finishes
- `revit_rooms_auto_place`, then `revit_rooms_renumber` and `revit_room_finishes_set`.
- Area check: `revit_level_area_summary`, compared with the brief.

## 6. Verify
- Capture each level plan and a 3D view.
- `revit_warnings_grouped`.
- `revit_room_problems` must show 0 unbounded rooms.
- Report areas against the brief.
