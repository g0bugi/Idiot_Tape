using System;
using System.Collections.Generic;
using IdiotTape.Gameplay;
using UnityEditor;

namespace IdiotTape.EditorTools
{

    public readonly struct AppliedNoteQuantizationSummary
    {

        public AppliedNoteQuantizationSummary(
            int candidateCount,
            int quantizedCount,
            int outsideWindowCount,
            int changedCount,
            double minimumCorrectionSeconds,
            double maximumCorrectionSeconds)
        {

            CandidateCount = candidateCount;
            QuantizedCount = quantizedCount;
            OutsideWindowCount = outsideWindowCount;
            ChangedCount = changedCount;
            MinimumCorrectionSeconds = minimumCorrectionSeconds;
            MaximumCorrectionSeconds = maximumCorrectionSeconds;

        }

        public int CandidateCount { get; }
        public int QuantizedCount { get; }
        public int OutsideWindowCount { get; }
        public int ChangedCount { get; }
        public double MinimumCorrectionSeconds { get; }
        public double MaximumCorrectionSeconds { get; }

    }

    public static class ChartAuthoringNoteUtility
    {

        private sealed class QuantizedNote
        {

            public int ChartIndex;
            public double OriginalTime;
            public double CorrectedTime;

        }

        private sealed class EditableNote
        {

            public string Id;
            public double HitTime;
            public int LaneIndex;
            public string MusicalPartId;

        }

        public static int CountNotesInRange(
            PrototypeChart chart,
            string partId,
            double startTime,
            double endTime)
        {

            if (chart == null ||
                string.IsNullOrWhiteSpace(partId) ||
                endTime <= startTime)
            {

                return 0;

            }

            int count = 0;

            for (int index = 0; index < chart.Notes.Count; index++)
            {

                ChartNote note = chart.Notes[index];

                if (note.MusicalPartId == partId &&
                    note.HitTime >= startTime &&
                    note.HitTime < endTime)
                {

                    count++;

                }

            }

            return count;

        }

        public static bool DeleteNote(PrototypeChart chart, string noteId)
        {

            if (chart == null || string.IsNullOrWhiteSpace(noteId))
            {

                return false;

            }

            for (int index = 0; index < chart.Notes.Count; index++)
            {

                if (chart.Notes[index].Id != noteId)
                {

                    continue;

                }

                Undo.RecordObject(chart, "Delete applied chart note");
                SerializedObject serializedChart = new(chart);
                SerializedProperty notes = serializedChart.FindProperty("notes");
                notes.DeleteArrayElementAtIndex(index);
                serializedChart.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(chart);
                return true;

            }

            return false;

        }

        public static int DeleteNotesInRange(
            PrototypeChart chart,
            string partId,
            double startTime,
            double endTime)
        {

            int deleteCount = CountNotesInRange(chart, partId, startTime, endTime);

            if (deleteCount == 0)
            {

                return 0;

            }

            Undo.RecordObject(chart, "Delete applied chart notes in range");
            SerializedObject serializedChart = new(chart);
            SerializedProperty notes = serializedChart.FindProperty("notes");

            for (int index = chart.Notes.Count - 1; index >= 0; index--)
            {

                ChartNote note = chart.Notes[index];

                if (note.MusicalPartId == partId &&
                    note.HitTime >= startTime &&
                    note.HitTime < endTime)
                {

                    notes.DeleteArrayElementAtIndex(index);

                }

            }

            serializedChart.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(chart);
            return deleteCount;

        }

        public static AppliedNoteQuantizationSummary PreviewQuantization(
            PrototypeChart chart,
            string partId,
            double startTime,
            double endTime,
            int subdivisionsPerBeat,
            double maximumCorrectionSeconds,
            double strength,
            double inputAdvanceSeconds,
            double chordGroupingSeconds)
        {

            CalculateQuantizedNotes(
                chart,
                partId,
                startTime,
                endTime,
                subdivisionsPerBeat,
                maximumCorrectionSeconds,
                strength,
                inputAdvanceSeconds,
                chordGroupingSeconds,
                out AppliedNoteQuantizationSummary summary);
            return summary;

        }

