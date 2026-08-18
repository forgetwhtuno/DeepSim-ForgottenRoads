using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace ErenshorDeepSims
{
    // How strongly a party line invites another present Sim to answer it. This is a graded signal
    // rather than a boolean: a plain-but-substantive remark should occasionally draw a reply, while a
    // direct question or a named address should almost always draw one.
    internal enum SimReplyUrge
    {
        None = 0,
        Weak = 1,
        Normal = 2,
        Strong = 3
    }

    // ---------------------------------------------------------------------------------------------
    // Deterministic gate for "a Sim just said something in party chat - should another Sim answer?".
    //
    // ShouldReplyDeterministic answers the same question for a line the PLAYER typed. This is its
    // Sim-to-Sim counterpart, and the two differ deliberately: a player line that is merely
    // incidental should not force a directed response, but a Sim line that is merely incidental is
    // exactly the kind of remark a party member would still react to now and then. So this evaluator
    // never returns a hard "no" for an ordinary substantive statement - it returns Weak, and the
    // caller's momentum curve turns that into an occasional reply rather than a guaranteed one.
    //
    // Pure and IO-free: no Ollama call, no Unity types, no hardcoded Sim/player names. The caller
    // supplies the speaker and the live party roster, so the "addressed by name" signal always comes
    // from current membership.
    // ---------------------------------------------------------------------------------------------
    internal static class SimResponseDecision
    {
        // The user-visible contract: one party line may draw at most this many Sim responses before
        // the group falls quiet and waits for the player. A cap, never a target.
        internal const int MaxResponsesPerLine = 3;

        internal struct Result
        {
            internal SimReplyUrge Urge;
            internal string Reason;
            internal Result(SimReplyUrge urge, string reason) { Urge = urge; Reason = reason; }
        }

        // Contrast, hedging, and explicit disagreement all leave the floor open for someone else.
        private static readonly Regex ContrastRegex = new Regex(
            @"\b(actually|but|though|however|disagree|not sure|really\?|seriously|on the other hand|" +
            @"either way|depends|maybe|i guess|honestly)\b",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        // A stated opinion or preference is a social offer: agreeing, teasing, or countering it is a
        // natural next turn for another Sim.
        private static readonly Regex OpinionRegex = new Regex(
            @"\b(i (think|feel|reckon|prefer|like|love|hate|enjoy|miss|swear)|" +
            @"my (favou?rite|pick|take)|" +
            @"(always|never) (been|felt|liked)|" +
            @"we should|we ought|let'?s)\b",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        // Anomaly / surprise vocabulary. Same intent as the player-side reaction hook.
        private static readonly Regex ReactionRegex = new Regex(
            @"^(huh|whoa|wow|weird|strange|wtf|uh|oh)\b|" +
            @"\b(vanish|disappear|glitch)(ed|ing)?\b",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        // Evaluate(text, speakerName, currentPartySimNames). speakerName is excluded from the
        // addressee set - a Sim naming itself is not addressing anyone.
        internal static Result Evaluate(string text, string speakerName, IList<string> currentPartySimNames)
        {
            string trimmed = (text ?? string.Empty).Trim();
            if (trimmed.Length == 0) return new Result(SimReplyUrge.None, "empty");

            // Tactical spam, slash commands, and diagnostic output are not conversation and must never
            // anchor a reply. Reuses the single noise definition the turn guard already owns.
            if (ConversationTurnGuard.IsNoiseLine(trimmed)) return new Result(SimReplyUrge.None, "noise_or_command");
            if (ShouldReplyDeterministic.IsTrivialAcknowledgement(trimmed)) return new Result(SimReplyUrge.None, "trivial_acknowledgement");

            if (NamesAnotherMember(trimmed, speakerName, currentPartySimNames))
                return new Result(SimReplyUrge.Strong, "addressed_party_member_by_name");

            if (trimmed.IndexOf('?') >= 0 || ShouldReplyDeterministic.LooksInterrogative(trimmed))
                return new Result(SimReplyUrge.Strong, "question_or_interrogative_phrase");

            if (ContrastRegex.IsMatch(trimmed)) return new Result(SimReplyUrge.Normal, "contrast_or_hedge");
            if (OpinionRegex.IsMatch(trimmed)) return new Result(SimReplyUrge.Normal, "stated_opinion_invites_reaction");
            if (ReactionRegex.IsMatch(trimmed)) return new Result(SimReplyUrge.Normal, "notable_reaction_hook");

            // Anything left that is a real sentence is still worth an occasional chime-in. Very short
            // fragments ("sure thing", "on my way") are not.
            if (WordCount(trimmed) >= 4) return new Result(SimReplyUrge.Weak, "substantive_statement");
            return new Result(SimReplyUrge.None, "too_thin_to_answer");
        }

        private static bool NamesAnotherMember(string text, string speakerName, IList<string> currentPartySimNames)
        {
            if (currentPartySimNames == null) return false;
            for (int i = 0; i < currentPartySimNames.Count; i++)
            {
                string name = currentPartySimNames[i];
                if (string.IsNullOrWhiteSpace(name)) continue;
                name = name.Trim();
                if (!string.IsNullOrWhiteSpace(speakerName) && string.Equals(name, speakerName.Trim(), StringComparison.OrdinalIgnoreCase))
                    continue; // the speaker naming itself is not an address
                if (Regex.IsMatch(text, @"\b" + Regex.Escape(name) + @"\b", RegexOptions.IgnoreCase)) return true;
            }
            return false;
        }

        private static int WordCount(string text)
        {
            int words = 0;
            bool inWord = false;
            for (int i = 0; i < text.Length; i++)
            {
                if (char.IsWhiteSpace(text[i])) { inWord = false; continue; }
                if (!inWord) { words++; inWord = true; }
            }
            return words;
        }

        internal static List<string> RunSelfTests()
        {
            List<string> lines = new List<string>();
            List<string> party = new List<string> { "Astra", "Seren", "Mira", "Kestrel" };

            lines.Add("[DeepSims SimReply] named address is Strong: " +
                Pass(Evaluate("Kestrel would have pulled the whole camp", "Astra", party).Urge == SimReplyUrge.Strong));
            lines.Add("[DeepSims SimReply] speaker naming itself is not an address: " +
                Pass(Evaluate("Astra travels light these days", "Astra", new List<string> { "Astra" }).Urge != SimReplyUrge.Strong));
            lines.Add("[DeepSims SimReply] direct question is Strong: " +
                Pass(Evaluate("anyone remember which zone that drops in?", "Astra", party).Urge == SimReplyUrge.Strong));
            lines.Add("[DeepSims SimReply] disagreement is at least Normal: " +
                Pass(Evaluate("actually the north camp was worse", "Astra", party).Urge >= SimReplyUrge.Normal));
            lines.Add("[DeepSims SimReply] stated opinion is at least Normal: " +
                Pass(Evaluate("i think healing is the harder job", "Astra", party).Urge >= SimReplyUrge.Normal));

            // The core behaviour change: a plain statement no longer kills the thread outright.
            lines.Add("[DeepSims SimReply] plain substantive statement still invites an occasional reply: " +
                Pass(Evaluate("the rain finally let up over the ridge", "Astra", party).Urge == SimReplyUrge.Weak));

            lines.Add("[DeepSims SimReply] trivial acknowledgement gets no reply: " +
                Pass(Evaluate("lol", "Astra", party).Urge == SimReplyUrge.None));
            lines.Add("[DeepSims SimReply] short filler gets no reply: " +
                Pass(Evaluate("on my way", "Astra", party).Urge == SimReplyUrge.None));
            lines.Add("[DeepSims SimReply] tactical spam gets no reply: " +
                Pass(Evaluate("Attacking a Rock Crawler and so am i", "Astra", party).Urge == SimReplyUrge.None));
            lines.Add("[DeepSims SimReply] slash command gets no reply: " +
                Pass(Evaluate("/deepsims status", "Astra", party).Urge == SimReplyUrge.None));

            // Urge ordering must drive momentum monotonically, and the cap must stay at three.
            double strong = AmbientCadence.ContinuationChance(2, SimReplyUrge.Strong, SocialActivityPreset.Normal);
            double normal = AmbientCadence.ContinuationChance(2, SimReplyUrge.Normal, SocialActivityPreset.Normal);
            double weak = AmbientCadence.ContinuationChance(2, SimReplyUrge.Weak, SocialActivityPreset.Normal);
            double none = AmbientCadence.ContinuationChance(2, SimReplyUrge.None, SocialActivityPreset.Normal);
            lines.Add("[DeepSims SimReply] momentum is ordered strong>normal>weak>none: " +
                Pass(strong > normal && normal > weak && weak > none && none == 0.0));
            lines.Add("[DeepSims SimReply] a hooked line very likely draws one answer: " + Pass(strong >= 0.9));
            lines.Add("[DeepSims SimReply] a plain line only sometimes draws one: " + Pass(weak > 0.2 && weak < 0.6));
            lines.Add("[DeepSims SimReply] response cap is three: " + Pass(MaxResponsesPerLine == 3));
            lines.Add("[DeepSims SimReply] cap is enforced by the turn guard: " +
                Pass(!ConversationTurnGuard.ShouldContinueThread(MaxResponsesPerLine, MaxResponsesPerLine, true)));

            return lines;
        }

        private static string Pass(bool value) { return value ? "PASS" : "FAIL"; }
    }
}
