using System;
using System.Collections.Generic;
using UnityEngine;

namespace IdiotTape.Gameplay
{

    [Serializable]
    public sealed class ChartTempoSection
    {

        [SerializeField, Min(1)] private int startBar = 1;
        [SerializeField, Min(0f)] private double startTime;
        [SerializeField, Min(1f)] private double beatsPerMinute = 120d;
        [SerializeField, Min(1)] private int beatsPerBar = 4;
        [SerializeField, Min(1)] private int beatUnit = 4;

        public int StartBar => startBar;
        public double StartTime => startTime;
        public double BeatsPerMinute => beatsPerMinute;
        public int BeatsPerBar => beatsPerBar;
        public int BeatUnit => beatUnit;
        public double SecondsPerBeat => 60d / beatsPerMinute * (4d / beatUnit);
        public double SecondsPerBar => SecondsPerBeat * beatsPerBar;

    }

    public readonly struct ChartBeatPosition
    {

        public ChartBeatPosition(int bar, int beat, double beatFraction)
        {

            Bar = bar;
            Beat = beat;
            BeatFraction = beatFraction;

        }

        public int Bar { get; }
        public int Beat { get; }
        public double BeatFraction { get; }

    }

    public static class ChartTempoMap
    {

        private const double BoundaryTolerance = 0.000001d;

        public static double GetSongTime(
            IReadOnlyList<ChartTempoSection> sections,
            int bar,
            int beat,
            double beatFraction = 0d)
        {

            ChartTempoSection section = FindSectionForBar(sections, bar);
            int clampedBeat = Math.Max(1, beat);
            double beatsFromSectionStart =
                (bar - section.StartBar) * section.BeatsPerBar +
                (clampedBeat - 1) +
                beatFraction;
            return Math.Max(0d, section.StartTime + beatsFromSectionStart * section.SecondsPerBeat);

        }

        public static ChartBeatPosition GetBeatPosition(
            IReadOnlyList<ChartTempoSection> sections,
            double songTime)
        {

            ChartTempoSection section = FindSectionForTime(sections, songTime);
            double elapsed = Math.Max(0d, songTime - section.StartTime);
            double totalBeats = elapsed / section.SecondsPerBeat;
            int wholeBeats = (int)Math.Floor(totalBeats + BoundaryTolerance);
            int barOffset = wholeBeats / section.BeatsPerBar;
            int beatOffset = wholeBeats % section.BeatsPerBar;
            double fraction = Math.Max(0d, totalBeats - wholeBeats);
            return new ChartBeatPosition(
                section.StartBar + barOffset,
                beatOffset + 1,
                fraction);

        }

        public static double SnapSongTime(
            IReadOnlyList<ChartTempoSection> sections,
            double songTime,
            int subdivisionsPerBeat)
        {

            ChartTempoSection section = FindSectionForTime(sections, songTime);
            int subdivisions = Math.Max(1, subdivisionsPerBeat);
            double subdivisionSeconds = section.SecondsPerBeat / subdivisions;
            double elapsed = songTime - section.StartTime;
            double snappedElapsed = Math.Round(elapsed / subdivisionSeconds) * subdivisionSeconds;
            return Math.Max(0d, section.StartTime + snappedElapsed);

        }

        public static double GetBeatTimeAtOrBefore(
            IReadOnlyList<ChartTempoSection> sections,
            double songTime)
        {

            ChartBeatPosition position = GetBeatPosition(sections, songTime);
            return GetSongTime(sections, position.Bar, position.Beat);

        }

        public static double GetBeatTimeAfter(
            IReadOnlyList<ChartTempoSection> sections,
            double songTime)
        {

            ChartBeatPosition position = GetBeatPosition(sections, songTime);
            ChartTempoSection section = FindSectionForBar(sections, position.Bar);
            int nextBeat = position.Beat + 1;
            int nextBar = position.Bar;

            if (nextBeat > section.BeatsPerBar)
            {

                nextBeat = 1;
                nextBar++;

            }

            return GetSongTime(sections, nextBar, nextBeat);

        }

        public static double GetBarStartAtOrBefore(
            IReadOnlyList<ChartTempoSection> sections,
            double songTime)
        {

            ChartBeatPosition position = GetBeatPosition(sections, songTime);
            return GetSongTime(sections, position.Bar, 1);

        }

        public static double GetBarStartAtOrAfter(
            IReadOnlyList<ChartTempoSection> sections,
            double songTime)
        {

            ChartBeatPosition position = GetBeatPosition(sections, songTime);
            double currentBarStart = GetSongTime(sections, position.Bar, 1);

            if (Math.Abs(currentBarStart - songTime) <= BoundaryTolerance)
            {

                return currentBarStart;

            }

            return GetSongTime(sections, position.Bar + 1, 1);

        }

        public static ChartTempoSection FindSectionForBar(
            IReadOnlyList<ChartTempoSection> sections,
            int bar)
        {

            EnsureSections(sections);
            ChartTempoSection selected = sections[0];

            for (int index = 1; index < sections.Count; index++)
            {

                if (sections[index].StartBar > bar)
                {

                    break;

                }

                selected = sections[index];

            }

            return selected;

        }

        public static ChartTempoSection FindSectionForTime(
            IReadOnlyList<ChartTempoSection> sections,
            double songTime)
        {

            EnsureSections(sections);
            ChartTempoSection selected = sections[0];

            for (int index = 1; index < sections.Count; index++)
            {

                if (sections[index].StartTime > songTime)
                {

                    break;

                }

                selected = sections[index];

            }

            return selected;

        }

        private static void EnsureSections(IReadOnlyList<ChartTempoSection> sections)
        {

            if (sections == null || sections.Count == 0)
            {

                throw new ArgumentException("A chart needs at least one tempo section.", nameof(sections));

            }

        }

    }

}
