using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using UnityEngine;

namespace ErenshorDeepSims
{
    internal static class PartyResolver
    {
        private static readonly string[] StrongHints = new string[] { "group", "party", "member", "raid", "companion" };
        private static readonly string[] WeakHints = new string[] { "slot", "follow" };
        private static readonly Dictionary<Type, ReflectedMembers> StrongInstanceMembers = new Dictionary<Type, ReflectedMembers>();
        private static readonly Dictionary<Type, ReflectedMembers> WeakInstanceMembers = new Dictionary<Type, ReflectedMembers>();
        private static readonly Dictionary<Type, ReflectedMembers> StrongStaticMembers = new Dictionary<Type, ReflectedMembers>();
        private static readonly Dictionary<Type, ReflectedMembers> GroupFlagMembers = new Dictionary<Type, ReflectedMembers>();

        private sealed class ReflectedMembers
        {
            internal FieldInfo[] Fields;
            internal PropertyInfo[] Properties;
        }

        // `source == null` on an `object`-typed parameter resolves to plain reference equality at
        // compile time, bypassing UnityEngine.Object's overloaded == that detects a destroyed native
        // object ("fake null"). A destroyed SimPlayer/NPC would then read as non-null here and get
        // reflected over. Route the null check through the Unity overload whenever the value actually
        // is a UnityEngine.Object.
        private static bool IsUnityNull(object value)
        {
            if (value == null) return true;
            UnityEngine.Object unityObject = value as UnityEngine.Object;
            if (!ReferenceEquals(unityObject, null)) return unityObject == null;
            return false;
        }

        internal static List<string> ResolvePartyMemberNames()
        {
            return ResolvePartyMemberNames(SimContextReader.GetActiveSims());
        }

        internal static List<string> ResolvePartyMemberNames(List<SimSnapshot> active)
        {
            if (active == null) active = new List<SimSnapshot>();
            Dictionary<string, SimSnapshot> activeByName = new Dictionary<string, SimSnapshot>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < active.Count; i++) activeByName[active[i].Name] = active[i];

            List<string> result = new List<string>();
            HashSet<string> seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            // This is Erenshor's direct, known party signal and is also what the bundled COOP client
            // populates. Prefer it over compatibility reflection during every normal poll.
            try
            {
                SimPlayerTracking[] members = GameData.GroupMembers;
                if (members != null)
                    for (int i = 0; i < members.Length; i++)
                        if (members[i] != null) AddName(members[i].SimName, activeByName, result, seen);
            }
            catch { }

            if (result.Count > 0) return result;

            // Compatibility fallback for older/forked builds. Member discovery is cached per type;
            // the ordinary poll never re-enumerates every reflected field/property.
            try { ScanObject(GameData.SimPlayerGrouping, activeByName, result, seen, true); } catch { }
            try { ScanObject(GameData.PlayerControl, activeByName, result, seen, true); } catch { }
            if (result.Count == 0)
                try { ScanStaticType(typeof(GameData), activeByName, result, seen); } catch { }

            // Fallback: some builds store only a bool-like group flag on each spawned Sim/NPC.
            if (result.Count == 0)
            {
                for (int i = 0; i < active.Count; i++)
                {
                    try
                    {
                        if (LooksGrouped(active[i].RuntimeSim)) AddName(active[i].Name, activeByName, result, seen);
                    }
                    catch { }
                    try
                    {
                        NPC npc = active[i].RuntimeSim.GetComponent<NPC>();
                        if (LooksGrouped(npc)) AddName(active[i].Name, activeByName, result, seen);
                    }
                    catch { }
                }
            }

            return result;
        }

