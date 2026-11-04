---
name: bim-base
description: "BURAK BIM base method for ANY BIM/CAD/structural/takeoff app. Use at the start of every BURAK BIM task: how to explore, query, change, get approval and verify safely."
metadata:
  discipline: "All"
  product: "BURAK BIM 1.3"
---
# BURAK BIM - base method (every application)

The connector gives you the tools. This skill gives you the method that an experienced BIM/VDC engineer follows.

## 1. Orient first (cheap, read-only)
1. `burak_status`: which applications are installed and running, and which bridges are live.
2. Summaries before detail. Use counts, never full dumps:
   - Revit: `revit_info`, `revit_count_by_category`, `revit_levels`, `revit_views`
   - AutoCAD: `autocad_info`, `autocad_layer_report`
   - Navisworks: `navisworks_info`, `navis_models`
   - CSI: `csi_status`
   - Bluebeam: `pdf_info`
   - PlanSwift: `planswift_status`
3. **Look** at the model: `revit_capture` (any view or sheet) or `capture app=<app>`.

## 2. Query narrowly
- Ask only for what the task needs: filter by category, level, view, parameter or type, and set `limit`.
- Never guess ids, type names, levels, load cases or section names. List them first, then use the exact names.
- If a tool is not in your list, find it with `burak_tools query="..."` and run it with `burak_run`.

## 3. Plan, preview, approve
- Before changing anything, state the plan in one short list: what changes, how many elements, where.
- For more than about 20 elements, anything destructive (delete, purge, renumber, unjoin), or anything that is hard to undo: **show the preview and wait for the user's OK**. Use `dryRun` where a tool offers it.
- Respect read-only mode. Never try to work around it.

## 4. Change in bulk, in one undo step
- Prefer bulk tools: `revit_param_bulk_set`, `revit_wall_polyline`, `revit_columns_at_grids`, `revit_copy_to_levels`, `revit_tag_category`, `navis_clash_bulk_status`.
  Avoid loops of single-element calls.
- Each BURAK BIM call is one undo step. Group logically related work into one call where you can (`script_run` for custom bulk logic).

## 5. Verify before reporting
- Read back what changed (ids, counts, parameters) and **capture the result** (capture mode does this automatically).
- Check warnings: `revit_warnings_grouped` after modelling.
- Report clearly:
  - what you changed (numbers);
  - what you verified and how;
  - what still needs a human (open issues).

## 6. When no API can do it
Escalate in this order:
1. `ribbon_invoke`
2. native command (`revit_command`, `autocad_command`)
3. `api_*` / `script_run` / `csi_call` / `com_*`
4. `ui_*`
5. computer control. Follow the **computer-control** skill: ask first, approve on screen, check every step.

## Units
- Revit tools use millimetres.
- AutoCAD uses drawing units.
- CSI uses the model's present units (`csi_units`).
- Navisworks uses model units.
