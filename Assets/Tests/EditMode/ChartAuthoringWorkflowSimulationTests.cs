using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Reflection;
using IdiotTape.EditorTools;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;
using WorkspaceFixture = IdiotTape.Gameplay.Tests.ChartWorkspaceWindowTests.WorkspaceFixture;

namespace IdiotTape.Gameplay.Tests
{

    public sealed class ChartAuthoringWorkflowSimulationTests
    {

        private const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;
        private const int CorrectionNoteCount = 12;
        private const double CorrectionSeconds = 0.125d;
        private static WorkspaceFixture simulationFixture;

        public static void OpenSimulationWorkspace()
        {

            CloseSimulationWorkspace();
            simulationFixture = new WorkspaceFixture();
            ConfigureTapPattern(simulationFixture, CorrectionNoteCount);
            simulationFixture.Show(new Vector2(1200f, 800f));
            for (int index = 0; index < 4; index++)
            {

                simulationFixture.Invoke("SetWorkspaceNoteSelection", "workflow-" + index, -1, index > 0);

            }

            simulationFixture.Render();

        }

        public static void CloseSimulationWorkspace()
        {

            simulationFixture?.Dispose();
            simulationFixture = null;

        }

        public static void CaptureSimulationWorkspace()
        {

            string directory = Environment.GetEnvironmentVariable("IDIOT_TAPE_AUTHORING_EVIDENCE");
            if (Application.isBatchMode || string.IsNullOrWhiteSpace(directory) || !Path.IsPathRooted(directory))
            {

                throw new InvalidOperationException("Visual capture needs a rendered Editor and absolute IDIOT_TAPE_AUTHORING_EVIDENCE directory.");

            }

            Directory.CreateDirectory(directory);
            OpenSimulationWorkspace();
            EditorApplication.delayCall += () => CaptureSimulationStep(directory, 0);

        }

        private static void CaptureSimulationStep(string directory, int step)
        {

            try
            {

                simulationFixture.Render();
                string[] filenames = { "workflow-wide.png", "workflow-compact.png", "workflow-compact-inspector.png" };
                typeof(ChartWorkspaceWindowTests).GetMethod("SaveWindowPixels", BindingFlags.Static | BindingFlags.NonPublic)
                    .Invoke(null, new object[] { simulationFixture.Window, Path.Combine(directory, filenames[step]) });
                if (step < 2)
                {

                    simulationFixture.Window.position = new Rect(80f, 80f, 700f, 600f);
                    simulationFixture.Set("workspaceCompactInspector", step == 1);
                    EditorApplication.delayCall += () => CaptureSimulationStep(directory, step + 1);

                }
                else
                {

                    simulationFixture.Window.position = new Rect(80f, 80f, 1200f, 800f);
                    simulationFixture.Render();
                    File.WriteAllText(Path.Combine(directory, "workflow-visual-capture.txt"),
                        "Captured the actual Unity IMGUI view in wide, compact canvas and compact inspector states.\n" +
                        "Synthetic in-memory fixture; production chart assets unchanged; window remains open for inspection.\n");

                }

            }
            catch (Exception error)
            {

                File.WriteAllText(Path.Combine(directory, "workflow-visual-capture-error.txt"), error.ToString());
                UnityEngine.Debug.LogException(error);

            }

        }

