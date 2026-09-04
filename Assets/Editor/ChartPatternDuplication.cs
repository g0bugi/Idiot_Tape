using System;
using System.Collections.Generic;
using System.Text;
using IdiotTape.Gameplay;
using UnityEditor;

namespace IdiotTape.EditorTools
{

    public enum ChartPatternConflictMode
    {

        AbortIfOccupied,
        ReplaceTargetPart,
        KeepExisting

    }

    public readonly struct ChartPatternPreviewNote
    {

        internal ChartPatternPreviewNote(
            double hitTime,
            int laneIndex,
            string musicalPartId,
            ChartNote sourceNote,
            int barOffset)
        {

            HitTime = hitTime;
            LaneIndex = laneIndex;
            MusicalPartId = musicalPartId;
            SourceNote = sourceNote;
            BarOffset = barOffset;

        }

        public double HitTime { get; }
        public int LaneIndex { get; }
        public string MusicalPartId { get; }
        internal ChartNote SourceNote { get; }
        internal int BarOffset { get; }

    }

    public sealed class ChartPatternDuplicationPreview
    {

        private readonly List<ChartPatternPreviewNote> generatedNotes = new();

        public bool IsValid { get; internal set; }
        public string Error { get; internal set; } = string.Empty;
        public int SourceStartBar { get; internal set; }
        public int SourceBarCount { get; internal set; }
        public int TargetStartBar { get; internal set; }
        public int TargetEndBar { get; internal set; }
        public double TargetStartTime { get; internal set; }
        public double TargetEndTime { get; internal set; }
        public int SourceNoteCount { get; internal set; }
        public int ExistingTargetNoteCount { get; internal set; }
        public int InactiveGeneratedNoteCount { get; internal set; }
        public IReadOnlyList<ChartPatternPreviewNote> GeneratedNotes => generatedNotes;
        public int GeneratedNoteCount => generatedNotes.Count;

        internal List<ChartPatternPreviewNote> EditableGeneratedNotes => generatedNotes;

        public bool CanApply(ChartPatternConflictMode conflictMode)
        {

            return IsValid &&
                   GeneratedNoteCount > 0 &&
                   (conflictMode != ChartPatternConflictMode.AbortIfOccupied ||
                    ExistingTargetNoteCount == 0);

        }

    }

    public static class ChartPatternDuplication
    {

        private const double BoundaryTolerance = 0.000001d;

        private readonly struct EditableWindow
        {

            public EditableWindow(string partId, double startTime, double endTime)
            {

                PartId = partId;
                StartTime = startTime;
                EndTime = endTime;

            }

            public string PartId { get; }
            public double StartTime { get; }
            public double EndTime { get; }

        }

