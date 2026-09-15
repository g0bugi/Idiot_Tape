using UnityEngine;
using UnityEngine.UI;

namespace IdiotTape.Gameplay
{

    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class GameplayMenuCurve : MaskableGraphic
    {

        protected override void Awake()
        {

            base.Awake();
            raycastTarget = false;

        }

        protected override void OnPopulateMesh(VertexHelper vertices)
        {

            vertices.Clear();
            Rect area = rectTransform.rect;
            const int segments = 48;

            for (int index = 0; index <= segments; index++)
            {

                float t = index / (float)segments;
                float x = Mathf.Lerp(area.xMin, area.xMax, t);
                float y = area.yMax - 4f * t * (1f - t) * area.height;
                vertices.AddVert(new Vector3(x, y - 0.8f), color, Vector2.zero);
                vertices.AddVert(new Vector3(x, y + 0.8f), color, Vector2.zero);

                if (index > 0)
                {

                    int current = index * 2;
                    vertices.AddTriangle(current - 2, current - 1, current);
                    vertices.AddTriangle(current, current - 1, current + 1);

                }

            }

        }

    }

}