        [UnityTest]
        public IEnumerator IndividualAndBulkGuiCorrectionsProduceTheSameTwelveNoteTimingChange()
        {

            // Both workflows run against the same build and fixture. The first uses the retained
            // single-note millisecond buttons; the second uses the new selection and grid keys.
            // Counts and elapsed times measure automation, not human chart-authoring speed.
            foreach (bool bulk in new[] { false, true })
            {

                using (WorkspaceFixture fixture = new())
                {

                    ConfigureTapPattern(fixture, CorrectionNoteCount);
                    fixture.Set("noteNudgeMilliseconds", 125f);
                    fixture.Show(new Vector2(1200f, 800f));
                    yield return null;
                    fixture.Render();
                    GuiActions actions = new(fixture);
                    Stopwatch elapsed = Stopwatch.StartNew();

                    if (bulk)
                    {

                        actions.Key(KeyCode.A, EventModifiers.Control);
                        Assert.That(SelectionCount(fixture), Is.EqualTo(CorrectionNoteCount));
                        actions.Key(KeyCode.DownArrow);

                    }
                    else
                    {

                        for (int index = 0; index < CorrectionNoteCount; index++)
                        {

                            ChartNote note = fixture.FindNote("workflow-" + index);
                            actions.Click(NotePosition(fixture, note.HitTime, note.LaneIndex));
                            Assert.That(fixture.Get<string>("selectedChartNoteId"), Is.EqualTo(note.Id));
                            actions.Click(ActionRect(fixture, "legacyBackward").center);

                        }

                    }

                    elapsed.Stop();
                    for (int index = 0; index < CorrectionNoteCount; index++)
                    {

                        Assert.That(fixture.FindNote("workflow-" + index).HitTime,
                            Is.EqualTo(PatternTime(index) + CorrectionSeconds).Within(0.000001d));

                    }

                    Assert.That(fixture.Chart.TryValidate(out string error), Is.True, error);
                    Assert.That(actions.ActionCount, Is.EqualTo(bulk ? 2 : CorrectionNoteCount * 2));
                    WriteEvidence(bulk ? "bulk-twelve-tap-correction" : "individual-twelve-tap-correction",
                        "EditorWindow.SendEvent mouse/key input; setup excluded; same new build, retained old controls",
                        actions, elapsed.Elapsed.TotalMilliseconds);
                    LogAssert.NoUnexpectedReceived();

                }

                yield return null;

            }

        }

        [UnityTest]
        public IEnumerator ShiftClicksAndAdditiveDragSelectAppliedAndBufferedNotesForOneGridMove()
        {

            using (WorkspaceFixture fixture = new())
            {

                ReplaceNotes(fixture, new List<ChartNoteAuthoringData>
                {
                    ChartNoteAuthoringData.CreateTap("applied-first", 2d, 1, "synth"),
                    ChartNoteAuthoringData.CreateTap("applied-last", 4d, 4, "synth")
                });
                fixture.AddBufferedNote(ChartNoteAuthoringData.CreateTap("buffer-middle", 3d, 2, "synth"));
                fixture.Show(new Vector2(1200f, 800f));
                yield return null;
                fixture.Render();
                GuiActions actions = new(fixture);
                Stopwatch elapsed = Stopwatch.StartNew();
                actions.Click(NotePosition(fixture, 2d, 1));
                actions.Click(NotePosition(fixture, 3d, 2), EventModifiers.Shift);
                Assert.That(SelectionCount(fixture), Is.EqualTo(2));
                actions.Key(KeyCode.DownArrow);
                Assert.That(fixture.FindNote("applied-first").HitTime, Is.EqualTo(2d + CorrectionSeconds));
                Assert.That(BufferedData(fixture, 0).HitTime, Is.EqualTo(3d + CorrectionSeconds));
                Assert.That(fixture.FindNote("applied-last").HitTime, Is.EqualTo(4d));

                actions.Click(NotePosition(fixture, 2d + CorrectionSeconds, 1));
                Vector2 start = NotePosition(fixture, 2.8d, 2) + new Vector2(-16f, 0f);
                Vector2 end = NotePosition(fixture, 4.3d, 4) + new Vector2(16f, 0f);
                actions.Drag(start, end, EventModifiers.Shift);
                Assert.That(SelectionCount(fixture), Is.EqualTo(3),
                    "Shift-drag must add the buffered and later applied note to the existing selection.");
                actions.Key(KeyCode.RightArrow);
                elapsed.Stop();
                Assert.That(fixture.FindNote("applied-first").LaneIndex, Is.EqualTo(2));
                Assert.That(BufferedData(fixture, 0).LaneIndex, Is.EqualTo(3));
                Assert.That(fixture.FindNote("applied-last").LaneIndex, Is.EqualTo(5));
                Assert.That(fixture.Chart.TryValidate(out string error), Is.True, error);
                WriteEvidence("mixed-selection-shift-drag", "EditorWindow.SendEvent clicks, drag and grid keys",
                    actions, elapsed.Elapsed.TotalMilliseconds);
                LogAssert.NoUnexpectedReceived();

            }

        }

