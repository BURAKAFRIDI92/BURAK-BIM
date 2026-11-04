---
name: revit-scan-to-bim
description: "Scan to BIM in Revit: link ReCap point clouds, detect levels/walls/columns, model native elements, check deviation against the scan, inspector sign-off."
metadata:
  discipline: "Revit / Reality Capture"
  product: "BURAK BIM 1.3"
---
# Revit Scan to BIM

1. **Link the scan:** `scan_pointclouds`; if nothing is linked yet, `scan_link_pointcloud path=...rcp`. Confirm the coordinates (origin, shared coordinates) with the user.
2. **Levels:** `scan_detect_levels` gives floor tops and slab undersides. Compare them with `revit_levels`, and create or adjust levels after approval.
3. **Plan the walls and columns:**
   - Run `scan_to_bim dryRun=true` per level and show the SVG plan, the wall list (thickness, confidence) and the columns.
   - Discuss single-face walls and low-confidence results with the user.
4. **Create:** `scan_to_bim dryRun=false` with wall and column types the user picked. Each storey is one undo step, and every element is stamped AI/unverified.
5. **Complete the rest:** model doors, windows, MEP and sloped elements with the other skills.
6. **QA:** `scan_deviation toleranceMm=25`. Fix out-of-tolerance elements and run it again.
7. **Inspector review:**
   - `inspector_review_view` shows the colour-coded status.
   - The inspector signs off with `inspector_mark`.
   - `burak_inspection_report` produces the record.

Never claim LOD 500 without verified sign-off.
