# Idiot_Tape — Verification Baseline — 2026-08-26

> Status: Passed after follow-up fixes
> Date: 2026-08-26
> Backlog relation: `IT-P0-001`, `IT-P0-011`, `IT-P0-012`
> Hypothesis relation: `H-RHYTHM-001`, `H-MOBILE-001`

## Record Metadata

- Tester or observer: Codex batch verification
- Git commit: `6976c9a55ed4d5c33d9dc7244dd96ecb97e39118`
- Unity: `6000.3.15f1 (c1aa84e375f6)`
- Scene: `Gameplay`
- Chart asset: `SnowPrototypeChart`
- Song event: `event:/Music/KIRARA/Snow`
- Test environment: Windows Editor batch mode

## Goal

Establish the automated compilation and test baseline for the current repository HEAD before
starting further P0 milestone work.

## Preconditions

- the Git working tree was clean before Unity started
- no other Unity Editor process was using the project
- the repository-selected Unity version was available locally
- no source, chart, scene, or package changes were made before the test run

## Procedure

1. Run the complete EditMode suite in batch mode and write NUnit XML plus a Unity log.
2. Run the complete PlayMode suite in batch mode and write NUnit XML plus a Unity log.
3. Inspect failed test names, messages, and source locations.
4. Search both Unity logs for compiler errors, C# warnings, null-reference errors, missing-reference
   errors, assertion failures, and unhandled exceptions.
5. Check whether `Assets/_Recovery` or a retained `Temp/__Backupscenes` directory was created.
6. Review the Git working tree and restore the existing Standalone define that Unity removed during
   its batch-mode platform refresh.

## Expected Result

- Unity scripts compile without errors
- all discovered EditMode and PlayMode tests pass
- the Unity logs contain no new runtime or serialization errors
- no recovery scene is created

## Observed Result

### EditMode

- Result: `Pass`
- Discovered: 64
- Passed: 64
- Failed: 0
- Duration reported by NUnit: 0.278 seconds
- The batch run completed with Unity test-run exit code 0.

### PlayMode

- Result: `Fail`
- Discovered: 4
- Passed: 2
- Failed: 2
- Duration reported by NUnit: 4.890 seconds
- The batch run completed with Unity test-run exit code 2.

Passing PlayMode tests:

- `GameplaySceneSmokeTests.SnowEventPreparesAndReportsFullSongDuration`
- `LanePressFeedbackViewTests.FeedbackRemainsVisibleWhilePressedAndFadesAfterRelease`

Failing PlayMode tests:

1. `GameplaySceneSmokeTests.GameplaySceneStartsAndSpawnsRuntimeNotes`
   - failure: the expected combo text remained empty
   - the test waits until song time 1.47 seconds and submits lane 0
   - the current `SnowPrototypeChart` activation window begins at 2.079548 seconds, so the opening
     notes used by the hard-coded test target are inactive and cannot be judged
   - classification: stale chart-specific smoke-test assumption
   - follow-up: `IT-P0-011`
2. `NoteSpeedSliderTests.HudCreatesSliderAndMapsItsEndsToOneAndFour`
   - failure: `TrySetNoteSpeedFromScreenPosition` rejected the exact right endpoint
   - the current containment check and endpoint coordinate do not agree at the slider boundary
   - classification: input-boundary defect exposed by the newly discovered fourth PlayMode test
   - follow-up: `IT-P0-012`

## Automated and Build Checks

- Compilation: passed; gameplay, Editor, EditMode-test, and PlayMode-test assemblies compiled
- C# compiler errors: none found in either Unity log
- C# compiler warnings: none found in either Unity log
- EditMode tests: 64 passed, 0 failed
- PlayMode tests: 2 passed, 2 failed
- Unity runtime exceptions searched: none found outside the two NUnit assertion failures
- `Assets/_Recovery`: not created
- retained `Temp/__Backupscenes`: not present after the test process exited

## Issues Found

