# Idiot_Tape — Chart Format

## Document Purpose

This document defines the conceptual contract for Idiot_Tape chart data.

The final serialization format is not yet fixed.

This document intentionally separates:

1. what chart data must represent
2. how authoring tools edit it
3. how runtime code consumes it
4. how the data is eventually serialized


## Core Principle

A chart is structured gameplay data.

It is not a collection of manually placed runtime note GameObjects in a Unity scene.

Chart data must remain usable independently from runtime note GameObject lifetime.


## Chart Responsibilities

A chart may need to describe:

- note timing
- note type
- musical part / layer
- spatial position
- note duration
- presentation information
- movement behavior
- repeated musical structures
- future chart-specific metadata

The current prototype chart also identifies its FMOD song event by path. This is song-selection
data, not a timing source: runtime timing still comes from the playback system's DSP clock.

Not all of these fields need to exist in the first prototype.


## Timing Representation

Chart timing must use a deterministic musical timeline.

Do not store authoritative note timing as:

- frame numbers tied to rendering frame rate
- runtime spawn time
- world-space position
- manually accumulated gameplay time

Runtime note timing must be compatible with the authoritative timeline described in
`RHYTHM_SYSTEM.md`.


## Absolute Runtime Timing

Runtime judgement should ultimately operate on resolved absolute chart timing.

Tempo / BPM data may later be useful for:

- editor grids
- beat snapping
- musical navigation
- authoring tools

However, runtime judgement must not require reconstructing timing from rendering frames.

### Current Prototype Tempo Map

Charts may now provide ordered tempo sections for authoring. Each section identifies:

- the first bar covered by the section
- the absolute song time of that bar's downbeat
- BPM
- beats per bar
- beat unit

Runtime notes continue to store and judge absolute song time. Tempo sections exist to support
musical navigation, count-in, metronome cues, timeline grids, snapping, and bar-aligned musical-part
activation. A chart without tempo information can still run, but the authoring tool disables
tempo-dependent operations for that chart.

The Snow prototype currently uses one section: 130 BPM, 4/4, with the first downbeat at song time
`0`. The earlier forced one-beat offset was removed so restarting from the beginning and the
authoring beat grid share the same origin. Tempo changes remain supported by the data shape but
have not yet been proven through a variable-tempo chart.


## Stable Note Identity

Each note should have a stable identity within the chart when practical.

Stable IDs are useful for:

- editor selection
- debugging
- validation
- future migration
- referencing chart events

Do not use a runtime GameObject instance ID as persistent chart identity.


## Musical Part Identity

Notes may belong to musical parts or layers.

Examples:

```text
drums
bass
synth_main
synth_secondary
vocal
fx
```

These are examples only.

Do not hard-code this example list as the universal instrument model.

Charts should be able to define or reference song-specific musical-part identities.

### Musical-Part Activation Windows

The current prototype stores musical-part activation as chart-level time windows. Each window
contains:

- a musical-part ID
- an inclusive start time
- an exclusive end time

Windows for different parts may overlap. A note is valid when its referenced musical part is
defined. A note is playable only when that part is active at the note's hit time. Notes outside
activation windows remain valid chart data and are rendered as dim, non-interactive notes.
Activation affects chart structure; color remains presentation data and must not be used as the
authoritative part identity.

This representation is intended to support later look-ahead presentation, such as announcing that
a drum or synthesizer layer will become active at an upcoming boundary.


## Spatial Representation

The final chart coordinate system has not yet been decided.

Do not persist core chart positions only as:

- physical screen pixels
- current device resolution coordinates
- scene-specific world coordinates

unless an intentional coordinate-system design requires it.

Chart spatial data should be capable of being interpreted consistently across different
mobile screen conditions.

### Current Prototype Representation

For the first playable prototype, a chart defines a lane count and each note stores a zero-based
lane index. The runtime converts that index into a normalized horizontal position. These lanes are
not drawn on screen.

