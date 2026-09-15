#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Reflection;
using IdiotTape.Audio;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace IdiotTape.Gameplay.Tests
{

    public sealed class ChartAuthoringRecordingWorkflowTests
    {

        private const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;
        private static GameObject playModeListener;

        [OneTimeSetUp]
        public void KeepAListenerAvailableForTheSharedFmodRuntime()
        {

            if (FMODUnity.StudioListener.ListenerCount > 0)
            {

                return;

            }

            // FMOD's shared RuntimeManager keeps updating between test cases and fixture classes.
            // Per-case teardown must not leave it without a listener during that intervening frame.
            // This test-created object shares the Play Mode lifetime of that audio runtime, then
            // Unity destroys it on exit; the callback also handles disabled domain-reload settings.
            playModeListener = new GameObject("Authoring workflow test listener (Play Mode lifetime)");
            playModeListener.AddComponent<FMODUnity.StudioListener>();
            Object.DontDestroyOnLoad(playModeListener);
            EditorApplication.playModeStateChanged += ReleasePlayModeListener;

        }

        private static void ReleasePlayModeListener(PlayModeStateChange state)
        {

            if (state != PlayModeStateChange.EnteredEditMode)
            {

                return;

            }

            EditorApplication.playModeStateChanged -= ReleasePlayModeListener;
            if (playModeListener != null)
            {

                Object.DestroyImmediate(playModeListener);

            }

            playModeListener = null;

        }

        [UnityTest]
        public IEnumerator LaterCurrentPositionRetryPreservesEarlierTakeAndSavedChart()
        {

            return RunRecordingWorkflow("CurrentPosition");

        }

        [UnityTest]
        public IEnumerator LaterLoopRetryAndAutomaticReentryPreserveEarlierTakeAndSavedChart()
        {

            return RunRecordingWorkflow("Loop");

        }

        private static IEnumerator RunRecordingWorkflow(string laterStartMode)
        {

            PrototypeChart source = AssetDatabase.LoadAssetAtPath<PrototypeChart>("Assets/Data/PlutoPrototypeChart.asset");
            Assert.That(source, Is.Not.Null);
            string sourceBefore = JsonUtility.ToJson(source);
            string folder = "Assets/__AuthoringRecordingWorkflow_" + Guid.NewGuid().ToString("N");
            string assetPath = folder + "/Recording.asset";
            AssetDatabase.CreateFolder("Assets", Path.GetFileName(folder));
            PrototypeChart chart = Object.Instantiate(source);
            chart.hideFlags = HideFlags.None;
            AssetDatabase.CreateAsset(chart, assetPath);
            GameObject playbackObject = new("Authoring workflow FMOD verification");
            playbackObject.AddComponent<FMODUnity.StudioListener>();
            FmodSongPlayback playback = playbackObject.AddComponent<FmodSongPlayback>();
            EditorWindow window = null;
            GameplaySession session = Object.FindAnyObjectByType<GameplaySession>();
            bool sessionWasEnabled = session != null && session.enabled;
            int previousFrameRate = Application.targetFrameRate;
            int previousVsync = QualitySettings.vSyncCount;
            bool previousBackground = Application.runInBackground;
            bool previousTextEditing = EditorGUIUtility.editingTextField;
            Stopwatch elapsed = Stopwatch.StartNew();
            int guiActions = 0;
            bool passed = false;

            try
            {

                QualitySettings.vSyncCount = 0;
                Application.targetFrameRate = 60;
                Application.runInBackground = true;
                EditorGUIUtility.editingTextField = false;
                if (session != null)
                {

                    session.enabled = false;

                }

                playback.ConfigureEventPath(chart.SongEventPath, false);
                playback.ConfigureStemParameters(chart.StemParameters);
                playback.Prepare();
                double deadline = Time.realtimeSinceStartupAsDouble + 15d;
                while (!playback.IsPrepared && !playback.PreparationFailed && Time.realtimeSinceStartupAsDouble < deadline)
                {

                    yield return null;

                }

                Assert.That(playback.IsPrepared, Is.True);
                Type type = Type.GetType("IdiotTape.EditorTools.PrototypeChartRecorderWindow, IdiotTape.Gameplay.Editor", true);
                window = (EditorWindow)ScriptableObject.CreateInstance(type);
                window.hideFlags = HideFlags.DontSave;
                Set(window, "chart", chart);
                Set(window, "songPlayback", playback);
                Set(window, "configuredEventPath", chart.SongEventPath);
                Set(window, "workspaceInitialized", true);
                Set(window, "timelineAutoScroll", false);
                Set(window, "metronomeDuringRecording", false);
                Set(window, "automaticQuantization", false);
                Set(window, "addMissingActivationWindows", true);
                Set(window, "loopStart", 8d);
                Set(window, "loopEnd", 9.25d);
                SetEnum(window, "recordingNoteMode", "Tap");
                SetEnum(window, "applyMode", "Append");
                window.ShowUtility();
                window.position = new Rect(80f, 80f, 1200f, 800f);
                window.Focus();
                yield return null;
                Render(window);
                Assert.That(chart.Notes.Count, Is.EqualTo(0), "The Pluto fixture starts as an empty chart.");

                StartMode(window, "Beginning");
                yield return WaitForRecording(window, playback);
                SendKey(window, KeyCode.Alpha2);
                guiActions++;
                yield return new WaitForSecondsRealtime(0.12f);
                SendKey(window, KeyCode.Alpha3);
                guiActions++;
                Invoke(window, "StopRecording");
                playback.Stop();
                Assert.That(Records(window).Count, Is.EqualTo(2));
                Dictionary<string, string> earlierTake = SnapshotRecords(window);

                if (laterStartMode == "CurrentPosition")
                {

                    Assert.That(playback.SchedulePlay(0.2d, 8d, out _, out _), Is.True);
                    deadline = Time.realtimeSinceStartupAsDouble + 5d;
                    while ((!playback.HasReachedScheduledStart || playback.SongTime < 8d) &&
                        Time.realtimeSinceStartupAsDouble < deadline)
                    {

                        yield return null;

                    }

                    Assert.That(playback.SongTime, Is.GreaterThanOrEqualTo(8d));

                }

                StartMode(window, laterStartMode);
                double savedTarget = LastTakeTarget(window);
                Assert.That(savedTarget, Is.GreaterThanOrEqualTo(8d));
                Assert.That(savedTarget, Is.LessThan(9d));
                yield return WaitForRecording(window, playback);
                Assert.That(playback.SongTime, Is.InRange(savedTarget, savedTarget + 0.2d),
                    "Positive pre-roll must arrive at the later recording target using the scheduled song clock.");
                SendKey(window, KeyCode.Alpha4);
                guiActions++;
                yield return new WaitForSecondsRealtime(0.12f);
                SendKey(window, KeyCode.Alpha5);
                guiActions++;
                Invoke(window, "StopRecording");
                playback.Stop();
                Assert.That(Records(window).Count, Is.EqualTo(4));
                Dictionary<string, string> beforeRetry = SnapshotRecords(window);
                Assert.That((bool)Invoke(window, "CanRetryLastTake"), Is.True);

                // Scheduling and retry commands use their real production methods. Number-key,
                // apply and save inputs below traverse the actual EditorWindow IMGUI event path.
                Invoke(window, "RetryLastTake");
                Assert.That(LastTakeTarget(window), Is.EqualTo(savedTarget).Within(0.000001d),
                    "Retry must retain the original target even though stopping playback changed the current position.");
                Assert.That(Records(window).Count, Is.EqualTo(2));
                AssertEarlierTake(window, earlierTake);
                yield return WaitForRecording(window, playback);
                Assert.That(playback.SongTime, Is.InRange(savedTarget, savedTarget + 0.2d));
                SendKey(window, KeyCode.Alpha6);
                guiActions++;
                yield return new WaitForSecondsRealtime(0.12f);
                SendKey(window, KeyCode.Alpha7);
                guiActions++;
                if (laterStartMode == "Loop")
                {

                    deadline = Time.realtimeSinceStartupAsDouble + 5d;
                    while ((bool)Get(window, "isRecording") && Time.realtimeSinceStartupAsDouble < deadline)
                    {

                        yield return null;

                    }

                    Assert.That((bool)Get(window, "isRecording"), Is.False,
                        "The later loop must reenter its positive pre-roll after reaching the loop end.");
                    Assert.That(Get(window, "recordingPhase").ToString(), Is.Not.EqualTo("Idle"));
                    yield return WaitForRecording(window, playback);
                    Assert.That(playback.SongTime, Is.InRange(savedTarget, savedTarget + 0.2d));
                    SendKey(window, KeyCode.Alpha8);
                    guiActions++;

                }

                Invoke(window, "StopRecording");
                playback.Stop();
                int expectedNotes = laterStartMode == "Loop" ? 5 : 4;
                Assert.That(Records(window).Count, Is.EqualTo(expectedNotes));
                AssertEarlierTake(window, earlierTake);
                Dictionary<string, string> replacement = SnapshotRecords(window);
                foreach (string id in beforeRetry.Keys)
                {

                    if (!earlierTake.ContainsKey(id))
                    {

                        Assert.That(replacement.ContainsKey(id), Is.False, "The rejected take must not survive retry.");

                    }

                }

                if (laterStartMode == "CurrentPosition")
                {

                    yield return RecordOtherInteractionKinds(window, playback);
                    guiActions += 8;
                    expectedNotes += 4;
                    Assert.That(Records(window).Count, Is.EqualTo(expectedNotes));
                    AssertEarlierTake(window, earlierTake);

                }

                Render(window);
                ClickAction(window, "applyBuffer");
                guiActions++;
                Assert.That(chart.Notes.Count, Is.EqualTo(expectedNotes), (string)Get(window, "statusMessage"));
                Assert.That(chart.TryValidate(out string error), Is.True, error);
                if (laterStartMode == "CurrentPosition")
                {

                    HashSet<ChartNoteType> types = new();
                    foreach (ChartNote note in chart.Notes)
                    {

                        types.Add(note.NoteType);

                    }

                    Assert.That(types, Is.EquivalentTo(new[]
                    {
                        ChartNoteType.Tap, ChartNoteType.Hold, ChartNoteType.Slide,
                        ChartNoteType.Flick, ChartNoteType.Banana
                    }), "Every interaction kind must come from real FMOD-timed recording key events.");

                }

                Assert.That(EditorUtility.IsDirty(chart), Is.True, "Applying the buffer and saving the asset remain separate actions.");
                Assert.That((bool)Invoke(window, "CanRetryLastTake"), Is.False,
                    "Retry must not silently rewrite a take already applied to chart data.");
                ClickAction(window, "saveChart");
                guiActions++;
                Assert.That(EditorUtility.IsDirty(chart), Is.False);
                string saved = JsonUtility.ToJson(chart);
                AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate);
                PrototypeChart reloaded = AssetDatabase.LoadAssetAtPath<PrototypeChart>(assetPath);
                Assert.That(reloaded.TryValidate(out string reloadError), Is.True, reloadError);
                Assert.That(JsonUtility.ToJson(reloaded), Is.EqualTo(saved));
                Assert.That(JsonUtility.ToJson(source), Is.EqualTo(sourceBefore));
                LogAssert.NoUnexpectedReceived();
                passed = true;

            }
            finally
            {

                elapsed.Stop();
                WriteEvidence(laterStartMode, guiActions, elapsed.Elapsed.TotalMilliseconds, passed);
                if (window != null)
                {

                    Undo.ClearUndo(window);
                    window.Close();
                    if (window != null)
                    {

                        Object.DestroyImmediate(window);

                    }

                }

                playback.Stop();
                Object.Destroy(playbackObject);
                if (chart != null)
                {

                    Undo.ClearUndo(chart);

                }

                // The folder name is unique to this invocation and was created above by the test.
                AssetDatabase.DeleteAsset(folder);
                Application.targetFrameRate = previousFrameRate;
                QualitySettings.vSyncCount = previousVsync;
                Application.runInBackground = previousBackground;
                EditorGUIUtility.editingTextField = previousTextEditing;
                if (session != null)
                {

                    session.enabled = sessionWasEnabled;

                }

            }

        }

        private static IEnumerator WaitForRecording(EditorWindow window, FmodSongPlayback playback)
        {

            double deadline = Time.realtimeSinceStartupAsDouble + 12d;
            while (!(bool)Get(window, "isRecording") && Time.realtimeSinceStartupAsDouble < deadline)
            {

                yield return null;

            }

            Assert.That((bool)Get(window, "isRecording"), Is.True, (string)Get(window, "statusMessage"));
            Assert.That(playback.IsPlaying, Is.True);
            Assert.That(playback.HasReachedScheduledStart, Is.True);
            EditorGUIUtility.editingTextField = false;
            window.Focus();
            Render(window);

        }

        private static IEnumerator RecordOtherInteractionKinds(EditorWindow window, FmodSongPlayback playback)
        {

            // The recording mode is changed only while stopped, matching the real workspace's
            // mode-control rule. Every endpoint below is entered through its IMGUI key path.
            SetEnum(window, "recordingNoteMode", "SlideAndHold");
            yield return StartAtLaterPosition(window, playback, 12d);
            SendKey(window, KeyCode.Alpha2);
            yield return new WaitForSecondsRealtime(0.2f);
            SendKey(window, KeyCode.Alpha2);
            yield return new WaitForSecondsRealtime(0.12f);
            SendKey(window, KeyCode.Alpha3);
            yield return new WaitForSecondsRealtime(0.2f);
            SendKey(window, KeyCode.Alpha4);
            yield return new WaitForSecondsRealtime(0.2f);
            SendKey(window, KeyCode.Alpha4);
            Invoke(window, "StopRecording");
            playback.Stop();

            SetEnum(window, "recordingNoteMode", "Flick");
            yield return StartAtLaterPosition(window, playback, 16d);
            SendKey(window, KeyCode.Alpha2);
            Invoke(window, "StopRecording");
            playback.Stop();

            SetEnum(window, "recordingNoteMode", "Banana");
            yield return StartAtLaterPosition(window, playback, 20d);
            SendKey(window, KeyCode.Alpha2);
            yield return new WaitForSecondsRealtime(0.4f);
            SendKey(window, KeyCode.Alpha6);
            Invoke(window, "StopRecording");
            playback.Stop();

        }

        private static IEnumerator StartAtLaterPosition(EditorWindow window, FmodSongPlayback playback, double target)
        {

            Assert.That(playback.SchedulePlay(0.2d, target, out _, out _), Is.True);
            double deadline = Time.realtimeSinceStartupAsDouble + 5d;
            while ((!playback.HasReachedScheduledStart || playback.SongTime < target) &&
                Time.realtimeSinceStartupAsDouble < deadline)
            {

                yield return null;

            }

            Assert.That(playback.SongTime, Is.GreaterThanOrEqualTo(target));
            StartMode(window, "CurrentPosition");
            yield return WaitForRecording(window, playback);

        }

        private static void StartMode(EditorWindow window, string mode)
        {

            MethodInfo method = window.GetType().GetMethod("StartRecording", PrivateInstance);
            method.Invoke(window, new[] { Enum.Parse(method.GetParameters()[0].ParameterType, mode) });

        }

        private static double LastTakeTarget(EditorWindow window)
        {

            object take = Get(window, "lastRecordingTake");
            Assert.That(take, Is.Not.Null);
            return (double)take.GetType().GetField("targetTime").GetValue(take);

        }

        private static void AssertEarlierTake(EditorWindow window, Dictionary<string, string> expected)
        {

            Dictionary<string, string> current = SnapshotRecords(window);
            foreach (KeyValuePair<string, string> pair in expected)
            {

                Assert.That(current.ContainsKey(pair.Key), Is.True);
                Assert.That(current[pair.Key], Is.EqualTo(pair.Value), "Retry changed a note belonging to an earlier take.");

            }

        }

        private static Dictionary<string, string> SnapshotRecords(EditorWindow window)
        {

            Dictionary<string, string> result = new();
            foreach (object record in Records(window))
            {

                string id = (string)record.GetType().GetField("editorId").GetValue(record);
                Assert.That(id, Is.Not.Empty);
                result.Add(id, JsonUtility.ToJson(record));

            }

            return result;

        }

        private static IList Records(EditorWindow window)
        {

            return (IList)Get(window, "recordedNotes");

        }

        private static void Render(EditorWindow window)
        {

            window.SendEvent(new Event { type = EventType.Layout });
            window.SendEvent(new Event { type = EventType.Repaint });

        }

        private static void SendKey(EditorWindow window, KeyCode key)
        {

            window.SendEvent(new Event { type = EventType.KeyDown, keyCode = key });
            window.SendEvent(new Event { type = EventType.KeyUp, keyCode = key });
            Render(window);

        }

        private static void ClickAction(EditorWindow window, string key)
        {

            Render(window);
            Dictionary<string, Rect> actions = (Dictionary<string, Rect>)Get(window, "workspaceSelectionActionRects");
            Assert.That(actions.ContainsKey(key), Is.True, "No visible control bounds for " + key);
            Vector2 point = actions[key].center;
            Assert.That(new Rect(Vector2.zero, window.position.size).Contains(point), Is.True);
            window.SendEvent(new Event { type = EventType.MouseDown, button = 0, clickCount = 1, mousePosition = point });
            window.SendEvent(new Event { type = EventType.MouseUp, button = 0, clickCount = 1, mousePosition = point });
            Render(window);

        }

        private static object Get(EditorWindow window, string name)
        {

            return window.GetType().GetField(name, PrivateInstance).GetValue(window);

        }

        private static void Set(EditorWindow window, string name, object value)
        {

            window.GetType().GetField(name, PrivateInstance).SetValue(window, value);

        }

        private static void SetEnum(EditorWindow window, string name, string value)
        {

            FieldInfo field = window.GetType().GetField(name, PrivateInstance);
            field.SetValue(window, Enum.Parse(field.FieldType, value));

        }

        private static object Invoke(EditorWindow window, string name)
        {

            return window.GetType().GetMethod(name, PrivateInstance).Invoke(window, null);

        }

        private static void WriteEvidence(string startMode, int guiActions, double milliseconds, bool passed)
        {

            string line = "scenario=fmod-" + startMode + "-tap-take-retry-apply-save-reload\tresult=" + (passed ? "pass" : "failed") +
                "\tmethod=real FMOD; SendEvent recording keys/apply/save; method-level start/stop/retry commands" +
                $"\tinputActions={guiActions}\tinputEvents={guiActions * 2}" +
                $"\tautomationMilliseconds={milliseconds.ToString("F3", CultureInfo.InvariantCulture)}" +
                "\thumanAuthoringSpeed=not-measured\tphysicalTouch=not-tested";
            TestContext.Progress.WriteLine(line);
            string directory = Environment.GetEnvironmentVariable("IDIOT_TAPE_AUTHORING_EVIDENCE");
            if (!string.IsNullOrWhiteSpace(directory))
            {

                Directory.CreateDirectory(directory);
                File.AppendAllText(Path.Combine(directory, "workflow-simulation.tsv"), line + Environment.NewLine);

            }

        }

    }

}
#endif
