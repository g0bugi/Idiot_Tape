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

        public static double FromDspClockSigned(
            ulong anchorDspClock,
            ulong currentDspClock,
            int sampleRate,
            double anchorSongTime)
        {

            if (sampleRate <= 0)
            {

                throw new ArgumentOutOfRangeException(nameof(sampleRate));

            }

            // Subtract the unsigned sample clocks before converting to double so long
            // running DSP clocks retain sample precision on either side of the anchor.
            return currentDspClock >= anchorDspClock
                ? anchorSongTime + (currentDspClock - anchorDspClock) / (double)sampleRate
                : anchorSongTime - (anchorDspClock - currentDspClock) / (double)sampleRate;

        }

        public static double ForExternalTimestamp(double songTime, double eventTimestamp, double externalNow)
        {

            double eventAge = Math.Max(0d, externalNow - eventTimestamp);
            return songTime - eventAge;

        }

        public static ulong GetScheduledStartDelaySamples(
            double requestedDelaySeconds,
            int sampleRate,
            ulong nativePreparationSamples,
            ulong bufferLeadSamples)
        {

            if (sampleRate <= 0)
            {

                throw new ArgumentOutOfRangeException(nameof(sampleRate));

            }

            if (double.IsNaN(requestedDelaySeconds) || double.IsInfinity(requestedDelaySeconds) ||
                requestedDelaySeconds < 0d)
            {

                throw new ArgumentOutOfRangeException(nameof(requestedDelaySeconds));

            }

            ulong requestedSamples = checked((ulong)Math.Ceiling(requestedDelaySeconds * sampleRate));
            return Math.Max(requestedSamples, checked(nativePreparationSamples + bufferLeadSamples));

        }

    }

}
