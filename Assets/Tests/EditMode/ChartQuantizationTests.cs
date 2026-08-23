using System.Collections.Generic;
using IdiotTape.EditorTools;
using NUnit.Framework;
using UnityEngine;

namespace IdiotTape.Gameplay.Tests
{

    public sealed class ChartQuantizationTests
    {

        private IReadOnlyList<ChartTempoSection> snowTempo;

        [SetUp]
        public void SetUp()
        {

            ChartTempoSection section = JsonUtility.FromJson<ChartTempoSection>(
                "{\"startBar\":1,\"startTime\":0.46153846153846156,\"beatsPerMinute\":130.0," +
                "\"beatsPerBar\":4,\"beatUnit\":4}");
            snowTempo = new[] { section };

        }

        [Test]
        public void QuantizeUsesSnowDownbeatOffset()
        {

            double gridTime = ChartTempoMap.GetSongTime(snowTempo, 3, 2, 0.25d);
            ChartQuantizationResult result = ChartQuantization.Quantize(
                snowTempo,
                gridTime + 0.030d,
                4,
                0.060d,
                1d,
                0d);

            Assert.That(result.WasQuantized, Is.True);
            Assert.That(result.CorrectedTime, Is.EqualTo(gridTime).Within(0.000001d));

        }

        [Test]
        public void CorrectionOutsideWindowKeepsInputAdjustedTime()
        {

            double originalTime = ChartTempoMap.GetSongTime(snowTempo, 2, 1) + 0.050d;
            ChartQuantizationResult result = ChartQuantization.Quantize(
                snowTempo,
                originalTime,
                4,
                0.020d,
                1d,
                0d);

            Assert.That(result.WasQuantized, Is.False);
            Assert.That(result.CorrectedTime, Is.EqualTo(originalTime).Within(0.000001d));

        }

        [Test]
        public void StrengthAppliesPartialCorrection()
        {

            double gridTime = ChartTempoMap.GetSongTime(snowTempo, 2, 3);
            double originalTime = gridTime + 0.040d;
            ChartQuantizationResult result = ChartQuantization.Quantize(
                snowTempo,
                originalTime,
                2,
                0.060d,
                0.5d,
                0d);

            Assert.That(result.CorrectedTime, Is.EqualTo(gridTime + 0.020d).Within(0.000001d));

        }

        [Test]
        public void PositiveInputAdvanceMovesRecordedTimeEarlier()
        {

            double originalTime = ChartTempoMap.GetSongTime(snowTempo, 2, 1) + 0.040d;
            ChartQuantizationResult result = ChartQuantization.Quantize(
                snowTempo,
                originalTime,
                4,
                0d,
                1d,
                0.025d);

            Assert.That(result.WasQuantized, Is.False);
            Assert.That(result.CorrectedTime, Is.EqualTo(originalTime - 0.025d).Within(0.000001d));

        }

        [Test]
        public void TripletSubdivisionSnapsToThirdOfBeat()
        {

            double beat = snowTempo[0].SecondsPerBeat;
            double sectionStart = snowTempo[0].StartTime;
            double expected = sectionStart + beat / 3d;
            ChartQuantizationResult result = ChartQuantization.Quantize(
                snowTempo,
                expected + 0.020d,
                3,
                0.050d,
                1d,
                0d);

            Assert.That(result.CorrectedTime, Is.EqualTo(expected).Within(0.000001d));

        }

    }

}
