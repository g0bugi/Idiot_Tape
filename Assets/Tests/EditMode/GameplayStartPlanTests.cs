using System;
using System.Globalization;
using NUnit.Framework;
using UnityEngine;

namespace IdiotTape.Gameplay.Tests
{

    public sealed class GameplayStartPlanTests
    {

        [TestCase(120d, 4, 4, 4.1d)]
        [TestCase(120d, 3, 4, 3.1d)]
        [TestCase(120d, 6, 8, 3.1d)]
        [TestCase(60d, 3, 4, 6.1d)]
        [TestCase(240d, 4, 4, 2.1d)]
        public void RequestedPreparationUsesTheSongsTempoAndTimeSignature(
            double bpm,
            int beatsPerBar,
            int beatUnit,
            double expectedDelay)
        {

            GameplayStartPlan plan = CreatePlan(CreateTempo(bpm, beatsPerBar, beatUnit));

            Assert.That(plan.AudioStartDelaySeconds, Is.EqualTo(expectedDelay).Within(0.000001d));
            Assert.That(plan.CountInBars, Is.EqualTo(2));
            Assert.That(plan.BeatsPerBar, Is.EqualTo(beatsPerBar));
            Assert.That(plan.UsesFallbackTempo, Is.False);

        }

        [TestCase(1f, 4)]
        [TestCase(4f, 1)]
        public void EvenATimeZeroNoteGetsItsFullApproachAtTheSelectedSpeed(float speed, int expectedBars)
        {

            float lead = NoteSpeedMath.GetVisualLeadTime(8f, speed);
            GameplayStartPlan plan = CreatePlan(CreateTempo(), 0d, lead, 1);

            Assert.That(plan.CountInBars, Is.EqualTo(expectedBars));
            Assert.That(0d - plan.InitialSongTime, Is.GreaterThanOrEqualTo(lead));
            Assert.That(plan.InitialSongTime, Is.LessThan(0d - lead));
            Assert.That(plan.CountInStartSongTime, Is.LessThanOrEqualTo(0d - lead));

        }

        [Test]
        public void LongerVisualApproachExtendsPreparationByWholeBars()
        {

            GameplayStartPlan plan = CreatePlan(CreateTempo(), 0d, 4.01d, 2);

            Assert.That(plan.CountInBars, Is.EqualTo(3));
            Assert.That(plan.AudioStartDelaySeconds, Is.EqualTo(6.1d).Within(0.000001d));

        }

        [Test]
        public void AnAuthoredIntroContributesToFirstNoteApproachWithoutMovingTheNote()
        {

            const double firstNoteTime = 6d;
            GameplayStartPlan plan = CreatePlan(CreateTempo(), firstNoteTime, 7.1d, 1);

            Assert.That(plan.CountInBars, Is.EqualTo(1));
            Assert.That(plan.AudioStartDelaySeconds, Is.EqualTo(2.1d).Within(0.000001d));
            Assert.That(firstNoteTime - plan.InitialSongTime, Is.GreaterThanOrEqualTo(7.1d));

        }

        [TestCase(0d)]
        [TestCase(0.233293d)]
        [TestCase(2.125d)]
        public void NegativeBeatsExtendTheCalibratedOriginWithoutRewritingIt(double firstDownbeat)
        {

            ChartTempoSection tempo = CreateTempo(startTime: firstDownbeat);
            GameplayStartPlan plan = CreatePlan(tempo);

            Assert.That(plan.FirstDownbeatTime, Is.EqualTo(firstDownbeat));
            Assert.That(plan.GetBeatTime(0), Is.EqualTo(tempo.StartTime));
            Assert.That(plan.GetBeatTime(plan.LastCountInBeatIndex), Is.LessThan(0d));
            Assert.That(plan.GetBeatTime(plan.LastCountInBeatIndex + 1), Is.GreaterThanOrEqualTo(0d));
            Assert.That(plan.GetBeatTime(plan.FirstCountInBeatIndex), Is.GreaterThanOrEqualTo(plan.CountInStartSongTime));
            Assert.That(plan.LastCountInBeatIndex - plan.FirstCountInBeatIndex + 1, Is.EqualTo(8));
            Assert.That(plan.AudioStartDelaySeconds, Is.EqualTo(4.1d).Within(0.000001d));

        }

        [TestCase(0.6d, 300d)]
        [TestCase(0.3d, 600d)]
        public void DecimalBeatBoundariesDoNotScheduleAClickAtAudioStart(double firstDownbeat, double bpm)
        {

            GameplayStartPlan plan = CreatePlan(CreateTempo(bpm, startTime: firstDownbeat));
            double lastBeatTime = plan.GetBeatTime(plan.LastCountInBeatIndex);

            Assert.That(lastBeatTime, Is.EqualTo(-plan.SecondsPerBeat).Within(0.000000001d));
            Assert.That(Math.Round(-lastBeatTime * 48000d), Is.GreaterThan(0d));
            Assert.That(plan.GetCountdownNumber(-0.05d), Is.EqualTo(1));
            Assert.That(plan.GetCountdownNumber(0d), Is.Zero);

        }

