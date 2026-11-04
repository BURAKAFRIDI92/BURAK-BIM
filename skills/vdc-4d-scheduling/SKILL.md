---
name: vdc-4d-scheduling
description: "4D construction sequencing in Navisworks TimeLiner: link the programme to search sets by level and trade, check unlinked items and delays, and produce sequence images."
metadata:
  discipline: "VDC / 4D"
  product: "BURAK BIM 1.3"
---
# VDC 4D scheduling

The programme belongs to the planner. Never change a date without saying so and getting approval.

1. **Check the model:** `navisworks_info`, `navis_models`, `navis_grids_levels`. Federate first with `bim-coordination` if models are missing.
2. **Check the current state:** `navis_timeliner_summary`. Ask before running `navis_timeliner_clear`.
3. **Build the sets:** `navis_sets_from_levels`, `navis_sets_by_property` (trade × level). Sets must match how the programme breaks down the work.
4. **Load the programme:** `navis_timeliner_import_csv` from the planner's export. Without a programme, draft tasks with `navis_timeliner_create_tasks` and label them as a draft.
5. **Link:** `navis_timeliner_link_sets`. Then `navis_timeliner_summary`, and report every task without a set and every trade without a task.
6. **Check progress:** `navis_timeliner_delays`, and `navis_timeliner_task_dates` for approved date changes only.
7. **Outputs:** `navis_viewpoint_images` at the agreed milestones and `navis_timeliner_export_csv`.
8. **Report:** tasks linked and unlinked, delays, and sequence questions for the planner. Do not simulate in the UI unless asked. Use `capture app=navisworks` to show the result.
