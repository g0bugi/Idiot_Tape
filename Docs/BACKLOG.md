# Idiot_Tape — Prototype Backlog

> Status: Current
> Last reviewed: 2026-08-27
> Applies to: The milestone in `PROTOTYPE_MILESTONE.md`
> Authority: Work sequencing, scope, and acceptance evidence

## Purpose

This backlog converts the current prototype milestone into small, verifiable work items.

It is not a replacement for design or technical contracts. If a task conflicts with another
project document, resolve or report the conflict before implementation.

## Status Vocabulary

- `Ready`: scoped and available to start
- `In progress`: actively being changed
- `Implemented; verification pending`: code or data exists, but required evidence is incomplete
- `Blocked`: cannot proceed without a named decision or dependency
- `Done`: acceptance conditions passed and evidence was recorded
- `Deferred`: intentionally outside the current milestone

A checkbox or existing implementation alone is not sufficient evidence for `Done`.

## Operating Rules

- Finish P0 validation before unrelated P1 expansion.
- Prefer one observable behavior per work item.
- Preserve the authoritative timing and chart-data contracts.
- Record the exact automated and manual checks that were run.
- Link meaningful manual validation to a file under `Playtests/`.
- If work exposes a new design decision, add a stable open-decision ID to the owning document.
- If work changes an accepted technical decision, update or supersede the relevant ADR.

## Milestone Overview

| Gate | Work items | Exit condition |
|---|---|---|
| M1 Timing Baseline | `IT-P0-001` to `IT-P0-003` | Automated and manual timing evidence exists |
| M2 Complete Song | `IT-P0-004` to `IT-P0-005` | One complete song ends and restarts cleanly |
| M3 Mobile Feel and Readability | `IT-P0-006` to `IT-P0-008` | Physical-device and player evidence exists |
| M4 Authoring Throughput | `IT-P0-009` | A measured authoring iteration is recorded |
| M5 Interaction Set | `IT-P0-013` to `IT-P0-019` | Accepted notes are implemented and device-validated |
| Exit Decision | `IT-P0-010` | Continue, revise, or redirect is recorded |

## P0 Work

### IT-P0-001 — Establish the Verification Baseline

Status: `Done`

Goal:

- establish which current automated and manual checks pass before further milestone work

Includes:

- EditMode timing, judgement, geometry, tempo, quantization, and chart utility tests
- PlayMode gameplay-scene and lane-feedback smoke tests
- Unity compilation and Console inspection
- exact test commands or Unity Test Runner selections in the evidence record

Acceptance:

- all executed results are recorded
- failures are assigned a follow-up item or recorded as pre-existing
- no unexecuted check is reported as passing

Evidence:

- `Playtests/2026-08-26-verification-baseline.md`
- current baseline passes 64 EditMode tests and 4 PlayMode tests after resolving `IT-P0-011` and
  `IT-P0-012`

### IT-P0-002 — Verify Full-Song Timeline Stability

Status: `Implemented; verification pending`

Depends on: `IT-P0-001`

Goal:

- verify `H-RHYTHM-001` using the representative full-song chart

Includes:

- synchronization observations near the beginning, middle, and end
- frame-hitch recovery
- input timestamp conversion
- long-song note scheduling and retirement

Excludes:

- final calibration UX
- playback-rate modification

Acceptance:

- timing observations and reproduction conditions are recorded
- no persistent visual drift remains after a temporary hitch
- any measured or perceived offset is classified rather than hidden by an unexplained constant
- relevant automated tests pass

### IT-P0-003 — Verify Pause, Resume, Restart, and Seek Boundaries

Status: `Implemented; verification pending`

Depends on: `IT-P0-001`

Goal:

- prove that playback state changes recapture or rebuild timing state consistently

Includes:

- gameplay pause and resume
- gameplay restart
- explicit start, chart-tempo preparation, and full-distance first-note approach
- authoring seek and loop boundaries where they use the same playback component

