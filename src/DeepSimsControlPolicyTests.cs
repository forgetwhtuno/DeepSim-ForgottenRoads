using System;
using System.Collections.Generic;

namespace ErenshorDeepSims
{
    internal static class DeepSimsControlPolicyTests
    {
        internal static List<string> Run()
        {
            List<string> r = new List<string>();
            string v;

            Add(r, "social LLM normalizes exact wire casing",
                DeepSimsControlPolicy.TryNormalizeSocialMode("Llm", out v) && v == "LLM" &&
                DeepSimsControlPolicy.ChoiceContains(DeepSimsControlPolicy.SocialModeOptions, v));
            Add(r, "social legacy parser alias normalizes to advertised value",
                DeepSimsControlPolicy.TryNormalizeSocialMode("template", out v) && v == "Templates");
            Add(r, "social arbitrary command rejected", !DeepSimsControlPolicy.TryNormalizeSocialMode("/dstalk do anything", out v));
            Add(r, "activity Lively normalizes", DeepSimsControlPolicy.TryNormalizeActivity("lively", out v) && v == "Lively");
            Add(r, "perspective aliases normalize to advertised wires",
                DeepSimsControlPolicy.TryNormalizePerspective("rp", out v) && v == "Roleplay" &&
                DeepSimsControlPolicy.TryNormalizePerspective("player", out v) && v == "MMO" &&
                !DeepSimsControlPolicy.TryNormalizePerspective("on", out v));
            Add(r, "inference GPU normalizes", DeepSimsControlPolicy.TryNormalizeInferenceMode("gpu", out v) && v == "GPU");
            Add(r, "reasoning aliases normalize to advertised wires",
                DeepSimsControlPolicy.TryNormalizeReasoningMode("selective", out v) && v == "Selective" &&
                DeepSimsControlPolicy.TryNormalizeReasoningMode("on", out v) && v == "Always");
            Add(r, "wire bool accepts only true false",
                DeepSimsControlPolicy.TryParseWireBool("true", out boolValue) && boolValue &&
                DeepSimsControlPolicy.TryParseWireBool("FALSE", out boolValue) && !boolValue &&
                !DeepSimsControlPolicy.TryParseWireBool("1", out boolValue) &&
                !DeepSimsControlPolicy.TryParseWireBool("on", out boolValue));

            Add(r, "public model label hides filesystem paths",
                DeepSimsControlPolicy.SafePublicModelLabel("qwen3.5:4b") == "qwen3.5:4b" &&
                DeepSimsControlPolicy.SafePublicModelLabel(@"X:\models\private-model.gguf") == "custom" &&
                DeepSimsControlPolicy.SafePublicModelLabel("/opt/models/private-model.gguf") == "custom");

            Add(r, "public response status is coarse and privacy-safe",
                DeepSimsControlPolicy.SafeResponseStatusCategory("generating") == "generating" &&
                DeepSimsControlPolicy.SafeResponseStatusCategory(@"C:\profile\private-chat.txt") == "busy" &&
                DeepSimsControlPolicy.SafeResponseStatusCategory("Dancer answering a question") == "busy");

            Dictionary<string, string> snapshot = SafeSnapshot();
            string basic = DeepSimsSuiteDescriptorPolicy.BuildBasicSettings(snapshot);
            string advanced = DeepSimsSuiteDescriptorPolicy.BuildAdvancedSettings(snapshot);
            string developer = DeepSimsSuiteDescriptorPolicy.BuildDeveloperSettings(snapshot);
            string describe = DeepSimsSuiteDescriptorPolicy.BuildDescribe("0.7.1", "Enabled | Roleplay | Ollama idle");

            Add(r, "basic choices current values are advertised", DeepSimsSuiteDescriptorPolicy.AllChoiceValuesAdvertised(basic));
            Add(r, "advanced choices current values are advertised", DeepSimsSuiteDescriptorPolicy.AllChoiceValuesAdvertised(advanced));
            Add(r, "established defaults are advertised choices",
                DeepSimsControlPolicy.ChoiceContains(DeepSimsControlPolicy.SocialModeOptions, DeepSimsControlPolicy.SocialModeOrDefault("Auto")) &&
                DeepSimsControlPolicy.ChoiceContains(DeepSimsControlPolicy.ActivityOptions, DeepSimsControlPolicy.ActivityOrDefault("Adaptive")) &&
                DeepSimsControlPolicy.ChoiceContains(DeepSimsControlPolicy.PerspectiveOptions, DeepSimsControlPolicy.PerspectiveOrDefault("MMO")) &&
                DeepSimsControlPolicy.ChoiceContains(DeepSimsControlPolicy.InferenceModeOptions, DeepSimsControlPolicy.InferenceModeOrDefault("Auto")) &&
                DeepSimsControlPolicy.ChoiceContains(DeepSimsControlPolicy.ReasoningModeOptions, DeepSimsControlPolicy.ReasoningModeOrDefault("Selective")));
            Add(r, "invalid stored choices fall back to advertised defaults",
                DeepSimsControlPolicy.ChoiceContains(DeepSimsControlPolicy.SocialModeOptions, DeepSimsControlPolicy.SocialModeOrDefault("garbage")) &&
                DeepSimsControlPolicy.ChoiceContains(DeepSimsControlPolicy.ActivityOptions, DeepSimsControlPolicy.ActivityOrDefault("garbage")) &&
                DeepSimsControlPolicy.ChoiceContains(DeepSimsControlPolicy.PerspectiveOptions, DeepSimsControlPolicy.PerspectiveOrDefault("garbage")) &&
                DeepSimsControlPolicy.ChoiceContains(DeepSimsControlPolicy.InferenceModeOptions, DeepSimsControlPolicy.InferenceModeOrDefault("garbage")) &&
                DeepSimsControlPolicy.ChoiceContains(DeepSimsControlPolicy.ReasoningModeOptions, DeepSimsControlPolicy.ReasoningModeOrDefault("garbage")));
            Add(r, "Hub settings payloads stay below contract maximum",
                basic.Length <= 8192 && advanced.Length <= 8192 && developer.Length <= 8192);
            Add(r, "LLM wire never regresses to enum Llm", basic.IndexOf("LLM", StringComparison.Ordinal) >= 0 && basic.IndexOf("Llm", StringComparison.Ordinal) < 0);
            Add(r, "descriptor tiers are explicit", AllLinesContainTier(basic, "basic") && AllLinesContainTier(advanced, "advanced") && AllLinesContainTier(developer, "developer"));
            Add(r, "all advertised mutable settings route through policy",
                AllDescriptorSettingsRoute(basic) && AllDescriptorSettingsRoute(advanced) && AllDescriptorSettingsRoute(developer));
            Add(r, "unknown setting and invalid bool rejected",
                !DeepSimsControlPolicy.TryNormalizeSettingValue("endpoint", "http://127.0.0.1", out v) &&
                !DeepSimsControlPolicy.TryNormalizeSettingValue("autonomousSocial", "1", out v));

            Add(r, "every supported choice option round-trips descriptor membership",
                ExerciseChoiceOptions(snapshot, "perspective", DeepSimsControlPolicy.PerspectiveOptions, true) &&
                ExerciseChoiceOptions(snapshot, "socialMode", DeepSimsControlPolicy.SocialModeOptions, true) &&
                ExerciseChoiceOptions(snapshot, "activity", DeepSimsControlPolicy.ActivityOptions, true) &&
                ExerciseChoiceOptions(snapshot, "inferenceMode", DeepSimsControlPolicy.InferenceModeOptions, false) &&
                ExerciseChoiceOptions(snapshot, "reasoningMode", DeepSimsControlPolicy.ReasoningModeOptions, false));

            Add(r, "status text is bounded", DecodedField(describe, "status").Length <= DeepSimsSuiteDescriptorPolicy.MaxHubText);
            string longDescribe = DeepSimsSuiteDescriptorPolicy.BuildDescribe("0.7.1", new string('x', 500));
            Add(r, "oversize status truncates deterministically", DecodedField(longDescribe, "status").Length == DeepSimsSuiteDescriptorPolicy.MaxHubText);
            Add(r, "Hub descriptors expose no sensitive field names",
                !DeepSimsSuiteDescriptorPolicy.ContainsSensitiveFieldName(describe) &&
                !DeepSimsSuiteDescriptorPolicy.ContainsSensitiveFieldName(basic) &&
                !DeepSimsSuiteDescriptorPolicy.ContainsSensitiveFieldName(advanced) &&
                !DeepSimsSuiteDescriptorPolicy.ContainsSensitiveFieldName(developer));
            Add(r, "Hub settings exclude secrets and private-content surfaces",
                AllMissing(basic + "\n" + advanced + "\n" + developer,
                    "ExternalNewsApiKey", "Endpoint", "Memory/", "conversationHistory", "rawMemory", "prompt", "filepath"));

            return r;
        }