        [UnityTest]
        public IEnumerator FocusedNumericFieldKeepsArrowKeysAndSelectionSurvivesWideAndCompactRepaint()
        {

            using (WorkspaceFixture fixture = new())
            {

                ConfigureTapPattern(fixture, 3);
                fixture.Show(new Vector2(1200f, 800f));
                yield return null;
                fixture.Render();
                GuiActions actions = new(fixture);
                Stopwatch elapsed = Stopwatch.StartNew();
                actions.Click(NotePosition(fixture, PatternTime(0), 2));
                Rect field = ActionRect(fixture, "startTimeField");
                actions.Click(new Vector2(field.xMax - 24f, field.center.y));
                Assert.That(EditorGUIUtility.editingTextField, Is.True,
                    "The test must focus the real IMGUI numeric field before checking keyboard ownership.");
                string chartBefore = JsonUtility.ToJson(fixture.Chart);
                actions.Key(KeyCode.DownArrow);
                actions.Key(KeyCode.RightArrow);
                Assert.That(JsonUtility.ToJson(fixture.Chart), Is.EqualTo(chartBefore),
                    "Editing a numeric value must not also move the selected note.");

                actions.Click(NotePosition(fixture, PatternTime(0), 2));
                actions.Key(KeyCode.A, EventModifiers.Control);
                Assert.That(SelectionCount(fixture), Is.EqualTo(3));
                foreach (Vector2 size in new[] { new Vector2(1200f, 800f), new Vector2(700f, 600f) })
                {

                    fixture.Window.position = new Rect(new Vector2(80f, 80f), size);
                    foreach (bool compactInspector in new[] { false, true })
                    {

                        fixture.Set("workspaceCompactInspector", compactInspector);
                        fixture.Render();
                        yield return null;
                        Assert.That(SelectionCount(fixture), Is.EqualTo(3));
                        Assert.That(JsonUtility.ToJson(fixture.Chart), Is.EqualTo(chartBefore));
                        LogAssert.NoUnexpectedReceived();

                    }

                }

                elapsed.Stop();
                WriteEvidence("text-focus-and-responsive-repaint", "Real numeric-field focus and key events; Layout/Repaint at 1200x800 and 700x600",
                    actions, elapsed.Elapsed.TotalMilliseconds);

            }

        }

