using System;
using System.Collections.Generic;
using IdiotTape.Gameplay;
using UnityEditor;
using UnityEngine;

namespace IdiotTape.EditorTools
{

    public sealed partial class PrototypeChartRecorderWindow
    {

        [SerializeField] private List<string> workspaceSelectedChartIds = new();
        [SerializeField] private List<string> workspaceSelectedBufferIds = new();
        [SerializeField] private PrototypeChart workspaceSelectionChart;
        private double workspaceBatchBeats;
        private double workspaceBatchMilliseconds;
        private int workspaceBatchLanes;
        private int workspaceBatchPart;
        private List<ChartNoteAuthoringData> workspaceSelectionPreview;
        private bool workspaceMarqueeActive;
        private bool workspaceMarqueeMoved;
        private bool workspaceMarqueeAdditive;
        private int workspaceMarqueeControl;
        private Vector2 workspaceMarqueeStart;
        private Vector2 workspaceMarqueeEnd;
        private readonly Dictionary<string, Rect> workspaceSelectionActionRects = new();
        private readonly HashSet<string> workspaceChartSelectionSet = new();
        private readonly HashSet<string> workspaceBufferSelectionSet = new();
        private readonly HashSet<string> workspaceExistingChartIds = new();
        private readonly HashSet<string> workspaceExistingBufferIds = new();

        private void ClearWorkspaceMultiSelection()
        {

            workspaceSelectedChartIds.Clear();
            workspaceSelectedBufferIds.Clear();
            workspaceChartSelectionSet.Clear();
            workspaceBufferSelectionSet.Clear();
            workspaceSelectionPreview = null;
            workspaceSelectionChart = chart;

        }

        private void PruneWorkspaceSelection()
        {

            if (workspaceSelectionChart != chart)
            {

                ClearWorkspaceMultiSelection();

            }

            workspaceExistingChartIds.Clear();
            workspaceExistingBufferIds.Clear();
            if (chart != null)
            {

                for (int index = 0; index < chart.Notes.Count; index++)
                {

                    workspaceExistingChartIds.Add(chart.Notes[index].Id);

                }

            }

            for (int index = 0; index < recordedNotes.Count; index++)
            {

                workspaceExistingBufferIds.Add(recordedNotes[index].editorId);

            }

            int removed = workspaceSelectedChartIds.RemoveAll(id => !workspaceExistingChartIds.Contains(id));
            removed += workspaceSelectedBufferIds.RemoveAll(id => !workspaceExistingBufferIds.Contains(id));
            RebuildWorkspaceSelectionLookup();
            if (removed > 0)
            {

                workspaceSelectionPreview = null;
                RefreshWorkspacePrimarySelection();

            }

        }

        private void RebuildWorkspaceSelectionLookup()
        {

            workspaceChartSelectionSet.Clear();
            workspaceBufferSelectionSet.Clear();
            foreach (string id in workspaceSelectedChartIds)
            {

                workspaceChartSelectionSet.Add(id);

            }

            foreach (string id in workspaceSelectedBufferIds)
            {

                workspaceBufferSelectionSet.Add(id);

            }

        }

        private bool IsWorkspaceChartSelected(string id)
        {

            return workspaceChartSelectionSet.Contains(id) ||
                (workspaceSelectedChartIds.Count + workspaceSelectedBufferIds.Count == 0 && selectedChartNoteId == id);

        }

        private bool IsWorkspaceBufferSelected(int index)
        {

            return (!string.IsNullOrEmpty(recordedNotes[index].editorId) &&
                workspaceBufferSelectionSet.Contains(recordedNotes[index].editorId)) ||
                (workspaceSelectedChartIds.Count + workspaceSelectedBufferIds.Count == 0 && selectedRecordedNoteIndex == index);

        }

        private int GetWorkspaceSelectionCount()
        {

            PruneWorkspaceSelection();
            int count = 0;

            if (chart != null)
            {

                for (int index = 0; index < chart.Notes.Count; index++)
                {

                    if (IsWorkspaceChartSelected(chart.Notes[index].Id))
                    {

                        count++;

                    }

                }

            }

            for (int index = 0; index < recordedNotes.Count; index++)
            {

                if (IsWorkspaceBufferSelected(index))
                {

                    count++;

                }

            }

            return count;

        }

