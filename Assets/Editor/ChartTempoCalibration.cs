using System;

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
            double timeDistance)
        {

            IsValid = isValid;
            Error = error;
            BeatsPerMinute = beatsPerMinute;
            FirstDownbeatTime = firstDownbeatTime;
            BeatDistance = beatDistance;
            TimeDistance = timeDistance;

        }

        public bool IsValid { get; }
        public string Error { get; }
        public double BeatsPerMinute { get; }
        public double FirstDownbeatTime { get; }
        public int BeatDistance { get; }
        public double TimeDistance { get; }

    }

    public static class ChartTempoCalibration
    {

        public static ChartTempoCalibrationResult Calculate(
            ChartTempoAnchor firstAnchor,
            ChartTempoAnchor secondAnchor,
            int beatsPerBar,
            int beatUnit)
        {

            if (beatsPerBar < 1 || beatUnit < 1)
            {

                return Invalid("박자표가 올바르지 않습니다.");

            }

            if (!IsAnchorValid(firstAnchor, beatsPerBar) || !IsAnchorValid(secondAnchor, beatsPerBar))
            {

                return Invalid($"앵커의 박은 1부터 {beatsPerBar} 사이여야 합니다.");

            }

            int firstBeatIndex = GetBeatIndex(firstAnchor, beatsPerBar);
            int secondBeatIndex = GetBeatIndex(secondAnchor, beatsPerBar);
            int beatDistance = secondBeatIndex - firstBeatIndex;
            double timeDistance = secondAnchor.SongTime - firstAnchor.SongTime;

            if (beatDistance <= 0)
            {

                return Invalid("기준 B의 음악적 위치는 기준 A보다 뒤여야 합니다.");

            }

            if (timeDistance <= 0d)
            {

                return Invalid("기준 B의 곡 시간은 기준 A보다 뒤여야 합니다.");

            }

            double beatsPerMinute = beatDistance * 60d * (4d / beatUnit) / timeDistance;
            double secondsPerBeat = 60d / beatsPerMinute * (4d / beatUnit);
            double firstDownbeatTime = firstAnchor.SongTime - firstBeatIndex * secondsPerBeat;

            if (beatsPerMinute < 1d || beatsPerMinute > 1000d)
            {

                return Invalid("계산된 BPM이 허용 범위를 벗어났습니다.");

            }

            if (firstDownbeatTime < 0d)
            {

                return Invalid("계산된 1마디 1박이 곡 시작 이전입니다. 앵커의 마디 번호를 확인하세요.");

            }

            return new ChartTempoCalibrationResult(
                true,
                string.Empty,
                beatsPerMinute,
                firstDownbeatTime,
                beatDistance,
                timeDistance);

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
                   anchor.SongTime >= 0d;

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