        public static AppliedNoteQuantizationSummary QuantizeNotes(
            PrototypeChart chart,
            string partId,
            double startTime,
            double endTime,
            int subdivisionsPerBeat,
            double maximumCorrectionSeconds,
            double strength,
            double inputAdvanceSeconds,
            double chordGroupingSeconds)
        {

            List<QuantizedNote> quantizedNotes = CalculateQuantizedNotes(
                chart,
                partId,
                startTime,
                endTime,
                subdivisionsPerBeat,
                maximumCorrectionSeconds,
                strength,
                inputAdvanceSeconds,
                chordGroupingSeconds,
                out AppliedNoteQuantizationSummary summary);

            if (chart == null || summary.ChangedCount == 0)
            {

                return summary;

            }

            Dictionary<int, double> correctedTimes = new(quantizedNotes.Count);

            for (int index = 0; index < quantizedNotes.Count; index++)
            {

                correctedTimes.Add(quantizedNotes[index].ChartIndex, quantizedNotes[index].CorrectedTime);

            }

            List<EditableNote> editedNotes = new(chart.Notes.Count);

            for (int index = 0; index < chart.Notes.Count; index++)
            {

                ChartNote note = chart.Notes[index];
                editedNotes.Add(new EditableNote
                {

                    Id = note.Id,
                    HitTime = correctedTimes.TryGetValue(index, out double correctedTime)
                        ? correctedTime
                        : note.HitTime,
                    LaneIndex = note.LaneIndex,
                    MusicalPartId = note.MusicalPartId

                });

            }

            editedNotes.Sort((left, right) =>
            {

                int timeComparison = left.HitTime.CompareTo(right.HitTime);
                return timeComparison != 0
                    ? timeComparison
                    : string.CompareOrdinal(left.Id, right.Id);

            });

            Undo.RecordObject(chart, "Quantize applied chart notes");
            SerializedObject serializedChart = new(chart);
            SerializedProperty notesProperty = serializedChart.FindProperty("notes");
            notesProperty.arraySize = editedNotes.Count;

            for (int index = 0; index < editedNotes.Count; index++)
            {

                EditableNote note = editedNotes[index];
                SerializedProperty noteProperty = notesProperty.GetArrayElementAtIndex(index);
                noteProperty.FindPropertyRelative("id").stringValue = note.Id;
                noteProperty.FindPropertyRelative("hitTime").doubleValue = note.HitTime;
                noteProperty.FindPropertyRelative("laneIndex").intValue = note.LaneIndex;
                noteProperty.FindPropertyRelative("musicalPartId").stringValue = note.MusicalPartId;

            }

            serializedChart.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(chart);
            return summary;

        }

        private static List<QuantizedNote> CalculateQuantizedNotes(
            PrototypeChart chart,
            string partId,
            double startTime,
            double endTime,
            int subdivisionsPerBeat,
            double maximumCorrectionSeconds,
            double strength,
            double inputAdvanceSeconds,
            double chordGroupingSeconds,
            out AppliedNoteQuantizationSummary summary)
        {

            List<QuantizedNote> candidates = new();

            if (chart == null ||
                chart.TempoSections.Count == 0 ||
                string.IsNullOrWhiteSpace(partId) ||
                endTime <= startTime)
            {

                summary = new AppliedNoteQuantizationSummary();
                return candidates;

            }

            for (int index = 0; index < chart.Notes.Count; index++)
            {

                ChartNote note = chart.Notes[index];

                if (note.MusicalPartId != partId ||
                    note.HitTime < startTime ||
                    note.HitTime >= endTime)
                {

                    continue;

                }

                candidates.Add(new QuantizedNote
                {

                    ChartIndex = index,
                    OriginalTime = note.HitTime,
                    CorrectedTime = note.HitTime

                });

            }

            int quantizedCount = 0;
            int outsideWindowCount = 0;
            double safeChordGroupingSeconds = Math.Max(0d, chordGroupingSeconds);
            int candidateIndex = 0;

            while (candidateIndex < candidates.Count)
            {

                int groupEnd = candidateIndex + 1;
                double groupStartTime = candidates[candidateIndex].OriginalTime;

                while (groupEnd < candidates.Count &&
                       candidates[groupEnd].OriginalTime - groupStartTime <= safeChordGroupingSeconds)
                {

                    groupEnd++;

                }

                int middleIndex = candidateIndex + (groupEnd - candidateIndex - 1) / 2;
                ChartQuantizationResult result = ChartQuantization.Quantize(
                    chart.TempoSections,
                    candidates[middleIndex].OriginalTime,
                    subdivisionsPerBeat,
                    maximumCorrectionSeconds,
                    strength,
                    inputAdvanceSeconds);

                if (result.WasQuantized)
                {

                    quantizedCount += groupEnd - candidateIndex;

                }
                else
                {

                    outsideWindowCount += groupEnd - candidateIndex;

                }

                for (int index = candidateIndex; index < groupEnd; index++)
                {

                    candidates[index].CorrectedTime = result.CorrectedTime;

                }

                candidateIndex = groupEnd;

            }

            int changedCount = 0;
            double minimumCorrectionSeconds = 0d;
            double maximumCorrectionSecondsResult = 0d;

            for (int index = 0; index < candidates.Count; index++)
            {

                double correctionSeconds = candidates[index].CorrectedTime - candidates[index].OriginalTime;

                if (Math.Abs(correctionSeconds) <= 0.0000001d)
                {

                    continue;

                }

                if (changedCount == 0)
                {

                    minimumCorrectionSeconds = correctionSeconds;
                    maximumCorrectionSecondsResult = correctionSeconds;

                }
                else
                {

                    minimumCorrectionSeconds = Math.Min(minimumCorrectionSeconds, correctionSeconds);
                    maximumCorrectionSecondsResult = Math.Max(maximumCorrectionSecondsResult, correctionSeconds);

                }

                changedCount++;

            }

            summary = new AppliedNoteQuantizationSummary(
                candidates.Count,
                quantizedCount,
                outsideWindowCount,
                changedCount,
                minimumCorrectionSeconds,
                maximumCorrectionSecondsResult);
            return candidates;

        }

    }

}
