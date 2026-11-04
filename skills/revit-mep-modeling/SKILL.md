---
name: revit-mep-modeling
description: "MEP modelling in Revit: HVAC ducts, plumbing/piping, fire protection, electrical (lighting, power, circuits, trays/conduits); systems, sizing, connections and clearances."
metadata:
  discipline: "Revit / MEP"
  product: "BURAK BIM 1.3"
---
# Revit MEP - mechanical, electrical, plumbing and piping

## Prepare
- Link or confirm the architectural and structural models: `revit_links`.
- Create spaces with `revit_spaces_from_rooms`, then set up `revit_hvac_zones`.
- Read the available system and segment types with `revit_mep_types`. Never invent types.

## HVAC
- **Equipment and terminals:** `revit_place_instances` (family type, points).
- **Ducts:** `revit_duct_route` (path, system, size). Size with `revit_duct_size`.
- **Connect:** `revit_mep_connect`, `revit_mep_tee`.
- **Airflow:** `revit_space_airflow` against the design airflow.

## Plumbing and piping
- **Pipes:** `revit_pipe_route` (system: domestic cold or hot water, sanitary, vent, chilled water ...).
- **Falls:** `revit_pipe_slope_set` for sanitary pipes (for example 1-2 %).
- **Size and connect:** `revit_pipe_size`; `revit_mep_connect` to fixtures.
- **Insulation:** `revit_insulation_add`.

## Electrical
- **Lights:** `revit_lights_grid` (spacing, mounting height).
- **Devices:** `revit_place_instances`.
- **Circuits:** `revit_circuit_create`, then `revit_electrical_panels` / `revit_electrical_circuits`.
- **Trays and conduits:** `revit_cable_tray_route`, `revit_conduit_route`.

## Check
- Open ends: `revit_mep_open_ends` must show 0 in completed systems.
- Connectivity and colours: `revit_mep_connectors`, `revit_mep_color_by_system`.
- Clearances: `revit_mep_clearance` (for example 50 mm from structure). `revit_clash_check` for MEP against structure.
- Capture plans and a 3D section box per riser (`revit_section_box`).
- Run the bim-coordination skill in Navisworks for federated clash detection.
