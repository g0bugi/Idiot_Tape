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
