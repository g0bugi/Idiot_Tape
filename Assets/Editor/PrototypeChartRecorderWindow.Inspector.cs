using System;
using IdiotTape.Gameplay;
using UnityEditor;
using UnityEngine;

namespace IdiotTape.EditorTools
{

    public sealed partial class PrototypeChartRecorderWindow
    {

        [SerializeField] private bool workspaceShowNodeFields;
        [SerializeField] private bool workspaceShowCurveFields;
        [SerializeField] private bool workspaceShowCheckpointFields;
        [SerializeField] private int workspaceDraggedBananaHandle = -1;

        private void DrawWorkspaceInspector()
        {

            float previousLabelWidth = EditorGUIUtility.labelWidth;
            EditorGUIUtility.labelWidth = 116f;

            try
            {

                if (workspaceInspectorTab == WorkspaceInspectorTab.Tools || chart == null)
                {

                    DrawSceneControls();

                }

                if (chart == null)
                {

                    GUILayout.Label("상단에서 작업할 차트를 선택하세요.", EditorStyles.wordWrappedLabel);
                    return;

                }

                switch (workspaceInspectorTab)
                {

                    case WorkspaceInspectorTab.Note:
                        DrawWorkspaceNoteInspector();
                        break;
                    case WorkspaceInspectorTab.Part:
                        DrawWorkspacePartInspector();
                        break;
                    case WorkspaceInspectorTab.Recording:
                        DrawWorkspaceRecordingSettings();
                        break;
                    case WorkspaceInspectorTab.Tempo:
                        DrawTempoCalibration();
                        break;
                    case WorkspaceInspectorTab.Tools:
                        DrawWorkspaceTools();
                        break;

                }

            }
            finally
            {

                EditorGUIUtility.labelWidth = previousLabelWidth;

            }

        }

        private void DrawWorkspaceNoteInspector()
        {

            if (recordingPhase != RecordingPhase.Idle)
            {

                GUILayout.Label("녹화 중", EditorStyles.boldLabel);
                GUILayout.Label("녹화를 멈춘 뒤 노트의 시간과 경로를 수정하세요.", EditorStyles.wordWrappedLabel);
                return;

            }

            using (new EditorGUI.DisabledScope(recordingPhase != RecordingPhase.Idle))
            {

                if (selectedRecordedNoteIndex >= 0 && selectedRecordedNoteIndex < recordedNotes.Count)
                {

                    DrawSelectedRecordedInteractionDetails(recordedNotes[selectedRecordedNoteIndex]);
                    return;

                }

                if (FindSelectedChartNote() != null)
                {

                    DrawSelectedAppliedNoteDetails();
                    return;

                }

                GUILayout.Label("노트를 선택하세요", EditorStyles.boldLabel);
                GUILayout.Space(10f);
                GUILayout.Label("채보에서 노트를 클릭하면 시간, 위치와 경로를 여기서 수정할 수 있습니다.",
                    EditorStyles.wordWrappedLabel);
                GUILayout.Space(18f);
                GUILayout.Label("입력 방법", EditorStyles.miniBoldLabel);
                GUILayout.Label(GetRecordingModeInstructions(), EditorStyles.wordWrappedLabel);

            }

        }

        private void DrawWorkspaceNoteHeader(ChartNoteAuthoringData data, bool buffered)
        {

            using (new EditorGUILayout.HorizontalScope())
            {

                GUILayout.Label(GetNoteTypeName(data.NoteType), EditorStyles.boldLabel);
                GUILayout.FlexibleSpace();
                GUILayout.Label(buffered ? "임시 기록" : "차트 반영", EditorStyles.miniLabel);

            }

            GUILayout.Label(buffered ? "반영 전까지 임시 기록만 수정합니다." :
                    EditorUtility.IsDirty(chart) ? "직접 수정 · 저장 필요 · Undo 가능" : "직접 수정 · 현재 저장됨 · Undo 가능",
                EditorStyles.wordWrappedMiniLabel);
            GUILayout.Space(12f);

        }

