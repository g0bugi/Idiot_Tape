using System;
using System.Collections.Generic;
using IdiotTape.Audio;
using IdiotTape.Gameplay;

namespace IdiotTape.EditorTools
{

    public sealed class ChartAuthoringMetronome : IDisposable
    {

        public const double MinimumScheduleLeadSeconds = FmodMetronome.MinimumScheduleLeadSeconds;

        private readonly FmodMetronome metronome = new();

        public bool IsInitialized => metronome.IsInitialized;

        public void Initialize()
        {

            metronome.Initialize();

        }

        public bool Schedule(double delaySeconds, bool accent, float volume)
        {

            return metronome.Schedule(delaySeconds, accent, volume);

        }

        public bool ScheduleAtDspClock(ulong startClock, bool accent, float volume)
        {

            return metronome.ScheduleAtDspClock(startClock, accent, volume);

        }

        public static bool TryGetScheduleDelay(
            double targetSongTime,
            double currentSongTime,
            out double delaySeconds)
        {

            return FmodMetronome.TryGetScheduleDelay(targetSongTime, currentSongTime, out delaySeconds);

        }

        public void StopAll()
        {

            metronome.StopAll();

        }

        public static double GetBeatTimeAtOrAfter(IReadOnlyList<ChartTempoSection> sections, double songTime)
        {

            ChartTempoSection first = sections[0];
            if (songTime < first.StartTime)
            {

                // Audible count-in continues across audio time zero, even when
                // bar 1 starts more than one beat into the file. Runtime chart
                // positions still start at bar 1 and need no negative bar data.
                double index = Math.Ceiling((songTime - first.StartTime) / first.SecondsPerBeat - 0.000001d);
                return first.StartTime + index * first.SecondsPerBeat;

            }

            double before = ChartTempoMap.GetBeatTimeAtOrBefore(sections, songTime);
            return Math.Abs(songTime - before) <= 0.000001d
                ? before : ChartTempoMap.GetBeatTimeAfter(sections, songTime);

        }

        public static double GetBeatTimeAfter(IReadOnlyList<ChartTempoSection> sections, double beatTime)
        {

            ChartTempoSection first = sections[0];
            return beatTime < first.StartTime - 0.000001d
                ? Math.Min(first.StartTime, beatTime + first.SecondsPerBeat)
                : ChartTempoMap.GetBeatTimeAfter(sections, beatTime + 0.000001d);

        }

        public static bool IsDownbeat(IReadOnlyList<ChartTempoSection> sections, double beatTime)
        {

            ChartTempoSection first = sections[0];
            return beatTime < first.StartTime
                ? (long)Math.Round((beatTime - first.StartTime) / first.SecondsPerBeat) % first.BeatsPerBar == 0
                : ChartTempoMap.GetBeatPosition(sections, beatTime).Beat == 1;

        }

        public void Dispose()
        {

            metronome.Dispose();

        }

    }

    public readonly struct ChartAuthoringCountInBeat
    {

        public ChartAuthoringCountInBeat(double songTime, bool accent)
        {

            SongTime = songTime;
            Accent = accent;

        }

        public double SongTime { get; }
        public bool Accent { get; }

    }

    public static class ChartAuthoringCountIn
    {

        private const double BoundaryTolerance = 0.000001d;

        public static IReadOnlyList<ChartAuthoringCountInBeat> BuildBeats(
            ChartTempoSection tempo,
            double recordingTargetTime,
            int countInBars)
        {

            if (tempo == null)
            {

                throw new ArgumentNullException(nameof(tempo));

            }

            int barCount = Math.Max(1, countInBars);
            double virtualStartTime = recordingTargetTime - tempo.SecondsPerBar * barCount;
            double beatsFromTempoOrigin =
                (virtualStartTime - tempo.StartTime) / tempo.SecondsPerBeat;
            long firstBeatIndex = (long)Math.Ceiling(beatsFromTempoOrigin - BoundaryTolerance);
            List<ChartAuthoringCountInBeat> beats = new(barCount * tempo.BeatsPerBar);

            for (long beatIndex = firstBeatIndex; ; beatIndex++)
            {

                double beatTime = tempo.StartTime + beatIndex * tempo.SecondsPerBeat;

                if (beatTime >= recordingTargetTime - BoundaryTolerance)
                {

                    break;

                }

                beats.Add(new ChartAuthoringCountInBeat(
                    beatTime,
                    PositiveModulo(beatIndex, tempo.BeatsPerBar) == 0));

            }

            return beats;

        }

        private static long PositiveModulo(long value, int divisor)
        {

            long remainder = value % divisor;
            return remainder < 0 ? remainder + divisor : remainder;

        }

    }

}
