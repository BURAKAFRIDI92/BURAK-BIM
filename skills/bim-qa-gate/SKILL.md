---
name: bim-qa-gate
description: "Pass-or-fail model QA gate before issuing or federating: standards, naming, warnings, rooms, data completeness and alignment, with inspector review and an evidence report."
metadata:
  discipline: "BIM management / QA"
  product: "BURAK BIM 1.3"
---
# BIM QA gate

This gate reports. It does not fix anything unless the user approves each fix.

1. **Get the criteria** from the BIM Execution Plan: naming, required parameters, allowed warnings, LOD. If there are none, ask, or label the result as a general health check.
2. **Revit checks:**
   - `revit_warnings_grouped`
   - `revit_naming_audit`
   - `revit_view_templates_audit`
   - `revit_room_problems`
   - `revit_links`
3. **Federated checks (Navisworks):**
   - `navis_qa_model_health`
   - `navis_qa_model_alignment`
   - `navis_qa_missing_property`
   - `navis_qa_duplicates`
4. **Score it:** one row per criterion with pass or fail, count, evidence and owner. Do not average failures into a single score.
5. **Visual evidence:** `revit_capture` or `capture` for each blocking issue.
6. **Inspector review:** `inspector_review_view`, and the inspector records the decision with `inspector_mark`. Never mark elements on the inspector's behalf.
7. **Report:** `burak_inspection_report`, with `burak_audit_log` for the changes made since the last gate. State clearly whether the model passes and what blocks it.
