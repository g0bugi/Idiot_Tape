using System;
using IdiotTape.Gameplay;
using UnityEditor;
using UnityEngine;

namespace IdiotTape.EditorTools
{

    public sealed partial class PrototypeChartRecorderWindow
    {

        private enum WorkspaceInspectorTab
        {

            Note,
            Part,
            Recording,
            Tempo,
            Tools

        }

        [SerializeField] private WorkspaceInspectorTab workspaceInspectorTab;
        [SerializeField] private RecordingStartMode workspaceRecordingStart = RecordingStartMode.Loop;
        [SerializeField] private bool workspaceShowChecks;
        [SerializeField] private bool workspaceShowContext = true;
        [SerializeField] private bool workspaceShowBufferDetails;
        [SerializeField] private bool workspaceCompactInspector;
        [SerializeField] private bool workspaceInitialized;
        private Vector2 workspacePartsScroll;
        private Vector2 workspaceInspectorScroll;
        private Vector2 workspaceBufferScroll;
        private static GUIStyle workspaceLabel;
        private static GUIStyle workspaceMutedLabel;
        private static GUIStyle workspaceWrapLabel;
        private static readonly string[] WorkspaceStartNames = { "현재 위치부터", "루프부터", "처음부터" };
        private static readonly string[] WorkspaceInspectorNames = { "노트", "파트", "녹화", "박자", "도구" };
        private static readonly string[] WorkspaceViewNames = { "노트 편집", "파트 개요" };
        private static readonly string[] WorkspaceApplyNames = { "추가", "이 구간의 기록 파트 교체" };

        private static Color WorkspaceBackground => WorkspaceColor(0xf7f8fa, 0x191b21);
        private static Color WorkspacePanel => WorkspaceColor(0xeef0f3, 0x22252d);
        private static Color WorkspaceCanvas => WorkspaceColor(0xffffff, 0x17191e);
        private static Color WorkspaceText => WorkspaceColor(0x21252d, 0xe3e6ed);
        private static Color WorkspaceMuted => WorkspaceColor(0x586170, 0xabb2c0);
        private static Color WorkspaceBorder => WorkspaceColor(0xd5d9e0, 0x393e4a);
        private static Color WorkspaceAccent => WorkspaceColor(0x09697a, 0x77d4e3);

        internal readonly struct WorkspaceLayout
        {

            public readonly Rect Transport;
            public readonly Rect Loop;
            public readonly Rect Parts;
            public readonly Rect Main;
            public readonly Rect Inspector;
            public readonly Rect Buffer;
            public readonly Rect Status;
            public readonly bool Compact;

            public WorkspaceLayout(float width, float height, bool expandedBuffer)
            {

                Compact = width < 1000f;
                float transportHeight = Compact ? 78f : 46f;
                float loopHeight = 38f;
                float statusHeight = 26f;
                float bufferHeight = 62f + (expandedBuffer ? Mathf.Min(146f, height * 0.2f) : 0f);
                float bodyTop = transportHeight + loopHeight;
                float bodyHeight = Mathf.Max(0f, height - bodyTop - bufferHeight - statusHeight);
                float partWidth = Compact ? 138f : 150f;
                float inspectorWidth = Compact ? 0f : Mathf.Min(350f, Mathf.Max(320f, width * 0.25f));
                Transport = new Rect(0f, 0f, width, transportHeight);
                Loop = new Rect(0f, transportHeight, width, loopHeight);
                Parts = new Rect(0f, bodyTop, partWidth, bodyHeight);
                Main = new Rect(partWidth + 1f, bodyTop, width - partWidth - inspectorWidth - 2f, bodyHeight);
                Inspector = new Rect(width - inspectorWidth, bodyTop, inspectorWidth, bodyHeight);
                Buffer = new Rect(0f, bodyTop + bodyHeight, width, bufferHeight);
                Status = new Rect(0f, height - statusHeight, width, statusHeight);

            }

        }

        internal static WorkspaceLayout CalculateWorkspaceLayout(float width, float height, bool expandedBuffer)
        {

            return new WorkspaceLayout(width, height, expandedBuffer);

        }

