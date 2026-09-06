using System;
using System.Collections.Generic;

namespace IdiotTape.EditorTools
{

    public readonly struct ChartTempoAnchor
    {

        public ChartTempoAnchor(int bar, int beat, double songTime)
        {

            Bar = bar;
            Beat = beat;
            SongTime = songTime;

        }

        public int Bar { get; }
        public int Beat { get; }
        public double SongTime { get; }

    }

    public readonly struct ChartTempoCalibrationResult
    {

        public ChartTempoCalibrationResult(
            bool isValid,
            string error,
            double beatsPerMinute,
            double firstDownbeatTime,
            int beatDistance,
            double timeDistance,
            int anchorCount = 2,
            double rootMeanSquareError = 0d)
        {

            IsValid = isValid;
            Error = error;
            BeatsPerMinute = beatsPerMinute;
            FirstDownbeatTime = firstDownbeatTime;
            BeatDistance = beatDistance;
            TimeDistance = timeDistance;
            AnchorCount = anchorCount;
            RootMeanSquareError = rootMeanSquareError;

        }

        public bool IsValid { get; }
        public string Error { get; }
        public double BeatsPerMinute { get; }
        public double FirstDownbeatTime { get; }
        public int BeatDistance { get; }
        public double TimeDistance { get; }
        public int AnchorCount { get; }
        public double RootMeanSquareError { get; }

    }

    public static class ChartTempoCalibration
    {

        public static ChartTempoCalibrationResult Calculate(
            ChartTempoAnchor firstAnchor,
            ChartTempoAnchor secondAnchor,
            int beatsPerBar,
            int beatUnit)
        {

            return Calculate(
                new[] { firstAnchor, secondAnchor },
                beatsPerBar,
                beatUnit);

        }

