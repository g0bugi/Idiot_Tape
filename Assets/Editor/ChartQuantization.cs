using System;
using System.Collections.Generic;
using IdiotTape.Gameplay;

namespace IdiotTape.EditorTools
{

    public readonly struct ChartQuantizationResult
    {

        public ChartQuantizationResult(
            double originalTime,
            double inputAdjustedTime,
            double snappedTime,
            double correctedTime,
            bool wasQuantized)
        {

            OriginalTime = originalTime;
            InputAdjustedTime = inputAdjustedTime;
            SnappedTime = snappedTime;
            CorrectedTime = correctedTime;
            WasQuantized = wasQuantized;

        }

        public double OriginalTime { get; }
        public double InputAdjustedTime { get; }
        public double SnappedTime { get; }
        public double CorrectedTime { get; }
        public bool WasQuantized { get; }
        public double CorrectionSeconds => CorrectedTime - OriginalTime;

    }

    public static class ChartQuantization
    {

        public static ChartQuantizationResult Quantize(
            IReadOnlyList<ChartTempoSection> tempoSections,
            double originalTime,
            int subdivisionsPerBeat,
            double maximumCorrectionSeconds,
            double strength,
            double inputAdvanceSeconds)
        {

            if (tempoSections == null || tempoSections.Count == 0)
            {

                throw new ArgumentException("Quantization requires tempo sections.", nameof(tempoSections));

            }

            double safeOriginalTime = Math.Max(0d, originalTime);
            double inputAdjustedTime = Math.Max(0d, safeOriginalTime - inputAdvanceSeconds);
            double snappedTime = ChartTempoMap.SnapSongTime(
                tempoSections,
                inputAdjustedTime,
                Math.Max(1, subdivisionsPerBeat));
            double distanceToGrid = Math.Abs(snappedTime - inputAdjustedTime);
            double safeMaximumCorrection = Math.Max(0d, maximumCorrectionSeconds);
            double safeStrength = Math.Max(0d, Math.Min(1d, strength));
            bool wasQuantized = distanceToGrid <= safeMaximumCorrection + 0.0000001d;
            double targetTime = wasQuantized ? snappedTime : inputAdjustedTime;
            double correctedTime = Math.Max(
                0d,
                inputAdjustedTime + (targetTime - inputAdjustedTime) * safeStrength);

            return new ChartQuantizationResult(
                safeOriginalTime,
                inputAdjustedTime,
                snappedTime,
                correctedTime,
                wasQuantized);

        }

    }

}
