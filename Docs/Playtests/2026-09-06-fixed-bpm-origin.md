# Fixed-BPM origin calibration and tempo pane verification

Date: 2026-09-06. Unity: 6000.3.15f1, Windows Editor.

## Change

The tempo pane defaults to preserving the selected chart's BPM while estimating its first
downbeat from numbered-bar taps. All raw taps contribute to the fixed-slope origin fit and
RMS residual. Manual A/B input and BPM estimation remain available. Fixed-BPM apply skips
writing the BPM field and uses the existing explicit apply, Undo, and save paths.
Inputs, wrapped summaries, and main actions fit inside the narrow inspector with vertical scrolling.

## Automated verification

Executable: `C:/Program Files/Unity/Hub/Editor/6000.3.15f1/Editor/Unity.exe`.
Final command arguments:

```text
-batchmode -projectPath C:/Users/User/Idiot_Tape -runTests -testPlatform EditMode -testFilter "IdiotTape.Gameplay.Tests.ChartTempoCalibrationTests;IdiotTape.Gameplay.Tests.ChartWorkspaceWindowTests;IdiotTape.Gameplay.Tests.ChartAuthoringMetronomeTests" -testResults C:/Users/User/tempo-fixed-render-tests.xml -logFile C:/Users/User/tempo-fixed-render-tests.log
```

Result: 36/36 passed, process exit 0. Coverage includes fixed 128 BPM with noisy taps,
all-sample origin and RMS, beat-unit handling, invalid tempo/time values, negative origins,
non-mutating window calculation, and existing calibration, metronome, and workspace regressions.
The initial run with `-nographics` passed 31 cases but failed five native window tests because
no graphics device was available. Removing that flag passed all cases. Initial diagnostics:
`C:/Users/User/tempo-fixed-tests.xml` and `C:/Users/User/tempo-fixed-tests.log`.

## Rendered inspection

With `IDIOT_TAPE_CAPTURE_DIR=C:/Users/User/tempo-fixed-capture`, executed:

```text
-projectPath C:/Users/User/Idiot_Tape -executeMethod IdiotTape.Gameplay.Tests.ChartWorkspaceWindowTests.CaptureWorkspace -logFile C:/Users/User/tempo-fixed-capture.log
```

Result: exit 0. Visually inspected `workspace-tempo-top.png`, `workspace-tempo-result.png`,
and `workspace-tempo-manual.png` in that directory. At a 1200x800 logical window size
(320px inspector), summaries wrap, fields and nudge buttons fit, and preview/apply actions
are fully visible at the bottom of the vertical scroll range. No horizontal scrollbar appeared.
Images use a temporary fixture, not the user's song data.

## Limits and preservation

Not verified: live Pluto tapping, human reaction/output phase, actual first-downbeat accuracy,
and audition/apply/Undo/save/replay through interactive Play Mode in this run.
No actual Pluto downbeat was measured or applied. Authored chart, scene, and audio data were
unchanged; the pre-existing untracked Pluto chart was preserved. ProjectSettings returned to
its preflight state after Unity finished. Final diff and whitespace checks passed.
