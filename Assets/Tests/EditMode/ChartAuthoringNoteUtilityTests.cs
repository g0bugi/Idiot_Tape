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

    }

}
