using System.Collections.Generic;
using UnityEngine;

namespace IdiotTape.Gameplay
{

    public sealed class PlayfieldPresenter : MonoBehaviour
    {

        private const int LinePointCount = 65;
        private const float LineFlashDuration = 0.16f;
        private const int HitEffectPoolSize = 16;
        private const int LineReactionPoolSize = 8;

        private static readonly Color MainLineColor = new(0.92f, 0.91f, 0.88f, 0.9f);
        private static readonly Color EchoLineColor = new(0.75f, 0.72f, 0.7f, 0.28f);
        private static readonly Color BarGuideColor = new(0.88f, 0.86f, 0.82f, 0.34f);
        private static readonly Color BeatGuideColor = new(0.82f, 0.81f, 0.79f, 0.14f);

        [SerializeField] private Camera gameplayCamera;
        [SerializeField] private Transform noteRoot;
        [SerializeField] private Sprite noteSprite;
        [SerializeField] private float halfWidth = 8.2f;
        [SerializeField] private float spawnY = 4.45f;
        [SerializeField] private float judgementBaseY = -2.75f;
        [SerializeField] private float judgementCurvature = 0.18f;
        [SerializeField] private float noteScale = 0.86f;

        private LineRenderer mainLine;
        private LineRenderer echoLine;
        private Material lineMaterial;
        private Material additiveMaterial;
        private HitEffectView[] hitEffectPool;
        private JudgementLineReaction[] lineReactionPool;
        private LanePressFeedbackView[] laneFeedbackViews;
        private readonly List<RuntimeTimingGuideView> timingGuidePool = new();
        private int nextHitEffectIndex;
        private int nextLineReactionIndex;
        private float lineFlashRemaining;
        private Color lineFlashColor = Color.white;

        private void Awake()
        {

            lineMaterial = new Material(Shader.Find("Sprites/Default"))
            {
                name = "Runtime Judgement Line Material"
            };
            Shader additiveShader = Shader.Find("IdiotTape/GameplayAdditiveSprite");

            if (additiveShader == null)
            {

                Debug.LogError("Gameplay additive sprite shader could not be loaded.", this);
                additiveShader = Shader.Find("Sprites/Default");

            }

            additiveMaterial = new Material(additiveShader)
            {
                name = "Runtime Gameplay Additive Material"
            };
            mainLine = CreateSketchLine("JudgementLine_Main", MainLineColor, 0.025f, 0f);
            echoLine = CreateSketchLine("JudgementLine_Echo", EchoLineColor, 0.012f, -0.035f);
            CreateFeedbackPools();

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

        public RuntimeNoteView CreateNote(
            ChartNote note,
            Color color,
            int laneCount,
            float visualLeadTime,
            bool isPlayable)
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
                noteScale,
                additiveMaterial,
                isPlayable);
            return view;

        }

        public RuntimeTimingGuideView CreateTimingGuide(
            double beatTime,
            int bar,
            int beat,
            bool isBar,
            float visualLeadTime)
        {

            RuntimeTimingGuideView view = GetAvailableTimingGuide();
            view.gameObject.name = isBar
                ? $"BarGuide_{bar:000}"
                : $"BeatGuide_{bar:000}_{beat:00}";
            view.Initialize(
                beatTime,
                isBar,
                halfWidth,
                spawnY,
                judgementBaseY,
                judgementCurvature,
                visualLeadTime,
                lineMaterial,
                isBar ? BarGuideColor : BeatGuideColor,
                isBar ? 0.018f : 0.008f);
            return view;

        }

