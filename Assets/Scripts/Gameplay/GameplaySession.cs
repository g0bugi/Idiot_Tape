using System;
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
        private const int KeyboardContactIdBase = -1000;
        private const double ScheduleTolerance = 0.000001d;
        private const int PerfectTapScore = 1000;
        private const int GoodTapScore = 700;
        private const int BananaCheckpointScore = PerfectTapScore / 10;

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
            public bool HasStarted { get; set; }
            public bool HasFailed { get; set; }
            public bool ReleasedInHoldGrace { get; set; }
            public JudgementGrade StartGrade { get; set; }
            public int? PrimaryContactId { get; set; }
            public double NextQuarterCheckTime { get; set; } = double.PositiveInfinity;
            public double NextHalfRewardTime { get; set; } = double.PositiveInfinity;
            public int NextSlideNodeIndex { get; set; }
            public int NextBananaCheckpointIndex { get; set; }
            public int SuccessfulBananaCheckpoints { get; set; }
            public bool TerminalFlickReady { get; set; }
            public float FlickStartNormalizedX { get; set; }

        }

        private sealed class ContactState
        {

            private readonly List<ContactSample> samples = new();

            public ContactState(int id, float normalizedX, double songTime)
            {

                Id = id;
                NormalizedX = normalizedX;
                PreviousNormalizedX = normalizedX;
                SongTime = songTime;
                PreviousSongTime = songTime;
                IsDown = true;
                samples.Add(new ContactSample(songTime, normalizedX, true));

            }

            public int Id { get; }
            public float NormalizedX { get; set; }
            public float PreviousNormalizedX { get; set; }
            public double SongTime { get; set; }
            public double PreviousSongTime { get; set; }
            public bool IsDown { get; set; }
            public int LaneIndex { get; set; }
            public ActiveNote Owner { get; set; }

            public void Record(float normalizedX, double songTime, bool isDown)
            {

                PreviousNormalizedX = NormalizedX;
                PreviousSongTime = SongTime;
                NormalizedX = normalizedX;
                SongTime = songTime;
                IsDown = isDown;
                samples.Add(new ContactSample(songTime, normalizedX, isDown));

            }

            public bool TryGetNormalizedX(double songTime, out float normalizedX)
            {

                ContactSample previous = samples[0];

                if (songTime < previous.SongTime - ScheduleTolerance)
                {

                    normalizedX = 0f;
                    return false;

                }

                for (int index = 1; index < samples.Count; index++)
                {

                    ContactSample next = samples[index];

                    if (next.SongTime > songTime + ScheduleTolerance)
                    {

                        if (!previous.IsDown)
                        {

                            normalizedX = 0f;
                            return false;

                        }

                        if (next.IsDown && next.SongTime > previous.SongTime + ScheduleTolerance)
                        {

                            float progress = Mathf.Clamp01(
                                (float)((songTime - previous.SongTime) /
                                        (next.SongTime - previous.SongTime)));
                            normalizedX = Mathf.Lerp(
                                previous.NormalizedX,
                                next.NormalizedX,
                                progress);
                            return true;

                        }

                        normalizedX = previous.NormalizedX;
                        return true;

                    }

                    previous = next;

                }

                normalizedX = previous.NormalizedX;
                return previous.IsDown;

            }

        }

        private readonly struct ContactSample
        {

            public ContactSample(double songTime, float normalizedX, bool isDown)
            {

                SongTime = songTime;
                NormalizedX = normalizedX;
                IsDown = isDown;

            }

            public double SongTime { get; }
            public float NormalizedX { get; }
            public bool IsDown { get; }

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
        private readonly Dictionary<int, ContactState> contacts = new();
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
            inputRouter.FlickAssistRequested += HandleFlickAssistRequested;
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
            inputRouter.FlickAssistRequested -= HandleFlickAssistRequested;
            inputRouter.RestartRequested -= BeginSession;
            inputRouter.PauseRequested -= TogglePause;
            ClearContactsAndLanePresses();

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

                Debug.LogError($"Cannot prepare FMOD song event '{chart.SongEventPath}'.", chart);
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
            ClearContactsAndLanePresses();
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

                if (!activeNote.IsPlayable)
                {

                    if (songTime > activeNote.Note.EndTime + judgementSettings.GoodWindowSeconds)
                    {

                        RemoveActiveNoteAt(index);

                    }

                    continue;

                }

                if (activeNote.HasFailed)
                {

                    if (songTime >= activeNote.Note.EndTime)
                    {

                        RemoveActiveNoteAt(index);

                    }

                    continue;

                }

                if (!activeNote.HasStarted)
                {

                    if (activeNote.Note.NoteType == ChartNoteType.Flick)
                    {

                        TryAcquireWaitingFlick(activeNote, songTime);

                    }

                    if (!activeNote.HasStarted &&
                        songTime > activeNote.Note.HitTime + judgementSettings.GoodWindowSeconds)
                    {

                        ShowMiss();
                        RemoveActiveNoteAt(index);

                    }

                    continue;

                }

                switch (activeNote.Note.NoteType)
                {

                    case ChartNoteType.Hold:
                        UpdateHold(activeNote, songTime);
                        break;
                    case ChartNoteType.Slide:
                        UpdateSlide(activeNote, songTime);
                        break;
                    case ChartNoteType.Flick:
                        if (songTime > activeNote.Note.HitTime + judgementSettings.GoodWindowSeconds)
                        {

                            FailAndRemove(activeNote);

                        }
                        break;
                    case ChartNoteType.Banana:
                        UpdateBanana(activeNote, songTime);
                        break;

                }

            }

        }

        private void UpdateHold(ActiveNote activeNote, double songTime)
        {

            if (activeNote.ReleasedInHoldGrace)
            {

                if (songTime >= activeNote.Note.EndTime)
                {

                    AwardTapEquivalent(activeNote, activeNote.StartGrade, activeNote.Note.EndLaneIndex);
                    RemoveActiveNote(activeNote);

                }

                return;

            }

            while (activeNote.NextQuarterCheckTime < activeNote.Note.EndTime - ScheduleTolerance &&
                   activeNote.NextQuarterCheckTime <= songTime + ScheduleTolerance)
            {

                if (!IsPrimaryContactInLane(
                        activeNote,
                        activeNote.Note.LaneIndex,
                        activeNote.NextQuarterCheckTime))
                {

                    FailSustained(activeNote);
                    return;

                }

                if (Math.Abs(activeNote.NextHalfRewardTime - activeNote.NextQuarterCheckTime) <=
                    ScheduleTolerance)
                {

                    AwardTapEquivalent(activeNote, activeNote.StartGrade, activeNote.Note.LaneIndex, false);
                    activeNote.NextHalfRewardTime = ChartTempoMap.GetSubdivisionTimeAfter(
                        chart.TempoSections,
                        activeNote.NextHalfRewardTime,
                        2);

                }

                activeNote.NextQuarterCheckTime = ChartTempoMap.GetSubdivisionTimeAfter(
                    chart.TempoSections,
                    activeNote.NextQuarterCheckTime,
                    4);

            }

            if (songTime >= activeNote.Note.EndTime)
            {

                if (!IsPrimaryContactInLane(
                        activeNote,
                        activeNote.Note.LaneIndex,
                        activeNote.Note.EndTime))
                {

                    FailSustained(activeNote);
                    return;

                }

                AwardTapEquivalent(activeNote, activeNote.StartGrade, activeNote.Note.EndLaneIndex);
                RemoveActiveNote(activeNote);

            }

        }

        private void UpdateSlide(ActiveNote activeNote, double songTime)
        {

            double terminalStartTime = GetTerminalFlickStartTime(activeNote.Note);

            if (activeNote.Note.SlideEndBehavior == SlideEndBehavior.Flick &&
                !activeNote.TerminalFlickReady &&
                songTime >= terminalStartTime)
            {

                InitializeTerminalFlick(activeNote);

            }

            int middleNodeCount = Math.Max(0, activeNote.Note.SlideNodes.Count - 1);

            while (true)
            {

                double quarterTime = activeNote.NextQuarterCheckTime < activeNote.Note.EndTime - ScheduleTolerance
                    ? activeNote.NextQuarterCheckTime
                    : double.PositiveInfinity;
                double nodeTime = activeNote.NextSlideNodeIndex < middleNodeCount
                    ? activeNote.Note.SlideNodes[activeNote.NextSlideNodeIndex].Time
                    : double.PositiveInfinity;
                double checkTime = Math.Min(quarterTime, nodeTime);

                if (checkTime > songTime + ScheduleTolerance)
                {

                    break;

                }

                float expectedX = NotePathMath.GetSlideNormalizedX(
                    activeNote.Note,
                    checkTime,
                    chart.LaneCount);

                if (!TryClaimEligibleContact(activeNote, expectedX, checkTime))
                {

                    FailSustained(activeNote);
                    return;

                }

                bool isQuarter = Math.Abs(quarterTime - checkTime) <= ScheduleTolerance;
                bool isNode = Math.Abs(nodeTime - checkTime) <= ScheduleTolerance;

                if (isNode)
                {

                    ChartPathNode node = activeNote.Note.SlideNodes[activeNote.NextSlideNodeIndex];
                    AwardTapEquivalent(activeNote, activeNote.StartGrade, node.LaneIndex, false);
                    activeNote.NextSlideNodeIndex++;

                }

                if (isQuarter)
                {

                    if (Math.Abs(activeNote.NextHalfRewardTime - activeNote.NextQuarterCheckTime) <=
                        ScheduleTolerance)
                    {

                        AwardTapEquivalentAtNormalizedX(
                            activeNote,
                            activeNote.StartGrade,
                            expectedX,
                            false);
                        activeNote.NextHalfRewardTime = ChartTempoMap.GetSubdivisionTimeAfter(
                            chart.TempoSections,
                            activeNote.NextHalfRewardTime,
                            2);

                    }

                    activeNote.NextQuarterCheckTime = ChartTempoMap.GetSubdivisionTimeAfter(
                        chart.TempoSections,
                        activeNote.NextQuarterCheckTime,
                        4);

                }

            }

            if (activeNote.Note.SlideEndBehavior == SlideEndBehavior.Flick)
            {

                if (songTime > activeNote.Note.EndTime + judgementSettings.GoodWindowSeconds)
                {

                    FailSustained(activeNote);

                }

                return;

            }

            if (songTime >= activeNote.Note.EndTime)
            {

                float endX = PlayfieldGeometry.GetLaneCenterNormalized(
                    activeNote.Note.EndLaneIndex,
                    chart.LaneCount);

                if (!TryClaimEligibleContact(activeNote, endX, activeNote.Note.EndTime))
                {

                    FailSustained(activeNote);
                    return;

                }

                AwardTapEquivalent(activeNote, activeNote.StartGrade, activeNote.Note.EndLaneIndex);
                RemoveActiveNote(activeNote);

            }

        }

        private void UpdateBanana(ActiveNote activeNote, double songTime)
        {

            while (activeNote.NextBananaCheckpointIndex < activeNote.Note.BananaCheckpoints.Count &&
                   activeNote.Note.BananaCheckpoints[activeNote.NextBananaCheckpointIndex].Time <=
                   songTime + ScheduleTolerance)
            {

                BananaCheckpoint checkpoint =
                    activeNote.Note.BananaCheckpoints[activeNote.NextBananaCheckpointIndex];

                if (TryClaimEligibleContact(
                        activeNote,
                        checkpoint.NormalizedX,
                        checkpoint.Time))
                {

                    activeNote.SuccessfulBananaCheckpoints++;
                    score += BananaCheckpointScore;
                    hud.SetScore(score);
                    activeNote.View.SetCharge(
                        (float)activeNote.SuccessfulBananaCheckpoints /
                        activeNote.Note.BananaCheckpoints.Count);

                }

                activeNote.NextBananaCheckpointIndex++;

            }

            if (songTime < activeNote.Note.EndTime)
            {

                return;

            }

            float endX = PlayfieldGeometry.GetLaneCenterNormalized(
                activeNote.Note.EndLaneIndex,
                chart.LaneCount);

            bool wasAtEndOnTime = TryClaimEligibleContact(
                activeNote,
                endX,
                activeNote.Note.EndTime);

            if (wasAtEndOnTime || TryClaimEligibleContact(activeNote, endX, songTime))
            {

                double timingError = wasAtEndOnTime
                    ? 0d
                    : Math.Abs(songTime - activeNote.Note.EndTime);
                JudgementGrade grade = timingError <= judgementSettings.PerfectWindowSeconds
                    ? JudgementGrade.Perfect
                    : JudgementGrade.Good;
                AwardTapEquivalent(activeNote, grade, activeNote.Note.EndLaneIndex);
                int bonusCombo = NoteInteractionMath.GetBananaBonusCombo(
                    activeNote.SuccessfulBananaCheckpoints,
                    activeNote.Note.BananaCheckpoints.Count,
                    activeNote.Note.BananaMaximumBonusCombo);
                combo += bonusCombo;
                hud.ShowCombo(combo);
                RemoveActiveNote(activeNote);
                return;

            }

            if (songTime > activeNote.Note.EndTime + judgementSettings.GoodWindowSeconds)
            {

                ShowMiss();
                RemoveActiveNote(activeNote);

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

            if (songPlayback.IsPaused ||
                !presenter.TryGetInputPosition(screenPosition, out float normalizedX))
            {

                return;

            }

            BeginContact(contactId, normalizedX, ConvertInputTimestamp(eventTimestamp));

        }

        private void HandleContactMoved(int contactId, Vector2 screenPosition, double eventTimestamp)
        {

            if (speedSliderContacts.Contains(contactId))
            {

                hud.TrySetNoteSpeedFromScreenPosition(screenPosition, false);
                return;

            }

            if (!contacts.TryGetValue(contactId, out ContactState contact) ||
                !presenter.TryGetPlayfieldNormalizedX(screenPosition, out float normalizedX))
            {

                return;

            }

            int previousLane = contact.LaneIndex;
            contact.Record(normalizedX, ConvertInputTimestamp(eventTimestamp), true);
            contact.LaneIndex = PlayfieldGeometry.GetLaneIndex(normalizedX, chart.LaneCount);

            if (previousLane != contact.LaneIndex)
            {

                RemoveLanePress(previousLane);
                AddLanePress(contact.LaneIndex);

            }

            if (contact.Owner != null &&
                contact.Owner.Note.NoteType == ChartNoteType.Hold &&
                contact.LaneIndex != contact.Owner.Note.LaneIndex)
            {

                FailSustained(contact.Owner);
                return;

            }

            EvaluateFlickMotion(contact);

        }

        private void HandleContactReleased(int contactId, double eventTimestamp)
        {

            if (speedSliderContacts.Remove(contactId))
            {

                return;

            }

            if (!contacts.TryGetValue(contactId, out ContactState contact) || !contact.IsDown)
            {

                return;

            }

            contact.Record(
                contact.NormalizedX,
                ConvertInputTimestamp(eventTimestamp),
                false);
            RemoveLanePress(contact.LaneIndex);
            ActiveNote owner = contact.Owner;

            if (owner == null)
            {

                return;

            }

            if (owner.HasFailed || !owner.HasStarted)
            {

                ReleaseContact(contact);
                return;

            }

            if (owner.Note.NoteType == ChartNoteType.Hold)
            {

                UpdateHold(
                    owner,
                    Math.Min(
                        contact.SongTime,
                        owner.Note.EndTime - ScheduleTolerance));

                if (owner.HasFailed || !activeNotes.Contains(owner))
                {

                    ReleaseContact(contact);
                    return;

                }

                ReleaseContact(contact);

                if (NoteInteractionMath.IsInsideHoldGrace(
                        owner.Note,
                        chart.TempoSections,
                        contact.SongTime))
                {

                    owner.ReleasedInHoldGrace = true;

                }
                else
                {

                    FailSustained(owner);

                }

            }
            else
            {

                ReleaseContact(contact);

                if (owner.Note.NoteType == ChartNoteType.Flick)
                {

                    FailAndRemove(owner);

                }

            }

        }

        private void HandleLanePressed(int laneIndex, double eventTimestamp)
        {

            if (!isReady || songPlayback.IsPaused || laneIndex < 0 || laneIndex >= chart.LaneCount)
            {

                return;

            }

            float normalizedX = PlayfieldGeometry.GetLaneCenterNormalized(laneIndex, chart.LaneCount);
            BeginContact(KeyboardContactIdBase - laneIndex, normalizedX, ConvertInputTimestamp(eventTimestamp));

        }

        private void HandleLaneReleased(int laneIndex, double eventTimestamp)
        {

            if (laneIndex < 0 || laneIndex >= chart.LaneCount)
            {

                return;

            }

            HandleContactReleased(KeyboardContactIdBase - laneIndex, eventTimestamp);

        }

        private void HandleFlickAssistRequested(double eventTimestamp)
        {

            if (!isReady || songPlayback.IsPaused)
            {

                return;

            }

            double songTime = ConvertInputTimestamp(eventTimestamp);
            ActiveNote best = null;
            double bestError = double.PositiveInfinity;

            for (int index = 0; index < activeNotes.Count; index++)
            {

                ActiveNote candidate = activeNotes[index];
                bool ordinary = candidate.Note.NoteType == ChartNoteType.Flick;
                bool terminal = candidate.Note.NoteType == ChartNoteType.Slide &&
                                candidate.HasStarted &&
                                candidate.Note.SlideEndBehavior == SlideEndBehavior.Flick;

                if (!candidate.IsPlayable || candidate.HasFailed || (!ordinary && !terminal))
                {

                    continue;

                }

                double targetTime = ordinary ? candidate.Note.HitTime : candidate.Note.EndTime;
                double error = Math.Abs(songTime - targetTime);

                if (error <= judgementSettings.GoodWindowSeconds && error < bestError)
                {

                    best = candidate;
                    bestError = error;

                }

            }

            if (best == null)
            {

                return;

            }

            AwardTapEquivalent(
                best,
                best.Note.NoteType == ChartNoteType.Flick
                    ? JudgementGrade.Perfect
                    : best.StartGrade,
                best.Note.EndLaneIndex);
            RemoveActiveNote(best);

        }

        private void BeginContact(int contactId, float normalizedX, double songTime)
        {

            if (contacts.TryGetValue(contactId, out ContactState existing) && existing.IsDown)
            {

                RemoveLanePress(existing.LaneIndex);
                ReleaseContact(existing);

            }

            ContactState contact = new(contactId, normalizedX, songTime)
            {

                LaneIndex = PlayfieldGeometry.GetLaneIndex(normalizedX, chart.LaneCount)

            };
            contacts[contactId] = contact;
            AddLanePress(contact.LaneIndex);
            JudgeContactStart(contact);

        }

        private void JudgeContactStart(ContactState contact)
        {

            judgementCandidates.Clear();

            for (int index = 0; index < activeNotes.Count; index++)
            {

                ActiveNote candidateActiveNote = activeNotes[index];

                if (!candidateActiveNote.IsPlayable ||
                    candidateActiveNote.HasStarted ||
                    candidateActiveNote.HasFailed)
                {

                    continue;

                }

                judgementCandidates.Add(new JudgementCandidate(
                    index,
                    candidateActiveNote.Note.HitTime,
                    candidateActiveNote.Note.LaneIndex));

            }

            JudgementResult result = JudgementEvaluator.Evaluate(
                judgementCandidates,
                contact.SongTime,
                contact.NormalizedX,
                chart.LaneCount,
                judgementSettings);

            if (!result.HasHit)
            {

                return;

            }

            ActiveNote activeNote = activeNotes[result.SourceIndex];
            JudgementGrade grade = activeNote.Note.NoteType == ChartNoteType.Flick
                ? JudgementGrade.Perfect
                : result.Grade;
            StartInteraction(activeNote, contact, grade);

        }

        private void StartInteraction(
            ActiveNote activeNote,
            ContactState contact,
            JudgementGrade grade)
        {

            if (activeNote.Note.NoteType == ChartNoteType.Tap)
            {

                AwardTapEquivalent(activeNote, grade, activeNote.Note.LaneIndex);
                RemoveActiveNote(activeNote);
                return;

            }

            activeNote.HasStarted = true;
            activeNote.StartGrade = grade;
            ClaimContact(activeNote, contact);

            if (activeNote.Note.NoteType == ChartNoteType.Flick)
            {

                activeNote.FlickStartNormalizedX = contact.NormalizedX;
                return;

            }

            AwardTapEquivalent(activeNote, grade, activeNote.Note.LaneIndex);

            if (activeNote.Note.NoteType == ChartNoteType.Hold ||
                activeNote.Note.NoteType == ChartNoteType.Slide)
            {

                activeNote.NextQuarterCheckTime = ChartTempoMap.GetSubdivisionTimeAfter(
                    chart.TempoSections,
                    activeNote.Note.HitTime,
                    4);
                activeNote.NextHalfRewardTime = ChartTempoMap.GetSubdivisionTimeAfter(
                    chart.TempoSections,
                    activeNote.Note.HitTime,
                    2);

            }

        }

        private void TryAcquireWaitingFlick(ActiveNote activeNote, double songTime)
        {

            if (songTime < activeNote.Note.HitTime - judgementSettings.GoodWindowSeconds ||
                songTime > activeNote.Note.HitTime + judgementSettings.GoodWindowSeconds)
            {

                return;

            }

            float startX = PlayfieldGeometry.GetLaneCenterNormalized(
                activeNote.Note.LaneIndex,
                chart.LaneCount);
            ContactState contact = FindEligibleContact(activeNote, startX, songTime);

            if (contact != null)
            {

                StartInteraction(activeNote, contact, JudgementGrade.Perfect);

            }

        }

        private void EvaluateFlickMotion(ContactState contact)
        {

            ActiveNote owner = contact.Owner;

            if (owner == null || owner.HasFailed || !owner.HasStarted)
            {

                return;

            }

            if (owner.Note.NoteType == ChartNoteType.Flick)
            {

                EvaluateFlick(owner, contact, owner.Note.HitTime, JudgementGrade.Perfect);

            }
            else if (owner.Note.NoteType == ChartNoteType.Slide &&
                     owner.Note.SlideEndBehavior == SlideEndBehavior.Flick)
            {

                if (!owner.TerminalFlickReady &&
                    contact.SongTime >= GetTerminalFlickStartTime(owner.Note))
                {

                    InitializeTerminalFlick(owner);

                }

                if (owner.TerminalFlickReady)
                {

                    EvaluateFlick(owner, contact, owner.Note.EndTime, owner.StartGrade);

                }

            }

        }

        private void EvaluateFlick(
            ActiveNote activeNote,
            ContactState contact,
            double targetTime,
            JudgementGrade rewardGrade)
        {

            FlickMotionResult motionResult = NoteInteractionMath.EvaluateFlickMotion(
                activeNote.FlickStartNormalizedX,
                contact.PreviousNormalizedX,
                contact.NormalizedX,
                contact.PreviousSongTime,
                contact.SongTime,
                activeNote.Note.EndLaneIndex,
                chart.LaneCount,
                targetTime,
                judgementSettings);

            if (motionResult == FlickMotionResult.WrongDirection)
            {

                if (activeNote.Note.NoteType == ChartNoteType.Slide)
                {

                    FailSustained(activeNote);

                }
                else
                {

                    FailAndRemove(activeNote);

                }

                return;

            }

            if (motionResult != FlickMotionResult.Success)
            {

                return;

            }

            AwardTapEquivalent(activeNote, rewardGrade, activeNote.Note.EndLaneIndex);
            RemoveActiveNote(activeNote);

        }

        private void InitializeTerminalFlick(ActiveNote activeNote)
        {

            int startLane = activeNote.Note.SlideNodes.Count > 1
                ? activeNote.Note.SlideNodes[^2].LaneIndex
                : activeNote.Note.LaneIndex;
            activeNote.TerminalFlickReady = true;
            activeNote.FlickStartNormalizedX = PlayfieldGeometry.GetLaneCenterNormalized(
                startLane,
                chart.LaneCount);

        }

        private bool TryClaimEligibleContact(
            ActiveNote activeNote,
            float expectedX,
            double checkTime)
        {

            ContactState contact = FindEligibleContact(activeNote, expectedX, checkTime);

            if (contact == null)
            {

                return false;

            }

            if (contact.IsDown)
            {

                ClaimContact(activeNote, contact);

            }

            return true;

        }

        private ContactState FindEligibleContact(
            ActiveNote activeNote,
            float expectedX,
            double checkTime)
        {

            float tolerance = activeNote.Note.NoteType == ChartNoteType.Hold
                ? 0.49f
                : judgementSettings.SlideToleranceInLaneWidths;
            ContactState best = null;
            float bestDistance = float.PositiveInfinity;

            foreach (ContactState contact in contacts.Values)
            {

                if ((contact.Owner != null && contact.Owner != activeNote) ||
                    !contact.TryGetNormalizedX(checkTime, out float sampledX))
                {

                    continue;

                }

                float distance = Mathf.Abs(sampledX - expectedX);

                if (!NotePathMath.IsInsideLaneCorridor(
                        sampledX,
                        expectedX,
                        chart.LaneCount,
                        tolerance) ||
                    distance >= bestDistance)
                {

                    continue;

                }

                best = contact;
                bestDistance = distance;

            }

            return best;

        }

        private bool IsPrimaryContactInLane(
            ActiveNote activeNote,
            int laneIndex,
            double checkTime)
        {

            if (!activeNote.PrimaryContactId.HasValue ||
                !contacts.TryGetValue(activeNote.PrimaryContactId.Value, out ContactState contact) ||
                !contact.TryGetNormalizedX(checkTime, out float sampledX))
            {

                return false;

            }

            return PlayfieldGeometry.GetLaneIndex(sampledX, chart.LaneCount) == laneIndex;

        }

        private void ClaimContact(ActiveNote activeNote, ContactState contact)
        {

            if (contact.Owner != null && contact.Owner != activeNote)
            {

                return;

            }

            if (activeNote.PrimaryContactId.HasValue &&
                activeNote.PrimaryContactId.Value != contact.Id &&
                contacts.TryGetValue(activeNote.PrimaryContactId.Value, out ContactState previous) &&
                previous.Owner == activeNote)
            {

                previous.Owner = null;

            }

            contact.Owner = activeNote;
            activeNote.PrimaryContactId = contact.Id;

        }

        private void ReleaseContact(ContactState contact)
        {

            ActiveNote owner = contact.Owner;
            contact.Owner = null;

            if (owner != null && owner.PrimaryContactId == contact.Id)
            {

                owner.PrimaryContactId = null;

            }

        }

        private void ReleaseOwnedContacts(ActiveNote activeNote)
        {

            foreach (ContactState contact in contacts.Values)
            {

                if (contact.Owner == activeNote)
                {

                    contact.Owner = null;

                }

            }

            activeNote.PrimaryContactId = null;

        }

        private static double GetTerminalFlickStartTime(ChartNote note)
        {

            return note.SlideNodes.Count > 1
                ? note.SlideNodes[^2].Time
                : note.HitTime;

        }

        private void FailSustained(ActiveNote activeNote)
        {

            if (activeNote.HasFailed)
            {

                return;

            }

            activeNote.HasFailed = true;
            activeNote.View.SetFailed();
            ReleaseOwnedContacts(activeNote);
            ShowMiss();

        }

        private void FailAndRemove(ActiveNote activeNote)
        {

            ShowMiss();
            RemoveActiveNote(activeNote);

        }

        private void AwardTapEquivalent(
            ActiveNote activeNote,
            JudgementGrade grade,
            int laneIndex,
            bool playFeedback = true)
        {

            float normalizedX = PlayfieldGeometry.GetLaneCenterNormalized(laneIndex, chart.LaneCount);
            AwardTapEquivalentAtNormalizedX(activeNote, grade, normalizedX, playFeedback);

        }

        private void AwardTapEquivalentAtNormalizedX(
            ActiveNote activeNote,
            JudgementGrade grade,
            float normalizedX,
            bool playFeedback)
        {

            combo++;
            score += grade == JudgementGrade.Perfect ? PerfectTapScore : GoodTapScore;
            hud.SetScore(score);
            hud.ShowCombo(combo);
            hud.ShowJudgement(grade);
            Color partColor = chart.GetPartColor(activeNote.Note.MusicalPartId);
            hud.ShowInstrument(
                chart.GetPartDisplayName(activeNote.Note.MusicalPartId),
                partColor);

            if (playFeedback)
            {

                presenter.PlayHitFeedbackAtNormalizedX(
                    activeNote.Note.Id,
                    normalizedX,
                    partColor,
                    grade);

            }

        }

        private void ShowMiss()
        {

            combo = 0;
            hud.ShowCombo(combo);
            hud.ShowJudgement(JudgementGrade.Miss);

        }

        private void RemoveActiveNote(ActiveNote activeNote)
        {

            int index = activeNotes.IndexOf(activeNote);

            if (index >= 0)
            {

                RemoveActiveNoteAt(index);

            }

        }

        private void RemoveActiveNoteAt(int index)
        {

            ActiveNote activeNote = activeNotes[index];
            ReleaseOwnedContacts(activeNote);
            activeNote.View.Remove();
            activeNotes.RemoveAt(index);

        }

        private double ConvertInputTimestamp(double eventTimestamp)
        {

            return songPlayback.GetSongTimeForExternalTimestamp(
                eventTimestamp,
                InputState.currentTime);

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
                    beatTime + ScheduleTolerance);

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

                ClearContactsAndLanePresses();
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

            if (activeLanePressCounts[laneIndex] == 1 && presenter != null)
            {

                presenter.SetLanePressed(laneIndex, true);

            }

        }

        private void RemoveLanePress(int laneIndex)
        {

            if (activeLanePressCounts == null || activeLanePressCounts[laneIndex] <= 0)
            {

                return;

            }

            activeLanePressCounts[laneIndex]--;

            if (activeLanePressCounts[laneIndex] == 0 && presenter != null)
            {

                presenter.SetLanePressed(laneIndex, false);

            }

        }

        private void ClearContactsAndLanePresses()
        {

            contacts.Clear();
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
