---
name: rhino-grasshopper-computational
description: "Computational design in Rhino and Grasshopper: inspect the document, run definitions with controlled inputs, script safely, compare options, and check the geometry produced."
metadata:
  discipline: "Computational design"
  product: "BURAK BIM 1.3"
---
# Rhino and Grasshopper computational design

1. **Inspect:** `rhino_info`, `rhino_layers`, then `rhino_objects` filtered by layer. Start with counts, not every object.
2. **Agree the brief:** inputs, ranges, units, the output layer, and how options will be compared.
3. **Grasshopper:** `grasshopper_open` the definition, then `grasshopper_run` with one small test case first.
4. **Rhino edits:** use `rhino_add` or `rhino_command` for simple work and `rhino_script` for repeated work. Write to a new layer so the original stays untouched.
5. **Verify:** `rhino_measure` for key dimensions, `rhino_objects` for counts, `rhino_capture_view` for a picture.
6. **Compare options:** a table with input values, key measures and an image per option. Let the user choose.
7. **Move to Revit or Dynamo if needed:** `dynamo_inspect`, `dynamo_set_inputs`, then `revit_dynamo_run` after approval. Check the result with `revit_capture`.
