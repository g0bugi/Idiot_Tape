# Pluto recording metronome verification

Date: 2026-09-07. Unity 6000.3.15f1, Windows, real FMOD playback and master-mixer PCM capture.
Chart reference: `Assets/Data/PlutoPrototypeChart.asset`, 128 BPM, 4/4,
first downbeat `0.5773379970760288`. Test input stays in a temporary recorder buffer on a chart clone.

## Reproduction and fixes

With ongoing recording clicks enabled, first recording from Beginning scheduled all eight
bar-1/bar-2 clicks and recorded four input notes. A second recording in the same window still
recorded four notes but scheduled none of those eight clicks. Count-in continued working.
The old `nextMetronomeSongTime` survived the take restart. Reset it when resetting the recording
session and starting standalone count-in (including short-loop reentry).

The original count-in also ended at `-0.360162...` but the next click was bar 1 at `0.577338...`:
the implied beat at `0.108588...` was missing. Extend the authoring metronome grid before bar 1
across audio time zero; keep this beat unaccented and preserve bar-1 identity. Runtime chart data
and judgement timing are unchanged. Pre-schedule the first recording click with count-in so a
Loop start exactly on a downbeat does not wait until activation and miss the minimum DSP lead.

## Play Mode and mixer evidence

Executable: `C:/Program Files/Unity/Hub/Editor/6000.3.15f1/Editor/Unity.exe`.
Final run sets `IDIOT_TAPE_AUDIO_EVIDENCE=C:/Users/User/pluto-metronome-complete` and uses:

```text
-batchmode -projectPath C:/Users/User/Idiot_Tape -runTests -testPlatform PlayMode -testFilter "IdiotTape.Gameplay.Tests.ChartMetronomeRecordingTests;IdiotTape.Gameplay.Tests.ChartRecordingPlaybackTests;IdiotTape.Gameplay.Tests.ChartWorkspacePlaybackTests" -testResults C:/Users/User/pluto-metronome-complete.xml -logFile C:/Users/User/pluto-metronome-complete.log
```

Result: 3/3 passed, exit 0. New regression runs Beginning twice and Loop twice through the real
recorder start/count-in/stop paths in one persistent playback/window fixture. Every take records
four notes and includes the nine expected positive-time clicks (pre-bar-1 beat plus eight chart
beats). Existing hold recording and playback transport regressions also pass.

Output directory contains four `.f32` stereo captures and clock metadata, `recording-clicks.txt`,
`pcm-click-analysis.json`, and normalized WAV copies for listening. Offline analysis uses captured
negative-time clicks as oscillator templates and matches them against the full song mix around
expected times. All nine expected clicks match in each final take; peaks are within two 48-kHz
samples of their grid positions. PCM callback/clock faults are zero and captured blocks are
continuous. This establishes mixer output timing, not speaker/headphone presentation latency.
Analysis script: `C:/Users/User/analyze_pluto_metronome.py`, run with the bundled Python runtime
against `C:/Users/User/pluto-metronome-complete`.

Initial failure artifacts: `C:/Users/User/pluto-metronome-before.xml`, `.log`, and corresponding
directory. The second take has no scheduled opening clicks; its PCM lacks the matching transients.
Intermediate directories `pluto-metronome-after` and `pluto-metronome-final` isolate the cursor
and pre-bar-1 fixes. An intermediate two-test fixture (`pluto-metronome-verified`) had passing
click/input assertions but a missing-listener warning between destroyed/recreated fixtures;
the final test keeps one listener, playback component, and recorder alive across all four takes,
matching the actual repeated-recording workflow, without suppressing logs.

## Remaining verification scope

Edit Mode regression run (exit 0, 31/31 passed):

```text
-batchmode -projectPath C:/Users/User/Idiot_Tape -runTests -testPlatform EditMode -testFilter "IdiotTape.Gameplay.Tests.ChartAuthoringMetronomeTests;IdiotTape.Gameplay.Tests.ChartTempoMapTests;IdiotTape.Gameplay.Tests.ChartWorkspaceWindowTests" -testResults C:/Users/User/pluto-metronome-edit.xml -logFile C:/Users/User/pluto-metronome-edit.log
```

This includes the delayed-first-downbeat interval, negative-grid accent phase, later tempo
section boundaries, and existing metronome/workspace checks. The test-generated scripting-define
change was restored to the clean preflight setting; final diff/whitespace review passed.

Not verified: human tapping feel, physical output latency, full-song/variable-tempo Pluto authoring,
or automatically reaching a loop boundary repeatedly in this run. Short-loop reentry uses the
same reset path; current tests explicitly restart Loop recordings. The first-downbeat value and
all existing authored chart/scene data remain unchanged. The new test does not apply or save notes.
