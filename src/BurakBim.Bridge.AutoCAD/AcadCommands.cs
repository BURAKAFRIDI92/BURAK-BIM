// BURAK BIM - AutoCAD-family bridge methods (drawing units on the wire).
using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.Colors;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using BurakBim.Shared;
using AcApp = Autodesk.AutoCAD.ApplicationServices.Application;

namespace BurakBim.Bridge.AutoCAD
{
    public static class AcadCommands
    {
        const string RegApp = "BURAKBIM";

        static void Add(BridgeServer s, string name, string desc, bool writes, BridgeHandler h)
        {
            s.Register(new BridgeCommand { Name = name, Description = desc, Writes = writes, Handler = h });
        }

        public static void Register(BridgeServer s, AcadHost host)
        {
            Add(s, "acad.info", "Product, version, open drawings, units and current settings.", false, Info);
            Add(s, "acad.command", "Run ANY command line input (built-in, vertical, LISP-defined, add-in): e.g. \"_.LINE 0,0 100,0 \" or a macro.", true, Command);
            Add(s, "acad.sysvar", "Get or set a system variable.", true, SysVar);
            Add(s, "acad.entities.query", "Entities by type (DXF name), layer, space, with geometry.", false, Query);
            Add(s, "acad.entities.modify", "Change layer/color, move, delete entities by handle.", true, Modify);
            Add(s, "acad.draw", "Create line, polyline, rectangle, circle, arc, text, mtext, block insert (batch with items[]).", true, Draw);
            Add(s, "acad.layers", "List layers, or create/modify one (color, on/off, freeze, lock, current).", true, Layers);
            Add(s, "acad.blocks", "Block definitions with insert counts.", false, Blocks);
            Add(s, "acad.doc", "open | save | saveas | new | close | list drawings.", false, DocOps);
            Add(s, "acad.civil", "Civil 3D objects: alignments, surfaces, profiles, corridors, pipe networks, cogo points, sites.", false, Civil);
            Add(s, "acad.inspector.list", "Entities created by AI (BURAKBIM xdata stamp).", false, InspectorList);
        }

        public static void Send(string text, bool echo)
        {
            var doc = AcadHost.RequireDoc();
            if (!text.EndsWith(" ") && !text.EndsWith("\n")) text += "\n";
            doc.SendStringToExecute(text, true, false, echo);
        }

        static object Info(Dictionary<string, object> p)
        {
            var docs = new List<object>();
            foreach (Document d in AcApp.DocumentManager)
                docs.Add(Json.O("name", d.Name, "readOnly", d.IsReadOnly, "active", d == AcApp.DocumentManager.MdiActiveDocument));
            var doc = AcApp.DocumentManager.MdiActiveDocument;
            return Json.O("product", AcadPlugin.Host.Product, "acadVersion", AcApp.Version.ToString(), "drawings", docs,
                "active", doc == null ? null : doc.Name,
                "insunits", Var("INSUNITS"), "measurement", Var("MEASUREMENT"), "clayer", Var("CLAYER"),
                "civil3d", AcadHost.CivilDoc() != null, "scripting", ScriptRunner.Available,
                "units", "Coordinates are in drawing units (see insunits: 4 = mm, 6 = m, 1 = inch, 2 = ft).");
        }

        static object Var(string n) { try { return AcApp.GetSystemVariable(n); } catch { return null; } }

        static object Command(Dictionary<string, object> p)
        {
            var cmd = Json.Str(p, "command");
            if (string.IsNullOrWhiteSpace(cmd)) throw new BridgeException("command is empty");
            if (Json.Bool(p, "cancelFirst", true)) cmd = "\x03\x03" + cmd;
            Send(cmd, Json.Bool(p, "echo", true));
            return Json.O("sent", cmd.Replace("\x03", "^C"),
                "note", "Queued to the AutoCAD command line; it runs as soon as AutoCAD is idle. For a synchronous result use autocad_lisp (COM) or query the drawing afterwards.");
        }

