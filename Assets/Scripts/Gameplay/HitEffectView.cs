using UnityEngine;

namespace IdiotTape.Gameplay
{

    public sealed class HitEffectView : MonoBehaviour
    {

        private const int MaximumSparkCount = 14;
        private const float PerfectDuration = 0.34f;
        private const float GoodDuration = 0.25f;

        private sealed class SparkLayer
        {

            public Transform Transform;
            public SpriteRenderer Renderer;
            public Vector2 Direction;
            public float Distance;

        }

        private readonly SparkLayer[] sparks = new SparkLayer[MaximumSparkCount];

        private SpriteRenderer flashRenderer;
        private SpriteRenderer glowRenderer;
        private SpriteRenderer firstRingRenderer;
        private SpriteRenderer secondRingRenderer;
        private Color partColor;
        private float baseScale;
        private float duration;
        private float elapsed;
        private int activeSparkCount;
        private bool hasSecondRing;

        public void Initialize(Material additiveMaterial)
        {

            glowRenderer = CreateLayer("Afterglow", GameplayVisualAssets.GlowSprite, 18, additiveMaterial);
            firstRingRenderer = CreateLayer("RingA", GameplayVisualAssets.RingSprite, 20, additiveMaterial);
            secondRingRenderer = CreateLayer("RingB", GameplayVisualAssets.RingSprite, 19, additiveMaterial);
            flashRenderer = CreateLayer("CoreFlash", GameplayVisualAssets.CircleSprite, 22, additiveMaterial);

            for (int index = 0; index < sparks.Length; index++)
            {

                SpriteRenderer renderer = CreateLayer(
                    $"Spark_{index:00}",
                    GameplayVisualAssets.CircleSprite,
                    21,
                    additiveMaterial);
                sparks[index] = new SparkLayer
                {
                    Transform = renderer.transform,
                    Renderer = renderer
                };

            }

        }

        public void Play(
            string noteId,
            Vector3 worldPosition,
            Color color,
            float scale,
            JudgementGrade grade)
        {

            gameObject.name = $"HitEffect_{noteId}";
            transform.position = worldPosition;
            transform.localScale = Vector3.one;
            partColor = color;
            baseScale = scale;
            duration = grade == JudgementGrade.Perfect ? PerfectDuration : GoodDuration;
            activeSparkCount = grade == JudgementGrade.Perfect ? MaximumSparkCount : 7;
            hasSecondRing = grade == JudgementGrade.Perfect;
            elapsed = 0f;
            gameObject.SetActive(true);
            ApplyPresentation(0f);

        }

        private void Update()
        {

            elapsed += Time.unscaledDeltaTime;
            float progress = Mathf.Clamp01(elapsed / duration);
            ApplyPresentation(progress);

            if (progress >= 1f)
            {

                gameObject.SetActive(false);

            }

        }

        private void ApplyPresentation(float progress)
        {

            float easedProgress = 1f - (1f - progress) * (1f - progress);
            float fade = 1f - progress;
            float flashFade = 1f - Mathf.Clamp01(progress / 0.34f);
            SetColor(flashRenderer, Color.white, flashFade);
            flashRenderer.transform.localScale = Vector3.one * Mathf.Lerp(
                baseScale * 0.42f,
                baseScale * 1.05f,
                easedProgress);

            SetColor(glowRenderer, partColor, fade * 0.98f);
            glowRenderer.transform.localScale = Vector3.one * Mathf.Lerp(
                baseScale * 0.82f,
                baseScale * 2.72f,
                easedProgress);

            ApplyRing(firstRingRenderer, progress, 0f, 1f);
            secondRingRenderer.enabled = hasSecondRing;

            if (hasSecondRing)
            {

                ApplyRing(secondRingRenderer, progress, 0.13f, 0.82f);

            }

            for (int index = 0; index < sparks.Length; index++)
            {

                SparkLayer spark = sparks[index];
                bool isActive = index < activeSparkCount;
                spark.Renderer.enabled = isActive;

                if (!isActive)
                {

                    continue;

                }

                float angle = 18f + index * (144f / Mathf.Max(1, activeSparkCount - 1));
                float angleRadians = angle * Mathf.Deg2Rad;
                spark.Direction = new Vector2(Mathf.Cos(angleRadians), Mathf.Sin(angleRadians));
                spark.Distance = baseScale * (1.02f + (index % 3) * 0.2f);
                spark.Transform.localPosition = spark.Direction * (spark.Distance * easedProgress);
                spark.Transform.localRotation = Quaternion.Euler(0f, 0f, angle - 90f);
                spark.Transform.localScale = new Vector3(
                    baseScale * Mathf.Lerp(0.105f, 0.035f, progress),
                    baseScale * Mathf.Lerp(0.52f, 0.2f, progress),
                    1f);
                SetColor(spark.Renderer, index % 5 == 0 ? Color.white : partColor, fade);

            }

        }

        private void ApplyRing(
            SpriteRenderer renderer,
            float progress,
            float delay,
            float maximumAlpha)
        {

            float ringProgress = Mathf.Clamp01((progress - delay) / (1f - delay));
            float easedRingProgress = 1f - (1f - ringProgress) * (1f - ringProgress);
            renderer.transform.localScale = Vector3.one * Mathf.Lerp(
                baseScale * 0.55f,
                baseScale * 2.38f,
                easedRingProgress);
            float alpha = progress < delay ? 0f : (1f - ringProgress) * maximumAlpha;
            SetColor(renderer, Color.Lerp(partColor, Color.white, 0.18f), alpha);

        }

        private SpriteRenderer CreateLayer(
            string layerName,
            Sprite sprite,
            int sortingOrder,
            Material material)
        {

            GameObject layerObject = new(layerName);
            layerObject.transform.SetParent(transform, false);
            SpriteRenderer renderer = layerObject.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.sortingOrder = sortingOrder;
            renderer.sharedMaterial = material;
            return renderer;

        }

        private static void SetColor(SpriteRenderer renderer, Color color, float alpha)
        {

            color.a = Mathf.Clamp01(alpha);
            renderer.color = color;

        }

    }

}
