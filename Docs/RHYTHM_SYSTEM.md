# Idiot_Tape — Rhythm System

## Document Purpose

This document defines timing and synchronization rules for rhythm-sensitive gameplay.

Changes involving any of the following must follow this document:

- song playback
- chart time
- note timing
- note visual position
- judgement
- pause / resume
- calibration
- latency compensation
- seeking or restarting
- playback scheduling


## Primary Rule

Idiot_Tape must have one authoritative song timeline.

All rhythm-critical systems must derive their timing from that timeline.

Do not maintain independent clocks for:

- audio playback
- note movement
- judgement
- chart progression

that can drift apart.


## Authoritative Clock

The authoritative clock should be based on a stable audio timeline rather than accumulated
render-frame time.

For Unity's built-in audio system, DSP-based audio time is the expected reference candidate.

The exact implementation may change if the project later adopts another audio backend,
but the architectural requirement remains:

> rhythm gameplay must use one authoritative high-precision audio timeline.


## Do Not Use Accumulated Delta Time as Song Time

Do not implement authoritative song time as:

```csharp
songTime += Time.deltaTime;
```

A frame rate drop, pause behavior, or accumulated numerical error must not cause the
gameplay timeline to permanently diverge from the audio timeline.

`Update()` may read the current song time and update visuals every frame.

It must not become a second authoritative music clock.


## Timing Terminology

Use the following concepts consistently.

### Song Time

Current position on the authoritative gameplay/audio timeline.

Measured relative to the intentionally defined start of the song timeline.


### Note Time

The chart-defined time at which a note should be hit.

A note's timing should not be defined by the frame on which it happens to spawn.


### Timing Error

Judgement timing should use:

```text
timingError = inputChartTime - noteTime
```

By convention:

```text
timingError < 0  => early
timingError > 0  => late
```

Keep this sign convention consistent throughout the project.


### Visual Lead Time

The amount of time before its hit time that a note becomes visible or enters gameplay.

This affects presentation.

It must not redefine the note's actual hit timing.


### Judgement Offset

A timing calibration value applied intentionally to judgement timing.

Its meaning and sign must be documented wherever it is exposed to users or tools.

Do not introduce multiple overlapping offsets without clearly defined responsibilities.


## Note Presentation

A note's visual state should be derivable from:

- current authoritative song time
- the note's chart timing
- its chart-defined presentation / motion information

Conceptually:

```text
timeUntilHit = noteTime - currentSongTime
```

Visual position should ultimately be recoverable from timing state.

Do not make correct note position depend only on repeatedly moving the note from its previous
frame position.

This ensures that after a temporary frame hitch, the visual can return immediately to the
correct timeline position instead of preserving accumulated drift.


## Spawning

Spawning is a runtime optimization and presentation concern.

Spawn time is not hit time.

A note may be instantiated or activated shortly before it becomes relevant.

Its actual timing always comes from chart data.

Changing spawn lead time must not change judgement timing.


## Runtime Object Lifetime

The chart may contain all note data for the entire song.

Runtime GameObjects do not need to exist for the entire chart simultaneously.

The runtime may use:

- look-ahead activation
- spawn windows
- recycling
- object pooling

when appropriate.

Do not introduce pooling complexity before there is a meaningful repeated creation/destruction
pattern or performance reason.

Regardless of pooling strategy, runtime object lifetime must not define musical timing.


## Judgement

Judgement must compare input timing against chart timing using the same authoritative timeline
used by the rest of gameplay.

Avoid judgement implementations that:

- depend on visual transform position as the authoritative timing source
- rely on frame count
- scan the entire chart for every input
- allow the same note to be judged multiple times
- produce different timing results solely because rendering frame rate changes

Judgement windows are not yet finalized and should be configurable rather than scattered
as unexplained constants.


## Candidate Note Search

Judgement should operate on a bounded set of currently relevant notes.

As chart size grows, input processing should not require iterating over every note in the song.

The exact indexing strategy should remain proportional to actual project needs.

Avoid speculative complex data structures without evidence they are needed.


## Input Timing

Input handling and judgement must use a consistent definition of when an input occurred.

Do not convert input into judgement based only on when a later `Update()` happens to process it
if more accurate input event timing is available.

If input timestamps require conversion into the song timeline, keep that conversion explicit
and centralized.

Do not duplicate latency compensation logic across individual note components.


## Pause and Resume

Pause / resume must preserve synchronization.

Do not implement pause by allowing gameplay time to continue while merely hiding or freezing notes.

After resume:

- authoritative song time
- chart scheduling
- visual note state
- judgement state

must agree.

If audio scheduling or timing origin must be recalculated, perform that recalculation in one
central timing system.


## Restart and Seeking

Restarting gameplay must reset all rhythm state consistently.

This includes, where applicable:

- song timeline origin
- consumed note state
- active notes
- judgement state
- score / combo state
- scheduler indices

Seeking, if later implemented, must rebuild runtime note state from chart time instead of trying
to replay every missed frame.


## `Time.timeScale`

Do not assume `Time.timeScale` is the authoritative way to control rhythm playback.

Any pause or slowdown feature must explicitly account for the audio timeline.

Gameplay animation time and rhythm time are different concerns.


## Frame Rate Independence

Rhythm results should not fundamentally change between common rendering frame rates.

At minimum, timing logic should be designed so that 30 FPS, 60 FPS, 120 FPS, and temporary
frame hitches do not create independent song-time drift.

Rendering smoothness may differ.

Authoritative timing should not.


## Long-Song Stability

Prototype songs may be several minutes long.

Synchronization must remain stable throughout an entire track.

When testing timing-system changes, pay attention to:

- beginning of song
- middle of song
- end of song

Do not validate synchronization using only the first few seconds.


## Timing Tests

Timing logic that can be separated from Unity scene behavior should be testable without
requiring visual inspection.

Useful cases include:

- exact hit
- early hit
- late hit
- judgement boundary
- just outside judgement boundary
- offset application
- early/late sign convention
- frame hitch recovery
- restart
- pause / resume
- consumed-note handling
- long-song timing

When fixing a timing bug that can be reproduced deterministically, prefer adding a regression
test when practical.


## Timing Change Verification

When changing rhythm-critical behavior:

1. verify compilation
2. run relevant timing tests
3. test the affected behavior in Play Mode when available
4. inspect the beginning and a later part of a song when synchronization is involved
5. check for new Console errors
6. review whether a second timing source was accidentally introduced

If mobile latency is relevant and no device test was performed, report:

`Not verified on physical mobile hardware.`


## Unresolved Timing Decisions

The following are not yet fixed:

- final judgement window values
- device calibration UX
- exact input-latency compensation strategy
- exact output-latency compensation strategy
- final audio backend
- playback-rate modification behavior
- detailed pause UX

Do not hard-code unresolved design decisions into unrelated systems.
