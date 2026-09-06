# Idiot_Tape — Rhythm System

> Status: Current
> Last reviewed: 2026-09-05
> Applies to: All rhythm-sensitive runtime and chart-authoring behavior
> Authority: Song time, synchronization, input timing, and judgement timing contracts

## Scope and Authoritative Clock

This contract applies to playback, chart/input timing, note position, judgement, preparation,
pause/resume/restart/seek, calibration, and latency compensation. All rhythm-critical systems
derive from one high-precision audio timeline. Rendering may sample it, never replace it
with accumulated `Time.deltaTime`, frame counts, or transform movement.

The current owner is `FmodSongPlayback`, using the selected FMOD event and an FMOD DSP-sample
anchor plus captured timeline position. The millisecond Studio cursor supports audition/seek
and anchor capture, not per-frame judgement. Playback state transitions must recapture a
consistent anchor. See [ADR 0001](ADR/0001-fmod-dsp-authoritative-song-time.md).

`TimelineTime` is the signed view before a scheduled anchor; it permits negative preparation
time without a second timer. `SongTime` remains nonnegative and authoring timestamp APIs keep
their established semantics. The views agree during ordinary playback. Unity's built-in
audio clock must not run alongside FMOD as another authoritative gameplay clock.

## Timing Terminology

| Concept | Meaning |
|---|---|
| Song time | Position in seconds relative to the intentional song origin |
| Note time | Authored absolute hit time, independent of spawn frame |
| Timing error | `inputChartTime - noteTime`; negative is early, positive is late |
| Visual lead | Seconds of approach before hit time; presentation only |
| Judgement offset | Intentional input/judgement calibration with documented sign and responsibility |

Keep timestamp conversion centralized. Use accurate input-event timestamps when available,
not the later Update processing time. Do not duplicate latency compensation in note components
or add overlapping offsets without defining their responsibilities.

## Presentation, Scheduling, and Judgement

Derive note position from chart motion/timing and current song time, conceptually
`timeUntilHit = noteTime - currentSongTime`. A hitch must return visuals to the correct
position immediately without permanent drift. Animation cannot alter judgement.

The HUD's x1–x4 speed divides the chart's visual lead time. Changing it immediately recomputes
active note/guide presentation from song time; hit times, timestamps, clock, and judgement
windows remain unchanged. Speed is locked during CountIn as specified below.

Runtime bar/beat guides resolve absolute beat times through `ChartTempoMap`, appear within
the visual lead, and disappear at the judgement line. They never become a timing source.

Keep whole-song lightweight data separately from active GameObjects. Ordered look-ahead
scheduling activates/retires relevant notes; spawning and pooling cannot define hit time.
Use pooling only when a measured or clear creation/destruction pattern warrants it.

Judge a bounded set of relevant candidates from chart and timestamped input data. Do not scan
the whole song per input, use transforms/frame counts as judgement truth, or judge the same event
twice. Window values remain configurable and provisional; frame rate alone must not change
results.

## Accepted Sustained and Gesture Timing

The accepted hold, slide, flick, and banana rules are defined in `NOTE_INTERACTIONS.md`. Their
implementation must follow the timing contract below.

This section describes the implemented interaction-timing contract. Verification evidence is
recorded in `BACKLOG.md`; representative interaction and physical-device timing, including the
revised step-slide transition, still require validation.

### Global Musical Check Grid

Hold and slide do not calculate their internal cadence by repeatedly adding a fixed number of
seconds from note start.

- validity checks occur on the chart's global quarter-beat grid
- combo and score reward ticks occur on the chart's global half-beat grid
- the grid uses the same tempo-map origin that drives authoring navigation and runtime timing
  guides
- tempo changes alter the corresponding absolute intervals without creating a second clock
- only grid points strictly inside the interaction's start/end interval count as internal events
- an authored slide node and a half-beat reward at the same absolute time may both award results

All resolved check and reward times must be deterministic absolute chart times before judgement.

### Inherited Hold and Slide Grade

Hold and slide establish Perfect or Good from their starting input. Later successful checks, nodes,
and completion inherit that grade rather than manufacturing new timing errors from render frames.

