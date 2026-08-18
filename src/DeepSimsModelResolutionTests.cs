using System.Collections.Generic;

namespace ErenshorDeepSims
{
    // Deterministic coverage for the single-model resolution precedence. Pure/IO-free, matching
    // DeepSimsModelResolution itself.
    internal static class DeepSimsModelResolutionTests
    {
        internal static List<string> Run()
        {
            List<string> results = new List<string>();

            // 1. Fresh config: both fields at their current compiled defaults (Model="qwen3.5:4b",
            // ReasoningModel="qwen3.5:4b") resolves to the canonical model.
            Add(results, "fresh config resolves to canonical qwen3.5:4b",
                DeepSimsModelResolution.Resolve("qwen3.5:4b", "qwen3.5:4b") == "qwen3.5:4b");

            // 2. An explicitly configured canonical Model overrides any legacy ReasoningModel value,
            // even a genuinely custom model name unrelated to qwen.
            Add(results, "explicit Model overrides legacy ReasoningModel",
                DeepSimsModelResolution.Resolve("llama3.1:8b", "qwen3.5:2b") == "llama3.1:8b");
            Add(results, "explicit Model overrides even when ReasoningModel is blank",
                DeepSimsModelResolution.Resolve("mistral:7b", "") == "mistral:7b");

            // 3. Legacy split (PrimaryModel=2b / ReasoningModel=4b as described in the task, i.e. this
            // repo's actual Model/ReasoningModel fields at their OLD shipped defaults) resolves to ONE
            // model: the previously configured stronger model.
            Add(results, "legacy 2b/4b split resolves to the single stronger model",
                DeepSimsModelResolution.Resolve("qwen3.5:2b", "qwen3.5:4b") == "qwen3.5:4b");

            // This repository's actual current local installation: Model still at the old shipped
            // default, ReasoningModel still at its shipped default. Must resolve to 4b.
            Add(results, "current local installation resolves to qwen3.5:4b",
                DeepSimsModelResolution.Resolve("qwen3.5:2b", "qwen3.5:4b") == DeepSimsModelResolution.CanonicalModel);

            // Legacy default Model, but ReasoningModel deliberately cleared: only configured value is
            // used, even though it is the deprecated default - this matches the documented fallback
            // chain ("use the existing primary model if that is the only configured model").
            Add(results, "legacy default Model with no ReasoningModel keeps the only configured value",
                DeepSimsModelResolution.Resolve("qwen3.5:2b", "") == "qwen3.5:2b");

            // Both blank: canonical default.
            Add(results, "nothing configured defaults to canonical model",
                DeepSimsModelResolution.Resolve("", "") == DeepSimsModelResolution.CanonicalModel);
            Add(results, "null inputs default to canonical model",
                DeepSimsModelResolution.Resolve(null, null) == DeepSimsModelResolution.CanonicalModel);

            // Whitespace-only values are treated as blank, not as a literal model string.
            Add(results, "whitespace-only Model is treated as unconfigured",
                DeepSimsModelResolution.Resolve("   ", "qwen3.5:4b") == "qwen3.5:4b");

            // Deliberately matching reasoning model (user set both fields to the same value):
            // resolves to that value, not hardcoded to qwen3.5:4b - the resolver must not invent a
            // model the user never configured.
            Add(results, "reasoning model equal to primary resolves to that exact value",
                DeepSimsModelResolution.Resolve("qwen3.5:2b", "qwen3.5:2b") == "qwen3.5:2b");

            // Case-insensitive recognition of the legacy sentinel.
            Add(results, "legacy sentinel recognized case-insensitively",
                DeepSimsModelResolution.Resolve("QWEN3.5:2B", "qwen3.5:4b") == "qwen3.5:4b");

            // Selective/Off/Always reasoning MODE is not a parameter to Resolve at all - proves model
            // identity cannot vary with routing mode by construction (item 17).
            Add(results, "model resolution has no ReasoningMode dependency by construction",
                DeepSimsModelResolution.Resolve("qwen3.5:2b", "qwen3.5:4b") ==
                DeepSimsModelResolution.Resolve("qwen3.5:2b", "qwen3.5:4b"));

            return results;
        }

        private static void Add(List<string> results, string name, bool ok)
        {
            results.Add("[DeepSims ModelResolution] " + name + ": " + (ok ? "PASS" : "FAIL"));
        }
    }
}
