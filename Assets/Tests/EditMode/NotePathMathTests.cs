using NUnit.Framework;
using UnityEngine;

namespace IdiotTape.Gameplay.Tests
{

    public sealed class NotePathMathTests
    {

        [TestCase(0.5d, 2)]
        [TestCase(1d, 2)]
        [TestCase(1.999999d, 2)]
        [TestCase(2d, 6)]
        [TestCase(2.000001d, 6)]
        [TestCase(2.999999d, 6)]
        [TestCase(3d, 5)]
        [TestCase(3.000001d, 5)]
        [TestCase(3.999999d, 5)]
        [TestCase(4d, 5)]
        [TestCase(10d, 5)]
        public void SlideHoldsEachLaneUntilItsNextAuthoredTransition(double songTime, int expectedLane)
        {

            ChartNote note = JsonUtility.FromJson<ChartNote>(
                "{\"hitTime\":1.0,\"laneIndex\":2,\"noteType\":2," +
                "\"slideNodes\":[{\"time\":2.0,\"laneIndex\":6}," +
                "{\"time\":3.0,\"laneIndex\":5},{\"time\":4.0,\"laneIndex\":5}]} ");

            float result = NotePathMath.GetSlideNormalizedX(note, songTime, 8);
            float expected = PlayfieldGeometry.GetLaneCenterNormalized(expectedLane, 8);

            Assert.That(result, Is.EqualTo(expected).Within(0.000001f));

        }

        [TestCase(2.5d, 6)]
        [TestCase(2.999999d, 6)]
        [TestCase(3d, 5)]
        [TestCase(3.000001d, 5)]
        public void TerminalFlickWaitsAtPreviousLaneUntilItsEndpoint(double songTime, int expectedLane)
        {

            ChartNote note = JsonUtility.FromJson<ChartNote>(
                "{\"hitTime\":1.0,\"laneIndex\":2,\"noteType\":2," +
                "\"slideEndBehavior\":1," +
                "\"slideNodes\":[{\"time\":2.0,\"laneIndex\":6},{\"time\":3.0,\"laneIndex\":5}]} ");

            Assert.That(
                NotePathMath.GetSlideNormalizedX(note, songTime, 8),
                Is.EqualTo(PlayfieldGeometry.GetLaneCenterNormalized(expectedLane, 8)).Within(0.000001f));

        }

        [TestCase(4, 0, 3)]
        [TestCase(12, 11, 1)]
        public void SlideStepsUseTheChartsLaneCount(int laneCount, int startLane, int endLane)
        {

            ChartNote note = JsonUtility.FromJson<ChartNote>(
                $"{{\"hitTime\":1.0,\"laneIndex\":{startLane},\"noteType\":2," +
                $"\"slideNodes\":[{{\"time\":2.0,\"laneIndex\":{endLane}}}]}}");

            Assert.That(
                NotePathMath.GetSlideNormalizedX(note, 1.5d, laneCount),
                Is.EqualTo(PlayfieldGeometry.GetLaneCenterNormalized(startLane, laneCount)));
            Assert.That(
                NotePathMath.GetSlideNormalizedX(note, 2d, laneCount),
                Is.EqualTo(PlayfieldGeometry.GetLaneCenterNormalized(endLane, laneCount)));

        }

        [TestCase(0)]
        [TestCase(1)]
        public void SlideRenderPointsKeepEachTransitionAtOneAuthoredTime(int endBehavior)
        {

            ChartNote note = JsonUtility.FromJson<ChartNote>(
                "{\"hitTime\":1.0,\"laneIndex\":2,\"noteType\":2," +
                $"\"slideEndBehavior\":{endBehavior}," +
                "\"slideNodes\":[{\"time\":2.0,\"laneIndex\":6},{\"time\":3.0,\"laneIndex\":5}]} ");
            double[] expectedTimes = { 1d, 2d, 2d, 3d, 3d };
            int[] expectedLanes = { 2, 2, 6, 6, 5 };

            Assert.That(NotePathMath.GetSlideRenderPointCount(note), Is.EqualTo(expectedTimes.Length));

            for (int index = 0; index < expectedTimes.Length; index++)
            {

                NotePathMath.GetSlideRenderPoint(note, index, out double pointTime, out int pointLane);
                Assert.That(pointTime, Is.EqualTo(expectedTimes[index]), $"Point {index} time");
                Assert.That(pointLane, Is.EqualTo(expectedLanes[index]), $"Point {index} lane");

            }

            Assert.That(note.SlideNodes.Count, Is.EqualTo(2));
            Assert.That(note.SlideNodes[0].Time, Is.EqualTo(2d));
            Assert.That(note.SlideNodes[1].Time, Is.EqualTo(3d));

        }

        [Test]
        public void BananaCurvePassesThroughLaneAnchoredEnds()
        {

            ChartNote note = JsonUtility.FromJson<ChartNote>(
                "{\"hitTime\":1.0,\"laneIndex\":0,\"noteType\":4," +
                "\"endTime\":3.0,\"endLaneIndex\":7," +
                "\"bananaCurveHandles\":[{\"normalizedTime\":0.5," +
                "\"normalizedX\":0.8}]} ");

            Assert.That(
                NotePathMath.GetBananaNormalizedX(note, 1d, 8),
                Is.EqualTo(PlayfieldGeometry.GetLaneCenterNormalized(0, 8)).Within(0.00001f));
            Assert.That(
                NotePathMath.GetBananaNormalizedX(note, 3d, 8),
                Is.EqualTo(PlayfieldGeometry.GetLaneCenterNormalized(7, 8)).Within(0.00001f));

        }

    }

}
