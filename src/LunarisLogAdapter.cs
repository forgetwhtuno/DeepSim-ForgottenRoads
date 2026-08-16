using Lunaris;

namespace ErenshorDeepSims
{
    internal sealed class LunarisDeepSimsLog : IDeepSimsLog
    {
        private readonly ILog _log;

        internal LunarisDeepSimsLog(ILog log) { _log = log; }
        public void LogDebug(string message) { if (_log != null && DeepSimsDiagnostics.Verbose) _log.LogDebug(message ?? string.Empty); }
        public void LogInfo(string message) { if (_log != null) _log.LogInfo(message ?? string.Empty); }
        public void LogWarning(string message) { if (_log != null) _log.LogWarning(message ?? string.Empty); }
        public void LogError(string message) { if (_log != null) _log.LogError(message ?? string.Empty); }
    }
}