| Severity | Reproduction summary | Expected | Actual | Proposed backlog item |
|---|---|---|---|---|
| P0 test blocker | Run the complete PlayMode suite with the current Snow chart | Smoke input hits a playable note | Fixed 1.47-second input targets an inactive chart section | `IT-P0-011` |
| P0 input defect | Submit the exact right endpoint of the note-speed slider | Input is accepted and selects `x4` | Endpoint is rejected as outside | `IT-P0-012` |

## IT-P0-011 Follow-up Verification

The gameplay smoke test now selects the first future note for which
`PrototypeChart.IsNotePlayable` is true, waits for that note using FMOD song time, and submits the
matching chart lane. Its instrument assertion is also derived from the selected note's musical
part rather than a hard-coded label.

- Targeted PlayMode test: 1 passed, 0 failed
- Complete PlayMode suite: 3 passed, 1 failed
- Remaining PlayMode failure: `NoteSpeedSliderTests.HudCreatesSliderAndMapsItsEndsToOneAndFour`
- Complete EditMode suite after the change: 64 passed, 0 failed
- Compiler errors, compiler warnings, null-reference errors, and missing-reference errors: none
- `Assets/_Recovery`: not created
- retained `Temp/__Backupscenes`: not present after Unity exited

At this checkpoint, `IT-P0-011` was complete and the baseline remained pending only on
`IT-P0-012`.

The Snow chart's Drum activation start was also restored from the second-bar downbeat at
2.079548 seconds to the first-bar downbeat at 0.233293 seconds. The smoke test now explicitly
asserts that the chart's first note is playable, preventing the opening measure from becoming
inactive without detection.

## IT-P0-012 Follow-up Verification

The note-speed pointer conversion now accepts inclusive slider boundaries with a small coordinate
tolerance, clamps the local horizontal coordinate to the slider rectangle, and then maps that
value to the `x1` through `x4` range. Vertical and materially out-of-range initial presses remain
rejected.

The PlayMode test was also isolated from the previously loaded Gameplay scene. It now resolves the
slider and value label beneath the Canvas created by the test instead of using a global object-name
search that could select the gameplay scene's existing slider.

- Targeted note-speed PlayMode test: 1 passed, 0 failed
- Final complete PlayMode suite: 4 passed, 0 failed
- Final complete EditMode suite: 64 passed, 0 failed
- Compiler errors, compiler warnings, null-reference errors, and missing-reference errors: none
- `Assets/_Recovery`: not created
- retained `Temp/__Backupscenes`: not present after Unity exited

`IT-P0-012` and the automated `IT-P0-001` baseline are complete.

## Session Controls and Chart Save Follow-up

The complete PlayMode suite was rerun after extending the gameplay smoke test to exercise a
restart after a successful judgement and a pause/resume cycle. The restart check verifies that
FMOD playback remains running and unpaused, song time returns behind its pre-restart position,
score and feedback text reset, and runtime notes are rebuilt.

- Complete PlayMode suite: 4 passed, 0 failed
- Complete EditMode suite including chart persistence: 65 passed, 0 failed
- Playback reaches the chart target using authoritative FMOD song time: passed
- Pause sets the FMOD playback state to paused: passed
- Resume clears the paused state: passed
- Restart resets playback and gameplay session state: passed
- Compiler errors, null-reference errors, and missing-reference errors: none
- `Assets/_Recovery`: not created

The chart authoring window was opened with `SnowPrototypeChart`, its `에셋 저장` command path was
reviewed, and the asset was left with only the intentional first-bar activation change already in
the working tree. The button delegates directly to `AssetDatabase.SaveAssetIfDirty(chart)`.
A targeted EditMode test created a temporary chart asset, changed its lane count, saved it through
the same API, forced a synchronous reimport, and confirmed that the changed value survived. The
temporary asset and its meta file were removed by the test.

- Targeted chart-save EditMode test: 1 passed, 0 failed

### Pause/Resume Timing and Presentation Verification

The gameplay smoke test now captures the authoritative DSP-derived song time, the FMOD
millisecond timeline position, one active note, one bar guide, and one beat guide around a
pause/resume cycle.

Observed behavior:

- while paused for 0.45 seconds, authoritative song time stayed within 5 ms of its captured value
- after the pause transition settled, the FMOD millisecond timeline stayed within 10 ms across an
  additional 0.2-second wait
