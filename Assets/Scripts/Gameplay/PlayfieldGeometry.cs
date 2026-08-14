using UnityEngine;

namespace IdiotTape.Gameplay
{

    public static class PlayfieldGeometry
    {

        public static float GetLaneCenterNormalized(int laneIndex, int laneCount)
        {

            return (laneIndex + 0.5f) / laneCount;

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

    }

}
