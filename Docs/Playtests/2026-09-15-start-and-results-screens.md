# Idiot_Tape — Start and Results Screen Verification

> Status: Passed automated scope; physical-device verification pending
> Date: 2026-09-15
> Authority: Evidence only

## Environment and scope

- Working tree based on `0ac1645`, including pre-existing, uncommitted chart-authoring work.
- Unity `6000.3.15f1`, Windows, Direct3D 11 / NVIDIA GeForce RTX 4060 Laptop GPU.
- Scene: `Assets/Scenes/Gameplay.unity`; FMOD event: `event:/Music/KIRARA/Snow`.
- The tests use temporary chart clones; the saved Snow notes, activation windows, and timing were not changed.
- Goal: apply the proposed start/results design, retain the shared start/settings flow, and
  verify end-of-song aggregation and result input isolation without changing judgement rules.

## Automated checks

Final Play Mode run: **6 passed, 0 failed**, normal test-runner exit code 0.

- `GameplayResultsFlowTests`: ready screen title and bundled Korean glyph; opening hit/miss;
  inactive-note exclusion; maximum combo after a miss; short chart waiting for the audio outro;
  no completion while paused; real audio-tail completion; stopped playback and frozen result;
  part selection and overall reset; retry count-in and cleared statistics; cancellation to ready;
  more than three parts; back button; empty-result display.
- `GameplayStartFlowTests`: queued start/input, opening approach/count-in, and restart behavior.
- `GameplayStartHudTests` and `NoteSpeedSliderTests`: shared slider, control visibility/locking,
  screen-coordinate mapping, and retained speed/preparation settings.
- `GameplaySceneSmokeTests`: Snow preparation/full-song duration and scene note spawning.

Final Edit Mode run: **105 passed, 0 failed**, normal test-runner exit code 0. The filter included
`GameplayPerformanceTests`, `GameplayStartPlanTests`, `JudgementEvaluatorTests`,
`NoteInteractionMathTests`, `GameplaySlideTransitionTests`, `MusicalPartActivationWindowTests`,
`NoteSpeedMathTests`, and `SongTimelineMathTests`. The new statistics tests cover per-part totals,
inherited rewards, bonus combo without invented judgements, reset, and older-chart metadata fallback.

No C# compilation errors or unexpected runtime exceptions were reported in the final runs.

## Reproduction and artifacts

Artifacts on the verification machine:

```text
C:/Users/User/.codex/visualizations/2026/09/12/01a09491-aa79-73a3-9272-94bfc14104c3/results-verification/
```

PowerShell commands, with `$evidencePath` set to that directory:

```powershell
$env:IDIOT_TAPE_RESULTS_EVIDENCE = $evidencePath
$unity = 'C:/Program Files/Unity/Hub/Editor/6000.3.15f1/Editor/Unity.exe'
$playArgs = @('-batchmode', '-projectPath', 'C:/Users/User/Idiot_Tape',
  '-runTests', '-testPlatform', 'PlayMode',
  '-testFilter', 'GameplayResultsFlowTests|GameplayStartHudTests|GameplayStartFlowTests|NoteSpeedSliderTests|GameplaySceneSmokeTests',
  '-testResults', "$evidencePath/playmode-final.xml", '-logFile', "$evidencePath/playmode-final.log")
Start-Process $unity -ArgumentList $playArgs -WindowStyle Hidden -Wait
$editArgs = @('-batchmode', '-projectPath', 'C:/Users/User/Idiot_Tape',
  '-runTests', '-testPlatform', 'EditMode',
  '-testFilter', 'GameplayPerformanceTests|GameplayStartPlanTests|JudgementEvaluatorTests|NoteInteractionMathTests|GameplaySlideTransitionTests|MusicalPartActivationWindowTests|NoteSpeedMathTests|SongTimelineMathTests',
  '-testResults', "$evidencePath/editmode.xml", '-logFile', "$evidencePath/editmode.log")
Start-Process $unity -ArgumentList $editArgs -WindowStyle Hidden -Wait
```

## Visual inspection and corrections

Both screens were rendered from the actual Unity camera at 1280×720, 1920×1080, 2340×1080,
and 1024×768. Captures are `01-start-<width>x<height>.png` and
`02-results-<width>x<height>.png`. The result screenshots use a deterministic display fixture
(428,800 score, 186 maximum combo) after the real lifecycle assertions; these are sample
values, not a recorded full-song human performance.

Inspection found the world-space judgement line sorting above the initial menu background.
The final menus temporarily raise the HUD canvas order; gameplay restores its serialized order.
The final captures show readable Korean text, separated score/part regions, visible controls,
the curved divider, and no overlapping labels at the tested sizes. Buttons use a shared
nine-sliced rounded sprite. Nanum Gothic (2,054,744 bytes) is bundled with its SIL OFL license;
its importer includes font data, so menus do not depend on an installed desktop Korean font.

The first two result-flow test attempts timed out while using live seek to jump to the audio
tail: the requested late target did not become the gameplay anchor (`time=6.89`, FMOD cursor
0, expected end 488.557). This is the separately documented live-seek issue, not a result-screen
path. The final test uses the existing scheduled nonzero-target API to reach the real event tail;
the ordinary start/count-in path is also exercised before that jump. Live seek was not changed.

## Limits

- Not verified on physical mobile hardware: touch feel, device safe-area behavior, and latency.
- Not verified: an uninterrupted eight-minute Snow performance through the result boundary;
  the automated end test schedules the audio tail to bound its running time.
- The screenshots check multiple camera target sizes; they do not substitute for a device build.
- Personal-best persistence, ranks, accuracy percentages, and progression are outside this change.
