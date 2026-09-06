#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using FMOD;
using FMOD.Studio;
using FMODUnity;
using IdiotTape.Audio;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace IdiotTape.Gameplay.Tests
{

    public sealed class FmodSeekVerificationTests
    {

        private const BindingFlags Members = BindingFlags.Instance | BindingFlags.NonPublic;
        // A deliberately loose diagnostic bound detects transport errors, not device latency.
        private const double TransportTolerance = 0.25d;
        private readonly List<string> failures = new();
        private GameObject playbackObject;
        private FmodSongPlayback playback;
        private EventInstance instance;
        private int previousFrameRate;
        private int previousVSync;
        private bool previousBackground;

        [UnitySetUp]
        public IEnumerator Prepare()
        {

            previousFrameRate = Application.targetFrameRate;
            previousVSync = QualitySettings.vSyncCount;
            previousBackground = Application.runInBackground;
            Application.targetFrameRate = 60;
            QualitySettings.vSyncCount = 0;
            Application.runInBackground = true;
            failures.Clear();
            playbackObject = new GameObject("Seek Verification");
            playbackObject.AddComponent<StudioListener>();
            playback = playbackObject.AddComponent<FmodSongPlayback>();
            playback.ConfigureEventPath("event:/Music/KIRARA/Snow", false);
            playback.Prepare();
            double deadline = Time.realtimeSinceStartupAsDouble + 15d;

            while (!playback.IsPrepared && !playback.PreparationFailed &&
                Time.realtimeSinceStartupAsDouble < deadline)
            {

                yield return null;

            }

            Assert.That(playback.IsPrepared, Is.True);
            instance = (EventInstance)typeof(FmodSongPlayback).GetField("songInstance", Members).GetValue(playback);
            RuntimeManager.CoreSystem.getSoftwareFormat(out int rate, out _, out _);
            RuntimeManager.CoreSystem.getDSPBufferSize(out uint frames, out int buffers);
            TestContext.WriteLine($"Unity={Application.unityVersion}; outputRate={rate}; buffer={frames}x{buffers}; duration={playback.DurationSeconds:R}");

        }

        [UnityTearDown]
        public IEnumerator Cleanup()
        {

            if (playback != null)
            {

                playback.Stop();

            }

            Object.Destroy(playbackObject);
            Application.targetFrameRate = previousFrameRate;
            QualitySettings.vSyncCount = previousVSync;
            Application.runInBackground = previousBackground;
            yield return null;

        }

        [UnityTest, Explicit("IT-P0-003 known defect: select this diagnostic explicitly until Seek is repaired.")]
        public IEnumerator LiveForwardAndBackwardSeekPreserveTransportAgreement()
        {

            double[] starts = { 0d, playback.DurationSeconds * 0.6d, 30d };
            double[] targets = { playback.DurationSeconds * 0.6d, 30d, playback.DurationSeconds * 0.9d };

            for (int index = 0; index < targets.Length; index++)
            {

                yield return StartAt(starts[index]);
                string label = $"live-{index}";
                Snapshot(label + "-before", starts[index], false);
                double requestedAt = Time.realtimeSinceStartupAsDouble;
                Assert.That(playback.Seek(targets[index]), Is.True);
                Snapshot(label + "-immediate", targets[index], false);
                yield return ObserveRunning(label, targets[index], requestedAt);

            }

            Assert.That(failures, Is.Empty, string.Join("\n", failures));

        }

        [UnityTest, Explicit("IT-P0-003 known defect: select this diagnostic explicitly until Seek is repaired.")]
        public IEnumerator PausedSeekMovesTheFrozenClockAndResumesFromTheTarget()
        {

            yield return StartAt(playback.DurationSeconds * 0.6d);
            playback.Pause();
            Assert.That(playback.IsPaused, Is.True);
            double frozen = playback.SongTime;
            yield return new WaitForSecondsRealtime(0.5f);
            Assert.That(playback.SongTime, Is.EqualTo(frozen).Within(0.001d), "Pause itself must freeze the clock.");
            Snapshot("paused-before", frozen, false);
            Assert.That(playback.Seek(30d), Is.True);
            Snapshot("paused-immediate", 30d, false);
            yield return new WaitForSecondsRealtime(0.5f);
            Snapshot("paused-settled", 30d, true);
            Assert.That(playback.IsPaused, Is.True, "Seek must preserve pause state.");
            playback.Resume();
            double resumedAt = Time.realtimeSinceStartupAsDouble;
            Assert.That(playback.IsPaused, Is.False);
            yield return ObserveRunning("resumed-after-seek", 30d, resumedAt);
            Assert.That(failures, Is.Empty, string.Join("\n", failures));

        }

        [UnityTest, Explicit("IT-P0-003 known defect: select this diagnostic explicitly until Seek is repaired.")]
        public IEnumerator AuthoringLoopReentrySeeksToItsPrerollOnRepeatedCycles()
        {

            Type windowType = Type.GetType("IdiotTape.EditorTools.PrototypeChartRecorderWindow, IdiotTape.Gameplay.Editor", true);
            ScriptableObject window = ScriptableObject.CreateInstance(windowType);
            PrototypeChart source = AssetDatabase.LoadAssetAtPath<PrototypeChart>("Assets/Data/SnowPrototypeChart.asset");
            Assert.That(source, Is.Not.Null);
            PrototypeChart chart = Object.Instantiate(source);
            chart.hideFlags = HideFlags.DontSave;
            string before = JsonUtility.ToJson(source);

            try
            {

                window.hideFlags = HideFlags.DontSave;
                // Drive the real loop transition at controlled boundaries. Automatic EditorUpdate
                // would otherwise alter the observation interval independently of the fixture.
                EditorApplication.update -= (EditorApplication.CallbackFunction)Delegate.CreateDelegate(
                    typeof(EditorApplication.CallbackFunction), window, windowType.GetMethod("EditorUpdate", Members));
                windowType.GetField("chart", Members).SetValue(window, chart);
                windowType.GetField("songPlayback", Members).SetValue(window, playback);
                windowType.GetField("configuredEventPath", Members).SetValue(window, "event:/Music/KIRARA/Snow");
                windowType.GetField("preRollStartTime", Members).SetValue(window, 28d);
                windowType.GetField("recordingTargetTime", Members).SetValue(window, 30d);
                windowType.GetField("metronomeDuringRecording", Members).SetValue(window, false);
                MethodInfo restartLoop = windowType.GetMethod("RestartLoopCycle", Members);
                yield return StartAt(32d);

                for (int cycle = 0; cycle < 2; cycle++)
                {

                    double requestedAt = Time.realtimeSinceStartupAsDouble;
                    restartLoop.Invoke(window, null);
                    Snapshot($"loop-{cycle}-immediate", 28d, false);
                    yield return ObserveRunning($"loop-{cycle}", 28d, requestedAt);

                }

                Assert.That(JsonUtility.ToJson(source), Is.EqualTo(before), "The source chart must not change.");
                Assert.That(JsonUtility.ToJson(chart), Is.EqualTo(before), "Transport must not change note data.");
                Assert.That(failures, Is.Empty, string.Join("\n", failures));

            }
            finally
            {

                Object.DestroyImmediate(window);
                Object.DestroyImmediate(chart);

            }

        }

        private IEnumerator StartAt(double target)
        {

            playback.Stop();

            if (target == 0d)
            {

                playback.Restart();

            }
            else
            {

                Assert.That(playback.SchedulePlay(0.2d, target, out _, out _), Is.True);

            }

            double deadline = Time.realtimeSinceStartupAsDouble + 8d;

            while (((target != 0d && !playback.HasReachedScheduledStart) || playback.SongTime < target + 0.5d) &&
                Time.realtimeSinceStartupAsDouble < deadline)
            {

                yield return null;

            }

            Assert.That(playback.IsPlaying, Is.True);
            Assert.That(playback.SongTime, Is.InRange(target + 0.5d, target + 1d));

        }

        private IEnumerator ObserveRunning(string label, double target, double requestedAt)
        {

            foreach (double elapsed in new[] { 0.05d, 0.5d, 1d, 5d })
            {

                while (Time.realtimeSinceStartupAsDouble - requestedAt < elapsed)
                {

                    yield return null;

                }

                double actualElapsed = Time.realtimeSinceStartupAsDouble - requestedAt;
                Snapshot($"{label}+{actualElapsed:F3}s", target + actualElapsed, elapsed >= 0.5d);

            }

        }

        private void Snapshot(string label, double expected, bool verify)
        {

            List<double> channelTimes = new();
            List<string> channels = new();
            double songTime;
            double studioTime;
            Assert.That(RuntimeManager.CoreSystem.lockDSP(), Is.EqualTo(RESULT.OK));

            try
            {

                songTime = playback.SongTime;
                studioTime = playback.PlaybackPositionSeconds;
                Assert.That(instance.getChannelGroup(out ChannelGroup group), Is.EqualTo(RESULT.OK));
                CollectChannels(group, channelTimes, channels);

            }
            finally
            {

                RuntimeManager.CoreSystem.unlockDSP();

            }

            TestContext.WriteLine($"{label}: expected={expected:F6}, song={songTime:F6}, studio={studioTime:F6}, " +
                $"anchor={(double)typeof(FmodSongPlayback).GetField("anchorSongTime", Members).GetValue(playback):F6}, " +
                $"paused={playback.IsPaused}\n" + string.Join("\n", channels));

            if (!verify)
            {

                return;

            }

            CheckAgreement(label + " song vs target", songTime, expected);
            CheckAgreement(label + " studio vs target", studioTime, expected);

            // Paused native voices may retain their previous cursor until resumed. The frozen
            // public clock and Studio target must update now; source agreement is checked after Resume.
            if (playback.IsPaused)
            {

                return;

            }

            if (channelTimes.Count == 0)
            {

                failures.Add(label + ": no native song channels observed");

            }

            foreach (double channelTime in channelTimes)
            {

                // Native source cursors expose transport location, not speaker presentation time.
                CheckAgreement(label + " native vs song", channelTime, songTime);

            }

        }

        private void CheckAgreement(string label, double actual, double expected)
        {

            if (Math.Abs(actual - expected) > TransportTolerance)
            {

                failures.Add($"{label}: actual={actual:F6}, expected={expected:F6}, difference={actual - expected:F6}s");

            }

        }

        private static void CollectChannels(ChannelGroup group, List<double> times, List<string> rows)
        {

            Assert.That(group.getNumChannels(out int count), Is.EqualTo(RESULT.OK));

            for (int index = 0; index < count; index++)
            {

                Assert.That(group.getChannel(index, out Channel channel), Is.EqualTo(RESULT.OK));
                Assert.That(channel.getPosition(out uint milliseconds, TIMEUNIT.MS), Is.EqualTo(RESULT.OK));
                Assert.That(channel.getCurrentSound(out Sound sound), Is.EqualTo(RESULT.OK));
                Assert.That(sound.getName(out string name, 256), Is.EqualTo(RESULT.OK));
                times.Add(milliseconds / 1000d);
                rows.Add($"{name}: native={milliseconds / 1000d:F6}");

            }

            Assert.That(group.getNumGroups(out int groups), Is.EqualTo(RESULT.OK));

            for (int index = 0; index < groups; index++)
            {

                Assert.That(group.getGroup(index, out ChannelGroup child), Is.EqualTo(RESULT.OK));
                CollectChannels(child, times, rows);

            }

        }

    }

}
#endif
