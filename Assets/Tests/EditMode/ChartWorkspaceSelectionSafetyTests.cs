using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using IdiotTape.EditorTools;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using WorkspaceFixture = IdiotTape.Gameplay.Tests.ChartWorkspaceWindowTests.WorkspaceFixture;

namespace IdiotTape.Gameplay.Tests
{

    public sealed class ChartWorkspaceSelectionSafetyTests
    {

        private const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;

        [Test]
        public void MixedPreviewPreservesChartBufferAndUndoHistory()
        {

            using (WorkspaceFixture fixture = new())
            {

                fixture.Invoke("SetWorkspaceNoteSelection", "tap", -1, false);
                fixture.Invoke("SetWorkspaceNoteSelection", null, 0, true);
                string chartBefore = JsonUtility.ToJson(fixture.Chart);
                string bufferBefore = fixture.BufferJson;
                int undoBefore = Undo.GetCurrentGroup();

                Assert.That(Transform(fixture, 0.125d, 0, true), Is.True);

                Assert.That(fixture.Get<List<ChartNoteAuthoringData>>("workspaceSelectionPreview").Count, Is.EqualTo(2));
                Assert.That(JsonUtility.ToJson(fixture.Chart), Is.EqualTo(chartBefore));
                Assert.That(fixture.BufferJson, Is.EqualTo(bufferBefore));
                Assert.That(Undo.GetCurrentGroup(), Is.EqualTo(undoBefore));

            }

        }

        [Test]
        public void InvalidBufferEndpointRejectsTheAppliedAndBufferSelectionTogether()
        {

            using (WorkspaceFixture fixture = new())
            {

                fixture.Get<IList>("recordedNotes").Clear();
                ChartNoteAuthoringData flick = ChartNoteAuthoringData.CreateTap("edge-flick", 3d, 2, "synth");
                flick.NoteType = ChartNoteType.Flick;
                flick.EndLaneIndex = 7;
                fixture.AddBufferedNote(flick);
                fixture.Invoke("SetWorkspaceNoteSelection", "tap", -1, false);
                fixture.Invoke("SetWorkspaceNoteSelection", null, 0, true);
                string chartBefore = JsonUtility.ToJson(fixture.Chart);
                string bufferBefore = fixture.BufferJson;
                int undoBefore = Undo.GetCurrentGroup();

                Assert.That(Transform(fixture, 0.25d, 1), Is.False);

                Assert.That(JsonUtility.ToJson(fixture.Chart), Is.EqualTo(chartBefore));
                Assert.That(fixture.BufferJson, Is.EqualTo(bufferBefore));
                Assert.That(Undo.GetCurrentGroup(), Is.EqualTo(undoBefore));

            }

        }

        [Test]
        public void MovingBufferedBananaKeepsOriginalTimestampAndRestoresTheEntirePath()
        {

            using (WorkspaceFixture fixture = new())
            {

                fixture.Get<IList>("recordedNotes").Clear();
                ChartNoteAuthoringData original = ChartNoteAuthoringData.FromChartNote(fixture.FindNote("banana"));
                fixture.AddBufferedNote(original.CloneWithOffset(0d));
                fixture.Invoke("SetWorkspaceNoteSelection", null, 0, false);
                string chartBefore = JsonUtility.ToJson(fixture.Chart);

                Assert.That(Transform(fixture, 0.375d, 0), Is.True);
                object record = fixture.Get<IList>("recordedNotes")[0];
                Assert.That((double)record.GetType().GetField("originalHitTime").GetValue(record),
                    Is.EqualTo(original.HitTime));
                Assert.That((bool)record.GetType().GetField("hasOriginalHitTime").GetValue(record), Is.True);

                fixture.Invoke("RestoreOriginalRecordedTimes");
                ChartNoteAuthoringData restored = (ChartNoteAuthoringData)record.GetType().GetField("noteData").GetValue(record);
                Assert.That(restored.HitTime, Is.EqualTo(original.HitTime));
                Assert.That(restored.EndTime, Is.EqualTo(original.EndTime).Within(1e-9d));
                Assert.That(restored.BananaCheckpoints.Count, Is.EqualTo(original.BananaCheckpoints.Count));

                for (int index = 0; index < original.BananaCheckpoints.Count; index++)
                {

                    Assert.That(restored.BananaCheckpoints[index].Time,
                        Is.EqualTo(original.BananaCheckpoints[index].Time).Within(1e-9d));

                }

                Assert.That(JsonUtility.ToJson(fixture.Chart), Is.EqualTo(chartBefore));

            }

        }

