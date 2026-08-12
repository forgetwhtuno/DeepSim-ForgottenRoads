using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;

namespace ErenshorDeepSims
{
    // ---------------------------------------------------------------------
    // Optional, reflection-only, read-only bridge to the standalone Erenshor
    // Campmaster mod (ErenshorCampmaster.CampmasterApi). Deep Sims takes no
    // compile-time reference to Campmaster and works normally when it is
    // absent.
    //
    // This class never calls anything that could affect gameplay: it only
    // reads Campmaster's snapshot/event surface. All facts it returns are
    // OBSERVED_NOW / EXPERIENCED per AGENTS.md's trust hierarchy, and every
    // field is nullable-by-convention: an unset field means unknown and must
    // never be rendered as a claim.
    // ---------------------------------------------------------------------
    internal static class CampmasterBridge
    {
        private const string ApiTypeName = "ErenshorCampmaster.CampmasterApi";

        private static readonly object ResolveLock = new object();
        private static volatile bool _resolved;
        private static Type _apiType;
        private static PropertyInfo _isActiveProperty;
        private static MethodInfo _snapshotMethod;
        private static MethodInfo _eventsMethod;

        private static long _lastSequence;

        static CampmasterBridge()
        {
            try { AppDomain.CurrentDomain.AssemblyLoad += OnAssemblyLoad; }
            catch { }
        }

        private static void OnAssemblyLoad(object sender, AssemblyLoadEventArgs args)
        {
            _resolved = false;
        }

        // AppDomain events are process-wide and survive a Lunaris plugin GameObject. Remove the
        // handler explicitly so a disabled Deep Sims assembly cannot be retained by the AppDomain.
        internal static void Shutdown()
        {
            try { AppDomain.CurrentDomain.AssemblyLoad -= OnAssemblyLoad; }
            catch { }
            lock (ResolveLock)
            {
                _resolved = false;
                _apiType = null;
                _isActiveProperty = null;
                _snapshotMethod = null;
                _eventsMethod = null;
                _lastSequence = 0;
            }
        }

        internal static bool IsPresent
        {
            get { EnsureResolved(); return _apiType != null; }
        }

        // Null when Campmaster is absent or the read failed; a real bool
        // otherwise. Never guess this from CampContextFacts.Active alone,
        // since a snapshot read can fail independently of the property read.
        internal static bool? TryGetHuntCampActive()
        {
            EnsureResolved();
            if (_isActiveProperty == null) return null;
            try { return (bool)_isActiveProperty.GetValue(null, null); }
            catch { return null; }
        }

        internal static CampContextFacts ReadSnapshot()
        {
            EnsureResolved();
            if (_snapshotMethod == null) return null;
            try
            {
                object raw = _snapshotMethod.Invoke(null, null);
                return ParseSnapshot(raw as Dictionary<string, string>);
            }
            catch { return null; }
        }

        // Advances the internal cursor. Call at most once per poll cycle.
        internal static List<CampEventFact> ReadNewEvents()
        {
            List<CampEventFact> result = new List<CampEventFact>();
            EnsureResolved();
            if (_eventsMethod == null) return result;
            try
            {
                object raw = _eventsMethod.Invoke(null, new object[] { _lastSequence });
                List<Dictionary<string, string>> rows = raw as List<Dictionary<string, string>>;
                result = ParseEvents(rows);
                for (int i = 0; i < result.Count; i++)
                    if (result[i].Sequence > _lastSequence) _lastSequence = result[i].Sequence;
            }
            catch { }
            return result;
        }

        private static void EnsureResolved()
        {
            if (_resolved) return;
            lock (ResolveLock)
            {
                if (_resolved) return;
                try
                {
                    Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();
                    for (int i = 0; i < assemblies.Length; i++)
                    {
                        Assembly assembly = assemblies[i];
                        if (assembly == null) continue;
                        Type type = assembly.GetType(ApiTypeName, false);
                        if (type == null) continue;
                        _apiType = type;
                        _isActiveProperty = type.GetProperty("IsHuntCampActive", BindingFlags.Public | BindingFlags.Static);
                        _snapshotMethod = type.GetMethod("GetCurrentSnapshot", BindingFlags.Public | BindingFlags.Static, null, Type.EmptyTypes, null);
                        _eventsMethod = type.GetMethod("GetEventsAfter", BindingFlags.Public | BindingFlags.Static, null, new[] { typeof(long) }, null);
                        break;
                    }
                }
                catch { }
                _resolved = true;
            }
        }

