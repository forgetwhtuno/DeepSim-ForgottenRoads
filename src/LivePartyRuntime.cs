using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace ErenshorDeepSims
{
    internal static class PartyActorIdentity
    {
        internal static string ForTracking(SimPlayerTracking tracking)
        {
            if (tracking == null) return string.Empty;
            return "tracking:" + RuntimeHelpers.GetHashCode(tracking).ToString("x8");
        }

        internal static string ForSim(SimPlayer sim)
        {
            if (sim == null) return string.Empty;
            try
            {
                SimPlayerTracking tracking = sim.MySimTracking;
                if (tracking != null) return ForTracking(tracking);
            }
            catch { }
            try { return "sim_object:" + sim.GetInstanceID(); }
            catch { return string.Empty; }
        }
    }

    internal static class LivePartyRuntime
    {
        internal static LivePartyCaptureObservation Observe()
        {
            LivePartyCaptureObservation observation = new LivePartyCaptureObservation();
            observation.CapturedUtc = DateTime.UtcNow;
            observation.CapturedFrame = Time.frameCount;
            observation.AuthoritySource = "GameData.GroupMembers";
            observation.NativeAuthorityAvailable = false;
            observation.NativeTransitionActive = SafeZoning();
            observation.LocalPlayer = BuildLocalPlayer();

            try
            {
                SimPlayerTracking[] members = GameData.GroupMembers;
                observation.NativeAuthorityAvailable = members != null;
                if (members != null)
                {
                    for (int i = 0; i < members.Length; i++)
                    {
                        SimPlayerTracking tracking = members[i];
                        if (tracking == null) continue;
                        LivePartyActorFacts actor = BuildTrackingActor(tracking);
                        if (actor != null) observation.Members.Add(actor);
                    }
                }
            }
            catch
            {
                observation.NativeAuthorityAvailable = false;
            }

            // COOP remote humans have a separate proven group authority. They belong in the live party
            // facts even though they remain categorically ineligible as generated Deep Sim speakers.
            try
            {
                List<CoopCompatibility.VerifiedRemotePartyMember> remote = CoopCompatibility.GetVerifiedRemotePartyHumans();
                for (int i = 0; i < remote.Count; i++)
                {
                    CoopCompatibility.VerifiedRemotePartyMember peer = remote[i];
                    if (peer == null || string.IsNullOrWhiteSpace(peer.Name)) continue;
                    if (ContainsRemoteHuman(observation.Members, peer.Name)) continue;
                    observation.Members.Add(new LivePartyActorFacts(
                        "coop_player:" + peer.PlayerId,
                        peer.Name,
                        LivePartyActorKind.RemoteHuman,
                        LivePartyStatus.CurrentPartyMember,
                        KnownTruth.Unknown,
                        KnownTruth.True,
                        "COOP ClientGroup.currentGroup + connected Players"));
                }
            }
            catch { }

            return observation;
        }

        private static LivePartyActorFacts BuildLocalPlayer()
        {
            PlayerSnapshot player = null;
            try { player = SimContextReader.GetPlayerSnapshot(); } catch { }
            string name = player == null ? string.Empty : player.Name;
            return new LivePartyActorFacts("local_player", name, LivePartyActorKind.LocalHuman,
                LivePartyStatus.CurrentPartyMember, player == null ? KnownTruth.Unknown : KnownTruth.True,
                KnownTruth.Unknown, "local player authority");
        }

        private static LivePartyActorFacts BuildTrackingActor(SimPlayerTracking tracking)
        {
            if (tracking == null) return null;
            string name = string.Empty;
            SimPlayer avatar = null;
            try { name = tracking.SimName ?? string.Empty; } catch { }
            try { avatar = tracking.MyAvatar; } catch { }

            // Tracking proves current native membership, but not network/local actor kind when the
            // avatar is absent. UNKNOWN is safer than guessing a local Sim during transient loads.
            LivePartyActorKind kind = avatar == null ? LivePartyActorKind.Unknown : LivePartyActorKind.LocalSim;
            if (avatar != null)
            {
                try
                {
                    if (CoopCompatibility.IsRemoteCoopHuman(avatar)) kind = LivePartyActorKind.RemoteHuman;
                    else if (CoopCompatibility.IsRemoteCoopSim(avatar)) kind = LivePartyActorKind.RemoteSim;
                }
                catch { kind = LivePartyActorKind.Unknown; }
            }

            KnownTruth online = KnownTruth.Unknown;
            if (kind == LivePartyActorKind.RemoteHuman)
            {
                try { if (CoopCompatibility.IsRemoteCoopPlayerName(name)) online = KnownTruth.True; }
                catch { }
            }

            return new LivePartyActorFacts(PartyActorIdentity.ForTracking(tracking), name, kind,
                LivePartyStatus.CurrentPartyMember, avatar == null ? KnownTruth.Unknown : KnownTruth.True,
                online, "GameData.GroupMembers");
        }

        private static bool ContainsRemoteHuman(IList<LivePartyActorFacts> actors, string name)
        {
            if (actors == null || string.IsNullOrWhiteSpace(name)) return false;
            for (int i = 0; i < actors.Count; i++)
            {
                LivePartyActorFacts actor = actors[i];
                if (actor != null && actor.ActorKind == LivePartyActorKind.RemoteHuman &&
                    string.Equals(actor.Name, name, StringComparison.OrdinalIgnoreCase)) return true;
            }
            return false;
        }

        private static bool SafeZoning()
        {
            try { return GameData.Zoning; }
            catch { return false; }
        }
    }
}