        private void SetWorkspaceNoteSelection(string chartId, int bufferIndex, bool additive)
        {

            PruneWorkspaceSelection();

            if (!additive)
            {

                ClearWorkspaceMultiSelection();

            }
            else if (workspaceSelectedChartIds.Count + workspaceSelectedBufferIds.Count == 0)
            {

                if (!string.IsNullOrEmpty(selectedChartNoteId))
                {

                    workspaceSelectedChartIds.Add(selectedChartNoteId);

                }

                if (selectedRecordedNoteIndex >= 0 && selectedRecordedNoteIndex < recordedNotes.Count)
                {

                    workspaceSelectedBufferIds.Add(EnsureWorkspaceBufferId(recordedNotes[selectedRecordedNoteIndex]));

                }

            }

            workspaceSelectionPreview = null;

            if (bufferIndex >= 0 && bufferIndex < recordedNotes.Count)
            {

                string id = EnsureWorkspaceBufferId(recordedNotes[bufferIndex]);
                if (!additive || !workspaceSelectedBufferIds.Remove(id))
                {

                    workspaceSelectedBufferIds.Add(id);

                }

            }
            else if (!string.IsNullOrEmpty(chartId))
            {

                if (!additive || !workspaceSelectedChartIds.Remove(chartId))
                {

                    workspaceSelectedChartIds.Add(chartId);

                }

            }

            RefreshWorkspacePrimarySelection();
            if (recordingPhase == RecordingPhase.Idle)
            {

                OpenWorkspaceInspector(WorkspaceInspectorTab.Note);

            }

        }

        private static string EnsureWorkspaceBufferId(RecordedNote note)
        {

            // Older completed buffers acquire an editor identity only on an explicit selection.
            if (string.IsNullOrEmpty(note.editorId))
            {

                note.editorId = Guid.NewGuid().ToString("N");

            }

            return note.editorId;

        }

        private void RefreshWorkspacePrimarySelection()
        {

            RebuildWorkspaceSelectionLookup();
            selectedChartNoteId = workspaceSelectedChartIds.Count > 0 ? workspaceSelectedChartIds[^1] : string.Empty;
            selectedRecordedNoteIndex = -1;
            selectedAppliedNoteDataId = string.Empty;
            selectedAppliedNoteData = null;

            if (workspaceSelectedBufferIds.Count > 0)
            {

                string lastId = workspaceSelectedBufferIds[^1];
                selectedRecordedNoteIndex = recordedNotes.FindIndex(note => note.editorId == lastId);
                selectedChartNoteId = string.Empty;

            }
            else if (!string.IsNullOrEmpty(selectedChartNoteId))
            {

                ChartNote note = FindSelectedChartNote();
                if (note != null)
                {

                    LoadSelectedAppliedNoteData(note);

                }

            }

        }

        private void SelectAllWorkspacePartNotes()
        {

            ClearWorkspaceMultiSelection();
            string partId = GetWorkspaceSelectedPartId();

            for (int index = 0; index < chart.Notes.Count; index++)
            {

                if (chart.Notes[index].MusicalPartId == partId)
                {

                    workspaceSelectedChartIds.Add(chart.Notes[index].Id);

                }

            }

            for (int index = 0; index < recordedNotes.Count; index++)
            {

                if (recordedNotes[index].musicalPartId == partId)
                {

                    workspaceSelectedBufferIds.Add(EnsureWorkspaceBufferId(recordedNotes[index]));

                }

            }

            RefreshWorkspacePrimarySelection();
            OpenWorkspaceInspector(WorkspaceInspectorTab.Note);

        }