        private void DrawWorkspaceNoteFields(ChartNoteAuthoringData data)
        {

            double nextTime = Math.Max(0d, EditorGUILayout.DoubleField(
                data.NoteType == ChartNoteType.Flick ? "판정 시간(초)" : "시작 시간(초)", data.HitTime));

            if (Math.Abs(nextTime - data.HitTime) > ScheduleToleranceSeconds)
            {

                data.ShiftTimes(nextTime - data.HitTime);

            }

            int previousLane = data.LaneIndex;
            data.LaneIndex = Mathf.Clamp(EditorGUILayout.IntField("시작 위치", data.LaneIndex + 1) - 1,
                0, chart.LaneCount - 1);

            if (data.NoteType == ChartNoteType.Flick && data.LaneIndex == data.EndLaneIndex)
            {

                data.LaneIndex = previousLane;

            }

            DrawAppliedPartField(data);

            if (data.NoteType == ChartNoteType.Hold || data.NoteType == ChartNoteType.Banana)
            {

                double nextEndTime = EditorGUILayout.DoubleField("끝 시간(초)", data.EndTime);

                if (nextEndTime != data.EndTime)
                {

                    data.EndTime = Math.Max(data.HitTime + DuplicateInputThresholdSeconds, nextEndTime);

                }

            }

            if (data.NoteType == ChartNoteType.Hold)
            {

                data.EndLaneIndex = data.LaneIndex;
                DrawHoldPathEditor(data);

            }
            else if (data.NoteType == ChartNoteType.Slide)
            {

                data.SlideEndBehavior = (SlideEndBehavior)EditorGUILayout.Popup("끝 동작",
                    (int)data.SlideEndBehavior, SlideEndBehaviorNames);
                DrawSlidePathEditor(data);
                workspaceShowNodeFields = EditorGUILayout.Foldout(workspaceShowNodeFields,
                    $"노드 세부 값 · {data.SlideNodes.Count + 1}개", true);

                if (workspaceShowNodeFields)
                {

                    DrawSlideNodeFields(data);

                }

            }
            else if (data.NoteType == ChartNoteType.Flick || data.NoteType == ChartNoteType.Banana)
            {

                int endLane = Mathf.Clamp(EditorGUILayout.IntField("도착 위치", data.EndLaneIndex + 1) - 1,
                    0, chart.LaneCount - 1);

                if (data.NoteType != ChartNoteType.Flick || endLane != data.LaneIndex)
                {

                    data.EndLaneIndex = endLane;

                }

                if (data.NoteType == ChartNoteType.Flick)
                {

                    DrawFlickPathEditor(data);

                }
                else
                {

                    DrawWorkspaceBananaEditor(data);
                    data.BananaMaximumBonusCombo = Mathf.Max(0,
                        EditorGUILayout.IntField("최대 보너스 콤보", data.BananaMaximumBonusCombo));
                    workspaceShowCurveFields = EditorGUILayout.Foldout(workspaceShowCurveFields,
                        $"곡선 핸들 · {data.BananaCurveHandles.Count}개", true);

                    if (workspaceShowCurveFields)
                    {

                        DrawBananaHandleFields(data);

                    }

                    workspaceShowCheckpointFields = EditorGUILayout.Foldout(workspaceShowCheckpointFields,
                        $"체크포인트 · {data.BananaCheckpoints.Count}개", true);

                    if (workspaceShowCheckpointFields)
                    {

                        DrawBananaCheckpointFields(data);

                    }

                }

            }

        }

