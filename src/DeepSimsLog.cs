namespace ErenshorDeepSims
{
    // Loader-neutral logging surface so the social/memory/network subsystems do not depend on
    // BepInEx or Lunaris implementation types.
    internal interface IDeepSimsLog
    {
        void LogDebug(string message);
        void LogInfo(string message);
        void LogWarning(string message);
        void LogError(string message);
    }

    internal sealed class NullDeepSimsLog : IDeepSimsLog
    {
        internal static readonly NullDeepSimsLog Instance = new NullDeepSimsLog();
        private NullDeepSimsLog() { }
        public void LogDebug(string message) { }
        public void LogInfo(string message) { }
        public void LogWarning(string message) { }
        public void LogError(string message) { }
    }
}
