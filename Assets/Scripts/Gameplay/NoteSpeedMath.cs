using UnityEngine;

namespace IdiotTape.Gameplay
{

    public static class NoteSpeedMath
    {

        public const float MinimumMultiplier = 1f;
        public const float MaximumMultiplier = 4f;

        public static float ClampMultiplier(float multiplier)
        {

            return Mathf.Clamp(multiplier, MinimumMultiplier, MaximumMultiplier);

        }

        public static float GetVisualLeadTime(float baseVisualLeadTime, float multiplier)
        {

            float safeLeadTime = Mathf.Max(0.0001f, baseVisualLeadTime);
            return safeLeadTime / ClampMultiplier(multiplier);

        }

        public static float GetMultiplierFromNormalized(float normalized)
        {

            return Mathf.Lerp(
                MinimumMultiplier,
                MaximumMultiplier,
                Mathf.Clamp01(normalized));

        }

    }

}
