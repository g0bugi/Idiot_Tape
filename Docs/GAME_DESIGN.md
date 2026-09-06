# Idiot_Tape — Game Design

> Status: Current
> Last reviewed: 2026-09-05
> Applies to: The current prototype and durable game-design direction
> Authority: Player experience, design principles, and player-visible rules

## Concept and Prototype Goal

`Idiot_Tape` is a mobile touch rhythm-game prototype inspired by the direct play feel of
older games such as Tapsonic, not a remake. Note layout and movement should communicate
instruments, voices, and musical layers through spatial patterns, regions, trajectories,
and interactions. The player should recognize a relationship between sound and chart structure.

The prototype must establish fun, readability, technical/mobile viability, and reasonable
authoring speed before expanding into a commercial production system. Its executable scope
and pass conditions live in [PROTOTYPE_MILESTONE.md](PROTOTYPE_MILESTONE.md).
Editor/PC playtesting is useful but cannot prove physical-device latency or performance.

## Design Pillars

| Principle | Implication |
|---|---|
| Music structure becomes gameplay structure | A percussion pattern, new instrument entry, or simultaneous layers may have recognizable spatial identities. These are possibilities, not mandatory patterns for every song. |
| Timing before spectacle | Dynamic visuals must preserve accurate judgement, synchronization, and readable chart motion. |
| Dynamic does not mean random | Repeated phrases, instrument identity, rhythmic motifs, and visual continuity should help players form expectations. |
| Readability is difficulty design | Challenge comes from meaningful rhythm and spatial interaction; a skilled player should understand a miss. |
| Fast chart iteration matters | Timing, positions, part assignment, repeated patterns, and playtesting must be easy enough to support experiments. |

The target audience enjoys rhythm play itself. Focus attention on music, touch response,
chart interaction, and visual rhythm. Detailed note rules belong in
[NOTE_INTERACTIONS.md](NOTE_INTERACTIONS.md); timing, data, and ownership belong in their
respective technical documents.

## Starting and Restarting Play

After the selected chart and audio are prepared, the game waits for an explicit start. The ready
screen shows the song, note speed, and preparation-length choice. Music and gameplay notes do not
advance while the player is choosing settings. Touching the start button or pressing Enter/Space
begins preparation; the development lane keys do not start a session.

The default preparation lasts two musical bars, with a short one-bar option. Both use the selected
chart's tempo and time signature, including its beat unit. These are reusable session settings,
not durations or musical values embedded for one song. Charts without a tempo map use explicit
session fallback settings. A small final-bar countdown and quiet beat clicks introduce the pulse
without covering the approaching notes.

The first note must have its full approach from the playfield entrance. If the chosen preparation
would be too short for the current note speed and the first authored note time, preparation extends
by whole bars. A note at song time zero therefore approaches during preparation instead of first
appearing beside the judgement line. A song with a later first note retains its authored intro;
neither its note times nor its calibrated first downbeat are shifted to create preparation time.

Preparation displays note movement but cannot award score, produce Misses, or retain gameplay
contacts. Note speed is locked until preparation finishes. A pause/cancel request during
preparation returns to the ready screen and cancels both scheduled music and clicks. Restart
during play clears the previous attempt and uses the same preparation flow. Repeated start or
restart requests during an existing preparation do not stack additional starts.

## Lane Philosophy

Do not design the game under the assumption that every chart has a traditional fixed lane count.

The game may still use stable positions, temporary lane-like regions, repeated patterns,
or other structured layouts when they improve readability.

The important requirement is that the architecture does not unnecessarily prevent charts
from changing spatial structure according to the music.

### Current Prototype Layout

The first playable prototype uses a landscape screen and an eight-position hidden lane grid.

- lane dividers are not rendered
- notes approach a slightly curved judgement line
- the area below the judgement line accepts touch input
- judgement considers both timing and horizontal touch position
- horizontal tolerance is intentionally generous for early mobile playtesting

The hidden grid is an authoring and input structure, not a requirement that the final game always
display or use eight conventional lanes. Lane count should remain data-driven so later charts can
test other layouts without replacing the timing or judgement architecture.

The scope and reconsideration conditions for this prototype choice are recorded in
`ADR/0003-hidden-lane-prototype-layout.md`.

For Editor playtesting, the number keys `1` through `8` trigger the corresponding hidden positions
from left to right. This is a development input path and does not replace mobile touch input.

## Musical Parts

Use chart-defined musical-part identities; drums, bass, synthesizer, melody, vocals, and effects
are examples, not a universal instrument enum. The final classification remains open.

### Musical-Part Activation