        private void DrawAuthoringWorkspace()
        {

            if (!workspaceInitialized)
            {

                timelineViewMode = TimelineViewMode.Vertical;
                workspaceInitialized = true;

            }

            if (chart != null)
            {

                ClampSelections();
                RefreshSongPlayback();

            }

            WorkspaceLayout layout = CalculateWorkspaceLayout(position.width, position.height, workspaceShowBufferDetails);
            EditorGUI.DrawRect(new Rect(Vector2.zero, position.size), WorkspaceBackground);
            DrawWorkspaceTransport(layout.Transport, layout.Compact);
            DrawWorkspaceLoopBar(layout.Loop);
            DrawWorkspaceParts(layout.Parts);

            if (layout.Compact && workspaceCompactInspector)
            {

                DrawWorkspaceInspectorPane(layout.Main, true);

            }
            else
            {

                DrawWorkspaceMain(layout.Main, layout.Compact);

            }

            if (!layout.Compact)
            {

                DrawWorkspaceInspectorPane(layout.Inspector, false);

            }

            DrawWorkspaceBuffer(layout.Buffer);
            DrawWorkspaceStatus(layout.Status);

        }

        private void DrawWorkspaceTransport(Rect rect, bool compact)
        {

            EditorGUI.DrawRect(rect, WorkspaceBackground);
            DrawWorkspaceDivider(new Rect(rect.x, rect.yMax - 1f, rect.width, 1f));
            GUILayout.BeginArea(new Rect(rect.x + 10f, rect.y + 9f, rect.width - 20f, rect.height - 12f));
            using (new EditorGUILayout.HorizontalScope())
            {

                GUILayout.Label(new GUIContent("차트", "작업할 차트 에셋"), WorkspaceLabelStyle(true), GUILayout.Width(28f));
                EditorGUI.BeginChangeCheck();
                PrototypeChart nextChart = (PrototypeChart)EditorGUILayout.ObjectField(chart, typeof(PrototypeChart), false, GUILayout.MinWidth(105f));

                if (EditorGUI.EndChangeCheck())
                {

                    ChangeChart(nextChart);

                }

                if (!compact)
                {

                    DrawWorkspaceTransportButtons();

                }

                DrawWorkspaceTempoButton();
                bool dirty = chart != null && EditorUtility.IsDirty(chart);
                using (new EditorGUI.DisabledScope(chart == null || !dirty))
                {

                    if (GUILayout.Button(new GUIContent(dirty ? "저장 필요" : "저장됨", "차트에 반영된 변경을 에셋에 저장합니다. 임시 기록은 별도로 반영하세요."), GUILayout.Width(70f), GUILayout.Height(24f)))
                    {

                        AssetDatabase.SaveAssetIfDirty(chart);
                        statusMessage = $"'{chart.name}' 차트를 저장했습니다.";

                    }

                }

            }

            if (compact)
            {

                GUILayout.Space(8f);
                using (new EditorGUILayout.HorizontalScope())
                {

                    DrawWorkspaceTransportButtons();
                    GUILayout.FlexibleSpace();

                    if (GUILayout.Button("작업 준비", GUILayout.Width(72f), GUILayout.Height(24f)))
                    {

                        OpenWorkspaceInspector(WorkspaceInspectorTab.Tools);

                    }

                }

            }

            GUILayout.EndArea();

        }

