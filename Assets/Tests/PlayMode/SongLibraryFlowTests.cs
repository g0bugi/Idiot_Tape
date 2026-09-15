using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using IdiotTape.Audio;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace IdiotTape.Gameplay.Tests
{

    public sealed class SongLibraryFlowTests
    {

        private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        private const string PreferenceKey = "IdiotTape.Library.v1";

        [UnityTest]
        public IEnumerator LibraryPreviewCancelPlayRetryAndRestoreSelection()
        {

            string saved = PlayerPrefs.GetString(PreferenceKey, "{}");
            bool oldBackground = Application.runInBackground;
            int oldFrameRate = Application.targetFrameRate;
            PrototypeChart fixture = null;
            try
            {

                PlayerPrefs.SetString(PreferenceKey, "{}");
                Application.runInBackground = true;
                Application.targetFrameRate = 60;
                yield return SceneManager.LoadSceneAsync("SongLibrary");
                yield return null;
                SongLibraryFlow flow = Object.FindAnyObjectByType<SongLibraryFlow>();
                SongLibraryView view = Object.FindAnyObjectByType<SongLibraryView>();
                SongPreviewPlayer preview = Object.FindAnyObjectByType<SongPreviewPlayer>();
                SongCatalog catalog = Get<SongCatalog>(flow, "catalog");
                SongDefinition snow = null;
                SongDefinition pluto = null;
                foreach (CatalogChart entry in catalog.Charts)
                {

                    if (entry.Chart.SongTitle == "Snow")
                    {

                        snow = entry.Chart.Song;

                    }
                    if (entry.Chart.SongTitle == "Pluto")
                    {

                        pluto = entry.Chart.Song;

                    }

                }

                Assert.That(snow, Is.Not.Null);
                Assert.That(pluto, Is.Not.Null);
                view.SelectSong(snow);
                view.SelectSong(pluto);
                view.SelectSong(snow);
                yield return WaitFor(() => Get<FmodSongPlayback>(preview, "playback")?.IsRunning == true, 20, "preview");
                Assert.That(preview.SelectedSong, Is.SameAs(snow));
                FmodSongPlayback previewPlayback = Get<FmodSongPlayback>(preview, "playback");
                Assert.That(previewPlayback.DurationSeconds, Is.GreaterThan(480));
                Assert.That(previewPlayback.PlaybackPositionSeconds, Is.LessThan(3d));
                Capture(view, "01-library-snow");
                InputField search = Get<InputField>(view, "search");
                search.text = "no-match";
                Assert.That(view.SelectedSong, Is.Null);
                Assert.That(Get<Button>(view, "playButton").interactable, Is.False);
                search.text = "KIRARA";
                Assert.That(view.SelectedSong, Is.SameAs(snow));
                PrototypeChart chart = view.SelectedChart.Chart;
                flow.StartPlay(new PlayRequest(chart, 2.3f, true));
                flow.CancelPreparation();
                yield return WaitFor(() => !flow.IsBusy, 25, "cancel loading");
                Assert.That(flow.Session, Is.Null);
                Assert.That(SceneManager.GetSceneByName("Gameplay").isLoaded, Is.False);

                flow.StartPlay(new PlayRequest(chart, 2.3f, true));
                flow.StartPlay(new PlayRequest(chart, 4f, false));
                yield return WaitFor(() => !flow.IsBusy && flow.Session != null, 25, "start selected song");
                Assert.That(Object.FindObjectsByType<GameplaySession>(FindObjectsSortMode.None).Length, Is.EqualTo(1));
                GameplayHud hud = Object.FindAnyObjectByType<GameplayHud>();
                Assert.That(hud.NoteSpeedMultiplier, Is.EqualTo(2.3f).Within(.001f));
                Assert.That(hud.ShortPreparation, Is.True);
                Assert.That(preview.SelectedSong, Is.Null);
                Assert.That(previewPlayback.IsPrepared, Is.False);
                Assert.That(Get<GameObject>(flow, "previewAudioRoot").activeSelf, Is.False);
                yield return WaitFor(() => !flow.Session.IsCountingIn, 10, "count-in");
                GameplaySession session = flow.Session;
                Invoke(session, "ClearSessionState");
                Set(session, "nextNoteIndex", chart.Notes.Count);
                Invoke(session, "TryCompleteSession", double.MaxValue);
                Assert.That(session.IsShowingResults, Is.True);
                session.RequestStart();
                Assert.That(session.IsCountingIn, Is.True);
                Assert.That(hud.NoteSpeedMultiplier, Is.EqualTo(2.3f).Within(.001f));
                Object.FindAnyObjectByType<GameplayInputRouter>().RequestPause();
                yield return WaitFor(() => !flow.IsBusy && flow.Session == null, 15, "return to library");
                Assert.That(search.text, Is.EqualTo("KIRARA"));
                Assert.That(view.SelectedSong, Is.SameAs(snow));
                Assert.That(Get<GameObject>(flow, "previewAudioRoot").activeSelf, Is.True);

                // A temporary second playable chart exercises event/part replacement without
                // publishing invented notes into Pluto's empty authored chart.
                fixture = Object.Instantiate(chart);
                Set(fixture, "song", pluto);
                flow.StartPlay(new PlayRequest(fixture, 1.5f, false));
                yield return WaitFor(() => !flow.IsBusy && flow.Session != null, 25, "second song");
                Assert.That(Get<PrototypeChart>(flow.Session, "chart"), Is.SameAs(fixture));
                Assert.That(Object.FindObjectsByType<GameplaySession>(FindObjectsSortMode.None).Length, Is.EqualTo(1));
                Object.FindAnyObjectByType<GameplayInputRouter>().RequestPause();
                yield return WaitFor(() => !flow.IsBusy && flow.Session == null, 15, "second return");
                search.text = "";
                view.SelectSong(pluto);
                Assert.That(Get<Button>(view, "playButton").interactable, Is.False);
                Capture(view, "02-library-unplayable");
                Set(fixture, "laneCount", 0);
                flow.StartPlay(new PlayRequest(fixture, 1f, true));
                yield return WaitFor(() => !flow.IsBusy, 20, "invalid chart recovery");
                Assert.That(flow.Session, Is.Null);
                Assert.That(Get<Text>(view, "statusText").text, Does.Contain("Lane count"));
                preview.StopPreview();

            }
            finally
            {

                PlayerPrefs.SetString(PreferenceKey, saved);
                PlayerPrefs.Save();
                Application.runInBackground = oldBackground;
                Application.targetFrameRate = oldFrameRate;
                if (fixture != null)
                {

                    Object.Destroy(fixture);

                }

            }

        }

        [UnityTest]
        public IEnumerator LibraryPoolsRowsForThreeThirtyAndThreeHundredSongs()
        {

            string saved = PlayerPrefs.GetString(PreferenceKey, "{}");
            List<Object> fixtures = new();
            try
            {

                yield return SceneManager.LoadSceneAsync("SongLibrary");
                yield return null;
                SongLibraryView view = Object.FindAnyObjectByType<SongLibraryView>();
                SongLibraryFlow flow = Object.FindAnyObjectByType<SongLibraryFlow>();
                SongPreviewPlayer preview = Object.FindAnyObjectByType<SongPreviewPlayer>();
                Set(view, "previewEnabled", false);
                preview.StopPreview();
                SongCatalog original = Get<SongCatalog>(flow, "catalog");
                SongCatalog catalog = Object.Instantiate(original);
                fixtures.Add(catalog);
                Set(view, "catalog", catalog);
                List<CatalogChart> entries = new();
                for (int i = 0; i < 300; i++)
                {

                    SongDefinition song = Object.Instantiate(original.Charts[0].Chart.Song);
                    PrototypeChart chart = Object.Instantiate(original.Charts[0].Chart);
                    fixtures.Add(song);
                    fixtures.Add(chart);
                    Set(song, "id", $"fixture-song-{i}");
                    Set(song, "title", $"Track {i:000}");
                    Set(chart, "song", song);
                    Set(chart, "chartId", $"fixture-chart-{i}");
                    entries.Add(new CatalogChart(chart, "Drum → Synth → Bass", 272));

                }

                int previousPool = 0;
                foreach (int count in new[] { 3, 30, 300 })
                {

                    Set(catalog, "charts", entries.GetRange(0, count));
                    Invoke(view, "RefreshSongs");
                    Canvas.ForceUpdateCanvases();
                    yield return null;
                    ScrollRect scroll = Get<ScrollRect>(view, "scroll");
                    scroll.verticalNormalizedPosition = 0;
                    yield return null;
                    Assert.That(view.VisibleRowCount, Is.LessThan(20));
                    if (previousPool != 0)
                    {

                        Assert.That(view.VisibleRowCount, Is.EqualTo(previousPool));

                    }
                    previousPool = view.VisibleRowCount;
                    Text[] texts = scroll.content.GetComponentsInChildren<Text>();
                    Assert.That(Array.Exists(texts, text => text.text == $"Track {count - 1:000}"), Is.True);
                    Debug.Log($"Library fixture {count}: pool={view.VisibleRowCount}, managedBytes={UnityEngine.Profiling.Profiler.GetMonoUsedSizeLong()}");

                }

                InputField search = Get<InputField>(view, "search");
                search.text = "Track 299";
                Assert.That(view.SelectedSong.Title, Is.EqualTo("Track 299"));
                search.text = "no matching track";
                Assert.That(view.SelectedSong, Is.Null);
                Assert.That(Get<Button>(view, "playButton").interactable, Is.False);
                // Drop references before destroying temporary content.
                Set(view, "catalog", original);
                search.text = "";
                Invoke(view, "RefreshSongs");
                preview.StopPreview();

            }
            finally
            {

                foreach (Object fixture in fixtures)
                {

                    if (fixture != null)
                    {

                        Object.Destroy(fixture);

                    }

                }
                PlayerPrefs.SetString(PreferenceKey, saved);
                PlayerPrefs.Save();

            }

        }

        [UnityTest]
        public IEnumerator PreviewUsesConfiguredRangeAndLoopsWithoutLiveSeek()
        {

            string saved = PlayerPrefs.GetString(PreferenceKey, "{}");
            SongDefinition fixture = null;
            try
            {

                yield return SceneManager.LoadSceneAsync("SongLibrary");
                yield return null;
                SongLibraryView view = Object.FindAnyObjectByType<SongLibraryView>();
                SongPreviewPlayer preview = Object.FindAnyObjectByType<SongPreviewPlayer>();
                fixture = Object.Instantiate(view.SelectedSong);
                Set(fixture, "previewStart", 30f);
                Set(fixture, "previewDuration", 1f);
                preview.Select(fixture);
                yield return WaitFor(() => Get<FmodSongPlayback>(preview, "playback")?.IsRunning == true, 20, "range preparation");
                FmodSongPlayback playback = Get<FmodSongPlayback>(preview, "playback");
                yield return WaitFor(() => playback.PlaybackPositionSeconds > 30.65d, 5, "configured range");
                yield return WaitFor(() => playback.PlaybackPositionSeconds < 30.4d && playback.HasReachedScheduledStart, 5, "preview loop");
                Assert.That(playback.PlaybackPositionSeconds, Is.GreaterThanOrEqualTo(30d));
                preview.StopPreview();
                Assert.That(playback.IsPrepared, Is.False);

            }
            finally
            {

                if (fixture != null) Object.Destroy(fixture);
                PlayerPrefs.SetString(PreferenceKey, saved);
                PlayerPrefs.Save();

            }

        }

        private static IEnumerator WaitFor(Func<bool> condition, float seconds, string stage)
        {

            double deadline = Time.realtimeSinceStartupAsDouble + seconds;
            while (!condition() && Time.realtimeSinceStartupAsDouble < deadline)
            {

                yield return null;

            }
            Assert.That(condition(), Is.True, $"Timed out: {stage}");

        }

        private static T Get<T>(object target, string field) => (T)target.GetType().GetField(field, Private).GetValue(target);
        private static void Set(object target, string field, object value) => target.GetType().GetField(field, Private).SetValue(target, value);
        private static void Invoke(object target, string method, params object[] args) => target.GetType().GetMethod(method, Private).Invoke(target, args);

        private static void Capture(SongLibraryView view, string name)
        {

            string directory = Environment.GetEnvironmentVariable("IDIOT_TAPE_LIBRARY_EVIDENCE");
            if (string.IsNullOrEmpty(directory))
            {

                return;

            }
            Canvas canvas = view.GetComponentInChildren<Canvas>();
            GameObject cameraObject = new("Capture camera");
            Camera camera = cameraObject.AddComponent<Camera>();
            camera.orthographic = true;
            camera.transform.position = new Vector3(0, 0, -10);
            camera.clearFlags = CameraClearFlags.SolidColor;
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = camera;
            canvas.planeDistance = 1;
            RenderTexture oldActive = RenderTexture.active;
            int[,] sizes = { {1280, 720}, {1920, 1080}, {2340, 1080}, {1024, 768} };
            try
            {

                for (int i = 0; i < sizes.GetLength(0); i++)
                {

                    int width = sizes[i, 0];
                    int height = sizes[i, 1];
                    RenderTexture target = new(width, height, 24);
                    Texture2D pixels = new(width, height, TextureFormat.RGBA32, false);
                    camera.targetTexture = target;
                    RenderTexture.active = target;
                    Canvas.ForceUpdateCanvases();
                    Invoke(view, "BindRows");
                    Canvas.ForceUpdateCanvases();
                    camera.Render();
                    pixels.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                    pixels.Apply();
                    File.WriteAllBytes(Path.Combine(directory, $"{name}-{width}x{height}.png"), pixels.EncodeToPNG());
                    camera.targetTexture = null;
                    RenderTexture.active = oldActive;
                    Object.Destroy(target);
                    Object.Destroy(pixels);

                }

            }
            finally
            {

                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.worldCamera = null;
                RenderTexture.active = oldActive;
                Object.Destroy(cameraObject);

            }

        }

    }

}