        public static ChartPatternDuplicationPreview CreatePreview(
            PrototypeChart chart,
            string partId,
            double sourceStartTime,
            double sourceEndTime,
            int targetStartBar,
            int repeatCount)
        {

            ChartPatternDuplicationPreview preview = new();

            if (chart == null)
            {

                return Invalid(preview, "차트를 선택하세요.");

            }

            if (chart.TempoSections.Count == 0)
            {

                return Invalid(preview, "패턴 복제에는 차트 템포 정보가 필요합니다.");

            }

            if (string.IsNullOrWhiteSpace(partId))
            {

                return Invalid(preview, "복제할 음악 파트를 선택하세요.");

            }

            if (sourceEndTime <= sourceStartTime)
            {

                return Invalid(preview, "원본 반복 구간이 올바르지 않습니다.");

            }

            if (targetStartBar < 1 || repeatCount < 1)
            {

                return Invalid(preview, "붙여넣기 시작 마디와 반복 횟수는 1 이상이어야 합니다.");

            }

            ChartBeatPosition sourceStart = ChartTempoMap.GetBeatPosition(
                chart.TempoSections,
                sourceStartTime);
            ChartBeatPosition sourceEnd = ChartTempoMap.GetBeatPosition(
                chart.TempoSections,
                sourceEndTime);
            double expectedSourceStart = ChartTempoMap.GetSongTime(
                chart.TempoSections,
                sourceStart.Bar,
                1);
            double expectedSourceEnd = ChartTempoMap.GetSongTime(
                chart.TempoSections,
                sourceEnd.Bar,
                1);

            if (sourceStart.Beat != 1 ||
                sourceEnd.Beat != 1 ||
                sourceStart.BeatFraction > BoundaryTolerance ||
                sourceEnd.BeatFraction > BoundaryTolerance ||
                Math.Abs(expectedSourceStart - sourceStartTime) > BoundaryTolerance ||
                Math.Abs(expectedSourceEnd - sourceEndTime) > BoundaryTolerance)
            {

                return Invalid(preview, "원본 반복 구간을 마디 경계에 맞춰주세요.");

            }

            int sourceBarCount = sourceEnd.Bar - sourceStart.Bar;

            if (sourceBarCount < 1)
            {

                return Invalid(preview, "원본 패턴은 한 마디 이상이어야 합니다.");

            }

            int targetEndBar = targetStartBar + sourceBarCount * repeatCount;
            double targetStartTime = ChartTempoMap.GetSongTime(chart.TempoSections, targetStartBar, 1);
            double targetEndTime = ChartTempoMap.GetSongTime(chart.TempoSections, targetEndBar, 1);

            preview.SourceStartBar = sourceStart.Bar;
            preview.SourceBarCount = sourceBarCount;
            preview.TargetStartBar = targetStartBar;
            preview.TargetEndBar = targetEndBar;
            preview.TargetStartTime = targetStartTime;
            preview.TargetEndTime = targetEndTime;

            if (sourceStartTime < targetEndTime && targetStartTime < sourceEndTime)
            {

                return Invalid(preview, "원본 구간과 붙여넣기 구간이 겹칩니다.");

            }

            ChartTempoSection sourceMeter = ChartTempoMap.FindSectionForBar(
                chart.TempoSections,
                sourceStart.Bar);

            for (int bar = sourceStart.Bar; bar < sourceEnd.Bar; bar++)
            {

                if (!HasMatchingMeter(sourceMeter, ChartTempoMap.FindSectionForBar(chart.TempoSections, bar)))
                {

                    return Invalid(preview, "원본 구간 안에서 박자표가 바뀌어 패턴을 안전하게 복제할 수 없습니다.");

                }

            }

            for (int bar = targetStartBar; bar < targetEndBar; bar++)
            {

                if (!HasMatchingMeter(sourceMeter, ChartTempoMap.FindSectionForBar(chart.TempoSections, bar)))
                {

                    return Invalid(preview, "붙여넣기 구간의 박자표가 원본과 다릅니다.");

                }

            }

            List<ChartNote> sourceNotes = new();

            for (int index = 0; index < chart.Notes.Count; index++)
            {

                ChartNote note = chart.Notes[index];

                if (note.MusicalPartId == partId &&
                    note.HitTime >= sourceStartTime &&
                    note.HitTime < sourceEndTime)
                {

                    sourceNotes.Add(note);

                }

                if (note.MusicalPartId == partId &&
                    note.HitTime >= targetStartTime &&
                    note.HitTime < targetEndTime)
                {

                    preview.ExistingTargetNoteCount++;

                }

            }

            preview.SourceNoteCount = sourceNotes.Count;

            if (sourceNotes.Count == 0)
            {

                return Invalid(preview, "선택한 파트의 원본 반복 구간에 노트가 없습니다.");

            }

            for (int repetition = 0; repetition < repeatCount; repetition++)
            {

                int repetitionStartBar = targetStartBar + repetition * sourceBarCount;

                for (int index = 0; index < sourceNotes.Count; index++)
                {

                    ChartNote sourceNote = sourceNotes[index];
                    ChartBeatPosition sourcePosition = ChartTempoMap.GetBeatPosition(
                        chart.TempoSections,
                        sourceNote.HitTime);
                    int targetBar = repetitionStartBar + sourcePosition.Bar - sourceStart.Bar;
                    double targetTime = ChartTempoMap.GetSongTime(
                        chart.TempoSections,
                        targetBar,
                        sourcePosition.Beat,
                        sourcePosition.BeatFraction);
                    ChartPatternPreviewNote generatedNote = new(
                        targetTime,
                        sourceNote.LaneIndex,
                        sourceNote.MusicalPartId,
                        sourceNote,
                        targetBar - sourcePosition.Bar);
                    preview.EditableGeneratedNotes.Add(generatedNote);

                    if (!chart.IsPartActive(partId, targetTime))
                    {

                        preview.InactiveGeneratedNoteCount++;

                    }

                }

            }

            preview.IsValid = true;
            return preview;

        }

