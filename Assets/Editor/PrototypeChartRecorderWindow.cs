using System;
using System.Collections.Generic;
using IdiotTape.Audio;
using IdiotTape.Gameplay;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.Serialization;

namespace IdiotTape.EditorTools
{

    public sealed class PrototypeChartRecorderWindow : EditorWindow
    {

        private const string GameplayScenePath = "Assets/Scenes/Gameplay.unity";
        private const int SupportedKeyboardLaneCount = 8;
        private const double DuplicateInputThresholdSeconds = 0.010d;
        private const double DuplicateTempoTapThresholdSeconds = 0.050d;
        private const double ScheduleToleranceSeconds = 0.000001d;
        private const double MetronomeScheduleLeadSeconds = 0.4d;
        private const float TimelineRulerHeight = 26f;
        private const float TimelinePartHeight = 42f;
        private const float VerticalTimelineRulerWidth = 64f;
        private const float VerticalTimelineHeaderHeight = 34f;
        private const float TimelineNoteHitRadius = 8f;
        private const float TempoAnchorBarFieldWidth = 84f;
        private const float TempoAnchorBeatFieldWidth = 84f;
        private const float TempoAnchorTimeFieldWidth = 132f;
        private static readonly string[] QuantizationGridNames =
        {

            "1/8음표",
            "1/8 셋잇단음",
            "1/16음표",
            "1/16 셋잇단음",
            "1/32음표"

        };
        private static readonly int[] QuantizationGridValues = { 2, 3, 4, 6, 8 };

        private enum TimelineViewMode
        {

            Horizontal,
            Vertical

        }

        private enum QuantizationGrid
        {

            Eighth = 2,
            EighthTriplet = 3,
            Sixteenth = 4,
            SixteenthTriplet = 6,
            ThirtySecond = 8

        }

        private enum ApplyMode
        {

            Append,
            ReplaceRecordedPartsInLoop

        }

        private enum AppliedQuantizationRange
        {

            WholeChart,
            CurrentLoop

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

        private enum RecordingNoteMode
        {

            Tap,
            SlideAndHold,
            Flick,
            Banana

        }

        private enum FlickDefaultDirection
        {

            Left = -1,
            Right = 1

        }

        [Serializable]
        private sealed class RecordedNote
        {

            public double hitTime;
            public double originalHitTime;
            public int laneIndex;
            public string musicalPartId;
            public bool hasOriginalHitTime;
            public bool pendingAutomaticQuantization;
            public ChartNoteAuthoringData noteData;

        }

        private sealed class EditableNote
        {

            public string id;
            public double hitTime;
            public int laneIndex;
            public string musicalPartId;
            public ChartNoteAuthoringData fullData;

        }

        [SerializeField] private PrototypeChart chart;
        [SerializeField] private List<RecordedNote> recordedNotes = new();
        [SerializeField] private RecordingNoteMode recordingNoteMode;
        [SerializeField] private FlickDefaultDirection flickDefaultDirection =
            FlickDefaultDirection.Right;
        [SerializeField] private ChartNoteAuthoringData pendingInteraction;
        [SerializeField] private int draggedSlidePointIndex = int.MinValue;
        [SerializeField] private int selectedPartIndex;
        [SerializeField] private int selectedRecordedNoteIndex = -1;
        [SerializeField] private string selectedChartNoteId = string.Empty;
        [SerializeField] private double seekTime;
        [SerializeField] private double loopStart;
        [SerializeField] private double loopEnd = 8d;
        [SerializeField] private ApplyMode applyMode;
        [SerializeField] private bool addMissingActivationWindows;
        [SerializeField, Min(1)] private int countInBars = 2;
        [SerializeField, Range(0f, 1f)] private float metronomeVolume = 0.15f;
        [SerializeField, Range(-50f, 50f)] private float metronomeOutputOffsetMilliseconds;
        [SerializeField] private bool metronomeDuringRecording;
        [SerializeField, Min(4f)] private float timelineVisibleDuration = 16f;
        [SerializeField, Min(0f)] private double timelineStartTime;
        [SerializeField, Min(1)] private int loopStartBar = 1;
        [SerializeField, Min(2)] private int loopEndBar = 5;
        [SerializeField, Min(1)] private int loopBarCount = 4;
        [SerializeField] private TimelineViewMode timelineViewMode;
        [FormerlySerializedAs("verticalTimelineAutoScroll")]
        [SerializeField] private bool timelineAutoScroll = true;
        [SerializeField, Range(360f, 900f)] private float verticalTimelineHeight = 560f;
        [SerializeField] private bool automaticQuantization;
        [SerializeField] private QuantizationGrid quantizationGrid = QuantizationGrid.Sixteenth;
        [SerializeField, Range(0f, 100f)] private float maximumQuantizationMilliseconds = 60f;
        [SerializeField, Range(0f, 1f)] private float quantizationStrength = 1f;
        [SerializeField, Range(-200f, 200f)] private float inputAdvanceMilliseconds;
        [SerializeField, Range(0f, 80f)] private float chordGroupingMilliseconds = 25f;
        [SerializeField] private bool clearBufferAfterApply = true;
        [SerializeField] private bool bufferWasApplied;
        [SerializeField] private bool showTempoCalibration = true;
        [SerializeField] private bool showAdvancedNavigation;
        [SerializeField] private bool showQuantizationSettings;
        [SerializeField] private bool showAppliedNoteQuantization;
        [SerializeField] private AppliedQuantizationRange appliedQuantizationRange;
        [SerializeField] private bool showPatternDuplication;
        [SerializeField, Min(1)] private int patternTargetStartBar = 2;
        [SerializeField, Min(1)] private int patternRepeatCount = 1;
        [SerializeField] private ChartPatternConflictMode patternConflictMode;
        [SerializeField] private bool patternAddActivationWindow = true;
        [SerializeField] private bool showStemMixer;
        [SerializeField, Min(1f)] private float noteNudgeMilliseconds = 5f;
        [SerializeField, Min(1)] private int tempoAnchorABar = 17;
        [SerializeField, Min(1)] private int tempoAnchorABeat = 1;
        [SerializeField, Min(0f)] private double tempoAnchorATime;
        [SerializeField] private bool hasTempoAnchorA;
        [SerializeField, Min(1)] private int tempoAnchorBBar = 81;
        [SerializeField, Min(1)] private int tempoAnchorBBeat = 1;
        [SerializeField, Min(0f)] private double tempoAnchorBTime;
        [SerializeField] private bool hasTempoAnchorB;
        [SerializeField, Range(-250f, 250f)] private float tempoCalibrationFineOffsetMilliseconds;
        [SerializeField, Min(1)] private int tempoTapStartBar = 1;
        [SerializeField, Min(1)] private int tempoTapBarInterval = 1;

        private readonly double[] lastRecordedInputTimestamps = new double[SupportedKeyboardLaneCount];
        private readonly List<ChartTempoAnchor> tempoTapAnchors = new();
        private InputAction[] recordingInputActions;
        private InputAction terminalFlickInputAction;
        private InputAction tempoTapInputAction;
        private Vector2 windowScrollPosition;
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
        private bool tempoCalibrationPreview;
        private double previewBeatsPerMinute;
        private double previewFirstDownbeatTime;
        private int previewBeatsPerBar;
        private int previewBeatUnit;
        private double nextPreviewMetronomeSongTime = double.NaN;
        private bool tempoTapCapture;
        private bool tempoTapSpacePressed;
        private double lastTempoTapInputTimestamp = double.NegativeInfinity;
        private bool tempoAnchorsFromTapCapture;
        private ChartTempoCalibrationResult tempoTapResult;
        private ChartPatternDuplicationPreview patternDuplicationPreview;
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
            CreateTempoTapInputAction();
            InputSystem.onEvent += OnInputSystemEvent;
            EditorApplication.update += EditorUpdate;

        }

        private void OnDisable()
        {

            EditorApplication.update -= EditorUpdate;
            InputSystem.onEvent -= OnInputSystemEvent;
            isRecording = false;
            isLoopRecording = false;
            recordingPhase = RecordingPhase.Idle;
            pendingInteraction = null;
            tempoCalibrationPreview = false;
            tempoTapCapture = false;
            DestroyMetronome();
            DisposeRecordingInputActions();
            DisposeTempoTapInputAction();

        }

        private void OnGUI()
        {

            HandleRecorderWindowKeyboardEvent(Event.current);
            windowScrollPosition = EditorGUILayout.BeginScrollView(windowScrollPosition);
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
                EditorGUILayout.EndScrollView();
                return;

            }

            ClampSelections();
            DrawSceneControls();
            EditorGUILayout.Space(8f);
            DrawPlaybackControls();
            EditorGUILayout.Space(8f);
            DrawTempoCalibration();
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
            EditorGUILayout.EndScrollView();

            if (selectedRecordedNoteIndex >= 0 &&
                selectedRecordedNoteIndex < recordedNotes.Count)
            {

                DrawSelectedRecordedInteractionDetails(recordedNotes[selectedRecordedNoteIndex]);

            }

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
                ChartTempoSection activeTempo = ChartTempoMap.FindSectionForTime(
                    chart.TempoSections,
                    songPlayback.SongTime);
                EditorGUILayout.LabelField(
                    "음악 위치",
                    $"{position.Bar}마디 {position.Beat}박 · {activeTempo.BeatsPerMinute:0.###} BPM");

            }

            using (new EditorGUILayout.HorizontalScope())
            {

                if (GUILayout.Button("처음으로"))
                {

                    if (recordingPhase != RecordingPhase.Idle)
                    {

                        StopRecording();

                    }

                    DisableGameplaySessionForAuthoring();
                    songPlayback.Restart();

                }

                if (GUILayout.Button(songPlayback.IsPaused ? "계속 재생" : "일시정지"))
                {

                    if (recordingPhase != RecordingPhase.Idle)
                    {

                        StopRecording();

                    }

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

                    if (recordingPhase != RecordingPhase.Idle)
                    {

                        StopRecording();

                    }

                    StopTempoCalibrationPreview();
                    DisableGameplaySessionForAuthoring();
                    songPlayback.Stop();

                }

            }

            using (new EditorGUILayout.HorizontalScope())
            {

                loopStartBar = Math.Max(1, EditorGUILayout.IntField("시작 마디", loopStartBar));
                loopBarCount = Math.Max(1, EditorGUILayout.IntField("마디 수", loopBarCount));
                loopEndBar = loopStartBar + loopBarCount;

                if (GUILayout.Button("루프 설정", GUILayout.Width(88f)))
                {

                    SetLoopFromBars();

                }

            }

            showAdvancedNavigation = EditorGUILayout.Foldout(
                showAdvancedNavigation,
                "고급 탐색 · 초 단위 루프",
                true);

            if (showAdvancedNavigation)
            {

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

                if (GUILayout.Button("초 단위 반복 구간을 마디 경계에 맞춤"))
                {

                    SnapLoopToBars();

                }

            }

