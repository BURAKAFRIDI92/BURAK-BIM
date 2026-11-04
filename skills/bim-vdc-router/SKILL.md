---
name: bim-vdc-router
description: "Start here for any BIM or VDC responsibility: maps the task (setup, modelling, coordination, 4D, 5D, QA, documentation, review, handover) to the right skill and says what still needs a person."
metadata:
  discipline: "BIM management / VDC"
  product: "BURAK BIM 1.3"
---
# BIM and VDC task router

1. Call `burak_status`. Only plan with software that is installed and running.
2. Find the responsibility below, then read that skill with `burak_skills action=get name=<skill>`.
3. For a task that spans several rows, list the skills in order and show the plan to the user first.

| Responsibility | Skill |
|---|---|
| Start a project: levels, grids, worksets, templates, links | `bim-project-setup` |
| Architectural modelling | `revit-architecture-modeling`, `archicad-modeling` |
| Structural modelling and analysis | `revit-structure-modeling`, `csi-structural-analysis` |
| HVAC, plumbing, piping, electrical | `revit-mep-modeling` |
| Families | `revit-family-creation` |
| Existing conditions | `revit-scan-to-bim` |
| Site and civil | `civil3d-site`, `autocad-civil3d-base` |
| Fabrication cut files | `autocad-cut-files` |
| Computational design | `rhino-grasshopper-computational` |
| Clash detection and coordination | `bim-coordination` |
| 4D construction sequencing | `vdc-4d-scheduling` |
| 5D quantities and reconciliation | `vdc-5d-quantities` |
| Takeoff from PDFs | `bluebeam-takeoff`, `planswift-takeoff` |
| Model QA before an issue | `bim-qa-gate`, `revit-model-health` |
| Sheets, schedules and drawing sets | `revit-documentation` |
| ISO 19650 numbering, status and revisions | `revit-document-control-iso19650` |
| Drawing review and comment closure | `drawing-review-cycle` |
| Handover and asset data | `bim-handover` |
| Nothing above fits | `bim-base`, then `computer-control` as the last resort |

## What stays with a person
These tools prepare the information. A named person decides:
- Engineering design, code compliance and sign-off.
- Contract, cost and programme commitments.
- Approving a drawing issue, a clash as acceptable, or LOD 500 (`inspector_mark`).

Say which of these decisions is waiting when you report back.
