---
name: bim-handover
description: "Project closeout and handover: check asset data, export IFC, NWD, schedules and CSV registers, and build a handover package against the client's requirements."
metadata:
  discipline: "BIM management / Handover"
  product: "BURAK BIM 1.3"
---
# BIM handover

BURAK BIM has no dedicated COBie exporter. It can prepare COBie-style data as CSV, but a file is only COBie-compliant after the client's own checker accepts it. Say so to the user.

1. **Get the requirements:** the client's information requirements or EIR, the asset classes, the required fields and the formats.
2. **Find gaps:**
   - `revit_find_by_parameter` for empty required parameters.
   - `navis_qa_missing_property` across the federated model.
   - `revit_room_problems`, because assets need a valid room.
3. **Fill data only with approval:** preview, then `revit_set_parameters`. Never invent serial numbers, warranties or manufacturers. Leave them blank and list them for the contractor.
4. **Export registers:** `revit_export_elements_csv`, `revit_export_rooms_csv`, `revit_export_all_schedules`.
5. **Export models:** `revit_export_ifc_options` then `revit_export`, and `navis_publish_nwd`. Open or re-import the files where possible to confirm they are readable.
6. **Export drawings:** `revit_export_sheets_pdf`, `revit_sheet_revision_matrix`.
7. **Report:** a package index with file, format, revision and date, plus the open data gaps and who owns each one.
