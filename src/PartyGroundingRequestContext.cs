using System;

namespace ErenshorDeepSims
{
    internal sealed class PartyGroundingRequestContext
    {
        internal readonly long RequestId;
        internal readonly string Path;
        internal readonly long MembershipVersion;
        internal readonly DateTime CapturedUtc;
        internal readonly string SpeakerActorId;
        internal readonly string SpeakerName;
        internal readonly int EligibleSpeakerCount;

        internal PartyGroundingRequestContext(long requestId, string path, LivePartyFacts facts,
            string speakerActorId, string speakerName, int eligibleSpeakerCount)
        {
            RequestId = requestId;
            Path = path ?? string.Empty;
            MembershipVersion = facts == null ? -1 : facts.MembershipVersion;
            CapturedUtc = facts == null ? DateTime.MinValue : facts.CapturedUtc;
            SpeakerActorId = speakerActorId ?? string.Empty;
            SpeakerName = speakerName ?? string.Empty;
            EligibleSpeakerCount = Math.Max(0, eligibleSpeakerCount);
        }


        internal PartyGroundingRequestContext(long requestId, string path, long membershipVersion, DateTime capturedUtc,
            string speakerActorId, string speakerName, int eligibleSpeakerCount)
        {
            RequestId = requestId;
            Path = path ?? string.Empty;
            MembershipVersion = membershipVersion;
            CapturedUtc = capturedUtc;
            SpeakerActorId = speakerActorId ?? string.Empty;
            SpeakerName = speakerName ?? string.Empty;
            EligibleSpeakerCount = Math.Max(0, eligibleSpeakerCount);
        }

        internal bool MembershipChanged(LivePartyFacts current)
        {
            return current == null || MembershipVersion < 0 || current.MembershipVersion != MembershipVersion;
        }
    }
}