Acceptance:

- audio, chart scheduling, active notes, score, combo, progress, and input presentation agree after each supported transition
- the test covers a transition from a later part of the song, not only the first seconds
- new Console errors are absent

Open verification finding (2026-09-04):

- the existing live `Seek` path can capture the previous cached FMOD timeline as its new anchor;
  a request near 293.134 seconds left the song clock near 6.027 seconds after waiting five seconds
- a temporary explicit-target anchor experiment removed that stale origin but still showed about
  149 ms of streaming-transition discrepancy; that experiment was reverted
- define and repair the seek/streaming transition contract in follow-up work; it is outside the
  explicit-start/count-in change and is not a regression introduced by that flow
- start-flow verification uses the changed `SchedulePlay` path at a later song position and does
  not establish that existing live seek or paused seek is correct

Start synchronization regression (2026-09-04): master-mix PCM comparison found that compensating
native startup delay only in the new scheduled path advanced music by approximately 149 ms
relative to ordinary Restart/authoring playback. The correction restores the shared timebase
without retiming charts or adding a song-specific offset. A separate nonzero scheduled-start
ordering correction prevents Studio unpause from replacing the future Core gate. Final PCM
comparison confirms the early-music regression is removed across five scheduled target/delay
conditions, with approximately 21.33 ms of residual phase difference from ordinary Restart.
The corrected complete suites passed 12/12 PlayMode and 268/268 EditMode; final chart/settings
preservation and diff review passed. Sampled PCM, UI, residual timing limits, and remaining
physical-device/full-song verification are recorded in
`Playtests/2026-09-04-start-sync-regression-verification.md`.

Historical evidence: `Playtests/2026-09-04-gameplay-start-flow-verification.md` records the earlier
268/268 EditMode and 11/11 PlayMode pass. Those checks did not compare actual output phase with
the established playback/recording timebase, so they do not establish synchronization acceptance
for the corrected implementation or close the separate live-seek and physical-device findings.

### IT-P0-004 — Add a Minimal End-of-Song State

Status: `Ready`

Depends on: `IT-P0-002`, `IT-P0-003`

Goal:

- make completion of the representative song explicit and safely restartable

Includes:

- detecting the end of the current song session
- stopping further judgement after completion
- presenting a minimal summary using current score, combo, and judgement data
- restarting without stale notes or input state

Excludes:

- final scoring formula
- health, failure, grade, reward, or progression systems
- production results-screen art

Acceptance:

- completing the song transitions exactly once
- late frames do not award duplicate completion
- restart begins from a clean state
- PlayMode coverage is added where the logic can be tested deterministically

### IT-P0-005 — Complete-Song Play Mode Pass

Status: `Ready`

Depends on: `IT-P0-004`

Goal:

- exercise the complete Editor/Play Mode validation flow for one representative chart

Acceptance:

- chart validation, FMOD preparation, play, judgement, pause, resume, completion, and restart are exercised
- the Unity Console is checked after the run
- affected serialized references are intact
- the result is recorded under `Playtests/`

### IT-P0-006 — Physical Mobile Build and Runtime Baseline

Status: `Ready`

Depends on: `IT-P0-001`

Goal:

- verify `H-MOBILE-001` on at least one representative physical mobile device

Acceptance:

- device, OS, display mode, build, and audio output route are recorded
- the gameplay scene starts and a complete representative section can be played
- frame behavior, audio continuity, input handling, and errors are recorded
- Editor behavior is not used as a substitute for device evidence

### IT-P0-007 — Touch Timing and Feedback Evaluation

Status: `Ready`

Depends on: `IT-P0-006`

Goal:

- evaluate `H-INPUT-001` on physical hardware

Acceptance:

- early and late behavior is exercised intentionally
- any perceived latency is recorded with the device and audio route
- visual feedback is checked against the input moment
- proposed offset changes state their sign and responsibility according to `RHYTHM_SYSTEM.md`

