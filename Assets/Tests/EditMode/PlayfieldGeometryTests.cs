using NUnit.Framework;

namespace IdiotTape.Gameplay.Tests
{

    public sealed class PlayfieldGeometryTests
    {

        [Test]
        public void EightLaneCentersStayInsideNormalizedPlayfield()
        {

            Assert.That(PlayfieldGeometry.GetLaneCenterNormalized(0, 8), Is.EqualTo(0.0625f));
            Assert.That(PlayfieldGeometry.GetLaneCenterNormalized(7, 8), Is.EqualTo(0.9375f));

        }

        [TestCase(0f, 0)]
        [TestCase(0.1249f, 0)]
        [TestCase(0.125f, 1)]
        [TestCase(0.9999f, 7)]
        [TestCase(1f, 7)]
        public void NormalizedInputMapsToEightLaneGrid(float normalizedX, int expectedLaneIndex)
        {

            Assert.That(PlayfieldGeometry.GetLaneIndex(normalizedX, 8), Is.EqualTo(expectedLaneIndex));

        }

        [Test]
        public void JudgementLineIsOnlySlightlyRaisedAtEdges()
        {

            float center = PlayfieldGeometry.GetJudgementLineY(0.5f, -2.75f, 0.18f);
            float edge = PlayfieldGeometry.GetJudgementLineY(0f, -2.75f, 0.18f);

            Assert.That(center, Is.EqualTo(-2.75f).Within(0.0001f));
            Assert.That(edge, Is.EqualTo(-2.57f).Within(0.0001f));

        }

    }

}