        static object SysVar(Dictionary<string, object> p)
        {
            var name = Json.Str(p, "name");
            var v = Json.Get(p, "value");
            if (v != null)
            {
                var cur = AcApp.GetSystemVariable(name);
                object conv = v;
                if (cur is short) conv = (short)Json.ToDouble(v);
                else if (cur is int) conv = (int)Json.ToDouble(v);
                else if (cur is double) conv = Json.ToDouble(v);
                else if (cur is string) conv = Convert.ToString(v);
                using (AcadHost.RequireDoc().LockDocument()) AcApp.SetSystemVariable(name, conv);
            }
            return Json.O("name", name, "value", Reflector.Describe(AcApp.GetSystemVariable(name)));
        }

        internal static List<object> Pt(Point3d p) { return new List<object> { Math.Round(p.X, 6), Math.Round(p.Y, 6), Math.Round(p.Z, 6) }; }
        static Point3d P3(object v)
        {
            var a = Json.Vec(v);
            if (a == null) throw new BridgeException("Expected a point [x,y] or [x,y,z].");
            return new Point3d(a[0], a[1], a[2]);
        }

        static Dictionary<string, object> Describe(Entity e, bool geometry)
        {
            var d = Json.O("handle", e.Handle.ToString(), "type", e.GetRXClass().DxfName, "layer", e.Layer,
                "color", e.Color.ColorIndex, "linetype", e.Linetype);
            if (geometry)
            {
                try { var ex = e.GeometricExtents; d["extents"] = Json.O("min", Pt(ex.MinPoint), "max", Pt(ex.MaxPoint)); } catch { }
                var line = e as Line;
                if (line != null) { d["start"] = Pt(line.StartPoint); d["end"] = Pt(line.EndPoint); d["length"] = line.Length; }
                var c = e as Circle;
                if (c != null) { d["center"] = Pt(c.Center); d["radius"] = c.Radius; }
                var a = e as Arc;
                if (a != null) { d["center"] = Pt(a.Center); d["radius"] = a.Radius; d["startAngle"] = a.StartAngle; d["endAngle"] = a.EndAngle; }
                var pl = e as Polyline;
                if (pl != null)
                {
                    var v = new List<object>();
                    for (int i = 0; i < pl.NumberOfVertices; i++) v.Add(Pt(pl.GetPoint3dAt(i)));
                    d["vertices"] = v; d["closed"] = pl.Closed; d["length"] = pl.Length;
                    try { d["area"] = pl.Area; } catch { }
                }
                var t = e as DBText;
                if (t != null) { d["text"] = t.TextString; d["position"] = Pt(t.Position); d["height"] = t.Height; }
                var mt = e as MText;
                if (mt != null) { d["text"] = mt.Contents; d["position"] = Pt(mt.Location); }
                var br = e as BlockReference;
                if (br != null)
                {
                    d["block"] = br.Name; d["position"] = Pt(br.Position); d["rotation"] = br.Rotation;
                    var atts = new Dictionary<string, object>();
                    var tr = e.Database.TransactionManager.TopTransaction;
                    if (tr != null)
                        foreach (ObjectId aid in br.AttributeCollection)
                        {
                            var ar = tr.GetObject(aid, OpenMode.ForRead) as AttributeReference;
                            if (ar != null) atts[ar.Tag] = ar.TextString;
                        }
                    if (atts.Count > 0) d["attributes"] = atts;
                }
            }
            var xd = e.GetXDataForApplication(RegApp);
            if (xd != null)
            {
                var vals = xd.AsArray();
                if (vals.Length > 1) d["burakbim"] = vals[1].Value;
            }
            return d;
        }

        static object Query(Dictionary<string, object> p)
        {
            var doc = AcadHost.RequireDoc();
            var db = doc.Database;
            var types = Json.StrList(p, "types").Select(x => x.ToUpperInvariant()).ToList();
            var layer = Json.Str(p, "layer");
            int limit = (int)Json.Int(p, "limit", 500);
            bool geo = Json.Bool(p, "geometry", true);
            var space = Json.Str(p, "space", "model");
            var result = new List<object>();
            int total = 0;
            var counts = new Dictionary<string, object>();
            using (var tr = db.TransactionManager.StartTransaction())
            {
                var btr = (BlockTableRecord)tr.GetObject(space == "paper" ? SymbolUtilityServices.GetBlockPaperSpaceId(db) : SymbolUtilityServices.GetBlockModelSpaceId(db), OpenMode.ForRead);
                foreach (ObjectId id in btr)
                {
                    var dxf = id.ObjectClass.DxfName;
                    if (types.Count > 0 && !types.Contains(dxf)) continue;
                    var e = tr.GetObject(id, OpenMode.ForRead) as Entity;
                    if (e == null) continue;
                    if (!string.IsNullOrEmpty(layer) && !string.Equals(e.Layer, layer, StringComparison.OrdinalIgnoreCase)) continue;
                    total++;
                    counts[dxf] = (counts.ContainsKey(dxf) ? (int)counts[dxf] : 0) + 1;
                    if (result.Count < limit) result.Add(Describe(e, geo));
                }
                tr.Commit();
            }
            return Json.O("total", total, "byType", counts, "entities", result);
        }

