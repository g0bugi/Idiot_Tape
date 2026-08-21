using System;
using System.Collections.Generic;
using IdiotTape.Audio;
using IdiotTape.Gameplay;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace IdiotTape.EditorTools
{

    public sealed class PrototypeChartRecorderWindow : EditorWindow
    {

        private const string GameplayScenePath = "Assets/Scenes/Gameplay.unity";
        private const int SupportedKeyboardLaneCount = 8;
        private const double DuplicateInputThresholdSeconds = 0.010d;
        private const double MetronomeScheduleLeadSeconds = 0.4d;
        private const float TimelineRulerHeight = 26f;
        private const float TimelinePartHeight = 42f;

        private enum ApplyMode
        {

            Append,
            ReplaceRecordedPartsInLoop

        }

        private enum RecordingStartMode
        {

            CurrentPosition,
            Loop,
            Beginning

        }

        private enum RecordingPhase
        {

            Idle,
            CountIn,
            PreRoll,
            Recording

        }

        [Serializable]
        private sealed class RecordedNote
        {

            public double hitTime;
            public int laneIndex;
            public string musicalPartId;

        }

        private sealed class EditableNote
        {

            public string id;
            public double hitTime;
            public int laneIndex;
            public string musicalPartId;

        }

        [SerializeField] private PrototypeChart chart;
        [SerializeField] private List<RecordedNote> recordedNotes = new();
        [SerializeField] private int selectedPartIndex;
        [SerializeField] private int selectedRecordedNoteIndex = -1;
        [SerializeField] private double seekTime;
        [SerializeField] private double loopStart;
        [SerializeField] private double loopEnd = 8d;
        [SerializeField] private ApplyMode applyMode;
        [SerializeField] private bool addMissingActivationWindows = true;
        [SerializeField, Min(1)] private int countInBars = 2;
        [SerializeField, Range(0f, 1f)] private float metronomeVolume = 0.15f;
        [SerializeField] private bool metronomeDuringRecording;
        [SerializeField, Min(4f)] private float timelineVisibleDuration = 16f;
        [SerializeField, Min(0f)] private double timelineStartTime;
        [SerializeField, Min(1)] private int loopStartBar = 1;
        [SerializeField, Min(2)] private int loopEndBar = 5;

        private readonly double[] lastRecordedInputTimestamps = new double[SupportedKeyboardLaneCount];
        private InputAction[] recordingInputActions;
        private Vector2 scrollPosition;
        private FmodSongPlayback songPlayback;
        private ChartAuthoringMetronome metronome;
        private RecordingPhase recordingPhase;
        private RecordingStartMode activeStartMode;
        private bool isRecording;
        private bool isLoopRecording;
        private string configuredEventPath = string.Empty;
        private double recordingTargetTime;
        private double preRollStartTime;
        private double countInEndRealtime;
        private double nextMetronomeSongTime = double.NaN;
        private string statusMessage = "차트를 선택하세요.";

        [MenuItem("Tools/Idiot Tape/채보 제작 도구")]
        public static void Open()
        {

            PrototypeChartRecorderWindow window = GetWindow<PrototypeChartRecorderWindow>();
            window.titleContent = new GUIContent("채보 제작");
            window.minSize = new Vector2(700f, 720f);
            window.Show();

        }

        private void OnEnable()
        {

            if (chart == null)
            {

                chart = AssetDatabase.LoadAssetAtPath<PrototypeChart>(SnowPrototypeChartSetup.ChartPath);

            }

            CreateRecordingInputActions();
            EditorApplication.update += EditorUpdate;

        }

        private void OnDisable()
        {

            EditorApplication.update -= EditorUpdate;
            isRecording = false;
            isLoopRecording = false;
            recordingPhase = RecordingPhase.Idle;
            DestroyMetronome();
            DisposeRecordingInputActions();

        }

        private void OnGUI()
        {

            HandleRecorderWindowKeyboardEvent(Event.current);
            EditorGUILayout.LabelField("Idiot_Tape 채보 제작 도구", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "게임플레이와 동일한 FMOD DSP 시간으로 숫자키 입력을 기록합니다. " +
                "'기록 적용'을 누르기 전까지 차트 원본은 바뀌지 않습니다.",
                MessageType.Info);

            EditorGUI.BeginChangeCheck();
            PrototypeChart selectedChart = (PrototypeChart)EditorGUILayout.ObjectField(
                "차트",
                chart,
                typeof(PrototypeChart),
                false);

            if (EditorGUI.EndChangeCheck())
            {

                ChangeChart(selectedChart);

            }

            if (chart == null)
            {

                DrawSceneControls();
                return;

            }

            ClampSelections();
            DrawSceneControls();
            EditorGUILayout.Space(8f);
            DrawPlaybackControls();
            EditorGUILayout.Space(8f);
            DrawTimeline();
            EditorGUILayout.Space(8f);

            if (chart.MusicalParts.Count > 0)
            {

                DrawRecordingControls();
                EditorGUILayout.Space(8f);
                DrawStemControls();
                EditorGUILayout.Space(8f);
                DrawRecordedNotes();
                EditorGUILayout.Space(8f);

            }
            else
            {

                EditorGUILayout.HelpBox(
                    "노트를 녹화하려면 차트에 음악 파트를 하나 이상 추가하세요.",
                    MessageType.Error);

            }

            DrawApplyControls();
            EditorGUILayout.Space(6f);
            EditorGUILayout.HelpBox(statusMessage, MessageType.None);

        }

        private void DrawSceneControls()
        {

            using (new EditorGUILayout.HorizontalScope())
            {

                if (!Application.isPlaying && GUILayout.Button("게임플레이 씬 열기"))
                {

                    if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                    {

                        EditorSceneManager.OpenScene(GameplayScenePath);

                    }

                }

                if (!Application.isPlaying && GUILayout.Button("플레이 모드 시작"))
                {

                    EditorApplication.EnterPlaymode();

                }

                if (Application.isPlaying && GUILayout.Button("플레이 모드 종료"))
                {

                    isRecording = false;
                    isLoopRecording = false;
                    recordingPhase = RecordingPhase.Idle;
                    metronome?.StopAll();
                    DisableRecordingInputActions();
                    EditorApplication.ExitPlaymode();

                }

            }

            if (!Application.isPlaying)
            {

                EditorGUILayout.HelpBox(
                    "미리 듣거나 녹화하려면 게임플레이 씬을 열고 플레이 모드를 시작하세요. " +
                    "임시 기록은 일반적인 플레이 모드 전환 중에도 유지됩니다.",
                    MessageType.Warning);

            }

        }

        private void DrawPlaybackControls()
        {

            EditorGUILayout.LabelField("재생 제어", EditorStyles.boldLabel);
            RefreshSongPlayback();

            if (!Application.isPlaying || songPlayback == null)
            {

                EditorGUILayout.LabelField("게임플레이용 FMOD 재생기를 찾을 수 없습니다.");
                return;

            }

            EditorGUILayout.LabelField("곡 이벤트", chart.SongEventPath);
            EditorGUILayout.LabelField("현재 곡 시간", $"{songPlayback.SongTime:0.000}초");
            EditorGUILayout.LabelField("전체 길이", $"{songPlayback.DurationSeconds:0.000}초");
            EditorGUILayout.LabelField("오디오 상태", songPlayback.IsPrepared ? "준비 완료" : "불러오는 중");

            if (chart.TempoSections.Count > 0)
            {

                ChartBeatPosition position = ChartTempoMap.GetBeatPosition(chart.TempoSections, songPlayback.SongTime);
                EditorGUILayout.LabelField(
                    "음악 위치",
                    $"{position.Bar}마디 {position.Beat}박 · {chart.TempoSections[0].BeatsPerMinute:0.###} BPM");

            }

            using (new EditorGUILayout.HorizontalScope())
            {

                if (GUILayout.Button("처음으로"))
                {

                    DisableGameplaySessionForAuthoring();
                    songPlayback.Restart();

                }

                if (GUILayout.Button(songPlayback.IsPaused ? "계속 재생" : "일시정지"))
                {

                    if (songPlayback.IsPaused)
                    {

                        songPlayback.Resume();

                    }
                    else
                    {

                        songPlayback.Pause();

                    }

                }

                if (GUILayout.Button("정지"))
                {

                    isRecording = false;
                    isLoopRecording = false;
                    recordingPhase = RecordingPhase.Idle;
                    metronome?.StopAll();
                    DisableRecordingInputActions();
                    DisableGameplaySessionForAuthoring();
                    songPlayback.Stop();

                }

            }

            using (new EditorGUILayout.HorizontalScope())
            {

                seekTime = Math.Max(0d, EditorGUILayout.DoubleField("이동할 시간", seekTime));

                if (GUILayout.Button("이동", GUILayout.Width(48f)))
                {

                    Seek(seekTime);

                }

            }

            loopStart = Math.Max(0d, EditorGUILayout.DoubleField("반복 시작", loopStart));
            loopEnd = Math.Max(0d, EditorGUILayout.DoubleField("반복 끝", loopEnd));

            using (new EditorGUILayout.HorizontalScope())
            {

                loopStartBar = Math.Max(1, EditorGUILayout.IntField("시작 마디", loopStartBar));
                loopEndBar = Math.Max(loopStartBar + 1, EditorGUILayout.IntField("끝 마디", loopEndBar));

                if (GUILayout.Button("마디 범위 적용", GUILayout.Width(112f)))
                {

                    SetLoopFromBars();

                }

                if (GUILayout.Button("반복 구간을 마디에 맞춤", GUILayout.Width(150f)))
                {

                    SnapLoopToBars();

                }

            }

            if (loopEnd <= loopStart)
            {

                EditorGUILayout.HelpBox("반복 끝은 반복 시작보다 뒤여야 합니다.", MessageType.Error);

            }

        }

        private void DrawRecordingControls()
        {

            EditorGUILayout.LabelField("녹화", EditorStyles.boldLabel);
            string[] partNames = GetPartNames();
            selectedPartIndex = EditorGUILayout.Popup("음악 파트", selectedPartIndex, partNames);
            countInBars = Mathf.Max(1, EditorGUILayout.IntField("카운트인 마디", countInBars));
            metronomeVolume = EditorGUILayout.Slider("메트로놈 음량", metronomeVolume, 0f, 1f);
            metronomeDuringRecording = EditorGUILayout.Toggle("녹화 중 메트로놈", metronomeDuringRecording);

            if (chart.TempoSections.Count > 0)
            {

                double countInDuration = chart.TempoSections[0].SecondsPerBar * countInBars;
                EditorGUILayout.LabelField("준비 시간", $"약 {countInDuration:0.00}초");

            }

            int recordableLaneCount = Math.Min(chart.LaneCount, SupportedKeyboardLaneCount);
            EditorGUILayout.LabelField(
                "숫자키 위치",
                recordableLaneCount == SupportedKeyboardLaneCount
                    ? "1–8"
                    : $"1–{recordableLaneCount}");

            if (chart.LaneCount > SupportedKeyboardLaneCount)
            {

                EditorGUILayout.HelpBox(
                    "현재 녹화 도구는 1번부터 8번 위치까지만 숫자키로 기록할 수 있습니다.",
                    MessageType.Warning);

            }

            GUI.enabled = Application.isPlaying &&
                          songPlayback != null &&
                          songPlayback.IsPrepared &&
                          recordingPhase == RecordingPhase.Idle;

            using (new EditorGUILayout.HorizontalScope())
            {

                if (GUILayout.Button("현재 위치부터 녹화"))
                {

                    StartRecording(RecordingStartMode.CurrentPosition);

                }

                if (GUILayout.Button("구간 반복 녹화"))
                {

                    StartRecording(RecordingStartMode.Loop);

                }

                if (GUILayout.Button("처음부터 녹화"))
                {

                    StartRecording(RecordingStartMode.Beginning);

                }

            }

            GUI.enabled = true;

            using (new EditorGUILayout.HorizontalScope())
            {

                GUI.enabled = recordingPhase != RecordingPhase.Idle;

                if (GUILayout.Button("녹화 중지"))
                {

                    StopRecording();

                }

                GUI.enabled = true;

                if (GUILayout.Button("임시 기록 지우기"))
                {

                    ClearBuffer();

                }

            }

            if (recordingPhase != RecordingPhase.Idle)
            {

                EditorGUILayout.HelpBox(
                    GetRecordingPhaseMessage(),
                    MessageType.Warning);

            }

        }

        private void DrawStemControls()
        {

            EditorGUILayout.LabelField("파트 소리 조절", EditorStyles.boldLabel);

            if (!Application.isPlaying || songPlayback == null)
            {

                EditorGUILayout.LabelField("플레이 모드에서 사용할 수 있습니다.");
                return;

            }

            if (chart.StemParameters.Count == 0)
            {

                EditorGUILayout.LabelField("이 차트에는 파트별 소리 설정이 없습니다.");
                return;

            }

            for (int index = 0; index < chart.StemParameters.Count; index++)
            {

                FmodStemDefinition stem = chart.StemParameters[index];

                if (!songPlayback.TryGetStemVolume(stem.StemId, out float volume))
                {

                    continue;

                }

                float nextVolume = EditorGUILayout.Slider(stem.StemId, volume, 0f, 1f);

                if (!Mathf.Approximately(nextVolume, volume))
                {

                    songPlayback.SetStemVolume(stem.StemId, nextVolume);

                }

            }

            using (new EditorGUILayout.HorizontalScope())
            {

                if (GUILayout.Button("전체 소리 듣기"))
                {

                    SetAllStemVolumes(1f);

                }

                if (GUILayout.Button("선택한 파트만 듣기"))
                {

                    SoloSelectedPart();

                }

            }

        }

        private void DrawRecordedNotes()
        {

            EditorGUILayout.LabelField($"임시 기록 ({recordedNotes.Count}개)", EditorStyles.boldLabel);

            using (new EditorGUILayout.HorizontalScope())
            {

                GUI.enabled = selectedRecordedNoteIndex >= 0;

                if (GUILayout.Button("10ms 앞"))
                {

                    NudgeSelected(-0.010d);

                }

                if (GUILayout.Button("5ms 앞"))
                {

                    NudgeSelected(-0.005d);

                }

                if (GUILayout.Button("1ms 앞"))
                {

                    NudgeSelected(-0.001d);

                }

                if (GUILayout.Button("1ms 뒤"))
                {

                    NudgeSelected(0.001d);

                }

                if (GUILayout.Button("5ms 뒤"))
                {

                    NudgeSelected(0.005d);

                }

                if (GUILayout.Button("10ms 뒤"))
                {

                    NudgeSelected(0.010d);

                }

                GUI.enabled = true;

            }

            string[] partNames = GetPartNames();
            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition, GUILayout.MinHeight(180f));
            int removeIndex = -1;

            for (int index = 0; index < recordedNotes.Count; index++)
            {

                RecordedNote note = recordedNotes[index];

                using (new EditorGUILayout.HorizontalScope())
                {

                    bool selected = GUILayout.Toggle(
                        selectedRecordedNoteIndex == index,
                        (index + 1).ToString("000"),
                        "Button",
                        GUILayout.Width(48f));

                    if (selected)
                    {

                        selectedRecordedNoteIndex = index;

                    }

                    note.hitTime = Math.Max(0d, EditorGUILayout.DoubleField(note.hitTime, GUILayout.Width(92f)));
                    note.laneIndex = Mathf.Clamp(
                        EditorGUILayout.IntField(note.laneIndex + 1, GUILayout.Width(42f)) - 1,
                        0,
                        chart.LaneCount - 1);
                    int partIndex = FindPartIndex(note.musicalPartId);
                    partIndex = EditorGUILayout.Popup(partIndex, partNames);
                    note.musicalPartId = chart.MusicalParts[partIndex].Id;

                    if (GUILayout.Button("삭제", GUILayout.Width(44f)))
                    {

                        removeIndex = index;

                    }

                }

            }

            EditorGUILayout.EndScrollView();

            if (removeIndex >= 0)
            {

                recordedNotes.RemoveAt(removeIndex);
                selectedRecordedNoteIndex = Mathf.Clamp(
                    selectedRecordedNoteIndex,
                    -1,
                    recordedNotes.Count - 1);

            }

        }

        private void DrawApplyControls()
        {

            EditorGUILayout.LabelField("차트 적용 및 검사", EditorStyles.boldLabel);
            applyMode = (ApplyMode)EditorGUILayout.Popup(
                "적용 방식",
                (int)applyMode,
                new[] { "기존 노트 유지하고 추가", "반복 구간의 같은 파트 교체" });
            addMissingActivationWindows = EditorGUILayout.Toggle(
                "누락된 파트 활성 구간 추가",
                addMissingActivationWindows);

            if (applyMode == ApplyMode.ReplaceRecordedPartsInLoop && loopEnd <= loopStart)
            {

                EditorGUILayout.HelpBox(
                    "교체하려면 올바른 반복 구간이 필요합니다.",
                    MessageType.Error);

            }

            using (new EditorGUILayout.HorizontalScope())
            {

                GUI.enabled = recordedNotes.Count > 0 &&
                              (applyMode == ApplyMode.Append || loopEnd > loopStart);

                if (GUILayout.Button("기록 적용"))
                {

                    ApplyRecordedNotes();

                }

                GUI.enabled = true;

                if (GUILayout.Button("차트 검사"))
                {

                    ValidateChart();

                }

                if (GUILayout.Button("에셋 저장"))
                {

                    AssetDatabase.SaveAssetIfDirty(chart);
                    statusMessage = $"'{chart.name}' 차트를 저장했습니다.";

                }

                if (GUILayout.Button("실행 취소"))
                {

                    Undo.PerformUndo();
                    statusMessage = "Unity 작업 하나를 실행 취소했습니다.";

                }

            }

            EditorGUILayout.HelpBox(
                "적용하고 저장한 뒤 플레이 모드를 다시 시작하면 바뀐 차트를 테스트할 수 있습니다.",
                MessageType.Info);

        }

        private void EditorUpdate()
        {

            if (!Application.isPlaying)
            {

                songPlayback = null;
                isRecording = false;
                isLoopRecording = false;
                recordingPhase = RecordingPhase.Idle;
                configuredEventPath = string.Empty;
                DestroyMetronome();
                DisableRecordingInputActions();
                return;

            }

            RefreshSongPlayback();

            if (songPlayback == null)
            {

                return;

            }

            if (recordingPhase == RecordingPhase.CountIn)
            {

                if (EditorApplication.timeSinceStartup >= countInEndRealtime)
                {

                    songPlayback.Seek(recordingTargetTime);
                    songPlayback.Play();
                    ActivateRecording();

                }

            }
            else if (recordingPhase == RecordingPhase.PreRoll)
            {

                ScheduleSongMetronome();

                if (songPlayback.SongTime >= recordingTargetTime)
                {

                    ActivateRecording();

                }

            }
            else if (recordingPhase == RecordingPhase.Recording)
            {

                if (metronomeDuringRecording)
                {

                    ScheduleSongMetronome();

                }

                if (isLoopRecording && loopEnd > loopStart && songPlayback.SongTime >= loopEnd)
                {

                    RestartLoopCycle();

                }

            }

            Repaint();

        }

        private void StartRecording(RecordingStartMode startMode)
        {

            if (songPlayback == null || !songPlayback.IsPrepared || chart.MusicalParts.Count == 0)
            {

                statusMessage = "FMOD 재생과 음악 파트가 하나 이상 필요합니다.";
                return;

            }

            if (startMode == RecordingStartMode.Loop && loopEnd <= loopStart)
            {

                statusMessage = "구간 반복 녹화를 시작하려면 올바른 반복 시작·끝 시간을 입력하세요.";
                return;

            }

            if (chart.TempoSections.Count == 0)
            {

                statusMessage = "카운트인과 마디 계산을 위한 템포 정보가 필요합니다.";
                return;

            }

            DisableGameplaySessionForAuthoring();

            activeStartMode = startMode;
            isLoopRecording = startMode == RecordingStartMode.Loop;
            isRecording = false;
            DisableRecordingInputActions();

            if (startMode == RecordingStartMode.Loop)
            {

                recordingTargetTime = loopStart;

            }
            else if (startMode == RecordingStartMode.Beginning)
            {

                recordingTargetTime = 0d;

            }
            else
            {

                recordingTargetTime = songPlayback.SongTime;

            }

            BeginCountInOrPreRoll();

            statusMessage = startMode switch
            {
                RecordingStartMode.CurrentPosition => "현재 위치 녹화를 위한 카운트인을 시작했습니다.",
                RecordingStartMode.Loop => "반복 구간 녹화를 위한 프리롤을 시작했습니다.",
                _ => "곡 처음 녹화를 위한 카운트인을 시작했습니다."
            };

        }

        private void StopRecording()
        {

            isRecording = false;
            isLoopRecording = false;
            recordingPhase = RecordingPhase.Idle;
            DisableRecordingInputActions();
            metronome?.StopAll();
            statusMessage = $"녹화를 중지했습니다. 임시 기록에 노트 {recordedNotes.Count}개가 있습니다.";

        }

        private void DrawTimeline()
        {

            EditorGUILayout.LabelField("채보 타임라인", EditorStyles.boldLabel);

            if (chart.TempoSections.Count == 0)
            {

                EditorGUILayout.HelpBox("타임라인을 그리려면 템포 정보가 필요합니다.", MessageType.Error);
                return;

            }

            double duration = GetAuthoringDuration();
            timelineVisibleDuration = EditorGUILayout.Slider(
                "표시 범위(초)",
                timelineVisibleDuration,
                4f,
                60f);
            double maximumStart = Math.Max(0d, duration - timelineVisibleDuration);
            timelineStartTime = EditorGUILayout.Slider(
                "시작 위치",
                (float)Math.Min(timelineStartTime, maximumStart),
                0f,
                (float)Math.Max(0.001d, maximumStart));

            float height = TimelineRulerHeight + chart.MusicalParts.Count * TimelinePartHeight;
            Rect timelineRect = EditorGUILayout.GetControlRect(false, height);
            const float labelWidth = 88f;
            Rect contentRect = new(
                timelineRect.x + labelWidth,
                timelineRect.y,
                timelineRect.width - labelWidth,
                timelineRect.height);
            double visibleEnd = timelineStartTime + timelineVisibleDuration;
            EditorGUI.DrawRect(timelineRect, new Color(0.075f, 0.075f, 0.085f, 1f));
            EditorGUI.DrawRect(
                new Rect(contentRect.x, contentRect.y, contentRect.width, TimelineRulerHeight),
                new Color(0.12f, 0.12f, 0.14f, 1f));
            DrawTimelineBeatGrid(contentRect, visibleEnd);
            DrawTimelineParts(timelineRect, contentRect, visibleEnd);
            DrawTimelineLoop(contentRect, visibleEnd);
            DrawTimelinePlayhead(contentRect, visibleEnd);
            HandleTimelineInput(contentRect);

            using (new EditorGUILayout.HorizontalScope())
            {

                if (GUILayout.Button("재생 위치를 가운데로"))
                {

                    CenterTimelineOnPlayback();

                }

                GUI.enabled = chart.MusicalParts.Count > 0 && loopEnd > loopStart;

                if (GUILayout.Button("반복 구간을 선택 파트 활성 구간으로"))
                {

                    CreateActivationWindowFromLoop();

                }

                GUI.enabled = true;

            }

            EditorGUILayout.HelpBox(
                "진한 선은 마디, 옅은 선은 박입니다. 타임라인을 클릭하면 해당 위치로 이동합니다. " +
                "기존 노트는 채워진 표시, 임시 녹화 노트는 노란 테두리로 표시됩니다.",
                MessageType.Info);

        }

        private void DrawTimelineBeatGrid(Rect contentRect, double visibleEnd)
        {

            double beatTime = ChartTempoMap.GetBeatTimeAtOrBefore(
                chart.TempoSections,
                timelineStartTime);

            if (beatTime < timelineStartTime - 0.000001d)
            {

                beatTime = ChartTempoMap.GetBeatTimeAfter(chart.TempoSections, beatTime + 0.000001d);

            }

            int guard = 0;

            while (beatTime <= visibleEnd && guard++ < 2048)
            {

                ChartBeatPosition position = ChartTempoMap.GetBeatPosition(chart.TempoSections, beatTime);
                float x = TimeToTimelineX(contentRect, beatTime);
                bool barStart = position.Beat == 1;
                EditorGUI.DrawRect(
                    new Rect(x, contentRect.y, barStart ? 2f : 1f, contentRect.height),
                    barStart
                        ? new Color(0.72f, 0.72f, 0.78f, 0.55f)
                        : new Color(0.45f, 0.45f, 0.5f, 0.22f));

                if (barStart)
                {

                    GUI.Label(
                        new Rect(x + 3f, contentRect.y + 3f, 58f, 18f),
                        $"{position.Bar}마디",
                        EditorStyles.miniLabel);

                }

                beatTime = ChartTempoMap.GetBeatTimeAfter(
                    chart.TempoSections,
                    beatTime + 0.000001d);

            }

        }

        private void DrawTimelineParts(Rect timelineRect, Rect contentRect, double visibleEnd)
        {

            for (int partIndex = 0; partIndex < chart.MusicalParts.Count; partIndex++)
            {

                MusicalPartDefinition part = chart.MusicalParts[partIndex];
                float rowY = timelineRect.y + TimelineRulerHeight + partIndex * TimelinePartHeight;
                Rect labelRect = new(timelineRect.x, rowY, contentRect.x - timelineRect.x, TimelinePartHeight);
                Rect rowRect = new(contentRect.x, rowY, contentRect.width, TimelinePartHeight);
                EditorGUI.DrawRect(
                    rowRect,
                    partIndex % 2 == 0
                        ? new Color(0.095f, 0.095f, 0.11f, 0.96f)
                        : new Color(0.115f, 0.115f, 0.13f, 0.96f));
                EditorGUI.DrawRect(new Rect(labelRect.x + 4f, rowY + 8f, 5f, 26f), part.Color);
                GUI.Label(
                    new Rect(labelRect.x + 14f, rowY + 10f, labelRect.width - 16f, 22f),
                    part.DisplayName,
                    EditorStyles.miniLabel);
                DrawActivationWindows(rowRect, part.Id, part.Color, visibleEnd);
                DrawChartNotes(rowRect, part.Id, part.Color, visibleEnd);
                DrawRecordedNotesOnTimeline(rowRect, part.Id, visibleEnd);

            }

        }

        private void DrawActivationWindows(
            Rect rowRect,
            string partId,
            Color partColor,
            double visibleEnd)
        {

            for (int index = 0; index < chart.ActivationWindows.Count; index++)
            {

                MusicalPartActivationWindow window = chart.ActivationWindows[index];

                if (window.MusicalPartId != partId ||
                    window.EndTime <= timelineStartTime ||
                    window.StartTime >= visibleEnd)
                {

                    continue;

                }

                float startX = TimeToTimelineX(rowRect, Math.Max(window.StartTime, timelineStartTime));
                float endX = TimeToTimelineX(rowRect, Math.Min(window.EndTime, visibleEnd));
                Color windowColor = partColor;
                windowColor.a = 0.18f;
                EditorGUI.DrawRect(
                    new Rect(startX, rowRect.y + 3f, Math.Max(2f, endX - startX), rowRect.height - 6f),
                    windowColor);

            }

        }

        private void DrawChartNotes(
            Rect rowRect,
            string partId,
            Color partColor,
            double visibleEnd)
        {

            for (int index = 0; index < chart.Notes.Count; index++)
            {

                ChartNote note = chart.Notes[index];

                if (note.MusicalPartId != partId ||
                    note.HitTime < timelineStartTime ||
                    note.HitTime > visibleEnd)
                {

                    continue;

                }

                float x = TimeToTimelineX(rowRect, note.HitTime);
                float y = GetLaneTimelineY(rowRect, note.LaneIndex);
                EditorGUI.DrawRect(new Rect(x - 3f, y - 3f, 7f, 7f), partColor);

            }

        }

        private void DrawRecordedNotesOnTimeline(Rect rowRect, string partId, double visibleEnd)
        {

            for (int index = 0; index < recordedNotes.Count; index++)
            {

                RecordedNote note = recordedNotes[index];

                if (note.musicalPartId != partId ||
                    note.hitTime < timelineStartTime ||
                    note.hitTime > visibleEnd)
                {

                    continue;

                }

                float x = TimeToTimelineX(rowRect, note.hitTime);
                float y = GetLaneTimelineY(rowRect, note.laneIndex);
                Rect outer = new(x - 4f, y - 4f, 9f, 9f);
                EditorGUI.DrawRect(outer, new Color(1f, 0.82f, 0.2f, 1f));
                EditorGUI.DrawRect(new Rect(outer.x + 2f, outer.y + 2f, 5f, 5f), new Color(0.1f, 0.1f, 0.11f, 1f));

            }

        }

        private void DrawTimelineLoop(Rect contentRect, double visibleEnd)
        {

            if (loopEnd <= loopStart || loopEnd <= timelineStartTime || loopStart >= visibleEnd)
            {

                return;

            }

            float startX = TimeToTimelineX(contentRect, Math.Max(loopStart, timelineStartTime));
            float endX = TimeToTimelineX(contentRect, Math.Min(loopEnd, visibleEnd));
            EditorGUI.DrawRect(
                new Rect(startX, contentRect.y, Math.Max(2f, endX - startX), 3f),
                new Color(1f, 0.72f, 0.15f, 0.9f));

        }

        private void DrawTimelinePlayhead(Rect contentRect, double visibleEnd)
        {

            if (songPlayback == null)
            {

                return;

            }

            double songTime = songPlayback.SongTime;

            if (songTime < timelineStartTime || songTime > visibleEnd)
            {

                return;

            }

            float x = TimeToTimelineX(contentRect, songTime);
            EditorGUI.DrawRect(
                new Rect(x, contentRect.y, 2f, contentRect.height),
                new Color(1f, 0.25f, 0.2f, 0.95f));

        }

        private void HandleTimelineInput(Rect contentRect)
        {

            Event currentEvent = Event.current;

            if (currentEvent.type != EventType.MouseDown ||
                currentEvent.button != 0 ||
                !contentRect.Contains(currentEvent.mousePosition))
            {

                return;

            }

            double normalized = Mathf.InverseLerp(contentRect.x, contentRect.xMax, currentEvent.mousePosition.x);
            double selectedTime = timelineStartTime + normalized * timelineVisibleDuration;
            seekTime = ChartTempoMap.SnapSongTime(chart.TempoSections, selectedTime, 4);

            if (Application.isPlaying && songPlayback != null && songPlayback.IsPrepared)
            {

                Seek(seekTime);

            }

            currentEvent.Use();

        }

        private float TimeToTimelineX(Rect contentRect, double songTime)
        {

            double normalized = (songTime - timelineStartTime) / timelineVisibleDuration;
            return contentRect.x + (float)normalized * contentRect.width;

        }

        private float GetLaneTimelineY(Rect rowRect, int laneIndex)
        {

            float normalized = chart.LaneCount <= 1
                ? 0.5f
                : laneIndex / (chart.LaneCount - 1f);
            return Mathf.Lerp(rowRect.y + 9f, rowRect.yMax - 9f, normalized);

        }

        private void BeginCountInOrPreRoll()
        {

            ChartTempoSection tempo = ChartTempoMap.FindSectionForTime(
                chart.TempoSections,
                recordingTargetTime);
            double countInDuration = tempo.SecondsPerBar * countInBars;
            preRollStartTime = Math.Max(0d, recordingTargetTime - countInDuration);

            if (preRollStartTime > 0.000001d)
            {

                recordingPhase = RecordingPhase.PreRoll;
                songPlayback.Seek(preRollStartTime);
                songPlayback.Play();
                ResetSongMetronomeScheduler(preRollStartTime);
                return;

            }

            StartStandaloneCountIn();

        }

        private void StartStandaloneCountIn()
        {

            ChartTempoSection tempo = ChartTempoMap.FindSectionForTime(
                chart.TempoSections,
                recordingTargetTime);
            int clickCount = Math.Max(1, countInBars * tempo.BeatsPerBar);
            const double firstClickDelay = 0.12d;
            double firstClickRealtime = EditorApplication.timeSinceStartup + firstClickDelay;
            EnsureMetronome();
            metronome.StopAll();
            songPlayback.Stop();
            songPlayback.Seek(recordingTargetTime);

            for (int index = 0; index < clickCount; index++)
            {

                bool accent = index % tempo.BeatsPerBar == 0;
                metronome.Schedule(
                    firstClickDelay + index * tempo.SecondsPerBeat,
                    accent,
                    metronomeVolume);

            }

            countInEndRealtime = firstClickRealtime + clickCount * tempo.SecondsPerBeat;
            recordingPhase = RecordingPhase.CountIn;

        }

        private void ActivateRecording()
        {

            recordingPhase = RecordingPhase.Recording;
            isRecording = true;
            Array.Clear(lastRecordedInputTimestamps, 0, lastRecordedInputTimestamps.Length);
            EnableRecordingInputActions();

            if (!metronomeDuringRecording)
            {

                nextMetronomeSongTime = double.NaN;

            }
            else if (double.IsNaN(nextMetronomeSongTime))
            {

                ResetSongMetronomeScheduler(songPlayback.SongTime);

            }

            statusMessage = activeStartMode switch
            {
                RecordingStartMode.CurrentPosition => "현재 위치부터 녹화 중입니다.",
                RecordingStartMode.Loop => "반복 구간을 녹화 중입니다.",
                _ => "곡 처음부터 녹화 중입니다."
            };

        }

        private void RestartLoopCycle()
        {

            isRecording = false;
            DisableRecordingInputActions();

            if (preRollStartTime > 0.000001d)
            {

                recordingPhase = RecordingPhase.PreRoll;
                songPlayback.Seek(preRollStartTime);
                songPlayback.Play();
                ResetSongMetronomeScheduler(preRollStartTime);

            }
            else
            {

                StartStandaloneCountIn();

            }

        }

        private void ScheduleSongMetronome()
        {

            if (chart.TempoSections.Count == 0 || !songPlayback.IsPlaying)
            {

                return;

            }

            EnsureMetronome();
            double songTime = songPlayback.SongTime;

            if (double.IsNaN(nextMetronomeSongTime))
            {

                ResetSongMetronomeScheduler(songTime);

            }

            double scheduleLimit = songTime + MetronomeScheduleLeadSeconds;

            if (recordingPhase == RecordingPhase.PreRoll)
            {

                scheduleLimit = Math.Min(scheduleLimit, recordingTargetTime);

            }

            while (nextMetronomeSongTime <= scheduleLimit + 0.000001d)
            {

                ChartBeatPosition position = ChartTempoMap.GetBeatPosition(
                    chart.TempoSections,
                    nextMetronomeSongTime);
                double delay = Math.Max(0.025d, nextMetronomeSongTime - songTime);
                metronome.Schedule(
                    delay,
                    position.Beat == 1,
                    metronomeVolume);
                nextMetronomeSongTime = ChartTempoMap.GetBeatTimeAfter(
                    chart.TempoSections,
                    nextMetronomeSongTime + 0.000001d);

            }

        }

        private void ResetSongMetronomeScheduler(double songTime)
        {

            double beatTime = ChartTempoMap.GetBeatTimeAtOrBefore(chart.TempoSections, songTime);
            nextMetronomeSongTime = songTime - beatTime <= 0.025d
                ? beatTime
                : ChartTempoMap.GetBeatTimeAfter(chart.TempoSections, songTime);

        }

        private void EnsureMetronome()
        {

            if (metronome != null)
            {

                return;

            }

            metronome = new ChartAuthoringMetronome();
            metronome.Initialize();

        }

        private void DestroyMetronome()
        {

            if (metronome == null)
            {

                return;

            }

            metronome.Dispose();
            metronome = null;

        }

        private string GetRecordingPhaseMessage()
        {

            string partName = chart.MusicalParts[selectedPartIndex].DisplayName;

            return recordingPhase switch
            {
                RecordingPhase.CountIn =>
                    $"카운트인 · {Math.Max(0d, countInEndRealtime - EditorApplication.timeSinceStartup):0.0}초 뒤 녹화 시작",
                RecordingPhase.PreRoll =>
                    $"프리롤 · {recordingTargetTime:0.000}초부터 {partName} 녹화",
                RecordingPhase.Recording =>
                    $"녹화 중 · {partName} · 숫자키를 누르세요",
                _ => string.Empty
            };

        }

        private void RecordLane(int laneIndex, double eventTimestamp)
        {

            double hitTime = songPlayback.GetSongTimeForExternalTimestamp(
                eventTimestamp,
                InputState.currentTime);

            if (isLoopRecording && (hitTime < loopStart || hitTime >= loopEnd))
            {

                return;

            }

            RecordedNote recordedNote = new RecordedNote
            {

                hitTime = Math.Max(0d, hitTime),
                laneIndex = laneIndex,
                musicalPartId = chart.MusicalParts[selectedPartIndex].Id

            };
            recordedNotes.Add(recordedNote);
            SortRecordedNotes();
            selectedRecordedNoteIndex = recordedNotes.IndexOf(recordedNote);
            statusMessage = $"{hitTime:0.000}초에 {laneIndex + 1}번 위치를 기록했습니다.";

        }

        private void CreateRecordingInputActions()
        {

            DisposeRecordingInputActions();
            recordingInputActions = new InputAction[SupportedKeyboardLaneCount];

            for (int laneIndex = 0; laneIndex < recordingInputActions.Length; laneIndex++)
            {

                int capturedLaneIndex = laneIndex;
                InputAction action = new InputAction(
                    $"차트 녹화 위치 {laneIndex + 1}",
                    InputActionType.Button);
                action.AddBinding($"<Keyboard>/digit{laneIndex + 1}");
                action.AddBinding($"<Keyboard>/numpad{laneIndex + 1}");
                action.performed += context => RecordLaneFromInputEvent(capturedLaneIndex, context.time);
                recordingInputActions[laneIndex] = action;

            }

        }

        private void EnableRecordingInputActions()
        {

            if (recordingInputActions == null)
            {

                CreateRecordingInputActions();

            }

            int laneCount = Math.Min(chart.LaneCount, SupportedKeyboardLaneCount);

            for (int laneIndex = 0; laneIndex < recordingInputActions.Length; laneIndex++)
            {

                if (laneIndex < laneCount)
                {

                    recordingInputActions[laneIndex].Enable();

                }
                else
                {

                    recordingInputActions[laneIndex].Disable();

                }

            }

        }

        private void DisableRecordingInputActions()
        {

            if (recordingInputActions == null)
            {

                return;

            }

            for (int laneIndex = 0; laneIndex < recordingInputActions.Length; laneIndex++)
            {

                recordingInputActions[laneIndex].Disable();

            }

        }

        private void DisposeRecordingInputActions()
        {

            if (recordingInputActions == null)
            {

                return;

            }

            for (int laneIndex = 0; laneIndex < recordingInputActions.Length; laneIndex++)
            {

                recordingInputActions[laneIndex]?.Dispose();

            }

            recordingInputActions = null;

        }

        private void RecordLaneFromInputEvent(int laneIndex, double eventTimestamp)
        {

            if (!isRecording || songPlayback == null || chart == null || laneIndex >= chart.LaneCount)
            {

                return;

            }

            if (eventTimestamp <=
                lastRecordedInputTimestamps[laneIndex] + DuplicateInputThresholdSeconds)
            {

                return;

            }

            lastRecordedInputTimestamps[laneIndex] = eventTimestamp;

            RecordLane(laneIndex, eventTimestamp);
            Repaint();

        }

        private void HandleRecorderWindowKeyboardEvent(Event editorEvent)
        {

            if (!isRecording || editorEvent == null || editorEvent.type != EventType.KeyDown)
            {

                return;

            }

            int laneIndex = GetLaneIndex(editorEvent.keyCode);

            if (laneIndex < 0)
            {

                return;

            }

            RecordLaneFromInputEvent(laneIndex, InputState.currentTime);
            editorEvent.Use();

        }

        private static int GetLaneIndex(KeyCode keyCode)
        {

            switch (keyCode)
            {

                case KeyCode.Alpha1:
                case KeyCode.Keypad1:
                    return 0;
                case KeyCode.Alpha2:
                case KeyCode.Keypad2:
                    return 1;
                case KeyCode.Alpha3:
                case KeyCode.Keypad3:
                    return 2;
                case KeyCode.Alpha4:
                case KeyCode.Keypad4:
                    return 3;
                case KeyCode.Alpha5:
                case KeyCode.Keypad5:
                    return 4;
                case KeyCode.Alpha6:
                case KeyCode.Keypad6:
                    return 5;
                case KeyCode.Alpha7:
                case KeyCode.Keypad7:
                    return 6;
                case KeyCode.Alpha8:
                case KeyCode.Keypad8:
                    return 7;
                default:
                    return -1;

            }

        }

        private void ApplyRecordedNotes()
        {

            List<EditableNote> combinedNotes = new(chart.Notes.Count + recordedNotes.Count);
            HashSet<string> recordedPartIds = new();

            for (int index = 0; index < recordedNotes.Count; index++)
            {

                recordedPartIds.Add(recordedNotes[index].musicalPartId);

            }

            for (int index = 0; index < chart.Notes.Count; index++)
            {

                ChartNote note = chart.Notes[index];
                bool replace = applyMode == ApplyMode.ReplaceRecordedPartsInLoop &&
                               recordedPartIds.Contains(note.MusicalPartId) &&
                               note.HitTime >= loopStart &&
                               note.HitTime < loopEnd;

                if (replace)
                {

                    continue;

                }

                combinedNotes.Add(new EditableNote
                {

                    id = note.Id,
                    hitTime = note.HitTime,
                    laneIndex = note.LaneIndex,
                    musicalPartId = note.MusicalPartId

                });

            }

            HashSet<string> usedIds = new();

            for (int index = 0; index < combinedNotes.Count; index++)
            {

                usedIds.Add(combinedNotes[index].id);

            }

            for (int index = 0; index < recordedNotes.Count; index++)
            {

                RecordedNote recordedNote = recordedNotes[index];
                combinedNotes.Add(new EditableNote
                {

                    id = GenerateNoteId(recordedNote.musicalPartId, usedIds),
                    hitTime = recordedNote.hitTime,
                    laneIndex = recordedNote.laneIndex,
                    musicalPartId = recordedNote.musicalPartId

                });

            }

            combinedNotes.Sort((left, right) =>
            {

                int timeComparison = left.hitTime.CompareTo(right.hitTime);
                return timeComparison != 0
                    ? timeComparison
                    : string.CompareOrdinal(left.id, right.id);

            });

            Undo.IncrementCurrentGroup();
            int undoGroup = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("녹화한 차트 노트 적용");
            Undo.RecordObject(chart, "녹화한 차트 노트 적용");
            SerializedObject serializedChart = new(chart);
            SerializedProperty notesProperty = serializedChart.FindProperty("notes");
            notesProperty.arraySize = combinedNotes.Count;

            for (int index = 0; index < combinedNotes.Count; index++)
            {

                EditableNote note = combinedNotes[index];
                SerializedProperty noteProperty = notesProperty.GetArrayElementAtIndex(index);
                noteProperty.FindPropertyRelative("id").stringValue = note.id;
                noteProperty.FindPropertyRelative("hitTime").doubleValue = note.hitTime;
                noteProperty.FindPropertyRelative("laneIndex").intValue = note.laneIndex;
                noteProperty.FindPropertyRelative("musicalPartId").stringValue = note.musicalPartId;

            }

            if (addMissingActivationWindows)
            {

                AddMissingActivationWindows(serializedChart, recordedPartIds);

            }

            serializedChart.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(chart);
            Undo.CollapseUndoOperations(undoGroup);

            if (chart.TryValidate(out string error))
            {

                statusMessage = $"노트 {recordedNotes.Count}개를 적용했고 차트 검사도 통과했습니다. 에셋을 저장하세요.";

            }
            else
            {

                statusMessage = $"노트는 적용했지만 차트 검사에 실패했습니다: {error}. 수정하거나 실행 취소하세요.";
                Debug.LogError(statusMessage, chart);

            }

        }

        private void AddMissingActivationWindows(
            SerializedObject serializedChart,
            HashSet<string> recordedPartIds)
        {

            SerializedProperty windowsProperty = serializedChart.FindProperty("activationWindows");

            foreach (string partId in recordedPartIds)
            {

                double firstTime = double.PositiveInfinity;
                double lastTime = double.NegativeInfinity;
                bool needsWindow = false;

                for (int index = 0; index < recordedNotes.Count; index++)
                {

                    RecordedNote note = recordedNotes[index];

                    if (note.musicalPartId != partId)
                    {

                        continue;

                    }

                    firstTime = Math.Min(firstTime, note.hitTime);
                    lastTime = Math.Max(lastTime, note.hitTime);
                    needsWindow |= !chart.IsPartActive(partId, note.hitTime);

                }

                if (!needsWindow)
                {

                    continue;

                }

                double startTime = chart.TempoSections.Count > 0
                    ? ChartTempoMap.GetBarStartAtOrBefore(chart.TempoSections, firstTime)
                    : firstTime;
                double endTime = chart.TempoSections.Count > 0
                    ? ChartTempoMap.GetBarStartAtOrAfter(chart.TempoSections, lastTime + 0.001d)
                    : Math.Max(firstTime + 0.001d, lastTime + 0.001d);
                int newIndex = windowsProperty.arraySize;
                windowsProperty.InsertArrayElementAtIndex(newIndex);
                SerializedProperty window = windowsProperty.GetArrayElementAtIndex(newIndex);
                window.FindPropertyRelative("musicalPartId").stringValue = partId;
                window.FindPropertyRelative("startTime").doubleValue = startTime;
                window.FindPropertyRelative("endTime").doubleValue = endTime;

            }

        }

        private string GenerateNoteId(string partId, HashSet<string> usedIds)
        {

            string prefix = $"{SanitizeId(chart.name)}_{SanitizeId(partId)}_";
            int sequence = 1;
            string candidate;

            do
            {

                candidate = $"{prefix}{sequence:0000}";
                sequence++;

            }
            while (!usedIds.Add(candidate));

            return candidate;

        }

        private static string SanitizeId(string value)
        {

            if (string.IsNullOrWhiteSpace(value))
            {

                return "note";

            }

            char[] characters = value.ToLowerInvariant().ToCharArray();

            for (int index = 0; index < characters.Length; index++)
            {

                char character = characters[index];

                if (!char.IsLetterOrDigit(character) && character != '_')
                {

                    characters[index] = '_';

                }

            }

            return new string(characters);

        }

        private void ValidateChart()
        {

            if (chart.TryValidate(out string error))
            {

                statusMessage = "차트 검사를 통과했습니다.";

            }
            else
            {

                statusMessage = $"차트 검사에 실패했습니다: {error}";
                Debug.LogError(statusMessage, chart);

            }

        }

        private void ClearBuffer()
        {

            if (recordedNotes.Count > 0 &&
                !EditorUtility.DisplayDialog(
                    "임시 기록 지우기",
                    "현재 임시 기록에 있는 노트를 모두 지울까요?",
                    "모두 지우기",
                    "취소"))
            {

                return;

            }

            recordedNotes.Clear();
            selectedRecordedNoteIndex = -1;
            statusMessage = "임시 기록을 지웠습니다. 차트 원본은 바뀌지 않았습니다.";

        }

        private void Seek(double time)
        {

            DisableGameplaySessionForAuthoring();

            if (songPlayback == null || !songPlayback.Seek(Math.Max(0d, time)))
            {

                statusMessage = "FMOD가 재생 위치 이동을 처리하지 못했습니다.";

            }

        }

        private void ChangeChart(PrototypeChart selectedChart)
        {

            if (chart == selectedChart)
            {

                return;

            }

            if (recordingPhase != RecordingPhase.Idle)
            {

                StopRecording();

            }

            chart = selectedChart;
            configuredEventPath = string.Empty;
            recordedNotes.Clear();
            selectedRecordedNoteIndex = -1;
            timelineStartTime = 0d;
            statusMessage = chart == null
                ? "차트를 선택하세요."
                : $"'{chart.name}' 차트를 선택했습니다.";

            if (Application.isPlaying)
            {

                RefreshSongPlayback();

            }

        }

        private void SetLoopFromBars()
        {

            if (chart.TempoSections.Count == 0)
            {

                return;

            }

            loopStartBar = Math.Max(1, loopStartBar);
            loopEndBar = Math.Max(loopStartBar + 1, loopEndBar);
            loopStart = ChartTempoMap.GetSongTime(chart.TempoSections, loopStartBar, 1);
            loopEnd = ChartTempoMap.GetSongTime(chart.TempoSections, loopEndBar, 1);
            timelineStartTime = Math.Max(0d, loopStart - chart.TempoSections[0].SecondsPerBar);
            statusMessage = $"{loopStartBar}마디부터 {loopEndBar}마디 직전까지 반복 구간을 설정했습니다.";

        }

        private void SnapLoopToBars()
        {

            if (chart.TempoSections.Count == 0 || loopEnd <= loopStart)
            {

                return;

            }

            loopStart = ChartTempoMap.GetBarStartAtOrBefore(chart.TempoSections, loopStart);
            loopEnd = ChartTempoMap.GetBarStartAtOrAfter(chart.TempoSections, loopEnd);
            ChartBeatPosition start = ChartTempoMap.GetBeatPosition(chart.TempoSections, loopStart);
            ChartBeatPosition end = ChartTempoMap.GetBeatPosition(chart.TempoSections, loopEnd);
            loopStartBar = start.Bar;
            loopEndBar = end.Bar;
            statusMessage = $"반복 구간을 {loopStartBar}~{loopEndBar - 1}마디 경계에 맞췄습니다.";

        }

        private void CreateActivationWindowFromLoop()
        {

            if (chart.MusicalParts.Count == 0 || chart.TempoSections.Count == 0 || loopEnd <= loopStart)
            {

                return;

            }

            double startTime = ChartTempoMap.GetBarStartAtOrBefore(chart.TempoSections, loopStart);
            double endTime = ChartTempoMap.GetBarStartAtOrAfter(chart.TempoSections, loopEnd);
            string partId = chart.MusicalParts[selectedPartIndex].Id;
            Undo.RecordObject(chart, "Add musical-part activation window");
            SerializedObject serializedChart = new(chart);
            SerializedProperty windows = serializedChart.FindProperty("activationWindows");
            int index = windows.arraySize;
            windows.InsertArrayElementAtIndex(index);
            SerializedProperty window = windows.GetArrayElementAtIndex(index);
            window.FindPropertyRelative("musicalPartId").stringValue = partId;
            window.FindPropertyRelative("startTime").doubleValue = startTime;
            window.FindPropertyRelative("endTime").doubleValue = endTime;
            serializedChart.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(chart);
            ChartBeatPosition startPosition = ChartTempoMap.GetBeatPosition(chart.TempoSections, startTime);
            ChartBeatPosition endPosition = ChartTempoMap.GetBeatPosition(chart.TempoSections, endTime);
            statusMessage =
                $"{partId} 활성 구간을 {startPosition.Bar}~{endPosition.Bar - 1}마디로 추가했습니다.";

        }

        private void CenterTimelineOnPlayback()
        {

            if (songPlayback == null)
            {

                return;

            }

            double maximumStart = Math.Max(0d, GetAuthoringDuration() - timelineVisibleDuration);
            timelineStartTime = Math.Max(
                0d,
                Math.Min(
                    maximumStart,
                    songPlayback.SongTime - timelineVisibleDuration * 0.5d));

        }

        private double GetAuthoringDuration()
        {

            if (songPlayback != null && songPlayback.DurationSeconds > 0d)
            {

                return songPlayback.DurationSeconds;

            }

            return Math.Max(60d, Math.Max(chart.Duration, loopEnd));

        }

        private void DisableGameplaySessionForAuthoring()
        {

            GameplaySession gameplaySession = FindAnyObjectByType<GameplaySession>();

            if (gameplaySession != null && gameplaySession.enabled)
            {

                gameplaySession.StopAllCoroutines();
                gameplaySession.enabled = false;
                statusMessage =
                    "차트 작업을 위해 게임플레이 입력과 노트 생성을 멈췄습니다. " +
                    "차트를 적용한 뒤 플레이 모드를 다시 시작하세요.";

            }

        }

        private void RefreshSongPlayback()
        {

            if (!Application.isPlaying)
            {

                return;

            }

            if (songPlayback == null)
            {

                songPlayback = FindAnyObjectByType<FmodSongPlayback>();

            }

            if (songPlayback == null || chart == null || configuredEventPath == chart.SongEventPath)
            {

                return;

            }

            DisableGameplaySessionForAuthoring();
            songPlayback.ConfigureEventPath(chart.SongEventPath, false);
            songPlayback.ConfigureStemParameters(chart.StemParameters);
            songPlayback.Prepare();
            configuredEventPath = chart.SongEventPath;
            statusMessage = $"'{chart.SongEventPath}' 이벤트를 채보 제작용으로 준비하고 있습니다.";

        }

        private void SetAllStemVolumes(float volume)
        {

            for (int index = 0; index < chart.StemParameters.Count; index++)
            {

                songPlayback.SetStemVolume(chart.StemParameters[index].StemId, volume);

            }

        }

        private void SoloSelectedPart()
        {

            string selectedPartId = chart.MusicalParts[selectedPartIndex].Id;

            for (int index = 0; index < chart.StemParameters.Count; index++)
            {

                string stemId = chart.StemParameters[index].StemId;
                songPlayback.SetStemVolume(stemId, stemId == selectedPartId ? 1f : 0f);

            }

        }

        private void NudgeSelected(double deltaSeconds)
        {

            if (selectedRecordedNoteIndex < 0 || selectedRecordedNoteIndex >= recordedNotes.Count)
            {

                return;

            }

            RecordedNote note = recordedNotes[selectedRecordedNoteIndex];
            note.hitTime = Math.Max(0d, note.hitTime + deltaSeconds);
            SortRecordedNotes();

        }

        private void SortRecordedNotes()
        {

            RecordedNote selectedNote = selectedRecordedNoteIndex >= 0 && selectedRecordedNoteIndex < recordedNotes.Count
                ? recordedNotes[selectedRecordedNoteIndex]
                : null;
            recordedNotes.Sort((left, right) => left.hitTime.CompareTo(right.hitTime));
            selectedRecordedNoteIndex = selectedNote == null ? -1 : recordedNotes.IndexOf(selectedNote);

        }

        private string[] GetPartNames()
        {

            string[] names = new string[chart.MusicalParts.Count];

            for (int index = 0; index < names.Length; index++)
            {

                MusicalPartDefinition part = chart.MusicalParts[index];
                names[index] = $"{part.DisplayName} [{part.Id}]";

            }

            return names;

        }

        private int FindPartIndex(string partId)
        {

            for (int index = 0; index < chart.MusicalParts.Count; index++)
            {

                if (chart.MusicalParts[index].Id == partId)
                {

                    return index;

                }

            }

            return 0;

        }

        private void ClampSelections()
        {

            selectedPartIndex = Mathf.Clamp(selectedPartIndex, 0, Math.Max(0, chart.MusicalParts.Count - 1));
            selectedRecordedNoteIndex = Mathf.Clamp(
                selectedRecordedNoteIndex,
                -1,
                recordedNotes.Count - 1);

        }

    }

}