        internal static string BuildDiagnosticReport()
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("DeepSims party detection diagnostic");
            sb.AppendLine("Generated: " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
            sb.AppendLine("Scene: " + UnityEngine.SceneManagement.SceneManager.GetActiveScene().name);
            sb.AppendLine();

            List<SimSnapshot> active = SimContextReader.GetActiveSims();
            sb.AppendLine("ACTIVE SIMS (" + active.Count + ")");
            for (int i = 0; i < active.Count; i++)
            {
                SimSnapshot sim = active[i];
                sb.AppendLine("  " + sim.SummaryLine());
                sb.AppendLine("    native-style: " + NativeDialogueStyle.Describe(sim));
                if (sim.DialogueExamples != null && sim.DialogueExamples.Count > 0)
                    sb.AppendLine("    sampled-dialogue: " + string.Join(" | ", sim.DialogueExamples.ToArray()));
            }
            sb.AppendLine();

            DumpLikelyMembers(sb, "GameData.SimPlayerGrouping", SafeValue(delegate { return (object)GameData.SimPlayerGrouping; }));
            DumpLikelyMembers(sb, "GameData.PlayerControl", SafeValue(delegate { return (object)GameData.PlayerControl; }));
            DumpStaticLikelyMembers(sb, typeof(GameData));

            sb.AppendLine();
            sb.AppendLine("PER-SIM GROUP/PARTY FLAGS");
            for (int i = 0; i < active.Count; i++)
            {
                sb.AppendLine("-- " + active[i].Name + " / SimPlayer --");
                DumpBoolAndScalarHints(sb, active[i].RuntimeSim);
                try
                {
                    NPC npc = active[i].RuntimeSim.GetComponent<NPC>();
                    sb.AppendLine("-- " + active[i].Name + " / NPC --");
                    DumpBoolAndScalarHints(sb, npc);
                }
                catch { }
            }

            sb.AppendLine();
            sb.AppendLine("RESOLVED PARTY NAMES");
            List<string> resolved = ResolvePartyMemberNames();
            if (resolved.Count == 0) sb.AppendLine("  (none)");
            for (int i = 0; i < resolved.Count; i++) sb.AppendLine("  " + resolved[i]);
            return sb.ToString();
        }

        private static void ScanObject(object source, Dictionary<string, SimSnapshot> activeByName, List<string> result, HashSet<string> seen, bool strongOnly)
        {
            if (IsUnityNull(source)) return;
            Type t = source.GetType();
            ReflectedMembers members = GetCachedMembers(t, false, strongOnly, false);
            FieldInfo[] fields = members.Fields;
            for (int i = 0; i < fields.Length; i++)
            {
                if (!HasHint(fields[i].Name, strongOnly)) continue;
                object value = null;
                try { value = fields[i].GetValue(source); } catch { }
                ExtractNames(value, activeByName, result, seen, 0);
            }
            PropertyInfo[] props = members.Properties;
            for (int i = 0; i < props.Length; i++)
            {
                if (props[i].GetIndexParameters().Length != 0 || !HasHint(props[i].Name, strongOnly)) continue;
                object value = null;
                try { value = props[i].GetValue(source, null); } catch { }
                ExtractNames(value, activeByName, result, seen, 0);
            }
        }

        private static void ScanStaticType(Type t, Dictionary<string, SimSnapshot> activeByName, List<string> result, HashSet<string> seen)
        {
            ReflectedMembers members = GetCachedMembers(t, true, true, false);
            FieldInfo[] fields = members.Fields;
            for (int i = 0; i < fields.Length; i++)
            {
                if (!HasHint(fields[i].Name, true)) continue;
                object value = null;
                try { value = fields[i].GetValue(null); } catch { }
                ExtractNames(value, activeByName, result, seen, 0);
            }
            PropertyInfo[] props = members.Properties;
            for (int i = 0; i < props.Length; i++)
            {
                if (props[i].GetIndexParameters().Length != 0 || !HasHint(props[i].Name, true)) continue;
                object value = null;
                try { value = props[i].GetValue(null, null); } catch { }
                ExtractNames(value, activeByName, result, seen, 0);
            }
        }

