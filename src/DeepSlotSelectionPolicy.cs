using System;
using System.Collections.Generic;

namespace ErenshorDeepSims
{
    // Manual Deep Sim configuration is only an enhancement filter over the authoritative native roster.
    // It can remove a native party member from Deep Sim eligibility; it can never add membership.
    internal static class DeepSlotSelectionPolicy
    {
        internal static List<string> FilterManualNativeCandidates(IList<string> nativeCandidates, string manualSlots)
        {
            List<string> result = new List<string>();
            if (nativeCandidates == null || string.IsNullOrWhiteSpace(manualSlots)) return result;
            HashSet<string> requested = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            string[] split = manualSlots.Split(new char[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries);
            for (int i = 0; i < split.Length; i++)
            {
                string value = split[i] == null ? string.Empty : split[i].Trim();
                if (value.Length > 0) requested.Add(value);
            }
            if (requested.Count == 0) return result;
            for (int i = 0; i < nativeCandidates.Count; i++)
            {
                string native = nativeCandidates[i];
                if (!string.IsNullOrWhiteSpace(native) && requested.Contains(native)) result.Add(native);
            }
            return result;
        }
    }
}