Hold completion is automatic while the required lane contact remains valid. Slide completion is
automatic while a valid contact occupies its end lane, except when the slide has an authored
terminal flick. A normal end edited to a different lane uses the slide transition allowance below;
a same-lane end retains its exact authored end check. Neither normal completion requires a release
timestamp.

A slide terminal flick uses flick motion conditions to decide success, but its one end result still
inherits the slide start grade. It is not an additional independently graded flick reward.

### Hold Early-Release Grace

For holds at least two musical beats long, the final one musical beat is an accepted provisional
early-release grace interval. Its boundary must be resolved through the tempo map rather than by
subtracting one fixed-duration second value.

Release inside that interval completes the hold without Miss. Reward ticks after release are not
awarded, while the authored end result remains awarded with the start grade. Release before the
grace boundary fails immediately.

### Slide Sampling and Contact Handoff

Slide target position is piecewise constant: retain the previous authored node's lane until the
next node time, then change to that node's lane. The visible step follows those exact chart times.
Do not interpolate a diagonal target between nodes.

An ordinary lane-changing node has a provisional transition allowance using the existing
configurable `GoodWindowSeconds` (currently 0.14 seconds on each side). This lets a finger cross
intermediate positions or a handoff occur around the musical transition without demanding an
instantaneous jump. It does not require flick direction or speed.

To avoid a dense sequence becoming one long free-travel corridor, resolve each node's window from
the adjacent authored times:

```text
windowStart = max(nodeTime - GoodWindowSeconds, (previousTime + nodeTime) / 2)
windowEnd   = min(nodeTime + GoodWindowSeconds, (nodeTime + nextTime) / 2)
```

`previousTime` is the previous node or slide start. The final normal end has no next-node midpoint
cap. A same-lane node or same-lane end has no added transition allowance. A terminal-flick end
retains the separate flick-motion contract and its existing window.

After the preceding node has been validated, quarter-beat checks strictly after that node and
inside the terminal flick's Good window wait for successful flick motion. They must not require
the finger to remain in the departure lane while flicking. Success resolves crossed checks and
their half-beat rewards before the single end reward; an unsuccessful flick fails at its deadline.
Checks before that motion window and arrival at the preceding node remain mandatory.

- outside a transition window, required quarter-beat checks sample the held lane at the original
  absolute check time
- checks, half-beat reward ticks, and node arrival checks inside the window require an eligible
  contact to occupy its destination lane at some timestamp in that window
- intermediate positions, a release gap, and contact handoff during the window are tolerated only
  if that timely destination arrival occurs
- an unresolved event waits for destination arrival or the window deadline; process events in
  chronological order and emit every authored check/reward/node result at most once
- late accepted arrival may delay visible results, but does not move authored note times, musical
  grid times, or the displayed step
- after the window closes, subsequent held-lane checks again require that destination lane

Each successful node and half-beat reward remains a separate inherited-grade result even when they
share a destination check. Contact histories and absolute timestamps must resolve early, exact,
late, and frame-hitch cases consistently. A contact may not satisfy two sustained interactions at
once.

A failed required check ends the entire slide. Later check times must not generate repeated Misses
or rewards after termination. The transition allowance is provisional and requires physical-device
testing for distant and closely spaced lane changes.

### Flick Timestamp

Flick input retains timestamped position samples. The judgement time is the first timestamp at
which the motion satisfies the authored direction, distance, speed, and end-lane conditions.

For the accepted prototype, every such completion inside the ordinary Good window becomes Perfect.
Touch jitter inside a configurable dead zone is ignored. Moving beyond that dead zone in the wrong
horizontal direction or passing the late boundary without completion produces Miss.

### Banana Checkpoints

Every applied banana checkpoint has an explicit deterministic chart time and normalized target
position. Default checkpoint generation uses the tempo map, but runtime judgement must not
regenerate edited checkpoints from render frames or elapsed `deltaTime`.

Checkpoint success adds charge and fractional score. An unsuccessful checkpoint does not emit
Miss. Banana start and end remain ordinary timestamped lane judgements, and a missed end breaks
combo according to `NOTE_INTERACTIONS.md`.

### Frame Hitches and Input History

If one render frame crosses multiple required checks, the runtime must process every crossed check
exactly once in chronological order. It must use timestamped contact state or an equivalent
deterministic input history sufficient to evaluate the relevant interval, rather than applying the
latest frame position retroactively to every missed check.