The prototype treats musical parts and their active time ranges as chart data. A part definition
owns a stable ID, a display name, and presentation color. Separate activation windows describe
when each part contributes playable notes.

All authored musical parts remain visible together. Notes whose hit times belong to an inactive
part window are presented dimly and are not judgement targets. Only playable notes can change
score or combo, and missing a dim inactive note has no gameplay consequence.

Activation windows may overlap. This supports structures such as:

- drums only
- bass only
- drums and synthesizer together
- bass and synthesizer together

The current HUD displays the part of the note that was just judged. A later anticipation UI may
look ahead to upcoming activation-window boundaries without changing note timing or judgement.

## Notes and Interaction Types

The current runtime and authoring tool implement the following prototype interaction set:

- tap for discrete lane-and-time input
- hold for sustained input inside one lane
- slide for holding authored lanes and changing lanes at designated musical times
- horizontal flick for a directed gesture between lane-anchored endpoints
- banana for free curved tracking whose middle is not lane-constrained

Banana notes are intended to become a distinctive expression of the project's central idea: a
curved path may communicate melodic or instrumental motion directly rather than reducing that
motion to a fixed sequence of lanes. Slide remains deliberately lane-based so the two interactions
have different reading and execution purposes. Its step shape expresses a held position followed
by a timed lane change; it does not ask the player to trace a diagonal path between lane nodes.

The accepted player rules, prototype score and combo behavior, contact ownership, and failure
semantics are defined in `NOTE_INTERACTIONS.md`.

This accepted prototype set does not finalize the production note taxonomy. New note mechanics
should still be added only when their musical and gameplay purpose is clear.

## Prototype Questions

| Area | Questions to validate |
|---|---|
| Rhythm | Is input immediate and judgement consistent? Does synchronization hold through a whole song? |
| Readability | Can players anticipate repeated patterns and understand changing/multiple-part layouts? |
| Music relationship | Do players recognize musical parts in the chart, and does that add meaningful gameplay? |
| Authoring | Can timings, positions, parts, and repeated structures be edited efficiently across a long chart? |
| Feasibility | Can timing, active-object overhead, and mobile performance remain stable? |

Hypothesis IDs and evidence requirements live in `PROTOTYPE_MILESTONE.md`.

## Demo Identity

The current prototype / demo is named:

`Idiot_Tape`

IDIOTAPE's album `11111101` is currently being considered as the musical basis for
prototype testing.

The current chart-authoring prototype uses KIRARA's `Snow` as its primary full-song reference.
The current grid is documented in [CHART_FORMAT.md](CHART_FORMAT.md#current-prototype-tempo-map)
and stored in its chart asset; it is independent of runtime preparation. The full-song audio
reference does not imply the entire chart is authored. Two-anchor and repeated-downbeat
calibration can refine the grid while preserving absolute note times.

Likely early test tracks include:

- Melodie
- Pluto
- Even Floor
- Idio_T

These tracks are prototype reference content.

Do not assume that prototype music is permanently licensed production content.

## Current Non-Goals

Unless explicitly requested: accounts, multiplayer, leaderboards, monetization/gacha, character
or story progression, production song-selection/download infrastructure, live services,
anti-cheat, and production analytics. Keep work focused on the prototype gates.

## Decisions Not Yet Finalized

The following should remain open until intentionally decided:

| ID | Open decision | Status |
|---|---|---|
| `GD-OPEN-001` | Final production note-type taxonomy beyond the accepted prototype interactions | Open |
| `GD-OPEN-002` | Production scoring formula | Open |
| `GD-OPEN-003` | Production combo rules | Open |
| `GD-OPEN-004` | Health and failure system | Open |
| `GD-OPEN-005` | Final judgement-window values | Open |
| `GD-OPEN-006` | Difficulty naming | Open |
| `GD-OPEN-007` | Final chart coordinate system | Open |
| `GD-OPEN-008` | Permanent chart serialization format | Open |
| `GD-OPEN-009` | User-facing calibration UI | Open |
| `GD-OPEN-010` | Final production audio backend | Open |
| `GD-OPEN-011` | Final chart-editor UI and interaction model | Open |
| `GD-OPEN-012` | Song-selection flow | Open |
| `GD-OPEN-013` | Detailed pause UX | Open |
| `GD-OPEN-014` | Production content pipeline | Open |

Do not silently convert an unresolved design question into a permanent architectural assumption.

## Updating This Document

Update this file when an intentional game-design decision changes.

Do not update it merely because temporary prototype code happens to behave differently.

When implementation and this document disagree, report the discrepancy rather than silently
rewriting either side.
