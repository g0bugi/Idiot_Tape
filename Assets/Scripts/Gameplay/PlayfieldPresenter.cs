using UnityEngine;

namespace IdiotTape.Gameplay
{

    public sealed class PlayfieldPresenter : MonoBehaviour
    {

        private const int LinePointCount = 65;

        [SerializeField] private Camera gameplayCamera;
        [SerializeField] private Transform noteRoot;
        [SerializeField] private Sprite noteSprite;
        [SerializeField] private float halfWidth = 8.2f;
        [SerializeField] private float spawnY = 4.45f;
        [SerializeField] private float judgementBaseY = -2.75f;
        [SerializeField] private float judgementCurvature = 0.18f;
        [SerializeField] private float noteScale = 0.92f;

        private void Awake()
        {

            CreateSketchLine("JudgementLine_Main", new Color(0.92f, 0.91f, 0.88f, 0.9f), 0.025f, 0f);
            CreateSketchLine("JudgementLine_Echo", new Color(0.75f, 0.72f, 0.7f, 0.28f), 0.012f, -0.035f);

        }

        public RuntimeNoteView CreateNote(ChartNote note, Color color, int laneCount, float visualLeadTime)
        {

            GameObject noteObject = new($"Note_{note.Id}");
            noteObject.transform.SetParent(noteRoot, false);
            RuntimeNoteView view = noteObject.AddComponent<RuntimeNoteView>();
            view.Initialize(
                note,
                noteSprite,
                color,
                laneCount,
                halfWidth,
                spawnY,
                judgementBaseY,
                judgementCurvature,
                visualLeadTime,
                noteScale);
            return view;

        }

        public bool TryGetInputPosition(Vector2 screenPosition, out float normalizedX)
        {

            Vector3 screenPoint = new(screenPosition.x, screenPosition.y, -gameplayCamera.transform.position.z);
            Vector3 worldPosition = gameplayCamera.ScreenToWorldPoint(screenPoint);
            normalizedX = Mathf.InverseLerp(-halfWidth, halfWidth, worldPosition.x);

            if (worldPosition.x < -halfWidth || worldPosition.x > halfWidth)
            {

                return false;

            }

            float lineY = PlayfieldGeometry.GetJudgementLineY(normalizedX, judgementBaseY, judgementCurvature);
            return worldPosition.y <= lineY + 0.2f;

        }

        private void CreateSketchLine(string objectName, Color color, float width, float verticalOffset)
        {

            GameObject lineObject = new(objectName);
            lineObject.transform.SetParent(transform, false);
            LineRenderer line = lineObject.AddComponent<LineRenderer>();
            line.positionCount = LinePointCount;
            line.useWorldSpace = false;
            line.startWidth = width;
            line.endWidth = width * 0.85f;
            line.startColor = color;
            line.endColor = color;
            line.numCapVertices = 2;
            line.textureMode = LineTextureMode.Stretch;
            line.material = new Material(Shader.Find("Sprites/Default"));
            line.sortingOrder = 2;

            for (int index = 0; index < LinePointCount; index++)
            {

                float normalizedX = index / (LinePointCount - 1f);
                float worldX = PlayfieldGeometry.GetWorldX(normalizedX, halfWidth);
                float worldY = PlayfieldGeometry.GetJudgementLineY(
                    normalizedX,
                    judgementBaseY + verticalOffset,
                    judgementCurvature);
                line.SetPosition(index, new Vector3(worldX, worldY, 0f));

            }

        }

    }

}
