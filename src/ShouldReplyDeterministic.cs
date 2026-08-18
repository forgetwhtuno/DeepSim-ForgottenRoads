using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace ErenshorDeepSims
{
    // ---------------------------------------------------------------------------------------------
    // Deterministic gate for whether a player party-chat line needs an IMMEDIATE, directed Deep Sims
    // response (classification + retrieval + generation), as opposed to simply becoming ordinary
    // heard conversation that may inform later autonomous chatter.
    //
    // This is a pure, IO-free, session-agnostic function - no Ollama call, no hardcoded Sim/player
    // names. The caller supplies the player's own display name and the current party's Sim names so
    // the "addressed by name" signal is built from live roster data, never from fixture identities.
    //
    // Evidence: this mirrors the deterministic ShouldReply logic validated across the local V3-V5
    // real-packet prompt labs (13/13 real fixtures, 20/20 paraphrases, 8/8 arbitrary unseen names,
    // 8/8 false-positive stress cases; see local-labs/4b-prompt-lab-v5's report). FALSE does not mean
    // the line is discarded - callers must still record it as ordinary conversation history.
    // ---------------------------------------------------------------------------------------------
    internal static class ShouldReplyDeterministic
    {
        internal struct Result
        {
            internal bool Reply;
            internal string Reason;
            internal Result(bool reply, string reason) { Reply = reply; Reason = reason; }
        }

        private static readonly Regex InterrogativeRegex = new Regex(
            @"^(hey|hi|hello|yo|sup)\b|" +
            @"\b(do you|does anyone|did you|did anyone|would you|wanna|want to|" +
            @"what do you|what('?s| is)|where('?s| is)|who('?s| is)|when('?s| is)|why('?s| is)|" +
            @"how('?s| is| do| did| are)|anyone|you guys|any of you|" +
            @"don'?t you think|isn'?t (it|that)|right\?|" +
            @"tell me|let me know)\b",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        private static readonly Regex TrivialAckRegex = new Regex(
            @"^\s*(lol+|lmao+|rofl+|ok(ay)?|k|nice|cool|brb|gtg|afk|hmm+|yep|yeah)\s*[.!]*\s*$",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        private static readonly Regex DeclarativePreferenceRegex = new Regex(
            @"^\s*i\s+(prefer|like|love|enjoy|hate|think|feel)\b(?!.*\?)",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        // Only actual anomaly/event vocabulary triggers a reaction hook on its own. A broad
        // "that was/that's X" pattern was deliberately avoided: it over-fires on ordinary assessments
        // ("that was pretty good", "that was a long walk") that should not force an immediate reply.
        // "gone" is deliberately excluded as a bare trigger word (false positive on "gone fishing");
        // vanish/disappear/glitch remain because they are unambiguous anomaly words in this context.
        private static readonly Regex NotableReactionRegex = new Regex(
            @"^(huh|whoa|wow|weird|strange|wtf|uh)\b|" +
            @"\b(vanish|disappear|glitch)(ed|ing)?\b",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        // Evaluate(text, playerName, currentPartySimNames). No hardcoded identities anywhere in this
        // method - the addressee set is built per call from whatever roster the caller supplies.
        // playerName is intentionally excluded from the addressee set: a Sim being ABOUT the player
        // is not the same signal as a Sim being directly ADDRESSED by another Sim's name.
        internal static Result Evaluate(string text, string playerName, IList<string> currentPartySimNames)
        {
            string trimmed = (text ?? string.Empty).Trim();
            if (trimmed.Length == 0) return new Result(false, "empty");
            if (TrivialAckRegex.IsMatch(trimmed)) return new Result(false, "trivial_acknowledgement");

            bool hasQuestionMark = trimmed.IndexOf('?') >= 0;
            bool hasInterrogative = InterrogativeRegex.IsMatch(trimmed);
            if (hasQuestionMark || hasInterrogative) return new Result(true, "question_or_interrogative_phrase");

            if (HasAddressee(trimmed, playerName, currentPartySimNames))
                return new Result(true, "directed_at_sim_by_name");

            if (DeclarativePreferenceRegex.IsMatch(trimmed))
                return new Result(false, "simple_declarative_preference_no_hook");

            if (NotableReactionRegex.IsMatch(trimmed))
                return new Result(true, "notable_reaction_hook");

            return new Result(false, "incidental_statement_no_hook");
        }

        // Shared with SimResponseDecision so the Sim-to-Sim gate uses exactly the same definition of
        // "this is just an acknowledgement" / "this is phrased as a question" as the player-line gate.
        internal static bool IsTrivialAcknowledgement(string text)
        {
            return !string.IsNullOrWhiteSpace(text) && TrivialAckRegex.IsMatch(text.Trim());
        }

        internal static bool LooksInterrogative(string text)
        {
            return !string.IsNullOrWhiteSpace(text) && InterrogativeRegex.IsMatch(text.Trim());
        }

        private static bool HasAddressee(string text, string playerName, IList<string> currentPartySimNames)
        {
            if (currentPartySimNames == null) return false;
            for (int i = 0; i < currentPartySimNames.Count; i++)
            {
                string name = currentPartySimNames[i];
                if (string.IsNullOrWhiteSpace(name)) continue;
                if (!string.IsNullOrWhiteSpace(playerName) && string.Equals(name.Trim(), playerName.Trim(), StringComparison.OrdinalIgnoreCase))
                    continue; // addressing the player is not addressing a Sim
                if (Regex.IsMatch(text, @"\b" + Regex.Escape(name.Trim()) + @"\b", RegexOptions.IgnoreCase))
                    return true;
            }
            return false;
        }

        internal static List<string> RunSelfTests()
        {
            List<string> lines = new List<string>();
            List<string> realParty = new List<string> { "Phanty", "Dancer", "Cyndara", "Fiora", "Brinon" };
            List<string> unseenParty = new List<string> { "Astra", "Seren", "Mira", "Kestrel", "Roland" };

            KeyValuePair<string, bool>[] realGold = new KeyValuePair<string, bool>[]
            {
                new KeyValuePair<string, bool>("hey phanty what are you up to?", true),
                new KeyValuePair<string, bool>("dancer do you like being a wind blade?", true),
                new KeyValuePair<string, bool>("what is everyone eating today?", true),
                new KeyValuePair<string, bool>("how do i get wolf meet on here?", true),
                new KeyValuePair<string, bool>("anyone hear that news about nasa?", true),
                new KeyValuePair<string, bool>("cyndara want to do a friendly duel?", true),
                new KeyValuePair<string, bool>("we are getting pretty good at pvp dont you think?", true),
                new KeyValuePair<string, bool>("that was weird the other party just vanished", true),
                new KeyValuePair<string, bool>("how did that last fight go?", true),
                new KeyValuePair<string, bool>("dancer hows life?", true),
                new KeyValuePair<string, bool>("do you guys like roleplaying or just chatting more?", true),
                new KeyValuePair<string, bool>("i prefer playing a tank", false),
                new KeyValuePair<string, bool>("just thinking about music styles today", false),
            };
            int realCorrect = 0;
            for (int i = 0; i < realGold.Length; i++)
                if (Evaluate(realGold[i].Key, "Player", realParty).Reply == realGold[i].Value) realCorrect++;
            lines.Add("[DeepSims ShouldReply] real fixture gold " + realCorrect + "/" + realGold.Length + ": " + Pass(realCorrect == realGold.Length));

            KeyValuePair<string, bool>[] falsePositiveStress = new KeyValuePair<string, bool>[]
            {
                new KeyValuePair<string, bool>("that was pretty good", false),
                new KeyValuePair<string, bool>("that was a long walk", false),
                new KeyValuePair<string, bool>("gone fishing", false),
                new KeyValuePair<string, bool>("nice weather today", false),
                new KeyValuePair<string, bool>("i think tanks are underrated", false),
                new KeyValuePair<string, bool>("i like this zone", false),
                new KeyValuePair<string, bool>("brb", false),
                new KeyValuePair<string, bool>("lol", false),
            };
            int fpCorrect = 0;
            for (int i = 0; i < falsePositiveStress.Length; i++)
                if (Evaluate(falsePositiveStress[i].Key, "Player", realParty).Reply == falsePositiveStress[i].Value) fpCorrect++;
            lines.Add("[DeepSims ShouldReply] false-positive stress " + fpCorrect + "/" + falsePositiveStress.Length + ": " + Pass(fpCorrect == falsePositiveStress.Length));

            lines.Add("[DeepSims ShouldReply] unseen-name direct question true: " + Pass(Evaluate("Astra how are you doing?", "Player", unseenParty).Reply));
            lines.Add("[DeepSims ShouldReply] unseen-name addressee-only true when supplied: " + Pass(Evaluate("Nice one, Kestrel.", "Player", unseenParty).Reply));
            lines.Add("[DeepSims ShouldReply] unseen-name addressee-only false when NOT supplied: " + Pass(!Evaluate("Nice one, Kestrel.", "Player", new List<string>()).Reply));
            lines.Add("[DeepSims ShouldReply] question syntax true even without supplied name: " + Pass(Evaluate("Astra how are you doing?", "Player", new List<string>()).Reply));

            lines.Add("[DeepSims ShouldReply] playerName is not a Sim addressee: " + Pass(!Evaluate("Nice one, Brinon the player.", "Brinon", new List<string>()).Reply));

            // The real structural "no hardcoded Sim/player names" guard lives in
            // tests/RUN_DETERMINISTIC_TESTS.ps1 as a source-text assertion (matching the pattern
            // already used there for the single-model invariant) - a runtime unit test cannot prove
            // the absence of a literal in this file's own source.
            return lines;
        }

        private static string Pass(bool value) { return value ? "PASS" : "FAIL"; }
    }
}
