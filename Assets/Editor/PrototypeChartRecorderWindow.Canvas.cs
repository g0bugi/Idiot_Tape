using System;
using System.Collections.Generic;
using IdiotTape.Gameplay;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace IdiotTape.EditorTools
{

    public sealed partial class PrototypeChartRecorderWindow
    {

        private const float WorkspaceRulerWidth = 44f;
        private const float WorkspaceLaneHeaderHeight = 30f;
        private const float WorkspaceOverviewLabelWidth = 100f;
        private readonly List<Vector3> workspaceNotePath = new();
        private readonly Vector3[] workspaceStrokePoints = new Vector3[2];
        private Vector2 workspaceOverviewScroll;

        private void DrawWorkspaceTimeline(Rect canvasRect)
        {

            EditorGUI.DrawRect(canvasRect, WorkspaceCanvas);

            if (chart == null || chart.TempoSections.Count == 0)
            {

                GUI.Label(canvasRect, "템포를 설정하면 채보가 표시됩니다.", WorkspaceLabelStyle(true));
                return;

            }

            if (tempoTapCapture && Keyboard.current?.spaceKey.wasPressedThisFrame == true)
            {

                TryCaptureTempoDownbeat(InputState.currentTime);

            }

            RefreshPatternDuplicationPreview();
            timelineVisibleDuration = Mathf.Clamp(timelineVisibleDuration, 1f, 120f);
            timelineStartTime = Math.Max(0d, Math.Min(
                Math.Max(0d, GetAuthoringDuration() - timelineVisibleDuration), timelineStartTime));
            GUI.BeginGroup(canvasRect);
            Rect localRect = new(0f, 0f, canvasRect.width, canvasRect.height);

            if (timelineViewMode == TimelineViewMode.Vertical)
            {

                DrawWorkspaceNoteSheet(localRect);

            }
            else
            {

                DrawWorkspacePartOverview(localRect);

            }

            GUI.EndGroup();

        }

        private void DrawWorkspaceNoteSheet(Rect canvasRect)
        {

            Rect plotRect = new(WorkspaceRulerWidth, WorkspaceLaneHeaderHeight,
                Math.Max(1f, canvasRect.width - WorkspaceRulerWidth - 12f),
                Math.Max(1f, canvasRect.height - WorkspaceLaneHeaderHeight - 22f));
            GUIStyle centered = new(WorkspaceLabelStyle(true)) { alignment = TextAnchor.MiddleCenter };

            for (int lane = 0; lane < chart.LaneCount; lane++)
            {

                float laneWidth = plotRect.width / chart.LaneCount;
                GUI.Label(new Rect(plotRect.x + lane * laneWidth, 2f, laneWidth, 24f),
                    (lane + 1).ToString(), centered);

            }

            DrawWorkspaceBarLabels(plotRect, true);
            GUI.Label(new Rect(7f, canvasRect.height - 20f, 38f, 18f), "마디", WorkspaceLabelStyle(true));
            GUIStyle rightAligned = new(WorkspaceLabelStyle(true)) { alignment = TextAnchor.MiddleRight };
            GUI.Label(new Rect(plotRect.x, canvasRect.height - 20f, plotRect.width, 18f),
                $"시간 ↓   ·   차트 입력 위치 1–{chart.LaneCount}", rightAligned);
            GUI.BeginGroup(plotRect);
            Rect localPlot = new(0f, 0f, plotRect.width, plotRect.height);
            DrawWorkspaceSheetBackground(localPlot);
            DrawWorkspaceBeatGrid(localPlot, true);
            DrawWorkspaceLoop(localPlot, true);

            if (workspaceShowContext)
            {

                DrawWorkspaceNotes(localPlot, true, null, true);

            }

            string selectedPart = GetWorkspaceSelectedPartId();
            DrawWorkspaceNotes(localPlot, true, selectedPart, false);
            DrawWorkspaceGhostNotes(localPlot, true, selectedPart);
            DrawWorkspacePlayhead(localPlot, true);
            HandleWorkspaceTimelineInput(localPlot, true);
            GUI.EndGroup();

        }

        private void DrawWorkspaceSheetBackground(Rect plotRect)
        {

            float laneWidth = plotRect.width / Math.Max(1, chart.LaneCount);

            for (int lane = 0; lane < chart.LaneCount; lane++)
            {

                if (lane % 2 == 1)
                {

                    EditorGUI.DrawRect(new Rect(lane * laneWidth, 0f, laneWidth, plotRect.height),
                        WorkspaceWithAlpha(WorkspacePanel, 0.22f));

                }

                EditorGUI.DrawRect(new Rect(lane * laneWidth, 0f, 1f, plotRect.height),
                    WorkspaceWithAlpha(WorkspaceBorder, 0.45f));

            }

            string partId = GetWorkspaceSelectedPartId();
            Color partColor = GetWorkspaceCanvasPartColor(partId);

            for (int index = 0; index < chart.ActivationWindows.Count; index++)
            {

                MusicalPartActivationWindow window = chart.ActivationWindows[index];

                if (window.MusicalPartId != partId || !WorkspaceTimeRangeVisible(window.StartTime, window.EndTime))
                {

                    continue;

                }

                float start = TimeToVerticalTimelineY(plotRect, Math.Max(timelineStartTime, window.StartTime));
                float end = TimeToVerticalTimelineY(plotRect, Math.Min(
                    timelineStartTime + timelineVisibleDuration, window.EndTime));
                EditorGUI.DrawRect(new Rect(0f, start, plotRect.width, end - start),
                    WorkspaceWithAlpha(partColor, 0.045f));
                EditorGUI.DrawRect(new Rect(plotRect.width - 3f, start, 3f, end - start),
                    WorkspaceWithAlpha(partColor, 0.5f));

            }

        }

        private void DrawWorkspacePartOverview(Rect canvasRect)
        {

            float rowHeight = Math.Max(88f, chart.LaneCount * 10f + 24f);
            float usableHeight = Math.Max(1f, canvasRect.height - WorkspaceLaneHeaderHeight);
            float totalHeight = Math.Max(usableHeight, chart.MusicalParts.Count * rowHeight);
            bool scrollable = totalHeight > usableHeight;
            float contentWidth = Math.Max(1f, canvasRect.width - (scrollable ? 16f : 0f));
            Rect headerPlot = new(WorkspaceOverviewLabelWidth, 0f,
                Math.Max(1f, contentWidth - WorkspaceOverviewLabelWidth - 12f), usableHeight);
            DrawWorkspaceBarLabels(headerPlot, false);
            Rect viewport = new(0f, WorkspaceLaneHeaderHeight, canvasRect.width, usableHeight);
            workspaceOverviewScroll = GUI.BeginScrollView(viewport, workspaceOverviewScroll,
                new Rect(0f, 0f, contentWidth, totalHeight), false, false);

            for (int index = 0; index < chart.MusicalParts.Count; index++)
            {

                MusicalPartDefinition part = chart.MusicalParts[index];
                float rowY = index * rowHeight;
                Rect row = new(0f, rowY, contentWidth, rowHeight);
                bool selected = index == selectedPartIndex;
                EditorGUI.DrawRect(row, selected
                    ? WorkspaceWithAlpha(WorkspaceAccent, 0.1f)
                    : WorkspaceWithAlpha(WorkspacePanel, index % 2 == 0 ? 0.28f : 0.12f));
                EditorGUI.DrawRect(new Rect(9f, rowY + 15f, 4f, 22f), part.Color);
                GUIStyle partLabel = new(WorkspaceLabelStyle(!selected))
                {

                    wordWrap = true,
                    fontStyle = selected ? FontStyle.Bold : FontStyle.Normal

                };
                Rect label = new(20f, rowY + 10f, WorkspaceOverviewLabelWidth - 26f, 40f);
                GUI.Label(label, part.DisplayName, partLabel);

                if (Event.current.type == EventType.MouseDown && Event.current.button == 0 && label.Contains(Event.current.mousePosition))
                {

                    SelectWorkspacePart(index);
                    OpenWorkspaceInspector(WorkspaceInspectorTab.Part);
                    Event.current.Use();
                    Repaint();

                }

                GUI.Label(new Rect(20f, rowY + 53f, WorkspaceOverviewLabelWidth - 24f, 20f),
                    $"위치 1–{chart.LaneCount}", WorkspaceLabelStyle(true));
                Rect rowPlot = new(headerPlot.x, rowY, headerPlot.width, rowHeight);
                GUI.BeginGroup(rowPlot);
                Rect localPlot = new(0f, 0f, rowPlot.width, rowPlot.height);
                DrawWorkspaceOverviewActivation(localPlot, part);
                DrawWorkspaceBeatGrid(localPlot, false);
                DrawWorkspaceLoop(localPlot, false);
                DrawWorkspaceNotes(localPlot, false, part.Id, false, selected ? 1f : 0.55f);
                DrawWorkspaceGhostNotes(localPlot, false, part.Id);
                DrawWorkspacePlayhead(localPlot, false);
                HandleWorkspaceTimelineInput(localPlot, false, part.Id);
                GUI.EndGroup();
                EditorGUI.DrawRect(new Rect(0f, row.yMax - 1f, contentWidth, 1f), WorkspaceBorder);

            }

            GUI.EndScrollView();

        }

        private void DrawWorkspaceOverviewActivation(Rect plotRect, MusicalPartDefinition part)
        {

            for (int index = 0; index < chart.ActivationWindows.Count; index++)
            {

                MusicalPartActivationWindow window = chart.ActivationWindows[index];

                if (window.MusicalPartId != part.Id || !WorkspaceTimeRangeVisible(window.StartTime, window.EndTime))
                {

                    continue;

                }

                float start = TimeToTimelineX(plotRect, Math.Max(timelineStartTime, window.StartTime));
                float end = TimeToTimelineX(plotRect, Math.Min(timelineStartTime + timelineVisibleDuration, window.EndTime));
                EditorGUI.DrawRect(new Rect(start, 3f, end - start, plotRect.height - 6f),
                    WorkspaceWithAlpha(part.Color, 0.08f));
                EditorGUI.DrawRect(new Rect(start, plotRect.height - 4f, end - start, 2f),
                    WorkspaceWithAlpha(part.Color, 0.5f));

            }

        }

        private void DrawWorkspaceBarLabels(Rect plotRect, bool vertical)
        {

            double beatTime = ChartTempoMap.GetBeatTimeAtOrBefore(chart.TempoSections, timelineStartTime);
            float lastLabelPosition = float.NegativeInfinity;
            int guard = 0;

            while (beatTime <= timelineStartTime + timelineVisibleDuration && guard++ < 2048)
            {

                ChartBeatPosition beat = ChartTempoMap.GetBeatPosition(chart.TempoSections, beatTime);

                if (beat.Beat == 1 && beatTime >= timelineStartTime)
                {

                    float position = vertical ? TimeToVerticalTimelineY(plotRect, beatTime) : TimeToTimelineX(plotRect, beatTime);

                    if (position - lastLabelPosition >= (vertical ? 24f : 32f))
                    {

                        Rect label = vertical
                            ? new Rect(7f, position - 8f, WorkspaceRulerWidth - 8f, 18f)
                            : new Rect(position + 3f, 5f, 38f, 20f);
                        GUI.Label(label, beat.Bar.ToString(), WorkspaceLabelStyle(true));
                        lastLabelPosition = position;

                    }

                }

                beatTime = ChartTempoMap.GetBeatTimeAfter(chart.TempoSections, beatTime + ScheduleToleranceSeconds);

            }

        }

        private void DrawWorkspaceBeatGrid(Rect plotRect, bool vertical)
        {

            double beatTime = ChartTempoMap.GetBeatTimeAtOrBefore(chart.TempoSections, timelineStartTime);
            int guard = 0;

            while (beatTime <= timelineStartTime + timelineVisibleDuration && guard++ < 2048)
            {

                ChartBeatPosition beat = ChartTempoMap.GetBeatPosition(chart.TempoSections, beatTime);
                bool barStart = beat.Beat == 1;
                float position = vertical ? TimeToVerticalTimelineY(plotRect, beatTime) : TimeToTimelineX(plotRect, beatTime);
                Rect line = vertical
                    ? new Rect(plotRect.x, position, plotRect.width, barStart ? 1.5f : 1f)
                    : new Rect(position, plotRect.y, barStart ? 1.5f : 1f, plotRect.height);
                EditorGUI.DrawRect(line, WorkspaceWithAlpha(WorkspaceBorder, barStart ? 0.95f : 0.42f));
                beatTime = ChartTempoMap.GetBeatTimeAfter(chart.TempoSections, beatTime + ScheduleToleranceSeconds);

            }

        }

        private void DrawWorkspaceLoop(Rect plotRect, bool vertical)
        {

            if (loopEnd <= loopStart || !WorkspaceTimeRangeVisible(loopStart, loopEnd))
            {

                return;

            }

            double startTime = Math.Max(loopStart, timelineStartTime);
            double endTime = Math.Min(loopEnd, timelineStartTime + timelineVisibleDuration);
            float start = vertical ? TimeToVerticalTimelineY(plotRect, startTime) : TimeToTimelineX(plotRect, startTime);
            float end = vertical ? TimeToVerticalTimelineY(plotRect, endTime) : TimeToTimelineX(plotRect, endTime);
            Rect area = vertical ? new Rect(0f, start, plotRect.width, end - start)
                : new Rect(start, 0f, end - start, plotRect.height);
            EditorGUI.DrawRect(area, WorkspaceWithAlpha(WorkspaceAccent, 0.035f));
            EditorGUI.DrawRect(vertical ? new Rect(0f, start, 3f, end - start)
                : new Rect(start, 0f, end - start, 3f), WorkspaceWithAlpha(WorkspaceAccent, 0.8f));

        }

        private void DrawWorkspacePlayhead(Rect plotRect, bool vertical)
        {

            double time = songPlayback == null ? seekTime : songPlayback.SongTime;

            if (time < timelineStartTime || time > timelineStartTime + timelineVisibleDuration)
            {

                return;

            }

            Color playheadColor = new(1f, 0.4f, 0.48f, 0.95f);
            float position = vertical ? TimeToVerticalTimelineY(plotRect, time) : TimeToTimelineX(plotRect, time);
            EditorGUI.DrawRect(vertical ? new Rect(0f, position, plotRect.width, 1.5f)
                : new Rect(position, 0f, 1.5f, plotRect.height), playheadColor);

        }

        private void DrawWorkspaceNotes(Rect plotRect, bool vertical, string partId, bool context, float opacity = 1f)
        {

            if (Event.current.type != EventType.Repaint)
            {

                return;

            }

            ChartNoteAuthoringData selectedData = null;
            bool selectedIsTemporary = false;
            Color selectedColor = WorkspaceAccent;

            for (int index = 0; index < chart.Notes.Count; index++)
            {

                ChartNote note = chart.Notes[index];

                if (!WorkspaceNotePartVisible(note.MusicalPartId, partId, context) ||
                    !WorkspaceTimeRangeVisible(note.HitTime, note.EndTime))
                {

                    continue;

                }

                ChartNoteAuthoringData data = ChartNoteAuthoringData.FromChartNote(note);
                Color color = WorkspaceWithAlpha(GetWorkspaceCanvasPartColor(note.MusicalPartId), context ? 0.19f : opacity);

                if (note.Id == selectedChartNoteId && !context)
                {

                    selectedData = data;
                    selectedColor = color;
                    continue;

                }

                DrawWorkspaceNote(plotRect, data, vertical, color, false, false, context);

            }

            for (int index = 0; index < recordedNotes.Count; index++)
            {

                RecordedNote note = recordedNotes[index];
                ChartNoteAuthoringData data = GetWorkspaceRecordedData(note);

                if (!WorkspaceNotePartVisible(note.musicalPartId, partId, context) ||
                    !WorkspaceTimeRangeVisible(data.HitTime, GetWorkspaceNoteEndTime(data)))
                {

                    continue;

                }

                Color color = WorkspaceWithAlpha(GetWorkspaceCanvasPartColor(note.musicalPartId), context ? 0.19f : opacity);

                if (index == selectedRecordedNoteIndex && !context)
                {

                    selectedData = data;
                    selectedColor = color;
                    selectedIsTemporary = true;
                    continue;

                }

                DrawWorkspaceNote(plotRect, data, vertical, color, true, false, context);

            }

            if (selectedData != null)
            {

                DrawWorkspaceNote(plotRect, selectedData, vertical, selectedColor, selectedIsTemporary, true, false);

            }

        }

        private void DrawWorkspaceNote(Rect plotRect, ChartNoteAuthoringData data, bool vertical,
            Color color, bool temporary, bool selected, bool context)
        {

            BuildWorkspaceNotePath(plotRect, data, vertical, workspaceNotePath);
            float width = selected ? 4f : context ? 2f : 3f;

            for (int index = 1; index < workspaceNotePath.Count; index++)
            {

                DrawWorkspaceStroke(workspaceNotePath[index - 1], workspaceNotePath[index],
                    color, width, temporary, plotRect);

            }

            if (workspaceShowChecks && !context)
            {

                DrawWorkspaceNoteChecks(plotRect, data, vertical, color);

            }

            Vector2 start = WorkspaceNotePosition(plotRect, data.HitTime, data.LaneIndex, vertical);

            if (plotRect.Contains(start))
            {

                DrawWorkspaceStartMarker(start, data.NoteType, color, temporary, selected, context);

            }

            if (workspaceNotePath.Count > 1)
            {

                Vector2 end = workspaceNotePath[^1];

                if (data.NoteType == ChartNoteType.Flick ||
                    (data.NoteType == ChartNoteType.Slide && data.SlideEndBehavior == SlideEndBehavior.Flick))
                {

                    Vector2 before = workspaceNotePath[^2];
                    DrawWorkspaceArrow(end, end - before, color, plotRect);

                }
                else if (plotRect.Contains(end))
                {

                    DrawWorkspaceCircle(end, 4.5f, color, false, false);

                }

            }

            if (data.NoteType == ChartNoteType.Slide && !context)
            {

                for (int index = 0; index < data.SlideNodes.Count - 1; index++)
                {

                    ChartNoteAuthoringData.PathNodeData node = data.SlideNodes[index];
                    Vector2 point = WorkspaceNotePosition(plotRect, node.Time, node.LaneIndex, vertical);

                    if (plotRect.Contains(point))
                    {

                        DrawWorkspaceCircle(point, 3.5f, color, !temporary, false);

                    }

                }

            }

        }

        private void BuildWorkspaceNotePath(Rect plotRect, ChartNoteAuthoringData data, bool vertical, List<Vector3> points)
        {

            points.Clear();
            points.Add(WorkspaceNotePosition(plotRect, data.HitTime, data.LaneIndex, vertical));

            if (data.NoteType == ChartNoteType.Hold)
            {

                points.Add(WorkspaceNotePosition(plotRect, data.EndTime, data.LaneIndex, vertical));

            }
            else if (data.NoteType == ChartNoteType.Flick)
            {

                points.Add(WorkspaceNotePosition(plotRect, data.HitTime, data.EndLaneIndex, vertical));

            }
            else if (data.NoteType == ChartNoteType.Slide)
            {

                int previousLane = data.LaneIndex;

                for (int index = 0; index < data.SlideNodes.Count; index++)
                {

                    ChartNoteAuthoringData.PathNodeData node = data.SlideNodes[index];
                    points.Add(WorkspaceNotePosition(plotRect, node.Time, previousLane, vertical));
                    points.Add(WorkspaceNotePosition(plotRect, node.Time, node.LaneIndex, vertical));
                    previousLane = node.LaneIndex;

                }

            }
            else if (data.NoteType == ChartNoteType.Banana)
            {

                for (int index = 1; index <= 64; index++)
                {

                    Vector2 curve = EvaluateWorkspaceBananaPoint(data, index / 64f, chart.LaneCount);
                    double time = data.HitTime + curve.x * (data.EndTime - data.HitTime);
                    points.Add(WorkspaceNormalizedPosition(plotRect, time, curve.y, vertical));

                }

            }

        }

        private static Vector2 EvaluateWorkspaceBananaPoint(ChartNoteAuthoringData data, float parameter, int laneCount)
        {

            float t = Mathf.Clamp01(parameter);
            float u = 1f - t;
            Vector2 start = new(0f, PlayfieldGeometry.GetLaneCenterNormalized(data.LaneIndex, laneCount));
            Vector2 end = new(1f, PlayfieldGeometry.GetLaneCenterNormalized(data.EndLaneIndex, laneCount));

            if (data.BananaCurveHandles.Count == 0)
            {

                return Vector2.Lerp(start, end, t);

            }

            ChartNoteAuthoringData.CurveHandleData first = data.BananaCurveHandles[0];
            Vector2 handleA = new(first.NormalizedTime, first.NormalizedX);

            if (data.BananaCurveHandles.Count == 1)
            {

                return u * u * start + 2f * u * t * handleA + t * t * end;

            }

            ChartNoteAuthoringData.CurveHandleData second = data.BananaCurveHandles[1];
            Vector2 handleB = new(second.NormalizedTime, second.NormalizedX);
            return u * u * u * start + 3f * u * u * t * handleA +
                   3f * u * t * t * handleB + t * t * t * end;

        }

        private static float GetWorkspaceBananaNormalizedX(ChartNoteAuthoringData data, double songTime, int laneCount)
        {

            if (data.EndTime <= data.HitTime)
            {

                return PlayfieldGeometry.GetLaneCenterNormalized(data.LaneIndex, laneCount);

            }

            float normalizedTime = Mathf.Clamp01((float)((songTime - data.HitTime) / (data.EndTime - data.HitTime)));
            float lower = 0f;
            float upper = 1f;

            // Handle time is a Bezier coordinate, so chart time must be inverted before sampling position.
            for (int iteration = 0; iteration < 20; iteration++)
            {

                float parameter = (lower + upper) * 0.5f;

                if (EvaluateWorkspaceBananaPoint(data, parameter, laneCount).x < normalizedTime)
                {

                    lower = parameter;

                }
                else
                {

                    upper = parameter;

                }

            }

            return Mathf.Clamp01(EvaluateWorkspaceBananaPoint(data, (lower + upper) * 0.5f, laneCount).y);

        }

        private void DrawWorkspaceNoteChecks(Rect plotRect, ChartNoteAuthoringData data, bool vertical, Color color)
        {

            if (data.NoteType == ChartNoteType.Banana)
            {

                for (int index = 0; index < data.BananaCheckpoints.Count; index++)
                {

                    ChartNoteAuthoringData.CheckpointData checkpoint = data.BananaCheckpoints[index];
                    Vector2 point = WorkspaceNormalizedPosition(plotRect, checkpoint.Time, checkpoint.NormalizedX, vertical);

                    if (plotRect.Contains(point))
                    {

                        DrawWorkspaceCircle(point, 3f, WorkspaceText, true, false);

                    }

                }

                return;

            }

            if (data.NoteType != ChartNoteType.Hold && data.NoteType != ChartNoteType.Slide)
            {

                return;

            }

            double endTime = Math.Min(GetWorkspaceNoteEndTime(data), timelineStartTime + timelineVisibleDuration);
            double check = ChartTempoMap.GetSubdivisionTimeAfter(chart.TempoSections,
                Math.Max(data.HitTime, timelineStartTime - ScheduleToleranceSeconds), 4);
            int guard = 0;

            while (check < endTime - ScheduleToleranceSeconds && guard++ < 4096)
            {

                int lane = data.LaneIndex;

                if (data.NoteType == ChartNoteType.Slide)
                {

                    for (int index = 0; index < data.SlideNodes.Count && data.SlideNodes[index].Time <= check; index++)
                    {

                        lane = data.SlideNodes[index].LaneIndex;

                    }

                }

                double halfBeat = ChartTempoMap.SnapSongTime(chart.TempoSections, check, 2);
                bool reward = Math.Abs(halfBeat - check) <= ScheduleToleranceSeconds;
                Vector2 point = WorkspaceNotePosition(plotRect, check, lane, vertical);
                DrawWorkspaceCircle(point, reward ? 2.6f : 1.6f,
                    reward ? WorkspaceText : WorkspaceWithAlpha(color, 0.8f), reward, false);
                check = ChartTempoMap.GetSubdivisionTimeAfter(chart.TempoSections, check + ScheduleToleranceSeconds, 4);

            }

        }

        private void DrawWorkspaceGhostNotes(Rect plotRect, bool vertical, string partId)
        {

            if (Event.current.type != EventType.Repaint || patternDuplicationPreview == null || !patternDuplicationPreview.IsValid)
            {

                return;

            }

            for (int index = 0; index < patternDuplicationPreview.GeneratedNotes.Count; index++)
            {

                ChartPatternPreviewNote note = patternDuplicationPreview.GeneratedNotes[index];

                if (note.MusicalPartId != partId)
                {

                    continue;

                }

                ChartNoteAuthoringData data = ChartPatternDuplication.CloneAtMusicalBarOffset(
                    chart, ChartNoteAuthoringData.FromChartNote(note.SourceNote), note.BarOffset, string.Empty);

                if (!WorkspaceTimeRangeVisible(data.HitTime, GetWorkspaceNoteEndTime(data)))
                {

                    continue;

                }

                bool inactive = !chart.IsPartActive(note.MusicalPartId, note.HitTime) && !patternAddActivationWindow;
                Color color = inactive ? new Color(1f, 0.4f, 0.35f, 0.48f)
                    : WorkspaceWithAlpha(WorkspaceAccent, 0.42f);
                DrawWorkspaceNote(plotRect, data, vertical, color, true, false, false);

            }

        }

        private void DrawWorkspaceStartMarker(Vector2 point, ChartNoteType type, Color color,
            bool temporary, bool selected, bool context)
        {

            float radius = context ? 4f : 7f;

            if (selected)
            {

                DrawWorkspaceCircle(point, 11f, WorkspaceText, false, false);

            }

            DrawWorkspaceCircle(point, radius, color, !temporary, temporary);

            if (context)
            {

                return;

            }

            Color symbol = temporary ? color : WorkspaceCanvas;

            if (type == ChartNoteType.Hold)
            {

                EditorGUI.DrawRect(new Rect(point.x - 1.3f, point.y - 4f, 2.6f, 8f), symbol);

            }
            else if (type == ChartNoteType.Slide)
            {

                Handles.BeginGUI();
                Handles.color = symbol;
                Handles.DrawAAConvexPolygon(new Vector3(point.x, point.y - 3.5f),
                    new Vector3(point.x + 3.5f, point.y), new Vector3(point.x, point.y + 3.5f),
                    new Vector3(point.x - 3.5f, point.y));
                Handles.EndGUI();

            }
            else if (type == ChartNoteType.Banana)
            {

                DrawWorkspaceCircle(point, 2.5f, symbol, false, false);

            }
            else if (type == ChartNoteType.Flick)
            {

                EditorGUI.DrawRect(new Rect(point.x - 3.5f, point.y - 1f, 7f, 2f), symbol);

            }

        }

        private void DrawWorkspaceCircle(Vector2 center, float radius, Color color, bool filled, bool dashed)
        {

            if (Event.current.type != EventType.Repaint)
            {

                return;

            }

            Handles.BeginGUI();
            Handles.color = filled ? color : WorkspaceWithAlpha(WorkspaceCanvas, color.a);
            Handles.DrawSolidDisc(center, Vector3.forward, radius);
            Handles.color = color;
            const int segments = 24;

            for (int index = 0; index < segments; index++)
            {

                if (dashed && index % 4 >= 2)
                {

                    continue;

                }

                float start = index * Mathf.PI * 2f / segments;
                float end = (index + 1) * Mathf.PI * 2f / segments;
                workspaceStrokePoints[0] = center + new Vector2(Mathf.Cos(start), Mathf.Sin(start)) * radius;
                workspaceStrokePoints[1] = center + new Vector2(Mathf.Cos(end), Mathf.Sin(end)) * radius;
                Handles.DrawAAPolyLine(1.8f, workspaceStrokePoints);

            }

            Handles.EndGUI();

        }

        private void DrawWorkspaceArrow(Vector2 tip, Vector2 direction, Color color, Rect clip)
        {

            if (direction.sqrMagnitude < 0.001f)
            {

                return;

            }

            direction.Normalize();
            Vector2 normal = new(-direction.y, direction.x);
            DrawWorkspaceStroke(tip - direction * 9f + normal * 5f, tip, color, 2.5f, false, clip);
            DrawWorkspaceStroke(tip - direction * 9f - normal * 5f, tip, color, 2.5f, false, clip);

        }

        private void DrawWorkspaceStroke(Vector2 start, Vector2 end, Color color, float width, bool dashed, Rect clip)
        {

            if (Event.current.type != EventType.Repaint || !ClipWorkspaceSegment(clip, ref start, ref end))
            {

                return;

            }

            Handles.BeginGUI();
            Handles.color = color;
            float length = Vector2.Distance(start, end);

            if (!dashed || length < 6f)
            {

                workspaceStrokePoints[0] = start;
                workspaceStrokePoints[1] = end;
                Handles.DrawAAPolyLine(width, workspaceStrokePoints);

            }
            else
            {

                Vector2 direction = (end - start) / length;

                for (float offset = 0f; offset < length; offset += 10f)
                {

                    workspaceStrokePoints[0] = start + direction * offset;
                    workspaceStrokePoints[1] = start + direction * Math.Min(length, offset + 6f);
                    Handles.DrawAAPolyLine(width, workspaceStrokePoints);

                }

            }

            Handles.EndGUI();

        }

        private static bool ClipWorkspaceSegment(Rect rect, ref Vector2 start, ref Vector2 end)
        {

            Vector2 delta = end - start;
            float lower = 0f;
            float upper = 1f;

            if (!ClipWorkspaceBoundary(-delta.x, start.x - rect.xMin, ref lower, ref upper) ||
                !ClipWorkspaceBoundary(delta.x, rect.xMax - start.x, ref lower, ref upper) ||
                !ClipWorkspaceBoundary(-delta.y, start.y - rect.yMin, ref lower, ref upper) ||
                !ClipWorkspaceBoundary(delta.y, rect.yMax - start.y, ref lower, ref upper))
            {

                return false;

            }

            end = start + delta * upper;
            start += delta * lower;
            return true;

        }

        private static bool ClipWorkspaceBoundary(float direction, float distance, ref float lower, ref float upper)
        {

            if (Mathf.Abs(direction) < 0.00001f)
            {

                return distance >= 0f;

            }

            float ratio = distance / direction;

            if (direction < 0f)
            {

                lower = Math.Max(lower, ratio);

            }
            else
            {

                upper = Math.Min(upper, ratio);

            }

            return lower <= upper;

        }

        private void HandleWorkspaceTimelineInput(Rect plotRect, bool vertical, string partId = null)
        {

            Event currentEvent = Event.current;

            if (!plotRect.Contains(currentEvent.mousePosition))
            {

                return;

            }

            if (currentEvent.type == EventType.ScrollWheel)
            {

                HandleTimelineScroll(currentEvent, plotRect, vertical);
                return;

            }

            if (currentEvent.type != EventType.MouseDown || currentEvent.button != 0)
            {

                return;

            }

            string selectablePart = vertical ? GetWorkspaceSelectedPartId() : partId;

            if (!TrySelectWorkspaceNote(plotRect, currentEvent.mousePosition, vertical, selectablePart))
            {

                float normalized = vertical
                    ? Mathf.InverseLerp(plotRect.y, plotRect.yMax, currentEvent.mousePosition.y)
                    : Mathf.InverseLerp(plotRect.x, plotRect.xMax, currentEvent.mousePosition.x);
                seekTime = ChartTempoMap.SnapSongTime(chart.TempoSections,
                    timelineStartTime + normalized * timelineVisibleDuration, (int)quantizationGrid);
                timelineAutoScroll = false;

                if (Application.isPlaying && songPlayback != null && songPlayback.IsPrepared)
                {

                    Seek(seekTime);

                }

            }

            GUI.FocusControl(null);
            currentEvent.Use();
            Repaint();

        }

        private bool TrySelectWorkspaceNote(Rect plotRect, Vector2 mousePosition, bool vertical, string partId)
        {

            float closestDistance = 10f;
            int closestRecordedIndex = -1;
            ChartNote closestChartNote = null;

            for (int index = 0; index < chart.Notes.Count; index++)
            {

                ChartNote note = chart.Notes[index];

                if (note.MusicalPartId != partId || !WorkspaceTimeRangeVisible(note.HitTime, note.EndTime))
                {

                    continue;

                }

                float distance = GetWorkspaceNoteDistance(plotRect, mousePosition,
                    ChartNoteAuthoringData.FromChartNote(note), vertical);

                if (distance <= closestDistance)
                {

                    closestDistance = distance;
                    closestChartNote = note;

                }

            }

            for (int index = 0; index < recordedNotes.Count; index++)
            {

                RecordedNote note = recordedNotes[index];
                ChartNoteAuthoringData data = GetWorkspaceRecordedData(note);

                if (note.musicalPartId != partId || !WorkspaceTimeRangeVisible(data.HitTime, GetWorkspaceNoteEndTime(data)))
                {

                    continue;

                }

                float distance = GetWorkspaceNoteDistance(plotRect, mousePosition, data, vertical);

                if (distance <= closestDistance)
                {

                    closestDistance = distance;
                    closestRecordedIndex = index;
                    closestChartNote = null;

                }

            }

            if (closestRecordedIndex >= 0)
            {

                selectedRecordedNoteIndex = closestRecordedIndex;
                selectedChartNoteId = string.Empty;
                selectedAppliedNoteDataId = string.Empty;
                selectedAppliedNoteData = null;

            }
            else if (closestChartNote != null)
            {

                selectedChartNoteId = closestChartNote.Id;
                selectedRecordedNoteIndex = -1;
                LoadSelectedAppliedNoteData(closestChartNote);

            }
            else
            {

                return false;

            }

            selectedPartIndex = FindPartIndex(partId);
            OpenWorkspaceInspector(WorkspaceInspectorTab.Note);
            return true;

        }

        private float GetWorkspaceNoteDistance(Rect plotRect, Vector2 mousePosition,
            ChartNoteAuthoringData data, bool vertical)
        {

            BuildWorkspaceNotePath(plotRect, data, vertical, workspaceNotePath);
            float closest = Vector2.Distance(mousePosition, workspaceNotePath[0]);

            for (int index = 1; index < workspaceNotePath.Count; index++)
            {

                Vector2 start = workspaceNotePath[index - 1];
                Vector2 end = workspaceNotePath[index];
                Vector2 delta = end - start;
                float amount = delta.sqrMagnitude < 0.001f ? 0f
                    : Mathf.Clamp01(Vector2.Dot(mousePosition - start, delta) / delta.sqrMagnitude);
                closest = Math.Min(closest, Vector2.Distance(mousePosition, start + delta * amount));

            }

            return closest;

        }

        private bool WorkspaceNotePartVisible(string notePartId, string partId, bool context)
        {

            return context ? notePartId != GetWorkspaceSelectedPartId() : notePartId == partId;

        }

        private string GetWorkspaceSelectedPartId()
        {

            return chart.MusicalParts.Count == 0 ? string.Empty
                : chart.MusicalParts[Mathf.Clamp(selectedPartIndex, 0, chart.MusicalParts.Count - 1)].Id;

        }

        private bool WorkspaceTimeRangeVisible(double start, double end)
        {

            return end >= timelineStartTime && start <= timelineStartTime + timelineVisibleDuration;

        }

        private static ChartNoteAuthoringData GetWorkspaceRecordedData(RecordedNote note)
        {

            return note.noteData ?? ChartNoteAuthoringData.CreateTap(string.Empty,
                note.hitTime, note.laneIndex, note.musicalPartId);

        }

        private static double GetWorkspaceNoteEndTime(ChartNoteAuthoringData data)
        {

            return data.NoteType == ChartNoteType.Slide && data.SlideNodes.Count > 0
                ? data.SlideNodes[^1].Time
                : data.NoteType == ChartNoteType.Hold || data.NoteType == ChartNoteType.Banana
                    ? data.EndTime : data.HitTime;

        }

        private Vector2 WorkspaceNotePosition(Rect plotRect, double time, int lane, bool vertical)
        {

            return WorkspaceNormalizedPosition(plotRect, time,
                PlayfieldGeometry.GetLaneCenterNormalized(lane, chart.LaneCount), vertical);

        }

        private Vector2 WorkspaceNormalizedPosition(Rect plotRect, double time, float normalizedX, bool vertical)
        {

            return vertical
                ? new Vector2(Mathf.Lerp(plotRect.x, plotRect.xMax, normalizedX), TimeToVerticalTimelineY(plotRect, time))
                : new Vector2(TimeToTimelineX(plotRect, time), Mathf.Lerp(plotRect.y + 9f, plotRect.yMax - 9f, normalizedX));

        }

        private float TimeToVerticalTimelineY(Rect plotRect, double songTime)
        {

            return plotRect.y + (float)((songTime - timelineStartTime) / timelineVisibleDuration) * plotRect.height;

        }

        private float TimeToTimelineX(Rect plotRect, double songTime)
        {

            return plotRect.x + (float)((songTime - timelineStartTime) / timelineVisibleDuration) * plotRect.width;

        }

        private static Color WorkspaceWithAlpha(Color color, float alpha)
        {

            color.a = alpha;
            return color;

        }

        private Color GetWorkspaceCanvasPartColor(string partId)
        {

            Color color = chart.GetPartColor(partId);
            Color.RGBToHSV(color, out float hue, out float saturation, out float value);
            float backgroundLuminance = WorkspaceCanvas.grayscale;
            value = backgroundLuminance > 0.5f ? Math.Min(value, 0.62f) : Math.Max(value, 0.65f);
            return Color.HSVToRGB(hue, saturation, value);

        }

    }

}
