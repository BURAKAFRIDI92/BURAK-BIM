<p align="center"><img src="../assets/burakbim-logo.png" alt="BURAK-BIM" width="220"></p>

# BURAK BIM tools

984 tools, generated from the server (`BurakBim.Server.exe --tools-md`). The AI also has `burak_tools` (search) and `burak_run` (run by name).
### api

| Tool | What it does | Changes model |
|---|---|---|
| `api_call` | Call any API method or constructor ('new'). target = object path/handle or type:Full.Name for statics. args are JSON: numbers, strings, enum names, [x,y,z] for XYZ/Point3d, "$h12" handles, {"$new":"Type","args":[...]}. In Revit the call runs inside a transaction (one undo step) unless transaction=false. | yes |
| `api_get` | Evaluate an API path and return the value. Roots - Revit: doc, uidoc, uiapp, app, view, selection. AutoCAD: doc, db, ed, docs, civil. Navisworks: doc, clash, timeliner, selection. Also $h<handle> and type:Full.Type.Name for statics. Path can include [index] and Method() calls without args or with JSON args. | no |
| `api_inspect` | Constructors, properties, methods, fields and enum values of an object (path) or a type (full name). Use it to discover the API before api_call/script_run. | no |
| `api_set` | Set a writable property or field on an API object (inside a transaction in Revit). | yes |
| `api_types` | Search loaded .NET API types by name in Revit / AutoCAD / Civil 3D / Navisworks (e.g. 'Rebar', 'Alignment', 'ClashTest'). | no |
| `ribbon_invoke` | Presses any ribbon button exactly like a user click: by id (from ribbon_list), by path 'Tab > Panel > Button', or by button text. Revit buttons (incl. add-ins) run through Revit's own command system; AutoCAD buttons run their macro. | yes |
| `ribbon_list` | Lists EVERY ribbon item (built-in, contextual and third-party add-in tabs: Architecture, Structure, Steel, Precast, Systems, Insert, Annotate, Analyze, Massing & Site, Collaborate, View, Manage, Add-Ins, Modify...) of Revit, AutoCAD/Civil 3D or Navisworks. Filter with tab and search. Use the id or path with ribbon_invoke. | no |
| `script_run` | Compile and run C# against the live API (Roslyn). Revit globals: doc, uidoc, uiapp, app. AutoCAD: doc, db, ed, civil. Navisworks: doc. Args: Args dictionary. Print(x) streams text. The value of the last expression / return is returned. Revit: runs in one transaction (single undo) unless transaction=false. No 'await'. | yes |

### archicad

