using System;
using System.Collections.Generic;
using IdiotTape.Gameplay;

namespace IdiotTape.EditorTools
{

    internal static class ChartSelectionTransform
    {

        public static bool TryTransform(
            IReadOnlyList<ChartNoteAuthoringData> source,
            IReadOnlyList<ChartTempoSection> tempoSections,
            int laneCount,
            double timeOffsetSeconds,
            double quarterBeatOffset,
            int laneOffset,
            string partId,
            out List<ChartNoteAuthoringData> result,
            out string error)
        {

            result = null;
            error = string.Empty;

            if (source == null || source.Count == 0)
            {

                error = "이동할 노트를 먼저 선택하세요.";
                return false;

            }

            if (laneCount < 2 || !IsFinite(timeOffsetSeconds) || !IsFinite(quarterBeatOffset))
            {

                error = "레인 수 또는 이동량이 올바르지 않습니다.";
                return false;

            }

            if (partId != null && string.IsNullOrWhiteSpace(partId))
            {

                error = "변경할 파트 ID가 비어 있습니다.";
                return false;

            }

            if (!TryValidateTempo(tempoSections, quarterBeatOffset != 0d, out error))
            {

                return false;

            }

            bool hasTempo = tempoSections != null && tempoSections.Count > 0;
            double anchor = double.PositiveInfinity;

            for (int index = 0; index < source.Count; index++)
            {

                if (!TryValidateNote(source[index], laneCount, hasTempo, out string detail))
                {

                    error = DescribeNote(source[index], index, detail);
                    return false;

                }

                anchor = Math.Min(anchor, source[index].HitTime);

            }

            // Translate the whole selection by its earliest note's musical displacement.
            // Moving each note through the tempo map separately would stretch recorded spacing.
            double musicalOffset = quarterBeatOffset == 0d
                ? 0d
                : MoveByQuarterBeats(tempoSections, anchor, quarterBeatOffset) - anchor;
            double totalOffset = timeOffsetSeconds + musicalOffset;

            if (!IsFinite(totalOffset))
            {

                error = "이동한 노트 시각이 유효한 범위를 벗어납니다.";
                return false;

            }

            List<ChartNoteAuthoringData> transformed = new(source.Count);
            float normalizedOffset = (float)((double)laneOffset / laneCount);

            for (int index = 0; index < source.Count; index++)
            {

                ChartNoteAuthoringData note = source[index];
                long shiftedLane = (long)note.LaneIndex + laneOffset;
                long shiftedEndLane = (long)note.EndLaneIndex + laneOffset;

                if (shiftedLane < 0 || shiftedLane >= laneCount ||
                    shiftedEndLane < int.MinValue || shiftedEndLane > int.MaxValue)
                {

                    error = DescribeNote(note, index, "이동한 레인이 차트 범위를 벗어납니다.");
                    return false;

                }

                ChartNoteAuthoringData clone = note.CloneWithOffset(totalOffset);
                clone.LaneIndex = (int)shiftedLane;
                clone.EndLaneIndex = (int)shiftedEndLane;

                if (partId != null)
                {

                    clone.MusicalPartId = partId;

                }

                for (int nodeIndex = 0; nodeIndex < clone.SlideNodes.Count; nodeIndex++)
                {

                    long nodeLane = (long)clone.SlideNodes[nodeIndex].LaneIndex + laneOffset;

                    if (nodeLane < 0 || nodeLane >= laneCount)
                    {

                        error = DescribeNote(note, index, $"슬라이드 노드 {nodeIndex + 1}이 레인 범위를 벗어납니다.");
                        return false;

                    }

                    clone.SlideNodes[nodeIndex].LaneIndex = (int)nodeLane;

                }

                for (int handleIndex = 0; handleIndex < clone.BananaCurveHandles.Count; handleIndex++)
                {

                    clone.BananaCurveHandles[handleIndex].NormalizedX += normalizedOffset;

                }

                for (int checkpointIndex = 0; checkpointIndex < clone.BananaCheckpoints.Count; checkpointIndex++)
                {

                    clone.BananaCheckpoints[checkpointIndex].NormalizedX += normalizedOffset;

                }

                if (!TryValidateNote(clone, laneCount, hasTempo, out string detail))
                {

                    error = DescribeNote(note, index, detail);
                    return false;

                }

                transformed.Add(clone);

            }

            result = transformed;
            return true;

        }