### IT-P0-008 — Musical-Part and Spatial Readability Playtest

Status: `Ready`

Depends on: `IT-P0-005`

Goal:

- evaluate `H-READ-001` and `H-PART-001`

Acceptance:

- the session includes overlapping and changing musical-part activation
- observations cover anticipation, recurring-pattern recognition, and miss causes
- the player is asked to describe the relationship between heard parts and visible structures
- findings distinguish color recognition from gameplay-structure recognition

### IT-P0-009 — Measure Chart Authoring Throughput

Status: `Ready`

Authoring workspace implementation: `Implemented; verification pending`. The throughput measurement
itself has not been completed. Automated workspace checks and sampled native-window visual inspection
passed on 2026-09-04; representative manual workflow validation remains pending.

Depends on: `IT-P0-001`

Goal:

- evaluate `H-AUTHOR-001` using the normal chart-authoring workflow

Current workflow under evaluation:

- top recording/loop/snap/save controls, left musical-part selection, and central `노트 편집` /
  `파트 개요` views
- right-hand `노트` / `파트` / `녹화` / `박자` / `도구` tabs for interaction editing and existing
  detailed operations, including full banana-curve and applied-note editing
- bottom temporary-record drawer and explicit `차트에 반영`, followed by separate asset saving
- at widths below 1000 pixels, `속성 열기` / `채보로 돌아가기` switches the center between chart and
  properties; the other working controls remain available

Acceptance:

- a representative layered or dense section is chosen before timing begins
- recording, correction, part assignment, activation, validation, saving, and replay are included
- total time and repeated friction points are recorded
- compare full-width and compact workflows for control reachability, time spent finding properties,
  unnecessary scrolling, and confusion between temporary records and unsaved chart edits
- the next tooling improvement is justified by observed cost

Evidence:

- `Playtests/2026-09-04-authoring-workspace-verification.md`: complete suites passed **188/188
  EditMode** and **7/7 PlayMode** on 2026-09-04
- 10 window tests and 16 canvas tests cover pane bounds, actual EditorWindow render smoke in both
  views and all five properties tabs, all five note types, compact/full-width layouts, preservation
  of chart and temporary-buffer state, applied-banana serialization/Undo, and path geometry
- native-window captures at 150% scaling were inspected in both chart views and compact chart /
  properties modes, including the expanded buffer; no timed authoring session is recorded, so this
  evidence does not establish `H-AUTHOR-001`
- follow-up stability verification passed **202/202 EditMode** and **8/8 PlayMode**, including actual
  view/inspector clicks, buffer Undo/Redo, banana boundary preservation, and stopped-audio Space
  playback; see `Playtests/2026-09-04-authoring-stability-verification.md`
- repeated hold recording, unresolved-part preservation, and validation before buffer application
  are covered by `Playtests/2026-09-04-recording-serialization-verification.md`

Verification pending:

- representative interactive use of every properties tab, both chart views, and the expanded drawer
- all-type recording/correction, scoped duplication/replacement, Undo, validation, explicit save,
  and re-entered Play Mode inspection
- measured throughput and readability; the implemented layout is not evidence of a speed improvement

### IT-P0-010 — Record the Prototype Decision

Status: `Ready`

Depends on: `IT-P0-002` through `IT-P0-009`, `IT-P0-013` through `IT-P0-019`

Goal:

- decide whether to continue, revise, or redirect the current concept

Acceptance:

- each hypothesis in `PROTOTYPE_MILESTONE.md` has an evidence-based outcome
- blockers and accepted limitations are separated
- the next milestone contains only work justified by the decision
- any newly finalized design or technical decisions are updated in their owning document or ADR

### IT-P0-011 — Make the Gameplay Smoke Test Target a Playable Note

Status: `Done`

Depends on: None

Goal:

- restore a chart-valid gameplay-scene smoke hit without relying on a stale hard-coded song position

Includes:

