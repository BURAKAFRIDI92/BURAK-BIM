---
name: autocad-cut-files
description: "AutoCAD cut files for laser/CNC: closed profiles, kerf/offset allowance, nesting on sheets, layers per operation, DXF export."
metadata:
  discipline: "AutoCAD / Fabrication"
  product: "BURAK BIM 1.3"
---
# AutoCAD cut files - laser and CNC

1. **Collect the parts:** `autocad_query types=[LWPOLYLINE,CIRCLE,ARC,LINE,SPLINE] layer=...`.
2. **Closed profiles:**
   - Join open geometry: `autocad_command "_.JOIN "` or PEDIT join.
   - Check with `autocad_entity_info` that every cut profile is a **closed** polyline or circle.
   - List any open entities to the user.
3. **Kerf:** ask for the kerf (for example 0.2 mm laser, 3 mm plasma).
   - Outer profiles: `autocad_offset_explode` offset outward by kerf/2.
   - Holes: offset inward by kerf/2.
   - Keep the originals on a reference layer.
4. **Layers per operation:** CUT_OUTER, CUT_INNER, ENGRAVE, BEND (`autocad_layer_standard`).
5. **Nesting:**
   - Arrange the parts on stock sheets (for example 2500×1250) with the spacing the user gives, using `autocad_transform` and `autocad_array`.
   - Report the sheet count and utilisation (`autocad_quantities` area of parts ÷ sheet area).
6. **Export:** `autocad_export format=dxf` per sheet (R2000/R2013, as the machine needs), into `exports`.
7. **Verify:** capture each sheet. Recount the closed profiles: there must be 0 open entities.