        public static ChartTempoCalibrationResult Calculate(
            IReadOnlyList<ChartTempoAnchor> anchors,
            int beatsPerBar,
            int beatUnit,
            double? fixedBeatsPerMinute = null)
        {

            if (beatsPerBar < 1 || beatUnit < 1)
            {

                return Invalid("박자표가 올바르지 않습니다.");

            }

            if (fixedBeatsPerMinute.HasValue &&
                (!double.IsFinite(fixedBeatsPerMinute.Value) || fixedBeatsPerMinute.Value < 1d ||
                 fixedBeatsPerMinute.Value > 1000d))
            {

                return Invalid("고정 BPM은 1~1000 사이의 유한한 값이어야 합니다.");

            }

            if (anchors == null || anchors.Count < 2)
            {

                return Invalid("캘리브레이션에는 앵커가 두 개 이상 필요합니다.");

            }

            double beatIndexSum = 0d;
            double songTimeSum = 0d;
            int previousBeatIndex = -1;
            double previousSongTime = -1d;

            for (int index = 0; index < anchors.Count; index++)
            {

                ChartTempoAnchor anchor = anchors[index];

                if (!IsAnchorValid(anchor, beatsPerBar))
                {

                    return Invalid($"앵커의 박은 1부터 {beatsPerBar} 사이여야 합니다.");

                }

                int beatIndex = GetBeatIndex(anchor, beatsPerBar);

                if (index > 0 && beatIndex <= previousBeatIndex)
                {

                    return Invalid("뒤의 앵커는 앞의 앵커보다 뒤의 음악적 위치여야 합니다.");

                }

                if (index > 0 && anchor.SongTime <= previousSongTime)
                {

                    return Invalid("뒤의 앵커는 앞의 앵커보다 뒤의 곡 시간이어야 합니다.");

                }

                beatIndexSum += beatIndex;
                songTimeSum += anchor.SongTime;
                previousBeatIndex = beatIndex;
                previousSongTime = anchor.SongTime;

            }

            int firstBeatIndex = GetBeatIndex(anchors[0], beatsPerBar);
            int secondBeatIndex = GetBeatIndex(anchors[^1], beatsPerBar);
            int beatDistance = secondBeatIndex - firstBeatIndex;
            double timeDistance = anchors[^1].SongTime - anchors[0].SongTime;
            double meanBeatIndex = beatIndexSum / anchors.Count;
            double meanSongTime = songTimeSum / anchors.Count;
            double beatVariance = 0d;
            double beatTimeCovariance = 0d;

            for (int index = 0; index < anchors.Count; index++)
            {

                double centeredBeatIndex = GetBeatIndex(anchors[index], beatsPerBar) - meanBeatIndex;
                double centeredSongTime = anchors[index].SongTime - meanSongTime;
                beatVariance += centeredBeatIndex * centeredBeatIndex;
                beatTimeCovariance += centeredBeatIndex * centeredSongTime;

            }

            if (beatVariance <= 0d || beatTimeCovariance <= 0d)
            {

                return Invalid("앵커 간격으로 유효한 BPM을 계산할 수 없습니다.");

            }

            // With a fixed slope, the least-squares origin is the mean of each
            // measured time minus its musical distance from bar 1. Keep raw taps
            // so their scatter remains visible instead of fitting the BPM to them.
            double secondsPerBeat = fixedBeatsPerMinute.HasValue
                ? 60d / fixedBeatsPerMinute.Value * (4d / beatUnit)
                : beatTimeCovariance / beatVariance;
            double beatsPerMinute = fixedBeatsPerMinute ?? 60d * (4d / beatUnit) / secondsPerBeat;
            double firstDownbeatTime = meanSongTime - meanBeatIndex * secondsPerBeat;

            if (beatsPerMinute < 1d || beatsPerMinute > 1000d)
            {

                return Invalid("계산된 BPM이 허용 범위를 벗어났습니다.");

            }

            if (firstDownbeatTime < 0d)
            {

                return Invalid("계산된 1마디 1박이 곡 시작 이전입니다. 앵커의 마디 번호를 확인하세요.");

            }

            double squaredErrorSum = 0d;

            for (int index = 0; index < anchors.Count; index++)
            {

                int beatIndex = GetBeatIndex(anchors[index], beatsPerBar);
                double expectedTime = firstDownbeatTime + beatIndex * secondsPerBeat;
                double error = anchors[index].SongTime - expectedTime;
                squaredErrorSum += error * error;

            }

            return new ChartTempoCalibrationResult(
                true,
                string.Empty,
                beatsPerMinute,
                firstDownbeatTime,
                beatDistance,
                timeDistance,
                anchors.Count,
                Math.Sqrt(squaredErrorSum / anchors.Count));

        }

        public static double GetExpectedSongTime(
            double firstDownbeatTime,
            double beatsPerMinute,
            int beatsPerBar,
            int beatUnit,
            int bar,
            int beat)
        {

            int beatIndex = (Math.Max(1, bar) - 1) * Math.Max(1, beatsPerBar) + Math.Max(1, beat) - 1;
            double secondsPerBeat = 60d / Math.Max(0.000001d, beatsPerMinute) * (4d / Math.Max(1, beatUnit));
            return firstDownbeatTime + beatIndex * secondsPerBeat;

        }

        private static bool IsAnchorValid(ChartTempoAnchor anchor, int beatsPerBar)
        {

            return anchor.Bar >= 1 &&
                   anchor.Beat >= 1 &&
                   anchor.Beat <= beatsPerBar &&
                   double.IsFinite(anchor.SongTime) && anchor.SongTime >= 0d;

        }

        private static int GetBeatIndex(ChartTempoAnchor anchor, int beatsPerBar)
        {

            return (anchor.Bar - 1) * beatsPerBar + anchor.Beat - 1;

        }

        private static ChartTempoCalibrationResult Invalid(string error)
        {

            return new ChartTempoCalibrationResult(false, error, 0d, 0d, 0, 0d);

        }

    }

}