        static IEnumerable<ObjectId> ByHandles(Database db, Dictionary<string, object> p)
        {
            foreach (var h in Json.StrList(p, "handles"))
            {
                ObjectId id;
                try { id = db.GetObjectId(false, new Handle(Convert.ToInt64(h, 16)), 0); }
                catch { throw new BridgeException("Handle " + h + " not found."); }
                yield return id;
            }
        }

        static object Modify(Dictionary<string, object> p)
        {
            var doc = AcadHost.RequireDoc();
            var db = doc.Database;
            int n = 0;
            using (doc.LockDocument())
            using (var tr = db.TransactionManager.StartTransaction())
            {
                var move = Json.Vec(Json.Get(p, "move"));
                foreach (var id in ByHandles(db, p).ToList())
                {
                    var e = (Entity)tr.GetObject(id, OpenMode.ForWrite);
                    if (Json.Bool(p, "delete")) { e.Erase(); n++; continue; }
                    var layer = Json.Str(p, "layer"); if (!string.IsNullOrEmpty(layer)) { EnsureLayer(db, tr, layer, -1); e.Layer = layer; }
                    var color = Json.Get(p, "color"); if (color != null) e.Color = Color.FromColorIndex(ColorMethod.ByAci, (short)Json.ToDouble(color));
                    if (move != null) e.TransformBy(Matrix3d.Displacement(new Vector3d(move[0], move[1], move[2])));
                    n++;
                }
                tr.Commit();
            }
            return Json.O("modified", n);
        }

        static void EnsureLayer(Database db, Transaction tr, string name, int color)
        {
            var lt = (LayerTable)tr.GetObject(db.LayerTableId, OpenMode.ForRead);
            if (lt.Has(name)) return;
            lt.UpgradeOpen();
            var ltr = new LayerTableRecord { Name = name };
            if (color > 0) ltr.Color = Color.FromColorIndex(ColorMethod.ByAci, (short)color);
            lt.Add(ltr);
            tr.AddNewlyCreatedDBObject(ltr, true);
        }

        static void EnsureRegApp(Database db, Transaction tr)
        {
            var rat = (RegAppTable)tr.GetObject(db.RegAppTableId, OpenMode.ForRead);
            if (rat.Has(RegApp)) return;
            rat.UpgradeOpen();
            var r = new RegAppTableRecord { Name = RegApp };
            rat.Add(r);
            tr.AddNewlyCreatedDBObject(r, true);
        }

        static void Stamp(Entity e)
        {
            var info = "AI|" + RequestContext.Get("client", "ai") + "|" + DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ") + "|" + Environment.UserName + "|unverified";
            if (info.Length > 250) info = info.Substring(0, 250);
            e.XData = new ResultBuffer(new TypedValue((int)DxfCode.ExtendedDataRegAppName, RegApp), new TypedValue((int)DxfCode.ExtendedDataAsciiString, info));
        }

