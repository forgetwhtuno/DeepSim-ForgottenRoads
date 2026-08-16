using System;
using System.Collections.Generic;
using System.Text;

namespace ErenshorDeepSims
{
    internal enum LivePartyMembershipState
    {
        Confirmed,
        TransitionUncertain,
        Unavailable
    }

    internal enum LivePartyActorKind
    {
        LocalHuman,
        LocalSim,
        RemoteHuman,
        RemoteSim,
        Unknown
    }

    internal enum LivePartyStatus
    {
        CurrentPartyMember,
        NotCurrentPartyMember,
        TransitionUncertain,
        Unknown
    }

    internal enum KnownTruth
    {
        Unknown,
        False,
        True
    }

    internal sealed class LivePartyActorFacts
    {
        internal readonly string ActorId;
        internal readonly string Name;
        internal readonly LivePartyActorKind ActorKind;
        internal readonly LivePartyStatus PartyStatus;
        internal readonly KnownTruth Present;
        internal readonly KnownTruth Online;
        internal readonly string AuthoritySource;

        internal LivePartyActorFacts(string actorId, string name, LivePartyActorKind actorKind,
            LivePartyStatus partyStatus, KnownTruth present, KnownTruth online, string authoritySource)
        {
            ActorId = actorId ?? string.Empty;
            Name = name ?? string.Empty;
            ActorKind = actorKind;
            PartyStatus = partyStatus;
            Present = present;
            Online = online;
            AuthoritySource = authoritySource ?? string.Empty;
        }

        internal LivePartyActorFacts WithPartyStatus(LivePartyStatus status, string authority)
        {
            return new LivePartyActorFacts(ActorId, Name, ActorKind, status, Present, Online, authority);
        }
    }

    public sealed class LivePartyFacts
    {
        private readonly List<LivePartyActorFacts> _members;

        internal readonly long MembershipVersion;
        internal readonly DateTime CapturedUtc;
        internal readonly int CapturedFrame;
        internal readonly LivePartyMembershipState MembershipState;
        internal readonly string NativeAuthoritySource;
        internal readonly LivePartyActorFacts LocalPlayer;
        internal readonly string Fingerprint;

        internal LivePartyFacts(long membershipVersion, DateTime capturedUtc, int capturedFrame,
            LivePartyMembershipState membershipState, string authoritySource, LivePartyActorFacts localPlayer,
            IList<LivePartyActorFacts> members, string fingerprint)
        {
            MembershipVersion = membershipVersion;
            CapturedUtc = capturedUtc;
            CapturedFrame = capturedFrame;
            MembershipState = membershipState;
            NativeAuthoritySource = authoritySource ?? string.Empty;
            LocalPlayer = localPlayer;
            Fingerprint = fingerprint ?? string.Empty;
            _members = new List<LivePartyActorFacts>();
            if (members != null)
                for (int i = 0; i < members.Count; i++)
                    if (members[i] != null) _members.Add(members[i]);
        }

        internal IList<LivePartyActorFacts> Members { get { return _members.AsReadOnly(); } }

        internal int CurrentPartyCount
        {
            get
            {
                int count = LocalPlayer != null && LocalPlayer.PartyStatus == LivePartyStatus.CurrentPartyMember ? 1 : 0;
                for (int i = 0; i < _members.Count; i++)
                    if (_members[i].PartyStatus == LivePartyStatus.CurrentPartyMember) count++;
                return count;
            }
        }

        internal int RemoteHumanCount
        {
            get
            {
                int count = 0;
                for (int i = 0; i < _members.Count; i++)
                    if (_members[i].ActorKind == LivePartyActorKind.RemoteHuman &&
                        _members[i].PartyStatus == LivePartyStatus.CurrentPartyMember) count++;
                return count;
            }
        }

        internal LivePartyActorFacts FindByActorId(string actorId)
        {
            if (string.IsNullOrWhiteSpace(actorId)) return null;
            if (LocalPlayer != null && string.Equals(LocalPlayer.ActorId, actorId, StringComparison.Ordinal)) return LocalPlayer;
            for (int i = 0; i < _members.Count; i++)
                if (string.Equals(_members[i].ActorId, actorId, StringComparison.Ordinal)) return _members[i];
            return null;
        }

        internal LivePartyActorFacts FindCurrentByName(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return null;
            LivePartyActorFacts match = null;
            for (int i = 0; i < _members.Count; i++)
            {
                LivePartyActorFacts actor = _members[i];
                if (actor.PartyStatus != LivePartyStatus.CurrentPartyMember ||
                    !string.Equals(actor.Name, name, StringComparison.OrdinalIgnoreCase)) continue;
                if (match != null) return null; // same-name ambiguity fails closed
                match = actor;
            }
            return match;
        }
    }

    internal sealed class LivePartyCaptureObservation
    {
        internal DateTime CapturedUtc;
        internal int CapturedFrame;
        internal bool NativeAuthorityAvailable;
        internal bool NativeTransitionActive;
        internal string AuthoritySource;
        internal LivePartyActorFacts LocalPlayer;
        internal List<LivePartyActorFacts> Members = new List<LivePartyActorFacts>();
    }

    // Pure deterministic state machine. Runtime code supplies observations from proven native authority;
    // this policy only versions them and gives the existing zoning tolerance an explicit uncertain state.
    internal sealed class LivePartyFactsTracker
    {
        private const double EmptyHoldSeconds = 10.0;
        private long _version;
        private string _lastFingerprint = string.Empty;
        private LivePartyFacts _lastConfirmed;
        private DateTime? _emptySinceUtc;

