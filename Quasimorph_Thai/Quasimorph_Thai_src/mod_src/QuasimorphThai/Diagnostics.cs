using System.Collections;
using System.IO;
using UnityEngine;

namespace QuasimorphThai
{
    /// <summary>
    /// Opt-in troubleshooting aid. Only runs when a file named <c>diagnostics.on</c> sits
    /// next to the mod, so it costs a normal player nothing.
    ///
    /// It exists because Thai rendering problems (missing glyphs, clipped vowel marks)
    /// cannot be diagnosed from a log alone - somebody has to look at a picture.
    /// </summary>
    internal static class Diagnostics
    {
        private const string SwitchFileName = "diagnostics.on";
        private const string ShotFileName = "screenshot.png";

        internal static bool IsEnabled(string modDirectory)
        {
            return File.Exists(Path.Combine(modDirectory, SwitchFileName));
        }

        internal static void CaptureAfterDelay(string modDirectory, float seconds)
        {
            if (!IsEnabled(modDirectory))
            {
                return;
            }

            var host = new GameObject("QuasimorphThaiDiagnostics");
            Object.DontDestroyOnLoad(host);
            host.hideFlags = HideFlags.HideAndDontSave;
            host.AddComponent<DiagnosticsRunner>().Begin(modDirectory, seconds);
        }

        private sealed class DiagnosticsRunner : MonoBehaviour
        {
            private string _modDirectory;
            private float _delay;

            internal void Begin(string modDirectory, float delay)
            {
                _modDirectory = modDirectory;
                _delay = delay;
                StartCoroutine(Run());
            }

            private IEnumerator Run()
            {
                yield return new WaitForSeconds(_delay);

                var path = Path.Combine(_modDirectory, ShotFileName);
                if (File.Exists(path))
                {
                    File.Delete(path);
                }

                // Supersize so Thai vowel and tone marks are legible when reviewing
                // the shot; mark height is the usual failure mode for Thai in TMP.
                ScreenCapture.CaptureScreenshot(path, 3);
                ModLog.Info("Diagnostics: screenshot requested -> " + path);

                // CaptureScreenshot writes asynchronously at end of frame.
                for (var i = 0; i < 60 && !File.Exists(path); i++)
                {
                    yield return new WaitForEndOfFrame();
                }
                ModLog.Info("Diagnostics: screenshot written=" + File.Exists(path));
            }
        }
    }
}
