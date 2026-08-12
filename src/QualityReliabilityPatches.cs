using HarmonyLib;
using System;

namespace ErenshorDeepSims
{
    // SessionTelemetry is an observation cache, not gameplay authority. Filter structured Expedition
    // lifecycle noise before it enters the recent VERIFIED fact list so repeated resume/transition
    // notifications cannot crowd prompt context. Meaningful arrival/interruption facts remain eligible.
    [HarmonyPatch(typeof(SessionTelemetry), "RecordObservedEvent")]
    internal static class SessionTelemetryExpeditionBoundaryPatch
    {
        private static readonly object Gate = new object();
        private static readonly SemanticEventDeduplicator Dedupe = new SemanticEventDeduplicator();

        private static bool Prefix(string type, string description)
        {
            if (!ExpeditionSocialPolicy.IsExpeditionType(type)) return true;
            if (!ExpeditionSocialPolicy.ShouldPersistSocialMemory(type)) return false;
            lock (Gate) { return Dedupe.ShouldAccept(type, description, DateTime.UtcNow); }
        }
    }
}
