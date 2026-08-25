using NUnit.Framework;

namespace IdiotTape.Gameplay.Tests
{

    public sealed class NoteSpeedMathTests
    {

        [TestCase(1f, 2.4f)]
        [TestCase(2f, 1.2f)]
        [TestCase(4f, 0.6f)]
        public void MultiplierChangesOnlyVisualLeadTime(float multiplier, float expectedLeadTime)
        {

            float leadTime = NoteSpeedMath.GetVisualLeadTime(2.4f, multiplier);

            Assert.That(leadTime, Is.EqualTo(expectedLeadTime).Within(0.0001f));

        }

        [TestCase(-1f, 1f)]
        [TestCase(1f, 1f)]
        [TestCase(2.5f, 2.5f)]
        [TestCase(5f, 4f)]
        public void MultiplierIsClampedToSupportedRange(float input, float expected)
        {

            Assert.That(NoteSpeedMath.ClampMultiplier(input), Is.EqualTo(expected));

        }

        [TestCase(0f, 1f)]
        [TestCase(0.5f, 2.5f)]
        [TestCase(1f, 4f)]
        public void SliderPositionMapsToOneThroughFour(float normalized, float expected)
        {

            Assert.That(
                NoteSpeedMath.GetMultiplierFromNormalized(normalized),
                Is.EqualTo(expected).Within(0.0001f));

        }

    }

}
