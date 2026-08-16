using System;
using System.Text.RegularExpressions;

namespace ErenshorDeepSims
{
    internal enum PartyStanceMeaning
    {
        None,
        RequestsPartyInvitation,
        OffersOrPromisesToJoin,
        AdvertisesExternalAvailability,
        OffersToComeWithParty,
        AlreadyPresentParticipation
    }

    internal enum PartyStanceDisposition
    {
        Allowed,
        Rewritten,
        Rejected
    }

    internal sealed class PartyStanceDecision
    {
        internal readonly PartyStanceMeaning Meaning;
        internal readonly PartyStanceDisposition Disposition;
        internal readonly string Output;
        internal readonly string Reason;

        internal PartyStanceDecision(PartyStanceMeaning meaning, PartyStanceDisposition disposition, string output, string reason)
        {
            Meaning = meaning;
            Disposition = disposition;
            Output = output ?? string.Empty;
            Reason = reason ?? string.Empty;
        }
    }

    // Final deterministic semantic boundary for current-party stance. It never calls the LLM and it
    // never invents capability facts. Narrow rewrites are allowlisted; compound claims fail closed.
    internal static class PartyStanceGuard
    {
        internal static PartyStanceDecision Evaluate(string line, LivePartyFacts facts, string speakerActorId, string speakerName)
        {
            string text = line == null ? string.Empty : line.Trim();
            if (text.Length == 0)
                return new PartyStanceDecision(PartyStanceMeaning.None, PartyStanceDisposition.Allowed, text, string.Empty);

            LivePartyActorFacts speaker = ResolveSpeaker(facts, speakerActorId, speakerName);
            PartyStanceMeaning meaning = Classify(text);
            if (speaker == null)
            {
                if (meaning == PartyStanceMeaning.RequestsPartyInvitation ||
                    meaning == PartyStanceMeaning.OffersOrPromisesToJoin ||
                    meaning == PartyStanceMeaning.AdvertisesExternalAvailability ||
                    meaning == PartyStanceMeaning.OffersToComeWithParty)
                    return Reject(meaning, "speaker party status is not authoritatively known");
                return Allow(meaning, text);
            }

            if (speaker.PartyStatus == LivePartyStatus.TransitionUncertain || facts == null ||
                facts.MembershipState != LivePartyMembershipState.Confirmed)
            {
                if (meaning != PartyStanceMeaning.None)
                    return Reject(meaning, "party membership is transition-uncertain");
                return Allow(meaning, text);
            }

            if (speaker.PartyStatus == LivePartyStatus.CurrentPartyMember)
            {
                if (meaning == PartyStanceMeaning.RequestsPartyInvitation ||
                    meaning == PartyStanceMeaning.OffersOrPromisesToJoin ||
                    meaning == PartyStanceMeaning.AdvertisesExternalAvailability ||
                    meaning == PartyStanceMeaning.OffersToComeWithParty)
                {
                    string replacement;
                    if (TryRewriteCurrentMember(text, meaning, out replacement))
                        return new PartyStanceDecision(meaning, PartyStanceDisposition.Rewritten, replacement,
                            "current party member cannot speak as external/awaiting invitation");
                    return Reject(meaning, "current party member line conflicts with live membership");
                }
                return Allow(meaning, text);
            }

            // Deep Sims currently has no supported non-party speaker-generation authority. Even if a
            // future caller reaches this guard, availability/join claims need proven presence + online.
            if (meaning == PartyStanceMeaning.RequestsPartyInvitation ||
                meaning == PartyStanceMeaning.OffersOrPromisesToJoin ||
                meaning == PartyStanceMeaning.AdvertisesExternalAvailability ||
                meaning == PartyStanceMeaning.OffersToComeWithParty)
            {
                if (speaker.Present != KnownTruth.True || speaker.Online != KnownTruth.True)
                    return Reject(meaning, "non-party availability is not proven by live presence/online authority");
            }
            return Allow(meaning, text);
        }

