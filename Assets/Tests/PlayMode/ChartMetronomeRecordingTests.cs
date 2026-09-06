#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using IdiotTape.Audio;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace IdiotTape.Gameplay.Tests
{

    public sealed class ChartMetronomeRecordingTests
    {

        private const BindingFlags Members = BindingFlags.Instance | BindingFlags.NonPublic;

        [UnityTest]
        public IEnumerator BeginningAndLoopRecordingScheduleOpeningBeatsAcrossFourTakes()
        {

            PrototypeChart source = AssetDatabase.LoadAssetAtPath<PrototypeChart>("Assets/Data/PlutoPrototypeChart.asset");
            Assert.That(source, Is.Not.Null);
            string sourceBefore = JsonUtility.ToJson(source);
            PrototypeChart chart = Object.Instantiate(source);
            chart.hideFlags = HideFlags.DontSave;
            GameObject playbackObject = new("Pluto recording metronome verification");
            playbackObject.AddComponent<FMODUnity.StudioListener>();
            FmodSongPlayback playback = playbackObject.AddComponent<FmodSongPlayback>();
            ScriptableObject window = null;
            int previousFrameRate = Application.targetFrameRate;
            int previousVsync = QualitySettings.vSyncCount;
            bool previousTextEditing = EditorGUIUtility.editingTextField;
            bool previousBackground = Application.runInBackground;
            GameplaySession session = Object.FindAnyObjectByType<GameplaySession>();
            bool sessionWasEnabled = session != null && session.enabled;
            string evidence = Environment.GetEnvironmentVariable("IDIOT_TAPE_AUDIO_EVIDENCE");
            StringBuilder report = new();
            List<string> failures = new();

            try
            {

                Application.targetFrameRate = 60;
                QualitySettings.vSyncCount = 0;
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
                Type windowType = Type.GetType("IdiotTape.EditorTools.PrototypeChartRecorderWindow, IdiotTape.Gameplay.Editor", true);
                window = ScriptableObject.CreateInstance(windowType);
                window.hideFlags = HideFlags.DontSave;
                windowType.GetField("chart", Members).SetValue(window, chart);
                windowType.GetField("songPlayback", Members).SetValue(window, playback);
                windowType.GetField("configuredEventPath", Members).SetValue(window, chart.SongEventPath);
                windowType.GetField("metronomeDuringRecording", Members).SetValue(window, true);
                windowType.GetField("automaticQuantization", Members).SetValue(window, false);
                MethodInfo start = windowType.GetMethod("StartRecording", Members);
                MethodInfo keyboard = windowType.GetMethod("HandleRecorderWindowKeyboardEvent", Members);
                double firstBeat = chart.TempoSections[0].StartTime;
                double secondsPerBeat = chart.TempoSections[0].SecondsPerBeat;
                double endTime = firstBeat + secondsPerBeat * 7d + 0.15d;
                windowType.GetField("loopStart", Members).SetValue(window, firstBeat);
                windowType.GetField("loopEnd", Members).SetValue(window, endTime + 1d);
                Assert.That(endTime, Is.LessThan(7d), "Use an opening section that fits the bounded PCM capture.");

                // Keep one playback/listener and one recorder alive, just as a user
                // does while switching start modes and recording repeated takes.
                for (int take = 0; take < 4; take++)
                {

                    string startModeName = take < 2 ? "Beginning" : "Loop";
                    object startMode = Enum.Parse(start.GetParameters()[0].ParameterType, startModeName);
                    using (FmodOutputCapture capture = string.IsNullOrWhiteSpace(evidence)
                        ? null : new FmodOutputCapture(Path.Combine(evidence, $"pluto-{startModeName}-take-{take + 1}")))
                    {

                        start.Invoke(window, new[] { startMode });
                        ulong anchor = (ulong)typeof(FmodSongPlayback).GetField("anchorDspClock", Members).GetValue(playback);
                        FMODUnity.RuntimeManager.CoreSystem.getSoftwareFormat(out int sampleRate, out _, out _);
                        capture?.SetTimelineReference(anchor, 0d, $"Pluto actual recording take {take + 1}");
                        HashSet<ulong> clocks = new();
                        int inputCount = 0;
                        int recordsBefore = ((IList)windowType.GetField("recordedNotes", Members).GetValue(window)).Count;
                        deadline = Time.realtimeSinceStartupAsDouble + 12d;

                        while ((!playback.HasReachedScheduledStart || playback.SongTime < endTime) &&
                               Time.realtimeSinceStartupAsDouble < deadline)
                        {

                            CollectClickClocks(window, windowType, clocks);
                            bool recording = (bool)windowType.GetField("isRecording", Members).GetValue(window);
                            if (recording && inputCount < 4 && playback.SongTime >= firstBeat + inputCount * secondsPerBeat)
                            {

                                keyboard.Invoke(window, new object[] { new Event { type = EventType.KeyDown, keyCode = KeyCode.Alpha4 } });
                                inputCount++;

                            }

                            yield return null;

                        }

                        CollectClickClocks(window, windowType, clocks);
                        List<double> songClicks = new();
                        foreach (ulong clock in clocks)
                        {

                            double time = ((double)clock - anchor) / sampleRate;
                            report.AppendLine($"take={take + 1} clickSongTime={time:R}");
                            if (time >= 0d && time < endTime)
                            {

                                songClicks.Add(time);

                            }

                        }

                        windowType.GetMethod("StopRecording", Members).Invoke(window, null);
                        playback.Stop();
                        int recordsAfter = ((IList)windowType.GetField("recordedNotes", Members).GetValue(window)).Count;
                        report.AppendLine($"take={take + 1} recordedNotes={recordsAfter - recordsBefore} songClicks={songClicks.Count}");
                        Assert.That(recordsAfter - recordsBefore, Is.EqualTo(4), report.ToString());
                        int firstAudibleBeat = (int)Math.Ceiling(-firstBeat / secondsPerBeat);
                        for (int beat = firstAudibleBeat; beat < 8; beat++)
                        {

                            double expected = firstBeat + beat * secondsPerBeat;
                            if (!songClicks.Exists(time => Math.Abs(time - expected) < 0.04d))
                            {

                                failures.Add($"Take {take + 1} missing beat {beat + 1} at {expected:R}s");

                            }

                        }

                        if (capture != null)
                        {

                            Assert.That(capture.HasFault, Is.False);
                            Assert.That(capture.CapturedFrames, Is.GreaterThan(0));

                        }

                    }

                    yield return new WaitForSecondsRealtime(0.2f);

                }

                Assert.That(JsonUtility.ToJson(source), Is.EqualTo(sourceBefore));
                Assert.That(chart.Notes.Count, Is.EqualTo(source.Notes.Count), "Recording must stay in the temporary buffer.");
                Assert.That(failures, Is.Empty, string.Join("\n", failures) + "\n" + report);
                LogAssert.NoUnexpectedReceived();

            }
            finally
            {

                if (!string.IsNullOrWhiteSpace(evidence))
                {

                    Directory.CreateDirectory(evidence);
                    File.WriteAllText(Path.Combine(evidence, "recording-clicks.txt"), report.ToString());

                }

                if (window != null)
                {

                    Undo.ClearUndo(window);
                    Object.DestroyImmediate(window);

                }

                playback.Stop();
                Object.Destroy(playbackObject);
                Object.DestroyImmediate(chart);
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

        private static void CollectClickClocks(ScriptableObject window, Type windowType, HashSet<ulong> clocks)
        {

            object authoring = windowType.GetField("metronome", Members).GetValue(window);
            if (authoring == null)
            {

                return;

            }

            object runtime = authoring.GetType().GetField("metronome", Members).GetValue(authoring);
            IList clicks = (IList)typeof(FmodMetronome).GetField("scheduledClicks", Members).GetValue(runtime);
            foreach (object click in clicks)
            {

                FMOD.Channel channel = (FMOD.Channel)click.GetType().GetField("Channel").GetValue(click);
                if (channel.getDelay(out ulong start, out _) == FMOD.RESULT.OK)
                {

                    clocks.Add(start);

                }

            }

        }

    }

}
#endif
