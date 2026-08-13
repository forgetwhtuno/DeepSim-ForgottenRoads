using System;
using Lunaris.IPC;

namespace ErenshorDeepSims
{
    // Thin optional transport adapter for Suite Hub Aura v1. The provider consumes only the
    // primitive, allowlisted DeepSimsControlApi surface and never references ErenshorSuiteHub.dll.
    // Descriptor construction lives in DeepSimsSuiteDescriptorPolicy so the same exact wire values
    // are exercised by deterministic tests (especially ordinal choice membership/casing).
    internal sealed class DeepSimsSuiteAuraProvider
    {
        private const string Prefix = "forgetwhtuno.erenshor.suite." + DeepSimsControlApi.ModuleId + ".v1.";
        private const int MaxFieldLength = 200;

        private readonly IAuraProvider<string> _describe;
        private readonly IAuraProvider<string> _basicSettings;
        private readonly IAuraProvider<string> _advancedSettings;
        private readonly IAuraProvider<string> _developerSettings;
        private readonly IAuraProvider<string, string, string> _setSetting;
        private readonly IAuraProvider<string, string, string> _action;

        private readonly Func<string> _describeFunc;
        private readonly Func<string> _basicSettingsFunc;
        private readonly Func<string> _advancedSettingsFunc;
        private readonly Func<string> _developerSettingsFunc;
        private readonly Func<string, string, string> _setSettingFunc;
        private readonly Func<string, string, string> _actionFunc;

        private bool _registered;

        internal DeepSimsSuiteAuraProvider(DeepSimsPlugin owner)
        {
            if (owner == null) throw new ArgumentNullException("owner");
            _describe = owner.IPCAuraProvider<string>(Prefix + "describe");
            _basicSettings = owner.IPCAuraProvider<string>(Prefix + "settings.basic");
            _advancedSettings = owner.IPCAuraProvider<string>(Prefix + "settings.advanced");
            _developerSettings = owner.IPCAuraProvider<string>(Prefix + "settings.developer");
            _setSetting = owner.IPCAuraProvider<string, string, string>(Prefix + "setting.set");
            _action = owner.IPCAuraProvider<string, string, string>(Prefix + "action");

            _describeFunc = BuildDescribe;
            _basicSettingsFunc = BuildBasicSettings;
            _advancedSettingsFunc = BuildAdvancedSettings;
            _developerSettingsFunc = BuildDeveloperSettings;
            _setSettingFunc = HandleSetSetting;
            _actionFunc = HandleAction;
        }

        internal void Register()
        {
            if (_registered) return;
            try
            {
                _describe.RegisterFunc(_describeFunc);
                _basicSettings.RegisterFunc(_basicSettingsFunc);
                _advancedSettings.RegisterFunc(_advancedSettingsFunc);
                _developerSettings.RegisterFunc(_developerSettingsFunc);
                _setSetting.RegisterFunc(_setSettingFunc);
                _action.RegisterFunc(_actionFunc);
                _registered = true;
            }
            catch
            {
                UnregisterAll();
                throw;
            }
        }

        // Unregister mutations first, then all read endpoints, before the owning plugin tears down
        // queues/integrations. This prevents stale Aura references from calling a half-destroyed
        // plugin during Lunaris hot unload/reload.
        internal void Unregister()
        {
            UnregisterAll();
        }

        private void UnregisterAll()
        {
            // Always attempt every endpoint: this also cleans up a partially-completed Register().
            try { _setSetting.UnregisterFunc(); } catch { }
            try { _action.UnregisterFunc(); } catch { }
            try { _developerSettings.UnregisterFunc(); } catch { }
            try { _advancedSettings.UnregisterFunc(); } catch { }
            try { _basicSettings.UnregisterFunc(); } catch { }
            try { _describe.UnregisterFunc(); } catch { }
            _registered = false;
        }

        private static string BuildDescribe()
        {
            return DeepSimsSuiteDescriptorPolicy.BuildDescribe(
                DeepSimsPlugin.PluginVersion,
                DeepSimsControlApi.GetHubStatus());
        }

        private static string BuildBasicSettings()
        {
            return DeepSimsSuiteDescriptorPolicy.BuildBasicSettings(DeepSimsControlApi.GetSettingsSnapshot());
        }

        private static string BuildAdvancedSettings()
        {
            return DeepSimsSuiteDescriptorPolicy.BuildAdvancedSettings(DeepSimsControlApi.GetSettingsSnapshot());
        }

        private static string BuildDeveloperSettings()
        {
            return DeepSimsSuiteDescriptorPolicy.BuildDeveloperSettings(DeepSimsControlApi.GetSettingsSnapshot());
        }

        private static string HandleSetSetting(string settingId, string value)
        {
            string failure;
            bool ok = DeepSimsControlApi.TrySetSetting(settingId, value, out failure);
            return ok ? "ok" : ("error: " + Bound(failure ?? "rejected", MaxFieldLength));
        }

        // Only advertised, bounded actions are accepted. This endpoint deliberately does not expose
        // arbitrary commands or internal methods.
        private static string HandleAction(string actionId, string argument)
        {
            if (!string.Equals(actionId, "refreshStatus", StringComparison.OrdinalIgnoreCase))
                return "error: unknown action";
            string failure;
            bool ok = DeepSimsControlApi.TryRefreshStatus(out failure);
            return ok ? "ok" : ("error: " + Bound(failure ?? "refresh failed", MaxFieldLength));
        }

        private static string Bound(string value, int max)
        {
            if (string.IsNullOrEmpty(value) || value.Length <= max) return value ?? string.Empty;
            return value.Substring(0, max);
        }
    }
}
