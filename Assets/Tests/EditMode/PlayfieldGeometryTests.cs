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

        [Test]
        public void TimingGuideStartsFlatAndEndsOnJudgementLine()
        {

            float spawnCenter = PlayfieldGeometry.GetTimingGuideY(0.5f, 4.45f, -2.75f, 0.18f, 0f);
            float spawnEdge = PlayfieldGeometry.GetTimingGuideY(0f, 4.45f, -2.75f, 0.18f, 0f);
            float hitCenter = PlayfieldGeometry.GetTimingGuideY(0.5f, 4.45f, -2.75f, 0.18f, 1f);
            float hitEdge = PlayfieldGeometry.GetTimingGuideY(0f, 4.45f, -2.75f, 0.18f, 1f);

            Assert.That(spawnCenter, Is.EqualTo(4.45f).Within(0.0001f));
            Assert.That(spawnEdge, Is.EqualTo(spawnCenter).Within(0.0001f));
            Assert.That(hitCenter, Is.EqualTo(-2.75f).Within(0.0001f));
            Assert.That(hitEdge, Is.EqualTo(-2.57f).Within(0.0001f));

        }

    }

}
