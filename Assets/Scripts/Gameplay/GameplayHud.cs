using UnityEngine;
using UnityEngine.UI;

namespace IdiotTape.Gameplay
{

    public sealed class GameplayHud : MonoBehaviour
    {

        private sealed class RisingTextAnimation
        {

            private readonly Text text;
            private readonly RectTransform rectTransform;
            private readonly Vector2 restingPosition;
            private readonly Vector3 restingScale;
            private readonly float riseDuration;
            private readonly float holdDuration;
            private readonly float fadeDuration;
            private readonly float riseDistance;
            private readonly float entryScale;
            private readonly float punchScale;
            private float elapsed;
            private Color visibleColor;
            private bool persists;
            private bool isActive;

            public RisingTextAnimation(
                Text animatedText,
                float riseDuration,
                float holdDuration,
                float fadeDuration,
                float riseDistance,
                float entryScale,
                float punchScale)
            {

                text = animatedText;
                rectTransform = animatedText.rectTransform;
                restingPosition = rectTransform.anchoredPosition;
                restingScale = rectTransform.localScale;
                this.riseDuration = riseDuration;
                this.holdDuration = holdDuration;
                this.fadeDuration = fadeDuration;
                this.riseDistance = riseDistance;
                this.entryScale = entryScale;
                this.punchScale = punchScale;

            }

            public void Play(string value, Color color, bool persistAfterRise)
            {

                text.text = value;
                visibleColor = color;
                visibleColor.a = Mathf.Clamp01(color.a);
                persists = persistAfterRise;
                elapsed = 0f;
                isActive = true;
                Apply(0f, -riseDistance, entryScale);

            }

            public void Stop()
            {

                isActive = false;
                text.text = string.Empty;
                rectTransform.anchoredPosition = restingPosition;
                rectTransform.localScale = restingScale;

            }

            public void Update(float unscaledDeltaTime)
            {

                if (!isActive)
                {

                    return;

                }

                elapsed += unscaledDeltaTime;

                if (elapsed < riseDuration)
                {

                    float progress = elapsed / riseDuration;
                    float easedProgress = 1f - Mathf.Pow(1f - progress, 3f);
                    float scale = Mathf.Lerp(entryScale, 1f, easedProgress) +
                        Mathf.Sin(progress * Mathf.PI) * punchScale;
                    Apply(
                        easedProgress,
                        Mathf.Lerp(-riseDistance, 0f, easedProgress),
                        scale);
                    return;

                }

                if (persists)
                {

                    Apply(1f, 0f, 1f);
                    return;

                }

                if (elapsed < riseDuration + holdDuration)
                {

                    Apply(1f, 0f, 1f);
                    return;

                }

                float fadeProgress = (elapsed - riseDuration - holdDuration) / fadeDuration;

                if (fadeProgress >= 1f)
                {

                    Stop();
                    return;

                }

                Apply(1f - fadeProgress, 0f, Mathf.Lerp(1f, 1.08f, fadeProgress));

            }

            private void Apply(float alpha, float verticalOffset, float scale)
            {

                Color color = visibleColor;
                color.a *= Mathf.Clamp01(alpha);
                text.color = color;
                rectTransform.anchoredPosition = restingPosition + Vector2.up * verticalOffset;
                rectTransform.localScale = restingScale * scale;

            }

        }

        [SerializeField] private Text scoreText;
        [SerializeField] private Text comboText;
        [SerializeField] private Text judgementText;
        [SerializeField] private Text instrumentText;
        [SerializeField] private Image progressFill;
        [SerializeField] private RectTransform pauseButtonArea;
        [SerializeField] private Text pauseButtonText;

        private Canvas hudCanvas;
        private Slider noteSpeedSlider;
        private RectTransform noteSpeedSliderArea;
        private Text noteSpeedValueText;
        private float noteSpeedMultiplier = NoteSpeedMath.MinimumMultiplier;
        private RisingTextAnimation comboAnimation;
        private RisingTextAnimation judgementAnimation;
        private RisingTextAnimation instrumentAnimation;

        private void Awake()
        {

            hudCanvas = GetComponent<Canvas>();
            CreateNoteSpeedControl();
            ConfigurePrimaryFeedbackText(
                comboText,
                88,
                new Vector2(0.32f, 0.52f),
                new Vector2(0.68f, 0.74f),
                new Vector2(3f, -3f));
            ConfigurePrimaryFeedbackText(
                judgementText,
                72,
                new Vector2(0.28f, 0.38f),
                new Vector2(0.72f, 0.53f),
                new Vector2(3f, -3f));
            comboAnimation = new RisingTextAnimation(comboText, 0.16f, 0f, 0.2f, 34f, 0.68f, 0.16f);
            judgementAnimation = new RisingTextAnimation(
                judgementText,
                0.14f,
                0.3f,
                0.24f,
                30f,
                0.72f,
                0.14f);
            instrumentAnimation = new RisingTextAnimation(
                instrumentText,
                0.18f,
                0.34f,
                0.3f,
                18f,
                0.82f,
                0.06f);

        }

