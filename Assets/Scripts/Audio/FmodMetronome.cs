using System;
using System.Collections.Generic;
using FMODUnity;
using UnityEngine;

namespace IdiotTape.Audio
{

    public sealed class FmodMetronome : IDisposable
    {

        public const double MinimumScheduleLeadSeconds = 0.025d;

        private const float ClickDurationSeconds = 0.012f;
        private const float RegularFrequency = 1500f;
        private const float AccentFrequency = 2400f;
        private const float OutputVolumeMultiplier = 2f;

        private sealed class ScheduledClick
        {

            public FMOD.Channel Channel;
            public FMOD.DSP Dsp;

        }

        private readonly List<ScheduledClick> scheduledClicks = new();
        private FMOD.System coreSystem;
        private FMOD.ChannelGroup masterChannelGroup;
        private int sampleRate;
        private bool isInitialized;

        public bool IsInitialized => isInitialized;

        public void Initialize()
        {

            if (isInitialized)
            {

                return;

            }

            coreSystem = RuntimeManager.CoreSystem;
            CheckResult(
                coreSystem.getMasterChannelGroup(out masterChannelGroup),
                "get the FMOD master channel group");
            CheckResult(
                coreSystem.getSoftwareFormat(out sampleRate, out _, out _),
                "read the FMOD software format");
            isInitialized = true;

        }

        public bool Schedule(double delaySeconds, bool accent, float volume)
        {

            if (delaySeconds < MinimumScheduleLeadSeconds)
            {

                return false;

            }

            Initialize();
            CheckResult(
                masterChannelGroup.getDSPClock(out ulong currentClock, out _),
                "read the FMOD DSP clock");
            return ScheduleAtClock(
                currentClock + SecondsToSamples(delaySeconds),
                accent,
                volume);

        }

        public bool ScheduleAtDspClock(ulong startClock, bool accent, float volume)
        {

            Initialize();
            CheckResult(
                masterChannelGroup.getDSPClock(out ulong currentClock, out _),
                "read the FMOD DSP clock");

            if (startClock < currentClock + SecondsToSamples(MinimumScheduleLeadSeconds))
            {

                return false;

            }

            return ScheduleAtClock(startClock, accent, volume);

        }

        private bool ScheduleAtClock(ulong startClock, bool accent, float volume)
        {

            ReleaseFinishedClicks();
            FMOD.DSP dsp = default;
            FMOD.Channel channel = default;

            try
            {

                CheckResult(
                    coreSystem.createDSPByType(FMOD.DSP_TYPE.OSCILLATOR, out dsp),
                    "create a metronome oscillator");
                CheckResult(
                    dsp.setParameterInt((int)FMOD.DSP_OSCILLATOR.TYPE, 0),
                    "select the metronome waveform");
                CheckResult(
                    dsp.setParameterFloat(
                        (int)FMOD.DSP_OSCILLATOR.RATE,
                        accent ? AccentFrequency : RegularFrequency),
                    "set the metronome frequency");
                CheckResult(
                    coreSystem.playDSP(dsp, masterChannelGroup, true, out channel),
                    "create a metronome channel");
                float outputVolume = Mathf.Clamp01(volume * OutputVolumeMultiplier);
                CheckResult(
                    channel.setVolume(outputVolume),
                    "set the metronome volume");

                ulong endClock = startClock + SecondsToSamples(ClickDurationSeconds);
                AddEnvelopeFadePoint(channel, startClock, 1f);
                AddEnvelopeFadePoint(channel, startClock + SecondsToSamples(0.002d), 0.65f);
                AddEnvelopeFadePoint(channel, startClock + SecondsToSamples(0.005d), 0.25f);
                AddEnvelopeFadePoint(channel, endClock, 0f);
                CheckResult(
                    channel.setDelay(startClock, endClock, true),
                    "schedule the metronome click");
                CheckResult(
                    channel.setPaused(false),
                    "start the metronome channel");
                scheduledClicks.Add(new ScheduledClick
                {

                    Channel = channel,
                    Dsp = dsp

                });
                return true;

            }
            catch
            {

                if (channel.hasHandle())
                {

                    channel.stop();

                }

                if (dsp.hasHandle())
                {

                    dsp.release();

                }

                throw;

            }

        }

        public static bool TryGetScheduleDelay(
            double targetSongTime,
            double currentSongTime,
            out double delaySeconds)
        {

            delaySeconds = targetSongTime - currentSongTime;
            return delaySeconds >= MinimumScheduleLeadSeconds;

        }

        public void StopAll()
        {

            for (int index = 0; index < scheduledClicks.Count; index++)
            {

                ScheduledClick click = scheduledClicks[index];

                if (click.Channel.hasHandle())
                {

                    click.Channel.stop();

                }

                if (click.Dsp.hasHandle())
                {

                    click.Dsp.release();

                }

            }

            scheduledClicks.Clear();

        }

        public void Dispose()
        {

            StopAll();
            coreSystem = default;
            masterChannelGroup = default;
            sampleRate = 0;
            isInitialized = false;

        }

        private void ReleaseFinishedClicks()
        {

            for (int index = scheduledClicks.Count - 1; index >= 0; index--)
            {

                ScheduledClick click = scheduledClicks[index];
                FMOD.RESULT result = click.Channel.isPlaying(out bool isPlaying);

                if (result == FMOD.RESULT.OK && isPlaying)
                {

                    continue;

                }

                if (click.Dsp.hasHandle())
                {

                    click.Dsp.release();

                }

                scheduledClicks.RemoveAt(index);

            }

        }

        private ulong SecondsToSamples(double seconds)
        {

            return (ulong)Math.Ceiling(seconds * sampleRate);

        }

        private static void AddEnvelopeFadePoint(FMOD.Channel channel, ulong dspClock, float volume)
        {

            CheckResult(
                channel.addFadePoint(dspClock, volume),
                "shape the metronome click envelope");

        }

        private static void CheckResult(FMOD.RESULT result, string operation)
        {

            if (result == FMOD.RESULT.OK)
            {

                return;

            }

            throw new InvalidOperationException(
                $"Could not {operation}: {result} ({FMOD.Error.String(result)}).");

        }

    }

}
