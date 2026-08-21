using System;
using System.Collections.Generic;
using FMODUnity;
using UnityEngine;

namespace IdiotTape.EditorTools
{

    public sealed class ChartAuthoringMetronome : IDisposable
    {

        private const float ClickDurationSeconds = 0.045f;
        private const double MinimumScheduleDelaySeconds = 0.025d;
        private const float RegularFrequency = 920f;
        private const float AccentFrequency = 1380f;

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

        public void Schedule(double delaySeconds, bool accent, float volume)
        {

            Initialize();
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
                    masterChannelGroup.getDSPClock(out ulong currentClock, out _),
                    "read the FMOD DSP clock");
                CheckResult(
                    coreSystem.playDSP(dsp, masterChannelGroup, true, out channel),
                    "create a metronome channel");
                CheckResult(
                    channel.setVolume(Mathf.Clamp01(volume)),
                    "set the metronome volume");

                ulong startClock = currentClock + SecondsToSamples(
                    Math.Max(MinimumScheduleDelaySeconds, delaySeconds));
                ulong endClock = startClock + SecondsToSamples(ClickDurationSeconds);
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