- selecting or deriving a note whose musical part is active at its hit time
- submitting the matching development-lane input inside its judgement window
- preserving the existing runtime-note, timing-guide, HUD, feedback, pause, and resume assertions

Acceptance:

- the smoke test produces one valid hit with the current representative chart
- changing inactive opening notes does not silently invalidate the target selection
- the complete PlayMode suite passes this test in batch mode

Evidence:

- failure recorded in `Playtests/2026-08-26-verification-baseline.md`
- updated smoke test passed alone, and the complete PlayMode suite passed this test on 2026-08-26

### IT-P0-012 — Make the Note-Speed Slider Maximum Reachable

Status: `Done`

Depends on: None

Goal:

- allow pointer or touch input at the slider's right endpoint to select the documented `x4` maximum

Includes:

- resolving the endpoint containment and coordinate-mapping mismatch
- preserving the `x1` minimum, continuous values, and immediate song-time-derived presentation update

Acceptance:

- the left and right slider endpoints map to `x1` and `x4`
- the current `NoteSpeedSliderTests` PlayMode test passes in batch mode
- note timing and judgement windows remain unchanged

Evidence:

- failure recorded in `Playtests/2026-08-26-verification-baseline.md`
- targeted slider verification passed, followed by 4/4 complete PlayMode and 64/64 complete
  EditMode results on 2026-08-26

## P1 Parking Lot

These items are intentionally unscheduled until P0 evidence justifies them.

- musical-part anticipation UI
- chart multi-selection and drag editing
- activation-window handle editing
- variable-tempo authoring workflow
- user-facing calibration UI
- additional spatial-layout representation
- production results and scoring rules
- song-selection and content-library flow

## P0 Note-Interaction Work

The work below is scoped from the accepted prototype contract in `NOTE_INTERACTIONS.md`. The user
intentionally moved it ahead of the remaining P0 validation because the tap-only baseline is not a
sufficiently representative fun/readability test. The accepted prototype reward rules do not
finalize production scoring.

Automated implementation evidence is recorded in
[`Playtests/2026-08-27-new-note-implementation.md`](Playtests/2026-08-27-new-note-implementation.md).

### IT-P0-013 — Add the Accepted Interaction Chart Contract

Status: `Implemented; verification pending`

Depends on: `IT-P0-001`

Player or author goal:

- store tap, hold, slide, flick, and banana interactions without losing existing tap charts

Includes:

- the smallest coherent serialized representation required by `CHART_FORMAT.md`
- explicit duration, ordered slide nodes, flick endpoints, banana curve/checkpoint data, and banana
  maximum bonus combo
- validation with affected interaction IDs and fields
- an intentional compatibility or migration path for current `ChartNote` data

Excludes:

- a permanent external chart format
- generic interaction composition or automatic note merging
- final production note taxonomy

Contract impact:

- Timing: resolved events must remain absolute and tempo-map-derived where required
- Chart data: accepted interaction target in `CHART_FORMAT.md`
- Serialized assets: current `PrototypeChart` assets require compatibility review and possibly an
  explicit migration

Acceptance:

- existing tap charts retain their IDs, times, lanes, parts, and playability
- each accepted interaction can be represented and invalid interaction data fails contextually
- same-lane/same-time overlaps are not rejected solely as collisions

Automated verification:

- EditMode serialization/validation and legacy-chart compatibility tests

Manual verification:

- inspect representative chart assets and Unity serialized references after migration

Evidence:

- 2026-08-27 complete EditMode suite: 83/83 passed
- legacy four-field tap compatibility, interaction validation, quantization preservation, and
  sustained-note pattern-copy coverage are automated
- representative serialized chart inspection remains manual verification

### IT-P0-014 — Add Deterministic Contact and Interaction Evaluation

Status: `Implemented; verification pending`

Depends on: `IT-P0-013`

Player or author goal:

- make sustained paths and flick gestures respond consistently without depending on render frame
  rate

Includes:

