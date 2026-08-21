using System.Collections;
using UnityEngine;

namespace IdiotTape.Audio
{

    [DisallowMultipleComponent]
    public sealed class FmodSongPlaybackTestHarness : MonoBehaviour
    {

        private const float PreparationTimeoutSeconds = 15f;
        private const float PlaybackTimeoutSeconds = 5f;

        [SerializeField]
        private FmodSongPlayback songPlayback;

        [SerializeField]
        private bool runAutomaticSmokeTest = true;

        private float synthVolume = 1f;
        private float bassVolume = 1f;
        private float drumVolume = 1f;
        private float etcVolume = 1f;
        private string testStatus = "Not started";
        private bool smokeTestRunning;

        public void Configure(FmodSongPlayback playback, bool runAutomatically)
        {

            songPlayback = playback;
            runAutomaticSmokeTest = runAutomatically;

        }

        private void Start()
        {

            if (runAutomaticSmokeTest)
            {

                StartCoroutine(RunSmokeTest());

            }

        }

        private void OnGUI()
        {

            const float panelWidth = 460f;
            const float panelHeight = 430f;

            GUILayout.BeginArea(new Rect(20f, 20f, panelWidth, panelHeight), GUI.skin.box);
            GUILayout.Label("Idiot_Tape FMOD Playback Test");
            GUILayout.Label($"Status: {testStatus}");

            if (songPlayback != null)
            {

                GUILayout.Label($"Prepared: {songPlayback.IsPrepared}");
                GUILayout.Label($"Playing: {songPlayback.IsPlaying}");
                GUILayout.Label($"Paused: {songPlayback.IsPaused}");
                GUILayout.Label($"Timeline: {songPlayback.PlaybackPositionSeconds:F3} s");

                GUILayout.BeginHorizontal();

                if (GUILayout.Button("Play"))
                {

                    songPlayback.Play();

                }

                if (GUILayout.Button("Pause"))
                {

                    songPlayback.Pause();

                }

                if (GUILayout.Button("Resume"))
                {

                    songPlayback.Resume();

                }

                if (GUILayout.Button("Restart"))
                {

                    songPlayback.Restart();

                }

                if (GUILayout.Button("Stop"))
                {

                    songPlayback.Stop();

                }

                GUILayout.EndHorizontal();

                if (GUILayout.Button("Seek to 30 seconds"))
                {

                    songPlayback.Seek(30d);

                }

                synthVolume = DrawStemSlider("Synth", "synth", synthVolume);
                bassVolume = DrawStemSlider("Bass", "bass", bassVolume);
                drumVolume = DrawStemSlider("Drum", "drum", drumVolume);
                etcVolume = DrawStemSlider("ETC", "etc", etcVolume);

                GUI.enabled = !smokeTestRunning;

                if (GUILayout.Button("Run automated smoke test"))
                {

                    StartCoroutine(RunSmokeTest());

                }

                GUI.enabled = true;

            }
            else
            {

                GUILayout.Label("Song playback reference is missing.");

            }

            GUILayout.EndArea();

        }

        private float DrawStemSlider(string label, string stemId, float currentValue)
        {

            GUILayout.BeginHorizontal();
            GUILayout.Label(label, GUILayout.Width(60f));
            float nextValue = GUILayout.HorizontalSlider(currentValue, 0f, 1f);
            GUILayout.Label(nextValue.ToString("F2"), GUILayout.Width(40f));
            GUILayout.EndHorizontal();

            if (!Mathf.Approximately(currentValue, nextValue))
            {

                songPlayback.SetStemVolume(stemId, nextValue);

            }

            return nextValue;

        }