        private static double MoveByQuarterBeats(
            IReadOnlyList<ChartTempoSection> sections,
            double songTime,
            double quarterBeats)
        {

            ChartTempoSection section = ChartTempoMap.FindSectionForTime(sections, songTime);
            int sectionIndex = 0;

            while (!ReferenceEquals(sections[sectionIndex], section))
            {

                sectionIndex++;

            }

            double remaining = Math.Abs(quarterBeats);
            bool forward = quarterBeats > 0d;
            double cursor = songTime;

            while (true)
            {

                section = sections[sectionIndex];
                double secondsPerQuarterBeat = section.SecondsPerBeat * section.BeatUnit / 4d;
                bool finalSection = forward ? sectionIndex == sections.Count - 1 : sectionIndex == 0;

                if (finalSection)
                {

                    return cursor + (forward ? remaining : -remaining) * secondsPerQuarterBeat;

                }

                double boundary = forward ? sections[sectionIndex + 1].StartTime : section.StartTime;
                double available = Math.Abs(boundary - cursor) / secondsPerQuarterBeat;

                if (remaining <= available)
                {

                    return cursor + (forward ? remaining : -remaining) * secondsPerQuarterBeat;

                }

                remaining -= available;
                cursor = boundary;
                sectionIndex += forward ? 1 : -1;

            }

        }

        private static bool TryValidateTempo(
            IReadOnlyList<ChartTempoSection> sections,
            bool required,
            out string error)
        {

            error = string.Empty;

            if (sections == null || sections.Count == 0)
            {

                if (required)
                {

                    error = "박자 단위 이동에는 템포 정보가 필요합니다.";
                    return false;

                }

                return true;

            }

            double previousTime = -1d;
            int previousBar = 0;

            for (int index = 0; index < sections.Count; index++)
            {

                ChartTempoSection section = sections[index];

                if (section == null || !IsFinite(section.StartTime) ||
                    !IsFinite(section.BeatsPerMinute) || section.BeatsPerMinute <= 0d ||
                    section.BeatsPerBar < 1 || section.BeatUnit < 1 ||
                    !IsFinite(section.SecondsPerBeat) || section.SecondsPerBeat <= 0d ||
                    section.StartTime < 0d || section.StartTime <= previousTime ||
                    section.StartBar <= previousBar)
                {

                    error = $"템포 구간 {index + 1}의 값이나 순서가 올바르지 않습니다.";
                    return false;

                }

                previousTime = section.StartTime;
                previousBar = section.StartBar;

            }

            return true;

        }