        private bool TransformWorkspaceSelection(double seconds, double quarterBeats, int lanes, string partId,
            bool previewOnly = false)
        {

            if (chart == null || recordingPhase != RecordingPhase.Idle)
            {

                return false;

            }

            if (seconds == 0d && quarterBeats == 0d && lanes == 0 && partId == null)
            {

                statusMessage = "이동량이나 변경할 파트를 지정하세요.";
                return false;

            }
            PruneWorkspaceSelection();
            List<ChartNoteAuthoringData> source = new();
            Dictionary<string, int> appliedIndices = new(StringComparer.Ordinal);
            List<RecordedNote> buffer = new();

            for (int index = 0; index < chart.Notes.Count; index++)
            {

                if (!IsWorkspaceChartSelected(chart.Notes[index].Id))
                {

                    continue;

                }

                appliedIndices[chart.Notes[index].Id] = source.Count;
                source.Add(ChartNoteAuthoringData.FromChartNote(chart.Notes[index]));

            }

            for (int index = 0; index < recordedNotes.Count; index++)
            {

                if (!IsWorkspaceBufferSelected(index))
                {

                    continue;

                }

                buffer.Add(recordedNotes[index]);
                source.Add(GetWorkspaceRecordedData(recordedNotes[index]));

            }

            if (partId != null && FindNotePartIndex(partId) < 0)
            {

                statusMessage = "일괄 수정할 음악 파트를 확인하세요.";
                workspaceSelectionPreview = null;
                return false;

            }

            if (!ChartSelectionTransform.TryTransform(source, chart.TempoSections, chart.LaneCount,
                seconds, quarterBeats, lanes, partId, out List<ChartNoteAuthoringData> transformed, out string error))
            {

                statusMessage = $"선택을 변경하지 않았습니다: {error}";
                workspaceSelectionPreview = null;
                return false;

            }

            if (previewOnly)
            {

                workspaceSelectionPreview = transformed;
                statusMessage = $"{source.Count}개 수정 미리보기 · 차트와 임시 기록은 그대로입니다.";
                Repaint();
                return true;

            }

            // Validate the complete candidate before touching either destination of a mixed selection.
            PrototypeChart candidate = Instantiate(chart);
            candidate.hideFlags = HideFlags.HideAndDontSave;

            try
            {

                List<ChartNoteAuthoringData> candidateNotes = new();
                for (int index = 0; index < chart.Notes.Count; index++)
                {

                    candidateNotes.Add(appliedIndices.TryGetValue(chart.Notes[index].Id, out int selectedIndex)
                        ? transformed[selectedIndex] : ChartNoteAuthoringData.FromChartNote(chart.Notes[index]));

                }

                // Buffer notes are validated on a clone with temporary IDs, without adding them to the asset.
                List<ChartNoteAuthoringData> validationNotes = new(candidateNotes);
                for (int index = appliedIndices.Count; index < transformed.Count; index++)
                {

                    validationNotes.Add(transformed[index].CloneWithOffset(0d, "selection-check-" + Guid.NewGuid().ToString("N")));

                }

                WriteWorkspaceSelectionNotes(candidate, validationNotes);
                if (!candidate.TryValidate(out error))
                {

                    statusMessage = $"선택을 변경하지 않았습니다: {error}";
                    return false;

                }

                Undo.IncrementCurrentGroup();
                int group = Undo.GetCurrentGroup();
                Undo.SetCurrentGroupName("선택 노트 일괄 수정");
                Undo.RecordObject(this, "선택 노트 일괄 수정");
                if (appliedIndices.Count > 0)
                {

                    Undo.RecordObject(chart, "선택 노트 일괄 수정");
                    WriteWorkspaceSelectionNotes(chart, candidateNotes);
                    EditorUtility.SetDirty(chart);

                }

                for (int index = 0; index < buffer.Count; index++)
                {

                    RecordedNote note = buffer[index];
                    ChartNoteAuthoringData data = transformed[appliedIndices.Count + index];
                    note.noteData = data;
                    note.hitTime = data.HitTime;
                    note.laneIndex = data.LaneIndex;
                    note.musicalPartId = data.MusicalPartId;
                    note.pendingAutomaticQuantization = false;
                    // Keep the actual recorded timestamp so the existing original-time command remains meaningful.

                }

                if (buffer.Count > 0)
                {

                    bufferWasApplied = false;

                }

                SortRecordedNotes();
                workspaceSelectionPreview = null;
                if (selectedRecordedNoteIndex < 0 && FindSelectedChartNote() != null)
                {

                    LoadSelectedAppliedNoteData(FindSelectedChartNote());

                }

                Undo.CollapseUndoOperations(group);
                statusMessage = $"{source.Count}개 수정 · 차트 {appliedIndices.Count} / 임시 {buffer.Count} · Undo 가능";
                Repaint();
                return true;

            }
            finally
            {

                DestroyImmediate(candidate);

            }

        }

