---
name: archicad-modeling
description: "Graphisoft Archicad through BURAK BIM: JSON API + Tapir - elements, properties, classifications, layouts, publishing."
metadata:
  discipline: "Archicad"
  product: "BURAK BIM 1.3"
---
# Archicad

1. **Requirements:** Archicad running with the JSON API enabled. Install the free **Tapir** add-on for the create/modify commands.
   `archicad_info` shows the version, port and Tapir status.
2. **Explore:**
   - `archicad_elements types=[Wall,Slab,...]`
   - `archicad_properties`, `archicad_classifications`, `archicad_navigator` (views and layouts)
3. **Model:**
   - Simple elements: `archicad_create`, `archicad_move`.
   - Everything else: the Tapir catalog tools (`burak_tools query="archicad create slab"`, then `burak_run`).
   - Use bulk property updates where possible.
4. **Document:** `archicad_publish` (publisher sets), and layouts through the navigator tools.
5. **Verify:** `capture app=archicad`, then re-read the element counts and properties.
