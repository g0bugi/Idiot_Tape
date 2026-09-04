using System.Collections;
using IdiotTape.EditorTools;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using WorkspaceFixture = IdiotTape.Gameplay.Tests.ChartWorkspaceWindowTests.WorkspaceFixture;

namespace IdiotTape.Gameplay.Tests
{

    public sealed class ChartWorkspaceDataSafetyTests
    {

        [TestCase(false)]
        [TestCase(true)]
        public void ApplyingBufferUndoAndRedoRestoreChartAndBufferTogether(bool clearAfterApply)
        {

            using (WorkspaceFixture fixture = new())
            {

                fixture.Set("clearBufferAfterApply", clearAfterApply);
                fixture.SetEnum("applyMode", "Append");
                fixture.Set("selectedRecordedNoteIndex", 1);
                fixture.Set("selectedChartNoteId", string.Empty);
                string originalChart = JsonUtility.ToJson(fixture.Chart);
                string originalBuffer = fixture.BufferJson;
                int originalCount = fixture.Chart.Notes.Count;
                int bufferCount = fixture.Get<IList>("recordedNotes").Count;
                Undo.ClearUndo(fixture.Chart);
                Undo.ClearUndo(fixture.Window);

                fixture.Invoke("ApplyRecordedNotes");
                Undo.FlushUndoRecordObjects();
                string appliedChart = JsonUtility.ToJson(fixture.Chart);
                string appliedBuffer = fixture.BufferJson;
                Assert.That(fixture.Chart.Notes.Count, Is.EqualTo(originalCount + bufferCount));
                Assert.That(fixture.Chart.TryValidate(out string error), Is.True, error);
                Assert.That(fixture.Get<bool>("bufferWasApplied"), Is.EqualTo(!clearAfterApply));
                Assert.That(fixture.BufferJson, Is.EqualTo(clearAfterApply ? string.Empty : originalBuffer));

                Undo.PerformUndo();
                Assert.That(JsonUtility.ToJson(fixture.Chart), Is.EqualTo(originalChart));
                Assert.That(fixture.BufferJson, Is.EqualTo(originalBuffer));
                Assert.That(fixture.Get<bool>("bufferWasApplied"), Is.False,
                    "Undo must return the restored recording buffer to its unapplied state.");
                Assert.That(fixture.Get<int>("selectedRecordedNoteIndex"), Is.EqualTo(1));

                Undo.PerformRedo();
                Assert.That(JsonUtility.ToJson(fixture.Chart), Is.EqualTo(appliedChart));
                Assert.That(fixture.BufferJson, Is.EqualTo(appliedBuffer));
                Assert.That(fixture.Get<bool>("bufferWasApplied"), Is.EqualTo(!clearAfterApply));
                Assert.That(fixture.Get<int>("selectedRecordedNoteIndex"), Is.EqualTo(clearAfterApply ? -1 : 1));
                LogAssert.NoUnexpectedReceived();

            }

        }

        [UnityTest]
        public IEnumerator OpeningAppliedBananaDetailsPreservesValidBoundaryValues()
        {

            return VerifyBananaBoundaryFoldouts(false);

        }

        [UnityTest]
        public IEnumerator OpeningBufferedBananaDetailsPreservesValidBoundaryValues()
        {

            return VerifyBananaBoundaryFoldouts(true);

        }