        private static void WriteWorkspaceSelectionNotes(PrototypeChart destination, List<ChartNoteAuthoringData> notes)
        {

            notes.Sort((left, right) => left.HitTime != right.HitTime ? left.HitTime.CompareTo(right.HitTime)
                : string.CompareOrdinal(left.Id, right.Id));
            SerializedObject serialized = new(destination);
            SerializedProperty property = serialized.FindProperty("notes");
            property.arraySize = notes.Count;
            for (int index = 0; index < notes.Count; index++)
            {

                notes[index].WriteTo(property.GetArrayElementAtIndex(index));

            }

            serialized.ApplyModifiedPropertiesWithoutUndo();

        }

        private bool HandleWorkspaceEditingKeyboardEvent(Event current)
        {

            if (current == null || current.type != EventType.KeyDown || chart == null ||
                recordingPhase != RecordingPhase.Idle || EditorGUIUtility.editingTextField || tempoTapCapture)
            {

                return false;

            }

            if ((current.control || current.command) && current.keyCode == KeyCode.A)
            {

                SelectAllWorkspacePartNotes();
                current.Use();
                Repaint();
                return true;

            }

            if (current.control || current.command || GetWorkspaceSelectionCount() == 0)
            {

                return false;

            }

            bool vertical = timelineViewMode == TimelineViewMode.Vertical;
            int timeDirection = current.keyCode == (vertical ? KeyCode.UpArrow : KeyCode.LeftArrow) ? -1
                : current.keyCode == (vertical ? KeyCode.DownArrow : KeyCode.RightArrow) ? 1 : 0;
            int laneDirection = current.keyCode == (vertical ? KeyCode.LeftArrow : KeyCode.UpArrow) ? -1
                : current.keyCode == (vertical ? KeyCode.RightArrow : KeyCode.DownArrow) ? 1 : 0;

            if (timeDirection != 0 || laneDirection != 0)
            {

                if (timeDirection != 0 && !current.alt && chart.TempoSections.Count == 0)
                {

                    statusMessage = "박자 이동에는 템포 정보가 필요합니다. Alt+방향키로 ms 이동할 수 있습니다.";
                    current.Use();
                    return true;

                }
                double seconds = current.alt ? timeDirection * noteNudgeMilliseconds / 1000d : 0d;
                double beats = current.alt ? 0d : timeDirection * GetWorkspaceGridQuarterBeatStep(timeDirection);
                TransformWorkspaceSelection(seconds, beats, laneDirection, null);
                current.Use();
                return true;

            }

            if (current.keyCode == KeyCode.Delete && GetWorkspaceSelectionCount() > 1)
            {

                DeleteWorkspaceSelection(true);
                current.Use();
                return true;

            }

            return false;

        }

        private void DeleteWorkspaceSelection(bool askConfirmation)
        {

            int count = GetWorkspaceSelectionCount();
            if (count == 0 || recordingPhase != RecordingPhase.Idle)
            {

                return;

            }

            if (askConfirmation && workspaceSelectedChartIds.Count > 0 &&
                !EditorUtility.DisplayDialog("선택 노트 삭제", $"선택한 {count}개 노트를 삭제할까요? Undo로 되돌릴 수 있습니다.", "삭제", "취소"))
            {

                return;

            }

            Undo.IncrementCurrentGroup();
            int group = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("선택 노트 삭제");
            Undo.RecordObject(this, "선택 노트 삭제");
            List<ChartNoteAuthoringData> remaining = new();
            for (int index = 0; index < chart.Notes.Count; index++)
            {

                if (!IsWorkspaceChartSelected(chart.Notes[index].Id))
                {

                    remaining.Add(ChartNoteAuthoringData.FromChartNote(chart.Notes[index]));

                }

            }

            if (remaining.Count != chart.Notes.Count)
            {

                Undo.RecordObject(chart, "선택 노트 삭제");
                WriteWorkspaceSelectionNotes(chart, remaining);
                EditorUtility.SetDirty(chart);

            }
            for (int index = recordedNotes.Count - 1; index >= 0; index--)
            {

                if (IsWorkspaceBufferSelected(index)) {

                    recordedNotes.RemoveAt(index);
                    bufferWasApplied = false;

                }

            }

            ClearWorkspaceMultiSelection();
            RefreshWorkspacePrimarySelection();
            Undo.CollapseUndoOperations(group);
            statusMessage = $"{count}개 삭제 · Undo 가능";

        }

