using IdiotTape.EditorTools;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace IdiotTape.Gameplay.Tests
{

    public sealed class ChartAuthoringNoteUtilityTests
    {

        [Test]
        public void DeleteNoteRemovesOnlyMatchingStableId()
        {

            PrototypeChart chart = CreateChart(
                ("drum_a", 1d, 0, "drum"),
                ("drum_b", 2d, 1, "drum"),
                ("bass_a", 3d, 2, "bass"));

            bool deleted = ChartAuthoringNoteUtility.DeleteNote(chart, "drum_b");

            Assert.That(deleted, Is.True);
            Assert.That(chart.Notes.Count, Is.EqualTo(2));
            Assert.That(chart.Notes[0].Id, Is.EqualTo("drum_a"));
            Assert.That(chart.Notes[1].Id, Is.EqualTo("bass_a"));
            Object.DestroyImmediate(chart);

        }

        [Test]
        public void DeleteNotesInRangeUsesSelectedPartAndExclusiveEnd()
        {

            PrototypeChart chart = CreateChart(
                ("drum_before", 0.999d, 0, "drum"),
                ("drum_start", 1d, 1, "drum"),
                ("bass_inside", 1.5d, 2, "bass"),
                ("drum_inside", 1.999d, 3, "drum"),
                ("drum_end", 2d, 4, "drum"));

            int deletedCount = ChartAuthoringNoteUtility.DeleteNotesInRange(
                chart,
                "drum",
                1d,
                2d);

            Assert.That(deletedCount, Is.EqualTo(2));
            Assert.That(chart.Notes.Count, Is.EqualTo(3));
            Assert.That(chart.Notes[0].Id, Is.EqualTo("drum_before"));
            Assert.That(chart.Notes[1].Id, Is.EqualTo("bass_inside"));
            Assert.That(chart.Notes[2].Id, Is.EqualTo("drum_end"));
            Object.DestroyImmediate(chart);

        }

        [Test]
        public void EmptyOrInvalidRangeDoesNotModifyChart()
        {

            PrototypeChart chart = CreateChart(("drum_a", 1d, 0, "drum"));

            int deletedCount = ChartAuthoringNoteUtility.DeleteNotesInRange(
                chart,
                "drum",
                2d,
                1d);

            Assert.That(deletedCount, Is.Zero);
            Assert.That(chart.Notes.Count, Is.EqualTo(1));
            Object.DestroyImmediate(chart);

        }

        [Test]
        public void PreviewQuantizationFiltersPartAndRangeWithoutChangingChart()
        {

            PrototypeChart chart = CreateChart(
                ("drum_inside_a", 1.02d, 0, "drum"),
                ("drum_inside_b", 1.03d, 1, "drum"),
                ("bass_inside", 1.04d, 2, "bass"),
                ("drum_outside", 2.02d, 3, "drum"));
            AddTempoSection(chart, 120d);

            AppliedNoteQuantizationSummary result = ChartAuthoringNoteUtility.PreviewQuantization(
                chart,
                "drum",
                1d,
                2d,
                4,
                0.06d,
                1d,
                0d,
                0.025d);

            Assert.That(result.CandidateCount, Is.EqualTo(2));
            Assert.That(result.QuantizedCount, Is.EqualTo(2));
            Assert.That(result.ChangedCount, Is.EqualTo(2));
            Assert.That(chart.Notes[0].HitTime, Is.EqualTo(1.02d));
            Assert.That(chart.Notes[1].HitTime, Is.EqualTo(1.03d));
            Assert.That(chart.Notes[2].HitTime, Is.EqualTo(1.04d));
            Assert.That(chart.Notes[3].HitTime, Is.EqualTo(2.02d));
            Object.DestroyImmediate(chart);

        }

        [Test]
        public void QuantizeNotesPreservesNoteDataAndRestoresGlobalTimeOrder()
        {

            PrototypeChart chart = CreateChart(
                ("bass", 1.01d, 2, "bass"),
                ("drum", 1.04d, 5, "drum"));
            AddTempoSection(chart, 120d);

            AppliedNoteQuantizationSummary result = ChartAuthoringNoteUtility.QuantizeNotes(
                chart,
                "drum",
                0d,
                double.PositiveInfinity,
                4,
                0.06d,
                1d,
                0d,
                0.025d);

            Assert.That(result.ChangedCount, Is.EqualTo(1));
            Assert.That(chart.Notes[0].Id, Is.EqualTo("drum"));
            Assert.That(chart.Notes[0].HitTime, Is.EqualTo(1d).Within(0.0000001d));
            Assert.That(chart.Notes[0].LaneIndex, Is.EqualTo(5));
            Assert.That(chart.Notes[0].MusicalPartId, Is.EqualTo("drum"));
            Assert.That(chart.Notes[1].Id, Is.EqualTo("bass"));
            Assert.That(chart.Notes[1].HitTime, Is.EqualTo(1.01d));
            Object.DestroyImmediate(chart);

        }

        private static PrototypeChart CreateChart(
            params (string id, double hitTime, int laneIndex, string partId)[] noteValues)
        {

            PrototypeChart chart = ScriptableObject.CreateInstance<PrototypeChart>();
            SerializedObject serializedChart = new(chart);
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

            serializedChart.ApplyModifiedPropertiesWithoutUndo();
            return chart;

        }

        private static void AddTempoSection(PrototypeChart chart, double beatsPerMinute)
        {

            SerializedObject serializedChart = new(chart);
            SerializedProperty tempoSections = serializedChart.FindProperty("tempoSections");
            tempoSections.arraySize = 1;
            SerializedProperty section = tempoSections.GetArrayElementAtIndex(0);
            section.FindPropertyRelative("startBar").intValue = 1;
            section.FindPropertyRelative("startTime").doubleValue = 0d;
            section.FindPropertyRelative("beatsPerMinute").doubleValue = beatsPerMinute;
            section.FindPropertyRelative("beatsPerBar").intValue = 4;
            section.FindPropertyRelative("beatUnit").intValue = 4;
            serializedChart.ApplyModifiedPropertiesWithoutUndo();

        }

    }

}
