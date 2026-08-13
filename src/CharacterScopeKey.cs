using System;
using System.Text;

namespace ErenshorDeepSims
{
    // Pure character-scope key composition shared by the live resolver and deterministic tests.
    // The suite convention is verified save-slot index + live character name when both agree;
    // name-only is the conservative fallback when the slot cannot be proven.
    internal static class CharacterScopeKey
    {
        internal const string Unscoped = "_unscoped";

        internal static string Compose(string playerName, int verifiedSlotIndex)
        {
            string safe = SafeKey(playerName);
            return verifiedSlotIndex >= 0 ? "slot" + verifiedSlotIndex + "_" + safe : safe;
        }

        internal static string SafeKey(string value)
        {
            string source = string.IsNullOrWhiteSpace(value) ? "player" : value.Trim().ToLowerInvariant();
            StringBuilder sb = new StringBuilder(Math.Min(48, source.Length));
            for (int i = 0; i < source.Length && sb.Length < 48; i++)
            {
                char c = source[i];
                sb.Append(char.IsLetterOrDigit(c) ? c : '_');
            }
            return sb.Length == 0 ? "player" : sb.ToString();
        }
    }
}
