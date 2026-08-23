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
        private const float VerticalTimelineRulerWidth = 64f;
        private const float VerticalTimelineHeaderHeight = 34f;
        private const float TimelineNoteHitRadius = 8f;
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
            public double originalHitTime;
            public int laneIndex;
            public string musicalPartId;
            public bool hasOriginalHitTime;
            public bool pendingAutomaticQuantization;

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
        [SerializeField] private bool addMissingActivationWindows;
        [SerializeField, Min(1)] private int countInBars = 2;
        [SerializeField, Range(0f, 1f)] private float metronomeVolume = 0.15f;
        [SerializeField] private bool metronomeDuringRecording;
        [SerializeField, Min(4f)] private float timelineVisibleDuration = 16f;
        [SerializeField, Min(0f)] private double timelineStartTime;
        [SerializeField, Min(1)] private int loopStartBar = 1;
        [SerializeField, Min(2)] private int loopEndBar = 5;
        [SerializeField, Min(1)] private int loopBarCount = 4;
        [SerializeField] private TimelineViewMode timelineViewMode;
        [SerializeField] private bool verticalTimelineAutoScroll = true;
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

        private readonly double[] lastRecordedInputTimestamps = new double[SupportedKeyboardLaneCount];
        private InputAction[] recordingInputActions;
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
            tempoCalibrationPreview = false;
            DestroyMetronome();
            DisposeRecordingInputActions();

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

                EditorGUI.BeginChangeCheck();
                int nextBar = Math.Max(1, EditorGUILayout.IntField("마디", bar));
                int nextBeat = Mathf.Clamp(
                    EditorGUILayout.IntField("박", beat),
                    1,
                    Math.Max(1, beatsPerBar));
                double nextSongTime = Math.Max(0d, EditorGUILayout.DoubleField("시간", songTime));

                if (EditorGUI.EndChangeCheck())
                {

                    bar = nextBar;
                    beat = nextBeat;
                    songTime = nextSongTime;
                    hasAnchor = true;

                }

                GUI.enabled = Application.isPlaying && songPlayback != null && songPlayback.IsPrepared;

                if (GUILayout.Button("현재 위치 지정", GUILayout.Width(96f)))
                {

                    songTime = songPlayback.SongTime;
                    hasAnchor = true;
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

                GUILayout.Space(EditorGUIUtility.labelWidth);

                if (GUILayout.Button("-10ms"))
                {

                    songTime = Math.Max(0d, songTime - 0.010d);
                    hasAnchor = true;

                }

                if (GUILayout.Button("-1ms"))
                {

                    songTime = Math.Max(0d, songTime - 0.001d);
                    hasAnchor = true;

                }

                if (GUILayout.Button("+1ms"))
                {

                    songTime += 0.001d;
                    hasAnchor = true;

                }

                if (GUILayout.Button("+10ms"))
                {

                    songTime += 0.010d;
                    hasAnchor = true;

                }

                GUILayout.Label(hasAnchor ? "설정됨" : "미설정", GUILayout.Width(52f));

            }

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
            countInBars = Mathf.Max(1, EditorGUILayout.IntField("카운트인 마디", countInBars));
            metronomeVolume = EditorGUILayout.Slider("메트로놈 음량", metronomeVolume, 0f, 1f);
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
                        note.hitTime = nextHitTime;
                        note.originalHitTime = nextHitTime;
                        note.hasOriginalHitTime = true;
                        note.pendingAutomaticQuantization = false;
                        note.laneIndex = nextLaneIndex;
                        note.musicalPartId = nextPartId;
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

            if (tempoCalibrationPreview)
            {

                ScheduleTempoCalibrationPreview();

            }

            if (timelineViewMode == TimelineViewMode.Vertical &&
                verticalTimelineAutoScroll &&
                songPlayback.IsPlaying)
            {

                UpdateVerticalTimelineAutoScroll();

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

            if (automaticQuantization && chart.TempoSections.Count > 0)
            {

                QuantizeRecordedNotes(false, true);

            }
            else
            {

                statusMessage = $"녹화를 중지했습니다. 임시 기록에 노트 {recordedNotes.Count}개가 있습니다.";

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

            timelineViewMode = (TimelineViewMode)GUILayout.Toolbar(
                (int)timelineViewMode,
                new[] { "가로 타임라인", "세로 채보 시트" });

            if (timelineViewMode == TimelineViewMode.Vertical)
            {

                using (new EditorGUILayout.HorizontalScope())
                {

                    verticalTimelineAutoScroll = EditorGUILayout.Toggle(
                        "재생 중 자동 내려가기",
                        verticalTimelineAutoScroll);
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

                GUI.enabled = true;

            }

            EditorGUILayout.HelpBox(
                "진한 선은 마디, 옅은 선은 박입니다. 빈 곳을 클릭하면 이동하고 임시 노트를 클릭하면 선택합니다. " +
                "마우스 휠로 이동하고 Ctrl+휠로 확대할 수 있습니다. 기존 노트는 채워진 표시, " +
                "임시 녹화 노트는 노란 테두리로 표시됩니다.",
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
                EditorGUI.DrawRect(new Rect(x - 3f, y - 3f, 7f, 7f), chart.GetPartColor(note.MusicalPartId));

            }

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

            double normalized = Mathf.InverseLerp(contentRect.y, contentRect.yMax, currentEvent.mousePosition.y);
            double selectedTime = timelineStartTime + normalized * timelineVisibleDuration;
            seekTime = ChartTempoMap.SnapSongTime(chart.TempoSections, selectedTime, (int)quantizationGrid);
            verticalTimelineAutoScroll = false;

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
            scrollPosition.y = Math.Max(
                0f,
                closestIndex * (EditorGUIUtility.singleLineHeight + 2f) - 60f);
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

            double normalized = Mathf.InverseLerp(contentRect.x, contentRect.xMax, currentEvent.mousePosition.x);
            double selectedTime = timelineStartTime + normalized * timelineVisibleDuration;
            seekTime = ChartTempoMap.SnapSongTime(
                chart.TempoSections,
                selectedTime,
                (int)quantizationGrid);

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
                double delay = Math.Max(0.025d, nextPreviewMetronomeSongTime - songTime);
                metronome.Schedule(delay, accent, metronomeVolume);
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
                originalHitTime = Math.Max(0d, hitTime),
                laneIndex = laneIndex,
                musicalPartId = chart.MusicalParts[selectedPartIndex].Id,
                hasOriginalHitTime = true,
                pendingAutomaticQuantization = true

            };
            Undo.RecordObject(this, "채보 입력 기록");
            recordedNotes.Add(recordedNote);
            bufferWasApplied = false;
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

            }

            if (!isRecording)
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
            scrollPosition.y = Math.Max(
                0f,
                closestIndex * (EditorGUIUtility.singleLineHeight + 2f) - 60f);
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

            if (vertical)
            {

                verticalTimelineAutoScroll = false;

            }

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
            selectedRecordedNoteIndex = -1;
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
                "단축키: Space 재생/일시정지 · R 현재 위치 녹화 · Esc 녹화 중지 · Delete 선택 노트 삭제",
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
            configuredEventPath = string.Empty;
            recordedNotes.Clear();
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

        private void UpdateVerticalTimelineAutoScroll()
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

                    candidates[index].hitTime = result.CorrectedTime;
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

                note.hitTime = note.originalHitTime;
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
            note.hitTime = nextTime;
            note.originalHitTime = nextTime;
            note.hasOriginalHitTime = true;
            note.pendingAutomaticQuantization = false;
            bufferWasApplied = false;
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
