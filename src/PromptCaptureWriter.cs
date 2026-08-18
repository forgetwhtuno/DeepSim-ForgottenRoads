using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;

namespace ErenshorDeepSims
{
    // ---------------------------------------------------------------------------------------------
    // Filesystem side of prompt capture. Unity-free so the deterministic harness can drive it against
    // a temporary directory.
    //
    // HARD RULE: every public entry point is best-effort. A diagnostic write failure must never
    // surface to the generation pipeline, so all IO is wrapped and only the exception TYPE plus the
    // packet id is reported back to the caller's log.
    // ---------------------------------------------------------------------------------------------
    internal sealed class PromptCaptureWriter
    {
        private readonly string _sessionDirectory;
        private readonly object _indexGate = new object();
        private readonly Action<string> _safeLog;

        internal string SessionDirectory { get { return _sessionDirectory; } }

        internal PromptCaptureWriter(string sessionDirectory, Action<string> safeLog)
        {
            _sessionDirectory = sessionDirectory ?? string.Empty;
            _safeLog = safeLog;
        }

        internal bool EnsureDirectory()
        {
            try
            {
                if (string.IsNullOrEmpty(_sessionDirectory)) return false;
                Directory.CreateDirectory(_sessionDirectory);
                return true;
            }
            catch (Exception ex)
            {
                Report(0, ex);
                return false;
            }
        }

        // Serializes on the calling thread only if `synchronous` is set (tests). In game we hand the
        // already-copied packet to the thread pool so no serialization happens on Unity's main thread.
        internal void WritePacket(PromptCapturePacket packet, bool synchronous)
        {
            if (packet == null) return;
            if (synchronous) { WritePacketCore(packet); return; }
            try
            {
                ThreadPool.QueueUserWorkItem(delegate { WritePacketCore(packet); });
            }
            catch (Exception ex)
            {
                // Queueing itself failed; fall back to a direct best-effort write rather than losing
                // the packet, still without ever rethrowing into the caller.
                Report(packet.RequestId, ex);
                WritePacketCore(packet);
            }
        }

        private void WritePacketCore(PromptCapturePacket packet)
        {
            if (packet == null) return;
            try
            {
                if (!EnsureDirectory()) return;
                WriteTextFile(Path.Combine(_sessionDirectory, packet.RequestFileName),
                    PromptCapturePacketSerializer.BuildSemanticRequestJson(packet));
                WriteTextFile(Path.Combine(_sessionDirectory, packet.RawRequestFileName),
                    PromptCapturePacketSerializer.BuildExactRequestJson(packet));
                WriteTextFile(Path.Combine(_sessionDirectory, packet.ResultFileName),
                    PromptCapturePacketSerializer.BuildResultJson(packet));
                AppendIndex(packet);
            }
            catch (Exception ex)
            {
                Report(packet.RequestId, ex);
            }
        }

        private void AppendIndex(PromptCapturePacket packet)
        {
            try
            {
                string line = PromptCapturePacketSerializer.BuildIndexLine(packet);
                if (string.IsNullOrEmpty(line)) return;
                lock (_indexGate)
                {
                    File.AppendAllText(Path.Combine(_sessionDirectory, "index.jsonl"), line + "\n", new UTF8Encoding(false));
                }
            }
            catch (Exception ex)
            {
                Report(packet == null ? 0 : packet.RequestId, ex);
            }
        }

        private static void WriteTextFile(string path, string content)
        {
            File.WriteAllText(path, content ?? string.Empty, new UTF8Encoding(false));
        }

        private void Report(int requestId, Exception ex)
        {
            if (_safeLog == null) return;
            try
            {
                _safeLog("Prompt capture write failed request=" + requestId + " error=" + DiagnosticPrivacy.ExceptionType(ex));
            }
            catch { }
        }

        // Test/diagnostic helper: how many logical request packets exist on disk.
        internal int CountRequestFiles()
        {
            try
            {
                if (!Directory.Exists(_sessionDirectory)) return 0;
                return Directory.GetFiles(_sessionDirectory, "*-request.json", SearchOption.TopDirectoryOnly).Length;
            }
            catch { return 0; }
        }
    }

    // Process-wide facade used by the plugin. Every method is a no-op unless capture was explicitly
    // enabled, so the OFF path costs one boolean read.
    internal static class PromptCapture
    {
        private static readonly object _gate = new object();
        private static bool _enabled;
        private static bool _includeClassifier;
        private static PromptCaptureState _state;
        private static PromptCaptureWriter _writer;
        private static string _dataRoot = string.Empty;
        private static Action<string> _log;
        private static int _sessionSequence;

        internal static bool Enabled { get { return _enabled; } }

        internal static PromptCaptureState State { get { lock (_gate) return _state; } }

        internal static bool IncludeClassifier { get { return _includeClassifier; } }

