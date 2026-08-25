# Idiot_Tape — Current Prototype Milestone

> Status: Current
> Last reviewed: 2026-08-25
> Applies to: The current full-song mobile rhythm prototype
> Authority: Current validation scope and exit criteria

## Purpose

This document defines what the current prototype must prove before the project expands its feature
scope. It converts the questions in `GAME_DESIGN.md` into observable validation gates.

This document does not finalize open design decisions such as the production scoring formula,
final note types, or the permanent chart format.

## Milestone Statement

Prove that one complete song can be authored, played, judged, paused, and restarted using the
authoritative FMOD-backed song timeline, while the visual chart communicates musical-part
structure clearly enough to justify further development on physical mobile hardware.

## Current Implementation Baseline

The repository currently contains:

- FMOD event playback with a DSP-sample-backed authoritative song time
- chart-selected FMOD event paths and optional stem parameter mappings
- ScriptableObject prototype charts with tempo sections, musical parts, activation windows, and notes
- chart-driven lane count for runtime presentation and touch judgement
- touch, mouse, and eight-position keyboard development input
- time-and-horizontal-position judgement for the current tap-style prototype
- score, combo, judgement, instrument, progress, and pause presentation
- look-ahead note activation and song-time-derived note positions
- Play Mode chart authoring with looping, quantization, part activation, and tempo calibration tools
- EditMode tests for timing math, judgement, geometry, tempo, quantization, and chart utilities
- PlayMode smoke tests for the gameplay scene and lane feedback

Existence in the repository is not proof that the behavior has passed the milestone gates below.

## Validation Hypotheses

| ID | Hypothesis | Required evidence |
|---|---|---|
| `H-RHYTHM-001` | Judgement and note presentation remain synchronized for a complete song. | Beginning, middle, and end observations plus automated timing checks |
| `H-INPUT-001` | Touch input feels immediate and produces explainable early/late results on a physical mobile device. | Device, audio route, build, observations, and any measured offset recorded |
| `H-READ-001` | Players can read changing spatial patterns without losing the relationship to the music. | Playtest notes covering anticipation, miss causes, and recurring patterns |
| `H-PART-001` | Musical-part activation creates recognizable gameplay structure rather than color-only decoration. | Player explanation of heard and seen part changes after play |
| `H-AUTHOR-001` | A chart author can correct and iterate a representative section without excessive workflow cost. | Timed authoring session and friction log |
| `H-MOBILE-001` | The prototype remains technically feasible on representative mobile hardware. | Physical-device build, frame behavior, input, audio, and Console/log evidence |

Numeric thresholds that are not yet established must be marked as provisional in the relevant
playtest record. They must not be silently converted into permanent design rules.

## P0 Scope

The current milestone includes:

- one representative full-song chart
- authoritative playback, scheduling, presentation, input conversion, and judgement
- current tap-style interaction using chart-driven horizontal positions
- overlapping musical-part activation windows and inactive-note presentation
- score, combo, miss, progress, pause, restart, and a clear end-of-song state
- authoring, validation, saving, and replaying the chart
- automated checks for deterministic timing and isolated chart logic
- Play Mode verification of the complete gameplay path
- at least one representative physical mobile-device validation pass
- documented playtest evidence and a written milestone decision

## P1 Scope

P1 work may start only when it directly improves a failed P0 hypothesis or after the P0 gates pass.

- anticipation UI for upcoming musical-part changes
- improved multi-selection and drag editing in the chart authoring tool
- variable-tempo authoring UX beyond direct tempo-section data
- user-facing calibration UI
- additional reversible spatial-layout experiments
- additional note interactions whose gameplay purpose is documented

## Explicitly Outside This Milestone

- accounts, online services, multiplayer, leaderboards, and anti-cheat
- monetization, gacha, character collection, and progression frameworks
- a production song-selection or downloadable-content pipeline
- final scoring, health, failure, difficulty naming, or note-type taxonomies
- a permanent external chart serialization format
- final art, broad content production, or a generic UI framework
- production analytics infrastructure

## Playable Validation Flow

```text
Open the gameplay prototype with a selected chart
  -> validate chart data and prepare its FMOD event
  -> begin or restart from one synchronized timeline origin
  -> present upcoming notes from chart time
  -> receive touch or development input with an explicit timestamp
  -> judge playable notes and update feedback
  -> pause and resume without changing chart timing
  -> reach a clear end-of-song state
  -> inspect the result and restart without stale runtime state
```

The current implementation does not yet expose a distinct end-of-song results state. Completing
that state is a P0 backlog item, but its presentation must remain minimal until scoring and failure
rules are intentionally finalized.

## Milestone Gates

### Gate M1 — Timing Baseline

- relevant EditMode and PlayMode tests pass
- no second authoritative song clock is present
- restart, pause, resume, and input timestamp conversion follow `RHYTHM_SYSTEM.md`
- a frame hitch changes rendering smoothness but does not leave permanent note-position drift

### Gate M2 — Complete Song

- the representative chart can be played from beginning to end
- the beginning, middle, and end are inspected for synchronization
- the session reaches an explicit completion state
- restart clears consumed notes, active notes, score, combo, progress, and input presentation
- no new Console errors or invalid serialized references appear

### Gate M3 — Mobile Feel and Readability

- the current build runs on at least one representative physical mobile device
- the device, display mode, audio route, and build are recorded
- touch timing and visible feedback are evaluated on the device
- playtest evidence covers spatial readability and musical-part recognition
- misses can be explained as timing, position, chart reading, or a recorded defect

### Gate M4 — Authoring Throughput

- a representative dense or musically layered section is edited through the normal tool workflow
- elapsed authoring and iteration time is recorded
- the author records the most expensive repeated steps
- saved data validates and can be replayed without manual scene-note placement
- the next authoring investment is selected from evidence rather than feature count

## Milestone Exit Criteria

The milestone is complete only when:

1. gates M1 through M4 have evidence in `Playtests/`
2. P0 backlog items are complete or intentionally removed with a recorded reason
3. relevant automated tests and Play Mode checks have actually been run
4. physical-device limitations are recorded rather than inferred from Editor behavior
5. unresolved defects are classified as blocker, follow-up, or accepted prototype limitation
6. a milestone decision is recorded as one of:
   - continue with the core concept
   - revise the music-to-chart interaction and repeat selected gates
   - stop or substantially redirect the current concept

## Evidence Location

- executable work: `BACKLOG.md`
- manual sessions: `Playtests/`
- timing contract: `RHYTHM_SYSTEM.md`
- chart contract: `CHART_FORMAT.md`
- consequential technical decisions: `ADR/`

