# Idiot_Tape — Chart Authoring

> Status: Current
> Last reviewed: 2026-09-04
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

1. open the chart-authoring tool, select the chart at the top, and prepare the Gameplay scene and
   Play Mode through the right-hand `도구` tab with the intended chart and FMOD references
2. select a musical part on the left and set the bar-aligned loop at the top
3. audition the full mix or selected part, then set count-in and the recording type
4. choose current position, loop, or beginning beside the top `녹화` button and record
5. inspect the central `노트 편집` or `파트 개요` view and select the interaction to correct
6. edit timing, lanes, paths, and quantization through the right-hand properties
7. use the bottom `차트에 반영` action to append or replace the recorded parts in the loop
8. validate the chart and save explicitly with the top `저장 필요` button
9. exit and re-enter Play Mode before validating runtime scheduling from the edited chart

Do not bypass validation or direct serialized-asset edits merely to shorten this loop.

## Current Workspace Layout

Implementation status: `Implemented; verification pending`. The workspace replaces the long
whole-window control stack. The latest 2026-09-04 automated runs passed **230/230 EditMode** and **9/9
PlayMode** tests. Coverage includes real-click navigation, buffer Undo/Redo, boundary-value
preservation, repeated Synth holds through the FMOD-backed recording path, unresolved-part
preservation, and apply rejection without partial chart changes; see
[the recording verification record](Playtests/2026-09-04-recording-serialization-verification.md)
and [the earlier stability verification record](Playtests/2026-09-04-authoring-stability-verification.md).
Native window captures
were inspected at 150% display scaling in full-width and compact layouts. Representative interactive
verification and timed throughput evidence are still required; see
[the workspace verification record](Playtests/2026-09-04-authoring-workspace-verification.md).
Existing timing, buffering, apply, Undo, validation, and save responsibilities remain unchanged.

| Area | Controls and purpose |
|---|---|
| Top | Chart selection, playback and stop, `녹화` and recording-start choice, current bar/beat, tempo, and independent `저장 필요` / `저장됨` state. The second row contains loop bars, the shared snap grid, metronome, count-in, and validation. |
| Left | Musical-part selection for recording and scoped tools, selected-part solo, full-mix audition, and access to part volume/settings. Listening does not change game activation windows. |
| Center — `노트 편집` | Vertical time and the selected part's chart-defined input positions. Optional faint context from other parts, full interaction paths, and optional check/reward markers. |
| Center — `파트 개요` | Horizontal time with a row for each musical part, activation windows, note paths, and duplication preview. |
| Right — `노트` | Shared type-specific editing for selected temporary or applied notes, including time, lane, part, path, nudge, and deletion. Detailed node/checkpoint fields unfold when needed. |
| Right — `파트` | Selected-part activation windows and stem volume controls. |
| Right — `녹화` | Count-in, metronome volume/output offset, and automatic/manual quantization settings. |
| Right — `박자` | Two-anchor or repeated-downbeat calibration, candidate metronome preview, phase adjustment, and explicit tempo-map apply. |
| Right — `도구` | Scene/Play Mode preparation, navigation, loop duplication, applied-note quantization and deletion, validation, and activation-window normalization. |
| Bottom | Temporary-record count, discard, append/scoped-replacement choice, and `차트에 반영`. Expanding the drawer exposes the numeric list, original-time restoration, full-buffer quantization, missing-window option, and clear-after-apply option. |

The center's zoom, playhead centering, loop fit, and follow controls keep navigation next to the
chart. Selecting a note opens its `노트` properties. Below 1000 pixels of window width, the center
and properties share space: use `속성 열기` and `채보로 돌아가기` to switch. The top controls, left
part list, and bottom apply area remain available. This compact layout does not change chart data.

New recordings stay temporary until `차트에 반영`. Applied-note edits write to the chart immediately
with Undo and make the asset dirty; the top save state is separate from the temporary-buffer state.
Reapplying an unchanged retained buffer still requires the existing duplicate-application check.

## Current Capabilities

### Playback and Navigation

- Korean-language play, pause, restart, seek, and repeated-range controls
- Space and the playback button share start/pause/resume behavior, including after Stop; Space in
  a text field remains text input, and tempo-tap capture keeps its dedicated Space action
- separate recording starts from the current position, a repeated range, or the beginning
- bar and beat display derived from chart tempo data
- bar-number loop entry and bar-aligned loop snapping
- a bar-first loop workflow using start bar plus bar count
- second-based controls under an advanced authoring foldout
- horizontal timeline and vertical chart-sheet views
- playback auto-follow in both views derived from FMOD song time
- timeline click-to-seek using the selected shared snap/quantization grid
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
- full hold, slide, flick, and banana shapes in both timeline views rather than start-only markers
- shared detail editing for buffered and applied tap/hold/slide/flick/banana notes, with a path canvas where
  the interaction has endpoints or nodes