        private void DrawWorkspaceNoteActions(bool buffered)
        {

            GUILayout.Space(12f);
            noteNudgeMilliseconds = Math.Max(1f,
                EditorGUILayout.FloatField("미세 이동(ms)", noteNudgeMilliseconds));

            using (new EditorGUILayout.HorizontalScope())
            {

                if (GUILayout.Button("앞으로"))
                {

                    NudgeWorkspaceSelection(-noteNudgeMilliseconds / 1000d, buffered);

                }

                if (GUILayout.Button("뒤로"))
                {

                    NudgeWorkspaceSelection(noteNudgeMilliseconds / 1000d, buffered);

                }

                if (GUILayout.Button("삭제"))
                {

                    if (buffered && selectedRecordedNoteIndex >= 0 && selectedRecordedNoteIndex < recordedNotes.Count)
                    {

                        Undo.RecordObject(this, "임시 노트 삭제");
                        recordedNotes.RemoveAt(selectedRecordedNoteIndex);
                        selectedRecordedNoteIndex = -1;
                        bufferWasApplied = false;

                    }
                    else if (!buffered)
                    {

                        DeleteSelectedChartNote(true);

                    }

                }

            }

            if (buffered)
            {

                using (new EditorGUI.DisabledScope(chart.TempoSections.Count == 0))
                {

                    if (GUILayout.Button("선택 노트 박자 보정"))
                    {

                        QuantizeRecordedNotes(true, false);

                    }

                }

            }

        }

        private void NudgeWorkspaceSelection(double delta, bool buffered)
        {

            if (buffered)
            {

                NudgeSelected(delta);

            }
            else if (selectedAppliedNoteData != null)
            {

                selectedAppliedNoteData.ShiftTimes(Math.Max(-selectedAppliedNoteData.HitTime, delta));
                ApplySelectedAppliedNoteData();

            }

        }

        private void DrawWorkspacePartInspector()
        {

            if (chart.MusicalParts.Count == 0)
            {

                GUILayout.Label("차트 Inspector에 음악 파트를 추가하세요.", EditorStyles.wordWrappedLabel);
                return;

            }

            MusicalPartDefinition part = chart.MusicalParts[selectedPartIndex];
            GUILayout.Label(part.DisplayName, EditorStyles.boldLabel);
            GUILayout.Space(10f);
            GUILayout.Label("게임 활성 구간", EditorStyles.miniBoldLabel);
            bool hasWindows = false;

            foreach (MusicalPartActivationWindow window in chart.ActivationWindows)
            {

                if (window.MusicalPartId != part.Id)
                {

                    continue;

                }

                hasWindows = true;
                GUILayout.Label($"{window.StartTime:0.000}–{window.EndTime:0.000}초", EditorStyles.miniLabel);

            }

            if (!hasWindows)
            {

                GUILayout.Label("설정된 구간 없음", EditorStyles.miniLabel);

            }

            GUILayout.Label("게임에서 이 파트를 판정하는 구간입니다. 소리 듣기와 채보 표시 설정은 별도입니다.",
                EditorStyles.wordWrappedMiniLabel);

            using (new EditorGUI.DisabledScope(loopEnd <= loopStart || recordingPhase != RecordingPhase.Idle))
            {

                if (GUILayout.Button("현재 루프를 활성 구간으로"))
                {

                    CreateActivationWindowFromLoop();

                }

                if (GUILayout.Button("이 파트의 활성 구간 지우기"))
                {

                    ClearSelectedPartActivationWindows();

                }

            }

            GUILayout.Space(12f);
            DrawStemControls();

        }

        private void DrawWorkspaceRecordingSettings()
        {

            GUILayout.Label("녹화 설정", EditorStyles.boldLabel);
            GUILayout.Label(GetRecordingModeInstructions(), EditorStyles.wordWrappedLabel);
            GUILayout.Space(12f);
            countInBars = Mathf.Max(1, EditorGUILayout.IntField("카운트인(마디)", countInBars));
            metronomeDuringRecording = EditorGUILayout.Toggle("녹화 중 메트로놈", metronomeDuringRecording);
            metronomeVolume = EditorGUILayout.Slider("메트로놈 음량", metronomeVolume, 0f, 1f);
            metronomeOutputOffsetMilliseconds = EditorGUILayout.Slider("출력 보정(ms)",
                metronomeOutputOffsetMilliseconds, -50f, 50f);
            GUILayout.Space(12f);
            GUILayout.Label("박자 보정", EditorStyles.boldLabel);
            DrawQuantizationSettings();
            GUILayout.Label("상단 스냅과 같은 격자를 사용합니다. 보정 버튼은 실제 노트 시간을 변경합니다.",
                EditorStyles.wordWrappedMiniLabel);

        }

