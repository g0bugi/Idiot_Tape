using System.Collections.Generic;
using UnityEngine;

namespace IdiotTape.Gameplay
{

    public sealed class RuntimeNoteView : MonoBehaviour
    {

        private const int BananaLinePointCount = 25;
        private const int SlideConnectorSegmentCount = 4;

        private readonly List<SpriteRenderer> spriteRenderers = new();
        private ChartNote note;
        private int laneCount;
        private float halfWidth;
        private float spawnY;
        private float judgementBaseY;
        private float judgementCurvature;
        private float visualLeadTime;
        private float baseScale;
        private Color noteColor;
        private Transform startMarker;
        private Transform endMarker;
        private LineRenderer bodyOutline;
        private LineRenderer bodyFill;
        private Material bodyMaterial;
        private float charge;
        private bool failed;

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
            float scale,
            Material additiveMaterial,
            bool isPlayable)
        {

            note = chartNote;
            laneCount = chartLaneCount;
            halfWidth = playfieldHalfWidth;
            spawnY = noteSpawnY;
            judgementBaseY = lineBaseY;
            judgementCurvature = lineCurvature;
            baseScale = scale;
            noteColor = color;
            SetVisualLeadTime(leadTime);

            startMarker = CreateMarkerRoot("Start");
            CreateMarker(startMarker, sprite, color, additiveMaterial, isPlayable, 1f);

            if (note.NoteType != ChartNoteType.Tap)
            {

                endMarker = CreateMarkerRoot("End");
                CreateMarker(endMarker, sprite, color, additiveMaterial, isPlayable, 0.82f);
                CreateBody(isPlayable);

            }

        }

        public void UpdatePresentation(double songTime)
        {

            SetStartMarkerPresentation(songTime);

            if (endMarker == null)
            {

                return;

            }

            SetMarkerPresentation(endMarker, note.EndTime, note.EndLaneIndex, songTime, 0.82f);
            UpdateBody(songTime);

        }

        private void SetStartMarkerPresentation(double songTime)
        {

            float normalizedX = PlayfieldGeometry.GetLaneCenterNormalized(note.LaneIndex, laneCount);
            transform.position = GetWorldPosition(note.HitTime, normalizedX, songTime);
            startMarker.localPosition = Vector3.zero;
            float progress = 1f - Mathf.Clamp01(
                (float)(note.HitTime - songTime) / visualLeadTime);
            float scale = Mathf.Lerp(baseScale * 0.88f, baseScale, progress);
            startMarker.localScale = Vector3.one * scale;

        }

        public void SetVisualLeadTime(float leadTime)
        {

            visualLeadTime = Mathf.Max(0.0001f, leadTime);

        }

        public void SetFailed()
        {

            failed = true;

            for (int index = 0; index < spriteRenderers.Count; index++)
            {

                Color color = spriteRenderers[index].color;
                float alpha = color.a;
                spriteRenderers[index].color = new Color(0.035f, 0.035f, 0.045f, alpha);

            }

            ApplyBodyColors();

        }

        public void SetCharge(float normalizedCharge)
        {

            charge = Mathf.Clamp01(normalizedCharge);
            ApplyBodyColors();

        }

        public void Remove()
        {

            Destroy(gameObject);

        }

        private Transform CreateMarkerRoot(string markerName)
        {

            GameObject markerObject = new(markerName);
            markerObject.transform.SetParent(transform, false);
            return markerObject.transform;

        }

