---
name: vdc-5d-quantities
description: "5D quantity extraction and reconciliation: compare Revit and Navisworks quantities with Bluebeam and PlanSwift takeoffs, explain differences, and export for the estimator."
metadata:
  discipline: "VDC / 5D"
  product: "BURAK BIM 1.3"
---
# VDC 5D quantities

Quantities support the estimator. Do not present them as a priced or final estimate.

1. **Agree the scope:** categories, levels, phases, units and measurement rules. Ask which source wins when two disagree.
2. **Model quantities:**
   - Revit: `revit_materials_takeoff`, `revit_concrete_volume_by_level`, `revit_level_area_summary`, `revit_schedule_export_csv`.
   - Navisworks: `navis_quantities`, `navis_export_properties_csv`.
3. **Drawing quantities:**
   - Bluebeam: `bluebeam_takeoff_summary`.
   - PlanSwift: `planswift_quantities`.
4. **Check data quality first:** `revit_warnings_grouped`, `navis_qa_missing_property`, `navis_qa_duplicates`. Duplicates and missing properties distort totals.
5. **Reconcile:** one table per item with model quantity, takeoff quantity, unit, difference and percent difference. Convert units explicitly and show the conversion.
6. **Explain each large difference:** scope, measurement rule, missing model content or a drawing revision. Mark the cause as unknown when it cannot be shown from the data.
7. **Export:** `revit_export_all_schedules`, plus the reconciliation table as CSV. Record the model and PDF revisions used.