        [Test]
        public void BulkCorrectionPreservesInteractionStructureAcrossBufferUndoAndAssetRoundTrip()
        {

            using (WorkspaceFixture fixture = new())
            {

                fixture.Get<IList>("recordedNotes").Clear();
                ChartNoteAuthoringData buffered = ChartNoteAuthoringData.FromChartNote(fixture.FindNote("banana"));
                buffered.Id = "buffer-banana";
                fixture.AddBufferedNote(buffered);
                Dictionary<string, ChartNoteAuthoringData> before = new();
                foreach (string id in new[] { "hold", "slide", "banana" })
                {

                    before.Add(id, ChartNoteAuthoringData.FromChartNote(fixture.FindNote(id)));
                    fixture.Invoke("SetWorkspaceNoteSelection", id, -1, before.Count > 1);

                }

                fixture.Invoke("SetWorkspaceNoteSelection", string.Empty, 0, true);
                string chartBefore = JsonUtility.ToJson(fixture.Chart);
                string bufferBefore = fixture.BufferJson;
                string untouchedTap = JsonUtility.ToJson(fixture.FindNote("tap"));
                Stopwatch elapsed = Stopwatch.StartNew();
                Undo.IncrementCurrentGroup();
                Assert.That(Transform(fixture, 0.5d, 0d, 1, "bass"), Is.True);
                Undo.FlushUndoRecordObjects();
                Assert.That(SelectionCount(fixture), Is.EqualTo(4));
                foreach (KeyValuePair<string, ChartNoteAuthoringData> pair in before)
                {

                    AssertTranslated(pair.Value, ChartNoteAuthoringData.FromChartNote(fixture.FindNote(pair.Key)),
                        fixture.Chart.LaneCount, 0.5d, 1, "bass");

                }

                AssertTranslated(before["banana"], BufferedData(fixture, 0), fixture.Chart.LaneCount, 0.5d, 1, "bass");
                Assert.That(JsonUtility.ToJson(fixture.FindNote("tap")), Is.EqualTo(untouchedTap));
                Assert.That(fixture.Chart.TryValidate(out string error), Is.True, error);
                string transformedChart = JsonUtility.ToJson(fixture.Chart);
                AssertAssetRoundTrip(fixture.Chart);
                Undo.PerformUndo();
                Assert.That(JsonUtility.ToJson(fixture.Chart), Is.EqualTo(chartBefore));
                Assert.That(fixture.BufferJson, Is.EqualTo(bufferBefore));
                Undo.PerformRedo();
                Assert.That(JsonUtility.ToJson(fixture.Chart), Is.EqualTo(transformedChart));
                elapsed.Stop();
                WriteEvidence("interaction-bulk-undo-save-reload", "Method-level transform plus real Unity Undo and temporary asset save/import/reload; no GUI action count",
                    null, elapsed.Elapsed.TotalMilliseconds);
                LogAssert.NoUnexpectedReceived();

            }

        }

        private static bool Transform(WorkspaceFixture fixture, double seconds, double beats, int lanes, string part)
        {

            return (bool)typeof(PrototypeChartRecorderWindow).GetMethod("TransformWorkspaceSelection", PrivateInstance)
                .Invoke(fixture.Window, new object[] { seconds, beats, lanes, part, false });

        }

        private static int SelectionCount(WorkspaceFixture fixture)
        {

            return (int)typeof(PrototypeChartRecorderWindow).GetMethod("GetWorkspaceSelectionCount", PrivateInstance)
                .Invoke(fixture.Window, null);

        }

        private static void ConfigureTapPattern(WorkspaceFixture fixture, int count)
        {

            List<ChartNoteAuthoringData> notes = new();
            for (int index = 0; index < count; index++)
            {

                notes.Add(ChartNoteAuthoringData.CreateTap("workflow-" + index, PatternTime(index), 2, "synth"));

            }

            ReplaceNotes(fixture, notes);

        }

        private static double PatternTime(int index)
        {

            return 1d + index * 0.375d;

        }

        private static void ReplaceNotes(WorkspaceFixture fixture, List<ChartNoteAuthoringData> notes)
        {

            fixture.Get<IList>("recordedNotes").Clear();
            SerializedObject serialized = new(fixture.Chart);
            SerializedProperty stored = serialized.FindProperty("notes");
            stored.arraySize = notes.Count;
            for (int index = 0; index < notes.Count; index++)
            {

                notes[index].WriteTo(stored.GetArrayElementAtIndex(index));

            }

            serialized.ApplyModifiedPropertiesWithoutUndo();
            fixture.Set("selectedPartIndex", 0);
            fixture.SetEnum("quantizationGrid", "Sixteenth");
            fixture.Set("workspaceShowBufferDetails", false);
            fixture.Set("workspaceCompactInspector", false);
            fixture.Set("workspaceInspectorScroll", Vector2.zero);
            fixture.Invoke("SetWorkspaceNoteSelection", string.Empty, -1, false);
            Assert.That(fixture.Chart.TryValidate(out string error), Is.True, error);

        }

