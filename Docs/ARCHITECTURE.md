# Idiot_Tape — Architecture

> Status: Current
> Last reviewed: 2026-09-04
> Applies to: The current Unity prototype
> Authority: Runtime ownership, dependency, and data-flow contracts

## Document Purpose

This document describes intended high-level system boundaries for Idiot_Tape.

It is not a requirement to immediately create every system named here.

The repository's current implementation must be inspected before introducing new classes.

The purpose of this document is to define responsibilities and prevent incompatible parallel
systems from growing over time.


## Architectural Goals

The architecture should support:

- accurate rhythm timing
- unconventional chart layouts
- rapid chart iteration
- long songs
- mobile performance
- clear ownership of gameplay state
- testable timing and judgement behavior


## Main Data Flow

The intended conceptual flow is:

```text
Song / Chart Data
        |
        v
Gameplay Session
        |
        +----> Authoritative Song Clock
        |
        +----> Chart Scheduling
        |          |
        |          v
        |      Runtime Notes
        |
        +----> Input
                   |
                   v
              Judgement
                   |
                   v
            Gameplay Result
```

Visual systems observe gameplay state.

Visual systems must not become the authoritative source of musical timing.


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
| Session orchestration | `GameplaySession` | Preparing/Ready/CountIn/Playing phase, preparation settings, active-note and timing-guide collections, scheduler indices, score, combo, input-to-judgement flow |
| Chart definition and validation | `PrototypeChart` | Song event path, stem mappings, tempo sections, lane count, parts, activation windows, notes |
| Tempo navigation math | `ChartTempoMap` | Bar, beat, and song-time conversion for authoring and runtime timing guides |
| Input collection | `GameplayInputRouter` | Touch, mouse, and development keyboard events with timestamps |
| Judgement calculation | `JudgementEvaluator` | Pure candidate selection and judgement result |
| Playfield presentation | `PlayfieldPresenter` and focused view classes | Note and timing-guide visuals, lane feedback, hit effects, judgement-line reaction |
| HUD presentation | `GameplayHud` | Ready/start controls, preparation choice, note speed, countdown, score, combo, judgement, instrument, progress, and pause display |
| Chart authoring | `PrototypeChartRecorderWindow` and Editor utilities | Play Mode recording, navigation, quantization, tempo calibration, apply, Undo, validation, save |

### Current Assembly Boundaries

| Assembly | Current responsibility | Current dependency direction |
|---|---|---|
| `IdiotTape.Audio` | FMOD playback, timeline math, and shared DSP metronome | FMOD Unity integration |
| `IdiotTape.Gameplay` | Chart data, session, input, judgement, and presentation | Audio, Input System, uGUI |
| `IdiotTape.Gameplay.Editor` | Chart-authoring and prototype setup tools | Gameplay, Audio, Editor-facing Unity and FMOD APIs |
| `IdiotTape.Gameplay.Tests` | EditMode tests for isolated gameplay, timing, and authoring logic | Gameplay, Audio, Editor tool assembly |
| `IdiotTape.Gameplay.PlayModeTests` | Gameplay-scene and view smoke tests | Gameplay, Audio, Input System, uGUI |

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

`FmodSongPlayback` owns the native event scheduling budget and preserves the playback/recording
timebase used by ordinary Play/Restart. It restores the default event scheduling property and
places the timeline anchor at the Core gate, without subtracting native startup delay from only
the scheduled path. Simultaneous parent/master clock readings translate that gate into the
returned master-clock anchor. Streaming/update and DSP-buffer settings determine minimum lead:
one native preparation budget plus buffer/update lead. Very short requests may be extended;
session and authoring callers use the actual returned anchor and signed timeline. For a nonzero
scheduled target, the event starts paused before its timeline position is set, preventing Studio's
later unpause from replacing the installed Core gate. These FMOD details do not belong in chart
data, HUD logic, or `GameplayStartPlan`. Preserving this timebase does not assert zero hardware
latency or close the separate live-seek verification finding.

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
- chart authoring disables the live gameplay session while recording to prevent stale scheduler state

New failure handling should remain explicit and testable. Do not hide essential data or playback
failures by silently substituting unrelated charts, songs, or clocks.


## Accepted Interaction Expansion Boundaries

This section defines responsibility boundaries for the implemented prototype interactions in
`NOTE_INTERACTIONS.md`. It does not require creating one class per bullet.

### Shared Interaction State

Tap, hold, slide, flick, and banana should share focused timing, input, and result concepts where
their accepted behavior actually overlaps. They must not be forced through one generic framework
that hides interaction-specific rules.

Runtime interaction state may need to own:

- the chart interaction identity and resolved timing events
- its active, completed, or failed lifecycle
- the established start grade for hold and slide
- the next unprocessed check, reward, or authored-node index
- eligible contact ownership or slide handoff state
- banana checkpoint success and pending bonus charge

That state must not own the song clock, global score, chart loading, or unrelated active notes.

### Timestamped Contact State

Input collection must retain enough timestamped press, move, and release information to evaluate
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


## Core Responsibility Boundaries

### Gameplay Session

The gameplay session coordinates the current play session.

Possible responsibilities:

- selecting the current song / chart
- starting gameplay
- restarting gameplay
- coordinating pause / resume
- owning session-level state
- coordinating completion

It should not personally implement every subsystem.


### Song Clock

The song clock owns the authoritative rhythm timeline.

