# Idiot_Tape — Architecture

> Status: Current
> Last reviewed: 2026-09-05
> Applies to: The current Unity prototype
> Authority: Runtime ownership, dependency, and data-flow contracts

## Scope and Data Flow

This document owns responsibility boundaries, not a list of classes to create. Inspect the
current owners below before adding a system. Gameplay state flows from chart data and the
FMOD timeline through session scheduling, timestamped input, and judgement to score and
presentation. Visuals observe that state; they never own musical timing.

## Current Implementation Map

This section maps the conceptual responsibilities in this document to the current repository.
It is descriptive, not permission to preserve an implementation that violates a documented
contract.

| Responsibility | Current implementation | Owned state or output |
|---|---|---|
| FMOD playback and authoritative song time | `FmodSongPlayback` | FMOD event instance, shared DSP/timeline anchor, nonnegative song time and signed preparation time, pause and seek state, stem volumes |
| Timeline calculations | `SongTimelineMath` | Pure DSP-clock-to-song-time calculations, including the signed view before a future anchor |
| Preparation planning | `GameplayStartPlan` | Pure tempo-derived bar duration, minimum first-note approach, negative beat grid, and countdown calculation |
| Beat-click scheduling | `FmodMetronome` | FMOD DSP-scheduled click channels shared by gameplay and the Editor authoring wrapper |
| Session orchestration | `GameplaySession` | Preparing/Ready/CountIn/Playing phase, preparation settings, active-note and timing-guide collections, scheduler indices, timestamped contact/ownership histories, per-note interaction cursors, score, combo, input-to-judgement flow |
| Chart definition and validation | `PrototypeChart` | Song event path, stem mappings, tempo sections, lane count, parts, activation windows, notes |
| Tempo navigation math | `ChartTempoMap` | Bar, beat, and song-time conversion for authoring and runtime timing guides |
| Interaction geometry and math | `NotePathMath`, `NoteInteractionMath` | Step geometry, banana curves, transition windows, hold grace, flick motion, bonus calculations |
| Input collection | `GameplayInputRouter` | Touch, mouse, and development keyboard events with timestamps |
| Judgement calculation | `JudgementEvaluator` | Pure candidate selection and judgement result |
| Playfield presentation | `PlayfieldPresenter` and focused view classes | Note and timing-guide visuals, lane feedback, hit effects, judgement-line reaction |
| HUD presentation | `GameplayHud` | Ready/start controls, preparation choice, note speed, countdown, score, combo, judgement, instrument, progress, and pause display |
| Chart authoring | `PrototypeChartRecorderWindow` partials (`.cs`, `.Workspace.cs`, `.Canvas.cs`, `.Inspector.cs`) and Editor utilities | Workspace, recording drafts, selection/path editing, navigation, quantization, calibration, transactional apply, Undo, validation, save |

### Current Assembly Boundaries

| Assembly | Current responsibility | Current dependency direction |
|---|---|---|
| `IdiotTape.Audio` | FMOD playback, timeline math, and shared DSP metronome | FMOD Unity integration |
| `IdiotTape.Gameplay` | Chart data, session, input, judgement, and presentation | Audio, Input System, uGUI |
| `IdiotTape.Audio.Editor` | FMOD playback-test scene construction | Audio, FMOD Unity integration and Editor APIs |
| `IdiotTape.Gameplay.Editor` | Chart-authoring and prototype setup tools | Gameplay, Audio, Editor-facing Unity and FMOD APIs |
| `IdiotTape.Gameplay.Tests` | EditMode tests for isolated gameplay, timing, and authoring logic | Gameplay, Audio, Editor tool assembly |
| `IdiotTape.Gameplay.PlayModeTests` | Scene/view, start-flow, authoring playback, and scheduled FMOD playback tests | Gameplay, Audio, FMOD Unity integration, Input System, uGUI |

Runtime assemblies must not acquire a dependency on the Editor assembly. Gameplay may read the
public audio timeline contract, but it must not duplicate FMOD timing ownership.

### Current Session Lifecycle

The session owns an explicit `SessionPhase` for the shared start flow:

