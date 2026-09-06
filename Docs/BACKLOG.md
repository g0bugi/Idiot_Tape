# Idiot_Tape — Prototype Backlog

> Status: Current
> Last reviewed: 2026-09-05
> Authority: Work sequencing, scope, acceptance status, and evidence index

## Operating Rules

Use [PROTOTYPE_MILESTONE.md](PROTOTYPE_MILESTONE.md) for gates and the owning design/technical
documents for contracts. Finish P0 validation before unrelated P1 expansion unless the user
intentionally changes scope. Reference one observable work item instead of copying its whole
brief into each request.

Record executed checks in dated `Playtests/` files and link them here. Do not repeat detailed
commands, test histories, or full interaction contracts. A new unresolved decision gets a stable
ID in its owning document; a changed consequential decision updates the relevant ADR.

## Status Vocabulary

| Status | Meaning |
|---|---|
| `Ready` | Scoped; check listed dependencies before execution |
| `In progress` | Actively being changed |
| `Implemented; verification pending` | Implementation exists; required evidence is incomplete |
| `Blocked` | Cannot proceed without a named decision or dependency |
| `Done` | Acceptance conditions passed and evidence was recorded |
| `Deferred` | Intentionally outside the current milestone |

Existing code or a checked box does not prove `Done`. Automated tests do not replace required
manual or physical-device evidence.

## Current Evidence Snapshot

The latest recorded complete suites are **268/268 EditMode** and **12/12 PlayMode** on
2026-09-04, in the [start-sync regression record](Playtests/2026-09-04-start-sync-regression-verification.md).
That record supersedes the earlier start-flow synchronization conclusion. These are historical
results, not tests rerun during the 2026-09-05 documentation review.

| Area | Evidence | Still required |
|---|---|---|
| Original baseline and test fixes | [Baseline](Playtests/2026-08-26-verification-baseline.md) | Later feature acceptance is separate |
| Interaction data/runtime | [Initial interactions](Playtests/2026-08-27-new-note-implementation.md), [step slides](Playtests/2026-09-04-step-slide-verification.md) | Representative interactive and device cases |
| Authoring workspace | [Workspace](Playtests/2026-09-04-authoring-workspace-verification.md), [stability](Playtests/2026-09-04-authoring-stability-verification.md), [recording/apply safety](Playtests/2026-09-04-recording-serialization-verification.md) | Timed authoring, interactive Undo/save/re-entry, long sessions |
| Start/count-in | [Corrected output phase and tests](Playtests/2026-09-04-start-sync-regression-verification.md) | Full-song and physical-device timing; separate live-seek defect |

Current implementation provides all five interaction types, explicit start/preparation, and
the authoring workspace. It does not provide a results phase. The checked-in Snow chart covers
the opening roughly one minute with tap and hold notes; it is not yet the representative
full-song/all-interaction chart required by the milestone.

## Milestone Overview

| Gate | Work items | Exit condition |
|---|---|---|
| M1 Timing Baseline | `IT-P0-001`–`IT-P0-003` | Automated and manual timing evidence |
| M2 Complete Song | `IT-P0-004`–`IT-P0-005` | Complete-song completion and clean restart |
| M3 Mobile Feel and Readability | `IT-P0-006`–`IT-P0-008` | Physical-device and player evidence |
| M4 Authoring Throughput | `IT-P0-009` | Measured authoring iteration |
| M5 Interaction Set | `IT-P0-013`–`IT-P0-019` | Accepted interactions implemented and device-validated |
| Exit Decision | `IT-P0-010` | Continue, revise, or redirect decision |

## P0 Work

### IT-P0-001 — Establish the Verification Baseline

Status: `Done`
Depends on: None

Establish and record the initial compilation, Console, EditMode timing/chart/authoring tests,
and PlayMode scene/feedback baseline, including exact commands and failures. The
[2026-08-26 baseline](Playtests/2026-08-26-verification-baseline.md) records completion after
`IT-P0-011` and `IT-P0-012`. It is the original baseline, not the current suite count.

### IT-P0-002 — Verify Full-Song Timeline Stability

Status: `Implemented; verification pending`
Depends on: `IT-P0-001`

Acceptance: record beginning/middle/end synchronization, input timestamp conversion, note
scheduling/retirement, and frame-hitch recovery on the representative full-song chart.
Relevant automated tests must pass; a hitch must leave no permanent visual drift. Classify
offsets with reproduction conditions instead of hiding them in constants.

