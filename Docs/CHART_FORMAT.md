# Idiot_Tape — Chart Format

> Status: Current
> Last reviewed: 2026-09-05
> Applies to: Current prototype chart data and future-compatible chart contracts
> Authority: Chart data semantics, validation, and runtime interpretation

## Scope and Invariants

The current format is a `PrototypeChart` ScriptableObject. Its data is independent of live
scene objects and runtime note lifetimes; permanent production serialization remains open.

Chart data owns note identity, timing, type, musical part, spatial data, duration, tempo,
activation, and song/stem selection. Never derive authored timing from render frames, spawn
time, world position, or accumulated gameplay time. Runtime interpretation follows
[RHYTHM_SYSTEM.md](RHYTHM_SYSTEM.md). Add fields only for actual gameplay or authoring needs.

## Absolute Runtime Timing

Runtime notes store absolute song seconds. Tempo data is already used for navigation,
snapping, preparation, guides, and hold/slide checks and rewards. Those operations resolve
musical positions to the same absolute timeline; rendering never reconstructs judgement time.

### Current Prototype Tempo Map

Charts store ordered tempo sections when musical-grid operations are needed. Each section identifies:

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
can run tap, flick, and explicitly authored banana notes; hold and slide require a tempo map and fail validation without one. Tempo-dependent authoring
operations and runtime guides are unavailable without a map. Gameplay preparation alone uses
explicit session fallback tempo settings; that fallback does not satisfy hold/slide validation.

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
| Tempo sections | Start bar, absolute start time in seconds, BPM, beats per bar, beat unit | Required for hold/slide and musical-grid operations; ordered and valid when present |
| Lane count | Integer count of hidden normalized horizontal positions | At least 2; current sample uses 8 |
| Visual lead time | Seconds before hit time | Positive presentation value; does not change hit time |
| Musical parts | Stable part ID, display name, presentation color | IDs must be non-empty and unique |
| Activation windows | Part ID with inclusive start and exclusive end time in seconds | Must reference a part and not overlap another window for that part |
| Notes | Stable ID, type-specific interaction data, absolute hit time in seconds, start lane, part ID | Ordered by start time with valid unique IDs and references |

This table describes the current implementation. It does not finalize the permanent production
serialization format, coordinate system, or note-type model.

### Implemented Interaction Contract

The current prototype preserves existing tap data while storing the data required by the accepted
interactions in `NOTE_INTERACTIONS.md`.

The following table summarizes the implemented data requirements. It does not require an
inheritance hierarchy:

| Interaction | Required authored data |
|---|---|
| Tap | Stable ID, absolute hit time, lane, musical-part ID |
| Hold | Stable ID, absolute start and end times, one lane, musical-part ID |
| Slide | Stable ID, ordered absolute-time lane nodes, normal or flick terminal behavior, musical-part ID |
| Flick | Stable ID, absolute judgement time, different start and end lanes, musical-part ID |
| Banana | Stable ID, absolute start and end times, start and end lanes, one or two curve handles in normalized playfield space, deterministic checkpoints, maximum bonus combo, musical-part ID |

Hold and slide quarter-beat checks and half-beat rewards are derived from the chart tempo map and
the interaction interval. They do not need to be persisted as duplicate authored events unless a
later measured workflow requires it. Their resolved runtime times must nevertheless be
deterministic.

Banana checkpoints differ: the author may change their count, subdivision, time, or position, so
the applied chart must preserve enough information to reproduce those explicit checkpoints. The
number of checkpoints must remain independent from the authored maximum bonus-combo value.

Authoring data may store curve handles and checkpoint-generation settings while runtime data stores
resolved sample times and positions. This difference is permitted by the authoring/runtime
separation below.

### Serialized Interaction Mapping

In [PrototypeChart.cs](../Assets/Scripts/Gameplay/PrototypeChart.cs), `ChartNote` keeps
`id`, `hitTime`, `laneIndex`, and `musicalPartId`; `noteType` defaults to `Tap = 0`
for legacy tap assets.

| Type | Additional stored data and interpretation |
|---|---|
| Hold | `endTime`; the resolved end lane is the start lane, not `endLaneIndex` |
| Slide | `slideNodes` contain strictly later nodes after the start; the last node supplies end time/lane. `slideEndBehavior` selects normal/flick termination |
| Flick | `endLaneIndex`; resolved end time equals `hitTime` |
| Banana | `endTime`, `endLaneIndex`, `bananaCurveHandles`, `bananaCheckpoints`, `bananaMaximumBonusCombo` |