        private void DrawWorkspaceTransportButtons()
        {

            bool canPlay = Application.isPlaying && songPlayback != null && songPlayback.IsPrepared;
            string playLabel = !canPlay ? "준비" : songPlayback.IsRunning && !songPlayback.IsPaused ? "일시정지" : "재생";

            if (GUILayout.Button(new GUIContent(playLabel, "Space: 재생/일시정지 · 처음 사용할 때는 도구에서 게임플레이 씬과 Play Mode를 준비하세요."), GUILayout.Width(65f), GUILayout.Height(24f)))
            {

                ToggleWorkspacePlayback();

            }

            using (new EditorGUI.DisabledScope(!canPlay))
            {

                if (GUILayout.Button("정지", GUILayout.Width(42f), GUILayout.Height(24f)))
                {

                    if (recordingPhase != RecordingPhase.Idle)
                    {

                        StopRecording();

                    }

                    StopTempoCalibrationPreview();
                    DisableGameplaySessionForAuthoring();
                    songPlayback.Stop();

                }

            }

            bool activeRecording = recordingPhase != RecordingPhase.Idle;
            using (new EditorGUI.DisabledScope(!activeRecording && (!canPlay || chart == null || chart.MusicalParts.Count == 0)))
            {

                Color previousColor = GUI.color;
                GUI.color = WorkspaceColor(0xa5223e, 0xff93a7);

                if (GUILayout.Button(activeRecording ? "■ 녹화 중지" : "● 녹화", GUILayout.Width(86f), GUILayout.Height(24f)))
                {

                    if (activeRecording)
                    {

                        StopRecording();

                    }
                    else
                    {

                        StartRecording(workspaceRecordingStart);

                    }

                }

                GUI.color = previousColor;

            }

            using (new EditorGUI.DisabledScope(activeRecording))
            {

                workspaceRecordingStart = (RecordingStartMode)EditorGUILayout.Popup((int)workspaceRecordingStart, WorkspaceStartNames, GUILayout.Width(102f));

            }

            GUILayout.Label(new GUIContent(GetWorkspaceMusicalPosition(), "FMOD 곡 시간에서 계산한 현재 마디와 박"), WorkspaceLabelStyle(), GUILayout.Width(102f));

        }

        private void DrawWorkspaceTempoButton()
        {

            string label = "박자 맞추기";

            if (chart != null && chart.TempoSections.Count > 0)
            {

                ChartTempoSection tempo = ChartTempoMap.FindSectionForTime(chart.TempoSections, songPlayback != null ? songPlayback.SongTime : seekTime);
                label = $"{tempo.BeatsPerMinute:0.##} BPM · {tempo.BeatsPerBar}/{tempo.BeatUnit}";

            }

            if (GUILayout.Button(new GUIContent(label, "두 앵커 또는 연속 다운비트로 박자 맞추기"), GUILayout.Width(116f), GUILayout.Height(24f)))
            {

                OpenWorkspaceInspector(WorkspaceInspectorTab.Tempo);

            }

        }

        private void DrawWorkspaceLoopBar(Rect rect)
        {

            EditorGUI.DrawRect(rect, WorkspacePanel);
            DrawWorkspaceDivider(new Rect(rect.x, rect.yMax - 1f, rect.width, 1f));
            GUILayout.BeginArea(new Rect(rect.x + 10f, rect.y + 8f, rect.width - 20f, rect.height - 10f));
            using (new EditorGUI.DisabledScope(chart == null || chart.TempoSections.Count == 0))
            using (new EditorGUILayout.HorizontalScope())
            {

                GUILayout.Label("반복", WorkspaceLabelStyle(), GUILayout.Width(26f));
                loopStartBar = Mathf.Max(1, EditorGUILayout.IntField(loopStartBar, GUILayout.Width(36f)));
                GUILayout.Label(new GUIContent("부터", "반복을 시작할 마디 번호"), WorkspaceLabelStyle(true), GUILayout.Width(26f));
                loopBarCount = Mathf.Max(1, EditorGUILayout.IntField(loopBarCount, GUILayout.Width(30f)));
                GUILayout.Label("마디", WorkspaceLabelStyle(true), GUILayout.Width(24f));

                if (GUILayout.Button("설정", GUILayout.Width(40f)))
                {

                    SetLoopFromBars();

                }

                GUILayout.Space(6f);
                GUILayout.Label(new GUIContent("스냅", "편집 스냅과 박자 보정이 함께 사용하는 격자입니다. 격자 선택만으로 노트 시각은 바뀌지 않습니다."), WorkspaceLabelStyle(true), GUILayout.Width(26f));
                quantizationGrid = (QuantizationGrid)EditorGUILayout.IntPopup((int)quantizationGrid, QuantizationGridNames, QuantizationGridValues, GUILayout.Width(102f));
                metronomeDuringRecording = GUILayout.Toggle(metronomeDuringRecording, new GUIContent("메트로놈", "녹화 중 클릭을 재생합니다. 음량·출력 보정은 녹화 속성에서 조절하세요."), GUILayout.Width(78f));

                if (GUILayout.Button($"카운트인 {countInBars}마디", GUILayout.Width(98f)))
                {

                    OpenWorkspaceInspector(WorkspaceInspectorTab.Recording);

                }

                GUILayout.FlexibleSpace();

                if (GUILayout.Button("검사", GUILayout.Width(42f)))
                {

                    ValidateChart();

                }

            }

            GUILayout.EndArea();

        }

