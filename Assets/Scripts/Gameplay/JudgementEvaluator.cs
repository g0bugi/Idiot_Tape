using System;
using System.Collections.Generic;
using UnityEngine;

namespace IdiotTape.Gameplay
{

    public enum JudgementGrade
    {

        None,
        Perfect,
        Good,
        Miss

    }

    [Serializable]
    public sealed class JudgementSettings
    {

        [SerializeField, Min(0.001f)] private float perfectWindowSeconds = 0.055f;
        [SerializeField, Min(0.001f)] private float goodWindowSeconds = 0.14f;
        [SerializeField, Min(0.1f)] private float positionToleranceInLaneWidths = 0.75f;
        [SerializeField, Min(0.05f)] private float slideToleranceInLaneWidths = 0.8f;
        [SerializeField, Min(0.01f)] private float flickDeadZoneInLaneWidths = 0.2f;
        [SerializeField, Min(0.01f)] private float flickMinimumSpeedInLaneWidthsPerSecond = 3f;

        public double PerfectWindowSeconds => perfectWindowSeconds;
        public double GoodWindowSeconds => goodWindowSeconds;
        public float PositionToleranceInLaneWidths => positionToleranceInLaneWidths;
        public float SlideToleranceInLaneWidths => slideToleranceInLaneWidths;
        public float FlickDeadZoneInLaneWidths => flickDeadZoneInLaneWidths;
        public float FlickMinimumSpeedInLaneWidthsPerSecond => flickMinimumSpeedInLaneWidthsPerSecond;

    }

    public readonly struct JudgementCandidate
    {

        public JudgementCandidate(int sourceIndex, double hitTime, int laneIndex)
        {

            SourceIndex = sourceIndex;
            HitTime = hitTime;
            LaneIndex = laneIndex;

        }

        public int SourceIndex { get; }
        public double HitTime { get; }
        public int LaneIndex { get; }

    }

    public readonly struct JudgementResult
    {

        public JudgementResult(int sourceIndex, JudgementGrade grade, double timingError, float positionError)
        {

            SourceIndex = sourceIndex;
            Grade = grade;
            TimingError = timingError;
            PositionError = positionError;

        }

        public int SourceIndex { get; }
        public JudgementGrade Grade { get; }
        public double TimingError { get; }
        public float PositionError { get; }
        public bool HasHit => Grade == JudgementGrade.Perfect || Grade == JudgementGrade.Good;

    }

    public static class JudgementEvaluator
    {

        public static JudgementResult Evaluate(
            IReadOnlyList<JudgementCandidate> candidates,
            double inputSongTime,
            float inputNormalizedX,
            int laneCount,
            JudgementSettings settings)
        {

            float laneWidth = 1f / laneCount;
            float allowedPositionError = laneWidth * settings.PositionToleranceInLaneWidths;
            int bestIndex = -1;
            float bestPositionError = float.PositiveInfinity;
            double bestAbsoluteTimingError = double.PositiveInfinity;
            double bestTimingError = 0d;

            for (int index = 0; index < candidates.Count; index++)
            {

                JudgementCandidate candidate = candidates[index];
                double timingError = inputSongTime - candidate.HitTime;
                double absoluteTimingError = Math.Abs(timingError);

                if (absoluteTimingError > settings.GoodWindowSeconds)
                {

                    continue;

                }

                float laneCenter = PlayfieldGeometry.GetLaneCenterNormalized(candidate.LaneIndex, laneCount);
                float positionError = Mathf.Abs(inputNormalizedX - laneCenter);

                if (positionError > allowedPositionError)
                {

                    continue;

                }

                bool isCloser = positionError < bestPositionError;
                bool samePositionAndBetterTiming = Mathf.Approximately(positionError, bestPositionError) &&
                                                       absoluteTimingError < bestAbsoluteTimingError;

                if (!isCloser && !samePositionAndBetterTiming)
                {

                    continue;

                }

                bestIndex = index;
                bestPositionError = positionError;
                bestAbsoluteTimingError = absoluteTimingError;
                bestTimingError = timingError;

            }

            if (bestIndex < 0)
            {

                return new JudgementResult(-1, JudgementGrade.None, 0d, 0f);

            }

            JudgementGrade grade = bestAbsoluteTimingError <= settings.PerfectWindowSeconds
                ? JudgementGrade.Perfect
                : JudgementGrade.Good;

            return new JudgementResult(
                candidates[bestIndex].SourceIndex,
                grade,
                bestTimingError,
                bestPositionError);

        }

    }

}
