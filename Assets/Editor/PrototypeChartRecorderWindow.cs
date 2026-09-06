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

    public sealed partial class PrototypeChartRecorderWindow : EditorWindow
    {

        private const string GameplayScenePath = "Assets/Scenes/Gameplay.unity";
        private const int SupportedKeyboardLaneCount = 8;
        private const double DuplicateInputThresholdSeconds = 0.010d;
        private const double DuplicateTempoTapThresholdSeconds = 0.050d;
        private const double ScheduleToleranceSeconds = 0.000001d;
        private const double MetronomeScheduleLeadSeconds = 0.4d;
        private static readonly string[] QuantizationGridNames =
        {

            "1/8음표",
            "1/8 셋잇단음",
            "1/16음표",
            "1/16 셋잇단음",
            "1/32음표"

        };
        private static readonly int[] QuantizationGridValues = { 2, 3, 4, 6, 8 };
        private static readonly string[] RecordingNoteModeNames =
        {

            "탭",
            "홀드·슬라이드",
            "플릭",
            "바나나"

        };
        private static readonly string[] FlickDirectionNames = { "왼쪽", "오른쪽" };
        private static readonly int[] FlickDirectionValues = { -1, 1 };
        private static readonly string[] SlideEndBehaviorNames = { "일반 종료", "종단 플릭" };

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
        // Inline Unity serialization turns null into a default note during Undo snapshots.
        // Incomplete input is transient; only completed recordings belong in the saved buffer.
        [NonSerialized] private ChartNoteAuthoringData pendingInteraction;
        [SerializeField] private int draggedSlidePointIndex = int.MinValue;
        [SerializeField] private Vector2 draggedSlidePointOffset;
        [SerializeField] private int draggedSimplePointIndex = int.MinValue;
        [SerializeField] private double interactionEditorRangeStart;
        [SerializeField] private double interactionEditorRangeEnd;
        [SerializeField] private bool interactionEditorRangeLocked;
        [SerializeField] private int selectedPartIndex;
        [SerializeField] private int selectedRecordedNoteIndex = -1;
        [SerializeField] private string selectedChartNoteId = string.Empty;
        [SerializeField] private string selectedAppliedNoteDataId = string.Empty;
        [SerializeField] private ChartNoteAuthoringData selectedAppliedNoteData;
        [SerializeField] private double seekTime;
        [SerializeField] private double loopStart;
        [SerializeField] private double loopEnd = 8d;
        [SerializeField] private ApplyMode applyMode;
        [SerializeField] private bool addMissingActivationWindows;
        [SerializeField, Min(1)] private int countInBars = 2;
        [SerializeField, Range(0f, 1f)] private float metronomeVolume = 0.15f;
        [SerializeField, Range(-50f, 50f)] private float metronomeOutputOffsetMilliseconds;
        [SerializeField] private bool metronomeDuringRecording;
        [SerializeField, Min(1f)] private float timelineVisibleDuration = 16f;
        [SerializeField, Min(0f)] private double timelineStartTime;
        [SerializeField, Min(1)] private int loopStartBar = 1;
        [SerializeField, Min(2)] private int loopEndBar = 5;
        [SerializeField, Min(1)] private int loopBarCount = 4;
        [SerializeField] private TimelineViewMode timelineViewMode;
        [FormerlySerializedAs("verticalTimelineAutoScroll")]
        [SerializeField] private bool timelineAutoScroll = true;
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
        [SerializeField] private bool tempoCalibrationFixedBpm = true;
        [SerializeField] private bool showManualTempoAnchors;
        [SerializeField, Min(1)] private int tempoTapStartBar = 1;
        [SerializeField, Min(1)] private int tempoTapBarInterval = 1;

        private readonly double[] lastRecordedInputTimestamps = new double[SupportedKeyboardLaneCount];
        private readonly List<ChartTempoAnchor> tempoTapAnchors = new();
        private InputAction[] recordingInputActions;
        private InputAction terminalFlickInputAction;
        private InputAction tempoTapInputAction;
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
            window.minSize = new Vector2(700f, 600f);

            if (!window.workspaceInitialized)
            {

                window.position = new Rect(80f, 80f, 1200f, 800f);

            }

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
            Undo.undoRedoPerformed += HandleUndoRedo;

        }

        private void OnDisable()
        {

            EditorApplication.update -= EditorUpdate;
            InputSystem.onEvent -= OnInputSystemEvent;
            Undo.undoRedoPerformed -= HandleUndoRedo;
            ResetRecordingSession();
            tempoCalibrationPreview = false;
            tempoTapCapture = false;
            DestroyMetronome();
            DisposeRecordingInputActions();
            DisposeTempoTapInputAction();

        }

        private void OnGUI()
        {

            HandleRecorderWindowKeyboardEvent(Event.current);
            DrawAuthoringWorkspace();

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

        private void DrawTempoCalibration()
        {

            showTempoCalibration = EditorGUILayout.Foldout(
                showTempoCalibration,
                "박자 설정 · 시작점 보정",
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
                DrawTempoSummary(
                    "현재 기준",
                    $"1마디 1박 {currentTempo.StartTime:0.000000}초 · " +
                    $"{currentTempo.BeatsPerMinute:0.######} BPM · " +
                    $"{currentTempo.BeatsPerBar}/{currentTempo.BeatUnit}");
                EditorGUI.BeginChangeCheck();
                bool fixedBpm = EditorGUILayout.ToggleLeft("현재 BPM 고정 · 시작점만 보정", tempoCalibrationFixedBpm);

                if (EditorGUI.EndChangeCheck())
                {

                    ClearTempoTapCapture();
                    StopTempoCalibrationPreview();
                    tempoCalibrationFixedBpm = fixedBpm;
                    tempoCalibrationFineOffsetMilliseconds = 0f;

                }

                EditorGUILayout.HelpBox(tempoCalibrationFixedBpm
                    ? $"{currentTempo.BeatsPerMinute:0.######} BPM을 유지합니다. 알고 있는 마디의 첫 박을 여러 번 찍으면 1마디 시작 시간을 계산합니다."
                    : "여러 마디의 첫 박으로 BPM과 시작 시간을 함께 계산합니다.", MessageType.None);
                DrawTempoTapCalibration();

                showManualTempoAnchors = EditorGUILayout.Foldout(showManualTempoAnchors, "수동 기준 A / B", true);
                if (showManualTempoAnchors)
                {

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

                }

                ChartTempoCalibrationResult result = GetTempoCalibrationResult();

                if (!result.IsValid)
                {

                    if ((hasTempoAnchorA && hasTempoAnchorB) || tempoTapAnchors.Count >= 2)
                    {

                        EditorGUILayout.HelpBox(result.Error, MessageType.Error);

                    }

                    return;

                }

                GUILayout.Label("전체 박자 미세 이동(ms)", EditorStyles.wordWrappedLabel);
                tempoCalibrationFineOffsetMilliseconds = EditorGUILayout.Slider(
                    tempoCalibrationFineOffsetMilliseconds,
                    -250f,
                    250f);
                double adjustedFirstDownbeat =
                    result.FirstDownbeatTime + tempoCalibrationFineOffsetMilliseconds / 1000d;
                double bpmDifference = result.BeatsPerMinute - currentTempo.BeatsPerMinute;
                double downbeatDifferenceMilliseconds =
                    (adjustedFirstDownbeat - currentTempo.StartTime) * 1000d;
                DrawTempoSummary(
                    "계산 결과",
                    $"{result.BeatsPerMinute:0.######} BPM · 1마디 1박 {adjustedFirstDownbeat:0.000000}초");
                DrawTempoSummary(
                    "현재 설정과 차이",
                    $"BPM {bpmDifference:+0.######;-0.######;0} · " +
                    $"박자 원점 {downbeatDifferenceMilliseconds:+0.0;-0.0;0.0}ms");
                DrawTempoSummary(
                    "측정 간격",
                    $"{result.BeatDistance}박 · {result.TimeDistance:0.000}초");
                DrawTempoSummary("측정 오차", $"{result.AnchorCount}회 · RMS {result.RootMeanSquareError * 1000d:0.0}ms");
                EditorGUILayout.HelpBox("변속 전 일정한 구간에서 측정하세요. 오차가 작아도 탭 반응 지연은 남을 수 있으니 메트로놈으로 확인하세요.", MessageType.None);

                if (songPlayback != null && songPlayback.IsPrepared)
                {

                    double nearestBeat = GetNearestCalibrationBeatTime(
                        songPlayback.SongTime,
                        adjustedFirstDownbeat,
                        result.BeatsPerMinute,
                        currentTempo.BeatUnit);
                    double currentErrorMilliseconds = (songPlayback.SongTime - nearestBeat) * 1000d;
                    DrawTempoSummary(
                        "현재 위치와 가까운 계산 박자",
                        $"{currentErrorMilliseconds:+0.0;-0.0;0.0}ms");

                }

                using (new EditorGUILayout.VerticalScope())
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

        private static void DrawTempoSummary(string label, string value)
        {

            GUILayout.Label(label, EditorStyles.miniBoldLabel);
            GUILayout.Label(value, EditorStyles.wordWrappedLabel);

        }

        private void DrawTempoTapCalibration()
        {

            EditorGUILayout.Space(6f);
            EditorGUILayout.LabelField("여러 마디 다운비트 연속 측정", EditorStyles.miniBoldLabel);
            EditorGUILayout.HelpBox(
                "시작 마디를 지정한 뒤 음악을 들으며 각 마디의 1박에 스페이스바를 누르세요. " +
                "두 번째 입력부터 모든 탭으로 시작점을 계산합니다. 탭 간격 1은 매 마디, 4는 네 마디마다입니다.",
                MessageType.None);

            using (new EditorGUI.DisabledScope(tempoTapCapture))
            {

                tempoTapStartBar = Math.Max(1, EditorGUILayout.IntField("시작 마디", tempoTapStartBar));
                tempoTapBarInterval = Math.Max(
                    1,
                    EditorGUILayout.IntField("탭 간격(마디)", tempoTapBarInterval));

            }

            using (new EditorGUILayout.VerticalScope())
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

                DrawTempoSummary(
                    "연속 탭",
                    $"{tempoTapAnchors.Count}개 · " +
                    $"{tempoTapAnchors[0].Bar}~{tempoTapAnchors[^1].Bar}마디");

            }

            if (tempoTapResult.IsValid)
            {

                DrawTempoSummary(
                    "다중 앵커 오차",
                    $"RMS {tempoTapResult.RootMeanSquareError * 1000d:0.0}ms · " +
                    $"{tempoTapResult.AnchorCount}개 측정값");

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
            hasTempoAnchorA = false;
            hasTempoAnchorB = false;
            tempoAnchorsFromTapCapture = false;
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
                currentTempo.BeatUnit,
                tempoCalibrationFixedBpm ? currentTempo.BeatsPerMinute : null);

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
                $"{tempoTapAnchors.Count}회 측정 · {result.BeatsPerMinute:0.######} BPM · 첫 박 {result.FirstDownbeatTime:0.000000}초";

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

            GUILayout.Space(8f);
            GUILayout.Label(label, EditorStyles.miniBoldLabel);
            EditorGUI.BeginChangeCheck();
            bar = Math.Max(1, EditorGUILayout.IntField("마디", bar));
            beat = Mathf.Clamp(EditorGUILayout.IntField("박", beat), 1, Math.Max(1, beatsPerBar));
            songTime = Math.Max(0d, EditorGUILayout.DoubleField("시간(초)", songTime));

            if (EditorGUI.EndChangeCheck())
            {

                hasAnchor = true;
                MarkTempoAnchorManuallyEdited();

            }

            using (new EditorGUILayout.HorizontalScope())
            {

                using (new EditorGUI.DisabledScope(!Application.isPlaying || songPlayback == null || !songPlayback.IsPrepared))
                {

                    if (GUILayout.Button("현재 위치 지정"))
                    {

                        songTime = songPlayback.SongTime;
                        hasAnchor = true;
                        MarkTempoAnchorManuallyEdited();
                        StopTempoCalibrationPreview();

                    }

                    using (new EditorGUI.DisabledScope(!hasAnchor))
                    {

                        if (GUILayout.Button("이동"))
                        {

                            Seek(songTime);

                        }

                    }

                }

            }

            using (new EditorGUILayout.HorizontalScope())
            {

                double adjustment = 0d;
                if (GUILayout.Button("-10ms"))
                {

                    adjustment = -0.010d;

                }
                if (GUILayout.Button("-1ms"))
                {

                    adjustment = -0.001d;

                }
                if (GUILayout.Button("+1ms"))
                {

                    adjustment = 0.001d;

                }
                if (GUILayout.Button("+10ms"))
                {

                    adjustment = 0.010d;

                }

                if (adjustment != 0d)
                {

                    songTime = Math.Max(0d, songTime + adjustment);
                    hasAnchor = true;
                    MarkTempoAnchorManuallyEdited();

                }

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
            if (tempoAnchorsFromTapCapture && tempoTapAnchors.Count >= 2)
            {

                return ChartTempoCalibration.Calculate(tempoTapAnchors, tempo.BeatsPerBar, tempo.BeatUnit,
                    tempoCalibrationFixedBpm ? tempo.BeatsPerMinute : null);

            }

            return ChartTempoCalibration.Calculate(
                new[]
                {

                    new ChartTempoAnchor(tempoAnchorABar, tempoAnchorABeat, tempoAnchorATime),
                    new ChartTempoAnchor(tempoAnchorBBar, tempoAnchorBBeat, tempoAnchorBTime)

                },
                tempo.BeatsPerBar,
                tempo.BeatUnit,
                tempoCalibrationFixedBpm ? tempo.BeatsPerMinute : null);

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
            if (!tempoCalibrationFixedBpm)
            {

                firstSection.FindPropertyRelative("beatsPerMinute").doubleValue = result.BeatsPerMinute;

            }
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

                if (GUILayout.Button("선택한 파트만 듣기") && CanWorkspaceAuditionSelectedPart())
                {

                    SoloSelectedPart();

                }

            }

        }

        private void EditorUpdate()
        {

            if (!Application.isPlaying)
            {

                songPlayback = null;

                if (recordingPhase != RecordingPhase.Idle || isRecording || isLoopRecording || pendingInteraction != null)
                {

                    ResetRecordingSession();

                }

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

            ResetRecordingSession();
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

        private void ResetRecordingSession()
        {

            // Lifecycle resets discard incomplete gestures without quantizing or clearing completed recordings.
            pendingInteraction = null;
            isRecording = false;
            isLoopRecording = false;
            recordingPhase = RecordingPhase.Idle;
            DisableRecordingInputActions();
            metronome?.StopAll();
            nextMetronomeSongTime = double.NaN;
            Array.Clear(lastRecordedInputTimestamps, 0, lastRecordedInputTimestamps.Length);
            draggedSlidePointIndex = int.MinValue;
            draggedSimplePointIndex = int.MinValue;
            draggedSlidePointOffset = Vector2.zero;
            interactionEditorRangeLocked = false;
            workspaceDraggedBananaHandle = -1;

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

            // A new take or a short loop restarts the song clock. Discard the
            // previous cycle's cursor or opening clicks wait for its old time.
            nextMetronomeSongTime = double.NaN;
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

            if (metronomeDuringRecording)
            {

                // In loop mode recording can begin exactly on a beat. Waiting
                // for ActivateRecording would miss the minimum DSP scheduling lead.
                double firstRecordingBeat = ChartAuthoringMetronome.GetBeatTimeAtOrAfter(
                    chart.TempoSections, recordingTargetTime);
                ulong firstRecordingClock = AddSongTimeToDspClock(songStartDspClock,
                    firstRecordingBeat + metronomeOutputOffsetMilliseconds / 1000d, sampleRate);
                if (!metronome.ScheduleAtDspClock(firstRecordingClock,
                    ChartAuthoringMetronome.IsDownbeat(chart.TempoSections, firstRecordingBeat), metronomeVolume))
                {

                    metronome.StopAll();
                    songPlayback.Stop();
                    statusMessage = "녹화 첫 박을 충분히 미리 예약하지 못했습니다. 다시 시도하세요.";
                    recordingPhase = RecordingPhase.Idle;
                    return;

                }

                nextMetronomeSongTime = ChartAuthoringMetronome.GetBeatTimeAfter(chart.TempoSections, firstRecordingBeat);

            }

            countInEndRealtime = EditorApplication.timeSinceStartup +
                Math.Max(0d, recordingTargetTime - songPlayback.TimelineTime);
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

                double adjustedClickTime = nextMetronomeSongTime +
                    metronomeOutputOffsetMilliseconds / 1000d;

                if (ChartAuthoringMetronome.TryGetScheduleDelay(
                    adjustedClickTime,
                    songTime,
                    out double delay))
                {

                    metronome.Schedule(
                        delay,
                        ChartAuthoringMetronome.IsDownbeat(chart.TempoSections, nextMetronomeSongTime),
                        metronomeVolume);

                }

                nextMetronomeSongTime = ChartAuthoringMetronome.GetBeatTimeAfter(
                    chart.TempoSections,
                    nextMetronomeSongTime);

            }

        }

        private void ResetSongMetronomeScheduler(double songTime)
        {

            nextMetronomeSongTime = ChartAuthoringMetronome.GetBeatTimeAtOrAfter(chart.TempoSections, songTime);

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

        private string GetRecordingModeInstructions()
        {

            return recordingNoteMode switch
            {
                RecordingNoteMode.Tap =>
                    "숫자키 1~8: 해당 레인에 탭 기록",
                RecordingNoteMode.SlideAndHold =>
                    "첫 키: 시작 · 다른 레인 키: 경로 추가 · 현재 레인 재입력: 종료 · " +
                    "0: 마지막 이동을 종단 플릭으로 종료 · Esc: 녹화 중지 및 미완성 노트 폐기",
                RecordingNoteMode.Flick =>
                    "숫자키 1~8: 시작 레인에 플릭 기록 · 선택한 방향의 인접 레인에 끝점 생성 · " +
                    "기록 후 상세 편집에서 끝점을 드래그해 변경",
                RecordingNoteMode.Banana =>
                    "첫 키: 시작 · 두 번째 키: 끝 · 녹화 후 곡선 핸들과 체크포인트 편집",
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

            ChartNoteAuthoringData data = recordedNote.noteData ?? ChartNoteAuthoringData.CreateTap(
                string.Empty, recordedNote.hitTime, recordedNote.laneIndex, recordedNote.musicalPartId);
            DrawWorkspaceNoteHeader(data, true);
            string before = JsonUtility.ToJson(data);
            Undo.RecordObject(this, "임시 노트 수정");
            EditorGUI.BeginChangeCheck();
            DrawWorkspaceNoteFields(data);
            bool changed = EditorGUI.EndChangeCheck();

            if (changed && before != JsonUtility.ToJson(data))
            {

                data.BananaCurveHandles.Sort((left, right) => left.NormalizedTime.CompareTo(right.NormalizedTime));
                data.BananaCheckpoints.Sort((left, right) => left.Time.CompareTo(right.Time));
                recordedNote.noteData = data;
                recordedNote.hitTime = data.HitTime;
                recordedNote.originalHitTime = data.HitTime;
                recordedNote.hasOriginalHitTime = true;
                recordedNote.laneIndex = data.LaneIndex;
                recordedNote.musicalPartId = data.MusicalPartId;
                recordedNote.pendingAutomaticQuantization = false;
                bufferWasApplied = false;
                SortRecordedNotes();

            }

            DrawWorkspaceNoteActions(true);

        }

        private void DrawSelectedAppliedNoteDetails()
        {

            ChartNote selectedNote = FindSelectedChartNote();

            if (selectedNote == null)
            {

                selectedAppliedNoteDataId = string.Empty;
                selectedAppliedNoteData = null;
                return;

            }

            if (selectedAppliedNoteData == null || selectedAppliedNoteDataId != selectedNote.Id)
            {

                LoadSelectedAppliedNoteData(selectedNote);

            }

            ChartNoteAuthoringData data = selectedAppliedNoteData;
            DrawWorkspaceNoteHeader(data, false);
            string before = JsonUtility.ToJson(data);
            EditorGUI.BeginChangeCheck();
            DrawWorkspaceNoteFields(data);
            bool changed = EditorGUI.EndChangeCheck();

            if (changed && before != JsonUtility.ToJson(data))
            {

                data.BananaCurveHandles.Sort((left, right) => left.NormalizedTime.CompareTo(right.NormalizedTime));
                data.BananaCheckpoints.Sort((left, right) => left.Time.CompareTo(right.Time));
                ApplySelectedAppliedNoteData();

            }

            DrawWorkspaceNoteActions(false);

        }

        private void DrawAppliedPartField(ChartNoteAuthoringData data)
        {

            data.MusicalPartId = DrawNotePartPopup(data.MusicalPartId, GetPartNames(), "음악 파트");

        }

        private void LoadSelectedAppliedNoteData(ChartNote note)
        {

            selectedAppliedNoteDataId = note.Id;
            selectedAppliedNoteData = ChartNoteAuthoringData.FromChartNote(note);

        }

        private void ApplySelectedAppliedNoteData()
        {

            if (chart == null || selectedAppliedNoteData == null)
            {

                return;

            }

            bool found = false;
            List<ChartNoteAuthoringData> editedNotes = new(chart.Notes.Count);

            for (int index = 0; index < chart.Notes.Count; index++)
            {

                if (chart.Notes[index].Id == selectedAppliedNoteDataId)
                {

                    editedNotes.Add(selectedAppliedNoteData.CloneWithOffset(0d));
                    found = true;

                }
                else
                {

                    editedNotes.Add(ChartNoteAuthoringData.FromChartNote(chart.Notes[index]));

                }

            }

            if (!found)
            {

                return;

            }

            Undo.RecordObject(chart, "적용된 상호작용 노트 수정");
            editedNotes.Sort((left, right) =>
            {

                int timeComparison = left.HitTime.CompareTo(right.HitTime);
                return timeComparison != 0
                    ? timeComparison
                    : string.CompareOrdinal(left.Id, right.Id);

            });
            SerializedObject serializedChart = new(chart);
            SerializedProperty notesProperty = serializedChart.FindProperty("notes");
            notesProperty.arraySize = editedNotes.Count;

            for (int index = 0; index < editedNotes.Count; index++)
            {

                editedNotes[index].WriteTo(notesProperty.GetArrayElementAtIndex(index));

            }
            serializedChart.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(chart);

            if (chart.TryValidate(out string error))
            {

                statusMessage =
                    $"적용된 노트 '{selectedAppliedNoteDataId}'를 수정했습니다. 에셋을 저장하세요.";

            }
            else
            {

                statusMessage = $"수정한 노트가 검사에 실패했습니다: {error}. 수정하거나 Undo로 되돌리세요.";

            }

        }

        private void HandleUndoRedo()
        {

            ChartNote selectedNote = FindSelectedChartNote();

            if (selectedNote != null)
            {

                LoadSelectedAppliedNoteData(selectedNote);

            }

            Repaint();

        }

        private void DrawHoldPathEditor(ChartNoteAuthoringData data)
        {

            GUILayout.Label(
                "점 드래그: 좌우로 시간 · 위아래로 위치 변경\nAlt: 스냅 해제",
                EditorStyles.wordWrappedMiniLabel);
            Rect rect = DrawInteractionEditorBackground();
            GetInteractionEditorRange(data, out double rangeStart, out double rangeEnd);
            Vector3 start = GetInteractionEditorPoint(
                rect,
                data.HitTime,
                data.LaneIndex,
                rangeStart,
                rangeEnd);
            Vector3 end = GetInteractionEditorPoint(
                rect,
                data.EndTime,
                data.LaneIndex,
                rangeStart,
                rangeEnd);
            DrawInteractionEditorPath(new[] { start, end });
            DrawInteractionEditorPoint(start, true);
            DrawInteractionEditorPoint(end, true);

            Event currentEvent = Event.current;

            if (currentEvent.type == EventType.MouseDown &&
                currentEvent.button == 0 &&
                rect.Contains(currentEvent.mousePosition))
            {

                if (Vector2.Distance(currentEvent.mousePosition, start) <= 9f)
                {

                    BeginInteractionEditorDrag(data, 0);
                    currentEvent.Use();

                }
                else if (Vector2.Distance(currentEvent.mousePosition, end) <= 9f)
                {

                    BeginInteractionEditorDrag(data, 1);
                    currentEvent.Use();

                }

            }

            if (currentEvent.type == EventType.MouseDrag && draggedSimplePointIndex >= 0)
            {

                double time = GetInteractionEditorTime(rect, currentEvent.mousePosition.x);
                int laneIndex = GetSlideEditorLane(rect, currentEvent.mousePosition.y);

                if (draggedSimplePointIndex == 0)
                {

                    data.HitTime = Math.Clamp(
                        SnapInteractionEditorTime(time, currentEvent.alt),
                        0d,
                        data.EndTime - DuplicateInputThresholdSeconds);

                }
                else
                {

                    data.EndTime = Math.Max(
                        data.HitTime + DuplicateInputThresholdSeconds,
                        SnapInteractionEditorTime(time, currentEvent.alt));

                }

                data.LaneIndex = laneIndex;
                data.EndLaneIndex = laneIndex;
                GUI.changed = true;
                currentEvent.Use();
                Repaint();

            }

            FinishInteractionEditorDrag(currentEvent);

        }

        private void DrawFlickPathEditor(ChartNoteAuthoringData data)
        {

            GUILayout.Label(
                "시작점 드래그: 시간·위치 변경\n끝점 위아래 드래그: 도착 위치 변경",
                EditorStyles.wordWrappedMiniLabel);
            Rect rect = DrawInteractionEditorBackground();
            GetInteractionEditorRange(data, out double rangeStart, out double rangeEnd);
            Vector3 start = GetInteractionEditorPoint(
                rect,
                data.HitTime,
                data.LaneIndex,
                rangeStart,
                rangeEnd);
            Vector3 end = GetInteractionEditorPoint(
                rect,
                data.HitTime,
                data.EndLaneIndex,
                rangeStart,
                rangeEnd);
            DrawInteractionEditorPath(new[] { start, end });
            DrawInteractionEditorPoint(start, true);
            DrawInteractionEditorPoint(end, true, new Color(1f, 0.45f, 0.16f, 1f));

            Event currentEvent = Event.current;

            if (currentEvent.type == EventType.MouseDown &&
                currentEvent.button == 0 &&
                rect.Contains(currentEvent.mousePosition))
            {

                if (Vector2.Distance(currentEvent.mousePosition, start) <= 9f)
                {

                    BeginInteractionEditorDrag(data, 0);
                    currentEvent.Use();

                }
                else if (Vector2.Distance(currentEvent.mousePosition, end) <= 9f)
                {

                    BeginInteractionEditorDrag(data, 1);
                    currentEvent.Use();

                }

            }

            if (currentEvent.type == EventType.MouseDrag && draggedSimplePointIndex >= 0)
            {

                int laneIndex = GetSlideEditorLane(rect, currentEvent.mousePosition.y);

                if (draggedSimplePointIndex == 0)
                {

                    int previousStartLane = data.LaneIndex;
                    data.HitTime = Math.Max(
                        0d,
                        SnapInteractionEditorTime(
                            GetInteractionEditorTime(rect, currentEvent.mousePosition.x),
                            currentEvent.alt));
                    data.EndTime = data.HitTime;

                    if (laneIndex != data.EndLaneIndex)
                    {

                        data.LaneIndex = laneIndex;

                    }
                    else
                    {

                        data.LaneIndex = previousStartLane;
                        statusMessage = "플릭의 시작과 끝 레인은 서로 달라야 합니다.";

                    }

                }
                else if (laneIndex != data.LaneIndex)
                {

                    data.EndLaneIndex = laneIndex;

                }
                else
                {

                    statusMessage = "플릭의 시작과 끝 레인은 서로 달라야 합니다.";

                }

                GUI.changed = true;
                currentEvent.Use();
                Repaint();

            }

            FinishInteractionEditorDrag(currentEvent);

        }

        private Rect DrawInteractionEditorBackground()
        {

            Rect rect = GUILayoutUtility.GetRect(120f, 170f, GUILayout.ExpandWidth(true));
            EditorGUI.DrawRect(rect, WorkspaceCanvas);

            for (int laneIndex = 0; laneIndex < chart.LaneCount; laneIndex++)
            {

                float y = GetSlideEditorY(rect, laneIndex);
                EditorGUI.DrawRect(
                    new Rect(rect.x, y, rect.width, 1f),
                    new Color(1f, 1f, 1f, 0.08f));

            }

            return rect;

        }

        private void GetInteractionEditorRange(
            ChartNoteAuthoringData data,
            out double rangeStart,
            out double rangeEnd)
        {

            if (interactionEditorRangeLocked)
            {

                rangeStart = interactionEditorRangeStart;
                rangeEnd = interactionEditorRangeEnd;
                return;

            }

            double duration = Math.Max(0d, data.EndTime - data.HitTime);
            double padding = Math.Max(0.25d, duration * 0.25d);
            rangeStart = Math.Max(0d, data.HitTime - padding);
            rangeEnd = Math.Max(rangeStart + 0.5d, data.EndTime + padding);

        }

        private void BeginInteractionEditorDrag(ChartNoteAuthoringData data, int pointIndex)
        {

            GetInteractionEditorRange(data, out interactionEditorRangeStart, out interactionEditorRangeEnd);
            interactionEditorRangeLocked = true;
            draggedSimplePointIndex = pointIndex;

        }

        private void BeginSlideEditorDrag(
            ChartNoteAuthoringData data,
            int pointIndex,
            Vector2 pointerOffset)
        {

            GetInteractionEditorRange(data, out interactionEditorRangeStart, out interactionEditorRangeEnd);
            interactionEditorRangeLocked = true;
            draggedSlidePointIndex = pointIndex;
            draggedSlidePointOffset = pointerOffset;

        }

        private void FinishInteractionEditorDrag(Event currentEvent)
        {

            if (currentEvent.rawType != EventType.MouseUp)
            {

                return;

            }

            draggedSimplePointIndex = int.MinValue;
            draggedSlidePointIndex = int.MinValue;
            interactionEditorRangeLocked = false;

        }

        private Vector3 GetInteractionEditorPoint(
            Rect rect,
            double time,
            int laneIndex,
            double rangeStart,
            double rangeEnd)
        {

            float normalizedTime = (float)((time - rangeStart) / Math.Max(
                ScheduleToleranceSeconds,
                rangeEnd - rangeStart));
            return new Vector3(
                Mathf.Lerp(rect.x, rect.xMax, Mathf.Clamp01(normalizedTime)),
                GetSlideEditorY(rect, laneIndex),
                0f);

        }

        private double GetInteractionEditorTime(Rect rect, float x)
        {

            float normalizedTime = Mathf.InverseLerp(rect.x, rect.xMax, x);
            return Mathf.Lerp(
                (float)interactionEditorRangeStart,
                (float)interactionEditorRangeEnd,
                normalizedTime);

        }

        private double SnapInteractionEditorTime(double time, bool bypassSnap)
        {

            double clampedTime = Math.Max(0d, time);

            if (bypassSnap || chart.TempoSections.Count == 0)
            {

                return clampedTime;

            }

            return ChartTempoMap.SnapSongTime(
                chart.TempoSections,
                clampedTime,
                (int)quantizationGrid);

        }

        private static void DrawInteractionEditorPath(Vector3[] points)
        {

            Handles.BeginGUI();
            Handles.color = new Color(0.95f, 0.82f, 0.25f, 0.92f);
            Handles.DrawAAPolyLine(4f, points);
            Handles.EndGUI();

        }

        private static void DrawInteractionEditorPoint(
            Vector3 point,
            bool endpoint,
            Color? overrideColor = null)
        {

            EditorGUI.DrawRect(
                new Rect(point.x - 6f, point.y - 6f, 12f, 12f),
                overrideColor ?? (endpoint
                    ? new Color(1f, 0.95f, 0.72f, 1f)
                    : new Color(0.95f, 0.62f, 0.18f, 1f)));

        }

        private void DrawSlidePathEditor(ChartNoteAuthoringData data)
        {

            GUILayout.Label(
                "계단형 경로 · 점/전환선 드래그\n유지선 클릭: 노드 추가 · Alt: 스냅 해제",
                EditorStyles.wordWrappedMiniLabel);
            Rect rect = DrawInteractionEditorBackground();
            GetInteractionEditorRange(data, out double rangeStart, out double rangeEnd);

            Vector3[] points = new Vector3[data.SlideNodes.Count + 1];
            points[0] = GetInteractionEditorPoint(
                rect,
                data.HitTime,
                data.LaneIndex,
                rangeStart,
                rangeEnd);

            for (int index = 0; index < data.SlideNodes.Count; index++)
            {

                points[index + 1] = GetInteractionEditorPoint(
                    rect,
                    data.SlideNodes[index].Time,
                    data.SlideNodes[index].LaneIndex,
                    rangeStart,
                    rangeEnd);

            }

            DrawInteractionEditorPath(BuildSteppedSlidePath(points, false));
            if (workspaceShowChecks)
            {

                DrawSlideTimingPreview(rect, data, rangeStart, rangeEnd);

            }

            for (int index = 0; index < points.Length; index++)
            {

                DrawInteractionEditorPoint(
                    points[index],
                    index == 0 || index == points.Length - 1,
                    index == points.Length - 1 && data.SlideEndBehavior == SlideEndBehavior.Flick
                        ? new Color(1f, 0.45f, 0.16f, 1f)
                        : null);

            }

            HandleSlidePathEditorInput(rect, data, points, rangeStart, rangeEnd);
            GUILayout.Label(
                "가로: 위치 유지 · 세로: 지정 시각에 전환",
                EditorStyles.wordWrappedMiniLabel);

            if (workspaceShowChecks)
            {

                GUILayout.Label(
                    "청록: 1/4박 판정점 · 주황: 1/2박 보상점\n큰 점: 직접 입력한 노드",
                    EditorStyles.wordWrappedMiniLabel);

            }

        }

        private void HandleSlidePathEditorInput(
            Rect rect,
            ChartNoteAuthoringData data,
            Vector3[] points,
            double rangeStart,
            double rangeEnd)
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

                        BeginSlideEditorDrag(
                            data,
                            index - 1,
                            (Vector2)points[index] - currentEvent.mousePosition);
                        currentEvent.Use();
                        return;

                    }

                }

                // A transition connector has one authored time. Drag its destination node
                // instead of creating a second node at that same time.
                for (int index = 1; index < points.Length; index++)
                {

                    Vector3 previous = points[index - 1];
                    Vector3 destination = points[index];

                    if (Mathf.Abs(currentEvent.mousePosition.x - destination.x) <= 6f &&
                        currentEvent.mousePosition.y >= Mathf.Min(previous.y, destination.y) &&
                        currentEvent.mousePosition.y <= Mathf.Max(previous.y, destination.y))
                    {

                        BeginSlideEditorDrag(
                            data,
                            index - 1,
                            (Vector2)destination - currentEvent.mousePosition);
                        currentEvent.Use();
                        return;

                    }

                }

                float normalizedTime = Mathf.InverseLerp(rect.x, rect.xMax, currentEvent.mousePosition.x);
                double nodeTime = SnapInteractionEditorTime(
                    Mathf.Lerp((float)rangeStart, (float)rangeEnd, normalizedTime),
                    currentEvent.alt);

                if (TryGetSlideBodyInsertion(data, nodeTime, out int insertionIndex, out int heldLane))
                {

                    float expectedY = GetSlideEditorY(rect, heldLane);

                    if (Mathf.Abs(currentEvent.mousePosition.y - expectedY) > 10f)
                    {

                        return;

                    }

                    Undo.RecordObject(this, "슬라이드 노드 추가");
                    data.SlideNodes.Insert(insertionIndex, new ChartNoteAuthoringData.PathNodeData
                    {

                        Time = nodeTime,
                        LaneIndex = heldLane

                    });
                    GUI.changed = true;
                    currentEvent.Use();
                    return;

                }

            }

            if (currentEvent.type == EventType.MouseDrag &&
                draggedSlidePointIndex != int.MinValue)
            {

                Vector2 draggedPosition = currentEvent.mousePosition + draggedSlidePointOffset;
                int laneIndex = GetSlideEditorLane(rect, draggedPosition.y);
                Undo.RecordObject(this, "슬라이드 레인 드래그");
                double time = SnapInteractionEditorTime(
                    GetInteractionEditorTime(rect, draggedPosition.x),
                    currentEvent.alt);

                if (draggedSlidePointIndex < 0)
                {

                    double maximumTime = data.SlideNodes[0].Time - DuplicateInputThresholdSeconds;
                    data.HitTime = Math.Clamp(time, 0d, maximumTime);
                    data.LaneIndex = laneIndex;

                }
                else if (draggedSlidePointIndex < data.SlideNodes.Count)
                {

                    ChartNoteAuthoringData.PathNodeData node =
                        data.SlideNodes[draggedSlidePointIndex];
                    double minimumTime = draggedSlidePointIndex == 0
                        ? data.HitTime + DuplicateInputThresholdSeconds
                        : data.SlideNodes[draggedSlidePointIndex - 1].Time +
                          DuplicateInputThresholdSeconds;
                    double maximumTime = draggedSlidePointIndex == data.SlideNodes.Count - 1
                        ? Math.Max(minimumTime, interactionEditorRangeEnd)
                        : data.SlideNodes[draggedSlidePointIndex + 1].Time -
                          DuplicateInputThresholdSeconds;

                    if (maximumTime >= minimumTime)
                    {

                        node.Time = Math.Clamp(time, minimumTime, maximumTime);

                    }
                    node.LaneIndex = laneIndex;

                }

                data.EndTime = data.SlideNodes[^1].Time;
                data.EndLaneIndex = data.SlideNodes[^1].LaneIndex;
                GUI.changed = true;
                currentEvent.Use();
                Repaint();

            }

            FinishInteractionEditorDrag(currentEvent);

        }

        private void DrawSlideTimingPreview(
            Rect rect,
            ChartNoteAuthoringData data,
            double rangeStart,
            double rangeEnd)
        {

            if (chart.TempoSections.Count == 0 || data.SlideNodes.Count == 0)
            {

                return;

            }

            DrawSlideSubdivisionMarkers(
                rect,
                data,
                rangeStart,
                rangeEnd,
                4,
                new Color(0.2f, 0.9f, 0.92f, 0.95f),
                4f);
            DrawSlideSubdivisionMarkers(
                rect,
                data,
                rangeStart,
                rangeEnd,
                2,
                new Color(1f, 0.55f, 0.18f, 0.95f),
                7f);

        }

        private void DrawSlideSubdivisionMarkers(
            Rect rect,
            ChartNoteAuthoringData data,
            double rangeStart,
            double rangeEnd,
            int subdivisionsPerBeat,
            Color color,
            float size)
        {

            double time = ChartTempoMap.GetSubdivisionTimeAfter(
                chart.TempoSections,
                data.HitTime,
                subdivisionsPerBeat);
            int guard = 0;

            while (time < data.EndTime - ScheduleToleranceSeconds && guard++ < 2048)
            {

                float lane = EvaluateSlideLane(data, time);
                float normalizedTime = (float)((time - rangeStart) / Math.Max(
                    ScheduleToleranceSeconds,
                    rangeEnd - rangeStart));
                float x = Mathf.Lerp(rect.x, rect.xMax, Mathf.Clamp01(normalizedTime));
                float normalizedLane = (lane + 0.5f) / chart.LaneCount;
                float y = Mathf.Lerp(rect.yMax, rect.y, normalizedLane);
                EditorGUI.DrawRect(
                    new Rect(x - size * 0.5f, y - size * 0.5f, size, size),
                    color);
                time = ChartTempoMap.GetSubdivisionTimeAfter(
                    chart.TempoSections,
                    time,
                    subdivisionsPerBeat);

            }

        }

        internal static Vector3[] BuildSteppedSlidePath(
            IReadOnlyList<Vector3> authoredPoints,
            bool verticalTimeline)
        {

            if (authoredPoints.Count == 0)
            {

                return Array.Empty<Vector3>();

            }

            Vector3[] path = new Vector3[authoredPoints.Count * 2 - 1];
            path[0] = authoredPoints[0];

            for (int index = 1; index < authoredPoints.Count; index++)
            {

                Vector3 previous = authoredPoints[index - 1];
                Vector3 destination = authoredPoints[index];
                // Corners are display geometry only; chart nodes keep strictly increasing times.
                path[index * 2 - 1] = verticalTimeline
                    ? new Vector3(previous.x, destination.y, 0f)
                    : new Vector3(destination.x, previous.y, 0f);
                path[index * 2] = destination;

            }

            return path;

        }

        internal static bool TryGetSlideBodyInsertion(
            ChartNoteAuthoringData data,
            double nodeTime,
            out int insertionIndex,
            out int heldLane)
        {

            double previousTime = data.HitTime;
            heldLane = data.LaneIndex;

            for (int index = 0; index < data.SlideNodes.Count; index++)
            {

                ChartNoteAuthoringData.PathNodeData node = data.SlideNodes[index];

                if (nodeTime >= previousTime + DuplicateInputThresholdSeconds &&
                    nodeTime <= node.Time - DuplicateInputThresholdSeconds)
                {

                    insertionIndex = index;
                    return true;

                }

                previousTime = node.Time;
                heldLane = node.LaneIndex;

            }

            insertionIndex = -1;
            return false;

        }

        internal static int EvaluateSlideLane(ChartNoteAuthoringData data, double time)
        {

            int heldLane = data.LaneIndex;

            for (int index = 0; index < data.SlideNodes.Count; index++)
            {

                ChartNoteAuthoringData.PathNodeData node = data.SlideNodes[index];

                if (time < node.Time)
                {

                    return heldLane;

                }

                heldLane = node.LaneIndex;

            }

            return heldLane;

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
            bool changed = false;

            for (int index = 0; index < data.SlideNodes.Count; index++)
            {

                ChartNoteAuthoringData.PathNodeData node = data.SlideNodes[index];

                using (new EditorGUILayout.HorizontalScope())
                {

                    GUILayout.Label((index + 1).ToString("00"), GUILayout.Width(24f));
                    double minimumTime = index == 0
                        ? data.HitTime + DuplicateInputThresholdSeconds
                        : data.SlideNodes[index - 1].Time + DuplicateInputThresholdSeconds;
                    double nextTime = EditorGUILayout.DoubleField(node.Time, GUILayout.Width(92f));
                    int nextLane = EditorGUILayout.IntField(node.LaneIndex + 1, GUILayout.Width(42f)) - 1;

                    if (nextTime != node.Time)
                    {

                        node.Time = Math.Max(minimumTime, nextTime);
                        changed = true;

                    }

                    if (nextLane != node.LaneIndex)
                    {

                        node.LaneIndex = Mathf.Clamp(nextLane, 0, chart.LaneCount - 1);
                        changed = true;

                    }

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
                changed = true;

            }

            if (GUILayout.Button("중간 노드 추가"))
            {

                ChartNoteAuthoringData.PathNodeData final = data.SlideNodes[^1];
                double previousTime = data.SlideNodes.Count > 1
                    ? data.SlideNodes[^2].Time
                    : data.HitTime;

                if (final.Time - previousTime <= DuplicateInputThresholdSeconds * 2d)
                {

                    statusMessage = "마지막 구간이 너무 짧아 중간 노드를 추가할 수 없습니다.";

                }
                else
                {

                double middleTime = SnapInteractionEditorTime(
                    (previousTime + final.Time) * 0.5d,
                    false);
                middleTime = Math.Clamp(
                    middleTime,
                    previousTime + DuplicateInputThresholdSeconds,
                    final.Time - DuplicateInputThresholdSeconds);
                data.SlideNodes.Insert(
                    data.SlideNodes.Count - 1,
                    new ChartNoteAuthoringData.PathNodeData
                    {

                        Time = middleTime,
                        LaneIndex = data.SlideNodes.Count > 1
                            ? data.SlideNodes[^2].LaneIndex
                            : data.LaneIndex

                    });
                changed = true;

                }

            }

            if (changed && data.SlideNodes.Count > 0)
            {

                data.EndTime = data.SlideNodes[^1].Time;
                data.EndLaneIndex = data.SlideNodes[^1].LaneIndex;

            }

        }

        private static void DrawBananaHandleFields(ChartNoteAuthoringData data)
        {

            for (int index = 0; index < data.BananaCurveHandles.Count; index++)
            {

                ChartNoteAuthoringData.CurveHandleData handle = data.BananaCurveHandles[index];
                using (new EditorGUILayout.HorizontalScope())
                {

                    GUILayout.Label($"핸들 {index + 1}", EditorStyles.miniBoldLabel);
                    using (new EditorGUI.DisabledScope(data.BananaCurveHandles.Count <= 1))
                    {

                        if (GUILayout.Button("삭제", GUILayout.Width(44f)))
                        {

                            data.BananaCurveHandles.RemoveAt(index);
                            break;

                        }

                    }

                }

                float nextTime = EditorGUILayout.Slider("시간 비율", handle.NormalizedTime, 0f, 1f);

                if (nextTime != handle.NormalizedTime)
                {

                    handle.NormalizedTime = Mathf.Clamp(nextTime, 0.01f, 0.99f);

                }

                handle.NormalizedX = EditorGUILayout.Slider("가로 위치", handle.NormalizedX, 0f, 1f);
                GUILayout.Space(5f);

            }

            using (new EditorGUI.DisabledScope(data.BananaCurveHandles.Count >= 2))
            {

                if (GUILayout.Button("곡선 핸들 추가"))
                {

                    data.BananaCurveHandles.Add(new ChartNoteAuthoringData.CurveHandleData
                    {

                        NormalizedTime = 0.66f,
                        NormalizedX = 0.5f

                    });
                    data.BananaCurveHandles.Sort((left, right) => left.NormalizedTime.CompareTo(right.NormalizedTime));

                }

            }

        }

        private void DrawBananaCheckpointFields(ChartNoteAuthoringData data)
        {

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

            for (int index = 0; index < data.BananaCheckpoints.Count; index++)
            {

                ChartNoteAuthoringData.CheckpointData checkpoint = data.BananaCheckpoints[index];
                using (new EditorGUILayout.HorizontalScope())
                {

                    GUILayout.Label($"체크포인트 {index + 1}", EditorStyles.miniBoldLabel);
                    using (new EditorGUI.DisabledScope(data.BananaCheckpoints.Count <= 1))
                    {

                        if (GUILayout.Button("삭제", GUILayout.Width(44f)))
                        {

                            data.BananaCheckpoints.RemoveAt(index);
                            break;

                        }

                    }

                }

                double nextTime = EditorGUILayout.DoubleField("시간(초)", checkpoint.Time);

                if (nextTime != checkpoint.Time)
                {

                    checkpoint.Time = Math.Clamp(nextTime,
                        data.HitTime + ScheduleToleranceSeconds, data.EndTime - ScheduleToleranceSeconds);

                }

                checkpoint.NormalizedX = EditorGUILayout.Slider("가로 위치", checkpoint.NormalizedX, 0f, 1f);
                GUILayout.Space(5f);

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

            bool wasEmpty = pendingInteraction == null;
            ChartInteractionRecordingUtility.RecordSlideOrHoldLane(
                pendingInteraction,
                laneIndex,
                hitTime,
                chart.MusicalParts[selectedPartIndex].Id,
                out pendingInteraction,
                out ChartNoteAuthoringData completed);

            if (completed != null)
            {

                AddRecordedInteraction(completed);
                return;

            }

            if (wasEmpty && pendingInteraction != null)
            {

                statusMessage =
                    $"{hitTime:0.000}초 {laneIndex + 1}번에서 홀드/슬라이드를 시작했습니다.";

            }
            else if (pendingInteraction != null && pendingInteraction.NoteType == ChartNoteType.Slide)
            {

                statusMessage =
                    $"{hitTime:0.000}초에 {laneIndex + 1}번 슬라이드 노드를 추가했습니다.";

            }

        }

        private void RecordFlickLane(int laneIndex, double hitTime)
        {

            if (!ChartInteractionRecordingUtility.TryCreateFlick(
                    laneIndex,
                    (int)flickDefaultDirection,
                    chart.LaneCount,
                    hitTime,
                    chart.MusicalParts[selectedPartIndex].Id,
                    out ChartNoteAuthoringData flick,
                    out string error))
            {

                statusMessage = error;
                return;

            }
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

                banana.BananaCheckpoints.Add(new ChartNoteAuthoringData.CheckpointData
                {

                    Time = checkpointTime,
                    NormalizedX = GetWorkspaceBananaNormalizedX(banana, checkpointTime, chart.LaneCount)

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
                    NormalizedX = GetWorkspaceBananaNormalizedX(banana, middleTime, chart.LaneCount)

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

            if (!ChartInteractionRecordingUtility.TryCompleteTerminalFlick(
                    pendingInteraction,
                    out ChartNoteAuthoringData completed,
                    out string error))
            {

                statusMessage = error;
                return;

            }

            AddRecordedInteraction(completed);
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

                    ToggleWorkspacePlayback();

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

            if (chart == null || recordedNotes.Count == 0)
            {

                statusMessage = "반영할 임시 기록과 차트를 먼저 선택하세요.";
                return;

            }

            if (!TryValidateRecordingBuffer(out string bufferError))
            {

                statusMessage = $"차트에 반영하지 않았습니다: {bufferError}";
                return;

            }

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
                               note != null &&
                               recordedPartIds.Contains(note.MusicalPartId) &&
                               note.HitTime >= loopStart &&
                               note.HitTime < loopEnd;

                if (replace)
                {

                    continue;

                }

                if (note == null || !Enum.IsDefined(typeof(ChartNoteType), note.NoteType) ||
                    !Enum.IsDefined(typeof(SlideEndBehavior), note.SlideEndBehavior) ||
                    HasMissingElement(note.SlideNodes) || HasMissingElement(note.BananaCurveHandles) ||
                    HasMissingElement(note.BananaCheckpoints))
                {

                    statusMessage = $"차트에 반영하지 않았습니다: 기존 노트 {index + 1}의 정보가 손상되었습니다.";
                    return;

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

            // Validate the complete result, including replacement and activation windows,
            // before touching the asset, recording buffer, or user Undo history.
            PrototypeChart candidate = Instantiate(chart);

            try
            {

                candidate.hideFlags = HideFlags.HideAndDontSave;
                SerializedObject serializedChart = new(candidate);
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

                    ChartActivationWindowUtility.Normalize(candidate, recordUndo: false);

                }

                if (!candidate.TryValidate(out string error))
                {

                    statusMessage = $"차트에 반영하지 않았습니다. 임시 기록을 유지합니다: {error}";
                    return;

                }

                Undo.IncrementCurrentGroup();
                int undoGroup = Undo.GetCurrentGroup();
                Undo.SetCurrentGroupName("녹화한 차트 노트 적용");
                Undo.RecordObject(chart, "녹화한 차트 노트 적용");
                Undo.RecordObject(this, "녹화한 차트 노트 적용");
                serializedChart.Update();
                SerializedObject destination = new(chart);
                destination.CopyFromSerializedProperty(serializedChart.FindProperty("notes"));
                destination.CopyFromSerializedProperty(serializedChart.FindProperty("activationWindows"));
                destination.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(chart);
                statusMessage = $"노트 {appliedNoteCount}개를 적용했고 차트 검사도 통과했습니다. 에셋을 저장하세요.";
                bufferWasApplied = true;

                if (clearBufferAfterApply)
                {

                    recordedNotes.Clear();
                    selectedRecordedNoteIndex = -1;
                    bufferWasApplied = false;

                }

                Undo.CollapseUndoOperations(undoGroup);

            }
            finally
            {

                DestroyImmediate(candidate);

            }

        }

        private bool TryValidateRecordingBuffer(out string error)
        {

            for (int index = 0; index < chart.ActivationWindows.Count; index++)
            {

                MusicalPartActivationWindow window = chart.ActivationWindows[index];

                // Normalization may merge ranges, but applying notes must not silently delete
                // malformed pre-existing windows as a side effect of adding missing coverage.
                if (window == null || string.IsNullOrWhiteSpace(window.MusicalPartId) ||
                    double.IsNaN(window.StartTime) || double.IsInfinity(window.StartTime) ||
                    double.IsNaN(window.EndTime) || double.IsInfinity(window.EndTime) ||
                    window.StartTime < 0d || window.EndTime <= window.StartTime)
                {

                    error = $"기존 활성 구간 {index + 1}의 파트 또는 시간을 먼저 수정하세요.";
                    return false;

                }

            }

            for (int index = 0; index < recordedNotes.Count; index++)
            {

                RecordedNote note = recordedNotes[index];
                string prefix = $"임시 기록 {index + 1:000}";

                if (note == null || double.IsNaN(note.hitTime) || double.IsInfinity(note.hitTime) ||
                    note.hitTime < 0d || note.laneIndex < 0 || note.laneIndex >= chart.LaneCount)
                {

                    error = $"{prefix}의 시간 또는 입력 위치가 올바르지 않습니다.";
                    return false;

                }

                if (FindNotePartIndex(note.musicalPartId) < 0)
                {

                    error = $"{prefix}의 음악 파트를 지정하세요.";
                    return false;

                }

                ChartNoteAuthoringData data = note.noteData;

                // Older tap-only buffers may not have full interaction data.
                if (data == null)
                {

                    continue;

                }

                if (data.MusicalPartId != note.musicalPartId || data.LaneIndex != note.laneIndex ||
                    !Enum.IsDefined(typeof(ChartNoteType), data.NoteType) ||
                    !Enum.IsDefined(typeof(SlideEndBehavior), data.SlideEndBehavior) ||
                    double.IsNaN(data.HitTime) || double.IsInfinity(data.HitTime) ||
                    data.SlideNodes == null || data.BananaCurveHandles == null || data.BananaCheckpoints == null ||
                    data.SlideNodes.Exists(node => node == null) ||
                    data.BananaCurveHandles.Exists(handle => handle == null) ||
                    data.BananaCheckpoints.Exists(checkpoint => checkpoint == null))
                {

                    error = $"{prefix}의 노트 정보가 일치하지 않거나 손상되었습니다. 확인 후 다시 입력하세요.";
                    return false;

                }

            }

            error = string.Empty;
            return true;

        }

        private static bool HasMissingElement<T>(IReadOnlyList<T> items) where T : class
        {

            if (items != null)
            {

                for (int index = 0; index < items.Count; index++)
                {

                    if (items[index] == null)
                    {

                        return true;

                    }

                }

            }

            return false;

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
                double nextVisibleDuration = Math.Max(1d, Math.Min(120d, timelineVisibleDuration * zoomFactor));
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
            selectedAppliedNoteDataId = string.Empty;
            selectedAppliedNoteData = null;
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
            selectedChartNoteId = string.Empty;
            selectedAppliedNoteDataId = string.Empty;
            selectedAppliedNoteData = null;
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
                "차트 노트 박자 보정",
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

            if (GUILayout.Button("보정 적용"))
            {

                string rangeDescription = appliedQuantizationRange == AppliedQuantizationRange.WholeChart
                    ? "차트 전체"
                    : $"{loopStart:0.000}초 이상 {loopEnd:0.000}초 미만";

                if (EditorUtility.DisplayDialog(
                        "차트 노트 박자 보정",
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

        private static string GetNoteTypeName(ChartNoteType noteType)
        {

            return noteType switch
            {
                ChartNoteType.Tap => "탭",
                ChartNoteType.Hold => "홀드",
                ChartNoteType.Slide => "슬라이드",
                ChartNoteType.Flick => "플릭",
                ChartNoteType.Banana => "바나나",
                _ => noteType.ToString()
            };

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
