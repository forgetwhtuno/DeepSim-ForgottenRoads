using System;

namespace ErenshorDeepSims
{
    // ---------------------------------------------------------------------------------------------
    // Single-model pipeline.
    //
    // Deep Sims previously could route a request to one of two Ollama models: an ordinary "Model"
    // and a stronger "ReasoningModel" selected per-call for factual/history/correction turns. Because
    // Ollama requests carry keep_alive, that could leave BOTH models resident at once. Deep Sims now
    // requests exactly one canonical model for every call; ReasoningMode/ShouldUseReasoning still
    // exist as a routing/diagnostic signal (see PromptBuilder.ShouldUseReasoning) but no longer select
    // a different model.
    //
    // This class is pure and IO-free so its precedence rules are exercised by the deterministic test
    // suite outside Unity/Lunaris.
    // ---------------------------------------------------------------------------------------------
    internal static class DeepSimsModelResolution
    {
        // The single model Deep Sims requests once no legacy configuration says otherwise.
        internal const string CanonicalModel = "qwen3.5:4b";

        // Sentinel matching the OLD split-architecture shipped default for the primary "Model"
        // setting. This is a frozen historical constant used only to recognize an untouched legacy
        // config during the one-time migration below; it is independent of CanonicalModel and of
        // whatever DeepSimsSettings.Model's own compiled default is today.
        private const string LegacyDefaultPrimaryModel = "qwen3.5:2b";

        // Precedence, applied once at startup migration (see DeepSimsPlugin's ConfigVersion-gated
        // migration block) and available as a pure function for testing:
        //
        //   1. An explicitly configured Model that differs from the old shipped primary default is
        //      authoritative, whatever it is - including a genuinely custom model name.
        //   2. Otherwise (Model is blank, or still exactly the old primary default) a non-blank
        //      configured ReasoningModel is honored, because under the old architecture it
        //      represented the user's previously chosen higher-quality model.
        //   3. Otherwise, if Model has SOME value (even the old default) and no ReasoningModel is
        //      configured at all, that Model is the only configured value and is used as-is.
        //   4. Otherwise (nothing usable configured): CanonicalModel.
        //
        // Note this cannot perfectly distinguish "user explicitly chose qwen3.5:2b" from "this is
        // simply the untouched historical default" - both look identical once persisted. That
        // ambiguity is unavoidable and is resolved conservatively in favor of the previously
        // configured stronger model, matching the documented precedence.
        internal static string Resolve(string configuredModel, string configuredReasoningModel)
        {
            string model = Clean(configuredModel);
            string reasoning = Clean(configuredReasoningModel);

            if (model.Length > 0 && !EqualsOrdinalIgnoreCase(model, LegacyDefaultPrimaryModel)) return model;
            if (reasoning.Length > 0) return reasoning;
            if (model.Length > 0) return model;
            return CanonicalModel;
        }

        private static string Clean(string value) { return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim(); }

        private static bool EqualsOrdinalIgnoreCase(string a, string b) { return string.Equals(a, b, StringComparison.OrdinalIgnoreCase); }
    }
}