        private static IEnumerator VerifyBananaBoundaryFoldouts(bool buffered)
        {

            using (WorkspaceFixture fixture = new())
            {

                ChartNoteAuthoringData banana = ChartNoteAuthoringData.FromChartNote(fixture.FindNote("banana"));
                banana.BananaCurveHandles.Clear();
                banana.BananaCurveHandles.Add(new ChartNoteAuthoringData.CurveHandleData
                {

                    NormalizedTime = 0.005f,
                    NormalizedX = 0.25f

                });
                banana.BananaCurveHandles.Add(new ChartNoteAuthoringData.CurveHandleData
                {

                    NormalizedTime = 0.995f,
                    NormalizedX = 0.75f

                });
                banana.BananaCheckpoints.Clear();
                banana.BananaCheckpoints.Add(new ChartNoteAuthoringData.CheckpointData
                {

                    Time = banana.HitTime + 0.0000005d,
                    NormalizedX = 0.25f

                });
                banana.BananaCheckpoints.Add(new ChartNoteAuthoringData.CheckpointData
                {

                    Time = banana.EndTime - 0.0000005d,
                    NormalizedX = 0.75f

                });
                SerializedObject serialized = new(fixture.Chart);
                SerializedProperty notes = serialized.FindProperty("notes");

                for (int index = 0; index < notes.arraySize; index++)
                {

                    SerializedProperty note = notes.GetArrayElementAtIndex(index);

                    if (note.FindPropertyRelative("id").stringValue == "banana")
                    {

                        banana.WriteTo(note);

                    }

                }

                serialized.ApplyModifiedPropertiesWithoutUndo();
                Assert.That(fixture.Chart.TryValidate(out string error), Is.True, error);
                fixture.SelectNote("banana");

                if (buffered)
                {

                    fixture.AddBufferedNote(banana.CloneWithOffset(0d, "buffer-banana"));
                    fixture.Set("selectedRecordedNoteIndex", fixture.Get<IList>("recordedNotes").Count - 1);
                    fixture.Set("selectedChartNoteId", string.Empty);

                }

                fixture.Set("workspaceShowCurveFields", false);
                fixture.Set("workspaceShowCheckpointFields", false);
                fixture.Show(new Vector2(1200f, 900f));
                string originalChart = JsonUtility.ToJson(fixture.Chart);
                string originalBuffer = fixture.BufferJson;
                fixture.Render();
                yield return null;
                AssertDataUnchanged(fixture, originalChart, originalBuffer);

                Vector2 checkpointFoldout = ClickBananaFoldout(fixture, "workspaceShowCheckpointFields");
                yield return null;
                Assert.That(fixture.Get<bool>("workspaceShowCheckpointFields"), Is.True);
                AssertDataUnchanged(fixture, originalChart, originalBuffer);
                fixture.Click(checkpointFoldout);
                Assert.That(fixture.Get<bool>("workspaceShowCheckpointFields"), Is.False);

                ClickBananaFoldout(fixture, "workspaceShowCurveFields");
                yield return null;
                Assert.That(fixture.Get<bool>("workspaceShowCurveFields"), Is.True);
                AssertDataUnchanged(fixture, originalChart, originalBuffer);

            }

        }

        private static Vector2 ClickBananaFoldout(WorkspaceFixture fixture, string field)
        {

            PrototypeChartRecorderWindow.WorkspaceLayout layout =
                PrototypeChartRecorderWindow.CalculateWorkspaceLayout(
                    fixture.Window.position.width, fixture.Window.position.height, false);
            string otherField = field == "workspaceShowCheckpointFields"
                ? "workspaceShowCurveFields" : "workspaceShowCheckpointFields";

            // Search only the foldout label column; field values and action buttons sit farther right.
            // The small vertical range tolerates theme-specific wrapped-label heights.
            for (float y = layout.Inspector.y + 400f; y < layout.Inspector.y + 610f; y += 4f)
            {

                Vector2 point = new(layout.Inspector.x + 24f, y);
                bool otherWasOpen = fixture.Get<bool>(otherField);
                fixture.Click(point);

                if (fixture.Get<bool>(field))
                {

                    return point;

                }

                if (fixture.Get<bool>(otherField) != otherWasOpen)
                {

                    fixture.Click(point);

                }

            }

            Assert.Fail("The actual banana inspector foldout did not open from mouse input: " + field);
            return Vector2.zero;

        }

        private static void AssertDataUnchanged(WorkspaceFixture fixture, string chartJson, string bufferJson)
        {

            Assert.That(JsonUtility.ToJson(fixture.Chart), Is.EqualTo(chartJson), "Viewing details changed the chart.");
            Assert.That(fixture.BufferJson, Is.EqualTo(bufferJson), "Viewing details changed the recording buffer.");
            Assert.That(fixture.Get<bool>("bufferWasApplied"), Is.False);
            LogAssert.NoUnexpectedReceived();

        }

    }

}
