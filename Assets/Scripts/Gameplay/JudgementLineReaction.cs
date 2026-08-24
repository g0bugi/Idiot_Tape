using UnityEngine;

namespace IdiotTape.Gameplay
{

    public sealed class JudgementLineReaction : MonoBehaviour
    {

        private const int PointCount = 17;
        private const float PerfectDuration = 0.28f;
        private const float GoodDuration = 0.2f;

        private LineRenderer reactionLine;
        private SpriteRenderer centerBead;
        private SpriteRenderer leftPulse;
        private SpriteRenderer rightPulse;
        private float halfWidth;
        private float judgementBaseY;
        private float judgementCurvature;
        private float centerNormalizedX;
        private float segmentHalfWidth;
        private float duration;
        private float elapsed;
        private Color partColor;

        public void Initialize(
            Material material,
            float playfieldHalfWidth,
            float lineBaseY,
            float lineCurvature)
        {

            halfWidth = playfieldHalfWidth;
            judgementBaseY = lineBaseY;
            judgementCurvature = lineCurvature;

            reactionLine = gameObject.AddComponent<LineRenderer>();
            reactionLine.positionCount = PointCount;
            reactionLine.useWorldSpace = false;
            reactionLine.numCapVertices = 2;
            reactionLine.textureMode = LineTextureMode.Stretch;
            reactionLine.sharedMaterial = material;
            reactionLine.sortingOrder = 5;

            centerBead = CreateLightPoint("ContactBead", 7);
            leftPulse = CreateLightPoint("LeftPulse", 6);
            rightPulse = CreateLightPoint("RightPulse", 6);

        }

        public void Play(string noteId, float normalizedX, Color color, JudgementGrade grade)
        {

            gameObject.name = $"LineReaction_{noteId}";
            centerNormalizedX = normalizedX;
            partColor = color;
            segmentHalfWidth = grade == JudgementGrade.Perfect ? 0.085f : 0.06f;
            duration = grade == JudgementGrade.Perfect ? PerfectDuration : GoodDuration;
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

            float fade = 1f - progress;
            float easedProgress = 1f - (1f - progress) * (1f - progress);
            float startX = Mathf.Max(0f, centerNormalizedX - segmentHalfWidth);
            float endX = Mathf.Min(1f, centerNormalizedX + segmentHalfWidth);

            for (int index = 0; index < PointCount; index++)
            {

                float pointProgress = index / (PointCount - 1f);
                float normalizedX = Mathf.Lerp(startX, endX, pointProgress);
                float distanceFromCenter = Mathf.Abs(pointProgress * 2f - 1f);
                float spatialEnvelope = 1f - distanceFromCenter;
                float verticalOffset = Mathf.Sin(progress * Mathf.PI) * spatialEnvelope * 0.055f;
                float worldX = PlayfieldGeometry.GetWorldX(normalizedX, halfWidth);
                float worldY = PlayfieldGeometry.GetJudgementLineY(
                    normalizedX,
                    judgementBaseY,
                    judgementCurvature);
                reactionLine.SetPosition(index, new Vector3(worldX, worldY + verticalOffset, 0f));

            }

            Color lineColor = Color.Lerp(partColor, Color.white, 0.34f);
            lineColor.a = fade;
            reactionLine.startColor = lineColor;
            reactionLine.endColor = lineColor;
            reactionLine.startWidth = Mathf.Lerp(0.095f, 0.02f, progress);
            reactionLine.endWidth = reactionLine.startWidth;

            ApplyLightPoint(centerBead, centerNormalizedX, fade, Mathf.Lerp(0.26f, 0.1f, progress));
            ApplyLightPoint(leftPulse, Mathf.Lerp(centerNormalizedX, startX, easedProgress), fade, 0.15f);
            ApplyLightPoint(rightPulse, Mathf.Lerp(centerNormalizedX, endX, easedProgress), fade, 0.15f);

        }

        private void ApplyLightPoint(
            SpriteRenderer renderer,
            float normalizedX,
            float alpha,
            float scale)
        {

            float worldX = PlayfieldGeometry.GetWorldX(normalizedX, halfWidth);
            float worldY = PlayfieldGeometry.GetJudgementLineY(
                normalizedX,
                judgementBaseY,
                judgementCurvature);
            renderer.transform.localPosition = new Vector3(worldX, worldY, 0f);
            renderer.transform.localScale = Vector3.one * scale;
            Color color = Color.Lerp(partColor, Color.white, 0.5f);
            color.a = alpha;
            renderer.color = color;

        }

        private SpriteRenderer CreateLightPoint(string objectName, int sortingOrder)
        {

            GameObject lightObject = new(objectName);
            lightObject.transform.SetParent(transform, false);
            SpriteRenderer renderer = lightObject.AddComponent<SpriteRenderer>();
            renderer.sprite = GameplayVisualAssets.GlowSprite;
            renderer.sortingOrder = sortingOrder;
            renderer.sharedMaterial = reactionLine.sharedMaterial;
            return renderer;

        }

    }

}
