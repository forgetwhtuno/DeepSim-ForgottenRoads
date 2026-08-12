using System;
using System.Collections.Generic;

namespace ErenshorDeepSims
{
    // Read-only compatibility boundary for Erenshor's actual Manage Roles storage. These members
    // were established from the installed Assembly-CSharp call graph (see Campmaster's
    // CAMP_PHASE1_ASSEMBLY_FINDINGS.md). Any read failure leaves the whole snapshot unknown rather
    // than falling back to class guesses.
    internal static class NativeRoleReader
    {
        internal static void ApplyTo(IList<SimSnapshot> sims)
        {
            if (sims == null) return;
            for (int i = 0; i < sims.Count; i++)
            {
                if (sims[i] == null) continue;
                sims[i].RoleAssignmentsKnown = false;
                if (sims[i].AssignedRoles == null) sims[i].AssignedRoles = new List<string>();
                else sims[i].AssignedRoles.Clear();
            }

            SimPlayerGrouping grouping;
            try { grouping = GameData.SimPlayerGrouping; }
            catch { return; }
            if (grouping == null) return;

            try
            {
                AddSingle(sims, grouping.MainTank, "Main Tank");
                // DesignatedMA is the player's actual assignment. MainAssist is volatile during
                // combat and must not be described as the configured Manage Roles choice.
                AddSingle(sims, grouping.DesignatedMA, "Main Assist");
                AddSingle(sims, grouping.Puller, "Puller");
                AddMany(sims, grouping.CC, "Crowd Control");
                AddMany(sims, grouping.Heals, "Healing/Mana");
                for (int i = 0; i < sims.Count; i++)
                    if (sims[i] != null) sims[i].RoleAssignmentsKnown = true;
            }
            catch
            {
                // Partial role state is more dangerous than no role state. Clear everything and
                // fail closed so prompts and grounding cannot treat a half-read roster as exact.
                for (int i = 0; i < sims.Count; i++)
                {
                    if (sims[i] == null) continue;
                    sims[i].RoleAssignmentsKnown = false;
                    sims[i].AssignedRoles.Clear();
                }
            }
        }

        private static void AddSingle(IList<SimSnapshot> sims, SimPlayerTracking tracking, string role)
        {
            AddByName(sims, TrackingName(tracking), role);
        }

        private static void AddMany(IList<SimSnapshot> sims, IList<SimPlayerTracking> trackings, string role)
        {
            if (trackings == null) return;
            for (int i = 0; i < trackings.Count; i++) AddByName(sims, TrackingName(trackings[i]), role);
        }

        private static string TrackingName(SimPlayerTracking tracking)
        {
            if (tracking == null || string.IsNullOrWhiteSpace(tracking.SimName)) return string.Empty;
            return tracking.SimName.Trim();
        }

        private static void AddByName(IList<SimSnapshot> sims, string name, string role)
        {
            if (string.IsNullOrWhiteSpace(name)) return;
            for (int i = 0; i < sims.Count; i++)
            {
                SimSnapshot sim = sims[i];
                if (sim == null || !string.Equals(sim.Name, name, StringComparison.OrdinalIgnoreCase)) continue;
                if (!sim.AssignedRoles.Contains(role)) sim.AssignedRoles.Add(role);
                return;
            }
        }
    }
}