        private IEnumerator RunSmokeTest()
        {

            if (smokeTestRunning)
            {

                yield break;

            }

            smokeTestRunning = true;
            testStatus = "Preparing FMOD event";
            songPlayback.Prepare();

            float deadline = Time.realtimeSinceStartup + PreparationTimeoutSeconds;

            while (!songPlayback.IsPrepared && Time.realtimeSinceStartup < deadline)
            {

                yield return null;

            }

            if (!songPlayback.IsPrepared)
            {

                Fail("Timed out while preparing event:/Music/Idiotape/Pluto.");
                yield break;

            }

            testStatus = "Checking playback timeline";
            songPlayback.Restart();
            deadline = Time.realtimeSinceStartup + PlaybackTimeoutSeconds;

            while (songPlayback.PlaybackPositionSeconds < 0.15d && Time.realtimeSinceStartup < deadline)
            {

                yield return null;

            }

            if (!songPlayback.IsPlaying || songPlayback.PlaybackPositionSeconds < 0.15d)
            {

                Fail("The FMOD event started, but its timeline did not advance.");
                yield break;

            }

            testStatus = "Checking stem parameters";

            if (!CheckStemParameter("synth") ||
                !CheckStemParameter("bass") ||
                !CheckStemParameter("drum") ||
                !CheckStemParameter("etc"))
            {

                Fail("One or more stem IDs could not be controlled.");
                yield break;

            }

            testStatus = "Checking pause and resume";
            songPlayback.Pause();
            yield return null;

            if (!songPlayback.IsPaused)
            {

                Fail("The FMOD event did not enter the paused state.");
                yield break;

            }

            // FMOD applies Studio commands on its mixer update. Establish the pause
            // baseline after that command has crossed the asynchronous boundary.
            yield return WaitForRealtimeSeconds(0.5f);
            double pausedPosition = songPlayback.PlaybackPositionSeconds;
            yield return WaitForRealtimeSeconds(0.25f);
            double positionAfterPauseWait = songPlayback.PlaybackPositionSeconds;

            if (System.Math.Abs(positionAfterPauseWait - pausedPosition) > 0.05d)
            {

                Fail(
                    $"The FMOD timeline advanced while paused " +
                    $"({pausedPosition:F3} -> {positionAfterPauseWait:F3} seconds).");
                yield break;

            }

            songPlayback.Resume();
            deadline = Time.realtimeSinceStartup + PlaybackTimeoutSeconds;

            while (songPlayback.PlaybackPositionSeconds < pausedPosition + 0.15d &&
                   Time.realtimeSinceStartup < deadline)
            {

                yield return null;

            }

            if (!songPlayback.IsPlaying || songPlayback.PlaybackPositionSeconds < pausedPosition + 0.15d)
            {

                Fail("The FMOD timeline did not resume.");
                yield break;

            }

            testStatus = "Checking seek";

            if (!songPlayback.Seek(5d))
            {

                Fail("FMOD rejected a seek request.");
                yield break;

            }

            deadline = Time.realtimeSinceStartup + 2f;

            while (songPlayback.PlaybackPositionSeconds < 4.75d && Time.realtimeSinceStartup < deadline)
            {

                yield return null;

            }

            if (System.Math.Abs(songPlayback.PlaybackPositionSeconds - 5d) > 0.5d)
            {

                Fail($"Seek landed at {songPlayback.PlaybackPositionSeconds:F3} seconds instead of 5 seconds.");
                yield break;

            }

            songPlayback.Stop();
            smokeTestRunning = false;
            testStatus = "PASS";
            Debug.Log("[FMOD Playback Smoke Test] PASS", this);
            ExitBatchMode(0);

        }

        private bool CheckStemParameter(string stemId)
        {

            if (!songPlayback.SetStemVolume(stemId, 0f))
            {

                return false;

            }

            return songPlayback.SetStemVolume(stemId, 1f);

        }

        private static IEnumerator WaitForRealtimeSeconds(float durationSeconds)
        {

            float deadline = Time.realtimeSinceStartup + durationSeconds;

            while (Time.realtimeSinceStartup < deadline)
            {

                yield return null;

            }

        }

        private void Fail(string reason)
        {

            smokeTestRunning = false;
            testStatus = $"FAIL: {reason}";
            songPlayback.Stop();
            Debug.LogError($"[FMOD Playback Smoke Test] FAIL: {reason}", this);
            ExitBatchMode(1);

        }

        private static void ExitBatchMode(int exitCode)
        {

            if (!Application.isBatchMode)
            {

                return;

            }

#if UNITY_EDITOR
            UnityEditor.EditorApplication.Exit(exitCode);
#else
            Application.Quit(exitCode);
#endif

        }

    }

}