        private void Update()
        {

            float deltaTime = Time.unscaledDeltaTime;
            comboAnimation.Update(deltaTime);
            judgementAnimation.Update(deltaTime);
            instrumentAnimation.Update(deltaTime);

        }

        public void SetScore(int score)
        {

            scoreText.text = score.ToString("00000000");

        }

        public void ShowCombo(int combo)
        {

            if (combo <= 0)
            {

                comboAnimation.Stop();
                return;

            }

            string value = $"<size=88>{combo}</size>\n<size=26>COMBO</size>";
            comboAnimation.Play(value, new Color(1f, 0.82f, 0.34f, 0.98f), true);

        }

        public void SetProgress(float normalizedProgress)
        {

            progressFill.fillAmount = Mathf.Clamp01(normalizedProgress);

        }

        public float NoteSpeedMultiplier => noteSpeedMultiplier;

        public bool TrySetNoteSpeedFromScreenPosition(
            Vector2 screenPosition,
            bool requireInside = true)
        {

            if (noteSpeedSliderArea == null)
            {

                return false;

            }

            Camera eventCamera = hudCanvas != null && hudCanvas.renderMode != RenderMode.ScreenSpaceOverlay
                ? hudCanvas.worldCamera
                : null;

            if (requireInside &&
                !RectTransformUtility.RectangleContainsScreenPoint(
                    noteSpeedSliderArea,
                    screenPosition,
                    eventCamera))
            {

                return false;

            }

            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    noteSpeedSliderArea,
                    screenPosition,
                    eventCamera,
                    out Vector2 localPosition))
            {

                return false;

            }

