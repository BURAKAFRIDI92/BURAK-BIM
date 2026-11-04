---
name: autocad-civil3d-base
description: "How to work in AutoCAD and every vertical (Civil 3D, Plant 3D, Map 3D, MEP, Advance Steel) with BURAK BIM: layers, blocks, drafting, plotting, Civil 3D objects."
metadata:
  discipline: "AutoCAD / Civil 3D"
  product: "BURAK BIM 1.3"
---
# AutoCAD and Civil 3D - base method

## Orient
- `autocad_info`, `autocad_drawings`, `autocad_layer_report`, `autocad_blocks_report`
- `capture app=autocad`

## Query
- `autocad_query types=[LWPOLYLINE] layer=...`, `autocad_select`, `autocad_entity_info`
- Quantities: `autocad_quantities` (length and area by layer), `autocad_measure`.

## Draft and edit in bulk
- Create: `autocad_add`, `autocad_draw`, `autocad_add_hatch`, `autocad_add_dimension`, `autocad_add_table`.
- Edit: `autocad_transform`, `autocad_array`, `autocad_offset_explode`.
- Blocks and attributes: `autocad_insert_block`, `autocad_block_attributes_set`.
- Standards: `autocad_layer_standard` (names, colours, linetypes), `autocad_text_find_replace`, `autocad_drawing_cleanup` (purge and audit).
- Any command: `autocad_command command="_.OVERKILL "`; LISP: `autocad_lisp`.

## Civil 3D
- `civil3d_objects kind=surfaces|alignments|profiles|corridors`, `civil3d_com_surfaces`, `civil3d_com_alignments`, `civil3d_com_points`.
- For corridors, grading and pipe networks, use the API (`api_*`, `script_run` with the civil root) or `ribbon_invoke`.

## Output
- `autocad_layouts`, then `autocad_plot_pdf` (batch). `autocad_export` for DXF and DWG versions.

## Verify
- Capture the result. Re-run `autocad_layer_report` and the quantities.