Excludes final calibration UX and playback-rate modification. Short scheduled PCM captures
in the evidence snapshot do not establish continuous full-song stability.

### IT-P0-003 — Verify Pause, Resume, Restart, and Seek Boundaries

Status: `Implemented; verification pending`
Depends on: `IT-P0-001`

Acceptance: verify gameplay pause/resume/restart, explicit start, tempo-based preparation,
full first-note approach, and authoring seek/loop boundaries. Audio, scheduling, active notes,
score/combo/progress, and input presentation must agree after each transition, including a
later song position, without new Console errors.

Known open finding: live `Seek` can capture a stale cached FMOD timeline as its new anchor.
The recorded request near 293.134 seconds left the clock near 6.027 seconds five seconds
later. An explicit-target experiment still had a streaming discrepancy and was reverted.
Define and repair this transition in scoped follow-up work; corrected nonzero `SchedulePlay`
does not prove live or paused seek correct.

The [2026-09-06 Seek verification](Playtests/2026-09-06-seek-verification.md) reproduces live
forward/backward seek and repeated authoring loop re-entry failures. It also confirms a separate
paused-seek defect: the frozen `pausedSongTime` remains at the old position and restores that
stale origin on Resume. Opt-in failing diagnostics preserve these cases; runtime repair and
interactive/output-phase verification remain open.

The [start-sync regression record](Playtests/2026-09-04-start-sync-regression-verification.md)
documents the correction of scheduled-only startup compensation and nonzero gate ordering.
PCM comparison removed the roughly 149 ms early-music regression but retained about 21.33 ms
of backend phase difference from ordinary Restart. This is neither zero-latency acceptance
nor physical-device evidence. Detailed measurements and earlier findings remain in the record.

### IT-P0-004 — Add a Minimal End-of-Song State

Status: `Ready`
Depends on: `IT-P0-002`, `IT-P0-003`

Implement a once-only completion transition that stops judgement, shows a minimal summary
from current score/combo/judgement data, and restarts cleanly without stale notes or input.
Late frames must not duplicate completion; add deterministic PlayMode coverage where practical.

Excludes production results art, final scoring, health, failure, grades, rewards, and progression.
Current `GameplaySession.SessionPhase` has no completion/results phase.

### IT-P0-005 — Complete-Song Play Mode Pass

Status: `Ready`
Depends on: `IT-P0-004`

Acceptance: play a representative full chart through validation, FMOD preparation, start,
judgement, pause/resume, completion, and restart. Inspect Console and serialized references;
record the full procedure and results under `Playtests/`.

### IT-P0-006 — Physical Mobile Build and Runtime Baseline

Status: `Ready`
Depends on: `IT-P0-001`

Acceptance for `H-MOBILE-001`: record device, OS, display mode, build, audio route, and an
actually played representative section. Capture frame behavior, audio continuity, input,
and errors. Editor behavior is not substitute evidence.

### IT-P0-007 — Touch Timing and Feedback Evaluation

Status: `Ready`
Depends on: `IT-P0-006`

Acceptance for `H-INPUT-001`: intentionally test early/late input, perceived latency, and
visible feedback on physical hardware. Record device/audio route and any proposed offset's
sign and responsibility according to `RHYTHM_SYSTEM.md`.

### IT-P0-008 — Musical-Part and Spatial Readability Playtest

Status: `Ready`
Depends on: `IT-P0-005`

Acceptance for `H-READ-001` and `H-PART-001`: test recurring spatial patterns, overlapping/changing
part activation, and inactive-note presentation. Record anticipation, miss causes, confusing
sections, and whether players can explain the relationship between heard parts and visible
structure. Do not infer success from color differences alone.

### IT-P0-009 — Measure Chart Authoring Throughput

Status: `Ready`
Depends on: `IT-P0-001`

Use the implemented workspace in [CHART_AUTHORING.md](CHART_AUTHORING.md). Acceptance for
`H-AUTHOR-001`: choose a representative layered/dense section before timing; include recording,
correction, part assignment, activation, validation, saving, and replay. Compare full-width and
compact layouts for control reachability, property lookup, scrolling, and temporary/unsaved
state confusion. Record total time, repeated friction, and the next investment justified by cost.