        private void DrawWorkspaceTools()
        {

            GUILayout.Space(10f);
            GUILayout.Label("재생 상태", EditorStyles.boldLabel);
            GUILayout.Label(chart.SongEventPath, EditorStyles.wordWrappedMiniLabel);

            if (songPlayback != null)
            {

                EditorGUILayout.LabelField("곡 시간", $"{songPlayback.SongTime:0.000} / {songPlayback.DurationSeconds:0.000}초");
                EditorGUILayout.LabelField("오디오", songPlayback.IsPrepared ? "준비 완료" : "불러오는 중");

                using (new EditorGUI.DisabledScope(!Application.isPlaying || !songPlayback.IsPrepared))
                {

                    if (GUILayout.Button("처음으로"))
                    {

                        StopRecording();
                        StopTempoCalibrationPreview();
                        DisableGameplaySessionForAuthoring();
                        songPlayback.Restart();
                        ResetSongMetronomeScheduler(0d);

                    }

                }

            }

            GUILayout.Space(14f);
            GUILayout.Label("선택 파트 · 현재 루프 작업", EditorStyles.boldLabel);
            DrawPatternDuplicationControls();
            DrawAppliedNoteQuantizationControls();
            DrawAppliedNoteDeletionControls();
            GUILayout.Space(12f);

            if (GUILayout.Button("차트 검사"))
            {

                ValidateChart();

            }

            if (GUILayout.Button("중복·겹친 활성 구간 정리"))
            {

                int removed = ChartActivationWindowUtility.Normalize(chart);
                statusMessage = removed > 0 ? $"활성 구간 {removed}개를 정리했습니다. 저장하세요." : "정리할 활성 구간이 없습니다.";

            }

            GUILayout.Space(12f);
            showAdvancedNavigation = EditorGUILayout.Foldout(showAdvancedNavigation, "초 단위 탐색", true);

            if (showAdvancedNavigation)
            {

                seekTime = Math.Max(0d, EditorGUILayout.DoubleField("이동할 시간", seekTime));

                using (new EditorGUI.DisabledScope(songPlayback == null || !songPlayback.IsPrepared))
                {

                    if (GUILayout.Button("해당 시각으로 이동"))
                    {

                        Seek(seekTime);

                    }

                }

                loopStart = Math.Max(0d, EditorGUILayout.DoubleField("반복 시작", loopStart));
                loopEnd = Math.Max(0d, EditorGUILayout.DoubleField("반복 끝", loopEnd));

                if (GUILayout.Button("반복 구간을 마디 경계에 맞춤"))
                {

                    SnapLoopToBars();

                }

            }

        }