        private static bool TryValidateNote(
            ChartNoteAuthoringData note,
            int laneCount,
            bool hasTempo,
            out string error)
        {

            error = string.Empty;

            if (note == null || !Enum.IsDefined(typeof(ChartNoteType), note.NoteType) ||
                !IsFinite(note.HitTime) || note.HitTime < 0d ||
                note.LaneIndex < 0 || note.LaneIndex >= laneCount)
            {

                error = "노트 종류, 시작 시각 또는 시작 레인이 올바르지 않습니다.";
                return false;

            }

            if (note.SlideNodes == null || note.BananaCurveHandles == null || note.BananaCheckpoints == null)
            {

                error = "노트 경로 데이터가 누락되었습니다.";
                return false;

            }

            if ((note.NoteType == ChartNoteType.Hold || note.NoteType == ChartNoteType.Slide) && !hasTempo)
            {

                error = "홀드와 슬라이드에는 템포 정보가 필요합니다.";
                return false;

            }

            if ((note.NoteType == ChartNoteType.Hold || note.NoteType == ChartNoteType.Banana) &&
                (!IsFinite(note.EndTime) || note.EndTime <= note.HitTime))
            {

                error = "종료 시각은 시작 시각보다 늦어야 합니다.";
                return false;

            }

            if ((note.NoteType == ChartNoteType.Flick || note.NoteType == ChartNoteType.Banana) &&
                (note.EndLaneIndex < 0 || note.EndLaneIndex >= laneCount))
            {

                error = "종료 레인이 차트 범위를 벗어납니다.";
                return false;

            }

            if (note.NoteType == ChartNoteType.Flick && note.EndLaneIndex == note.LaneIndex)
            {

                error = "플릭의 시작과 종료 레인은 달라야 합니다.";
                return false;

            }

            if (note.NoteType == ChartNoteType.Slide &&
                (note.SlideNodes.Count == 0 || !Enum.IsDefined(typeof(SlideEndBehavior), note.SlideEndBehavior)))
            {

                error = "슬라이드 노드 또는 종료 동작이 올바르지 않습니다.";
                return false;

            }

            double previousTime = note.HitTime;
            int previousLane = note.LaneIndex;
            bool hasLaneChange = false;

            for (int index = 0; index < note.SlideNodes.Count; index++)
            {

                ChartNoteAuthoringData.PathNodeData node = note.SlideNodes[index];

                if (node == null || !IsFinite(node.Time) || node.Time <= previousTime ||
                    node.LaneIndex < 0 || node.LaneIndex >= laneCount)
                {

                    error = $"슬라이드 노드 {index + 1}의 시각 또는 레인이 올바르지 않습니다.";
                    return false;

                }

                bool changesLane = node.LaneIndex != previousLane;
                hasLaneChange |= changesLane;

                if (note.NoteType == ChartNoteType.Slide && index == note.SlideNodes.Count - 1 &&
                    note.SlideEndBehavior == SlideEndBehavior.Flick && !changesLane)
                {

                    error = "슬라이드의 마지막 플릭에는 레인 전환이 필요합니다.";
                    return false;

                }

                previousTime = node.Time;
                previousLane = node.LaneIndex;

            }

            if (note.NoteType == ChartNoteType.Slide && !hasLaneChange)
            {

                error = "슬라이드에는 레인 전환이 필요합니다.";
                return false;

            }

            if (note.NoteType == ChartNoteType.Banana &&
                (note.BananaCurveHandles.Count < 1 || note.BananaCurveHandles.Count > 2 ||
                 note.BananaCheckpoints.Count == 0 || note.BananaMaximumBonusCombo < 0))
            {

                error = "바나나 곡선, 체크포인트 또는 최대 보너스 콤보가 올바르지 않습니다.";
                return false;

            }

            double previousNormalizedTime = 0d;

            for (int index = 0; index < note.BananaCurveHandles.Count; index++)
            {

                ChartNoteAuthoringData.CurveHandleData handle = note.BananaCurveHandles[index];

                if (handle == null || !IsFinite(handle.NormalizedTime) ||
                    handle.NormalizedTime <= previousNormalizedTime || handle.NormalizedTime >= 1f ||
                    !IsNormalized(handle.NormalizedX))
                {

                    error = $"바나나 곡선 핸들 {index + 1}이 유효한 시간 또는 가로 범위를 벗어납니다.";
                    return false;

                }

                previousNormalizedTime = handle.NormalizedTime;

            }

            previousTime = note.HitTime;

            for (int index = 0; index < note.BananaCheckpoints.Count; index++)
            {

                ChartNoteAuthoringData.CheckpointData checkpoint = note.BananaCheckpoints[index];

                if (checkpoint == null || !IsFinite(checkpoint.Time) || checkpoint.Time <= previousTime ||
                    checkpoint.Time >= note.EndTime || !IsNormalized(checkpoint.NormalizedX))
                {

                    error = $"바나나 체크포인트 {index + 1}의 시각 또는 가로 위치가 올바르지 않습니다.";
                    return false;

                }

                previousTime = checkpoint.Time;

            }

            return true;

        }

        private static string DescribeNote(ChartNoteAuthoringData note, int index, string detail)
        {

            string identity = note != null && !string.IsNullOrEmpty(note.Id) ? $" '{note.Id}'" : string.Empty;
            return $"선택 노트 {index + 1}{identity}: {detail}";

        }

        private static bool IsNormalized(double value)
        {

            return IsFinite(value) && value >= 0d && value <= 1d;

        }

        private static bool IsFinite(double value)
        {

            return !double.IsNaN(value) && !double.IsInfinity(value);

        }

    }

}
