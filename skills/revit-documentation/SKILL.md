---
name: revit-documentation
description: "Revit documentation: create views, tags, dimensions, schedules and sheets, then issue PDF and DWG drawing sets."
metadata:
  discipline: "Revit / Documentation"
  product: "BURAK BIM 1.3"
---
# Revit Documentation

## 1. Agree the set
Ask, or read from the project:
- sheet list and numbering;
- title block;
- scale per view type;
- view template names.

`revit_sheets_report`, `revit_view_templates` and `revit_titleblock_params` show what already exists.

## 2. Views
- **Plans per level:** `revit_plans_for_levels` (floor and ceiling plans), then `revit_view_template_apply`.
- **Elevations:** `revit_elevations_create`. **Sections:** `revit_create_view kind=section`. **Callouts:** `revit_callout_create`.
- **Crop and range:** `revit_view_crop`, `revit_view_range`. Use `revit_dependent_views` for large floors.
- Check each view with `revit_capture`.

## 3. Annotate
- **Tags:** `revit_tag_category` for doors, windows, rooms and walls, or `revit_room_tag_all`. Check `revit_untagged_elements` afterwards.
- **Dimensions:** `revit_dimension_grids` and `revit_dimension_between`.
- **Text and keynotes:** `revit_text_note_add`, `revit_keynotes_report`.

## 4. Schedules
- Create with `revit_schedule_create`, add `revit_schedule_fields` and `revit_schedule_filter`.
- Check the output with `revit_schedule`.

## 5. Sheets
- Create with `revit_sheets_create` (number, name, title block).
- Place views with `revit_place_view_on_sheet`, and tidy them with `revit_viewport_move`.
- Fill `revit_titleblock_params`. Add revisions with `revit_revision_create` and `revit_sheet_add_revision`.
- Capture each sheet with `revit_capture view="<sheet number>"` and check it for overlaps.

## 6. Issue
- **Sheet set:** `revit_sheet_set_create`.
- **Export:** `revit_export_sheets_pdf` and `revit_export_sheets_dwg` to `<install>\exports\<date>`.
- Report the file list. Run `revit_sheet_revision_matrix` for the transmittal.