        private void DrawWorkspaceBufferDetails()
        {

            if (chart == null || chart.MusicalParts.Count == 0)
            {

                return;

            }

            using (new EditorGUILayout.HorizontalScope())
            {

                using (new EditorGUI.DisabledScope(recordedNotes.Count == 0 || chart.TempoSections.Count == 0))
                {

                    if (GUILayout.Button("전체 박자 보정", GUILayout.Width(110f)))
                    {

                        QuantizeRecordedNotes(false, false);

                    }

                }

                if (GUILayout.Button("원본 시간 복원", GUILayout.Width(110f)))
                {

                    RestoreOriginalRecordedTimes();

                }

                addMissingActivationWindows = GUILayout.Toggle(addMissingActivationWindows, "누락 활성 구간 추가");
                clearBufferAfterApply = GUILayout.Toggle(clearBufferAfterApply, "반영 후 비우기");

            }

            string[] partNames = GetPartNames();
            int removeIndex = -1;

            for (int index = 0; index < recordedNotes.Count; index++)
            {

                RecordedNote note = recordedNotes[index];

                using (new EditorGUILayout.HorizontalScope())
                {

                    bool wasSelected = selectedRecordedNoteIndex == index;

                    if (GUILayout.Toggle(wasSelected, (index + 1).ToString("000"),
                            EditorStyles.miniButton, GUILayout.Width(38f)) && !wasSelected)
                    {

                        selectedRecordedNoteIndex = index;
                        selectedChartNoteId = string.Empty;
                        OpenWorkspaceInspector(WorkspaceInspectorTab.Note);

                    }

                    GUILayout.Label(GetNoteTypeName(note.noteData?.NoteType ?? ChartNoteType.Tap), GUILayout.Width(55f));
                    double time = Math.Max(0d, EditorGUILayout.DoubleField(note.hitTime, GUILayout.Width(88f)));
                    int lane = Mathf.Clamp(EditorGUILayout.IntField(note.laneIndex + 1, GUILayout.Width(42f)) - 1,
                        0, chart.LaneCount - 1);
                    string partId = DrawNotePartPopup(note.musicalPartId, partNames);
                    double correction = note.hasOriginalHitTime ? (note.hitTime - note.originalHitTime) * 1000d : 0d;
                    GUILayout.Label($"{correction:+0.0;-0.0;0.0}ms", GUILayout.Width(64f));

                    if (Math.Abs(time - note.hitTime) > ScheduleToleranceSeconds || lane != note.laneIndex ||
                        partId != note.musicalPartId)
                    {

                        Undo.RecordObject(this, "임시 노트 수정");
                        SetRecordedNoteHitTime(note, time);
                        note.originalHitTime = time;
                        note.hasOriginalHitTime = true;
                        note.pendingAutomaticQuantization = false;
                        note.laneIndex = lane;
                        note.musicalPartId = partId;
                        SynchronizeRecordedNoteIdentity(note);
                        bufferWasApplied = false;

                    }

                    if (GUILayout.Button("삭제", GUILayout.Width(44f)))
                    {

                        removeIndex = index;

                    }

                }

            }

            if (removeIndex >= 0)
            {

                Undo.RecordObject(this, "임시 노트 삭제");
                recordedNotes.RemoveAt(removeIndex);
                selectedRecordedNoteIndex = -1;
                bufferWasApplied = false;

            }

        }

        private string DrawNotePartPopup(string partId, string[] partNames, string label = null)
        {

            int partIndex = FindNotePartIndex(partId);
            bool unresolved = partIndex < 0;
            string[] options = partNames;

            if (unresolved)
            {

                options = new string[partNames.Length + 1];
                options[0] = string.IsNullOrWhiteSpace(partId) ? "파트 미지정" : $"알 수 없는 파트 ({partId})";
                Array.Copy(partNames, 0, options, 1, partNames.Length);

            }

            int selection = unresolved ? 0 : partIndex;
            EditorGUI.BeginChangeCheck();

            using (new EditorGUI.DisabledScope(partNames.Length == 0))
            {

                selection = label == null
                    ? EditorGUILayout.Popup(selection, options)
                    : EditorGUILayout.Popup(label, selection, options);

            }

            bool changed = EditorGUI.EndChangeCheck();
            int nextPartIndex = unresolved ? selection - 1 : selection;

            // Rendering an unresolved reference must not assign the first chart part. Preserve its
            // original identity until the author explicitly selects a defined part, even if another field changes.
            return changed && nextPartIndex >= 0 && nextPartIndex < chart.MusicalParts.Count
                ? chart.MusicalParts[nextPartIndex].Id
                : partId;

        }

        private int FindNotePartIndex(string partId)
        {

            if (!string.IsNullOrWhiteSpace(partId))
            {

                for (int index = 0; index < chart.MusicalParts.Count; index++)
                {

                    if (chart.MusicalParts[index].Id == partId)
                    {

                        return index;

                    }

                }

            }

            return -1;

        }

