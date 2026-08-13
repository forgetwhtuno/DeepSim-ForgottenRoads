using System;
using System.Collections;
using System.Reflection;
using UnityEngine;

namespace ErenshorDeepSims
{
    // Optional, reflection-only compatibility with mizuki's Erenshor COOP. No COOP assembly
    // reference is taken, so Deep Sims remains usable when the co-op mod is absent.
    internal static class CoopCompatibility
    {
        // COOP 2.3.1 exposes these types at the root namespace. Keep the earlier client-namespace
        // spelling as a fallback for any older/forked builds.
        private const string NetworkedPlayerType = "ErenshorCoop.NetworkedPlayer";
        private const string LegacyNetworkedPlayerType = "ErenshorCoop.Client.NetworkedPlayer";
        private const string NetworkedSimType = "ErenshorCoop.NetworkedSim";
        private const string ClientConnectionManagerType = "ErenshorCoop.Client.ClientConnectionManager";
        private const string ServerConnectionManagerType = "ErenshorCoop.Server.ServerConnectionManager";
        private const string ClientGroupType = "ErenshorCoop.Client.Grouping.ClientGroup";
        private const string GameHooksType = "ErenshorCoop.GameHooks";

        // Type resolution used to walk every loaded assembly on each call, from paths as hot as
        // per-chat-line and per-damage-tick. Resolve once and invalidate only when the CLR actually
        // loads something new, which covers another loader/plugin manager loading COOP after this plugin.
        private static readonly object ResolveLock = new object();
        private static volatile bool _resolved;
        private static Type _networkedPlayer;
        private static Type _legacyNetworkedPlayer;
        private static Type _networkedSim;
        private static Type _clientConnectionManager;
        private static Type _serverConnectionManager;
        private static Type _clientGroup;
        private static Type _gameHooks;

        static CoopCompatibility()
        {
            try { AppDomain.CurrentDomain.AssemblyLoad += OnAssemblyLoad; }
            catch { }
        }

        private static void OnAssemblyLoad(object sender, AssemblyLoadEventArgs args)
        {
            _resolved = false;
        }

