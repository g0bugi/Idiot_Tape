using NUnit.Framework;
using UnityEngine;

namespace IdiotTape.Gameplay.Tests
{

    public sealed class NoteInteractionMathTests
    {

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