        private RuntimeTimingGuideView GetAvailableTimingGuide()
        {

            for (int index = 0; index < timingGuidePool.Count; index++)
            {

                if (!timingGuidePool[index].gameObject.activeSelf)
                {

                    return timingGuidePool[index];

                }

            }

            GameObject guideObject = new("TimingGuidePool");
            guideObject.transform.SetParent(transform, false);
            RuntimeTimingGuideView view = guideObject.AddComponent<RuntimeTimingGuideView>();
            timingGuidePool.Add(view);
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

        public void PlayHitFeedback(
            ChartNote note,
            int laneCount,
            Color partColor,
            JudgementGrade grade)
        {

            lineFlashRemaining = LineFlashDuration;
            lineFlashColor = Color.Lerp(partColor, Color.white, 0.62f);

            float normalizedX = PlayfieldGeometry.GetLaneCenterNormalized(note.LaneIndex, laneCount);
            float worldX = PlayfieldGeometry.GetWorldX(normalizedX, halfWidth);
            float worldY = PlayfieldGeometry.GetJudgementLineY(normalizedX, judgementBaseY, judgementCurvature);
            HitEffectView hitEffect = GetNextHitEffect();
            hitEffect.Play(
                note.Id,
                new Vector3(worldX, worldY, 0f),
                partColor,
                noteScale,
                grade);

            JudgementLineReaction lineReaction = GetNextLineReaction();
            lineReaction.Play(note.Id, normalizedX, partColor, grade);

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
            line.sharedMaterial = lineMaterial;
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

        public void ConfigureLaneFeedback(int laneCount)
        {

            if (laneFeedbackViews != null && laneFeedbackViews.Length == laneCount)
            {

                return;

            }

            if (laneFeedbackViews != null)
            {

                for (int index = 0; index < laneFeedbackViews.Length; index++)
                {

                    Destroy(laneFeedbackViews[index].gameObject);

                }

            }

            laneFeedbackViews = new LanePressFeedbackView[laneCount];
            float bottomY = gameplayCamera.ViewportToWorldPoint(new Vector3(0f, 0f, 0f)).y;

            for (int laneIndex = 0; laneIndex < laneCount; laneIndex++)
            {

                GameObject feedbackObject = new($"LanePressFeedback_{laneIndex + 1:00}");
                feedbackObject.transform.SetParent(transform, false);
                LanePressFeedbackView feedbackView = feedbackObject.AddComponent<LanePressFeedbackView>();
                feedbackView.Initialize(
                    additiveMaterial,
                    laneIndex,
                    laneCount,
                    halfWidth,
                    bottomY,
                    judgementBaseY,
                    judgementCurvature);
                laneFeedbackViews[laneIndex] = feedbackView;

            }

        }

        public void SetLanePressed(int laneIndex, bool isPressed)
        {

            if (laneFeedbackViews == null || laneIndex < 0 || laneIndex >= laneFeedbackViews.Length)
            {

                return;

            }

            laneFeedbackViews[laneIndex].SetPressed(isPressed);

        }

        private void OnDestroy()
        {

            if (lineMaterial != null)
            {

                Destroy(lineMaterial);

            }

            if (additiveMaterial != null)
            {

                Destroy(additiveMaterial);

            }

        }

        private void CreateFeedbackPools()
        {

            hitEffectPool = new HitEffectView[HitEffectPoolSize];

            for (int index = 0; index < hitEffectPool.Length; index++)
            {

                GameObject effectObject = new($"HitEffectPool_{index:00}");
                effectObject.transform.SetParent(transform, false);
                HitEffectView effect = effectObject.AddComponent<HitEffectView>();
                effect.Initialize(additiveMaterial);
                effectObject.SetActive(false);
                hitEffectPool[index] = effect;

            }

            lineReactionPool = new JudgementLineReaction[LineReactionPoolSize];

            for (int index = 0; index < lineReactionPool.Length; index++)
            {

                GameObject reactionObject = new($"LineReactionPool_{index:00}");
                reactionObject.transform.SetParent(transform, false);
                JudgementLineReaction reaction = reactionObject.AddComponent<JudgementLineReaction>();
                reaction.Initialize(additiveMaterial, halfWidth, judgementBaseY, judgementCurvature);
                reactionObject.SetActive(false);
                lineReactionPool[index] = reaction;

            }

        }

        private HitEffectView GetNextHitEffect()
        {

            for (int offset = 0; offset < hitEffectPool.Length; offset++)
            {

                int index = (nextHitEffectIndex + offset) % hitEffectPool.Length;

                if (hitEffectPool[index].gameObject.activeSelf)
                {

                    continue;

                }

                nextHitEffectIndex = (index + 1) % hitEffectPool.Length;
                return hitEffectPool[index];

            }

            HitEffectView reusedEffect = hitEffectPool[nextHitEffectIndex];
            nextHitEffectIndex = (nextHitEffectIndex + 1) % hitEffectPool.Length;
            return reusedEffect;

        }

        private JudgementLineReaction GetNextLineReaction()
        {

            for (int offset = 0; offset < lineReactionPool.Length; offset++)
            {

                int index = (nextLineReactionIndex + offset) % lineReactionPool.Length;

                if (lineReactionPool[index].gameObject.activeSelf)
                {

                    continue;

                }

                nextLineReactionIndex = (index + 1) % lineReactionPool.Length;
                return lineReactionPool[index];

            }

            JudgementLineReaction reusedReaction = lineReactionPool[nextLineReactionIndex];
            nextLineReactionIndex = (nextLineReactionIndex + 1) % lineReactionPool.Length;
            return reusedReaction;

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