- direct time-and-lane dragging for hold endpoints and slide nodes, plus direct flick endpoint dragging
- banana curve-handle dragging in the selected-note canvas, with detailed handle/checkpoint fields
- immediate Undo-supported editing of applied notes of every supported type before explicit save
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
- applying temporary records, their applied state, and optional buffer clearing share one Undo/Redo
  operation, so restoring the chart also restores the corresponding recording buffer
- incomplete hold/slide/banana input is transient session state rather than serialized Undo data;
  completed temporary records remain serialized and Undo-supported
- missing or unknown musical-part IDs are displayed as unresolved; merely showing a list or note
  inspector never assigns them to the first part
- temporary records and the complete proposed chart are validated before apply changes the chart;
  rejection preserves the chart, temporary records, applied state, and Undo history
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

The prepared gameplay scene waits at START. Choose note speed and preparation length, then use
START or Enter/Space to begin its chart-tempo count-in and first-note approach. The authoring
window continues to use its own playback and recording controls.

When replacing notes inside a loop, the scope is every musical-part ID present in the temporary
buffer, not just the part selected on the left. Existing notes are replaced only when they belong
to one of those parts and their start time is within `[loopStart, loopEnd)`. Do not delete unrelated
chart data.

Apply validates the buffer and the complete candidate chart, including any requested activation
windows, before recording an Undo operation or changing the source chart. If a part reference,
interaction, or resulting chart is invalid, correct or remove the reported temporary record and
retry; rejection does not partially apply or clear the buffer. A successful apply and optional
buffer clearing remain one Undo/Redo operation, followed by explicit asset saving.

An unresolved part must be assigned explicitly. Previously corrupted records that already say
`Drum` cannot be distinguished safely from intentional Drum notes by their label or a zero start
time alone. The tool does not guess their original part or missing start timestamps; those records
need author inspection and correction or re-recording.


## Current Interaction Authoring

This section defines the implemented workflow for `NOTE_INTERACTIONS.md`. Its deterministic data
and preservation paths have automated coverage; authoring speed and physical play feel still need
the manual evidence listed in `BACKLOG.md`.

### Recording Modes

The tool provides explicit tap, slide/hold, flick, and banana recording modes. Hold is produced by
slide recording without a lane change rather than by a separate live-recording mode.

#### Tap Recording

- number keys `1` through `8` record the corresponding lane at the converted FMOD-backed song time
- the existing temporary-buffer, quantization, part, apply, Undo, validation, and save rules remain
  in force

#### Slide and Hold Recording

Slide recording is a sequence of discrete lane-key presses. The author does not need to keep a
physical key held down.

- the first `1` through `8` press opens a pending slide at that lane and time
- pressing a different lane key adds a transition node at the new lane and input time; the previous
  lane is held until that time
- pressing the current lane key a second time closes the slide normally at that time
- starting and closing on the same lane without any movement node produces a hold
- each completed hold leaves an empty pending input state, so eight presses of the same lane key
  produce four independent holds, retaining the part and timestamps of each pair
- stopping recording, ending the loop, changing chart, or otherwise leaving recording with an open
  slide discards that incomplete slide rather than guessing an end time

Examples:

```text
3 -> 3           = hold in lane 3
3 -> 1 -> 1      = hold lane 3, change to lane 1, hold lane 1, then normal completion
3 -> 7 -> 6 -> 6 = hold lane 3, change to 7, hold 7, change to 6, hold 6, then normal completion
```

Pressing `0` closes a pending slide with a terminal flick, but it does not record its own time or
add another node. Instead, it retroactively changes the final lane transition into the terminal
flick:

```text
3 -> 1 -> 0 = hold lane 3, then flick from lane 3 to lane 1 at the recorded lane-1 time
```

The temporary hold at the last lane is discarded. A `0` input is valid only after at least one
transition between different lanes exists. Ordinary and slide-terminal flicks may cross multiple
lanes.

#### Flick Recording

- number keys `1` through `8` record the start lane and judgement time
- the author selects a default left or right direction before recording
- the tool initially creates the end marker in the adjacent lane in that direction
- an impossible outward direction at a boundary lane must be rejected clearly rather than creating
  invalid data
- after recording, the end marker may be dragged to any different lane, including a non-adjacent
  lane
- a separate development helper key may directly succeed flicks during PC gameplay testing; it is
  not an authoring timestamp source and does not validate the gesture

### Post-Recording Slide Editing

