---
name: revit-family-creation
description: "Revit family creation and editing: parameters, types, formulas, reference planes, nested families, loading and testing in a project."
metadata:
  discipline: "Revit / Content"
  product: "BURAK BIM 1.3"
---
# Revit family creation and editing

1. **Start from a template:** `revit_open` a family template (.rft) or an existing .rfa. Family documents use the same tools; `revit_info` shows isFamily.
2. **Parameters and types:**
   - `revit_family_edit_parameter` (add, set formula, type or instance) and `revit_family_types`.
   - Use **formulas** for dependent dimensions. Keep instance parameters to what users actually change.
3. **Geometry:** use `script_run` with FamilyItemFactory extrusions, sweeps and blends locked to reference planes.
   - Name the reference planes (Left, Right, Front, Back, Top) and set IsReference.
   - Never draw geometry without constraints.
4. **Flex test:** set each type with `revit_family_types` and capture a 3D view and a plan for each. Geometry must follow every type.
5. **Load and test:** `revit_family_reload`, or `revit_load_family` into the project. Place an instance (`revit_place_instances`), tag it and schedule it.
6. **Standards:**
   - Name it per the practice convention.
   - Set Category, OmniClass/Uniclass and shared parameters for schedules.
   - Keep it light: no imported CAD, and keep detail levels in mind.
