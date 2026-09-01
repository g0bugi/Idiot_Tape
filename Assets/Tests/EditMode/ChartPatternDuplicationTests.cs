using System.Collections.Generic;
using IdiotTape.EditorTools;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace IdiotTape.Gameplay.Tests
{

    public sealed class ChartPatternDuplicationTests
    {

        [Test]
        public void PreviewRepeatsOnlySelectedPartUsingMusicalPositions()
        {

            PrototypeChart chart = CreateChart(
                new[]
                {

                    ("drum_a", 0.25d, 1, "drum"),
                    ("bass_a", 0.5d, 2, "bass"),
                    ("drum_b", 1d, 3, "drum")

                },
                false);

            ChartPatternDuplicationPreview preview = ChartPatternDuplication.CreatePreview(
                chart,
                "drum",
                0d,
                2d,
                2,
                2);

            Assert.That(preview.IsValid, Is.True, preview.Error);
            Assert.That(preview.SourceNoteCount, Is.EqualTo(2));
            Assert.That(preview.GeneratedNoteCount, Is.EqualTo(4));
            Assert.That(preview.TargetStartBar, Is.EqualTo(2));
            Assert.That(preview.TargetEndBar, Is.EqualTo(4));
            Assert.That(preview.GeneratedNotes[0].HitTime, Is.EqualTo(2.25d).Within(0.0000001d));
            Assert.That(preview.GeneratedNotes[1].HitTime, Is.EqualTo(3d).Within(0.0000001d));
            Assert.That(preview.GeneratedNotes[2].HitTime, Is.EqualTo(4.25d).Within(0.0000001d));
            Assert.That(preview.GeneratedNotes[3].HitTime, Is.EqualTo(5d).Within(0.0000001d));
            Assert.That(preview.GeneratedNotes[0].LaneIndex, Is.EqualTo(1));
            Assert.That(preview.GeneratedNotes[1].LaneIndex, Is.EqualTo(3));
            Assert.That(chart.Notes.Count, Is.EqualTo(3));
            Object.DestroyImmediate(chart);

        }

        [Test]
        public void AbortConflictModeDoesNotModifyOccupiedTarget()
        {

            PrototypeChart chart = CreateChart(
                new[]
                {

                    ("drum_source", 0.5d, 0, "drum"),
                    ("drum_target", 2.5d, 1, "drum")

                },
                false);

            bool applied = ChartPatternDuplication.TryApply(
                chart,
                "drum",
                0d,
                2d,
                2,
                1,
                ChartPatternConflictMode.AbortIfOccupied,
                false,
                out ChartPatternDuplicationPreview preview);

            Assert.That(applied, Is.False);
            Assert.That(preview.IsValid, Is.True, preview.Error);
            Assert.That(preview.ExistingTargetNoteCount, Is.EqualTo(1));
            Assert.That(chart.Notes.Count, Is.EqualTo(2));
            Assert.That(chart.Notes[1].Id, Is.EqualTo("drum_target"));
            Object.DestroyImmediate(chart);

        }

        [Test]
        public void ReplaceModePreservesOtherPartsAndCreatesValidUniqueNotesAndWindows()
        {

            PrototypeChart chart = CreateChart(
                new[]
                {

                    ("drum_source", 0.5d, 4, "drum"),
                    ("bass_source", 1d, 2, "bass"),
                    ("drum_old_target", 2.5d, 7, "drum"),
                    ("bass_target", 3d, 5, "bass")

                },
                true);

            bool applied = ChartPatternDuplication.TryApply(
                chart,
                "drum",
                0d,
                2d,
                2,
                2,
                ChartPatternConflictMode.ReplaceTargetPart,
                true,
                out ChartPatternDuplicationPreview preview);

            Assert.That(applied, Is.True, preview.Error);
            Assert.That(preview.GeneratedNoteCount, Is.EqualTo(2));
            Assert.That(FindNote(chart, "drum_old_target"), Is.Null);
            Assert.That(FindNote(chart, "bass_source"), Is.Not.Null);
            Assert.That(FindNote(chart, "bass_target"), Is.Not.Null);
            Assert.That(CountPartNotes(chart, "drum"), Is.EqualTo(3));

            HashSet<string> ids = new();
            double previousTime = double.NegativeInfinity;

            for (int index = 0; index < chart.Notes.Count; index++)
            {

                ChartNote note = chart.Notes[index];
                Assert.That(ids.Add(note.Id), Is.True);
                Assert.That(note.HitTime, Is.GreaterThanOrEqualTo(previousTime));
                previousTime = note.HitTime;

            }

            Assert.That(chart.IsPartActive("drum", 2.5d), Is.True);
            Assert.That(chart.IsPartActive("drum", 4.5d), Is.True);
            Assert.That(chart.TryValidate(out string error), Is.True, error);
            Object.DestroyImmediate(chart);

        }

        [Test]
        public void PreviewRejectsDifferentTargetMeter()
        {

            PrototypeChart chart = CreateChart(
                new[] { ("drum_source", 0.5d, 0, "drum") },
                false);
            AddTempoSection(chart, 2, 2d, 120d, 3, 4);

            ChartPatternDuplicationPreview preview = ChartPatternDuplication.CreatePreview(
                chart,
                "drum",
                0d,
                2d,
                2,
                1);

            Assert.That(preview.IsValid, Is.False);
            Assert.That(preview.Error, Does.Contain("박자표"));
            Object.DestroyImmediate(chart);

        }

        [Test]
        public void PreviewRejectsOverlappingSourceAndTarget()
        {

            PrototypeChart chart = CreateChart(
                new[] { ("drum_source", 0.5d, 0, "drum") },
                false);

            ChartPatternDuplicationPreview preview = ChartPatternDuplication.CreatePreview(
                chart,
                "drum",
                0d,
                2d,
                1,
                1);

            Assert.That(preview.IsValid, Is.False);
            Assert.That(preview.Error, Does.Contain("겹칩니다"));
            Object.DestroyImmediate(chart);

        }

        [Test]
        public void DuplicationPreservesSustainedTypeAndOffsetsItsEnd()
        {

            PrototypeChart chart = CreateChart(
                new[] { ("hold_source", 0.5d, 2, "drum") },
                false);
            SerializedObject serializedChart = new(chart);
            SerializedProperty hold = serializedChart.FindProperty("notes").GetArrayElementAtIndex(0);
            hold.FindPropertyRelative("noteType").enumValueIndex = (int)ChartNoteType.Hold;
            hold.FindPropertyRelative("endTime").doubleValue = 1.5d;
            serializedChart.ApplyModifiedPropertiesWithoutUndo();

            bool applied = ChartPatternDuplication.TryApply(
                chart,
                "drum",
                0d,
                2d,
                2,
                1,
                ChartPatternConflictMode.KeepExisting,
                false,
                out ChartPatternDuplicationPreview preview);

            Assert.That(applied, Is.True, preview.Error);
            Assert.That(chart.Notes.Count, Is.EqualTo(2));
            ChartNote duplicate = chart.Notes[1];
            Assert.That(duplicate.NoteType, Is.EqualTo(ChartNoteType.Hold));
            Assert.That(duplicate.HitTime, Is.EqualTo(2.5d).Within(0.0000001d));
            Assert.That(duplicate.EndTime, Is.EqualTo(3.5d).Within(0.0000001d));
            Object.DestroyImmediate(chart);

        }

        [Test]
        public void SustainedCopyKeepsMusicalDurationAcrossBpmChange()
        {

            PrototypeChart chart = CreateChart(
                new[] { ("hold_source", 0.5d, 2, "drum") },
                false);
            AddTempoSection(chart, 2, 2d, 60d, 4, 4);
            SerializedObject serializedChart = new(chart);
            SerializedProperty hold = serializedChart.FindProperty("notes").GetArrayElementAtIndex(0);
            hold.FindPropertyRelative("noteType").enumValueIndex = (int)ChartNoteType.Hold;
            hold.FindPropertyRelative("endTime").doubleValue = 1.5d;
            serializedChart.ApplyModifiedPropertiesWithoutUndo();

            bool applied = ChartPatternDuplication.TryApply(
                chart,
                "drum",
                0d,
                2d,
                2,
                1,
                ChartPatternConflictMode.KeepExisting,
                false,
                out ChartPatternDuplicationPreview preview);

            Assert.That(applied, Is.True, preview.Error);
            Assert.That(chart.Notes[1].HitTime, Is.EqualTo(3d).Within(0.0000001d));
            Assert.That(chart.Notes[1].EndTime, Is.EqualTo(5d).Within(0.0000001d));
            Object.DestroyImmediate(chart);

        }

        private static PrototypeChart CreateChart(
            (string id, double hitTime, int laneIndex, string partId)[] noteValues,
            bool addActivationWindows)
        {

            PrototypeChart chart = ScriptableObject.CreateInstance<PrototypeChart>();
            chart.name = "PatternTestChart";
            SerializedObject serializedChart = new(chart);
            SerializedProperty parts = serializedChart.FindProperty("musicalParts");
            parts.arraySize = 2;
            SetPart(parts.GetArrayElementAtIndex(0), "drum", "Drum", Color.white);
            SetPart(parts.GetArrayElementAtIndex(1), "bass", "Bass", Color.gray);
            SerializedProperty notes = serializedChart.FindProperty("notes");
            notes.arraySize = noteValues.Length;

            for (int index = 0; index < noteValues.Length; index++)
            {

                SerializedProperty note = notes.GetArrayElementAtIndex(index);
                note.FindPropertyRelative("id").stringValue = noteValues[index].id;
                note.FindPropertyRelative("hitTime").doubleValue = noteValues[index].hitTime;
                note.FindPropertyRelative("laneIndex").intValue = noteValues[index].laneIndex;
                note.FindPropertyRelative("musicalPartId").stringValue = noteValues[index].partId;

            }

            SerializedProperty tempoSections = serializedChart.FindProperty("tempoSections");
            tempoSections.arraySize = 1;
            SetTempoSection(tempoSections.GetArrayElementAtIndex(0), 1, 0d, 120d, 4, 4);

            if (addActivationWindows)
            {

                SerializedProperty windows = serializedChart.FindProperty("activationWindows");
                windows.arraySize = 2;
                SetWindow(windows.GetArrayElementAtIndex(0), "drum", 0d, 2d);
                SetWindow(windows.GetArrayElementAtIndex(1), "bass", 0d, 10d);

            }

            serializedChart.ApplyModifiedPropertiesWithoutUndo();
            return chart;

        }

        private static void AddTempoSection(
            PrototypeChart chart,
            int startBar,
            double startTime,
            double beatsPerMinute,
            int beatsPerBar,
            int beatUnit)
        {

            SerializedObject serializedChart = new(chart);
            SerializedProperty tempoSections = serializedChart.FindProperty("tempoSections");
            int index = tempoSections.arraySize;
            tempoSections.InsertArrayElementAtIndex(index);
            SetTempoSection(
                tempoSections.GetArrayElementAtIndex(index),
                startBar,
                startTime,
                beatsPerMinute,
                beatsPerBar,
                beatUnit);
            serializedChart.ApplyModifiedPropertiesWithoutUndo();

        }

        private static void SetPart(
            SerializedProperty property,
            string id,
            string displayName,
            Color color)
        {

            property.FindPropertyRelative("id").stringValue = id;
            property.FindPropertyRelative("displayName").stringValue = displayName;
            property.FindPropertyRelative("color").colorValue = color;

        }

        private static void SetTempoSection(
            SerializedProperty property,
            int startBar,
            double startTime,
            double beatsPerMinute,
            int beatsPerBar,
            int beatUnit)
        {

            property.FindPropertyRelative("startBar").intValue = startBar;
            property.FindPropertyRelative("startTime").doubleValue = startTime;
            property.FindPropertyRelative("beatsPerMinute").doubleValue = beatsPerMinute;
            property.FindPropertyRelative("beatsPerBar").intValue = beatsPerBar;
            property.FindPropertyRelative("beatUnit").intValue = beatUnit;

        }

        private static void SetWindow(
            SerializedProperty property,
            string partId,
            double startTime,
            double endTime)
        {

            property.FindPropertyRelative("musicalPartId").stringValue = partId;
            property.FindPropertyRelative("startTime").doubleValue = startTime;
            property.FindPropertyRelative("endTime").doubleValue = endTime;

        }

        private static ChartNote FindNote(PrototypeChart chart, string id)
        {

            for (int index = 0; index < chart.Notes.Count; index++)
            {

                if (chart.Notes[index].Id == id)
                {

                    return chart.Notes[index];

                }

            }

            return null;

        }

        private static int CountPartNotes(PrototypeChart chart, string partId)
        {

            int count = 0;

            for (int index = 0; index < chart.Notes.Count; index++)
            {

                if (chart.Notes[index].MusicalPartId == partId)
                {

                    count++;

                }

            }

            return count;

        }

    }

}
