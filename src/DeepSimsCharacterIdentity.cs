using System;

namespace ErenshorDeepSims
{
    // Live GameData wiring around CharacterScopeKey. This mirrors the already-live-tested character
    // scoping pattern used by Journal/Contracts/Nemesis, and never reads or writes Erenshor save files.
    internal static class DeepSimsCharacterIdentity
    {
        internal static bool IsLocalCharacterReady()
        {
            try
            {
                return !GameData.InCharSelect && GameData.PlayerControl != null && GameData.PlayerControl.Myself != null &&
                    GameData.PlayerControl.Myself.MyStats != null && GameData.PlayerControl.Myself.gameObject.activeInHierarchy;
            }
            catch { return false; }
        }

        internal static bool IsCharacterSelectActive()
        {
            try { return GameData.InCharSelect; }
            catch { return false; }
        }

        internal static string ResolveCharacterKey()
        {
            return CharacterScopeKey.Compose(PlayerName(), ResolveSlotIndex());
        }

        internal static string PlayerName()
        {
            try
            {
                string name = GameData.PlayerControl.Myself.MyStats.MyName;
                return string.IsNullOrWhiteSpace(name) ? "Player" : name.Trim();
            }
            catch { return "Player"; }
        }

        private static int ResolveSlotIndex()
        {
            try
            {
                SaveGameData active = GameData.CurrentCharacterSlot != null ? GameData.CurrentCharacterSlot : GameData.ActiveSaveSlot;
                if (active == null || active.index < 0) return -1;
                string recorded = (active.CharName ?? string.Empty).Trim();
                if (recorded.Length > 0 && !string.Equals(recorded, PlayerName(), StringComparison.OrdinalIgnoreCase)) return -1;
                return active.index;
            }
            catch { return -1; }
        }
    }
}
