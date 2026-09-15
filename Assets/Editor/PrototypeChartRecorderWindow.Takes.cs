using System;
using IdiotTape.Gameplay;
using UnityEditor;
using UnityEngine;

namespace IdiotTape.EditorTools
{

    public sealed partial class PrototypeChartRecorderWindow
    {

        [Serializable]
        private sealed class RecordingTake
        {

            public string id;
            public PrototypeChart chart;
            public string musicalPartId;
            public bool wasApplied;
            public RecordingStartMode startMode;
            public RecordingNoteMode noteMode;
            public FlickDefaultDirection flickDirection;
            public double targetTime;
            public double loopStart;
            public double loopEnd;
            public int loopStartBar;
            public int loopEndBar;
            public int loopBarCount;
            public int countInBars;
            public bool metronomeDuringRecording;
            public bool automaticQuantization;
            public QuantizationGrid quantizationGrid;
            public float maximumQuantizationMilliseconds;
            public float quantizationStrength;
            public float inputAdvanceMilliseconds;
            public float chordGroupingMilliseconds;

        }

        [SerializeField] private RecordingTake lastRecordingTake;
        [NonSerialized] private string currentRecordingTakeId = string.Empty;

        private void BeginRecordingTake(RecordingStartMode startMode, double targetTime)
        {

            // Explicit starts create takes. Automatic loop cycles retain this ID even when
            // their times interleave with earlier buffer contents after sorting/quantization.
            lastRecordingTake = new RecordingTake
            {

                id = Guid.NewGuid().ToString("N"),
                chart = chart,
                musicalPartId = chart.MusicalParts[selectedPartIndex].Id,
                startMode = startMode,
                noteMode = recordingNoteMode,
                flickDirection = flickDefaultDirection,
                targetTime = targetTime,
                loopStart = loopStart,
                loopEnd = loopEnd,
                loopStartBar = loopStartBar,
                loopEndBar = loopEndBar,
                loopBarCount = loopBarCount,
                countInBars = countInBars,
                metronomeDuringRecording = metronomeDuringRecording,
                automaticQuantization = automaticQuantization,
                quantizationGrid = quantizationGrid,
                maximumQuantizationMilliseconds = maximumQuantizationMilliseconds,
                quantizationStrength = quantizationStrength,
                inputAdvanceMilliseconds = inputAdvanceMilliseconds,
                chordGroupingMilliseconds = chordGroupingMilliseconds

            };
            currentRecordingTakeId = lastRecordingTake.id;

        }

        private bool CanRetryLastTake()
        {

            if (recordingPhase != RecordingPhase.Idle || chart == null || lastRecordingTake == null ||
                string.IsNullOrEmpty(lastRecordingTake.id) || lastRecordingTake.chart != chart ||
                lastRecordingTake.wasApplied || chart.TempoSections.Count == 0)
            {

                return false;

            }

            for (int index = 0; index < chart.MusicalParts.Count; index++)
            {

                if (chart.MusicalParts[index].Id == lastRecordingTake.musicalPartId)
                {

                    return true;

                }

            }

            return false;

        }

        private void DrawWorkspaceTakeRetry()
        {

            bool available = CanRetryLastTake() && songPlayback != null && songPlayback.IsPrepared;
            using (new EditorGUI.DisabledScope(!available))
            {

                if (GUILayout.Button(new GUIContent("테이크 재녹음",
                    "Shift+R · 마지막 녹화 시작~중지 사이의 임시 노트만 다시 녹음합니다. 이전 기록은 유지하며 Undo로 복원할 수 있습니다. 차트에 반영한 테이크는 재녹음할 수 없습니다."),
                    GUILayout.Width(108f), GUILayout.Height(24f)))
                {

                    RetryLastTake();

                }

                CaptureWorkspaceActionRect("retryTake");

            }

        }

        private void RetryLastTake()
        {

            if (!CanRetryLastTake())
            {

                statusMessage = "같은 차트에서 아직 반영하지 않은 마지막 테이크를 녹화 중지 후 다시 녹음할 수 있습니다.";
                return;

            }

            if (songPlayback == null || !songPlayback.IsPrepared)
            {

                statusMessage = "테이크 재녹음 전에 FMOD 재생을 준비하세요. 임시 기록은 유지했습니다.";
                return;

            }

            RecordingTake previousTake = lastRecordingTake;
            RestoreRecordingTakeContext(previousTake);

            // Scheduling must succeed before removing any completed input. Current-position
            // retries reuse the captured target, never the transport's later stop position.
            if (!CanStartRecording(previousTake.startMode) ||
                !StartRecordingAt(previousTake.startMode, previousTake.targetTime))
            {

                return;

            }

            int removed = ReplaceRecordedTake(previousTake);
            statusMessage = $"마지막 테이크의 임시 노트 {removed}개를 되돌리고 같은 설정으로 재녹음을 시작했습니다. Undo로 복원할 수 있습니다.";

        }

        private void RestoreRecordingTakeContext(RecordingTake take)
        {

            selectedPartIndex = FindPartIndex(take.musicalPartId);
            recordingNoteMode = take.noteMode;
            flickDefaultDirection = take.flickDirection;
            workspaceRecordingStart = take.startMode;
            loopStart = take.loopStart;
            loopEnd = take.loopEnd;
            loopStartBar = take.loopStartBar;
            loopEndBar = take.loopEndBar;
            loopBarCount = take.loopBarCount;
            countInBars = take.countInBars;
            metronomeDuringRecording = take.metronomeDuringRecording;
            automaticQuantization = take.automaticQuantization;
            quantizationGrid = take.quantizationGrid;
            maximumQuantizationMilliseconds = take.maximumQuantizationMilliseconds;
            quantizationStrength = take.quantizationStrength;
            inputAdvanceMilliseconds = take.inputAdvanceMilliseconds;
            chordGroupingMilliseconds = take.chordGroupingMilliseconds;

        }

        private int ReplaceRecordedTake(RecordingTake previousTake)
        {

            Undo.FlushUndoRecordObjects();
            Undo.IncrementCurrentGroup();
            int undoGroup = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("마지막 테이크 재녹음");
            Undo.RegisterCompleteObjectUndo(this, "마지막 테이크 재녹음");
            int removed = 0;

            for (int index = recordedNotes.Count - 1; index >= 0; index--)
            {

                if (recordedNotes[index].takeId == previousTake.id)
                {

                    recordedNotes.RemoveAt(index);
                    removed++;

                }

            }

            pendingInteraction = null;
            ClearWorkspaceMultiSelection();
            selectedRecordedNoteIndex = -1;
            selectedChartNoteId = string.Empty;
            selectedAppliedNoteDataId = string.Empty;
            selectedAppliedNoteData = null;
            BeginRecordingTake(previousTake.startMode, previousTake.targetTime);
            Undo.CollapseUndoOperations(undoGroup);
            Undo.IncrementCurrentGroup();
            Repaint();
            return removed;

        }

        private void MarkLastRecordingTakeApplied()
        {

            if (lastRecordingTake != null && lastRecordingTake.chart == chart)
            {

                lastRecordingTake.wasApplied = true;

            }

        }

    }

}