        static object Draw(Dictionary<string, object> p)
        {
            var doc = AcadHost.RequireDoc();
            var db = doc.Database;
            var items = Json.Arr(p, "items");
            var list = items != null && items.Count > 0 ? items.OfType<Dictionary<string, object>>().ToList() : new List<Dictionary<string, object>> { p };
            var created = new List<object>();
            using (doc.LockDocument())
            using (var tr = db.TransactionManager.StartTransaction())
            {
                EnsureRegApp(db, tr);
                var ms = (BlockTableRecord)tr.GetObject(SymbolUtilityServices.GetBlockModelSpaceId(db), OpenMode.ForWrite);
                foreach (var it in list)
                {
                    var q = new Dictionary<string, object>(p);
                    foreach (var kv in it) q[kv.Key] = kv.Value;
                    var kind = (Json.Str(q, "kind") ?? "").ToLowerInvariant();
                    Entity e;
                    switch (kind)
                    {
                        case "line": e = new Line(P3(Json.Get(q, "start")), P3(Json.Get(q, "end"))); break;
                        case "circle": e = new Circle(P3(Json.Get(q, "center")), Vector3d.ZAxis, Json.Num(q, "radius", 1)); break;
                        case "arc":
                            e = new Arc(P3(Json.Get(q, "center")), Json.Num(q, "radius", 1), Json.Num(q, "startAngle") * Math.PI / 180, Json.Num(q, "endAngle", 90) * Math.PI / 180); break;
                        case "polyline":
                        case "rectangle":
                            {
                                var pts = new List<double[]>();
                                if (kind == "rectangle")
                                {
                                    var a = Json.Vec(Json.Get(q, "corner1")); var b = Json.Vec(Json.Get(q, "corner2"));
                                    pts.Add(new[] { a[0], a[1] }); pts.Add(new[] { b[0], a[1] }); pts.Add(new[] { b[0], b[1] }); pts.Add(new[] { a[0], b[1] });
                                }
                                else foreach (var v in Json.Arr(q, "points") ?? new List<object>()) pts.Add(Json.Vec(v));
                                var pl = new Polyline();
                                for (int i = 0; i < pts.Count; i++) pl.AddVertexAt(i, new Point2d(pts[i][0], pts[i][1]), 0, 0, 0);
                                pl.Closed = kind == "rectangle" || Json.Bool(q, "closed");
                                if (Json.Get(q, "elevation") != null) pl.Elevation = Json.Num(q, "elevation");
                                e = pl; break;
                            }
                        case "text":
                            e = new DBText { Position = P3(Json.Get(q, "position")), TextString = Json.Str(q, "text", ""), Height = Json.Num(q, "height", 2.5), Rotation = Json.Num(q, "rotation") * Math.PI / 180 };
                            break;
                        case "mtext":
                            e = new MText { Location = P3(Json.Get(q, "position")), Contents = Json.Str(q, "text", ""), TextHeight = Json.Num(q, "height", 2.5), Width = Json.Num(q, "width", 0) };
                            break;
                        case "insert":
                        case "block":
                            {
                                var bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
                                var name = Json.Str(q, "block");
                                if (!bt.Has(name)) throw new BridgeException("Block '" + name + "' is not defined in this drawing.");
                                var br = new BlockReference(P3(Json.Get(q, "position")), bt[name]);
                                br.Rotation = Json.Num(q, "rotation") * Math.PI / 180;
                                var sc = Json.Num(q, "scale", 1);
                                br.ScaleFactors = new Scale3d(sc);
                                e = br; break;
                            }
                        default: throw new BridgeException("kind must be line, polyline, rectangle, circle, arc, text, mtext or insert. For anything else use autocad_command.");
                    }
                    var layer = Json.Str(q, "layer");
                    if (!string.IsNullOrEmpty(layer)) { EnsureLayer(db, tr, layer, -1); e.Layer = layer; }
                    var color = Json.Get(q, "color"); if (color != null) e.Color = Color.FromColorIndex(ColorMethod.ByAci, (short)Json.ToDouble(color));
                    ms.AppendEntity(e);
                    tr.AddNewlyCreatedDBObject(e, true);
                    Stamp(e);
                    var bref = e as BlockReference;
                    if (bref != null)
                    {
                        var def = (BlockTableRecord)tr.GetObject(bref.BlockTableRecord, OpenMode.ForRead);
                        var attVals = Json.Obj(q, "attributes") ?? new Dictionary<string, object>();
                        foreach (ObjectId id in def)
                        {
                            var ad = tr.GetObject(id, OpenMode.ForRead) as AttributeDefinition;
                            if (ad == null || ad.Constant) continue;
                            var ar = new AttributeReference();
                            ar.SetAttributeFromBlock(ad, bref.BlockTransform);
                            if (attVals.ContainsKey(ad.Tag)) ar.TextString = Convert.ToString(attVals[ad.Tag]);
                            bref.AttributeCollection.AppendAttribute(ar);
                            tr.AddNewlyCreatedDBObject(ar, true);
                        }
                    }
                    created.Add(Json.O("handle", e.Handle.ToString(), "kind", kind));
                }
                tr.Commit();
            }
            return Json.O("created", created);
        }

