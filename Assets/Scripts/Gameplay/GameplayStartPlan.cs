using System;
using System.Collections.Generic;

namespace IdiotTape.Gameplay
{

    public readonly struct GameplayStartPlan
    {

        private const double BeatBoundaryTolerance = 0.000000001d;

        private GameplayStartPlan(
            double firstDownbeatTime,
            double secondsPerBeat,
            int beatsPerBar,
            int countInBars,
            double minimumSchedulingLead,
            bool usesFallbackTempo)
        {

            FirstDownbeatTime = firstDownbeatTime;
            SecondsPerBeat = secondsPerBeat;
            BeatsPerBar = beatsPerBar;
            CountInBars = countInBars;
            CountInStartSongTime = -countInBars * secondsPerBeat * beatsPerBar;
            AudioStartDelaySeconds = -CountInStartSongTime + minimumSchedulingLead;
            UsesFallbackTempo = usesFallbackTempo;
            FirstCountInBeatIndex = (int)Math.Ceiling(
                (CountInStartSongTime - firstDownbeatTime) / secondsPerBeat - BeatBoundaryTolerance);
            // Decimal grid arithmetic can place the zero beat infinitesimally below zero.
            LastCountInBeatIndex = (int)Math.Ceiling(
                -firstDownbeatTime / secondsPerBeat - BeatBoundaryTolerance) - 1;

        }

        public double FirstDownbeatTime { get; }
        public double SecondsPerBeat { get; }
        public int BeatsPerBar { get; }
        public int CountInBars { get; }
        public double CountInStartSongTime { get; }
        public double AudioStartDelaySeconds { get; }
        public double InitialSongTime => -AudioStartDelaySeconds;
        public bool UsesFallbackTempo { get; }
        public int FirstCountInBeatIndex { get; }
        public int LastCountInBeatIndex { get; }

        public static GameplayStartPlan Create(
            IReadOnlyList<ChartTempoSection> tempoSections,
            double firstVisibleNoteTime,
            double visualLeadTime,
            int requestedBars,
            double minimumSchedulingLead,
            double fallbackBpm,
            int fallbackBeatsPerBar,
            int fallbackBeatUnit)
        {

            RequireNonnegativeFinite(firstVisibleNoteTime, nameof(firstVisibleNoteTime));
            RequireNonnegativeFinite(visualLeadTime, nameof(visualLeadTime));
            RequireNonnegativeFinite(minimumSchedulingLead, nameof(minimumSchedulingLead));

            if (requestedBars < 1)
            {

                throw new ArgumentOutOfRangeException(nameof(requestedBars));

            }

            bool usesFallbackTempo = tempoSections == null || tempoSections.Count == 0;
            ChartTempoSection firstSection = usesFallbackTempo ? null : tempoSections[0];

            if (!usesFallbackTempo && firstSection == null)
            {

                throw new ArgumentException("The first tempo section is missing.", nameof(tempoSections));

            }

            double firstDownbeatTime = usesFallbackTempo ? 0d : firstSection.StartTime;
            double bpm = usesFallbackTempo ? fallbackBpm : firstSection.BeatsPerMinute;
            int beatsPerBar = usesFallbackTempo ? fallbackBeatsPerBar : firstSection.BeatsPerBar;
            int beatUnit = usesFallbackTempo ? fallbackBeatUnit : firstSection.BeatUnit;
            RequireNonnegativeFinite(firstDownbeatTime, nameof(tempoSections));

            if (double.IsNaN(bpm) || double.IsInfinity(bpm) || bpm <= 0d ||
                beatsPerBar < 1 || beatUnit < 1)
            {

                throw new ArgumentException("A positive, finite tempo and time signature are required.");

            }

            double secondsPerBeat = 60d / bpm * (4d / beatUnit);
            double secondsPerBar = secondsPerBeat * beatsPerBar;
            double requiredVisualPreparation = Math.Max(0d, visualLeadTime - firstVisibleNoteTime);
            double requiredBars = Math.Max(requestedBars, Math.Ceiling(requiredVisualPreparation / secondsPerBar));

            if (double.IsInfinity(secondsPerBar) || secondsPerBar <= 0d || requiredBars > int.MaxValue)
            {

                throw new ArgumentOutOfRangeException(nameof(visualLeadTime), "The start plan is too long.");

            }

            return new GameplayStartPlan(
                firstDownbeatTime,
                secondsPerBeat,
                beatsPerBar,
                (int)requiredBars,
                minimumSchedulingLead,
                usesFallbackTempo);

        }

        public int GetBeatIndexAtOrBefore(double songTime)
        {

            // Extend the calibrated first tempo backwards; do not shift the chart's downbeat.
            return (int)Math.Floor(
                (songTime - FirstDownbeatTime) / SecondsPerBeat + BeatBoundaryTolerance);

        }

        public double GetBeatTime(int beatIndex)
        {

            return FirstDownbeatTime + beatIndex * SecondsPerBeat;

        }

        public int GetCountdownNumber(double songTime)
        {

            if (songTime < CountInStartSongTime || songTime >= 0d || double.IsNaN(songTime))
            {

                return 0;

            }

            int beatIndex = GetBeatIndexAtOrBefore(songTime);
            int remainingBeats = LastCountInBeatIndex - beatIndex + 1;
            return beatIndex >= FirstCountInBeatIndex && remainingBeats >= 1 && remainingBeats <= BeatsPerBar
                ? remainingBeats
                : 0;

        }

        private static void RequireNonnegativeFinite(double value, string parameterName)
        {

            if (double.IsNaN(value) || double.IsInfinity(value) || value < 0d)
            {

                throw new ArgumentOutOfRangeException(parameterName);

            }

        }

    }

}