        private void DrawWorkspaceSelectionActions()
        {

            GUILayout.Label("박자·위치 이동", EditorStyles.miniBoldLabel);
            GUILayout.Label("시간 방향키: 스냅 한 칸 · Alt: 미세 이동\n위치 방향키: 한 칸 · Ctrl/⌘+A: 현재 파트 전체", EditorStyles.wordWrappedMiniLabel);
            using (new EditorGUILayout.HorizontalScope())
            {

                using (new EditorGUI.DisabledScope(chart.TempoSections.Count == 0))
                {

                    if (GUILayout.Button("한 칸 앞"))
                    {

                        TransformWorkspaceSelection(0d, -GetWorkspaceGridQuarterBeatStep(-1), 0, null);

                    }

                    CaptureWorkspaceActionRect("selectionForward");
                    if (GUILayout.Button("한 칸 뒤"))
                    {

                        TransformWorkspaceSelection(0d, GetWorkspaceGridQuarterBeatStep(1), 0, null);

                    }

                    CaptureWorkspaceActionRect("selectionBackward");

                }
                if (GUILayout.Button("위치 −"))
                {

                    TransformWorkspaceSelection(0d, 0d, -1, null);

                }

                if (GUILayout.Button("위치 +"))
                {

                    TransformWorkspaceSelection(0d, 0d, 1, null);

                }

            }

        }

        private double GetWorkspaceGridQuarterBeatStep(int direction)
        {

            if (chart == null || chart.TempoSections.Count == 0)
            {

                return 0d;

            }

            double firstTime = double.PositiveInfinity;
            for (int index = 0; index < chart.Notes.Count; index++)
            {

                if (IsWorkspaceChartSelected(chart.Notes[index].Id))
                {

                    firstTime = Math.Min(firstTime, chart.Notes[index].HitTime);

                }

            }

            for (int index = 0; index < recordedNotes.Count; index++)
            {

                if (IsWorkspaceBufferSelected(index))
                {

                    firstTime = Math.Min(firstTime, recordedNotes[index].hitTime);

                }

            }

            if (double.IsInfinity(firstTime))
            {

                return 0d;

            }

            ChartTempoSection section = ChartTempoMap.FindSectionForTime(chart.TempoSections,
                firstTime - (direction < 0 ? ScheduleToleranceSeconds : 0d));
            return 4d / section.BeatUnit / (int)quantizationGrid;

        }

        private void DrawWorkspaceBulkInspector()
        {

            GUILayout.Label($"{GetWorkspaceSelectionCount()}개 선택", EditorStyles.boldLabel);
            GUILayout.Label("차트 노트는 직접 수정하고, 임시 기록은 반영 전까지 별도로 유지합니다. 모든 변경은 한 번에 Undo할 수 있습니다.", EditorStyles.wordWrappedMiniLabel);
            GUILayout.Space(10f);
            DrawWorkspaceSelectionActions();
            GUILayout.Space(12f);
            EditorGUI.BeginChangeCheck();
            workspaceBatchBeats = EditorGUILayout.DoubleField("이동(4분음표 박)", workspaceBatchBeats);
            workspaceBatchMilliseconds = EditorGUILayout.DoubleField("추가 이동(ms)", workspaceBatchMilliseconds);
            workspaceBatchLanes = EditorGUILayout.IntField("위치 이동", workspaceBatchLanes);
            string[] parts = new string[chart.MusicalParts.Count + 1];
            parts[0] = "기존 파트 유지";
            for (int index = 0; index < chart.MusicalParts.Count; index++)
            {

                parts[index + 1] = chart.MusicalParts[index].DisplayName;

            }

            workspaceBatchPart = EditorGUILayout.Popup("음악 파트", Mathf.Clamp(workspaceBatchPart, 0, parts.Length - 1), parts);
            if (EditorGUI.EndChangeCheck())
            {

                workspaceSelectionPreview = null;

            }

            string partId = workspaceBatchPart == 0 ? null : chart.MusicalParts[workspaceBatchPart - 1].Id;
            using (new EditorGUILayout.HorizontalScope())
            {

                if (GUILayout.Button("미리보기"))
                {

                    TransformWorkspaceSelection(workspaceBatchMilliseconds / 1000d,
                        workspaceBatchBeats, workspaceBatchLanes, partId, true);

                }

                if (GUILayout.Button("선택에 적용"))
                {

                    if (TransformWorkspaceSelection(workspaceBatchMilliseconds / 1000d, workspaceBatchBeats,
                        workspaceBatchLanes, partId))
                    {

                        workspaceBatchBeats = 0d;
                        workspaceBatchMilliseconds = 0d;
                        workspaceBatchLanes = 0;
                        workspaceBatchPart = 0;

                    }

                }
                CaptureWorkspaceActionRect("bulkApply");

            }
            GUILayout.Label("묶음의 시간 간격과 경로를 유지합니다. 경계 밖으로 나가면 전체 변경을 취소합니다.", EditorStyles.wordWrappedMiniLabel);
            if (GUILayout.Button("선택 삭제"))
            {

                DeleteWorkspaceSelection(true);

            }

        }

