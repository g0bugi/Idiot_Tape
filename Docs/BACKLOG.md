# Idiot_Tape — Prototype Backlog

> Status: Current
> Last reviewed: 2026-08-25
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
| Exit Decision | `IT-P0-010` | Continue, revise, or redirect is recorded |

## P0 Work

### IT-P0-001 — Establish the Verification Baseline

Status: `Ready`

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

- create a record from `Playtests/TEMPLATE.md`

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
- authoring seek and loop boundaries where they use the same playback component

Acceptance:

- audio, chart scheduling, active notes, score, combo, progress, and input presentation agree after each supported transition
- the test covers a transition from a later part of the song, not only the first seconds
- new Console errors are absent

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

Depends on: `IT-P0-001`

Goal:

- evaluate `H-AUTHOR-001` using the normal chart-authoring workflow

Acceptance:

- a representative layered or dense section is chosen before timing begins
- recording, correction, part assignment, activation, validation, saving, and replay are included
- total time and repeated friction points are recorded
- the next tooling improvement is justified by observed cost

### IT-P0-010 — Record the Prototype Decision

Status: `Ready`

Depends on: `IT-P0-002` through `IT-P0-009`

Goal:

- decide whether to continue, revise, or redirect the current concept

Acceptance:

- each hypothesis in `PROTOTYPE_MILESTONE.md` has an evidence-based outcome
- blockers and accepted limitations are separated
- the next milestone contains only work justified by the decision
- any newly finalized design or technical decisions are updated in their owning document or ADR

## P1 Parking Lot

These items are intentionally unscheduled until P0 evidence justifies them.

- musical-part anticipation UI
- chart multi-selection and drag editing
- activation-window handle editing
- variable-tempo authoring workflow
- user-facing calibration UI
- additional spatial-layout representation
- additional note interaction types
- production results and scoring rules
- song-selection and content-library flow

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