        private static bool boolValue;

        private static Dictionary<string, string> SafeSnapshot()
        {
            Dictionary<string, string> d = new Dictionary<string, string>(StringComparer.Ordinal);
            d["perspective"] = "Roleplay";
            d["socialMode"] = "LLM";
            d["activity"] = "Lively";
            d["autonomousSocial"] = "true";
            d["partyChatResponses"] = "true";
            d["wholeParty"] = "true";
            d["eventChatter"] = "true";
            d["idleChatter"] = "true";
            d["simToSim"] = "true";
            d["conversationThreads"] = "true";
            d["conversationSeeding"] = "true";
            d["hybridWhispers"] = "true";
            d["vanillaTyping"] = "true";
            d["wikiLookup"] = "true";
            d["officialNews"] = "true";
            d["externalNews"] = "false";
            d["externalNewsAuto"] = "false";
            d["pauseAutonomousCombat"] = "true";
            d["campmasterIntegration"] = "true";
            d["inferenceMode"] = "Auto";
            d["reasoningMode"] = "Selective";
            d["verboseLogging"] = "false";
            d["seedDiagnostics"] = "false";
            return d;
        }

        private static bool ExerciseChoiceOptions(Dictionary<string, string> snapshot, string id, string optionsCsv, bool basicTier)
        {
            string original = snapshot[id];
            string[] options = optionsCsv.Split(',');
            try
            {
                for (int i = 0; i < options.Length; i++)
                {
                    snapshot[id] = options[i];
                    string payload = basicTier
                        ? DeepSimsSuiteDescriptorPolicy.BuildBasicSettings(snapshot)
                        : DeepSimsSuiteDescriptorPolicy.BuildAdvancedSettings(snapshot);
                    if (!DeepSimsSuiteDescriptorPolicy.AllChoiceValuesAdvertised(payload)) return false;
                    string normalized;
                    if (!DeepSimsControlPolicy.TryNormalizeSettingValue(id, options[i], out normalized)) return false;
                    if (!string.Equals(normalized, options[i], StringComparison.Ordinal)) return false;
                }
                return true;
            }
            finally { snapshot[id] = original; }
        }