        private static void ExtractNames(object value, Dictionary<string, SimSnapshot> activeByName, List<string> result, HashSet<string> seen, int depth)
        {
            if (value == null || depth > 2) return;
            string str = value as string;
            if (str != null)
            {
                AddName(str, activeByName, result, seen);
                return;
            }

            // Only the name is needed to match against the already-built active list. Building a full
            // snapshot here meant re-running the whole reflection pass for every Sim referenced by
            // every group-ish field, several times per party poll.
            SimPlayer sp = value as SimPlayer;
            if (sp != null)
            {
                AddName(SimContextReader.ReadSimName(sp), activeByName, result, seen);
                return;
            }

            SimPlayerTracking tracking = value as SimPlayerTracking;
            if (tracking != null)
            {
                AddName(tracking.SimName, activeByName, result, seen);
                return;
            }

            GameObject go = value as GameObject;
            if (go != null)
            {
                SimPlayer goSp = go.GetComponent<SimPlayer>();
                if (goSp != null) AddName(SimContextReader.ReadSimName(goSp), activeByName, result, seen);
                return;
            }

            Component comp = value as Component;
            if (comp != null)
            {
                SimPlayer compSp = comp.GetComponent<SimPlayer>();
                if (compSp != null) AddName(SimContextReader.ReadSimName(compSp), activeByName, result, seen);
                return;
            }

            IEnumerable enumerable = value as IEnumerable;
            if (enumerable != null)
            {
                int count = 0;
                foreach (object item in enumerable)
                {
                    ExtractNames(item, activeByName, result, seen, depth + 1);
                    count++;
                    if (count >= 32) break;
                }
            }
        }

        private static void AddName(string name, Dictionary<string, SimSnapshot> activeByName, List<string> result, HashSet<string> seen)
        {
            if (string.IsNullOrWhiteSpace(name)) return;
            SimSnapshot actual;
            if (!activeByName.TryGetValue(name.Trim(), out actual)) return;
            if (seen.Add(actual.Name)) result.Add(actual.Name);
        }

        private static bool LooksGrouped(object source)
        {
            if (IsUnityNull(source)) return false;
            Type t = source.GetType();
            ReflectedMembers members = GetCachedMembers(t, false, false, true);
            FieldInfo[] fields = members.Fields;
            for (int i = 0; i < fields.Length; i++)
            {
                if (fields[i].FieldType != typeof(bool) || !HasHint(fields[i].Name, false)) continue;
                try { if ((bool)fields[i].GetValue(source)) return true; } catch { }
            }
            PropertyInfo[] props = members.Properties;
            for (int i = 0; i < props.Length; i++)
            {
                if (props[i].PropertyType != typeof(bool) || props[i].GetIndexParameters().Length != 0 || !HasHint(props[i].Name, false)) continue;
                try { if ((bool)props[i].GetValue(source, null)) return true; } catch { }
            }
            return false;
        }

        private static ReflectedMembers GetCachedMembers(Type type, bool isStatic, bool strongOnly, bool boolOnly)
        {
            Dictionary<Type, ReflectedMembers> cache = boolOnly ? GroupFlagMembers :
                (isStatic ? StrongStaticMembers : (strongOnly ? StrongInstanceMembers : WeakInstanceMembers));
            ReflectedMembers cached;
            if (cache.TryGetValue(type, out cached)) return cached;

            BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic | (isStatic ? BindingFlags.Static : BindingFlags.Instance);
            List<FieldInfo> fields = new List<FieldInfo>();
            FieldInfo[] allFields = type.GetFields(flags);
            for (int i = 0; i < allFields.Length; i++)
                if ((!boolOnly || allFields[i].FieldType == typeof(bool)) && HasHint(allFields[i].Name, strongOnly)) fields.Add(allFields[i]);
            List<PropertyInfo> properties = new List<PropertyInfo>();
            PropertyInfo[] allProperties = type.GetProperties(flags);
            for (int i = 0; i < allProperties.Length; i++)
                if (allProperties[i].GetIndexParameters().Length == 0 && (!boolOnly || allProperties[i].PropertyType == typeof(bool)) && HasHint(allProperties[i].Name, strongOnly)) properties.Add(allProperties[i]);

            cached = new ReflectedMembers { Fields = fields.ToArray(), Properties = properties.ToArray() };
            cache[type] = cached;
            return cached;
        }

        private static bool HasHint(string name, bool strongOnly)
        {
            if (string.IsNullOrEmpty(name)) return false;
            string lower = name.ToLowerInvariant();
            for (int i = 0; i < StrongHints.Length; i++) if (lower.Contains(StrongHints[i])) return true;
            if (!strongOnly)
                for (int i = 0; i < WeakHints.Length; i++) if (lower.Contains(WeakHints[i])) return true;
            return false;
        }