Workspace rendering, sampled 150% captures, real-click stability, recording serialization,
and transactional apply have the evidence linked above. They do not establish authoring speed.
Pending interactive work includes all properties tabs, both chart views, expanded drawer,
all-type correction, scoped duplication/replacement, Undo/save, and re-entered Play Mode.

### IT-P0-010 — Record the Prototype Decision

Status: `Ready`
Depends on: `IT-P0-002` through `IT-P0-009`, `IT-P0-013` through `IT-P0-019`

Acceptance: record evidence-based outcomes for every milestone hypothesis, classify blockers
and accepted limitations, and choose continue/revise/redirect. The next milestone must follow
that evidence. Update owning documents or ADRs for newly accepted decisions.

### IT-P0-011 — Make the Gameplay Smoke Test Target a Playable Note

Status: `Done`
Depends on: None

The test derives an active-part note and submits matching lane/timestamp input while preserving
note, timing-guide, HUD, feedback, and pause/resume assertions. It must remain valid when inactive
opening notes change. Targeted and full PlayMode results are in the
[baseline record](Playtests/2026-08-26-verification-baseline.md).

### IT-P0-012 — Make the Note-Speed Slider Maximum Reachable

Status: `Done`
Depends on: None

Pointer/touch endpoints select x1/x4 while preserving continuous values, immediate song-time
presentation, and unchanged hit times/windows. Targeted slider and full-suite results are in
the [baseline record](Playtests/2026-08-26-verification-baseline.md).

## P0 Note-Interaction Work

The user intentionally prioritized this accepted set before the remaining validation because
a tap-only baseline cannot represent its fun/readability. Rules remain in
[NOTE_INTERACTIONS.md](NOTE_INTERACTIONS.md), data in [CHART_FORMAT.md](CHART_FORMAT.md),
timing in [RHYTHM_SYSTEM.md](RHYTHM_SYSTEM.md), and editing in
[CHART_AUTHORING.md](CHART_AUTHORING.md). Production scoring/taxonomy, automatic composition,
and generic interaction frameworks remain outside this work.

### IT-P0-013 — Add the Accepted Interaction Chart Contract

Status: `Implemented; verification pending`
Depends on: `IT-P0-001`

Acceptance: represent all five types with type-specific durations, ordered slide nodes, flick
endpoints, banana curves/checkpoints and independent maximum bonus. Preserve legacy tap IDs,
times, lanes, parts, and playability. Reject invalid fields contextually without rejecting
same-lane/same-time overlaps solely as collisions.

Verification: serialization/validation, quantization/copy preservation, and legacy compatibility
have [automated evidence](Playtests/2026-08-27-new-note-implementation.md). Representative
serialized asset/reference inspection and compatibility review remain required; no permanent
external format or speculative migration pipeline is implied.

### IT-P0-014 — Add Deterministic Contact and Interaction Evaluation

Status: `Implemented; verification pending`
Depends on: `IT-P0-013`

Acceptance: retain timestamped input/ownership history, resolve global quarter-beat checks and
half-beat rewards, and process crossed events chronologically exactly once. One contact cannot
own two sustained interactions. Failed notes cannot emit later rewards or repeated Misses.
Pause/restart must clear ownership, cursors, and pending banana charge.

Verification: use timing, ownership, ordering, hitch/reset tests and
[interaction evidence](Playtests/2026-08-27-new-note-implementation.md).
Representative multi-contact Play Mode and 30/60/120 FPS target inspection remain pending.
Thresholds remain provisional.

### IT-P0-015 — Implement Hold and Slide Gameplay

Status: `Implemented; verification pending`
Depends on: `IT-P0-014`

Acceptance: implement inherited start grade, half-beat rewards, node/end results, hold continuity
and final-one-beat release grace for holds at least two beats long, and step-slide transitions,
handoff, normal/terminal-flick endings. Coincident nodes and reward ticks award separately.
A required failure produces one Miss, stops rewards, and leaves the body black until authored end.
A distant `3 -> 7 -> 6` slide must hold lanes and require timely destination arrival.

Verification: run relevant state/geometry and runtime-view tests; inspect serialized visuals.
[Step-slide evidence](Playtests/2026-09-04-step-slide-verification.md) covers deterministic
geometry, transitions, ownership, and terminal motion. Interactive normal, grace, failure,
handoff, early/exact/late, and terminal cases plus device readability/feel remain pending.

### IT-P0-016 — Implement Horizontal Flick Gameplay

