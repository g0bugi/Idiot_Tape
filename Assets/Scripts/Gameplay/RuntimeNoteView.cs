using UnityEngine;

namespace IdiotTape.Gameplay
{

    public sealed class RuntimeNoteView : MonoBehaviour
    {

        private ChartNote note;
        private SpriteRenderer spriteRenderer;
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
            float scale)
        {

            note = chartNote;
            laneCount = chartLaneCount;
            halfWidth = playfieldHalfWidth;
            spawnY = noteSpawnY;
            judgementBaseY = lineBaseY;
            judgementCurvature = lineCurvature;
            visualLeadTime = leadTime;
            baseScale = scale;

            spriteRenderer = gameObject.AddComponent<SpriteRenderer>();
            spriteRenderer.sprite = sprite;
            spriteRenderer.color = color;
            spriteRenderer.sortingOrder = 10;

            float rotation = ((note.LaneIndex * 29) + Mathf.RoundToInt((float)note.HitTime * 17f)) % 24 - 12f;
            transform.rotation = Quaternion.Euler(0f, 0f, rotation);

        }

        public void UpdatePresentation(double songTime)
        {

            float normalizedX = PlayfieldGeometry.GetLaneCenterNormalized(note.LaneIndex, laneCount);
            float targetX = PlayfieldGeometry.GetWorldX(normalizedX, halfWidth);
            float targetY = PlayfieldGeometry.GetJudgementLineY(normalizedX, judgementBaseY, judgementCurvature);
            float timeUntilHit = (float)(note.HitTime - songTime);
            float progress = 1f - Mathf.Clamp01(timeUntilHit / visualLeadTime);
            float worldY = Mathf.Lerp(spawnY, targetY, progress);
            float scale = Mathf.Lerp(baseScale * 0.72f, baseScale, progress);

            transform.position = new Vector3(targetX, worldY, 0f);
            transform.localScale = Vector3.one * scale;

        }

        public void Remove()
        {

            Destroy(gameObject);

        }

    }

}
