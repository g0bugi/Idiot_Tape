using System.Collections;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace IdiotTape.Gameplay.Tests
{

    public sealed class GameplaySceneSmokeTests
    {

        [UnityTest]
        public IEnumerator GameplaySceneStartsAndSpawnsRuntimeNotes()
        {

            AsyncOperation loadOperation = SceneManager.LoadSceneAsync("Gameplay", LoadSceneMode.Single);

            while (!loadOperation.isDone)
            {

                yield return null;

            }

            yield return null;
            yield return new WaitForSecondsRealtime(0.4f);

            Assert.That(Object.FindAnyObjectByType<GameplaySession>(), Is.Not.Null);
            Assert.That(Object.FindAnyObjectByType<DspSongClock>(), Is.Not.Null);
            Assert.That(Object.FindObjectsByType<RuntimeNoteView>(FindObjectsSortMode.None).Length, Is.GreaterThan(0));

            CaptureGameplayCamera(Path.GetFullPath("Logs/GameplayPreview.png"));

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
            gameplayCamera.Render();
            screenshot.ReadPixels(new Rect(0f, 0f, 1280f, 720f), 0, 0);
            screenshot.Apply();
            File.WriteAllBytes(screenshotPath, screenshot.EncodeToPNG());

            gameplayCamera.targetTexture = previousTarget;
            RenderTexture.active = previousActive;
            Object.Destroy(renderTexture);
            Object.Destroy(screenshot);

        }

    }

}
