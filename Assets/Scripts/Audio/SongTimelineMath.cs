using System;

namespace IdiotTape.Audio
{

    public static class SongTimelineMath
    {

        public static double FromDspClock(
            ulong anchorDspClock,
            ulong currentDspClock,
            int sampleRate,
            double anchorSongTime)
        {

            if (sampleRate <= 0)
            {

                throw new ArgumentOutOfRangeException(nameof(sampleRate));

            }

            if (currentDspClock <= anchorDspClock)
            {

                return anchorSongTime;

            }

            return anchorSongTime + (currentDspClock - anchorDspClock) / (double)sampleRate;

        }

        public static double ForExternalTimestamp(double songTime, double eventTimestamp, double externalNow)
        {

            double eventAge = Math.Max(0d, externalNow - eventTimestamp);
            return songTime - eventAge;

        }

    }

}