        static object Layers(Dictionary<string, object> p)
        {
            var doc = AcadHost.RequireDoc();
            var db = doc.Database;
            var name = Json.Str(p, "name");
            using (doc.LockDocument())
            using (var tr = db.TransactionManager.StartTransaction())
            {
                if (!string.IsNullOrEmpty(name))
                {
                    EnsureLayer(db, tr, name, (int)Json.Int(p, "color", -1));
                    var lt = (LayerTable)tr.GetObject(db.LayerTableId, OpenMode.ForRead);
                    var ltr = (LayerTableRecord)tr.GetObject(lt[name], OpenMode.ForWrite);
                    if (Json.Get(p, "color") != null) ltr.Color = Color.FromColorIndex(ColorMethod.ByAci, (short)Json.Int(p, "color"));
                    if (Json.Get(p, "on") != null) ltr.IsOff = !Json.Bool(p, "on");
                    if (Json.Get(p, "frozen") != null) ltr.IsFrozen = Json.Bool(p, "frozen");
                    if (Json.Get(p, "locked") != null) ltr.IsLocked = Json.Bool(p, "locked");
                    if (Json.Bool(p, "current")) db.Clayer = ltr.ObjectId;
                }
                var list = new List<object>();
                var table = (LayerTable)tr.GetObject(db.LayerTableId, OpenMode.ForRead);
                foreach (ObjectId id in table)
                {
                    var l = (LayerTableRecord)tr.GetObject(id, OpenMode.ForRead);
                    list.Add(Json.O("name", l.Name, "color", l.Color.ColorIndex, "on", !l.IsOff, "frozen", l.IsFrozen, "locked", l.IsLocked, "current", id == db.Clayer));
                }
                tr.Commit();
                return list;
            }
        }

        static object Blocks(Dictionary<string, object> p)
        {
            var db = AcadHost.RequireDoc().Database;
            var list = new List<object>();
            using (var tr = db.TransactionManager.StartTransaction())
            {
                var bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
                foreach (ObjectId id in bt)
                {
                    var b = (BlockTableRecord)tr.GetObject(id, OpenMode.ForRead);
                    if (b.IsLayout || b.IsAnonymous) continue;
                    list.Add(Json.O("name", b.Name, "inserts", b.GetBlockReferenceIds(true, false).Count, "isXref", b.IsFromExternalReference, "hasAttributes", b.HasAttributeDefinitions));
                }
                tr.Commit();
            }
            return list;
        }

        static object DocOps(Dictionary<string, object> p)
        {
            var op = (Json.Str(p, "op", "list")).ToLowerInvariant();
            var path = Json.Str(p, "path");
            var dm = AcApp.DocumentManager;
            switch (op)
            {
                case "open":
                    {
                        // DocumentCollectionExtension.Open(path, readOnly) - resolved late so it fits every release
                        var d = Reflector.CallMethod(dm, dm.GetType(), "Open", new List<object> { path, Json.Bool(p, "readOnly") }, false) as Document;
                        if (d != null) dm.MdiActiveDocument = d;
                        return Json.O("opened", path);
                    }
                case "new":
                    {
                        var d = Reflector.CallMethod(dm, dm.GetType(), "Add", new List<object> { Json.Str(p, "template", "acadiso.dwt") }, false) as Document;
                        if (d != null) dm.MdiActiveDocument = d;
                        return Json.O("created", d == null ? null : d.Name);
                    }
                case "save":
                case "saveas":
                    {
                        var doc = AcadHost.RequireDoc();
                        var target = string.IsNullOrEmpty(path) ? doc.Name : path;
                        using (doc.LockDocument()) doc.Database.SaveAs(target, DwgVersion.Current);
                        return Json.O("saved", target);
                    }
                case "close":
                    {
                        var doc = AcadHost.RequireDoc();
                        Reflector.CallMethod(doc, doc.GetType(), Json.Bool(p, "save") ? "CloseAndSave" : "CloseAndDiscard",
                            Json.Bool(p, "save") ? new List<object> { doc.Name } : new List<object>(), false);
                        return Json.O("closed", true);
                    }
                default:
                    {
                        var list = new List<object>();
                        foreach (Document d in dm) list.Add(d.Name);
                        return list;
                    }
            }
        }