| Tool | What it does | Changes model |
|---|---|---|
| `archicad_add_comment_to_issue` | Adds a new comment to the specified issue. (Tapir add-on command AddCommentToIssue, since Tapir 1.0.6; install the free Tapir add-on in Archicad.) | yes |
| `archicad_add_files_to_embedded_library` | Adds the given files into the embedded library. With the overwriteExisting flag an embedded library item already existing on an outputPath is replaced (Archicad 27 or newer). (Tapir add-on command AddFilesToEmbeddedLibrary, since Tapir 1.2.2; install the free Tapir add-on in Archicad.) | yes |
| `archicad_add_libraries` | Adds the given folders to the project's local libraries, skipping any already loaded. (Tapir add-on command AddLibraries, since Tapir 1.5.9; install the free Tapir add-on in Archicad.) | yes |
| `archicad_apply_favorites_to_element_defaults` | Apply the given favorites to element defaults. (Tapir add-on command ApplyFavoritesToElementDefaults, since Tapir 1.1.2; install the free Tapir add-on in Archicad.) | yes |
| `archicad_apply_favorites_to_elements` | Apply the given favorites to existing elements. Only settings-type parameters are changed - geometry (position, floor, and dimensions such as a Wall's height) is left untouched, so applying a Favorite never moves or resizes the target element. For the hierarchical types (Stair, Railing, Curtain Wall) the settings of the sub-elements are not applied, because they are inseparable from the Favorite's own geometry. By default settings, classifications, categories and properties are all applied; each can be opted out of individually. (Tapir add-on command ApplyFavoritesToElements, since Tapir 1.5.4; install the free Tapir add-on in Archicad.) | yes |
| `archicad_attach_elements_to_issue` | Attaches elements to the specified issue. (Tapir add-on command AttachElementsToIssue, since Tapir 1.0.6; install the free Tapir add-on in Archicad.) | yes |
| `archicad_change_drawing_link` | Relinks a Drawing to a different source navigator item. Archicad has no in-place relink API, so this recreates the Drawing against the new source and deletes the original - the returned elementId is a NEW guid, not the input one. The Drawing Title marker's own position is not preserved (undocumented Archicad limitation). (Tapir add-on command ChangeDrawingLink, since Tapir 1.5.7; install the free Tapir add-on in Archicad.) | yes |
| `archicad_change_hotlink_instances` | Moves, rotates or mirrors placed hotlink instances by changing their transformation. MoveElements and RotateElements do not work on hotlink instances. (Tapir add-on command ChangeHotlinkInstances, since Tapir 1.5.9; install the free Tapir add-on in Archicad.) | yes |
| `archicad_change_hotlink_nodes` | Repoints hotlink module nodes at other source files, as the Hotlink Manager's Relink button does. Follow it with UpdateHotlinks to re-read the content. (Tapir add-on command ChangeHotlinkNodes, since Tapir 1.7.1; install the free Tapir add-on in Archicad.) | yes |
| `archicad_change_selection_of_elements` | Adds/removes a number of elements to/from the current selection. (Tapir add-on command ChangeSelectionOfElements, since Tapir 1.0.7; install the free Tapir add-on in Archicad.) | yes |
| `archicad_change_window` | Changes the current (active) window to the given window. With windowType 'FloorPlan' and a storyIndex it also activates that story on the floor plan. (Tapir add-on command ChangeWindow, since Tapir 1.3.1; install the free Tapir add-on in Archicad.) | yes |
| `archicad_classifications` | List classification systems, the items of a system, the classifications of elements, or set an element's classification. | yes |
| `archicad_clone_project_map_item_to_view_map` | Clones a project map item to the view map. (Graphisoft JSON API API.CloneProjectMapItemToViewMap, Archicad 24+.) | yes |
| `archicad_close_project` | Closes the currently opened project. (Tapir add-on command CloseProject, since Tapir 1.3.1; install the free Tapir add-on in Archicad.) | yes |
| `archicad_command` | Send any Archicad JSON API command (API.GetElementsByType, API.GetBoundingBoxes3D, API.GetAttributesByType, API.GetBuildingMaterialPhysicalProperties, ...) with its parameters. | yes |
| `archicad_connect_mep_elements` | Connects MEP routing elements to other MEP elements or routes. Merges routes, splits routes or creates branch elements as needed. Available from Archicad 28. (Tapir add-on command ConnectMEPElements, since Tapir 1.5.6; install the free Tapir add-on in Archicad.) | yes |
| `archicad_connect_to_electrical_circuits` | Connects electrical devices, or panels as sub-panels, to electrical circuits. Available from Archicad 30. (Tapir add-on command ConnectToElectricalCircuits, since Tapir 1.7.2; install the free Tapir add-on in Archicad.) | yes |
| `archicad_create` | Create columns at points, slabs from polygons, or library objects at points via Tapir. | yes |
| `archicad_create_arcs` | Creates Arc elements based on the given parameters. (Tapir add-on command CreateArcs, since Tapir 1.5.6; install the free Tapir add-on in Archicad.) | yes |
| `archicad_create_associative_dimensions` | Creates associative linear dimensions from explicit witness point references. (Tapir add-on command CreateAssociativeDimensions, since Tapir 1.4.0; install the free Tapir add-on in Archicad.) | yes |
| `archicad_create_associative_dimensions_on_section` | Creates associative linear dimensions on section elements using common wall, slab, beam, column and opening presets. The preset points of multiple section elements can be merged into one continuous dimension chain via sectionElementIds. (Tapir add-on command CreateAssociativeDimensionsOnSection, since Tapir 1.4.0; install the free Tapir add-on in Archicad.) | yes |
| `archicad_create_attribute_folders` | Creates attribute folders. To create a folder, its full path has to be provided. The command will create all folders along the path, if they do not exist. (Graphisoft JSON API API.CreateAttributeFolders, Archicad 26+.) | yes |
| `archicad_create_beams` | Creates Beam elements based on the given parameters. (Tapir add-on command CreateBeams, since Tapir 1.4.0; install the free Tapir add-on in Archicad.) | yes |
| `archicad_create_building_materials` | Creates or overwrites Building Material attributes based on the given parameters. (Tapir add-on command CreateBuildingMaterials, since Tapir 1.0.1; install the free Tapir add-on in Archicad.) | yes |
| `archicad_create_circles` | Creates Circle elements based on the given parameters. (Tapir add-on command CreateCircles, since Tapir 1.5.6; install the free Tapir add-on in Archicad.) | yes |
| `archicad_create_classification_items` | Creates Classification Items in the given Classification Systems based on the given parameters. (Tapir add-on command CreateClassificationItems, since Tapir 1.5.0; install the free Tapir add-on in Archicad.) | yes |
| `archicad_create_classification_systems` | Creates Classification Systems including Classification Items based on the given parameters. (Tapir add-on command CreateClassificationSystems, since Tapir 1.5.0; install the free Tapir add-on in Archicad.) | yes |
| `archicad_create_columns` | Creates Column elements based on the given parameters. (Tapir add-on command CreateColumns, since Tapir 1.0.3; install the free Tapir add-on in Archicad.) | yes |
| `archicad_create_composites` | Creates or overwrites Composite attributes based on the given parameters. (Tapir add-on command CreateComposites, since Tapir 1.0.2; install the free Tapir add-on in Archicad.) | yes |
| `archicad_create_curtain_walls` | Creates Curtain Wall elements along a reference polyline with a column (primary) and row (secondary) pattern. The frame and panel classes come from the favorite or from the Curtain Wall tool defaults; the pattern cells can assign them to the grid. (Tapir add-on command CreateCurtainWalls, since Tapir 1.7.2; install the free Tapir add-on in Archicad.) | yes |
| `archicad_create_design_option_combinations` | Creates new design option combinations with the given parameters. Available from Archicad 29. (Tapir add-on command CreateDesignOptionCombinations, since Tapir 1.5.1; install the free Tapir add-on in Archicad.) | yes |
| `archicad_create_design_option_sets` | Creates new design option sets with the given names. Available from Archicad 29. (Tapir add-on command CreateDesignOptionSets, since Tapir 1.5.1; install the free Tapir add-on in Archicad.) | yes |
| `archicad_create_design_options` | Creates new design options with the given parameters. Available from Archicad 29. (Tapir add-on command CreateDesignOptions, since Tapir 1.5.1; install the free Tapir add-on in Archicad.) | yes |
| `archicad_create_details` | Creates independent Detail databases. (Tapir add-on command CreateDetails, since Tapir 1.4.0; install the free Tapir add-on in Archicad.) | yes |
| `archicad_create_doors` | Creates Door elements in host walls based on the given parameters. (Tapir add-on command CreateDoors, since Tapir 1.4.0; install the free Tapir add-on in Archicad.) | yes |
| `archicad_create_drawings` | Creates Drawing elements on the specified or active layout from navigator items. (Tapir add-on command CreateDrawings, since Tapir 1.4.0; install the free Tapir add-on in Archicad.) | yes |
| `archicad_create_electrical_circuits` | Adds new circuits to electrical panels, optionally with the given description, voltage, wiring type and used phases. Available from Archicad 30. (Tapir add-on command CreateElectricalCircuits, since Tapir 1.7.2; install the free Tapir add-on in Archicad.) | yes |
| `archicad_create_electrical_devices` | Places electrical devices with the default settings of the Electrical Device tool, optionally with the given library part, size, rotation, mirroring, apparent power and load classification, and connects them to a circuit. Available from Archicad 30. (Tapir add-on command CreateElectricalDevices, since Tapir 1.7.2; install the free Tapir add-on in Archicad.) | yes |
| `archicad_create_electrical_panels` | Places electrical panels with the default settings of the Electrical Panel tool, optionally with the given library part, size, rotation, mirroring, panel name and circuit naming. Available from Archicad 30. (Tapir add-on command CreateElectricalPanels, since Tapir 1.7.2; install the free Tapir add-on in Archicad.) | yes |
| `archicad_create_favorites_from_elements` | Create favorites from the given elements. (Tapir add-on command CreateFavoritesFromElements, since Tapir 1.1.2; install the free Tapir add-on in Archicad.) | yes |
| `archicad_create_fills` | Creates or overwrites Fill attributes based on the given parameters. (Tapir add-on command CreateFills, since Tapir 1.5.4; install the free Tapir add-on in Archicad.) | yes |
| `archicad_create_graphic_override_combinations` | Creates graphic override combinations from existing rules. Available from Archicad 27. (Tapir add-on command CreateGraphicOverrideCombinations, since Tapir 1.7.2; install the free Tapir add-on in Archicad.) | yes |
| `archicad_create_graphic_override_rules` | Creates graphic override rules. The criterion is given as Archicad's criterion XML or as structured conditions. The rule group is created when no rule group has the given name. Available from Archicad 27. (Tapir add-on command CreateGraphicOverrideRules, since Tapir 1.7.2; install the free Tapir add-on in Archicad.) | yes |
| `archicad_create_graphical_override_combination` | Creates a graphic override combination from rule names, or replaces the rules of an existing one. Needs Archicad 27 or later. (Tapir add-on command CreateGraphicalOverrideCombination, since Tapir 1.7.2; install the free Tapir add-on in Archicad.) | yes |
| `archicad_create_graphical_override_rule` | Creates or overwrites a graphic override rule in a rule group: name, criterion XML, surface color and line pen. Needs Archicad 27 or later. (Tapir add-on command CreateGraphicalOverrideRule, since Tapir 1.7.2; install the free Tapir add-on in Archicad.) | yes |
| `archicad_create_grid_elements` | Creates grid elements, the elements the Grid Tool places, from begin and end coordinates, with optional story, layer and GDL parameters. Needs Archicad 26 or later. (Tapir add-on command CreateGridElements, since Tapir 1.7.2; install the free Tapir add-on in Archicad.) | yes |
| `archicad_create_groups` | Creates groups of the passed elements (Tapir add-on command CreateGroups, since Tapir 1.4.0; install the free Tapir add-on in Archicad.) | yes |
| `archicad_create_hatches` | Creates Hatch elements based on the given parameters. (Tapir add-on command CreateHatches, since Tapir 1.5.6; install the free Tapir add-on in Archicad.) | yes |
| `archicad_create_hotlink_instances` | Places instances of hotlink module nodes at an origin, rotation and mirroring. (Tapir add-on command CreateHotlinkInstances, since Tapir 1.5.9; install the free Tapir add-on in Archicad.) | yes |
| `archicad_create_hotlink_nodes` | Creates hotlink module nodes from source files. A node that already points at the same file is returned instead of duplicated (Archicad 26 and later; 25 cannot see an unplaced node). (Tapir add-on command CreateHotlinkNodes, since Tapir 1.5.9; install the free Tapir add-on in Archicad.) | yes |
| `archicad_create_hotspots` | Creates Hotspot elements based on the given parameters. (Tapir add-on command CreateHotspots, since Tapir 1.5.6; install the free Tapir add-on in Archicad.) | yes |
| `archicad_create_interior_elevations` | Creates Interior Elevation elements on the floor plan. Every consecutive pair of the given points becomes one segment, each with its own viewpoint - feed it the corner points of a room to get an interior elevation of that room. (Tapir add-on command CreateInteriorElevations, since Tapir 1.5.8; install the free Tapir add-on in Archicad.) | yes |
| `archicad_create_issue` | Creates a new issue. (Tapir add-on command CreateIssue, since Tapir 1.0.2; install the free Tapir add-on in Archicad.) | yes |
| `archicad_create_keynote_folders` | Creates keynote folders under the given parent folders (or under the root folder). Available from Archicad 28. (Tapir add-on command CreateKeynoteFolders, since Tapir 1.5.6; install the free Tapir add-on in Archicad.) | yes |
| `archicad_create_keynote_items` | Creates keynote items in the given parent folders (or in the root folder). Available from Archicad 28. (Tapir add-on command CreateKeynoteItems, since Tapir 1.5.6; install the free Tapir add-on in Archicad.) | yes |
| `archicad_create_keynote_labels` | Creates Label elements that reference the given keynote items via autotext. Available from Archicad 28. (Tapir add-on command CreateKeynoteLabels, since Tapir 1.5.6; install the free Tapir add-on in Archicad.) | yes |
| `archicad_create_labels` | Creates Label elements based on the given parameters. (Tapir add-on command CreateLabels, since Tapir 1.2.5; install the free Tapir add-on in Archicad.) | yes |
| `archicad_create_lamps` | Creates Lamp elements based on the given parameters. (Tapir add-on command CreateLamps, since Tapir 1.5.7; install the free Tapir add-on in Archicad.) | yes |
| `archicad_create_layer_combinations` | Creates or overwrites Layer Combination attributes based on the given parameters. (Tapir add-on command CreateLayerCombinations, since Tapir 1.2.4; install the free Tapir add-on in Archicad.) | yes |
| `archicad_create_layers` | Creates or overwrites Layer attributes based on the given parameters. (Tapir add-on command CreateLayers, since Tapir 1.0.3; install the free Tapir add-on in Archicad.) | yes |
| `archicad_create_layout` | Creates a new layout. (Graphisoft JSON API API.CreateLayout, Archicad 24+.) | yes |
| `archicad_create_layout_subset` | Creates a new layout subset. (Graphisoft JSON API API.CreateLayoutSubset, Archicad 24+.) | yes |
| `archicad_create_level_dimensions` | Creates Level Dimension elements (the Level Dimension tool) based on the given parameters. With a parentElementId the dimension is associative: it shows the height of the parent element (Mesh, Slab, Roof ...) at its position and follows the element when it changes. (Tapir add-on command CreateLevelDimensions, since Tapir 1.7.2; install the free Tapir add-on in Archicad.) | yes |
| `archicad_create_line_elements` | Creates Line elements based on the given parameters. (Tapir add-on command CreateLineElements, since Tapir 1.5.6; install the free Tapir add-on in Archicad.) | yes |
| `archicad_create_lines` | Creates or overwrites Line attributes based on the given parameters. (Tapir add-on command CreateLines, since Tapir 1.5.4; install the free Tapir add-on in Archicad.) | yes |
| `archicad_create_mep_elements` | Creates MEP elements (Terminal, Accessory, Equipment or Fitting) at the given positions. Available from Archicad 28. (Tapir add-on command CreateMEPElements, since Tapir 1.5.6; install the free Tapir add-on in Archicad.) | yes |
| `archicad_create_mep_physical_systems` | Creates physical systems in MEP system groups, optionally with a root element and an MEP system as their category. Available from Archicad 29. (Tapir add-on command CreateMEPPhysicalSystems, since Tapir 1.7.2; install the free Tapir add-on in Archicad.) | yes |
| `archicad_create_mep_routing_elements` | Creates MEP routing elements (duct, pipe or cable carrier routes) along the given polylines with optional cross section data (shape, size, preference table and reference id), MEP system and layer (also set on the segments, elbows and transitions). Available from Archicad 28. (Tapir add-on command CreateMEPRoutingElements, since Tapir 1.5.6; install the free Tapir add-on in Archicad.) | yes |
| `archicad_create_mep_system_groups` | Creates MEP system groups. Available from Archicad 29. (Tapir add-on command CreateMEPSystemGroups, since Tapir 1.7.2; install the free Tapir add-on in Archicad.) | yes |
| `archicad_create_mep_systems` | Creates or overwrites MEP System attributes (domain, pens, fill, center line type, surface and insulation surface) based on the given parameters. (Tapir add-on command CreateMEPSystems, since Tapir 1.5.4; install the free Tapir add-on in Archicad.) | yes |
| `archicad_create_meshes` | Creates Mesh elements based on the given parameters. (Tapir add-on command CreateMeshes, since Tapir 1.1.9; install the free Tapir add-on in Archicad.) | yes |
| `archicad_create_morphs` | Creates Morph elements from simple box definitions. (Tapir add-on command CreateMorphs, since Tapir 1.4.0; install the free Tapir add-on in Archicad.) | yes |
| `archicad_create_objects` | Creates Object elements based on the given parameters. (Tapir add-on command CreateObjects, since Tapir 1.5.7; install the free Tapir add-on in Archicad.) | yes |
| `archicad_create_openings` | Creates Opening elements in the given host elements. (Tapir add-on command CreateOpenings, since Tapir 1.4.0; install the free Tapir add-on in Archicad.) | yes |
| `archicad_create_pen_tables` | Creates or overwrites Pen Table attributes based on the given parameters. (Tapir add-on command CreatePenTables, since Tapir 1.5.4; install the free Tapir add-on in Archicad.) | yes |
| `archicad_create_polylines` | Creates Polyline elements based on the given parameters. (Tapir add-on command CreatePolylines, since Tapir 1.1.5; install the free Tapir add-on in Archicad.) | yes |
| `archicad_create_profiles` | Creates or overwrites Profile attributes as a copy of an existing Profile's geometry, based on the given parameters. (Tapir add-on command CreateProfiles, since Tapir 1.5.4; install the free Tapir add-on in Archicad.) | yes |
| `archicad_create_project_info_fields` | Creates one or more custom project info fields. (Tapir add-on command CreateProjectInfoFields, since Tapir 1.5.2; install the free Tapir add-on in Archicad.) | yes |
| `archicad_create_property_definitions` | Creates Custom Property Definitions based on the given parameters. (Tapir add-on command CreatePropertyDefinitions, since Tapir 1.0.9; install the free Tapir add-on in Archicad.) | yes |
| `archicad_create_property_groups` | Creates Property Groups based on the given parameters. (Tapir add-on command CreatePropertyGroups, since Tapir 1.0.7; install the free Tapir add-on in Archicad.) | yes |
| `archicad_create_roofs` | Creates Roof elements based on footprint, level and roof profile data. Creates a multi-plane roof by default; pass 'pivotLine' (and optionally 'angle') to create a single-plane roof instead. (Tapir add-on command CreateRoofs, since Tapir 1.4.0; install the free Tapir add-on in Archicad.) | yes |
| `archicad_create_sections` | Creates Section elements on the floor plan. (Tapir add-on command CreateSections, since Tapir 1.5.0; install the free Tapir add-on in Archicad.) | yes |
| `archicad_create_slabs` | Creates Slab elements based on the given parameters. (Tapir add-on command CreateSlabs, since Tapir 1.0.3; install the free Tapir add-on in Archicad.) | yes |
| `archicad_create_solid_element_links` | Creates solid element operation links between target and operator elements. (Tapir add-on command CreateSolidElementLinks, since Tapir 1.5.4; install the free Tapir add-on in Archicad.) | yes |
| `archicad_create_splines` | Creates Spline elements based on the given parameters. (Tapir add-on command CreateSplines, since Tapir 1.5.6; install the free Tapir add-on in Archicad.) | yes |
| `archicad_create_stairs` | Creates Stair elements based on the given baseline and parameters. (Tapir add-on command CreateStairs, since Tapir 1.5.0; install the free Tapir add-on in Archicad.) | yes |
| `archicad_create_surfaces` | Creates or overwrites Surface attributes based on the given parameters. (Tapir add-on command CreateSurfaces, since Tapir 1.2.2; install the free Tapir add-on in Archicad.) | yes |
| `archicad_create_texts` | Creates standalone Text elements based on the given parameters. (Tapir add-on command CreateTexts, since Tapir 1.5.0; install the free Tapir add-on in Archicad.) | yes |
| `archicad_create_view_map_folder` | Creates a view folder item at the given position in the navigator tree. (Graphisoft JSON API API.CreateViewMapFolder, Archicad 24+.) | yes |
| `archicad_create_views_in_view_map` | Creates independent (non-clone) navigator views in a specified View Map folder, by copying database and settings from source items. (Tapir add-on command CreateViewsInViewMap, since Tapir 1.1.7; install the free Tapir add-on in Archicad.) | yes |
| `archicad_create_wall_thickness_dimensions` | Creates associative wall thickness dimensions for the given walls. (Tapir add-on command CreateWallThicknessDimensions, since Tapir 1.4.0; install the free Tapir add-on in Archicad.) | yes |
| `archicad_create_walls` | Creates Wall elements based on the given parameters. (Tapir add-on command CreateWalls, since Tapir 1.4.0; install the free Tapir add-on in Archicad.) | yes |
| `archicad_create_windows` | Creates Window elements in host walls based on the given parameters. (Tapir add-on command CreateWindows, since Tapir 1.4.0; install the free Tapir add-on in Archicad.) | yes |
| `archicad_create_worksheets` | Creates independent Worksheet databases. (Tapir add-on command CreateWorksheets, since Tapir 1.4.0; install the free Tapir add-on in Archicad.) | yes |
| `archicad_create_zone_categories` | Creates or overwrites Zone Category attributes based on the given parameters. (Tapir add-on command CreateZoneCategories, since Tapir 1.5.4; install the free Tapir add-on in Archicad.) | yes |
| `archicad_create_zones` | Creates Zone elements based on the given parameters. (Tapir add-on command CreateZones, since Tapir 1.1.8; install the free Tapir add-on in Archicad.) | yes |
| `archicad_delete_attribute_folders` | Deletes attribute folders and all the deletable attributes and folders it contains. To delete a folder, its identifier has to be provided. (Graphisoft JSON API API.DeleteAttributeFolders, Archicad 26+.) | yes |
| `archicad_delete_attributes` | Deletes attributes. (Graphisoft JSON API API.DeleteAttributes, Archicad 26+.) | yes |
| `archicad_delete_classification_items` | Deletes the given Classification Items. (Tapir add-on command DeleteClassificationItems, since Tapir 1.5.2; install the free Tapir add-on in Archicad.) | yes |
| `archicad_delete_classification_systems` | Deletes the given Classification Systems. (Tapir add-on command DeleteClassificationSystems, since Tapir 1.5.2; install the free Tapir add-on in Archicad.) | yes |
| `archicad_delete_design_option_combinations` | Deletes the given design option combinations. Available from Archicad 29. (Tapir add-on command DeleteDesignOptionCombinations, since Tapir 1.7.1; install the free Tapir add-on in Archicad.) | yes |
| `archicad_delete_design_option_sets` | Deletes the given design option sets. Available from Archicad 29. (Tapir add-on command DeleteDesignOptionSets, since Tapir 1.7.1; install the free Tapir add-on in Archicad.) | yes |
| `archicad_delete_design_options` | Deletes the given design options. Available from Archicad 29. (Tapir add-on command DeleteDesignOptions, since Tapir 1.7.1; install the free Tapir add-on in Archicad.) | yes |
| `archicad_delete_electrical_circuits` | Removes electrical circuits from their panels. Available from Archicad 30. (Tapir add-on command DeleteElectricalCircuits, since Tapir 1.7.2; install the free Tapir add-on in Archicad.) | yes |
| `archicad_delete_elements` | Deletes elements. Returns an execution result for each input element: an element that could not be deleted (for example because its layer is locked) gets a failed execution result instead of being skipped silently. (Tapir add-on command DeleteElements, since Tapir 1.2.1; install the free Tapir add-on in Archicad.) | yes |
| `archicad_delete_embedded_library_items` | Deletes the given items from the embedded library. The path of an item is the same relative path that AddFilesToEmbeddedLibrary takes as outputPath. Available from Archicad 27. (Tapir add-on command DeleteEmbeddedLibraryItems, since Tapir 1.6.0; install the free Tapir add-on in Archicad.) | yes |
| `archicad_delete_favorites` | Delete existing favorites. (Tapir add-on command DeleteFavorites, since Tapir 1.5.4; install the free Tapir add-on in Archicad.) | yes |
| `archicad_delete_graphic_override_combinations` | Deletes graphic override combinations. Their rules are kept. Available from Archicad 27. (Tapir add-on command DeleteGraphicOverrideCombinations, since Tapir 1.7.2; install the free Tapir add-on in Archicad.) | yes |
| `archicad_delete_graphic_override_rule_groups` | Deletes graphic override rule groups, found by name. A group that still contains rules is kept unless its rules are moved to another group or deleted with it. Available from Archicad 27. (Tapir add-on command DeleteGraphicOverrideRuleGroups, since Tapir 1.7.2; install the free Tapir add-on in Archicad.) | yes |
| `archicad_delete_graphic_override_rules` | Deletes graphic override rules. They are also removed from the combinations that contain them. Available from Archicad 27. (Tapir add-on command DeleteGraphicOverrideRules, since Tapir 1.7.2; install the free Tapir add-on in Archicad.) | yes |
| `archicad_delete_issue` | Deletes the specified issue. (Tapir add-on command DeleteIssue, since Tapir 1.0.2; install the free Tapir add-on in Archicad.) | yes |
| `archicad_delete_keynote_folders` | Deletes the given keynote folders including their content. Available from Archicad 28. (Tapir add-on command DeleteKeynoteFolders, since Tapir 1.5.6; install the free Tapir add-on in Archicad.) | yes |
| `archicad_delete_keynote_items` | Deletes the given keynote items. Available from Archicad 28. (Tapir add-on command DeleteKeynoteItems, since Tapir 1.5.6; install the free Tapir add-on in Archicad.) | yes |
| `archicad_delete_navigator_items` | Deletes items from navigator tree. (Graphisoft JSON API API.DeleteNavigatorItems, Archicad 24+.) | yes |
| `archicad_delete_project_info_fields` | Deletes one or more custom project info fields. Hardcoded fields cannot be deleted. (Tapir add-on command DeleteProjectInfoFields, since Tapir 1.5.4; install the free Tapir add-on in Archicad.) | yes |
| `archicad_delete_property_definitions` | Deletes the given Custom Property Definitions. (Tapir add-on command DeletePropertyDefinitions, since Tapir 1.0.9; install the free Tapir add-on in Archicad.) | yes |
| `archicad_delete_property_groups` | Deletes the given Custom Property Groups. (Tapir add-on command DeletePropertyGroups, since Tapir 1.0.9; install the free Tapir add-on in Archicad.) | yes |
| `archicad_detach_elements_from_issue` | Detaches elements from the specified issue. (Tapir add-on command DetachElementsFromIssue, since Tapir 1.0.6; install the free Tapir add-on in Archicad.) | yes |
| `archicad_elements` | Element GUIDs of a type (Wall, Slab, Column, Beam, Door, Window, Object, Zone, Roof, Stair, Railing, CurtainWall, Mesh, Morph, ...) or all elements, optionally with 3D bounding boxes. | no |
| `archicad_execute_add_on_command` | Executes a command registered in an Add-On. (Graphisoft JSON API API.ExecuteAddOnCommand, Archicad 25+.) | yes |
| `archicad_export_favorites` | Export the project's Favorites to a .prefs file or folder. (Tapir add-on command ExportFavorites, since Tapir 1.5.0; install the free Tapir add-on in Archicad.) | yes |
| `archicad_export_issues_to_bcf` | Exports specified issues to a BCF file. (Tapir add-on command ExportIssuesToBCF, since Tapir 1.0.6; install the free Tapir add-on in Archicad.) | yes |
| `archicad_filter_elements` | Tests an elements by the given criterias. (Tapir add-on command FilterElements, since Tapir 1.0.7; install the free Tapir add-on in Archicad.) | no |
| `archicad_fit_in_window` | Zooms to the given elements or fits everything in the window. (Tapir add-on command FitInWindow, since Tapir 1.3.1; install the free Tapir add-on in Archicad.) | yes |
| `archicad_generate_documentation` | Generates files for the documentation. Used by Tapir developers only. (Tapir add-on command GenerateDocumentation, since Tapir 1.0.7; install the free Tapir add-on in Archicad.) | yes |
| `archicad_get_2d_bounding_boxes` | Get the 2D bounding box of elements identified by their GUIDs. The bounding box is calculated from the global origin on the floor plan view. The output is the array of the bounding boxes respective to the input GUIDs. Only works for elements detailed in <i>Element Information</i>. (Graphisoft JSON API API.Get2DBoundingBoxes, Archicad 24+.) | no |
| `archicad_get_3d_bounding_boxes` | Get the 3D bounding box of elements identified by their GUIDs. The bounding box is calculated from the global origin in the 3D view. The output is the array of the bounding boxes respective to the input GUIDs. Only works for elements detailed in <i>Element Information</i>. (Graphisoft JSON API API.Get3DBoundingBoxes, Archicad 24+.) | no |
| `archicad_get_3d_geometry_of_elements` | Gets the 3D geometry of elements as the solid bodies of their 3D model: a vertex list in world coordinates and the planar faces (with their holes) referencing it, each face with its surface attribute when that is a project attribute. The bodies of the subelements of a Stair, Railing or Curtain Wall are included. The output is aligned with the input array of elements. The 3D model of the elements is generated as needed, so many elements can take a while. (Tapir add-on command Get3DGeometryOfElements, since Tapir 1.7.2; install the free Tapir add-on in Archicad.) | no |
| `archicad_get_3d_projection_settings` | Gets the 3D projection settings of the 3D window: whether it is perspective or axonometric, the camera and target position, view cone, roll angle, distance and azimuth of a perspective, or the transformation matrix of an axonometry. (Tapir add-on command Get3DProjectionSettings, since Tapir 1.7.1; install the free Tapir add-on in Archicad.) | no |
| `archicad_get_active_graphical_override_combination` | Gets the graphic override combination applied to the current window, with its id and name. The response is empty when no combination is applied. This is the combination of the window itself, not the one stored in a saved view (see GetViewSettings). Available from Archicad 30. (Tapir add-on command GetActiveGraphicalOverrideCombination, since Tapir 1.7.2; install the free Tapir add-on in Archicad.) | no |
| `archicad_get_active_pen_tables` | Returns the model view and layout book pen table identifiers. (Graphisoft JSON API API.GetActivePenTables, Archicad 25+.) | no |
| `archicad_get_add_on_version` | Retrieves the version of the Tapir Additional JSON Commands Add-On. (Tapir add-on command GetAddOnVersion, since Tapir 0.1.0; install the free Tapir add-on in Archicad.) | no |
| `archicad_get_all_classification_systems` | Returns the list of available classification systems. (Graphisoft JSON API API.GetAllClassificationSystems, Archicad 24+.) | no |
| `archicad_get_all_classifications_in_system` | Returns the tree of classifications in the given classification system. (Graphisoft JSON API API.GetAllClassificationsInSystem, Archicad 24+.) | no |
| `archicad_get_all_elements` | Returns the identifier of every element in the current plan. (Graphisoft JSON API API.GetAllElements, Archicad 24+.) | no |
| `archicad_get_all_properties` | Returns all user defined and built-in properties. (Tapir add-on command GetAllProperties, since Tapir 1.1.3; install the free Tapir add-on in Archicad.) | no |
| `archicad_get_all_property_group_ids` | Returns the identifier of every property group in the current plan. The optional propertyType parameter can be used to filter the results based on the type of the property group (Built-in or User Defined). (Graphisoft JSON API API.GetAllPropertyGroupIds, Archicad 26+.) | no |
| `archicad_get_all_property_ids` | Returns the identifier of every property in the current plan. The optional propertyType parameter can be used to filter the results based on the type of the property (Built-in or User Defined). (Graphisoft JSON API API.GetAllPropertyIds, Archicad 26+.) | no |
| `archicad_get_all_property_ids_of_elements` | Returns all property identifiers of the given elements. The optional propertyType parameter can be used to filter the results based on the type of the property (Built-in or User Defined). (Graphisoft JSON API API.GetAllPropertyIdsOfElements, Archicad 26+.) | no |
| `archicad_get_all_property_names` | Returns the human-readable names of available Property definitions for debug and development purposes. (Graphisoft JSON API API.GetAllPropertyNames, Archicad 24+.) | no |
| `archicad_get_archicad_location` | Retrieves the location of the currently running Archicad executable. (Tapir add-on command GetArchicadLocation, since Tapir 0.1.0; install the free Tapir add-on in Archicad.) | no |
| `archicad_get_attribute_folder` | Get an attribute folder's path and guid. To get an attribute folder guid, it's full path has to be provided and to get full path, it's guid has to be provided. (Graphisoft JSON API API.GetAttributeFolder, Archicad 26+.) | no |
| `archicad_get_attribute_folder_content` | Get attribute folder's content, subfolders and attributes. To get an attribute folder's content, it's full path or guid has to be provided. (Graphisoft JSON API API.GetAttributeFolderContent, Archicad 26+.) | no |
| `archicad_get_attribute_folder_structure` | Returns the detailed folder structure for the attributes of a given type. If the path is not given, the root folder will be returned (Graphisoft JSON API API.GetAttributeFolderStructure, Archicad 27+.) | no |
| `archicad_get_attribute_folders` | Returns the detailed attribute folders identified by their Ids. (Graphisoft JSON API API.GetAttributeFolders, Archicad 27+.) | no |
| `archicad_get_attributes_by_type` | Returns the identifier of every attribute of the given type. (Graphisoft JSON API API.GetAttributesByType, Archicad 25+.) | no |
| `archicad_get_attributes_creation_info` | Returns the creation information of attributes identified by their GUIDs. (Graphisoft JSON API API.GetAttributesCreationInfo, Archicad 30+.) | no |
| `archicad_get_attributes_indices` | Returns the requested indices and guids of attributes. (Graphisoft JSON API API.GetAttributesIndices, Archicad 28+.) | no |
| `archicad_get_auto_text_keys` | Retrieves the available autotext keys (name and embeddable key), optionally for a specific element. Embed a key in a Text or Label content by surrounding it with '<' and '>'. (Tapir add-on command GetAutoTextKeys, since Tapir 1.5.9; install the free Tapir add-on in Archicad.) | no |
| `archicad_get_auto_text_name` | Retrieves the display names of one or more autotext keys (as returned inside a '<...>' embedded key), with a direct guid lookup for property-based keys instead of enumerating every property definition. (Tapir add-on command GetAutoTextName, since Tapir 1.5.9; install the free Tapir add-on in Archicad.) | no |
| `archicad_get_available_library_parts` | Lists library parts currently available to the project. Filter by typeId (e.g. 'Door', 'Window', 'Object', 'Lamp'). (Tapir add-on command GetAvailableLibraryParts, since Tapir 1.5.0; install the free Tapir add-on in Archicad.) | no |
| `archicad_get_building_material_attributes` | Returns the detailed building material attributes identified by their GUIDs. (Graphisoft JSON API API.GetBuildingMaterialAttributes, Archicad 25+.) | no |
| `archicad_get_building_material_physical_properties` | Retrieves the physical properties of the given Building Materials. (Tapir add-on command GetBuildingMaterialPhysicalProperties, since Tapir 0.1.3; install the free Tapir add-on in Archicad.) | no |
| `archicad_get_building_materials` | Returns the details of the given Building Material attributes. (Tapir add-on command GetBuildingMaterials, since Tapir 1.5.4; install the free Tapir add-on in Archicad.) | no |
| `archicad_get_built_in_container_navigator_items` | Returns the details of the built-in container navigator items identified by their Ids. (Graphisoft JSON API API.GetBuiltInContainerNavigatorItems, Archicad 26+.) | no |
| `archicad_get_calculation_units` | Gets the project calculation units. (Tapir add-on command GetCalculationUnits, since Tapir 1.4.0; install the free Tapir add-on in Archicad.) | no |
| `archicad_get_classification_item_availability` | Returns the ids of property definitions available for a given classification item. (Graphisoft JSON API API.GetClassificationItemAvailability, Archicad 26+.) | no |
| `archicad_get_classification_system_ids` | Returns the list of available classification systems. (Graphisoft JSON API API.GetClassificationSystemIds, Archicad 26+.) | no |
| `archicad_get_classification_systems` | Returns the details of classification systems identified by their GUIDs. (Graphisoft JSON API API.GetClassificationSystems, Archicad 26+.) | no |
| `archicad_get_classifications_of_elements` | Returns the classification of the given elements in the given classification systems. (Graphisoft JSON API API.GetClassificationsOfElements, Archicad 24+.) | no |
| `archicad_get_collisions` | Detect collisions between the given two groups of elements. (Tapir add-on command GetCollisions, since Tapir 1.2.2; install the free Tapir add-on in Archicad.) | no |
| `archicad_get_comments_from_issue` | Retrieves comments information from the specified issue. (Tapir add-on command GetCommentsFromIssue, since Tapir 1.0.6; install the free Tapir add-on in Archicad.) | no |
| `archicad_get_components_of_elements` | Returns the identifier of every component for a list of elements. The order of the returned list is the same as the given elements. (Graphisoft JSON API API.GetComponentsOfElements, Archicad 25+.) | no |
| `archicad_get_composite_attributes` | Returns the detailed composite attributes identified by their GUIDs. (Graphisoft JSON API API.GetCompositeAttributes, Archicad 25+.) | no |
| `archicad_get_composites` | Returns the details of the given Composite attributes. (Tapir add-on command GetComposites, since Tapir 1.5.4; install the free Tapir add-on in Archicad.) | no |
| `archicad_get_connected_elements` | Gets the elements hosted by (connected to) the given owner elements, filtered to the given element type: for example the Windows or Doors of a Wall, the frames, panels, junctions and accessories of a Curtain Wall, the risers, treads and structures of a Stair, or the posts, rails and panels of a Railing. (Tapir add-on command GetConnectedElements, since Tapir 1.1.4; install the free Tapir add-on in Archicad.) | no |
| `archicad_get_current_revision_changes_of_layouts` | Retrieves all changes belong to the last revision of the given layouts. (Tapir add-on command GetCurrentRevisionChangesOfLayouts, since Tapir 1.1.9; install the free Tapir add-on in Archicad.) | no |
| `archicad_get_current_window_type` | Returns the type of the current (active) window. (Tapir add-on command GetCurrentWindowType, since Tapir 1.0.7; install the free Tapir add-on in Archicad.) | no |
| `archicad_get_database_id_from_navigator_item_id` | Gets the ID of the database associated with the supplied navigator item id (Tapir add-on command GetDatabaseIdFromNavigatorItemId, since Tapir 1.1.4; install the free Tapir add-on in Archicad.) | no |
| `archicad_get_design_option_combinations` | Retrieves information about existing design option combinations. (Tapir add-on command GetDesignOptionCombinations, since Tapir 1.2.9; install the free Tapir add-on in Archicad.) | no |
| `archicad_get_design_option_for_elements` | Retrieves the design option association for the specified elements. Available from Archicad 29. (Tapir add-on command GetDesignOptionForElements, since Tapir 1.5.1; install the free Tapir add-on in Archicad.) | no |
| `archicad_get_design_option_sets` | Retrieves information about existing design option sets. Available from Archicad 29. (Tapir add-on command GetDesignOptionSets, since Tapir 1.2.9; install the free Tapir add-on in Archicad.) | no |
| `archicad_get_design_options` | Retrieves information about existing design options. Available from Archicad 29. (Tapir add-on command GetDesignOptions, since Tapir 1.2.9; install the free Tapir add-on in Archicad.) | no |
| `archicad_get_detail_navigator_items` | Returns the details of the detail navigator items identified by their Ids. (Graphisoft JSON API API.GetDetailNavigatorItems, Archicad 26+.) | no |
| `archicad_get_details_of_classification_items` | Returns the details of classification items. (Graphisoft JSON API API.GetDetailsOfClassificationItems, Archicad 24+.) | no |
| `archicad_get_details_of_elements` | Gets the details of the given elements (geometry parameters etc). Use the optional fields parameter to return only the fields you need and skip the computation of the others (for example floorPlanPolygons). (Tapir add-on command GetDetailsOfElements, since Tapir 1.5.7; install the free Tapir add-on in Archicad.) | no |
| `archicad_get_details_of_properties` | Returns the details of property definitions. (Graphisoft JSON API API.GetDetailsOfProperties, Archicad 24+.) | no |
| `archicad_get_dimension_data` | Gets witness point data (coordinates, measured values) from existing dimension chains. (Tapir add-on command GetDimensionData, since Tapir 1.5.0; install the free Tapir add-on in Archicad.) | no |
| `archicad_get_document_3d_navigator_items` | Returns the details of the 3D document navigator items identified by their Ids. (Graphisoft JSON API API.GetDocument3DNavigatorItems, Archicad 26+.) | no |
| `archicad_get_document_revisions` | Retrieves all document revisions. (Tapir add-on command GetDocumentRevisions, since Tapir 1.1.9; install the free Tapir add-on in Archicad.) | no |
| `archicad_get_electrical_circuits` | Gets the given electrical circuits, the circuits of the given panels, or every circuit of the project: name, description, owner panel, connected devices and panels, voltage, phases, wiring type, loads, voltage drop and estimated cable length. Available from Archicad 30. (Tapir add-on command GetElectricalCircuits, since Tapir 1.7.2; install the free Tapir add-on in Archicad.) | no |
| `archicad_get_electrical_devices` | Gets the electrical devices (lights, sockets and other consumers) of the project or the given ones: parent circuit, placement, apparent power, load classification, demand load and directly connected MEP elements. Available from Archicad 30. (Tapir add-on command GetElectricalDevices, since Tapir 1.7.2; install the free Tapir add-on in Archicad.) | no |
| `archicad_get_electrical_panels` | Gets the electrical panels (distribution boards) of the project or the given ones: name, circuit naming, circuits, parent circuit, placement and the connected and demand loads in total and per phase. Available from Archicad 30. (Tapir add-on command GetElectricalPanels, since Tapir 1.7.2; install the free Tapir add-on in Archicad.) | no |
| `archicad_get_electrical_project_settings` | Gets the electrical project settings: the default voltage, the wiring types with their cross-sectional area and the load classifications with their demand factor. Available from Archicad 30. (Tapir add-on command GetElectricalProjectSettings, since Tapir 1.7.2; install the free Tapir add-on in Archicad.) | no |
| `archicad_get_element_owner` | Get the owner user of an element identified by its GUID. The response contains the owner user information. (Graphisoft JSON API API.GetElementOwner, Archicad 30+.) | no |
| `archicad_get_element_preview_image` | Returns the preview image of the given element. (Tapir add-on command GetElementPreviewImage, since Tapir 1.2.7; install the free Tapir add-on in Archicad.) | no |
| `archicad_get_element_trims` | Which roofs and shells trim each queried element, with the trim type, and which elements it trims. (Tapir add-on command GetElementTrims, since Tapir 1.5.9; install the free Tapir add-on in Archicad.) | no |
| `archicad_get_elements_attached_to_issue` | Retrieves attached elements of the specified issue, filtered by attachment type. (Tapir add-on command GetElementsAttachedToIssue, since Tapir 1.0.6; install the free Tapir add-on in Archicad.) | no |
| `archicad_get_elements_by_classification` | Returns the identifier of every element with the given classification identifier. (Graphisoft JSON API API.GetElementsByClassification, Archicad 24+.) | no |
| `archicad_get_elements_by_ifc_ids` | Retrieves the elements by the given IFC identifiers. (Tapir add-on command GetElementsByIFCIds, since Tapir 1.5.1; install the free Tapir add-on in Archicad.) | no |
| `archicad_get_elements_by_type` | Returns the identifier of every element of the given type on the plan. (Graphisoft JSON API API.GetElementsByType, Archicad 24+.) | no |
| `archicad_get_elements_of_design_options` | Retrieves the elements associated with the given design options. Available from Archicad 29. (Tapir add-on command GetElementsOfDesignOptions, since Tapir 1.5.1; install the free Tapir add-on in Archicad.) | no |
| `archicad_get_elements_of_groups` | Gets the elements directly contained by each given group. (Tapir add-on command GetElementsOfGroups, since Tapir 1.5.6; install the free Tapir add-on in Archicad.) | no |
| `archicad_get_elements_related_to_zones` | Returns related elements of the given zones. The related elements will be grouped by type. If multiple zones was given, then the order of the returned list is that of the given zones. (Graphisoft JSON API API.GetElementsRelatedToZones, Archicad 24+.) | no |
| `archicad_get_elevation_navigator_items` | Returns the detailed elevation navigator items identified by their Ids. (Graphisoft JSON API API.GetElevationNavigatorItems, Archicad 26+.) | no |
| `archicad_get_favorite_preview_image` | Returns the preview image of the given favorite. (Tapir add-on command GetFavoritePreviewImage, since Tapir 1.2.7; install the free Tapir add-on in Archicad.) | no |
| `archicad_get_favorites_by_type` | Returns a list of the names of all favorites with the given element type (Tapir add-on command GetFavoritesByType, since Tapir 1.2.2; install the free Tapir add-on in Archicad.) | no |
| `archicad_get_fill_attributes` | Returns the detailed fill attributes identified by their GUIDs. (Graphisoft JSON API API.GetFillAttributes, Archicad 25+.) | no |
| `archicad_get_fills` | Returns the details of the given Fill attributes. (Tapir add-on command GetFills, since Tapir 1.5.4; install the free Tapir add-on in Archicad.) | no |
| `archicad_get_gdl_parameters_of_elements` | Gets all the GDL parameters (name, type, value) of the given elements. (Tapir add-on command GetGDLParametersOfElements, since Tapir 1.5.7; install the free Tapir add-on in Archicad.) | no |
| `archicad_get_geo_location` | Gets the project location details. (Tapir add-on command GetGeoLocation, since Tapir 1.1.6; install the free Tapir add-on in Archicad.) | no |
| `archicad_get_graphic_override_combinations` | Gets all graphic override combinations of the project with the rules they contain. Available from Archicad 27. (Tapir add-on command GetGraphicOverrideCombinations, since Tapir 1.7.2; install the free Tapir add-on in Archicad.) | no |
| `archicad_get_graphic_override_rule_groups` | Gets all graphic override rule groups of the project with the rules they contain, empty groups included. Available from Archicad 27. (Tapir add-on command GetGraphicOverrideRuleGroups, since Tapir 1.7.2; install the free Tapir add-on in Archicad.) | no |
| `archicad_get_graphic_override_rules` | Gets all graphic override rules of the project, with their rule group, criterion and style. Available from Archicad 27. (Tapir add-on command GetGraphicOverrideRules, since Tapir 1.7.2; install the free Tapir add-on in Archicad.) | no |
| `archicad_get_graphical_override_rules` | Returns every graphic override rule with its criterion XML. Needs Archicad 27 or later. (Tapir add-on command GetGraphicalOverrideRules, since Tapir 1.7.2; install the free Tapir add-on in Archicad.) | no |
| `archicad_get_grid_element_defaults` | Returns the Grid Tool defaults: library part, size and GDL parameters of a new grid element. Needs Archicad 26 or later. (Tapir add-on command GetGridElementDefaults, since Tapir 1.7.2; install the free Tapir add-on in Archicad.) | no |
| `archicad_get_grid_elements` | Lists the grid elements (placed with the Grid Tool or created by CreateGridElements) with their position, angle, size, library part and optionally GDL parameters. Needs Archicad 26 or later. (Tapir add-on command GetGridElements, since Tapir 1.7.2; install the free Tapir add-on in Archicad.) | no |
| `archicad_get_groups_of_elements` | Gets the identifier of the group that directly contains each given element. Returns an error for elements that are not part of any group. (Tapir add-on command GetGroupsOfElements, since Tapir 1.5.6; install the free Tapir add-on in Archicad.) | no |
| `archicad_get_hotlinks` | Gets the file system locations (path) of the hotlink modules. The hotlinks can have tree hierarchy in the project. (Tapir add-on command GetHotlinks, since Tapir 0.1.0; install the free Tapir add-on in Archicad.) | no |
| `archicad_get_ifc_export_translators` | Lists the IFC export translators of the project, the preview translator first. Pass one of the names to IFCFileOperation as translatorName. (Tapir add-on command GetIFCExportTranslators, since Tapir 1.6.0; install the free Tapir add-on in Archicad.) | no |
| `archicad_get_ifc_ids_of_elements` | Retrieves the IFC identifiers of the given elements. (Tapir add-on command GetIFCIdsOfElements, since Tapir 1.5.1; install the free Tapir add-on in Archicad.) | no |
| `archicad_get_ifc_properties_of_elements` | Retrieves the IFC properties of the given elements. (Tapir add-on command GetIFCPropertiesOfElements, since Tapir 1.5.1; install the free Tapir add-on in Archicad.) | no |
| `archicad_get_ifc_type_of_elements` | Retrieves the IFC types of the given elements. (Tapir add-on command GetIFCTypeOfElements, since Tapir 1.5.1; install the free Tapir add-on in Archicad.) | no |
| `archicad_get_interior_elevation_navigator_items` | Returns the details of the interior elevation navigator items identified by their Ids. (Graphisoft JSON API API.GetInteriorElevationNavigatorItems, Archicad 26+.) | no |
| `archicad_get_issues` | Retrieves information about existing issues. (Tapir add-on command GetIssues, since Tapir 1.0.2; install the free Tapir add-on in Archicad.) | no |
| `archicad_get_keynote_auto_texts` | Retrieves the autotext tokens of the given keynote items. The tokens can be used as label text content to reference the fields of a keynote item. Available from Archicad 28. (Tapir add-on command GetKeynoteAutoTexts, since Tapir 1.5.6; install the free Tapir add-on in Archicad.) | no |
| `archicad_get_keynote_tree` | Retrieves the whole keynote folder and item hierarchy. The technical root folder is not included in the output; the top-level folders and items are returned directly. Available from Archicad 28. (Tapir add-on command GetKeynoteTree, since Tapir 1.5.6; install the free Tapir add-on in Archicad.) | no |
| `archicad_get_layer_attributes` | Returns the detailed layer attributes identified by their GUIDs. (Graphisoft JSON API API.GetLayerAttributes, Archicad 25+.) | no |
| `archicad_get_layer_combination_attributes` | Returns the detailed layer combination attributes identified by their GUIDs. (Graphisoft JSON API API.GetLayerCombinationAttributes, Archicad 25+.) | no |
| `archicad_get_layer_combinations` | Returns the details of layer combination attributes. (Tapir add-on command GetLayerCombinations, since Tapir 1.2.4; install the free Tapir add-on in Archicad.) | no |
| `archicad_get_layers` | Returns the details of the given Layer attributes. (Tapir add-on command GetLayers, since Tapir 1.5.4; install the free Tapir add-on in Archicad.) | no |
| `archicad_get_layout_custom_scheme` | Gets the Layout Info Panel custom field definitions (name and key) from Book Settings. (Tapir add-on command GetLayoutCustomScheme, since Tapir 1.1.7; install the free Tapir add-on in Archicad.) | no |
| `archicad_get_layout_settings` | Returns the parameters (settings) of the given layout. (Graphisoft JSON API API.GetLayoutSettings, Archicad 24+.) | no |
| `archicad_get_libraries` | Gets the list of loaded libraries. (Tapir add-on command GetLibraries, since Tapir 1.0.1; install the free Tapir add-on in Archicad.) | no |
| `archicad_get_line_attributes` | Returns the detailed line attributes identified by their GUIDs. (Graphisoft JSON API API.GetLineAttributes, Archicad 25+.) | no |
| `archicad_get_lines` | Returns the details of the given Line attributes. (Tapir add-on command GetLines, since Tapir 1.5.4; install the free Tapir add-on in Archicad.) | no |
| `archicad_get_lockable_object_set_status` | Returns the Teamwork reservation status of a non-element lockable object set and the users who hold it. (Tapir add-on command GetLockableObjectSetStatus, since Tapir 1.7.1; install the free Tapir add-on in Archicad.) | no |
| `archicad_get_marker_parameters_of_elements` | Gets the marker library part and the marker's GDL parameters (name, type, value) of the given Window and Door elements. The marker is the stamp drawn next to the opening on the floor plan; its parameters are separate from the opening's own GDL parameters, which GetGDLParametersOfElements returns. (Tapir add-on command GetMarkerParametersOfElements, since Tapir 1.7.1; install the free Tapir add-on in Archicad.) | no |
| `archicad_get_mep_distribution_systems` | Retrieves the MEP distribution systems of the project with their domain, MEP system attribute and member elements. Available from Archicad 28. (Tapir add-on command GetMEPDistributionSystems, since Tapir 1.5.6; install the free Tapir add-on in Archicad.) | no |
| `archicad_get_mep_elements` | Retrieves the MEP (Mechanical, Electrical, Plumbing) elements of the project, optionally filtered by type and domain. MEP elements are ordinary elements, so the generic element commands work on them as well (for example they can be deleted with the DeleteElements command). Available from Archicad 28. (Tapir add-on command GetMEPElements, since Tapir 1.5.6; install the free Tapir add-on in Archicad.) | no |
| `archicad_get_mep_ports` | Retrieves the ports of the given MEP elements including position, shape, size and connection status. Available from Archicad 28. (Tapir add-on command GetMEPPorts, since Tapir 1.5.6; install the free Tapir add-on in Archicad.) | no |
| `archicad_get_mep_preference_tables` | Gets the circular cross section preference tables (referenceId, diameter, description) and the reference set of the Piping or Ventilation domain, optionally with the elbow, branch and transition preference tables. The angle set and the transition tables are available from Archicad 29. Available from Archicad 28. (Tapir add-on command GetMEPPreferenceTables, since Tapir 1.5.7; install the free Tapir add-on in Archicad.) | no |
| `archicad_get_mep_routing_elements` | Retrieves the details of the given MEP routing elements: domain, MEP system, route polyline, segments with cross section data (shape, size, preference table and reference id) and nodes. Available from Archicad 28. (Tapir add-on command GetMEPRoutingElements, since Tapir 1.5.6; install the free Tapir add-on in Archicad.) | no |
| `archicad_get_mep_system_attributes` | Returns the detailed MEP system attributes identified by their GUIDs. (Graphisoft JSON API API.GetMEPSystemAttributes, Archicad 30+.) | no |
| `archicad_get_mep_system_groups` | Gets the MEP system groups of the project with their physical systems: name, root element and MEP system. Available from Archicad 29. (Tapir add-on command GetMEPSystemGroups, since Tapir 1.7.2; install the free Tapir add-on in Archicad.) | no |
| `archicad_get_mep_systems` | Returns the details of the given MEP System attributes (domain, pens, fill, center line type, surface and insulation surface). (Tapir add-on command GetMEPSystems, since Tapir 1.5.4; install the free Tapir add-on in Archicad.) | no |
| `archicad_get_model_view_options` | Gets all model view options (Tapir add-on command GetModelViewOptions, since Tapir 1.1.4; install the free Tapir add-on in Archicad.) | no |
| `archicad_get_navigator_item_tree` | Returns the tree of navigator items. (Graphisoft JSON API API.GetNavigatorItemTree, Archicad 24+.) | no |
| `archicad_get_navigator_items_type` | Returns all navigator item types based on the navigator item identifiers given. An error is returned for each identifier that is not found. (Graphisoft JSON API API.GetNavigatorItemsType, Archicad 26+.) | no |
| `archicad_get_operation_profile_attributes` | Returns the detailed operation profile attributes identified by their GUIDs. (Graphisoft JSON API API.GetOperationProfileAttributes, Archicad 30+.) | no |
| `archicad_get_pen_table_attributes` | Returns the detailed pen table attributes (including their pens) identified by their GUIDs. (Graphisoft JSON API API.GetPenTableAttributes, Archicad 25+.) | no |
| `archicad_get_pen_tables` | Returns the details of the given Pen Table attributes. (Tapir add-on command GetPenTables, since Tapir 1.5.4; install the free Tapir add-on in Archicad.) | no |
| `archicad_get_point_from_user` | Asks the designer to click a point in the current window and returns it. Archicad waits for the click or for Escape, and every other JSON command queues behind this one until then; the call fails when the input is cancelled. (Tapir add-on command GetPointFromUser, since Tapir 1.5.9; install the free Tapir add-on in Archicad.) | no |
| `archicad_get_product_info` | Accesses the version information from the running Archicad. (Graphisoft JSON API API.GetProductInfo, Archicad 24+.) | no |
| `archicad_get_profile_attribute_preview` | Returns the preview image of each requested profile attribute in a base64 string format. (Graphisoft JSON API API.GetProfileAttributePreview, Archicad 25+.) | no |
| `archicad_get_profile_attributes` | Returns the detailed profile attributes identified by their GUIDs. (Graphisoft JSON API API.GetProfileAttributes, Archicad 25+.) | no |
| `archicad_get_profiles` | Returns the details of the given Profile attributes. (Tapir add-on command GetProfiles, since Tapir 1.5.4; install the free Tapir add-on in Archicad.) | no |
| `archicad_get_project_info` | Retrieves information about the currently loaded project. (Tapir add-on command GetProjectInfo, since Tapir 0.1.0; install the free Tapir add-on in Archicad.) | no |
| `archicad_get_project_info_fields` | Retrieves the names and values of all project info fields. (Tapir add-on command GetProjectInfoFields, since Tapir 0.1.2; install the free Tapir add-on in Archicad.) | no |
| `archicad_get_property_definition_availability` | Returns the ids of classification items a given property definition is available for. (Graphisoft JSON API API.GetPropertyDefinitionAvailability, Archicad 26+.) | no |
| `archicad_get_property_groups` | Returns the details of property groups. (Graphisoft JSON API API.GetPropertyGroups, Archicad 26+.) | no |
| `archicad_get_property_ids` | Returns the identifiers of property definitions for the requested property names. (Graphisoft JSON API API.GetPropertyIds, Archicad 24+.) | no |
| `archicad_get_property_values_of_attributes` | Returns the property values of the attributes for the given property. (Tapir add-on command GetPropertyValuesOfAttributes, since Tapir 1.1.8; install the free Tapir add-on in Archicad.) | no |
| `archicad_get_property_values_of_element_components` | Returns the property values of the components for the given property. (Graphisoft JSON API API.GetPropertyValuesOfElementComponents, Archicad 25+.) | no |
| `archicad_get_property_values_of_elements` | Returns the property values of the elements for the given property. (Graphisoft JSON API API.GetPropertyValuesOfElements, Archicad 24+.) | no |
| `archicad_get_publisher_set_names` | Returns the names of available publisher sets. (Graphisoft JSON API API.GetPublisherSetNames, Archicad 24+.) | no |
| `archicad_get_quantities_of_elements` | Gets the quantities of the given elements calculated by Archicad (volumes, surfaces, lengths etc.), together with the volume and projected area per building material (one item per skin of composite and complex profile elements, one item for basic elements). Supported types: Wall, Column, Beam, Slab, Roof, Shell, Morph, Zone, ColumnSegment, BeamSegment. Use the optional minOpeningSize parameter to control the conditional values. (Tapir add-on command GetQuantitiesOfElements, since Tapir 1.7.2; install the free Tapir add-on in Archicad.) | no |
| `archicad_get_relations_of_elements` | Gets the type-specific relations of the given elements: endpoint and reference line connections of walls, beams and beam segments, boundary elements and boundary sections of zones, the zones on the two sides of windows, doors, skylights and curtain wall panels, and the zones connected to roofs and shells. Available from Archicad 26. (Tapir add-on command GetRelationsOfElements, since Tapir 1.5.7; install the free Tapir add-on in Archicad.) | no |
| `archicad_get_revision_changes` | Retrieves all changes. (Tapir add-on command GetRevisionChanges, since Tapir 1.1.9; install the free Tapir add-on in Archicad.) | no |
| `archicad_get_revision_changes_of_elements` | Retrieves the changes belong to the given elements. (Tapir add-on command GetRevisionChangesOfElements, since Tapir 1.1.9; install the free Tapir add-on in Archicad.) | no |
| `archicad_get_revision_issues` | Retrieves all issues. (Tapir add-on command GetRevisionIssues, since Tapir 1.1.9; install the free Tapir add-on in Archicad.) | no |
| `archicad_get_room_image` | Returns the room image of the given zone. (Tapir add-on command GetRoomImage, since Tapir 1.2.7; install the free Tapir add-on in Archicad.) | no |
| `archicad_get_script_ui_result` | Retrieves and clears the result last submitted from the Script UI palette's page (via window.ACAPI.SubmitResult), if any, and reports whether the palette is still open, so a waiting script can stop when the user closes it. (Tapir add-on command GetScriptUIResult, since Tapir 1.5.4; install the free Tapir add-on in Archicad.) | no |
| `archicad_get_section_elements` | Gets the elements drawn in the given section, elevation or interior elevation databases, each with the owner element it was generated from. This is the only command that returns raw section element identifiers - every other listing command converts them to their owner - so it is the way to obtain the sectionElementId that CreateAssociativeDimensionsOnSection requires. (Tapir add-on command GetSectionElements, since Tapir 1.5.8; install the free Tapir add-on in Archicad.) | no |
| `archicad_get_section_navigator_items` | Returns the details of the section navigator items identified by their Ids. (Graphisoft JSON API API.GetSectionNavigatorItems, Archicad 26+.) | no |
| `archicad_get_selected_elements` | Returns the identifiers of selected elements in the current plan. (Graphisoft JSON API API.GetSelectedElements, Archicad 26+.) | no |
| `archicad_get_solid_element_links` | Returns solid element operation links for each queried element, grouped by role (target or operator). (Tapir add-on command GetSolidElementLinks, since Tapir 1.5.4; install the free Tapir add-on in Archicad.) | no |
| `archicad_get_special_folders` | Retrieves the filesystem paths of the special folders of the running Archicad (preferences, cache, data, temporary, application, defaults, templates, help, embedded project library, etc.). (Tapir add-on command GetSpecialFolders, since Tapir 1.5.6; install the free Tapir add-on in Archicad.) | no |
| `archicad_get_stories` | Retrieves information about the story structure of the currently loaded project: index, floor id, name, level, height and, from Archicad 28, the IFC GlobalId of each story. (Tapir add-on command GetStories, since Tapir 1.1.5; install the free Tapir add-on in Archicad.) | no |
| `archicad_get_story_navigator_items` | Returns the details of the story navigator items identified by their Ids. (Graphisoft JSON API API.GetStoryNavigatorItems, Archicad 26+.) | no |
| `archicad_get_subelements_of_hierarchical_elements` | Gets the subelements of the given hierarchical elements. (Tapir add-on command GetSubelementsOfHierarchicalElements, since Tapir 1.0.6; install the free Tapir add-on in Archicad.) | no |
| `archicad_get_surface_attributes` | Returns the detailed surface attributes identified by their GUIDs. (Graphisoft JSON API API.GetSurfaceAttributes, Archicad 25+.) | no |
| `archicad_get_surfaces` | Returns the details of the given Surface attributes. (Tapir add-on command GetSurfaces, since Tapir 1.5.4; install the free Tapir add-on in Archicad.) | no |
| `archicad_get_suspend_groups_mode` | Gets the current state of the Suspend Groups mode. (Tapir add-on command GetSuspendGroupsMode, since Tapir 1.5.6; install the free Tapir add-on in Archicad.) | no |
| `archicad_get_types_of_elements` | Returns the types of the given elements. (Graphisoft JSON API API.GetTypesOfElements, Archicad 26+.) | no |
| `archicad_get_user_gsid` | Get the current registered User-GSID and OrganizationsID. Requires Archicad 27 or later. (Tapir add-on command GetUserGSID, since Tapir 1.5.6; install the free Tapir add-on in Archicad.) | no |
| `archicad_get_view_2d_transformations` | Get zoom and rotation of 2D views (Tapir add-on command GetView2DTransformations, since Tapir 1.1.7; install the free Tapir add-on in Archicad.) | no |
| `archicad_get_view_settings` | Gets the view settings of navigator items (Tapir add-on command GetViewSettings, since Tapir 1.1.4; install the free Tapir add-on in Archicad.) | no |
| `archicad_get_worksheet_navigator_items` | Returns the details of the worksheet navigator items identified by their Ids. (Graphisoft JSON API API.GetWorksheetNavigatorItems, Archicad 26+.) | no |
| `archicad_get_zone_boundaries` | Gets the boundaries of the given Zones (connected elements, neighbour zones, etc.). Accepts either a single zoneElementId or a list of zones. Prefer the list: the expensive boundary recalculation runs once per call, so querying many Zones in one call is much faster than calling the command once per Zone. (Tapir add-on command GetZoneBoundaries, since Tapir 1.2.3; install the free Tapir add-on in Archicad.) | no |
| `archicad_get_zone_categories` | Returns the details of the given Zone Category attributes. (Tapir add-on command GetZoneCategories, since Tapir 1.5.4; install the free Tapir add-on in Archicad.) | no |
| `archicad_get_zone_category_attributes` | Returns the detailed zone category attributes identified by their GUIDs. (Graphisoft JSON API API.GetZoneCategoryAttributes, Archicad 25+.) | no |
| `archicad_highlight_elements` | Highlights the elements given in the elements array. In case of empty elements array removes all previously set highlights. (Tapir add-on command HighlightElements, since Tapir 1.0.3; install the free Tapir add-on in Archicad.) | yes |
| `archicad_ifc_file_operation` | Executes an IFC file operation: opens or merges an IFC file, or saves the project as an IFC file. A save can name the export translator to use. (Tapir add-on command IFCFileOperation, since Tapir 1.2.6; install the free Tapir add-on in Archicad.) | yes |
| `archicad_import_classifications_xml` | Imports a Classification Manager XML export, with the given policies for systems and items that already exist. Returns the systems and items it created and removed. (Tapir add-on command ImportClassificationsXml, since Tapir 1.6.0; install the free Tapir add-on in Archicad.) | yes |
| `archicad_import_favorites` | Import Favorites from a .prefs file or folder into the current project. (Tapir add-on command ImportFavorites, since Tapir 1.5.0; install the free Tapir add-on in Archicad.) | yes |
| `archicad_import_issues_from_bcf` | Imports issues from the specified BCF file. (Tapir add-on command ImportIssuesFromBCF, since Tapir 1.0.6; install the free Tapir add-on in Archicad.) | yes |
| `archicad_import_properties_xml` | Imports a Property Manager XML export, with the given policy for names that already exist. Returns the property definitions it created and removed. (Tapir add-on command ImportPropertiesXml, since Tapir 1.6.0; install the free Tapir add-on in Archicad.) | yes |
| `archicad_info` | Running Archicad instances (port, version, build, language). Requires Archicad 24 or newer. | no |
| `archicad_is_add_on_command_available` | Checks if the command is available or not. (Graphisoft JSON API API.IsAddOnCommandAvailable, Archicad 25+.) | no |
| `archicad_is_alive` | Checks if the Archicad connection is alive. (Graphisoft JSON API API.IsAlive, Archicad 24+.) | no |
| `archicad_lock_elements` | Locks the given elements. Manual lock, not teamwork! (Tapir add-on command LockElements, since Tapir 1.5.2; install the free Tapir add-on in Archicad.) | yes |
| `archicad_modify_beams` | Modifies Beam elements based on the given parameters. (Tapir add-on command ModifyBeams, since Tapir 1.4.0; install the free Tapir add-on in Archicad.) | yes |
| `archicad_modify_columns` | Modifies Column elements based on the given parameters. (Tapir add-on command ModifyColumns, since Tapir 1.4.0; install the free Tapir add-on in Archicad.) | yes |
| `archicad_modify_doors` | Modifies Door elements based on the given parameters. (Tapir add-on command ModifyDoors, since Tapir 1.4.0; install the free Tapir add-on in Archicad.) | yes |
| `archicad_modify_electrical_circuits` | Modifies the description, voltage, wiring type and used phases of electrical circuits. Available from Archicad 30. (Tapir add-on command ModifyElectricalCircuits, since Tapir 1.7.2; install the free Tapir add-on in Archicad.) | yes |
| `archicad_modify_electrical_devices` | Modifies the apparent power, load classification and placement (anchor point, orientation, rotation, mirroring, library part, size) of electrical devices. Available from Archicad 30. (Tapir add-on command ModifyElectricalDevices, since Tapir 1.7.2; install the free Tapir add-on in Archicad.) | yes |
| `archicad_modify_electrical_panels` | Modifies the name, circuit naming and placement (anchor point, orientation, rotation, mirroring, library part, size) of electrical panels. Available from Archicad 30. (Tapir add-on command ModifyElectricalPanels, since Tapir 1.7.2; install the free Tapir add-on in Archicad.) | yes |
| `archicad_modify_electrical_project_settings` | Modifies the electrical project settings: sets the default voltage and adds or removes wiring types and load classifications. Available from Archicad 30. (Tapir add-on command ModifyElectricalProjectSettings, since Tapir 1.7.2; install the free Tapir add-on in Archicad.) | yes |
| `archicad_modify_graphic_override_rules` | Modifies the name, criterion or style of graphic override rules. The criterion is given as Archicad's criterion XML or as structured conditions. A given style replaces the whole style of the rule. Available from Archicad 27. (Tapir add-on command ModifyGraphicOverrideRules, since Tapir 1.7.2; install the free Tapir add-on in Archicad.) | yes |
| `archicad_modify_keynote_folders` | Modifies the key, title or reference of the given keynote folders. Available from Archicad 28. (Tapir add-on command ModifyKeynoteFolders, since Tapir 1.5.6; install the free Tapir add-on in Archicad.) | yes |
| `archicad_modify_keynote_items` | Modifies the key, title, description or reference of the given keynote items. Available from Archicad 28. (Tapir add-on command ModifyKeynoteItems, since Tapir 1.5.6; install the free Tapir add-on in Archicad.) | yes |
| `archicad_modify_labels` | Modifies Label elements based on the given parameters. (Tapir add-on command ModifyLabels, since Tapir 1.5.9; install the free Tapir add-on in Archicad.) | yes |
| `archicad_modify_lamps` | Modifies Lamp elements based on the given parameters. (Tapir add-on command ModifyLamps, since Tapir 1.5.7; install the free Tapir add-on in Archicad.) | yes |
| `archicad_modify_mep_routing_elements` | Modifies the given MEP routing elements: MEP system, cross section data (shape, size, preference table and reference id) of all segments and node positions. Available from Archicad 28. (Tapir add-on command ModifyMEPRoutingElements, since Tapir 1.5.6; install the free Tapir add-on in Archicad.) | yes |
| `archicad_modify_meshes` | Modifies the attributes of Mesh elements based on the given parameters. (Tapir add-on command ModifyMeshes, since Tapir 1.5.4; install the free Tapir add-on in Archicad.) | yes |
| `archicad_modify_morphs` | Modifies Morph elements based on the given parameters. (Tapir add-on command ModifyMorphs, since Tapir 1.4.0; install the free Tapir add-on in Archicad.) | yes |
| `archicad_modify_objects` | Modifies Object elements based on the given parameters. (Tapir add-on command ModifyObjects, since Tapir 1.5.7; install the free Tapir add-on in Archicad.) | yes |
| `archicad_modify_roofs` | Modifies multi-plane Roof elements based on the given parameters. (Tapir add-on command ModifyRoofs, since Tapir 1.4.0; install the free Tapir add-on in Archicad.) | yes |
| `archicad_modify_slabs` | Modifies Slab elements based on the given parameters. (Tapir add-on command ModifySlabs, since Tapir 1.4.0; install the free Tapir add-on in Archicad.) | yes |
| `archicad_modify_texts` | Modifies standalone Text elements based on the given parameters. (Tapir add-on command ModifyTexts, since Tapir 1.5.9; install the free Tapir add-on in Archicad.) | yes |
| `archicad_modify_walls` | Modifies Wall elements based on the given parameters. (Tapir add-on command ModifyWalls, since Tapir 1.4.0; install the free Tapir add-on in Archicad.) | yes |
| `archicad_modify_windows` | Modifies Window elements based on the given parameters. (Tapir add-on command ModifyWindows, since Tapir 1.4.0; install the free Tapir add-on in Archicad.) | yes |
| `archicad_move` | Move (or copy) elements by a vector in metres. | yes |
| `archicad_move_attributes_and_folders` | Moves attributes and attribute folders. (Graphisoft JSON API API.MoveAttributesAndFolders, Archicad 26+.) | yes |
| `archicad_move_design_options_to_another_set` | Moves the given design options to another sets. Available from Archicad 29. (Tapir add-on command MoveDesignOptionsToAnotherSet, since Tapir 1.5.1; install the free Tapir add-on in Archicad.) | yes |
| `archicad_move_elements` | Moves elements with a given vector. An element that cannot be edited (its layer is hidden or locked, it is locked, or it is not available in teamwork) gets a failed execution result naming the reason instead of being skipped silently. (Tapir add-on command MoveElements, since Tapir 1.0.2; install the free Tapir add-on in Archicad.) | yes |
| `archicad_move_elements_to_design_options` | Moves the given elements into the given design options. Use NULLGuid for design option to remove the element from any design options and move it to the main model. Available from Archicad 29. (Tapir add-on command MoveElementsToDesignOptions, since Tapir 1.5.1; install the free Tapir add-on in Archicad.) | yes |
| `archicad_move_navigator_item` | Moves the given navigator item under the <i>parentNavigatorItemId</i> in the navigator tree. If <i>previousNavigatorItemId</i> is not given then inserts it at the first place under the new parent. If it is given then inserts it after this navigator item. (Graphisoft JSON API API.MoveNavigatorItem, Archicad 24+.) | yes |
| `archicad_navigator` | Navigator tree (ProjectMap, ViewMap, LayoutBook, PublisherSets) and publisher set names. | no |
| `archicad_open_project` | Opens the given project. (Tapir add-on command OpenProject, since Tapir 1.0.7; install the free Tapir add-on in Archicad.) | yes |
| `archicad_print_view` | Prints from the current view. (Tapir add-on command PrintView, since Tapir 1.3.1; install the free Tapir add-on in Archicad.) | yes |
| `archicad_project` | Project info fields and story levels via Tapir. | no |
| `archicad_properties` | Find property ids (built-in nonLocalizedName or user-defined group/name), read property values of elements, or write values. | yes |
| `archicad_publish` | Publish a publisher set (PDF / DWG / IFC / BIMx outputs as configured in Archicad), optionally into a folder. | yes |
| `archicad_publish_publisher_set` | Performs a publish operation on the currently opened project. Only the given publisher set will be published. (Tapir add-on command PublishPublisherSet, since Tapir 0.1.0; install the free Tapir add-on in Archicad.) | yes |
| `archicad_quit_archicad` | Performs a quit operation on the currently running Archicad instance. (Tapir add-on command QuitArchicad, since Tapir 0.1.0; install the free Tapir add-on in Archicad.) | yes |
| `archicad_rebuild_view` | Rebuilds the current view. (Tapir add-on command RebuildView, since Tapir 1.5.0; install the free Tapir add-on in Archicad.) | yes |
| `archicad_release_elements` | Releases elements in Teamwork mode. (Tapir add-on command ReleaseElements, since Tapir 1.1.4; install the free Tapir add-on in Archicad.) | yes |
| `archicad_release_hotlink_cache_management` | Releases the Hotlink and XRef management in Teamwork mode. (Tapir add-on command ReleaseHotlinkCacheManagement, since Tapir 1.7.1; install the free Tapir add-on in Archicad.) | yes |
| `archicad_release_lockable_object_set` | Releases a non-element lockable object set reserved with ReserveLockableObjectSet in Teamwork mode. (Tapir add-on command ReleaseLockableObjectSet, since Tapir 1.7.1; install the free Tapir add-on in Archicad.) | yes |
| `archicad_reload_libraries` | Executes the reload libraries command. (Tapir add-on command ReloadLibraries, since Tapir 1.0.0; install the free Tapir add-on in Archicad.) | yes |
| `archicad_remove_element_notification_client` | Removes an element notification client. (Tapir add-on command RemoveElementNotificationClient, since Tapir 1.2.8; install the free Tapir add-on in Archicad.) | yes |
| `archicad_remove_element_trims` | Removes the trim between an element and the roof or shell trimming it. (Tapir add-on command RemoveElementTrims, since Tapir 1.5.9; install the free Tapir add-on in Archicad.) | yes |
| `archicad_remove_solid_element_links` | Removes solid element operation links between target and operator elements. (Tapir add-on command RemoveSolidElementLinks, since Tapir 1.5.4; install the free Tapir add-on in Archicad.) | yes |
| `archicad_rename_attribute_folder` | Rename attribute folder. (Graphisoft JSON API API.RenameAttributeFolder, Archicad 26+.) | yes |
| `archicad_rename_attribute_folders` | Rename attribute folder. (Graphisoft JSON API API.RenameAttributeFolders, Archicad 27+.) | yes |
| `archicad_rename_design_option_combinations` | Changes the name of the given design option combinations. Available from Archicad 29. (Tapir add-on command RenameDesignOptionCombinations, since Tapir 1.7.1; install the free Tapir add-on in Archicad.) | yes |
| `archicad_rename_design_option_sets` | Changes the name of the given design option sets. Available from Archicad 29. (Tapir add-on command RenameDesignOptionSets, since Tapir 1.7.1; install the free Tapir add-on in Archicad.) | yes |
| `archicad_rename_design_options` | Changes the name and/or the string id of the given design options. Available from Archicad 29. (Tapir add-on command RenameDesignOptions, since Tapir 1.7.1; install the free Tapir add-on in Archicad.) | yes |
| `archicad_rename_favorites` | Rename existing favorites. (Tapir add-on command RenameFavorites, since Tapir 1.5.4; install the free Tapir add-on in Archicad.) | yes |
| `archicad_rename_navigator_item` | Renames an existing navigator item by specifying either the name or the ID, or both. (Graphisoft JSON API API.RenameNavigatorItem, Archicad 24+.) | yes |
| `archicad_reserve_elements` | Reserves elements in Teamwork mode. (Tapir add-on command ReserveElements, since Tapir 1.1.4; install the free Tapir add-on in Archicad.) | yes |
| `archicad_reserve_hotlink_cache_management` | Reserves the Hotlink and XRef management in Teamwork mode, which is needed before changing hotlink nodes. (Tapir add-on command ReserveHotlinkCacheManagement, since Tapir 1.7.1; install the free Tapir add-on in Archicad.) | yes |
| `archicad_reserve_lockable_object_set` | Reserves a non-element lockable object set (attributes, favorites, project info, project preferences, etc.) in Teamwork mode. The object set is given by its Archicad API name, e.g. 'Composites', 'LayerSettingsDialog' or 'ProjectInfo'. (Tapir add-on command ReserveLockableObjectSet, since Tapir 1.7.1; install the free Tapir add-on in Archicad.) | yes |
| `archicad_rotate_elements` | Rotates elements around a reference point. An element that cannot be edited (its layer is hidden or locked, it is locked, or it is not available in teamwork) gets a failed execution result naming the reason instead of being skipped silently. (Tapir add-on command RotateElements, since Tapir 1.5.3; install the free Tapir add-on in Archicad.) | yes |
| `archicad_save_as_module_file` | Saves the given elements, or the current selection, as a hotlink module (.mod) file. (Tapir add-on command SaveAsModuleFile, since Tapir 1.5.9; install the free Tapir add-on in Archicad.) | yes |
| `archicad_save_current_window_as_image` | Saves what the current window shows as an image file (png, jpg, bmp, tiff or gif), as File > Save as does with a picture format. (Tapir add-on command SaveCurrentWindowAsImage, since Tapir 1.7.1; install the free Tapir add-on in Archicad.) | yes |
| `archicad_save_project` | Saves the currently opened project. (Tapir add-on command SaveProject, since Tapir 1.3.1; install the free Tapir add-on in Archicad.) | yes |
| `archicad_save_project_as_archive` | Saves the open project as an archive (.pla) file, with the library parts it uses inside. (Tapir add-on command SaveProjectAsArchive, since Tapir 1.6.0; install the free Tapir add-on in Archicad.) | yes |
| `archicad_selection` | Get the selected elements, change the selection or highlight elements in Archicad. | yes |
| `archicad_set_3d_cut_planes` | Sets the 3D cut planes. (Tapir add-on command Set3DCutPlanes, since Tapir 1.3.1; install the free Tapir add-on in Archicad.) | yes |
| `archicad_set_3d_projection_settings` | Sets a perspective projection for the 3D window. Every perspective parameter is required: read a known good set with Get3DProjectionSettings and pass it back, with the values to change replaced. (Tapir add-on command Set3DProjectionSettings, since Tapir 1.7.1; install the free Tapir add-on in Archicad.) | yes |
| `archicad_set_active_design_options_in_combinations` | Sets active design options in the given combinations. Available from Archicad 29. (Tapir add-on command SetActiveDesignOptionsInCombinations, since Tapir 1.5.1; install the free Tapir add-on in Archicad.) | yes |
| `archicad_set_active_graphical_override_combination` | Applies a graphic override combination, given by id or by name, to the current window. The current window must be a model window (floor plan, section, elevation, 3D, ...). To remove the overrides, apply the project's 'No Overrides' combination. The saved view is not changed (see SetViewSettings for that). Available from Archicad 30. (Tapir add-on command SetActiveGraphicalOverrideCombination, since Tapir 1.7.2; install the free Tapir add-on in Archicad.) | yes |
| `archicad_set_classifications_of_elements` | Sets the classifications of elements. In order to set the classification of an element to unclassified, omit the classificationItemId field. (Graphisoft JSON API API.SetClassificationsOfElements, Archicad 24+.) | yes |
| `archicad_set_details_of_elements` | Sets the details of the given elements (floor, layer, order etc). (Tapir add-on command SetDetailsOfElements, since Tapir 1.0.7; install the free Tapir add-on in Archicad.) | yes |
| `archicad_set_element_notification_client` | Sets up a new notification client to receive element events. (Tapir add-on command SetElementNotificationClient, since Tapir 1.2.8; install the free Tapir add-on in Archicad.) | yes |
| `archicad_set_gdl_parameters_of_elements` | Sets the given GDL parameters of the given elements. (Tapir add-on command SetGDLParametersOfElements, since Tapir 1.5.7; install the free Tapir add-on in Archicad.) | yes |
| `archicad_set_geo_location` | Sets the project location details. (Tapir add-on command SetGeoLocation, since Tapir 1.2.9; install the free Tapir add-on in Archicad.) | yes |
| `archicad_set_ifc_properties_of_elements` | Sets local IFC properties on elements by creating or modifying single value (IfcPropertySingleValue) properties in the given property sets. Available only in Archicad 25, 26 and 27 for now. (Tapir add-on command SetIFCPropertiesOfElements, since Tapir 1.7.0; install the free Tapir add-on in Archicad.) | yes |
| `archicad_set_layout_settings` | Sets the parameters (settings) of the given layout. (Graphisoft JSON API API.SetLayoutSettings, Archicad 24+.) | yes |
| `archicad_set_libraries` | Makes the given folders the project's local libraries; built-in, embedded, server and web libraries are kept. Set the libraries before opening a file that needs them and the missing-library dialog does not appear. (Tapir add-on command SetLibraries, since Tapir 1.5.9; install the free Tapir add-on in Archicad.) | yes |
| `archicad_set_marker_parameters_of_elements` | Sets the given GDL parameters of the marker of the given Window and Door elements. The opening's own settings and the other marker parameters are left as they are. (Tapir add-on command SetMarkerParametersOfElements, since Tapir 1.7.1; install the free Tapir add-on in Archicad.) | yes |
| `archicad_set_project_info_field` | Sets the value of a project info field. (Tapir add-on command SetProjectInfoField, since Tapir 0.1.2; install the free Tapir add-on in Archicad.) | yes |
| `archicad_set_property_values_of_attributes` | Sets the property values of attributes. (Tapir add-on command SetPropertyValuesOfAttributes, since Tapir 1.1.8; install the free Tapir add-on in Archicad.) | yes |
| `archicad_set_property_values_of_elements` | Sets the property values of elements. (Graphisoft JSON API API.SetPropertyValuesOfElements, Archicad 24+.) | yes |
| `archicad_set_stories` | Sets the story sructure of the currently loaded project. (Tapir add-on command SetStories, since Tapir 1.1.5; install the free Tapir add-on in Archicad.) | yes |
| `archicad_set_suspend_groups_mode` | Turns the Suspend Groups mode on or off. Suspend groups to perform operations on elements that are part of a group; remember to restore the previous state afterwards. (Tapir add-on command SetSuspendGroupsMode, since Tapir 1.5.6; install the free Tapir add-on in Archicad.) | yes |
| `archicad_set_view_rotation` | Set the rotation angle of 2D views via their floor plan database. (Tapir add-on command SetViewRotation, since Tapir 1.1.7; install the free Tapir add-on in Archicad.) | yes |
| `archicad_set_view_settings` | Sets the view settings of navigator items (Tapir add-on command SetViewSettings, since Tapir 1.1.4; install the free Tapir add-on in Archicad.) | yes |
| `archicad_show_alert` | Display a dialog with up to three buttons. (Tapir add-on command ShowAlert, since Tapir 1.5.6; install the free Tapir add-on in Archicad.) | yes |
| `archicad_show_script_ui` | Shows the given HTML content in a native Archicad palette (a tkinter alternative for Python scripts). (Tapir add-on command ShowScriptUI, since Tapir 1.5.4; install the free Tapir add-on in Archicad.) | yes |
| `archicad_tapir` | Run a Tapir add-on command (GetProjectInfo, GetStories, GetSelectedElements, GetDetailsOfElements, CreateColumns, CreateSlabs, CreateObjects, MoveElements, HighlightElements, SetGDLParametersOfElements, TeamworkReceive, ...). Needs the free Tapir add-on. | yes |
| `archicad_tapir_clone_project_map_item_to_view_map` | Clones Project Map viewpoints into a specified View Map folder. (Tapir add-on command CloneProjectMapItemToViewMap, since Tapir 1.1.7; install the free Tapir add-on in Archicad.) | yes |
| `archicad_tapir_create_layout` | Creates Layouts and their backing master layouts. (Tapir add-on command CreateLayout, since Tapir 1.4.0; install the free Tapir add-on in Archicad.) | yes |
| `archicad_tapir_create_layout_subset` | Creates Layout Book subsets. (Tapir add-on command CreateLayoutSubset, since Tapir 1.4.0; install the free Tapir add-on in Archicad.) | yes |
| `archicad_tapir_create_view_map_folder` | Creates a new folder in the View Map. (Tapir add-on command CreateViewMapFolder, since Tapir 1.1.7; install the free Tapir add-on in Archicad.) | yes |
| `archicad_tapir_delete_attributes` | Deletes the given attributes. (Tapir add-on command DeleteAttributes, since Tapir 1.5.4; install the free Tapir add-on in Archicad.) | yes |
| `archicad_tapir_delete_navigator_items` | Deletes navigator items from the navigator tree. (Tapir add-on command DeleteNavigatorItems, since Tapir 1.1.7; install the free Tapir add-on in Archicad.) | yes |
| `archicad_tapir_get_3d_bounding_boxes` | Get the 3D bounding box of elements. The bounding box is calculated from the global origin in the 3D view. The output is the array of the bounding boxes respective to the input array of elements. (Tapir add-on command Get3DBoundingBoxes, since Tapir 1.1.2; install the free Tapir add-on in Archicad.) | no |
| `archicad_tapir_get_all_elements` | Returns the identifier of all elements on the plan. Use the optional filter parameter for filtering. (Tapir add-on command GetAllElements, since Tapir 1.0.7; install the free Tapir add-on in Archicad.) | no |
| `archicad_tapir_get_attributes_by_type` | Returns the details of every attribute of the given type. (Tapir add-on command GetAttributesByType, since Tapir 1.1.3; install the free Tapir add-on in Archicad.) | no |
| `archicad_tapir_get_classifications_of_elements` | Returns the classification of the given elements in the given classification systems. It works for subelements of hierarchal elements also. (Tapir add-on command GetClassificationsOfElements, since Tapir 1.0.7; install the free Tapir add-on in Archicad.) | no |
| `archicad_tapir_get_elements_by_type` | Returns the identifier of every element of the given type on the plan. It works for any type. Use the optional filter parameter for filtering. (Tapir add-on command GetElementsByType, since Tapir 1.0.7; install the free Tapir add-on in Archicad.) | no |
| `archicad_tapir_get_layout_settings` | Gets settings of layouts, including Layout Info Panel custom data fields. (Tapir add-on command GetLayoutSettings, since Tapir 1.1.7; install the free Tapir add-on in Archicad.) | no |
| `archicad_tapir_get_navigator_item_tree` | Returns the full navigator item tree for the specified map. (Tapir add-on command GetNavigatorItemTree, since Tapir 1.1.7; install the free Tapir add-on in Archicad.) | no |
| `archicad_tapir_get_property_values_of_elements` | Returns the property values of the elements for the given property. It works for subelements of hierarchal elements also. (Tapir add-on command GetPropertyValuesOfElements, since Tapir 1.0.6; install the free Tapir add-on in Archicad.) | no |
| `archicad_tapir_get_selected_elements` | Gets the list of the currently selected elements. (Tapir add-on command GetSelectedElements, since Tapir 0.1.0; install the free Tapir add-on in Archicad.) | no |
| `archicad_tapir_move_navigator_item` | Moves a navigator item to a new parent in the navigator tree. (Tapir add-on command MoveNavigatorItem, since Tapir 1.1.7; install the free Tapir add-on in Archicad.) | yes |
| `archicad_tapir_rename_navigator_item` | Renames a navigator item or changes its ID. Fails when Archicad does not apply the new name (e.g. on Project Map schedule items, which are named by their scheme). With customName false the item drops its custom name and follows its Project Map source again. (Tapir add-on command RenameNavigatorItem, since Tapir 1.1.7; install the free Tapir add-on in Archicad.) | yes |
| `archicad_tapir_set_classifications_of_elements` | Sets the classifications of elements. In order to set the classification of an element to unclassified, omit the classificationItemId field. It works for subelements of hierarchal elements also. (Tapir add-on command SetClassificationsOfElements, since Tapir 1.0.7; install the free Tapir add-on in Archicad.) | yes |
| `archicad_tapir_set_layout_settings` | Sets settings of layouts, including Layout Info Panel custom data fields. (Tapir add-on command SetLayoutSettings, since Tapir 1.1.7; install the free Tapir add-on in Archicad.) | yes |
| `archicad_tapir_set_property_values_of_elements` | Sets the property values of elements. It works for subelements of hierarchal elements also. (Tapir add-on command SetPropertyValuesOfElements, since Tapir 1.0.6; install the free Tapir add-on in Archicad.) | yes |
| `archicad_teamwork_receive` | Performs a receive operation on the currently opened Teamwork project. (Tapir add-on command TeamworkReceive, since Tapir 0.1.0; install the free Tapir add-on in Archicad.) | yes |
| `archicad_teamwork_send` | Performs a send operation on the currently opened Teamwork project. (Tapir add-on command TeamworkSend, since Tapir 0.1.0; install the free Tapir add-on in Archicad.) | yes |
| `archicad_trim_elements` | Trims construction elements with a roof or shell: the roofs and shells in the list, or one given trimming element with a trim type. (Tapir add-on command TrimElements, since Tapir 1.5.9; install the free Tapir add-on in Archicad.) | yes |
| `archicad_unlock_elements` | Unlocks the given elements. Manual lock, not teamwork! (Tapir add-on command UnlockElements, since Tapir 1.5.2; install the free Tapir add-on in Archicad.) | yes |
| `archicad_update_classification_items` | Updates the id (code), name and/or description of existing Classification Items, keeping their guid and so the elements classified with them. Items cannot be moved to another parent. (Tapir add-on command UpdateClassificationItems, since Tapir 1.6.0; install the free Tapir add-on in Archicad.) | yes |
| `archicad_update_classification_systems` | Updates the name, description, source, version and/or date of existing Classification Systems, keeping their guid. (Tapir add-on command UpdateClassificationSystems, since Tapir 1.6.0; install the free Tapir add-on in Archicad.) | yes |
| `archicad_update_drawings` | Performs a drawing update on the given elements. (Tapir add-on command UpdateDrawings, since Tapir 1.1.4; install the free Tapir add-on in Archicad.) | yes |
| `archicad_update_favorites_from_elements` | Update existing favorites from the given elements. (Tapir add-on command UpdateFavoritesFromElements, since Tapir 1.5.4; install the free Tapir add-on in Archicad.) | yes |
| `archicad_update_hotlinks` | Re-reads the source files of hotlink module nodes and refreshes their cached content in the project, as the Hotlink Manager's Update button does. (Tapir add-on command UpdateHotlinks, since Tapir 1.7.1; install the free Tapir add-on in Archicad.) | yes |
| `archicad_update_property_definitions` | Updates existing Custom Property Definitions in place, keeping their guid: name, description, group, default value or expressions, availability, and enum options (add, rename, remove, reorder). (Tapir add-on command UpdatePropertyDefinitions, since Tapir 1.5.4; install the free Tapir add-on in Archicad.) | yes |
| `archicad_update_property_groups` | Updates the name and/or description of existing Custom Property Groups, keeping their guid. (Tapir add-on command UpdatePropertyGroups, since Tapir 1.6.0; install the free Tapir add-on in Archicad.) | yes |
| `archicad_update_zones` | Updates all Zones (recalculates their geometry, updates their Zone Stamps and the connected elements). (Tapir add-on command UpdateZones, since Tapir 1.5.4; install the free Tapir add-on in Archicad.) | yes |

### autocad

| Tool | What it does | Changes model |
|---|---|---|
| `autocad_add` | Add line, polyline (closed optional, with width), circle, arc, ellipse, point, spline, 3D polyline, text, mtext, revision-cloud-like closed polyline. Coordinates in drawing units. | yes |
| `autocad_add_dimension` | Aligned, rotated (linear), radial, diametric or angular dimension with the current dimension style (or a named one). | yes |
| `autocad_add_hatch` | Hatch closed polylines / circles (handles) with a pattern (ANSI31, SOLID, AR-CONC, ...), scale and angle. | yes |
| `autocad_add_solid` | Box, cylinder, cone, sphere, wedge, torus, or extrude / revolve a closed polyline (handle) into a solid. | yes |
| `autocad_add_table` | Insert a table from rows of text (first row = header) at a point. | yes |
| `autocad_array` | Array entities: rectangular (rows, columns, spacing) or polar (count, angle, centre). | yes |
| `autocad_attach_image` | Attach a raster image (jpg, png, tif) at a point with scale and rotation. | yes |
| `autocad_block_attributes_set` | Set attribute values {TAG: value} on block references (handles), or on every reference of a block name. | yes |
| `autocad_block_define` | Create a block definition from entities (copied), with a base point; optionally replace the entities with one block reference. | yes |
| `autocad_blocks` | Blocks with insert counts. | no |
| `autocad_blocks_report` | Every block reference (optionally of one block) with position, rotation, scale, layer and attribute values - the classic attribute extraction (also for Plant 3D / Electrical symbols). | no |
| `autocad_command` | Send ANY command-line input to AutoCAD or a vertical (Civil 3D, Plant 3D, Map 3D, MEP, Advance Steel, LISP commands, add-ins). Use '_.' prefixes and spaces/newlines as Enter: '_.LINE 0,0 1000,0 ', '_.-LAYER _M WALLS ', '_.ZOOM _E '. Queued; runs when AutoCAD is idle. | yes |
| `autocad_document` | list \| open \| new \| save \| saveas \| close. | yes |
| `autocad_draw` | Create line, polyline, rectangle, circle, arc, text, mtext or block insert (with attributes). Batch with items[]. Stamped as AI-created (xdata BURAKBIM). | yes |
| `autocad_drawing_cleanup` | Purge all unused named objects (repeat for nested), audit and fix errors, regenerate, and zoom extents - the standard drawing clean-up before issue. | yes |
| `autocad_drawing_file` | Drawing file operations. saveAs takes a version: 2018 (default), 2013, 2010, 2007, 2004, 2000, and format dwg or dxf. | yes |
| `autocad_drawing_properties` | Read or set drawing properties: title, subject, author, keywords, comments and custom properties - used by title blocks and sheet sets. | yes |
| `autocad_drawings` | All open drawings (name, path, saved state, active) and the AutoCAD product/version - works for Civil 3D, Plant 3D and every vertical. | no |
| `autocad_entity_info` | Type, layer, colour, bounding box, length / area / radius / text / block name of entities. | no |
| `autocad_entity_set` | Set layer, ACI colour (1-255, 256 = ByLayer) or RGB, linetype, lineweight (mm x 100, -1 ByLayer), linetype scale, visibility. | yes |
| `autocad_export` | Export the drawing or selected entities: wblock to a new DWG, or export WMF, SAT (ACIS), EPS, BMP, STL (solids), DXX. | yes |
| `autocad_groups` | List groups with their entity count, create a group from entities, or explode (delete) a group. | yes |
| `autocad_info` | Product (AutoCAD, Civil 3D, Plant 3D, Advance Steel...), open drawings, units. | no |
| `autocad_insert_block` | Insert a block definition or an external DWG file as a block at a point with scale, rotation (deg) and attribute values {TAG: value}. | yes |
| `autocad_layer_report` | Every layer with state (on, frozen, locked, plot), colour, linetype, lineweight and the number of entities on it - finds empty and non-standard layers. | no |
| `autocad_layer_standard` | Create / update many layers at once from a list [{name, color, linetype, lineweight, plot, description}] - e.g. the company CAD standard. Missing linetypes are loaded from acad.lin. | yes |
| `autocad_layers` | List layers; with name: create/modify (color, on, frozen, locked, current). | yes |
| `autocad_layouts` | List layouts (tab order, paper size, plot device, plot style), create, rename, delete, copy or activate a layout. | yes |
| `autocad_lisp` | Evaluate an AutoLISP expression in the running AutoCAD / Civil 3D (via COM) and return its printed value - synchronous. Works even without the bridge. e.g. (getvar "DWGNAME"), (sslength (ssget "X" '((0 . "LINE")))). | yes |
| `autocad_measure` | Distance between two points, polyline area & perimeter by handle, or the angle (deg) of a line from p1 to p2. | no |
| `autocad_modify` | Change layer/color, move by [dx,dy,dz] or delete entities by handle. | yes |
| `autocad_named_views` | List named views and UCSs, save the current view under a name, or restore a named view. | yes |
| `autocad_offset_explode` | Offset curves by a distance (negative = other side), explode blocks/polylines, or delete entities. | yes |
| `autocad_plot_pdf` | Plot layouts (all paper-space layouts, or the given names) to PDF files with 'DWG To PDF.pc3' (or another device), paper size and plot style - batch issue of drawings. | yes |
| `autocad_quantities` | Take-off from the drawing: total length of lines/polylines/arcs, area of closed polylines and hatches, and block counts - grouped by layer (or block name). | no |
| `autocad_query` | Entities in model/paper space by DXF type (LINE, LWPOLYLINE, INSERT, TEXT, MTEXT, CIRCLE, AECC_ALIGNMENT...) and layer, with geometry. | no |
| `autocad_select` | Find entities by DXF type (LINE, LWPOLYLINE, INSERT, TEXT, MTEXT, CIRCLE, HATCH, DIMENSION, AECC_*, ACPP*...), layer (wildcards) and/or window, and return handles + summary. Optionally makes them the active selection. | no |
| `autocad_styles` | List text styles, dimension styles and linetypes; set the current text or dimension style; load a linetype. | yes |
| `autocad_sysvar` | Get (no value) or set a system variable. | yes |
| `autocad_text_export` | All TEXT / MTEXT contents with position, height, layer and space - for spell checks, title-block audits and translation. | no |
| `autocad_text_find_replace` | Find (and optionally replace) text in TEXT, MTEXT, attributes and table cells across model and paper space. Dry run unless apply=true. | yes |
| `autocad_transform` | Transform entities. Angles in degrees. Copy and mirror return the new handles. | yes |
| `autocad_xrefs` | List xrefs (path, status, nesting), attach a DWG as xref (attach/overlay), reload, unload, bind or detach. | yes |
| `autocad_zoom` | Zoom extents, all, window (two points), centre (point + magnification) or to entities (handles). | yes |
| `civil3d_com_alignments` | Alignments (site and siteless) with length, start / end station and their profiles (Civil 3D COM API). | no |
| `civil3d_com_points` | List COGO points (number, northing, easting, elevation, description) or add points from [[e, n, z, "desc"], ...]. | yes |
| `civil3d_com_surfaces` | TIN / grid surfaces with min / max / mean elevation, 2D and 3D area and point count (Civil 3D COM API, every version). | no |
| `civil3d_objects` | Alignments, profiles, surfaces (elevation stats), sites, pipe networks, corridors, COGO points. | no |

### com

| Tool | What it does | Changes model |
|---|---|---|
| `com_call` | Call a method on a COM object (application path or $c handle) with JSON args ($c handles allowed). | yes |
| `com_constants` | Named enum constants from a COM application's type library (e.g. Robot I_LT_SUPPORT, Inventor kPartDocumentObject, AutoCAD acActiveViewport). Use the values with com_call / com_set. | no |
| `com_get` | Read a property path from a COM application, e.g. progId=Inventor.Application path=ActiveDocument.ComponentDefinition.Parameters.UserParameters.Count. Objects return $c handles. | no |
| `com_members` | Properties and methods (with help text) of any COM object - discover Inventor / AutoCAD / Robot APIs. | no |
| `com_set` | Set a property on a COM object. | yes |

### core

| Tool | What it does | Changes model |
|---|---|---|
| `burak_audit_log` | Every AI action through BURAK BIM: time, AI client, session, tool, arguments, result, duration, errors. For BIM inspectors and managers. | no |
| `burak_config` | Show settings, or set one: readOnly (block all changes), allowScripts, allowComAutomation, allowUiAutomation, toolsets (["auto"] = tools for installed software; ["all"]; or a list: core, api, revit, scan, inspector, autocad, navisworks, inventor, recap, com, ui), inspectorName, requestTimeoutSeconds. | yes |
| `burak_guide` | Workflow guides: scan (scan-to-BIM), inspector, ribbon (every command/tab), api (full API access). | no |
| `burak_launch` | Start an installed application (optionally opening a file): revit, autocad (incl. Civil 3D etc. by name), navisworks, inventor, recap, robot. Waits until its BURAK BIM bridge is live when one exists. | yes |
| `burak_rescan` | Re-detect installed Autodesk software and build/register BURAK BIM bridges for newly installed versions (runs the installer in rescan mode, same as the logon sentinel). Software in custom folders (e.g. D:\ALL PROGRAMS) is found automatically from the registry, shortcuts and running apps; add searchPaths to also scan folders directly. | yes |
| `burak_status` | START HERE. Installed Autodesk software (2022-2027 and others), which are running, live BURAK BIM bridges, COM apps, config (read-only mode etc.). | no |

### dynamo

| Tool | What it does | Changes model |
|---|---|---|
| `dynamo_cli` | Run a .dyn with DynamoCLI (Dynamo Sandbox / Core installs) without Revit and return the output-node values. For Revit graphs use revit_dynamo_run. | yes |
| `dynamo_inspect` | Read a .dyn file: Dynamo version, inputs (name, type, value), outputs, node list (type, function, package) and required packages - so inspectors can review a script before it runs. | no |
| `dynamo_set_inputs` | Write a copy of a .dyn with new input values (by input name or node id). Run it in Revit with revit_dynamo_run, or headless with dynamo_cli. | yes |

### inspector

| Tool | What it does | Changes model |
|---|---|---|
| `autocad_ai_entities` | Entities created by AI through BURAK BIM (inspector review). | no |
| `burak_inspection_report` | Write an HTML inspection report for the open Revit model: AI-created elements by status, provenance, scan deviation, and today's audit trail. Returns the file path. | no |
| `inspector_list` | Every element created by AI through BURAK BIM, with provenance (AI client, session, tool, user, time), review status and scan deviation. | no |
| `inspector_mark` | Mark elements verified / rejected / unverified with inspector name, note and LOD (e.g. 400, 500 for field-verified as-built). | yes |
| `inspector_review_view` | Create/refresh the 'BURAK BIM - AI Review' 3D view: red unverified, green verified, orange rejected, purple out of scan tolerance. | yes |

### inventor

| Tool | What it does | Changes model |
|---|---|---|
| `inventor_bom` | Structured or parts-only BOM of the active assembly: item, quantity, part number, description, material, file. | no |
| `inventor_command` | Execute any Inventor command by internal name (e.g. PartExtrudeCmd, AppZoomallCmd, AssemblyPlaceComponentCmd). synchronous=true waits for the command to finish. | yes |
| `inventor_commands` | Every Inventor command (ControlDefinitions: ~4000 incl. add-ins) with internal name, display name and description. Filter with search. | no |
| `inventor_component_state` | Change components of the active assembly by name: suppress, unsuppress, show, hide, ground, unground or delete. | yes |
| `inventor_constraints` | Constraints and joints of the active assembly with type, suppressed state and health - finds broken / sick constraints. | no |
| `inventor_documents` | open \| new (part/assembly/drawing, optional template) \| save \| saveas/export (by extension: .ipt .iam .idw .dwg .stp .step .igs .sat .stl .pdf .dwf) \| close. | yes |
| `inventor_drawing` | In the active drawing: list sheets and views, add a sheet, or place a base view of a model file (front/top/right/iso) at a sheet position (cm) with scale. | yes |
| `inventor_features` | Features of the active part (name, type, suppressed, health) or suppress / unsuppress features by name. | yes |
| `inventor_ilogic` | Run an iLogic rule in the active document (or an external rule file path). | yes |
| `inventor_info` | Inventor version, open documents, active document type and path. | no |
| `inventor_interference` | Analyze interference between all components (or two named groups) of the active assembly; returns the clashing pairs and interference volume (cm3). | no |
| `inventor_iproperties` | Read all iProperties of the active document, or set: values {"Design Tracking Properties/Part Number": "BB-001", "Inventor Summary Information/Title": "..."}. | yes |
| `inventor_mass_properties` | Mass (kg), volume (cm3), surface area (cm2), centre of mass (cm) and bounding box of the active part or assembly. | no |
| `inventor_material` | Read the active part's material, list available materials, or assign a material by name. | yes |
| `inventor_model_states` | List model states (2022+) of the active part/assembly or activate one. | yes |
| `inventor_occurrences` | Components of the active assembly (name, file, suppressed, visible, grounded, position in cm), optionally recursive. | no |
| `inventor_parameters` | List model/user parameters of the active part/assembly, or set them: values {"d0": "50 mm", "Length": "1200 mm"}. Updates the model. | yes |
| `inventor_place_component` | Place a part / sub-assembly file into the active assembly at a position (cm), optionally grounded; repeat with count + spacing. | yes |
| `inventor_update_save_all` | Rebuild and update the active document, and optionally save every open document. | yes |
| `inventor_view_image` | Save the active Inventor view as an image (PNG / JPG / BMP) at a given size, optionally after zoom-all and isometric view. | yes |

### navisworks

| Tool | What it does | Changes model |
|---|---|---|
| `navis_add_custom_property` | Add or update a custom property tab with name/value pairs on items (COM API user-defined properties) - e.g. status, inspector, package, cost code. | no |
| `navis_appearance_profile` | Colour and/or make transparent everything matching each rule, like Navisworks' Appearance Profiler: [{"category":"Element","property":"Category","value":"Ducts","rgb":[0,120,255],"transparency":0}]. op per rule: equals (default), contains, defined. | yes |
| `navis_append_folder` | Appends all files of the given extensions in a folder (optionally recursive) - one-click federation of a discipline drop folder. | no |
| `navis_bounding_box` | Bounding box and size (model units and mm) of items or of the whole model. | no |
| `navis_camera` | Read the camera (position, look-at, projection, field of view) or set position + target (model units) and projection. | no |
| `navis_clash_assign` | Assign all active clashes of a test (optionally only those whose items come from a given model file) to a person and add a comment - batch coordination workflow. | yes |
| `navis_clash_auto_approve` | Sets every clash of a test whose penetration is smaller than a tolerance (mm) to Approved (or another status) with a comment - removes nuisance clashes from the coordination list. | no |
| `navis_clash_auto_group` | Group the results of a test by their first item's parent (e.g. one group per beam/duct) to reduce hundreds of clashes to actionable issues. | yes |
| `navis_clash_bulk_status` | Set the status (and assignee) of all results of a test that match a current status - e.g. mark all New as Active, all Approved as Resolved. | yes |
| `navis_clash_by_discipline` | Counts active clashes per pair of source model files (e.g. STR.nwc x MEP.nwc) across all tests - the classic coordination-meeting matrix. | no |
| `navis_clash_compact` | Remove resolved results from all tests (Compact) to keep the file light. | yes |
| `navis_clash_delete_tests` | Delete clash tests by name, or all tests (clear). | yes |
| `navis_clash_edit_test` | Change tolerance, type or name of an existing clash test. | yes |
| `navis_clash_group_by_level` | Report clash results of a test bucketed by a property of item 1 (e.g. Element/Level) - where are the problems. | no |
| `navis_clash_items_report` | Ranks model items (and their parent element) by how many active clashes they are part of across all tests - finds the few elements causing most clashes. | no |
| `navis_clash_matrix` | Create a hard-clash test for every pair of property values (e.g. Element/Category: Structural Framing x Ducts, Pipes, Cable Trays) and optionally run them. | yes |
| `navis_clash_rename_by_location` | Renames clash results as "<level> \| <grid> \| <item A> x <item B>" from the active grid system - meaningful clash names for reports and BCF. | no |
| `navis_clash_report_csv` | Write a CSV clash report (test, clash, status, distance mm, location, item paths, assignee) for one or all tests. | no |
| `navis_clash_report_html` | HTML clash report with a rendered image per clash (first N results of a test) - send to designers. | yes |
| `navis_clash_select_items` | Selects (and optionally colours red) every item involved in the clashes of a test with the given statuses - shows the problem elements in the model. | no |
| `navis_clash_summary` | Every clash test with totals by status (New/Active/Reviewed/Approved/Resolved) plus grand totals - coordination meeting summary. | no |
| `navis_clash_viewpoint` | Select both items of a clash result, color them red/green, zoom to it and optionally save a viewpoint named after the clash. | no |
| `navis_clip_planes_get` | Current sectioning/clip plane definition (JSON) of the active view. | no |
| `navis_color` | Permanently override color (#RRGGBB or [r,g,b] 0-255) and/or transparency (0-100) of items. | no |
| `navis_color_by_property` | Give each distinct value of a property its own color (e.g. Element/Category, Element/Level, Item/Layer, System Name) - instant appearance profile. | no |
| `navis_comment_add` | Add a comment (body, status) to a saved viewpoint. | no |
| `navis_comments` | All comments attached to saved viewpoints (body, author, status, date). | no |
| `navis_compare_models` | Compares two appended models (e.g. last week's and this week's NWC of a discipline) by an id property (default Element/Id, fallback InstanceGuid): items added, removed and moved / resized (bounding box change above tolerance). Optionally colours added green, moved orange and selects the changes. | no |
| `navis_documentation_snapshot` | For every saved viewpoint in a folder (or all), activates it and saves a PNG into a folder - instant issue sheet for coordination meetings. | no |
| `navis_export_properties_csv` | Writes a CSV of the scope items (or all geometry items) with chosen properties ([{category, property}]) or every property of the given categories. Good for quantity take-off and data audits in Excel. | no |
| `navis_export_tree_json` | Writes the selection tree (name, class, guid, geometry flag, children) to a JSON file down to a depth - for external dashboards / digital-twin mapping. | no |
| `navis_exporters` | Lists installed export plugins (FBX, DWF/DWFx, Google Earth KML, Images, Viewpoints XML, Clash XML/HTML, PDS, Piranesi...) or runs one by id (partial match) to a file. | no |
| `navis_file_ops` | Append one or many model files (NWC, NWD, RVT, IFC, DWG, FBX, RCP, ...), merge an NWF/NWD, open, save (NWF/NWD by extension) or clear the document. | no |
| `navis_file_units` | Document units, file name, title, current file version/state. | no |
| `navis_geometry_stats` | Number of items, geometry items, models and total extents - model weight check. | no |
| `navis_grids_levels` | Grid systems, levels (name, elevation) and grid lines from the federated model (Revit/IFC grids) - use for "which level / grid is this clash on". | no |
| `navis_hidden_items` | Items currently hidden (top-most hidden nodes). | no |
| `navis_hide_unselected` | Hide everything except the selection (like Hide Unselected). | no |
| `navis_image_export` | Render the current view (or a saved viewpoint) to a PNG file at a resolution. | yes |
| `navis_isolate` | Isolate the scope items (hide everything else, keeping their ancestors visible) or unhide everything. | yes |
| `navis_issue_viewpoints` | Creates a saved viewpoint zoomed to each scope item (named by a property or item name) inside a viewpoint folder - instant issue list for RFIs and reviews. | no |
| `navis_item_geometry` | For scope items: bounding box size (mm), volume of the box, original vs active colour and transparency, fragment count. | no |
| `navis_item_info` | Path, class, GUID, model file, bounding box (model units), hidden state and child count of items. | no |
| `navis_item_level` | For the scope items, reports the grid level below each item's centre and the nearest grid intersection (via GridSystem.ClosestIntersection when available). | no |
| `navis_items_distance` | Clearance between the bounding boxes of two items (or of the two selected items): overall gap and per-axis gap in model units and mm - quick clearance check (0 = boxes touch / overlap). | no |
| `navis_items_near` | Geometry items whose bounding boxes come within a distance (mm) of the scope items - e.g. everything within 500 mm of a selected duct. Optionally filtered by a property and selected. | no |
| `navis_layers` | Second-level nodes per model (layers in DWG/NWC, levels in Revit exports) with child counts. | no |
| `navis_lighting` | Set the current viewpoint lighting: none, headlight, scene, full. | no |
| `navis_measure` | Centre-to-centre distance, clear gap and dx/dy/dz between two items (in mm and model units). | no |
| `navis_model_offset` | Applies a translation (model units) to a whole appended model - fixes models that came in with wrong shared coordinates. reset=true removes the override. | yes |
| `navis_models` | Every appended model/file with source file, units, creator, item count at root. | no |
| `navis_plan_cut` | Section the model as a plan: keeps everything between a bottom and a top elevation (model units), or between a grid level and the cut height above it - floor-by-floor coordination views. | no |
| `navis_plugins` | Lists every plug-in (add-ins, docking panes, exporters, file readers, tools) with load state, or executes an AddInPlugin by id with string parameters (third-party add-ins, iConstruct, Naviate...). | no |
| `navis_property_tabs` | Every property category (tab) and property name present on the selected items - to know what you can search, colour or report on. | no |
| `navis_property_values` | All distinct values (with counts) of a property across the model - e.g. Element/Category, Item/Layer, Element/Level, Material. | no |
| `navis_publish_nwd` | Publish the current document to an NWD (title, author, comments, optional password protection off). | yes |
| `navis_qa_duplicates` | Geometry items with the same class and an identical bounding box (within tolerance) - typical double-modelled or double-appended elements. | no |
| `navis_qa_missing_property` | Geometry items (in scope) that do NOT have a property, or have it empty - e.g. elements without "Element/Level" or "Identity Data/Mark". Optionally selects them. | no |
| `navis_qa_model_alignment` | Compares the extents of every appended model with the first (reference) model - large offsets reveal shared-coordinate / units mistakes between disciplines. | no |
| `navis_qa_model_health` | Per appended model: item count, geometry items, hidden items, items without properties, class breakdown (top 15) and overall extents - a quick federated-model audit. | no |
| `navis_qa_property_stats` | Distinct values of a property with item counts (and numeric min/max/sum) across the model or the scope - e.g. Element/Category, Element/Level, Element/Workset, Item/Layer. | no |
| `navis_quantities` | Count of geometry items and sum of a numeric property (e.g. Element/Volume, Element/Area, Element/Length) grouped by another property (e.g. Element/Category) - quick takeoff. | no |
| `navis_required` | Mark items as required (always drawn while navigating) or not. | no |
| `navis_reset_appearance` | Reset color/transparency overrides for items, or all permanent/temporary overrides in the model. | no |
| `navis_section_box` | Enable sectioning as a box around items (or a box in model units), or turn sectioning off. Uses the view clipping-planes JSON API (Navisworks 2017+). | no |
| `navis_select_all` | Select all, none, or invert the current selection. | no |
| `navis_select_by_class` | Select geometry items whose class (Item > Type) contains text, e.g. "Pipe", "Duct", "Solid", "Block". | no |
| `navis_select_by_guid` | Find items by InstanceGuid, Revit Element ID (Element/Id) or IFC GlobalId and select them - links issues from BCF / Revit back into Navisworks. | no |
| `navis_select_by_property` | Find Items with several AND conditions, e.g. [{"category":"Element","property":"Category","value":"Walls"},{"category":"Element","property":"Level","value":"L1"}]. op: equals, contains, wildcard, defined, notequals, greater, less. | no |
| `navis_select_in_box` | Select geometry whose bounding box intersects (or is inside) a box in model units. | no |
| `navis_select_parents` | Expand the current selection: to parents (e.g. from geometry to the Revit element), first-level children, all descendants with geometry, or siblings. | no |
| `navis_select_same` | Select all items with the same display name (or class) as the given/selected items - like Select Same. | no |
| `navis_selection_inspector` | Tabular view of the current selection with chosen quick properties (default Item/Name, Item/Type, Element/Category, Element/Level) - like Navisworks' Selection Inspector. | no |
| `navis_selection_set_apply` | Select the items of a saved selection/search set (by name), optionally hide everything else. | no |
| `navis_selection_set_create` | Save the current selection (or handles) as a selection set, or a property search as a dynamic search set. | no |
| `navis_selection_sets` | All selection sets and search sets (folders included) with item counts. | no |
| `navis_sets_by_property` | Create one search set per distinct value of a property (e.g. a set per Revit Category or per Level) inside a folder - the classic clash-matrix setup in one call. | no |
| `navis_sets_from_levels` | Creates one search set per distinct value of a level property (default Element/Level) inside a folder - the standard floor-by-floor set structure for clash tests and 4D. | no |
| `navis_timeliner_clear` | Delete all TimeLiner tasks. | yes |
| `navis_timeliner_create_tasks` | Create TimeLiner tasks (Construct) from selection/search sets or from distinct values of a property (e.g. Element/Level), with planned dates spaced by N days. | yes |
| `navis_timeliner_delays` | Tasks whose actual end is after the planned end (or not finished past planned end), with delay in days - 4D progress check. | no |
| `navis_timeliner_export_csv` | Writes all TimeLiner tasks (hierarchy path, planned / actual dates, task type, attached set) to CSV. | no |
| `navis_timeliner_import_csv` | Creates TimeLiner tasks from a CSV with columns Name, Start, End[, Type][, Set] (dates ISO or local format). Tasks whose Set column names a saved selection/search set get it attached - quick 4D from any schedule export (Primavera, MS Project, Excel). | no |
| `navis_timeliner_link_sets` | Attaches saved selection / search sets to TimeLiner tasks whose names match the set names (case-insensitive) - fast 4D linking after task import. | yes |
| `navis_timeliner_summary` | Project start/end, task count, tasks without attached items, overdue (actual end after planned end) and current-date tasks - a 4D health check. | no |
| `navis_timeliner_task_dates` | Set planned/actual dates of a TimeLiner task by name. | yes |
| `navis_transform` | Translate (model units) and/or rotate (degrees about Z) items with an override transform - e.g. fix a misaligned model. reset=true restores. | no |
| `navis_transparency` | Set permanent transparency (0-100 %) on the scope items, e.g. make architecture 70 % transparent to review MEP. | yes |
| `navis_tree_search_name` | Find items whose display name contains text (selection tree search) and optionally select them. | no |
| `navis_units_info` | Document display units, each model's units, transform origin and overall scene extents in model units and millimetres. | no |
| `navis_view_preset` | Change the current view: home, front/back/left/right/top/bottom, perspective or orthographic projection, and render style (full, shaded, wireframe, hidden line). | no |
| `navis_viewpoint_delete` | Delete saved viewpoints by name (or all whose name starts with a prefix). | yes |
| `navis_viewpoint_folder` | Create a viewpoint folder and optionally move/copy named viewpoints into it. | no |
| `navis_viewpoint_images` | Export a PNG for every saved viewpoint (top level) - fast coordination report material. | yes |
| `navis_viewpoint_rename` | Rename a saved viewpoint. | no |
| `navis_viewpoint_zoom` | Zoom the current view to items (handles, search or selection), optionally saving it as a viewpoint. | no |
| `navis_viewpoints_tree` | Every saved viewpoint and folder with comment counts and redline presence. | no |
| `navis_visibility_by_property` | Hide, show, isolate or make transparent every item whose property matches - e.g. hide all Element/Category = Ceilings, isolate Element/Level = L03. | yes |
| `navis_zoom_to` | Select the scope items and zoom the active view to them (Focus on selection). | no |
| `navisworks_clash_create` | Create (and run) a clash test between two property searches, e.g. Structural Framing vs Ducts. | yes |
| `navisworks_clash_results` | Results of a test: status, distance, centre, both clashing items. Filter by status. | no |
| `navisworks_clash_run` | Run one test (name) or all tests. | yes |
| `navisworks_clash_tests` | Clash Detective tests with result counts by status. | no |
| `navisworks_clash_update` | Set status and/or assigned-to on clash results (handles from navisworks_clash_results). | yes |
| `navisworks_file` | open \| append (paths[] of rvt/nwc/dwg/ifc/rcp...) \| save (.nwd/.nwf) \| new \| refresh. | yes |
| `navisworks_info` | Open file, appended models, units, selection. | no |
| `navisworks_properties` | All properties of selected items (or handles / search). | no |
| `navisworks_search` | Search model items by property (Find Items), optionally select them. | no |
| `navisworks_selection` | get \| clear \| set (from handles or search). | no |
| `navisworks_timeliner` | 4D TimeLiner tasks with planned/actual dates. | no |
| `navisworks_tree` | Model item tree to a depth. | no |
| `navisworks_viewpoints` | list \| apply (name) \| save (name) the current view. | yes |
| `navisworks_visibility` | hide \| unhide \| isolate \| unhide_all for items (handles, search or selection). | yes |

### recap

| Tool | What it does | Changes model |
|---|---|---|
| `recap_info` | ReCap Pro installation, running instances, and the scan projects (.rcp/.rcs) found in a folder. ReCap has no public automation API: drive its UI with ui_* tools; process scans in Revit/Navisworks/AutoCAD (scan_* tools). | no |
| `recap_open` | Start ReCap Pro, optionally opening a .rcp project or importing scan files. | yes |

### revit

| Tool | What it does | Changes model |
|---|---|---|
| `revit_3d_orientation` | Set the camera of a 3D view from an eye point toward a target (mm), or to a named direction (top, front, back, left, right, iso). | yes |
| `revit_activate_view` | Open/activate a view or sheet by id, name or sheet number. | no |
| `revit_add_shared_parameter` | Create (if needed) a shared parameter in the shared parameter file and bind it to categories. Data type: text, integer, number, length, area, volume, yesno. | yes |
| `revit_annotation_types` | Text types, dimension types, tag families, fill patterns and line patterns available. | no |
| `revit_area_boundaries` | Draws area boundary lines in an area plan through a closed polygon of points (mm). | yes |
| `revit_area_by_department` | Room area totals grouped by a room parameter (Department, Occupancy, Name...). | no |
| `revit_area_create` | Places Area elements at points (mm) in an area plan view (the area boundaries must exist), optionally naming them. | yes |
| `revit_areas_report` | Areas grouped by area scheme (Gross Building, Rentable, ...) and level with totals in m2. | no |
| `revit_array_linear` | Copy elements N times along a vector (mm), like Revit Array. | yes |
| `revit_assemblies_list` | Assembly instances with member counts. | no |
| `revit_assembly_create` | Create an assembly instance from elements (prefabrication / shop drawings). | yes |
| `revit_auto_join_category` | Join geometry of every intersecting pair between two categories (e.g. Structural Columns with Floors). | yes |
| `revit_batch_parameter_formula` | Set a text/number parameter from a template using other parameters, e.g. "{Level}-{Mark}" or numeric "{Width}*{Height}" (numbers in internal units unless suffix _mm). | yes |
| `revit_beam_offsets` | Set start/end level offsets (mm) and/or z-justification of beams. | yes |
| `revit_beams_between_columns` | Create beams along grid lines connecting consecutive columns at a level (frame generator). | yes |
| `revit_boq_by_category` | Bill of quantities per family/type: count, length (m), area (m2), volume (m3) for chosen categories; also writes a CSV. | no |
| `revit_braces_trusses` | Structural trusses, braces and structural connections in the model. | no |
| `revit_cable_tray_route` | Creates cable tray runs through a polyline of points (mm) with type, width and height; elbows at bends. | yes |
| `revit_cad_cleanup` | Delete imported (not linked) CAD instances - optionally only view-specific ones. Dry run first. | yes |
| `revit_cad_files` | DWG/DXF/DGN imports and links with view-specific flag, owner view and level - QA (imports bloat models). | no |
| `revit_cad_link` | Link (or import) a DWG/DXF into a view or the model, auto-centered or origin-to-origin, with units. | yes |
| `revit_callout_create` | Create a callout in a plan/section view from two corner points (mm). | yes |
| `revit_categories` | All model/annotation categories. | no |
| `revit_category_override` | Color/halftone/transparency override for a whole category in a view. | yes |
| `revit_category_visibility` | Hide or show categories in views (Visibility/Graphics). | yes |
| `revit_ceiling_from_room` | Create a ceiling for each room at a height (mm) above its level. | yes |
| `revit_ceiling_heights` | Ceilings with type, level, height offset from level (mm) and area - flags ceilings below a minimum clear height. | no |
| `revit_change_type` | Swap elements to another type of the same category (e.g. wall types, door sizes). | yes |
| `revit_circuit_create` | Create a power circuit from electrical fixtures/devices and optionally assign it to a panel. | yes |
| `revit_clash_check` | Hard clashes (solid intersection) between two categories inside the model, e.g. Structural Framing vs Ducts. Optionally colors clashing elements in the active view. | no |
| `revit_clear_overrides` | Remove graphic overrides from elements in a view. | yes |
| `revit_clearance_check` | Element pairs between two categories whose bounding boxes come closer than a clearance (mm) without touching - soft clashes. | no |
| `revit_close_views` | Close all open view windows except the active one (keeps Revit fast). | no |
| `revit_column_grid_check` | Structural columns that are not on a grid intersection (QA), with distance to the nearest grid (mm). | no |
| `revit_columns_at_grids` | Place structural columns at every grid intersection (or listed grids) from a base to a top level. | yes |
| `revit_columns_report` | Columns with type, base/top level and offsets, height, location and material. | no |
| `revit_command` | Run any Revit command: PostableCommand name (Wall, Door, Align, SectionBox, StructuralRebar, EnergySettings, Purge...), internal id (ID_OBJECTS_WALL) or add-in button id. Interactive commands then wait for user picks. | yes |
| `revit_commands` | Every runnable Revit command (PostableCommand) for this Revit version, filterable. | no |
| `revit_compare_types_usage` | For each category: number of types loaded vs. used, most used types - helps standardisation. | no |
| `revit_concrete_volume_by_level` | Concrete takeoff (m3) per level and structural category, from material volumes whose material name contains a keyword. | no |
| `revit_conduit_route` | Creates straight conduit runs through a polyline of points (mm) with a conduit type and diameter; elbows are added at the bends. | yes |
| `revit_copy_from_link` | Copies elements of given categories (e.g. Grids, Levels, Floors) from a Revit link into the host model with the link's transform - the usual way to bring grids/levels from the architect's model. | yes |
| `revit_copy_monitor_report` | Elements monitoring linked elements (copy/monitor) and links positioning - coordination QA. | no |
| `revit_copy_to_levels` | Copy elements vertically to other levels (offset = level elevation difference), e.g. typical floors. | yes |
| `revit_cost_estimate` | Quick estimate: type 'Cost' parameter x count (or x area/volume/length for host and line elements), per category. | no |
| `revit_count_by_category` | Number of model elements per category in the whole model or the active view. | no |
| `revit_count_by_type` | Instance count per family/type for a category (e.g. Doors, Windows, Structural Columns). | no |
| `revit_create` | Create (batch with items[]) in one undo step - ARCH: wall, floor, ceiling, roof, room, family_instance (doors/windows with hostId, furniture, any category); STR: column, beam, brace, foundation; MEP: pipe, duct, cable_tray, conduit; DATUM: grid, level; DOC: text, model_line, detail_line. Coordinates in mm. Every element is stamped AI-created for inspectors. | yes |
| `revit_create_view` | Create a 3d, plan, ceiling, structural plan, section (start/end/depth/height mm) or sheet. | yes |
| `revit_curtain_grid_add` | Add evenly spaced U (horizontal) or V (vertical) grid lines on a curtain wall. | yes |
| `revit_curtain_walls_report` | Curtain walls with grid counts, panel and mullion counts. | no |
| `revit_delete` | Delete elements by id (one undo step). | yes |
| `revit_delete_by_category` | Delete every element of a category (optionally only in the active view or on a level). Use dryRun first. | yes |
| `revit_delete_unused_views` | Delete views not placed on sheets (keeps templates, the active view, schedules and views whose name contains a keep keyword). Dry run first. | yes |
| `revit_dependent_views` | Creates dependent views of a plan (one per scope box, or a given count) - the standard way to split large floors across sheets. | yes |
| `revit_design_options` | Design option sets and options (primary flag) with element counts. | no |
| `revit_detail_component` | Place a detail item family in a drafting/plan/section view at a point (mm). | yes |
| `revit_detail_lines` | Draw detail lines (polyline, mm) in a view with an optional line style. | yes |
| `revit_dimension_between` | Linear dimension between two elements that have stable references (grids, levels, reference planes, walls centre) along a line (mm). | yes |
| `revit_dimension_grids` | Add a dimension string across all parallel grids (vertical or horizontal set) in a plan view. | yes |
| `revit_dimension_overrides` | Dimensions whose displayed value was overridden (fake dimensions) - a classic drawing-quality check. | no |
| `revit_dimensions_report` | Dimensions in a view (or model) with value (mm), type and override text. | no |
| `revit_distance` | Centre-to-centre and closest bounding-box distance (mm) between two elements. | no |
| `revit_documents` | All documents open in this Revit session (projects, families, links). | no |
| `revit_door_fire_rating_check` | Doors whose Fire Rating is lower than / missing compared with the host wall type's Fire Rating (numeric minutes compared when possible). | no |
| `revit_door_from_to_rooms` | Every door with its From Room and To Room (phase of the view or last phase), host wall and fire rating - checks door numbering and egress. | no |
| `revit_door_place` | Place a door (or any wall-hosted family) in a wall at a point (mm) or at a distance from the wall start. | yes |
| `revit_doors_windows_report` | All doors and windows with type, size (width/height mm), host wall, level, room from/to and mark. | no |
| `revit_drafting_view` | Create a drafting view (for 2D details) with name and scale. | yes |
| `revit_duct_route` | Create a connected duct run through points (mm) with elbows at bends. | yes |
| `revit_duct_size` | Change duct width/height or diameter (mm). | yes |
| `revit_ducts_report` | Ducts with system, type, size, length and level; totals by system. | no |
| `revit_duplicate_type` | Duplicate any element type under a new name, optionally setting type parameters. | yes |
| `revit_dynamo_run` | Run a Dynamo (.dyn) graph inside Revit in automation mode (Dynamo for Revit must be installed). Graph inputs can be set beforehand in the .dyn or via Dynamo Player. | yes |
| `revit_electrical_circuits` | Electrical circuits with panel, circuit number, load name, apparent load, voltage, number of elements. | no |
| `revit_electrical_panels` | Panels / electrical equipment with circuits count and connected load. | no |
| `revit_element_bounding_boxes` | Model bounding box (min/max/size in mm) and centre of elements. | no |
| `revit_element_geometry_summary` | Solid volume, surface area, face/edge counts and bounding box of elements. | no |
| `revit_element_history` | Workshared models: creator, last person who changed it, current owner and checkout status of elements - the core question of every BIM inspection ("who made this?"). | no |
| `revit_element_host_info` | Host (for doors/windows/hosted families), room, super/sub components and dependent elements. | no |
| `revit_element_materials` | Materials used by elements with area (m2) and volume (m3) per material. | no |
| `revit_element_owner` | Creator, owner, last changed by and checkout status of elements in a workshared model. | no |
| `revit_element_phase` | Set Phase Created and/or Phase Demolished of elements. | yes |
| `revit_element_workset` | Move elements to a workset (workshared models). | yes |
| `revit_elements_below_level` | Family instances whose base is more than a tolerance (mm) below their host level, or floating above - placement QA. | no |
| `revit_elements_in_room` | Family instances located in a room (furniture, fixtures, equipment) - room data sheets. | no |
| `revit_elevations_create` | Create an elevation marker with up to 4 interior/exterior elevations at a point (mm) in a plan view. | yes |
| `revit_embodied_carbon` | Embodied carbon (kgCO2e) = material volume x density x factor. Supply factors per material keyword, e.g. {"Concrete":[2400,0.13],"Steel":[7850,1.55],"Brick":[1800,0.24]} (density kg/m3, kgCO2e/kg). | no |
| `revit_energy_settings` | Energy data settings: building type, analysis mode, ground plane, project phase, core offset. | no |
| `revit_export` | Export IFC (ifcVersion), DWG, NWC, PDF, FBX, gbXML or PNG/JPG images. Default folder: <BURAK BIM>\exports. | yes |
| `revit_export_all_schedules` | Exports every schedule (or those whose name contains a filter) to CSV files in a folder. | no |
| `revit_export_elements_csv` | Export elements of a category with chosen parameters (or all) to CSV - for Excel, Power BI, cost, FM. | no |
| `revit_export_families` | Save loaded (editable) families as .rfa files into a folder, optionally one category - build a library from a project. | yes |
| `revit_export_ifc_options` | Export IFC (IFC2x3, IFC4, IFC4x3...) with a view filter (only visible elements), space boundaries and base quantities. | yes |
| `revit_export_images_views` | Export PNG images of several views/sheets (e.g. every 3D view) at a pixel width. | yes |
| `revit_export_model_json` | Lightweight JSON of the model (levels, grids, rooms, element counts, types) for dashboards or other AI tools. | no |
| `revit_export_nwc_views` | Export one Navisworks NWC per listed 3D view (e.g. per discipline) for coordination. | yes |
| `revit_export_rooms_csv` | Room data (number, name, level, area, perimeter, finishes, department, occupancy) to CSV. | no |
| `revit_export_sheets_dwg` | Export sheets or views to DWG using a saved DWG export setup (or defaults); merged views option. | yes |
| `revit_export_sheets_pdf` | Export sheets (all, by numbers or prefix) to PDF - one combined file or one per sheet (Revit 2022+ native PDF). | yes |
| `revit_families_report` | Every loaded family with category, number of types, placed instances and in-place flag. | no |
| `revit_family_edit_parameter` | Open a loaded family in the background, add a family parameter (instance/type, data type), and load it back into the project. | yes |
| `revit_family_instances_report` | Loadable family instances counted by category, family and type, with host / level distribution - finds unexpected or generic families. | no |
| `revit_family_reload` | Load (or reload, overwriting parameter values) every .rfa in a folder or a single file - e.g. after updating a company library. | yes |
| `revit_family_types` | Types of a loaded family with their type parameters. | no |
| `revit_filled_region` | Create a filled region from an outline (mm) in a view with a filled region type. | yes |
| `revit_filter_apply` | Add an existing filter to views with color or visibility. | yes |
| `revit_filter_create` | Create a parameter filter for categories with one rule (equals / contains / begins / greater / less) on a parameter, optionally applying it with a color to views. | yes |
| `revit_filters_report` | Parameter/selection filters in the model and which views use them (with visibility). | no |
| `revit_find_by_ifc_guid` | Finds elements by IFC GlobalId (IfcGUID / IFC GUID parameters) - links BCF issues and IFC viewer comments back to Revit. Optionally selects them. | no |
| `revit_find_by_mark` | Elements whose Mark parameter equals / contains a value. | no |
| `revit_find_by_parameter` | Elements of a category whose parameter equals / contains / begins with / is greater or less than a value, or is empty - and optionally selects them. | no |
| `revit_find_duplicates` | Instances of the same type at the same location (within tolerance) - typical model QA problem. | no |
| `revit_fittings_accessories` | Pipe/duct fittings and accessories counted by family/type (BOQ). | no |
| `revit_floor_from_room` | Create a floor (finish slab) following each room's boundary. | yes |
| `revit_floor_opening` | Cuts a vertical opening through a floor, roof or ceiling with a rectangle (centre, width, depth in mm) or a polygon of points. | yes |
| `revit_floors_report` | Floors with type, level, area (m2), volume (m3), thickness and structural flag, plus totals by type. | no |
| `revit_footings_under_columns` | Place an isolated foundation type under every (selected) structural column at its base. | yes |
| `revit_foundation_slab` | Create a structural floor (or foundation slab type) from an outline (mm) on a level. | yes |
| `revit_foundations_report` | Isolated footings, wall foundations and foundation slabs with type and volume (m3). | no |
| `revit_framing_join_ends` | Allow or disallow joins at both ends of beams (StructuralFramingUtils). | yes |
| `revit_framing_report` | Beams/braces with type, level, length (mm), start/end offsets, material and total length per type. | no |
| `revit_framing_types` | Loaded column/framing/foundation types with key dimensions. | no |
| `revit_furniture_place_grid` | Place a family type in a rows x columns grid (mm spacing) - furniture, lights, equipment, parking. | yes |
| `revit_get_elements` | Full details, bounding box, instance + type parameters, BURAK BIM stamp of elements. | no |
| `revit_global_parameters` | List global parameters with values, create a new one (length, number, text, yes/no, area) or set a value. | yes |
| `revit_grid_bubbles` | Show or hide grid (and level) bubbles at end 0, end 1 or both ends in a view - drawing clean-up. | yes |
| `revit_grid_rename` | Rename grids with a map {"old":"new"} (two-pass, so swaps like 1<->2 work). | yes |
| `revit_grid_system` | Create a rectangular grid system from spacing lists (mm): vertical grids 1,2,3... along X and horizontal grids A,B,C... along Y. | yes |
| `revit_grids_report` | Grid lines with names, start/end points (mm), orientation and length. | no |
| `revit_group_create` | Create a group from elements and optionally name it. | yes |
| `revit_group_ungroup` | Ungroup group instances (members stay in the model). | yes |
| `revit_groups_list` | Group types with instance counts and member counts. | no |
| `revit_halftone_elements` | Set elements halftone in a view (e.g. existing / linked context). | yes |
| `revit_hide_in_view` | Permanently hide (or unhide) elements in a view. | yes |
| `revit_hvac_zones` | HVAC zones with their spaces. | no |
| `revit_import_params_csv` | Update element parameters from a CSV with an ElementId (or UniqueId) column and parameter columns (round-trip with revit_export_elements_csv). | yes |
| `revit_in_place_families` | In-place (model-in-place) families with category and instance count - usually against company BIM standards. | no |
| `revit_info` | Revit version, open models, active view. | no |
| `revit_insulation_add` | Add insulation (type + thickness mm) to pipes or ducts. Use lining=true for duct lining. | yes |
| `revit_isolate_temporary` | Temporary isolate or hide elements or categories in the active view (like the sunglasses menu). mode: isolate, hide, isolateCategory, hideCategory, reset. | yes |
| `revit_join_geometry` | Join the geometry of element pairs (e.g. walls with floors/columns) - first element is joined with each of the others. | yes |
| `revit_keynotes_report` | Keynote values used by element types and instances (Keynote parameter) with counts, plus the keynote table path. | no |
| `revit_legends_report` | Legend and drafting views with element counts. | no |
| `revit_level_area_summary` | Per level: floor area (slabs), room net area, number of rooms - area schedules at a glance. | no |
| `revit_level_edit` | Rename a level and/or change its elevation (mm). | yes |
| `revit_levels` | Levels with elevations in mm. | no |
| `revit_levels_series` | Create levels at a storey height (mm) above the highest (or a given) level, with plan views and names. | yes |
| `revit_lights_grid` | Place lighting fixtures in a regular grid inside a room (spacing mm), at the ceiling height offset. | yes |
| `revit_line_style_create` | Create a line style with color and weight (and optional line pattern). | yes |
| `revit_line_styles` | Line styles (subcategories of Lines) with color, weight and pattern. | no |
| `revit_lines_to_walls` | Creates walls along selected model / detail lines (or CAD-traced lines) with a wall type, base level and height - fast massing from sketches or Scan-to-BIM traces. | yes |
| `revit_link_add` | Link a .rvt model (origin to origin or shared coordinates). | yes |
| `revit_link_elements` | Query elements of a category inside a linked model (with transformed positions in host coordinates). | no |
| `revit_link_manage` | Reload, unload or reload-from a new path for a Revit link type. | yes |
| `revit_links` | Linked Revit models (type, path, loaded status, instances, pinned, positioning). | no |
| `revit_load_cases` | Load cases (with nature/category) and load combinations defined in the model. | no |
| `revit_load_families_folder` | Loads every .rfa in a folder (optionally recursive) into the project, overwriting existing families when asked - quick content setup from the company library. | yes |
| `revit_load_family` | Load an .rfa family file. | yes |
| `revit_loads_report` | Point, line and area loads with load case, nature and forces. | no |
| `revit_location` | Geographic location (lat/long, place name, time zone), project base point, survey point and true-north angle. | no |
| `revit_location_set` | Set latitude/longitude (degrees), place name and time zone (hours). | yes |
| `revit_material_assign` | Assign a material to elements' Material / Structural Material parameter, or to a layer of their type (layerIndex). | yes |
| `revit_material_create` | Create a material (or edit an existing one): color, transparency, class, cut/surface patterns. | yes |
| `revit_materials_report` | Materials with class, color, transparency, appearance/structural/thermal assets. | no |
| `revit_materials_takeoff` | Total area (m2) and volume (m3) of every material across a category or the whole model. | no |
| `revit_mep_clearance` | Pipes/ducts/trays whose bottom elevation is below a minimum clearance (mm) above their level - headroom QA. | no |
| `revit_mep_color_by_system` | Color pipes/ducts in the active view by system (distinct color per system) - quick coordination view. | yes |
| `revit_mep_connect` | Connect the nearest open ends of two pipes/ducts/trays/conduits with an elbow (or direct connection when aligned). | yes |
| `revit_mep_connectors` | All connectors of MEP elements with domain, shape, size, flow direction, system and what they connect to. | no |
| `revit_mep_equipment_report` | Mechanical equipment, plumbing fixtures, air terminals, sprinklers, lighting and electrical fixtures/equipment with type, level, room and system. | no |
| `revit_mep_offset` | Set the middle elevation / offset (mm above reference level) of pipes, ducts, trays and conduits. | yes |
| `revit_mep_open_ends` | Every unconnected MEP connector (open pipe/duct ends, unconnected equipment) with location (mm) - like Show Disconnects. | no |
| `revit_mep_split` | Break a pipe or duct into two at a point (mm), keeping connections (PlumbingUtils / MechanicalUtils). | yes |
| `revit_mep_systems_report` | Piping, duct and electrical systems with classification, element counts and base equipment. | no |
| `revit_mep_tee` | Create a tee fitting joining a branch into a main split at the same point (main1, main2, branch ids). | yes |
| `revit_mep_types` | Pipe types, duct types, piping/mechanical system types, cable tray and conduit types available. | no |
| `revit_mirror` | Mirror elements about a vertical plane through two points (mm), copying by default. | yes |
| `revit_model_health` | One-shot model audit: warnings, families, in-place families, CAD imports, groups, views not on sheets, unplaced rooms, purgeable types, file size. | no |
| `revit_model_lines` | Draws model lines (on a level work plane) or detail lines (in a view) through a polyline of points in mm, optionally with a line style. | yes |
| `revit_move` | Move or copy elements by an offset in mm. | yes |
| `revit_naming_audit` | Check views, sheets, families, types, levels or materials against a regex naming standard. | no |
| `revit_nearby_elements` | Elements whose bounding boxes are within a distance (mm) of an element or a point, optionally limited to categories - quick spatial query for inspectors. | no |
| `revit_new` | New project from a template (default template if omitted), optionally saved to path. | yes |
| `revit_object_style` | Set a category's (or subcategory's) projection/cut line weight and color in Object Styles. | yes |
| `revit_open` | Open and activate a .rvt/.rfa (detach=true for central files). | yes |
| `revit_open_views` | Views currently open in Revit windows. | no |
| `revit_orphan_tags` | Tags whose host is gone or that show nothing; optionally delete them. | yes |
| `revit_overlapping_walls` | Collinear walls that overlap each other (typical 'highlighted walls overlap' issue). | no |
| `revit_override_color` | Override projection/cut color (solid fill) of elements in a view. Color as "#RRGGBB" or [r,g,b]. | yes |
| `revit_param_bulk_set` | Set a parameter on every element of a category that matches an optional condition (param equals value). | yes |
| `revit_param_copy` | Copy the value of parameter A into parameter B for elements (ids/selection or a category). | yes |
| `revit_param_distinct_values` | All distinct values (with counts) of a parameter across a category - great for QA and naming audits. | no |
| `revit_param_find_empty` | Elements of a category where a (required) parameter is empty - data completeness check. | no |
| `revit_parts_create` | Divides walls / floors / roofs into Parts (one per layer) for construction modelling and quantity take-off per layer. | yes |
| `revit_phases` | Project phases (in order) and phase filters, with element counts created per phase. | no |
| `revit_pin` | Pin (or unpin) elements so they cannot be moved accidentally. | yes |
| `revit_pipe_route` | Create a connected pipe run through points (mm) with elbows at every bend (uses the pipe type's routing preferences). | yes |
| `revit_pipe_size` | Change pipe diameter (mm) and/or slope (e.g. "2%" or 0.02) for pipes. | yes |
| `revit_pipe_slope_set` | Sets the slope (percent) of pipes, e.g. 1 % / 2 % for sanitary lines. | yes |
| `revit_pipes_report` | Pipes with system, type, diameter (mm), length (m), slope and level; totals by system and diameter. | no |
| `revit_place_instances` | Places a family type (any category: furniture, equipment, fixtures, generic models, sprinklers, lights ...) at a list of points (mm) on a level, with rotation (deg) and optional parameter values. | yes |
| `revit_place_view_on_sheet` | Place a view (plan/section/3D/drafting/legend) on a sheet at a point (mm on sheet) or at the sheet centre. | yes |
| `revit_plans_for_levels` | Create floor / ceiling / structural plans for levels that don't have one yet, optionally with a template. | yes |
| `revit_point_cloud_link` | Links an Autodesk ReCap project (.rcp) or scan (.rcs) into the model, origin to origin or auto-positioned at the project origin. | yes |
| `revit_point_clouds` | Linked point clouds (RCP/RCS) with type, path, position, scan count and bounding box - the starting point for Scan-to-BIM in Revit. | no |
| `revit_project_info` | Project information (name, number, client, address, status, issue date, author) and file info. | no |
| `revit_project_info_set` | Set project information fields (name, number, client, address, status, issueDate, author, building, organization) or any Project Information parameter by name. | yes |
| `revit_project_parameters` | All project/shared parameters bound in the model with their categories and instance/type binding. | no |
| `revit_purge_unused` | Delete unused families and types using Revit's own 'unused families and types' rule. Optionally dry run. | yes |
| `revit_purge_view_specific` | Delete view templates and filters not used by any view (dry run first). | yes |
| `revit_query` | Find elements by category (Walls, OST_Doors...), class, level, type name, name, parameter conditions (where: [{param, op (=,!=,contains,>,<,>=,<=,empty,notempty), value}]). | no |
| `revit_raycast` | Shoot a ray from a point in a direction (mm vectors) in a 3D view and return the first elements hit with distance - for clearance, line-of-sight and sensing. | no |
| `revit_rebar_cover` | Set rebar cover type for top/bottom/other faces of concrete hosts. | yes |
| `revit_rebar_from_curves` | Create a standard rebar in a host from a polyline (mm) with bar type and optional layout (fixed number + spacing along a normal). | yes |
| `revit_rebar_report` | Rebar sets with bar type, host, quantity, total length (m) and estimated weight (kg). | no |
| `revit_reference_planes` | List reference planes or create one between two points (mm) in a view. | yes |
| `revit_relinquish_all` | Relinquish all borrowed elements and worksets you own (without saving changes to central) - frees elements for the team. | no |
| `revit_reload_latest` | Reload Latest from the central model so the local copy has everyone's changes (no sync). | no |
| `revit_rename_type` | Rename an element type. | yes |
| `revit_reveal_hidden_report` | Elements permanently hidden in a view (like Reveal Hidden Elements) - QA before printing. | no |
| `revit_revision_create` | Create a revision (date, description, issued by/to) and add it to sheets. | yes |
| `revit_revisions` | Revisions with sequence, date, description, issued flag and sheets carrying each. | no |
| `revit_roof_by_footprint_slope` | Create a footprint roof from an outline (mm) with every edge sloped (degrees) - hip roof - or flat when slope = 0. | yes |
| `revit_roofs_report` | Roofs with type, level, area and slope info. | no |
| `revit_room_boundaries` | Boundary loops (mm) of rooms with the bounding element of each segment. | no |
| `revit_room_create_at` | Create a room at a point (mm) on a level with name and number. | yes |
| `revit_room_finishes` | Rooms with number, name, level, area, height and the finish parameters (Floor / Wall / Ceiling / Base Finish) - plus rooms missing finishes. | no |
| `revit_room_finishes_set` | Write Floor / Wall / Ceiling / Base Finish to rooms (selection, ids, or all rooms with a given name). | yes |
| `revit_room_problems` | Rooms that are unplaced, not enclosed or redundant - and optionally delete unplaced ones. | no |
| `revit_room_separators` | Draw room separation lines on a level (mm) - pairs of points. | yes |
| `revit_room_tag_all` | Places a room tag at the location point of every room visible in a plan view that has no tag yet. | yes |
| `revit_rooms_auto_place` | Create rooms in every enclosed area of a level (like Place Rooms Automatically). | yes |
| `revit_rooms_from_link` | Lists rooms of a linked architectural model (number, name, level, area) and, for given host elements, which linked room contains them - typical for MEP models referencing architecture. | no |
| `revit_rooms_of_elements` | Which room each family instance is in (FamilyInstance.Room, else point-in-room test). | no |
| `revit_rooms_renumber` | Renumber rooms on a level in reading order (top-left to bottom-right) with a prefix and start number. | yes |
| `revit_rooms_report` | Rooms with number, name, level, area (m2), perimeter (m), volume, department and placement status, plus totals per level. | no |
| `revit_rotate` | Rotate elements about a vertical axis. | yes |
| `revit_save` | Save, or Save As with path. | yes |
| `revit_schedule` | Rows of a schedule view. | no |
| `revit_schedule_create` | Create a schedule for a category with named fields (in order), optional sort field and itemize flag. Use materialTakeoff=true for a material takeoff. | yes |
| `revit_schedule_export_csv` | Export one or all schedules to CSV/TXT files in the exports folder (or a folder). | no |
| `revit_schedule_fields` | Field names you can add to a schedule of a category. | no |
| `revit_schedule_filter` | Add a filter to a schedule (field equals/contains/greater/less a value). | yes |
| `revit_schedules_report` | All schedules with category, field names and row count. | no |
| `revit_scope_boxes` | List scope boxes (with extents) or assign one to views (Scope Box parameter). | yes |
| `revit_section_box` | Set (or remove) the section box of a 3D view around elements or a box (mm) - creates the 3D view if asked. | yes |
| `revit_section_through` | Create a section view cutting through elements (aligned with the first linear element, or along X) with a margin. | yes |
| `revit_select_by_category` | Select every element of a category (whole model or active view) in Revit and return the ids. | no |
| `revit_select_by_type` | Select every instance of a family type (by type name, optionally category). | no |
| `revit_select_in_box` | Elements whose bounding box is inside or intersects a 3D box (mm). Optional category. | no |
| `revit_select_similar` | Select all instances that share the type of the given/selected elements. | no |
| `revit_selection` | Without ids: returns the current selection. With ids: selects (and zooms to) those elements. | no |
| `revit_sequence_numbering` | Write sequential numbers (prefix + n) into a parameter for elements of a category ordered by X, Y or by level then X - marks for columns, doors, piles. | yes |
| `revit_set_parameters` | Set instance (or type with onType=true) parameters. Lengths given as numbers are mm; strings use project units ('3000 mm', '10 ft'). | yes |
| `revit_shaft_opening` | Vertical shaft opening through floors between a base and top level, from a closed outline (mm). | yes |
| `revit_sheet_add_revision` | Adds a revision (by sequence number or description) to the "Revisions on Sheet" of sheets (by number list or all). | yes |
| `revit_sheet_edit` | Renumber or rename sheets with a map {"A-101":{"number":"A-201","name":"..."}}. | yes |
| `revit_sheet_renumber` | Find/replace or prefix/suffix on sheet numbers and/or sheet names. Dry run by default. | yes |
| `revit_sheet_revision_matrix` | Which revisions appear on which sheet (clouds or manually added) - the revision schedule check before issue. | no |
| `revit_sheet_set_create` | Save a named set of sheets (by number list or prefix) for printing/exporting. | yes |
| `revit_sheet_sets` | Saved view/sheet sets used for printing and export. | no |
| `revit_sheets_create` | Create many sheets at once from [{number, name}] with a titleblock. | yes |
| `revit_sheets_report` | Sheets with number, name, titleblock, placed views and schedules, revisions on sheet. | no |
| `revit_slab_edge_check` | Floors whose type name suggests structure (slab/RCC) but are not flagged structural, and vice versa. | no |
| `revit_space_airflow` | MEP spaces with area, volume, specified / calculated supply airflow, air changes and zone - HVAC design check. | no |
| `revit_spaces_from_rooms` | Create an MEP space at every room location (same level) and copy name/number. | yes |
| `revit_spaces_report` | Spaces with number, name, level, area, volume, zone and room association. | no |
| `revit_stairs_railings_report` | Stairs (risers, tread depth, riser height, levels) and railings (type, length). | no |
| `revit_starting_view` | Set the model's starting view (e.g. a cover sheet). | yes |
| `revit_steel_tonnage` | Structural steel weight (tonnes) per section type from member volume x density (7850 kg/m3 default). | no |
| `revit_structural_material` | Set the Structural Material of columns/framing/foundations. | yes |
| `revit_structural_usage` | Set structural usage of framing (Girder, Joist, Purlin, HorizontalBracing, Other) or walls (Bearing, Shear, NonBearing). | yes |
| `revit_switch_join_order` | Switch which of two joined elements cuts the other. | yes |
| `revit_sync_central` | Synchronize with central (comment, relinquish all, compact). Workshared models only. | yes |
| `revit_system_elements` | All elements belonging to an MEP system (by name or id). | no |
| `revit_tag_category` | Tag every untagged element of a category in a view (doors, windows, walls, rooms, columns, beams, pipes, ducts, equipment...). | yes |
| `revit_text_note_add` | Place a text note in a view at a point (mm), with type and width. | yes |
| `revit_text_notes` | Text notes in the model (or a view), with optional search text. | no |
| `revit_text_replace` | Replace text in all text notes (optionally one view); case sensitive. | yes |
| `revit_text_type_create` | Create a text type by duplicating one: font, size (mm), bold. | yes |
| `revit_tiny_elements` | Line-based elements shorter than a threshold (mm) - drafting mistakes that cause warnings. | no |
| `revit_titleblock_params` | Read titleblock parameters of sheets, or set values (e.g. Drawn By, Checked By, Sheet Issue Date) on many sheets at once. | yes |
| `revit_titleblock_swap` | Changes the title block on sheets to another family type (e.g. A1 -> A0) - all sheets or a sheet number list. | yes |
| `revit_trays_conduits_report` | Cable trays and conduits with type, size and length; totals by type. | no |
| `revit_types` | Family types / system types loaded in the model, by category or class. | no |
| `revit_undo` | Undo the last change in Revit. | yes |
| `revit_units` | Length, area, volume, angle and slope display units. | no |
| `revit_units_set` | Set length unit (mm, cm, m, ft, in, ftin), area (m2, ft2) and volume (m3, ft3) with accuracy. | yes |
| `revit_unjoin_geometry` | Unjoin the first element from each of the others. | yes |
| `revit_unjoined_walls` | Wall ends that don't join any other wall (open corners) - QA for room boundaries and drawings. | no |
| `revit_untagged_elements` | Elements of a category visible in a view but not tagged - documentation QA. | no |
| `revit_unused_types` | Element types of a category with zero placed instances (purge candidates). | no |
| `revit_view_crop` | Enable crop and set the crop box of a plan/3D view around a box (mm) or around elements with a margin. | yes |
| `revit_view_duplicate` | Duplicate views (plain, with detailing, or as dependent) N times with a name suffix. | yes |
| `revit_view_filters_copy` | Copies filters with their visibility and graphic overrides from a source view (or template) to target views. | yes |
| `revit_view_filters_in_view` | Filters of a view with visibility, projection line / surface colours and halftone. | no |
| `revit_view_graphics_set` | Set scale, detail level (coarse/medium/fine), visual style (wireframe, hidden, shaded, consistent, realistic) and discipline on one or many views. | yes |
| `revit_view_phase_set` | Set the phase and phase filter of views (e.g. "Existing" + "Show Previous + Demo"). | yes |
| `revit_view_properties` | Set scale (1:n), detail level (Coarse/Medium/Fine), visual style (Wireframe, HLR, Shading, ShadingWithEdges, Realistic), discipline and phase of views. | yes |
| `revit_view_range` | Set top / cut plane / bottom / view depth offsets (mm, relative to the plan's level) of a floor plan. | yes |
| `revit_view_rename` | Rename views by a {"old":"new"} map, or find/replace text in all view names. | yes |
| `revit_view_template_apply` | Apply (or remove with template='none') a view template to views. | yes |
| `revit_view_template_from_view` | Create a new view template from an existing view. | yes |
| `revit_view_templates` | All view templates with view type and how many views use each. | no |
| `revit_view_templates_audit` | Views placed on sheets (or all printable views) that have no view template, and templates not used by any view - graphics consistency check. | no |
| `revit_view_worksets` | Show or hide a workset in a view (workshared models). | yes |
| `revit_viewport_move` | Move a viewport's centre to a sheet location (mm), or align it to another viewport. | yes |
| `revit_viewports_report` | Viewports on sheets with view, sheet, box centre (mm) and type. | no |
| `revit_views` | All views and sheets (id, name, type, sheet number). | no |
| `revit_views_not_on_sheets` | Views (plans, sections, elevations, 3D, drafting, legends, schedules) not placed on any sheet. | no |
| `revit_views_rename_pattern` | Batch rename views (optionally of a view type) with find/replace and prefix. Dry run by default. | yes |
| `revit_wall_arc` | Create an arc wall through start, a point on the arc, and end (mm). | yes |
| `revit_wall_flip` | Flip wall orientation (exterior/interior side). | yes |
| `revit_wall_foundations` | Create strip foundations under walls. | yes |
| `revit_wall_join_ends` | Allow or disallow automatic joins at both ends of walls. | yes |
| `revit_wall_layers` | Compound structure layers (function, material, thickness) of wall, floor, roof or ceiling types. | no |
| `revit_wall_offset_copy` | Copy walls perpendicular to their direction by an offset (mm) - e.g. parallel partitions, cladding layers. | yes |
| `revit_wall_opening` | Cut a rectangular opening in a wall between two points (mm, diagonal corners in the wall plane). | yes |
| `revit_wall_polyline` | Create a chain of walls through points (mm), optionally closed. | yes |
| `revit_wall_rectangle` | Create 4 joined walls around a rectangle (mm) on a level - quick rooms, cores, boundary walls. | yes |
| `revit_wall_set_height` | Set unconnected height (mm) or a top level (+ offset) for walls. | yes |
| `revit_wall_type_create` | Create a new basic wall type by duplicating one and setting layer thicknesses (mm) and optional materials, e.g. 13mm plaster + 230mm brick + 13mm plaster. | yes |
| `revit_walls_report` | Every wall with type, level, length (mm), height, area (m2), volume (m3), structural flag and location line - grouped totals by type. | no |
| `revit_warnings` | Revit warnings with failing element ids. | no |
| `revit_warnings_grouped` | Model warnings grouped by message with counts and up to 20 element ids each. | no |
| `revit_window_place` | Place a window in a wall at a distance from the wall start (mm) with sill height. | yes |
| `revit_window_wall_ratio` | Exterior wall area, window area and WWR per facade orientation (N/E/S/W, using true north) - energy & daylight design. | no |
| `revit_workset_create` | Create user worksets (enables worksharing first if needed). | yes |
| `revit_workset_move` | Moves elements to a user workset (by name) - fixes elements modelled on the wrong workset. | yes |
| `revit_worksets` | User worksets with owner, open/editable state, default visibility and element counts. | no |
| `revit_zoom` | Zoom the active view to fit, or to elements. | no |

### rhino

| Tool | What it does | Changes model |
|---|---|---|
| `grasshopper_open` | Load Grasshopper (if needed), open a .gh / .ghx definition and optionally show or hide the editor. | yes |
| `grasshopper_run` | Assign values to named parameters (by nickname), recompute the solution and optionally bake named outputs onto a layer. | yes |
| `rhino_add` | Add point, line, polyline, circle, arc (3 points), box (two corners), sphere, cylinder, text dot or text through RhinoScript; returns the object ids. | yes |
| `rhino_blocks` | List block definitions with instance counts, insert a block at points, or explode block instances. | yes |
| `rhino_capture_view` | Save the active viewport to an image file (PNG/JPG), list named views or restore one. | yes |
| `rhino_command` | Run any Rhino command macro exactly as typed on the command line, e.g. "_-Line 0,0,0 10,0,0" or "_-SaveAs D:\\x.3dm _Enter". Prefix with - for no dialogs. | yes |
| `rhino_file` | Open, save, save as, import or export (3dm, dwg, dxf, ifc, step, stl, obj, fbx, skp, ...) the Rhino document. Export uses the selected objects, or ids. | yes |
| `rhino_info` | Rhino version, document path, units, layers and object counts. | no |
| `rhino_layers` | List layers (name, color, visible, locked, object count) or add / delete / set current / show / hide / lock / unlock / color a layer. | yes |
| `rhino_measure` | Area and centroid of surfaces / meshes / closed curves, volume of closed solids, and length of curves. | no |
| `rhino_objects` | List objects (id, type, layer, name, bounding box) filtered by layer / type / selection, or select, delete, move, copy, hide, show, rename, set layer. | yes |
| `rhino_script` | Call any RhinoScript method on the running Rhino (AddLine, AddBox, ObjectsByLayer, ObjectName, BoundingBox, LayerColor, ExportFile, ...). Arrays are passed as JSON arrays. | yes |
| `rhino_user_text` | Read or write object user text (key/value data used for BIM attributes), names and display colour of Rhino objects. | yes |

### robot

| Tool | What it does | Changes model |
|---|---|---|
| `robot_bars` | Create bars between node pairs [[start,end],...] with optional section and material labels, list bars (nodes, section, material, length) or delete. | yes |
| `robot_calculate` | Run the structural calculation (all cases). Returns success flag and result availability. | yes |
| `robot_cases` | List load cases (number, name, nature, type) or create a simple case (dead, live, wind, snow, temperature, accidental, seismic). | yes |
| `robot_combinations` | Create a ULS / SLS / accidental combination from {caseNumber: factor}. | yes |
| `robot_info` | Robot Structural Analysis version, open project, structure type and node/bar/case counts. | no |
| `robot_labels` | List labels of a type (section, material, support, release, thickness) or assign an existing / database label to bars. | yes |
| `robot_loads` | Add a load record to a simple case: self-weight, nodal force/moment, bar uniform load or panel uniform pressure. Units are Robot's current units (default kN, m). | yes |
| `robot_nodes` | Create nodes from [[x,y,z],...] (metres, auto-numbered or numbered), list nodes with coordinates and support labels, or delete by selection. | yes |
| `robot_project` | Create (frame2d, frame3d, shell, truss2d, truss3d, grillage, building), open, save, save as or close a Robot project. | yes |
| `robot_reaction_sum` | Sum of support reactions (FX, FY, FZ) per case - equilibrium check against applied loads. | no |
| `robot_releases` | Create a bar release label (pinned-pinned, pinned-fixed, or custom start/end fixities) and assign it to bars. | yes |
| `robot_result_envelope` | Maximum / minimum bar forces (FX, FZ, MY ...) and nodal displacements over all bars / nodes for one or more cases - the governing values for design checks. | no |
| `robot_results` | Reactions, nodal displacements or bar forces (FX..MZ at relative positions) for a case or combination. | no |
| `robot_supports` | Create a nodal support label (fixed, pinned, roller or custom UX..RZ fixities) and assign it to nodes. | yes |
| `robot_view` | Redraw / zoom-all the active Robot view and optionally save a screen capture of it to a file (bmp/jpg/png). | yes |

### scan

| Tool | What it does | Changes model |
|---|---|---|
| `scan_detect_levels` | Find horizontal surfaces (floor tops, slab undersides, roof) in the point cloud and suggest level elevations; compares with existing Revit levels. | no |
| `scan_detect_walls` | Horizontal slice of the scan above a level -> wall faces (RANSAC), paired into walls with centreline + thickness, plus columns (rectangular/round). Writes an SVG plan preview for inspectors. | no |
| `scan_deviation` | Measure how far scan points are from each element's faces (mean/median/p95/max mm), pass/fail against tolerance; stored on the element for inspectors. | yes |
| `scan_link_pointcloud` | Link a ReCap .rcp project or .rcs scan into the open Revit model (origin to origin). | yes |
| `scan_pointclouds` | Point cloud instances in the open Revit model with extents (mm). | no |
| `scan_sample_points` | Read points from the linked point cloud inside a box (mm). Returns statistics and a preview; full points are saved to an .xyz file (path returned). | no |
| `scan_to_bim` | Full scan-to-BIM in Revit: detect levels -> per storey detect walls, columns and slab outline -> (dryRun=false) create levels, walls, columns, floors (one undo step per storey, all stamped AI/unverified) -> deviation QA against the scan. ALWAYS run dryRun=true first and show the plan. Output geometry is LOD 300-350; LOD 500 requires inspector verification. | yes |

### ui

| Tool | What it does | Changes model |
|---|---|---|
| `screenshot` | Capture the main window of an application (or the whole screen) as an image so you can SEE the current state: model view, dialogs, results. | no |

