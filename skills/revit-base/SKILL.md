---
name: revit-base
description: "How to work in Revit 2022-2027 through BURAK BIM: exploring, querying, modelling, documentation, verification. Use for any Revit task."
metadata:
  discipline: "Revit"
  product: "BURAK BIM 1.3"
---
# Revit - base method

## Orient
- `revit_info`: version, documents, active view. `revit_levels` and `revit_grids_report`: the datums every model depends on.
- `revit_count_by_category` / `revit_count_by_type`: get the size of the model before you read details.
- `revit_capture`: the active view. Use `view="Level 1"` or a sheet number, or `allOpenViews=true`, to see what the user sees.

## Query
- Use `revit_query` with category, level, type and parameter filters, and a limit.
- `revit_get_elements ids=[...]`: full parameters for a few elements.
- `revit_find_by_parameter`, `revit_param_distinct_values`: find problems in data without dumping elements.

## Model
- All coordinates are in **mm**, in the project internal origin.
- Read levels first: Z values are relative to them.
- Use types that exist (`revit_types category=Walls`). Duplicate a type with `revit_duplicate_type` instead of inventing one.
- Prefer bulk tools:
  - walls: `revit_wall_polyline`, `revit_wall_rectangle`
  - floors and ceilings: `revit_floor_from_room`, `revit_ceiling_from_room`
  - structure: `revit_columns_at_grids`, `revit_beams_between_columns`
  - copying: `revit_copy_to_levels`
  - data: `revit_param_bulk_set`
- Joins and cleanup: `revit_wall_join_ends`, `revit_auto_join_category`.

## Verify
- `revit_capture` the plan and a 3D view.
- `revit_warnings_grouped` for new warnings.
- Read back counts and key parameters.
- AI-created elements are stamped. Show the user `inspector_review_view` for sign-off on important work.

## Anything else
- Use `ribbon_list tab="..."` and `ribbon_invoke` for any button (including add-in tabs), or `revit_command` for any PostableCommand.
- Use `script_run` (C#, one transaction) for custom bulk logic. Run small read-only scripts first.
