using IdiotTape.EditorTools;
using NUnit.Framework;

namespace IdiotTape.Gameplay.Tests
{

    public sealed class ChartTempoCalibrationTests
    {

        [Test]
        public void SnowAnchorsRecoverBpmAndZeroFirstDownbeat()
        {

            const double firstDownbeat = 0d;
            ChartTempoAnchor first = new(17, 1, firstDownbeat + 64d * 60d / 130d);
            ChartTempoAnchor second = new(81, 1, firstDownbeat + 320d * 60d / 130d);

            ChartTempoCalibrationResult result = ChartTempoCalibration.Calculate(first, second, 4, 4);

            Assert.That(result.IsValid, Is.True);
            Assert.That(result.BeatsPerMinute, Is.EqualTo(130d).Within(0.000001d));
            Assert.That(result.FirstDownbeatTime, Is.EqualTo(firstDownbeat).Within(0.000001d));

        }

        [Test]
        public void CalibrationKeepsMeasuredOffsetInsteadOfAssumingOneBeat()
        {

            const double measuredFirstDownbeat = 0.487d;
            ChartTempoAnchor first = new(9, 1, measuredFirstDownbeat + 32d * 0.5d);
            ChartTempoAnchor second = new(25, 1, measuredFirstDownbeat + 96d * 0.5d);

            ChartTempoCalibrationResult result = ChartTempoCalibration.Calculate(first, second, 4, 4);

            Assert.That(result.IsValid, Is.True);
            Assert.That(result.BeatsPerMinute, Is.EqualTo(120d).Within(0.000001d));
            Assert.That(result.FirstDownbeatTime, Is.EqualTo(measuredFirstDownbeat).Within(0.000001d));

        }

        [Test]
        public void MultipleAnchorsUseAllMeasuredDownbeats()
        {

            ChartTempoAnchor[] anchors =
            {

                new(1, 1, 0.510d),
                new(2, 1, 2.495d),
                new(3, 1, 4.505d),
                new(4, 1, 6.490d),
                new(5, 1, 8.500d)

            };

            ChartTempoCalibrationResult result = ChartTempoCalibration.Calculate(anchors, 4, 4);

            Assert.That(result.IsValid, Is.True);
            Assert.That(result.AnchorCount, Is.EqualTo(5));
            Assert.That(result.BeatsPerMinute, Is.EqualTo(120d).Within(0.2d));
            Assert.That(result.FirstDownbeatTime, Is.EqualTo(0.5d).Within(0.01d));
            Assert.That(result.RootMeanSquareError, Is.GreaterThan(0d));

        }

        [Test]
        public void EighthNoteBeatUnitUsesBeatUnitDuration()
        {

            ChartTempoAnchor first = new(1, 1, 1d);
            ChartTempoAnchor second = new(2, 1, 3d);

            ChartTempoCalibrationResult result = ChartTempoCalibration.Calculate(first, second, 4, 8);

            Assert.That(result.IsValid, Is.True);
            Assert.That(result.BeatsPerMinute, Is.EqualTo(60d).Within(0.000001d));

        }

        [Test]
        public void RejectsReversedMusicalAnchors()
        {

            ChartTempoCalibrationResult result = ChartTempoCalibration.Calculate(
                new ChartTempoAnchor(8, 1, 10d),
                new ChartTempoAnchor(4, 1, 20d),
                4,
                4);

            Assert.That(result.IsValid, Is.False);

        }

        [Test]
        public void ExpectedTimeUsesCalculatedGrid()
        {

            double expected = ChartTempoCalibration.GetExpectedSongTime(0.5d, 120d, 4, 4, 3, 2);

            Assert.That(expected, Is.EqualTo(5d).Within(0.000001d));

        }

    }

}
