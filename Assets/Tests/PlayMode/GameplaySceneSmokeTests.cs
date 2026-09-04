using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using IdiotTape.Audio;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace IdiotTape.Gameplay.Tests
{

    public sealed class GameplaySceneSmokeTests
    {

        private int previousTargetFrameRate;
        private int previousVSyncCount;

        [SetUp]
        public void LimitBatchFrameRate()
        {

            previousTargetFrameRate = Application.targetFrameRate;
            previousVSyncCount = QualitySettings.vSyncCount;
            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = 60;

        }

        [TearDown]
        public void RestoreFrameRate()
        {

            Application.targetFrameRate = previousTargetFrameRate;
            QualitySettings.vSyncCount = previousVSyncCount;

        }

        [UnityTest]
        public IEnumerator SnowEventPreparesAndReportsFullSongDuration()
        {

            GameObject playbackObject = new("Snow Playback Test");

            try
            {

                FmodSongPlayback snowPlayback = playbackObject.AddComponent<FmodSongPlayback>();
                snowPlayback.ConfigureEventPath("event:/Music/KIRARA/Snow", false);
                snowPlayback.Prepare();
                float preparationDeadline = Time.realtimeSinceStartup + 15f;

                while (!snowPlayback.IsPrepared &&
                       !snowPlayback.PreparationFailed &&
                       Time.realtimeSinceStartup < preparationDeadline)
                {

                    yield return null;

                }

                Assert.That(snowPlayback.PreparationFailed, Is.False);
                Assert.That(snowPlayback.IsPrepared, Is.True, "Snow FMOD event did not become ready.");
                Assert.That(snowPlayback.DurationSeconds, Is.GreaterThan(480d));

            }
            finally
            {

                Object.Destroy(playbackObject);

            }

            yield return null;

        }

        [UnityTest]
        public IEnumerator GameplaySceneStartsAndSpawnsRuntimeNotes()
        {

            GameplaySession session = null;
            FmodSongPlayback songPlayback = null;
            PrototypeChart originalChart = null;
            PrototypeChart chart = null;

            try
            {

                AsyncOperation loadOperation = SceneManager.LoadSceneAsync("Gameplay", LoadSceneMode.Single);

                while (!loadOperation.isDone)
                {

                    yield return null;

                }

                yield return null;

                session = Object.FindAnyObjectByType<GameplaySession>();
                Assert.That(session, Is.Not.Null);
                songPlayback = Object.FindAnyObjectByType<FmodSongPlayback>();
                Assert.That(songPlayback, Is.Not.Null);

                float preparationDeadline = Time.realtimeSinceStartup + 15f;

                while (!session.IsWaitingForStart && Time.realtimeSinceStartup < preparationDeadline)
                {

                    yield return null;

                }

                Assert.That(session.IsWaitingForStart, Is.True, "Gameplay did not reach its start prompt.");
                Assert.That(songPlayback.IsRunning, Is.False);
                originalChart = GetChart(session);
                chart = CreatePlayableFixture(originalChart);
                SetChart(session, chart);
                Assert.That(chart.TryValidate(out string chartError), Is.True, chartError);
                session.RequestStart();
                Assert.That(session.IsCountingIn, Is.True);
                float countInDeadline = Time.realtimeSinceStartup + 15f;

                while ((session.IsCountingIn || !songPlayback.HasReachedScheduledStart || songPlayback.TimelineTime < 0d) &&
                       Time.realtimeSinceStartup < countInDeadline)
                {

                    yield return null;

                }

                Assert.That(session.IsCountingIn, Is.False, "The count-in did not complete.");
                Assert.That(songPlayback.HasReachedScheduledStart, Is.True);
                Assert.That(songPlayback.TimelineTime, Is.GreaterThanOrEqualTo(0d));
                Assert.That(songPlayback.IsRunning, Is.True, "Scheduled FMOD song playback did not start.");
                yield return null;
                Assert.That(Object.FindObjectsByType<RuntimeNoteView>(FindObjectsSortMode.None).Length, Is.GreaterThan(0));
                RuntimeTimingGuideView[] timingGuides = Object.FindObjectsByType<RuntimeTimingGuideView>(
                    FindObjectsSortMode.None);
                Assert.That(timingGuides.Length, Is.GreaterThan(0));
                Assert.That(System.Array.Exists(timingGuides, guide => guide.IsBar), Is.True);
                Assert.That(System.Array.Exists(timingGuides, guide => !guide.IsBar), Is.True);

                float playbackDeadline = Time.realtimeSinceStartup + 5f;

                while (songPlayback.SongTime < 1.47d && Time.realtimeSinceStartup < playbackDeadline)
                {

                    yield return null;

                }

                Assert.That(songPlayback.SongTime, Is.GreaterThanOrEqualTo(1.47d));
                Assert.That(GameObject.Find("BarGuide_001"), Is.Null);

                Assert.That(chart.Notes.Count, Is.GreaterThan(0));
                Assert.That(
                    chart.IsNotePlayable(chart.Notes[0]),
                    Is.True,
                    "The fixture enables every part so the opening note exercises gameplay independently of authored part windows.");
                ChartNote targetNote = FindPlayableNoteAfter(chart, songPlayback.SongTime + 0.1d);
                Assert.That(
                    targetNote,
                    Is.Not.Null,
                    "The gameplay chart does not contain a future playable note for the smoke input.");
                double inputTargetTime = targetNote.HitTime - 0.02d;
                float inputDeadline = Time.realtimeSinceStartup + 5f;

                while (songPlayback.SongTime < inputTargetTime && Time.realtimeSinceStartup < inputDeadline)
                {

                    yield return null;

                }

                Assert.That(
                    songPlayback.SongTime,
                    Is.GreaterThanOrEqualTo(inputTargetTime),
                    $"Playback did not reach playable note '{targetNote.Id}'.");
                GameplayInputRouter inputRouter = Object.FindAnyObjectByType<GameplayInputRouter>();
                Assert.That(inputRouter, Is.Not.Null);
                inputRouter.SubmitLaneInput(targetNote.LaneIndex, InputState.currentTime);
                yield return null;
                Text comboText = GameObject.Find("Combo").GetComponent<Text>();
                Text judgementText = GameObject.Find("Judgement").GetComponent<Text>();
                Text instrumentText = GameObject.Find("Instrument").GetComponent<Text>();
                Assert.That(comboText.text, Does.Contain("1"));
                Assert.That(comboText.text, Does.Contain("COMBO"));
                Assert.That(comboText.fontSize, Is.GreaterThanOrEqualTo(80));
                Assert.That(comboText.rectTransform.anchorMin.y, Is.GreaterThanOrEqualTo(0.5f));
                Assert.That(comboText.GetComponent<Outline>(), Is.Not.Null);
                Assert.That(judgementText.text, Is.EqualTo("PERFECT").Or.EqualTo("GOOD"));
                Assert.That(judgementText.fontSize, Is.GreaterThanOrEqualTo(64));
                Assert.That(judgementText.rectTransform.anchorMin.y, Is.GreaterThanOrEqualTo(0.35f));
                Assert.That(judgementText.GetComponent<Outline>(), Is.Not.Null);
                Assert.That(
                    instrumentText.text,
                    Is.EqualTo(chart.GetPartDisplayName(targetNote.MusicalPartId)));
                Assert.That(HasObjectWithNamePrefix<HitEffectView>("HitEffect_"), Is.True);
                Assert.That(HasObjectWithNamePrefix<JudgementLineReaction>("LineReaction_"), Is.True);

                yield return new WaitForSecondsRealtime(0.12f);
                CaptureGameplayCamera(Path.GetFullPath("Logs/GameplayPreview.png"));

                GameplayHud hud = Object.FindAnyObjectByType<GameplayHud>();
                RectTransform pauseButton = GameObject.Find("PauseButton").GetComponent<RectTransform>();
                Vector2 pauseButtonScreenPosition = RectTransformUtility.WorldToScreenPoint(Camera.main, pauseButton.position);
                Assert.That(hud.IsPauseButtonPress(pauseButtonScreenPosition), Is.True);

                inputRouter.RequestPause();
                yield return null;
                Assert.That(songPlayback.IsPaused, Is.True);

                double pausedSongTime = songPlayback.SongTime;
                double pausedPlaybackPosition = songPlayback.PlaybackPositionSeconds;
                RuntimeNoteView pausedNote = FindHighestRuntimeNote();
                RuntimeTimingGuideView pausedBarGuide = FindHighestTimingGuide(true);
                RuntimeTimingGuideView pausedBeatGuide = FindHighestTimingGuide(false);
                Assert.That(pausedNote, Is.Not.Null);
                Assert.That(pausedBarGuide, Is.Not.Null);
                Assert.That(pausedBeatGuide, Is.Not.Null);
                Vector3 pausedNotePosition = pausedNote.transform.position;
                Vector3 pausedBarPosition = GetTimingGuideMidpoint(pausedBarGuide);
                Vector3 pausedBeatPosition = GetTimingGuideMidpoint(pausedBeatGuide);
                int pausedGuideCount = Object.FindObjectsByType<RuntimeTimingGuideView>(
                    FindObjectsSortMode.None).Length;

                yield return new WaitForSecondsRealtime(0.25f);

                Assert.That(songPlayback.IsPaused, Is.True);
                Assert.That(songPlayback.SongTime, Is.EqualTo(pausedSongTime).Within(0.005d));
                double settledPausedPlaybackPosition = songPlayback.PlaybackPositionSeconds;
                Assert.That(pausedNote.transform.position, Is.EqualTo(pausedNotePosition));
                Assert.That(GetTimingGuideMidpoint(pausedBarGuide), Is.EqualTo(pausedBarPosition));
                Assert.That(GetTimingGuideMidpoint(pausedBeatGuide), Is.EqualTo(pausedBeatPosition));
                CaptureGameplayCamera(Path.GetFullPath("Logs/GameplayPaused.png"));
                Assert.That(
                    Object.FindObjectsByType<RuntimeTimingGuideView>(FindObjectsSortMode.None).Length,
                    Is.EqualTo(pausedGuideCount));

                yield return new WaitForSecondsRealtime(0.2f);

                Assert.That(songPlayback.SongTime, Is.EqualTo(pausedSongTime).Within(0.005d));
                Assert.That(
                    songPlayback.PlaybackPositionSeconds,
                    Is.EqualTo(settledPausedPlaybackPosition).Within(0.01d));
                Assert.That(pausedNote.transform.position, Is.EqualTo(pausedNotePosition));
                Assert.That(GetTimingGuideMidpoint(pausedBarGuide), Is.EqualTo(pausedBarPosition));
                Assert.That(GetTimingGuideMidpoint(pausedBeatGuide), Is.EqualTo(pausedBeatPosition));

                inputRouter.RequestPause();
                yield return null;
                Assert.That(songPlayback.IsPaused, Is.False);

                float resumeDeadline = Time.realtimeSinceStartup + 2f;

                while (songPlayback.SongTime < pausedSongTime + 0.12d &&
                       Time.realtimeSinceStartup < resumeDeadline)
                {

                    yield return null;

                }

                double resumedSongTime = songPlayback.SongTime;
                double resumedPlaybackPosition = songPlayback.PlaybackPositionSeconds;
                Assert.That(resumedSongTime, Is.GreaterThanOrEqualTo(pausedSongTime + 0.12d));
                Assert.That(resumedPlaybackPosition, Is.GreaterThan(pausedPlaybackPosition));
                Assert.That(
                    resumedPlaybackPosition,
                    Is.EqualTo(resumedSongTime).Within(0.08d));
                Assert.That(
                    Vector3.Distance(pausedNote.transform.position, pausedNotePosition),
                    Is.GreaterThan(0.001f));
                Assert.That(
                    Vector3.Distance(GetTimingGuideMidpoint(pausedBarGuide), pausedBarPosition),
                    Is.GreaterThan(0.001f));
                Assert.That(
                    Vector3.Distance(GetTimingGuideMidpoint(pausedBeatGuide), pausedBeatPosition),
                    Is.GreaterThan(0.001f));
                Assert.That(pausedBarGuide.IsBar, Is.True);
                Assert.That(pausedBeatGuide.IsBar, Is.False);
                CaptureGameplayCamera(Path.GetFullPath("Logs/GameplayResumed.png"));
                TestContext.WriteLine(
                    $"Pause transition timeline movement: " +
                    $"{settledPausedPlaybackPosition - pausedPlaybackPosition:0.000}s");
                TestContext.WriteLine(
                    $"Resume timeline difference: " +
                    $"{resumedPlaybackPosition - resumedSongTime:0.000}s");

                double songTimeBeforeRestart = songPlayback.SongTime;
                Assert.That(songTimeBeforeRestart, Is.GreaterThan(1d));

                MethodInfo beginSessionMethod = typeof(GameplaySession).GetMethod(
                    "BeginSession",
                    BindingFlags.Instance | BindingFlags.NonPublic);
                Assert.That(beginSessionMethod, Is.Not.Null, "GameplaySession restart method was not found.");
                beginSessionMethod.Invoke(session, null);
                yield return null;

                Assert.That(songPlayback.IsRunning, Is.True);
                Assert.That(songPlayback.IsPaused, Is.False);
                Assert.That(songPlayback.SongTime, Is.LessThan(songTimeBeforeRestart));
                Assert.That(GameObject.Find("Score").GetComponent<Text>().text, Is.EqualTo("00000000"));
                Assert.That(comboText.text, Is.Empty);
                Assert.That(judgementText.text, Is.Empty);
                Assert.That(session.IsCountingIn, Is.True);
                float restartSpawnDeadline = Time.realtimeSinceStartup + 15f;

                while (Object.FindObjectsByType<RuntimeNoteView>(FindObjectsSortMode.None).Length == 0 &&
                       Time.realtimeSinceStartup < restartSpawnDeadline)
                {

                    yield return null;

                }

                Assert.That(
                    Object.FindObjectsByType<RuntimeNoteView>(FindObjectsSortMode.None).Length,
                    Is.GreaterThan(0));

            }
            finally
            {

                if (session != null)
                {

                    session.enabled = false;

                    if (originalChart != null)
                    {

                        SetChart(session, originalChart);

                    }

                }

                if (songPlayback != null)
                {

                    songPlayback.Stop();

                }

                if (chart != null)
                {

                    Object.Destroy(chart);

                }

            }

            yield return null;

        }

        private static PrototypeChart CreatePlayableFixture(PrototypeChart source)
        {

            // Authored part windows can mute the opening while a chart is being edited.
            // Normalize only this in-memory fixture so the smoke test still covers early input and visual feedback.
            PrototypeChart fixture = Object.Instantiate(source);
            fixture.name = source.name + " Smoke Fixture";
            List<MusicalPartActivationWindow> windows = new();
            double endTime = fixture.Duration + 1d;

            for (int index = 0; index < fixture.MusicalParts.Count; index++)
            {

                MusicalPartActivationWindow window = new();
                SetPrivateField(window, "musicalPartId", fixture.MusicalParts[index].Id);
                SetPrivateField(window, "startTime", 0d);
                SetPrivateField(window, "endTime", endTime);
                windows.Add(window);

            }

            SetPrivateField(fixture, "activationWindows", windows);
            return fixture;

        }

        private static void SetChart(GameplaySession session, PrototypeChart chart)
        {

            SetPrivateField(session, "chart", chart);

        }

        private static void SetPrivateField(object target, string fieldName, object value)
        {

            FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, fieldName);
            field.SetValue(target, value);

        }

        private static PrototypeChart GetChart(GameplaySession session)
        {

            FieldInfo chartField = typeof(GameplaySession).GetField(
                "chart",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(chartField, Is.Not.Null, "GameplaySession chart field was not found.");
            PrototypeChart chart = chartField.GetValue(session) as PrototypeChart;
            Assert.That(chart, Is.Not.Null, "GameplaySession does not reference a prototype chart.");
            return chart;

        }

        private static ChartNote FindPlayableNoteAfter(PrototypeChart chart, double minimumHitTime)
        {

            for (int index = 0; index < chart.Notes.Count; index++)
            {

                ChartNote note = chart.Notes[index];

                if (note.HitTime >= minimumHitTime && chart.IsNotePlayable(note))
                {

                    return note;

                }

            }

            return null;

        }

        private static RuntimeNoteView FindHighestRuntimeNote()
        {

            RuntimeNoteView[] notes = Object.FindObjectsByType<RuntimeNoteView>(
                FindObjectsSortMode.None);
            RuntimeNoteView highestNote = null;

            for (int index = 0; index < notes.Length; index++)
            {

                if (highestNote == null ||
                    notes[index].transform.position.y > highestNote.transform.position.y)
                {

                    highestNote = notes[index];

                }

            }

            return highestNote;

        }

        private static RuntimeTimingGuideView FindHighestTimingGuide(bool isBar)
        {

            RuntimeTimingGuideView[] guides = Object.FindObjectsByType<RuntimeTimingGuideView>(
                FindObjectsSortMode.None);
            RuntimeTimingGuideView highestGuide = null;

            for (int index = 0; index < guides.Length; index++)
            {

                RuntimeTimingGuideView guide = guides[index];

                if (guide.IsBar != isBar)
                {

                    continue;

                }

                if (highestGuide == null ||
                    GetTimingGuideMidpoint(guide).y > GetTimingGuideMidpoint(highestGuide).y)
                {

                    highestGuide = guide;

                }

            }

            return highestGuide;

        }

        private static Vector3 GetTimingGuideMidpoint(RuntimeTimingGuideView guide)
        {

            LineRenderer line = guide.GetComponent<LineRenderer>();
            Assert.That(line, Is.Not.Null);
            return line.GetPosition(line.positionCount / 2);

        }

        private static void CaptureGameplayCamera(string screenshotPath)
        {

            Camera gameplayCamera = Camera.main;
            Assert.That(gameplayCamera, Is.Not.Null);

            RenderTexture renderTexture = new(1280, 720, 24, RenderTextureFormat.ARGB32);
            Texture2D screenshot = new(1280, 720, TextureFormat.RGBA32, false);
            RenderTexture previousActive = RenderTexture.active;
            RenderTexture previousTarget = gameplayCamera.targetTexture;

            gameplayCamera.targetTexture = renderTexture;
            RenderTexture.active = renderTexture;
            Canvas.ForceUpdateCanvases();
            gameplayCamera.Render();
            screenshot.ReadPixels(new Rect(0f, 0f, 1280f, 720f), 0, 0);
            screenshot.Apply();
            File.WriteAllBytes(screenshotPath, screenshot.EncodeToPNG());

            gameplayCamera.targetTexture = previousTarget;
            RenderTexture.active = previousActive;
            Object.Destroy(renderTexture);
            Object.Destroy(screenshot);

        }

        private static bool HasObjectWithNamePrefix<T>(string namePrefix)
            where T : Component
        {

            T[] objects = Object.FindObjectsByType<T>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);

            for (int index = 0; index < objects.Length; index++)
            {

                if (objects[index].name.StartsWith(namePrefix, System.StringComparison.Ordinal))
                {

                    return true;

                }

            }

            return false;

        }

    }

}