- the selected note, bar guide, and beat guide did not move while paused
- the number of active timing guides did not change while paused
- after resume, authoritative song time advanced by at least 120 ms before the deadline
- the FMOD millisecond timeline advanced again and was within 80 ms of authoritative song time
- the same note, bar guide, and beat guide all resumed movement
- the complete PlayMode suite passed: 4 passed, 0 failed
- a follow-up visual-evidence run passed: 1 passed, 0 failed
- measured pause-transition movement in the non-authoritative FMOD millisecond query: 150 ms
- measured FMOD millisecond query versus authoritative song time after resume: 22 ms
- captured paused and resumed game-camera frames both showed notes plus bar and beat guides; the
  paused frame showed the `PAUSED` overlay and the resumed frame returned to the pause-button icon

An initial diagnostic captured the FMOD millisecond timeline immediately after requesting pause
and observed an approximately 150 ms transition before that non-authoritative query settled. The DSP-derived
authoritative song time and all captured gameplay presentation remained frozen during that
transition. The rhythm-system contract explicitly does not use the millisecond timeline as the
per-frame gameplay clock.

## Result

- Outcome: `Pass`
- Hypothesis impact: Gate M1 now has a passing automated EditMode and PlayMode baseline
- Recommended decision: proceed to the manual full-song timing and transition checks in
  `IT-P0-002` and `IT-P0-003`
- Follow-up backlog IDs: none for the automated baseline

## Not Verified

- Not verified in interactive Unity Play Mode.
- Not verified through a full-song manual timing pass.
- Not verified on physical mobile hardware.

## Attachments

- generated EditMode result: `Logs/Baseline-20260826-EditMode.xml`
- generated EditMode log: `Logs/Baseline-20260826-EditMode.log`
- generated PlayMode result: `Logs/Baseline-20260826-PlayMode.xml`
- generated PlayMode log: `Logs/Baseline-20260826-PlayMode.log`
- targeted `IT-P0-011` result: `Logs/IT-P0-011-PlayMode.xml`
- targeted `IT-P0-011` log: `Logs/IT-P0-011-PlayMode.log`
- follow-up complete PlayMode result: `Logs/IT-P0-011-AllPlayMode.xml`
- follow-up complete PlayMode log: `Logs/IT-P0-011-AllPlayMode.log`
- follow-up complete EditMode result: `Logs/IT-P0-011-AllEditMode.xml`
- follow-up complete EditMode log: `Logs/IT-P0-011-AllEditMode.log`
- targeted `IT-P0-012` result: `Logs/IT-P0-012-Targeted.xml`
- targeted `IT-P0-012` log: `Logs/IT-P0-012-Targeted.log`
- final complete PlayMode result: `Logs/IT-P0-012-AllPlayMode-Final.xml`
- final complete PlayMode log: `Logs/IT-P0-012-AllPlayMode-Final.log`
- final complete EditMode result: `Logs/IT-P0-012-AllEditMode-Final.xml`
- final complete EditMode log: `Logs/IT-P0-012-AllEditMode-Final.log`
- session-control PlayMode result: `Logs/SessionControls-PlayMode.xml`
- session-control PlayMode log: `Logs/SessionControls-PlayMode.log`
- chart-save EditMode result: `Logs/ChartSave-EditMode.xml`
- chart-save EditMode log: `Logs/ChartSave-EditMode.log`
- session-control complete EditMode result: `Logs/SessionControls-AllEditMode.xml`
- session-control complete EditMode log: `Logs/SessionControls-AllEditMode.log`
- pause/resume final PlayMode result: `Logs/PauseResumeTiming-AllPlayMode-Final.xml`
- pause/resume final PlayMode log: `Logs/PauseResumeTiming-AllPlayMode-Final.log`
- pause/resume visual result: `Logs/PauseResumeTiming-Visual.xml`
- paused game-camera frame: `Logs/GameplayPaused.png`
- resumed game-camera frame: `Logs/GameplayResumed.png`

Generated logs remain outside version control.
