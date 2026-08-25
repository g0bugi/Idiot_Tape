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

            public ActiveNote(ChartNote note, RuntimeNoteView view, bool isPlayable)
            {

                Note = note;
                View = view;
                IsPlayable = isPlayable;

            }

            public ChartNote Note { get; }
            public RuntimeNoteView View { get; }
            public bool IsPlayable { get; }

        }

        private sealed class ActiveTimingGuide
        {

            public ActiveTimingGuide(double beatTime, RuntimeTimingGuideView view)
            {

                BeatTime = beatTime;
                View = view;

            }

            public double BeatTime { get; }
            public RuntimeTimingGuideView View { get; }

        }

        [SerializeField] private PrototypeChart chart;
        [SerializeField] private FmodSongPlayback songPlayback;
        [SerializeField] private GameplayInputRouter inputRouter;
        [SerializeField] private PlayfieldPresenter presenter;
        [SerializeField] private GameplayHud hud;
        [SerializeField] private JudgementSettings judgementSettings = new();

        private readonly List<ActiveNote> activeNotes = new();
        private readonly List<ActiveTimingGuide> activeTimingGuides = new();
        private readonly List<JudgementCandidate> judgementCandidates = new();
        private readonly Dictionary<int, int> activeContactLanes = new();
        private readonly HashSet<int> speedSliderContacts = new();
        private int[] activeLanePressCounts;
        private int nextNoteIndex;
        private double nextTimingGuideTime;
        private int score;
        private int combo;
        private bool isReady;

        private void OnEnable()
        {

            inputRouter.ContactPressed += HandleContactPressed;
            inputRouter.ContactMoved += HandleContactMoved;
            inputRouter.ContactReleased += HandleContactReleased;
            inputRouter.LanePressed += HandleLanePressed;
            inputRouter.LaneReleased += HandleLaneReleased;
            inputRouter.RestartRequested += BeginSession;
            inputRouter.PauseRequested += TogglePause;

        }

        private void OnDisable()
        {

            inputRouter.ContactPressed -= HandleContactPressed;
            inputRouter.ContactMoved -= HandleContactMoved;
            inputRouter.ContactReleased -= HandleContactReleased;
            inputRouter.LanePressed -= HandleLanePressed;
            inputRouter.LaneReleased -= HandleLaneReleased;
            inputRouter.RestartRequested -= BeginSession;
            inputRouter.PauseRequested -= TogglePause;
            ClearLanePresses();

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
            activeLanePressCounts = new int[chart.LaneCount];
            presenter.ConfigureLaneFeedback(chart.LaneCount);
            BeginSession();

        }

        private void Update()
        {

            if (!isReady || songPlayback.IsPaused)
            {

                return;

            }

            double songTime = songPlayback.SongTime;
            float visualLeadTime = NoteSpeedMath.GetVisualLeadTime(
                chart.VisualLeadTime,
                hud.NoteSpeedMultiplier);
            SpawnUpcomingNotes(songTime, visualLeadTime);
            SpawnUpcomingTimingGuides(songTime, visualLeadTime);
            UpdateActiveNotes(songTime, visualLeadTime);
            UpdateActiveTimingGuides(songTime, visualLeadTime);
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

            for (int index = activeTimingGuides.Count - 1; index >= 0; index--)
            {

                activeTimingGuides[index].View.Remove();

            }

            activeTimingGuides.Clear();
            judgementCandidates.Clear();
            nextNoteIndex = 0;
            nextTimingGuideTime = chart.TempoSections.Count > 0
                ? ChartTempoMap.GetBeatTimeAtOrBefore(chart.TempoSections, 0d)
                : 0d;
            score = 0;
            combo = 0;
            hud.SetScore(score);
            hud.ShowCombo(0);
            hud.ShowJudgement(JudgementGrade.None);
            hud.SetPaused(false);
            hud.SetProgress(0f);
            ClearLanePresses();
            songPlayback.Restart();

        }

        private void SpawnUpcomingNotes(double songTime, float visualLeadTime)
        {

            while (nextNoteIndex < chart.Notes.Count &&
                   chart.Notes[nextNoteIndex].HitTime - songTime <= visualLeadTime)
            {

                ChartNote note = chart.Notes[nextNoteIndex];
                Color color = chart.GetPartColor(note.MusicalPartId);
                bool isPlayable = chart.IsNotePlayable(note);
                RuntimeNoteView view = presenter.CreateNote(
                    note,
                    color,
                    chart.LaneCount,
                    visualLeadTime,
                    isPlayable);
                activeNotes.Add(new ActiveNote(note, view, isPlayable));
                nextNoteIndex++;

            }

        }

        private void UpdateActiveNotes(double songTime, float visualLeadTime)
        {

            for (int index = activeNotes.Count - 1; index >= 0; index--)
            {

                ActiveNote activeNote = activeNotes[index];
                activeNote.View.SetVisualLeadTime(visualLeadTime);
                activeNote.View.UpdatePresentation(songTime);

                if (songTime - activeNote.Note.HitTime <= judgementSettings.GoodWindowSeconds)
                {

                    continue;

                }

                activeNote.View.Remove();
                activeNotes.RemoveAt(index);

                if (!activeNote.IsPlayable)
                {

                    continue;

                }

                combo = 0;
                hud.ShowCombo(combo);
                hud.ShowJudgement(JudgementGrade.Miss);

            }

        }

        private void SpawnUpcomingTimingGuides(double songTime, float visualLeadTime)
        {

            if (chart.TempoSections.Count == 0)
            {

                return;

            }

            int guard = 0;

            while (nextTimingGuideTime - songTime <= visualLeadTime && guard++ < 128)
            {

                double beatTime = nextTimingGuideTime;
                nextTimingGuideTime = ChartTempoMap.GetBeatTimeAfter(
                    chart.TempoSections,
                    beatTime + 0.000001d);

                if (beatTime <= songTime)
                {

                    continue;

                }

                ChartBeatPosition position = ChartTempoMap.GetBeatPosition(
                    chart.TempoSections,
                    beatTime);
                RuntimeTimingGuideView view = presenter.CreateTimingGuide(
                    beatTime,
                    position.Bar,
                    position.Beat,
                    position.Beat == 1,
                    visualLeadTime);
                view.UpdatePresentation(songTime);
                activeTimingGuides.Add(new ActiveTimingGuide(beatTime, view));

            }

        }

        private void UpdateActiveTimingGuides(double songTime, float visualLeadTime)
        {

            for (int index = activeTimingGuides.Count - 1; index >= 0; index--)
            {

                ActiveTimingGuide guide = activeTimingGuides[index];

                if (songTime >= guide.BeatTime)
                {

                    guide.View.Remove();
                    activeTimingGuides.RemoveAt(index);
                    continue;

                }

                guide.View.SetVisualLeadTime(visualLeadTime);
                guide.View.UpdatePresentation(songTime);

            }

        }

        private void HandleContactPressed(int contactId, Vector2 screenPosition, double eventTimestamp)
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

            if (hud.TrySetNoteSpeedFromScreenPosition(screenPosition))
            {

                speedSliderContacts.Add(contactId);
                return;

            }

            if (songPlayback.IsPaused || !presenter.TryGetInputPosition(screenPosition, out float normalizedX))
            {

                return;

            }

            if (activeContactLanes.TryGetValue(contactId, out int previousLaneIndex))
            {

                RemoveLanePress(previousLaneIndex);

            }

            int laneIndex = PlayfieldGeometry.GetLaneIndex(normalizedX, chart.LaneCount);
            activeContactLanes[contactId] = laneIndex;
            AddLanePress(laneIndex);
            JudgeInput(normalizedX, eventTimestamp);

        }

        private void HandleContactMoved(int contactId, Vector2 screenPosition)
        {

            if (!speedSliderContacts.Contains(contactId))
            {

                return;

            }

            hud.TrySetNoteSpeedFromScreenPosition(screenPosition, false);

        }

        private void HandleContactReleased(int contactId)
        {

            if (speedSliderContacts.Remove(contactId))
            {

                return;

            }

            if (!activeContactLanes.Remove(contactId, out int laneIndex))
            {

                return;

            }

            RemoveLanePress(laneIndex);

        }

        private void HandleLanePressed(int laneIndex, double eventTimestamp)
        {

            if (!isReady || songPlayback.IsPaused || laneIndex < 0 || laneIndex >= chart.LaneCount)
            {

                return;

            }

            float normalizedX = PlayfieldGeometry.GetLaneCenterNormalized(laneIndex, chart.LaneCount);
            AddLanePress(laneIndex);
            JudgeInput(normalizedX, eventTimestamp);

        }

        private void HandleLaneReleased(int laneIndex)
        {

            if (laneIndex < 0 || laneIndex >= chart.LaneCount)
            {

                return;

            }

            RemoveLanePress(laneIndex);

        }

        private void JudgeInput(float normalizedX, double eventTimestamp)
        {

            judgementCandidates.Clear();

            for (int index = 0; index < activeNotes.Count; index++)
            {

                ActiveNote activeNote = activeNotes[index];

                if (!activeNote.IsPlayable)
                {

                    continue;

                }

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
            presenter.PlayHitFeedback(
                judgedNote.Note,
                chart.LaneCount,
                partColor,
                result.Grade);
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

                ClearLanePresses();
                songPlayback.Pause();
                hud.SetPaused(true);

            }

        }

        private void AddLanePress(int laneIndex)
        {

            if (activeLanePressCounts == null)
            {

                return;

            }

            activeLanePressCounts[laneIndex]++;

            if (activeLanePressCounts[laneIndex] == 1)
            {

                if (presenter != null)
                {

                    presenter.SetLanePressed(laneIndex, true);

                }

            }

        }

        private void RemoveLanePress(int laneIndex)
        {

            if (activeLanePressCounts == null || activeLanePressCounts[laneIndex] <= 0)
            {

                return;

            }

            activeLanePressCounts[laneIndex]--;

            if (activeLanePressCounts[laneIndex] == 0)
            {

                if (presenter != null)
                {

                    presenter.SetLanePressed(laneIndex, false);

                }

            }

        }

        private void ClearLanePresses()
        {

            activeContactLanes.Clear();
            speedSliderContacts.Clear();

            if (activeLanePressCounts == null)
            {

                return;

            }

            for (int laneIndex = 0; laneIndex < activeLanePressCounts.Length; laneIndex++)
            {

                activeLanePressCounts[laneIndex] = 0;

                if (presenter != null)
                {

                    presenter.SetLanePressed(laneIndex, false);

                }

            }

        }

    }

}