- timestamped contact press, move, and release history sufficient for crossed checks
- one-contact-to-one-sustained-interaction ownership
- global quarter-beat check and half-beat reward resolution through the tempo map
- ordered, exactly-once interaction outcomes and score/combo events
- restart, pause, resume, hitch, and termination state handling

Excludes:

- final gesture thresholds and production judgement windows
- speculative service frameworks

Contract impact:

- Timing: `RHYTHM_SYSTEM.md` accepted sustained and gesture timing
- Chart data: reads resolved interaction events from `IT-P0-013`
- Serialized assets: configurable thresholds may require inspected settings data

Acceptance:

- a frame crossing multiple checks processes every event once in chronological order
- no contact owns two sustained interactions
- failed interactions cannot emit later rewards or repeated Misses
- pause and restart clear ownership, cursors, and pending banana charge

Automated verification:

- EditMode tests for tempo-grid resolution, event ordering, ownership, hitch recovery, and reset

Manual verification:

- Play Mode input-state inspection at 30, 60, and 120 FPS targets where available

Evidence:

- timestamped press/move/release samples, ownership reset, global subdivision math, and crossed
  per-note event cursors are implemented
- 2026-08-27 complete EditMode suite: 83/83 passed
- frame-rate target inspection and representative multi-contact Play Mode evidence remain pending

### IT-P0-015 — Implement Hold and Slide Gameplay

Status: `Implemented; verification pending`

Depends on: `IT-P0-014`

Player or author goal:

- play lane-locked holds and readable step slides with the accepted reward cadence

Includes:

- inherited start grade, quarter-beat checks, half-beat rewards, node rewards, and end rewards
- hold lane continuity, final-one-beat grace for holds at least two beats long, and no recovery
- slide held lanes, bounded timed lane changes, contact handoff, normal ending, and terminal flick
  ending
- one-failure termination and black-until-end presentation

Excludes:

- curved slide paths
- slides that begin with a composed flick
- automatic merging of overlapping notes

Contract impact:

- Timing: hold grace and slide checks in `RHYTHM_SYSTEM.md`
- Chart data: hold and slide target data
- Serialized assets: note visual references require inspection

Acceptance:

- starts, ticks, nodes, and ends award the documented combo and tap-equivalent score
- a node and half-beat tick at the same time both award results
- invalid hold release or slide check fails once, stops later rewards, and leaves the note black
- valid slide handoff succeeds while an empty required check fails
- a `3 -> 7 -> 6` slide holds each preceding lane until its transition allowance, shows the step
  path in authoring and gameplay, and requires timely arrival at each destination

Automated verification:

- EditMode interaction-state tests and PlayMode hold/slide smoke tests

Manual verification:

- representative normal, early-release, handoff, failed-node, normal-end, and terminal-flick cases
- revised step-slide readability and early/exact/late lane changes, including a distant transition

Evidence:

- runtime start-grade inheritance, check/reward cadence, handoff, grace, termination, and failed
  black presentation are implemented
- existing complete PlayMode regression suite passed 4/4 on 2026-08-27
- 2026-09-04 complete EditMode suite passed 162/162 and PlayMode suite passed 7/7, including step
  geometry, transition-state, ownership, and terminal-motion regression coverage; see
  `Playtests/2026-09-04-step-slide-verification.md`
- representative interactive hold/slide cases and physical-device feel checks remain pending

### IT-P0-016 — Implement Horizontal Flick Gameplay

Status: `Implemented; verification pending`

Depends on: `IT-P0-014`

Player or author goal:

- perform readable left and right gestures between lane-anchored endpoints

Includes:

- different start/end lanes at adjacent or greater distance
- direction derived from endpoint order
- timestamped distance, speed, dead-zone, target-lane, and generous vertical-region checks
- the ordinary Good window mapped to a single Perfect result
- success before release and immediate wrong-direction failure after the dead zone
- one PC helper key for non-gesture gameplay-flow tests

Excludes:

- vertical flicks
- treating the helper key as gesture validation

