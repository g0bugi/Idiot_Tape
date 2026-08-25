using System.Collections;
using System.IO;
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

        [UnityTest]
        public IEnumerator SnowEventPreparesAndReportsFullSongDuration()
        {

            GameObject playbackObject = new("Snow Playback Test");
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
            Object.Destroy(playbackObject);
            yield return null;

        }

        [UnityTest]
        public IEnumerator GameplaySceneStartsAndSpawnsRuntimeNotes()
        {

            AsyncOperation loadOperation = SceneManager.LoadSceneAsync("Gameplay", LoadSceneMode.Single);

            while (!loadOperation.isDone)
            {

                yield return null;

            }

            yield return null;

            Assert.That(Object.FindAnyObjectByType<GameplaySession>(), Is.Not.Null);
            FmodSongPlayback songPlayback = Object.FindAnyObjectByType<FmodSongPlayback>();
            Assert.That(songPlayback, Is.Not.Null);

            float preparationDeadline = Time.realtimeSinceStartup + 15f;

            while (!songPlayback.IsRunning && Time.realtimeSinceStartup < preparationDeadline)
            {

                yield return null;

            }

            Assert.That(songPlayback.IsRunning, Is.True, "FMOD song playback did not become ready.");
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

            GameplayInputRouter inputRouter = Object.FindAnyObjectByType<GameplayInputRouter>();
            Assert.That(inputRouter, Is.Not.Null);
            inputRouter.SubmitLaneInput(0, InputState.currentTime);
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
            Assert.That(instrumentText.text, Is.EqualTo("Drum"));
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

            inputRouter.RequestPause();
            yield return null;
            Assert.That(songPlayback.IsPaused, Is.False);

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
