---
name: revit-model-health
description: "Revit model health check and cleanup: warnings, views, families, links, worksets, naming; prioritised issues with before/after report."
metadata:
  discipline: "Revit / BIM Management"
  product: "BURAK BIM 1.3"
---
# Revit Model Health

## 1. Baseline (read-only)
- `revit_model_health`: summary score and metrics.
- `revit_warnings_grouped`: warnings by type and count.
- `revit_count_by_category`
- `revit_views_not_on_sheets`
- `revit_view_templates_audit`
- `revit_unused_types`
- `revit_in_place_families`
- `revit_links`, `revit_cad_files`: imported or linked CAD, and links that are not loaded.
- `revit_naming_audit`
- `revit_room_problems`: unplaced, redundant and not enclosed rooms.
- `revit_find_duplicates`, `revit_overlapping_walls`, `revit_unjoined_walls`
- `revit_tiny_elements`, `revit_dimension_overrides`
- Worksharing: `revit_worksets`.

Save the baseline numbers. This is the **BEFORE** report.

## 2. Prioritise
| Priority | Typical issues |
|---|---|
| P1: model integrity | Duplicate or overlapping elements, rooms not enclosed, imported CAD in the project, broken links, very large warning groups |
| P2: performance | Unused views, in-place families, unused types and families, huge groups |
| P3: standards | Naming, view templates not applied, overridden dimensions |

Present the issues to the user as a table: issue, count, fix, risk.

## 3. Fix with approval
For each group the user approves:
- **Duplicates:** delete with `revit_delete` (ids from `revit_find_duplicates`).
- **Unjoined walls:** `revit_wall_join_ends`.
- **Unused views:** `revit_delete_unused_views`. Preview first. Never delete views that are on sheets.
- **Purge:** `revit_purge_unused`, after the user confirms.
- **Imported CAD:** `revit_cad_cleanup`.
- **Naming:** `revit_views_rename_pattern`.

## 4. After
Re-run the baseline tools and produce a **before/after** table. Add `burak_inspection_report` if inspectors need the record. Capture one representative 3D view.
