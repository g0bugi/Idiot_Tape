using UnityEngine;

namespace IdiotTape.Gameplay
{

    public static class PlayfieldGeometry
    {

        public static float GetLaneCenterNormalized(int laneIndex, int laneCount)
        {

            return (laneIndex + 0.5f) / laneCount;

        }

        public static int GetLaneIndex(float normalizedX, int laneCount)
        {

            return Mathf.Clamp(Mathf.FloorToInt(normalizedX * laneCount), 0, laneCount - 1);

        }

        public static float GetWorldX(float normalizedX, float halfWidth)
        {

            return Mathf.Lerp(-halfWidth, halfWidth, normalizedX);

        }

        public static float GetJudgementLineY(float normalizedX, float baseY, float curvature)
        {

            float centeredX = normalizedX * 2f - 1f;
            return baseY + curvature * centeredX * centeredX;

        }

        public static float GetTimingGuideY(
            float normalizedX,
            float spawnY,
            float judgementBaseY,
            float judgementCurvature,
            float progress)
        {

            float clampedProgress = Mathf.Clamp01(progress);
            float shapeProgress = Mathf.SmoothStep(0f, 1f, clampedProgress);
            float centeredX = normalizedX * 2f - 1f;
            float guideBaseY = Mathf.Lerp(spawnY, judgementBaseY, clampedProgress);
            return guideBaseY + judgementCurvature * centeredX * centeredX * shapeProgress;

        }

    }

}
