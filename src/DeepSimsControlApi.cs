using System;
using System.Collections.Generic;
using ForgottenRoads.StandaloneUi;

namespace ErenshorDeepSims
{
    // Optional primitive-only status/control surface for Suite Hub or other late-bound consumers.
    // No Hub dependency, Unity objects, raw memories, arbitrary command execution, or gameplay actions
    // are exposed here. Consumers may bind this type by reflection at any time.
    public static class DeepSimsControlApi
    {
        // SchemaVersion is the internal primitive-status-snapshot shape version consumed by
        // GetStatusSnapshot()/BuildControlStatusSnapshot. ApiVersion is the canonical Suite
        // integration contract version; the two are independent so Deep Sims can evolve either
        // surface without forcing a lockstep bump on the other.
        public const int SchemaVersion = 1;
        public const int ApiVersion = 1;
        public const string ModuleId = "deepsims";
        public static bool HasDedicatedPanel { get { return true; } }
        public static bool IsPanelOpen { get { return StandaloneFallbackUi.IsOpen; } }

        public static bool IsAvailable { get { return DeepSimsPlugin.Instance != null; } }

        public static Dictionary<string, string> GetStatusSnapshot()
        {
            DeepSimsPlugin plugin = DeepSimsPlugin.Instance;
            if (plugin == null)
            {
                return new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    { "schemaVersion", SchemaVersion.ToString() },
                    { "source", "ErenshorDeepSims" },
                    { "available", "false" },
                    { "version", DeepSimsPlugin.PluginVersion }
                };
            }
            return plugin.BuildControlStatusSnapshot(SchemaVersion);
        }

        // This is intentionally a separate allowlisted snapshot from GetStatusSnapshot().
        // It contains only settings that are safe and useful to expose through Suite Hub.
        // Endpoint URLs, API keys, model filesystem details, memory paths/contents and conversation
        // history are structurally absent rather than merely hidden by the Hub.
        public static Dictionary<string, string> GetSettingsSnapshot()
        {
            DeepSimsPlugin plugin = DeepSimsPlugin.Instance;
            return plugin == null
                ? new Dictionary<string, string>(StringComparer.Ordinal)
                : plugin.BuildControlSettingsSnapshot();
        }

        public static string GetHubStatus()
        {
            DeepSimsPlugin plugin = DeepSimsPlugin.Instance;
            return plugin == null ? "Unavailable" : plugin.BuildControlHubStatus();
        }

        public static bool TrySetSetting(string settingId, string value, out string failure)
        {
            DeepSimsPlugin plugin = DeepSimsPlugin.Instance;
            if (plugin == null) { failure = "Deep Sims is not loaded."; return false; }
            return plugin.TrySetControlSetting(settingId, value, out failure);
        }

        public static bool TrySetSocialMode(string mode, out string failure)
        {
            return TrySetSetting("socialMode", mode, out failure);
        }

        public static bool TrySetActivity(string preset, out string failure)
        {
            return TrySetSetting("activity", preset, out failure);
        }

        public static bool TrySetPerspective(string perspective, out string failure)
        {
            return TrySetSetting("perspective", perspective, out failure);
        }

        public static bool TrySetRoleplay(bool enabled, out string failure)
        {
            return TrySetPerspective(enabled ? "Roleplay" : "MMO", out failure);
        }

        public static bool TryRefreshStatus(out string failure)
        {
            DeepSimsPlugin plugin = DeepSimsPlugin.Instance;
            if (plugin == null) { failure = "Deep Sims is not loaded."; return false; }
            return plugin.TryRefreshControlStatus(out failure);
        }

        public static bool OpenPanel() { return StandaloneFallbackUi.Open(); }
        public static bool ClosePanel() { return StandaloneFallbackUi.Close(); }
    }
}
