using System.Collections;
using IdiotTape.EditorTools;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using WorkspaceFixture = IdiotTape.Gameplay.Tests.ChartWorkspaceWindowTests.WorkspaceFixture;

namespace IdiotTape.Gameplay.Tests
{

    public sealed class ChartWorkspaceInputTests
    {

        [TestCase(ChartNoteType.Hold)]
        [TestCase(ChartNoteType.Slide)]
        [TestCase(ChartNoteType.Banana)]
        public void LeavingPlayModeDiscardsOnlyIncompleteInteractionWithoutQuantizingCompletedBuffer(ChartNoteType type)
        {

            using (WorkspaceFixture fixture = new())
            {

                string originalChart = JsonUtility.ToJson(fixture.Chart);
                PrepareInterruptedRecording(fixture, type);
                string originalBuffer = fixture.BufferJson;

                fixture.Invoke("EditorUpdate");

                Assert.That(fixture.Get<ChartNoteAuthoringData>("pendingInteraction"), Is.Null);
                Assert.That(fixture.Get<bool>("isRecording"), Is.False);
                Assert.That(fixture.Get<bool>("isLoopRecording"), Is.False);
                Assert.That(fixture.Get<object>("recordingPhase").ToString(), Is.EqualTo("Idle"));
                Assert.That(fixture.Get<bool>("interactionEditorRangeLocked"), Is.False);
                Assert.That(fixture.Get<int>("draggedSlidePointIndex"), Is.EqualTo(int.MinValue));
                Assert.That(fixture.Get<int>("draggedSimplePointIndex"), Is.EqualTo(int.MinValue));
                Assert.That(fixture.Get<int>("workspaceDraggedBananaHandle"), Is.EqualTo(-1));
                Assert.That(fixture.BufferJson, Is.EqualTo(originalBuffer));
                Assert.That(JsonUtility.ToJson(fixture.Chart), Is.EqualTo(originalChart));

            }

        }

        [Test]
        public void ClosingAuthoringWindowDiscardsPendingInteractionAndPreservesCompletedBuffer()
        {

            using (WorkspaceFixture fixture = new())
            {

                PrepareInterruptedRecording(fixture, ChartNoteType.Slide);
                string originalBuffer = fixture.BufferJson;

                fixture.Invoke("OnDisable");

                Assert.That(fixture.Get<ChartNoteAuthoringData>("pendingInteraction"), Is.Null);
                Assert.That(fixture.Get<bool>("isRecording"), Is.False);
                Assert.That(fixture.Get<bool>("interactionEditorRangeLocked"), Is.False);
                Assert.That(fixture.BufferJson, Is.EqualTo(originalBuffer));

            }

        }

        [Test]
        public void OrdinaryEditModeUpdatesDoNotCancelAChartEditingDrag()
        {

            using (WorkspaceFixture fixture = new())
            {

                fixture.Set("draggedSlidePointIndex", 0);
                fixture.Set("interactionEditorRangeLocked", true);

                fixture.Invoke("EditorUpdate");

                Assert.That(fixture.Get<int>("draggedSlidePointIndex"), Is.EqualTo(0));
                Assert.That(fixture.Get<bool>("interactionEditorRangeLocked"), Is.True);

            }

        }

