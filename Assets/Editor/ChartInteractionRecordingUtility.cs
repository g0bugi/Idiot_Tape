using System;
using IdiotTape.Gameplay;

namespace IdiotTape.EditorTools
{

    internal static class ChartInteractionRecordingUtility
    {

        internal const double MinimumInteractionSpacingSeconds = 0.010d;

        public static void RecordSlideOrHoldLane(
            ChartNoteAuthoringData pending,
            int laneIndex,
            double hitTime,
            string musicalPartId,
            out ChartNoteAuthoringData nextPending,
            out ChartNoteAuthoringData completed)
        {

            completed = null;

            if (pending == null)
            {

                nextPending = ChartNoteAuthoringData.CreateTap(
                    string.Empty,
                    hitTime,
                    laneIndex,
                    musicalPartId);
                nextPending.NoteType = ChartNoteType.Hold;
                nextPending.EndTime = hitTime;
                nextPending.EndLaneIndex = laneIndex;
                return;

            }

            nextPending = pending;

            if (hitTime <= pending.HitTime + MinimumInteractionSpacingSeconds)
            {

                return;

            }

            int currentLane = pending.EndLaneIndex;

            if (pending.NoteType == ChartNoteType.Hold && laneIndex == currentLane)
            {

                pending.EndTime = hitTime;
                completed = pending;
                nextPending = null;
                return;

            }

            pending.NoteType = ChartNoteType.Slide;
            pending.EndTime = hitTime;
            pending.EndLaneIndex = laneIndex;
            pending.SlideNodes.Add(new ChartNoteAuthoringData.PathNodeData
            {

                Time = hitTime,
                LaneIndex = laneIndex

            });

            if (laneIndex == currentLane)
            {

                completed = pending;
                nextPending = null;

            }

        }

        public static bool TryCompleteTerminalFlick(
            ChartNoteAuthoringData pending,
            out ChartNoteAuthoringData completed,
            out string error)
        {

            completed = null;

            if (pending == null ||
                pending.NoteType != ChartNoteType.Slide ||
                pending.SlideNodes.Count == 0)
            {

                error = "종단 플릭으로 바꿀 마지막 레인 이동이 없습니다.";
                return false;

            }

            int previousLane = pending.SlideNodes.Count > 1
                ? pending.SlideNodes[^2].LaneIndex
                : pending.LaneIndex;

            if (previousLane == pending.EndLaneIndex)
            {

                error = "종단 플릭은 마지막 구간에 레인 이동이 있어야 합니다.";
                return false;

            }

            pending.SlideEndBehavior = SlideEndBehavior.Flick;
            completed = pending;
            error = string.Empty;
            return true;

        }

        public static bool TryCreateFlick(
            int laneIndex,
            int direction,
            int laneCount,
            double hitTime,
            string musicalPartId,
            out ChartNoteAuthoringData flick,
            out string error)
        {

            int endLane = laneIndex + Math.Sign(direction);

            if (endLane < 0 || endLane >= laneCount)
            {

                flick = null;
                error = $"{laneIndex + 1}번 레인에서는 선택한 방향의 인접 플릭을 만들 수 없습니다.";
                return false;

            }

            flick = ChartNoteAuthoringData.CreateTap(
                string.Empty,
                hitTime,
                laneIndex,
                musicalPartId);
            flick.NoteType = ChartNoteType.Flick;
            flick.EndLaneIndex = endLane;
            error = string.Empty;
            return true;

        }

    }

}
