---
name: civil3d-site
description: "Site and civil work: Civil 3D surfaces, alignments, COGO points, plus Revit site/toposolid and coordinates."
metadata:
  discipline: "Civil 3D / Site"
  product: "BURAK BIM 1.3"
---
# Site and civil

1. **Survey:** `civil3d_com_points` (COGO points by group), then `civil3d_com_surfaces` (surfaces with statistics: minimum, maximum and area).
2. **Alignments and profiles:** `civil3d_com_alignments` (lengths, stations). Create or edit them through `ribbon_invoke` or `api_*` with the civil root.
3. **Earthwork:** surface comparison (volume surface) through `api_*`/`script_run`. Report cut and fill.
4. **Coordinates:** agree the shared coordinates.
   - Revit: `revit_location`, `revit_location_set`.
   - Navisworks: `navis_qa_model_alignment`.
5. **Revit site:**
   - Link the DWG (`revit_cad_link`); create the toposolid or topography with `script_run`.
   - Place the building pad.
   - Capture the site plan.
6. **Verify:** compare spot elevations between Civil 3D and Revit at 3 points.
