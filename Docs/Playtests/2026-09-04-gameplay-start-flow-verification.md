# Idiot_Tape — Gameplay Start Flow Verification — 2026-09-04

> Superseded for scheduled-playback synchronization by the
> [start-sync regression verification](2026-09-04-start-sync-regression-verification.md).
> The results below describe the earlier implementation. They did not compare actual output
> phase with ordinary Restart/authoring playback and must not be treated as final sync acceptance.
> The historical counts and observations are retained as evidence of that coverage gap.

> Status: Automated regression checks and visual review passed; manual/device verification outstanding
> Scope: Explicit start, reusable count-in, first-note approach, and restart/cancel behavior
> Authority: Verification evidence for the accepted gameplay start-flow contract

## Verified Results

Environment: Unity 6000.3.15f1, Windows, current uncommitted working tree. Tests use temporary
charts and input settings; no physical device or human audio-latency measurement is represented.

| Final gate | Result |
|---|---|
| Complete EditMode, 13:32:54–13:33:04 UTC | **268/268 passed**, 0 failed or skipped; 10.0540757 seconds; exit code 0 |
| Complete PlayMode, 13:31:27–13:31:52 UTC | **11/11 passed**, 0 failed or skipped; 24.9825245 seconds; exit code 0 |
| Corrected preparation-plan fixture, 13:13:11 UTC | **23/23 passed**, 0 failed or skipped; 0.0814415 seconds |
| Final asset/settings preservation and diff review | Passed; original chart hash and project-setting bytes preserved |

The complete EditMode run includes `GameplayStartPlanTests` 23/23, `SongTimelineMathTests` 17/17,
and `ChartAuthoringMetronomeTests` 8/8. Both final Unity processes exited normally with code 0;
final logs contain no C# compiler errors or exceptions. One existing FMOD Studio Listener warning
appeared during PlayMode cleanup even though the gameplay camera has that component. The run is
not claimed to be warning-free.

The final PlayMode run verifies explicit START input, silence and no notes while Ready, a time-zero
note first appearing above 85% of its full approach distance, subsequent downward movement,
count-in input rejection, hits at zero and one second, repeated-start handling, Enter, restart,
Escape cancellation, cleanup/re-enable during preparation, and preparation after re-enabling an
unfinished session. The same complete run retains the prior gameplay, recording, and view tests.

The real FMOD event and signed DSP timeline agreed within approximately 12 ms in the sampled
checks at startup, around 0.3 seconds, and around one second. A separate scheduled start at 60%
of the song (`293.1342` seconds) exercised a requested 0.1-second delay that must expand to the
native backend budget: the settled signed time was `293.443533` and FMOD reported `293.455`, a
difference of approximately 11.5 ms. Pause settling, a frozen paused timeline, resume, and restart
from that later position passed. The native pause transition advanced by 0.149 seconds before
settling, within the existing test allowance; this is not an immediate-pause or zero-latency claim.

The corrected pure-plan fixture covers 60–600 BPM; 3/4, 4/4, and 6/8; x1/x4 note speed; a first note
at zero or after an authored intro; nonzero downbeat offsets; later tempo changes; explicit
fallbacks; countdown recovery after skipped frames; scheduling margins; and decimal boundaries
that must not produce a click at audio zero. Real whole-song frame-hitch and latency measurements
are not established by those pure calculations.

## Requested Behavior

The player must be able to prepare before music starts. The ready screen leads through a small
beat-based count-in into play, and the first note must approach from the playfield entrance rather
than first appearing beside the judgement line. The same implementation must support other songs
without song-specific timing constants or changes to their authored notes.

## Implementation Contract

- `GameplaySession` owns Preparing, Ready, CountIn, and Playing. Ready waits for an explicit HUD
  start or Enter/Space. Its lane-key input does not start the song.
- The standard and short choices request two and one bars. `GameplayStartPlan` reads the chart's
  first tempo section, including beat unit and calibrated downbeat. A chart without a tempo map
  uses explicit session fallback settings.
- The plan extends preparation by whole bars when necessary to provide the full current visual
  lead for the earliest visible note. Dim inactive notes remain included in that guarantee.
- Audio and negative-time beat clicks share one FMOD DSP sample origin. Signed `TimelineTime`
  exposes the existing future anchor for preview; ordinary `SongTime` and authoring APIs retain
  their previous semantics. Authored note and tempo times are unchanged.
- CountIn presents note movement, disables judgement/Misses, clears preparation contacts before
  Playing, and locks note speed. The final-bar countdown follows the actual time signature.
- A pause/cancel request during CountIn stops scheduled audio and clicks and returns to Ready.
  Restart from Playing clears the old attempt and repeats preparation. Repeated requests during
  CountIn cannot stack starts. Existing Playing pause/resume and end-of-song behavior remain.