        private static void DumpLikelyMembers(StringBuilder sb, string title, object source)
        {
            sb.AppendLine(title);
            if (source == null) { sb.AppendLine("  NULL"); sb.AppendLine(); return; }
            Type t = source.GetType();
            sb.AppendLine("  Type: " + t.FullName);
            FieldInfo[] fields = t.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            for (int i = 0; i < fields.Length; i++)
            {
                if (!HasHint(fields[i].Name, false)) continue;
                object value = null;
                try { value = fields[i].GetValue(source); } catch { }
                sb.AppendLine("  field " + fields[i].Name + " = " + DescribeValue(value));
            }
            PropertyInfo[] props = t.GetProperties(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            for (int i = 0; i < props.Length; i++)
            {
                if (props[i].GetIndexParameters().Length != 0 || !HasHint(props[i].Name, false)) continue;
                object value = null;
                try { value = props[i].GetValue(source, null); } catch { }
                sb.AppendLine("  prop  " + props[i].Name + " = " + DescribeValue(value));
            }
            sb.AppendLine();
        }

        private static void DumpStaticLikelyMembers(StringBuilder sb, Type t)
        {
            sb.AppendLine("GameData static group/party-looking members");
            FieldInfo[] fields = t.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
            for (int i = 0; i < fields.Length; i++)
            {
                if (!HasHint(fields[i].Name, false)) continue;
                object value = null;
                try { value = fields[i].GetValue(null); } catch { }
                sb.AppendLine("  field " + fields[i].Name + " = " + DescribeValue(value));
            }
            PropertyInfo[] props = t.GetProperties(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
            for (int i = 0; i < props.Length; i++)
            {
                if (props[i].GetIndexParameters().Length != 0 || !HasHint(props[i].Name, false)) continue;
                object value = null;
                try { value = props[i].GetValue(null, null); } catch { }
                sb.AppendLine("  prop  " + props[i].Name + " = " + DescribeValue(value));
            }
            sb.AppendLine();
        }

        private static void DumpBoolAndScalarHints(StringBuilder sb, object source)
        {
            if (IsUnityNull(source)) { sb.AppendLine("  NULL"); return; }
            Type t = source.GetType();
            FieldInfo[] fields = t.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            for (int i = 0; i < fields.Length; i++)
            {
                if (!HasHint(fields[i].Name, false)) continue;
                object value = null;
                try { value = fields[i].GetValue(source); } catch { }
                sb.AppendLine("  field " + fields[i].Name + " = " + DescribeValue(value));
            }
            PropertyInfo[] props = t.GetProperties(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            for (int i = 0; i < props.Length; i++)
            {
                if (props[i].GetIndexParameters().Length != 0 || !HasHint(props[i].Name, false)) continue;
                object value = null;
                try { value = props[i].GetValue(source, null); } catch { }
                sb.AppendLine("  prop  " + props[i].Name + " = " + DescribeValue(value));
            }
        }

        private static string DescribeValue(object value)
        {
            if (value == null) return "NULL";
            string s = value as string;
            if (s != null) return "\"" + s + "\"";
            IEnumerable enumerable = value as IEnumerable;
            if (enumerable != null)
            {
                List<string> items = new List<string>();
                int count = 0;
                foreach (object item in enumerable)
                {
                    items.Add(item == null ? "null" : ShortObject(item));
                    count++;
                    if (count >= 12) { items.Add("..."); break; }
                }
                return value.GetType().Name + " [" + string.Join(", ", items.ToArray()) + "]";
            }
            return ShortObject(value);
        }

        private static string ShortObject(object value)
        {
            if (value == null) return "null";
            SimPlayerTracking tracking = value as SimPlayerTracking;
            if (tracking != null) return "SimPlayerTracking:" + tracking.SimName;
            SimPlayer sp = value as SimPlayer;
            if (sp != null)
            {
                string simName = SimContextReader.ReadSimName(sp);
                return "SimPlayer:" + (string.IsNullOrWhiteSpace(simName) ? sp.name : simName);
            }
            GameObject go = value as GameObject;
            if (go != null) return "GameObject:" + go.name;
            Component comp = value as Component;
            if (comp != null) return comp.GetType().Name + ":" + comp.name;
            string text;
            try { text = Convert.ToString(value); } catch { text = value.GetType().Name; }
            if (text != null && text.Length > 100) text = text.Substring(0, 100) + "...";
            return value.GetType().Name + ":" + text;
        }

        private delegate object Getter();
        private static object SafeValue(Getter getter)
        {
            try { return getter(); } catch { return null; }
        }
    }
}