Responsibilities include:

- current song time
- timing origin
- pause / resume synchronization
- restart synchronization
- conversion needed by judgement timing

There must not be multiple independent authoritative song clocks.

For the current FMOD prototype, the FMOD playback component owns both event playback and the
DSP-backed song timeline. The gameplay session reads that component for scheduling, presentation,
input timestamp conversion, pause, resume, and restart. It must not run a parallel Unity
`AudioSettings.dspTime` gameplay clock while FMOD is playing the song.


### Chart Data

Chart data describes what should happen and when.

It must not depend on live scene GameObjects.

See `CHART_FORMAT.md`.


### Chart Scheduler

The scheduler determines which chart objects are currently relevant.

Possible responsibilities include:

- advancing through ordered chart data
- activating notes before their hit time
- retiring notes after they are no longer relevant

The scheduler does not redefine note timing.


### Runtime Note

A runtime note represents a currently active gameplay object.

Its responsibilities should remain focused.

A runtime note may:

- display itself
- expose its chart identity
- represent its current interaction state
- react to judgement results

It should not become responsible for:

- global song time
- loading charts
- global scoring
- managing every other note
- global input routing


### Input

Input code captures player input and exposes it to gameplay.

Input handling should not duplicate rhythm-clock calculations throughout multiple components.

Timestamp conversion or calibration logic should remain centralized where possible.


### Judgement

Judgement determines how input relates to chart notes.

Judgement should primarily operate on data such as:

- note timing
- current relevant notes
- input timing
- judgement windows

It should not depend on visual note position as the authoritative timing value.


### Presentation

Presentation includes:

- note visuals
- judgement effects
- UI animation
- background effects
- chart visual transitions

Presentation may use rhythm state.

Presentation must not modify the authoritative rhythm timeline merely to make an effect work.


### Score / Combo / Results

Scoring is downstream from judgement.

Judgement should emit or expose an outcome that score/result systems can consume.

Avoid embedding scoring formulas independently into individual note objects.


## Data Ownership

Every important piece of state should have one clear owner.

Examples:

```text
song time             -> timing system
chart definition      -> chart data
active note state     -> gameplay/runtime note system
judgement rules       -> judgement system
score                  -> score/result system
```

Avoid two systems independently storing mutable copies of the same authoritative state.


## Dependency Direction

Prefer dependencies that flow from orchestration toward focused systems.

Avoid circular dependencies such as:

```text
Note -> GameManager -> JudgementManager -> Note -> GameManager
```

Do not use static globals merely to avoid defining dependencies clearly.


## Managers

A class should not be named or designed as a `Manager` simply because its responsibility
has not been decided.

Before creating a new manager:

1. search for an existing owner of the responsibility
2. define what state the new class owns
3. define who calls it
4. define what it must not own

Avoid a single giant `GameManager` that accumulates unrelated responsibilities.


## Singletons

Do not use singletons by default.

A singleton may be appropriate only when the lifetime and global ownership semantics genuinely
match the system.

Do not use a singleton merely because Inspector references are inconvenient.


## Scene References

Prefer explicit serialized or runtime dependencies over expensive global searches.

Avoid repeated use of:

- `FindObjectOfType`
- `FindFirstObjectByType`
- object name searches

in gameplay hot paths.

Do not introduce complex dependency-injection infrastructure solely to eliminate Inspector
references.


## ScriptableObjects

ScriptableObjects may be useful for static project data.

Do not use ScriptableObjects as implicit mutable global runtime state without intentionally
defining those semantics.

Runtime session state should have a clear lifecycle.


## Scenes

Scene structure is not yet finalized.

Do not reorganize existing scenes solely to match this document.

When modifying a scene:

- preserve unrelated serialized references
- avoid unnecessary hierarchy changes
- verify missing references afterward


## Editor Tooling

Chart authoring tools should remain separated from runtime gameplay where practical.

Editor-only code should not accidentally become a runtime dependency.

As editor tooling grows, prefer keeping Unity Editor-specific code behind an Editor assembly
or otherwise excluded from runtime builds.


## Runtime Allocation

Systems that scale with note count should avoid unnecessary garbage generation.

Particularly inspect:

- scheduler loops
- note updates
- judgement
- input processing

Do not introduce complex custom containers without an actual need.


## Object Pooling

Object pooling is an optimization strategy, not a timing system.

If runtime notes are frequently created and destroyed, pooling may become appropriate.

Adding or removing pooling must not change:

- note hit time
- judgement behavior
- chart semantics


## Events and Polling

Use events when they represent discrete state changes naturally.

Examples:

- judgement occurred
- song started
- song ended
- pause state changed

Do not create event chains so indirect that state ownership becomes difficult to understand.

Polling is acceptable when it is simpler and inexpensive.

Choose based on responsibility rather than ideology.


## Error Handling

Fail clearly when essential gameplay data is invalid.

Do not silently continue with obviously invalid timing or chart references if doing so would
produce misleading gameplay.

Development-time validation should provide enough context to identify:

- affected song
- affected chart
- affected note or event
- invalid field


## Architecture Change Rule

Do not rewrite working architecture solely to match the conceptual names in this document.

When existing code already fulfills the responsibility correctly, preserve and evolve it.

When intentionally changing a core responsibility:

1. inspect current dependencies
2. determine migration impact
3. implement the smallest coherent transition
4. verify affected gameplay
5. update this document if the architectural contract changed


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
