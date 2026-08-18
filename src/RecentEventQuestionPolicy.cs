using System;
using System.Text.RegularExpressions;

namespace ErenshorDeepSims
{
    // ---------------------------------------------------------------------------------------------
    // Deterministic handling for "how did that last fight go?" style questions when Deep Sims has NO
    // authoritative recorded result (world.Outing.LastEncounter is empty). The real-packet prompt
    // labs (see local-labs/4b-prompt-lab-v3..v5) found repeatedly that even an explicit "say you don't
    // know" instruction in the prompt does not reliably stop qwen3.5:4b from inventing a win/loss, a
    // cause, another Sim's presumed knowledge, or a future investigation ("I'll ask around") once
    // generation is allowed to run at all. When the authoritative answer is genuinely unknown, this
    // is not a prompting problem - Deep Sims already knows the true answer is "no data", so the
    // correct fix is to never ask the model in the first place. Zero Ollama calls.
    // ---------------------------------------------------------------------------------------------
    internal static class RecentEventQuestionPolicy
    {
        private static readonly Regex LastFightRegex = new Regex(
            @"\b(?:last|previous|that)\s+(?:fight|encounter|battle)\b|" +
            @"\bhow\s+(?:was|did|'d)\s+that\s+fight\b|" +
            @"\bwhat\s+happened(?:\s+(?:there|back\s+there|with\s+that))?\b",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        // A small bounded set of natural, honest-uncertainty replies. Selection is deterministic
        // (stable hash of speaker + player message), never random and never a second LLM call, so the
        // same question from the same speaker in the same turn always answers the same way while
        // different turns/speakers still vary rather than repeating one fixed sentence forever.
        private static readonly string[] UncertaintyReplies = new string[]
        {
            "Not sure how that one ended. What happened?",
            "I don't know how it finished - what happened?",
            "Hard to say. How did it end?",
            "Couldn't tell you how that wrapped up. What happened?",
            "No idea how that one went. What happened?",
        };

        // True only when: the player's question is asking about the MOST RECENT COMPLETED encounter,
        // AND no authoritative completed-encounter summary exists yet. A fight that is instead
        // currently in progress, or a whole-outing question, is intentionally out of scope here - this
        // policy exists specifically for the "we have literally no recorded answer" case.
        internal static bool TryGetDeterministicUnknownReply(string playerMessage, WorldSnapshot world, string speakerName, out string reply)
        {
            reply = string.Empty;
            if (string.IsNullOrWhiteSpace(playerMessage)) return false;
            if (!LastFightRegex.IsMatch(playerMessage)) return false;
            if (world == null || world.Outing == null) return false;
            if (!string.IsNullOrWhiteSpace(world.Outing.LastEncounter)) return false; // an answer IS known; let generation handle it normally
            if (!string.IsNullOrWhiteSpace(world.Outing.CurrentEncounter)) return false; // a fight is live; that is a different, answerable question

            int hash = StableHash((speakerName ?? string.Empty) + "|" + playerMessage);
            reply = UncertaintyReplies[Math.Abs(hash == int.MinValue ? 0 : hash) % UncertaintyReplies.Length];
            return true;
        }

        private static int StableHash(string value)
        {
            unchecked
            {
                int h = 17;
                string v = value ?? string.Empty;
                for (int i = 0; i < v.Length; i++) h = h * 31 + v[i];
                return h;
            }
        }

        internal static System.Collections.Generic.List<string> RunSelfTests()
        {
            System.Collections.Generic.List<string> lines = new System.Collections.Generic.List<string>();

            WorldSnapshot noOuting = new WorldSnapshot();
            string reply;
            lines.Add("[DeepSims RecentEvent] no outing data does not crash / stays false: " +
                Pass(!TryGetDeterministicUnknownReply("how did that last fight go?", noOuting, "Dancer", out reply)));

            WorldSnapshot unknownOuting = new WorldSnapshot { Outing = new OutingSnapshot() };
            bool matched = TryGetDeterministicUnknownReply("how did that last fight go?", unknownOuting, "Dancer", out reply);
            lines.Add("[DeepSims RecentEvent] unknown last-fight result triggers deterministic reply: " + Pass(matched && !string.IsNullOrWhiteSpace(reply)));

            WorldSnapshot knownOuting = new WorldSnapshot { Outing = new OutingSnapshot { LastEncounter = "The party defeated a lone wolf." } };
            lines.Add("[DeepSims RecentEvent] known last-fight result does NOT short-circuit generation: " +
                Pass(!TryGetDeterministicUnknownReply("how did that last fight go?", knownOuting, "Dancer", out reply)));

            WorldSnapshot liveOuting = new WorldSnapshot { Outing = new OutingSnapshot { CurrentEncounter = "Fighting a lone wolf." } };
            lines.Add("[DeepSims RecentEvent] live current fight does NOT trigger the last-fight-unknown reply: " +
                Pass(!TryGetDeterministicUnknownReply("how did that last fight go?", liveOuting, "Dancer", out reply)));

            lines.Add("[DeepSims RecentEvent] unrelated question does not match: " +
                Pass(!TryGetDeterministicUnknownReply("what is everyone eating today?", unknownOuting, "Dancer", out reply)));

            string replyA, replyB;
            bool okA = TryGetDeterministicUnknownReply("how did that last fight go?", unknownOuting, "Dancer", out replyA);
            bool okB = TryGetDeterministicUnknownReply("how did that last fight go?", unknownOuting, "Dancer", out replyB);
            lines.Add("[DeepSims RecentEvent] same speaker/message deterministically repeats, does not randomize: " + Pass(okA && okB && replyA == replyB));

            return lines;
        }

        private static string Pass(bool value) { return value ? "PASS" : "FAIL"; }
    }
}
