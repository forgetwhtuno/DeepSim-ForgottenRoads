using HarmonyLib;
using System;
using UnityEngine;

namespace ErenshorDeepSims
{
    // These hooks mirror stable event points already used by other open-source Erenshor mods.
    // They observe player events only; Erenshor still owns all gameplay decisions and execution.

    // Erenshor's player object is looked up by name, which is far too expensive to repeat inside a
    // hook that runs on every point of damage in the scene. Cache it and let Unity's overloaded null
    // check re-acquire it automatically: on scene teardown the old object compares equal to null.
    internal static class PlayerObjectCache
    {
        private static GameObject _player;
        private static int _lastFailedLookupFrame = -1;

        internal static GameObject Get()
        {
            if (_player != null) return _player;
            // Throttle to one failed lookup per frame so a long load screen cannot spin on Find.
            int frame = Time.frameCount;
            if (frame == _lastFailedLookupFrame) return null;
            _player = GameObject.Find("Player");
            if (_player == null) _lastFailedLookupFrame = frame;
            return _player;
        }

        internal static bool Is(GameObject candidate)
        {
            if (candidate == null) return false;
            GameObject player = Get();
            return player != null && ReferenceEquals(candidate, player);
        }
    }

    [HarmonyPatch(typeof(Stats), "DoLevelUp")]
    internal static class DeepSimsPlayerLevelPatch
    {
        [HarmonyPostfix]
        private static void Postfix(Stats __instance)
        {
            try
            {
                if (DeepSimsPlugin.Instance == null) return;
                if (__instance != null && !PlayerObjectCache.Is(__instance.gameObject)) return;
                PlayerSnapshot player = SimContextReader.GetPlayerSnapshot();
                string text = player != null && player.Level > 0
                    ? "The player just reached level " + player.Level + "."
                    : "The player just leveled up.";
                DeepSimsPlugin.Instance.NotifyObservedGameEvent("player_level_up", text, 80, true, 1.0);
            }
            catch { }
        }
    }

    [HarmonyPatch(typeof(Respawn), "RespawnPlayer")]
    internal static class DeepSimsPlayerRevivePatch
    {
        [HarmonyPostfix]
        private static void Postfix()
        {
            try
            {
                if (DeepSimsPlugin.Instance == null) return;
                DeepSimsPlugin.Instance.NotifyObservedGameEvent("player_revive", "The player just respawned after dying.", 55, false, 0.75);
            }
            catch { }
        }
    }

    [HarmonyPatch(typeof(GameData), "FinishQuest", new Type[] { typeof(string) })]
    internal static class DeepSimsQuestCompletePatch
    {
        [HarmonyPostfix]
        private static void Postfix(string __0)
        {
            try
            {
                if (DeepSimsPlugin.Instance == null) return;
                string quest = string.IsNullOrWhiteSpace(__0) ? "a quest" : "the quest '" + __0 + "'";
                DeepSimsPlugin.Instance.NotifyObservedGameEvent("quest_complete", "The player just completed " + quest + ".", 70, true, 0.80);
            }
            catch { }
        }
    }
}


namespace ErenshorDeepSims
{
    // Single observer for Stats.ReduceHP. This previously lived in three separate Harmony patches on
    // the same method, each re-deriving who was hit and two of them calling GameObject.Find per tick.
    // One patch, one cached actor classification, one player lookup.
    //
    // Lightweight combat awareness only. This does not drive actions; it tells the social layer that
    // damage is happening now, and gives kill telemetry a direct fallback when the visible log line is
    // delayed or missed by a particular Erenshor UI path.
    [HarmonyPatch(typeof(Stats), "ReduceHP")]
    internal static class DeepSimsDamageObserverPatch
    {
        private sealed class ActorInfo
        {
            public GameObject Go;
            public bool IsLocalPlayer;
            public bool IsRemoteCoopHuman;
            public bool IsSim;
            public bool IsNpc;
            public bool IsPetLike;
            public string EnemyName;

            // True only for hostile scenery/NPCs: not the player, not a Sim, not a remote co-op human,
            // not a pet. These are the only actors whose deaths count as party kills.
            public bool IsEnemy
            {
                get { return IsNpc && !IsSim && !IsLocalPlayer && !IsRemoteCoopHuman && !IsPetLike; }
            }
        }

        private static readonly System.Collections.Generic.Dictionary<int, ActorInfo> ActorCache =
            new System.Collections.Generic.Dictionary<int, ActorInfo>();

        [HarmonyPrefix]
        private static void Prefix(Stats __instance, ref float __state)
        {
            try { __state = __instance == null ? -1f : Convert.ToSingle(__instance.CurrentHP); }
            catch { __state = -1f; }
        }

