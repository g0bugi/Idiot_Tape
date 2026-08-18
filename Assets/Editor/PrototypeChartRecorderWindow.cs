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

        private readonly double[] lastRecordedInputTimestamps = new double[SupportedKeyboardLaneCount];
        private InputAction[] recordingInputActions;
        private Vector2 scrollPosition;
        private FmodSongPlayback songPlayback;
        private bool isRecording;
        private bool isLoopRecording;
        private string statusMessage = "차트를 선택하세요.";

        [MenuItem("Tools/Idiot Tape/차트 녹화 도구")]
        public static void Open()
        {

            PrototypeChartRecorderWindow window = GetWindow<PrototypeChartRecorderWindow>();
            window.titleContent = new GUIContent("차트 녹화");
            window.minSize = new Vector2(560f, 640f);
            window.Show();

        }

        private void OnEnable()
        {

            CreateRecordingInputActions();
            EditorApplication.update += EditorUpdate;

        }

        private void OnDisable()
        {

            EditorApplication.update -= EditorUpdate;
            isRecording = false;
            isLoopRecording = false;
            DisposeRecordingInputActions();

        }

        private void OnGUI()
        {

            HandleRecorderWindowKeyboardEvent(Event.current);
            EditorGUILayout.LabelField("Idiot_Tape 차트 녹화 도구", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "게임플레이와 동일한 FMOD DSP 시간으로 숫자키 입력을 기록합니다. " +
                "'기록 적용'을 누르기 전까지 차트 원본은 바뀌지 않습니다.",
                MessageType.Info);

            chart = (PrototypeChart)EditorGUILayout.ObjectField("차트", chart, typeof(PrototypeChart), false);

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

            GUI.enabled = Application.isPlaying && songPlayback != null && songPlayback.IsPrepared && !isRecording;

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

                GUI.enabled = isRecording;

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

            if (isRecording)
            {

                EditorGUILayout.HelpBox(
                    $"녹화 중 · {chart.MusicalParts[selectedPartIndex].DisplayName} · 숫자키를 누르세요",
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
                DisableRecordingInputActions();
                return;

            }

            RefreshSongPlayback();

            if (isRecording &&
                songPlayback != null &&
                isLoopRecording &&
                loopEnd > loopStart &&
                songPlayback.SongTime >= loopEnd)
            {

                songPlayback.Seek(loopStart);

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

            DisableGameplaySessionForAuthoring();

            Array.Clear(lastRecordedInputTimestamps, 0, lastRecordedInputTimestamps.Length);
            isRecording = true;
            isLoopRecording = startMode == RecordingStartMode.Loop;
            EnableRecordingInputActions();

            if (startMode == RecordingStartMode.Loop)
            {

                songPlayback.Seek(loopStart);

            }
            else if (startMode == RecordingStartMode.Beginning)
            {

                songPlayback.Restart();

            }

            songPlayback.Play();

            statusMessage = startMode switch
            {
                RecordingStartMode.CurrentPosition => "현재 위치부터 녹화를 시작했습니다.",
                RecordingStartMode.Loop => "지정한 구간의 반복 녹화를 시작했습니다.",
                _ => "곡 처음부터 녹화를 시작했습니다."
            };

        }

        private void StopRecording()
        {

            isRecording = false;
            isLoopRecording = false;
            DisableRecordingInputActions();
            statusMessage = $"녹화를 중지했습니다. 임시 기록에 노트 {recordedNotes.Count}개가 있습니다.";

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

                double startTime = firstTime;
                double endTime = Math.Max(firstTime + 0.001d, lastTime + 0.001d);
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

        private void DisableGameplaySessionForAuthoring()
        {

            GameplaySession gameplaySession = FindAnyObjectByType<GameplaySession>();

            if (gameplaySession != null && gameplaySession.enabled)
            {

                gameplaySession.enabled = false;
                statusMessage =
                    "차트 작업을 위해 게임플레이 입력과 노트 생성을 멈췄습니다. " +
                    "차트를 적용한 뒤 플레이 모드를 다시 시작하세요.";

            }

        }

        private void RefreshSongPlayback()
        {

            if (!Application.isPlaying || songPlayback != null)
            {

                return;

            }

            songPlayback = FindAnyObjectByType<FmodSongPlayback>();

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