        public static bool TryApply(
            PrototypeChart chart,
            string partId,
            double sourceStartTime,
            double sourceEndTime,
            int targetStartBar,
            int repeatCount,
            ChartPatternConflictMode conflictMode,
            bool addActivationWindow,
            out ChartPatternDuplicationPreview preview)
        {

            preview = CreatePreview(
                chart,
                partId,
                sourceStartTime,
                sourceEndTime,
                targetStartBar,
                repeatCount);

            if (!preview.CanApply(conflictMode))
            {

                return false;

            }

            List<ChartNoteAuthoringData> notes = new(
                chart.Notes.Count + preview.GeneratedNoteCount);
            HashSet<string> usedIds = new();

            for (int index = 0; index < chart.Notes.Count; index++)
            {

                ChartNote note = chart.Notes[index];
                usedIds.Add(note.Id);

                if (conflictMode == ChartPatternConflictMode.ReplaceTargetPart &&
                    note.MusicalPartId == partId &&
                    note.HitTime >= preview.TargetStartTime &&
                    note.HitTime < preview.TargetEndTime)
                {

                    continue;

                }

                notes.Add(ChartNoteAuthoringData.FromChartNote(note));

            }

            string idPrefix = $"{SanitizeId(chart.name)}_{SanitizeId(partId)}_copy_";
            int nextIdNumber = 1;

            for (int index = 0; index < preview.GeneratedNotes.Count; index++)
            {

                ChartPatternPreviewNote note = preview.GeneratedNotes[index];
                string id = GenerateUniqueId(idPrefix, usedIds, ref nextIdNumber);
                ChartNoteAuthoringData source = ChartNoteAuthoringData.FromChartNote(note.SourceNote);
                notes.Add(CloneAtMusicalBarOffset(chart, source, note.BarOffset, id));

            }

            notes.Sort((left, right) =>
            {

                int timeComparison = left.HitTime.CompareTo(right.HitTime);
                return timeComparison != 0
                    ? timeComparison
                    : string.CompareOrdinal(left.Id, right.Id);

            });

            List<EditableWindow> windows = BuildActivationWindows(
                chart,
                partId,
                preview.TargetStartTime,
                preview.TargetEndTime,
                addActivationWindow);

            Undo.RecordObject(chart, "Duplicate chart pattern");
            SerializedObject serializedChart = new(chart);
            WriteNotes(serializedChart.FindProperty("notes"), notes);

            if (addActivationWindow)
            {

                WriteWindows(serializedChart.FindProperty("activationWindows"), windows);

            }

            serializedChart.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(chart);
            return true;

        }

        private static bool HasMatchingMeter(ChartTempoSection left, ChartTempoSection right)
        {

            return left.BeatsPerBar == right.BeatsPerBar && left.BeatUnit == right.BeatUnit;

        }

        internal static ChartNoteAuthoringData CloneAtMusicalBarOffset(
            PrototypeChart chart,
            ChartNoteAuthoringData source,
            int barOffset,
            string id)
        {

            ChartNoteAuthoringData clone = source.CloneWithOffset(0d, id);
            clone.HitTime = MapTimeByBarOffset(chart, source.HitTime, barOffset);

            if (source.NoteType == ChartNoteType.Hold || source.NoteType == ChartNoteType.Banana)
            {

                clone.EndTime = MapTimeByBarOffset(chart, source.EndTime, barOffset);

            }

            for (int index = 0; index < clone.SlideNodes.Count; index++)
            {

                clone.SlideNodes[index].Time = MapTimeByBarOffset(
                    chart,
                    source.SlideNodes[index].Time,
                    barOffset);

            }

            for (int index = 0; index < clone.BananaCheckpoints.Count; index++)
            {

                clone.BananaCheckpoints[index].Time = MapTimeByBarOffset(
                    chart,
                    source.BananaCheckpoints[index].Time,
                    barOffset);

            }

            return clone;

        }

