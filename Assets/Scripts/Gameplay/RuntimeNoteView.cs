using UnityEngine;

namespace IdiotTape.Gameplay
{

    public sealed class RuntimeNoteView : MonoBehaviour
    {

        private ChartNote note;
        private int laneCount;
        private float halfWidth;
        private float spawnY;
        private float judgementBaseY;
        private float judgementCurvature;
        private float visualLeadTime;
        private float baseScale;

        public void Initialize(
            ChartNote chartNote,
            Sprite sprite,
            Color color,
            int chartLaneCount,
            float playfieldHalfWidth,
            float noteSpawnY,
            float lineBaseY,
            float lineCurvature,
            float leadTime,
            float scale,
            Material additiveMaterial,
            bool isPlayable)
        {

            note = chartNote;
            laneCount = chartLaneCount;
            halfWidth = playfieldHalfWidth;
            spawnY = noteSpawnY;
            judgementBaseY = lineBaseY;
            judgementCurvature = lineCurvature;
            visualLeadTime = leadTime;
            baseScale = scale;

            Sprite simpleCircleSprite = GameplayVisualAssets.CircleSprite != null
                ? GameplayVisualAssets.CircleSprite
                : sprite;
            Color outlineColor = isPlayable
                ? Color.white
                : new Color(0.24f, 0.24f, 0.27f, 0.58f);
            Color fillColor = isPlayable
                ? color
                : Color.Lerp(color, new Color(0.08f, 0.08f, 0.1f, color.a), 0.78f);
            fillColor.a = isPlayable ? color.a : 0.52f;
            CreateLayer("Outline", simpleCircleSprite, outlineColor, 10, 1f, null);
            CreateLayer("Fill", simpleCircleSprite, fillColor, 11, 0.84f, null);

            Color glowColor = color;
            glowColor.a = isPlayable ? 0.34f : 0.035f;
            CreateLayer(
                "Glow",
                GameplayVisualAssets.GlowSprite,
                glowColor,
                9,
                1.62f,
                additiveMaterial);

            Color highlightColor = Color.white;
            highlightColor.a = isPlayable ? 0.72f : 0.1f;
            SpriteRenderer highlight = CreateLayer(
                "Highlight",
                simpleCircleSprite,
                highlightColor,
                12,
                0.2f,
                additiveMaterial);
            highlight.transform.localPosition = new Vector3(-0.18f, 0.18f, 0f);
            highlight.transform.localScale = new Vector3(0.24f, 0.13f, 1f);

        }

        public void UpdatePresentation(double songTime)
        {

            float normalizedX = PlayfieldGeometry.GetLaneCenterNormalized(note.LaneIndex, laneCount);
            float targetX = PlayfieldGeometry.GetWorldX(normalizedX, halfWidth);
            float targetY = PlayfieldGeometry.GetJudgementLineY(normalizedX, judgementBaseY, judgementCurvature);
            float timeUntilHit = (float)(note.HitTime - songTime);
            float progress = 1f - Mathf.Clamp01(timeUntilHit / visualLeadTime);
            float worldY = Mathf.Lerp(spawnY, targetY, progress);
            float scale = Mathf.Lerp(baseScale * 0.88f, baseScale, progress);

            transform.position = new Vector3(targetX, worldY, 0f);
            transform.localScale = Vector3.one * scale;

        }

        public void Remove()
        {

            Destroy(gameObject);

        }

        private SpriteRenderer CreateLayer(
            string layerName,
            Sprite sprite,
            Color color,
            int sortingOrder,
            float scale,
            Material material)
        {

            GameObject layerObject = new(layerName);
            layerObject.transform.SetParent(transform, false);
            layerObject.transform.localScale = Vector3.one * scale;
            SpriteRenderer renderer = layerObject.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.color = color;
            renderer.sortingOrder = sortingOrder;

            if (material != null)
            {

                renderer.sharedMaterial = material;

            }

            return renderer;

        }

    }

}
