// BURAK BIM - one Clash Detective and publish API for every Navisworks year.
// Autodesk changed these members, so nothing else in the bridge or the tool packs calls them directly:
//   2026: TestsEditResultStatus / AssignedTo / ApprovedBy take an Assignee instead of a string; PublishFile needs NwdExportOptions.
//   2027: DocumentClashTests.Tests, TestsAddCopy(ClashTest) and TestsRemove(ClashTest) were removed; tests live under TestsRoot.
// TestsRoot and the (GroupItem, SavedItem) overloads exist in 2022-2027, so they are used everywhere.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace BurakBim.Bridge.Navisworks
{
    public static class NavisCompat
    {
        static dynamic Part() { return NavisCommands.ClashData().TestsData; }
        static dynamic Root() { return Part().Value.TestsRoot; }

        /// <summary>Every clash test, including tests inside folders.</summary>
        public static List<dynamic> Tests()
        {
            var list = new List<dynamic>();
            Walk(Root(), list);
            return list;
        }

        static void Walk(dynamic folder, List<dynamic> into)
        {
            foreach (dynamic child in folder.Children)
            {
                if (((object)child).GetType().Name == "ClashTest") into.Add(child);
                else Walk(child, into);
            }
        }

        public static void AddTest(object test) { Part().TestsAddCopy(Root(), (dynamic)test); }

        public static void RemoveTest(object test)
        {
            dynamic parent = ((dynamic)test).Parent ?? Root();
            Part().TestsRemove(parent, (dynamic)test);
        }

        public static void SetStatus(object result, object status)
        {
            var part = (object)Part();
            var m = Method(part, "TestsEditResultStatus");
            var ps = m.GetParameters();
            // 2026+ also takes the assignee: keep the one the result already has
            var args = ps.Length >= 3 ? new[] { result, status, ToParam(ps[2].ParameterType, ((dynamic)result).AssignedTo) } : new[] { result, status };
            Invoke(m, part, args);
        }

        public static void SetAssignee(object result, string name)
        {
            var part = (object)Part();
            var m = Method(part, "TestsEditResultAssignedTo");
            Invoke(m, part, new[] { result, ToParam(m.GetParameters()[1].ParameterType, name) });
        }

        /// <summary>The name a clash result is assigned to: a string before 2026, an Assignee afterwards.</summary>
        public static string AssigneeOf(object result)
        {
            object a = ((dynamic)result).AssignedTo;
            if (a == null) return "";
            var s = a as string;
            if (s != null) return s;
            var p = a.GetType().GetProperty("DisplayName");
            return p != null ? Convert.ToString(p.GetValue(a, null)) ?? "" : a.ToString();
        }

        public static void Publish(object doc, string path, object properties)
        {
            var m = doc.GetType().GetMethods().First(x => x.Name == "PublishFile");
            var ps = m.GetParameters();
            // 2026+: PublishFile(path, NwdExportOptions, properties)
            Invoke(m, doc, ps.Length >= 3 ? new[] { path, Activator.CreateInstance(ps[1].ParameterType), properties } : new[] { path, properties });
        }

        /// <summary>Picture of a view. 2024+ adds enableSectioning, and the result shows the view's active sections.</summary>
        public static dynamic ViewImage(object view, object style, int width, int height)
        {
            var m = view.GetType().GetMethods().First(x => x.Name == "GenerateImage" && x.GetParameters().Length >= 3 && x.GetParameters()[3 % x.GetParameters().Length].ParameterType != typeof(double));
            var args = m.GetParameters().Length >= 4 ? new[] { style, width, height, (object)true } : new[] { style, width, height };
            try { return m.Invoke(view, args); }
            catch (TargetInvocationException ex) { throw ex.InnerException ?? ex; }
        }

        static MethodInfo Method(object target, string name)
        {
            return target.GetType().GetMethods().First(x => x.Name == name);
        }

        static object ToParam(Type type, object value)
        {
            if (value == null) return type == typeof(string) ? null : Activator.CreateInstance(type);
            if (type.IsInstanceOfType(value)) return value;
            if (type == typeof(string)) return value.ToString();
            return Activator.CreateInstance(type, value.ToString());
        }

        static void Invoke(MethodInfo m, object target, object[] args)
        {
            try { m.Invoke(target, args); }
            catch (TargetInvocationException ex) { throw ex.InnerException ?? ex; }
        }
    }
}