- `FmodMetronome` is shared by runtime and the Editor wrapper so runtime does not depend on the
  Editor assembly or introduce a second audio clock.
- Native Studio event preparation is explicit: the backend reads FMOD settings, opens the event
  gate before the audio-content anchor by that budget, and returns the actual master-clock start.
  Requests shorter than the backend minimum can be extended without changing authored timing.

## Verification Corrections and Visual Review

The initial full EditMode run passed 263/263. Subsequent review added a decimal-boundary correction
and two tests: a nominal `-1.11e-16`-second beat must be treated as audio zero, not scheduled as a
preparation click. The corrected 23-case preparation fixture passed.

Two initial full PlayMode runs each passed 10/11. The first exposed batch-editor focus filtering
of queued mouse input. The fixture now clones input settings temporarily, routes input to the
Game View, and restores those settings and `runInBackground` in cleanup. The second exposed an
approximately 171 ms scheduled-audio discrepancy. Native Studio event preparation is now explicit,
and the parent gate opens before the returned audio-content anchor. Short requests also include
unpause-command and buffer/update lead. The final 11/11 run covers those corrections.

The ready-screen capture was visually reviewed: text renders normally, and preparation choice,
START, and note-speed controls are distinct. The final-run approach captures were also directly
reviewed: the first note enters near the top and travels downward, while the count display stays
below the judgement line without overlapping the note. Screenshots establish visual layout only,
not audible synchronization or device feel.

Final preservation checks confirmed the original Snow chart's SHA-256 remained
`E8BB663F86F8452BF47AB223C3D3BE3B868411EF5746535CEDECCE49A0E4A28A`. Unity's automatic scripting-define
change was inspected and the original ProjectSettings bytes restored exactly. No Unity process,
recovery directory, or temporary verification asset remained. The final diff review found no
changes under ProjectSettings, Assets/Scenes, or Packages, and `git diff --check` passed. Existing
unrelated working-tree changes were preserved.

## Separate Existing Seek Finding

Additional exploration after the start-scheduling correction exercised the existing live `Seek`
path. A request to approximately 293.1342 seconds captured a previous cached FMOD timeline value
as its DSP anchor. After a five-second wait, the song clock was only 6.027 seconds instead of
reaching the requested region. This is an existing seek-boundary defect discovered while checking
a later song position, outside the explicit-start/count-in implementation.

A temporary experiment captured the requested target explicitly. It removed the stale origin,
but the streaming transition still left signed time at 293.603333 seconds while FMOD reported
293.454 seconds, a discrepancy of about 149 ms. That experiment was reverted: the final start-flow
change does not claim to repair live or paused seek. A separate follow-up under `IT-P0-003` must
define the intended seek/streaming transition and verify its timing before changing that contract.

The start-flow task's later-position verification therefore uses the modified production
`SchedulePlay(0.1, targetAt60Percent, ...)` path, including its native minimum scheduling-budget
extension. This checks the implementation changed by this task; it must not be described as a
passing live-seek test. The final full PlayMode run passed that later-position schedule and its
minimum-delay, pause/resume, and restart assertions.

The separate seek observations are preserved in `playmode-audio-fix.xml` / `.log` and
`playmode-seek-fix.xml` / `.log`. Those diagnostic runs failed on their added seek assertions;
their failure does not itself establish that the preceding scheduled-start checks failed.

## Remaining Verification

Not verified: human audio/playtesting, physical mobile input and audio latency, and continuous
full-song synchronization or rendering-hitch measurements. The early and 60%-position samples
are not a continuous beginning/middle/end playthrough. Known unresolved: the separate existing
live-seek boundary described above; paused seek was not reached by those failed diagnostic runs.

## Artifacts

`C:/Users/User/.codex/visualizations/2026/09/04/01a06b4c-8abc-7143-9fb3-3c81f313484c/verification/start-flow/`

- Final gates: `editmode-complete.xml` / `.log`, `playmode-complete.xml` / `.log`,
  `start-plan-final.xml` / `.log`.
- Earlier checks: `editmode.xml` / `.log`, `playmode.xml` / `.log`, `playmode-final.xml` / `.log`.
- Diagnostics and separate seek exploration: `playmode-diagnostics.xml` / `.log`,
  `playmode-audio-fix.xml` / `.log`, `playmode-seek-fix.xml` / `.log`.
- Actual captures: `01-ready.png`, `02-opening-note-at-top.png`, `03-opening-note-approaching.png`.
- Preservation: `chart-hash-before.json`, `preservation-final.json`, and
  `ProjectSettings.before.asset`.