        [TestCase(3, 4)]
        [TestCase(4, 4)]
        [TestCase(6, 8)]
        public void FinalCountdownUsesOneBarOfTheActualTimeSignature(int beatsPerBar, int beatUnit)
        {

            GameplayStartPlan plan = CreatePlan(CreateTempo(120d, beatsPerBar, beatUnit, 0.233293d));

            for (int remaining = beatsPerBar; remaining >= 1; remaining--)
            {

                int index = plan.LastCountInBeatIndex - remaining + 1;
                double time = plan.GetBeatTime(index);
                Assert.That(plan.GetCountdownNumber(time), Is.EqualTo(remaining));
                Assert.That(plan.GetCountdownNumber(time + 0.01d), Is.EqualTo(remaining));

            }

            Assert.That(plan.GetCountdownNumber(plan.GetBeatTime(plan.LastCountInBeatIndex - beatsPerBar)), Is.Zero);
            Assert.That(plan.GetCountdownNumber(0d), Is.Zero);
            Assert.That(plan.GetCountdownNumber(10d), Is.Zero);

        }

        [Test]
        public void CountdownRecoversImmediatelyAfterSkippedRenderingFrames()
        {

            GameplayStartPlan plan = CreatePlan(CreateTempo());

            Assert.That(plan.GetCountdownNumber(-2d), Is.EqualTo(4));
            Assert.That(plan.GetCountdownNumber(-0.7d), Is.EqualTo(2));
            Assert.That(plan.GetCountdownNumber(-0.1d), Is.EqualTo(1));
            Assert.That(plan.GetCountdownNumber(0.2d), Is.Zero);
            Assert.That(plan.GetBeatIndexAtOrBefore(-0.7d), Is.EqualTo(-2));
            Assert.That(plan.GetBeatTime(plan.GetBeatIndexAtOrBefore(-0.7d)), Is.EqualTo(-1d));

        }

        [Test]
        public void SchedulingMarginPrecedesCountInAndNeverChangesTheMusicalGrid()
        {

            GameplayStartPlan plan = GameplayStartPlan.Create(
                new[] { CreateTempo() }, 0d, 1d, 2, 0.3d, 90d, 3, 8);

            Assert.That(plan.AudioStartDelaySeconds, Is.EqualTo(4.3d).Within(0.000001d));
            Assert.That(plan.CountInStartSongTime, Is.EqualTo(-4d));
            Assert.That(plan.GetBeatTime(plan.FirstCountInBeatIndex), Is.EqualTo(-4d));
            Assert.That(plan.GetCountdownNumber(plan.InitialSongTime), Is.Zero);

        }

        [Test]
        public void TempoChangesAfterSongStartDoNotChangeThePreparationTempo()
        {

            GameplayStartPlan plan = GameplayStartPlan.Create(
                new[] { CreateTempo(90d, 3, 4), CreateTempo(180d, 6, 8, 8d, 5) },
                0d, 1d, 2, 0.1d, 120d, 4, 4);

            Assert.That(plan.SecondsPerBeat, Is.EqualTo(2d / 3d).Within(0.000001d));
            Assert.That(plan.BeatsPerBar, Is.EqualTo(3));
            Assert.That(plan.AudioStartDelaySeconds, Is.EqualTo(4.1d).Within(0.000001d));

        }

        [Test]
        public void MissingTempoUsesTheCallersExplicitFallback()
        {

            GameplayStartPlan plan = GameplayStartPlan.Create(
                Array.Empty<ChartTempoSection>(), 0d, 1d, 2, 0.2d, 80d, 3, 8);

            Assert.That(plan.UsesFallbackTempo, Is.True);
            Assert.That(plan.FirstDownbeatTime, Is.Zero);
            Assert.That(plan.SecondsPerBeat, Is.EqualTo(0.375d));
            Assert.That(plan.BeatsPerBar, Is.EqualTo(3));
            Assert.That(plan.AudioStartDelaySeconds, Is.EqualTo(2.45d).Within(0.000001d));

        }

        [Test]
        public void AChartTempoDoesNotDependOnUnusedFallbackValues()
        {

            GameplayStartPlan plan = GameplayStartPlan.Create(
                new[] { CreateTempo() }, 0d, 1d, 1, 0.1d, double.NaN, 0, 0);

            Assert.That(plan.UsesFallbackTempo, Is.False);
            Assert.That(plan.AudioStartDelaySeconds, Is.EqualTo(2.1d).Within(0.000001d));

        }

        [Test]
        public void InvalidTimingInputCannotProduceAnInvalidDspSchedule()
        {

            Assert.Throws<ArgumentOutOfRangeException>(() => CreatePlan(CreateTempo(), -1d));
            Assert.Throws<ArgumentOutOfRangeException>(() => CreatePlan(CreateTempo(), visualLead: double.NaN));
            Assert.Throws<ArgumentOutOfRangeException>(() => CreatePlan(CreateTempo(), bars: 0));
            Assert.Throws<ArgumentException>(() => GameplayStartPlan.Create(
                null, 0d, 1d, 1, 0.1d, double.NaN, 4, 4));

        }

        private static GameplayStartPlan CreatePlan(
            ChartTempoSection tempo,
            double firstNoteTime = 0d,
            double visualLead = 1d,
            int bars = 2)
        {

            return GameplayStartPlan.Create(new[] { tempo }, firstNoteTime, visualLead, bars, 0.1d, 120d, 4, 4);

        }

        private static ChartTempoSection CreateTempo(
            double bpm = 120d,
            int beatsPerBar = 4,
            int beatUnit = 4,
            double startTime = 0d,
            int startBar = 1)
        {

            return JsonUtility.FromJson<ChartTempoSection>(string.Format(
                CultureInfo.InvariantCulture,
                "{{\"startBar\":{0},\"startTime\":{1},\"beatsPerMinute\":{2},\"beatsPerBar\":{3},\"beatUnit\":{4}}}",
                startBar, startTime, bpm, beatsPerBar, beatUnit));

        }

    }

}
