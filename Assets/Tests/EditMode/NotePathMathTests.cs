using NUnit.Framework;
using UnityEngine;

namespace IdiotTape.Gameplay.Tests
{

    public sealed class NotePathMathTests
    {

        [Test]
        public void NormalSlideInterpolatesBetweenAuthoredLanes()
        {

            ChartNote note = JsonUtility.FromJson<ChartNote>(
                "{\"hitTime\":1.0,\"laneIndex\":1,\"noteType\":2," +
                "\"slideNodes\":[{\"time\":2.0,\"laneIndex\":5}]} ");

            float result = NotePathMath.GetSlideNormalizedX(note, 1.5d, 8);
            float expected = (PlayfieldGeometry.GetLaneCenterNormalized(1, 8) +
                              PlayfieldGeometry.GetLaneCenterNormalized(5, 8)) * 0.5f;

            Assert.That(result, Is.EqualTo(expected).Within(0.000001f));

        }

        [Test]
        public void TerminalFlickSegmentWaitsAtPreviousLane()
        {

            ChartNote note = JsonUtility.FromJson<ChartNote>(
                "{\"hitTime\":1.0,\"laneIndex\":2,\"noteType\":2," +
                "\"slideEndBehavior\":1," +
                "\"slideNodes\":[{\"time\":2.0,\"laneIndex\":0}]} ");

            Assert.That(
                NotePathMath.GetSlideNormalizedX(note, 1.9d, 8),
                Is.EqualTo(PlayfieldGeometry.GetLaneCenterNormalized(2, 8)).Within(0.000001f));

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