        private static double MapTimeByBarOffset(
            PrototypeChart chart,
            double sourceTime,
            int barOffset)
        {

            ChartBeatPosition position = ChartTempoMap.GetBeatPosition(
                chart.TempoSections,
                sourceTime);
            return ChartTempoMap.GetSongTime(
                chart.TempoSections,
                position.Bar + barOffset,
                position.Beat,
                position.BeatFraction);

        }

        private static ChartPatternDuplicationPreview Invalid(
            ChartPatternDuplicationPreview preview,
            string error)
        {

            preview.IsValid = false;
            preview.Error = error;
            return preview;

        }

        private static string GenerateUniqueId(
            string prefix,
            HashSet<string> usedIds,
            ref int nextNumber)
        {

            string candidate;

            do
            {

                candidate = $"{prefix}{nextNumber:0000}";
                nextNumber++;

            }
            while (!usedIds.Add(candidate));

            return candidate;

        }

        private static string SanitizeId(string value)
        {

            if (string.IsNullOrWhiteSpace(value))
            {

                return "chart";

            }

            StringBuilder builder = new(value.Length);

            for (int index = 0; index < value.Length; index++)
            {

                char character = char.ToLowerInvariant(value[index]);

                if (char.IsLetterOrDigit(character))
                {

                    builder.Append(character);

                }
                else if (builder.Length > 0 && builder[^1] != '_')
                {

                    builder.Append('_');

                }

            }

            return builder.Length == 0 ? "chart" : builder.ToString().TrimEnd('_');

        }

        private static List<EditableWindow> BuildActivationWindows(
            PrototypeChart chart,
            string partId,
            double targetStartTime,
            double targetEndTime,
            bool addActivationWindow)
        {

            List<EditableWindow> windows = new(chart.ActivationWindows.Count + 1);

            for (int index = 0; index < chart.ActivationWindows.Count; index++)
            {

                MusicalPartActivationWindow window = chart.ActivationWindows[index];
                windows.Add(new EditableWindow(
                    window.MusicalPartId,
                    window.StartTime,
                    window.EndTime));

            }

            if (!addActivationWindow)
            {

                return windows;

            }

            windows.Add(new EditableWindow(partId, targetStartTime, targetEndTime));
            windows.Sort((left, right) =>
            {

                int partComparison = string.CompareOrdinal(left.PartId, right.PartId);

                if (partComparison != 0)
                {

                    return partComparison;

                }

                int startComparison = left.StartTime.CompareTo(right.StartTime);
                return startComparison != 0
                    ? startComparison
                    : left.EndTime.CompareTo(right.EndTime);

            });

            List<EditableWindow> normalized = new(windows.Count);

            for (int index = 0; index < windows.Count; index++)
            {

                EditableWindow current = windows[index];

                if (normalized.Count == 0)
                {

                    normalized.Add(current);
                    continue;

                }

                EditableWindow previous = normalized[^1];

                if (previous.PartId == current.PartId &&
                    current.StartTime <= previous.EndTime + BoundaryTolerance)
                {

                    normalized[^1] = new EditableWindow(
                        previous.PartId,
                        previous.StartTime,
                        Math.Max(previous.EndTime, current.EndTime));

                }
                else
                {

                    normalized.Add(current);

                }

            }

            return normalized;

        }

        private static void WriteNotes(
            SerializedProperty notesProperty,
            List<ChartNoteAuthoringData> notes)
        {

            notesProperty.arraySize = notes.Count;

            for (int index = 0; index < notes.Count; index++)
            {

                ChartNoteAuthoringData note = notes[index];
                SerializedProperty property = notesProperty.GetArrayElementAtIndex(index);
                note.WriteTo(property);

            }

        }

        private static void WriteWindows(SerializedProperty windowsProperty, List<EditableWindow> windows)
        {

            windowsProperty.arraySize = windows.Count;

            for (int index = 0; index < windows.Count; index++)
            {

                EditableWindow window = windows[index];
                SerializedProperty property = windowsProperty.GetArrayElementAtIndex(index);
                property.FindPropertyRelative("musicalPartId").stringValue = window.PartId;
                property.FindPropertyRelative("startTime").doubleValue = window.StartTime;
                property.FindPropertyRelative("endTime").doubleValue = window.EndTime;

            }

        }

    }

}
