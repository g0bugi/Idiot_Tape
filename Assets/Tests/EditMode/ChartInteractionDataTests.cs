using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace IdiotTape.Gameplay.Tests
{

    public sealed class ChartInteractionDataTests
    {

        [Test]
        public void LegacyFourFieldNoteDefaultsToTap()
        {

            ChartNote note = JsonUtility.FromJson<ChartNote>(
                "{\"id\":\"legacy\",\"hitTime\":1.25,\"laneIndex\":3," +
                "\"musicalPartId\":\"part\"}");

            Assert.That(note.NoteType, Is.EqualTo(ChartNoteType.Tap));
            Assert.That(note.EndTime, Is.EqualTo(1.25d));
            Assert.That(note.EndLaneIndex, Is.EqualTo(3));

        }

        [Test]
        public void AcceptedInteractionShapesValidateAndDurationUsesLatestEnd()
        {

            PrototypeChart chart = CreateBaseChart(5);
            SerializedObject serializedChart = new(chart);
            SerializedProperty notes = serializedChart.FindProperty("notes");

            SetCommon(notes.GetArrayElementAtIndex(0), "tap", 0.5d, 0, ChartNoteType.Tap);

            SerializedProperty hold = notes.GetArrayElementAtIndex(1);
            SetCommon(hold, "hold", 1d, 1, ChartNoteType.Hold);
            hold.FindPropertyRelative("endTime").doubleValue = 2d;

            SerializedProperty slide = notes.GetArrayElementAtIndex(2);
            SetCommon(slide, "slide", 3d, 1, ChartNoteType.Slide);
            SerializedProperty slideNodes = slide.FindPropertyRelative("slideNodes");
            slideNodes.arraySize = 2;
            SetPathNode(slideNodes.GetArrayElementAtIndex(0), 3.5d, 2);
            SetPathNode(slideNodes.GetArrayElementAtIndex(1), 4d, 2);

            SerializedProperty flick = notes.GetArrayElementAtIndex(3);
            SetCommon(flick, "flick", 5d, 2, ChartNoteType.Flick);
            flick.FindPropertyRelative("endLaneIndex").intValue = 4;

            SerializedProperty banana = notes.GetArrayElementAtIndex(4);
            SetCommon(banana, "banana", 6d, 0, ChartNoteType.Banana);
            banana.FindPropertyRelative("endTime").doubleValue = 8d;
            banana.FindPropertyRelative("endLaneIndex").intValue = 7;
            SerializedProperty handles = banana.FindPropertyRelative("bananaCurveHandles");
            handles.arraySize = 1;
            handles.GetArrayElementAtIndex(0).FindPropertyRelative("normalizedTime").floatValue = 0.5f;
            handles.GetArrayElementAtIndex(0).FindPropertyRelative("normalizedX").floatValue = 0.7f;
            SerializedProperty checkpoints = banana.FindPropertyRelative("bananaCheckpoints");
            checkpoints.arraySize = 2;
            SetCheckpoint(checkpoints.GetArrayElementAtIndex(0), 6.5d, 0.35f);
            SetCheckpoint(checkpoints.GetArrayElementAtIndex(1), 7.5d, 0.65f);
            banana.FindPropertyRelative("bananaMaximumBonusCombo").intValue = 4;
            serializedChart.ApplyModifiedPropertiesWithoutUndo();

            Assert.That(chart.TryValidate(out string error), Is.True, error);
            Assert.That(chart.Duration, Is.EqualTo(9d));
            Assert.That(chart.Notes[2].EndTime, Is.EqualTo(4d));
            Assert.That(chart.Notes[2].EndLaneIndex, Is.EqualTo(2));

            Object.DestroyImmediate(chart);

        }

        [Test]
        public void FlickRejectsEqualStartAndEndLane()
        {

            PrototypeChart chart = CreateBaseChart(1);
            SerializedObject serializedChart = new(chart);
            SerializedProperty flick = serializedChart.FindProperty("notes").GetArrayElementAtIndex(0);
            SetCommon(flick, "invalid_flick", 1d, 3, ChartNoteType.Flick);
            flick.FindPropertyRelative("endLaneIndex").intValue = 3;
            serializedChart.ApplyModifiedPropertiesWithoutUndo();

            Assert.That(chart.TryValidate(out string error), Is.False);
            Assert.That(error, Does.Contain("different lane"));

            Object.DestroyImmediate(chart);

        }

        [Test]
        public void SlideTerminalFlickRequiresFinalLaneTransition()
        {

            PrototypeChart chart = CreateBaseChart(1);
            SerializedObject serializedChart = new(chart);
            SerializedProperty slide = serializedChart.FindProperty("notes").GetArrayElementAtIndex(0);
            SetCommon(slide, "invalid_slide", 1d, 2, ChartNoteType.Slide);
            slide.FindPropertyRelative("slideEndBehavior").enumValueIndex = (int)SlideEndBehavior.Flick;
            SerializedProperty nodes = slide.FindPropertyRelative("slideNodes");
            nodes.arraySize = 2;
            SetPathNode(nodes.GetArrayElementAtIndex(0), 1.5d, 4);
            SetPathNode(nodes.GetArrayElementAtIndex(1), 2d, 4);
            serializedChart.ApplyModifiedPropertiesWithoutUndo();

            Assert.That(chart.TryValidate(out string error), Is.False);
            Assert.That(error, Does.Contain("terminal flick"));

            Object.DestroyImmediate(chart);

        }

        private static PrototypeChart CreateBaseChart(int noteCount)
        {

            PrototypeChart chart = ScriptableObject.CreateInstance<PrototypeChart>();
            SerializedObject serializedChart = new(chart);
            serializedChart.FindProperty("songEventPath").stringValue = "event:/test";
            serializedChart.FindProperty("laneCount").intValue = 8;

            SerializedProperty tempoSections = serializedChart.FindProperty("tempoSections");
            tempoSections.arraySize = 1;
            SerializedProperty tempo = tempoSections.GetArrayElementAtIndex(0);
            tempo.FindPropertyRelative("startBar").intValue = 1;
            tempo.FindPropertyRelative("startTime").doubleValue = 0d;
            tempo.FindPropertyRelative("beatsPerMinute").doubleValue = 120d;
            tempo.FindPropertyRelative("beatsPerBar").intValue = 4;
            tempo.FindPropertyRelative("beatUnit").intValue = 4;

            SerializedProperty parts = serializedChart.FindProperty("musicalParts");
            parts.arraySize = 1;
            SerializedProperty part = parts.GetArrayElementAtIndex(0);
            part.FindPropertyRelative("id").stringValue = "part";
            part.FindPropertyRelative("displayName").stringValue = "Part";

            SerializedProperty windows = serializedChart.FindProperty("activationWindows");
            windows.arraySize = 1;
            SerializedProperty window = windows.GetArrayElementAtIndex(0);
            window.FindPropertyRelative("musicalPartId").stringValue = "part";
            window.FindPropertyRelative("startTime").doubleValue = 0d;
            window.FindPropertyRelative("endTime").doubleValue = 100d;

            serializedChart.FindProperty("notes").arraySize = noteCount;
            serializedChart.ApplyModifiedPropertiesWithoutUndo();
            return chart;

        }

        private static void SetCommon(
            SerializedProperty note,
            string id,
            double hitTime,
            int laneIndex,
            ChartNoteType noteType)
        {

            note.FindPropertyRelative("id").stringValue = id;
            note.FindPropertyRelative("hitTime").doubleValue = hitTime;
            note.FindPropertyRelative("laneIndex").intValue = laneIndex;
            note.FindPropertyRelative("musicalPartId").stringValue = "part";
            note.FindPropertyRelative("noteType").enumValueIndex = (int)noteType;
            note.FindPropertyRelative("slideEndBehavior").enumValueIndex = (int)SlideEndBehavior.Normal;
            note.FindPropertyRelative("slideNodes").arraySize = 0;
            note.FindPropertyRelative("bananaCurveHandles").arraySize = 0;
            note.FindPropertyRelative("bananaCheckpoints").arraySize = 0;

        }

        private static void SetPathNode(SerializedProperty node, double time, int laneIndex)
        {

            node.FindPropertyRelative("time").doubleValue = time;
            node.FindPropertyRelative("laneIndex").intValue = laneIndex;

        }

        private static void SetCheckpoint(SerializedProperty checkpoint, double time, float normalizedX)
        {

            checkpoint.FindPropertyRelative("time").doubleValue = time;
            checkpoint.FindPropertyRelative("normalizedX").floatValue = normalizedX;

        }

    }

}