A frame hitch may reduce visible smoothness. It must not skip reward ticks, duplicate results, or
change a path outcome solely because fewer `Update()` calls occurred.

### Authoring Metronome

The chart authoring tool may schedule quiet Editor-only metronome clicks from chart tempo data.
Those clicks are preparation and navigation cues, not an authoritative gameplay clock. Recorded
input timestamps must still be converted through the FMOD DSP-backed song timeline. Count-in state
must reject note input until the selected song position actually begins recording.

Tempo calibration may also preview a candidate beat grid derived from two manual anchors or a
least-squares fit across multiple downbeats tapped on successive bars. Tap timestamps must use the
same FMOD-backed song time as the rest of the authoring tool. The current Editor metronome applies
a doubled, clamped output gain so its guide clicks remain audible alongside the song.
Clicks use a short, fast-decaying oscillator envelope and are scheduled ahead on the FMOD DSP
clock. If a click cannot retain at least 25 ms of scheduling lead, it is skipped instead of being
played late; the following beat resumes normal ahead-of-time scheduling. An Editor-only output
offset may move metronome clicks by up to 50 ms in either direction without changing chart time,
song time, note times, or judgement.
Preview changes only Editor metronome scheduling. Applying calibration updates the tempo map,
not FMOD song time, existing absolute note times, or explicit banana checkpoints. Later
hold/slide grid checks, preparation, and guides resolve against the applied map; preview alone
must never change runtime judgement.

Because Unity's built-in audio is disabled in the FMOD prototype, authoring clicks must be scheduled
through FMOD. The Editor count-in display may use Editor realtime, but it must not depend on Unity's
audio DSP clock, which does not advance while built-in audio is disabled.

When a requested recording pre-roll extends before song time zero, the authoring tool must extend
the active tempo grid into virtual negative song time. Those count-in clicks and the song start must
be scheduled against one FMOD DSP clock. Editor realtime may estimate the remaining duration for UI,
but it must not trigger song playback or recording activation. A non-zero first-downbeat offset must
therefore remain part of the continuous interval between the final negative-time count-in beat and
the first musical downbeat after the audio begins.

## Gameplay Preparation and First-Note Approach

Preparing the selected FMOD event does not itself start a gameplay attempt. The session waits in
Ready until the player starts, then schedules a CountIn followed by Playing. Audio time zero keeps
its original meaning; no preparation offset is added to chart note times or tempo sections.

The preparation plan uses the first chart tempo section's BPM, beats per bar, beat unit, and
calibrated first-downbeat time. Only a missing tempo map uses explicit session fallback settings.
For requested bar count `N`, bar duration `B`, current visual lead `L`, and earliest displayed note
time `T`, the number of preparation bars is:

```text
preparationBars = max(N, ceil(max(0, L - T) / B))
requestedAudioStartDelay = preparationBars * B + schedulingLead
```

The default and short choices request two and one bars respectively. The earliest displayed note
includes dim notes outside musical-part activation windows, because those notes also need a full
approach. At the signed timeline's initial position, the earliest note is at least one full visual
lead away from its hit time. Notes are spawned and positioned from that signed time, including
during CountIn. A later first note contributes its existing intro to the approach; the plan never
rewrites the note to make it appear sooner.

Negative-time beat cues extend the first tempo section backwards from its calibrated downbeat.
Preparation duration and musical-grid phase are separate: a nonzero downbeat offset does not
silently become zero, and the audio may begin between grid beats. The small countdown shows the
last bar's remaining beats, using the actual time signature. A skipped rendering frame reads the
current beat directly from the same signed timeline rather than replaying stale countdown values.

The scheduled music start and negative-time clicks use the same FMOD DSP sample origin. Runtime
and authoring share `FmodMetronome`; the Editor wrapper retains authoring-specific grid and offset
behavior. Clicks remain presentation cues, with insufficient-lead clicks skipped rather than
played late. No Unity built-in audio clock or accumulated frame timer starts the song.