The initial sample chart uses eight lanes. This is a reversible prototype representation rather
than a permanent serialization decision. Lane count remains chart data, and gameplay code must not
depend on a single hard-coded count.

Each prototype chart supplies the FMOD event path for the song it accompanies. Changing songs
therefore means selecting different chart data rather than changing the gameplay scene or
hard-coding an event path in the playback component.

Optional FMOD stem mappings are also chart data. Each mapping associates a chart-specific stem ID
with that song event's FMOD parameter name. Songs may provide different mappings or none at all;
the runtime must not assume that every event exposes Pluto's stem parameters.


## Note Types

The final note type set is unresolved.

The chart model should support identifying note behavior without requiring the whole chart
format to be rewritten whenever a new supported note type is added.

Do not add generic extensibility frameworks purely for hypothetical note types.

Keep the prototype representation simple.


## Duration

Notes that require duration should represent it explicitly.

A duration must not be inferred solely from a visual object's scale.

Duration semantics must be defined by the note type that uses them.


## Movement and Presentation

A note may eventually require presentation information beyond a single static position.

Possible requirements include:

- start position
- target position
- path information
- musical-part-specific presentation
- layout transitions

Do not assume all of these are required immediately.

Keep musical timing independent from visual movement representation.


## Suggested Conceptual Runtime Model

The following is conceptual, not a mandatory exact C# API:

```text
Chart
 ├─ metadata
 ├─ musical parts
 └─ notes
      ├─ id
      ├─ time
      ├─ type
      ├─ part
      ├─ position
      ├─ duration (when relevant)
      └─ optional presentation data
```

Do not create fields merely because they appear in this diagram.

Only implement fields required by actual gameplay.


## Example Conceptual Data

The following illustrates intended separation of information.

It is not yet the required serialization schema.

```json
{
  "schemaVersion": 1,
  "chartId": "example_chart",
  "songId": "example_song",
  "parts": [
    {
      "id": "drums"
    },
    {
      "id": "synth_main"
    }
  ],
  "notes": [
    {
      "id": "n0001",
      "time": 12.500,
      "type": "tap",
      "part": "drums",
      "position": {
        "x": 0.25,
        "y": 0.0
      }
    }
  ]
}
```

The example does NOT finalize:

- JSON as the permanent storage format
- the coordinate range
- the note type list
- exact field names
- whether `y` is needed
- whether chart metadata is stored in the same file


## Serialization

The final authoring representation may eventually use:

- JSON
- ScriptableObject data
- another structured format
- an authoring representation that is converted into a different runtime format

Do not lock the architecture to one serialization mechanism before the authoring workflow
has been tested.


## Authoring Data vs Runtime Data

The format most convenient for editing does not have to be identical to the runtime representation.

For example, authoring data may contain:

- labels
- comments
- editor-only grouping
- beat-grid information
- descriptive musical-part names

while runtime data may contain a compact preprocessed representation.

Do not add a build-time conversion pipeline until it provides actual value.


## Ordering

Runtime systems may benefit from notes being sorted by timing.

Do not repeatedly sort the entire chart during gameplay.

Sorting or validation should happen:

- during import
- during loading
- during editing
- during preprocessing

rather than in per-frame gameplay code.


## Validation

A chart validator should eventually be able to identify issues such as:

- duplicate note IDs
- invalid timing
- negative duration
- unsupported note type
- invalid musical-part reference
- malformed position data
- unsorted data when ordering is required
- unsupported schema version

Validation errors should identify the affected chart item clearly enough for the chart author
to fix the problem.


## Schema Versioning

Once serialized chart files become persistent project content, include an explicit schema version.

Do not silently reinterpret older chart data after a breaking format change.

When format changes become necessary, use an intentional migration or conversion strategy.


## Runtime Performance

Keeping lightweight chart data for an entire song in memory is acceptable.

Do not translate every chart entry into an active GameObject at load time solely because
the data exists.

Runtime representation should scale primarily with currently relevant gameplay objects,
not total chart object count.


