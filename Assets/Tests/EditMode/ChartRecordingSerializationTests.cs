using System;
using System.Collections;
using System.Reflection;
using IdiotTape.EditorTools;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using WorkspaceFixture = IdiotTape.Gameplay.Tests.ChartWorkspaceWindowTests.WorkspaceFixture;

namespace IdiotTape.Gameplay.Tests
{

    public sealed class ChartRecordingSerializationTests
    {

        [UnityTest]
        public IEnumerator EightLaneFourInputsAcrossEditorFramesCreateFourSynthHolds()
        {

            using (WorkspaceFixture fixture = new())
            {

                PrepareRecording(fixture);
                string originalChart = JsonUtility.ToJson(fixture.Chart);
                double fourBeats = fixture.Chart.TempoSections[0].SecondsPerBar;

                for (int noteIndex = 0; noteIndex < 4; noteIndex++)
                {

                    // The fixture uses 120 BPM and 4/4: bar nine begins at sixteen seconds.
                    double startTime = 16d + noteIndex * fourBeats;
                    fixture.Invoke("RecordSlideOrHoldLane", 3, startTime);
                    Undo.FlushUndoRecordObjects();
                    yield return null;
                    Assert.That(fixture.Get<ChartNoteAuthoringData>("pendingInteraction"), Is.Not.Null);

                    fixture.Invoke("RecordSlideOrHoldLane", 3, startTime + fourBeats);
                    Assert.That(fixture.Get<ChartNoteAuthoringData>("pendingInteraction"), Is.Null,
                        "Recording the completed note's Undo snapshot must not recreate a default pending note.");
                    Undo.FlushUndoRecordObjects();
                    yield return null;
                    Assert.That(fixture.Get<ChartNoteAuthoringData>("pendingInteraction"), Is.Null);
                    Assert.That(fixture.Get<IList>("recordedNotes").Count, Is.EqualTo(noteIndex + 1));
                    AssertHold(GetRecordedData(fixture, noteIndex), startTime, startTime + fourBeats);

                }

                Assert.That(JsonUtility.ToJson(fixture.Chart), Is.EqualTo(originalChart),
                    "Recording into the temporary buffer must not change the applied chart.");
                LogAssert.NoUnexpectedReceived();

            }

        }

        [TestCase(ChartNoteType.Hold)]
        [TestCase(ChartNoteType.Slide)]
        [TestCase(ChartNoteType.Banana)]
        public void CompletedInteractionRemainsClosedAfterUndoSnapshotAndSerialization(ChartNoteType type)
        {

            using (WorkspaceFixture fixture = new())
            {

                PrepareRecording(fixture);
                RecordCompletedInteraction(fixture, type);
                Assert.That(GetRecordedData(fixture, 0).NoteType, Is.EqualTo(type));
                Assert.That(fixture.Get<ChartNoteAuthoringData>("pendingInteraction"), Is.Null);
                string originalBuffer = fixture.BufferJson;

                Undo.RecordObject(fixture.Window, "Verify completed recording snapshot");
                Assert.That(fixture.Get<ChartNoteAuthoringData>("pendingInteraction"), Is.Null);
                Undo.FlushUndoRecordObjects();
                RoundTripWindowSerialization(fixture);

                Assert.That(fixture.Get<ChartNoteAuthoringData>("pendingInteraction"), Is.Null);
                Assert.That(fixture.BufferJson, Is.EqualTo(originalBuffer));
                fixture.Invoke("RecordSlideOrHoldLane", 3, 20d);
                fixture.Invoke("RecordSlideOrHoldLane", 3, 22d);
                Assert.That(fixture.Get<IList>("recordedNotes").Count, Is.EqualTo(2));
                AssertHold(GetRecordedData(fixture, 1), 20d, 22d);
                LogAssert.NoUnexpectedReceived();

            }

        }

        [Test]
        public void UndoAndRedoRestoreCompletedRecordingWithoutReopeningItsGesture()
        {

            using (WorkspaceFixture fixture = new())
            {

                PrepareRecording(fixture);
                Undo.IncrementCurrentGroup();
                fixture.Invoke("RecordSlideOrHoldLane", 3, 16d);
                fixture.Invoke("RecordSlideOrHoldLane", 3, 18d);
                Undo.FlushUndoRecordObjects();
                string completedBuffer = fixture.BufferJson;
                Undo.IncrementCurrentGroup();

                Undo.PerformUndo();

                Assert.That(fixture.Get<IList>("recordedNotes"), Is.Empty);
                Assert.That(fixture.Get<ChartNoteAuthoringData>("pendingInteraction"), Is.Null);

                Undo.PerformRedo();

                Assert.That(fixture.BufferJson, Is.EqualTo(completedBuffer));
                Assert.That(fixture.Get<ChartNoteAuthoringData>("pendingInteraction"), Is.Null);
                fixture.Invoke("RecordSlideOrHoldLane", 3, 20d);
                fixture.Invoke("RecordSlideOrHoldLane", 3, 22d);
                Assert.That(fixture.Get<IList>("recordedNotes").Count, Is.EqualTo(2));
                AssertHold(GetRecordedData(fixture, 1), 20d, 22d);
                LogAssert.NoUnexpectedReceived();

            }

        }