Status: `Implemented; verification pending`
Depends on: `IT-P0-014`

Acceptance: adjacent and distant lane endpoints define left/right direction; timestamped
dead-zone, speed, distance, end-lane, and interaction-region checks determine success before
release within the Good window, scored Perfect once. Wrong direction, insufficient motion,
release failure, and late gestures fail predictably. `F` is only a PC gameplay-flow helper.

Verification: gesture-math boundaries have [automated evidence](Playtests/2026-08-27-new-note-implementation.md).
Required helper-flow/visual checks and physical-device direction, speed, distance, accidental
motion, and multitouch evaluation must be recorded; helper success is not gesture evidence.
Vertical flicks remain out of scope.

### IT-P0-017 — Implement Banana Gameplay and Charge Presentation

Status: `Implemented; verification pending`
Depends on: `IT-P0-014`

Acceptance: lane-anchored tap endpoints enclose a free normalized curve with explicit editable
checkpoints. Successful checkpoints award one-tenth Perfect-tap score without immediate combo;
missed checkpoints allow later recovery without Miss text. Charge/gaps show tracking quality.
Successful end settles the floored integer bonus independently of checkpoint count; missed end
breaks combo and discards pending bonus. Hitches must not skip or duplicate checkpoints.

Verification: curve/bonus math has [automated evidence](Playtests/2026-08-27-new-note-implementation.md).
Full/partial/zero charge, ordering/reset and presentation cases, serialized visuals, and device
tracing/readability/re-entry/endpoints remain required where not covered by recorded evidence.

### IT-P0-018 — Add Interaction Recording and Post-Editing

Status: `Implemented; verification pending`
Depends on: `IT-P0-013`

Acceptance: tap, slide/hold, flick, and banana recording plus shared temporary/applied editing
follow the authoring contract. Lane-key sequences must produce hold/normal slide, discard an
open slide, and use non-timestamped `0` to turn its final transition into a terminal flick.
Support adjacent-default flicks with arbitrary endpoint correction, snapped slide dragging and
hold-body insertion, banana curve/checkpoint editing, and full path previews.

Buffer application, quantization, duplication, Undo, validation, and explicit saving must preserve
interaction data; all applied types must replay after Play Mode re-entry. Controls remain
reachable in full-width and compact workspace layouts. Waveforms, automatic composition, and
using the flick helper as an authoring timestamp remain excluded.

Verification: initial recording and asset persistence, workspace rendering/path geometry,
actual-click stability, repeated hold serialization, unresolved-part preservation, and
transactional apply are covered by the evidence snapshot. Timed representative recording/
correction and interactive Undo/save/re-entry remain pending. Use the
[non-banana manual procedure](Playtests/2026-09-01-non-banana-authoring-manual.md) where relevant;
it is a procedure, not a completed playtest.

### IT-P0-019 — Validate the Accepted Interactions on Mobile

Status: `Ready`
Depends on: `IT-P0-015`, `IT-P0-016`, `IT-P0-017`, `IT-P0-018`

Acceptance: use a representative section containing every type on physical hardware, including
hold, slide handoff, distant flick, and banana tracing. Record beginning/later-song timing,
pause/resume/restart/hitch behavior, player explanations of shapes/failure/charge, and measured
authoring correction cost. Complete relevant automated suites and record Console results.

Separate timing, gesture, readability, and performance findings. Give each type a retain/revise/
remove recommendation; keep unproven thresholds provisional. Evidence must include device,
build, display, and audio route. No physical-device record is currently linked.

## P1 Parking Lot

Unscheduled until P0 evidence or an explicit user request justifies expansion:

- musical-part anticipation UI
- multi-note selection/editing and activation-window handle dragging (individual note/path
  dragging is already implemented)
- variable-tempo authoring workflow and user-facing calibration UI
- additional spatial representations or interactions
- production results/scoring and song-selection/content-library flow

## Work Item Template

Use only fields needed to make scope and acceptance clear. Do not create empty subheadings.

```md
### IT-<priority>-<number> — <observable outcome>

Status: `Ready`
Depends on: <IDs or None>

Goal/scope: <player or author outcome; important exclusions>
Contracts/asset impact: <links and migration needs, only when applicable>
Acceptance: <observable pass conditions>
Verification: <automated and manual/device checks appropriate to the change>
Evidence: <dated record or Not yet recorded; remaining limits>
```