        private static Vector2 NotePosition(WorkspaceFixture fixture, double time, int lane)
        {

            PrototypeChartRecorderWindow.WorkspaceLayout layout = PrototypeChartRecorderWindow.CalculateWorkspaceLayout(
                fixture.Window.position.width, fixture.Window.position.height, fixture.Get<bool>("workspaceShowBufferDetails"));
            float canvasBottom = Mathf.Max(109f, layout.Main.height - 106f);
            Rect plot = new(layout.Main.x + 44f, layout.Main.y + 73f + 30f,
                layout.Main.width - 56f, canvasBottom - 73f - 52f);
            double start = fixture.Get<double>("timelineStartTime");
            float duration = fixture.Get<float>("timelineVisibleDuration");
            return new Vector2(plot.x + plot.width * (lane + 0.5f) / fixture.Chart.LaneCount,
                plot.y + plot.height * (float)((time - start) / duration));

        }

        private static Rect ActionRect(WorkspaceFixture fixture, string name)
        {

            fixture.Render();
            Dictionary<string, Rect> rects = fixture.Get<Dictionary<string, Rect>>("workspaceSelectionActionRects");
            Assert.That(rects.ContainsKey(name), Is.True, "The visible control has no recorded window-local bounds: " + name);
            Rect rect = rects[name];
            Assert.That(rect.width, Is.GreaterThan(1f));
            Assert.That(new Rect(Vector2.zero, fixture.Window.position.size).Contains(rect.center), Is.True,
                "The clicked control must be inside the window: " + name);
            return rect;

        }

        private static ChartNoteAuthoringData BufferedData(WorkspaceFixture fixture, int index)
        {

            object record = fixture.Get<IList>("recordedNotes")[index];
            return (ChartNoteAuthoringData)record.GetType().GetField("noteData").GetValue(record);

        }

        private static void AssertTranslated(ChartNoteAuthoringData original, ChartNoteAuthoringData edited,
            int laneCount, double seconds, int lanes, string part)
        {

            float normalizedShift = (float)lanes / laneCount;
            Assert.That(edited.NoteType, Is.EqualTo(original.NoteType));
            Assert.That(edited.HitTime, Is.EqualTo(original.HitTime + seconds).Within(0.000001d));
            Assert.That(edited.EndTime, Is.EqualTo(original.EndTime + seconds).Within(0.000001d));
            Assert.That(edited.LaneIndex, Is.EqualTo(original.LaneIndex + lanes));
            Assert.That(edited.EndLaneIndex, Is.EqualTo(original.EndLaneIndex + lanes));
            Assert.That(edited.MusicalPartId, Is.EqualTo(part));
            Assert.That(edited.SlideEndBehavior, Is.EqualTo(original.SlideEndBehavior));
            Assert.That(edited.BananaMaximumBonusCombo, Is.EqualTo(original.BananaMaximumBonusCombo));
            Assert.That(edited.SlideNodes.Count, Is.EqualTo(original.SlideNodes.Count));
            Assert.That(edited.BananaCurveHandles.Count, Is.EqualTo(original.BananaCurveHandles.Count));
            Assert.That(edited.BananaCheckpoints.Count, Is.EqualTo(original.BananaCheckpoints.Count));
            for (int index = 0; index < original.SlideNodes.Count; index++)
            {

                Assert.That(edited.SlideNodes[index].Time, Is.EqualTo(original.SlideNodes[index].Time + seconds).Within(0.000001d));
                Assert.That(edited.SlideNodes[index].LaneIndex, Is.EqualTo(original.SlideNodes[index].LaneIndex + lanes));

            }

            for (int index = 0; index < original.BananaCurveHandles.Count; index++)
            {

                Assert.That(edited.BananaCurveHandles[index].NormalizedTime, Is.EqualTo(original.BananaCurveHandles[index].NormalizedTime));
                Assert.That(edited.BananaCurveHandles[index].NormalizedX,
                    Is.EqualTo(original.BananaCurveHandles[index].NormalizedX + normalizedShift).Within(0.000001f));

            }

            for (int index = 0; index < original.BananaCheckpoints.Count; index++)
            {

                Assert.That(edited.BananaCheckpoints[index].Time,
                    Is.EqualTo(original.BananaCheckpoints[index].Time + seconds).Within(0.000001d));
                Assert.That(edited.BananaCheckpoints[index].NormalizedX,
                    Is.EqualTo(original.BananaCheckpoints[index].NormalizedX + normalizedShift).Within(0.000001f));

            }

        }