        [UnityTest]
        public IEnumerator HiddenOverviewRowsDoNotConsumeLegendClicksOrSeekOutsideTheViewport()
        {

            using (WorkspaceFixture fixture = new())
            {

                AddOverviewParts(fixture.Chart, 12);
                fixture.Show(new Vector2(1200f, 600f));
                yield return null;
                fixture.SetEnum("timelineViewMode", "Horizontal");
                fixture.Set("workspaceOverviewScroll", new Vector2(0f, 130f));
                fixture.Set("workspaceShowChecks", false);
                fixture.Set("seekTime", 2.718d);
                fixture.Render();
                yield return null;
                string originalBuffer = fixture.BufferJson;
                string originalChart = JsonUtility.ToJson(fixture.Chart);
                int originalPart = fixture.Get<int>("selectedPartIndex");
                string originalSelection = fixture.Get<string>("selectedChartNoteId");
                PrototypeChartRecorderWindow.WorkspaceLayout layout = PrototypeChartRecorderWindow.CalculateWorkspaceLayout(
                    fixture.Window.position.width, fixture.Window.position.height, false);
                float canvasBottom = Mathf.Max(109f, layout.Main.height - 106f);

                fixture.Click(new Vector2(layout.Main.x + 190f, layout.Main.y + canvasBottom + 41f));
                yield return null;

                Assert.That(fixture.Get<bool>("workspaceShowChecks"), Is.True,
                    "A row clipped below the chart must not consume the checkbox click before the legend sees it.");
                Assert.That(fixture.Get<double>("seekTime"), Is.EqualTo(2.718d),
                    "Clicking a legend must not seek through a hidden overview row.");
                Assert.That(fixture.Get<int>("selectedPartIndex"), Is.EqualTo(originalPart));
                Assert.That(fixture.Get<string>("selectedChartNoteId"), Is.EqualTo(originalSelection));
                Assert.That(fixture.BufferJson, Is.EqualTo(originalBuffer));
                Assert.That(JsonUtility.ToJson(fixture.Chart), Is.EqualTo(originalChart));
                LogAssert.NoUnexpectedReceived();

            }

        }

        [UnityTest]
        public IEnumerator HiddenOverviewRowsDoNotSelectPartsWhenClickingTheModeRowAboveViewport()
        {

            using (WorkspaceFixture fixture = new())
            {

                AddOverviewParts(fixture.Chart, 12);
                fixture.Show(new Vector2(1200f, 600f));
                yield return null;
                fixture.SetEnum("timelineViewMode", "Horizontal");
                fixture.Set("workspaceOverviewScroll", new Vector2(0f, 390f));
                fixture.SetEnum("workspaceInspectorTab", "Note");
                fixture.Set("seekTime", 2.718d);
                fixture.Render();
                yield return null;
                int originalPart = fixture.Get<int>("selectedPartIndex");
                PrototypeChartRecorderWindow.WorkspaceLayout layout = PrototypeChartRecorderWindow.CalculateWorkspaceLayout(
                    fixture.Window.position.width, fixture.Window.position.height, false);

                // The label area has no command: a click must not fall through to a scrolled-off part title.
                fixture.Click(new Vector2(layout.Main.x + 45f, layout.Main.y + 52f));
                yield return null;

                Assert.That(fixture.Get<int>("selectedPartIndex"), Is.EqualTo(originalPart));
                Assert.That(fixture.Get<object>("workspaceInspectorTab").ToString(), Is.EqualTo("Note"));
                Assert.That(fixture.Get<double>("seekTime"), Is.EqualTo(2.718d));
                LogAssert.NoUnexpectedReceived();

            }

        }

        private static void PrepareInterruptedRecording(WorkspaceFixture fixture, ChartNoteType type)
        {

            ChartNoteAuthoringData pending = ChartNoteAuthoringData.CreateTap("unfinished", 9.25d, 2, "synth");
            pending.NoteType = type;
            fixture.Set("pendingInteraction", pending);
            fixture.Set("isRecording", true);
            fixture.Set("isLoopRecording", true);
            fixture.SetEnum("recordingPhase", "Recording");
            fixture.Set("automaticQuantization", true);
            fixture.Set("interactionEditorRangeLocked", true);
            fixture.Set("draggedSlidePointIndex", 0);
            fixture.Set("draggedSimplePointIndex", 1);
            fixture.Set("workspaceDraggedBananaHandle", 0);

            foreach (object record in fixture.Get<IList>("recordedNotes"))
            {

                record.GetType().GetField("pendingAutomaticQuantization").SetValue(record, true);

            }

        }

        private static void AddOverviewParts(PrototypeChart chart, int count)
        {

            SerializedObject serialized = new(chart);
            SerializedProperty parts = serialized.FindProperty("musicalParts");
            int previousCount = parts.arraySize;
            parts.arraySize = count;

            for (int index = previousCount; index < count; index++)
            {

                SerializedProperty part = parts.GetArrayElementAtIndex(index);
                part.FindPropertyRelative("id").stringValue = "context-" + index;
                part.FindPropertyRelative("displayName").stringValue = "Context " + index;
                part.FindPropertyRelative("color").colorValue = Color.gray;

            }

            serialized.ApplyModifiedPropertiesWithoutUndo();

        }

    }

}
