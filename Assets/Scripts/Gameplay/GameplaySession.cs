using System.Collections;
using System.Collections.Generic;
using IdiotTape.Audio;
using UnityEngine;
using UnityEngine.InputSystem.LowLevel;

namespace IdiotTape.Gameplay
{

    public sealed class GameplaySession : MonoBehaviour
    {

        private const float SongPreparationTimeoutSeconds = 15f;

        private sealed class ActiveNote
        {

            public ActiveNote(ChartNote note, RuntimeNoteView view)
            {

                Note = note;
                View = view;

            }

            public ChartNote Note { get; }
            public RuntimeNoteView View { get; }

        }

        [SerializeField] private PrototypeChart chart;
        [SerializeField] private FmodSongPlayback songPlayback;
        [SerializeField] private GameplayInputRouter inputRouter;
        [SerializeField] private PlayfieldPresenter presenter;
        [SerializeField] private GameplayHud hud;
        [SerializeField] private JudgementSettings judgementSettings = new();

        private readonly List<ActiveNote> activeNotes = new();
        private readonly List<JudgementCandidate> judgementCandidates = new();
        private int nextNoteIndex;
        private int score;
        private int combo;
        private bool isReady;

        private void OnEnable()
        {

            inputRouter.Pressed += HandlePressed;
            inputRouter.LanePressed += HandleLanePressed;
            inputRouter.RestartRequested += BeginSession;
            inputRouter.PauseRequested += TogglePause;

        }

        private void OnDisable()
        {

            inputRouter.Pressed -= HandlePressed;
            inputRouter.LanePressed -= HandleLanePressed;
            inputRouter.RestartRequested -= BeginSession;
            inputRouter.PauseRequested -= TogglePause;

        }

        private IEnumerator Start()
        {

            if (!chart.TryValidate(out string error))
            {

                Debug.LogError($"Cannot start prototype chart: {error}", chart);
                enabled = false;
                yield break;

            }

            songPlayback.ConfigureEventPath(chart.SongEventPath, false);
            songPlayback.ConfigureStemParameters(chart.StemParameters);
            songPlayback.Prepare();
            float preparationDeadline = Time.realtimeSinceStartup + SongPreparationTimeoutSeconds;

            while (!songPlayback.IsPrepared &&
                   !songPlayback.PreparationFailed &&
                   Time.realtimeSinceStartup < preparationDeadline)
            {

                yield return null;

            }

            if (!songPlayback.IsPrepared)
            {

                Debug.LogError(
                    $"Cannot prepare FMOD song event '{chart.SongEventPath}'.",
                    chart);
                enabled = false;
                yield break;

            }

            isReady = true;
            BeginSession();

        }

        private void Update()
        {

            if (!isReady || songPlayback.IsPaused)
            {

                return;

            }

            double songTime = songPlayback.SongTime;
            SpawnUpcomingNotes(songTime);
            UpdateActiveNotes(songTime);
            double duration = songPlayback.DurationSeconds > 0d
                ? songPlayback.DurationSeconds
                : chart.Duration;
            hud.SetProgress(duration <= 0d ? 0f : (float)(songTime / duration));

        }

        private void BeginSession()
        {

            if (!isReady)
            {

                return;

            }

            for (int index = activeNotes.Count - 1; index >= 0; index--)
            {

                activeNotes[index].View.Remove();

            }

            activeNotes.Clear();
            judgementCandidates.Clear();
            nextNoteIndex = 0;
            score = 0;
            combo = 0;
            hud.SetScore(score);
            hud.ShowCombo(0);
            hud.ShowJudgement(JudgementGrade.None);
            hud.SetPaused(false);
            hud.SetProgress(0f);
            songPlayback.Restart();

        }

        private void SpawnUpcomingNotes(double songTime)
        {

            while (nextNoteIndex < chart.Notes.Count &&
                   chart.Notes[nextNoteIndex].HitTime - songTime <= chart.VisualLeadTime)
            {

                ChartNote note = chart.Notes[nextNoteIndex];
                Color color = chart.GetPartColor(note.MusicalPartId);
                RuntimeNoteView view = presenter.CreateNote(note, color, chart.LaneCount, chart.VisualLeadTime);
                activeNotes.Add(new ActiveNote(note, view));
                nextNoteIndex++;

            }

        }

        private void UpdateActiveNotes(double songTime)
        {

            for (int index = activeNotes.Count - 1; index >= 0; index--)
            {

                ActiveNote activeNote = activeNotes[index];
                activeNote.View.UpdatePresentation(songTime);

                if (songTime - activeNote.Note.HitTime <= judgementSettings.GoodWindowSeconds)
                {

                    continue;

                }

                activeNote.View.Remove();
                activeNotes.RemoveAt(index);
                combo = 0;
                hud.ShowCombo(combo);
                hud.ShowJudgement(JudgementGrade.Miss);

            }

        }

        private void HandlePressed(Vector2 screenPosition, double eventTimestamp)
        {

            if (!isReady)
            {

                return;

            }

            if (hud.IsPauseButtonPress(screenPosition))
            {

                TogglePause();
                return;

            }

            if (songPlayback.IsPaused || !presenter.TryGetInputPosition(screenPosition, out float normalizedX))
            {

                return;

            }

            JudgeInput(normalizedX, eventTimestamp);

        }

        private void HandleLanePressed(int laneIndex, double eventTimestamp)
        {

            if (!isReady || songPlayback.IsPaused || laneIndex < 0 || laneIndex >= chart.LaneCount)
            {

                return;

            }

            float normalizedX = PlayfieldGeometry.GetLaneCenterNormalized(laneIndex, chart.LaneCount);
            JudgeInput(normalizedX, eventTimestamp);

        }

        private void JudgeInput(float normalizedX, double eventTimestamp)
        {

            judgementCandidates.Clear();

            for (int index = 0; index < activeNotes.Count; index++)
            {

                ActiveNote activeNote = activeNotes[index];
                judgementCandidates.Add(new JudgementCandidate(
                    index,
                    activeNote.Note.HitTime,
                    activeNote.Note.LaneIndex));

            }

            double inputSongTime = songPlayback.GetSongTimeForExternalTimestamp(
                eventTimestamp,
                InputState.currentTime);
            JudgementResult result = JudgementEvaluator.Evaluate(
                judgementCandidates,
                inputSongTime,
                normalizedX,
                chart.LaneCount,
                judgementSettings);

            if (!result.HasHit)
            {

                return;

            }

            ActiveNote judgedNote = activeNotes[result.SourceIndex];
            Color partColor = chart.GetPartColor(judgedNote.Note.MusicalPartId);
            presenter.PlayHitFeedback(judgedNote.Note, chart.LaneCount, partColor);
            judgedNote.View.Remove();
            activeNotes.RemoveAt(result.SourceIndex);
            combo++;
            score += result.Grade == JudgementGrade.Perfect ? 1000 : 700;
            hud.SetScore(score);
            hud.ShowCombo(combo);
            hud.ShowJudgement(result.Grade);
            hud.ShowInstrument(
                chart.GetPartDisplayName(judgedNote.Note.MusicalPartId),
                partColor);

        }

        private void TogglePause()
        {

            if (!isReady)
            {

                return;

            }

            if (songPlayback.IsPaused)
            {

                songPlayback.Resume();
                hud.SetPaused(false);

            }
            else
            {

                songPlayback.Pause();
                hud.SetPaused(true);

            }

        }

    }

}
