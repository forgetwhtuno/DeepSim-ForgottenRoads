using System;
using System.Collections.Generic;

namespace ErenshorDeepSims
{
    internal static class LivePartyGroundingTests
    {
        internal static List<string> Run()
        {
            List<string> results = new List<string>();
            TestGroupedStance(results);
            TestNonPartyUnknownAvailability(results);
            TestMembershipTransitions(results);
            TestZoningUncertainty(results);
            TestInFlightVersionChange(results);
            TestFingerprintOrderStability(results);
            TestQueuedVersionMetadata(results);
            TestQueuedReplyBecomesStale(results);
            TestHistoricalMembershipSuppressed(results);
            TestManualSlotsCannotFabricate(results);
            TestRemoteHumanContextOnly(results);
            TestSameNameExactIdentity(results);
            TestPromptHasExplicitAuthority(results);
            return results;
        }

        private static void TestGroupedStance(List<string> results)
        {
            LivePartyFacts facts = ConfirmedFacts(1, LocalSim("sim:a", "Cyndara", LivePartyStatus.CurrentPartyMember));
            AssertRewrite(results, "grouped XP availability", "I'm on too if you're XPing.", facts, "sim:a", "yeah im down to xp");
            AssertRewrite(results, "grouped invite", "Invite me.", facts, "sim:a", "im in");
            AssertRewrite(results, "grouped join", "I can join.", facts, "sim:a", "im in");
            AssertRewrite(results, "grouped need someone", "Let me know if you need someone.", facts, "sim:a", "im here");
            PartyStanceDecision compound = PartyStanceGuard.Evaluate("Invite me if you need heals.", facts, "sim:a", "Cyndara");
            Add(results, "compound unsupported capability rejected", compound.Disposition == PartyStanceDisposition.Rejected);
        }

        private static void TestNonPartyUnknownAvailability(List<string> results)
        {
            LivePartyActorFacts actor = new LivePartyActorFacts("sim:a", "Cyndara", LivePartyActorKind.LocalSim,
                LivePartyStatus.NotCurrentPartyMember, KnownTruth.Unknown, KnownTruth.Unknown, "test");
            LivePartyFacts facts = ConfirmedFacts(2, actor);
            PartyStanceDecision decision = PartyStanceGuard.Evaluate("I'm available if you need someone.", facts, "sim:a", "Cyndara");
            Add(results, "non-party unknown availability rejected", decision.Disposition == PartyStanceDisposition.Rejected);
        }

        private static void TestMembershipTransitions(List<string> results)
        {
            DateTime t0 = new DateTime(2026, 8, 15, 12, 0, 0, DateTimeKind.Utc);
            LivePartyFactsTracker tracker = new LivePartyFactsTracker();
            LivePartyFacts outside = tracker.Capture(Observation(t0, true, false));
            LivePartyFacts joined = tracker.Capture(Observation(t0.AddSeconds(1), true, false, LocalSim("sim:a", "Cyndara", LivePartyStatus.CurrentPartyMember)));
            LivePartyFacts leavingHold = tracker.Capture(Observation(t0.AddSeconds(2), true, false));
            LivePartyFacts left = tracker.Capture(Observation(t0.AddSeconds(13), true, false));
            LivePartyFacts rejoined = tracker.Capture(Observation(t0.AddSeconds(14), true, false, LocalSim("sim:a", "Cyndara", LivePartyStatus.CurrentPartyMember)));
            Add(results, "join increments membership version", joined.MembershipVersion > outside.MembershipVersion);
            Add(results, "empty roster enters transition hold", leavingHold.MembershipState == LivePartyMembershipState.TransitionUncertain);
            Add(results, "empty roster confirms after hold", left.MembershipState == LivePartyMembershipState.Confirmed && left.Members.Count == 0);
            Add(results, "leave increments membership version", left.MembershipVersion > joined.MembershipVersion);
            Add(results, "rejoin increments membership version", rejoined.MembershipVersion > left.MembershipVersion);
        }

        private static void TestZoningUncertainty(List<string> results)
        {
            DateTime t0 = new DateTime(2026, 8, 15, 13, 0, 0, DateTimeKind.Utc);
            LivePartyFactsTracker tracker = new LivePartyFactsTracker();
            LivePartyFacts confirmed = tracker.Capture(Observation(t0, true, false, LocalSim("sim:a", "Cyndara", LivePartyStatus.CurrentPartyMember)));
            LivePartyFacts zoning = tracker.Capture(Observation(t0.AddSeconds(1), false, true));
            Add(results, "zoning uses transition uncertain", zoning.MembershipState == LivePartyMembershipState.TransitionUncertain);
            Add(results, "retained member is not current authority", zoning.Members.Count == 1 && zoning.Members[0].PartyStatus == LivePartyStatus.TransitionUncertain);
            PartyStanceDecision decision = PartyStanceGuard.Evaluate("I can join.", zoning, "sim:a", "Cyndara");
            Add(results, "zoning party stance rejected", decision.Disposition == PartyStanceDisposition.Rejected);
            Add(results, "transition changes version", zoning.MembershipVersion > confirmed.MembershipVersion);
        }

