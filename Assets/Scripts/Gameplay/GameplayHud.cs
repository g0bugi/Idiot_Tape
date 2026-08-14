using UnityEngine;
using UnityEngine.UI;

namespace IdiotTape.Gameplay
{

    public sealed class GameplayHud : MonoBehaviour
    {

        private sealed class RisingTextAnimation
        {

            private const float RiseDuration = 0.18f;
            private const float HoldDuration = 0.34f;
            private const float FadeDuration = 0.3f;
            private const float RiseDistance = 18f;

            private readonly Text text;
            private readonly RectTransform rectTransform;
            private readonly Vector2 restingPosition;
            private float elapsed;
            private Color visibleColor;
            private bool persists;
            private bool isActive;

            public RisingTextAnimation(Text animatedText)
            {

                text = animatedText;
                rectTransform = animatedText.rectTransform;
                restingPosition = rectTransform.anchoredPosition;

            }

            public void Play(string value, Color color, bool persistAfterRise)
            {

                text.text = value;
                visibleColor = color;
                visibleColor.a = Mathf.Clamp01(color.a);
                persists = persistAfterRise;
                elapsed = 0f;
                isActive = true;
                Apply(0f, -RiseDistance);

            }

            public void Stop()
            {

                isActive = false;
                text.text = string.Empty;
                rectTransform.anchoredPosition = restingPosition;

            }

            public void Update(float unscaledDeltaTime)
            {

                if (!isActive)
                {

                    return;

                }

                elapsed += unscaledDeltaTime;

                if (elapsed < RiseDuration)
                {

                    float progress = elapsed / RiseDuration;
                    Apply(progress, Mathf.Lerp(-RiseDistance, 0f, progress));
                    return;

                }

                if (persists)
                {

                    Apply(1f, 0f);
                    return;

                }

                if (elapsed < RiseDuration + HoldDuration)
                {

                    Apply(1f, 0f);
                    return;

                }

                float fadeProgress = (elapsed - RiseDuration - HoldDuration) / FadeDuration;

                if (fadeProgress >= 1f)
                {

                    Stop();
                    return;

                }

                Apply(1f - fadeProgress, 0f);

            }

            private void Apply(float alpha, float verticalOffset)
            {

                Color color = visibleColor;
                color.a *= Mathf.Clamp01(alpha);
                text.color = color;
                rectTransform.anchoredPosition = restingPosition + Vector2.up * verticalOffset;

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
        private RisingTextAnimation comboAnimation;
        private RisingTextAnimation judgementAnimation;
        private RisingTextAnimation instrumentAnimation;

        private void Awake()
        {

            hudCanvas = GetComponent<Canvas>();
            comboAnimation = new RisingTextAnimation(comboText);
            judgementAnimation = new RisingTextAnimation(judgementText);
            instrumentAnimation = new RisingTextAnimation(instrumentText);

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

            comboAnimation.Play(combo.ToString(), new Color(0.92f, 0.91f, 0.88f, 0.92f), true);

        }

        public void SetProgress(float normalizedProgress)
        {

            progressFill.fillAmount = Mathf.Clamp01(normalizedProgress);

        }

        public void ShowJudgement(JudgementGrade grade)
        {

            if (grade == JudgementGrade.None)
            {

                judgementAnimation.Stop();
                return;

            }

            Color color = grade == JudgementGrade.Miss
                ? new Color(0.92f, 0.38f, 0.42f, 0.95f)
                : new Color(0.94f, 0.92f, 0.86f, 0.95f);
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

    }

}
