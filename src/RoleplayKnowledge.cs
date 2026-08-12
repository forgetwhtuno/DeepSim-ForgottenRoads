using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;

namespace ErenshorDeepSims
{
    // How a roleplay-usable fact entered the party's awareness. This is PROVENANCE (where the fact came
    // from), which is a different axis from knowledge EXPOSURE (whether a given Sim has met it yet).
    // Generated dialogue is never a provenance: a Sim saying "I heard the Wisp is watching us" proves
    // only that the Sim said it.
    internal enum RoleplayFactSource
    {
        CurrentWorld,   // read from live Erenshor state right now
        PlayerProgress, // verified quest/faction progression owned by Erenshor's save
        SharedEvent,    // an already-verified Deep Sims event/memory
        Reference       // transient external reference lookup, supports one answer only
    }

    internal sealed class RoleplayFact
    {
        internal readonly string TopicKey;   // semantic key only, never encyclopedic text
        internal readonly string Label;      // short display name sourced at runtime from the game
        internal readonly RoleplayFactSource Source;
        internal readonly string Detail;     // short, game-supplied; may be empty
        internal readonly float Value;       // live standing, when this fact is a faction
        internal readonly float DefaultValue;// starting standing, when this fact is a faction

        internal RoleplayFact(string topicKey, string label, RoleplayFactSource source, string detail)
            : this(topicKey, label, source, detail, 0f, 0f) { }

        internal RoleplayFact(string topicKey, string label, RoleplayFactSource source, string detail,
            float value, float defaultValue)
        {
            TopicKey = topicKey;
            Label = label;
            Source = source;
            Detail = detail;
            Value = value;
            DefaultValue = defaultValue;
        }
    }

    // Reads only what current Erenshor actually exposes. Everything is optional and fail-closed: if a
    // field or type is missing on this game build, the reader returns nothing rather than guessing.
    //
    // Verified hooks used (all plain state reads; no Harmony patching, no save writes):
    //   GameData.SceneName                       -> current zone
    //   GlobalFactionManager.AllFactions/FactionDB-> WorldFaction{FactionName,REFNAME,FactionDesc,
    //                                                FactionValue,DEFAULTVAL}
    //   GameData.CompletedQuests                 -> verified player progression
    //
    // FactionValue != DEFAULTVAL is the key signal: Erenshor only moves a faction away from its default
    // when the player has actually done something involving it. That is verified interaction, not a
    // guess from proximity or from a name appearing in a zone.
    internal static class RoleplayKnowledgeReader
    {
        private const int MaxFactions = 12;
        private const float FactionDivergenceEpsilon = 0.01f;

        private static Type _gameData;
        private static Type _factionManager;
        private static bool _probed;

        private static void Probe()
        {
            if (_probed) return;
            _probed = true;
            try
            {
                Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();
                for (int i = 0; i < assemblies.Length; i++)
                {
                    if (_gameData == null) _gameData = assemblies[i].GetType("GameData", false);
                    if (_factionManager == null) _factionManager = assemblies[i].GetType("GlobalFactionManager", false);
                    if (_gameData != null && _factionManager != null) break;
                }
            }
            catch { }
        }

