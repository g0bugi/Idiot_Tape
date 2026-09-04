using IdiotTape.Audio;
using NUnit.Framework;

namespace IdiotTape.Gameplay.Tests
{

    public sealed class SongTimelineMathTests
    {

        [Test]
        public void DspClockAdvancesFromTimelineAnchor()
        {

            double songTime = SongTimelineMath.FromDspClock(48000, 72000, 48000, 1.25d);

            Assert.That(songTime, Is.EqualTo(1.75d).Within(0.000001d));

        }

        [Test]
        public void DspClockBeforeAnchorDoesNotMoveSongBackward()
        {

            double songTime = SongTimelineMath.FromDspClock(48000, 47000, 48000, 2d);

            Assert.That(songTime, Is.EqualTo(2d));

        }

        [TestCase(0ul, -2d)]
        [TestCase(47999ul, -1.0000208333333333d)]
        [TestCase(95999ul, -0.000020833333333333333d)]
        [TestCase(96000ul, 0d)]
        [TestCase(96001ul, 0.000020833333333333333d)]
        [TestCase(120000ul, 0.5d)]
        public void SignedDspClockCrossesScheduledSongZeroContinuously(ulong currentClock, double expectedTime)
        {

            double timelineTime = SongTimelineMath.FromDspClockSigned(96000, currentClock, 48000, 0d);

            Assert.That(timelineTime, Is.EqualTo(expectedTime).Within(0.000000000001d));

        }

        [Test]
        public void SignedDspClockPreservesSubSamplePrecisionAfterLongMixerUptime()
        {

            ulong anchorClock = ulong.MaxValue - 48000;
            double beforeAnchor = SongTimelineMath.FromDspClockSigned(anchorClock, anchorClock - 1, 48000, 0d);
            double afterAnchor = SongTimelineMath.FromDspClockSigned(anchorClock, anchorClock + 1, 48000, 0d);

            Assert.That(beforeAnchor, Is.EqualTo(-1d / 48000d).Within(0.000000000001d));
            Assert.That(afterAnchor, Is.EqualTo(1d / 48000d).Within(0.000000000001d));

        }

        [Test]
        public void SignedDspClockUsesSeekPositionAsItsAnchor()
        {

            double timelineTime = SongTimelineMath.FromDspClockSigned(96000, 48000, 48000, 30d);

            Assert.That(timelineTime, Is.EqualTo(29d));
            Assert.That(SongTimelineMath.FromDspClock(96000, 48000, 48000, 30d), Is.EqualTo(30d));

        }

        [TestCase(0)]
        [TestCase(-48000)]
        public void SignedDspClockRejectsInvalidSampleRate(int sampleRate)
        {

            Assert.Throws<System.ArgumentOutOfRangeException>(
                () => SongTimelineMath.FromDspClockSigned(0, 0, sampleRate, 0d));

        }

        [Test]
        public void ExternalInputTimestampRetainsItsNegativePreparationTime()
        {

            double inputSongTime = SongTimelineMath.ForExternalTimestamp(0.01d, 9.98d, 10d);

            Assert.That(inputSongTime, Is.EqualTo(-0.01d).Within(0.000001d));

        }

        [Test]
        public void ExternalInputTimestampSubtractsEventAge()
        {

            double inputSongTime = SongTimelineMath.ForExternalTimestamp(3d, 10.9d, 11d);

            Assert.That(inputSongTime, Is.EqualTo(2.9d).Within(0.000001d));

        }

        [Test]
        public void ScheduledStartPreservesRequestedPreparationWhenItCoversNativeBudget()
        {

            ulong delay = SongTimelineMath.GetScheduledStartDelaySamples(2.1d, 48000, 8192, 2048);

            Assert.That(delay, Is.EqualTo(100800ul));

        }

        [Test]
        public void ShortScheduleUsesOnePreparationBudgetAndBufferLeadBeforeTheGate()
        {

            ulong delay = SongTimelineMath.GetScheduledStartDelaySamples(0.1d, 48000, 8192, 2048);

            Assert.That(delay, Is.EqualTo(10240ul));

        }

        [Test]
        public void ScheduledBudgetIsProvidedByTheBackendRatherThanAFixedLatency()
        {

            ulong delay = SongTimelineMath.GetScheduledStartDelaySamples(0.01d, 44100, 3072, 768);

            Assert.That(delay, Is.EqualTo(3840ul));

        }

    }

}
