using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using IdiotTape.EditorTools;
using NUnit.Framework;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace IdiotTape.Gameplay.Tests
{

    public sealed class ChartWorkspaceWindowTests
    {

        private const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;
        private static IEnumerator captureSequence;
        private static double captureNextUpdate;
        private static string captureFailure;

        [TestCase(700f, 600f, false)]
        [TestCase(700f, 600f, true)]
        [TestCase(999f, 720f, true)]
        [TestCase(1000f, 600f, true)]
        [TestCase(1200f, 800f, false)]
        [TestCase(1600f, 1000f, true)]
        public void FixedWorkspacePanesRemainInsideWindowWithoutOverlap(float width, float height, bool expandedBuffer)
        {

            PrototypeChartRecorderWindow.WorkspaceLayout layout =
                PrototypeChartRecorderWindow.CalculateWorkspaceLayout(width, height, expandedBuffer);
            Rect bounds = new(0f, 0f, width, height);
            Rect[] panes = { layout.Transport, layout.Loop, layout.Parts, layout.Main, layout.Buffer, layout.Status };

            foreach (Rect pane in panes)
            {

                AssertContained(bounds, pane);
                Assert.That(pane.width, Is.GreaterThan(0f));
                Assert.That(pane.height, Is.GreaterThan(0f));

            }

            for (int first = 0; first < panes.Length; first++)
            {

                for (int second = first + 1; second < panes.Length; second++)
                {

                    Assert.That(panes[first].Overlaps(panes[second]), Is.False, $"Panes {first} and {second} overlap.");

                }

            }

            Assert.That(layout.Main.width, Is.GreaterThanOrEqualTo(528f));
            Assert.That(layout.Main.height, Is.GreaterThanOrEqualTo(270f));
            Assert.That(layout.Compact, Is.EqualTo(width < 1000f));

            if (!layout.Compact)
            {

                AssertContained(bounds, layout.Inspector);
                Assert.That(layout.Inspector.width, Is.GreaterThanOrEqualTo(320f));

                foreach (Rect pane in panes)
                {

                    Assert.That(layout.Inspector.Overlaps(pane), Is.False);

                }

            }

        }

        [UnityTest]
        public IEnumerator ViewButtonsSwitchBothWaysRepeatedlyThroughMouseClicks()
        {

            using (WorkspaceFixture fixture = new())
            {

                string originalChart = JsonUtility.ToJson(fixture.Chart);
                string originalBuffer = fixture.BufferJson;
                fixture.Show(new Vector2(1200f, 800f));
                yield return null;
                Assert.That(fixture.Get<object>("timelineViewMode").ToString(), Is.EqualTo("Vertical"));

                for (int repetition = 0; repetition < 3; repetition++)
                {

                    PrototypeChartRecorderWindow.WorkspaceLayout layout =
                        PrototypeChartRecorderWindow.CalculateWorkspaceLayout(
                            fixture.Window.position.width, fixture.Window.position.height, false);
                    fixture.Click(new Vector2(layout.Main.x + 140f, layout.Main.y + 20f));
                    yield return null;
                    Assert.That(fixture.Get<object>("timelineViewMode").ToString(), Is.EqualTo("Horizontal"),
                        $"Clicking 파트 개요 did not enter the overview on repetition {repetition + 1}.");

                    fixture.Click(new Vector2(layout.Main.x + 53f, layout.Main.y + 20f));
                    yield return null;
                    Assert.That(fixture.Get<object>("timelineViewMode").ToString(), Is.EqualTo("Vertical"),
                        $"Clicking 노트 편집 did not return from the overview on repetition {repetition + 1}.");
                    Assert.That(JsonUtility.ToJson(fixture.Chart), Is.EqualTo(originalChart));
                    Assert.That(fixture.BufferJson, Is.EqualTo(originalBuffer));
                    LogAssert.NoUnexpectedReceived();

                }

            }

        }

        [UnityTest]
        public IEnumerator InspectorTabsAndTempoFoldoutRespondToClicksWithASelectedBufferNote()
        {

            using (WorkspaceFixture fixture = new())
            {

                fixture.Set("selectedRecordedNoteIndex", 0);
                fixture.Set("selectedChartNoteId", string.Empty);
                string originalChart = JsonUtility.ToJson(fixture.Chart);
                string originalBuffer = fixture.BufferJson;
                fixture.Show(new Vector2(1200f, 800f));
                yield return null;
                PrototypeChartRecorderWindow.WorkspaceLayout layout =
                    PrototypeChartRecorderWindow.CalculateWorkspaceLayout(
                        fixture.Window.position.width, fixture.Window.position.height, false);
                fixture.Click(new Vector2(layout.Buffer.x + 80f, layout.Buffer.y + 20f));
                yield return null;
                Assert.That(fixture.Get<bool>("workspaceShowBufferDetails"), Is.True,
                    "Clicking the temporary-recording drawer did not expand it.");

                string[] tabs = { "Note", "Part", "Recording", "Tempo", "Tools" };

                foreach (int tabIndex in new[] { 1, 2, 3, 4, 0, 1 })
                {

                    layout = PrototypeChartRecorderWindow.CalculateWorkspaceLayout(
                        fixture.Window.position.width, fixture.Window.position.height, true);
                    fixture.Click(new Vector2(
                        layout.Inspector.x + 9f + (layout.Inspector.width - 18f) * ((tabIndex + 0.5f) / tabs.Length),
                        layout.Inspector.y + 20f));
                    yield return null;
                    Assert.That(fixture.Get<object>("workspaceInspectorTab").ToString(), Is.EqualTo(tabs[tabIndex]),
                        "The selected buffer note overrode a clicked inspector tab.");

                    if (tabs[tabIndex] == "Tempo")
                    {

                        Assert.That(fixture.Get<bool>("showTempoCalibration"), Is.True);
                        Vector2 tempoFoldout = new(layout.Inspector.x + 75f, layout.Inspector.y + 54f);
                        fixture.Click(tempoFoldout);
                        yield return null;
                        Assert.That(fixture.Get<bool>("showTempoCalibration"), Is.False,
                            "The tempo section reopened after its foldout was clicked closed.");
                        fixture.Click(tempoFoldout);
                        yield return null;
                        Assert.That(fixture.Get<bool>("showTempoCalibration"), Is.True,
                            "The tempo section did not reopen when clicked.");

                    }

                    Assert.That(fixture.Get<int>("selectedRecordedNoteIndex"), Is.EqualTo(0));
                    Assert.That(JsonUtility.ToJson(fixture.Chart), Is.EqualTo(originalChart));
                    Assert.That(fixture.BufferJson, Is.EqualTo(originalBuffer));
                    LogAssert.NoUnexpectedReceived();

                }

            }

        }

        [UnityTest]
        public IEnumerator CompactInspectorAndCanvasCanBeReopenedRepeatedlyThroughMouseClicks()
        {

            using (WorkspaceFixture fixture = new())
            {

                string originalChart = JsonUtility.ToJson(fixture.Chart);
                string originalBuffer = fixture.BufferJson;
                fixture.Show(new Vector2(700f, 600f));
                yield return null;

                for (int repetition = 0; repetition < 3; repetition++)
                {

                    PrototypeChartRecorderWindow.WorkspaceLayout layout =
                        PrototypeChartRecorderWindow.CalculateWorkspaceLayout(
                            fixture.Window.position.width, fixture.Window.position.height, false);
                    Assert.That(layout.Compact, Is.True);
                    fixture.Click(new Vector2(layout.Main.xMax - 46f, layout.Main.y + 20f));
                    yield return null;
                    Assert.That(fixture.Get<bool>("workspaceCompactInspector"), Is.True,
                        "Clicking 속성 열기 did not show the compact inspector.");

                    fixture.Click(new Vector2(layout.Main.center.x, layout.Main.y + 20f));
                    yield return null;
                    Assert.That(fixture.Get<bool>("workspaceCompactInspector"), Is.False,
                        "Clicking 채보로 돌아가기 did not restore the canvas.");
                    Assert.That(fixture.Get<string>("selectedChartNoteId"), Is.EqualTo("slide"));
                    Assert.That(JsonUtility.ToJson(fixture.Chart), Is.EqualTo(originalChart));
                    Assert.That(fixture.BufferJson, Is.EqualTo(originalBuffer));
                    LogAssert.NoUnexpectedReceived();

                }

            }

        }

        [UnityTest]
        public IEnumerator BothViewsAndEveryInspectorRenderWithoutChangingChartOrApplyingBuffer()
        {

            using (WorkspaceFixture fixture = new())
            {

                string originalChart = JsonUtility.ToJson(fixture.Chart);
                string originalBuffer = fixture.BufferJson;
                fixture.Show(new Vector2(1200f, 800f));
                yield return null;

                foreach (string view in new[] { "Vertical", "Horizontal" })
                {

                    fixture.SetEnum("timelineViewMode", view);
                    fixture.Set("workspaceShowChecks", true);
                    fixture.Set("workspaceShowContext", true);

                    foreach (string tab in new[] { "Note", "Part", "Recording", "Tempo", "Tools" })
                    {

                        fixture.Set("workspaceShowBufferDetails", true);
                        fixture.Set("selectedRecordedNoteIndex", 0);
                        fixture.SetEnum("workspaceInspectorTab", tab);
                        fixture.Render();
                        yield return null;
                        Assert.That(JsonUtility.ToJson(fixture.Chart), Is.EqualTo(originalChart), $"{view}/{tab} changed chart data.");
                        Assert.That(fixture.BufferJson, Is.EqualTo(originalBuffer), $"{view}/{tab} changed the recording buffer.");
                        Assert.That(fixture.Get<object>("workspaceInspectorTab").ToString(), Is.EqualTo(tab));
                        LogAssert.NoUnexpectedReceived();

                    }

                }

                Assert.That(fixture.Get<bool>("bufferWasApplied"), Is.False);
                Assert.That(fixture.Chart.Notes.Count, Is.EqualTo(8));

            }

        }

        [UnityTest]
        public IEnumerator EveryNoteTypeAndCompactBufferRenderWithoutChangingTheSelectedData()
        {

            using (WorkspaceFixture fixture = new())
            {

                string originalChart = JsonUtility.ToJson(fixture.Chart);
                string originalBuffer = fixture.BufferJson;
                fixture.Show(new Vector2(1200f, 800f));

                foreach (string noteId in new[] { "tap", "hold", "slide", "flick", "banana" })
                {

                    fixture.SelectNote(noteId);
                    fixture.Set("workspaceShowNodeFields", true);
                    fixture.Set("workspaceShowCurveFields", true);
                    fixture.Set("workspaceShowCheckpointFields", true);
                    fixture.Render();
                    yield return null;
                    Assert.That(JsonUtility.ToJson(fixture.Chart), Is.EqualTo(originalChart), noteId + " inspector changed note data on repaint.");
                    LogAssert.NoUnexpectedReceived();

                }

                fixture.Window.position = new Rect(80f, 80f, 700f, 600f);
                fixture.Set("workspaceShowBufferDetails", true);

                foreach (bool showInspector in new[] { false, true })
                {

                    fixture.Set("workspaceCompactInspector", showInspector);
                    fixture.Render();
                    yield return null;
                    Assert.That(JsonUtility.ToJson(fixture.Chart), Is.EqualTo(originalChart));
                    Assert.That(fixture.BufferJson, Is.EqualTo(originalBuffer));
                    LogAssert.NoUnexpectedReceived();

                }

            }

        }

        [Test]
        public void ChangingSelectedPartOnlyChangesSelectionAndPreservesChartAndBuffer()
        {

            using (WorkspaceFixture fixture = new())
            {

                fixture.SelectNote("slide");
                string originalChart = JsonUtility.ToJson(fixture.Chart);
                string originalBuffer = fixture.BufferJson;
                fixture.Invoke("SelectWorkspacePart", 1);
                Assert.That(fixture.Get<int>("selectedPartIndex"), Is.EqualTo(1));
                Assert.That(fixture.Get<string>("selectedChartNoteId"), Is.Empty);
                Assert.That(fixture.Get<ChartNoteAuthoringData>("selectedAppliedNoteData"), Is.Null);
                Assert.That(JsonUtility.ToJson(fixture.Chart), Is.EqualTo(originalChart));
                Assert.That(fixture.BufferJson, Is.EqualTo(originalBuffer));

            }

        }

        [Test]
        public void AppliedBananaInspectorEditsRoundTripAndUndoWithoutChangingOtherNotes()
        {

            using (WorkspaceFixture fixture = new())
            {

                string originalChart = JsonUtility.ToJson(fixture.Chart);
                string originalBuffer = fixture.BufferJson;
                fixture.SelectNote("banana");
                ChartNoteAuthoringData data = fixture.Get<ChartNoteAuthoringData>("selectedAppliedNoteData");
                data.BananaCurveHandles[0].NormalizedX = 0.22f;
                data.BananaCheckpoints[0].NormalizedX = 0.37f;
                data.BananaMaximumBonusCombo = 7;
                Undo.IncrementCurrentGroup();
                fixture.Invoke("ApplySelectedAppliedNoteData");
                Undo.FlushUndoRecordObjects();
                Assert.That(fixture.Chart.TryValidate(out string error), Is.True, error);
                ChartNote edited = fixture.FindNote("banana");
                Assert.That(edited.BananaCurveHandles[0].NormalizedX, Is.EqualTo(0.22f));
                Assert.That(edited.BananaCheckpoints[0].NormalizedX, Is.EqualTo(0.37f));
                Assert.That(edited.BananaMaximumBonusCombo, Is.EqualTo(7));
                Assert.That(fixture.BufferJson, Is.EqualTo(originalBuffer));

                PrototypeChart serializedCopy = ScriptableObject.CreateInstance<PrototypeChart>();

                try
                {

                    JsonUtility.FromJsonOverwrite(JsonUtility.ToJson(fixture.Chart), serializedCopy);
                    Assert.That(serializedCopy.TryValidate(out string copyError), Is.True, copyError);
                    Assert.That(JsonUtility.ToJson(serializedCopy), Is.EqualTo(JsonUtility.ToJson(fixture.Chart)));

                }
                finally
                {

                    Object.DestroyImmediate(serializedCopy);

                }

                Undo.PerformUndo();
                Assert.That(JsonUtility.ToJson(fixture.Chart), Is.EqualTo(originalChart));
                LogAssert.NoUnexpectedReceived();

            }

        }

        public static void CaptureWorkspace()
        {

            if (Application.isBatchMode)
            {

                Debug.LogError("Workspace pixel capture needs a rendered Unity Editor window; omit -batchmode.");
                EditorApplication.Exit(1);
                return;

            }

            string outputDirectory = Environment.GetEnvironmentVariable("IDIOT_TAPE_CAPTURE_DIR");

            if (string.IsNullOrWhiteSpace(outputDirectory) || !Path.IsPathRooted(outputDirectory))
            {

                Debug.LogError("IDIOT_TAPE_CAPTURE_DIR must name an absolute output directory.");
                EditorApplication.Exit(1);
                return;

            }

            Directory.CreateDirectory(outputDirectory);
            captureFailure = null;
            captureNextUpdate = EditorApplication.timeSinceStartup;
            captureSequence = CaptureFrames(outputDirectory);
            Application.logMessageReceived += CaptureLog;
            EditorApplication.update += AdvanceCapture;

        }

        private static IEnumerator CaptureFrames(string outputDirectory)
        {

            using (WorkspaceFixture fixture = new())
            {

                fixture.Show(new Vector2(1200f, 800f));
                fixture.SelectNote("slide");
                fixture.SetEnum("recordingNoteMode", "SlideAndHold");
                fixture.Render();
                yield return null;
                SaveWindowPixels(fixture.Window, Path.Combine(outputDirectory, "workspace-wide.png"));

                fixture.SetEnum("timelineViewMode", "Horizontal");
                fixture.Render();
                yield return null;
                SaveWindowPixels(fixture.Window, Path.Combine(outputDirectory, "workspace-parts.png"));

                fixture.SetEnum("timelineViewMode", "Vertical");
                fixture.SelectNote("banana");
                fixture.SetEnum("recordingNoteMode", "Banana");
                fixture.Render();
                yield return null;
                SaveWindowPixels(fixture.Window, Path.Combine(outputDirectory, "workspace-banana.png"));

                fixture.Window.position = new Rect(80f, 80f, 700f, 600f);
                fixture.Set("workspaceCompactInspector", false);
                fixture.Set("workspaceShowBufferDetails", true);
                fixture.Render();
                yield return null;
                SaveWindowPixels(fixture.Window, Path.Combine(outputDirectory, "workspace-compact.png"));

                fixture.Set("workspaceCompactInspector", true);
                fixture.Render();
                yield return null;
                SaveWindowPixels(fixture.Window, Path.Combine(outputDirectory, "workspace-compact-inspector.png"));

            }

        }

        private static void AdvanceCapture()
        {

            if (EditorApplication.timeSinceStartup < captureNextUpdate)
            {

                return;

            }

            try
            {

                if (captureFailure != null)
                {

                    throw new InvalidOperationException(captureFailure);

                }

                if (captureSequence.MoveNext())
                {

                    captureNextUpdate = EditorApplication.timeSinceStartup + 0.8d;
                    return;

                }

                FinishCapture(0);

            }
            catch (Exception exception)
            {

                Debug.LogException(exception);
                FinishCapture(1);

            }

        }

        private static void FinishCapture(int exitCode)
        {

            EditorApplication.update -= AdvanceCapture;
            Application.logMessageReceived -= CaptureLog;
            (captureSequence as IDisposable)?.Dispose();
            captureSequence = null;
            EditorApplication.Exit(exitCode);

        }

        private static void CaptureLog(string condition, string stackTrace, LogType type)
        {

            if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert)
            {

                captureFailure ??= condition + "\n" + stackTrace;

            }

        }

        private static void SaveWindowPixels(EditorWindow window, string path)
        {

            window.Focus();
            object parent = typeof(EditorWindow).GetField("m_Parent", PrivateInstance)?.GetValue(window);
            Type guiViewType = typeof(EditorWindow).Assembly.GetType("UnityEditor.GUIView", true);
            Type viewType = typeof(EditorWindow).Assembly.GetType("UnityEditor.View", true);
            BindingFlags viewMembers = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            MethodInfo scaleMethod = guiViewType.GetMethod("GetBackingScaleFactor", viewMembers);
            MethodInfo grabMethod = guiViewType.GetMethod("GrabPixels", viewMembers);
            MethodInfo repaintMethod = guiViewType.GetMethod("RepaintImmediately", viewMembers);
            PropertyInfo positionProperty = viewType.GetProperty("position", viewMembers);

            if (parent == null || scaleMethod == null || grabMethod == null || repaintMethod == null || positionProperty == null)
            {

                throw new InvalidOperationException("Unity's workspace render-surface capture API is unavailable.");

            }

            if (!window.hasFocus)
            {

                throw new InvalidOperationException("The workspace must be the active view in its Unity host.");

            }

            repaintMethod.Invoke(parent, null);
            Rect logicalBounds = (Rect)positionProperty.GetValue(parent);
            float backingScale = (float)scaleMethod.Invoke(parent, null);

            if (float.IsNaN(backingScale) || float.IsInfinity(backingScale) || backingScale <= 0f)
            {

                throw new InvalidOperationException("Unity reported an invalid workspace backing scale.");

            }

            int width = Mathf.RoundToInt(logicalBounds.width * backingScale);
            int height = Mathf.RoundToInt(logicalBounds.height * backingScale);

            if (width <= 0 || height <= 0)
            {

                throw new InvalidOperationException("Unity reported an empty workspace render surface.");

            }

            Material captureMaterial = (Material)EditorGUIUtility.LoadRequired("SceneView/BlitSceneViewCapture.mat");
            RenderTexture previousTarget = RenderTexture.active;
            RenderTexture capturedWindow = new(width, height, 0, RenderTextureFormat.ARGB32);
            RenderTexture sourceWindow = RenderTexture.GetTemporary(capturedWindow.descriptor);
            Texture2D texture = new(width, height, TextureFormat.RGB24, false);

            try
            {

                capturedWindow.Create();
                // GrabPixels consumes the backing surface rectangle, independently of the current GUI context's scale.
                grabMethod.Invoke(parent, new object[] { sourceWindow, new Rect(0f, 0f, width, height) });
                Graphics.Blit(sourceWindow, capturedWindow, captureMaterial);
                RenderTexture.active = capturedWindow;
                texture.ReadPixels(new Rect(0f, 0f, width, height), 0, 0, false);
                texture.Apply();
                File.WriteAllBytes(path, texture.EncodeToPNG());
                Debug.Log($"Workspace preview saved: {path} · view {logicalBounds.width}×{logicalBounds.height}, " +
                          $"backing scale {backingScale}, pixels {width}×{height}");

            }
            finally
            {

                RenderTexture.active = previousTarget;
                Object.DestroyImmediate(texture);
                RenderTexture.ReleaseTemporary(sourceWindow);
                capturedWindow.Release();
                Object.DestroyImmediate(capturedWindow);

            }

        }

        private static void AssertContained(Rect outer, Rect inner)
        {

            Assert.That(inner.xMin, Is.GreaterThanOrEqualTo(outer.xMin));
            Assert.That(inner.yMin, Is.GreaterThanOrEqualTo(outer.yMin));
            Assert.That(inner.xMax, Is.LessThanOrEqualTo(outer.xMax));
            Assert.That(inner.yMax, Is.LessThanOrEqualTo(outer.yMax));

        }

        internal sealed class WorkspaceFixture : IDisposable
        {

            private bool isShown;
            public PrototypeChart Chart { get; }
            public PrototypeChartRecorderWindow Window { get; }
            public string BufferJson
            {

                get
                {

                    IList records = Get<IList>("recordedNotes");
                    string json = string.Empty;

                    foreach (object record in records)
                    {

                        json += JsonUtility.ToJson(record) + "\n";

                    }

                    return json;

                }

            }

            public WorkspaceFixture()
            {

                Chart = CreateChart();
                Window = ScriptableObject.CreateInstance<PrototypeChartRecorderWindow>();
                Window.hideFlags = HideFlags.DontSave;
                Window.titleContent = new GUIContent("채보 제작 · UI 검증");
                Window.minSize = new Vector2(700f, 600f);
                Set("chart", Chart);
                Set("workspaceInitialized", true);
                Set("timelineAutoScroll", false);
                Set("timelineVisibleDuration", 8f);
                Set("timelineStartTime", 0d);
                Set("loopStart", 0d);
                Set("loopEnd", 8d);
                Set("loopStartBar", 1);
                Set("loopBarCount", 4);
                SetEnum("timelineViewMode", "Vertical");
                AddBufferedNote(ChartNoteAuthoringData.CreateTap("buffer-tap", 2.5d, 0, "synth"));
                ChartNoteAuthoringData bufferedHold = ChartNoteAuthoringData.CreateTap("buffer-hold", 4.3d, 3, "synth");
                bufferedHold.NoteType = ChartNoteType.Hold;
                bufferedHold.EndTime = 5.3d;
                AddBufferedNote(bufferedHold);
                ChartNoteAuthoringData bufferedFlick = ChartNoteAuthoringData.CreateTap("buffer-flick", 7.5d, 0, "synth");
                bufferedFlick.NoteType = ChartNoteType.Flick;
                bufferedFlick.EndLaneIndex = 2;
                AddBufferedNote(bufferedFlick);
                SelectNote("slide");

            }

            public void Show(Vector2 size)
            {

                Window.ShowUtility();
                isShown = true;
                Window.position = new Rect(new Vector2(80f, 80f), size);
                Window.Focus();
                Window.Repaint();

            }

            public void Render()
            {

                Window.SendEvent(new Event { type = EventType.Layout });
                Window.SendEvent(new Event { type = EventType.Repaint });
                Window.Repaint();

            }

            public void Click(Vector2 windowPosition)
            {

                Render();
                Window.SendEvent(new Event
                {

                    type = EventType.MouseDown,
                    button = 0,
                    clickCount = 1,
                    mousePosition = windowPosition

                });
                Window.SendEvent(new Event
                {

                    type = EventType.MouseUp,
                    button = 0,
                    clickCount = 1,
                    mousePosition = windowPosition

                });
                Render();

            }

            public void SelectNote(string id)
            {

                Set("selectedRecordedNoteIndex", -1);
                Set("selectedChartNoteId", id);
                Invoke("LoadSelectedAppliedNoteData", FindNote(id));
                SetEnum("workspaceInspectorTab", "Note");

            }

            public ChartNote FindNote(string id)
            {

                foreach (ChartNote note in Chart.Notes)
                {

                    if (note.Id == id)
                    {

                        return note;

                    }

                }

                throw new InvalidOperationException("Missing fixture note: " + id);

            }

            public T Get<T>(string name)
            {

                return (T)typeof(PrototypeChartRecorderWindow).GetField(name, PrivateInstance).GetValue(Window);

            }

            public void Set(string name, object value)
            {

                typeof(PrototypeChartRecorderWindow).GetField(name, PrivateInstance).SetValue(Window, value);

            }

            public void SetEnum(string name, string value)
            {

                FieldInfo field = typeof(PrototypeChartRecorderWindow).GetField(name, PrivateInstance);
                field.SetValue(Window, Enum.Parse(field.FieldType, value));

            }

            public void Invoke(string name, params object[] arguments)
            {

                typeof(PrototypeChartRecorderWindow).GetMethod(name, PrivateInstance).Invoke(Window, arguments);

            }

            public void Dispose()
            {

                Undo.ClearUndo(Chart);
                Undo.ClearUndo(Window);
                if (isShown)
                {

                    Window.Close();

                }

                if (Window != null)
                {

                    Object.DestroyImmediate(Window);

                }

                Object.DestroyImmediate(Chart);

            }

            public void AddBufferedNote(ChartNoteAuthoringData data)
            {

                Type recordType = typeof(PrototypeChartRecorderWindow).GetNestedType("RecordedNote", BindingFlags.NonPublic);
                object record = Activator.CreateInstance(recordType, true);
                recordType.GetField("hitTime").SetValue(record, data.HitTime);
                recordType.GetField("originalHitTime").SetValue(record, data.HitTime);
                recordType.GetField("hasOriginalHitTime").SetValue(record, true);
                recordType.GetField("laneIndex").SetValue(record, data.LaneIndex);
                recordType.GetField("musicalPartId").SetValue(record, data.MusicalPartId);
                recordType.GetField("noteData").SetValue(record, data);
                Get<IList>("recordedNotes").Add(record);

            }

            private static PrototypeChart CreateChart()
            {

                PrototypeChart chart = ScriptableObject.CreateInstance<PrototypeChart>();
                chart.name = "Snow · Workspace fixture";
                chart.hideFlags = HideFlags.DontSave;
                SerializedObject serialized = new(chart);
                serialized.FindProperty("songEventPath").stringValue = "event:/WorkspaceVerification";
                SerializedProperty tempos = serialized.FindProperty("tempoSections");
                tempos.arraySize = 1;
                SerializedProperty tempo = tempos.GetArrayElementAtIndex(0);
                tempo.FindPropertyRelative("startBar").intValue = 1;
                tempo.FindPropertyRelative("startTime").doubleValue = 0d;
                tempo.FindPropertyRelative("beatsPerMinute").doubleValue = 120d;
                tempo.FindPropertyRelative("beatsPerBar").intValue = 4;
                tempo.FindPropertyRelative("beatUnit").intValue = 4;
                string[] ids = { "synth", "bass", "drum", "etc" };
                string[] names = { "Synth", "Bass", "Drum", "FX" };
                Color[] colors = { new(0.43f, 0.82f, 0.88f), new(0.71f, 0.62f, 0.86f), new(0.86f, 0.82f, 0.69f), new(0.82f, 0.59f, 0.69f) };
                SerializedProperty parts = serialized.FindProperty("musicalParts");
                SerializedProperty windows = serialized.FindProperty("activationWindows");
                parts.arraySize = ids.Length;
                windows.arraySize = ids.Length;

                for (int index = 0; index < ids.Length; index++)
                {

                    SerializedProperty part = parts.GetArrayElementAtIndex(index);
                    part.FindPropertyRelative("id").stringValue = ids[index];
                    part.FindPropertyRelative("displayName").stringValue = names[index];
                    part.FindPropertyRelative("color").colorValue = colors[index];
                    SerializedProperty activeWindow = windows.GetArrayElementAtIndex(index);
                    activeWindow.FindPropertyRelative("musicalPartId").stringValue = ids[index];
                    activeWindow.FindPropertyRelative("startTime").doubleValue = 0d;
                    activeWindow.FindPropertyRelative("endTime").doubleValue = 8d;

                }

                List<ChartNoteAuthoringData> notes = new()
                {
                    ChartNoteAuthoringData.CreateTap("tap", 0.25d, 5, "synth"),
                    ChartNoteAuthoringData.CreateTap("hold", 0.5d, 1, "synth"),
                    ChartNoteAuthoringData.CreateTap("flick", 1.75d, 0, "synth"),
                    ChartNoteAuthoringData.CreateTap("slide", 2d, 2, "synth"),
                    ChartNoteAuthoringData.CreateTap("banana", 5.4d, 1, "synth")
                };
                notes[1].NoteType = ChartNoteType.Hold;
                notes[1].EndTime = 1.5d;
                notes[2].NoteType = ChartNoteType.Flick;
                notes[2].EndLaneIndex = 7;
                notes[3].NoteType = ChartNoteType.Slide;
                notes[3].EndTime = 5d;
                notes[3].EndLaneIndex = 5;
                notes[3].SlideNodes.Add(new ChartNoteAuthoringData.PathNodeData { Time = 3d, LaneIndex = 6 });
                notes[3].SlideNodes.Add(new ChartNoteAuthoringData.PathNodeData { Time = 4d, LaneIndex = 5 });
                notes[3].SlideNodes.Add(new ChartNoteAuthoringData.PathNodeData { Time = 5d, LaneIndex = 5 });
                notes[4].NoteType = ChartNoteType.Banana;
                notes[4].EndTime = 7.4d;
                notes[4].EndLaneIndex = 6;
                notes[4].BananaCurveHandles.Add(new ChartNoteAuthoringData.CurveHandleData { NormalizedTime = 0.5f, NormalizedX = 0.8f });
                notes[4].BananaCheckpoints.Add(new ChartNoteAuthoringData.CheckpointData { Time = 6d, NormalizedX = 0.55f });
                notes[4].BananaCheckpoints.Add(new ChartNoteAuthoringData.CheckpointData { Time = 6.5d, NormalizedX = 0.72f });
                notes[4].BananaCheckpoints.Add(new ChartNoteAuthoringData.CheckpointData { Time = 7d, NormalizedX = 0.77f });
                notes.Add(ChartNoteAuthoringData.CreateTap("bass-tap", 1d, 0, "bass"));
                notes.Add(ChartNoteAuthoringData.CreateTap("drum-tap", 2.5d, 4, "drum"));
                notes.Add(ChartNoteAuthoringData.CreateTap("fx-tap", 4.5d, 7, "etc"));
                notes.Sort((left, right) => left.HitTime.CompareTo(right.HitTime));
                SerializedProperty storedNotes = serialized.FindProperty("notes");
                storedNotes.arraySize = notes.Count;

                for (int index = 0; index < notes.Count; index++)
                {

                    notes[index].WriteTo(storedNotes.GetArrayElementAtIndex(index));

                }

                serialized.ApplyModifiedPropertiesWithoutUndo();
                Assert.That(chart.TryValidate(out string error), Is.True, error);
                return chart;

            }

        }

    }

}