        internal static string CurrentZone()
        {
            Probe();
            try
            {
                if (_gameData == null) return null;
                FieldInfo f = _gameData.GetField("SceneName", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
                if (f == null) return null;
                string value = f.GetValue(null) as string;
                return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
            }
            catch { return null; }
        }

        // Factions whose live value has moved off its default. Bounded, and never includes the raw
        // numeric standing: a reputation number is a fact, but it is not an opinion and must not be
        // handed to expression as if it were one.
        internal static List<RoleplayFact> EncounteredFactions()
        {
            List<RoleplayFact> facts = new List<RoleplayFact>();
            Probe();
            try
            {
                if (_factionManager == null) return facts;
                IEnumerable all = ReadStaticEnumerable(_factionManager, "AllFactions");
                if (all == null) all = ReadStaticEnumerable(_factionManager, "FactionDB");
                if (all == null) return facts;

                foreach (object faction in all)
                {
                    if (faction == null) continue;
                    if (facts.Count >= MaxFactions) break;
                    Type t = faction.GetType();
                    string name = ReadString(faction, t, "FactionName");
                    if (string.IsNullOrWhiteSpace(name)) name = ReadString(faction, t, "Name");
                    if (string.IsNullOrWhiteSpace(name)) continue;

                    float current, initial;
                    if (!TryReadFloat(faction, t, "FactionValue", out current)) continue;
                    if (!TryReadFloat(faction, t, "DEFAULTVAL", out initial)) continue;
                    if (Math.Abs(current - initial) <= FactionDivergenceEpsilon) continue;

                    string refName = ReadString(faction, t, "REFNAME");
                    string key = "faction:" + Slug(string.IsNullOrWhiteSpace(refName) ? name : refName);
                    // FactionDesc is Erenshor's own in-world description, read at runtime from the
                    // player's installation. It is never bundled into this repository.
                    string desc = ReadString(faction, t, "FactionDesc");
                    if (string.IsNullOrWhiteSpace(desc)) desc = ReadString(faction, t, "Desc");
                    facts.Add(new RoleplayFact(key, name.Trim(), RoleplayFactSource.CurrentWorld, Trim(desc, 160), current, initial));
                }
            }
            catch { }
            return facts;
        }

        // Attitude comes from verified standing movement only. A number is evidence of dealings, not
        // of belief, so the mapping is intentionally shallow and never reaches Loyal.
        internal static RoleplayFactionAttitude AttitudeFor(RoleplayFact fact)
        {
            if (fact == null) return RoleplayFactionAttitude.Unknown;
            return RoleplayAffinity.AttitudeFor(true, fact.Value, fact.DefaultValue);
        }

        internal static int CompletedQuestCount()
        {
            Probe();
            try
            {
                if (_gameData == null) return 0;
                IEnumerable done = ReadStaticEnumerable(_gameData, "CompletedQuests");
                if (done == null) return 0;
                int count = 0;
                foreach (object q in done) { if (q != null) count++; }
                return count;
            }
            catch { return 0; }
        }

        private static IEnumerable ReadStaticEnumerable(Type owner, string member)
        {
            try
            {
                FieldInfo f = owner.GetField(member, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
                if (f != null) return f.GetValue(null) as IEnumerable;
                PropertyInfo p = owner.GetProperty(member, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
                if (p != null) return p.GetValue(null, null) as IEnumerable;
            }
            catch { }
            return null;
        }

        private static string ReadString(object instance, Type t, string member)
        {
            try
            {
                FieldInfo f = t.GetField(member, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                if (f != null) return f.GetValue(instance) as string;
            }
            catch { }
            return null;
        }

        private static bool TryReadFloat(object instance, Type t, string member, out float value)
        {
            value = 0f;
            try
            {
                FieldInfo f = t.GetField(member, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                if (f == null) return false;
                object raw = f.GetValue(instance);
                if (raw == null) return false;
                value = Convert.ToSingle(raw);
                return true;
            }
            catch { return false; }
        }

        internal static string Slug(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return string.Empty;
            System.Text.StringBuilder sb = new System.Text.StringBuilder();
            string lower = value.Trim().ToLowerInvariant();
            for (int i = 0; i < lower.Length; i++)
            {
                char c = lower[i];
                if (c >= 'a' && c <= 'z') sb.Append(c);
                else if (c >= '0' && c <= '9') sb.Append(c);
                else if (sb.Length > 0 && sb[sb.Length - 1] != '_') sb.Append('_');
            }
            return sb.ToString().Trim('_');
        }

        private static string Trim(string value, int max)
        {
            if (string.IsNullOrWhiteSpace(value)) return string.Empty;
            string v = value.Trim();
            return v.Length <= max ? v : v.Substring(0, max);
        }
    }
}
