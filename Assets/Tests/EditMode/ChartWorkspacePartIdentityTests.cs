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

    public sealed class ChartWorkspacePartIdentityTests
    {

        [UnityTest]
        public IEnumerator OpeningBufferDrawerPreservesUnassignedUnknownAndKnownPartIds()
        {

            using (WorkspaceFixture fixture = new())
            {

                fixture.Get<IList>("recordedNotes").Clear();
                string[] partIds = { null, string.Empty, "deleted-synth", "bass" };

                for (int index = 0; index < partIds.Length; index++)
                {

                    fixture.AddBufferedNote(ChartNoteAuthoringData.CreateTap(
                        "part-" + index, index + 1d, 3, partIds[index]));

                }

                fixture.Set("workspaceShowBufferDetails", true);
                string originalBuffer = fixture.BufferJson;
                string originalChart = JsonUtility.ToJson(fixture.Chart);
                fixture.Show(new Vector2(1200f, 900f));
                fixture.Render();
                yield return null;

                Assert.That(fixture.BufferJson, Is.EqualTo(originalBuffer),
                    "Drawing a part popup must not assign the first defined part to an unresolved recording.");
                Assert.That(JsonUtility.ToJson(fixture.Chart), Is.EqualTo(originalChart));
                Assert.That(fixture.Get<bool>("bufferWasApplied"), Is.False);
                LogAssert.NoUnexpectedReceived();

            }

        }

        [UnityTest]
        public IEnumerator OpeningBufferedNoteDetailsPreservesUnknownPartId()
        {

            using (WorkspaceFixture fixture = new())
            {

                fixture.Get<IList>("recordedNotes").Clear();
                fixture.AddBufferedNote(ChartNoteAuthoringData.CreateTap("missing-part", 3d, 3, "deleted-synth"));
                fixture.Set("selectedRecordedNoteIndex", 0);
                fixture.Set("selectedChartNoteId", string.Empty);
                fixture.Set("workspaceShowBufferDetails", true);
                string originalBuffer = fixture.BufferJson;
                fixture.Show(new Vector2(1200f, 900f));
                fixture.Render();
                yield return null;

                Assert.That(fixture.BufferJson, Is.EqualTo(originalBuffer),
                    "Opening selected-note details must not rewrite the missing part, including cached noteData.");
                LogAssert.NoUnexpectedReceived();

            }

        }

        [UnityTest]
        public IEnumerator OpeningAppliedNoteDetailsPreservesUnknownPartInChartAndEditCache()
        {

            using (WorkspaceFixture fixture = new())
            {

                SerializedObject serialized = new(fixture.Chart);
                SerializedProperty notes = serialized.FindProperty("notes");
                string selectedId = notes.GetArrayElementAtIndex(0).FindPropertyRelative("id").stringValue;
                notes.GetArrayElementAtIndex(0).FindPropertyRelative("musicalPartId").stringValue = "deleted-synth";
                serialized.ApplyModifiedPropertiesWithoutUndo();
                fixture.SelectNote(selectedId);
                string originalChart = JsonUtility.ToJson(fixture.Chart);
                fixture.Show(new Vector2(1200f, 900f));
                fixture.Render();
                yield return null;

                Assert.That(JsonUtility.ToJson(fixture.Chart), Is.EqualTo(originalChart));
                Assert.That(fixture.Get<ChartNoteAuthoringData>("selectedAppliedNoteData").MusicalPartId,
                    Is.EqualTo("deleted-synth"),
                    "A later timing edit must not apply a first-part fallback hidden in the selected-note cache.");
                LogAssert.NoUnexpectedReceived();

            }

        }

        [UnityTest]
        public IEnumerator OtherFieldChangesDoNotResolveMissingPartsAndEmptyChartsRemainSafe()
        {

            using (WorkspaceFixture fixture = new())
            {

                PartFieldTestWindow window = ScriptableObject.CreateInstance<PartFieldTestWindow>();

                try
                {

                    window.hideFlags = HideFlags.DontSave;
                    window.Recorder = fixture.Window;
                    window.Note = ChartNoteAuthoringData.CreateTap("missing-part", 3d, 3, "deleted-synth");
                    window.ShowUtility();
                    window.position = new Rect(80f, 80f, 500f, 180f);
                    yield return null;
                    Render(window);
                    Assert.That(window.Note.MusicalPartId, Is.EqualTo("deleted-synth"));
                    Assert.That(window.ParentChangePreserved, Is.True,
                        "The nested popup must preserve an unrelated field's change signal.");

                    window.Note.MusicalPartId = string.Empty;
                    Render(window);
                    Assert.That(window.Note.MusicalPartId, Is.Empty);

                    window.Note.MusicalPartId = "bass";
                    Render(window);
                    Assert.That(window.Note.MusicalPartId, Is.EqualTo("bass"));

                    SerializedObject serialized = new(fixture.Chart);
                    serialized.FindProperty("musicalParts").arraySize = 0;
                    serialized.ApplyModifiedPropertiesWithoutUndo();
                    window.Note.MusicalPartId = "deleted-synth";
                    Render(window);
                    Assert.That(window.Note.MusicalPartId, Is.EqualTo("deleted-synth"));
                    LogAssert.NoUnexpectedReceived();

                }
                finally
                {

                    window.Close();

                    if (window != null)
                    {

                        UnityEngine.Object.DestroyImmediate(window);

                    }

                }

            }

        }

        private static void Render(EditorWindow window)
        {

            window.SendEvent(new Event { type = EventType.Layout });
            window.SendEvent(new Event { type = EventType.Repaint });

        }

        private sealed class PartFieldTestWindow : EditorWindow
        {

            private static readonly MethodInfo DrawPart = typeof(PrototypeChartRecorderWindow).GetMethod(
                "DrawAppliedPartField", BindingFlags.Instance | BindingFlags.NonPublic);

            [NonSerialized] public PrototypeChartRecorderWindow Recorder;
            [NonSerialized] public ChartNoteAuthoringData Note;
            [NonSerialized] public bool ParentChangePreserved;

            private void OnGUI()
            {

                if (Recorder == null || Note == null)
                {

                    return;

                }

                // The inspector's time and lane fields precede the part popup and may already have
                // set GUI.changed. That signal alone is not an explicit part selection.
                GUI.changed = true;
                DrawPart.Invoke(Recorder, new object[] { Note });
                ParentChangePreserved = GUI.changed;

            }

        }

    }

}
