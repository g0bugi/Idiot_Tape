using UnityEngine;

namespace IdiotTape.Gameplay
{

    internal static class GameplayVisualAssets
    {

        private const int TextureSize = 128;

        private static Sprite circleSprite;
        private static Sprite glowSprite;
        private static Sprite ringSprite;
        private static Sprite roundedRectangleSprite;

        public static Sprite CircleSprite => circleSprite ??= CreateSprite("GameplayCircle", GetCircleAlpha);
        public static Sprite GlowSprite => glowSprite ??= CreateSprite("GameplayGlow", GetGlowAlpha);
        public static Sprite RingSprite => ringSprite ??= CreateSprite("GameplayRing", GetRingAlpha);
        public static Sprite RoundedRectangleSprite => roundedRectangleSprite ??= CreateRoundedRectangle();

        private static Sprite CreateRoundedRectangle()
        {

            const int size = 32;
            Texture2D texture = new(size, size, TextureFormat.RGBA32, false)
            {
                name = "GameplayRoundedRectangleTexture",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave
            };
            Color32[] pixels = new Color32[size * size];

            for (int y = 0; y < size; y++)
            {

                for (int x = 0; x < size; x++)
                {

                    float dx = Mathf.Max(0f, Mathf.Abs(x - 15.5f) - 7.5f);
                    float dy = Mathf.Max(0f, Mathf.Abs(y - 15.5f) - 7.5f);
                    byte alpha = (byte)Mathf.RoundToInt(255f * Mathf.Clamp01(8f - Mathf.Sqrt(dx * dx + dy * dy)));
                    pixels[y * size + x] = new Color32(255, 255, 255, alpha);

                }

            }

            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            Sprite sprite = Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f),
                100f, 0, SpriteMeshType.FullRect, new Vector4(8f, 8f, 8f, 8f));
            sprite.name = "GameplayRoundedRectangle";
            sprite.hideFlags = HideFlags.HideAndDontSave;
            return sprite;

        }

        private static Sprite CreateSprite(string assetName, System.Func<float, float> getAlpha)
        {

            Texture2D texture = new(TextureSize, TextureSize, TextureFormat.RGBA32, false)
            {
                name = $"{assetName}Texture",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave
            };
            Color32[] pixels = new Color32[TextureSize * TextureSize];
            float center = (TextureSize - 1f) * 0.5f;

            for (int y = 0; y < TextureSize; y++)
            {

                for (int x = 0; x < TextureSize; x++)
                {

                    float normalizedX = (x - center) / center;
                    float normalizedY = (y - center) / center;
                    float radius = Mathf.Sqrt(normalizedX * normalizedX + normalizedY * normalizedY);
                    byte alpha = (byte)Mathf.RoundToInt(Mathf.Clamp01(getAlpha(radius)) * 255f);
                    pixels[y * TextureSize + x] = new Color32(255, 255, 255, alpha);

                }

            }

            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            Sprite sprite = Sprite.Create(
                texture,
                new Rect(0f, 0f, TextureSize, TextureSize),
                new Vector2(0.5f, 0.5f),
                TextureSize);
            sprite.name = assetName;
            sprite.hideFlags = HideFlags.HideAndDontSave;
            return sprite;

        }

        private static float GetCircleAlpha(float radius)
        {

            return SmoothFalloff(0.94f, 1f, radius);

        }

        private static float GetGlowAlpha(float radius)
        {

            float normalizedRadius = Mathf.Clamp01(radius);
            float falloff = 1f - normalizedRadius;
            return falloff * falloff * 0.82f;

        }

        private static float GetRingAlpha(float radius)
        {

            float distanceFromRing = Mathf.Abs(radius - 0.82f);
            return SmoothFalloff(0.045f, 0.095f, distanceFromRing);

        }

        private static float SmoothFalloff(float opaqueUntil, float transparentAt, float value)
        {

            float progress = Mathf.InverseLerp(opaqueUntil, transparentAt, value);
            return 1f - Mathf.SmoothStep(0f, 1f, progress);

        }

    }

}
