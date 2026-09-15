using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace IdiotTape.Gameplay.Tests
{

    public sealed class SongLibraryPointerTests
    {

        private const BindingFlags Private = BindingFlags.NonPublic | BindingFlags.Instance;

        [UnityTest]
        public IEnumerator RealPointerSelectionDragLaunchAndCameraHandoff()
        {

            string preferences = PlayerPrefs.GetString("IdiotTape.Library.v1", "{}");
            bool background = Application.runInBackground;
            InputSettings originalInputSettings = InputSystem.settings;
            InputSettings testInputSettings = Object.Instantiate(originalInputSettings);
            Mouse mouse = null;
            Keyboard keyboard = null;
            List<Object> fixtures = new();
            try
            {

                Application.runInBackground = true;
                // Batch mode has no focused Game view. Route synthetic device events through
                // the normal input/UI pipeline without changing the project's saved settings.
                testInputSettings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
#if UNITY_EDITOR
                testInputSettings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
#endif
                InputSystem.settings = testInputSettings;
                PlayerPrefs.SetString("IdiotTape.Library.v1", "{}");
                yield return SceneManager.LoadSceneAsync("SongLibrary");
                yield return null;
                SongLibraryView view = Object.FindAnyObjectByType<SongLibraryView>();
                SongLibraryFlow flow = Object.FindAnyObjectByType<SongLibraryFlow>();
                Camera menuCamera = Get<GameObject>(flow, "previewAudioRoot").GetComponent<Camera>();
                Assert.That(menuCamera, Is.Not.Null);
                Assert.That(menuCamera.isActiveAndEnabled, Is.True);
                Assert.That(menuCamera.targetDisplay, Is.Zero);
                Assert.That(menuCamera.targetTexture, Is.Null);
                Canvas canvas = view.GetComponentInChildren<Canvas>();
                Assert.That(canvas.renderMode, Is.EqualTo(RenderMode.ScreenSpaceOverlay));
                GameplayMenuCurve curve = view.GetComponentInChildren<GameplayMenuCurve>();
                Assert.That(curve.GetComponent<CanvasRenderer>(), Is.Not.Null);
                Assert.That(curve.raycastTarget, Is.False);
                mouse = InputSystem.AddDevice<Mouse>();
                keyboard = InputSystem.AddDevice<Keyboard>();
                yield return null;
                RectTransform safe = Get<RectTransform>(view, "safeRoot");
                yield return Click(mouse, (RectTransform)safe.Find("SpeedPlus"));
                Assert.That(Get<Text>(view, "speedText").text, Does.Contain("1.1"));
                ScrollRect scroll = Get<ScrollRect>(view, "scroll");
                RectTransform pluto = FindSongRow(scroll, "Pluto");
                yield return Click(mouse, pluto);
                Assert.That(view.SelectedSong.Title, Is.EqualTo("Pluto"));
                Assert.That(Get<Button>(view, "playButton").interactable, Is.False);
                yield return Click(mouse, FindSongRow(scroll, "Snow"));
                Assert.That(view.SelectedSong.Title, Is.EqualTo("Snow"));

                // Use a larger temporary catalog to exercise ScrollRect drag arbitration.
                SongCatalog original = Get<SongCatalog>(flow, "catalog");
                SongCatalog catalog = Object.Instantiate(original);
                fixtures.Add(catalog);
                List<CatalogChart> entries = new();
                for (int i = 0; i < 30; i++)
                {

                    SongDefinition song = Object.Instantiate(view.SelectedSong);
                    PrototypeChart chart = Object.Instantiate(view.SelectedChart.Chart);
                    fixtures.Add(song);
                    fixtures.Add(chart);
                    Set(song, "id", $"pointer-{i}");
                    Set(song, "title", $"Track {i:00}");
                    Set(chart, "song", song);
                    Set(chart, "chartId", $"pointer-chart-{i}");
                    entries.Add(new CatalogChart(chart, "Drum", 272));

                }

                Set(view, "previewEnabled", false);
                view.ResumePreview();
                Set(catalog, "charts", entries);
                Set(view, "catalog", catalog);
                Invoke(view, "RefreshSongs");
                yield return null;
                Canvas.ForceUpdateCanvases();
                string selectedBeforeDrag = view.SelectedSong.Id;
                Vector2 origin = ScreenPoint(scroll.viewport);
                InputSystem.QueueStateEvent(mouse, new MouseState { position = origin }.WithButton(MouseButton.Left, true));
                yield return null;
                for (int i = 1; i <= 8; i++)
                {

                    InputSystem.QueueStateEvent(mouse, new MouseState { position = origin + Vector2.up * (i * 25) }.WithButton(MouseButton.Left, true));
                    yield return null;

                }

                InputSystem.QueueStateEvent(mouse, new MouseState { position = origin + Vector2.up * 200 });
                yield return null;
                Assert.That(scroll.content.anchoredPosition.y, Is.GreaterThan(50));
                Assert.That(view.SelectedSong.Id, Is.EqualTo(selectedBeforeDrag), "Dragging a row must not select it.");
                scroll.StopMovement();
                InputField search = Get<InputField>(view, "search");
                yield return Click(mouse, (RectTransform)search.transform);
                Assert.That(search.isFocused, Is.True);
                search.text = "no match";
                Assert.That(view.SelectedSong, Is.Null);
                Set(view, "catalog", original);
                search.text = "";
                Invoke(view, "RefreshSongs");
                yield return null;
                yield return Click(mouse, FindSongRow(scroll, "Snow"));
                yield return CaptureActualScreen();
                yield return Click(mouse, (RectTransform)Get<Button>(view, "playButton").transform);
                double deadline = Time.realtimeSinceStartupAsDouble + 20;
                while ((flow.IsBusy || flow.Session == null) && Time.realtimeSinceStartupAsDouble < deadline) yield return null;
                Assert.That(flow.Session, Is.Not.Null);
                Assert.That(flow.IsBusy, Is.False);
                Assert.That(menuCamera.isActiveAndEnabled, Is.False);
                Assert.That(Camera.allCamerasCount, Is.GreaterThan(0));
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Escape));
                yield return null;
                InputSystem.QueueStateEvent(keyboard, new KeyboardState());
                deadline = Time.realtimeSinceStartupAsDouble + 15;
                while ((flow.IsBusy || flow.Session != null) && Time.realtimeSinceStartupAsDouble < deadline) yield return null;
                Assert.That(flow.Session, Is.Null);
                Assert.That(menuCamera.isActiveAndEnabled, Is.True);
                Assert.That(canvas.renderMode, Is.EqualTo(RenderMode.ScreenSpaceOverlay));
                LogAssert.NoUnexpectedReceived();

            }
            finally
            {

                if (mouse != null) InputSystem.RemoveDevice(mouse);
                if (keyboard != null) InputSystem.RemoveDevice(keyboard);
                InputSystem.settings = originalInputSettings;
                Object.Destroy(testInputSettings);
                foreach (Object fixture in fixtures) Object.Destroy(fixture);
                PlayerPrefs.SetString("IdiotTape.Library.v1", preferences);
                PlayerPrefs.Save();
                Application.runInBackground = background;

            }

        }

        private static IEnumerator Click(Mouse mouse, RectTransform target)
        {

            Canvas.ForceUpdateCanvases();
            Vector2 position = ScreenPoint(target);
            List<RaycastResult> hits = new();
            EventSystem.current.RaycastAll(new PointerEventData(EventSystem.current) { position = position }, hits);
            Assert.That(hits.Count, Is.GreaterThan(0), target.name);
            Assert.That(hits[0].gameObject.transform.IsChildOf(target), Is.True, $"{target.name} is covered by {hits[0].gameObject.name}");
            InputSystem.QueueStateEvent(mouse, new MouseState { position = position });
            yield return null;
            InputSystem.QueueStateEvent(mouse, new MouseState { position = position }.WithButton(MouseButton.Left, true));
            yield return null;
            InputSystem.QueueStateEvent(mouse, new MouseState { position = position });
            yield return null;
            yield return null;

        }

        private static RectTransform FindSongRow(ScrollRect scroll, string title)
        {

            foreach (Button button in scroll.content.GetComponentsInChildren<Button>())
            {

                if (button.transform.Find("Title").GetComponent<Text>().text == title) return (RectTransform)button.transform;

            }

            throw new AssertionException($"Visible row missing: {title}");

        }

        private static Vector2 ScreenPoint(RectTransform rect)
        {

            Canvas canvas = rect.GetComponentInParent<Canvas>();
            return RectTransformUtility.WorldToScreenPoint(canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera,
                rect.TransformPoint(rect.rect.center));

        }

        private static IEnumerator CaptureActualScreen()
        {

            string directory = Environment.GetEnvironmentVariable("IDIOT_TAPE_REPAIR_EVIDENCE");
            if (string.IsNullOrEmpty(directory)) yield break;
#if UNITY_EDITOR
            // Real screen capture needs the Game view's render loop, unlike batch layout tests.
            UnityEditor.EditorWindow gameView = UnityEditor.EditorWindow.GetWindow(Type.GetType("UnityEditor.GameView,UnityEditor"));
            gameView.Focus();
            gameView.Repaint();
            yield return null;
#endif
            string path = Path.Combine(directory, "actual-library-screen.png");
            ScreenCapture.CaptureScreenshot(path);
            double deadline = Time.realtimeSinceStartupAsDouble + 5d;
            while (!File.Exists(path) && Time.realtimeSinceStartupAsDouble < deadline) yield return null;
            Assert.That(File.Exists(path), Is.True, "Capture the existing display without injecting a camera or changing Canvas mode.");

        }

        private static T Get<T>(object target, string name) => (T)target.GetType().GetField(name, Private).GetValue(target);
        private static void Set(object target, string name, object value) => target.GetType().GetField(name, Private).SetValue(target, value);
        private static void Invoke(object target, string name) => target.GetType().GetMethod(name, Private).Invoke(target, null);

    }

}