        private void DrawWorkspaceParts(Rect rect)
        {

            EditorGUI.DrawRect(rect, WorkspaceBackground);
            DrawWorkspaceDivider(new Rect(rect.xMax, rect.y, 1f, rect.height));
            GUILayout.BeginArea(new Rect(rect.x + 8f, rect.y + 10f, rect.width - 16f, Mathf.Max(1f, rect.height - 18f)));
            GUILayout.Label("음악 파트", WorkspaceLabelStyle(true));
            GUILayout.Space(8f);
            workspacePartsScroll = EditorGUILayout.BeginScrollView(workspacePartsScroll, false, false, GUILayout.Height(Mathf.Max(38f, rect.height - 161f)));

            if (chart == null || chart.MusicalParts.Count == 0)
            {

                GUILayout.Label("차트에 음악 파트를 추가하세요.", WorkspaceWrappedStyle());

            }
            else
            {

                for (int index = 0; index < chart.MusicalParts.Count; index++)
                {

                    MusicalPartDefinition part = chart.MusicalParts[index];
                    Rect row = GUILayoutUtility.GetRect(10f, 32f, GUILayout.ExpandWidth(true));
                    bool selected = selectedPartIndex == index;

                    if (selected)
                    {

                        EditorGUI.DrawRect(row, WorkspaceColor(0xe1f2f6, 0x293f48));

                    }

                    EditorGUI.DrawRect(new Rect(row.x + 6f, row.y + 12f, 7f, 7f), part.Color);

                    if (GUI.Button(new Rect(row.x + 17f, row.y + 3f, row.width - 19f, 26f), new GUIContent(part.DisplayName, $"녹화·보정·복제 대상: {part.DisplayName} [{part.Id}]"), WorkspaceLabelStyle()))
                    {

                        SelectWorkspacePart(index);
                        GUI.FocusControl(null);
                        Repaint();

                    }

                    GUILayout.Space(3f);

                }

            }

            EditorGUILayout.EndScrollView();
            GUILayout.Space(6f);
            using (new EditorGUI.DisabledScope(!CanWorkspaceAuditionSelectedPart()))
            {

                if (GUILayout.Button("선택 파트만 듣기", GUILayout.Height(24f)))
                {

                    SoloSelectedPart();

                }

            }

            using (new EditorGUI.DisabledScope(chart == null || chart.StemParameters.Count == 0 || songPlayback == null || !Application.isPlaying))
            {

                if (GUILayout.Button("전체 소리 듣기", GUILayout.Height(24f)))
                {

                    SetAllStemVolumes(1f);

                }

            }

            GUILayout.Space(5f);

            if (GUILayout.Button("파트 설정 · 음량", GUILayout.Height(24f)))
            {

                OpenWorkspaceInspector(WorkspaceInspectorTab.Part);

            }

            GUILayout.Label("청취 ≠ 게임 활성 구간", WorkspaceLabelStyle(true));
            GUILayout.EndArea();

        }

