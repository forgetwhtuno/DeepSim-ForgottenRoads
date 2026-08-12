using System;
using System.Collections.Generic;

namespace ErenshorDeepSims
{
    internal static class ChatRoutingRegression
    {
        internal static List<string> Run()
        {
            List<string> results = new List<string>();

            string[] accepted =
            {
                "Dancer lead us to Vitheo's Watch",
                "Dancer, lead the way to Vitheo's Watch",
                "Dancer lead the group to Vitheo's Watch",
                "Dancer show us the way to Vitheo's Watch",
                "Dancer guide us to Vitheo's Watch"
            };
            for (int i = 0; i < accepted.Length; i++)
                Add(results, "Follow-owned command: " + accepted[i],
                    VanillaGroupCommandClassifier.IsExternalGameplayControlIntent(accepted[i]));

            string[] rejected =
            {
                "can Dancer lead us to Vitheo's Watch?",
                "do you think Dancer can show us the way?",
                "who should guide us to Vitheo's Watch?",
                "I think Dancer should lead us there"
            };
            for (int i = 0; i < rejected.Length; i++)
            {
                Add(results, "conversation not stolen: " + rejected[i],
                    !VanillaGroupCommandClassifier.IsExternalGameplayControlIntent(rejected[i]));
                Add(results, "conversation remains non-vanilla: " + rejected[i],
                    !VanillaGroupCommandClassifier.ShouldLetVanillaHandle(rejected[i]));
            }

            string[] tactical = { "follow", "guard", "attack", "pull", "mana", "run away" };
            for (int i = 0; i < tactical.Length; i++)
                Add(results, "vanilla tactical remains vanilla: " + tactical[i],
                    VanillaGroupCommandClassifier.ShouldLetVanillaHandle(tactical[i]));

            string[] routeQuestions =
            {
                "how do I get to Vitheo's Watch?",
                "where is Vitheo's Watch?",
                "how do we reach Vitheo's Watch?"
            };
            for (int i = 0; i < routeQuestions.Length; i++)
            {
                string query = KnowledgeQueryClassifier.ExtractSearchQuery(routeQuestions[i], "Duskenlight");
                Add(results, "route query extracts destination: " + routeQuestions[i],
                    string.Equals(query, "Vitheo's Watch", StringComparison.Ordinal));
            }

            return results;
        }

        private static void Add(List<string> results, string name, bool pass)
        {
            results.Add("[DeepSims ChatRouting] " + name + ": " + (pass ? "PASS" : "FAIL"));
        }
    }
}
