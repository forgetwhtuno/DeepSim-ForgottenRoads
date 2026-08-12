using System;
using System.Collections.Generic;

namespace ErenshorSharedContracts
{
    // Single source of truth for the cross-mod PvP result contract.
    //
    // Three separately loaded BepInEx plugins share no assembly, so Erenshor PvP owns
    // classification while Erenshor Nemesis and Deep Sims each carry a local mirror used only
    // against pre-v2 PvP builds. This file is compiled into all three and pins every one of them
    // to the same table, so a mirror that drifts fails that mod's own self-test instead of quietly
    // mis-describing a match in game.
    //
    // The file is optional: each build script includes it when present and defines
    // SHARED_CONTRACTS, so a standalone copy of a single mod still compiles without it.
    //
    // Depends on nothing but mscorlib. Do not add Unity, BepInEx, or mod-specific references here.
    internal static class PvpContractConformance
    {
        internal const int ContractVersion = 2;

        // Field layout of one ErenshorPvpApi.RecentResults() row:
        // "sequence|match_id|opponent|outcome|mode|classification|utc_ticks"
        internal const int EncodedFieldCount = 7;
        internal const int SequenceField = 0;
        internal const int MatchIdField = 1;
        internal const int OpponentField = 2;
        internal const int OutcomeField = 3;
        internal const int ModeField = 4;
        internal const int ClassificationField = 5;
        internal const int UtcTicksField = 6;

        internal const string PlayerWin = "player_win";
        internal const string NemesisWin = "nemesis_win";
        internal const string PlayerFled = "player_fled";
        internal const string EnemyRetreated = "enemy_retreated";
        internal const string Cancelled = "cancelled";
        internal const string Invalid = "invalid";

        // Every raw outcome token any PvP code path can produce, and the verdict it must map to.
        // Adding a new termination reason means adding it here first; anything unlisted must
        // classify as invalid so an unrecognised failure is never treated as a real result.
        private static readonly string[][] Cases =
        {
            // Verified fight verdicts.
            new[] { "proxy_death", PlayerWin },
            new[] { "player_death", NemesisWin },
            new[] { "player_fled", PlayerFled },
            new[] { "retreat", EnemyRetreated },
            // Ended without a verdict.
            new[] { "scene_transition", Cancelled },
            new[] { "manual", Cancelled },
            new[] { "shutdown", Cancelled },
            new[] { "timer", Cancelled },
            new[] { "cleanup", Cancelled },
            // Never a legitimate escape: interference, internal failure, or a failed start.
            new[] { "third_party_aggro", Invalid },
            new[] { "fight_state_failed", Invalid },
            new[] { "team_spawn_failed", Invalid },
            new[] { "proxy_spawn_failed", Invalid },
            new[] { "combat_start_failed", Invalid },
            new[] { "target_rejected", Invalid },
            new[] { "failure", Invalid },
            new[] { "unknown", Invalid },
            new[] { "", Invalid },
            new[] { "not_a_real_reason", Invalid }
        };

        // Tokens that must never be mistaken for a decided fight. Kept separate so the intent
        // survives even if someone reorders the table above.
        private static readonly string[] MustNotBeDecisive =
        {
            "third_party_aggro", "fight_state_failed", "team_spawn_failed",
            "proxy_spawn_failed", "combat_start_failed", "scene_transition", "manual", "shutdown", "timer"
        };

        // Tokens that must never be reported as an escape/disengagement.
        private static readonly string[] MustNotBeEscape =
        {
            "third_party_aggro", "fight_state_failed", "team_spawn_failed",
            "proxy_spawn_failed", "combat_start_failed", "unknown", ""
        };

        internal static bool IsDecisive(string classification)
        { return classification == PlayerWin || classification == NemesisWin; }

        internal static bool IsEscape(string classification)
        { return classification == PlayerFled || classification == EnemyRetreated; }

        internal static bool AdvancesRivalry(string classification)
        { return IsDecisive(classification) || IsEscape(classification); }

        // Runs one classifier against the shared table. `label` names the implementation so a
        // failure says which mod's mirror drifted.
        internal static string RunClassifierConformance(string label, Func<string, string> classifier)
        {
            if (classifier == null) return "FAIL " + label + " classifier missing";
            for (int i = 0; i < Cases.Length; i++)
            {
                string outcome = Cases[i][0], expected = Cases[i][1];
                string actual = Safe(classifier, outcome);
                if (actual != expected)
                    return "FAIL " + label + " classify '" + outcome + "' expected " + expected + " got " + actual;
            }
            // A null token must behave like an empty one rather than throwing.
            if (Safe(classifier, null) != Invalid) return "FAIL " + label + " classify null";
            // Callers pass raw engine tokens, so casing and stray whitespace must not change meaning.
            if (Safe(classifier, "  Proxy_Death ") != PlayerWin) return "FAIL " + label + " classify is case/whitespace sensitive";
            if (Safe(classifier, "RETREAT") != EnemyRetreated) return "FAIL " + label + " classify is case sensitive";
            for (int i = 0; i < MustNotBeDecisive.Length; i++)
                if (IsDecisive(Safe(classifier, MustNotBeDecisive[i])))
                    return "FAIL " + label + " reports '" + MustNotBeDecisive[i] + "' as a decided fight";
            for (int i = 0; i < MustNotBeEscape.Length; i++)
                if (IsEscape(Safe(classifier, MustNotBeEscape[i])))
                    return "FAIL " + label + " reports '" + MustNotBeEscape[i] + "' as an escape";
            return "PASS " + label + " classifier conformance";
        }

        // Validates the encoded row shape a consumer parses out of RecentResults().
        internal static string RunRowConformance(string label, string[] rows)
        {
            if (rows == null) return "FAIL " + label + " rows missing";
            long previous = long.MinValue;
            List<string> seen = new List<string>();
            for (int i = 0; i < rows.Length; i++)
            {
                string[] fields = (rows[i] ?? string.Empty).Split(new[] { '|' });
                if (fields.Length != EncodedFieldCount)
                    return "FAIL " + label + " row " + i + " has " + fields.Length + " fields, expected " + EncodedFieldCount;
                long sequence;
                if (!long.TryParse(fields[SequenceField], out sequence))
                    return "FAIL " + label + " row " + i + " sequence is not a number";
                if (sequence <= previous) return "FAIL " + label + " rows are not in ascending sequence order";
                previous = sequence;
                if (fields[MatchIdField].Length == 0) return "FAIL " + label + " row " + i + " has no match id";
                if (seen.Contains(fields[MatchIdField])) return "FAIL " + label + " match id " + fields[MatchIdField] + " appears twice";
                seen.Add(fields[MatchIdField]);
                if (!IsKnownClassification(fields[ClassificationField]))
                    return "FAIL " + label + " row " + i + " classification '" + fields[ClassificationField] + "' is not a contract value";
                long ticks;
                if (!long.TryParse(fields[UtcTicksField], out ticks) || ticks <= 0)
                    return "FAIL " + label + " row " + i + " utc ticks are not a positive number";
            }
            return "PASS " + label + " row conformance (" + rows.Length + " rows)";
        }

        internal static bool IsKnownClassification(string value)
        {
            return value == PlayerWin || value == NemesisWin || value == PlayerFled ||
                   value == EnemyRetreated || value == Cancelled || value == Invalid;
        }

        private static string Safe(Func<string, string> classifier, string outcome)
        {
            try { return classifier(outcome) ?? string.Empty; } catch { return "<threw>"; }
        }
    }
}