        [Test]
        public void SixEightKeyboardNudgeMatchesTheVisibleSubdivisionGrid()
        {

            using (WorkspaceFixture fixture = new())
            {

                SerializedObject serialized = new(fixture.Chart);
                SerializedProperty tempo = serialized.FindProperty("tempoSections").GetArrayElementAtIndex(0);
                tempo.FindPropertyRelative("beatsPerBar").intValue = 6;
                tempo.FindPropertyRelative("beatUnit").intValue = 8;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                fixture.Invoke("SetWorkspaceNoteSelection", "tap", -1, false);
                EditorGUIUtility.editingTextField = false;
                fixture.Invoke("HandleWorkspaceEditingKeyboardEvent", new Event
                {

                    type = EventType.KeyDown,
                    keyCode = KeyCode.DownArrow

                });

                Assert.That(fixture.FindNote("tap").HitTime, Is.EqualTo(0.3125d).Within(1e-9d),
                    "At 120 BPM in 6/8, a quarter of the eighth-note beat is 62.5 ms.");

            }

        }

        [Test]
        public void AdditiveRectangleKeepsTheRestoredLegacySingleSelection()
        {

            using (WorkspaceFixture fixture = new())
            {

                fixture.Invoke("ClearWorkspaceMultiSelection");
                fixture.SelectNote("slide");
                fixture.Invoke("SelectWorkspaceRectangle", new Rect(0f, 0f, 800f, 800f), true,
                    "synth", new Rect(530f, 15f, 40f, 20f), true);

                CollectionAssert.AreEquivalent(new[] { "slide", "tap" },
                    fixture.Get<List<string>>("workspaceSelectedChartIds"));

            }

        }

        [Test]
        public void TogglingOffTheLastSelectionDoesNotLeaveALegacyFallbackTarget()
        {

            using (WorkspaceFixture fixture = new())
            {

                fixture.Invoke("SetWorkspaceNoteSelection", "tap", -1, false);
                fixture.Invoke("SetWorkspaceNoteSelection", "tap", -1, true);
                string before = JsonUtility.ToJson(fixture.Chart);

                Assert.That(Transform(fixture, 0.25d, 0), Is.False);
                Assert.That(fixture.Get<string>("selectedChartNoteId"), Is.Empty);
                Assert.That(fixture.Get<int>("selectedRecordedNoteIndex"), Is.EqualTo(-1));
                Assert.That(JsonUtility.ToJson(fixture.Chart), Is.EqualTo(before));

            }

        }

        [Test]
        public void UndoInvalidatesPreviewFromTheChangedChart()
        {

            using (WorkspaceFixture fixture = new())
            {

                fixture.Invoke("SetWorkspaceNoteSelection", "tap", -1, false);
                string chartBefore = JsonUtility.ToJson(fixture.Chart);
                Assert.That(Transform(fixture, 0.125d, 0), Is.True);
                Undo.FlushUndoRecordObjects();
                Assert.That(Transform(fixture, 0.25d, 0, true), Is.True);

                Undo.PerformUndo();

                Assert.That(JsonUtility.ToJson(fixture.Chart), Is.EqualTo(chartBefore));
                Assert.That(fixture.Get<List<ChartNoteAuthoringData>>("workspaceSelectionPreview"), Is.Null);

            }

        }

        private static bool Transform(WorkspaceFixture fixture, double seconds, int lanes, bool preview = false)
        {

            return (bool)typeof(PrototypeChartRecorderWindow).GetMethod("TransformWorkspaceSelection", PrivateInstance)
                .Invoke(fixture.Window, new object[] { seconds, 0d, lanes, null, preview });

        }

    }

}
