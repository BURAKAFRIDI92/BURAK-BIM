---
name: bim-coordination
description: "BIM coordination and clash management across disciplines: federate models in Navisworks, clash matrix, run, triage, group, assign, viewpoints, reports, issue cycle."
metadata:
  discipline: "Navisworks / VDC"
  product: "BURAK BIM 1.3"
---
# BIM Coordination - clash detection and issue cycle

1. **Federate:**
   - `navisworks_file op=append` with the discipline models (NWC from `revit_export_nwc_views`, plus DWG and IFC).
   - Check what loaded with `navis_models` and `navis_units_info`.
   - Check alignment with `navis_qa_model_alignment`. Misaligned models produce false clashes.
2. **Search sets:** `navis_sets_by_property` or `navis_sets_from_levels` (discipline × level).
3. **Clash matrix:** `navis_clash_matrix` creates tests such as structure vs MEP, ducts vs pipes and architecture vs MEP. Set tolerances per pair: hard clashes 10-25 mm, clearances per specification.
4. **Run and triage:**
   - `navisworks_clash_run`, then `navis_clash_summary`.
   - `navis_clash_auto_group` / `navis_clash_group_by_level` to group the results.
   - `navis_clash_auto_approve` for tiny or known-acceptable clashes, **after approval**.
   - `navis_clash_rename_by_location`.
5. **Assign and record:**
   - `navis_clash_assign` per discipline.
   - `navis_clash_viewpoint` / `navis_issue_viewpoints` for the top issues.
   - `capture app=navisworks` for the meeting deck.
6. **Report:** `navis_clash_report_html` and `navis_clash_report_csv`, plus a short summary: new, active, resolved and by responsible party.
7. **Next cycle:** reload updated models, rerun, compare (`navis_compare_models`), and show the trend.

**Revit-only check:** `revit_clash_check` between linked models gives a quick look without Navisworks.