            float normalized = Mathf.InverseLerp(
                noteSpeedSliderArea.rect.xMin,
                noteSpeedSliderArea.rect.xMax,
                localPosition.x);
            SetNoteSpeedMultiplier(NoteSpeedMath.GetMultiplierFromNormalized(normalized));
            return true;

        }

        public void ShowJudgement(JudgementGrade grade)
        {

            if (grade == JudgementGrade.None)
            {

                judgementAnimation.Stop();
                return;

            }

            Color color;

            switch (grade)
            {

                case JudgementGrade.Perfect:
                    color = new Color(1f, 0.84f, 0.32f, 1f);
                    break;
                case JudgementGrade.Good:
                    color = new Color(0.38f, 0.86f, 1f, 1f);
                    break;
                default:
                    color = new Color(1f, 0.3f, 0.48f, 1f);
                    break;

            }

            judgementAnimation.Play(grade.ToString().ToUpperInvariant(), color, false);

        }

        public void ShowInstrument(string displayName, Color color)
        {

            color.a = 0.95f;
            instrumentAnimation.Play(displayName, color, false);

        }

        public bool IsPauseButtonPress(Vector2 screenPosition)
        {

            Camera eventCamera = hudCanvas != null && hudCanvas.renderMode != RenderMode.ScreenSpaceOverlay
                ? hudCanvas.worldCamera
                : null;
            return RectTransformUtility.RectangleContainsScreenPoint(pauseButtonArea, screenPosition, eventCamera);

        }

        public void SetPaused(bool paused)
        {

            pauseButtonText.text = paused ? ">" : "II";

            if (paused)
            {

                judgementAnimation.Play("PAUSED", new Color(0.92f, 0.91f, 0.88f, 0.95f), true);

            }
            else
            {

                judgementAnimation.Stop();

            }

        }

        private void SetNoteSpeedMultiplier(float multiplier)
        {

            noteSpeedMultiplier = NoteSpeedMath.ClampMultiplier(multiplier);
            noteSpeedSlider?.SetValueWithoutNotify(noteSpeedMultiplier);

            if (noteSpeedValueText != null)
            {

                noteSpeedValueText.text = $"x{noteSpeedMultiplier:0.0}";

            }

        }

        private void CreateNoteSpeedControl()
        {

            GameObject panelObject = CreateImageObject(
                "NoteSpeedControl",
                transform,
                new Color(0.04f, 0.045f, 0.055f, 0.72f),
                new Vector2(0.035f, 0.785f),
                new Vector2(0.195f, 0.835f));
            Text caption = CreateTextObject(
                "NoteSpeedCaption",
                panelObject.transform,
                "SPEED",
                18,
                TextAnchor.MiddleCenter,
                new Vector2(0.02f, 0f),
                new Vector2(0.29f, 1f));
            caption.color = new Color(0.78f, 0.79f, 0.82f, 0.9f);

            GameObject sliderObject = new(
                "NoteSpeedSlider",
                typeof(RectTransform),
                typeof(Slider));
            sliderObject.transform.SetParent(panelObject.transform, false);
            noteSpeedSliderArea = sliderObject.GetComponent<RectTransform>();
            SetAnchors(
                noteSpeedSliderArea,
                new Vector2(0.3f, 0.16f),
                new Vector2(0.78f, 0.84f));

            GameObject trackObject = CreateImageObject(
                "Track",
                sliderObject.transform,
                new Color(1f, 1f, 1f, 0.16f),
                new Vector2(0f, 0.42f),
                new Vector2(1f, 0.58f));
            GameObject fillObject = CreateImageObject(
                "Fill",
                sliderObject.transform,
                new Color(0.3f, 0.9f, 1f, 0.9f),
                new Vector2(0f, 0.36f),
                new Vector2(1f, 0.64f));
            GameObject handleObject = CreateImageObject(
                "Handle",
                sliderObject.transform,
                new Color(0.94f, 0.95f, 1f, 1f),
                new Vector2(0f, 0.16f),
                new Vector2(0f, 0.84f));
            RectTransform handleRect = handleObject.GetComponent<RectTransform>();
            handleRect.sizeDelta = new Vector2(14f, 0f);

            noteSpeedSlider = sliderObject.GetComponent<Slider>();
            noteSpeedSlider.minValue = NoteSpeedMath.MinimumMultiplier;
            noteSpeedSlider.maxValue = NoteSpeedMath.MaximumMultiplier;
            noteSpeedSlider.wholeNumbers = false;
            noteSpeedSlider.direction = Slider.Direction.LeftToRight;
            noteSpeedSlider.fillRect = fillObject.GetComponent<RectTransform>();
            noteSpeedSlider.handleRect = handleRect;
            noteSpeedSlider.targetGraphic = handleObject.GetComponent<Image>();
            noteSpeedSlider.transition = Selectable.Transition.None;
            trackObject.GetComponent<Image>().raycastTarget = false;
            fillObject.GetComponent<Image>().raycastTarget = false;
            handleObject.GetComponent<Image>().raycastTarget = false;

            noteSpeedValueText = CreateTextObject(
                "NoteSpeedValue",
                panelObject.transform,
                "x1.0",
                20,
                TextAnchor.MiddleCenter,
                new Vector2(0.79f, 0f),
                new Vector2(0.99f, 1f));
            noteSpeedValueText.color = new Color(0.92f, 0.94f, 1f, 0.96f);
            SetNoteSpeedMultiplier(NoteSpeedMath.MinimumMultiplier);

        }

        private static GameObject CreateImageObject(
            string objectName,
            Transform parent,
            Color color,
            Vector2 anchorMin,
            Vector2 anchorMax)
        {

            GameObject imageObject = new(
                objectName,
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image));
            imageObject.transform.SetParent(parent, false);
            RectTransform rectTransform = imageObject.GetComponent<RectTransform>();
            SetAnchors(rectTransform, anchorMin, anchorMax);
            Image image = imageObject.GetComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            return imageObject;

        }

        private static Text CreateTextObject(
            string objectName,
            Transform parent,
            string value,
            int fontSize,
            TextAnchor alignment,
            Vector2 anchorMin,
            Vector2 anchorMax)
        {

            GameObject textObject = new(
                objectName,
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Text));
            textObject.transform.SetParent(parent, false);
            RectTransform rectTransform = textObject.GetComponent<RectTransform>();
            SetAnchors(rectTransform, anchorMin, anchorMax);
            Text text = textObject.GetComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.text = value;
            text.fontSize = fontSize;
            text.alignment = alignment;
            text.raycastTarget = false;
            return text;

        }

        private static void SetAnchors(
            RectTransform rectTransform,
            Vector2 anchorMin,
            Vector2 anchorMax)
        {

            rectTransform.anchorMin = anchorMin;
            rectTransform.anchorMax = anchorMax;
            rectTransform.offsetMin = Vector2.zero;
            rectTransform.offsetMax = Vector2.zero;

        }

        private static void ConfigurePrimaryFeedbackText(
            Text text,
            int fontSize,
            Vector2 anchorMin,
            Vector2 anchorMax,
            Vector2 outlineDistance)
        {

            RectTransform rectTransform = text.rectTransform;
            rectTransform.anchorMin = anchorMin;
            rectTransform.anchorMax = anchorMax;
            rectTransform.offsetMin = Vector2.zero;
            rectTransform.offsetMax = Vector2.zero;
            text.fontSize = fontSize;
            text.fontStyle = FontStyle.Bold;
            text.alignment = TextAnchor.MiddleCenter;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.raycastTarget = false;

            Outline outline = text.GetComponent<Outline>();

            if (outline == null)
            {

                outline = text.gameObject.AddComponent<Outline>();

            }

            outline.effectColor = new Color(0f, 0f, 0f, 0.82f);
            outline.effectDistance = outlineDistance;
            outline.useGraphicAlpha = true;

        }

    }

}