        private void DrawWorkspaceMain(Rect rect, bool compact)
        {

            EditorGUI.DrawRect(rect, WorkspaceCanvas);
            GUILayout.BeginArea(rect);
            Rect firstRow = new Rect(10f, 8f, rect.width - 20f, 25f);
            bool vertical = timelineViewMode == TimelineViewMode.Vertical;

            int selectedView = vertical ? 0 : 1;
            int nextView = GUI.Toolbar(new Rect(firstRow.x, firstRow.y, 174f, 25f),
                selectedView, WorkspaceViewNames, EditorStyles.miniButton);

            if (nextView != selectedView)
            {

                vertical = nextView == 0;
                timelineViewMode = vertical ? TimelineViewMode.Vertical : TimelineViewMode.Horizontal;
                GUI.FocusControl(null);
                Repaint();

            }

            if (compact && GUI.Button(new Rect(firstRow.xMax - 72f, firstRow.y, 72f, 25f), "속성 열기"))
            {

                workspaceCompactInspector = true;

            }
            else if (!compact && chart != null && chart.MusicalParts.Count > 0)
            {

                GUI.Label(new Rect(firstRow.x + 186f, firstRow.y, Mathf.Max(0f, firstRow.width - 186f), 25f), chart.MusicalParts[selectedPartIndex].DisplayName + (vertical ? " 집중 보기" : " · 음악 구조"), WorkspaceLabelStyle(true));

            }

            GUI.Label(new Rect(12f, 44f, 62f, 20f), "녹화 유형", WorkspaceLabelStyle(true));
            using (new EditorGUI.DisabledScope(recordingPhase != RecordingPhase.Idle || pendingInteraction != null))
            {

                recordingNoteMode = (RecordingNoteMode)EditorGUI.Popup(new Rect(76f, 44f, 121f, 20f), (int)recordingNoteMode, RecordingNoteModeNames);

                if (recordingNoteMode == RecordingNoteMode.Flick)
                {

                    flickDefaultDirection = (FlickDefaultDirection)EditorGUI.IntPopup(new Rect(204f, 44f, 87f, 20f), (int)flickDefaultDirection, FlickDirectionNames, FlickDirectionValues);

                }

            }

            float canvasBottom = Mathf.Max(109f, rect.height - 106f);
            Rect canvasRect = new Rect(0f, 73f, rect.width, Mathf.Max(30f, canvasBottom - 73f));

            if (chart != null)
            {

                DrawWorkspaceTimeline(canvasRect);

            }
            else
            {

                GUI.Label(new Rect(20f, 100f, rect.width - 40f, 70f), "상단에서 차트를 선택하세요.\n도구에서 게임플레이 씬을 준비하면 미리 듣고 녹화할 수 있습니다.", WorkspaceWrappedStyle());

            }

            DrawWorkspaceCanvasNavigation(new Rect(10f, canvasBottom + 4f, rect.width - 20f, 23f));
            float legendY = canvasBottom + 32f;
            DrawWorkspaceDivider(new Rect(0f, legendY - 2f, rect.width, 1f));
            GUI.Label(new Rect(12f, legendY, 142f, 20f), "● 차트 반영   ◌ 임시 기록", WorkspaceLabelStyle(true));
            workspaceShowChecks = GUI.Toggle(new Rect(156f, legendY, 103f, 20f), workspaceShowChecks, new GUIContent("판정점 보기", "작은 점: ¼박 유효성 검사 · 큰 점: ½박 보상. 표시만 바뀌며 노트 데이터는 변경되지 않습니다."));
            workspaceShowContext = GUI.Toggle(new Rect(268f, legendY, Mathf.Max(70f, rect.width - 277f), 20f), workspaceShowContext, new GUIContent("다른 파트", "노트 편집에서 다른 파트를 옅게 표시합니다. 소리와 게임 활성 구간에는 영향이 없습니다."));
            Rect hintRect = new Rect(0f, rect.height - 44f, rect.width, 44f);
            EditorGUI.DrawRect(hintRect, WorkspacePanel);
            string hint = pendingInteraction == null ? GetWorkspaceRecordingHint() : $"입력 진행 중 · {pendingInteraction.EndLaneIndex + 1}번에서 끝 입력 대기. 중지하면 미완성 노트는 버려집니다.";
            GUI.Label(new Rect(12f, hintRect.y + 6f, rect.width - 24f, 36f), new GUIContent(hint, GetRecordingModeInstructions()), WorkspaceWrappedStyle());
            GUILayout.EndArea();

        }