Contract impact:

- Timing: flick timestamp contract
- Chart data: flick endpoints
- Serialized assets: flick markers and presentation assets require inspection

Acceptance:

- valid near and distant flicks succeed once inside the accepted window
- wrong-direction, insufficient, and late gestures fail predictably
- helper-key success exercises state and rewards without being reported as physical-input evidence

Automated verification:

- EditMode gesture-math boundary tests and PlayMode helper-key flow test

Manual verification:

- physical-device direction, speed, distance, accidental-motion, and multi-touch evaluation

Evidence:

- direction, dead-zone, speed, target-lane, full-Good-window Perfect, release failure, terminal
  slide flick, and `F` development helper paths are implemented
- EditMode flick-math boundary coverage passes; mobile gesture validation remains pending

### IT-P0-017 — Implement Banana Gameplay and Charge Presentation

Status: `Implemented; verification pending`

Depends on: `IT-P0-014`

Player or author goal:

- trace a musically meaningful free curve and see tracking quality become an end-settled combo
  reward

Includes:

- lane-anchored tap start and end with a normalized non-lane middle curve
- deterministic editable checkpoints and curve-corridor evaluation
- one-tenth Perfect-tap checkpoint score without immediate combo or checkpoint Miss text
- recoverable later checkpoints after leaving or releasing the path
- visible body charge/gaps and integer end bonus independent of checkpoint count

Excludes:

- using curve rendering as judgement state
- fractional combo display

Contract impact:

- Timing: explicit banana checkpoint timing
- Chart data: curve, checkpoint, and maximum bonus data
- Serialized assets: curve and charge presentation assets require inspection

Acceptance:

- checkpoint count changes tracking resolution without changing the authored maximum combo bonus
- full, partial, and zero tracking settle to the documented integer tiers after a successful end
- a missed checkpoint does not emit Miss, while a missed end breaks combo and discards pending bonus
- frame hitches do not skip or duplicate checkpoint evaluation

Automated verification:

- EditMode ratio/tier, curve sample, event-order, and reset tests plus a PlayMode presentation smoke
  test

Manual verification:

- physical-device tracing, readability, re-entry, endpoint, and charge-feedback playtest

Evidence:

- normalized Bezier path evaluation, explicit checkpoint score, visible charge, endpoint result,
  and floored bonus-combo settlement are implemented
- EditMode curve and combo-tier coverage passes; representative Play Mode and device tracing remain
  pending

### IT-P0-018 — Add Interaction Recording and Post-Editing

Status: `Implemented; verification pending`

Depends on: `IT-P0-013`

Player or author goal:

- capture interaction timing while listening, then repair spatial structure without rushing live
  performance

Includes:

- explicit tap, slide/hold, and flick recording modes
- lane-key slide sequences, same-lane normal close, and incomplete-slide discard
- `0` as a non-timestamped command that converts the final slide transition to a terminal flick
- default-adjacent ordinary flick creation with later arbitrary end-lane dragging
- slide node dragging, hold-body node insertion, and transition-connector selection
- banana endpoint, curve-handle, generated-checkpoint, and manual-checkpoint editing
- preview, quantization, buffer safety, Undo, validation, and explicit save
- the workspace layout described in `CHART_AUTHORING.md`, including full banana paths in both
  timelines and shared temporary/applied banana properties

Excludes:

- waveform editing
- automatic composition of a flick and slide
- using the PC flick helper as an authoring timestamp

Contract impact:

- Timing: recorded keys retain FMOD-backed input conversion
- Chart data: all accepted interaction authoring data
- Serialized assets: edited prototype chart assets require Undo/save and migration verification

Acceptance:

- documented key sequences create the intended hold, normal slide, and terminal-flick slide
- stopping with an open slide leaves no incomplete buffered interaction
- `0` adds no timestamp or extra node
- every applied interaction validates, saves, and replays after leaving and re-entering Play Mode
- the same core edit controls remain reachable in the full-width and compact workspace