        [Test]
        public void StopAndSerializationDiscardOnlyTheIncompleteGesture()
        {

            using (WorkspaceFixture fixture = new())
            {

                PrepareRecording(fixture);
                RecordCompletedInteraction(fixture, ChartNoteType.Hold);
                fixture.Invoke("RecordSlideOrHoldLane", 3, 20d);
                string completedBuffer = fixture.BufferJson;

                fixture.Invoke("StopRecording");
                RoundTripWindowSerialization(fixture);

                Assert.That(fixture.Get<ChartNoteAuthoringData>("pendingInteraction"), Is.Null);
                Assert.That(fixture.Get<bool>("isRecording"), Is.False);
                Assert.That(fixture.BufferJson, Is.EqualTo(completedBuffer));
                fixture.Invoke("RecordSlideOrHoldLane", 3, 22d);
                fixture.Invoke("RecordSlideOrHoldLane", 3, 24d);
                Assert.That(fixture.Get<IList>("recordedNotes").Count, Is.EqualTo(2));
                AssertHold(GetRecordedData(fixture, 1), 22d, 24d);
                LogAssert.NoUnexpectedReceived();

            }

        }

        [Test]
        public void ClearingAnEmptyBufferCannotSeedTheNextHoldWithADefaultNote()
        {

            using (WorkspaceFixture fixture = new())
            {

                PrepareRecording(fixture);
                fixture.Invoke("RecordSlideOrHoldLane", 3, 16d);

                // There are no completed records, so ClearBuffer does not display a confirmation dialog.
                fixture.Invoke("ClearBuffer");
                Undo.FlushUndoRecordObjects();
                RoundTripWindowSerialization(fixture);

                Assert.That(fixture.Get<ChartNoteAuthoringData>("pendingInteraction"), Is.Null);
                fixture.Invoke("RecordSlideOrHoldLane", 3, 20d);
                fixture.Invoke("RecordSlideOrHoldLane", 3, 22d);
                Assert.That(fixture.Get<IList>("recordedNotes").Count, Is.EqualTo(1));
                AssertHold(GetRecordedData(fixture, 0), 20d, 22d);
                LogAssert.NoUnexpectedReceived();

            }

        }

        private static void RoundTripWindowSerialization(WorkspaceFixture fixture)
        {

            EditorJsonUtility.FromJsonOverwrite(EditorJsonUtility.ToJson(fixture.Window), fixture.Window);
            // This fixture chart is an unsaved, memory-only object, so JSON cannot restore its asset
            // reference. Reconnect only that dependency; leave pending and completed recording data untouched.
            fixture.Set("chart", fixture.Chart);

        }

        private static void PrepareRecording(WorkspaceFixture fixture)
        {

            // Keep the production recording and Undo paths, while excluding the unrelated EditMode
            // lifecycle rule that intentionally cancels open gestures when Play Mode is not running.
            MethodInfo updateMethod = typeof(PrototypeChartRecorderWindow).GetMethod(
                "EditorUpdate", BindingFlags.Instance | BindingFlags.NonPublic);
            EditorApplication.CallbackFunction update = (EditorApplication.CallbackFunction)Delegate.CreateDelegate(
                typeof(EditorApplication.CallbackFunction), fixture.Window, updateMethod);
            EditorApplication.update -= update;
            fixture.Get<IList>("recordedNotes").Clear();
            fixture.Set("pendingInteraction", null);
            fixture.Set("selectedPartIndex", 0);
            fixture.Set("selectedRecordedNoteIndex", -1);
            fixture.Set("selectedChartNoteId", string.Empty);
            fixture.Set("automaticQuantization", false);
            fixture.Set("isRecording", true);
            fixture.SetEnum("recordingPhase", "Recording");
            Assert.That(fixture.Chart.MusicalParts[0].Id, Is.EqualTo("synth"));
            Undo.ClearUndo(fixture.Window);

        }

        private static void RecordCompletedInteraction(WorkspaceFixture fixture, ChartNoteType type)
        {

            if (type == ChartNoteType.Banana)
            {

                fixture.Invoke("RecordBananaLane", 3, 16d);
                fixture.Invoke("RecordBananaLane", 6, 18d);

            }
            else
            {

                fixture.Invoke("RecordSlideOrHoldLane", 3, 16d);

                if (type == ChartNoteType.Slide)
                {

                    fixture.Invoke("RecordSlideOrHoldLane", 6, 17d);

                }

                fixture.Invoke("RecordSlideOrHoldLane", type == ChartNoteType.Slide ? 6 : 3, 18d);

            }

        }

        private static ChartNoteAuthoringData GetRecordedData(WorkspaceFixture fixture, int index)
        {

            object record = fixture.Get<IList>("recordedNotes")[index];
            return (ChartNoteAuthoringData)record.GetType().GetField("noteData").GetValue(record);

        }

        private static void AssertHold(ChartNoteAuthoringData data, double startTime, double endTime)
        {

            Assert.That(data.NoteType, Is.EqualTo(ChartNoteType.Hold));
            Assert.That(data.HitTime, Is.EqualTo(startTime));
            Assert.That(data.EndTime, Is.EqualTo(endTime));
            Assert.That(data.LaneIndex, Is.EqualTo(3));
            Assert.That(data.EndLaneIndex, Is.EqualTo(3));
            Assert.That(data.MusicalPartId, Is.EqualTo("synth"));
            Assert.That(data.SlideNodes, Is.Empty);

        }

    }

}