        [HarmonyPostfix]
        private static void Postfix(Stats __instance, float __state)
        {
            try
            {
                if (DeepSimsPlugin.Instance == null || __instance == null || __state < 0f) return;
                float current;
                try { current = Convert.ToSingle(__instance.CurrentHP); }
                catch { return; }
                if (current >= __state) return;

                ActorInfo actor = GetActorInfo(__instance);
                if (actor == null) return;

                // ReduceHP exposes the victim but not the damage source. A nearby damaged NPC may
                // belong to another player/NPC fight, so proximity can never establish party combat.
                // Player damage is authoritative for party involvement; enemy damage is forwarded
                // only as an untrusted target hint and SessionTelemetry must corroborate it.
                string target = actor.IsEnemy ? actor.EnemyName : string.Empty;
                if (actor.IsLocalPlayer) DeepSimsPlugin.Instance.NotifyCombatActivity();
                else if (!string.IsNullOrWhiteSpace(target)) DeepSimsPlugin.Instance.NotifyCombatActivity(target);

                // Everything below is the alive -> dead transition only, so each death reports once.
                if (__state <= 0f || current > 0f) return;

                if (actor.IsLocalPlayer)
                {
                    string scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
                    DeepSimsPlugin.Instance.NotifyObservedGameEvent("player_death", "The player just died in " + scene + ".", 90, true, 1.0);
                    return;
                }

                if (actor.IsSim)
                {
                    if (!IsActivePartySim(actor.Go)) return;
                    string name = ReadSimName(actor.Go);
                    if (string.IsNullOrWhiteSpace(name)) return;
                    DeepSimsPlugin.Instance.NotifyObservedGameEvent("sim_death",
                        name + " was defeated in " + UnityEngine.SceneManagement.SceneManager.GetActiveScene().name + ".", 70, true, 0.70);
                    return;
                }

                if (!actor.IsEnemy) return;
                if (string.IsNullOrWhiteSpace(actor.EnemyName) || string.Equals(actor.EnemyName, "enemy", StringComparison.OrdinalIgnoreCase)) return;
                DeepSimsPlugin.Instance.NotifyEnemyKilledDirect(actor.EnemyName);
            }
            catch { }
        }

        private static string ReadSimName(GameObject go)
        {
            if (go == null) return string.Empty;
            SimPlayer sim = null;
            try { sim = go.GetComponent<SimPlayer>(); }
            catch { }
            if (sim == null)
            {
                try { sim = go.GetComponentInParent<SimPlayer>(); }
                catch { }
            }
            if (sim == null) return go.name;
            SimSnapshot snap = SimContextReader.BuildSnapshot(sim);
            return snap == null || string.IsNullOrWhiteSpace(snap.Name) ? go.name : snap.Name;
        }

        private static bool IsActivePartySim(GameObject go)
        {
            try
            {
                if (go == null || DeepSimsPlugin.Instance == null) return false;
                SimPlayer dyingSim = go.GetComponent<SimPlayer>();
                if (dyingSim == null) dyingSim = go.GetComponentInParent<SimPlayer>();
                string dyingName = ReadSimName(go);
                System.Collections.Generic.List<SimSnapshot> active = DeepSimsPlugin.Instance.GetActiveDeepSims();
                for (int i = 0; i < active.Count; i++)
                {
                    SimSnapshot snap = active[i];
                    if (snap == null) continue;
                    if (dyingSim != null && snap.RuntimeSim != null && ReferenceEquals(dyingSim, snap.RuntimeSim)) return true;
                    if (snap.RuntimeSim == null && !string.IsNullOrWhiteSpace(dyingName) &&
                        string.Equals(snap.Name, dyingName, StringComparison.OrdinalIgnoreCase)) return true;
                }
                return false;
            }
            catch { return false; }
        }

        private static ActorInfo GetActorInfo(Stats stats)
        {
            if (stats == null || stats.gameObject == null) return null;
            GameObject go = stats.gameObject;
            int id = stats.GetInstanceID();
            ActorInfo cached;
            if (ActorCache.TryGetValue(id, out cached) && cached != null && ReferenceEquals(cached.Go, go)) return cached;

            ActorInfo info = new ActorInfo();
            info.Go = go;
            string rootName = string.Empty;
            try { rootName = go.transform == null || go.transform.root == null ? go.name : go.transform.root.name; }
            catch { rootName = go.name; }
            info.IsLocalPlayer = string.Equals(go.name, "Player", StringComparison.OrdinalIgnoreCase) ||
                                 string.Equals(rootName, "Player", StringComparison.OrdinalIgnoreCase);

            SimPlayer sim = null;
            try { sim = go.GetComponent<SimPlayer>(); }
            catch { }
            if (sim == null)
            {
                try { sim = go.GetComponentInParent<SimPlayer>(); }
                catch { }
            }
            info.IsRemoteCoopHuman = CoopCompatibility.IsRemoteCoopHuman(sim);
            info.IsSim = sim != null && !info.IsRemoteCoopHuman && !info.IsLocalPlayer;

            NPC npc = null;
            try { npc = go.GetComponent<NPC>(); }
            catch { }
            if (npc == null)
            {
                try { npc = go.GetComponentInParent<NPC>(); }
                catch { }
            }
            info.IsNpc = npc != null;
            string combinedName = (go.name ?? string.Empty) + " " + rootName;
            info.IsPetLike = combinedName.IndexOf("pet", StringComparison.OrdinalIgnoreCase) >= 0;
            info.EnemyName = info.IsNpc && !info.IsSim && !info.IsLocalPlayer && !info.IsRemoteCoopHuman
                ? SessionTelemetry.ReadActorName(go) : string.Empty;

            if (ActorCache.Count > 1024) ActorCache.Clear();
            ActorCache[id] = info;
            return info;
        }
    }
}
