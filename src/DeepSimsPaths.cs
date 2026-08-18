using System;
using System.IO;

namespace ErenshorDeepSims
{
    // Lunaris stores plugin config under <Erenshor>/plugins/config. Keep Deep Sims' larger sidecar
    // data in a namespaced child of that persistent root; never touch Erenshor's own save files.
    internal static class DeepSimsPaths
    {
        internal static string ConfigRoot { get { return Path.Combine(AppContext.BaseDirectory, "plugins", "config"); } }
        internal static string DataRoot { get { return Path.Combine(ConfigRoot, "DeepSims"); } }
        internal static string MemoryDirectory { get { return Path.Combine(DataRoot, "Memory"); } }
        internal static string CharacterMemoryRoot { get { return Path.Combine(MemoryDirectory, "Characters"); } }
        internal static string CharacterMemoryDirectory(string characterKey)
        {
            string key = string.IsNullOrWhiteSpace(characterKey) ? CharacterScopeKey.Unscoped : CharacterScopeKey.SafeKey(characterKey);
            // CharacterScopeKey.Compose already produces a filesystem-safe key; SafeKey is repeated
            // here as defense in depth for any future external caller.
            return Path.Combine(CharacterMemoryRoot, key);
        }
        internal static string ExportDirectory { get { return Path.Combine(DataRoot, "Exports"); } }

        // Local-only developer diagnostics. Never packaged, never published, never committed:
        // see .gitignore and the suite release whitelist. Created lazily only when prompt capture is
        // explicitly enabled, so ordinary installs never grow this directory.
        internal static string DiagnosticsRoot { get { return Path.Combine(DataRoot, "Diagnostics"); } }
        internal static string PromptCaptureRoot { get { return Path.Combine(DiagnosticsRoot, "PromptCapture"); } }
        internal static string DiagnosticFile(string name) { return Path.Combine(DataRoot, name); }

        internal static bool HasLegacyGlobalMemory()
        {
            try { return Directory.GetFiles(MemoryDirectory, "*.json", SearchOption.TopDirectoryOnly).Length > 0; }
            catch { return false; }
        }

        internal static void EnsureDataDirectories(IDeepSimsLog log)
        {
            Directory.CreateDirectory(ConfigRoot);
            Directory.CreateDirectory(DataRoot);
            Directory.CreateDirectory(MemoryDirectory);
            Directory.CreateDirectory(CharacterMemoryRoot);
            Directory.CreateDirectory(ExportDirectory);
            TryImportDirectBepInExMemory(log);
        }

        // Conservative one-time convenience for users who previously installed Deep Sims directly in
        // the game-root BepInEx profile. We intentionally do NOT search arbitrary r2modman/Thunderstore
        // profiles. Existing data is copied only into an empty Lunaris memory directory and is never
        // deleted or rewritten in place.
        private static void TryImportDirectBepInExMemory(IDeepSimsLog log)
        {
            try
            {
                if (Directory.GetFiles(MemoryDirectory, "*", SearchOption.AllDirectories).Length != 0) return;
                string oldRoot = Path.Combine(AppContext.BaseDirectory, "BepInEx", "config", "DeepSims", "Memory");
                if (!Directory.Exists(oldRoot)) return;
                CopyMissingTree(oldRoot, MemoryDirectory);
                if (log != null) log.LogInfo("Preserved existing direct-install Deep Sims memory as unscoped legacy data. It is not assigned to a character automatically; the old files were left untouched.");
            }
            catch (Exception ex)
            {
                if (log != null) log.LogWarning("Could not import old direct-install Deep Sims memory: " + ex.GetType().Name);
            }
        }

        private static void CopyMissingTree(string source, string destination)
        {
            Directory.CreateDirectory(destination);
            string[] directories = Directory.GetDirectories(source, "*", SearchOption.AllDirectories);
            for (int i = 0; i < directories.Length; i++)
            {
                string relative = directories[i].Substring(source.Length).TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                Directory.CreateDirectory(Path.Combine(destination, relative));
            }
            string[] files = Directory.GetFiles(source, "*", SearchOption.AllDirectories);
            for (int i = 0; i < files.Length; i++)
            {
                string relative = files[i].Substring(source.Length).TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                string target = Path.Combine(destination, relative);
                Directory.CreateDirectory(Path.GetDirectoryName(target));
                if (!File.Exists(target)) File.Copy(files[i], target, false);
            }
        }
    }
}