```text
Preparing: validate chart and prepare the chart-selected FMOD event
  -> Ready: show start/settings controls and wait for the player
  -> CountIn: schedule music and beat clicks, present notes from signed TimelineTime
  -> Playing: schedule, present, and judge notes after the DSP start is reached
       <-> existing FMOD pause/resume state
  -> continue until playback ends
```

Ready does not automatically play music or advance note scheduling. Start requests arrive through
the HUD button or the input router's Enter/Space action. `GameplayStartPlan` consumes the selected
chart's first tempo section, earliest visible note, selected visual lead, and session preparation
settings. The session schedules the audio and shared `FmodMetronome` against one FMOD DSP anchor;
the plan itself owns no clock, audio objects, or chart mutation.

The playback component owns native scheduling lead, Core-gate/master-clock conversion, and
nonzero scheduled-target ordering. Callers use the returned actual anchor. These details and
the ordinary Play/Restart phase-compatibility contract are defined in
[RHYTHM_SYSTEM.md](RHYTHM_SYSTEM.md#gameplay-preparation-and-first-note-approach).
Scheduled-start verification does not close the separate live-seek finding in
[BACKLOG.md](BACKLOG.md#it-p0-003--verify-pause-resume-restart-and-seek-boundaries).

CountIn permits note presentation while suppressing judgement, Miss processing, and gameplay
contacts. The HUD locks preparation controls until this phase ends. On the transition to Playing,
the session discards preparation contacts. Pause/cancel during CountIn cancels both scheduled
music and clicks and returns to Ready; repeated start/restart requests cannot create overlapping
attempts. Pause while Playing continues to use the playback component's existing pause state and
does not introduce another session phase.

Restart clears active note and timing-guide views, candidates, scheduler positions, score, combo,
progress, contact ownership, and lane-press presentation before scheduling a fresh CountIn. The
same flow applies to other charts without embedded song identities, BPM values, or fixed seconds
of delay. Missing tempo data uses explicit session fallback configuration.

The current implementation does not expose a distinct end-of-song or results state. The current
prototype milestone treats that as a small missing lifecycle boundary, not as permission to invent
final scoring, failure, reward, or progression systems.

### Current Failure Behavior

- invalid chart data logs a contextual error and disables the gameplay session
- FMOD preparation failure or timeout logs the selected event path and disables the gameplay session
- unsupported or invalid input is rejected before judgement
- chart authoring disables the live gameplay session when taking playback ownership (including event preparation, audition, seeking, and recording) to prevent stale scheduler state

New failure handling should remain explicit and testable. Do not hide essential data or playback
failures by silently substituting unrelated charts, songs, or clocks.

## Interaction Ownership

This section defines responsibility boundaries for the implemented prototype interactions in
`NOTE_INTERACTIONS.md`. It does not require creating one class per bullet.

### Shared Interaction State

Tap, hold, slide, flick, and banana should share focused timing, input, and result concepts where
their accepted behavior actually overlaps. They must not be forced through one generic framework
that hides interaction-specific rules.

`GameplaySession.ActiveNote` owns per-interaction state, including:

- the chart interaction identity and resolved timing events
- its active, completed, or failed lifecycle
- the established start grade for hold and slide
- the next unprocessed check, reward, or authored-node index
- eligible contact ownership or slide handoff state
- banana checkpoint success and pending bonus charge

That state must not own the song clock, global score, chart loading, or unrelated active notes.

### Timestamped Contact State

`GameplayInputRouter` emits timestamped events; `GameplaySession.ContactState` retains samples
and ownership changes. That history must preserve enough press, move, and release information to evaluate
path state across required musical checks, including when one render frame crosses multiple checks.

Contact ownership belongs to gameplay interaction state, not presentation. One contact may own at
most one sustained interaction. Slide may transfer between eligible contacts; that exception must
not become implicit global handoff behavior for hold.

### Interaction Evaluation

Judgement responsibilities expand beyond one tap candidate while retaining focused, testable
logic:

- tap start and banana endpoints compare lane and timestamp
- hold checks lane continuity on resolved musical times
- slide checks the held lane and timestamped arrival at each authored lane-change node, with the
  bounded transition allowance defined in `RHYTHM_SYSTEM.md`
- flick checks timestamped direction, distance, speed, and end-lane entry
- banana checks a normalized curve corridor at explicit checkpoints

Evaluation must emit discrete outcomes and reward events. Presentation objects must not decide
whether a checkpoint, tick, node, or gesture succeeded.

Slide rendering derives step corners from adjacent chart nodes. Those corners do not become
gameplay events, and the judgement transition allowance must not turn the displayed hold body into
an interpolated diagonal. Required check times still come from the authoritative song timeline.

### Score and Combo Aggregation

The session-level score/combo owner consumes interaction results, including inherited-grade hold
and slide events and banana bonus settlement. Individual view objects must not mutate global score
or combo.

The accepted prototype reward units do not finalize a production scoring service or results
architecture. Implement the smallest owner that prevents formulas from being duplicated across
note views.

### Failure Presentation

Hold and slide failure state remains gameplay state until their authored end time. Presentation
observes that state to render the remaining object black and suppress further hit feedback. The
black material or tint is not itself the authoritative termination flag.

Banana fill and flick direction visuals similarly observe resolved interaction state and chart
data without becoming judgement inputs.

### Authoring Boundary

Live recording may create temporary interaction drafts that are not yet valid chart data. For
example, an open slide is discarded when recording stops. Only completed, validated interactions
may cross the apply boundary into the chart asset.

The Editor assembly owns recording modes, node/curve manipulation, checkpoint generation, Undo,
and save flow. Runtime assemblies consume the applied chart representation and must not depend on
Editor-only draft types.

## Responsibility and Dependency Rules

- Session orchestration coordinates playback, scheduling, input, results, and lifecycle. The
  current session also evaluates sustained interactions; a separate scheduler or score service
  is not required merely to match a conceptual diagram.
- `FmodSongPlayback` owns the clock, playback, and timestamp conversion. Gameplay must not
  introduce a parallel Unity DSP clock or duplicate conversion/latency logic in note components.
- Chart definitions are persistent data; runtime interaction state has a session lifetime.
  ScriptableObjects must not become implicit mutable global session state.
- Scheduling uses ordered chart data and activates/retires relevant notes. It does not redefine
  hit times. Views own presentation, not chart loading, global input, score, or other notes.
- Judgement operates on chart/input data, not transforms. Results feed the session's score/combo
  owner. Presentation may animate from rhythm state but cannot change it.
- Runtime assemblies must not depend on Editor assemblies or draft authoring types. Use explicit
  dependencies; avoid circular event chains, globals, default singletons, and repeated scene
  searches in hot paths. Use events for discrete outcomes or simple polling when appropriate.
- Before adding an owner, identify its state, callers, lifetime, and exclusions. Prefer evolving
  the existing owner over a parallel implementation or speculative dependency-injection system.
- Pooling and containers are performance choices, not timing rules. They must preserve hit times,
  judgement, and chart semantics; introduce them only for a measured or clear scaling need.
- Fail clearly on invalid essential data or playback. Errors should identify chart/song,
  note/event, and field where applicable; do not silently substitute another chart or clock.
- Preserve scene references and serialized values. For an intentional ownership change, inspect
  dependencies and migration impact, implement the smallest coherent transition, verify affected
  behavior, and update this contract. See [DEVELOPMENT_WORKFLOW.md](DEVELOPMENT_WORKFLOW.md).

## Currently Unresolved Architecture

The following are not yet mandatory architectural decisions:

| ID | Open decision | Status |
|---|---|---|
| `AR-OPEN-001` | Long-term class names beyond current responsibility ownership | Open |
| `AR-OPEN-002` | Final scene structure | Open |
| `AR-OPEN-003` | Final assembly definition structure | Open |
| `AR-OPEN-004` | Final event and notification approach | Open |
| `AR-OPEN-005` | Whether and when runtime note pooling is required | Open |
| `AR-OPEN-006` | Final chart loading and preprocessing boundary | Open |
| `AR-OPEN-007` | Whether dependency-injection infrastructure is warranted | Open |
| `AR-OPEN-008` | Final save system | Open |
| `AR-OPEN-009` | Final song database and selection ownership | Open |

Codex must not create these systems simply because they appear as unresolved items.
