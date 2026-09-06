# Idiot_Tape — Seek Reproduction and Verification — 2026-09-06

> Status: Seek defects reproduced; runtime repair and milestone acceptance remain open
> Work item: IT-P0-003

## Scope and Environment

The requested work was reproduction and verification. No runtime or authoring implementation
was changed. Three opt-in PlayMode diagnostics were added in
`Assets/Tests/PlayMode/FmodSeekVerificationTests.cs`.

- Base commit: `b7c2d4f7d5f43cc67accc5ce899b528175641a65`, with the user's pre-existing
  documentation changes preserved.
- Windows Unity Editor `6000.3.15f1`, batch PlayMode, target 60 FPS, VSync disabled in the fixture.
- FMOD event: `event:/Music/KIRARA/Snow`, reported duration 488.557 seconds.
- FMOD software output: 48000 Hz, DSP buffer 512 samples x 4.
- Measurements: public `SongTime`, cached Studio timeline, captured anchor, and native source
  channel positions sampled under the mixer lock. No microphone, speaker, display, or physical
  touch latency was measured.

## Procedure

The diagnostics compare the requested transport target and real elapsed observation time with
the authoritative clock, Studio position, and each native source cursor. They sample immediately
and near 0.05, 0.5, 1, and 5 seconds after the transition. The provisional 250 ms bound detects
large transport failures; it is not an acceptable rhythm offset or a device-latency target.

1. Seek forward to 60% of the song (293.1342 seconds), backward from that region to 30 seconds,
   and forward from 30 seconds to 90% (439.7013 seconds).
2. Pause in the later region, check the frozen clock, seek to 30 seconds while paused, inspect
   the frozen clock and Studio target, resume, and observe for five seconds.
3. Invoke the actual authoring `RestartLoopCycle` transition twice, seeking from the loop-end
   region to a 28-second pre-roll preceding a 30-second recording target. The source chart is
   cloned, and both source and clone are checked for preservation. The fixture controls calls
   rather than driving mouse input or the complete automatic recording loop.

The final fixture starts the zero-position case through ordinary `Restart`; nonzero setup
uses the existing scheduled-start path to establish a known starting position. The first
diagnostic run used scheduled setup for every case. Paused native voices are recorded for
diagnosis but are not required to seek their cursors until resumed.

## Reproduced Findings

The first run (06:49:28–06:50:04 UTC) failed all three transport diagnostics, with no skipped
cases; Unity exited with code 2. The failures were the intended synchronization assertions.

| Transition | Observation | Authoritative song time | Native source position |
|---|---|---:|---:|
| Forward to 293.1342 s | About 5.014 s after request | 5.344 s | 297.945 s |
| Backward to 30 s | About 5.012 s after request | 298.478 s | 34.800 s |
| Forward to 439.7013 s | About 5.015 s after request | 35.344 s | 444.512 s |
| Paused seek to 30 s | Still paused after 0.5 s | 293.518 s | Old paused voices retained; Studio target was 30.000 s |
| Resume after paused seek | About 5.001 s after resume | 298.521 s | 34.673 s |
| First loop re-entry to 28 s | About 5.008 s after request | 37.355 s | 32.811 s |
| Second loop re-entry to 28 s | About 5.003 s after request | 37.824 s | 32.811 s |

### Live Seek and Loop Re-entry

`Seek` returns true, but its immediate `CaptureTimelineAnchor()` reads the old cached Studio
position. The native event subsequently moves to the requested region while the authoritative
DSP-derived clock continues from that old origin. Five seconds of waiting does not recover
agreement. Forward, backward, and repeated authoring re-entry all reproduce this behavior.

The immediate samples show both old and newly created source voices during the transition;
the settled samples show the new region on all four stems. This distinguishes an asynchronous
native transition from a complete rejection of the seek command.

### Paused Seek

The paused case is a separate state defect: Studio and `anchorSongTime` reach 30 seconds, but
`pausedSongTime` stays at 293.518 seconds. `SongTime` returns that stale paused value. `Resume`
then captures an anchor from `pausedSongTime`, replacing the correctly moved anchor with the
old position. Updating only the live cached-anchor capture would not repair this case.

These measurements identify the clock/state defects. They do not establish that assigning the
target number directly is a complete repair: native streaming and output phase must also be
verified, as already recorded in the September 4 start-sync investigation.

## Final Verification

