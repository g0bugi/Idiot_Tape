using UnityEngine;

namespace IdiotTape.Gameplay
{

    public sealed class RuntimeTimingGuideView : MonoBehaviour
    {

        private const int LinePointCount = 65;

        private double beatTime;
        private float halfWidth;
        private float spawnY;
        private float judgementBaseY;
        private float judgementCurvature;
        private float visualLeadTime;
        private LineRenderer line;

        public bool IsBar { get; private set; }

        public void Initialize(
            double chartBeatTime,
            bool isBar,
            float playfieldHalfWidth,
            float noteSpawnY,
            float lineBaseY,
            float lineCurvature,
            float leadTime,
            Material material,
            Color color,
            float width)
        {

            beatTime = chartBeatTime;
            IsBar = isBar;
            halfWidth = playfieldHalfWidth;
            spawnY = noteSpawnY;
            judgementBaseY = lineBaseY;
            judgementCurvature = lineCurvature;
            SetVisualLeadTime(leadTime);

            if (line == null)
            {

                line = gameObject.AddComponent<LineRenderer>();

            }

            line.positionCount = LinePointCount;
            line.useWorldSpace = false;
            line.startWidth = width;
            line.endWidth = width;
            line.startColor = color;
            line.endColor = color;
            line.numCapVertices = 2;
            line.textureMode = LineTextureMode.Stretch;
            line.sharedMaterial = material;
            line.sortingOrder = 1;
            gameObject.SetActive(true);

        }

        public void UpdatePresentation(double songTime)
        {

            float timeUntilBeat = (float)(beatTime - songTime);
            float progress = 1f - Mathf.Clamp01(timeUntilBeat / visualLeadTime);

            for (int index = 0; index < LinePointCount; index++)
            {

                float normalizedX = index / (LinePointCount - 1f);
                float worldX = PlayfieldGeometry.GetWorldX(normalizedX, halfWidth);
                float worldY = PlayfieldGeometry.GetTimingGuideY(
                    normalizedX,
                    spawnY,
                    judgementBaseY,
                    judgementCurvature,
                    progress);
                line.SetPosition(index, new Vector3(worldX, worldY, 0f));

            }

        }

        public void SetVisualLeadTime(float leadTime)
        {

            visualLeadTime = Mathf.Max(0.0001f, leadTime);

        }

        public void Remove()
        {

            gameObject.SetActive(false);

        }

    }

}