        private static bool AllDescriptorSettingsRoute(string payload)
        {
            string[] lines = (payload ?? string.Empty).Replace("\r", string.Empty).Split('\n');
            for (int i = 0; i < lines.Length; i++)
            {
                if (lines[i].Length == 0) continue;
                string id = DecodedField(lines[i], "id");
                string value = DecodedField(lines[i], "value");
                string normalized;
                if (!DeepSimsControlPolicy.TryNormalizeSettingValue(id, value, out normalized)) return false;
                if (!string.Equals(normalized, value, StringComparison.Ordinal)) return false;
            }
            return true;
        }

        private static bool AllLinesContainTier(string payload, string tier)
        {
            string[] lines = (payload ?? string.Empty).Replace("\r", string.Empty).Split('\n');
            if (lines.Length == 0) return false;
            for (int i = 0; i < lines.Length; i++)
                if (lines[i].Length > 0 && !string.Equals(DecodedField(lines[i], "tier"), tier, StringComparison.Ordinal)) return false;
            return true;
        }

        private static string DecodedField(string line, string key)
        {
            string[] pairs = (line ?? string.Empty).Split('&');
            for (int i = 0; i < pairs.Length; i++)
            {
                int eq = pairs[i].IndexOf('=');
                if (eq <= 0) continue;
                string k = Uri.UnescapeDataString(pairs[i].Substring(0, eq));
                if (!string.Equals(k, key, StringComparison.Ordinal)) continue;
                return Uri.UnescapeDataString(pairs[i].Substring(eq + 1));
            }
            return string.Empty;
        }

        private static bool AllMissing(string value, params string[] needles)
        {
            string lower = (value ?? string.Empty).ToLowerInvariant();
            for (int i = 0; i < needles.Length; i++)
                if (lower.IndexOf((needles[i] ?? string.Empty).ToLowerInvariant(), StringComparison.Ordinal) >= 0) return false;
            return true;
        }

        private static void Add(List<string> r, string name, bool ok)
        {
            r.Add("[DeepSims ControlApi] " + name + ": " + (ok ? "PASS" : "FAIL"));
        }
    }
}