        private void DrawWorkspaceCanvasNavigation(Rect rect)
        {

            using (new EditorGUI.DisabledScope(chart == null))
            {

                if (GUI.Button(new Rect(rect.x, rect.y, 26f, 21f), new GUIContent("−", "축소 · Ctrl/⌘ + 휠로 포인터 중심 확대")))
                {

                    ZoomWorkspaceTimeline(1.25f);

                }

                if (GUI.Button(new Rect(rect.x + 29f, rect.y, 26f, 21f), new GUIContent("+", "확대 · 마우스 휠로 시간 이동")))
                {

                    ZoomWorkspaceTimeline(0.8f);

                }

                if (GUI.Button(new Rect(rect.x + 64f, rect.y, 58f, 21f), "재생선"))
                {

                    CenterTimelineOnPlayback();

                }

                if (GUI.Button(new Rect(rect.x + 127f, rect.y, 44f, 21f), "구간"))
                {

                    timelineAutoScroll = false;
                    timelineStartTime = Math.Max(0d, loopStart);
                    timelineVisibleDuration = Mathf.Clamp((float)(loopEnd - loopStart), 1f, 120f);

                }

                timelineAutoScroll = GUI.Toggle(new Rect(rect.x + 181f, rect.y, 106f, 21f), timelineAutoScroll, "재생선 따라가기");
                GUI.Label(new Rect(rect.xMax - 77f, rect.y, 77f, 21f), $"{timelineVisibleDuration:0.#}초 표시", WorkspaceLabelStyle(true));

            }

        }

        private void DrawWorkspaceInspectorPane(Rect rect, bool compact)
        {

            EditorGUI.DrawRect(rect, WorkspaceBackground);
            DrawWorkspaceDivider(new Rect(rect.x - 1f, rect.y, 1f, rect.height));
            GUILayout.BeginArea(new Rect(rect.x + 9f, rect.y + 8f, rect.width - 18f, Mathf.Max(1f, rect.height - 14f)));

            if (compact)
            {

                if (GUILayout.Button("← 채보로 돌아가기", GUILayout.Height(25f)))
                {

                    workspaceCompactInspector = false;

                }

                GUILayout.Space(7f);

            }

            WorkspaceInspectorTab nextTab = (WorkspaceInspectorTab)GUILayout.Toolbar((int)workspaceInspectorTab, WorkspaceInspectorNames, GUILayout.Height(25f));

            if (nextTab != workspaceInspectorTab)
            {

                workspaceInspectorTab = nextTab;
                workspaceInspectorScroll = Vector2.zero;
                GUI.FocusControl(null);

            }

            GUILayout.Space(9f);
            workspaceInspectorScroll = EditorGUILayout.BeginScrollView(workspaceInspectorScroll, false, false);
            float previousLabelWidth = EditorGUIUtility.labelWidth;
            EditorGUIUtility.labelWidth = Mathf.Min(108f, rect.width * 0.36f);
            if (workspaceInspectorTab == WorkspaceInspectorTab.Tempo)
            {

                // Reserve the vertical scrollbar width. Tempo fields must wrap
                // inside the pane rather than growing the scroll content sideways.
                using (new EditorGUILayout.VerticalScope(GUILayout.Width(Mathf.Max(180f, rect.width - 38f))))
                {

                    DrawWorkspaceInspector();

                }

            }
            else
            {

                DrawWorkspaceInspector();

            }
            EditorGUIUtility.labelWidth = previousLabelWidth;
            EditorGUILayout.EndScrollView();
            GUILayout.EndArea();

        }

