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

    public sealed class ChartRecordingTakeTests
    {

        [Test]
        public void RetryingSortedTakePreservesEarlierSamePartAndLegacyBuffer()
        {

            using (WorkspaceFixture fixture = new())
            {

                fixture.Get<IList>("recordedNotes").Clear();
                fixture.AddBufferedNote(ChartNoteAuthoringData.CreateTap("legacy", 2d, 7, "synth"));
                BeginTake(fixture, "Loop");
                AddTap(fixture, 5d, 1);
                AddTap(fixture, 1d, 2);
                fixture.Invoke("StopRecording");
                string earlierBuffer = fixture.BufferJson;
                string originalChart = JsonUtility.ToJson(fixture.Chart);

                BeginTake(fixture, "Loop");
                AddTap(fixture, 4d, 3);
                AddTap(fixture, 0.5d, 4);
                fixture.Invoke("StopRecording");
                object take = fixture.Get<object>("lastRecordingTake");
                fixture.Invoke("ReplaceRecordedTake", take);

                Assert.That(fixture.BufferJson, Is.EqualTo(earlierBuffer),
                    "Retry membership follows stable take IDs, even when two same-part takes interleave in time.");
                Assert.That(JsonUtility.ToJson(fixture.Chart), Is.EqualTo(originalChart));
                Assert.That(fixture.Get<string>("currentRecordingTakeId"), Is.Not.EqualTo(TakeField<string>(take, "id")));
                LogAssert.NoUnexpectedReceived();

            }

        }

        [Test]
        public void RetryUndoRestoresTakeAndCancelsReplacementWithoutReopeningIncompleteGesture()
        {

            using (WorkspaceFixture fixture = new())
            {

                BeginTake(fixture, "Beginning");
                fixture.Invoke("RecordSlideOrHoldLane", 3, 1d);
                fixture.Invoke("RecordSlideOrHoldLane", 3, 1.5d);
                fixture.Invoke("RecordBananaLane", 2, 2d);
                fixture.Invoke("StopRecording");
                string originalBuffer = fixture.BufferJson;
                string originalTakeId = TakeField<string>(fixture.Get<object>("lastRecordingTake"), "id");
                Undo.FlushUndoRecordObjects();
                fixture.Invoke("ReplaceRecordedTake", fixture.Get<object>("lastRecordingTake"));
                fixture.SetEnum("recordingPhase", "CountIn");
                string replacedBuffer = fixture.BufferJson;
                Undo.FlushUndoRecordObjects();

                Undo.PerformUndo();

                Assert.That(fixture.BufferJson, Is.EqualTo(originalBuffer));
                Assert.That(TakeField<string>(fixture.Get<object>("lastRecordingTake"), "id"), Is.EqualTo(originalTakeId));
                Assert.That(fixture.Get<object>("recordingPhase").ToString(), Is.EqualTo("Idle"));
                Assert.That(fixture.Get<ChartNoteAuthoringData>("pendingInteraction"), Is.Null);
                Assert.That(CanRetry(fixture), Is.True);

                Undo.PerformRedo();

                Assert.That(fixture.BufferJson, Is.EqualTo(replacedBuffer));
                Assert.That(fixture.Get<ChartNoteAuthoringData>("pendingInteraction"), Is.Null);
                Assert.That(fixture.Get<bool>("isRecording"), Is.False,
                    "Redo restores buffer edits; it must not silently schedule transport playback.");
                LogAssert.NoUnexpectedReceived();

            }

        }

        [Test]
        public void RetryRestoresCapturedPartModeLoopCountInAndOriginalCurrentPosition()
        {

            using (WorkspaceFixture fixture = new())
            {

                fixture.Set("selectedPartIndex", 1);
                fixture.SetEnum("recordingNoteMode", "Flick");
                fixture.SetEnum("flickDefaultDirection", "Left");
                fixture.Set("loopStart", 4d);
                fixture.Set("loopEnd", 12d);
                fixture.Set("loopStartBar", 3);
                fixture.Set("loopEndBar", 7);
                fixture.Set("loopBarCount", 4);
                fixture.Set("countInBars", 3);
                fixture.Set("metronomeDuringRecording", true);
                fixture.Set("inputAdvanceMilliseconds", 32f);
                BeginTake(fixture, "CurrentPosition", 6.25d);
                fixture.Invoke("StopRecording");
                object take = fixture.Get<object>("lastRecordingTake");
                fixture.Set("selectedPartIndex", 2);
                fixture.SetEnum("recordingNoteMode", "Banana");
                fixture.SetEnum("workspaceRecordingStart", "Beginning");
                fixture.Set("loopStart", 0d);
                fixture.Set("loopEnd", 2d);
                fixture.Set("countInBars", 1);
                fixture.Set("metronomeDuringRecording", false);
                fixture.Set("inputAdvanceMilliseconds", -50f);
                fixture.Set("seekTime", 39d);

                fixture.Invoke("RestoreRecordingTakeContext", take);

                Assert.That(fixture.Get<int>("selectedPartIndex"), Is.EqualTo(1));
                Assert.That(fixture.Get<object>("recordingNoteMode").ToString(), Is.EqualTo("Flick"));
                Assert.That(fixture.Get<object>("flickDefaultDirection").ToString(), Is.EqualTo("Left"));
                Assert.That(fixture.Get<object>("workspaceRecordingStart").ToString(), Is.EqualTo("CurrentPosition"));
                Assert.That(fixture.Get<double>("loopStart"), Is.EqualTo(4d));
                Assert.That(fixture.Get<double>("loopEnd"), Is.EqualTo(12d));
                Assert.That(fixture.Get<int>("loopStartBar"), Is.EqualTo(3));
                Assert.That(fixture.Get<int>("loopEndBar"), Is.EqualTo(7));
                Assert.That(fixture.Get<int>("loopBarCount"), Is.EqualTo(4));
                Assert.That(fixture.Get<int>("countInBars"), Is.EqualTo(3));
                Assert.That(fixture.Get<bool>("metronomeDuringRecording"), Is.True);
                Assert.That(fixture.Get<float>("inputAdvanceMilliseconds"), Is.EqualTo(32f));
                Assert.That(TakeField<double>(take, "targetTime"), Is.EqualTo(6.25d));

            }

        }

        [TestCase(true)]
        [TestCase(false)]
        public void AppliedTakeCannotBeRetriedButUndoRestoresItsAvailability(bool clearAfterApply)
        {

            using (WorkspaceFixture fixture = new())
            {

                fixture.Get<IList>("recordedNotes").Clear();
                fixture.Set("clearBufferAfterApply", clearAfterApply);
                fixture.Set("addMissingActivationWindows", true);
                BeginTake(fixture, "Loop");
                AddTap(fixture, 3d, 6);
                fixture.Invoke("StopRecording");
                Undo.FlushUndoRecordObjects();
                string originalChart = JsonUtility.ToJson(fixture.Chart);
                string originalBuffer = fixture.BufferJson;
                Assert.That(CanRetry(fixture), Is.True);

                fixture.Invoke("ApplyRecordedNotes");
                Undo.FlushUndoRecordObjects();

                Assert.That(CanRetry(fixture), Is.False);
                string appliedChart = JsonUtility.ToJson(fixture.Chart);
                string appliedBuffer = fixture.BufferJson;
                fixture.Invoke("RetryLastTake");
                Assert.That(JsonUtility.ToJson(fixture.Chart), Is.EqualTo(appliedChart));
                Assert.That(fixture.BufferJson, Is.EqualTo(appliedBuffer));

                Undo.PerformUndo();

                Assert.That(JsonUtility.ToJson(fixture.Chart), Is.EqualTo(originalChart));
                Assert.That(fixture.BufferJson, Is.EqualTo(originalBuffer));
                Assert.That(CanRetry(fixture), Is.True);
                LogAssert.NoUnexpectedReceived();

            }

        }

        [Test]
        public void UnpreparedRetryPreservesBufferChartAndUndoHistory()
        {

            using (WorkspaceFixture fixture = new())
            {

                BeginTake(fixture, "Loop");
                AddTap(fixture, 3d, 2);
                fixture.Invoke("StopRecording");
                string originalBuffer = fixture.BufferJson;
                string originalChart = JsonUtility.ToJson(fixture.Chart);
                int originalUndoGroup = Undo.GetCurrentGroup();

                fixture.Invoke("RetryLastTake");

                Assert.That(fixture.BufferJson, Is.EqualTo(originalBuffer));
                Assert.That(JsonUtility.ToJson(fixture.Chart), Is.EqualTo(originalChart));
                Assert.That(Undo.GetCurrentGroup(), Is.EqualTo(originalUndoGroup));
                Assert.That(fixture.Get<string>("statusMessage"), Does.Contain("FMOD"));

            }

        }

        [Test]
        public void ChangedChartOrMissingRecordedPartCannotRetry()
        {

            using (WorkspaceFixture fixture = new())
            {

                fixture.Get<IList>("recordedNotes").Clear();
                BeginTake(fixture, "Beginning");
                fixture.Invoke("StopRecording");
                Assert.That(CanRetry(fixture), Is.True);
                SerializedObject serialized = new(fixture.Chart);
                serialized.FindProperty("musicalParts").GetArrayElementAtIndex(0).FindPropertyRelative("id").stringValue = "renamed";
                serialized.ApplyModifiedPropertiesWithoutUndo();
                Assert.That(CanRetry(fixture), Is.False,
                    "A missing part must never fall back to the first part when restoring a take.");

                fixture.Invoke("ChangeChart", new object[] { null });

                Assert.That(CanRetry(fixture), Is.False);
                object take = fixture.Get<object>("lastRecordingTake");
                Assert.That(take == null || string.IsNullOrEmpty(TakeField<string>(take, "id")), Is.True);

            }

        }

        [TestCase(ChartNoteType.Hold)]
        [TestCase(ChartNoteType.Slide)]
        [TestCase(ChartNoteType.Banana)]
        public void EmptyIncompleteTakeCanBeRetriedWithoutRemovingEarlierCompletedInput(ChartNoteType type)
        {

            using (WorkspaceFixture fixture = new())
            {

                string earlierBuffer = fixture.BufferJson;
                BeginTake(fixture, "Loop");
                fixture.Invoke(type == ChartNoteType.Banana ? "RecordBananaLane" : "RecordSlideOrHoldLane", 3, 1d);

                if (type == ChartNoteType.Slide)
                {

                    fixture.Invoke("RecordSlideOrHoldLane", 5, 1.5d);

                }

                fixture.Invoke("StopRecording");
                Assert.That(CanRetry(fixture), Is.True);
                fixture.Invoke("ReplaceRecordedTake", fixture.Get<object>("lastRecordingTake"));

                Assert.That(fixture.BufferJson, Is.EqualTo(earlierBuffer));
                Assert.That(fixture.Get<ChartNoteAuthoringData>("pendingInteraction"), Is.Null);

            }

        }

        [Test]
        public void SerializedBufferKeepsTakeMembershipAndDistinctEditorIds()
        {

            using (WorkspaceFixture fixture = new())
            {

                fixture.Get<IList>("recordedNotes").Clear();
                BeginTake(fixture, "Loop");
                AddTap(fixture, 2d, 3);
                AddTap(fixture, 1d, 3);
                fixture.Invoke("StopRecording");
                string originalBuffer = fixture.BufferJson;
                EditorJsonUtility.FromJsonOverwrite(EditorJsonUtility.ToJson(fixture.Window), fixture.Window);
                fixture.Set("chart", fixture.Chart);
                object take = fixture.Get<object>("lastRecordingTake");
                take.GetType().GetField("chart").SetValue(take, fixture.Chart);

                Assert.That(fixture.BufferJson, Is.EqualTo(originalBuffer));
                IList records = fixture.Get<IList>("recordedNotes");
                Assert.That(TakeField<string>(records[0], "editorId"), Is.Not.Empty);
                Assert.That(TakeField<string>(records[0], "editorId"), Is.Not.EqualTo(TakeField<string>(records[1], "editorId")));
                Assert.That(TakeField<string>(records[0], "takeId"), Is.EqualTo(TakeField<string>(take, "id")));
                Assert.That(TakeField<string>(records[1], "takeId"), Is.EqualTo(TakeField<string>(take, "id")));
                fixture.Invoke("ReplaceRecordedTake", take);
                Assert.That(records, Is.Empty);

            }

        }

        private static void BeginTake(WorkspaceFixture fixture, string startMode, double targetTime = 0d)
        {

            fixture.Set("automaticQuantization", false);
            fixture.SetEnum("workspaceRecordingStart", startMode);
            fixture.Invoke("BeginRecordingTake", fixture.Get<object>("workspaceRecordingStart"), targetTime);
            fixture.Set("isRecording", true);
            fixture.SetEnum("recordingPhase", "Recording");

        }

        private static void AddTap(WorkspaceFixture fixture, double time, int lane)
        {

            fixture.Invoke("AddRecordedInteraction", ChartNoteAuthoringData.CreateTap(
                string.Empty, time, lane, fixture.Chart.MusicalParts[fixture.Get<int>("selectedPartIndex")].Id));

        }

        private static bool CanRetry(WorkspaceFixture fixture)
        {

            return (bool)typeof(PrototypeChartRecorderWindow).GetMethod("CanRetryLastTake",
                BindingFlags.Instance | BindingFlags.NonPublic).Invoke(fixture.Window, null);

        }

        private static T TakeField<T>(object take, string name)
        {

            return (T)take.GetType().GetField(name).GetValue(take);

        }

    }

}