A banana handle stores normalized time within the interaction and normalized horizontal X;
a checkpoint stores absolute song time and normalized X. Neither is an arbitrary screen-space
2D point. Hold/slide check and reward grids are derived, while edited banana checkpoints persist.

## Note Types

Tap, hold, slide, horizontal flick, and banana are implemented for the current prototype target.

Their player-visible semantics are owned by `NOTE_INTERACTIONS.md`. Chart code must not silently
reinterpret one interaction as another merely because they can share lower-level timing or contact
logic. In particular:

- a hold stays in one lane
- a slide is an ordered sequence of lane holds and timed lane changes
- a curved non-lane middle path is a banana
- a flick has different start and end lanes and may cross multiple lanes
- a slide terminal flick reuses the final slide transition rather than adding a second flick note

The chart model should support identifying note behavior without requiring the whole chart
format to be rewritten whenever a new supported note type is added.

Do not add generic extensibility frameworks purely for hypothetical note types.

Keep the prototype representation simple.

## Duration

Notes that require duration should represent it explicitly.

A duration must not be inferred solely from a visual object's scale.

Duration semantics must be defined by the note type that uses them.

In the current representation, hold, slide, and banana have explicit ordered start and end times. A flick
has one judgement time and a gesture window; its duration must not be inferred from the distance
between its lane markers.

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

For the accepted prototype target:

- tap and hold use lane positions
- slide nodes retain their explicit lane and time; hold the previous node's lane until the next
  node time, then change to that node's lane
- the old-lane corner at a slide transition is derived from the adjacent nodes and is not stored
  as a same-time duplicate node; it contributes no additional judgement or reward
- flick uses lane-anchored start and end markers
- banana start and end use lanes while its curve handles and internal checkpoints use normalized
  playfield coordinates independent of physical pixels and current device resolution

The step-slide interpretation was accepted on 2026-09-04 in place of the earlier diagonal
interpolation. Existing slide node times, lanes, ordering, IDs, and terminal flags are preserved;
their intervals now describe held lanes and transition beats. No chart schema or serialized asset
migration is required. Previously authored slides should be reviewed in playback because their
intended finger motion has changed. The transition allowance is a judgement setting owned by
`RHYTHM_SYSTEM.md`, not extra authored path points or a visual smoothing parameter.

## Serialization and Runtime Separation

`PrototypeChart` currently serves authoring and runtime loading directly
([ADR 0002](ADR/0002-scriptableobject-prototype-chart-authoring.md)). No external JSON schema
or build-time conversion format is implemented. Future serialization/preprocessing remains
open; preserve timing, stable IDs, and chart semantics if it changes.

Authoring and runtime representations may differ when useful, but do not introduce a conversion
pipeline or fields solely for hypothetical metadata. Presentation and runtime object lifetime
must remain independent from the storage format.

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

`PrototypeChart.TryValidate` already checks the following, returning contextual errors:

- duplicate note IDs
- invalid timing
- unsupported note type
- invalid musical-part reference
- lane indices outside the chart's lane count
- unsorted data when ordering is required

Interaction validity requires:

- hold/banana end times strictly after their start; slide ends resolve from their last node
- a tempo map for hold and slide; hold end lanes resolve to their start lane
- strictly increasing slide node times and valid lanes
- at least one slide node and an actual lane change; use a hold for a path with no lane change
- a slide terminal flick whose final transition changes lanes
- flicks with valid, different start and end lanes
- one or two banana handles, with increasing normalized times strictly inside (0, 1) and X in [0, 1]
- at least one banana checkpoint, strictly ordered inside the start/end interval, with X in [0, 1]
- a nonnegative banana maximum bonus-combo value

Different interactions sharing a lane and time are not invalid solely for that reason. The current
authoring workflow does not auto-merge or auto-resolve those conflicts; the chart author owns their
playability until a future composed-slide workflow is intentionally implemented.

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
| `CF-OPEN-003` | Final production note-type model beyond the accepted prototype contract | Open |
| `CF-OPEN-004` | Variable-tempo authoring UX beyond direct tempo-section data | Open |
| `CF-OPEN-005` | Chart difficulty metadata | Open |
| `CF-OPEN-006` | Pattern and group representation | Open |
| `CF-OPEN-007` | Movement-path representation | Open |
| `CF-OPEN-008` | Final chart-authoring UI and interaction model | Open |
| `CF-OPEN-009` | Runtime preprocessing format | Open |

Treat these as design questions, not missing fields that Codex should invent automatically.