        private void DrawWorkspaceBuffer(Rect rect)
        {

            EditorGUI.DrawRect(rect, WorkspacePanel);
            DrawWorkspaceDivider(new Rect(rect.x, rect.y, rect.width, 1f));
            GUILayout.BeginArea(new Rect(rect.x + 10f, rect.y + 8f, rect.width - 20f, Mathf.Max(1f, rect.height - 12f)));
            using (new EditorGUILayout.HorizontalScope())
            {

                Rect foldoutRect = GUILayoutUtility.GetRect(138f, 24f, GUILayout.Width(138f));
                workspaceShowBufferDetails = EditorGUI.Foldout(foldoutRect, workspaceShowBufferDetails, $"임시 기록 {recordedNotes.Count}개", true);
                GUILayout.FlexibleSpace();
                using (new EditorGUI.DisabledScope(recordedNotes.Count == 0 && pendingInteraction == null))
                {

                    if (GUILayout.Button("버리기", GUILayout.Width(60f), GUILayout.Height(24f)))
                    {

                        ClearBuffer();

                    }

                }

                applyMode = (ApplyMode)EditorGUILayout.Popup((int)applyMode, WorkspaceApplyNames, GUILayout.Width(176f));
                using (new EditorGUI.DisabledScope(chart == null || recordedNotes.Count == 0 || recordingPhase != RecordingPhase.Idle || (applyMode == ApplyMode.ReplaceRecordedPartsInLoop && loopEnd <= loopStart)))
                {

                    if (GUILayout.Button("차트에 반영", GUILayout.Width(104f), GUILayout.Height(24f)))
                    {

                        if (!bufferWasApplied || EditorUtility.DisplayDialog("기록 다시 적용", "현재 임시 기록은 이미 적용되었습니다. 그래도 다시 적용할까요?", "다시 적용", "취소"))
                        {

                            ApplyRecordedNotes();

                        }

                    }

                }

            }

            string bufferSummary = bufferWasApplied && recordedNotes.Count > 0
                ? "이미 반영한 기록 · 다시 반영하면 중복될 수 있습니다."
                : recordedNotes.Count == 0 ? "새 입력이 여기에 쌓입니다. 차트에 반영한 뒤 상단에서 저장하세요." : "아직 차트에 반영되지 않은 기록 · 펼쳐서 목록과 반영 옵션 보기";
            GUILayout.Label(bufferSummary, WorkspaceLabelStyle(true));

            if (workspaceShowBufferDetails)
            {

                GUILayout.Space(5f);
                workspaceBufferScroll = EditorGUILayout.BeginScrollView(workspaceBufferScroll, false, false);
                DrawWorkspaceBufferDetails();
                EditorGUILayout.EndScrollView();

            }

            GUILayout.EndArea();

        }

        private void DrawWorkspaceStatus(Rect rect)
        {

            EditorGUI.DrawRect(rect, WorkspaceBackground);
            DrawWorkspaceDivider(new Rect(rect.x, rect.y, rect.width, 1f));
            string state = recordingPhase != RecordingPhase.Idle && chart != null && chart.MusicalParts.Count > 0
                ? GetRecordingPhaseMessage()
                : statusMessage;
            GUI.Label(new Rect(rect.x + 10f, rect.y + 4f, rect.width - 20f, 18f), new GUIContent(state, state + "\nSpace 재생/일시정지 · R 현재 위치 녹화 · Esc 중지 · Delete 선택 삭제 · Ctrl/⌘+휠 확대"), WorkspaceLabelStyle(true));

        }

        private void ToggleWorkspacePlayback()
        {

            if (!Application.isPlaying || songPlayback == null || !songPlayback.IsPrepared)
            {

                OpenWorkspaceInspector(WorkspaceInspectorTab.Tools);
                return;

            }

            if (recordingPhase != RecordingPhase.Idle)
            {

                StopRecording();

            }

            DisableGameplaySessionForAuthoring();

            if (!songPlayback.IsRunning)
            {

                songPlayback.Play();

            }
            else if (songPlayback.IsPaused)
            {

                songPlayback.Resume();

            }
            else
            {

                songPlayback.Pause();

            }

        }

        private void OpenWorkspaceInspector(WorkspaceInspectorTab tab)
        {

            workspaceInspectorTab = tab;
            workspaceInspectorScroll = Vector2.zero;
            workspaceCompactInspector = position.width < 1000f;
            GUI.FocusControl(null);

        }

        private bool CanWorkspaceAuditionSelectedPart()
        {

            if (chart == null || chart.MusicalParts.Count == 0 || songPlayback == null || !Application.isPlaying)
            {

                return false;

            }

            string partId = chart.MusicalParts[selectedPartIndex].Id;

            for (int index = 0; index < chart.StemParameters.Count; index++)
            {

                if (chart.StemParameters[index].StemId == partId)
                {

                    return true;

                }

            }

            return false;

        }

