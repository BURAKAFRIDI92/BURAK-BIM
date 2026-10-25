// BURAK BIM - dependency-free JSON reader/writer.
// Shared by the server (.NET 8) and every host bridge (.NET Framework 4.8 / .NET 8).
// Kept at C# 7.3 so it compiles in every Autodesk host build.
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace BurakBim.Shared
{
    /// <summary>
    /// Values map to: null, bool, long, double, string, List&lt;object&gt;, Dictionary&lt;string, object&gt;.
    /// </summary>
    public static class Json
    {
        public static object Parse(string text)
        {
            if (text == null) throw new ArgumentNullException("text");
            var p = new Parser(text);
            p.SkipWs();
            var v = p.ReadValue();
            p.SkipWs();
            if (!p.End) throw new FormatException("Trailing characters in JSON at " + p.Pos);
            return v;
        }

        public static Dictionary<string, object> ParseObject(string text)
        {
            var o = Parse(text) as Dictionary<string, object>;
            if (o == null) throw new FormatException("JSON value is not an object");
            return o;
        }

        public static string Serialize(object value, bool indent = false)
        {
            var sb = new StringBuilder(256);
            Write(sb, value, indent, 0, 0);
            return sb.ToString();
        }

        static void NewLine(StringBuilder sb, bool indent, int level)
        {
            if (!indent) return;
            sb.Append('\n');
            sb.Append(' ', level * 2);
        }

        static void Write(StringBuilder sb, object v, bool indent, int level, int depth)
        {
            if (depth > 64) { sb.Append("\"<max depth>\""); return; }
            if (v == null || v is DBNull) { sb.Append("null"); return; }
            var s = v as string;
            if (s != null) { WriteString(sb, s); return; }
            if (v is bool) { sb.Append((bool)v ? "true" : "false"); return; }
            if (v is char) { WriteString(sb, v.ToString()); return; }
            if (v is double)
            {
                var d = (double)v;
                if (double.IsNaN(d) || double.IsInfinity(d)) sb.Append("null");
                else sb.Append(d.ToString("R", CultureInfo.InvariantCulture));
                return;
            }
            if (v is float)
            {
                var f = (float)v;
                if (float.IsNaN(f) || float.IsInfinity(f)) sb.Append("null");
                else sb.Append(((double)f).ToString("R", CultureInfo.InvariantCulture));
                return;
            }
            if (v is decimal) { sb.Append(((decimal)v).ToString(CultureInfo.InvariantCulture)); return; }
            if (v is int || v is long || v is short || v is byte || v is uint || v is ulong || v is ushort || v is sbyte)
            {
                sb.Append(Convert.ToString(v, CultureInfo.InvariantCulture));
                return;
            }
            if (v is Enum) { WriteString(sb, v.ToString()); return; }
            if (v is DateTime) { WriteString(sb, ((DateTime)v).ToString("o", CultureInfo.InvariantCulture)); return; }
            if (v is Guid) { WriteString(sb, v.ToString()); return; }
            var raw = v as JsonRaw;
            if (raw != null) { sb.Append(raw.Text); return; }

            var dict = v as IDictionary;
            if (dict != null)
            {
                sb.Append('{');
                bool first = true;
                foreach (DictionaryEntry e in dict)
                {
                    if (!first) sb.Append(',');
                    first = false;
                    NewLine(sb, indent, level + 1);
                    WriteString(sb, Convert.ToString(e.Key, CultureInfo.InvariantCulture));
                    sb.Append(indent ? ": " : ":");
                    Write(sb, e.Value, indent, level + 1, depth + 1);
                }
                if (!first) NewLine(sb, indent, level);
                sb.Append('}');
                return;
            }
            var en = v as IEnumerable;
            if (en != null)
            {
                sb.Append('[');
                bool first = true;
                foreach (var item in en)
                {
                    if (!first) sb.Append(',');
                    first = false;
                    NewLine(sb, indent, level + 1);
                    Write(sb, item, indent, level + 1, depth + 1);
                }
                if (!first) NewLine(sb, indent, level);
                sb.Append(']');
                return;
            }
            WriteString(sb, v.ToString());
        }

        public static void WriteString(StringBuilder sb, string s)
        {
            sb.Append('"');
            foreach (var c in s)
            {
                switch (c)
                {
                    case '"': sb.Append("\\\""); break;
                    case '\\': sb.Append("\\\\"); break;
                    case '\n': sb.Append("\\n"); break;
                    case '\r': sb.Append("\\r"); break;
                    case '\t': sb.Append("\\t"); break;
                    case '\b': sb.Append("\\b"); break;
                    case '\f': sb.Append("\\f"); break;
                    default:
                        if (c < 0x20) sb.Append("\\u").Append(((int)c).ToString("x4"));
                        else sb.Append(c);
                        break;
                }
            }
            sb.Append('"');
        }

        // ---------------- typed accessors (forgiving) ----------------
        public static object Get(IDictionary<string, object> o, string key)
        {
            object v;
            if (o != null && o.TryGetValue(key, out v)) return v;
            return null;
        }

        public static string Str(IDictionary<string, object> o, string key, string def = null)
        {
            var v = Get(o, key);
            if (v == null) return def;
            if (v is string) return (string)v;
            if (v is double) return ((double)v).ToString(CultureInfo.InvariantCulture);
            if (v is bool) return (bool)v ? "true" : "false";
            if (v is IDictionary || v is IList) return Serialize(v);
            return Convert.ToString(v, CultureInfo.InvariantCulture);
        }

        public static double Num(IDictionary<string, object> o, string key, double def = 0)
        {
            var v = Get(o, key);
            return ToDouble(v, def);
        }

        public static double ToDouble(object v, double def = 0)
        {
            if (v == null) return def;
            if (v is double) return (double)v;
            if (v is long) return (long)v;
            if (v is int) return (int)v;
            if (v is bool) return (bool)v ? 1 : 0;
            double d;
            if (double.TryParse(Convert.ToString(v, CultureInfo.InvariantCulture), NumberStyles.Float, CultureInfo.InvariantCulture, out d)) return d;
            return def;
        }

        public static long Int(IDictionary<string, object> o, string key, long def = 0)
        {
            var v = Get(o, key);
            if (v == null) return def;
            if (v is long) return (long)v;
            return (long)Math.Round(ToDouble(v, def));
        }

        public static bool Bool(IDictionary<string, object> o, string key, bool def = false)
        {
            var v = Get(o, key);
            if (v == null) return def;
            if (v is bool) return (bool)v;
            var s = Convert.ToString(v, CultureInfo.InvariantCulture).Trim().ToLowerInvariant();
            if (s == "true" || s == "1" || s == "yes" || s == "y") return true;
            if (s == "false" || s == "0" || s == "no" || s == "n") return false;
            return def;
        }

        public static Dictionary<string, object> Obj(IDictionary<string, object> o, string key)
        {
            return Get(o, key) as Dictionary<string, object>;
        }

        public static List<object> Arr(IDictionary<string, object> o, string key)
        {
            var v = Get(o, key);
            var l = v as List<object>;
            if (l != null) return l;
            if (v == null) return null;
            // A single value where a list was expected becomes a one-item list.
            return new List<object> { v };
        }

        public static List<string> StrList(IDictionary<string, object> o, string key)
        {
            var a = Arr(o, key);
            var r = new List<string>();
            if (a == null) return r;
            foreach (var x in a) if (x != null) r.Add(Convert.ToString(x, CultureInfo.InvariantCulture));
            return r;
        }

        public static List<long> IntList(IDictionary<string, object> o, string key)
        {
            var a = Arr(o, key);
            var r = new List<long>();
            if (a == null) return r;
            foreach (var x in a) if (x != null) r.Add((long)Math.Round(ToDouble(x)));
            return r;
        }

        /// <summary>Reads [x,y,z] or {x,y,z} into a double[3] (z optional).</summary>
        public static double[] Vec(object v)
        {
            if (v == null) return null;
            var l = v as IList;
            if (l != null && l.Count >= 2)
                return new[] { ToDouble(l[0]), ToDouble(l[1]), l.Count > 2 ? ToDouble(l[2]) : 0 };
            var d = v as IDictionary<string, object>;
            if (d != null)
                return new[] { Num(d, "x"), Num(d, "y"), Num(d, "z") };
            return null;
        }

        public static Dictionary<string, object> O(params object[] kv)
        {
            var d = new Dictionary<string, object>();
            for (int i = 0; i + 1 < kv.Length; i += 2) d[(string)kv[i]] = kv[i + 1];
            return d;
        }

        // ---------------- parser ----------------
        sealed class Parser
        {
            readonly string s;
            int i;
            public Parser(string text) { s = text; }
            public bool End { get { return i >= s.Length; } }
            public int Pos { get { return i; } }

            public void SkipWs()
            {
                while (i < s.Length)
                {
                    char c = s[i];
                    if (c == ' ' || c == '\t' || c == '\r' || c == '\n' || c == '﻿') { i++; continue; }
                    // tolerate // and /* */ comments (JSONC client configs)
                    if (c == '/' && i + 1 < s.Length && s[i + 1] == '/') { while (i < s.Length && s[i] != '\n') i++; continue; }
                    if (c == '/' && i + 1 < s.Length && s[i + 1] == '*')
                    {
                        int end = s.IndexOf("*/", i + 2, StringComparison.Ordinal);
                        i = end < 0 ? s.Length : end + 2;
                        continue;
                    }
                    break;
                }
            }

            public object ReadValue()
            {
                if (End) throw new FormatException("Unexpected end of JSON");
                char c = s[i];
                if (c == '{') return ReadObject();
                if (c == '[') return ReadArray();
                if (c == '"') return ReadString();
                if (c == 't' && Match("true")) return true;
                if (c == 'f' && Match("false")) return false;
                if (c == 'n' && Match("null")) return null;
                return ReadNumber();
            }

            bool Match(string word)
            {
                if (string.CompareOrdinal(s, i, word, 0, word.Length) == 0) { i += word.Length; return true; }
                throw new FormatException("Invalid token at " + i);
            }

            Dictionary<string, object> ReadObject()
            {
                var d = new Dictionary<string, object>();
                i++; SkipWs();
                if (i < s.Length && s[i] == '}') { i++; return d; }
                while (true)
                {
                    SkipWs();
                    if (i < s.Length && s[i] == '}') { i++; return d; } // trailing comma
                    if (i >= s.Length || s[i] != '"') throw new FormatException("Expected property name at " + i);
                    var k = ReadString();
                    SkipWs();
                    if (i >= s.Length || s[i] != ':') throw new FormatException("Expected ':' at " + i);
                    i++; SkipWs();
                    d[k] = ReadValue();
                    SkipWs();
                    if (i >= s.Length) throw new FormatException("Unterminated object");
                    if (s[i] == ',') { i++; continue; }
                    if (s[i] == '}') { i++; return d; }
                    throw new FormatException("Expected ',' or '}' at " + i);
                }
            }

            List<object> ReadArray()
            {
                var l = new List<object>();
                i++; SkipWs();
                if (i < s.Length && s[i] == ']') { i++; return l; }
                while (true)
                {
                    SkipWs();
                    if (i < s.Length && s[i] == ']') { i++; return l; }
                    l.Add(ReadValue());
                    SkipWs();
                    if (i >= s.Length) throw new FormatException("Unterminated array");
                    if (s[i] == ',') { i++; continue; }
                    if (s[i] == ']') { i++; return l; }
                    throw new FormatException("Expected ',' or ']' at " + i);
                }
            }

            string ReadString()
            {
                var sb = new StringBuilder();
                i++;
                while (i < s.Length)
                {
                    char c = s[i++];
                    if (c == '"') return sb.ToString();
                    if (c != '\\') { sb.Append(c); continue; }
                    if (i >= s.Length) break;
                    char e = s[i++];
                    switch (e)
                    {
                        case '"': sb.Append('"'); break;
                        case '\\': sb.Append('\\'); break;
                        case '/': sb.Append('/'); break;
                        case 'b': sb.Append('\b'); break;
                        case 'f': sb.Append('\f'); break;
                        case 'n': sb.Append('\n'); break;
                        case 'r': sb.Append('\r'); break;
                        case 't': sb.Append('\t'); break;
                        case 'u':
                            if (i + 4 > s.Length) throw new FormatException("Bad unicode escape");
                            sb.Append((char)int.Parse(s.Substring(i, 4), NumberStyles.HexNumber, CultureInfo.InvariantCulture));
                            i += 4;
                            break;
                        default: sb.Append(e); break;
                    }
                }
                throw new FormatException("Unterminated string");
            }

            object ReadNumber()
            {
                int start = i;
                bool isFloat = false;
                while (i < s.Length)
                {
                    char c = s[i];
                    if ((c >= '0' && c <= '9') || c == '-' || c == '+') { i++; continue; }
                    if (c == '.' || c == 'e' || c == 'E') { isFloat = true; i++; continue; }
                    break;
                }
                if (start == i) throw new FormatException("Unexpected character '" + s[i] + "' at " + i);
                var tok = s.Substring(start, i - start);
                long l;
                if (!isFloat && long.TryParse(tok, NumberStyles.Integer, CultureInfo.InvariantCulture, out l)) return l;
                return double.Parse(tok, NumberStyles.Float, CultureInfo.InvariantCulture);
            }
        }
    }

    /// <summary>Pre-serialized JSON fragment, written verbatim.</summary>
    public sealed class JsonRaw
    {
        public readonly string Text;
        public JsonRaw(string text) { Text = text; }
    }
}