        private static void TestInFlightVersionChange(List<string> results)
        {
            LivePartyFacts before = ConfirmedFacts(10, LocalSim("sim:a", "Cyndara", LivePartyStatus.CurrentPartyMember));
            PartyGroundingRequestContext request = new PartyGroundingRequestContext(99, "test", before, "sim:a", "Cyndara", 1);
            LivePartyFacts same = ConfirmedFacts(10, LocalSim("sim:a", "Cyndara", LivePartyStatus.CurrentPartyMember));
            LivePartyFacts changed = ConfirmedFacts(11);
            Add(results, "same membership version remains valid", !request.MembershipChanged(same));
            Add(results, "in-flight membership change detected", request.MembershipChanged(changed));
        }

        private static void TestFingerprintOrderStability(List<string> results)
        {
            DateTime t0 = new DateTime(2026, 8, 15, 13, 30, 0, DateTimeKind.Utc);
            LivePartyFactsTracker tracker = new LivePartyFactsTracker();
            LivePartyActorFacts a = LocalSim("sim:a", "Cyndara", LivePartyStatus.CurrentPartyMember);
            LivePartyActorFacts b = LocalSim("sim:b", "Phanty", LivePartyStatus.CurrentPartyMember);
            LivePartyFacts first = tracker.Capture(Observation(t0, true, false, a, b));
            LivePartyFacts reordered = tracker.Capture(Observation(t0.AddMilliseconds(20), true, false, b, a));
            Add(results, "native roster order does not stale membership", reordered.MembershipVersion == first.MembershipVersion);
        }

        private static void TestQueuedVersionMetadata(List<string> results)
        {
            GroupMessageQueue queue = new GroupMessageQueue();
            DateTime now = DateTime.UtcNow;
            queue.Enqueue(now, "Cyndara", "im in", false, 7, "test", 101, 44, "sim:a", "party_reply", now.AddMilliseconds(-25), 2);
            List<ScheduledGroupMessage> due = queue.TakeDue(now.AddSeconds(1));
            bool ok = due.Count == 1 && due[0].PartyRequestId == 101 && due[0].MembershipVersion == 44 &&
                due[0].SpeakerActorId == "sim:a" && due[0].GenerationPath == "party_reply" && due[0].EligibleSpeakerCount == 2;
            Add(results, "queued reply retains party version metadata", ok);
        }

        private static void TestQueuedReplyBecomesStale(List<string> results)
        {
            GroupMessageQueue queue = new GroupMessageQueue();
            DateTime now = DateTime.UtcNow;
            queue.Enqueue(now, "Cyndara", "yeah im down to xp", false, 8, "test", 202, 70, "sim:a", "party_reply", now, 1);
            List<ScheduledGroupMessage> due = queue.TakeDue(now.AddSeconds(1));
            if (due.Count != 1)
            {
                Add(results, "queued reply becomes stale before display", false);
                return;
            }
            ScheduledGroupMessage line = due[0];
            PartyGroundingRequestContext reconstructed = new PartyGroundingRequestContext(
                line.PartyRequestId, line.GenerationPath, line.MembershipVersion, line.PartySnapshotCapturedUtc,
                line.SpeakerActorId, line.Speaker, line.EligibleSpeakerCount);
            LivePartyFacts afterLeave = ConfirmedFacts(71);
            Add(results, "queued reply becomes stale before display", reconstructed.MembershipChanged(afterLeave));
        }

        private static void TestHistoricalMembershipSuppressed(List<string> results)
        {
            SimMemory memory = new SimMemory { Name = "Cyndara", SimKey = "cyndara" };
            memory.Normalize();
            memory.RecentEvents.Add(new MemoryEvent { type = "group_join", text = "Cyndara joined the party", importance = 50 });
            memory.RecentEvents.Add(new MemoryEvent { type = "group_leave", text = "Cyndara left the party", importance = 50 });
            List<RelevantMemory> selected = MemoryRelevance.Select(memory, "party join leave Cyndara", 3);
            Add(results, "historical membership omitted from current retrieval", selected.Count == 0);
        }

        private static void TestManualSlotsCannotFabricate(List<string> results)
        {
            List<string> native = new List<string> { "Cyndara" };
            List<string> filtered = DeepSlotSelectionPolicy.FilterManualNativeCandidates(native, "Cyndara, Phanty");
            List<string> fabricated = DeepSlotSelectionPolicy.FilterManualNativeCandidates(new List<string>(), "Cyndara");
            Add(results, "manual slots only filter native party", filtered.Count == 1 && filtered[0] == "Cyndara");
            Add(results, "manual slots cannot fabricate grouped member", fabricated.Count == 0);
        }

        private static void TestRemoteHumanContextOnly(List<string> results)
        {
            LivePartyActorFacts remote = new LivePartyActorFacts("coop_player:2", "Remote", LivePartyActorKind.RemoteHuman,
                LivePartyStatus.CurrentPartyMember, KnownTruth.True, KnownTruth.True, "COOP");
            LivePartyFacts facts = ConfirmedFacts(20, remote);
            Add(results, "remote human represented in live party", facts.RemoteHumanCount == 1 && facts.FindByActorId("coop_player:2") != null);
            Add(results, "remote human not generated speaker", !LivePartyEligibility.IsEligibleGeneratedSpeaker(remote));
        }

