#if UNITY_EDITOR
using System;
using System.Collections;
using System.Reflection;
using System.Text;
using IdiotTape.Audio;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace IdiotTape.Gameplay.Tests
{

    public sealed class ChartRecordingPlaybackTests
    {

        [UnityTest]
        public IEnumerator EightLaneFourKeyEventsAcrossFramesRecordFourSynthHoldsAtSongTime()
        {

            const BindingFlags members = BindingFlags.Instance | BindingFlags.NonPublic;
            const string eventPath = "event:/Music/KIRARA/Snow";
            GameObject playbackObject = new("Repeated Hold Recording Test");
            playbackObject.AddComponent(Type.GetType("FMODUnity.StudioListener, FMODUnity", true));
            FmodSongPlayback playback = playbackObject.AddComponent<FmodSongPlayback>();
            PrototypeChart chart = ScriptableObject.CreateInstance<PrototypeChart>();
            chart.hideFlags = HideFlags.DontSave;
            ScriptableObject window = null;
            bool previousTextEditing = EditorGUIUtility.editingTextField;
            int previousTargetFrameRate = Application.targetFrameRate;
            int previousVSyncCount = QualitySettings.vSyncCount;
            GameplaySession session = Object.FindAnyObjectByType<GameplaySession>();
            bool sessionWasEnabled = session != null && session.enabled;

            try
            {

                // Batch mode has no display pacing. Bound this real-time playback test so thousands
                // of editor frames per second cannot flood FMOD's asynchronous command buffer.
                QualitySettings.vSyncCount = 0;
                Application.targetFrameRate = 60;

                if (session != null)
                {

                    session.enabled = false;

                }

                ConfigureChart(chart, eventPath);
                playback.ConfigureEventPath(eventPath, false);
                playback.Prepare();
                float deadline = Time.realtimeSinceStartup + 15f;

                while (!playback.IsPrepared && !playback.PreparationFailed && Time.realtimeSinceStartup < deadline)
                {

                    yield return null;

                }

                Assert.That(playback.IsPrepared, Is.True, "FMOD must prepare before testing timestamped recording.");
                Type windowType = Type.GetType(
                    "IdiotTape.EditorTools.PrototypeChartRecorderWindow, IdiotTape.Gameplay.Editor", true);
                window = ScriptableObject.CreateInstance(windowType);
                window.hideFlags = HideFlags.DontSave;
                windowType.GetField("chart", members).SetValue(window, chart);
                windowType.GetField("songPlayback", members).SetValue(window, playback);
                windowType.GetField("configuredEventPath", members).SetValue(window, eventPath);
                windowType.GetField("selectedPartIndex", members).SetValue(window, 1);
                windowType.GetField("timelineAutoScroll", members).SetValue(window, false);
                windowType.GetField("metronomeDuringRecording", members).SetValue(window, false);
                FieldInfo mode = windowType.GetField("recordingNoteMode", members);
                mode.SetValue(window, Enum.Parse(mode.FieldType, "SlideAndHold"));
                MethodInfo keyboard = windowType.GetMethod("HandleRecorderWindowKeyboardEvent", members);
                Assert.That(playback.SchedulePlay(0.1d, 16d, out _, out _), Is.True,
                    "The real FMOD clock must schedule playback at the ninth bar.");
                deadline = Time.realtimeSinceStartup + 5f;

                while ((!playback.HasReachedScheduledStart || playback.SongTime < 16d || !playback.IsPlaying) &&
                    Time.realtimeSinceStartup < deadline)
                {

                    yield return null;

                }

                Assert.That(playback.IsRunning, Is.True);
                Assert.That(playback.IsPlaying, Is.True);
                Assert.That(playback.HasReachedScheduledStart, Is.True);
                Assert.That(playback.SongTime, Is.GreaterThanOrEqualTo(16d));
                windowType.GetMethod("ActivateRecording", members).Invoke(window, null);
                EditorGUIUtility.editingTextField = false;
                double[] beforeInput = new double[8];
                double[] afterInput = new double[8];
                StringBuilder inputDiagnostics = new();

                for (int input = 0; input < 8; input++)
                {

                    if (input > 0 && input % 2 == 0)
                    {

                        // End and next-start are separate physical presses. Allow a real frame and
                        // the recorder's 10 ms duplicate-input interval before beginning the next hold.
                        yield return new WaitForSecondsRealtime(0.025f);

                    }

                    // Each end key follows two seconds of real authoritative song-clock progression,
                    // rather than a synthetic timestamp or an immediately-read asynchronous seek.
                    double targetTime = input % 2 == 0 ? playback.SongTime : beforeInput[input - 1] + 2d;
                    deadline = Time.realtimeSinceStartup + 5f;

                    while (playback.SongTime < targetTime && Time.realtimeSinceStartup < deadline)
                    {

                        yield return null;

                    }

                    Assert.That(playback.IsRunning && playback.IsPlaying, Is.True, inputDiagnostics.ToString());
                    Assert.That(playback.SongTime, Is.GreaterThanOrEqualTo(targetTime), inputDiagnostics.ToString());
                    beforeInput[input] = playback.SongTime;
                    double eventTimestamp = InputState.currentTime;
                    double previousTimestamp = ((double[])windowType.GetField(
                        "lastRecordedInputTimestamps", members).GetValue(window))[3];
                    IList currentRecords = (IList)windowType.GetField("recordedNotes", members).GetValue(window);
                    int previousCount = currentRecords.Count;
                    Event key = new() { type = EventType.KeyDown, keyCode = KeyCode.Alpha4 };
                    keyboard.Invoke(window, new object[] { key });
                    afterInput[input] = playback.SongTime;
                    Assert.That(key.type, Is.EqualTo(EventType.Used));
                    Undo.FlushUndoRecordObjects();
                    currentRecords = (IList)windowType.GetField("recordedNotes", members).GetValue(window);
                    object pending = windowType.GetField("pendingInteraction", members).GetValue(window);
                    string pendingDescription = pending == null ? "null" : JsonUtility.ToJson(pending);
                    string diagnostic = $"Input {input + 1}: frame={Time.frameCount}, target={targetTime:R}, " +
                        $"FMOD before={beforeInput[input]:R}, after={afterInput[input]:R}, " +
                        $"timeline={playback.PlaybackPositionSeconds:R}, inputTime={eventTimestamp:R}, " +
                        $"previousGate={previousTimestamp:R}, inputDelta={eventTimestamp - previousTimestamp:R}, " +
                        $"count={previousCount}->{currentRecords.Count}, pending={pendingDescription}, " +
                        $"phase={windowType.GetField("recordingPhase", members).GetValue(window)}, " +
                        $"status={windowType.GetField("statusMessage", members).GetValue(window)}";
                    inputDiagnostics.AppendLine(diagnostic);
                    Assert.That(currentRecords.Count, Is.EqualTo((input + 1) / 2), inputDiagnostics.ToString());
                    Assert.That(pending != null, Is.EqualTo(input % 2 == 0), inputDiagnostics.ToString());

                }

                yield return null;
                windowType.GetMethod("StopRecording", members).Invoke(window, null);
                IList records = (IList)windowType.GetField("recordedNotes", members).GetValue(window);
                Assert.That(records.Count, Is.EqualTo(4), inputDiagnostics.ToString());

                for (int index = 0; index < records.Count; index++)
                {

                    object record = records[index];
                    Type recordType = record.GetType();
                    object data = recordType.GetField("noteData").GetValue(record);
                    Type dataType = data.GetType();
                    double start = (double)dataType.GetField("HitTime").GetValue(data);
                    double end = (double)dataType.GetField("EndTime").GetValue(data);
                    Assert.That(dataType.GetField("NoteType").GetValue(data), Is.EqualTo(ChartNoteType.Hold));
                    Assert.That(recordType.GetField("musicalPartId").GetValue(record), Is.EqualTo("synth"));
                    Assert.That(dataType.GetField("MusicalPartId").GetValue(data), Is.EqualTo("synth"));
                    Assert.That(recordType.GetField("laneIndex").GetValue(record), Is.EqualTo(3));
                    Assert.That(dataType.GetField("LaneIndex").GetValue(data), Is.EqualTo(3));
                    Assert.That(start, Is.InRange(beforeInput[index * 2] - 0.001d, afterInput[index * 2] + 0.001d),
                        inputDiagnostics.ToString());
                    Assert.That(end, Is.InRange(beforeInput[index * 2 + 1] - 0.001d, afterInput[index * 2 + 1] + 0.001d),
                        inputDiagnostics.ToString());
                    Assert.That(end - start, Is.EqualTo(2d).Within(0.08d),
                        "At 120 BPM, each hold spans four beats.\n" + inputDiagnostics);

                }

                Assert.That(windowType.GetField("pendingInteraction", members).GetValue(window), Is.Null);
                Assert.That(chart.Notes.Count, Is.Zero, "Recording must not apply notes to the chart.");
                LogAssert.NoUnexpectedReceived();

            }
            finally
            {

                Application.targetFrameRate = previousTargetFrameRate;
                QualitySettings.vSyncCount = previousVSyncCount;
                EditorGUIUtility.editingTextField = previousTextEditing;

                if (window != null)
                {

                    Undo.ClearUndo(window);
                    Object.DestroyImmediate(window);

                }

                playback.Stop();
                Object.Destroy(playbackObject);
                Object.DestroyImmediate(chart);

                if (session != null)
                {

                    session.enabled = sessionWasEnabled;

                }

            }

            yield return null;

        }

        private static void ConfigureChart(PrototypeChart chart, string eventPath)
        {

            SerializedObject serialized = new(chart);
            serialized.FindProperty("songEventPath").stringValue = eventPath;
            serialized.FindProperty("laneCount").intValue = 8;
            SerializedProperty parts = serialized.FindProperty("musicalParts");
            parts.arraySize = 2;
            string[] ids = { "drum", "synth" };

            for (int index = 0; index < parts.arraySize; index++)
            {

                SerializedProperty part = parts.GetArrayElementAtIndex(index);
                part.FindPropertyRelative("id").stringValue = ids[index];
                part.FindPropertyRelative("displayName").stringValue = ids[index];

            }

            SerializedProperty tempos = serialized.FindProperty("tempoSections");
            tempos.arraySize = 1;
            SerializedProperty tempo = tempos.GetArrayElementAtIndex(0);
            tempo.FindPropertyRelative("startBar").intValue = 1;
            tempo.FindPropertyRelative("startTime").doubleValue = 0d;
            tempo.FindPropertyRelative("beatsPerMinute").doubleValue = 120d;
            tempo.FindPropertyRelative("beatsPerBar").intValue = 4;
            tempo.FindPropertyRelative("beatUnit").intValue = 4;
            serialized.ApplyModifiedPropertiesWithoutUndo();

        }

    }

}
#endif
