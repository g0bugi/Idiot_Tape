using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace IdiotTape.Gameplay.Tests
{

    public sealed class ChartTempoMapTests
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
        public void SnowBeatAndBarDurationMatchOneHundredThirtyBpm()
        {

            Assert.That(snowTempo[0].SecondsPerBeat, Is.EqualTo(60d / 130d).Within(0.000001d));
            Assert.That(snowTempo[0].SecondsPerBar, Is.EqualTo(240d / 130d).Within(0.000001d));

        }

        [Test]
        public void BarAndBeatConvertToAbsoluteSongTime()
        {

            double thirdBarSecondBeat = ChartTempoMap.GetSongTime(snowTempo, 3, 2);

            Assert.That(thirdBarSecondBeat, Is.EqualTo(10d * 60d / 130d).Within(0.000001d));

        }

        [Test]
        public void SnowFirstDownbeatOffsetKeepsDisplayedBeatAligned()
        {

            double beat = snowTempo[0].SecondsPerBeat;
            ChartBeatPosition position = ChartTempoMap.GetBeatPosition(snowTempo, beat * 2d);

            Assert.That(position.Bar, Is.EqualTo(1));
            Assert.That(position.Beat, Is.EqualTo(2));

        }

        [Test]
        public void SongTimeConvertsBackToBarAndBeat()
        {

            double songTime = ChartTempoMap.GetSongTime(snowTempo, 5, 3, 0.5d);
            ChartBeatPosition position = ChartTempoMap.GetBeatPosition(snowTempo, songTime);

            Assert.That(position.Bar, Is.EqualTo(5));
            Assert.That(position.Beat, Is.EqualTo(3));
            Assert.That(position.BeatFraction, Is.EqualTo(0.5d).Within(0.000001d));

        }

        [Test]
        public void SongTimeSnapsToQuarterBeatSubdivision()
        {

            double beat = snowTempo[0].SecondsPerBeat;
            double snapped = ChartTempoMap.SnapSongTime(snowTempo, beat * 2.24d, 4);

            Assert.That(snapped, Is.EqualTo(beat * 2.25d).Within(0.000001d));

        }

        [Test]
        public void BarBoundsSurroundTimeInsideBar()
        {

            double time = ChartTempoMap.GetSongTime(snowTempo, 4, 2);

            Assert.That(
                ChartTempoMap.GetBarStartAtOrBefore(snowTempo, time),
                Is.EqualTo(ChartTempoMap.GetSongTime(snowTempo, 4, 1)).Within(0.000001d));
            Assert.That(
                ChartTempoMap.GetBarStartAtOrAfter(snowTempo, time),
                Is.EqualTo(ChartTempoMap.GetSongTime(snowTempo, 5, 1)).Within(0.000001d));

        }

    }

}