            if (loopEnd <= loopStart)
            {

                EditorGUILayout.HelpBox("반복 끝은 반복 시작보다 뒤여야 합니다.", MessageType.Error);

            }

        }

        private void DrawTempoCalibration()
        {

            showTempoCalibration = EditorGUILayout.Foldout(
                showTempoCalibration,
                "박자 설정 · 두 앵커 캘리브레이션",
                true);

            if (!showTempoCalibration)
            {

                return;

            }

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {

                if (chart.TempoSections.Count == 0)
                {

                    EditorGUILayout.HelpBox("캘리브레이션하려면 템포 섹션이 필요합니다.", MessageType.Error);
                    return;

                }

                ChartTempoSection currentTempo = chart.TempoSections[0];
                EditorGUILayout.LabelField(
                    "현재 기준",
                    $"1마디 1박 {currentTempo.StartTime:0.000000}초 · " +
                    $"{currentTempo.BeatsPerMinute:0.######} BPM · " +
                    $"{currentTempo.BeatsPerBar}/{currentTempo.BeatUnit}");
                EditorGUILayout.HelpBox(
                    "서로 멀리 떨어진 확실한 다운비트 두 곳을 지정하세요. 절대 노트 시간은 변경되지 않습니다.",
                    MessageType.None);
                DrawTempoTapCalibration();

                DrawTempoAnchor(
                    "기준 A",
                    ref tempoAnchorABar,
                    ref tempoAnchorABeat,
                    ref tempoAnchorATime,
                    ref hasTempoAnchorA,
                    currentTempo.BeatsPerBar);
                DrawTempoAnchor(
                    "기준 B",
                    ref tempoAnchorBBar,
                    ref tempoAnchorBBeat,
                    ref tempoAnchorBTime,
                    ref hasTempoAnchorB,
                    currentTempo.BeatsPerBar);

                ChartTempoCalibrationResult result = GetTempoCalibrationResult();

                if (!result.IsValid)
                {

                    if (hasTempoAnchorA && hasTempoAnchorB)
                    {

                        EditorGUILayout.HelpBox(result.Error, MessageType.Error);

                    }

                    return;

                }

                tempoCalibrationFineOffsetMilliseconds = EditorGUILayout.Slider(
                    "전체 박자 미세 이동(ms)",
                    tempoCalibrationFineOffsetMilliseconds,
                    -250f,
                    250f);
                double adjustedFirstDownbeat =
                    result.FirstDownbeatTime + tempoCalibrationFineOffsetMilliseconds / 1000d;
                double bpmDifference = result.BeatsPerMinute - currentTempo.BeatsPerMinute;
                double downbeatDifferenceMilliseconds =
                    (adjustedFirstDownbeat - currentTempo.StartTime) * 1000d;
                EditorGUILayout.LabelField(
                    "계산 결과",
                    $"{result.BeatsPerMinute:0.######} BPM · 1마디 1박 {adjustedFirstDownbeat:0.000000}초");
                EditorGUILayout.LabelField(
                    "현재 설정과 차이",
                    $"BPM {bpmDifference:+0.######;-0.######;0} · " +
                    $"박자 원점 {downbeatDifferenceMilliseconds:+0.0;-0.0;0.0}ms");
                EditorGUILayout.LabelField(
                    "측정 간격",
                    $"{result.BeatDistance}박 · {result.TimeDistance:0.000}초");

                if (songPlayback != null && songPlayback.IsPrepared)
                {

                    double nearestBeat = GetNearestCalibrationBeatTime(
                        songPlayback.SongTime,
                        adjustedFirstDownbeat,
                        result.BeatsPerMinute,
                        currentTempo.BeatUnit);
                    double currentErrorMilliseconds = (songPlayback.SongTime - nearestBeat) * 1000d;
                    EditorGUILayout.LabelField(
                        "현재 위치와 가까운 계산 박자",
                        $"{currentErrorMilliseconds:+0.0;-0.0;0.0}ms");

                }

                using (new EditorGUILayout.HorizontalScope())
                {

                    GUI.enabled = Application.isPlaying && songPlayback != null && songPlayback.IsPrepared;

                    if (GUILayout.Button(tempoCalibrationPreview ? "미리 듣기 중지" : "계산 박자로 메트로놈 미리 듣기"))
                    {

                        if (tempoCalibrationPreview)
                        {

                            StopTempoCalibrationPreview();

                        }
                        else
                        {

                            StartTempoCalibrationPreview(result, adjustedFirstDownbeat, currentTempo);

                        }

                    }

                    GUI.enabled = adjustedFirstDownbeat >= 0d && chart.TempoSections.Count == 1;

                    if (GUILayout.Button("템포 맵에 적용"))
                    {

                        ApplyTempoCalibration(result, adjustedFirstDownbeat);

                    }

                    GUI.enabled = true;

                }

                if (chart.TempoSections.Count > 1)
                {

                    EditorGUILayout.HelpBox(
                        "현재 캘리브레이션 적용은 단일 템포 섹션 차트만 지원합니다.",
                        MessageType.Warning);

                }

            }

        }

        private void DrawTempoTapCalibration()
        {

            EditorGUILayout.Space(6f);
            EditorGUILayout.LabelField("여러 마디 다운비트 연속 측정", EditorStyles.miniBoldLabel);
            EditorGUILayout.HelpBox(
                "시작 마디를 지정한 뒤 음악을 들으며 각 마디의 1박에 스페이스바를 누르세요. " +
                "두 번째 입력부터 모든 탭을 함께 계산해 BPM과 박자 원점을 계속 갱신합니다.",
                MessageType.None);

            using (new EditorGUILayout.HorizontalScope())
            {

                tempoTapStartBar = Math.Max(1, EditorGUILayout.IntField("시작 마디", tempoTapStartBar));
                tempoTapBarInterval = Math.Max(
                    1,
                    EditorGUILayout.IntField("탭 간격(마디)", tempoTapBarInterval));

            }

            using (new EditorGUILayout.HorizontalScope())
            {

                GUI.enabled = Application.isPlaying && songPlayback != null && songPlayback.IsPrepared;

                if (GUILayout.Button(tempoTapCapture ? "연속 측정 중지" : "연속 측정 시작"))
                {

                    if (tempoTapCapture)
                    {

                        StopTempoTapCapture();
                        statusMessage = $"다운비트 연속 측정을 마쳤습니다. 앵커 {tempoTapAnchors.Count}개를 사용했습니다.";

                    }
                    else
                    {

                        StartTempoTapCapture();

                    }

                }

                GUI.enabled = tempoTapAnchors.Count > 0;

                if (GUILayout.Button("탭 측정 지우기"))
                {

                    ClearTempoTapCapture();

                }

                GUI.enabled = true;

            }

            if (tempoTapCapture)
            {

                int nextBar = tempoTapStartBar + tempoTapAnchors.Count * tempoTapBarInterval;
                EditorGUILayout.HelpBox(
                    $"측정 중 · {nextBar}마디 1박에서 스페이스바를 누르세요.",
                    MessageType.Info);

            }

            if (tempoTapAnchors.Count > 0)
            {

                EditorGUILayout.LabelField(
                    "연속 탭",
                    $"{tempoTapAnchors.Count}개 · " +
                    $"{tempoTapAnchors[0].Bar}~{tempoTapAnchors[^1].Bar}마디");

            }

            if (tempoTapResult.IsValid)
            {

                EditorGUILayout.LabelField(
                    "다중 앵커 오차",
                    $"RMS {tempoTapResult.RootMeanSquareError * 1000d:0.0}ms · " +
                    $"{tempoTapResult.AnchorCount}개 앵커 회귀");

            }

        }

        private void StartTempoTapCapture()
        {

            if (songPlayback == null || !songPlayback.IsPrepared || chart.TempoSections.Count == 0)
            {

                return;

            }

            if (recordingPhase != RecordingPhase.Idle)
            {

                StopRecording();

            }

            DisableGameplaySessionForAuthoring();
            StopTempoCalibrationPreview();
            tempoTapAnchors.Clear();
            tempoTapResult = default;
            tempoTapCapture = true;
            tempoTapSpacePressed = false;
            lastTempoTapInputTimestamp = double.NegativeInfinity;

            if (tempoTapInputAction == null)
            {

                CreateTempoTapInputAction();

            }

            tempoTapInputAction?.Enable();
            tempoTapStartBar = Math.Max(1, tempoTapStartBar);
            tempoTapBarInterval = Math.Max(1, tempoTapBarInterval);
            tempoCalibrationFineOffsetMilliseconds = 0f;

            if (songPlayback.IsPaused)
            {

                songPlayback.Resume();

            }
            else if (!songPlayback.IsPlaying)
            {

                songPlayback.Play();

            }

            statusMessage = $"다운비트 연속 측정을 시작했습니다. {tempoTapStartBar}마디 1박에서 스페이스바를 누르세요.";

        }

        private void CaptureTempoDownbeat(double eventTimestamp)
        {

            if (!tempoTapCapture || songPlayback == null || !songPlayback.IsPlaying)
            {

                statusMessage = "다운비트 탭은 음악이 재생 중일 때만 기록됩니다.";
                return;

            }

            ChartTempoSection currentTempo = chart.TempoSections[0];
            int bar = tempoTapStartBar + tempoTapAnchors.Count * tempoTapBarInterval;
            double songTime = songPlayback.GetSongTimeForExternalTimestamp(
                eventTimestamp,
                InputState.currentTime);
            ChartTempoAnchor anchor = new(bar, 1, songTime);
            tempoTapAnchors.Add(anchor);

            if (tempoTapAnchors.Count < 2)
            {

                statusMessage = $"{bar}마디 1박을 {anchor.SongTime:0.000000}초에 기록했습니다.";
                return;

            }

            ChartTempoCalibrationResult result = ChartTempoCalibration.Calculate(
                tempoTapAnchors,
                currentTempo.BeatsPerBar,
                currentTempo.BeatUnit);

            if (!result.IsValid)
            {

                tempoTapAnchors.RemoveAt(tempoTapAnchors.Count - 1);
                statusMessage = result.Error;
                return;

            }

            tempoTapResult = result;
            ChartTempoAnchor firstTap = tempoTapAnchors[0];
            ChartTempoAnchor lastTap = tempoTapAnchors[^1];
            tempoAnchorABar = firstTap.Bar;
            tempoAnchorABeat = 1;
            tempoAnchorATime = ChartTempoCalibration.GetExpectedSongTime(
                result.FirstDownbeatTime,
                result.BeatsPerMinute,
                currentTempo.BeatsPerBar,
                currentTempo.BeatUnit,
                firstTap.Bar,
                1);
            tempoAnchorBBar = lastTap.Bar;
            tempoAnchorBBeat = 1;
            tempoAnchorBTime = ChartTempoCalibration.GetExpectedSongTime(
                result.FirstDownbeatTime,
                result.BeatsPerMinute,
                currentTempo.BeatsPerBar,
                currentTempo.BeatUnit,
                lastTap.Bar,
                1);
            hasTempoAnchorA = true;
            hasTempoAnchorB = true;
            tempoAnchorsFromTapCapture = true;
            statusMessage =
                $"{tempoTapAnchors.Count}개 다운비트로 {result.BeatsPerMinute:0.######} BPM을 계산했습니다.";

        }

        private void TryCaptureTempoDownbeat(double eventTimestamp)
        {

            if (eventTimestamp <=
                lastTempoTapInputTimestamp + DuplicateTempoTapThresholdSeconds)
            {

                return;

            }

            lastTempoTapInputTimestamp = eventTimestamp;
            CaptureTempoDownbeat(eventTimestamp);
            Repaint();

        }

        private void ClearTempoTapCapture()
        {

            StopTempoTapCapture();
            tempoTapAnchors.Clear();
            tempoTapResult = default;

            if (tempoAnchorsFromTapCapture)
            {

                hasTempoAnchorA = false;
                hasTempoAnchorB = false;
                tempoAnchorsFromTapCapture = false;

            }

            statusMessage = "다운비트 연속 측정을 지웠습니다.";

        }

        private void DrawTempoAnchor(
            string label,
            ref int bar,
            ref int beat,
            ref double songTime,
            ref bool hasAnchor,
            int beatsPerBar)
        {

            EditorGUILayout.Space(3f);
            EditorGUILayout.LabelField(label, EditorStyles.miniBoldLabel);

            using (new EditorGUILayout.HorizontalScope())
            {

                GUILayout.Label(
                    "마디",
                    EditorStyles.centeredGreyMiniLabel,
                    GUILayout.Width(TempoAnchorBarFieldWidth));
                GUILayout.Label(
                    "박",
                    EditorStyles.centeredGreyMiniLabel,
                    GUILayout.Width(TempoAnchorBeatFieldWidth));
                GUILayout.Label(
                    "시간(초)",
                    EditorStyles.centeredGreyMiniLabel,
                    GUILayout.Width(TempoAnchorTimeFieldWidth));

            }

            using (new EditorGUILayout.HorizontalScope())
            {

                EditorGUI.BeginChangeCheck();
                int nextBar = Math.Max(
                    1,
                    EditorGUILayout.IntField(bar, GUILayout.Width(TempoAnchorBarFieldWidth)));
                int nextBeat = Mathf.Clamp(
                    EditorGUILayout.IntField(beat, GUILayout.Width(TempoAnchorBeatFieldWidth)),
                    1,
                    Math.Max(1, beatsPerBar));
                double nextSongTime = Math.Max(
                    0d,
                    EditorGUILayout.DoubleField(songTime, GUILayout.Width(TempoAnchorTimeFieldWidth)));

                if (EditorGUI.EndChangeCheck())
                {

                    bar = nextBar;
                    beat = nextBeat;
                    songTime = nextSongTime;
                    hasAnchor = true;
                    MarkTempoAnchorManuallyEdited();

                }

                GUI.enabled = Application.isPlaying && songPlayback != null && songPlayback.IsPrepared;

                if (GUILayout.Button("현재 위치 지정", GUILayout.Width(96f)))
                {

                    songTime = songPlayback.SongTime;
                    hasAnchor = true;
                    MarkTempoAnchorManuallyEdited();
                    tempoCalibrationPreview = false;
                    metronome?.StopAll();

                }

                GUI.enabled = hasAnchor && Application.isPlaying && songPlayback != null && songPlayback.IsPrepared;

                if (GUILayout.Button("이동", GUILayout.Width(44f)))
                {

                    Seek(songTime);

                }

                GUI.enabled = true;

            }

            using (new EditorGUILayout.HorizontalScope())
            {

                GUILayout.Space(TempoAnchorBarFieldWidth + TempoAnchorBeatFieldWidth + 8f);

                if (GUILayout.Button("-10ms"))
                {

                    songTime = Math.Max(0d, songTime - 0.010d);
                    hasAnchor = true;
                    MarkTempoAnchorManuallyEdited();

                }

                if (GUILayout.Button("-1ms"))
                {

                    songTime = Math.Max(0d, songTime - 0.001d);
                    hasAnchor = true;
                    MarkTempoAnchorManuallyEdited();

                }

                if (GUILayout.Button("+1ms"))
                {

                    songTime += 0.001d;
                    hasAnchor = true;
                    MarkTempoAnchorManuallyEdited();

                }

                if (GUILayout.Button("+10ms"))
                {

                    songTime += 0.010d;
                    hasAnchor = true;
                    MarkTempoAnchorManuallyEdited();

                }

                GUILayout.Label(hasAnchor ? "설정됨" : "미설정", GUILayout.Width(52f));

            }

        }

        private void MarkTempoAnchorManuallyEdited()
        {

            StopTempoTapCapture();
            tempoTapAnchors.Clear();
            tempoTapResult = default;
            tempoAnchorsFromTapCapture = false;

        }

        private ChartTempoCalibrationResult GetTempoCalibrationResult()
        {

            if (!hasTempoAnchorA || !hasTempoAnchorB || chart.TempoSections.Count == 0)
            {

                return new ChartTempoCalibrationResult(
                    false,
                    "두 앵커를 모두 지정하세요.",
                    0d,
                    0d,
                    0,
                    0d);

            }

            ChartTempoSection tempo = chart.TempoSections[0];
            return ChartTempoCalibration.Calculate(
                new ChartTempoAnchor(tempoAnchorABar, tempoAnchorABeat, tempoAnchorATime),
                new ChartTempoAnchor(tempoAnchorBBar, tempoAnchorBBeat, tempoAnchorBTime),
                tempo.BeatsPerBar,
                tempo.BeatUnit);

        }

        private void ApplyTempoCalibration(
            ChartTempoCalibrationResult result,
            double adjustedFirstDownbeat)
        {

            if (!result.IsValid || adjustedFirstDownbeat < 0d || chart.TempoSections.Count != 1)
            {

                return;

            }

            ChartTempoSection currentTempo = chart.TempoSections[0];
            string message =
                $"BPM {currentTempo.BeatsPerMinute:0.######} → {result.BeatsPerMinute:0.######}\n" +
                $"1마디 1박 {currentTempo.StartTime:0.000000}초 → {adjustedFirstDownbeat:0.000000}초\n\n" +
                "기존 노트의 절대 판정 시간은 변경되지 않습니다.";

            if (!EditorUtility.DisplayDialog("템포 캘리브레이션 적용", message, "적용", "취소"))
            {

                return;

            }

            StopTempoCalibrationPreview();
            Undo.RecordObject(chart, "템포 캘리브레이션 적용");
            SerializedObject serializedChart = new(chart);
            SerializedProperty tempoSections = serializedChart.FindProperty("tempoSections");
            SerializedProperty firstSection = tempoSections.GetArrayElementAtIndex(0);
            firstSection.FindPropertyRelative("startBar").intValue = 1;
            firstSection.FindPropertyRelative("startTime").doubleValue = adjustedFirstDownbeat;
            firstSection.FindPropertyRelative("beatsPerMinute").doubleValue = result.BeatsPerMinute;
            serializedChart.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(chart);
            statusMessage =
                $"템포를 {result.BeatsPerMinute:0.######} BPM, " +
                $"1마디 1박 {adjustedFirstDownbeat:0.000000}초로 적용했습니다. 저장 전에 전체 곡을 검증하세요.";

        }

        private static double GetNearestCalibrationBeatTime(
            double songTime,
            double firstDownbeat,
            double beatsPerMinute,
            int beatUnit)
        {

            double secondsPerBeat = 60d / beatsPerMinute * (4d / beatUnit);
            double beatIndex = Math.Round((songTime - firstDownbeat) / secondsPerBeat);
            return firstDownbeat + beatIndex * secondsPerBeat;

        }

        private void StartTempoCalibrationPreview(
            ChartTempoCalibrationResult result,
            double adjustedFirstDownbeat,
            ChartTempoSection currentTempo)
        {

            previewBeatsPerMinute = result.BeatsPerMinute;
            previewFirstDownbeatTime = adjustedFirstDownbeat;
            previewBeatsPerBar = currentTempo.BeatsPerBar;
            previewBeatUnit = currentTempo.BeatUnit;
            tempoCalibrationPreview = true;
            nextPreviewMetronomeSongTime = double.NaN;
            EnsureMetronome();
            metronome.StopAll();

            if (songPlayback.IsPaused)
            {

                songPlayback.Resume();

            }
            else if (!songPlayback.IsPlaying)
            {

                songPlayback.Play();

            }

            statusMessage = "계산된 박자 격자로 메트로놈을 미리 듣고 있습니다.";

        }

        private void StopTempoCalibrationPreview()
        {

            tempoCalibrationPreview = false;
            nextPreviewMetronomeSongTime = double.NaN;
            metronome?.StopAll();

        }

        private void DrawRecordingControls()
        {

            EditorGUILayout.LabelField("녹화", EditorStyles.boldLabel);
            string[] partNames = GetPartNames();
            selectedPartIndex = EditorGUILayout.Popup("음악 파트", selectedPartIndex, partNames);
            GUI.enabled = recordingPhase == RecordingPhase.Idle && pendingInteraction == null;
            recordingNoteMode = (RecordingNoteMode)EditorGUILayout.EnumPopup(
                "노트 녹화 모드",
                recordingNoteMode);

            if (recordingNoteMode == RecordingNoteMode.Flick)
            {

                flickDefaultDirection = (FlickDefaultDirection)EditorGUILayout.EnumPopup(
                    "플릭 기본 방향",
                    flickDefaultDirection);

            }
            GUI.enabled = true;

            if (pendingInteraction != null)
            {

                string pendingDescription = pendingInteraction.NoteType == ChartNoteType.Banana
                    ? "바나나 끝 레인을 기다리는 중"
                    : $"지속 노트 진행 중 · 현재 {pendingInteraction.EndLaneIndex + 1}번 레인";
                EditorGUILayout.HelpBox(
                    pendingDescription + " (녹화를 중지하면 이 미완성 노트는 버려집니다.)",
                    MessageType.Warning);

            }
            countInBars = Mathf.Max(1, EditorGUILayout.IntField("카운트인 마디", countInBars));
            metronomeVolume = EditorGUILayout.Slider("메트로놈 음량(2배 출력)", metronomeVolume, 0f, 1f);
            metronomeOutputOffsetMilliseconds = EditorGUILayout.Slider(
                new GUIContent(
                    "메트로놈 출력 보정(ms)",
                    "음수는 클릭을 앞당기고 양수는 늦춥니다. 차트와 판정 시간은 변경하지 않습니다."),
                metronomeOutputOffsetMilliseconds,
                -50f,
                50f);
            metronomeDuringRecording = EditorGUILayout.Toggle("녹화 중 메트로놈", metronomeDuringRecording);
            showQuantizationSettings = EditorGUILayout.Foldout(
                showQuantizationSettings,
                automaticQuantization ? "입력 박자 보정 · 자동 적용 중" : "입력 박자 보정",
                true);

            if (showQuantizationSettings)
            {

                DrawQuantizationSettings();

            }

            if (chart.TempoSections.Count > 0)
            {

                double referenceTime = songPlayback != null ? songPlayback.SongTime : loopStart;
                ChartTempoSection activeTempo = ChartTempoMap.FindSectionForTime(
                    chart.TempoSections,
                    referenceTime);
                double countInDuration = activeTempo.SecondsPerBar * countInBars;
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

                if (showAdvancedNavigation && GUILayout.Button("처음부터 녹화"))
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

        private void DrawQuantizationSettings()
        {

            automaticQuantization = EditorGUILayout.Toggle("녹화 종료 시 자동 보정", automaticQuantization);
            quantizationGrid = (QuantizationGrid)EditorGUILayout.IntPopup(
                "보정 격자",
                (int)quantizationGrid,
                QuantizationGridNames,
                QuantizationGridValues);
            maximumQuantizationMilliseconds = EditorGUILayout.Slider(
                "최대 보정 거리(ms)",
                maximumQuantizationMilliseconds,
                0f,
                100f);
            quantizationStrength = EditorGUILayout.Slider("보정 강도", quantizationStrength, 0f, 1f);
            inputAdvanceMilliseconds = EditorGUILayout.Slider(
                "입력 앞당김(ms)",
                inputAdvanceMilliseconds,
                -200f,
                200f);
            chordGroupingMilliseconds = EditorGUILayout.Slider(
                "화음 묶음 범위(ms)",
                chordGroupingMilliseconds,
                0f,
                80f);

            EditorGUILayout.HelpBox(
                "원본 입력 시간은 보존됩니다. 양수 입력 앞당김 값은 기록 시간을 더 이르게 이동합니다.",
                MessageType.None);

        }

        private void DrawStemControls()
        {

            showStemMixer = EditorGUILayout.Foldout(showStemMixer, "오디오 믹서 · 파트 소리 조절", true);

            if (!showStemMixer)
            {

                return;

            }

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

                GUI.enabled = recordedNotes.Count > 0 && chart.TempoSections.Count > 0;

                if (GUILayout.Button("전체 박자 보정"))
                {

                    QuantizeRecordedNotes(false, false);

                }

                GUI.enabled = selectedRecordedNoteIndex >= 0 && chart.TempoSections.Count > 0;

                if (GUILayout.Button("선택 노트 보정"))
                {

                    QuantizeRecordedNotes(true, false);

                }

                GUI.enabled = recordedNotes.Count > 0;

                if (GUILayout.Button("원본 시간 복원"))
                {

                    RestoreOriginalRecordedTimes();

                }

                GUI.enabled = true;

            }

            using (new EditorGUILayout.HorizontalScope())
            {

                GUI.enabled = selectedRecordedNoteIndex >= 0;
                noteNudgeMilliseconds = Math.Max(
                    1f,
                    EditorGUILayout.FloatField("시간 이동 단위(ms)", noteNudgeMilliseconds));

                if (GUILayout.Button("앞으로", GUILayout.Width(64f)))
                {

                    NudgeSelected(-noteNudgeMilliseconds / 1000d);

                }

                if (GUILayout.Button("뒤로", GUILayout.Width(64f)))
                {

                    NudgeSelected(noteNudgeMilliseconds / 1000d);

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

                    GUILayout.Label(
                        note.noteData == null ? "Tap" : note.noteData.NoteType.ToString(),
                        GUILayout.Width(58f));

                    double nextHitTime = Math.Max(
                        0d,
                        EditorGUILayout.DoubleField(note.hitTime, GUILayout.Width(92f)));
                    int nextLaneIndex = Mathf.Clamp(
                        EditorGUILayout.IntField(note.laneIndex + 1, GUILayout.Width(42f)) - 1,
                        0,
                        chart.LaneCount - 1);
                    int partIndex = FindPartIndex(note.musicalPartId);
                    partIndex = EditorGUILayout.Popup(partIndex, partNames);
                    string nextPartId = chart.MusicalParts[partIndex].Id;
                    double correctionMilliseconds = note.hasOriginalHitTime
                        ? (note.hitTime - note.originalHitTime) * 1000d
                        : 0d;
                    GUILayout.Label($"{correctionMilliseconds:+0.0;-0.0;0.0}ms", GUILayout.Width(64f));

                    if (Math.Abs(nextHitTime - note.hitTime) > 0.0000001d ||
                        nextLaneIndex != note.laneIndex ||
                        nextPartId != note.musicalPartId)
                    {

                        Undo.RecordObject(this, "임시 채보 노트 수정");
                        SetRecordedNoteHitTime(note, nextHitTime);
                        note.originalHitTime = nextHitTime;
                        note.hasOriginalHitTime = true;
                        note.pendingAutomaticQuantization = false;
                        note.laneIndex = nextLaneIndex;
                        note.musicalPartId = nextPartId;
                        SynchronizeRecordedNoteIdentity(note);
                        bufferWasApplied = false;

                    }

                    if (GUILayout.Button("삭제", GUILayout.Width(44f)))
                    {

                        removeIndex = index;

                    }

                }

            }

            EditorGUILayout.EndScrollView();

            if (removeIndex >= 0)
            {

                Undo.RecordObject(this, "임시 채보 노트 삭제");
                recordedNotes.RemoveAt(removeIndex);
                bufferWasApplied = false;
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
            clearBufferAfterApply = EditorGUILayout.Toggle("적용 후 임시 기록 비우기", clearBufferAfterApply);

            if (bufferWasApplied && recordedNotes.Count > 0)
            {

                EditorGUILayout.HelpBox(
                    "현재 임시 기록은 이미 차트에 적용되었습니다. 다시 적용하면 중복될 수 있습니다.",
                    MessageType.Warning);

            }

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

                    if (!bufferWasApplied || EditorUtility.DisplayDialog(
                            "기록 다시 적용",
                            "현재 임시 기록은 이미 적용되었습니다. 그래도 다시 적용할까요?",
                            "다시 적용",
                            "취소"))
                    {

                        ApplyRecordedNotes();

                    }

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

            }

            if (GUILayout.Button("중복·겹친 활성 구간 정리"))
            {

                int removedCount = ChartActivationWindowUtility.Normalize(chart);
                statusMessage = removedCount > 0
                    ? $"중복·겹친 활성 구간 {removedCount}개를 정리했습니다."
                    : "정리할 중복 활성 구간이 없습니다.";

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
                tempoCalibrationPreview = false;
                StopTempoTapCapture();
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

                if (songPlayback.HasReachedScheduledStart &&
                    songPlayback.SongTime >= recordingTargetTime)
                {

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

            if (tempoCalibrationPreview)
            {

                ScheduleTempoCalibrationPreview();

            }

            if (timelineAutoScroll && songPlayback.IsPlaying)
            {

                UpdateTimelineAutoScroll();

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
            StopTempoCalibrationPreview();

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

            if (recordingPhase == RecordingPhase.Idle)
            {

                return;

            }

            statusMessage = startMode switch
            {
                RecordingStartMode.CurrentPosition => "현재 위치 녹화를 위한 카운트인을 시작했습니다.",
                RecordingStartMode.Loop => "반복 구간 녹화를 위한 프리롤을 시작했습니다.",
                _ => "곡 처음 녹화를 위한 카운트인을 시작했습니다."
            };

        }

        private void StopRecording()
        {

            bool discardedPendingInteraction = pendingInteraction != null;
            pendingInteraction = null;
            isRecording = false;
            isLoopRecording = false;
            recordingPhase = RecordingPhase.Idle;
            DisableRecordingInputActions();
            metronome?.StopAll();

            if (automaticQuantization && chart.TempoSections.Count > 0)
            {

                QuantizeRecordedNotes(false, true);

            }
            else
            {

                statusMessage =
                    $"녹화를 중지했습니다. 임시 기록에 노트 {recordedNotes.Count}개가 있습니다." +
                    (discardedPendingInteraction ? " 미완성 지속 노트는 버렸습니다." : string.Empty);

            }

        }

        private void DrawTimeline()
        {

            EditorGUILayout.LabelField("채보 타임라인", EditorStyles.boldLabel);

            if (chart.TempoSections.Count == 0)
            {

                EditorGUILayout.HelpBox("타임라인을 그리려면 템포 정보가 필요합니다.", MessageType.Error);
                return;

            }

            if (tempoTapCapture && Keyboard.current?.spaceKey.wasPressedThisFrame == true)
            {

                TryCaptureTempoDownbeat(InputState.currentTime);

            }

            RefreshPatternDuplicationPreview();

            timelineViewMode = (TimelineViewMode)GUILayout.Toolbar(
                (int)timelineViewMode,
                new[] { "가로 타임라인", "세로 채보 시트" });

            using (new EditorGUILayout.HorizontalScope())
            {

                timelineAutoScroll = EditorGUILayout.Toggle(
                    "재생 위치 자동 따라가기",
                    timelineAutoScroll);

                if (timelineViewMode == TimelineViewMode.Vertical)
                {

                    verticalTimelineHeight = EditorGUILayout.Slider(
                        "시트 높이",
                        verticalTimelineHeight,
                        360f,
                        900f);

                }

            }

            double duration = GetAuthoringDuration();
            double maximumStart = Math.Max(0d, duration - timelineVisibleDuration);

            if (showAdvancedNavigation)
            {

                timelineVisibleDuration = EditorGUILayout.Slider(
                    "표시 범위(초)",
                    timelineVisibleDuration,
                    4f,
                    60f);
                maximumStart = Math.Max(0d, duration - timelineVisibleDuration);
                timelineStartTime = EditorGUILayout.Slider(
                    "시작 위치",
                    (float)Math.Min(timelineStartTime, maximumStart),
                    0f,
                    (float)Math.Max(0.001d, maximumStart));

            }
            else
            {

                timelineStartTime = Math.Max(0d, Math.Min(maximumStart, timelineStartTime));

            }

            double visibleEnd = timelineStartTime + timelineVisibleDuration;

            if (timelineViewMode == TimelineViewMode.Horizontal)
            {

                DrawHorizontalTimeline(visibleEnd);

            }
            else
            {

                DrawVerticalTimeline(visibleEnd);

            }

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

                GUI.enabled = chart.MusicalParts.Count > 0;

                if (GUILayout.Button("선택 파트 활성 구간 모두 지우기"))
                {

                    ClearSelectedPartActivationWindows();

                }

                GUI.enabled = true;

            }

            DrawPatternDuplicationControls();
            DrawAppliedNoteDeletionControls();
            DrawAppliedNoteQuantizationControls();

            EditorGUILayout.HelpBox(
                "진한 선은 마디, 옅은 선은 박입니다. 빈 곳을 클릭하면 이동하고 임시 노트를 클릭하면 선택합니다. " +
                "마우스 휠로 이동하고 Ctrl+휠로 확대할 수 있습니다. 기존 노트는 채워진 표시이며 클릭해서 " +
                "삭제 대상으로 선택할 수 있습니다. 임시 녹화 노트는 노란 테두리로 표시됩니다.",
                MessageType.Info);

        }

        private void DrawHorizontalTimeline(double visibleEnd)
        {

            float height = TimelineRulerHeight + chart.MusicalParts.Count * TimelinePartHeight;
            Rect timelineRect = EditorGUILayout.GetControlRect(false, height);
            const float labelWidth = 88f;
            Rect contentRect = new(
                timelineRect.x + labelWidth,
                timelineRect.y,
                timelineRect.width - labelWidth,
                timelineRect.height);
            EditorGUI.DrawRect(timelineRect, new Color(0.075f, 0.075f, 0.085f, 1f));
            EditorGUI.DrawRect(
                new Rect(contentRect.x, contentRect.y, contentRect.width, TimelineRulerHeight),
                new Color(0.12f, 0.12f, 0.14f, 1f));
            DrawTimelineBeatGrid(contentRect, visibleEnd);
            DrawTimelineParts(timelineRect, contentRect, visibleEnd);
            DrawTimelineLoop(contentRect, visibleEnd);
            DrawTimelinePlayhead(contentRect, visibleEnd);
            HandleTimelineInput(contentRect);

        }

        private void DrawVerticalTimeline(double visibleEnd)
        {

            Rect timelineRect = EditorGUILayout.GetControlRect(false, verticalTimelineHeight);
            Rect contentRect = new(
                timelineRect.x + VerticalTimelineRulerWidth,
                timelineRect.y + VerticalTimelineHeaderHeight,
                timelineRect.width - VerticalTimelineRulerWidth,
                timelineRect.height - VerticalTimelineHeaderHeight);
            EditorGUI.DrawRect(timelineRect, new Color(0.075f, 0.075f, 0.085f, 1f));
            EditorGUI.DrawRect(
                new Rect(contentRect.x, timelineRect.y, contentRect.width, VerticalTimelineHeaderHeight),
                new Color(0.12f, 0.12f, 0.14f, 1f));
            DrawVerticalPartColumns(timelineRect, contentRect, visibleEnd);
            DrawVerticalBeatGrid(timelineRect, contentRect, visibleEnd);
            DrawVerticalLoop(contentRect, visibleEnd);
            DrawVerticalPlayhead(contentRect, visibleEnd);
            HandleVerticalTimelineInput(contentRect);

        }

        private void DrawVerticalPartColumns(Rect timelineRect, Rect contentRect, double visibleEnd)
        {

            int partCount = Math.Max(1, chart.MusicalParts.Count);
            float partWidth = contentRect.width / partCount;

            for (int partIndex = 0; partIndex < chart.MusicalParts.Count; partIndex++)
            {

                MusicalPartDefinition part = chart.MusicalParts[partIndex];
                Rect columnRect = new(
                    contentRect.x + partIndex * partWidth,
                    contentRect.y,
                    partWidth,
                    contentRect.height);
                EditorGUI.DrawRect(
                    columnRect,
                    partIndex % 2 == 0
                        ? new Color(0.095f, 0.095f, 0.11f, 0.96f)
                        : new Color(0.115f, 0.115f, 0.13f, 0.96f));
                EditorGUI.DrawRect(
                    new Rect(columnRect.x, timelineRect.y + VerticalTimelineHeaderHeight - 4f, partWidth, 4f),
                    part.Color);
                GUI.Label(
                    new Rect(columnRect.x + 3f, timelineRect.y + 7f, partWidth - 6f, 20f),
                    part.DisplayName,
                    EditorStyles.miniLabel);

            }

            for (int index = 0; index < chart.ActivationWindows.Count; index++)
            {

                MusicalPartActivationWindow window = chart.ActivationWindows[index];

                if (window.EndTime <= timelineStartTime || window.StartTime >= visibleEnd)
                {

                    continue;

                }

                int partIndex = FindPartIndex(window.MusicalPartId);
                MusicalPartDefinition part = chart.MusicalParts[partIndex];
                float startY = TimeToVerticalTimelineY(contentRect, Math.Max(window.StartTime, timelineStartTime));
                float endY = TimeToVerticalTimelineY(contentRect, Math.Min(window.EndTime, visibleEnd));
                Color color = part.Color;
                color.a = 0.18f;
                EditorGUI.DrawRect(
                    new Rect(
                        contentRect.x + partIndex * partWidth + 2f,
                        startY,
                        Math.Max(2f, partWidth - 4f),
                        Math.Max(2f, endY - startY)),
                    color);

            }

            for (int index = 0; index < chart.Notes.Count; index++)
            {

                ChartNote note = chart.Notes[index];

                if (note.HitTime < timelineStartTime || note.HitTime > visibleEnd)
                {

                    continue;

                }

                int partIndex = FindPartIndex(note.MusicalPartId);
                Rect columnRect = new(
                    contentRect.x + partIndex * partWidth,
                    contentRect.y,
                    partWidth,
                    contentRect.height);
                float x = GetVerticalLaneTimelineX(columnRect, note.LaneIndex);
                float y = TimeToVerticalTimelineY(contentRect, note.HitTime);

                if (note.Id == selectedChartNoteId)
                {

                    EditorGUI.DrawRect(
                        new Rect(x - 5f, y - 5f, 11f, 11f),
                        new Color(0.3f, 0.9f, 1f, 1f));

                }

                EditorGUI.DrawRect(new Rect(x - 3f, y - 3f, 7f, 7f), chart.GetPartColor(note.MusicalPartId));

            }

            DrawPatternPreviewNotesVertical(contentRect, partWidth, visibleEnd);

            for (int index = 0; index < recordedNotes.Count; index++)
            {

                RecordedNote note = recordedNotes[index];

                if (note.hitTime < timelineStartTime || note.hitTime > visibleEnd)
                {

                    continue;

                }

                int partIndex = FindPartIndex(note.musicalPartId);
                Rect columnRect = new(
                    contentRect.x + partIndex * partWidth,
                    contentRect.y,
                    partWidth,
                    contentRect.height);
                float x = GetVerticalLaneTimelineX(columnRect, note.laneIndex);
                float y = TimeToVerticalTimelineY(contentRect, note.hitTime);
                Rect outer = new(x - 4f, y - 4f, 9f, 9f);

                if (index == selectedRecordedNoteIndex)
                {

                    EditorGUI.DrawRect(
                        new Rect(outer.x - 2f, outer.y - 2f, outer.width + 4f, outer.height + 4f),
                        new Color(0.3f, 0.9f, 1f, 1f));

                }

                EditorGUI.DrawRect(outer, new Color(1f, 0.82f, 0.2f, 1f));
                EditorGUI.DrawRect(
                    new Rect(outer.x + 2f, outer.y + 2f, 5f, 5f),
                    new Color(0.1f, 0.1f, 0.11f, 1f));

            }

        }

        private void DrawVerticalBeatGrid(Rect timelineRect, Rect contentRect, double visibleEnd)
        {

            double beatTime = ChartTempoMap.GetBeatTimeAtOrBefore(chart.TempoSections, timelineStartTime);

            if (beatTime < timelineStartTime - 0.000001d)
            {

                beatTime = ChartTempoMap.GetBeatTimeAfter(chart.TempoSections, beatTime + 0.000001d);

            }

            int guard = 0;

            while (beatTime <= visibleEnd && guard++ < 2048)
            {

                ChartBeatPosition position = ChartTempoMap.GetBeatPosition(chart.TempoSections, beatTime);
                float y = TimeToVerticalTimelineY(contentRect, beatTime);
                bool barStart = position.Beat == 1;
                EditorGUI.DrawRect(
                    new Rect(contentRect.x, y, contentRect.width, barStart ? 2f : 1f),
                    barStart
                        ? new Color(0.72f, 0.72f, 0.78f, 0.55f)
                        : new Color(0.45f, 0.45f, 0.5f, 0.22f));

                if (barStart)
                {

                    GUI.Label(
                        new Rect(timelineRect.x + 2f, y - 8f, VerticalTimelineRulerWidth - 4f, 18f),
                        $"{position.Bar}마디",
                        EditorStyles.miniLabel);

                }

                beatTime = ChartTempoMap.GetBeatTimeAfter(chart.TempoSections, beatTime + 0.000001d);

            }

        }

        private void DrawVerticalLoop(Rect contentRect, double visibleEnd)
        {

            if (loopEnd <= loopStart || loopEnd <= timelineStartTime || loopStart >= visibleEnd)
            {

                return;

            }

            float startY = TimeToVerticalTimelineY(contentRect, Math.Max(loopStart, timelineStartTime));
            float endY = TimeToVerticalTimelineY(contentRect, Math.Min(loopEnd, visibleEnd));
            EditorGUI.DrawRect(
                new Rect(contentRect.x, startY, 4f, Math.Max(2f, endY - startY)),
                new Color(1f, 0.72f, 0.15f, 0.9f));

        }

        private void DrawVerticalPlayhead(Rect contentRect, double visibleEnd)
        {

            if (songPlayback == null || songPlayback.SongTime < timelineStartTime || songPlayback.SongTime > visibleEnd)
            {

                return;

            }

            float y = TimeToVerticalTimelineY(contentRect, songPlayback.SongTime);
            EditorGUI.DrawRect(
                new Rect(contentRect.x, y, contentRect.width, 2f),
                new Color(1f, 0.25f, 0.2f, 0.95f));

        }

        private void HandleVerticalTimelineInput(Rect contentRect)
        {

            Event currentEvent = Event.current;

            if (!contentRect.Contains(currentEvent.mousePosition))
            {

                return;

            }

            if (currentEvent.type == EventType.ScrollWheel)
            {

                HandleTimelineScroll(currentEvent, contentRect, true);
                return;

            }

            if (currentEvent.type != EventType.MouseDown || currentEvent.button != 0)
            {

                return;

            }

            if (TrySelectRecordedNoteVertical(contentRect, currentEvent.mousePosition))
            {

                currentEvent.Use();
                return;

            }

            if (TrySelectChartNoteVertical(contentRect, currentEvent.mousePosition))
            {

                currentEvent.Use();
                return;

            }

            double normalized = Mathf.InverseLerp(contentRect.y, contentRect.yMax, currentEvent.mousePosition.y);
            double selectedTime = timelineStartTime + normalized * timelineVisibleDuration;
            seekTime = ChartTempoMap.SnapSongTime(chart.TempoSections, selectedTime, (int)quantizationGrid);
            timelineAutoScroll = false;

            if (Application.isPlaying && songPlayback != null && songPlayback.IsPrepared)
            {

                Seek(seekTime);

            }

            currentEvent.Use();

        }

        private bool TrySelectRecordedNoteVertical(Rect contentRect, Vector2 mousePosition)
        {

            int partCount = Math.Max(1, chart.MusicalParts.Count);
            float partWidth = contentRect.width / partCount;
            int closestIndex = -1;
            float closestDistance = TimelineNoteHitRadius;

            for (int index = 0; index < recordedNotes.Count; index++)
            {

                RecordedNote note = recordedNotes[index];

                if (note.hitTime < timelineStartTime || note.hitTime > timelineStartTime + timelineVisibleDuration)
                {

                    continue;

                }

                int partIndex = FindPartIndex(note.musicalPartId);
                Rect columnRect = new(
                    contentRect.x + partIndex * partWidth,
                    contentRect.y,
                    partWidth,
                    contentRect.height);
                Vector2 notePosition = new(
                    GetVerticalLaneTimelineX(columnRect, note.laneIndex),
                    TimeToVerticalTimelineY(contentRect, note.hitTime));
                float distance = Vector2.Distance(mousePosition, notePosition);

                if (distance <= closestDistance)
                {

                    closestDistance = distance;
                    closestIndex = index;

                }

            }

            if (closestIndex < 0)
            {

                return false;

            }

            selectedRecordedNoteIndex = closestIndex;
            selectedChartNoteId = string.Empty;
            scrollPosition.y = Math.Max(
                0f,
                closestIndex * (EditorGUIUtility.singleLineHeight + 2f) - 60f);
            return true;

        }

        private bool TrySelectChartNoteVertical(Rect contentRect, Vector2 mousePosition)
        {

            int partCount = Math.Max(1, chart.MusicalParts.Count);
            float partWidth = contentRect.width / partCount;
            ChartNote closestNote = null;
            float closestDistance = TimelineNoteHitRadius;

            for (int index = 0; index < chart.Notes.Count; index++)
            {

                ChartNote note = chart.Notes[index];

                if (note.HitTime < timelineStartTime ||
                    note.HitTime > timelineStartTime + timelineVisibleDuration)
                {

                    continue;

                }

                int partIndex = FindPartIndex(note.MusicalPartId);
                Rect columnRect = new(
                    contentRect.x + partIndex * partWidth,
                    contentRect.y,
                    partWidth,
                    contentRect.height);
                Vector2 notePosition = new(
                    GetVerticalLaneTimelineX(columnRect, note.LaneIndex),
                    TimeToVerticalTimelineY(contentRect, note.HitTime));
                float distance = Vector2.Distance(mousePosition, notePosition);

                if (distance <= closestDistance)
                {

                    closestDistance = distance;
                    closestNote = note;

                }

            }

            if (closestNote == null)
            {

                return false;

            }

            selectedChartNoteId = closestNote.Id;
            selectedRecordedNoteIndex = -1;
            return true;

        }

        private float TimeToVerticalTimelineY(Rect contentRect, double songTime)
        {

            double normalized = (songTime - timelineStartTime) / timelineVisibleDuration;
            return contentRect.y + (float)normalized * contentRect.height;

        }

        private float GetVerticalLaneTimelineX(Rect columnRect, int laneIndex)
        {

            float normalized = chart.LaneCount <= 1
                ? 0.5f
                : laneIndex / (chart.LaneCount - 1f);
            return Mathf.Lerp(columnRect.x + 7f, columnRect.xMax - 7f, normalized);

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
                DrawPatternPreviewNotesHorizontal(rowRect, part.Id, visibleEnd);
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

                if (note.Id == selectedChartNoteId)
                {

                    EditorGUI.DrawRect(
                        new Rect(x - 5f, y - 5f, 11f, 11f),
                        new Color(0.3f, 0.9f, 1f, 1f));

                }

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

                if (index == selectedRecordedNoteIndex)
                {

                    EditorGUI.DrawRect(
                        new Rect(outer.x - 2f, outer.y - 2f, outer.width + 4f, outer.height + 4f),
                        new Color(0.3f, 0.9f, 1f, 1f));

                }

                EditorGUI.DrawRect(outer, new Color(1f, 0.82f, 0.2f, 1f));
                EditorGUI.DrawRect(new Rect(outer.x + 2f, outer.y + 2f, 5f, 5f), new Color(0.1f, 0.1f, 0.11f, 1f));

            }

        }

        private void DrawPatternPreviewNotesHorizontal(Rect rowRect, string partId, double visibleEnd)
        {

            if (patternDuplicationPreview == null || !patternDuplicationPreview.IsValid)
            {

                return;

            }

            for (int index = 0; index < patternDuplicationPreview.GeneratedNotes.Count; index++)
            {

                ChartPatternPreviewNote note = patternDuplicationPreview.GeneratedNotes[index];

                if (note.MusicalPartId != partId ||
                    note.HitTime < timelineStartTime ||
                    note.HitTime > visibleEnd)
                {

                    continue;

                }

                float x = TimeToTimelineX(rowRect, note.HitTime);
                float y = GetLaneTimelineY(rowRect, note.LaneIndex);
                DrawPatternPreviewMarker(x, y, note);

            }

        }

        private void DrawPatternPreviewNotesVertical(
            Rect contentRect,
            float partWidth,
            double visibleEnd)
        {

            if (patternDuplicationPreview == null || !patternDuplicationPreview.IsValid)
            {

                return;

            }

            for (int index = 0; index < patternDuplicationPreview.GeneratedNotes.Count; index++)
            {

                ChartPatternPreviewNote note = patternDuplicationPreview.GeneratedNotes[index];

                if (note.HitTime < timelineStartTime || note.HitTime > visibleEnd)
                {

                    continue;

                }

                int partIndex = FindPartIndex(note.MusicalPartId);
                Rect columnRect = new(
                    contentRect.x + partIndex * partWidth,
                    contentRect.y,
                    partWidth,
                    contentRect.height);
                float x = GetVerticalLaneTimelineX(columnRect, note.LaneIndex);
                float y = TimeToVerticalTimelineY(contentRect, note.HitTime);
                DrawPatternPreviewMarker(x, y, note);

            }

        }

        private void DrawPatternPreviewMarker(float x, float y, ChartPatternPreviewNote note)
        {

            bool inactive = !chart.IsPartActive(note.MusicalPartId, note.HitTime) &&
                            !patternAddActivationWindow;
            Color borderColor = inactive
                ? new Color(1f, 0.35f, 0.25f, 0.95f)
                : new Color(0.25f, 0.95f, 1f, 0.95f);
            Rect outer = new(x - 4f, y - 4f, 9f, 9f);
            EditorGUI.DrawRect(outer, borderColor);
            EditorGUI.DrawRect(
                new Rect(outer.x + 2f, outer.y + 2f, 5f, 5f),
                new Color(0.08f, 0.09f, 0.11f, 0.8f));

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

            if (!contentRect.Contains(currentEvent.mousePosition))
            {

                return;

            }

            if (currentEvent.type == EventType.ScrollWheel)
            {

                HandleTimelineScroll(currentEvent, contentRect, false);
                return;

            }

            if (currentEvent.type != EventType.MouseDown || currentEvent.button != 0)
            {

                return;

            }

            if (TrySelectRecordedNoteHorizontal(contentRect, currentEvent.mousePosition))
            {

                currentEvent.Use();
                return;

            }

            if (TrySelectChartNoteHorizontal(contentRect, currentEvent.mousePosition))
            {

                currentEvent.Use();
                return;

            }

            double normalized = Mathf.InverseLerp(contentRect.x, contentRect.xMax, currentEvent.mousePosition.x);
            double selectedTime = timelineStartTime + normalized * timelineVisibleDuration;
            seekTime = ChartTempoMap.SnapSongTime(
                chart.TempoSections,
                selectedTime,
                (int)quantizationGrid);
            timelineAutoScroll = false;

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
            IReadOnlyList<ChartAuthoringCountInBeat> countInBeats =
                ChartAuthoringCountIn.BuildBeats(
                    tempo,
                    recordingTargetTime,
                    countInBars);

            if (countInBeats.Count == 0)
            {

                statusMessage = "카운트인에 배치할 박자를 계산하지 못했습니다.";
                recordingPhase = RecordingPhase.Idle;
                return;

            }

            const double firstClickDelay = 0.20d;
            double firstClickSongTime = countInBeats[0].SongTime;
            double songStartDelay = firstClickDelay - firstClickSongTime;
            EnsureMetronome();
            metronome.StopAll();
            songPlayback.Stop();

            if (!songPlayback.SchedulePlay(
                songStartDelay,
                0d,
                out ulong songStartDspClock,
                out int sampleRate))
            {

                statusMessage = "FMOD DSP 시계에 카운트인과 음원 시작을 예약하지 못했습니다.";
                recordingPhase = RecordingPhase.Idle;
                return;

            }

            for (int index = 0; index < countInBeats.Count; index++)
            {

                ChartAuthoringCountInBeat beat = countInBeats[index];
                double clickSongTime = beat.SongTime +
                    metronomeOutputOffsetMilliseconds / 1000d;
                ulong clickDspClock = AddSongTimeToDspClock(
                    songStartDspClock,
                    clickSongTime,
                    sampleRate);

                if (!metronome.ScheduleAtDspClock(
                    clickDspClock,
                    beat.Accent,
                    metronomeVolume))
                {

                    metronome.StopAll();
                    songPlayback.Stop();
                    statusMessage = "카운트인 박자를 충분히 미리 예약하지 못했습니다. 다시 시도하세요.";
                    recordingPhase = RecordingPhase.Idle;
                    return;

                }

            }

            countInEndRealtime = EditorApplication.timeSinceStartup +
                songStartDelay + recordingTargetTime;
            recordingPhase = RecordingPhase.CountIn;

        }

        private static ulong AddSongTimeToDspClock(
            ulong songStartDspClock,
            double songTime,
            int sampleRate)
        {

            long sampleOffset = (long)Math.Round(songTime * sampleRate);

            if (sampleOffset >= 0)
            {

                return songStartDspClock + (ulong)sampleOffset;

            }

            ulong samplesBeforeStart = (ulong)(-sampleOffset);
            return samplesBeforeStart >= songStartDspClock
                ? 0
                : songStartDspClock - samplesBeforeStart;

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

            pendingInteraction = null;
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

        private void ScheduleTempoCalibrationPreview()
        {

            if (!songPlayback.IsPlaying || previewBeatsPerMinute <= 0d)
            {

                return;

            }

            EnsureMetronome();
            double songTime = songPlayback.SongTime;
            double secondsPerBeat = 60d / previewBeatsPerMinute * (4d / previewBeatUnit);

            if (double.IsNaN(nextPreviewMetronomeSongTime) ||
                nextPreviewMetronomeSongTime < songTime - 0.050d ||
                nextPreviewMetronomeSongTime > songTime + 2d)
            {

                if (songTime <= previewFirstDownbeatTime)
                {

                    nextPreviewMetronomeSongTime = previewFirstDownbeatTime;

                }
                else
                {

                    double elapsedBeats = (songTime - previewFirstDownbeatTime) / secondsPerBeat;
                    long nextBeatIndex = (long)Math.Ceiling(elapsedBeats - 0.000001d);
                    nextPreviewMetronomeSongTime =
                        previewFirstDownbeatTime + nextBeatIndex * secondsPerBeat;

                }

            }

            double scheduleLimit = songTime + MetronomeScheduleLeadSeconds;

            while (nextPreviewMetronomeSongTime <= scheduleLimit + 0.000001d)
            {

                long beatIndex = (long)Math.Round(
                    (nextPreviewMetronomeSongTime - previewFirstDownbeatTime) / secondsPerBeat);
                bool accent = beatIndex >= 0 && beatIndex % previewBeatsPerBar == 0;
                double adjustedClickTime = nextPreviewMetronomeSongTime +
                    metronomeOutputOffsetMilliseconds / 1000d;

                if (ChartAuthoringMetronome.TryGetScheduleDelay(
                    adjustedClickTime,
                    songTime,
                    out double delay))
                {

                    metronome.Schedule(delay, accent, metronomeVolume);

                }

                nextPreviewMetronomeSongTime += secondsPerBeat;

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
                double adjustedClickTime = nextMetronomeSongTime +
                    metronomeOutputOffsetMilliseconds / 1000d;

                if (ChartAuthoringMetronome.TryGetScheduleDelay(
                    adjustedClickTime,
                    songTime,
                    out double delay))
                {

                    metronome.Schedule(
                        delay,
                        position.Beat == 1,
                        metronomeVolume);

                }

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

            hitTime = Math.Max(0d, hitTime);

            switch (recordingNoteMode)
            {

                case RecordingNoteMode.SlideAndHold:
                    RecordSlideOrHoldLane(laneIndex, hitTime);
                    return;
                case RecordingNoteMode.Flick:
                    RecordFlickLane(laneIndex, hitTime);
                    return;
                case RecordingNoteMode.Banana:
                    RecordBananaLane(laneIndex, hitTime);
                    return;

            }

            ChartNoteAuthoringData tap = ChartNoteAuthoringData.CreateTap(
                string.Empty,
                hitTime,
                laneIndex,
                chart.MusicalParts[selectedPartIndex].Id);
            AddRecordedInteraction(tap);

        }

        private void DrawSelectedRecordedInteractionDetails(RecordedNote recordedNote)
        {

            if (recordedNote.noteData == null || recordedNote.noteData.NoteType == ChartNoteType.Tap)
            {

                return;

            }

            ChartNoteAuthoringData data = recordedNote.noteData;

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {

                EditorGUILayout.LabelField($"선택한 {data.NoteType} 상세", EditorStyles.boldLabel);
                Undo.RecordObject(this, "임시 상호작용 노트 수정");
                EditorGUI.BeginChangeCheck();

                if (data.NoteType == ChartNoteType.Hold || data.NoteType == ChartNoteType.Banana)
                {

                    data.EndTime = Math.Max(
                        data.HitTime + DuplicateInputThresholdSeconds,
                        EditorGUILayout.DoubleField("끝 시간", data.EndTime));

                }

                if (data.NoteType == ChartNoteType.Flick)
                {

                    int nextEndLane = Mathf.Clamp(
                        EditorGUILayout.IntField("끝 레인", data.EndLaneIndex + 1) - 1,
                        0,
                        chart.LaneCount - 1);

                    if (nextEndLane != data.LaneIndex)
                    {

                        data.EndLaneIndex = nextEndLane;

                    }

                }

                if (data.NoteType == ChartNoteType.Banana)
                {

                    data.EndLaneIndex = Mathf.Clamp(
                        EditorGUILayout.IntField("끝 레인", data.EndLaneIndex + 1) - 1,
                        0,
                        chart.LaneCount - 1);

                }

                if (data.NoteType == ChartNoteType.Slide)
                {

                    data.SlideEndBehavior = (SlideEndBehavior)EditorGUILayout.EnumPopup(
                        "끝 동작",
                        data.SlideEndBehavior);
                    DrawSlideNodeFields(data);
                    DrawSlidePathEditor(data);

                }

                if (data.NoteType == ChartNoteType.Banana)
                {

                    data.BananaMaximumBonusCombo = Mathf.Max(
                        0,
                        EditorGUILayout.IntField(
                            "최대 보너스 콤보",
                            data.BananaMaximumBonusCombo));
                    DrawBananaHandleFields(data);
                    DrawBananaCheckpointFields(data);

                }

                if (EditorGUI.EndChangeCheck())
                {

                    data.BananaCurveHandles.Sort(
                        (left, right) => left.NormalizedTime.CompareTo(right.NormalizedTime));
                    data.BananaCheckpoints.Sort((left, right) => left.Time.CompareTo(right.Time));
                    recordedNote.pendingAutomaticQuantization = false;
                    bufferWasApplied = false;

                }

            }

        }

        private void DrawSlidePathEditor(ChartNoteAuthoringData data)
        {

            EditorGUILayout.LabelField(
                "경로 편집 · 점을 세로로 드래그해 레인 변경 · 선을 클릭해 노드 추가",
                EditorStyles.miniLabel);
            Rect rect = GUILayoutUtility.GetRect(120f, 170f, GUILayout.ExpandWidth(true));
            EditorGUI.DrawRect(rect, new Color(0.075f, 0.08f, 0.095f, 1f));

            for (int laneIndex = 0; laneIndex < chart.LaneCount; laneIndex++)
            {

                float y = GetSlideEditorY(rect, laneIndex);
                EditorGUI.DrawRect(
                    new Rect(rect.x, y, rect.width, 1f),
                    new Color(1f, 1f, 1f, 0.08f));

            }

            Vector3[] points = new Vector3[data.SlideNodes.Count + 1];
            points[0] = new Vector3(rect.x, GetSlideEditorY(rect, data.LaneIndex), 0f);

            for (int index = 0; index < data.SlideNodes.Count; index++)
            {

                float normalizedTime = (float)((data.SlideNodes[index].Time - data.HitTime) /
                                               Math.Max(
                                                   ScheduleToleranceSeconds,
                                                   data.EndTime - data.HitTime));
                points[index + 1] = new Vector3(
                    Mathf.Lerp(rect.x, rect.xMax, Mathf.Clamp01(normalizedTime)),
                    GetSlideEditorY(rect, data.SlideNodes[index].LaneIndex),
                    0f);

            }

            Handles.BeginGUI();
            Handles.color = new Color(0.95f, 0.82f, 0.25f, 0.92f);
            Handles.DrawAAPolyLine(4f, points);
            Handles.EndGUI();

            for (int index = 0; index < points.Length; index++)
            {

                Rect pointRect = new(points[index].x - 6f, points[index].y - 6f, 12f, 12f);
                EditorGUI.DrawRect(
                    pointRect,
                    index == 0 || index == points.Length - 1
                        ? new Color(1f, 0.95f, 0.72f, 1f)
                        : new Color(0.95f, 0.62f, 0.18f, 1f));

            }

            HandleSlidePathEditorInput(rect, data, points);

        }

        private void HandleSlidePathEditorInput(
            Rect rect,
            ChartNoteAuthoringData data,
            Vector3[] points)
        {

            Event currentEvent = Event.current;

            if (currentEvent.type == EventType.MouseDown &&
                currentEvent.button == 0 &&
                rect.Contains(currentEvent.mousePosition))
            {

                for (int index = 0; index < points.Length; index++)
                {

                    if (Vector2.Distance(currentEvent.mousePosition, points[index]) <= 9f)
                    {

                        draggedSlidePointIndex = index - 1;
                        currentEvent.Use();
                        return;

                    }

                }

                float normalizedTime = Mathf.InverseLerp(
                    rect.x,
                    rect.xMax,
                    currentEvent.mousePosition.x);
                double nodeTime = Mathf.Lerp(
                    (float)data.HitTime,
                    (float)data.EndTime,
                    normalizedTime);

                for (int index = 0; index < data.SlideNodes.Count; index++)
                {

                    double segmentStartTime = index == 0
                        ? data.HitTime
                        : data.SlideNodes[index - 1].Time;
                    double segmentEndTime = data.SlideNodes[index].Time;

                    if (nodeTime <= segmentStartTime + ScheduleToleranceSeconds ||
                        nodeTime >= segmentEndTime - ScheduleToleranceSeconds)
                    {

                        continue;

                    }

                    int startLane = index == 0
                        ? data.LaneIndex
                        : data.SlideNodes[index - 1].LaneIndex;
                    float segmentProgress = (float)((nodeTime - segmentStartTime) /
                                                    (segmentEndTime - segmentStartTime));
                    float expectedY = Mathf.Lerp(
                        GetSlideEditorY(rect, startLane),
                        GetSlideEditorY(rect, data.SlideNodes[index].LaneIndex),
                        segmentProgress);

                    if (Mathf.Abs(currentEvent.mousePosition.y - expectedY) > 10f)
                    {

                        return;

                    }

                    int interpolatedLane = Mathf.Clamp(
                        Mathf.RoundToInt(Mathf.Lerp(
                            startLane,
                            data.SlideNodes[index].LaneIndex,
                            segmentProgress)),
                        0,
                        chart.LaneCount - 1);
                    Undo.RecordObject(this, "슬라이드 노드 추가");
                    data.SlideNodes.Insert(index, new ChartNoteAuthoringData.PathNodeData
                    {

                        Time = nodeTime,
                        LaneIndex = interpolatedLane

                    });
                    bufferWasApplied = false;
                    currentEvent.Use();
                    return;

                }

            }

            if (currentEvent.type == EventType.MouseDrag &&
                draggedSlidePointIndex != int.MinValue)
            {

                int laneIndex = GetSlideEditorLane(rect, currentEvent.mousePosition.y);
                Undo.RecordObject(this, "슬라이드 레인 드래그");

                if (draggedSlidePointIndex < 0)
                {

                    data.LaneIndex = laneIndex;

                }
                else if (draggedSlidePointIndex < data.SlideNodes.Count)
                {

                    data.SlideNodes[draggedSlidePointIndex].LaneIndex = laneIndex;

                }

                data.EndLaneIndex = data.SlideNodes[^1].LaneIndex;
                bufferWasApplied = false;
                currentEvent.Use();
                Repaint();

            }

            if (currentEvent.rawType == EventType.MouseUp)
            {

                draggedSlidePointIndex = int.MinValue;

            }

        }

        private float GetSlideEditorY(Rect rect, int laneIndex)
        {

            float normalizedLane = (laneIndex + 0.5f) / chart.LaneCount;
            return Mathf.Lerp(rect.yMax, rect.y, normalizedLane);

        }

        private int GetSlideEditorLane(Rect rect, float y)
        {

            float normalizedLane = Mathf.InverseLerp(rect.yMax, rect.y, y);
            return Mathf.Clamp(
                Mathf.FloorToInt(normalizedLane * chart.LaneCount),
                0,
                chart.LaneCount - 1);

        }

        private void DrawSlideNodeFields(ChartNoteAuthoringData data)
        {

            EditorGUILayout.LabelField("슬라이드 노드", EditorStyles.miniBoldLabel);
            int removeIndex = -1;

            for (int index = 0; index < data.SlideNodes.Count; index++)
            {

                ChartNoteAuthoringData.PathNodeData node = data.SlideNodes[index];

                using (new EditorGUILayout.HorizontalScope())
                {

                    GUILayout.Label((index + 1).ToString("00"), GUILayout.Width(24f));
                    double minimumTime = index == 0
                        ? data.HitTime + DuplicateInputThresholdSeconds
                        : data.SlideNodes[index - 1].Time + DuplicateInputThresholdSeconds;
                    node.Time = Math.Max(
                        minimumTime,
                        EditorGUILayout.DoubleField(node.Time, GUILayout.Width(92f)));
                    node.LaneIndex = Mathf.Clamp(
                        EditorGUILayout.IntField(node.LaneIndex + 1, GUILayout.Width(42f)) - 1,
                        0,
                        chart.LaneCount - 1);

                    GUI.enabled = data.SlideNodes.Count > 1;

                    if (GUILayout.Button("삭제", GUILayout.Width(44f)))
                    {

                        removeIndex = index;

                    }

                    GUI.enabled = true;

                }

            }

            if (removeIndex >= 0)
            {

                data.SlideNodes.RemoveAt(removeIndex);

            }

            if (GUILayout.Button("중간 노드 추가"))
            {

                ChartNoteAuthoringData.PathNodeData final = data.SlideNodes[^1];
                data.SlideNodes.Insert(
                    data.SlideNodes.Count - 1,
                    new ChartNoteAuthoringData.PathNodeData
                    {

                        Time = Math.Max(
                            data.HitTime + DuplicateInputThresholdSeconds,
                            final.Time - 0.25d),
                        LaneIndex = final.LaneIndex

                    });

            }

            if (data.SlideNodes.Count > 0)
            {

                data.EndTime = data.SlideNodes[^1].Time;
                data.EndLaneIndex = data.SlideNodes[^1].LaneIndex;

            }

        }

        private static void DrawBananaHandleFields(ChartNoteAuthoringData data)
        {

            EditorGUILayout.LabelField("곡선 핸들 (1~2개)", EditorStyles.miniBoldLabel);

            for (int index = 0; index < data.BananaCurveHandles.Count; index++)
            {

                ChartNoteAuthoringData.CurveHandleData handle = data.BananaCurveHandles[index];

                using (new EditorGUILayout.HorizontalScope())
                {

                    GUILayout.Label((index + 1).ToString("00"), GUILayout.Width(24f));
                    handle.NormalizedTime = EditorGUILayout.Slider(
                        handle.NormalizedTime,
                        0.01f,
                        0.99f);
                    handle.NormalizedX = EditorGUILayout.Slider(handle.NormalizedX, 0f, 1f);

                    GUI.enabled = data.BananaCurveHandles.Count > 1;

                    if (GUILayout.Button("삭제", GUILayout.Width(44f)))
                    {

                        data.BananaCurveHandles.RemoveAt(index);
                        GUI.enabled = true;
                        break;

                    }

                    GUI.enabled = true;

                }

            }

            GUI.enabled = data.BananaCurveHandles.Count < 2;

            if (GUILayout.Button("곡선 핸들 추가"))
            {

                data.BananaCurveHandles.Add(new ChartNoteAuthoringData.CurveHandleData
                {

                    NormalizedTime = 0.66f,
                    NormalizedX = 0.5f

                });
                data.BananaCurveHandles.Sort(
                    (left, right) => left.NormalizedTime.CompareTo(right.NormalizedTime));

            }

            GUI.enabled = true;

        }

        private void DrawBananaCheckpointFields(ChartNoteAuthoringData data)
        {

            EditorGUILayout.LabelField("체크포인트", EditorStyles.miniBoldLabel);

            using (new EditorGUILayout.HorizontalScope())
            {

                if (GUILayout.Button("1/4박 재생성"))
                {

                    data.BananaCheckpoints.Clear();
                    GenerateBananaCheckpoints(data, 4);

                }

                if (GUILayout.Button("1/8박 재생성"))
                {

                    data.BananaCheckpoints.Clear();
                    GenerateBananaCheckpoints(data, 8);

                }

            }

            int removeIndex = -1;

            for (int index = 0; index < data.BananaCheckpoints.Count; index++)
            {

                ChartNoteAuthoringData.CheckpointData checkpoint = data.BananaCheckpoints[index];

                using (new EditorGUILayout.HorizontalScope())
                {

                    GUILayout.Label((index + 1).ToString("00"), GUILayout.Width(24f));
                    checkpoint.Time = Math.Clamp(
                        EditorGUILayout.DoubleField(checkpoint.Time, GUILayout.Width(92f)),
                        data.HitTime + ScheduleToleranceSeconds,
                        data.EndTime - ScheduleToleranceSeconds);
                    checkpoint.NormalizedX = EditorGUILayout.Slider(
                        checkpoint.NormalizedX,
                        0f,
                        1f);

                    if (GUILayout.Button("삭제", GUILayout.Width(44f)))
                    {

                        removeIndex = index;

                    }

                }

            }

            if (removeIndex >= 0 && data.BananaCheckpoints.Count > 1)
            {

                data.BananaCheckpoints.RemoveAt(removeIndex);

            }

            if (GUILayout.Button("체크포인트 추가"))
            {

                data.BananaCheckpoints.Add(new ChartNoteAuthoringData.CheckpointData
                {

                    Time = (data.HitTime + data.EndTime) * 0.5d,
                    NormalizedX = 0.5f

                });
                data.BananaCheckpoints.Sort((left, right) => left.Time.CompareTo(right.Time));

            }

        }

        private void RecordSlideOrHoldLane(int laneIndex, double hitTime)
        {

            if (pendingInteraction == null)
            {

                pendingInteraction = ChartNoteAuthoringData.CreateTap(
                    string.Empty,
                    hitTime,
                    laneIndex,
                    chart.MusicalParts[selectedPartIndex].Id);
                pendingInteraction.NoteType = ChartNoteType.Hold;
                pendingInteraction.EndTime = hitTime;
                pendingInteraction.EndLaneIndex = laneIndex;
                statusMessage =
                    $"{hitTime:0.000}초 {laneIndex + 1}번에서 홀드/슬라이드를 시작했습니다.";
                return;

            }

            if (hitTime <= pendingInteraction.HitTime + DuplicateInputThresholdSeconds)
            {

                return;

            }

            int currentLane = pendingInteraction.EndLaneIndex;

            if (pendingInteraction.NoteType == ChartNoteType.Hold && laneIndex == currentLane)
            {

                pendingInteraction.EndTime = hitTime;
                AddRecordedInteraction(pendingInteraction);
                pendingInteraction = null;
                return;

            }

            pendingInteraction.NoteType = ChartNoteType.Slide;
            pendingInteraction.EndTime = hitTime;
            pendingInteraction.EndLaneIndex = laneIndex;
            pendingInteraction.SlideNodes.Add(new ChartNoteAuthoringData.PathNodeData
            {

                Time = hitTime,
                LaneIndex = laneIndex

            });

            if (laneIndex == currentLane)
            {

                AddRecordedInteraction(pendingInteraction);
                pendingInteraction = null;

            }
            else
            {

                statusMessage =
                    $"{hitTime:0.000}초에 {laneIndex + 1}번 슬라이드 노드를 추가했습니다.";

            }

        }

        private void RecordFlickLane(int laneIndex, double hitTime)
        {

            int endLane = laneIndex + (int)flickDefaultDirection;

            if (endLane < 0 || endLane >= chart.LaneCount)
            {

                statusMessage =
                    $"{laneIndex + 1}번 레인에서는 선택한 방향의 인접 플릭을 만들 수 없습니다.";
                return;

            }

            ChartNoteAuthoringData flick = ChartNoteAuthoringData.CreateTap(
                string.Empty,
                hitTime,
                laneIndex,
                chart.MusicalParts[selectedPartIndex].Id);
            flick.NoteType = ChartNoteType.Flick;
            flick.EndLaneIndex = endLane;
            AddRecordedInteraction(flick);

        }

        private void RecordBananaLane(int laneIndex, double hitTime)
        {

            if (pendingInteraction == null)
            {

                pendingInteraction = ChartNoteAuthoringData.CreateTap(
                    string.Empty,
                    hitTime,
                    laneIndex,
                    chart.MusicalParts[selectedPartIndex].Id);
                pendingInteraction.NoteType = ChartNoteType.Banana;
                pendingInteraction.EndTime = hitTime;
                pendingInteraction.EndLaneIndex = laneIndex;
                statusMessage =
                    $"{hitTime:0.000}초 {laneIndex + 1}번에서 바나나 노트를 시작했습니다.";
                return;

            }

            if (hitTime <= pendingInteraction.HitTime + DuplicateInputThresholdSeconds)
            {

                return;

            }

            pendingInteraction.EndTime = hitTime;
            pendingInteraction.EndLaneIndex = laneIndex;
            float startX = PlayfieldGeometry.GetLaneCenterNormalized(
                pendingInteraction.LaneIndex,
                chart.LaneCount);
            float endX = PlayfieldGeometry.GetLaneCenterNormalized(laneIndex, chart.LaneCount);
            float curveDirection = endX >= startX ? 1f : -1f;
            pendingInteraction.BananaCurveHandles.Add(
                new ChartNoteAuthoringData.CurveHandleData
                {

                    NormalizedTime = 0.5f,
                    NormalizedX = Mathf.Clamp01((startX + endX) * 0.5f + curveDirection * 0.16f)

                });
            GenerateBananaCheckpoints(pendingInteraction, 4);
            AddRecordedInteraction(pendingInteraction);
            pendingInteraction = null;

        }

        private void GenerateBananaCheckpoints(
            ChartNoteAuthoringData banana,
            int subdivisionsPerBeat)
        {

            double checkpointTime = ChartTempoMap.GetSubdivisionTimeAfter(
                chart.TempoSections,
                banana.HitTime,
                subdivisionsPerBeat);

            while (checkpointTime < banana.EndTime - ScheduleToleranceSeconds)
            {

                double normalizedTime = (checkpointTime - banana.HitTime) /
                                        (banana.EndTime - banana.HitTime);
                float startX = PlayfieldGeometry.GetLaneCenterNormalized(
                    banana.LaneIndex,
                    chart.LaneCount);
                float endX = PlayfieldGeometry.GetLaneCenterNormalized(
                    banana.EndLaneIndex,
                    chart.LaneCount);
                float handleX = banana.BananaCurveHandles[0].NormalizedX;
                float inverse = 1f - (float)normalizedTime;
                float curveX = inverse * inverse * startX +
                               2f * inverse * (float)normalizedTime * handleX +
                               (float)normalizedTime * (float)normalizedTime * endX;
                banana.BananaCheckpoints.Add(new ChartNoteAuthoringData.CheckpointData
                {

                    Time = checkpointTime,
                    NormalizedX = curveX

                });
                checkpointTime = ChartTempoMap.GetSubdivisionTimeAfter(
                    chart.TempoSections,
                    checkpointTime,
                    subdivisionsPerBeat);

            }

            if (banana.BananaCheckpoints.Count == 0)
            {

                double middleTime = (banana.HitTime + banana.EndTime) * 0.5d;
                banana.BananaCheckpoints.Add(new ChartNoteAuthoringData.CheckpointData
                {

                    Time = middleTime,
                    NormalizedX = banana.BananaCurveHandles[0].NormalizedX

                });

            }

        }

        private void AddRecordedInteraction(ChartNoteAuthoringData data)
        {

            RecordedNote recordedNote = new RecordedNote
            {

                hitTime = data.HitTime,
                originalHitTime = data.HitTime,
                laneIndex = data.LaneIndex,
                musicalPartId = data.MusicalPartId,
                hasOriginalHitTime = true,
                pendingAutomaticQuantization = true,
                noteData = data

            };
            Undo.RecordObject(this, "채보 입력 기록");
            recordedNotes.Add(recordedNote);
            bufferWasApplied = false;
            SortRecordedNotes();
            selectedRecordedNoteIndex = recordedNotes.IndexOf(recordedNote);
            statusMessage =
                $"{data.HitTime:0.000}초에 {data.NoteType} 노트를 기록했습니다.";

        }

        private void CreateTempoTapInputAction()
        {

            DisposeTempoTapInputAction();
            tempoTapInputAction = new InputAction(
                "다운비트 연속 측정",
                InputActionType.Button,
                "<Keyboard>/space");
            tempoTapInputAction.performed += OnTempoTapPerformed;

        }

        private void OnTempoTapPerformed(InputAction.CallbackContext context)
        {

            if (!tempoTapCapture)
            {

                return;

            }

            TryCaptureTempoDownbeat(context.time);

        }

        private void OnInputSystemEvent(InputEventPtr eventPointer, InputDevice device)
        {

            if (!tempoTapCapture || device is not Keyboard keyboard)
            {

                return;

            }

            if (!eventPointer.IsA<StateEvent>() && !eventPointer.IsA<DeltaStateEvent>())
            {

                return;

            }

            bool isSpacePressed = keyboard.spaceKey.ReadValueFromEvent(eventPointer) > 0f;

            if (!isSpacePressed)
            {

                tempoTapSpacePressed = false;
                return;

            }

            if (!tempoTapSpacePressed)
            {

                tempoTapSpacePressed = true;
                TryCaptureTempoDownbeat(eventPointer.time);

            }

        }

        private void StopTempoTapCapture()
        {

            tempoTapCapture = false;
            tempoTapInputAction?.Disable();

        }

        private void DisposeTempoTapInputAction()
        {

            if (tempoTapInputAction == null)
            {

                return;

            }

            tempoTapInputAction.performed -= OnTempoTapPerformed;
            tempoTapInputAction.Dispose();
            tempoTapInputAction = null;

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

            terminalFlickInputAction = new InputAction(
                "슬라이드 종단 플릭",
                InputActionType.Button);
            terminalFlickInputAction.AddBinding("<Keyboard>/digit0");
            terminalFlickInputAction.AddBinding("<Keyboard>/numpad0");
            terminalFlickInputAction.performed += context =>
                RecordTerminalFlickFromInputEvent(context.time);

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

            terminalFlickInputAction?.Enable();

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

            terminalFlickInputAction?.Disable();

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
            terminalFlickInputAction?.Dispose();
            terminalFlickInputAction = null;

        }

        private void RecordTerminalFlickFromInputEvent(double eventTimestamp)
        {

            if (!isRecording || recordingNoteMode != RecordingNoteMode.SlideAndHold)
            {

                return;

            }

            if (pendingInteraction == null ||
                pendingInteraction.NoteType != ChartNoteType.Slide ||
                pendingInteraction.SlideNodes.Count == 0)
            {

                statusMessage = "종단 플릭으로 바꿀 마지막 레인 이동이 없습니다.";
                return;

            }

            int previousLane = pendingInteraction.SlideNodes.Count > 1
                ? pendingInteraction.SlideNodes[^2].LaneIndex
                : pendingInteraction.LaneIndex;

            if (previousLane == pendingInteraction.EndLaneIndex)
            {

                statusMessage = "종단 플릭은 마지막 구간에 레인 이동이 있어야 합니다.";
                return;

            }

            pendingInteraction.SlideEndBehavior = SlideEndBehavior.Flick;
            AddRecordedInteraction(pendingInteraction);
            pendingInteraction = null;
            statusMessage =
                "마지막 레인 이동을 종단 플릭으로 바꾸고 슬라이드를 마쳤습니다.";
            Repaint();

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

            if (editorEvent == null || editorEvent.type != EventType.KeyDown)
            {

                return;

            }

            if (recordingPhase != RecordingPhase.Idle && editorEvent.keyCode == KeyCode.Escape)
            {

                StopRecording();
                editorEvent.Use();
                return;

            }

            if (recordingPhase != RecordingPhase.Idle && !isRecording)
            {

                return;

            }

            if (!isRecording && !EditorGUIUtility.editingTextField)
            {

                if (editorEvent.keyCode == KeyCode.Space && tempoTapCapture)
                {

                    TryCaptureTempoDownbeat(InputState.currentTime);
                    editorEvent.Use();
                    return;

                }

                if (editorEvent.keyCode == KeyCode.Space && songPlayback != null && songPlayback.IsPrepared)
                {

                    if (songPlayback.IsPaused)
                    {

                        songPlayback.Resume();

                    }
                    else
                    {

                        songPlayback.Pause();

                    }

                    editorEvent.Use();
                    return;

                }

                if (editorEvent.keyCode == KeyCode.R && recordingPhase == RecordingPhase.Idle)
                {

                    StartRecording(RecordingStartMode.CurrentPosition);
                    editorEvent.Use();
                    return;

                }

                if (editorEvent.keyCode == KeyCode.Delete &&
                    selectedRecordedNoteIndex >= 0 &&
                    selectedRecordedNoteIndex < recordedNotes.Count)
                {

                    Undo.RecordObject(this, "임시 채보 노트 삭제");
                    recordedNotes.RemoveAt(selectedRecordedNoteIndex);
                    selectedRecordedNoteIndex = Mathf.Clamp(
                        selectedRecordedNoteIndex,
                        -1,
                        recordedNotes.Count - 1);
                    bufferWasApplied = false;
                    editorEvent.Use();
                    return;

                }

                if (editorEvent.keyCode == KeyCode.Delete &&
                    !string.IsNullOrWhiteSpace(selectedChartNoteId))
                {

                    DeleteSelectedChartNote(true);
                    editorEvent.Use();
                    return;

                }

            }

            if (!isRecording)
            {

                return;

            }

            if (editorEvent.keyCode == KeyCode.Alpha0 ||
                editorEvent.keyCode == KeyCode.Keypad0)
            {

                RecordTerminalFlickFromInputEvent(InputState.currentTime);
                editorEvent.Use();
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

            int appliedNoteCount = recordedNotes.Count;
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
                    musicalPartId = note.MusicalPartId,
                    fullData = ChartNoteAuthoringData.FromChartNote(note)

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
                string noteId = GenerateNoteId(recordedNote.musicalPartId, usedIds);
                combinedNotes.Add(new EditableNote
                {

                    id = noteId,
                    hitTime = recordedNote.hitTime,
                    laneIndex = recordedNote.laneIndex,
                    musicalPartId = recordedNote.musicalPartId,
                    fullData = recordedNote.noteData == null
                        ? ChartNoteAuthoringData.CreateTap(
                            noteId,
                            recordedNote.hitTime,
                            recordedNote.laneIndex,
                            recordedNote.musicalPartId)
                        : recordedNote.noteData.CloneWithOffset(
                            recordedNote.hitTime - recordedNote.noteData.HitTime,
                            noteId)

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
                note.fullData.WriteTo(noteProperty);

            }

            if (addMissingActivationWindows)
            {

                AddMissingActivationWindows(serializedChart, recordedPartIds);

            }

            serializedChart.ApplyModifiedPropertiesWithoutUndo();

            if (addMissingActivationWindows)
            {

                ChartActivationWindowUtility.Normalize(chart);

            }

            EditorUtility.SetDirty(chart);
            Undo.CollapseUndoOperations(undoGroup);

            bool validationPassed = chart.TryValidate(out string error);

            if (validationPassed)
            {

                statusMessage = $"노트 {appliedNoteCount}개를 적용했고 차트 검사도 통과했습니다. 에셋을 저장하세요.";

            }
            else
            {

                statusMessage = $"노트는 적용했지만 차트 검사에 실패했습니다: {error}. 수정하거나 실행 취소하세요.";
                Debug.LogError(statusMessage, chart);

            }

            bufferWasApplied = true;

            if (clearBufferAfterApply && validationPassed)
            {

                Undo.RecordObject(this, "적용한 임시 기록 비우기");
                recordedNotes.Clear();
                selectedRecordedNoteIndex = -1;
                bufferWasApplied = false;

            }

        }

        private bool TrySelectRecordedNoteHorizontal(Rect contentRect, Vector2 mousePosition)
        {

            int closestIndex = -1;
            float closestDistance = TimelineNoteHitRadius;

            for (int index = 0; index < recordedNotes.Count; index++)
            {

                RecordedNote note = recordedNotes[index];

                if (note.hitTime < timelineStartTime || note.hitTime > timelineStartTime + timelineVisibleDuration)
                {

                    continue;

                }

                int partIndex = FindPartIndex(note.musicalPartId);
                Rect rowRect = new(
                    contentRect.x,
                    contentRect.y + TimelineRulerHeight + partIndex * TimelinePartHeight,
                    contentRect.width,
                    TimelinePartHeight);
                Vector2 notePosition = new(
                    TimeToTimelineX(contentRect, note.hitTime),
                    GetLaneTimelineY(rowRect, note.laneIndex));
                float distance = Vector2.Distance(mousePosition, notePosition);

                if (distance <= closestDistance)
                {

                    closestDistance = distance;
                    closestIndex = index;

                }

            }

            if (closestIndex < 0)
            {

                return false;

            }

            selectedRecordedNoteIndex = closestIndex;
            selectedChartNoteId = string.Empty;
            scrollPosition.y = Math.Max(
                0f,
                closestIndex * (EditorGUIUtility.singleLineHeight + 2f) - 60f);
            return true;

        }

        private bool TrySelectChartNoteHorizontal(Rect contentRect, Vector2 mousePosition)
        {

            ChartNote closestNote = null;
            float closestDistance = TimelineNoteHitRadius;

            for (int index = 0; index < chart.Notes.Count; index++)
            {

                ChartNote note = chart.Notes[index];

                if (note.HitTime < timelineStartTime ||
                    note.HitTime > timelineStartTime + timelineVisibleDuration)
                {

                    continue;

                }

                int partIndex = FindPartIndex(note.MusicalPartId);
                Rect rowRect = new(
                    contentRect.x,
                    contentRect.y + TimelineRulerHeight + partIndex * TimelinePartHeight,
                    contentRect.width,
                    TimelinePartHeight);
                Vector2 notePosition = new(
                    TimeToTimelineX(contentRect, note.HitTime),
                    GetLaneTimelineY(rowRect, note.LaneIndex));
                float distance = Vector2.Distance(mousePosition, notePosition);

                if (distance <= closestDistance)
                {

                    closestDistance = distance;
                    closestNote = note;

                }

            }

            if (closestNote == null)
            {

                return false;

            }

            selectedChartNoteId = closestNote.Id;
            selectedRecordedNoteIndex = -1;
            return true;

        }

        private void HandleTimelineScroll(Event currentEvent, Rect contentRect, bool vertical)
        {

            double duration = GetAuthoringDuration();
            float pointerNormalized = vertical
                ? Mathf.InverseLerp(contentRect.y, contentRect.yMax, currentEvent.mousePosition.y)
                : Mathf.InverseLerp(contentRect.x, contentRect.xMax, currentEvent.mousePosition.x);

            if (currentEvent.control || currentEvent.command)
            {

                double anchorTime = timelineStartTime + pointerNormalized * timelineVisibleDuration;
                double zoomFactor = Math.Pow(1.12d, currentEvent.delta.y);
                double nextVisibleDuration = Math.Max(4d, Math.Min(60d, timelineVisibleDuration * zoomFactor));
                timelineVisibleDuration = (float)nextVisibleDuration;
                timelineStartTime = anchorTime - pointerNormalized * nextVisibleDuration;

            }
            else
            {

                timelineStartTime += currentEvent.delta.y * timelineVisibleDuration * 0.04d;

            }

            double maximumStart = Math.Max(0d, duration - timelineVisibleDuration);
            timelineStartTime = Math.Max(0d, Math.Min(maximumStart, timelineStartTime));

            timelineAutoScroll = false;

            currentEvent.Use();
            Repaint();

        }

        private void AddMissingActivationWindows(
            SerializedObject serializedChart,
            HashSet<string> recordedPartIds)
        {

            SerializedProperty windowsProperty = serializedChart.FindProperty("activationWindows");

            foreach (string partId in recordedPartIds)
            {

                List<(double start, double end)> missingRanges = new();

                for (int index = 0; index < recordedNotes.Count; index++)
                {

                    RecordedNote note = recordedNotes[index];

                    if (note.musicalPartId != partId)
                    {

                        continue;

                    }

                    if (chart.IsPartActive(partId, note.hitTime))
                    {

                        continue;

                    }

                    double startTime = chart.TempoSections.Count > 0
                        ? ChartTempoMap.GetBarStartAtOrBefore(chart.TempoSections, note.hitTime)
                        : note.hitTime;
                    double endTime = chart.TempoSections.Count > 0
                        ? ChartTempoMap.GetBarStartAtOrAfter(chart.TempoSections, note.hitTime + 0.001d)
                        : note.hitTime + 0.001d;
                    missingRanges.Add((startTime, endTime));

                }

                if (missingRanges.Count == 0)
                {

                    continue;

                }

                missingRanges.Sort((left, right) => left.start.CompareTo(right.start));
                double mergedStart = missingRanges[0].start;
                double mergedEnd = missingRanges[0].end;

                for (int rangeIndex = 1; rangeIndex <= missingRanges.Count; rangeIndex++)
                {

                    if (rangeIndex < missingRanges.Count &&
                        missingRanges[rangeIndex].start <= mergedEnd + 0.000001d)
                    {

                        mergedEnd = Math.Max(mergedEnd, missingRanges[rangeIndex].end);
                        continue;

                    }

                    int newIndex = windowsProperty.arraySize;
                    windowsProperty.InsertArrayElementAtIndex(newIndex);
                    SerializedProperty window = windowsProperty.GetArrayElementAtIndex(newIndex);
                    window.FindPropertyRelative("musicalPartId").stringValue = partId;
                    window.FindPropertyRelative("startTime").doubleValue = mergedStart;
                    window.FindPropertyRelative("endTime").doubleValue = mergedEnd;

                    if (rangeIndex < missingRanges.Count)
                    {

                        mergedStart = missingRanges[rangeIndex].start;
                        mergedEnd = missingRanges[rangeIndex].end;

                    }

                }

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

            Undo.RecordObject(this, "임시 기록 지우기");
            recordedNotes.Clear();
            pendingInteraction = null;
            selectedRecordedNoteIndex = -1;
            selectedChartNoteId = string.Empty;
            bufferWasApplied = false;
            statusMessage = "임시 기록을 지웠습니다. 차트 원본은 바뀌지 않았습니다.";

        }

        private void Seek(double time)
        {

            if (recordingPhase != RecordingPhase.Idle)
            {

                StopRecording();

            }

            DisableGameplaySessionForAuthoring();

            if (songPlayback == null || !songPlayback.Seek(Math.Max(0d, time)))
            {

                statusMessage = "FMOD가 재생 위치 이동을 처리하지 못했습니다.";

            }

            EditorGUILayout.LabelField(
                "단축키: Space 재생/일시정지(연속 측정 중에는 다운비트 입력) · " +
                "R 현재 위치 녹화 · 0 슬라이드 종단 플릭 · Esc 녹화 중지 · " +
                "F 게임 플레이 플릭 보조 · Delete 선택 노트 삭제",
                EditorStyles.miniLabel);

        }

        private void ChangeChart(PrototypeChart selectedChart)
        {

            if (chart == selectedChart)
            {

                return;

            }

            if (recordedNotes.Count > 0 &&
                !EditorUtility.DisplayDialog(
                    "차트 변경",
                    "차트를 변경하면 현재 임시 기록이 지워집니다. 계속할까요?",
                    "변경하고 지우기",
                    "취소"))
            {

                return;

            }

            if (recordingPhase != RecordingPhase.Idle)
            {

                StopRecording();

            }

            chart = selectedChart;
            StopTempoCalibrationPreview();
            ClearTempoTapCapture();
            configuredEventPath = string.Empty;
            recordedNotes.Clear();
            pendingInteraction = null;
            selectedRecordedNoteIndex = -1;
            bufferWasApplied = false;
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
            loopBarCount = Math.Max(1, loopBarCount);
            loopEndBar = loopStartBar + loopBarCount;
            loopStart = ChartTempoMap.GetSongTime(chart.TempoSections, loopStartBar, 1);
            loopEnd = ChartTempoMap.GetSongTime(chart.TempoSections, loopEndBar, 1);
            ChartTempoSection loopTempo = ChartTempoMap.FindSectionForTime(chart.TempoSections, loopStart);
            timelineStartTime = Math.Max(0d, loopStart - loopTempo.SecondsPerBar);
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
            loopBarCount = Math.Max(1, loopEndBar - loopStartBar);
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
            ChartActivationWindowUtility.Normalize(chart);
            ChartBeatPosition startPosition = ChartTempoMap.GetBeatPosition(chart.TempoSections, startTime);
            ChartBeatPosition endPosition = ChartTempoMap.GetBeatPosition(chart.TempoSections, endTime);
            statusMessage =
                $"{partId} 활성 구간을 {startPosition.Bar}~{endPosition.Bar - 1}마디로 추가했습니다.";

        }

        private void ClearSelectedPartActivationWindows()
        {

            if (chart.MusicalParts.Count == 0)
            {

                return;

            }

            string partId = chart.MusicalParts[selectedPartIndex].Id;
            int removedCount = ChartActivationWindowUtility.RemovePart(chart, partId);
            statusMessage = removedCount > 0
                ? $"{partId} 활성 구간 {removedCount}개를 지웠습니다. Undo로 되돌릴 수 있습니다."
                : $"{partId}에 지울 활성 구간이 없습니다.";

        }

        private void DrawPatternDuplicationControls()
        {

            if (chart.MusicalParts.Count == 0)
            {

                return;

            }

            EditorGUILayout.Space(4f);
            bool nextShowPatternDuplication = EditorGUILayout.Foldout(
                showPatternDuplication,
                "선택 파트 패턴 복제",
                true);

            if (nextShowPatternDuplication && !showPatternDuplication)
            {

                ChartBeatPosition loopEndPosition = ChartTempoMap.GetBeatPosition(
                    chart.TempoSections,
                    loopEnd);
                patternTargetStartBar = Math.Max(1, loopEndPosition.Bar);

            }

            showPatternDuplication = nextShowPatternDuplication;

            if (!showPatternDuplication)
            {

                patternDuplicationPreview = null;
                return;

            }

            selectedPartIndex = EditorGUILayout.Popup(
                "복제할 음악 파트",
                selectedPartIndex,
                GetPartNames());
            ChartBeatPosition sourceStartPosition = ChartTempoMap.GetBeatPosition(
                chart.TempoSections,
                loopStart);
            ChartBeatPosition sourceEndPosition = ChartTempoMap.GetBeatPosition(
                chart.TempoSections,
                loopEnd);
            EditorGUILayout.LabelField(
                "원본 반복 구간",
                $"{sourceStartPosition.Bar}마디 → {sourceEndPosition.Bar}마디 직전");

            using (new EditorGUILayout.HorizontalScope())
            {

                patternTargetStartBar = Math.Max(
                    1,
                    EditorGUILayout.IntField("붙여넣기 시작 마디", patternTargetStartBar));

                if (GUILayout.Button("원본 다음 마디", GUILayout.Width(104f)))
                {

                    patternTargetStartBar = Math.Max(1, sourceEndPosition.Bar);

                }

            }

            patternRepeatCount = Math.Max(
                1,
                EditorGUILayout.IntField("반복 횟수", patternRepeatCount));
            patternConflictMode = (ChartPatternConflictMode)EditorGUILayout.Popup(
                "대상 구간 처리",
                (int)patternConflictMode,
                new[]
                {

                    "같은 파트 노트가 있으면 중단",
                    "대상 구간의 같은 파트 교체",
                    "기존 노트를 유지하고 추가"

                });
            patternAddActivationWindow = EditorGUILayout.Toggle(
                "대상 파트 활성 구간 자동 추가",
                patternAddActivationWindow);
            RefreshPatternDuplicationPreview();

            if (patternDuplicationPreview == null || !patternDuplicationPreview.IsValid)
            {

                string error = patternDuplicationPreview == null
                    ? "복제 미리보기를 계산할 수 없습니다."
                    : patternDuplicationPreview.Error;
                EditorGUILayout.HelpBox(error, MessageType.Error);
                return;

            }

            EditorGUILayout.HelpBox(
                $"미리보기 · 원본 {patternDuplicationPreview.SourceNoteCount}개 · " +
                $"생성 {patternDuplicationPreview.GeneratedNoteCount}개 · " +
                $"대상 {patternDuplicationPreview.TargetStartBar}~" +
                $"{patternDuplicationPreview.TargetEndBar - 1}마디 · " +
                $"기존 같은 파트 노트 {patternDuplicationPreview.ExistingTargetNoteCount}개",
                MessageType.Info);

            if (patternDuplicationPreview.InactiveGeneratedNoteCount > 0)
            {

                EditorGUILayout.HelpBox(
                    patternAddActivationWindow
                        ? $"활성 구간 밖 노트 {patternDuplicationPreview.InactiveGeneratedNoteCount}개를 위해 " +
                          "대상 구간을 선택 파트 활성 구간에 추가합니다."
                        : $"활성 구간 밖에 생성될 노트 {patternDuplicationPreview.InactiveGeneratedNoteCount}개는 " +
                          "게임에서 비활성 노트가 됩니다.",
                    patternAddActivationWindow ? MessageType.None : MessageType.Warning);

            }

            bool blockedByConflict =
                patternConflictMode == ChartPatternConflictMode.AbortIfOccupied &&
                patternDuplicationPreview.ExistingTargetNoteCount > 0;

            if (blockedByConflict)
            {

                EditorGUILayout.HelpBox(
                    "대상 구간에 같은 파트 노트가 있습니다. 다른 시작 마디를 선택하거나 대상 구간 처리 방식을 바꾸세요.",
                    MessageType.Warning);

            }

            if (songPlayback != null &&
                songPlayback.DurationSeconds > 0d &&
                patternDuplicationPreview.TargetEndTime > songPlayback.DurationSeconds + 0.000001d)
            {

                EditorGUILayout.HelpBox(
                    "복제 대상이 현재 오디오 길이를 넘어갑니다.",
                    MessageType.Warning);

            }

            EditorGUILayout.HelpBox(
                "청록색 테두리는 생성 예정 노트입니다. 적용하면 새 고유 ID를 만들고 전체 차트를 시간순으로 정렬합니다.",
                MessageType.None);
            GUI.enabled = patternDuplicationPreview.CanApply(patternConflictMode);

            if (GUILayout.Button(
                    $"{patternDuplicationPreview.TargetStartBar}~" +
                    $"{patternDuplicationPreview.TargetEndBar - 1}마디에 패턴 복제"))
            {

                string partId = chart.MusicalParts[selectedPartIndex].Id;
                string partName = chart.MusicalParts[selectedPartIndex].DisplayName;

                if (EditorUtility.DisplayDialog(
                        "선택 파트 패턴 복제",
                        $"{partName} [{partId}] 패턴을 " +
                        $"{patternDuplicationPreview.TargetStartBar}~" +
                        $"{patternDuplicationPreview.TargetEndBar - 1}마디에 복제할까요?\n\n" +
                        $"생성 노트: {patternDuplicationPreview.GeneratedNoteCount}개\n" +
                        $"기존 같은 파트 노트: {patternDuplicationPreview.ExistingTargetNoteCount}개\n" +
                        "적용 후에는 에셋 저장이 필요하며 Unity Undo로 되돌릴 수 있습니다.",
                        "복제 적용",
                        "취소"))
                {

                    ApplyPatternDuplication(partId);

                }

            }

            GUI.enabled = true;

        }

        private void RefreshPatternDuplicationPreview()
        {

            if (!showPatternDuplication ||
                chart == null ||
                chart.TempoSections.Count == 0 ||
                chart.MusicalParts.Count == 0)
            {

                patternDuplicationPreview = null;
                return;

            }

            ClampSelections();
            string partId = chart.MusicalParts[selectedPartIndex].Id;
            patternDuplicationPreview = ChartPatternDuplication.CreatePreview(
                chart,
                partId,
                loopStart,
                loopEnd,
                patternTargetStartBar,
                patternRepeatCount);

        }

        private void ApplyPatternDuplication(string partId)
        {

            bool applied = ChartPatternDuplication.TryApply(
                chart,
                partId,
                loopStart,
                loopEnd,
                patternTargetStartBar,
                patternRepeatCount,
                patternConflictMode,
                patternAddActivationWindow,
                out ChartPatternDuplicationPreview result);

            if (!applied)
            {

                statusMessage = result.IsValid
                    ? "대상 구간 충돌 때문에 패턴을 복제하지 않았습니다."
                    : $"패턴을 복제하지 못했습니다: {result.Error}";
                patternDuplicationPreview = result;
                return;

            }

            selectedChartNoteId = string.Empty;
            patternTargetStartBar = result.TargetEndBar;

            if (chart.TryValidate(out string error))
            {

                statusMessage =
                    $"{partId} 패턴 노트 {result.GeneratedNoteCount}개를 " +
                    $"{result.TargetStartBar}~{result.TargetEndBar - 1}마디에 복제했습니다. 에셋을 저장하세요.";

            }
            else
            {

                statusMessage = $"패턴을 복제했지만 차트 검사에 실패했습니다: {error}. 실행 취소하세요.";
                Debug.LogError(statusMessage, chart);

            }

            RefreshPatternDuplicationPreview();

        }

        private void DrawAppliedNoteDeletionControls()
        {

            if (chart.MusicalParts.Count == 0)
            {

                return;

            }

            EditorGUILayout.Space(4f);
            EditorGUILayout.LabelField("적용된 노트 삭제", EditorStyles.miniBoldLabel);
            ChartNote selectedNote = FindSelectedChartNote();

            using (new EditorGUILayout.HorizontalScope())
            {

                string selectedDescription = selectedNote == null
                    ? "선택된 적용 노트 없음"
                    : $"{selectedNote.MusicalPartId} · {selectedNote.HitTime:0.000}초 · " +
                      $"위치 {selectedNote.LaneIndex + 1} · {selectedNote.Id}";
                EditorGUILayout.LabelField(selectedDescription);
                GUI.enabled = selectedNote != null;

                if (GUILayout.Button("선택 노트 삭제", GUILayout.Width(112f)))
                {

                    DeleteSelectedChartNote(true);

                }

                GUI.enabled = true;

            }

            string partId = chart.MusicalParts[selectedPartIndex].Id;
            string partName = chart.MusicalParts[selectedPartIndex].DisplayName;
            int rangeNoteCount = ChartAuthoringNoteUtility.CountNotesInRange(
                chart,
                partId,
                loopStart,
                loopEnd);

            using (new EditorGUILayout.HorizontalScope())
            {

                EditorGUILayout.LabelField(
                    $"현재 반복 구간 · {partName} · 적용 노트 {rangeNoteCount}개");
                GUI.enabled = loopEnd > loopStart && rangeNoteCount > 0;

                if (GUILayout.Button("범위 노트 삭제", GUILayout.Width(112f)))
                {

                    DeleteChartNotesInLoop(partId, partName, rangeNoteCount);

                }

                GUI.enabled = true;

            }

            EditorGUILayout.HelpBox(
                "삭제는 차트 에셋에 즉시 반영되며 Unity Undo로 되돌릴 수 있습니다. " +
                "영구 저장하려면 아래의 에셋 저장을 누르세요.",
                MessageType.None);

        }

        private void DrawAppliedNoteQuantizationControls()
        {

            if (chart.MusicalParts.Count == 0)
            {

                return;

            }

            EditorGUILayout.Space(4f);
            showAppliedNoteQuantization = EditorGUILayout.Foldout(
                showAppliedNoteQuantization,
                "저장된 파트 채보 박자 보정",
                true);

            if (!showAppliedNoteQuantization)
            {

                return;

            }

            selectedPartIndex = EditorGUILayout.Popup(
                "보정할 음악 파트",
                selectedPartIndex,
                GetPartNames());
            appliedQuantizationRange = (AppliedQuantizationRange)EditorGUILayout.Popup(
                "보정 범위",
                (int)appliedQuantizationRange,
                new[] { "선택 파트 전체", "현재 반복 구간" });

            string partId = chart.MusicalParts[selectedPartIndex].Id;
            string partName = chart.MusicalParts[selectedPartIndex].DisplayName;
            double rangeStart = appliedQuantizationRange == AppliedQuantizationRange.WholeChart
                ? 0d
                : loopStart;
            double rangeEnd = appliedQuantizationRange == AppliedQuantizationRange.WholeChart
                ? double.PositiveInfinity
                : loopEnd;
            bool hasValidRange = rangeEnd > rangeStart;
            AppliedNoteQuantizationSummary preview = hasValidRange
                ? ChartAuthoringNoteUtility.PreviewQuantization(
                    chart,
                    partId,
                    rangeStart,
                    rangeEnd,
                    (int)quantizationGrid,
                    maximumQuantizationMilliseconds / 1000d,
                    quantizationStrength,
                    inputAdvanceMilliseconds / 1000d,
                    chordGroupingMilliseconds / 1000d)
                : new AppliedNoteQuantizationSummary();

            if (!hasValidRange)
            {

                EditorGUILayout.HelpBox("현재 반복 구간이 올바르지 않습니다.", MessageType.Error);

            }
            else if (preview.CandidateCount == 0)
            {

                EditorGUILayout.HelpBox($"선택 범위에 {partName} 노트가 없습니다.", MessageType.None);

            }
            else
            {

                EditorGUILayout.HelpBox(
                    $"미리보기 · 대상 {preview.CandidateCount}개 · 격자 보정 {preview.QuantizedCount}개 · " +
                    $"허용 범위 밖 {preview.OutsideWindowCount}개 · 시간 변경 {preview.ChangedCount}개\n" +
                    $"변경 범위 {preview.MinimumCorrectionSeconds * 1000d:+0.0;-0.0;0.0}ms ~ " +
                    $"{preview.MaximumCorrectionSeconds * 1000d:+0.0;-0.0;0.0}ms",
                    MessageType.Info);

            }

            EditorGUILayout.HelpBox(
                "위의 입력 박자 보정 설정을 사용합니다. 적용 시 선택한 파트의 저장된 노트 시간만 " +
                "변경하며, 노트 ID·위치·파트는 유지됩니다. Unity Undo로 되돌릴 수 있습니다.",
                MessageType.None);

            GUI.enabled = hasValidRange && preview.CandidateCount > 0 && preview.ChangedCount > 0;

            if (GUILayout.Button("미리보기대로 저장된 노트 보정 적용"))
            {

                string rangeDescription = appliedQuantizationRange == AppliedQuantizationRange.WholeChart
                    ? "차트 전체"
                    : $"{loopStart:0.000}초 이상 {loopEnd:0.000}초 미만";

                if (EditorUtility.DisplayDialog(
                        "저장된 파트 채보 박자 보정",
                        $"{partName} [{partId}]의 저장된 노트 {preview.ChangedCount}개 시간을 변경할까요?\n\n" +
                        $"범위: {rangeDescription}\n" +
                        "적용 후에는 에셋 저장이 필요하며, 저장 전에는 Unity Undo로 되돌릴 수 있습니다.",
                        "보정 적용",
                        "취소"))
                {

                    ApplyQuantizationToStoredPart(partId, rangeStart, rangeEnd);

                }

            }

            GUI.enabled = true;

        }

        private void ApplyQuantizationToStoredPart(string partId, double startTime, double endTime)
        {

            AppliedNoteQuantizationSummary result = ChartAuthoringNoteUtility.QuantizeNotes(
                chart,
                partId,
                startTime,
                endTime,
                (int)quantizationGrid,
                maximumQuantizationMilliseconds / 1000d,
                quantizationStrength,
                inputAdvanceMilliseconds / 1000d,
                chordGroupingMilliseconds / 1000d);
            selectedChartNoteId = string.Empty;

            if (chart.TryValidate(out string error))
            {

                statusMessage =
                    $"{partId} 저장 노트 {result.ChangedCount}개를 보정했습니다. " +
                    $"허용 범위 밖 {result.OutsideWindowCount}개에는 입력 오프셋만 적용했습니다. 에셋을 저장하세요.";

            }
            else
            {

                statusMessage = $"노트를 보정했지만 차트 검사에 실패했습니다: {error}. 실행 취소하세요.";
                Debug.LogError(statusMessage, chart);

            }

        }

        private ChartNote FindSelectedChartNote()
        {

            if (string.IsNullOrWhiteSpace(selectedChartNoteId))
            {

                return null;

            }

            for (int index = 0; index < chart.Notes.Count; index++)
            {

                ChartNote note = chart.Notes[index];

                if (note.Id == selectedChartNoteId)
                {

                    return note;

                }

            }

            selectedChartNoteId = string.Empty;
            return null;

        }

        private bool DeleteSelectedChartNote(bool requestConfirmation)
        {

            ChartNote selectedNote = FindSelectedChartNote();

            if (selectedNote == null)
            {

                return false;

            }

            if (requestConfirmation &&
                !EditorUtility.DisplayDialog(
                    "적용된 노트 삭제",
                    $"'{selectedNote.Id}' 노트를 삭제할까요?\n\n" +
                    $"파트: {selectedNote.MusicalPartId}\n" +
                    $"시간: {selectedNote.HitTime:0.000000}초\n" +
                    $"위치: {selectedNote.LaneIndex + 1}",
                    "삭제",
                    "취소"))
            {

                return false;

            }

            string deletedNoteId = selectedNote.Id;
            bool deleted = ChartAuthoringNoteUtility.DeleteNote(chart, deletedNoteId);

            if (deleted)
            {

                selectedChartNoteId = string.Empty;
                statusMessage = $"적용된 노트 '{deletedNoteId}'를 삭제했습니다. 에셋 저장 전에는 Undo로 되돌릴 수 있습니다.";
                Repaint();

            }

            return deleted;

        }

        private void DeleteChartNotesInLoop(string partId, string partName, int noteCount)
        {

            ChartBeatPosition startPosition = ChartTempoMap.GetBeatPosition(
                chart.TempoSections,
                loopStart);
            ChartBeatPosition endPosition = ChartTempoMap.GetBeatPosition(
                chart.TempoSections,
                loopEnd);

            if (!EditorUtility.DisplayDialog(
                    "반복 구간의 적용 노트 삭제",
                    $"현재 반복 구간에서 {partName} 적용 노트 {noteCount}개를 삭제할까요?\n\n" +
                    $"범위: {startPosition.Bar}마디 {startPosition.Beat}박부터 " +
                    $"{endPosition.Bar}마디 {endPosition.Beat}박 직전까지\n" +
                    $"시간: {loopStart:0.000}초 이상, {loopEnd:0.000}초 미만",
                    "모두 삭제",
                    "취소"))
            {

                return;

            }

            int deletedCount = ChartAuthoringNoteUtility.DeleteNotesInRange(
                chart,
                partId,
                loopStart,
                loopEnd);
            selectedChartNoteId = string.Empty;
            statusMessage =
                $"현재 반복 구간의 {partName} 적용 노트 {deletedCount}개를 삭제했습니다. " +
                "에셋 저장 전에는 Undo로 되돌릴 수 있습니다.";
            Repaint();

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

        private void UpdateTimelineAutoScroll()
        {

            double maximumStart = Math.Max(0d, GetAuthoringDuration() - timelineVisibleDuration);
            double targetStart = songPlayback.SongTime - timelineVisibleDuration * 0.32d;
            timelineStartTime = Math.Max(0d, Math.Min(maximumStart, targetStart));

        }

        private void QuantizeRecordedNotes(bool selectedOnly, bool pendingOnly)
        {

            if (chart == null || chart.TempoSections.Count == 0 || recordedNotes.Count == 0)
            {

                return;

            }

            List<RecordedNote> candidates = new();

            for (int index = 0; index < recordedNotes.Count; index++)
            {

                RecordedNote note = recordedNotes[index];

                if (selectedOnly && index != selectedRecordedNoteIndex)
                {

                    continue;

                }

                if (pendingOnly && !note.pendingAutomaticQuantization)
                {

                    continue;

                }

                if (!note.hasOriginalHitTime)
                {

                    note.originalHitTime = note.hitTime;
                    note.hasOriginalHitTime = true;

                }

                candidates.Add(note);

            }

            if (candidates.Count == 0)
            {

                statusMessage = "보정할 새 입력이 없습니다.";
                return;

            }

            Undo.RecordObject(this, "임시 채보 박자 보정");
            candidates.Sort((left, right) => left.originalHitTime.CompareTo(right.originalHitTime));
            double chordRangeSeconds = chordGroupingMilliseconds / 1000d;
            double inputAdvanceSeconds = inputAdvanceMilliseconds / 1000d;
            double maximumCorrectionSeconds = maximumQuantizationMilliseconds / 1000d;
            int quantizedCount = 0;
            int outsideWindowCount = 0;
            int candidateIndex = 0;

            while (candidateIndex < candidates.Count)
            {

                int groupEnd = candidateIndex + 1;
                double groupStartTime = candidates[candidateIndex].originalHitTime;

                while (groupEnd < candidates.Count &&
                       candidates[groupEnd].originalHitTime - groupStartTime <= chordRangeSeconds)
                {

                    groupEnd++;

                }

                int middleIndex = candidateIndex + (groupEnd - candidateIndex - 1) / 2;
                double representativeTime = candidates[middleIndex].originalHitTime;
                ChartQuantizationResult result = ChartQuantization.Quantize(
                    chart.TempoSections,
                    representativeTime,
                    (int)quantizationGrid,
                    maximumCorrectionSeconds,
                    quantizationStrength,
                    inputAdvanceSeconds);

                if (result.WasQuantized)
                {

                    quantizedCount += groupEnd - candidateIndex;

                }
                else
                {

                    outsideWindowCount += groupEnd - candidateIndex;

                }

                for (int index = candidateIndex; index < groupEnd; index++)
                {

                    SetRecordedNoteHitTime(candidates[index], result.CorrectedTime);
                    candidates[index].pendingAutomaticQuantization = false;

                }

                candidateIndex = groupEnd;

            }

            bufferWasApplied = false;
            SortRecordedNotes();
            statusMessage =
                $"임시 노트 {quantizedCount}개를 보정했습니다. 허용 범위 밖 {outsideWindowCount}개는 입력 오프셋만 적용했습니다.";

        }

        private void RestoreOriginalRecordedTimes()
        {

            Undo.RecordObject(this, "임시 채보 원본 시간 복원");
            int restoredCount = 0;

            for (int index = 0; index < recordedNotes.Count; index++)
            {

                RecordedNote note = recordedNotes[index];

                if (!note.hasOriginalHitTime)
                {

                    continue;

                }

                SetRecordedNoteHitTime(note, note.originalHitTime);
                note.pendingAutomaticQuantization = false;
                restoredCount++;

            }

            bufferWasApplied = false;
            SortRecordedNotes();
            statusMessage = $"임시 노트 {restoredCount}개의 원본 입력 시간을 복원했습니다.";

        }

        private void NudgeSelected(double deltaSeconds)
        {

            if (selectedRecordedNoteIndex < 0 || selectedRecordedNoteIndex >= recordedNotes.Count)
            {

                return;

            }

            Undo.RecordObject(this, "임시 채보 노트 시간 이동");
            RecordedNote note = recordedNotes[selectedRecordedNoteIndex];
            double nextTime = Math.Max(0d, note.hitTime + deltaSeconds);
            SetRecordedNoteHitTime(note, nextTime);
            note.originalHitTime = nextTime;
            note.hasOriginalHitTime = true;
            note.pendingAutomaticQuantization = false;
            bufferWasApplied = false;
            SortRecordedNotes();

        }

        private static void SetRecordedNoteHitTime(RecordedNote note, double hitTime)
        {

            double clampedTime = Math.Max(0d, hitTime);

            if (note.noteData != null)
            {

                note.noteData.ShiftTimes(clampedTime - note.noteData.HitTime);

            }

            note.hitTime = clampedTime;

        }

        private static void SynchronizeRecordedNoteIdentity(RecordedNote note)
        {

            if (note.noteData == null)
            {

                note.noteData = ChartNoteAuthoringData.CreateTap(
                    string.Empty,
                    note.hitTime,
                    note.laneIndex,
                    note.musicalPartId);
                return;

            }

            int previousStartLane = note.noteData.LaneIndex;
            note.noteData.LaneIndex = note.laneIndex;
            note.noteData.MusicalPartId = note.musicalPartId;

            if (note.noteData.NoteType == ChartNoteType.Tap ||
                note.noteData.NoteType == ChartNoteType.Hold)
            {

                note.noteData.EndLaneIndex = note.laneIndex;

            }
            else if (note.noteData.NoteType == ChartNoteType.Flick &&
                     note.noteData.EndLaneIndex == previousStartLane)
            {

                note.noteData.EndLaneIndex = note.laneIndex;

            }

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

            if (!string.IsNullOrWhiteSpace(selectedChartNoteId) &&
                FindSelectedChartNote() == null)
            {

                selectedChartNoteId = string.Empty;

            }

        }

    }

}