        // -----------------------------------------------------------------
        // Pure parsing (no reflection) so this logic is unit-testable without
        // Campmaster installed. Unknown/absent keys must stay unknown.
        // -----------------------------------------------------------------
        internal static CampContextFacts ParseSnapshot(Dictionary<string, string> data)
        {
            if (data == null) return null;
            CampContextFacts facts = new CampContextFacts();

            string mode;
            data.TryGetValue("mode", out mode);
            facts.Active = string.Equals(mode, "HuntCamp", StringComparison.OrdinalIgnoreCase);

            string state;
            facts.State = data.TryGetValue("state", out state) ? state : null;
            string recognition;
            facts.Recognition = data.TryGetValue("recognition", out recognition) ? recognition : null;
            string activity;
            facts.Activity = data.TryGetValue("activity", out activity) ? activity : null;
            string zone;
            facts.Zone = data.TryGetValue("zone", out zone) ? zone : null;
            string party;
            facts.Party = data.TryGetValue("party", out party) ? party : null;

            facts.ElapsedMinutes = ParseElapsedMinutes(data);

            string puller;
            facts.Puller = data.TryGetValue("puller", out puller) ? puller : null;
            string mainTank;
            facts.MainTank = data.TryGetValue("mainTank", out mainTank) ? mainTank : null;
            string mainAssist;
            facts.MainAssist = data.TryGetValue("mainAssist", out mainAssist) ? mainAssist : null;

            bool autoPull;
            string autoPullRaw;
            if (data.TryGetValue("autoPullEnabled", out autoPullRaw) && bool.TryParse(autoPullRaw, out autoPull))
            {
                facts.AutoPullEnabledKnown = true;
                facts.AutoPullEnabled = autoPull;
            }

            int holdPercent;
            string holdRaw;
            if (data.TryGetValue("holdManaPercent", out holdRaw) && int.TryParse(holdRaw, NumberStyles.Integer, CultureInfo.InvariantCulture, out holdPercent))
            {
                facts.HoldManaPercentKnown = true;
                facts.HoldManaPercent = holdPercent;
            }

            int completedEncounters;
            string encountersRaw;
            if (data.TryGetValue("completedEncounters", out encountersRaw) &&
                int.TryParse(encountersRaw, NumberStyles.Integer, CultureInfo.InvariantCulture, out completedEncounters))
                facts.CompletedEncounters = completedEncounters;

            return facts;
        }

        private static int? ParseElapsedMinutes(Dictionary<string, string> data)
        {
            string raw;
            int seconds;
            if (!data.TryGetValue("elapsedSeconds", out raw)) return null;
            if (!int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out seconds)) return null;
            return Math.Max(0, seconds / 60);
        }

        internal static List<CampEventFact> ParseEvents(List<Dictionary<string, string>> rows)
        {
            List<CampEventFact> result = new List<CampEventFact>();
            if (rows == null) return result;
            for (int i = 0; i < rows.Count; i++)
            {
                Dictionary<string, string> row = rows[i];
                if (row == null) continue;

                CampEventFact evt = new CampEventFact();
                string seqRaw;
                long seq;
                evt.Sequence = row.TryGetValue("sequence", out seqRaw) && long.TryParse(seqRaw, NumberStyles.Integer, CultureInfo.InvariantCulture, out seq) ? seq : 0L;

                string type;
                evt.Type = row.TryGetValue("type", out type) ? type : null;
                string zone;
                evt.Zone = row.TryGetValue("zone", out zone) ? zone : null;
                string detail;
                evt.Detail = row.TryGetValue("detail", out detail) ? detail : null;

                if (!string.IsNullOrEmpty(evt.Type)) result.Add(evt);
            }
            return result;
        }

        // -----------------------------------------------------------------
        // Deterministic self-tests for the pure parsing logic (no reflection,
        // no game required). Run from /dsguardtest.
        // -----------------------------------------------------------------
        internal static List<string> RunSelfTests()
        {
            List<string> lines = new List<string>();
            int failures = 0;

            failures += Check(lines, "absent snapshot returns null", ParseSnapshot(null) == null);

            failures += Check(lines, "minimal snapshot has only verified fields set", MinimalSnapshotStaysUnknown());
            failures += Check(lines, "full snapshot parses every field", FullSnapshotParsesEveryField());
            failures += Check(lines, "non-HuntCamp mode is inactive", NonHuntCampModeIsInactive());
            failures += Check(lines, "malformed numeric fields are ignored, not zeroed", MalformedNumbersStayUnknown());

            failures += Check(lines, "null event rows return empty list", ParseEvents(null).Count == 0);
            failures += Check(lines, "events without a type are skipped", EventsWithoutTypeAreSkipped());
            failures += Check(lines, "events parse sequence/type/zone/detail", EventsParseFields());

            lines.Add(failures == 0
                ? "[CampmasterBridge] self-tests: ALL PASS"
                : "[CampmasterBridge] self-tests summary: " + failures + " test(s) failed");
            return lines;
        }