        private void DrawWorkspaceBananaEditor(ChartNoteAuthoringData data)
        {

            GUILayout.Space(8f);
            GUILayout.Label("곡선 · 핸들을 드래그해 수정", EditorStyles.miniBoldLabel);
            Rect rect = GUILayoutUtility.GetRect(120f, 170f, GUILayout.ExpandWidth(true));
            EditorGUI.DrawRect(rect, WorkspaceCanvas);
            Rect plot = new(rect.x + 10f, rect.y + 10f, rect.width - 20f, rect.height - 20f);
            Vector3[] path = new Vector3[49];

            for (int index = 0; index < path.Length; index++)
            {

                Vector2 point = EvaluateWorkspaceBananaPoint(data, (float)index / (path.Length - 1), chart.LaneCount);
                path[index] = new Vector3(Mathf.Lerp(plot.x, plot.xMax, point.x),
                    Mathf.Lerp(plot.yMax, plot.y, point.y));

            }

            Color previousColor = Handles.color;
            Handles.color = chart.GetPartColor(data.MusicalPartId);
            Handles.DrawAAPolyLine(3f, path);

            foreach (ChartNoteAuthoringData.CheckpointData checkpoint in data.BananaCheckpoints)
            {

                float time = (float)((checkpoint.Time - data.HitTime) / (data.EndTime - data.HitTime));
                Vector3 point = new(Mathf.Lerp(plot.x, plot.xMax, time),
                    Mathf.Lerp(plot.yMax, plot.y, checkpoint.NormalizedX));
                Handles.DrawWireDisc(point, Vector3.forward, 2.5f);

            }

            Event current = Event.current;

            for (int index = 0; index < data.BananaCurveHandles.Count; index++)
            {

                ChartNoteAuthoringData.CurveHandleData handle = data.BananaCurveHandles[index];
                Vector3 point = new(Mathf.Lerp(plot.x, plot.xMax, handle.NormalizedTime),
                    Mathf.Lerp(plot.yMax, plot.y, handle.NormalizedX));
                Handles.color = WorkspaceMuted;
                Handles.DrawDottedLine(index == 0 ? path[0] : path[^1], point, 3f);
                EditorGUI.DrawRect(new Rect(point.x - 4f, point.y - 4f, 8f, 8f), WorkspaceAccent);

                if (current.type == EventType.MouseDown && current.button == 0 &&
                    Vector2.Distance(current.mousePosition, point) < 10f)
                {

                    workspaceDraggedBananaHandle = index;
                    current.Use();

                }

            }

            if (current.type == EventType.MouseDrag && workspaceDraggedBananaHandle >= 0 &&
                workspaceDraggedBananaHandle < data.BananaCurveHandles.Count)
            {

                ChartNoteAuthoringData.CurveHandleData handle = data.BananaCurveHandles[workspaceDraggedBananaHandle];
                float lowerTime = workspaceDraggedBananaHandle > 0
                    ? data.BananaCurveHandles[workspaceDraggedBananaHandle - 1].NormalizedTime + 0.01f : 0.01f;
                float upperTime = workspaceDraggedBananaHandle + 1 < data.BananaCurveHandles.Count
                    ? data.BananaCurveHandles[workspaceDraggedBananaHandle + 1].NormalizedTime - 0.01f : 0.99f;
                handle.NormalizedTime = Mathf.Clamp(Mathf.InverseLerp(plot.x, plot.xMax, current.mousePosition.x),
                    lowerTime, Mathf.Max(lowerTime, upperTime));
                handle.NormalizedX = Mathf.InverseLerp(plot.yMax, plot.y, current.mousePosition.y);
                GUI.changed = true;
                current.Use();

            }

            if (current.rawType == EventType.MouseUp)
            {

                workspaceDraggedBananaHandle = -1;

            }

            Handles.color = previousColor;
            GUILayout.Label("가로: 시작→끝 시간 · 세로: 입력 위치 · 작은 원: 체크포인트",
                EditorStyles.wordWrappedMiniLabel);

        }

    }

}
