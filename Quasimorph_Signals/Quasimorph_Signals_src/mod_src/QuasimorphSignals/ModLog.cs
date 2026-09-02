using System;
using System.IO;
using System.Text;

namespace QuasimorphSignals
{
    /// <summary>
    /// Writes a plain-text log next to the mod assembly. The game's own
    /// Player.log is shared by everything, so a dedicated file keeps
    /// troubleshooting a single-file question for the user.
    /// </summary>
    internal static class ModLog
    {
        private const string LogFileName = "QuasimorphSignals.log";
        private static readonly object Gate = new object();
        private static string _path;
        private static bool _failed;

        internal static void Start(string modDirectory)
        {
            _path = Path.Combine(modDirectory, LogFileName);
            _failed = false;
            try
            {
                File.WriteAllText(_path, string.Empty, new UTF8Encoding(false));
            }
            catch (Exception)
            {
                // A read-only mod folder must not stop the mod from working.
                _failed = true;
            }
            Info("=== Quasimorph Signals " + ModInfo.Version + " ===");
            Info("mod directory: " + modDirectory);
        }

        internal static void Info(string message) => Write("INFO ", message);

        internal static void Warn(string message) => Write("WARN ", message);

        internal static void Error(string message, Exception error = null)
        {
            Write("ERROR", error == null ? message : message + " :: " + error);
        }

        private static void Write(string level, string message)
        {
            var line = DateTime.Now.ToString("HH:mm:ss.fff") + " [" + level + "] " + message;

            // Mirror into Player.log so a user can hand over one file and still
            // have the whole story if the mod folder was not writable.
            UnityEngine.Debug.Log("[QuasimorphSignals] " + message);

            if (_failed || _path == null)
            {
                return;
            }

            try
            {
                lock (Gate)
                {
                    File.AppendAllText(_path, line + Environment.NewLine, new UTF8Encoding(false));
                }
            }
            catch (Exception)
            {
                _failed = true;
            }
        }
    }

    internal static class ModInfo
    {
        internal const string Version = "0.2.0";
        internal const string UniqueName = "QuasimorphSignals";
        internal const string HarmonyId = "quasimorph.signals";
    }
}