## Chart Tooling Goal

The chart workflow should eventually make the following operations fast:

- adjust timing
- move notes
- assign musical parts
- duplicate repeated patterns
- inspect dense sections
- jump to a song position
- replay a short section
- validate chart data

Do not build the complete editor before the core chart representation has been proven.

### Current Chart Authoring Tool

The Editor menu `Tools > Idiot Tape > 채보 제작 도구` opens the current Play Mode authoring tool.
It uses the Gameplay scene's FMOD playback component so recorded number-key input is converted to
the same DSP-backed song timeline used by runtime judgement.

The recorder currently supports:

- Korean-language play, pause, restart, seek, and repeated-range controls
- separate recording starts from the current position, a repeated range, or the beginning
- chart-defined musical-part selection
- number keys `1` through `8` as hidden-position input
- a temporary recording buffer that does not modify the chart until explicitly applied
- individual timing, lane, and part edits
- millisecond timing nudges
- chart-defined stem volume audition and selected-part soloing
- appending notes or replacing recorded parts inside the selected loop
- optional activation-window creation when recorded notes fall outside existing part windows
- Unity Undo, chart validation, and explicit asset saving
- bar and beat display derived from chart tempo data
- one- or multi-bar count-in with weak metronome clicks and accented downbeats
- musical pre-roll before recording starts when enough earlier song time exists
- loop recording that repeats through its pre-roll instead of jumping directly to the first note
- a visual timeline containing beat/bar lines, playhead, loop range, musical-part activation,
  applied notes, and buffered notes
- timeline click-to-seek with quarter-beat snapping
- selectable horizontal timeline and vertical chart-sheet views
- playback auto-follow in both horizontal and vertical timeline views, derived from FMOD song time
- mouse-wheel timeline navigation and pointer-centered Ctrl/Cmd-wheel zoom
- direct selection of buffered notes from either timeline view
- direct selection and Undo-supported deletion of applied notes from either timeline view
- confirmation-protected deletion of a selected musical part's applied notes inside the current loop
- bar-number loop entry and bar-aligned loop snapping
- creating a selected musical part's activation window from the current loop
- tempo-aware quantization of buffered input to straight or triplet grids
- configurable quantization strength, maximum correction distance, input-time advance, and
  near-simultaneous chord grouping
- preservation and restoration of the original DSP-backed input time after quantization
- optional automatic quantization when recording stops
- confirmation before chart changes discard buffered notes and protection against accidental
  repeated application of the same buffer
- manual two-anchor calibration plus successive-bar downbeat tapping that derives BPM and the first
  downbeat through a least-squares fit across all captured anchors on the FMOD song timeline
- candidate-grid metronome preview, millisecond phase adjustment, and an explicit Undo-supported
  tempo-map apply step that preserves absolute note times
- activation-window overlap validation and an Editor normalization command that merges duplicate,
  overlapping, or adjacent windows for the same musical part
- a bar-first loop workflow using start bar plus bar count, with second-based controls kept under
  an advanced authoring foldout

The recorder intentionally disables the live gameplay session while recording so seeking and
looping cannot leave runtime scheduling state inconsistent. After applying and saving, exit and
re-enter Play Mode to rebuild runtime notes from the edited chart.

The metronome is an Editor-only audible guide. It never replaces FMOD song time as the source used
to record notes. Quantization changes authoring data only; runtime notes continue to receive resolved
absolute song times. The current tool does not provide waveforms, hold/slide editing, automatic beat
analysis, multi-note selection, drag editing of notes or activation-window handles, or keyboard
recording beyond the first eight positions.


## Unresolved Chart Decisions

The following remain open:

- permanent serialization format
- exact coordinate system
- final note type model
- variable-tempo authoring UX beyond direct tempo-section data
- chart difficulty metadata
- pattern / group representation
- movement-path representation
- editor UI
- runtime preprocessing format

Treat these as design questions, not missing fields that Codex should invent automatically.
