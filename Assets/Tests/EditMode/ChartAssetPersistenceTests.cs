using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace IdiotTape.Gameplay.Tests
{

    public sealed class ChartAssetPersistenceTests
    {

        private const string TemporaryChartPath =
            "Assets/Tests/EditMode/__ChartAssetPersistenceVerification.asset";

        [Test]
        public void DirtyChartAssetPersistsAfterSaveAndReload()
        {

            AssetDatabase.DeleteAsset(TemporaryChartPath);

            try
            {

                PrototypeChart chart = ScriptableObject.CreateInstance<PrototypeChart>();
                AssetDatabase.CreateAsset(chart, TemporaryChartPath);

                SerializedObject serializedChart = new(chart);
                SerializedProperty laneCount = serializedChart.FindProperty("laneCount");
                Assert.That(laneCount, Is.Not.Null);
                laneCount.intValue = 6;
                serializedChart.ApplyModifiedProperties();

                AssetDatabase.SaveAssetIfDirty(chart);
                AssetDatabase.ImportAsset(
                    TemporaryChartPath,
                    ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);

                PrototypeChart reloadedChart = AssetDatabase.LoadAssetAtPath<PrototypeChart>(
                    TemporaryChartPath);
                Assert.That(reloadedChart, Is.Not.Null);
                Assert.That(reloadedChart.LaneCount, Is.EqualTo(6));

            }
            finally
            {

                AssetDatabase.DeleteAsset(TemporaryChartPath);

            }

        }

        [Test]
        public void TapHoldSlideAndFlickPersistAfterSaveAndReload()
        {

            AssetDatabase.DeleteAsset(TemporaryChartPath);

            try
            {

                PrototypeChart chart = ScriptableObject.CreateInstance<PrototypeChart>();
                AssetDatabase.CreateAsset(chart, TemporaryChartPath);
                SerializedObject serializedChart = new(chart);
                serializedChart.FindProperty("songEventPath").stringValue = "event:/test";
                serializedChart.FindProperty("laneCount").intValue = 8;
                SetTempoAndPart(serializedChart);
                SerializedProperty notes = serializedChart.FindProperty("notes");
                notes.arraySize = 4;
                SetCommonNote(notes.GetArrayElementAtIndex(0), "tap", 1d, 0, ChartNoteType.Tap);

                SerializedProperty hold = notes.GetArrayElementAtIndex(1);
                SetCommonNote(hold, "hold", 2d, 1, ChartNoteType.Hold);
                hold.FindPropertyRelative("endTime").doubleValue = 3d;
                hold.FindPropertyRelative("endLaneIndex").intValue = 1;

                SerializedProperty slide = notes.GetArrayElementAtIndex(2);
                SetCommonNote(slide, "slide", 4d, 2, ChartNoteType.Slide);
                SerializedProperty slideNodes = slide.FindPropertyRelative("slideNodes");
                slideNodes.arraySize = 2;
                SetPathNode(slideNodes.GetArrayElementAtIndex(0), 4.5d, 4);
                SetPathNode(slideNodes.GetArrayElementAtIndex(1), 5d, 4);

                SerializedProperty flick = notes.GetArrayElementAtIndex(3);
                SetCommonNote(flick, "flick", 6d, 5, ChartNoteType.Flick);
                flick.FindPropertyRelative("endLaneIndex").intValue = 7;
                serializedChart.ApplyModifiedProperties();
                Assert.That(chart.TryValidate(out string validationError), Is.True, validationError);

                AssetDatabase.SaveAssetIfDirty(chart);
                AssetDatabase.ImportAsset(
                    TemporaryChartPath,
                    ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);

                PrototypeChart reloadedChart = AssetDatabase.LoadAssetAtPath<PrototypeChart>(
                    TemporaryChartPath);
                Assert.That(reloadedChart, Is.Not.Null);
                Assert.That(reloadedChart.Notes.Count, Is.EqualTo(4));
                Assert.That(reloadedChart.Notes[0].NoteType, Is.EqualTo(ChartNoteType.Tap));
                Assert.That(reloadedChart.Notes[1].NoteType, Is.EqualTo(ChartNoteType.Hold));
                Assert.That(reloadedChart.Notes[1].EndTime, Is.EqualTo(3d));
                Assert.That(reloadedChart.Notes[2].NoteType, Is.EqualTo(ChartNoteType.Slide));
                Assert.That(reloadedChart.Notes[2].SlideNodes.Count, Is.EqualTo(2));
                Assert.That(reloadedChart.Notes[2].SlideNodes[0].LaneIndex, Is.EqualTo(4));
                Assert.That(reloadedChart.Notes[3].NoteType, Is.EqualTo(ChartNoteType.Flick));
                Assert.That(reloadedChart.Notes[3].EndLaneIndex, Is.EqualTo(7));

            }
            finally
            {

                AssetDatabase.DeleteAsset(TemporaryChartPath);

            }

        }

        private static void SetTempoAndPart(SerializedObject serializedChart)
        {

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
            window.FindPropertyRelative("endTime").doubleValue = 10d;

        }

        private static void SetCommonNote(
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
            note.FindPropertyRelative("endTime").doubleValue = hitTime;
            note.FindPropertyRelative("endLaneIndex").intValue = laneIndex;
            note.FindPropertyRelative("slideEndBehavior").enumValueIndex =
                (int)SlideEndBehavior.Normal;
            note.FindPropertyRelative("slideNodes").arraySize = 0;
            note.FindPropertyRelative("bananaCurveHandles").arraySize = 0;
            note.FindPropertyRelative("bananaCheckpoints").arraySize = 0;
            note.FindPropertyRelative("bananaMaximumBonusCombo").intValue = 4;

        }

        private static void SetPathNode(
            SerializedProperty node,
            double time,
            int laneIndex)
        {

            node.FindPropertyRelative("time").doubleValue = time;
            node.FindPropertyRelative("laneIndex").intValue = laneIndex;

        }

    }

}
