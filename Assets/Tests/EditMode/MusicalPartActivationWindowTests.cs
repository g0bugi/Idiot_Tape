using NUnit.Framework;
using IdiotTape.EditorTools;
using UnityEditor;
using UnityEngine;

namespace IdiotTape.Gameplay.Tests
{

    public sealed class MusicalPartActivationWindowTests
    {

        [Test]
        public void WindowUsesInclusiveStartAndExclusiveEnd()
        {

            MusicalPartActivationWindow window = JsonUtility.FromJson<MusicalPartActivationWindow>(
                "{\"musicalPartId\":\"drum\",\"startTime\":2.0,\"endTime\":4.0}");

            Assert.That(window.Contains(2d), Is.True);
            Assert.That(window.Contains(3.999d), Is.True);
            Assert.That(window.Contains(4d), Is.False);

        }

        [Test]
        public void ChartValidationRejectsOverlappingWindowsForSamePart()
        {

            PrototypeChart chart = ScriptableObject.CreateInstance<PrototypeChart>();
            ConfigureChart(
                chart,
                new[] { "drum" },
                new[]
                {

                    ("drum", 1d, 3d),
                    ("drum", 2d, 4d)

                });

            bool valid = chart.TryValidate(out string error);

            Assert.That(valid, Is.False);
            Assert.That(error, Does.Contain("overlaps"));
            Object.DestroyImmediate(chart);

        }

        [Test]
        public void NormalizeMergesOnlyWindowsForTheSamePart()
        {

            PrototypeChart chart = ScriptableObject.CreateInstance<PrototypeChart>();
            ConfigureChart(
                chart,
                new[] { "drum", "bass" },
                new[]
                {

                    ("drum", 1d, 3d),
                    ("drum", 2d, 4d),
                    ("bass", 2d, 4d)

                });

            int removedCount = ChartActivationWindowUtility.Normalize(chart);

            Assert.That(removedCount, Is.EqualTo(1));
            Assert.That(chart.ActivationWindows.Count, Is.EqualTo(2));
            Assert.That(chart.ActivationWindows[1].StartTime, Is.EqualTo(1d));
            Assert.That(chart.ActivationWindows[1].EndTime, Is.EqualTo(4d));
            Object.DestroyImmediate(chart);

        }

        [Test]
        public void ChartAllowsVisibleNotesOutsideActivationWindows()
        {

            PrototypeChart chart = ScriptableObject.CreateInstance<PrototypeChart>();
            ConfigureChart(
                chart,
                new[] { "drum" },
                new[] { ("drum", 1d, 2d) });
            AddNote(chart, "inactive_note", 3d, "drum");

            bool valid = chart.TryValidate(out string error);

            Assert.That(valid, Is.True, error);
            Assert.That(chart.IsNotePlayable(chart.Notes[0]), Is.False);
            Object.DestroyImmediate(chart);

        }

        [Test]
        public void NotePlayabilityUsesItsHitTime()
        {

            PrototypeChart chart = ScriptableObject.CreateInstance<PrototypeChart>();
            ConfigureChart(
                chart,
                new[] { "drum" },
                new[] { ("drum", 2d, 4d) });
            AddNote(chart, "active_note", 2d, "drum");

            Assert.That(chart.IsNotePlayable(chart.Notes[0]), Is.True);
            Object.DestroyImmediate(chart);

        }

        [Test]
        public void RemovePartLeavesOtherPartWindowsIntact()
        {

            PrototypeChart chart = ScriptableObject.CreateInstance<PrototypeChart>();
            ConfigureChart(
                chart,
                new[] { "drum", "bass" },
                new[]
                {

                    ("drum", 1d, 2d),
                    ("bass", 2d, 3d),
                    ("drum", 3d, 4d)

                });

            int removedCount = ChartActivationWindowUtility.RemovePart(chart, "drum");

            Assert.That(removedCount, Is.EqualTo(2));
            Assert.That(chart.ActivationWindows.Count, Is.EqualTo(1));
            Assert.That(chart.ActivationWindows[0].MusicalPartId, Is.EqualTo("bass"));
            Object.DestroyImmediate(chart);

        }

        private static void ConfigureChart(
            PrototypeChart chart,
            string[] partIds,
            (string partId, double startTime, double endTime)[] windowValues)
        {

            SerializedObject serializedChart = new(chart);
            serializedChart.FindProperty("songEventPath").stringValue = "event:/test";
            serializedChart.FindProperty("laneCount").intValue = 8;
            SerializedProperty parts = serializedChart.FindProperty("musicalParts");
            parts.arraySize = partIds.Length;

            for (int index = 0; index < partIds.Length; index++)
            {

                SerializedProperty part = parts.GetArrayElementAtIndex(index);
                part.FindPropertyRelative("id").stringValue = partIds[index];
                part.FindPropertyRelative("displayName").stringValue = partIds[index];

            }

            SerializedProperty windows = serializedChart.FindProperty("activationWindows");
            windows.arraySize = windowValues.Length;

            for (int index = 0; index < windowValues.Length; index++)
            {

                SerializedProperty window = windows.GetArrayElementAtIndex(index);
                window.FindPropertyRelative("musicalPartId").stringValue = windowValues[index].partId;
                window.FindPropertyRelative("startTime").doubleValue = windowValues[index].startTime;
                window.FindPropertyRelative("endTime").doubleValue = windowValues[index].endTime;

            }

            serializedChart.ApplyModifiedPropertiesWithoutUndo();

        }

        private static void AddNote(
            PrototypeChart chart,
            string noteId,
            double hitTime,
            string partId)
        {

            SerializedObject serializedChart = new(chart);
            SerializedProperty notes = serializedChart.FindProperty("notes");
            notes.arraySize = 1;
            SerializedProperty note = notes.GetArrayElementAtIndex(0);
            note.FindPropertyRelative("id").stringValue = noteId;
            note.FindPropertyRelative("hitTime").doubleValue = hitTime;
            note.FindPropertyRelative("laneIndex").intValue = 0;
            note.FindPropertyRelative("musicalPartId").stringValue = partId;
            serializedChart.ApplyModifiedPropertiesWithoutUndo();

        }

    }

}
