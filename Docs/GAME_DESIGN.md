# Idiot_Tape — Game Design

> Status: Current
> Last reviewed: 2026-08-25
> Applies to: The current prototype and durable game-design direction
> Authority: Player experience, design principles, and player-visible rules

## Document Purpose

This document describes the intended gameplay and design direction of `Idiot_Tape`.

It defines what the game is trying to achieve.

Implementation details belong in:

- `RHYTHM_SYSTEM.md`
- `CHART_FORMAT.md`
- `ARCHITECTURE.md`

This document should not be treated as permission to rewrite unrelated working systems.


## Project Status

`Idiot_Tape` is currently in the prototype / demo stage.

The immediate goal is not to build a complete commercial rhythm game.

The prototype exists to determine whether the central gameplay idea is:

- fun
- readable
- technically viable
- authorable at reasonable speed
- stable enough for mobile rhythm gameplay


## Core Concept

Idiot_Tape is a mobile rhythm game inspired by the direct touch-based gameplay feel of
older mobile rhythm games such as Tapsonic.

It is not intended to be a remake or clone.

The defining idea is that the chart should visually communicate the structure of the music.

Notes should not exist only as abstract objects falling through fixed lanes.

Different instruments, voices, or musical layers may occupy different spatial patterns,
regions, trajectories, or note structures.

The player should be able to perceive some relationship between what they hear and what
they see.


## Core Design Pillars

### 1. Music Structure Becomes Gameplay Structure

The chart should reflect meaningful musical parts.

Examples include:

- percussion generating one recognizable visual pattern
- a synthesizer line appearing in another region
- a newly entering instrument changing the visible chart structure
- multiple musical layers coexisting while remaining visually distinguishable

These examples are design possibilities, not mandatory rules for every song.


### 2. Timing Comes Before Spectacle

The game may use dynamic note movement and visually expressive charts.

However:

- animation must not damage judgement accuracy
- visual effects must not alter authoritative timing
- visual complexity must not make the chart unreadable

A visually impressive chart that feels rhythmically inconsistent is a failed chart.


### 3. Dynamic Does Not Mean Random

Spatial variation should communicate music, not arbitrary motion.

When positions or patterns change, players should be able to form expectations from:

- repeated musical phrases
- instrument identity
- rhythmic motifs
- visual continuity
- chart structure

Changes should feel intentional.


### 4. Readability Is Part of Difficulty Design

Difficulty should come primarily from meaningful rhythmic and spatial interaction.

It should not come from hiding information or making note motion unnecessarily confusing.

A difficult chart should still allow a skilled player to understand why they missed.


### 5. Fast Chart Iteration Matters

The project depends heavily on experimenting with unconventional chart presentation.

Chart editing must therefore support rapid:

- timing correction
- position adjustment
- musical-part reassignment
- pattern iteration
- playtesting

A technically flexible gameplay system that makes chart editing painfully slow is not
sufficient for this project.


## Target Experience

The primary target audience is players who enjoy rhythm-game play itself.

The game is not currently designed around:

- character collection
- gacha
- character progression
- story progression
- large collection systems

The prototype should focus attention on:

- music
- chart interaction
- timing
- touch response
- visual rhythm

The current executable validation scope and pass conditions are defined in
`PROTOTYPE_MILESTONE.md`. That milestone may narrow current work, but it must not silently redefine
the design principles in this document.


## Input and Platform

Primary target platform:

- mobile devices

Primary interaction style:

- touch-based rhythm gameplay

Development and frequent playtesting may occur inside the Unity Editor on PC.

Editor testing must not be treated as proof that mobile input latency and performance are correct.


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

Charts may associate gameplay information with musical parts such as:

- drums
- percussion
- bass
- synthesizer
- melody
- vocals
- effects
- other song-specific layers

The final classification system has not yet been decided.

Do not hard-code a universal list of instruments into the core runtime unless such a list
is later intentionally defined.

Prefer chart-defined identifiers or data-driven musical-part definitions.

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

The final set of note types has not yet been decided.

Do not assume that the final game must contain a specific conventional set such as:

- tap
- hold
- flick
- slide

unless explicitly specified.

New note mechanics should be added only when their gameplay purpose is clear.


## Prototype Questions

The prototype should help answer the following questions.

### Rhythm Feel

- Does touch input feel immediate?
- Do judgement results feel consistent?
- Does the game remain synchronized over an entire song?

### Visual Readability

- Can players understand changing spatial patterns?
- Can multiple musical parts appear simultaneously without becoming visual noise?
- Can a player anticipate recurring patterns?

### Music-to-Chart Relationship

- Does separating musical parts visually make the song easier or more interesting to perceive?
- Do players notice the relationship between sound and note structure?
- Does the feature create meaningful gameplay rather than only visual decoration?

### Authoring

- Can a chart be edited quickly?
- Can timings be corrected without manually moving scene objects?
- Can repeated musical structures be authored efficiently?
- Can long songs be iterated on without an excessive workflow cost?

### Technical Feasibility

- Can the rhythm clock remain stable?
- Can long charts run without unnecessary runtime object overhead?
- Can the prototype maintain acceptable performance on mobile hardware?


## Demo Identity

The current prototype / demo is named:

`Idiot_Tape`

IDIOTAPE's album `11111101` is currently being considered as the musical basis for
prototype testing.

The current chart-authoring prototype uses KIRARA's `Snow` as its primary full-song reference.
Its authoring grid is configured for 130 BPM and 4/4, with the first downbeat at song time 0.
The earlier forced one-beat offset was removed because it shifted the beat grid after restarting
from the beginning. The chart authoring tool supports two-anchor calibration so the stored BPM and
first-downbeat time can be replaced by measured values after checking widely separated downbeats
across the song.

Likely early test tracks include:

- Melodie
- Pluto
- Even Floor
- Idio_T

These tracks are prototype reference content.

Do not assume that prototype music is permanently licensed production content.


## Current Non-Goals

Unless explicitly requested, the prototype does not currently need:

- online accounts
- multiplayer
- leaderboards
- monetization
- gacha
- character systems
- story systems
- large song-selection progression systems
- live-service infrastructure
- downloadable content infrastructure
- anti-cheat systems
- production analytics infrastructure


## Decisions Not Yet Finalized

The following should remain open until intentionally decided:

| ID | Open decision | Status |
|---|---|---|
| `GD-OPEN-001` | Final note types | Open |
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
