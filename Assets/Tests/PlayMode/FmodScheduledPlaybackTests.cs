#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using FMOD;
using FMOD.Studio;
using FMODUnity;
using IdiotTape.Audio;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace IdiotTape.Gameplay.Tests
{

    public sealed class FmodScheduledPlaybackTests
    {

        [UnityTest]
        public IEnumerator ScheduledMusicPreservesTheAuthoringPlaybackTimebase()
        {

            GameObject playbackObject = new("Scheduled PCM Verification");
            playbackObject.AddComponent<StudioListener>();
            FmodSongPlayback playback = playbackObject.AddComponent<FmodSongPlayback>();
            int previousFrameRate = Application.targetFrameRate;
            bool previousBackground = Application.runInBackground;
            List<double> errors = new();

            try
            {

                Application.targetFrameRate = 60;
                Application.runInBackground = true;
                playback.ConfigureEventPath("event:/Music/KIRARA/Snow", false);
                playback.Prepare();
                float deadline = Time.realtimeSinceStartup + 15f;

                while (!playback.IsPrepared && !playback.PreparationFailed && Time.realtimeSinceStartup < deadline)
                {

                    yield return null;

                }

                Assert.That(playback.IsPrepared, Is.True);
                string evidenceDirectory = Environment.GetEnvironmentVariable("IDIOT_TAPE_AUDIO_EVIDENCE");
                bool captureOutput = !string.IsNullOrWhiteSpace(evidenceDirectory);

                if (captureOutput)
                {

                    playback.SetStemVolume("bass", 0f);
                    playback.SetStemVolume("synth", 0f);
                    playback.SetStemVolume("etc", 0f);

                }

                EventInstance instance = (EventInstance)typeof(FmodSongPlayback)
                    .GetField("songInstance", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(playback);
                List<double> baselineErrors = new();

                // Existing charts and live recording use Play/Restart's timeline origin.
                // Compare actual native channel phase against that path: matching the new
                // schedule's cursor alone missed the original start regression.
                using (FmodOutputCapture baselineCapture = captureOutput
                    ? new FmodOutputCapture(Path.Combine(evidenceDirectory, "immediate-restart"))
                    : null)
                {

                    playback.Restart();
                    ulong baselineAnchor = (ulong)typeof(FmodSongPlayback).GetField("anchorDspClock",
                        BindingFlags.Instance | BindingFlags.NonPublic).GetValue(playback);
                    baselineCapture?.SetTimelineReference(baselineAnchor, 0d, "Existing immediate Restart");
                    yield return new WaitForSecondsRealtime(captureOutput ? 3.2f : 1.1f);
                    Assert.That(instance.getChannelGroup(out ChannelGroup baselineGroup), Is.EqualTo(RESULT.OK));
                    List<string> baselineRows = new();
                    RuntimeManager.CoreSystem.lockDSP();

                    try
                    {

                        CollectChannelPositions(baselineGroup, playback.TimelineTime, baselineRows, baselineErrors);

                    }
                    finally
                    {

                        RuntimeManager.CoreSystem.unlockDSP();

                    }

                    TestContext.WriteLine("Existing authoring/Restart phase:\n" + string.Join("\n", baselineRows));
                    Assert.That(baselineErrors, Is.Not.Empty);

                    if (baselineCapture != null)
                    {

                        Assert.That(baselineCapture.HasFault, Is.False);

                    }

                }

                double baselinePhase = 0d;

                foreach (double phase in baselineErrors)
                {

                    baselinePhase += phase / baselineErrors.Count;

                }

                RuntimeManager.CoreSystem.getDSPBufferSize(out uint bufferFrames, out _);
                RuntimeManager.CoreSystem.getSoftwareFormat(out int outputRate, out _, out _);
                // The immediate API captures after an asynchronous Studio flush. Allow its
                // bounded buffer sampling uncertainty, never an entire native streaming delay.
                double phaseTolerance = bufferFrames * 4d / outputRate + 0.002d;
                double[] targets = { 0d, 0d, playback.DurationSeconds * 0.6d, playback.DurationSeconds * 0.9d, 0d };
                int attempt = 0;

                foreach (double target in targets)
                {

                    using FmodOutputCapture capture = captureOutput
                        ? new FmodOutputCapture(Path.Combine(evidenceDirectory, $"scheduled-{attempt}"))
                        : null;
                    using FmodMetronome referenceClick = captureOutput ? new FmodMetronome() : null;
                    double delay = attempt >= 3 ? 0.1d : attempt == 1 ? 3.8d : 2d;
                    attempt++;
                    Assert.That(playback.SchedulePlay(delay, target, out ulong audioStart, out int sampleRate), Is.True);
                    capture?.SetTimelineReference(audioStart, target, $"delay={delay:R}, target={target:R}");
                    if (delay >= 2d)
                    {

                        referenceClick?.ScheduleAtDspClock(audioStart - (ulong)sampleRate, true, 0.1f);

                    }

                    foreach (double elapsed in new[] { 0.3d, 1d, 3d })
                    {

                        deadline = Time.realtimeSinceStartup + 7f;

                        while (playback.TimelineTime < target + elapsed && Time.realtimeSinceStartup < deadline)
                        {

                            yield return null;

                        }

                        Assert.That(playback.TimelineTime, Is.GreaterThanOrEqualTo(target + elapsed));
                        Assert.That(instance.getChannelGroup(out ChannelGroup group), Is.EqualTo(RESULT.OK));
                        List<string> rows = new();
                        double chartTime;
                        double studioTime;
                        RuntimeManager.CoreSystem.lockDSP();

                        try
                        {

                            chartTime = playback.TimelineTime;
                            studioTime = playback.PlaybackPositionSeconds;
                            CollectChannelPositions(group, chartTime, rows, errors);

                        }
                        finally
                        {

                            RuntimeManager.CoreSystem.unlockDSP();

                        }

                        TestContext.WriteLine($"target={target:F6} chart={chartTime:F6} studio={studioTime:F6}\n" +
                            string.Join("\n", rows));
                        Assert.That(rows, Is.Not.Empty, "The assertion must observe actual song channels.");

                    }

                    if (capture != null)
                    {

                        Assert.That(capture.HasFault, Is.False);
                        Assert.That(capture.CapturedFrames, Is.GreaterThan(0));

                    }

                }

                Assert.That(errors, Is.Not.Empty);

                foreach (double phase in errors)
                {

                    Assert.That(phase, Is.EqualTo(baselinePhase).Within(phaseTolerance),
                        "Count-in, different preparation lengths, restart and nonzero targets must preserve the existing authoring timebase.");

                }

            }
            finally
            {

                playback.Stop();
                Object.Destroy(playbackObject);
                Application.targetFrameRate = previousFrameRate;
                Application.runInBackground = previousBackground;

            }

            yield return null;

        }

        private static void CollectChannelPositions(ChannelGroup group, double chartTime,
            List<string> rows, List<double> errors)
        {

            group.getNumChannels(out int channelCount);

            for (int index = 0; index < channelCount; index++)
            {

                group.getChannel(index, out Channel channel);
                channel.getCurrentSound(out Sound sound);
                sound.getName(out string name, 256);
                channel.getPosition(out uint milliseconds, TIMEUNIT.MS);
                channel.getDSPClock(out ulong clock, out ulong parent);
                channel.getDelay(out ulong start, out _, out _);
                channel.isVirtual(out bool virtualChannel);
                double error = milliseconds / 1000d - chartTime;
                rows.Add($"{name}: PCM={milliseconds / 1000d:F6} error={error:F6} clock={clock} parent={parent} delay={start} virtual={virtualChannel}");
                errors.Add(error);

            }

            group.getNumGroups(out int groupCount);

            for (int index = 0; index < groupCount; index++)
            {

                group.getGroup(index, out ChannelGroup child);
                CollectChannelPositions(child, chartTime, rows, errors);

            }

        }

    }

}
#endif
