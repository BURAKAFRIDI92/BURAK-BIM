---
name: bim-project-setup
description: "Set up a new Revit project the way a BIM manager does: project information, levels, grids, worksets, view templates, plans, links and shared parameters, checked before modelling starts."
metadata:
  discipline: "BIM management"
  product: "BURAK BIM 1.3"
---
# BIM project setup

Ask for the BIM Execution Plan or company standard first. Do not invent naming rules or coordinates.

1. **Current state:** `revit_project_info`, `revit_levels`, `revit_grids_report`, `revit_worksets`, `revit_links`, `revit_view_templates`. Report what already exists.
2. **Preview:** show the planned levels, grids, worksets and names as a table. Get approval before creating anything.
3. **Project information:** `revit_project_info_set`.
4. **Levels and grids:** `revit_levels_series`, `revit_grid_system`. Check with `revit_grids_report`.
5. **Worksets (workshared models only):** `revit_workset_create`, one per discipline or zone from the plan.
6. **Links:** `revit_link_add` for the other disciplines. Confirm the coordinates agreed in the plan with the user.
7. **Views:** `revit_plans_for_levels`, then `revit_view_template_apply`.
8. **Parameters:** `revit_add_shared_parameter`, and `revit_project_parameters` to confirm the bindings.
9. **Verify:** `revit_naming_audit`, `revit_view_templates_audit`, and `revit_capture` of one plan and one 3D view.
10. **Report:** what was created, what the plan left undecided, and what is ready for modelling.