        // Civil 3D is reached late-bound so this bridge also loads in plain AutoCAD.
        static object Civil(Dictionary<string, object> p)
        {
            var civil = AcadHost.CivilDoc();
            if (civil == null) throw new BridgeException("This is not Civil 3D (CivilApplication not loaded).");
            var kind = (Json.Str(p, "kind", "alignments")).ToLowerInvariant();
            string method;
            switch (kind)
            {
                case "alignments": method = "GetAlignmentIds"; break;
                case "surfaces": method = "GetSurfaceIds"; break;
                case "sites": method = "GetSiteIds"; break;
                case "pipenetworks": method = "GetPipeNetworkIds"; break;
                case "corridors": method = "CorridorCollection"; break;
                case "points": method = "CogoPoints"; break;
                case "profiles": method = "GetAlignmentIds"; break;
                default: throw new BridgeException("kind: alignments, profiles, surfaces, sites, pipenetworks, corridors, points.");
            }
            var t = civil.GetType();
            var src = t.GetMethod(method, Type.EmptyTypes) != null ? t.GetMethod(method, Type.EmptyTypes).Invoke(civil, null) : t.GetProperty(method).GetValue(civil, null);
            var db = AcadHost.RequireDoc().Database;
            var result = new List<object>();
            using (var tr = db.TransactionManager.StartTransaction())
            {
                foreach (var o in (System.Collections.IEnumerable)src)
                {
                    if (!(o is ObjectId)) continue;
                    var obj = tr.GetObject((ObjectId)o, OpenMode.ForRead);
                    var d = Json.O("handle", obj.Handle.ToString(), "type", obj.GetType().Name);
                    foreach (var prop in new[] { "Name", "Description", "Length", "StartingStation", "EndingStation", "Style", "PointNumber", "Easting", "Northing", "Elevation", "RawDescription" })
                    {
                        try { var pi = obj.GetType().GetProperty(prop); if (pi != null) { var v = pi.GetValue(obj, null); if (v != null) d[prop] = v is double || v is string || v is int || v is uint ? v : v.ToString(); } } catch { }
                    }
                    if (kind == "profiles")
                    {
                        try
                        {
                            var ids = obj.GetType().GetMethod("GetProfileIds").Invoke(obj, null) as System.Collections.IEnumerable;
                            var profs = new List<object>();
                            foreach (var pid in ids) { var pr = tr.GetObject((ObjectId)pid, OpenMode.ForRead); profs.Add(pr.GetType().GetProperty("Name").GetValue(pr, null)); }
                            d["profiles"] = profs;
                        }
                        catch { }
                    }
                    if (kind == "surfaces")
                    {
                        try
                        {
                            var props = obj.GetType().GetMethod("GetGeneralProperties", Type.EmptyTypes).Invoke(obj, null);
                            foreach (var n in new[] { "MinimumElevation", "MaximumElevation", "MeanElevation", "NumberOfPoints" })
                            { var pi = props.GetType().GetProperty(n); if (pi != null) d[n] = pi.GetValue(props, null); }
                        }
                        catch { }
                    }
                    result.Add(d);
                    if (result.Count >= Json.Int(p, "limit", 1000)) break;
                }
                tr.Commit();
            }
            return Json.O("kind", kind, "count", result.Count, "items", result,
                "hint", "Use autocad_script for full Civil 3D API (variable civil = CivilDocument).");
        }

        static object InspectorList(Dictionary<string, object> p)
        {
            var db = AcadHost.RequireDoc().Database;
            var list = new List<object>();
            using (var tr = db.TransactionManager.StartTransaction())
            {
                var ms = (BlockTableRecord)tr.GetObject(SymbolUtilityServices.GetBlockModelSpaceId(db), OpenMode.ForRead);
                foreach (ObjectId id in ms)
                {
                    var e = tr.GetObject(id, OpenMode.ForRead) as Entity;
                    if (e == null) continue;
                    var xd = e.GetXDataForApplication(RegApp);
                    if (xd == null) continue;
                    list.Add(Describe(e, false));
                }
                tr.Commit();
            }
            return Json.O("count", list.Count, "entities", list);
        }
    }
}
