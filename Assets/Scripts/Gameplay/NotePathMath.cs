using System;
using UnityEngine;

namespace IdiotTape.Gameplay
{

    public static class NotePathMath
    {

        private const int CurveSearchIterations = 20;

        public static float GetSlideNormalizedX(ChartNote note, double songTime, int laneCount)
        {

            if (note == null || note.SlideNodes == null || note.SlideNodes.Count == 0)
            {

                return GetLaneCenter(note == null ? 0 : note.LaneIndex, laneCount);

            }

            int previousLane = note.LaneIndex;

            for (int index = 0; index < note.SlideNodes.Count; index++)
            {

                ChartPathNode node = note.SlideNodes[index];
                if (songTime < node.Time)
                {

                    return GetLaneCenter(previousLane, laneCount);

                }

                previousLane = node.LaneIndex;

            }

            return GetLaneCenter(note.EndLaneIndex, laneCount);

        }

        public static int GetSlideRenderPointCount(ChartNote note)
        {

            return note == null ? 0 : 1 + 2 * (note.SlideNodes?.Count ?? 0);

        }

        public static void GetSlideRenderPoint(
            ChartNote note,
            int index,
            out double chartTime,
            out int laneIndex)
        {

            if (note == null)
            {

                throw new ArgumentNullException(nameof(note));

            }

            if (index < 0 || index >= GetSlideRenderPointCount(note))
            {

                throw new ArgumentOutOfRangeException(nameof(index));

            }

            if (index == 0)
            {

                chartTime = note.HitTime;
                laneIndex = note.LaneIndex;
                return;

            }

            int nodeIndex = (index - 1) / 2;
            ChartPathNode node = note.SlideNodes[nodeIndex];
            chartTime = node.Time;
            // Rendering pairs share a time; authored nodes still have strictly increasing times.
            laneIndex = index % 2 == 0
                ? node.LaneIndex
                : nodeIndex == 0 ? note.LaneIndex : note.SlideNodes[nodeIndex - 1].LaneIndex;

        }

        public static float GetBananaNormalizedX(ChartNote note, double songTime, int laneCount)
        {

            if (note == null || note.EndTime <= note.HitTime)
            {

                return GetLaneCenter(note == null ? 0 : note.LaneIndex, laneCount);

            }

            float normalizedTime = Mathf.Clamp01(
                (float)((songTime - note.HitTime) / (note.EndTime - note.HitTime)));
            float startX = GetLaneCenter(note.LaneIndex, laneCount);
            float endX = GetLaneCenter(note.EndLaneIndex, laneCount);

            if (note.BananaCurveHandles == null || note.BananaCurveHandles.Count == 0)
            {

                return Mathf.Lerp(startX, endX, normalizedTime);

            }

            float lower = 0f;
            float upper = 1f;

            for (int iteration = 0; iteration < CurveSearchIterations; iteration++)
            {

                float parameter = (lower + upper) * 0.5f;
                float curveTime = EvaluateBananaCoordinate(
                    note,
                    parameter,
                    0f,
                    1f,
                    true);

                if (curveTime < normalizedTime)
                {

                    lower = parameter;

                }
                else
                {

                    upper = parameter;

                }

            }

            return Mathf.Clamp01(EvaluateBananaCoordinate(
                note,
                (lower + upper) * 0.5f,
                startX,
                endX,
                false));

        }

        public static bool IsInsideLaneCorridor(
            float normalizedX,
            float expectedNormalizedX,
            int laneCount,
            float toleranceInLaneWidths)
        {

            float tolerance = Mathf.Max(0f, toleranceInLaneWidths) / Math.Max(1, laneCount);
            return Mathf.Abs(normalizedX - expectedNormalizedX) <= tolerance;

        }

        private static float EvaluateBananaCoordinate(
            ChartNote note,
            float parameter,
            float startValue,
            float endValue,
            bool evaluateTime)
        {

            float inverse = 1f - parameter;

            if (note.BananaCurveHandles.Count == 1)
            {

                BananaCurveHandle handle = note.BananaCurveHandles[0];
                float handleValue = evaluateTime ? handle.NormalizedTime : handle.NormalizedX;
                return inverse * inverse * startValue +
                       2f * inverse * parameter * handleValue +
                       parameter * parameter * endValue;

            }

            BananaCurveHandle first = note.BananaCurveHandles[0];
            BananaCurveHandle second = note.BananaCurveHandles[1];
            float firstValue = evaluateTime ? first.NormalizedTime : first.NormalizedX;
            float secondValue = evaluateTime ? second.NormalizedTime : second.NormalizedX;
            return inverse * inverse * inverse * startValue +
                   3f * inverse * inverse * parameter * firstValue +
                   3f * inverse * parameter * parameter * secondValue +
                   parameter * parameter * parameter * endValue;

        }

        private static float GetLaneCenter(int laneIndex, int laneCount)
        {

            return PlayfieldGeometry.GetLaneCenterNormalized(
                Mathf.Clamp(laneIndex, 0, Math.Max(1, laneCount) - 1),
                Math.Max(1, laneCount));

        }

    }

}
