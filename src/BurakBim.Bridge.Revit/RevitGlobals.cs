// BURAK BIM - globals and helpers available to Revit C# scripts and tool-pack snippets.
using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.ApplicationServices;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using BurakBim.Shared;

namespace BurakBim.Bridge.Revit
{
    public class RevitScriptGlobals : ScriptGlobals
    {
        public UIApplication uiapp;
        public Application app;
        public UIDocument uidoc;
        public Document doc;

        // ---- units (wire = millimetres, Revit internal = feet) ----
        public double Ft(double mm) { return U.Ft(mm); }
        public double Mm(double ft) { return U.Mm(ft); }
        public double SqM(double sqft) { return Math.Round(sqft * 0.09290304, 3); }
        public double CuM(double cuft) { return Math.Round(cuft * 0.028316846592, 3); }
        public double Deg(double rad) { return Math.Round(rad * 180.0 / Math.PI, 4); }
        public double Rad(double deg) { return deg * Math.PI / 180.0; }
        /// <summary>Point argument [x,y,z] in mm -> XYZ (feet). Null when missing.</summary>
        public XYZ P(string k) { var v = Raw(k); return v == null ? null : U.P(v); }
        public XYZ Pt(object v) { return U.P(v); }
        public List<object> Out(XYZ p) { return p == null ? null : U.Out(p); }

        // ---- ids & elements ----
        public ElementId Id(long v) { return Ids.Make(v); }
        public long IdOf(ElementId id) { return Ids.Of(id); }
        public long IdOf(Element e) { return Ids.Of(e.Id); }
        public Element El(long id) { return doc.GetElement(Ids.Make(id)); }
        public Element ElArg(string k)
        {
            var v = Raw(k);
            if (v == null) Fail("Argument '" + k + "' (element id) is required.");
            var e = El((long)Json.ToDouble(v));
            if (e == null) Fail("Element " + v + " not found.");
            return e;
        }
        public List<object> IdList(IEnumerable<ElementId> ids) { return ids.Select(i => (object)Ids.Of(i)).ToList(); }
        public List<object> IdList(IEnumerable<Element> els) { return els.Select(e => (object)Ids.Of(e.Id)).ToList(); }

        /// <summary>Elements from an id-list argument, or the current Revit selection when the argument is empty.</summary>
        public List<Element> Elems(string k = "ids", bool required = true)
        {
            var ids = L(k);
            if (ids.Count == 0 && uidoc != null) ids = uidoc.Selection.GetElementIds().Select(Ids.Of).ToList();
            var list = ids.Select(i => El(i)).Where(e => e != null).ToList();
            if (required && list.Count == 0) Fail("Pass '" + k + "' (element ids) or select elements in Revit.");
            return list;
        }
        public List<ElementId> ElemIds(string k = "ids", bool required = true) { return Elems(k, required).Select(e => e.Id).ToList(); }

        public FilteredElementCollector Col() { return new FilteredElementCollector(doc); }
        public FilteredElementCollector ColView(View v) { return new FilteredElementCollector(doc, v.Id); }
        public IEnumerable<T> All<T>() where T : Element { return new FilteredElementCollector(doc).OfClass(typeof(T)).Cast<T>(); }
        public IEnumerable<Element> OfCat(BuiltInCategory c) { return new FilteredElementCollector(doc).OfCategory(c).WhereElementIsNotElementType(); }
        public IEnumerable<Element> TypesOfCat(BuiltInCategory c) { return new FilteredElementCollector(doc).OfCategory(c).WhereElementIsElementType(); }

        public Category Cat(string name) { return RevitCommands.FindCategory(doc, name); }
        public Level Lvl(string nameOrNull) { return RevitCommands.FindLevel(doc, nameOrNull); }
        public Level LvlArg(string k = "level") { return RevitCommands.FindLevel(doc, Raw(k)); }
        public IEnumerable<Level> Levels() { return RevitCommands.Levels(doc); }
        public T TypeByName<T>(string name, BuiltInCategory? cat = null) where T : ElementType { return RevitCommands.FindType<T>(doc, name, cat); }

        public View ViewArg(string k = "view")
        {
            var key = S(k);
            if (string.IsNullOrEmpty(key)) return doc.ActiveView;
            var views = All<View>().Where(v => !v.IsTemplate).ToList();
            var hit = views.FirstOrDefault(v => Ids.Of(v.Id).ToString() == key)
                   ?? views.FirstOrDefault(v => string.Equals(v.Name, key, StringComparison.OrdinalIgnoreCase))
                   ?? views.OfType<ViewSheet>().FirstOrDefault(v => string.Equals(v.SheetNumber, key, StringComparison.OrdinalIgnoreCase));
            if (hit == null) Fail("View '" + key + "' not found.");
            return hit;
        }

        public Parameter Prm(Element e, string name) { return RevitCommands.FindParam(e, name); }
        public string PrmText(Element e, string name) { return RevitCommands.ParamText(RevitCommands.FindParam(e, name)); }
        public void SetPrm(Element e, string name, object value)
        {
            var p = RevitCommands.FindParam(e, name);
            if (p == null) Fail("Parameter '" + name + "' not found on element " + Ids.Of(e.Id));
            RevitCommands.SetParam(p, value);
        }
        public Dictionary<string, object> Desc(Element e, bool withParams = false) { return RevitCommands.Describe(e, withParams); }
        public List<object> DescAll(IEnumerable<Element> els, int limit = 500) { return els.Take(limit).Select(e => (object)RevitCommands.Describe(e, false)).ToList(); }
        public string Name(ElementId id) { var e = id == null || id == ElementId.InvalidElementId ? null : doc.GetElement(id); return e == null ? null : e.Name; }
        public Color ColorArg(string k, Color def)
        {
            var v = Raw(k);
            if (v == null) return def;
            var s = v as string;
            if (s != null && s.StartsWith("#") && s.Length == 7)
                return new Color(Convert.ToByte(s.Substring(1, 2), 16), Convert.ToByte(s.Substring(3, 2), 16), Convert.ToByte(s.Substring(5, 2), 16));
            var a = Json.Vec(v);
            if (a != null) return new Color((byte)a[0], (byte)a[1], (byte)a[2]);
            return def;
        }
        /// <summary>Family load options that overwrite the existing family and its parameter values.</summary>
        public IFamilyLoadOptions OverwriteOptions() { return new OverwriteFamilyLoadOptions(); }

        public ElementId SolidFill()
        {
            var f = All<FillPatternElement>().FirstOrDefault(x => x.GetFillPattern().IsSolidFill);
            return f == null ? ElementId.InvalidElementId : f.Id;
        }
    }

    public sealed class OverwriteFamilyLoadOptions : IFamilyLoadOptions
    {
        public bool OnFamilyFound(bool familyInUse, out bool overwriteParameterValues) { overwriteParameterValues = true; return true; }
        public bool OnSharedFamilyFound(Family sharedFamily, bool familyInUse, out FamilySource source, out bool overwriteParameterValues)
        { source = FamilySource.Family; overwriteParameterValues = true; return true; }
    }
}

namespace BurakBim.Bridge.Revit
{
    /// <summary>Copy/paste between documents: keep the destination's types when names clash.</summary>
    public sealed class UseDestinationTypes : Autodesk.Revit.DB.IDuplicateTypeNamesHandler
    {
        public Autodesk.Revit.DB.DuplicateTypeAction OnDuplicateTypeNamesFound(Autodesk.Revit.DB.DuplicateTypeNamesHandlerArgs args)
        { return Autodesk.Revit.DB.DuplicateTypeAction.UseDestinationTypes; }
    }
}
