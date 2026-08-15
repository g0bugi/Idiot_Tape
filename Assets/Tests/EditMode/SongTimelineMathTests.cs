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

        [Test]
        public void ExternalInputTimestampSubtractsEventAge()
        {

            double inputSongTime = SongTimelineMath.ForExternalTimestamp(3d, 10.9d, 11d);

            Assert.That(inputSongTime, Is.EqualTo(2.9d).Within(0.000001d));

        }

    }

}