        internal LivePartyFacts Capture(LivePartyCaptureObservation observation)
        {
            if (observation == null) observation = new LivePartyCaptureObservation { CapturedUtc = DateTime.UtcNow };
            if (observation.CapturedUtc == DateTime.MinValue) observation.CapturedUtc = DateTime.UtcNow;
            if (observation.Members == null) observation.Members = new List<LivePartyActorFacts>();

            bool hasMembers = observation.Members.Count > 0;
            bool uncertainty = !observation.NativeAuthorityAvailable || observation.NativeTransitionActive;
            if (!uncertainty && !hasMembers && _lastConfirmed != null && _lastConfirmed.Members.Count > 0)
            {
                if (!_emptySinceUtc.HasValue) _emptySinceUtc = observation.CapturedUtc;
                uncertainty = (observation.CapturedUtc - _emptySinceUtc.Value).TotalSeconds < EmptyHoldSeconds;
            }
            else if (hasMembers) _emptySinceUtc = null;

            LivePartyMembershipState state;
            List<LivePartyActorFacts> members;
            string authority = observation.AuthoritySource ?? string.Empty;
            if (uncertainty)
            {
                // If we have any prior/current roster context, an unavailable native array during zoning/loading
                // is explicitly uncertain -- never a confident empty/not-grouped assertion. Unavailable is reserved
                // for startup/no-authority cases where there is no roster context at all.
                bool hasRetainedContext = observation.Members.Count > 0 || (_lastConfirmed != null && _lastConfirmed.Members.Count > 0);
                state = observation.NativeTransitionActive || hasRetainedContext
                    ? LivePartyMembershipState.TransitionUncertain
                    : LivePartyMembershipState.Unavailable;
                members = BuildUncertainMembers(observation.Members.Count > 0 ? observation.Members : (_lastConfirmed == null ? null : _lastConfirmed.Members));
                if (authority.Length == 0) authority = "native party authority unavailable";
            }
            else
            {
                state = LivePartyMembershipState.Confirmed;
                members = NormalizeCurrentMembers(observation.Members);
                _emptySinceUtc = null;
            }

            string fingerprint = BuildFingerprint(state, observation.LocalPlayer, members);
            if (!string.Equals(fingerprint, _lastFingerprint, StringComparison.Ordinal))
            {
                _version++;
                _lastFingerprint = fingerprint;
            }
            LivePartyFacts result = new LivePartyFacts(_version, observation.CapturedUtc, observation.CapturedFrame,
                state, authority, observation.LocalPlayer, members, fingerprint);
            if (state == LivePartyMembershipState.Confirmed) _lastConfirmed = result;
            return result;
        }

        private static List<LivePartyActorFacts> NormalizeCurrentMembers(IList<LivePartyActorFacts> source)
        {
            List<LivePartyActorFacts> result = new List<LivePartyActorFacts>();
            if (source == null) return result;
            for (int i = 0; i < source.Count; i++)
            {
                LivePartyActorFacts actor = source[i];
                if (actor == null) continue;
                result.Add(actor.PartyStatus == LivePartyStatus.CurrentPartyMember
                    ? actor
                    : actor.WithPartyStatus(LivePartyStatus.CurrentPartyMember, actor.AuthoritySource));
            }
            return result;
        }

        private static List<LivePartyActorFacts> BuildUncertainMembers(IList<LivePartyActorFacts> source)
        {
            List<LivePartyActorFacts> result = new List<LivePartyActorFacts>();
            if (source == null) return result;
            for (int i = 0; i < source.Count; i++)
            {
                LivePartyActorFacts actor = source[i];
                if (actor == null) continue;
                result.Add(actor.WithPartyStatus(LivePartyStatus.TransitionUncertain, "retained transition context; not current membership authority"));
            }
            return result;
        }

        private static string BuildFingerprint(LivePartyMembershipState state, LivePartyActorFacts localPlayer,
            IList<LivePartyActorFacts> members)
        {
            List<string> parts = new List<string>();
            parts.Add("state=" + state.ToString());
            if (localPlayer != null) parts.Add("player=" + localPlayer.ActorId);
            if (members != null)
                for (int i = 0; i < members.Count; i++)
                {
                    LivePartyActorFacts actor = members[i];
                    if (actor == null) continue;
                    parts.Add(actor.ActorId + ":" + actor.ActorKind + ":" + actor.PartyStatus);
                }
            parts.Sort(StringComparer.Ordinal);
            return string.Join("|", parts.ToArray());
        }
    }

    internal static class LivePartyEligibility
    {
        internal static bool IsEligibleGeneratedSpeaker(LivePartyActorFacts actor)
        {
            return actor != null && actor.PartyStatus == LivePartyStatus.CurrentPartyMember &&
                actor.ActorKind == LivePartyActorKind.LocalSim;
        }
    }

    internal static class LivePartyFactsFormatting
    {
        internal static string ActorKind(LivePartyActorKind value)
        {
            if (value == LivePartyActorKind.LocalHuman) return "local_human";
            if (value == LivePartyActorKind.LocalSim) return "local_sim";
            if (value == LivePartyActorKind.RemoteHuman) return "remote_human";
            if (value == LivePartyActorKind.RemoteSim) return "remote_sim";
            return "unknown";
        }

        internal static string PartyStatus(LivePartyStatus value)
        {
            if (value == LivePartyStatus.CurrentPartyMember) return "current_party_member";
            if (value == LivePartyStatus.NotCurrentPartyMember) return "not_current_party_member";
            if (value == LivePartyStatus.TransitionUncertain) return "transition_uncertain";
            return "unknown";
        }

        internal static string Truth(KnownTruth value)
        {
            if (value == KnownTruth.True) return "true";
            if (value == KnownTruth.False) return "false";
            return "unknown";
        }
    }
}
