# Idiot_Tape — Chart Format

> Status: Current
> Last reviewed: 2026-08-25
> Applies to: Current prototype chart data and future-compatible chart contracts
> Authority: Chart data semantics, validation, and runtime interpretation

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

Runtime notes continue to store and judge absolute song time. Tempo sections support musical
navigation, count-in, metronome cues, timeline grids, snapping, bar-aligned musical-part activation,
and runtime bar/beat presentation guides. Runtime guides are presentation-only: their position is
derived from the guide's absolute beat time and the authoritative song time, and they disappear at
the judgement line without affecting note timing or judgement. A chart without tempo information
can still run, but tempo-dependent authoring operations and runtime timing guides are disabled for
that chart.

The Snow prototype currently uses one section: approximately 129.993 BPM, 4/4, with the calibrated
first downbeat at song time `0.233293`. This is a measured musical-grid origin rather than an added
runtime lead-in or judgement offset. Tempo changes remain supported by the data shape but have not
yet been proven through a variable-tempo chart.


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

The current authoring representation and reversible hidden-lane choice are recorded in
`ADR/0002-scriptableobject-prototype-chart-authoring.md` and
`ADR/0003-hidden-lane-prototype-layout.md`.

The initial sample chart uses eight lanes. This is a reversible prototype representation rather
than a permanent serialization decision. Lane count remains chart data, and gameplay code must not
depend on a single hard-coded count.

Each prototype chart supplies the FMOD event path for the song it accompanies. Changing songs
therefore means selecting different chart data rather than changing the gameplay scene or
hard-coding an event path in the playback component.

Optional FMOD stem mappings are also chart data. Each mapping associates a chart-specific stem ID
with that song event's FMOD parameter name. Songs may provide different mappings or none at all;
the runtime must not assume that every event exposes Pluto's stem parameters.

### Current Prototype Field Contract

The current `PrototypeChart` ScriptableObject persists the following authoring data:

| Data | Unit or identity | Current requirement |
|---|---|---|
| FMOD song event path | FMOD event path string | Required and non-empty |
| Stem mappings | Chart-specific stem ID to FMOD parameter name | Optional; IDs must be non-empty and unique |
| Tempo sections | Start bar, absolute start time in seconds, BPM, beats per bar, beat unit | Optional for runtime; ordered and valid when present |
| Lane count | Integer count of hidden normalized horizontal positions | At least 2; current sample uses 8 |
| Visual lead time | Seconds before hit time | Positive presentation value; does not change hit time |
| Musical parts | Stable part ID, display name, presentation color | IDs must be non-empty and unique |
| Activation windows | Part ID with inclusive start and exclusive end time in seconds | Must reference a part and not overlap another window for that part |
| Notes | Stable ID, absolute hit time in seconds, zero-based lane index, part ID | Ordered by time with valid unique IDs and references |

This table describes the current implementation. It does not finalize the permanent production
serialization format, coordinate system, or note-type model.


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

The current ScriptableObject prototype does not store a separate chart schema-version field.
Unity asset serialization and repository history currently provide the storage context, but they
do not remove the need to migrate breaking field or semantic changes intentionally.

Add an explicit schema version when the project introduces an external chart file, a build-time
conversion product, independently distributed chart content, or another representation that must
be interpreted outside the exact current Unity asset layout.

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

The current Play Mode workflow, supported features, timing invariants, and limitations are defined
in `CHART_AUTHORING.md`.

Authoring tools must continue to produce data that satisfies this document. A convenient Editor
operation must not silently redefine absolute note time, musical-part identity, activation-window
semantics, or runtime judgement.


## Unresolved Chart Decisions

The following remain open:

| ID | Open decision | Status |
|---|---|---|
| `CF-OPEN-001` | Permanent chart serialization format | Open |
| `CF-OPEN-002` | Final chart coordinate system | Open |
| `CF-OPEN-003` | Final note-type model | Open |
| `CF-OPEN-004` | Variable-tempo authoring UX beyond direct tempo-section data | Open |
| `CF-OPEN-005` | Chart difficulty metadata | Open |
| `CF-OPEN-006` | Pattern and group representation | Open |
| `CF-OPEN-007` | Movement-path representation | Open |
| `CF-OPEN-008` | Final chart-authoring UI and interaction model | Open |
| `CF-OPEN-009` | Runtime preprocessing format | Open |

Treat these as design questions, not missing fields that Codex should invent automatically.