| Run, UTC on 2026-09-06 | Result | Artifacts |
|---|---|---|
| Initial diagnostics, 06:49:28–06:50:04 | 0 passed, 3 failed, 0 skipped; 36.2591773 s; exit 2 | `seek-diagnostics.xml`, `.log`, `.exit.txt` |
| Default PlayMode suite, 06:51:24–06:52:15 | All 12 existing tests passed, 0 failed; 3 Explicit diagnostics skipped; 50.7693127 s; exit 0 | `playmode-baseline.xml`, `.log`, `.exit.txt` |
| Final diagnostics, 06:53:27–06:54:02 | 0 passed, 3 failed, 0 skipped; 35.1312745 s; exit 2 | `seek-final.xml`, `.log`, `.exit.txt` |

The final diagnostic run exercised ordinary Restart before the first forward Seek and again
reproduced every case. After 5.013 seconds, the forward target of 293.1342 seconds produced
`SongTime = 5.365333` versus native source position `297.955`. Paused Seek still retained
293.518 seconds instead of 30 seconds. Both loop re-entries still diverged, with first-cycle
song/native positions 37.354/32.832 and second-cycle positions 37.845/32.811 seconds.

The default suite covers scheduled audio phase, Ready/start/count-in and first-note approach,
gameplay smoke behavior including pause/resume, HUD/feedback, slide views, timestamped hold
recording, and authoring playback controls. Its pass does not supersede the three explicit
failures. EditMode tests were not rerun because no deterministic runtime/editor implementation
was changed; this task added and executed native PlayMode transport diagnostics.

Unity compiled the final fixture successfully. Logs contained no C# compilation errors,
unhandled exceptions, or WASAPI starvation reports. FMOD's missing-listener warning appeared
outside the fixture's active listener lifetime, and Unity logged a licensing access-token
refresh error before successfully running the tests. The runs are not described as warning-free.

After all test processes exited, the sole generated settings change (removing
`SENTIS_ANALYTICS_ENABLED`) was checked against the saved preflight bytes and restored.
SHA-256 comparison of all 689 pre-existing tracked files under `Assets`, `ProjectSettings`,
and `Packages` found no differences. No `Assets/_Recovery` directory was generated.
`preservation-final.json`, `hashes-before.json`, and `status-before.txt` retain this evidence.
The additions are the diagnostic source and its Unity-generated `.meta`, this record, and
the evidence link under `IT-P0-003`; the user's existing documentation edits remain preserved.

## Reproduction Commands and Artifacts

Artifacts are outside tracked project content:

`C:/Users/User/.codex/visualizations/2026/09/05/01a0716d-871d-7750-8838-7ecbe945251f/seek-verification-2026-09-06/`

Run with `C:/Program Files/Unity/Hub/Editor/6000.3.15f1/Editor/Unity.exe`:

```text
-batchmode -projectPath C:/Users/User/Idiot_Tape -runTests -testPlatform PlayMode
-testFilter IdiotTape.Gameplay.Tests.FmodSeekVerificationTests.LiveForwardAndBackwardSeekPreserveTransportAgreement;IdiotTape.Gameplay.Tests.FmodSeekVerificationTests.PausedSeekMovesTheFrozenClockAndResumesFromTheTarget;IdiotTape.Gameplay.Tests.FmodSeekVerificationTests.AuthoringLoopReentrySeeksToItsPrerollOnRepeatedCycles
-testResults <artifact-directory>/seek-diagnostics.xml
-logFile <artifact-directory>/seek-diagnostics.log
```

Pass the semicolon-separated filter as one argument. Launches use a hidden process, wait for
Unity to exit, and preserve its exit code in the matching `.exit.txt` file. Do not launch against
a project already open in another Unity Editor.

The final diagnostic command uses the same filter with `seek-final.xml` and `seek-final.log`.
The default-suite command omits `-testFilter` and uses `playmode-baseline.xml` and
`playmode-baseline.log`. Runtime source was identical across all three runs.

The diagnostics use NUnit `Explicit` because this reproduction-only change does not repair the
known runtime defect. They assert correct behavior and fail when explicitly selected; they do
not turn failure into a passing assertion. Default-suite passes therefore exclude these cases
and cannot establish Seek correctness. Promote the diagnostics to normal regression coverage
when the defect is repaired and their assertions pass.

## Remaining Limits

`IT-P0-003` remains `Implemented; verification pending`, with confirmed defects. Repair must
cover the live native transition, frozen paused time, resumed anchor, and authoring callers.

Not verified: interactive timeline clicks and automatic loop/input recording, note-view behavior
after a repaired Seek, source-PCM correlation, audible output timing, continuous full-song play,
and physical mobile hardware. This record is transport evidence, not a completed milestone gate.