        private void CreateMarker(
            Transform markerRoot,
            Sprite sprite,
            Color color,
            Material additiveMaterial,
            bool isPlayable,
            float markerScale)
        {

            Sprite circle = GameplayVisualAssets.CircleSprite != null
                ? GameplayVisualAssets.CircleSprite
                : sprite;
            Color outlineColor = isPlayable
                ? Color.white
                : new Color(0.24f, 0.24f, 0.27f, 0.58f);
            Color fillColor = isPlayable
                ? color
                : Color.Lerp(color, new Color(0.08f, 0.08f, 0.1f, color.a), 0.78f);
            fillColor.a = isPlayable ? color.a : 0.52f;
            CreateLayer(markerRoot, "Outline", circle, outlineColor, 10, markerScale, null);
            CreateLayer(markerRoot, "Fill", circle, fillColor, 11, markerScale * 0.84f, null);

            Color glowColor = color;
            glowColor.a = isPlayable ? 0.34f : 0.035f;
            CreateLayer(
                markerRoot,
                "Glow",
                GameplayVisualAssets.GlowSprite,
                glowColor,
                9,
                markerScale * 1.62f,
                additiveMaterial);

            if (note.NoteType == ChartNoteType.Flick ||
                note.SlideEndBehavior == SlideEndBehavior.Flick)
            {

                GameObject arrow = new("Direction");
                arrow.transform.SetParent(markerRoot, false);
                int flickStartLane = note.NoteType == ChartNoteType.Slide && note.SlideNodes.Count > 1
                    ? note.SlideNodes[^2].LaneIndex
                    : note.LaneIndex;
                float direction = note.EndLaneIndex > flickStartLane ? 1f : -1f;
                arrow.transform.localPosition = new Vector3(direction * 0.72f, 0f, 0f);
                arrow.transform.localRotation = Quaternion.Euler(0f, 0f, direction > 0f ? -90f : 90f);
                SpriteRenderer arrowRenderer = arrow.AddComponent<SpriteRenderer>();
                arrowRenderer.sprite = circle;
                arrowRenderer.color = new Color(1f, 1f, 1f, 0.72f);
                arrowRenderer.sortingOrder = 12;
                arrow.transform.localScale = new Vector3(0.12f, 0.42f, 1f);
                spriteRenderers.Add(arrowRenderer);

            }

        }

        private void CreateBody(bool isPlayable)
        {

            bodyMaterial = new Material(Shader.Find("Sprites/Default"))
            {

                name = $"Runtime Note Body {note.Id}"

            };
            bodyOutline = CreateLine("BodyOutline", bodyMaterial, 0.34f, 6);
            bodyFill = CreateLine("BodyFill", bodyMaterial, 0.22f, 7);
            bodyOutline.numCapVertices = 4;
            bodyFill.numCapVertices = 4;

            if (!isPlayable)
            {

                failed = true;

            }

            ApplyBodyColors();

        }

        private void OnDestroy()
        {

            if (bodyMaterial != null)
            {

                Destroy(bodyMaterial);

            }

        }

        private LineRenderer CreateLine(
            string lineName,
            Material material,
            float width,
            int sortingOrder)
        {

            GameObject lineObject = new(lineName);
            lineObject.transform.SetParent(transform, false);
            LineRenderer line = lineObject.AddComponent<LineRenderer>();
            line.useWorldSpace = true;
            line.sharedMaterial = material;
            line.startWidth = width;
            line.endWidth = width;
            line.sortingOrder = sortingOrder;
            line.textureMode = LineTextureMode.Stretch;
            return line;

        }

        private void UpdateBody(double songTime)
        {

            if (note.NoteType == ChartNoteType.Slide)
            {

                UpdateSlideBody(songTime);
                return;

            }

            int pointCount = GetBodyPointCount();
            bodyOutline.positionCount = pointCount;
            bodyFill.positionCount = pointCount;

            for (int index = 0; index < pointCount; index++)
            {

                double pointTime = GetBodyPointTime(index, pointCount);
                float normalizedX = note.NoteType == ChartNoteType.Flick
                    ? PlayfieldGeometry.GetLaneCenterNormalized(
                        index == 0 ? note.LaneIndex : note.EndLaneIndex,
                        laneCount)
                    : GetBodyPointNormalizedX(pointTime);
                Vector3 position = GetWorldPosition(pointTime, normalizedX, songTime);
                bodyOutline.SetPosition(index, position);
                bodyFill.SetPosition(index, position);

            }

        }

