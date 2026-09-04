using NUnit.Framework;
using UnityEngine;

namespace IdiotTape.Gameplay.Tests
{

    public sealed class NoteInteractionMathTests
    {

        [TestCase(1.859d, false)]
        [TestCase(1.86d, true)]
        [TestCase(2d, true)]
        [TestCase(2.14d, true)]
        [TestCase(2.141d, false)]
        [TestCase(3d, false)]
        public void SlideTransitionUsesGoodWindowOnlyAroundLaneChanges(double time, bool expected)
        {

            ChartNote slide = JsonUtility.FromJson<ChartNote>(
                "{\"hitTime\":1.0,\"laneIndex\":2,\"noteType\":2," +
                "\"slideNodes\":[{\"time\":2.0,\"laneIndex\":6}," +
                "{\"time\":3.0,\"laneIndex\":6}]}");

            bool found = NoteInteractionMath.TryGetSlideTransitionWindow(
                slide, time, 0.14d, out int nodeIndex, out double start, out double end);

            Assert.That(found, Is.EqualTo(expected));

            if (expected)
            {

                Assert.That(nodeIndex, Is.Zero);
                Assert.That(start, Is.EqualTo(1.86d).Within(0.000001d));
                Assert.That(end, Is.EqualTo(2.14d).Within(0.000001d));

            }

        }

        [TestCase(0d)]
        [TestCase(480d)]
        public void DenseSlideTransitionsAreCappedAtAdjacentTimeMidpoints(double offset)
        {

            ChartNote slide = JsonUtility.FromJson<ChartNote>(
                $"{{\"hitTime\":{(offset + 1.8d).ToString(System.Globalization.CultureInfo.InvariantCulture)}," +
                "\"laneIndex\":2,\"noteType\":2,\"slideNodes\":[" +
                $"{{\"time\":{offset + 2d},\"laneIndex\":6}}," +
                $"{{\"time\":{(offset + 2.2d).ToString(System.Globalization.CultureInfo.InvariantCulture)}," +
                "\"laneIndex\":5}]}");

            Assert.That(NoteInteractionMath.TryGetSlideTransitionWindow(
                slide, offset + 2d, 0.14d, out int first, out double start, out double end), Is.True);
            Assert.That(first, Is.Zero);
            Assert.That(start, Is.EqualTo(offset + 1.9d).Within(0.000001d));
            Assert.That(end, Is.EqualTo(offset + 2.1d).Within(0.000001d));
            Assert.That(NoteInteractionMath.TryGetSlideTransitionWindow(
                slide, offset + 2.2d, 0.14d, out int second, out start, out end), Is.True);
            Assert.That(second, Is.EqualTo(1));
            Assert.That(start, Is.EqualTo(offset + 2.1d).Within(0.000001d));
            Assert.That(end, Is.EqualTo(offset + 2.34d).Within(0.000001d));

        }

        [TestCase(0, true)]
        [TestCase(1, false)]
        public void MovedNormalEndUsesTransitionWindowButTerminalFlickKeepsGestureJudgement(
            int endBehavior,
            bool expected)
        {

            ChartNote slide = JsonUtility.FromJson<ChartNote>(
                "{\"hitTime\":1.0,\"laneIndex\":2,\"noteType\":2," +
                $"\"slideEndBehavior\":{endBehavior}," +
                "\"slideNodes\":[{\"time\":2.0,\"laneIndex\":6}]}");

            Assert.That(NoteInteractionMath.TryGetSlideTransitionWindow(
                slide, 2.1d, 0.14d, out _, out _, out _), Is.EqualTo(expected));

        }

        [TestCase(0, 8, 4, 0)]
        [TestCase(3, 8, 4, 1)]
        [TestCase(7, 8, 4, 3)]
        [TestCase(8, 8, 4, 4)]
        public void BananaBonusUsesFlooredSuccessRatio(
            int successes,
            int checkpoints,
            int maximumBonus,
            int expected)
        {

            Assert.That(
                NoteInteractionMath.GetBananaBonusCombo(
                    successes,
                    checkpoints,
                    maximumBonus),
                Is.EqualTo(expected));

        }

        [Test]
        public void FlickSucceedsOnlyAfterFastMotionReachesAuthoredEndLane()
        {

            JudgementSettings settings = new();
            float start = PlayfieldGeometry.GetLaneCenterNormalized(2, 8);
            float end = PlayfieldGeometry.GetLaneCenterNormalized(5, 8);

            FlickMotionResult result = NoteInteractionMath.EvaluateFlickMotion(
                start,
                start,
                end,
                1.95d,
                2d,
                5,
                8,
                2d,
                settings);

            Assert.That(result, Is.EqualTo(FlickMotionResult.Success));

        }

        [Test]
        public void FlickReportsWrongDirectionBeyondDeadZone()
        {

            JudgementSettings settings = new();
            float start = PlayfieldGeometry.GetLaneCenterNormalized(3, 8);

            FlickMotionResult result = NoteInteractionMath.EvaluateFlickMotion(
                start,
                start,
                start - 0.1f,
                1.95d,
                2d,
                6,
                8,
                2d,
                settings);

            Assert.That(result, Is.EqualTo(FlickMotionResult.WrongDirection));

        }

        [Test]
        public void TwoBeatHoldAllowsOnlyItsFinalBeatAsReleaseGrace()
        {

            ChartNote hold = JsonUtility.FromJson<ChartNote>(
                "{\"hitTime\":1.0,\"laneIndex\":2,\"noteType\":1,\"endTime\":2.0}");
            ChartTempoSection tempo = JsonUtility.FromJson<ChartTempoSection>(
                "{\"startBar\":1,\"startTime\":0.0,\"beatsPerMinute\":120.0," +
                "\"beatsPerBar\":4,\"beatUnit\":4}");

            Assert.That(
                NoteInteractionMath.IsInsideHoldGrace(hold, new[] { tempo }, 1.49d),
                Is.False);
            Assert.That(
                NoteInteractionMath.IsInsideHoldGrace(hold, new[] { tempo }, 1.5d),
                Is.True);

        }

    }

}