Automated verification:

- EditMode recorder-state, node-edit, checkpoint-generation, apply, validation, and Undo tests where
  deterministic

Manual verification:

- timed representative recording/correction session recorded under `Playtests/`

Evidence:

- per-type recording modes, discrete slide/hold sequences, non-timestamped `0` conversion,
  adjacent-default flicks, banana checkpoint generation, slide path canvas, detailed interaction
  fields, and full-data apply/quantize/copy preservation are implemented
- 2026-09-01 complete EditMode suite passed 88/88; deterministic hold, normal-slide,
  terminal-flick-slide, ordinary-flick, and all-type asset save/reload coverage now passes
- full non-banana shapes, snapped time/lane dragging, timing-event preview, Korean mode guidance,
  and applied-note re-editing are implemented
- the workspace panel layout, full banana-curve display, curve-handle canvas editing, and applied
  banana re-editing are implemented
- 2026-09-04 complete suites passed **188/188 EditMode** and **7/7 PlayMode**; see
  `Playtests/2026-09-04-authoring-workspace-verification.md`. Workspace tests render both views,
  all five tabs and note types, and compact/full-width layouts without changing chart/buffer data;
  they also verify applied-banana JSON round-trip and Undo, visible path selection, 12-input-position
  geometry, runtime-matching banana curves/checkpoints, and tempo-aware duplication previews
- sampled native-window inspection passed at 150% scaling; representative mouse/keyboard operation
  and physical-touch validation remain pending
- the reported overview-to-note-view failure was reproduced with real click events and fixed;
  follow-up stability suites passed **202/202 EditMode** and **8/8 PlayMode**, recorded in
  `Playtests/2026-09-04-authoring-stability-verification.md`
- the repeated-hold Undo serialization failure and implicit Drum assignment are corrected;
  buffer application validates a separate result before changing the chart or clearing recordings.
  See `Playtests/2026-09-04-recording-serialization-verification.md` for regression evidence.
- timed manual authoring plus interactive Undo/save/re-enter inspection remain pending

### IT-P0-019 — Validate the Accepted Interactions on Mobile

Status: `Ready`

Depends on: `IT-P0-015`, `IT-P0-016`, `IT-P0-017`, `IT-P0-018`

Player or author goal:

- determine whether the expanded interaction set is readable, responsive, and worth retaining

Includes:

- a representative chart section containing every accepted interaction
- physical-device hold, slide handoff, distant flick, and banana tracing
- beginning and later-song timing observations, pause/resume, restart, and frame behavior
- player explanation of note shapes, failure causes, and charge/reward expectations
- measured authoring correction time and friction

Excludes:

- final production balance, scoring, art, or difficulty taxonomy

Contract impact:

- Timing: verification only unless defects require a separate scoped change
- Chart data: representative test content
- Serialized assets: test chart and presentation references require inspection

Acceptance:

- automated suites pass and Unity Console results are recorded
- physical-device evidence distinguishes timing, gesture, readability, and performance failures
- each interaction receives a retain, revise, or remove recommendation
- unproven thresholds remain explicitly provisional

Automated verification:

- complete relevant EditMode and PlayMode suites

Manual verification:

- physical-device record under `Playtests/` with device, build, display mode, and audio route

Evidence:

- Not yet recorded

## Work Item Template

```md
### IT-<priority>-<number> — <observable outcome>

Status: `Ready`

Depends on: <IDs or None>

Player or author goal:

- <why this matters>

Includes:

- <required behavior>

Excludes:

- <nearby work that must not be added>

Contract impact:

- Timing: <none or affected sections>
- Chart data: <none or affected sections>
- Serialized assets: <none or affected assets>

Acceptance:

- <observable pass condition>

Automated verification:

- <test or Not applicable with reason>

Manual verification:

- <scene, device, and behavior>

Evidence:

- <Playtests path, test result, or Not yet recorded>
```