        private void ZoomWorkspaceTimeline(float factor)
        {

            double center = timelineStartTime + timelineVisibleDuration * 0.5d;
            timelineVisibleDuration = Mathf.Clamp(timelineVisibleDuration * factor, 1f, 120f);
            timelineStartTime = Math.Max(0d, center - timelineVisibleDuration * 0.5d);
            timelineAutoScroll = false;

        }

        private string GetWorkspaceMusicalPosition()
        {

            if (chart == null || chart.TempoSections.Count == 0)
            {

                return "박자 정보 없음";

            }

            ChartBeatPosition beat = ChartTempoMap.GetBeatPosition(chart.TempoSections, songPlayback != null ? songPlayback.SongTime : seekTime);
            return $"{beat.Bar}마디 {beat.Beat}박";

        }

        private void SelectWorkspacePart(int index)
        {

            selectedPartIndex = index;
            string partId = chart.MusicalParts[index].Id;
            bool selectedDifferentPart = selectedRecordedNoteIndex >= 0 &&
                                         selectedRecordedNoteIndex < recordedNotes.Count &&
                                         recordedNotes[selectedRecordedNoteIndex].musicalPartId != partId;

            if (!string.IsNullOrWhiteSpace(selectedChartNoteId))
            {

                for (int noteIndex = 0; noteIndex < chart.Notes.Count; noteIndex++)
                {

                    ChartNote note = chart.Notes[noteIndex];

                    if (note.Id == selectedChartNoteId)
                    {

                        selectedDifferentPart |= note.MusicalPartId != partId;
                        break;

                    }

                }

            }

            if (selectedDifferentPart)
            {

                selectedRecordedNoteIndex = -1;
                selectedChartNoteId = string.Empty;
                selectedAppliedNoteDataId = string.Empty;
                selectedAppliedNoteData = null;

            }

        }

        private string GetWorkspaceRecordingHint()
        {

            int laneCount = chart != null ? Math.Min(chart.LaneCount, SupportedKeyboardLaneCount) : SupportedKeyboardLaneCount;
            return recordingNoteMode switch
            {
                RecordingNoteMode.Tap => $"숫자키 1–{laneCount}: 탭 기록 · 노트 선택 → 오른쪽 속성에서 수정",
                RecordingNoteMode.SlideAndHold => "다른 키: 그 박자에 위치 전환 · 같은 키: 유지 종료 · 0: 마지막 전환을 플릭으로",
                RecordingNoteMode.Flick => "숫자키: 플릭 시작 · 선택 방향의 인접 위치로 이동 · 속성에서 끝점 수정",
                RecordingNoteMode.Banana => "첫 키: 시작 · 두 번째 키: 끝 · 속성에서 곡선 핸들과 체크포인트 수정",
                _ => string.Empty
            };

        }

        private static GUIStyle WorkspaceLabelStyle(bool muted = false)
        {

            workspaceLabel ??= new GUIStyle(EditorStyles.label) { fontSize = 12, clipping = TextClipping.Clip };
            workspaceMutedLabel ??= new GUIStyle(EditorStyles.label) { fontSize = 11, clipping = TextClipping.Clip };
            GUIStyle style = muted ? workspaceMutedLabel : workspaceLabel;
            style.normal.textColor = muted ? WorkspaceMuted : WorkspaceText;
            return style;

        }

        private static GUIStyle WorkspaceWrappedStyle()
        {

            workspaceWrapLabel ??= new GUIStyle(EditorStyles.wordWrappedMiniLabel) { fontSize = 11 };
            workspaceWrapLabel.normal.textColor = WorkspaceMuted;
            return workspaceWrapLabel;

        }

        private static Color WorkspaceColor(int light, int dark)
        {

            int value = EditorGUIUtility.isProSkin ? dark : light;
            return new Color(((value >> 16) & 255) / 255f, ((value >> 8) & 255) / 255f, (value & 255) / 255f, 1f);

        }

        private static void DrawWorkspaceDivider(Rect rect)
        {

            EditorGUI.DrawRect(rect, WorkspaceBorder);

        }

    }

}