        // Lunaris can unload this assembly at runtime. AppDomain events outlive plugin GameObjects, so
        // an unremoved handler would retain a delegate into the old Deep Sims assembly. Clear both the
        // handler and reflected cross-mod types during teardown; a reloaded assembly gets fresh statics.
        internal static void Shutdown()
        {
            try { AppDomain.CurrentDomain.AssemblyLoad -= OnAssemblyLoad; }
            catch { }
            lock (ResolveLock)
            {
                _resolved = false;
                _networkedPlayer = null;
                _legacyNetworkedPlayer = null;
                _networkedSim = null;
                _clientConnectionManager = null;
                _serverConnectionManager = null;
                _clientGroup = null;
                _gameHooks = null;
            }
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
                        if (_networkedPlayer == null) _networkedPlayer = assembly.GetType(NetworkedPlayerType, false);
                        if (_legacyNetworkedPlayer == null) _legacyNetworkedPlayer = assembly.GetType(LegacyNetworkedPlayerType, false);
                        if (_networkedSim == null) _networkedSim = assembly.GetType(NetworkedSimType, false);
                        if (_clientConnectionManager == null) _clientConnectionManager = assembly.GetType(ClientConnectionManagerType, false);
                        if (_serverConnectionManager == null) _serverConnectionManager = assembly.GetType(ServerConnectionManagerType, false);
                        if (_clientGroup == null) _clientGroup = assembly.GetType(ClientGroupType, false);
                        if (_gameHooks == null) _gameHooks = assembly.GetType(GameHooksType, false);
                    }
                }
                catch { }
                _resolved = true;
            }
        }

        internal static bool IsCoopInstalled()
        {
            EnsureResolved();
            return _networkedPlayer != null || _legacyNetworkedPlayer != null;
        }

        // True only when a co-op session is actually live. Merely having COOP installed must not
        // disable Deep Sims for someone playing solo with the mod sitting in their profile.
        internal static bool IsCoopSessionActive()
        {
            if (!IsCoopInstalled()) return false;
            try
            {
                object manager = GetConnectionManager(_clientConnectionManager);
                object server = GetConnectionManager(_serverConnectionManager);
                if (manager == null && server == null) return false;

                if (ReadRunning(manager) || ReadRunning(server)) return true;

                // Fallback for forks without the property: any known remote peer means a live session.
                IDictionary players = ReadPlayers(manager);
                return players != null && players.Count > 0;
            }
            catch { }
            return false;
        }

        // The bundled COOP host runs both its server and its local client. Requiring both live
        // managers distinguishes it from a connected client and fails closed on unknown/forked APIs.
        // This method is called from Unity's main thread; reflected manager objects never leave it.
        internal static bool CanOwnSocialDirector(out string reason)
        {
            reason = string.Empty;
            if (!IsCoopSessionActive()) return true;
            object client = GetConnectionManager(_clientConnectionManager);
            object server = GetConnectionManager(_serverConnectionManager);
            if (client != null && server != null && ReadRunning(client) && ReadRunning(server)) return true;
            reason = server == null || _serverConnectionManager == null
                ? "COOP host role could not be verified; Deep Sims fails closed in this session"
                : "this COOP peer is a client, not the local host";
            return false;
        }

        internal static bool IsRemoteCoopHuman(SimPlayer sim)
        {
            if (sim == null) return false;
            EnsureResolved();
            if (_networkedPlayer == null && _legacyNetworkedPlayer == null) return false;
            return HasAnyComponent(sim, _networkedPlayer, _legacyNetworkedPlayer);
        }

        // A Sim owned and driven by another co-op client. The host should not treat these as its own
        // Deep Sims: two machines would generate competing dialogue for the same character.
        internal static bool IsRemoteCoopSim(SimPlayer sim)
        {
            if (sim == null) return false;
            EnsureResolved();
            if (_networkedSim == null) return false;
            return HasAnyComponent(sim, _networkedSim, null);
        }

        private static bool HasAnyComponent(SimPlayer sim, Type first, Type second)
        {
            GameObject go = sim.gameObject;
            if (go == null) return false;
            try
            {
                // GetComponent(Type) avoids the Component[] allocation the previous GetComponents
                // scan performed for every Sim on every party poll.
                if (first != null && go.GetComponent(first) != null) return true;
                if (second != null && go.GetComponent(second) != null) return true;
            }
            catch { }
            return false;
        }

        // COOP 2.3.1's public SendMessageToPlayers API targets every same-zone peer, not the party.
        // There is no safe public party-recipient overload in the bundled source, so remote Deep Sim
        // group speech deliberately fails closed and remains visible on the host only.
        internal static bool TryBroadcastChat(string text, string color)
        {
            return false;
        }

        internal static bool IsRemoteCoopPlayerName(string speaker)
        {
            if (string.IsNullOrWhiteSpace(speaker) || !IsCoopInstalled()) return false;
            try
            {
                IDictionary players = ReadPlayers(GetConnectionManager(_clientConnectionManager));
                if (players == null) return false;
                string wanted = speaker.Trim();
                foreach (DictionaryEntry entry in players)
                {
                    object player = entry.Value;
                    if (player == null) continue;
                    FieldInfo nameField = player.GetType().GetField("entityName", BindingFlags.Public | BindingFlags.Instance);
                    string name = nameField == null ? null : nameField.GetValue(player) as string;
                    if (string.Equals(name, wanted, StringComparison.OrdinalIgnoreCase)) return true;
                }
            }
            catch { }
            return false;
        }

        internal static bool IsVerifiedRemotePartyMemberName(string speaker)
        {
            if (string.IsNullOrWhiteSpace(speaker) || !IsCoopSessionActive()) return false;
            try
            {
                object manager = GetConnectionManager(_clientConnectionManager);
                IDictionary players = ReadPlayers(manager);
                if (players == null || _clientGroup == null) return false;
                short playerId = -1;
                foreach (DictionaryEntry entry in players)
                {
                    object player = entry.Value;
                    if (player == null) continue;
                    FieldInfo nameField = player.GetType().GetField("entityName", BindingFlags.Public | BindingFlags.Instance);
                    string name = nameField == null ? null : nameField.GetValue(player) as string;
                    if (!string.Equals(name, speaker.Trim(), StringComparison.OrdinalIgnoreCase)) continue;
                    try { playerId = Convert.ToInt16(entry.Key); } catch { playerId = -1; }
                    break;
                }
                if (playerId < 0) return false;

                FieldInfo currentGroupField = _clientGroup.GetField("currentGroup", BindingFlags.Public | BindingFlags.Static);
                object group = currentGroupField == null ? null : currentGroupField.GetValue(null);
                if (group == null) return false;
                FieldInfo listField = group.GetType().GetField("groupList", BindingFlags.Public | BindingFlags.Instance);
                IEnumerable members = listField == null ? null : listField.GetValue(group) as IEnumerable;
                if (members == null) return false;
                foreach (object member in members)
                {
                    if (member == null) continue;
                    FieldInfo idField = member.GetType().GetField("entityID", BindingFlags.Public | BindingFlags.Instance);
                    FieldInfo simField = member.GetType().GetField("isSim", BindingFlags.Public | BindingFlags.Instance);
                    if (idField == null || simField == null) continue;
                    short id = Convert.ToInt16(idField.GetValue(member));
                    bool isSim = Convert.ToBoolean(simField.GetValue(member));
                    if (!isSim && id == playerId) return true;
                }
            }
            catch { }
            return false;
        }

        private static object GetConnectionManager(Type managerType)
        {
            EnsureResolved();
            if (managerType == null) return null;
            try
            {
                FieldInfo instanceField = managerType.GetField("Instance", BindingFlags.Public | BindingFlags.Static);
                return instanceField == null ? null : instanceField.GetValue(null);
            }
            catch { return null; }
        }

        private static bool ReadRunning(object manager)
        {
            if (manager == null) return false;
            try
            {
                PropertyInfo running = manager.GetType().GetProperty("IsRunning", BindingFlags.Public | BindingFlags.Instance);
                object value = running == null || running.GetIndexParameters().Length != 0 ? null : running.GetValue(manager, null);
                return value is bool && (bool)value;
            }
            catch { return false; }
        }

        private static IDictionary ReadPlayers(object manager)
        {
            if (manager == null) return null;
            try
            {
                FieldInfo playersField = manager.GetType().GetField("Players", BindingFlags.Public | BindingFlags.Instance);
                return playersField == null ? null : playersField.GetValue(manager) as IDictionary;
            }
            catch { return null; }
        }
    }
}
