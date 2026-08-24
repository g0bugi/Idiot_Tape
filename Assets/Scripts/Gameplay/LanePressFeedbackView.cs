using UnityEngine;

namespace IdiotTape.Gameplay
{

    public sealed class LanePressFeedbackView : MonoBehaviour
    {

        private const int SegmentCount = 8;
        private const float AttackDuration = 0.04f;
        private const float ReleaseDuration = 0.12f;

        private static readonly Color PressColor = new(0.66f, 0.86f, 1f, 1f);

        private Mesh laneMesh;
        private MeshRenderer laneRenderer;
        private LineRenderer lineRenderer;
        private MaterialPropertyBlock propertyBlock;
        private bool isPressed;
        private float intensity;

        public void Initialize(
            Material material,
            int laneIndex,
            int laneCount,
            float halfWidth,
            float bottomY,
            float judgementBaseY,
            float judgementCurvature)
        {

            laneMesh = CreateLaneMesh(
                laneIndex,
                laneCount,
                halfWidth,
                bottomY,
                judgementBaseY,
                judgementCurvature);
            MeshFilter meshFilter = gameObject.AddComponent<MeshFilter>();
            meshFilter.sharedMesh = laneMesh;
            laneRenderer = gameObject.AddComponent<MeshRenderer>();
            laneRenderer.sharedMaterial = material;
            laneRenderer.sortingOrder = 1;
            propertyBlock = new MaterialPropertyBlock();

            lineRenderer = gameObject.AddComponent<LineRenderer>();
            lineRenderer.positionCount = SegmentCount + 1;
            lineRenderer.useWorldSpace = false;
            lineRenderer.numCapVertices = 2;
            lineRenderer.textureMode = LineTextureMode.Stretch;
            lineRenderer.sharedMaterial = material;
            lineRenderer.sortingOrder = 4;

            float laneStart = laneIndex / (float)laneCount;
            float laneEnd = (laneIndex + 1f) / laneCount;

            for (int index = 0; index <= SegmentCount; index++)
            {

                float normalizedX = Mathf.Lerp(laneStart, laneEnd, index / (float)SegmentCount);
                float worldX = PlayfieldGeometry.GetWorldX(normalizedX, halfWidth);
                float worldY = PlayfieldGeometry.GetJudgementLineY(
                    normalizedX,
                    judgementBaseY,
                    judgementCurvature);
                lineRenderer.SetPosition(index, new Vector3(worldX, worldY, 0f));

            }

            ApplyPresentation();

        }

        public void SetPressed(bool pressed)
        {

            isPressed = pressed;

        }

        private void Update()
        {

            float targetIntensity = isPressed ? 1f : 0f;
            float duration = isPressed ? AttackDuration : ReleaseDuration;
            intensity = Mathf.MoveTowards(
                intensity,
                targetIntensity,
                Time.unscaledDeltaTime / duration);
            ApplyPresentation();

        }

        private void ApplyPresentation()
        {

            bool isVisible = intensity > 0f;
            laneRenderer.enabled = isVisible;
            lineRenderer.enabled = isVisible;

            if (!isVisible)
            {

                return;

            }

            float easedIntensity = Mathf.SmoothStep(0f, 1f, intensity);
            Color fillColor = PressColor;
            fillColor.a = easedIntensity * 0.32f;
            propertyBlock.SetColor("_Color", fillColor);
            laneRenderer.SetPropertyBlock(propertyBlock);

            Color lineColor = Color.Lerp(PressColor, Color.white, 0.42f);
            lineColor.a = easedIntensity * 0.92f;
            lineRenderer.startColor = lineColor;
            lineRenderer.endColor = lineColor;
            lineRenderer.startWidth = Mathf.Lerp(0.025f, 0.075f, easedIntensity);
            lineRenderer.endWidth = lineRenderer.startWidth;

        }

        private static Mesh CreateLaneMesh(
            int laneIndex,
            int laneCount,
            float halfWidth,
            float bottomY,
            float judgementBaseY,
            float judgementCurvature)
        {

            int columnCount = SegmentCount + 1;
            Vector3[] vertices = new Vector3[columnCount * 2];
            Vector2[] textureCoordinates = new Vector2[vertices.Length];
            Color[] colors = new Color[vertices.Length];
            int[] triangles = new int[SegmentCount * 6];
            float laneStart = laneIndex / (float)laneCount;
            float laneEnd = (laneIndex + 1f) / laneCount;

            for (int column = 0; column < columnCount; column++)
            {

                float columnProgress = column / (float)SegmentCount;
                float normalizedX = Mathf.Lerp(laneStart, laneEnd, columnProgress);
                float worldX = PlayfieldGeometry.GetWorldX(normalizedX, halfWidth);
                float topY = PlayfieldGeometry.GetJudgementLineY(
                    normalizedX,
                    judgementBaseY,
                    judgementCurvature);
                int bottomVertex = column * 2;
                int topVertex = bottomVertex + 1;
                vertices[bottomVertex] = new Vector3(worldX, bottomY, 0f);
                vertices[topVertex] = new Vector3(worldX, topY, 0f);
                textureCoordinates[bottomVertex] = new Vector2(columnProgress, 0f);
                textureCoordinates[topVertex] = new Vector2(columnProgress, 1f);
                colors[bottomVertex] = new Color(1f, 1f, 1f, 0.08f);
                colors[topVertex] = new Color(1f, 1f, 1f, 0.7f);

                if (column >= SegmentCount)
                {

                    continue;

                }

                int triangleIndex = column * 6;
                int nextBottomVertex = bottomVertex + 2;
                int nextTopVertex = bottomVertex + 3;
                triangles[triangleIndex] = bottomVertex;
                triangles[triangleIndex + 1] = topVertex;
                triangles[triangleIndex + 2] = nextTopVertex;
                triangles[triangleIndex + 3] = bottomVertex;
                triangles[triangleIndex + 4] = nextTopVertex;
                triangles[triangleIndex + 5] = nextBottomVertex;

            }

            Mesh mesh = new()
            {
                name = $"LanePressMesh_{laneIndex + 1:00}",
                hideFlags = HideFlags.HideAndDontSave,
                vertices = vertices,
                uv = textureCoordinates,
                colors = colors,
                triangles = triangles
            };
            mesh.RecalculateBounds();
            return mesh;

        }

        private void OnDestroy()
        {

            if (laneMesh != null)
            {

                Destroy(laneMesh);

            }

        }

    }

}
