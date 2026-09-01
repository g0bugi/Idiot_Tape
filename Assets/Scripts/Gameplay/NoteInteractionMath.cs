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