        private void CaptureWorkspaceActionRect(string name)
        {

            if (Event.current.type != EventType.Repaint)
            {

                return;

            }

            Rect rect = GUILayoutUtility.GetLastRect();
            rect.position = GUIUtility.GUIToScreenPoint(rect.position) - position.position;
            workspaceSelectionActionRects[name] = rect;

        }

        private void DrawWorkspaceSelectionPreview(Rect plot, bool vertical, string partId)
        {

            if (Event.current.type != EventType.Repaint || workspaceSelectionPreview == null)
            {

                return;

            }

            foreach (ChartNoteAuthoringData note in workspaceSelectionPreview)
            {

                if (vertical || note.MusicalPartId == partId)
                {

                    DrawWorkspaceNote(plot, note, vertical, new Color(1f, 0.63f, 0.12f, 0.65f), true, true, false);

                }

            }

        }

        private bool HandleWorkspaceMarquee(Rect plot, bool vertical, string partId)
        {

            Event current = Event.current;
            int control = GUIUtility.GetControlID("WorkspaceMarquee".GetHashCode(), FocusType.Passive, plot);
            bool active = workspaceMarqueeActive && workspaceMarqueeControl == control;
            if (active && current.type == EventType.Repaint && workspaceMarqueeMoved)
            {

                Rect rectangle = WorkspaceSelectionRectangle(workspaceMarqueeStart, workspaceMarqueeEnd);
                EditorGUI.DrawRect(rectangle, new Color(0.15f, 0.65f, 0.85f, 0.18f));
                Handles.BeginGUI();
                Handles.color = WorkspaceAccent;
                Handles.DrawAAPolyLine(2f, new Vector3(rectangle.x, rectangle.y), new Vector3(rectangle.xMax, rectangle.y),
                    new Vector3(rectangle.xMax, rectangle.yMax), new Vector3(rectangle.x, rectangle.yMax), new Vector3(rectangle.x, rectangle.y));
                Handles.EndGUI();

            }
            if (active && current.type == EventType.MouseDrag)
            {

                workspaceMarqueeEnd = current.mousePosition;
                workspaceMarqueeMoved |= Vector2.Distance(workspaceMarqueeStart, workspaceMarqueeEnd) > 4f;
                current.Use();
                Repaint();
                return true;

            }
            if (active && current.type == EventType.MouseUp && current.button == 0)
            {

                workspaceMarqueeEnd = current.mousePosition;
                workspaceMarqueeActive = false;
                GUIUtility.hotControl = 0;
                if (workspaceMarqueeMoved)
                {

                    SelectWorkspaceRectangle(plot, vertical, partId,
                        WorkspaceSelectionRectangle(workspaceMarqueeStart, workspaceMarqueeEnd), workspaceMarqueeAdditive);

                }
                else if (!workspaceMarqueeAdditive)
                {

                    ClearWorkspaceMultiSelection();
                    RefreshWorkspacePrimarySelection();
                    float normalized = vertical ? Mathf.InverseLerp(plot.y, plot.yMax, current.mousePosition.y)
                        : Mathf.InverseLerp(plot.x, plot.xMax, current.mousePosition.x);
                    seekTime = ChartTempoMap.SnapSongTime(chart.TempoSections,
                        timelineStartTime + normalized * timelineVisibleDuration, (int)quantizationGrid);
                    timelineAutoScroll = false;
                    if (Application.isPlaying && songPlayback != null && songPlayback.IsPrepared)
                    {

                        Seek(seekTime);

                    }

                }
                current.Use();
                Repaint();
                return true;

            }
            if (current.type == EventType.MouseDown && current.button == 0 && plot.Contains(current.mousePosition))
            {

                if (TrySelectWorkspaceNote(plot, current.mousePosition, vertical, partId))
                {

                    GUI.FocusControl(null);
                    current.Use();
                    return true;

                }
                workspaceMarqueeActive = true;
                workspaceMarqueeMoved = false;
                workspaceMarqueeAdditive = current.shift;
                workspaceMarqueeControl = control;
                workspaceMarqueeStart = workspaceMarqueeEnd = current.mousePosition;
                GUIUtility.hotControl = control;
                GUI.FocusControl(null);
                current.Use();
                return true;

            }
            return active;

        }