        // Called when the operator turns capture on. Creates a fresh session directory so separate
        // capture runs never interleave.
        internal static bool Start(string diagnosticsRoot, string dataRoot, int maxLogicalRequests,
            bool includeClassifier, Action<string> safeLog)
        {
            lock (_gate)
            {
                try
                {
                    _log = safeLog;
                    _dataRoot = dataRoot ?? string.Empty;
                    _includeClassifier = includeClassifier;
                    _sessionSequence++;
                    string sessionId = PromptCaptureRedaction.SafeSessionId(DateTime.UtcNow, _sessionSequence);
                    _state = new PromptCaptureState(sessionId, maxLogicalRequests);
                    _writer = new PromptCaptureWriter(Path.Combine(diagnosticsRoot ?? string.Empty, "session-" + sessionId), safeLog);
                    if (!_writer.EnsureDirectory())
                    {
                        _enabled = false;
                        return false;
                    }
                    _enabled = true;
                    return true;
                }
                catch (Exception ex)
                {
                    _enabled = false;
                    if (safeLog != null)
                    {
                        try { safeLog("Prompt capture start failed error=" + DiagnosticPrivacy.ExceptionType(ex)); } catch { }
                    }
                    return false;
                }
            }
        }

        internal static void Stop()
        {
            lock (_gate) { _enabled = false; }
        }

        internal static void SetIncludeClassifier(bool value) { _includeClassifier = value; }

        // Returns null when capture is off, the cap is reached, or anything goes wrong. Callers treat
        // null as "no capture" and continue normally.
        internal static PromptCapturePacket TryBegin(string stage, string source, bool classifier)
        {
            if (!_enabled) return null;
            try
            {
                PromptCaptureState state;
                lock (_gate) state = _state;
                if (state == null) return null;
                if (classifier && !_includeClassifier) return null;
                int requestId = state.TryBeginLogicalRequest(classifier);
                if (requestId <= 0)
                {
                    if (state.ShouldWarnLimitOnce() && _log != null)
                    {
                        try { _log("Prompt capture reached configured maximum; capture disabled for this session."); } catch { }
                    }
                    return null;
                }
                PromptCapturePacket packet = new PromptCapturePacket();
                packet.SessionId = state.SessionId;
                packet.RequestId = requestId;
                packet.Stage = stage ?? string.Empty;
                packet.Source = source ?? string.Empty;
                packet.IsClassifier = classifier;
                packet.Utc = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffZ", System.Globalization.CultureInfo.InvariantCulture);
                string label = state.ConsumeManualLabel();
                if (!string.IsNullOrEmpty(label)) packet.ManualLabel = label;
                return packet;
            }
            catch { return null; }
        }

        internal static void Complete(PromptCapturePacket packet)
        {
            if (packet == null) return;
            try
            {
                PromptCaptureWriter writer;
                PromptCaptureState state;
                lock (_gate) { writer = _writer; state = _state; }
                if (writer == null) return;
                packet.InterestingCases = PromptCaptureInterestingCases.Derive(packet.Stage, packet.RawTurnType,
                    packet.EffectiveTurnType, packet.RawKnowledgeNeed, packet.EffectiveKnowledgeNeed,
                    packet.RetrievalUsed, packet.RetrievalFound, packet.GroundingDecision,
                    packet.GroundingReasonCategory, packet.ConnectedSimTurn);
                if (state != null) state.RecordResult(packet.GroundingDecision);
                writer.WritePacket(packet, false);
                if (_log != null)
                {
                    int chars = 0;
                    for (int i = 0; i < packet.Messages.Count; i++) chars += packet.Messages[i].Content.Length;
                    try
                    {
                        _log(PromptCaptureLogLine.Build(packet.RequestId, packet.Source, packet.SpeakerName,
                            packet.EffectiveTurnType, packet.Messages.Count, chars, packet.GroundingDecision));
                    }
                    catch { }
                }
            }
            catch { }
        }

        internal static string RelativeDirectoryLabel()
        {
            try
            {
                PromptCaptureWriter writer;
                lock (_gate) writer = _writer;
                if (writer == null) return "<none>";
                return PromptCaptureRedaction.RelativeLabel(writer.SessionDirectory, _dataRoot);
            }
            catch { return "<none>"; }
        }

        internal static string StatusLine()
        {
            try
            {
                PromptCaptureState state;
                lock (_gate) state = _state;
                if (state == null) return "enabled=False session=<none> captured=0/0";
                return state.DescribeStatus(RelativeDirectoryLabel(), _enabled, _includeClassifier);
            }
            catch { return "enabled=False session=<none>"; }
        }

        internal static void MarkNext(string label)
        {
            try
            {
                PromptCaptureState state;
                lock (_gate) state = _state;
                if (state != null) state.SetManualLabel(label);
            }
            catch { }
        }

        // Test seam: reset everything so a deterministic test can run an isolated session.
        internal static void ResetForTests()
        {
            lock (_gate)
            {
                _enabled = false;
                _includeClassifier = false;
                _state = null;
                _writer = null;
                _dataRoot = string.Empty;
                _log = null;
            }
        }

        internal static PromptCaptureWriter WriterForTests { get { lock (_gate) return _writer; } }
    }
}
