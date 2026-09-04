using System;
using System.Collections.Generic;
using UnityEngine;

namespace IdiotTape.Gameplay
{

    public enum FlickMotionResult
    {

        None,
        Success,
        WrongDirection

    }

    public static class NoteInteractionMath
    {

        private const double Tolerance = 0.000001d;

        public static bool TryGetSlideTransitionWindow(
            ChartNote note,
            double checkTime,
            double goodWindowSeconds,
            out int nodeIndex,
            out double windowStart,
            out double windowEnd)
        {

            nodeIndex = -1;
            windowStart = 0d;
            windowEnd = 0d;

            if (note == null || note.NoteType != ChartNoteType.Slide)
            {

                return false;

            }

            double previousTime = note.HitTime;
            int previousLane = note.LaneIndex;

            for (int index = 0; index < note.SlideNodes.Count; index++)
            {

                ChartPathNode node = note.SlideNodes[index];
                bool isTerminalFlick = index == note.SlideNodes.Count - 1 &&
                                       note.SlideEndBehavior == SlideEndBehavior.Flick;

                if (node.LaneIndex != previousLane && !isTerminalFlick)
                {

                    double start = Math.Max(
                        node.Time - goodWindowSeconds,
                        (previousTime + node.Time) * 0.5d);
                    double end = node.Time + goodWindowSeconds;

                    if (index + 1 < note.SlideNodes.Count)
                    {

                        end = Math.Min(end, (node.Time + note.SlideNodes[index + 1].Time) * 0.5d);

                    }

                    if (checkTime >= start - Tolerance && checkTime <= end + Tolerance)
                    {

                        nodeIndex = index;
                        windowStart = start;
                        windowEnd = end;
                        return true;

                    }

                }

                previousTime = node.Time;
                previousLane = node.LaneIndex;

            }

            return false;

        }

        public static int GetBananaBonusCombo(
            int successfulCheckpoints,
            int totalCheckpoints,
            int maximumBonusCombo)
        {

            if (totalCheckpoints <= 0 || successfulCheckpoints <= 0 || maximumBonusCombo <= 0)
            {

                return 0;

            }

            float ratio = Mathf.Clamp01((float)successfulCheckpoints / totalCheckpoints);
            return Mathf.FloorToInt(ratio * maximumBonusCombo);

        }

        public static FlickMotionResult EvaluateFlickMotion(
            float startNormalizedX,
            float previousNormalizedX,
            float currentNormalizedX,
            double previousSongTime,
            double currentSongTime,
            int endLaneIndex,
            int laneCount,
            double targetSongTime,
            JudgementSettings settings)
        {

            float targetX = PlayfieldGeometry.GetLaneCenterNormalized(endLaneIndex, laneCount);
            float direction = Mathf.Sign(targetX - startNormalizedX);
            float displacement = currentNormalizedX - startNormalizedX;
            float deadZone = settings.FlickDeadZoneInLaneWidths / laneCount;

            if (Mathf.Abs(displacement) > deadZone && Mathf.Sign(displacement) != direction)
            {

                return FlickMotionResult.WrongDirection;

            }

            double sampleDuration = currentSongTime - previousSongTime;
            float speed = sampleDuration <= 0d
                ? 0f
                : Mathf.Abs(currentNormalizedX - previousNormalizedX) *
                  laneCount /
                  (float)sampleDuration;
            bool reachedTargetLane = PlayfieldGeometry.GetLaneIndex(
                currentNormalizedX,
                laneCount) == endLaneIndex;
            bool withinTime = Math.Abs(currentSongTime - targetSongTime) <=
                              settings.GoodWindowSeconds;

            return reachedTargetLane &&
                   withinTime &&
                   Mathf.Sign(displacement) == direction &&
                   speed >= settings.FlickMinimumSpeedInLaneWidthsPerSecond
                ? FlickMotionResult.Success
                : FlickMotionResult.None;

        }

        public static bool IsInsideHoldGrace(
            ChartNote note,
            IReadOnlyList<ChartTempoSection> tempoSections,
            double releaseSongTime)
        {

            if (releaseSongTime >= note.EndTime)
            {

                return true;

            }

            ChartTempoSection endSection = ChartTempoMap.FindSectionForTime(
                tempoSections,
                Math.Max(note.HitTime, note.EndTime - Tolerance));
            double beatSeconds = endSection.SecondsPerBeat;
            bool isAtLeastTwoBeats = note.EndTime - note.HitTime >= beatSeconds * 2d - Tolerance;
            return isAtLeastTwoBeats && releaseSongTime >= note.EndTime - beatSeconds;

        }

    }

}