        internal static PartyStanceMeaning Classify(string line)
        {
            string text = line == null ? string.Empty : line.Trim();
            if (text.Length == 0) return PartyStanceMeaning.None;

            if (Regex.IsMatch(text, @"^(?:yeah\s+)?i(?:'m|m| am)\s+(?:in|here|with\s+(?:you|yall|ya'll|the\s+group|the\s+party)|down\s+to\s+(?:xp|grind|quest|level))(?:[.!?]*)$", RegexOptions.IgnoreCase))
                return PartyStanceMeaning.AlreadyPresentParticipation;
            if (Regex.IsMatch(text, @"\b(?:invite|add|group)\s+me\b|\bcan\s+(?:you|u)\s+invite\s+me\b|\blet\s+me\s+in\b", RegexOptions.IgnoreCase))
                return PartyStanceMeaning.RequestsPartyInvitation;
            if (Regex.IsMatch(text, @"\b(?:i\s+can|i'll|ill|i\s+will|let\s+me)\s+(?:join|group\s+up)\b|\bjoin\s+(?:you|yall|ya'll|the\s+party|the\s+group)\b", RegexOptions.IgnoreCase))
                return PartyStanceMeaning.OffersOrPromisesToJoin;
            if (Regex.IsMatch(text, @"\b(?:i(?:'m|m| am)\s+(?:on|online|available)(?:\s+too)?|let\s+me\s+know\s+if\s+(?:you|u)\s+need\s+(?:someone|somebody|one\s+more|another)|if\s+(?:you|u)\s+need\s+(?:someone|somebody|one\s+more|another)[^.!?]{0,30}\bi(?:'m|m| am)\s+(?:here|on|available))\b", RegexOptions.IgnoreCase))
                return PartyStanceMeaning.AdvertisesExternalAvailability;
            if (Regex.IsMatch(text, @"\b(?:i\s+can|i'll|ill|i\s+will|let\s+me)\s+(?:come|go|tag)\s+(?:with|along)\b|\bcome\s+with\s+(?:you|yall|ya'll)\b", RegexOptions.IgnoreCase))
                return PartyStanceMeaning.OffersToComeWithParty;
            return PartyStanceMeaning.None;
        }

        private static bool TryRewriteCurrentMember(string text, PartyStanceMeaning meaning, out string replacement)
        {
            replacement = string.Empty;
            string simple = Regex.Replace(text.Trim(), @"[.!?]+$", string.Empty).Trim();

            if (meaning == PartyStanceMeaning.OffersOrPromisesToJoin &&
                Regex.IsMatch(simple, @"^(?:i\s+can|i'll|ill|i\s+will|let\s+me)\s+join(?:\s+(?:you|yall|ya'll|the\s+party|the\s+group))?$", RegexOptions.IgnoreCase))
            {
                replacement = "im in";
                return true;
            }
            if (meaning == PartyStanceMeaning.RequestsPartyInvitation &&
                Regex.IsMatch(simple, @"^(?:invite|add|group)\s+me$|^can\s+(?:you|u)\s+invite\s+me$|^let\s+me\s+in$", RegexOptions.IgnoreCase))
            {
                replacement = "im in";
                return true;
            }
            if (meaning == PartyStanceMeaning.AdvertisesExternalAvailability)
            {
                if (Regex.IsMatch(simple, @"^let\s+me\s+know\s+if\s+(?:you|u)\s+need\s+(?:someone|somebody|one\s+more|another)$", RegexOptions.IgnoreCase))
                {
                    replacement = "im here";
                    return true;
                }
                if (Regex.IsMatch(simple, @"^i(?:'m|m| am)\s+(?:on|online|available)(?:\s+too)?$", RegexOptions.IgnoreCase))
                {
                    replacement = "im here";
                    return true;
                }
                if (Regex.IsMatch(simple, @"^i(?:'m|m| am)\s+(?:on|online)(?:\s+too)?\s+if\s+(?:you(?:'re|re| are)|ur)\s+(?:xping|xp|grinding)$", RegexOptions.IgnoreCase))
                {
                    replacement = "yeah im down to xp";
                    return true;
                }
            }
            if (meaning == PartyStanceMeaning.OffersToComeWithParty &&
                Regex.IsMatch(simple, @"^(?:i\s+can|i'll|ill|i\s+will|let\s+me)\s+(?:come|go)\s+with\s+(?:you|yall|ya'll)$", RegexOptions.IgnoreCase))
            {
                replacement = "im with you";
                return true;
            }
            return false;
        }

        private static LivePartyActorFacts ResolveSpeaker(LivePartyFacts facts, string actorId, string name)
        {
            if (facts == null) return null;
            LivePartyActorFacts actor = facts.FindByActorId(actorId);
            if (actor != null) return actor;
            return facts.FindCurrentByName(name);
        }

        private static PartyStanceDecision Allow(PartyStanceMeaning meaning, string text)
        {
            return new PartyStanceDecision(meaning, PartyStanceDisposition.Allowed, text, string.Empty);
        }

        private static PartyStanceDecision Reject(PartyStanceMeaning meaning, string reason)
        {
            return new PartyStanceDecision(meaning, PartyStanceDisposition.Rejected, "NO_MESSAGE", reason);
        }
    }
}