        private static int Check(List<string> lines, string name, bool passed)
        {
            lines.Add("[CampmasterBridge] " + name + ": " + (passed ? "PASS" : "FAIL"));
            return passed ? 0 : 1;
        }

        private static bool MinimalSnapshotStaysUnknown()
        {
            Dictionary<string, string> data = new Dictionary<string, string> { { "mode", "HuntCamp" } };
            CampContextFacts facts = ParseSnapshot(data);
            return facts != null && facts.Active &&
                   facts.Zone == null && facts.Puller == null && facts.MainTank == null &&
                   !facts.ElapsedMinutes.HasValue && !facts.AutoPullEnabledKnown && !facts.HoldManaPercentKnown &&
                   facts.CompletedEncounters == 0;
        }

        private static bool FullSnapshotParsesEveryField()
        {
            Dictionary<string, string> data = new Dictionary<string, string>
            {
                { "mode", "HuntCamp" },
                { "state", "Active" },
                { "recognition", "Auto" },
                { "activity", "Fighting" },
                { "zone", "Azure" },
                { "party", "Phanty, Baetil" },
                { "elapsedSeconds", "1620" },
                { "puller", "Phanty" },
                { "mainTank", "Baetil" },
                { "mainAssist", "Baetil" },
                { "autoPullEnabled", "true" },
                { "holdManaPercent", "45" },
                { "completedEncounters", "14" }
            };
            CampContextFacts facts = ParseSnapshot(data);
            return facts != null && facts.Active && facts.State == "Active" && facts.Recognition == "Auto" &&
                   facts.Activity == "Fighting" && facts.Zone == "Azure" && facts.Party == "Phanty, Baetil" &&
                   facts.ElapsedMinutes == 27 && facts.Puller == "Phanty" && facts.MainTank == "Baetil" &&
                   facts.MainAssist == "Baetil" && facts.AutoPullEnabledKnown && facts.AutoPullEnabled &&
                   facts.HoldManaPercentKnown && facts.HoldManaPercent == 45 && facts.CompletedEncounters == 14;
        }

        private static bool NonHuntCampModeIsInactive()
        {
            Dictionary<string, string> data = new Dictionary<string, string> { { "mode", "None" }, { "state", "Inactive" } };
            CampContextFacts facts = ParseSnapshot(data);
            return facts != null && !facts.Active;
        }

        private static bool MalformedNumbersStayUnknown()
        {
            Dictionary<string, string> data = new Dictionary<string, string>
            {
                { "mode", "HuntCamp" },
                { "elapsedSeconds", "not-a-number" },
                { "autoPullEnabled", "maybe" },
                { "holdManaPercent", "half" }
            };
            CampContextFacts facts = ParseSnapshot(data);
            return facts != null && !facts.ElapsedMinutes.HasValue && !facts.AutoPullEnabledKnown && !facts.HoldManaPercentKnown;
        }

        private static bool EventsWithoutTypeAreSkipped()
        {
            List<Dictionary<string, string>> rows = new List<Dictionary<string, string>>
            {
                new Dictionary<string, string> { { "sequence", "1" }, { "zone", "Azure" } },
                null
            };
            return ParseEvents(rows).Count == 0;
        }

        private static bool EventsParseFields()
        {
            List<Dictionary<string, string>> rows = new List<Dictionary<string, string>>
            {
                new Dictionary<string, string>
                {
                    { "sequence", "3" }, { "type", "camp_started" }, { "zone", "Azure" }, { "detail", "declared with /camp here" }
                }
            };
            List<CampEventFact> events = ParseEvents(rows);
            return events.Count == 1 && events[0].Sequence == 3 && events[0].Type == "camp_started" &&
                   events[0].Zone == "Azure" && events[0].Detail == "declared with /camp here";
        }
    }
}