        private void UpdateSlideBody(double songTime)
        {

            int renderPointCount = NotePathMath.GetSlideRenderPointCount(note);
            int pointCount = 1 + note.SlideNodes.Count * (SlideConnectorSegmentCount + 1);
            bodyOutline.positionCount = pointCount;
            bodyFill.positionCount = pointCount;
            float previousX = PlayfieldGeometry.GetLaneCenterNormalized(note.LaneIndex, laneCount);
            int outputIndex = 0;

            for (int index = 0; index < renderPointCount; index++)
            {

                NotePathMath.GetSlideRenderPoint(note, index, out double pointTime, out int pointLane);
                float normalizedX = PlayfieldGeometry.GetLaneCenterNormalized(pointLane, laneCount);
                bool isConnector = index > 0 && index % 2 == 0;
                int segmentCount = isConnector ? SlideConnectorSegmentCount : 1;

                for (int segmentIndex = 1; segmentIndex <= segmentCount; segmentIndex++)
                {

                    // A lane transition spans space at one chart time, including on a curved playfield.
                    float pointX = isConnector
                        ? Mathf.Lerp(previousX, normalizedX, (float)segmentIndex / segmentCount)
                        : normalizedX;
                    Vector3 position = GetWorldPosition(pointTime, pointX, songTime);
                    bodyOutline.SetPosition(outputIndex, position);
                    bodyFill.SetPosition(outputIndex, position);
                    outputIndex++;

                }

                previousX = normalizedX;

            }

        }

        private int GetBodyPointCount()
        {

            if (note.NoteType == ChartNoteType.Banana)
            {

                return BananaLinePointCount;

            }

            return 2;

        }

        private double GetBodyPointTime(int index, int pointCount)
        {

            if (note.NoteType == ChartNoteType.Flick)
            {

                return note.HitTime;

            }

            return note.HitTime + (note.EndTime - note.HitTime) * index / (pointCount - 1d);

        }

        private float GetBodyPointNormalizedX(double pointTime)
        {

            switch (note.NoteType)
            {

                case ChartNoteType.Banana:
                    return NotePathMath.GetBananaNormalizedX(note, pointTime, laneCount);
                default:
                    return PlayfieldGeometry.GetLaneCenterNormalized(note.LaneIndex, laneCount);

            }

        }

        private void SetMarkerPresentation(
            Transform marker,
            double pointTime,
            int laneIndex,
            double songTime,
            float markerScale)
        {

            float normalizedX = PlayfieldGeometry.GetLaneCenterNormalized(laneIndex, laneCount);
            marker.position = GetWorldPosition(pointTime, normalizedX, songTime);
            float progress = 1f - Mathf.Clamp01((float)(pointTime - songTime) / visualLeadTime);
            float scale = Mathf.Lerp(baseScale * 0.88f, baseScale, progress) * markerScale;
            marker.localScale = Vector3.one * scale;

        }

        private Vector3 GetWorldPosition(double pointTime, float normalizedX, double songTime)
        {

            float targetX = PlayfieldGeometry.GetWorldX(normalizedX, halfWidth);
            float targetY = PlayfieldGeometry.GetJudgementLineY(
                normalizedX,
                judgementBaseY,
                judgementCurvature);
            float timeUntilHit = (float)(pointTime - songTime);
            float progress = 1f - Mathf.Clamp01(timeUntilHit / visualLeadTime);
            float worldY = Mathf.Lerp(spawnY, targetY, progress);
            return new Vector3(targetX, worldY, 0f);

        }

        private void ApplyBodyColors()
        {

            if (bodyOutline == null || bodyFill == null)
            {

                return;

            }

            Color outlineColor = failed
                ? new Color(0.015f, 0.015f, 0.02f, 0.9f)
                : new Color(1f, 1f, 1f, 0.72f);
            Color fillColor = failed
                ? new Color(0.025f, 0.025f, 0.035f, 0.82f)
                : note.NoteType == ChartNoteType.Banana
                    ? Color.Lerp(
                        new Color(noteColor.r, noteColor.g, noteColor.b, 0.12f),
                        new Color(noteColor.r, noteColor.g, noteColor.b, 0.92f),
                        charge)
                    : new Color(noteColor.r, noteColor.g, noteColor.b, 0.74f);
            bodyOutline.startColor = outlineColor;
            bodyOutline.endColor = outlineColor;
            bodyFill.startColor = fillColor;
            bodyFill.endColor = fillColor;

        }

        private SpriteRenderer CreateLayer(
            Transform parent,
            string layerName,
            Sprite sprite,
            Color color,
            int sortingOrder,
            float scale,
            Material material)
        {

            GameObject layerObject = new(layerName);
            layerObject.transform.SetParent(parent, false);
            layerObject.transform.localScale = Vector3.one * scale;
            SpriteRenderer renderer = layerObject.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.color = color;
            renderer.sortingOrder = sortingOrder;

            if (material != null)
            {

                renderer.sharedMaterial = material;

            }

            spriteRenderers.Add(renderer);
            return renderer;

        }

    }

}