- start, middle, and end nodes can be selected and dragged to another lane or time
- clicking a hold body creates a new middle node at the clicked absolute time and its held lane
- the new node time uses the active quantization/snap rules
- holding `Alt` while dragging or inserting bypasses musical snapping for a precise free-time edit
- transition connectors represent the destination node's existing time; their visual corners are
  not separate editable or reward-bearing nodes; clicking a connector selects its destination node
  for dragging rather than inserting a duplicate-time node
- the middle-node insertion button also begins in the preceding held lane; insertion requires the
  existing 10 ms minimum spacing from adjacent nodes
- start/end time collisions and non-increasing node times are rejected
- quarter-beat validity checks, half-beat reward ticks, and authored middle-node rewards are visible
  in preview
- a middle node and half-beat tick at the same time remain separate rewards even if the preview
  shares one positional marker

### Applied Interaction Editing

Applied tap, hold, slide, flick, and banana notes remain editable after the temporary recording buffer is
cleared. Selecting an applied note opens type-specific detail editing, including the path canvas
where the interaction has endpoints or nodes. Changes write to the selected `PrototypeChart`
immediately through Unity Undo, mark the chart dirty, run contextual chart validation, and still
require the top `저장 필요` action for permanent persistence.

The horizontal and vertical timelines draw each supported interaction shape:

- hold start, body, and end
- every slide hold body, timed lane-change connector, and normal or flick terminal marker
- flick start-to-end direction
- banana curve and its start/end markers, with optional explicit checkpoint markers

In the vertical timeline, slide holds are vertical and lane-change connectors are horizontal. In
the horizontal timeline and path canvas, time runs horizontally, so that geometry is transposed.
An authored node marks arrival in the new lane. Changing the view, zoom, or scroll direction does
not change the node times, and preview check/reward marks follow the held lane rather than a
diagonal interpolation. The normal-end node can be edited to a different lane; that represents a
final lane change at the end time.

The selected-note canvas provides detailed path editing while the center retains the complete
chart context. The same properties are available for temporary and applied banana notes.

### Banana Authoring

- in banana recording mode, the first lane key places the start and the second places the end
- closing a banana creates one editable curve handle and explicit global quarter-beat checkpoints
- place tap-style start and end points at authored lanes and absolute times
- adjust one or two curve handles in normalized playfield space through canvas dragging or the
  `곡선 핸들` detail fields; dragging a handle preserves the authored checkpoint data
- generate checkpoints from the chart tempo map at a quarter-beat default or an eighth-beat option
- allow generated subdivision or count to be changed without changing the maximum bonus combo
- allow individual checkpoint addition, deletion, timing correction, and position correction
- preview the complete curve and explicit checkpoints in the chart and selected-note canvas;
  unfold checkpoint fields for generation and individual correction
- inspect missed/successful charge presentation and resulting bonus behavior during gameplay;
  the authoring canvas does not simulate a player's trace or charge result
- reject temporary-buffer apply with contextual validation errors when endpoints, curve data,
  checkpoint ordering, or reward limits are invalid; the author can correct the temporary fields
  without changing the existing chart

### Overlap and Future Composition

The tool does not reject notes solely because they share a lane and time. It also does not yet
merge them automatically.

A future workflow may combine a flick ending at one lane/time with a slide beginning there to form
a slide that starts with a flick. This is a future possibility only; the current interaction work
must not add speculative merge UI or a generic composition system.

## Current Limitations

The current tool does not provide:

- waveforms
- automatic beat analysis
- multi-note selection
- drag editing of activation-window handles
- keyboard recording beyond the first eight development positions
- a proven variable-tempo workflow beyond direct tempo-section data
- direct checkpoint dragging on the central timeline; individual checkpoint correction uses the
  selected-note fields

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

For the workspace layout, also exercise all five right-hand tabs, a selected note in both chart
views, an expanded temporary drawer, and the below-1000-pixel properties switch. Check that viewing
other parts, showing check markers, navigating, or opening panels never changes absolute note times.

The current automated workspace coverage renders both views, all five tabs, and all five note
types in Unity EditorWindow tests, including 700- and 1200-pixel window widths. It checks chart and
buffer preservation, pane bounds, applied-banana serialization and Undo, path selection, and preview
agreement with runtime geometry. These checks do not establish visual readability, complete mouse
and keyboard operation, or an improvement in production speed. Executed results and the remaining
manual checks are recorded in
`Playtests/2026-09-04-authoring-workspace-verification.md`.

The repeated-hold serialization failure, unresolved-part preservation, and transactional buffer
apply regression checks are tracked separately in
`Playtests/2026-09-04-recording-serialization-verification.md`. This evidence does not replace a
timed authoring session or a measurement of physical input/audio latency.

Use `Playtests/TEMPLATE.md` for authoring evidence.
