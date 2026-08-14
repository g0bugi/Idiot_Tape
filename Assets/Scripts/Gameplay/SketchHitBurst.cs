using UnityEngine;

namespace IdiotTape.Gameplay
{

    public sealed class SketchHitBurst : MonoBehaviour
    {

        private const float Duration = 0.24f;

        private SpriteRenderer spriteRenderer;
        private Color partColor;
        private float baseScale;
        private float elapsed;

        public void Initialize(Sprite sprite, Color color, Vector3 worldPosition, float scale)
        {

            partColor = color;
            baseScale = scale;
            transform.position = worldPosition;
            transform.rotation = Quaternion.Euler(0f, 0f, 18f);
            spriteRenderer = gameObject.AddComponent<SpriteRenderer>();
            spriteRenderer.sprite = sprite;
            spriteRenderer.sortingOrder = 20;
            spriteRenderer.color = Color.white;

        }

        private void Update()
        {

            elapsed += Time.unscaledDeltaTime;
            float progress = Mathf.Clamp01(elapsed / Duration);
            float easedProgress = 1f - (1f - progress) * (1f - progress);
            transform.localScale = Vector3.one * Mathf.Lerp(baseScale * 0.82f, baseScale * 1.55f, easedProgress);
            transform.Rotate(0f, 0f, 110f * Time.unscaledDeltaTime);

            Color color = Color.Lerp(Color.white, partColor, progress * 0.7f);
            color.a = 1f - progress;
            spriteRenderer.color = color;

            if (progress >= 1f)
            {

                Destroy(gameObject);

            }

        }

    }

}