        private static void AssertAssetRoundTrip(PrototypeChart chart)
        {

            string folder = "Assets/__AuthoringWorkflowSimulation_" + Guid.NewGuid().ToString("N");
            string path = folder + "/RoundTrip.asset";
            AssetDatabase.CreateFolder("Assets", Path.GetFileName(folder));
            PrototypeChart copy = Object.Instantiate(chart);
            copy.hideFlags = HideFlags.None;
            try
            {

                AssetDatabase.CreateAsset(copy, path);
                EditorUtility.SetDirty(copy);
                AssetDatabase.SaveAssetIfDirty(copy);
                string expected = JsonUtility.ToJson(copy);
                Assert.That(File.Exists(path), Is.True);
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
                PrototypeChart reloaded = AssetDatabase.LoadAssetAtPath<PrototypeChart>(path);
                Assert.That(reloaded.TryValidate(out string error), Is.True, error);
                Assert.That(JsonUtility.ToJson(reloaded), Is.EqualTo(expected));

            }
            finally
            {

                // This uniquely named folder was created by this test and contains no user assets.
                AssetDatabase.DeleteAsset(folder);

            }

        }

        private static void WriteEvidence(string scenario, string method, GuiActions actions, double milliseconds)
        {

            string line = $"scenario={scenario}\tmethod={method}\tinputActions={actions?.ActionCount ?? 0}" +
                $"\tinputEvents={actions?.InputEventCount ?? 0}\tautomationMilliseconds={milliseconds.ToString("F3", CultureInfo.InvariantCulture)}" +
                "\thumanAuthoringSpeed=not-measured";
            TestContext.Progress.WriteLine(line);
            string directory = Environment.GetEnvironmentVariable("IDIOT_TAPE_AUTHORING_EVIDENCE");
            if (!string.IsNullOrWhiteSpace(directory))
            {

                Directory.CreateDirectory(directory);
                File.AppendAllText(Path.Combine(directory, "workflow-simulation.tsv"), line + Environment.NewLine);

            }

        }

        private sealed class GuiActions
        {

            private readonly WorkspaceFixture fixture;
            public int ActionCount { get; private set; }
            public int InputEventCount { get; private set; }

            public GuiActions(WorkspaceFixture fixture)
            {

                this.fixture = fixture;

            }

            public void Click(Vector2 point, EventModifiers modifiers = EventModifiers.None)
            {

                fixture.Render();
                Send(new Event { type = EventType.MouseDown, button = 0, clickCount = 1, mousePosition = point, modifiers = modifiers });
                Send(new Event { type = EventType.MouseUp, button = 0, clickCount = 1, mousePosition = point, modifiers = modifiers });
                fixture.Render();
                ActionCount++;

            }

            public void Key(KeyCode key, EventModifiers modifiers = EventModifiers.None)
            {

                Send(new Event { type = EventType.KeyDown, keyCode = key, modifiers = modifiers });
                Send(new Event { type = EventType.KeyUp, keyCode = key, modifiers = modifiers });
                fixture.Render();
                ActionCount++;

            }

            public void Drag(Vector2 start, Vector2 end, EventModifiers modifiers)
            {

                Send(new Event { type = EventType.MouseDown, button = 0, clickCount = 1, mousePosition = start, modifiers = modifiers });
                Send(new Event { type = EventType.MouseDrag, button = 0, mousePosition = end, delta = end - start, modifiers = modifiers });
                Send(new Event { type = EventType.MouseUp, button = 0, mousePosition = end, modifiers = modifiers });
                fixture.Render();
                ActionCount++;

            }

            private void Send(Event input)
            {

                fixture.Window.SendEvent(input);
                InputEventCount++;

            }

        }

    }

}
