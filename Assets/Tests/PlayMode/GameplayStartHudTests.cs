using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace IdiotTape.Gameplay.Tests
{

    public sealed class GameplayStartHudTests
    {

        [UnityTest]
        public IEnumerator StartPromptRoutesOnlyVisibleControlsAndPreservesPlayerChoices()
        {

            GameObject canvasObject = new("Start Flow HUD Test");
            canvasObject.SetActive(false);
            Canvas canvas = canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            CanvasScaler scaler = canvasObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            GameplayHud hud = canvasObject.AddComponent<GameplayHud>();
            SetField(hud, "scoreText", CreateText("Score", canvasObject.transform));
            SetField(hud, "comboText", CreateText("Combo", canvasObject.transform));
            SetField(hud, "judgementText", CreateText("Judgement", canvasObject.transform));
            SetField(hud, "instrumentText", CreateText("Instrument", canvasObject.transform));
            SetField(hud, "progressFill", CreateImage("Progress", canvasObject.transform));
            RectTransform pauseArea = CreateImage("Pause", canvasObject.transform).rectTransform;
            SetField(hud, "pauseButtonArea", pauseArea);
            SetField(hud, "pauseButtonText", CreateText("PauseText", pauseArea));

            try
            {

                canvasObject.SetActive(true);
                yield return null;
                hud.ShowStartPrompt("Another Song <Live>");
                Canvas.ForceUpdateCanvases();
                RectTransform startArea = FindArea(canvasObject, "StartPrompt/StartButton");
                RectTransform preparationArea = FindArea(canvasObject, "StartPrompt/PreparationButton");
                RectTransform speedArea = FindArea(canvasObject, "NoteSpeedControl/NoteSpeedSlider");
                Slider speedSlider = speedArea.GetComponent<Slider>();
                Text speedValueText = canvasObject.transform
                    .Find("NoteSpeedControl/NoteSpeedValue").GetComponent<Text>();
                Vector2 startPoint = ScreenCenter(startArea);
                Vector2 preparationPoint = ScreenCenter(preparationArea);
                Vector2 pausePoint = ScreenCenter(pauseArea);
                Vector3[] corners = new Vector3[4];
                speedArea.GetWorldCorners(corners);
                Vector2 speedRight = RectTransformUtility.WorldToScreenPoint(
                    null,
                    Vector3.Lerp(corners[3], corners[2], 0.5f));
                Vector2 speedLeft = RectTransformUtility.WorldToScreenPoint(
                    null,
                    Vector3.Lerp(corners[0], corners[1], 0.5f));
                Vector2 speedMiddle = Vector2.Lerp(speedLeft, speedRight, 0.5f);
                Vector2 dragOutside = speedRight + new Vector2(80f, 60f);

                Text songTitle = canvasObject.transform.Find("StartPrompt/SongTitle").GetComponent<Text>();
                Assert.That(songTitle.text, Is.EqualTo("Another Song <Live>"));
                Assert.That(songTitle.supportRichText, Is.False);
                Assert.That(canvasObject.GetComponentsInChildren<Slider>(true), Has.Length.EqualTo(1));
                Assert.That(canvasObject.transform.Find("StartPrompt").GetComponentsInChildren<Slider>(true), Is.Empty);
                Assert.That(preparationArea.GetComponentInChildren<Text>().text, Does.StartWith("COUNT-IN"));
                Assert.That(speedSlider.interactable, Is.True);
                Assert.That(hud.IsStartButtonPress(startPoint), Is.True);
                Assert.That(hud.IsPauseButtonPress(pausePoint), Is.False);
                Assert.That(hud.ShortPreparation, Is.False);
                Assert.That(hud.TryTogglePreparationFromScreenPosition(preparationPoint), Is.True);
                Assert.That(hud.ShortPreparation, Is.True);
                Assert.That(hud.TrySetNoteSpeedFromScreenPosition(speedMiddle), Is.True);
                Assert.That(hud.NoteSpeedMultiplier, Is.EqualTo(2.5f).Within(0.01f));
                Assert.That(speedSlider.value, Is.EqualTo(hud.NoteSpeedMultiplier));
                Assert.That(speedValueText.text, Is.EqualTo("x2.5"));
                Assert.That(hud.TrySetNoteSpeedFromScreenPosition(dragOutside, false), Is.True);
                Assert.That(hud.NoteSpeedMultiplier, Is.EqualTo(4f).Within(0.01f));
                Assert.That(speedSlider.value, Is.EqualTo(hud.NoteSpeedMultiplier));
                Assert.That(speedValueText.text, Is.EqualTo("x4.0"));

                hud.ShowCountIn(5);
                Assert.That(speedSlider.interactable, Is.False);
                Assert.That(hud.IsStartButtonPress(startPoint), Is.False);
                Assert.That(hud.TryTogglePreparationFromScreenPosition(preparationPoint), Is.False);
                Assert.That(hud.TrySetNoteSpeedFromScreenPosition(speedLeft, false), Is.False);
                Assert.That(hud.NoteSpeedMultiplier, Is.EqualTo(4f).Within(0.01f));
                Assert.That(hud.ShortPreparation, Is.True);
                Assert.That(hud.IsPauseButtonPress(pausePoint), Is.False);
                Text count = canvasObject.transform.Find("CountInDisplay/CountInText").GetComponent<Text>();
                Assert.That(count.text, Is.EqualTo("READY   5"));
                Assert.That(count.gameObject.activeInHierarchy, Is.True);

                hud.ShowCountIn(0);
                Assert.That(count.text, Is.EqualTo("READY"));
                hud.HideStartFlow();
                Assert.That(count.gameObject.activeInHierarchy, Is.False);
                Assert.That(hud.IsStartButtonPress(startPoint), Is.False);
                Assert.That(hud.TryTogglePreparationFromScreenPosition(preparationPoint), Is.False);
                Assert.That(hud.IsPauseButtonPress(pausePoint), Is.True);
                Assert.That(speedSlider.interactable, Is.True);

                hud.ShowStartPrompt("Restarted Song");
                Assert.That(FindArea(canvasObject, "NoteSpeedControl/NoteSpeedSlider"), Is.SameAs(speedArea));
                Assert.That(canvasObject.GetComponentsInChildren<Slider>(true), Has.Length.EqualTo(1));
                Assert.That(hud.ShortPreparation, Is.True);
                Assert.That(hud.IsStartButtonPress(startPoint), Is.True);
                Assert.That(hud.NoteSpeedMultiplier, Is.EqualTo(4f).Within(0.01f));
                Assert.That(hud.TrySetNoteSpeedFromScreenPosition(speedLeft), Is.True);
                Assert.That(hud.NoteSpeedMultiplier, Is.EqualTo(1f).Within(0.01f));

            }
            finally
            {

                Object.Destroy(canvasObject);

            }

            yield return null;

        }

        private static Vector2 ScreenCenter(RectTransform area)
        {

            return RectTransformUtility.WorldToScreenPoint(null, area.TransformPoint(area.rect.center));

        }

        private static RectTransform FindArea(GameObject root, string path)
        {

            return root.transform.Find(path).GetComponent<RectTransform>();

        }

        private static Text CreateText(string objectName, Transform parent)
        {

            GameObject textObject = new(objectName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            textObject.transform.SetParent(parent, false);
            Text text = textObject.GetComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            return text;

        }

        private static Image CreateImage(string objectName, Transform parent)
        {

            GameObject imageObject = new(objectName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            imageObject.transform.SetParent(parent, false);
            return imageObject.GetComponent<Image>();

        }

        private static void SetField(GameplayHud hud, string fieldName, object value)
        {

            FieldInfo field = typeof(GameplayHud).GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, fieldName);
            field.SetValue(hud, value);

        }

    }

}
