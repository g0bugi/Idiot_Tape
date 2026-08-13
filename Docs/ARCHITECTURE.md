# Idiot_Tape — Architecture

## Document Purpose

This document describes intended high-level system boundaries for Idiot_Tape.

It is not a requirement to immediately create every system named here.

The repository's current implementation must be inspected before introducing new classes.

The purpose of this document is to define responsibilities and prevent incompatible parallel
systems from growing over time.


## Architectural Goals

The architecture should support:

- accurate rhythm timing
- unconventional chart layouts
- rapid chart iteration
- long songs
- mobile performance
- clear ownership of gameplay state
- testable timing and judgement behavior


## Main Data Flow

The intended conceptual flow is:

```text
Song / Chart Data
        |
        v
Gameplay Session
        |
        +----> Authoritative Song Clock
        |
        +----> Chart Scheduling
        |          |
        |          v
        |      Runtime Notes
        |
        +----> Input
                   |
                   v
              Judgement
                   |
                   v
            Gameplay Result
```

Visual systems observe gameplay state.

Visual systems must not become the authoritative source of musical timing.


## Core Responsibility Boundaries

### Gameplay Session

The gameplay session coordinates the current play session.

Possible responsibilities:

- selecting the current song / chart
- starting gameplay
- restarting gameplay
- coordinating pause / resume
- owning session-level state
- coordinating completion

It should not personally implement every subsystem.


### Song Clock

The song clock owns the authoritative rhythm timeline.

Responsibilities include:

- current song time
- timing origin
- pause / resume synchronization
- restart synchronization
- conversion needed by judgement timing

There must not be multiple independent authoritative song clocks.


### Chart Data

Chart data describes what should happen and when.

It must not depend on live scene GameObjects.

See `CHART_FORMAT.md`.


### Chart Scheduler

The scheduler determines which chart objects are currently relevant.

Possible responsibilities include:

- advancing through ordered chart data
- activating notes before their hit time
- retiring notes after they are no longer relevant

The scheduler does not redefine note timing.


### Runtime Note

A runtime note represents a currently active gameplay object.

Its responsibilities should remain focused.

A runtime note may:

- display itself
- expose its chart identity
- represent its current interaction state
- react to judgement results

It should not become responsible for:

- global song time
- loading charts
- global scoring
- managing every other note
- global input routing


### Input

Input code captures player input and exposes it to gameplay.

Input handling should not duplicate rhythm-clock calculations throughout multiple components.

Timestamp conversion or calibration logic should remain centralized where possible.


### Judgement

Judgement determines how input relates to chart notes.

Judgement should primarily operate on data such as:

- note timing
- current relevant notes
- input timing
- judgement windows

It should not depend on visual note position as the authoritative timing value.


### Presentation

Presentation includes:

- note visuals
- judgement effects
- UI animation
- background effects
- chart visual transitions

Presentation may use rhythm state.

Presentation must not modify the authoritative rhythm timeline merely to make an effect work.


### Score / Combo / Results

Scoring is downstream from judgement.

Judgement should emit or expose an outcome that score/result systems can consume.

Avoid embedding scoring formulas independently into individual note objects.


## Data Ownership

Every important piece of state should have one clear owner.

Examples:

```text
song time             -> timing system
chart definition      -> chart data
active note state     -> gameplay/runtime note system
judgement rules       -> judgement system
score                  -> score/result system
```

Avoid two systems independently storing mutable copies of the same authoritative state.


## Dependency Direction

Prefer dependencies that flow from orchestration toward focused systems.

Avoid circular dependencies such as:

```text
Note -> GameManager -> JudgementManager -> Note -> GameManager
```

Do not use static globals merely to avoid defining dependencies clearly.


## Managers

A class should not be named or designed as a `Manager` simply because its responsibility
has not been decided.

Before creating a new manager:

1. search for an existing owner of the responsibility
2. define what state the new class owns
3. define who calls it
4. define what it must not own

Avoid a single giant `GameManager` that accumulates unrelated responsibilities.


## Singletons

Do not use singletons by default.

A singleton may be appropriate only when the lifetime and global ownership semantics genuinely
match the system.

Do not use a singleton merely because Inspector references are inconvenient.


## Scene References

Prefer explicit serialized or runtime dependencies over expensive global searches.

Avoid repeated use of:

- `FindObjectOfType`
- `FindFirstObjectByType`
- object name searches

in gameplay hot paths.

Do not introduce complex dependency-injection infrastructure solely to eliminate Inspector
references.


## ScriptableObjects

ScriptableObjects may be useful for static project data.

Do not use ScriptableObjects as implicit mutable global runtime state without intentionally
defining those semantics.

Runtime session state should have a clear lifecycle.


## Scenes

Scene structure is not yet finalized.

Do not reorganize existing scenes solely to match this document.

When modifying a scene:

- preserve unrelated serialized references
- avoid unnecessary hierarchy changes
- verify missing references afterward


## Editor Tooling

Chart authoring tools should remain separated from runtime gameplay where practical.

Editor-only code should not accidentally become a runtime dependency.

As editor tooling grows, prefer keeping Unity Editor-specific code behind an Editor assembly
or otherwise excluded from runtime builds.


## Runtime Allocation

Systems that scale with note count should avoid unnecessary garbage generation.

Particularly inspect:

- scheduler loops
- note updates
- judgement
- input processing

Do not introduce complex custom containers without an actual need.


## Object Pooling

Object pooling is an optimization strategy, not a timing system.

If runtime notes are frequently created and destroyed, pooling may become appropriate.

Adding or removing pooling must not change:

- note hit time
- judgement behavior
- chart semantics


## Events and Polling

Use events when they represent discrete state changes naturally.

Examples:

- judgement occurred
- song started
- song ended
- pause state changed

Do not create event chains so indirect that state ownership becomes difficult to understand.

Polling is acceptable when it is simpler and inexpensive.

Choose based on responsibility rather than ideology.


## Error Handling

Fail clearly when essential gameplay data is invalid.

Do not silently continue with obviously invalid timing or chart references if doing so would
produce misleading gameplay.

Development-time validation should provide enough context to identify:

- affected song
- affected chart
- affected note or event
- invalid field


## Architecture Change Rule

Do not rewrite working architecture solely to match the conceptual names in this document.

When existing code already fulfills the responsibility correctly, preserve and evolve it.

When intentionally changing a core responsibility:

1. inspect current dependencies
2. determine migration impact
3. implement the smallest coherent transition
4. verify affected gameplay
5. update this document if the architectural contract changed


## Currently Unresolved Architecture

The following are not yet mandatory architectural decisions:

- exact class names
- exact scene structure
- exact assembly definition structure
- exact event system
- exact note pooling implementation
- exact chart loader
- dependency injection
- final save system
- final song database

Codex must not create these systems simply because they appear as unresolved items.
