---
name: revit-document-control-iso19650
description: "ISO 19650 document control in Revit: container/sheet numbering, status and revision codes, drawing registers using the practice's templates."
metadata:
  discipline: "Revit / Information Management"
  product: "BURAK BIM 1.3"
---
# Revit Document Control - ISO 19650

## Naming convention
The ISO 19650-2 national annex field order is:

`Project-Originator-Volume/System-Level/Location-Type-Role-Number`, for example `PRJ-ABC-ZZ-01-DR-A-1001`.

Confirm the codes with the user or the BEP. Never invent originator or project codes.

## Steps
1. **Read the current state:** `revit_sheets_report`, `revit_revisions`, `revit_project_parameters`. Find out which parameters hold Status (S0 to S4, A1 and so on), Revision (P01, C01 ...) and Suitability.
2. **Add missing shared parameters** with `revit_add_shared_parameter`, after the user approves. Typical parameters: Originator, Volume, Level code, Type, Role, Number, Status, Revision.
3. **Build the sheet numbers** from the fields and preview a table of old name → new name. Apply with `revit_sheet_renumber` or `revit_param_bulk_set`.
4. **Statuses and revisions:**
   - Create the revision with `revit_revision_create` (Issued to, Issued by, date, description).
   - Add it to the issued sheets with `revit_sheet_add_revision`.
   - Set the Status parameter in bulk.
   - P-revisions are for work in progress and shared information; C-revisions are for published/contractual information.
5. **Drawing register:** `revit_sheet_revision_matrix` + `revit_schedule_export_csv` (sheet number, title, status, revision, date) → exports. Offer the register as Excel or PDF.
6. **Verify:** re-read the sheets; check title blocks with `revit_capture` on two sample sheets; confirm there are no duplicate numbers.
