# Idiot_Tape — Chart Authoring

> Status: Current
> Last reviewed: 2026-08-25
> Applies to: The current Play Mode prototype authoring tool
> Authority: Current authoring workflow and tool limitations

## Document Purpose

This document describes how the current chart-authoring tool edits prototype chart data.

The chart data contract remains in `CHART_FORMAT.md`. The rhythm and timestamp contract remains in
`RHYTHM_SYSTEM.md`. Tool convenience must not redefine either contract.

## Authoring Invariants

- recorded input timestamps are converted through the FMOD DSP-backed song timeline
- the metronome and count-in are authoring guides, not authoritative gameplay clocks
- a temporary recording buffer does not modify the chart until the author explicitly applies it
- quantization changes authoring data only and preserves the original recorded timestamp for restoration
- chart changes use Unity Undo where supported and require explicit asset saving
- tempo calibration previews do not change note times until the author explicitly applies the result
- the tool does not manually place runtime note GameObjects into the gameplay scene

## Opening the Tool

The Editor menu `Tools > Idiot Tape > 채보 제작 도구` opens the current Play Mode authoring tool.

It uses the Gameplay scene's FMOD playback component so number-key recording is converted to the
same DSP-backed song timeline used by runtime judgement.

The recorder intentionally disables the live gameplay session while recording. Seeking and looping
must not leave runtime scheduling state active behind the authoring session.

## Recommended Working Loop

1. open the Gameplay scene with the intended chart and FMOD event references
2. enter Play Mode and open the chart-authoring tool
3. select the chart, musical part, and a bar-aligned loop
4. audition the full mix, selected stem, count-in, and loop before recording
5. record into the temporary buffer
6. inspect and correct buffered timing, lanes, parts, and quantization
7. apply the buffer through append or scoped replacement
8. validate the chart and save the asset explicitly
9. exit and re-enter Play Mode before validating runtime scheduling from the edited chart

Do not bypass validation or direct serialized-asset edits merely to shorten this loop.

## Current Capabilities

### Playback and Navigation

- Korean-language play, pause, restart, seek, and repeated-range controls
- separate recording starts from the current position, a repeated range, or the beginning
- bar and beat display derived from chart tempo data
- bar-number loop entry and bar-aligned loop snapping
- a bar-first loop workflow using start bar plus bar count
- second-based controls under an advanced authoring foldout
- horizontal timeline and vertical chart-sheet views
- playback auto-follow in both views derived from FMOD song time
- timeline click-to-seek with quarter-beat snapping
- mouse-wheel navigation and pointer-centered Ctrl/Cmd-wheel zoom

### Recording and Editing

- chart-defined musical-part selection
- number keys `1` through `8` as hidden-position recording input
- a temporary recording buffer that does not modify the chart until explicitly applied
- individual timing, lane, and part edits
- millisecond timing nudges
- appending notes or replacing recorded parts inside the selected loop
- optional activation-window creation when recorded notes fall outside existing part windows
- direct selection of buffered notes from either timeline view
- direct selection and Undo-supported deletion of applied notes from either timeline view
- confirmation-protected deletion of a selected part's applied notes inside the current loop
- live preview and Undo-supported quantization of one selected part's applied notes across the full
  chart or current loop, using the same quantization settings as the recording buffer
- confirmation before chart changes discard buffered notes
- protection against accidental repeated application of the same buffer

### Pattern Duplication

- bar-aligned duplication of the selected musical part from the current loop
- a target start bar and repeat count, with the next bar selected by default
- ghost-note preview plus source, generated, inactive, and occupied-target counts before apply
- conflict policies that abort, replace the selected part in the target range, or keep existing notes
- optional target activation-window creation and normalization
- stable new note IDs, global time sorting, chart validation, explicit saving, and Unity Undo
- rejection when source and target overlap or when the target uses a different time signature

### Musical-Part Audition

- chart-defined stem volume audition
- selected-part soloing
- creating the selected part's activation window from the current loop
- activation-window overlap validation
- an Editor normalization command that merges duplicate, overlapping, or adjacent windows for the same part

### Count-In, Metronome, and Quantization

- one- or multi-bar count-in with weak clicks and accented downbeats
- musical pre-roll when enough earlier song time exists
- virtual negative-song-time count-in when requested pre-roll extends before song time zero
- loop recording that repeats through pre-roll rather than jumping directly to the first note
- tempo-aware quantization to straight or triplet grids
- configurable quantization strength, maximum correction distance, input-time advance, and near-simultaneous chord grouping
- optional automatic quantization when recording stops
- preservation and restoration of the original DSP-backed input time after quantization

### Tempo Calibration

- manual two-anchor calibration
- successive-bar downbeat tapping
- least-squares BPM and first-downbeat derivation across captured anchors
- candidate-grid metronome preview
- millisecond phase adjustment
- an explicit Undo-supported tempo-map apply step that preserves absolute note times

### Data Safety

- Unity Undo for supported chart changes
- chart validation
- explicit asset saving
- visual separation of applied notes and buffered notes
- a timeline showing beat/bar lines, playhead, loop range, musical-part activation, applied notes, and buffered notes

## Metronome and Count-In Timing

The metronome is an Editor-only guide and never replaces FMOD song time as the input-recording
source. Its scheduling rules are defined in `RHYTHM_SYSTEM.md`.

When count-in extends before song time zero, virtual negative chart time is used only to place guide
clicks and schedule the real song start on one FMOD DSP clock. It does not create negative runtime
note times or a second gameplay timeline.

## Apply and Replay Safety

After applying and saving chart changes, exit and re-enter Play Mode before validating gameplay.
This rebuilds runtime notes and scheduler state from the edited chart.

When replacing notes inside a loop, keep the operation scoped to the selected musical part and
selected time range. Do not delete unrelated chart data.

## Current Limitations

The current tool does not provide:

- waveforms
- hold or slide editing
- automatic beat analysis
- multi-note selection
- drag editing of notes
- drag editing of activation-window handles
- keyboard recording beyond the first eight development positions
- a proven variable-tempo workflow beyond direct tempo-section data

These limitations are not automatic feature requests. Prioritize improvements using the measured
authoring cost recorded by `BACKLOG.md` item `IT-P0-009`.

## Authoring Verification

When changing the tool or chart workflow:

1. run relevant EditMode tests
2. verify buffer, apply, Undo, validation, and explicit save behavior
3. confirm absolute note times change only when intended
4. confirm tempo previews do not mutate the chart before apply
5. confirm the edited chart can be replayed after leaving and re-entering Play Mode
6. check the Unity Console
7. record a measured authoring session when the change claims to improve workflow speed

Use `Playtests/TEMPLATE.md` for authoring evidence.
