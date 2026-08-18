using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace ErenshorDeepSims
{
    // ---------------------------------------------------------------------------------------------
    // LOCAL-ONLY DIAGNOSTIC INSTRUMENTATION.
    //
    // This file is deliberately Unity-free and IO-free so the deterministic regression harness can
    // compile and exercise it outside the game. It owns the packet SHAPE, the redaction rules, and
    // the pure state machine. All filesystem work lives in PromptCaptureWriter.
    //
    // Nothing here may influence generation. Every entry point is expected to be called from a
    // best-effort try/catch on the caller side; the pure logic itself never throws for ordinary
    // input.
    // ---------------------------------------------------------------------------------------------

    internal enum PromptCaptureAttemptKind
    {
        // Names mirror the actual phases in OllamaClient.ChatAsync so a captured attempt can be
        // traced back to the exact branch that produced it.
        Primary,
        PostLoadRetry,
        ExpandedBudget,
        FlattenedFallback
    }

    internal static class PromptCaptureAttemptKinds
    {
        internal static string Name(PromptCaptureAttemptKind kind)
        {
            switch (kind)
            {
                case PromptCaptureAttemptKind.PostLoadRetry: return "post_load_retry";
                case PromptCaptureAttemptKind.ExpandedBudget: return "expanded_budget";
                case PromptCaptureAttemptKind.FlattenedFallback: return "flattened_fallback";
                default: return "primary";
            }
        }
    }

    // Stable slugs for the free-text grounding reasons produced by GroundPartyLineAsync. The reason
    // text itself is preserved verbatim in the packet; this only adds a groupable category so a later
    // replay harness can select specimens without string-matching prose.
    internal static class PromptCaptureReasonCategory
    {
        internal static string Classify(string reason)
        {
            if (string.IsNullOrWhiteSpace(reason)) return "none";
            string lower = reason.ToLowerInvariant();
            if (lower.IndexOf("topic mismatch", StringComparison.Ordinal) >= 0) return "topic_mismatch";
            if (lower.IndexOf("uncertainty deflection", StringComparison.Ordinal) >= 0) return "subjective_deflection";
            if (Contains(lower, "loot") || Contains(lower, "acquisition") || Contains(lower, "drop")) return "loot_acquisition";
            if (Contains(lower, "kill") || Contains(lower, "cleared") || Contains(lower, "clear")) return "kill_clear";
            if (Contains(lower, "relationship") || Contains(lower, "entity")) return "entity_relationship";
            if (Contains(lower, "instruction")) return "instruction_leak";
            if (Contains(lower, "knowledge") || Contains(lower, "retrieved") || Contains(lower, "unsupported")) return "unsupported_by_evidence";
            if (Contains(lower, "relevan")) return "direct_reply_irrelevant";
            return "other";
        }

        private static bool Contains(string haystack, string needle)
        {
            return haystack.IndexOf(needle, StringComparison.Ordinal) >= 0;
        }
    }

    // Diagnostic-only labels. These NEVER change runtime behavior; they are derived after the fact
    // from values the pipeline already produced, purely so interesting specimens are easy to find.
    internal static class PromptCaptureInterestingCases
    {
        internal static List<string> Derive(string stage, string rawTurnType, string effectiveTurnType,
            string rawKnowledgeNeed, string effectiveKnowledgeNeed, bool retrievalUsed, bool retrievalFound,
            string groundingDecision, string reasonCategory, bool connectedSimTurn)
        {
            List<string> tags = new List<string>();
            bool opinionLike = IsOpinionLike(effectiveTurnType) || IsOpinionLike(rawTurnType);
            if (opinionLike && retrievalUsed) Add(tags, "opinion_with_retrieval");
            if (opinionLike && !string.IsNullOrEmpty(rawKnowledgeNeed) &&
                !string.Equals(rawKnowledgeNeed, effectiveKnowledgeNeed, StringComparison.OrdinalIgnoreCase))
                Add(tags, "opinion_knowledge_override");
            if (string.Equals(groundingDecision, "rejected", StringComparison.OrdinalIgnoreCase))
            {
                switch (reasonCategory)
                {
                    case "topic_mismatch": Add(tags, "grounding_reject_topic_mismatch"); break;
                    case "loot_acquisition": Add(tags, "grounding_reject_loot_acquisition"); break;
                    case "entity_relationship": Add(tags, "grounding_reject_entity_relationship"); break;
                    case "kill_clear": Add(tags, "grounding_reject_kill_clear"); break;
                    default: Add(tags, "grounding_reject_other"); break;
                }
            }
            if (connectedSimTurn) Add(tags, "connected_sim_banter");
            if (retrievalUsed && retrievalFound && string.Equals(groundingDecision, "accepted", StringComparison.OrdinalIgnoreCase))
                Add(tags, "accepted_retrieval_answer");
            if (!string.IsNullOrEmpty(stage) && stage.IndexOf("other_sim", StringComparison.OrdinalIgnoreCase) >= 0)
                Add(tags, "other_sim_preference");
            return tags;
        }

        private static bool IsOpinionLike(string turnType)
        {
            return string.Equals(turnType, "Opinion", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(turnType, "PersonalPreference", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(turnType, "SocialQuestion", StringComparison.OrdinalIgnoreCase);
        }

        private static void Add(List<string> tags, string tag)
        {
            if (!tags.Contains(tag)) tags.Add(tag);
        }
    }

    // Redaction is defence in depth. The capture pipeline is already built to pass ONLY values that
    // were actually handed to the model, but a future caller must not be able to accidentally leak an
    // absolute user path or a secret into a packet.
    internal static class PromptCaptureRedaction
    {
        internal const string Redacted = "<redacted>";

        // Absolute filesystem paths must never appear in a packet. Only the relative, mod-rooted
        // label is ever recorded.
        internal static string RelativeLabel(string absolutePath, string dataRoot)
        {
            if (string.IsNullOrEmpty(absolutePath)) return string.Empty;
            string normalizedPath = absolutePath.Replace('\\', '/');
            string normalizedRoot = (dataRoot ?? string.Empty).Replace('\\', '/').TrimEnd('/');
            if (normalizedRoot.Length > 0 && normalizedPath.StartsWith(normalizedRoot, StringComparison.OrdinalIgnoreCase))
            {
                string relative = normalizedPath.Substring(normalizedRoot.Length).TrimStart('/');
                return "DeepSims/" + relative;
            }
            // Unknown root: keep only the last two segments so no user profile path can survive.
            string[] parts = normalizedPath.Split('/');
            if (parts.Length <= 2) return parts[parts.Length - 1];
            return parts[parts.Length - 2] + "/" + parts[parts.Length - 1];
        }

        // Endpoints are recorded as a kind, never as a URL that could carry a key or a private host.
        internal static string EndpointKind(string endpoint)
        {
            if (string.IsNullOrWhiteSpace(endpoint)) return "unknown";
            string lower = endpoint.Trim().ToLowerInvariant();
            bool loopback = lower.IndexOf("//localhost", StringComparison.Ordinal) >= 0 ||
                            lower.IndexOf("//127.0.0.1", StringComparison.Ordinal) >= 0 ||
                            lower.IndexOf("//[::1]", StringComparison.Ordinal) >= 0;
            if (lower.IndexOf("/api/chat", StringComparison.Ordinal) >= 0)
                return loopback ? "ollama_chat" : "ollama_chat_remote";
            return loopback ? "loopback_other" : "remote_other";
        }

        // Belt-and-braces scrub for any text that is written into a packet. Real prompts and replies
        // are the point of this capture, so they are NOT truncated here; only recognisably secret or
        // machine-identifying shapes are replaced.
        internal static string Scrub(string value)
        {
            if (string.IsNullOrEmpty(value)) return value;
            string result = value;
            result = ScrubWindowsUserPaths(result);
            result = ScrubUnixHomePaths(result);
            result = ScrubBearerTokens(result);
            return result;
        }

        private static string ScrubWindowsUserPaths(string value)
        {
            int index = IndexOfIgnoreCase(value, "C:\\Users\\");
            if (index < 0) index = IndexOfIgnoreCase(value, "C:/Users/");
            if (index < 0) return value;
            StringBuilder sb = new StringBuilder(value.Length);
            int cursor = 0;
            while (index >= 0)
            {
                sb.Append(value, cursor, index - cursor);
                sb.Append(Redacted);
                cursor = index + "C:\\Users\\".Length;
                while (cursor < value.Length && value[cursor] != '\\' && value[cursor] != '/' &&
                       value[cursor] != ' ' && value[cursor] != '"') cursor++;
                int next = IndexOfIgnoreCase(value, "C:\\Users\\", cursor);
                if (next < 0) next = IndexOfIgnoreCase(value, "C:/Users/", cursor);
                index = next;
            }
            sb.Append(value, cursor, value.Length - cursor);
            return sb.ToString();
        }

        private static string ScrubUnixHomePaths(string value)
        {
            string result = value;
            result = ReplacePrefixedSegment(result, "/home/");
            result = ReplacePrefixedSegment(result, "/Users/");
            return result;
        }

        private static string ReplacePrefixedSegment(string value, string prefix)
        {
            int index = value.IndexOf(prefix, StringComparison.Ordinal);
            if (index < 0) return value;
            StringBuilder sb = new StringBuilder(value.Length);
            int cursor = 0;
            while (index >= 0)
            {
                sb.Append(value, cursor, index - cursor);
                sb.Append(Redacted);
                cursor = index + prefix.Length;
                while (cursor < value.Length && value[cursor] != '/' && value[cursor] != ' ' && value[cursor] != '"') cursor++;
                index = value.IndexOf(prefix, cursor, StringComparison.Ordinal);
            }
            sb.Append(value, cursor, value.Length - cursor);
            return sb.ToString();
        }

        private static string ScrubBearerTokens(string value)
        {
            string[] markers = new string[] { "Authorization:", "Bearer ", "api_key=", "apikey=", "api-key=", "access_token=" };
            string result = value;
            for (int i = 0; i < markers.Length; i++)
            {
                int index = IndexOfIgnoreCase(result, markers[i]);
                while (index >= 0)
                {
                    int start = index + markers[i].Length;
                    int end = start;
                    while (end < result.Length && result[end] != '&' && result[end] != '\n' && result[end] != '"' && result[end] != ' ') end++;
                    if (end > start) result = result.Substring(0, start) + Redacted + result.Substring(end);
                    index = IndexOfIgnoreCase(result, markers[i], start + Redacted.Length);
                }
            }
            return result;
        }

        private static int IndexOfIgnoreCase(string value, string needle) { return IndexOfIgnoreCase(value, needle, 0); }

        private static int IndexOfIgnoreCase(string value, string needle, int start)
        {
            if (start >= value.Length) return -1;
            return value.IndexOf(needle, start, StringComparison.OrdinalIgnoreCase);
        }

        // A session id must be filesystem-safe and must not encode anything identifying. It is derived
        // from UTC time plus a short counter, never from character name, Steam id, or machine name.
        internal static string SafeSessionId(DateTime utcNow, int sequence)
        {
            return utcNow.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture) + "-" +
                   (sequence & 0xFFF).ToString("x3", CultureInfo.InvariantCulture);
        }

        // Speaker names are ordinary in-game Sim names and are safe, but the log line uses a hash so
        // normal logs carry no dialogue-adjacent identity at all.
        internal static string SpeakerHash(string speaker)
        {
            if (string.IsNullOrEmpty(speaker)) return "0";
            unchecked
            {
                int hash = 23;
                for (int i = 0; i < speaker.Length; i++) hash = hash * 31 + speaker[i];
                return (hash & 0x7FFFFFFF).ToString("x8", CultureInfo.InvariantCulture);
            }
        }
    }

    // Minimal, dependency-free JSON emitter. Unity's JsonUtility cannot represent the nested/dynamic
    // shape of a packet (and OllamaClient already avoids it for the same reason), so packets are
    // written with an explicit writer that guarantees valid escaping.
    internal sealed class PromptCaptureJsonWriter
    {
        private readonly StringBuilder _sb = new StringBuilder(2048);
        private readonly List<bool> _firstStack = new List<bool>();
        private readonly int _indentWidth;

        internal PromptCaptureJsonWriter() : this(2) { }
        internal PromptCaptureJsonWriter(int indentWidth) { _indentWidth = Math.Max(0, indentWidth); }

        internal PromptCaptureJsonWriter StartObject() { Prefix(); _sb.Append('{'); Push(); return this; }
        internal PromptCaptureJsonWriter StartObject(string name) { Prefix(name); _sb.Append('{'); Push(); return this; }
        internal PromptCaptureJsonWriter EndObject() { Pop(); NewLineIndent(); _sb.Append('}'); return this; }
        internal PromptCaptureJsonWriter StartArray(string name) { Prefix(name); _sb.Append('['); Push(); return this; }
        internal PromptCaptureJsonWriter EndArray() { Pop(); NewLineIndent(); _sb.Append(']'); return this; }

        internal PromptCaptureJsonWriter String(string name, string value)
        {
            Prefix(name);
            AppendString(value);
            return this;
        }

        internal PromptCaptureJsonWriter Number(string name, long value)
        {
            Prefix(name);
            _sb.Append(value.ToString(CultureInfo.InvariantCulture));
            return this;
        }

        internal PromptCaptureJsonWriter Number(string name, double value)
        {
            Prefix(name);
            if (double.IsNaN(value) || double.IsInfinity(value)) _sb.Append('0');
            else _sb.Append(value.ToString("0.####", CultureInfo.InvariantCulture));
            return this;
        }

        internal PromptCaptureJsonWriter Bool(string name, bool value)
        {
            Prefix(name);
            _sb.Append(value ? "true" : "false");
            return this;
        }

        // Writes an already-valid JSON document as a nested value. Used for the exact serialized
        // Ollama body so the packet stores a real object rather than an escaped JSON string.
        internal PromptCaptureJsonWriter RawJson(string name, string json)
        {
            Prefix(name);
            if (string.IsNullOrWhiteSpace(json)) _sb.Append("null");
            else _sb.Append(json);
            return this;
        }

        internal PromptCaptureJsonWriter StringArrayItem(string value)
        {
            Prefix();
            AppendString(value);
            return this;
        }

        private void AppendString(string value)
        {
            if (value == null) { _sb.Append("null"); return; }
            string scrubbed = PromptCaptureRedaction.Scrub(value);
            _sb.Append('"');
            for (int i = 0; i < scrubbed.Length; i++)
            {
                char c = scrubbed[i];
                switch (c)
                {
                    case '\\': _sb.Append("\\\\"); break;
                    case '"': _sb.Append("\\\""); break;
                    case '\r': _sb.Append("\\r"); break;
                    case '\n': _sb.Append("\\n"); break;
                    case '\t': _sb.Append("\\t"); break;
                    case '\b': _sb.Append("\\b"); break;
                    case '\f': _sb.Append("\\f"); break;
                    default:
                        if (c < 0x20) _sb.Append("\\u").Append(((int)c).ToString("x4", CultureInfo.InvariantCulture));
                        else _sb.Append(c);
                        break;
                }
            }
            _sb.Append('"');
        }

        private void Push() { _firstStack.Add(true); }

        private void Pop()
        {
            if (_firstStack.Count > 0) _firstStack.RemoveAt(_firstStack.Count - 1);
        }

        private void Prefix() { Prefix(null); }

        private void Prefix(string name)
        {
            if (_firstStack.Count > 0)
            {
                if (!_firstStack[_firstStack.Count - 1]) _sb.Append(',');
                _firstStack[_firstStack.Count - 1] = false;
                NewLineIndent();
            }
            if (name != null)
            {
                AppendString(name);
                _sb.Append(':');
                if (_indentWidth > 0) _sb.Append(' ');
            }
        }

        private void NewLineIndent()
        {
            if (_indentWidth <= 0) return;
            _sb.Append('\n');
            int depth = _firstStack.Count;
            for (int i = 0; i < depth * _indentWidth; i++) _sb.Append(' ');
        }

        public override string ToString() { return _sb.ToString(); }
    }

    // Per-session bookkeeping: monotonic ids, the configured cap, and whether capture has stopped.
    // Deliberately "stop and warn once" rather than deleting older packets, so collected evidence is
    // never silently destroyed.
    internal sealed class PromptCaptureState
    {
        private readonly object _gate = new object();
        private int _nextRequestId;
        private int _logicalRequests;
        private bool _limitReached;
        private bool _limitWarned;

        internal string SessionId { get; private set; }
        internal int MaxLogicalRequests { get; private set; }
        internal int ClassifierPackets { get; private set; }
        internal int GenerationPackets { get; private set; }
        internal int AcceptedResults { get; private set; }
        internal int RejectedResults { get; private set; }
        internal string PendingManualLabel { get; private set; }

        internal PromptCaptureState(string sessionId, int maxLogicalRequests)
        {
            SessionId = string.IsNullOrEmpty(sessionId) ? "unknown" : sessionId;
            MaxLogicalRequests = maxLogicalRequests < 1 ? 1 : maxLogicalRequests;
            _nextRequestId = 0;
        }

        internal int CapturedLogicalRequests { get { lock (_gate) return _logicalRequests; } }
        internal bool LimitReached { get { lock (_gate) return _limitReached; } }

        internal void SetManualLabel(string label)
        {
            lock (_gate) PendingManualLabel = string.IsNullOrWhiteSpace(label) ? null : Bound(label.Trim(), 60);
        }

        internal string ConsumeManualLabel()
        {
            lock (_gate)
            {
                string label = PendingManualLabel;
                PendingManualLabel = null;
                return label;
            }
        }

        // Returns 0 when capture must not proceed (cap reached). Otherwise a new logical request id.
        internal int TryBeginLogicalRequest(bool classifier)
        {
            lock (_gate)
            {
                if (_limitReached) return 0;
                if (_logicalRequests >= MaxLogicalRequests)
                {
                    _limitReached = true;
                    return 0;
                }
                _logicalRequests++;
                _nextRequestId++;
                if (classifier) ClassifierPackets++; else GenerationPackets++;
                return _nextRequestId;
            }
        }

        internal bool ShouldWarnLimitOnce()
        {
            lock (_gate)
            {
                if (!_limitReached || _limitWarned) return false;
                _limitWarned = true;
                return true;
            }
        }

        internal void RecordResult(string groundingDecision)
        {
            lock (_gate)
            {
                if (string.Equals(groundingDecision, "rejected", StringComparison.OrdinalIgnoreCase)) RejectedResults++;
                else AcceptedResults++;
            }
        }

        internal static string Bound(string value, int max)
        {
            if (string.IsNullOrEmpty(value)) return string.Empty;
            string clean = value.Replace('\r', ' ').Replace('\n', ' ').Trim();
            return clean.Length <= max ? clean : clean.Substring(0, max);
        }

        internal string DescribeStatus(string relativeDirectoryLabel, bool enabled, bool includeClassifier)
        {
            lock (_gate)
            {
                StringBuilder sb = new StringBuilder();
                sb.Append("enabled=").Append(enabled ? "True" : "False");
                sb.Append(" session=").Append(SessionId);
                sb.Append(" captured=").Append(_logicalRequests).Append('/').Append(MaxLogicalRequests);
                sb.Append(" classifier=").Append(ClassifierPackets);
                sb.Append(" generation=").Append(GenerationPackets);
                sb.Append(" accepted=").Append(AcceptedResults);
                sb.Append(" rejected=").Append(RejectedResults);
                sb.Append(" includeClassifier=").Append(includeClassifier ? "True" : "False");
                if (_limitReached) sb.Append(" limitReached=True");
                sb.Append(" directory=").Append(string.IsNullOrEmpty(relativeDirectoryLabel) ? "<none>" : relativeDirectoryLabel);
                return sb.ToString();
            }
        }
    }

    // Log-line builder for the NORMAL log. Privacy-safe metadata only: never prompt text, never
    // player dialogue, never model output.
    internal static class PromptCaptureLogLine
    {
        internal static string Build(int requestId, string source, string speaker, string route, int messages, int chars, string result)
        {
            StringBuilder sb = new StringBuilder("PromptCapture: request=");
            sb.Append(requestId);
            sb.Append(" source=").Append(Safe(source));
            sb.Append(" speakerHash=").Append(PromptCaptureRedaction.SpeakerHash(speaker));
            sb.Append(" route=").Append(Safe(route));
            sb.Append(" messages=").Append(messages);
            sb.Append(" chars=").Append(chars);
            sb.Append(" result=").Append(Safe(result));
            return sb.ToString();
        }

        private static string Safe(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return "none";
            string clean = PromptCaptureState.Bound(value, 40).Replace(' ', '_');
            return clean.Length == 0 ? "none" : clean;
        }
    }
}
