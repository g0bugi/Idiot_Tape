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

    public sealed class ChartRecordingApplyValidationTests
    {

        [TestCase(ChartNoteType.Hold, "Append")]
        [TestCase(ChartNoteType.Hold, "ReplaceRecordedPartsInLoop")]
        [TestCase(ChartNoteType.Slide, "Append")]
        [TestCase(ChartNoteType.Slide, "ReplaceRecordedPartsInLoop")]
        public void InvalidInteractionTimingLeavesChartBufferAndUndoUntouched(ChartNoteType type, string applyMode)
        {

            using (WorkspaceFixture fixture = new())
            {

                PrepareApply(fixture, applyMode);
                ChartNoteAuthoringData invalid = ChartNoteAuthoringData.CreateTap("invalid", 9d, 3, "synth");
                invalid.NoteType = type;
                invalid.EndTime = type == ChartNoteType.Hold ? 9d : 9.75d;
                invalid.EndLaneIndex = type == ChartNoteType.Hold ? 3 : 6;

                if (type == ChartNoteType.Slide)
                {

                    invalid.SlideNodes.Add(new ChartNoteAuthoringData.PathNodeData { Time = 8.5d, LaneIndex = 6 });
                    invalid.SlideNodes.Add(new ChartNoteAuthoringData.PathNodeData { Time = 9.75d, LaneIndex = 6 });

                }

                fixture.AddBufferedNote(invalid);
                Assert.That(fixture.Chart.IsPartActive("synth", 9d), Is.False,
                    "The candidate must add and normalize an activation window before timing validation fails.");

                AssertRejectedWithoutMutation(fixture);

            }

        }

        [TestCase("missing-part")]
        [TestCase("unknown-part")]
        [TestCase("part-mismatch")]
        [TestCase("lane-mismatch")]
        [TestCase("null-record")]
        [TestCase("null-path-node")]
        [TestCase("invalid-type")]
        [TestCase("invalid-time")]
        public void CorruptRecordingIsRejectedBeforeItCanChangeTheChart(string corruption)
        {

            using (WorkspaceFixture fixture = new())
            {

                PrepareApply(fixture, "ReplaceRecordedPartsInLoop");
                ChartNoteAuthoringData data = ChartNoteAuthoringData.CreateTap("invalid", 9d, 3, "synth");
                fixture.AddBufferedNote(data);
                IList records = fixture.Get<IList>("recordedNotes");
                object record = records[0];

                switch (corruption)
                {

                    case "missing-part":
                        data.MusicalPartId = string.Empty;
                        SetRecordField(record, "musicalPartId", string.Empty);
                        break;
                    case "unknown-part":
                        data.MusicalPartId = "deleted-part";
                        SetRecordField(record, "musicalPartId", "deleted-part");
                        break;
                    case "part-mismatch":
                        data.MusicalPartId = "drum";
                        break;
                    case "lane-mismatch":
                        data.LaneIndex = 2;
                        break;
                    case "null-record":
                        records[0] = null;
                        break;
                    case "null-path-node":
                        data.NoteType = ChartNoteType.Slide;
                        data.EndTime = 9.75d;
                        data.SlideNodes.Add(null);
                        break;
                    case "invalid-type":
                        data.NoteType = (ChartNoteType)999;
                        break;
                    case "invalid-time":
                        SetRecordField(record, "hitTime", double.NaN);
                        break;

                }

                AssertRejectedWithoutMutation(fixture);

            }

        }

        [TestCase("null-note")]
        [TestCase("invalid-note-type")]
        [TestCase("invalid-activation-range")]
        public void InvalidExistingChartCannotBeSilentlyRepairedOrDiscardedByApply(string corruption)
        {

            using (WorkspaceFixture fixture = new())
            {

                PrepareApply(fixture, "Append");
                fixture.AddBufferedNote(ChartNoteAuthoringData.CreateTap("valid", 9d, 3, "synth"));

                if (corruption == "null-note")
                {

                    FieldInfo notesField = typeof(PrototypeChart).GetField(
                        "notes", BindingFlags.Instance | BindingFlags.NonPublic);
                    ((IList)notesField.GetValue(fixture.Chart))[0] = null;

                }
                else
                {

                    SerializedObject serialized = new(fixture.Chart);

                    if (corruption == "invalid-note-type")
                    {

                        serialized.FindProperty("notes").GetArrayElementAtIndex(0)
                            .FindPropertyRelative("noteType").intValue = 999;

                    }
                    else
                    {

                        SerializedProperty window = serialized.FindProperty("activationWindows").GetArrayElementAtIndex(0);
                        window.FindPropertyRelative("endTime").doubleValue =
                            window.FindPropertyRelative("startTime").doubleValue;

                    }

                    serialized.ApplyModifiedPropertiesWithoutUndo();

                }

                AssertRejectedWithoutMutation(fixture);

            }

        }

        [Test]
        public void ValidReplacementAndMissingActivationWindowApplyAndUndoTogether()
        {

            using (WorkspaceFixture fixture = new())
            {

                PrepareApply(fixture, "ReplaceRecordedPartsInLoop");
                ChartNoteAuthoringData hold = ChartNoteAuthoringData.CreateTap("replacement", 9d, 3, "synth");
                hold.NoteType = ChartNoteType.Hold;
                hold.EndTime = 9.75d;
                fixture.AddBufferedNote(hold);
                string originalChart = JsonUtility.ToJson(fixture.Chart);
                string originalBuffer = fixture.BufferJson;
                string otherParts = NonSynthNotesJson(fixture.Chart);
                Assert.That(fixture.Chart.IsPartActive("synth", 9d), Is.False);
                Undo.ClearUndo(fixture.Chart);
                Undo.ClearUndo(fixture.Window);

                fixture.Invoke("ApplyRecordedNotes");
                Undo.FlushUndoRecordObjects();

                Assert.That(fixture.Chart.Notes.Count, Is.EqualTo(4));
                Assert.That(NonSynthNotesJson(fixture.Chart), Is.EqualTo(otherParts));
                ChartNote applied = fixture.Chart.Notes[fixture.Chart.Notes.Count - 1];
                Assert.That(applied.NoteType, Is.EqualTo(ChartNoteType.Hold));
                Assert.That(applied.HitTime, Is.EqualTo(9d));
                Assert.That(applied.EndTime, Is.EqualTo(9.75d));
                Assert.That(applied.MusicalPartId, Is.EqualTo("synth"));
                Assert.That(fixture.Chart.IsPartActive("synth", 9d), Is.True);
                Assert.That(fixture.Chart.TryValidate(out string error), Is.True, error);
                Assert.That(fixture.Get<IList>("recordedNotes"), Is.Empty);
                string appliedChart = JsonUtility.ToJson(fixture.Chart);

                Undo.PerformUndo();

                Assert.That(JsonUtility.ToJson(fixture.Chart), Is.EqualTo(originalChart));
                Assert.That(fixture.BufferJson, Is.EqualTo(originalBuffer));
                Assert.That(fixture.Get<int>("selectedRecordedNoteIndex"), Is.EqualTo(0));
                Assert.That(fixture.Get<bool>("bufferWasApplied"), Is.False);

                Undo.PerformRedo();

                Assert.That(JsonUtility.ToJson(fixture.Chart), Is.EqualTo(appliedChart));
                Assert.That(fixture.Get<IList>("recordedNotes"), Is.Empty);
                Assert.That(fixture.Get<int>("selectedRecordedNoteIndex"), Is.EqualTo(-1));
                LogAssert.NoUnexpectedReceived();

            }

        }

        [Test]
        public void LegacyTapWithoutFullInteractionDataStillAppliesWithItsOriginalTimeLaneAndPart()
        {

            using (WorkspaceFixture fixture = new())
            {

                PrepareApply(fixture, "Append");
                fixture.AddBufferedNote(ChartNoteAuthoringData.CreateTap("legacy", 9d, 3, "synth"));
                object legacy = fixture.Get<IList>("recordedNotes")[0];
                SetRecordField(legacy, "noteData", null);
                int originalCount = fixture.Chart.Notes.Count;

                fixture.Invoke("ApplyRecordedNotes");
                Undo.FlushUndoRecordObjects();

                Assert.That(fixture.Chart.Notes.Count, Is.EqualTo(originalCount + 1));
                ChartNote applied = fixture.Chart.Notes[fixture.Chart.Notes.Count - 1];
                Assert.That(applied.NoteType, Is.EqualTo(ChartNoteType.Tap));
                Assert.That(applied.HitTime, Is.EqualTo(9d));
                Assert.That(applied.LaneIndex, Is.EqualTo(3));
                Assert.That(applied.MusicalPartId, Is.EqualTo("synth"));
                Assert.That(fixture.Chart.TryValidate(out string error), Is.True, error);
                Assert.That(fixture.Get<IList>("recordedNotes"), Is.Empty);
                LogAssert.NoUnexpectedReceived();

            }

        }

        private static void PrepareApply(WorkspaceFixture fixture, string applyMode)
        {

            fixture.Get<IList>("recordedNotes").Clear();
            fixture.SetEnum("applyMode", applyMode);
            fixture.Set("selectedRecordedNoteIndex", 0);
            fixture.Set("selectedChartNoteId", string.Empty);
            fixture.Set("addMissingActivationWindows", true);
            fixture.Set("clearBufferAfterApply", true);
            fixture.Set("bufferWasApplied", false);

        }

        private static void AssertRejectedWithoutMutation(WorkspaceFixture fixture)
        {

            string originalChart = JsonUtility.ToJson(fixture.Chart);
            string originalBuffer = BufferSnapshot(fixture);
            int selectedRecordedNote = fixture.Get<int>("selectedRecordedNoteIndex");
            string selectedChartNote = fixture.Get<string>("selectedChartNoteId");
            bool bufferWasApplied = fixture.Get<bool>("bufferWasApplied");
            int undoGroup = Undo.GetCurrentGroup();
            string undoGroupName = Undo.GetCurrentGroupName();

            Assert.DoesNotThrow(() => fixture.Invoke("ApplyRecordedNotes"));

            Assert.That(JsonUtility.ToJson(fixture.Chart), Is.EqualTo(originalChart));
            Assert.That(BufferSnapshot(fixture), Is.EqualTo(originalBuffer));
            Assert.That(fixture.Get<int>("selectedRecordedNoteIndex"), Is.EqualTo(selectedRecordedNote));
            Assert.That(fixture.Get<string>("selectedChartNoteId"), Is.EqualTo(selectedChartNote));
            Assert.That(fixture.Get<bool>("bufferWasApplied"), Is.EqualTo(bufferWasApplied));
            Assert.That(Undo.GetCurrentGroup(), Is.EqualTo(undoGroup),
                "Rejected application must not open an Undo group.");
            Assert.That(Undo.GetCurrentGroupName(), Is.EqualTo(undoGroupName));
            Assert.That(fixture.Get<string>("statusMessage"), Does.Contain("반영하지 않았습니다"));
            LogAssert.NoUnexpectedReceived();

        }

        private static string BufferSnapshot(WorkspaceFixture fixture)
        {

            string result = string.Empty;

            foreach (object record in fixture.Get<IList>("recordedNotes"))
            {

                result += record == null ? "<null>\n" : JsonUtility.ToJson(record) + "\n";

            }

            return result;

        }

        private static string NonSynthNotesJson(PrototypeChart chart)
        {

            string result = string.Empty;

            foreach (ChartNote note in chart.Notes)
            {

                if (note.MusicalPartId != "synth")
                {

                    result += JsonUtility.ToJson(note) + "\n";

                }

            }

            return result;

        }

        private static void SetRecordField(object record, string fieldName, object value)
        {

            record.GetType().GetField(fieldName).SetValue(record, value);

        }

    }

}
