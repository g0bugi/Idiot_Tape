using UnityEngine;

namespace IdiotTape.Gameplay
{

    public sealed class PlayfieldPresenter : MonoBehaviour
    {

        private const int LinePointCount = 65;
        private const float LineFlashDuration = 0.16f;

        private static readonly Color MainLineColor = new(0.92f, 0.91f, 0.88f, 0.9f);
        private static readonly Color EchoLineColor = new(0.75f, 0.72f, 0.7f, 0.28f);

        [SerializeField] private Camera gameplayCamera;
        [SerializeField] private Transform noteRoot;
        [SerializeField] private Sprite noteSprite;
        [SerializeField] private float halfWidth = 8.2f;
        [SerializeField] private float spawnY = 4.45f;
        [SerializeField] private float judgementBaseY = -2.75f;
        [SerializeField] private float judgementCurvature = 0.18f;
        [SerializeField] private float noteScale = 0.92f;

        private LineRenderer mainLine;
        private LineRenderer echoLine;
        private float lineFlashRemaining;
        private Color lineFlashColor = Color.white;

        private void Awake()
        {

            mainLine = CreateSketchLine("JudgementLine_Main", MainLineColor, 0.025f, 0f);
            echoLine = CreateSketchLine("JudgementLine_Echo", EchoLineColor, 0.012f, -0.035f);

        }

        private void Update()
        {

            if (lineFlashRemaining <= 0f)
            {

                return;

            }

            lineFlashRemaining = Mathf.Max(0f, lineFlashRemaining - Time.unscaledDeltaTime);
            float intensity = lineFlashRemaining / LineFlashDuration;
            Color mainColor = Color.Lerp(MainLineColor, lineFlashColor, intensity);
            Color echoColor = Color.Lerp(EchoLineColor, lineFlashColor, intensity * 0.65f);
            mainColor.a = Mathf.Lerp(MainLineColor.a, 1f, intensity);
            echoColor.a = Mathf.Lerp(EchoLineColor.a, 0.72f, intensity);
            ApplyLineAppearance(mainLine, mainColor, Mathf.Lerp(0.025f, 0.055f, intensity));
            ApplyLineAppearance(echoLine, echoColor, Mathf.Lerp(0.012f, 0.03f, intensity));

            if (lineFlashRemaining <= 0f)
            {

                ApplyLineAppearance(mainLine, MainLineColor, 0.025f);
                ApplyLineAppearance(echoLine, EchoLineColor, 0.012f);

            }

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

        public void PlayHitFeedback(ChartNote note, int laneCount, Color partColor)
        {

            lineFlashRemaining = LineFlashDuration;
            lineFlashColor = Color.Lerp(partColor, Color.white, 0.62f);

            float normalizedX = PlayfieldGeometry.GetLaneCenterNormalized(note.LaneIndex, laneCount);
            float worldX = PlayfieldGeometry.GetWorldX(normalizedX, halfWidth);
            float worldY = PlayfieldGeometry.GetJudgementLineY(normalizedX, judgementBaseY, judgementCurvature);
            GameObject burstObject = new($"HitBurst_{note.Id}");
            burstObject.transform.SetParent(transform, false);
            SketchHitBurst burst = burstObject.AddComponent<SketchHitBurst>();
            burst.Initialize(noteSprite, partColor, new Vector3(worldX, worldY, 0f), noteScale);

        }

        private LineRenderer CreateSketchLine(string objectName, Color color, float width, float verticalOffset)
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

            return line;

        }

        private static void ApplyLineAppearance(LineRenderer line, Color color, float width)
        {

            line.startColor = color;
            line.endColor = color;
            line.startWidth = width;
            line.endWidth = width * 0.85f;

        }

    }

}