        private static Rect WorkspaceSelectionRectangle(Vector2 start, Vector2 end)
        {

            return Rect.MinMaxRect(Math.Min(start.x, end.x), Math.Min(start.y, end.y),
                Math.Max(start.x, end.x), Math.Max(start.y, end.y));

        }

        private void SelectWorkspaceRectangle(Rect plot, bool vertical, string partId, Rect rectangle, bool additive)
        {

            if (!additive)
            {

                ClearWorkspaceMultiSelection();

            }
            else if (workspaceSelectedChartIds.Count + workspaceSelectedBufferIds.Count == 0)
            {

                if (!string.IsNullOrEmpty(selectedChartNoteId))
                {

                    workspaceSelectedChartIds.Add(selectedChartNoteId);

                }

                if (selectedRecordedNoteIndex >= 0 && selectedRecordedNoteIndex < recordedNotes.Count)
                {

                    workspaceSelectedBufferIds.Add(EnsureWorkspaceBufferId(recordedNotes[selectedRecordedNoteIndex]));

                }

            }
            for (int index = 0; index < chart.Notes.Count; index++)
            {

                ChartNote note = chart.Notes[index];
                if (note.MusicalPartId == partId && WorkspacePathIntersectsRectangle(plot, vertical,
                    ChartNoteAuthoringData.FromChartNote(note), rectangle) && !workspaceSelectedChartIds.Contains(note.Id))
                {

                    workspaceSelectedChartIds.Add(note.Id);

                }

            }
            for (int index = 0; index < recordedNotes.Count; index++)
            {

                RecordedNote note = recordedNotes[index];
                if (note.musicalPartId != partId || !WorkspacePathIntersectsRectangle(plot, vertical, GetWorkspaceRecordedData(note), rectangle))
                {

                    continue;

                }

                string id = EnsureWorkspaceBufferId(note);
                if (!workspaceSelectedBufferIds.Contains(id))
                {

                    workspaceSelectedBufferIds.Add(id);

                }

            }
            workspaceSelectionPreview = null;
            RefreshWorkspacePrimarySelection();
            OpenWorkspaceInspector(WorkspaceInspectorTab.Note);

        }

        private bool WorkspacePathIntersectsRectangle(Rect plot, bool vertical, ChartNoteAuthoringData data, Rect rectangle)
        {

            BuildWorkspaceNotePath(plot, data, vertical, workspaceNotePath);
            for (int index = 0; index < workspaceNotePath.Count; index++)
            {

                if (rectangle.Contains(workspaceNotePath[index]))
                {

                    return true;

                }

                if (index == 0)
                {

                    continue;

                }

                Vector2 a = workspaceNotePath[index - 1];
                Vector2 delta = (Vector2)workspaceNotePath[index] - a;
                float low = 0f;
                float high = 1f;
                bool intersects = true;
                for (int axis = 0; axis < 2; axis++)
                {

                    float minimum = axis == 0 ? rectangle.xMin : rectangle.yMin;
                    float maximum = axis == 0 ? rectangle.xMax : rectangle.yMax;
                    if (Mathf.Abs(delta[axis]) < 0.00001f)
                    {

                        intersects &= a[axis] >= minimum && a[axis] <= maximum;

                    }
                    else
                    {

                        float first = (minimum - a[axis]) / delta[axis];
                        float last = (maximum - a[axis]) / delta[axis];
                        low = Math.Max(low, Math.Min(first, last));
                        high = Math.Min(high, Math.Max(first, last));

                    }

                }
                if (intersects && low <= high)
                {

                    return true;

                }

            }
            return false;

        }

    }

}