        private static void TestSameNameExactIdentity(List<string> results)
        {
            LivePartyActorFacts a = LocalSim("sim:a", "SameName", LivePartyStatus.CurrentPartyMember);
            LivePartyActorFacts b = LocalSim("sim:b", "SameName", LivePartyStatus.CurrentPartyMember);
            LivePartyFacts facts = ConfirmedFacts(30, a, b);
            Add(results, "same-name lookup fails closed", facts.FindCurrentByName("SameName") == null);
            Add(results, "exact actor identity remains resolvable", object.ReferenceEquals(facts.FindByActorId("sim:b"), b));
        }

        private static void TestPromptHasExplicitAuthority(List<string> results)
        {
            SimSnapshot sim = new SimSnapshot { PartyActorId = "sim:a", Name = "Cyndara", ClassName = "Arcanist", Level = 12, DialogueExamples = new List<string>() };
            LivePartyActorFacts local = LocalSim("sim:a", "Cyndara", LivePartyStatus.CurrentPartyMember);
            LivePartyActorFacts remote = new LivePartyActorFacts("coop_player:2", "Remote", LivePartyActorKind.RemoteHuman,
                LivePartyStatus.CurrentPartyMember, KnownTruth.True, KnownTruth.True, "COOP");
            WorldSnapshot world = new WorldSnapshot
            {
                Scene = "Hidden Hills",
                Player = new PlayerSnapshot { Name = "Player", Level = 12, ClassName = "Paladin" },
                Party = new List<SimSnapshot> { sim },
                LiveParty = ConfirmedFacts(50, local, remote)
            };
            SimMemory memory = new SimMemory { Name = "Cyndara", SimKey = "cyndara" };
            memory.Normalize();
            List<ConversationLine> thread = new List<ConversationLine> { new ConversationLine("Player", "xp for a bit?") };
            List<ChatMessage> messages = PromptBuilder.BuildPartyThreadReply(sim, memory, world, thread, 1, null);
            string joined = string.Empty;
            for (int i = 0; i < messages.Count; i++) joined += "\n" + (messages[i] == null ? string.Empty : messages[i].content);
            bool has = joined.IndexOf("LIVE PARTY FACTS", StringComparison.OrdinalIgnoreCase) >= 0 &&
                joined.IndexOf("speakerPartyStatus=current_party_member", StringComparison.OrdinalIgnoreCase) >= 0 &&
                joined.IndexOf("speakerOnline=unknown", StringComparison.OrdinalIgnoreCase) >= 0 &&
                joined.IndexOf("Remote[remote_human]", StringComparison.OrdinalIgnoreCase) >= 0;
            Add(results, "party prompt carries explicit authoritative facts", has);
        }

        private static LivePartyCaptureObservation Observation(DateTime utc, bool authority, bool transition, params LivePartyActorFacts[] members)
        {
            LivePartyCaptureObservation o = new LivePartyCaptureObservation();
            o.CapturedUtc = utc;
            o.CapturedFrame = 1;
            o.NativeAuthorityAvailable = authority;
            o.NativeTransitionActive = transition;
            o.AuthoritySource = "GameData.GroupMembers";
            o.LocalPlayer = new LivePartyActorFacts("local_player", "Player", LivePartyActorKind.LocalHuman,
                LivePartyStatus.CurrentPartyMember, KnownTruth.True, KnownTruth.Unknown, "test");
            if (members != null) o.Members.AddRange(members);
            return o;
        }

        private static LivePartyFacts ConfirmedFacts(long version, params LivePartyActorFacts[] members)
        {
            List<LivePartyActorFacts> list = new List<LivePartyActorFacts>();
            if (members != null) list.AddRange(members);
            LivePartyActorFacts player = new LivePartyActorFacts("local_player", "Player", LivePartyActorKind.LocalHuman,
                LivePartyStatus.CurrentPartyMember, KnownTruth.True, KnownTruth.Unknown, "test");
            return new LivePartyFacts(version, DateTime.UtcNow, 1, LivePartyMembershipState.Confirmed, "GameData.GroupMembers", player, list, "test:" + version);
        }

        private static LivePartyActorFacts LocalSim(string id, string name, LivePartyStatus status)
        {
            return new LivePartyActorFacts(id, name, LivePartyActorKind.LocalSim, status,
                KnownTruth.True, KnownTruth.Unknown, "GameData.GroupMembers");
        }

        private static void AssertRewrite(List<string> results, string name, string input, LivePartyFacts facts, string actorId, string expected)
        {
            PartyStanceDecision decision = PartyStanceGuard.Evaluate(input, facts, actorId, "Cyndara");
            Add(results, name, decision.Disposition == PartyStanceDisposition.Rewritten && string.Equals(decision.Output, expected, StringComparison.Ordinal));
        }

        private static void Add(List<string> results, string name, bool pass)
        {
            results.Add("[LivePartyGrounding " + (pass ? "PASS" : "FAIL") + "] " + name);
        }
    }
}
