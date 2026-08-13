using System;
using System.Collections.Generic;

namespace ErenshorDeepSims
{
    internal class DeepSlotManager
    {
        private readonly IDeepSimsLog _log;
        private readonly MemoryStore _memory;
        private readonly List<string> _activeNames = new List<string>();
        private readonly Dictionary<string, SimSnapshot> _snapshots = new Dictionary<string, SimSnapshot>(StringComparer.OrdinalIgnoreCase);
        private string _manualSlots = string.Empty;
        private DateTime? _emptySinceUtc;

        // Diagnostics only, for correlating a frame hitch against how many Sims actually joined in
        // the same party-refresh batch. Not read by any gameplay logic.
        internal int LastJoinedCount { get; private set; }

        internal DeepSlotManager(MemoryStore memory, IDeepSimsLog log)
        {
            _memory = memory;
            _log = log;
        }

        internal IList<string> ActiveNames { get { return _activeNames.AsReadOnly(); } }
        internal bool ManualMode { get { return !string.IsNullOrWhiteSpace(_manualSlots); } }

        internal void SetManualSlots(string value)
        {
            _manualSlots = value == null ? string.Empty : value.Trim();
        }

        internal void Refresh(int maxSlots)
        {
            // A normal Erenshor party should fit entirely under this cap. If a raid-sized
            // collection is exposed by the game, DeepSims intentionally enhances only five.
            maxSlots = Math.Max(1, Math.Min(5, maxSlots));
            List<SimSnapshot> activeSims = SimContextReader.GetActiveSims();
            List<string> candidates = ManualMode ? ParseManualCandidates() : PartyResolver.ResolvePartyMemberNames(activeSims);

            // Scene transitions and some Erenshor spawn cycles can briefly expose no party collection.
            // Keep existing slots through a short empty window so zoning does not look like a new friendship session.
            if (!ManualMode && candidates.Count == 0 && _activeNames.Count > 0)
            {
                if (!_emptySinceUtc.HasValue) _emptySinceUtc = DateTime.UtcNow;
                if ((DateTime.UtcNow - _emptySinceUtc.Value).TotalSeconds < 10.0) return;
            }
            else
            {
                _emptySinceUtc = null;
            }

            Dictionary<string, SimSnapshot> activeByName = new Dictionary<string, SimSnapshot>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < activeSims.Count; i++)
            {
                SimSnapshot snap = activeSims[i];
                if (snap != null && !string.IsNullOrWhiteSpace(snap.Name)) activeByName[snap.Name] = snap;
            }

            Dictionary<string, SimSnapshot> currentSnapshots = new Dictionary<string, SimSnapshot>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < candidates.Count; i++)
            {
                SimSnapshot snap;
                if (activeByName.TryGetValue(candidates[i], out snap) && snap != null) currentSnapshots[snap.Name] = snap;
            }

            List<string> next = new List<string>();
            // Preserve existing slots while the Sim is still in the party. This prevents slot churn if collection order changes.
            for (int i = 0; i < _activeNames.Count && next.Count < maxSlots; i++)
            {
                string old = _activeNames[i];
                if (currentSnapshots.ContainsKey(old) && candidates.Exists(delegate(string x) { return string.Equals(x, old, StringComparison.OrdinalIgnoreCase); }))
                    next.Add(old);
            }

            // Prefer Sims with prior DeepSims history, then party order.
            for (int pass = 0; pass < 2 && next.Count < maxSlots; pass++)
            {
                for (int i = 0; i < candidates.Count && next.Count < maxSlots; i++)
                {
                    string name = candidates[i];
                    SimSnapshot snap;
                    if (!currentSnapshots.TryGetValue(name, out snap)) continue;
                    if (next.Exists(delegate(string x) { return string.Equals(x, name, StringComparison.OrdinalIgnoreCase); })) continue;
                    bool hasHistory = _memory.HasHistory(snap.Key);
                    if ((pass == 0 && hasHistory) || (pass == 1 && !hasHistory)) next.Add(name);
                }
            }

            // Record departures before replacing the slot map.
            for (int i = 0; i < _activeNames.Count; i++)
            {
                string old = _activeNames[i];
                if (!next.Exists(delegate(string x) { return string.Equals(x, old, StringComparison.OrdinalIgnoreCase); }))
                {
                    SimSnapshot oldSnap;
                    if (_snapshots.TryGetValue(old, out oldSnap)) _memory.RecordGroupLeave(oldSnap);
                }
            }

            int joinedCount = 0;
            for (int i = 0; i < next.Count; i++)
            {
                string name = next[i];
                SimSnapshot snap = currentSnapshots[name];
                bool isNew = !_activeNames.Exists(delegate(string x) { return string.Equals(x, name, StringComparison.OrdinalIgnoreCase); });
                if (isNew)
                {
                    joinedCount++;
                    _memory.RecordGroupJoin(snap);
                }
                _memory.RecordZone(snap, snap.Scene);
                _memory.RecordLevelIfChanged(snap);
            }
            LastJoinedCount = joinedCount;

            _activeNames.Clear();
            _snapshots.Clear();
            for (int i = 0; i < next.Count; i++)
            {
                _activeNames.Add(next[i]);
                _snapshots[next[i]] = currentSnapshots[next[i]];
            }
        }

        internal bool IsDeepSim(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return false;
            return _activeNames.Exists(delegate(string x) { return string.Equals(x, name, StringComparison.OrdinalIgnoreCase); });
        }

        internal List<SimSnapshot> GetActiveSnapshots()
        {
            List<SimSnapshot> result = new List<SimSnapshot>();
            for (int i = 0; i < _activeNames.Count; i++)
            {
                SimSnapshot snap = GetSnapshot(_activeNames[i]);
                if (snap != null) result.Add(snap);
            }
            return result;
        }

        internal SimSnapshot GetSnapshot(string name)
        {
            SimSnapshot snap;
            if (_snapshots.TryGetValue(name, out snap)) return snap;
            return null;
        }

        internal string Describe()
        {
            if (_activeNames.Count == 0)
                return ManualMode ? "No active Deep Sims (manual slots did not match active Sims)." : "No Deep Sims detected in the current party.";
            List<string> parts = new List<string>();
            for (int i = 0; i < _activeNames.Count; i++)
            {
                SimSnapshot snap = GetSnapshot(_activeNames[i]);
                parts.Add((i + 1) + ": " + (snap == null ? _activeNames[i] : snap.SummaryLine()));
            }
            return string.Join(" | ", parts.ToArray()) + (ManualMode ? " [manual]" : " [auto]");
        }

        private List<string> ParseManualCandidates()
        {
            List<string> names = new List<string>();
            string[] split = _manualSlots.Split(new char[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries);
            for (int i = 0; i < split.Length; i++)
            {
                string n = split[i].Trim();
                if (!string.IsNullOrWhiteSpace(n)) names.Add(n);
            }
            return names;
        }
    }
}