Scheduled playback must preserve the audio-to-chart phase used by ordinary Play/Restart and
authoring recording. It restores FMOD's default event scheduling property and anchors the signed
timeline at the event channel group's scheduled gate. Parent and master DSP clocks are captured
together under the mixer lock so the returned master-clock anchor represents the same gate.
The scheduled path must not independently subtract Studio's native startup delay: doing so only
for gameplay advances the music relative to charts recorded with the existing playback timebase.
The anchor is an event scheduling origin, not a promise that decoded sample zero or physical
speaker output occurs at that instant. Native buffer-phase differences between immediate and
scheduled starts must remain measured and explicit; preserving the timebase convention does not
assert identical output phase to the sample.

The playback component reads native streaming-schedule settings, the Studio update period, and
DSP buffer sizes only to obtain minimum preparation lead. That minimum is one native preparation
budget (at least one DSP block), plus the larger of the DSP buffer queue and one Studio update
period. Very short requests may be extended to that minimum. Ordinary bar-based preparation
retains its requested duration when the budget already fits. Gameplay clicks and preview follow
the returned anchor; the authoring count-in's remaining-time display derives from the resulting
signed timeline. A caller must not assume its requested delay is the final absolute start.
Neither a measured startup discrepancy nor a song identity becomes a hardcoded timing offset.

For a scheduled start at a nonzero song position, start the event paused, then set its timeline
position before flushing Studio commands and installing the Core gate. Setting the position while
the event is stopped can make a later unpause replace the gate; the observed failure allowed a
long preparation to play early. This ordering belongs to scheduled starts and does not establish
the correctness of the separate live `Seek` path.

Synchronization regression checks must compare scheduled output against ordinary Play/Restart
under the same backend settings, including repeated starts and later song positions. Raw Studio
or decoded-channel cursor agreement with the signed timeline alone is insufficient: it can pass
while the phase of existing authored charts changes. Master-mix PCM correlation can measure that
compatibility before final output downmix; it does not measure physical-device latency. See
[the start-sync regression record](Playtests/2026-09-04-start-sync-regression-verification.md)
for the observed failure and current verification status.

During CountIn, notes can be presented but judgement, Miss processing, and contact ownership are
disabled. Preparation contacts are cleared before Playing, and note speed is locked so the
approved visual-lead guarantee cannot change midway through the approach. Entering Playing
requires both reaching the scheduled DSP start and a nonnegative signed timeline.

A pause/cancel request during CountIn stops scheduled music and clicks, clears runtime state, and
returns to Ready. Repeated start/restart requests during CountIn are ignored. Restart from Playing
clears the previous attempt and schedules a fresh preparation from the same chart data. Normal
pause/resume while Playing continues to use the established FMOD pause state.

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
- active contact ownership
- sustained-note check cursors and termination state
- pending banana charge and unclaimed bonus combo

Authoring seek and looping already use `FmodSongPlayback.Seek` while gameplay is disabled.
The recorded live-seek stale-anchor/streaming finding remains open in
[IT-P0-003](BACKLOG.md#it-p0-003--verify-pause-resume-restart-and-seek-boundaries); a successful
scheduled nonzero start does not verify live or paused seek. Any gameplay seek must rebuild
runtime state from chart time rather than replay missed frames; no player-facing gameplay
seek flow is currently implemented.

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

### Timing Verification Evidence

For a milestone or regression check, record the test in `Playtests/` using
`Playtests/TEMPLATE.md`. Include, where relevant:

- Git commit or build identifier
- Unity version, device, display mode, and audio output route
- chart and FMOD song event
- beginning, middle, and end observations
- pause, resume, restart, seek, and frame-hitch conditions
- input timestamp source and applied offset semantics
- automated tests and Play Mode checks actually run
- relevant checks that were not performed

Do not convert a perceived offset into a hidden constant before distinguishing input latency,
output latency, chart timing error, and clock-conversion error.

## Unresolved Timing Decisions

The following are not yet fixed:

| ID | Open decision | Status |
|---|---|---|
| `RT-OPEN-001` | Final judgement-window values | Open |
| `RT-OPEN-002` | Device calibration UX | Open |
| `RT-OPEN-003` | Exact input-latency compensation strategy | Open |
| `RT-OPEN-004` | Exact output-latency compensation strategy | Open |
| `RT-OPEN-005` | Final production audio backend | Open |
| `RT-OPEN-006` | Playback-rate modification behavior | Open |
| `RT-OPEN-007` | Detailed pause UX | Open |

Do not hard-code unresolved design decisions into unrelated systems.
