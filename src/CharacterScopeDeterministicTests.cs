using System;
using System.Collections.Generic;

namespace ErenshorDeepSims
{
    internal static class CharacterScopeDeterministicTests
    {
        internal static List<string> Run()
        {
            List<string> r = new List<string>();
            Add(r, "slot-qualified key", CharacterScopeKey.Compose("Brinon", 0) == "slot0_brinon");
            Add(r, "same name different slots stay separate", CharacterScopeKey.Compose("Brinon", 0) != CharacterScopeKey.Compose("Brinon", 1));
            Add(r, "unsafe name characters sanitized", CharacterScopeKey.Compose("A B!", 2) == "slot2_a_b_");
            Add(r, "unverified slot falls back to name", CharacterScopeKey.Compose("Brinon", -1) == "brinon");
            Add(r, "empty name uses player fallback", CharacterScopeKey.Compose(null, -1) == "player");
            Add(r, "same scope and conversation may commit delayed memory",
                CharacterScopeWriteGuard.CanCommit(4, 4, 9, 9));
            Add(r, "character switch rejects delayed memory commit",
                !CharacterScopeWriteGuard.CanCommit(4, 5, 9, 10));
            Add(r, "conversation supersession rejects delayed memory commit",
                !CharacterScopeWriteGuard.CanCommit(4, 4, 9, 10));
            return r;
        }

        private static void Add(List<string> r, string name, bool ok)
        {
            r.Add("[DeepSims CharacterScope] " + name + ": " + (ok ? "PASS" : "FAIL"));
        }
    }
}
