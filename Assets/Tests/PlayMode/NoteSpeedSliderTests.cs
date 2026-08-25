using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace IdiotTape.Gameplay.Tests
{

    public sealed class NoteSpeedSliderTests
    {

        [UnityTest]
        public IEnumerator HudCreatesSliderAndMapsItsEndsToOneAndFour()
        {

            GameObject canvasObject = new("Note Speed HUD Test");
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
            canvasObject.SetActive(true);
            yield return null;
            Canvas.ForceUpdateCanvases();

            RectTransform sliderArea = GameObject.Find("NoteSpeedSlider").GetComponent<RectTransform>();
            Vector3[] corners = new Vector3[4];
            sliderArea.GetWorldCorners(corners);
            Vector2 left = RectTransformUtility.WorldToScreenPoint(
                null,
                Vector3.Lerp(corners[0], corners[1], 0.5f));
            Vector2 right = RectTransformUtility.WorldToScreenPoint(
                null,
                Vector3.Lerp(corners[3], corners[2], 0.5f));

            Assert.That(hud.NoteSpeedMultiplier, Is.EqualTo(1f));
            Assert.That(hud.TrySetNoteSpeedFromScreenPosition(right), Is.True);
            Assert.That(hud.NoteSpeedMultiplier, Is.EqualTo(4f).Within(0.01f));
            Assert.That(hud.TrySetNoteSpeedFromScreenPosition(left), Is.True);
            Assert.That(hud.NoteSpeedMultiplier, Is.EqualTo(1f).Within(0.01f));
            Assert.That(GameObject.Find("NoteSpeedValue").GetComponent<Text>().text, Is.EqualTo("x1.0"));
            Object.Destroy(canvasObject);

        }

        private static Text CreateText(string objectName, Transform parent)
        {

            GameObject textObject = new(
                objectName,
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Text));
            textObject.transform.SetParent(parent, false);
            Text text = textObject.GetComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            return text;

        }

        private static Image CreateImage(string objectName, Transform parent)
        {

            GameObject imageObject = new(
                objectName,
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image));
            imageObject.transform.SetParent(parent, false);
            return imageObject.GetComponent<Image>();

        }

        private static void SetField(GameplayHud hud, string fieldName, object value)
        {

            FieldInfo field = typeof(GameplayHud).GetField(
                fieldName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, fieldName);
            field.SetValue(hud, value);

        }

    }

}
