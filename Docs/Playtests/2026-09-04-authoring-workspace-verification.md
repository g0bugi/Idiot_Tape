# Idiot_Tape — Authoring Workspace Verification — 2026-09-04

> Status: Automated verification and sampled native-window inspection passed; interactive and device verification pending
> Scope: Authoring workspace layout, interaction editing, preview geometry, and data preservation
> Authority: Verification evidence; this record does not redefine gameplay or authoring contracts

## Environment and Scope

- Backlog items: `IT-P0-009`, `IT-P0-018`; throughput hypothesis `H-AUTHOR-001` remains unmeasured.
- Build: uncommitted workspace based on `ad4376f`.
- Unity: `6000.3.15f1 (c1aa84e375f6)`, Windows 11 `10.0.26200` Core, x64.
- Runner: Unity Test Runner in Editor batch mode, plus native EditorWindow capture outside batch mode;
  no mobile build was exercised.
- Workspace content: generated test charts and temporary recording buffers, not a manually authored
  representative song section.
- Window sizes: render tests use 1200 x 800 and 700 x 600; pane-layout tests additionally cover
  999 x 720, 1000 x 600, and 1600 x 1000.
- Native-window backing scale: 1.5 (150%); full-size captures are 1800 x 1200 pixels and compact
  final compact captures are 1052 x 903 pixels after native window sizing.
- Physical refresh mode, input device, audio output route, and authoring-session duration: `Not recorded`.

The implemented layout has top recording/save controls, left musical parts, central `노트 편집` /
`파트 개요`, right `노트` / `파트` / `녹화` / `박자` / `도구`, and bottom temporary records with an
explicit `차트에 반영` action. Below 1000 pixels, the center switches between the chart and properties.

## Executed Automated Results

| Suite | UTC start and end | Result | XML duration |
|---|---|---|---|
| Complete EditMode | 2026-09-04 09:00:47–09:00:53 | **188/188 passed**, 0 failed/skipped/inconclusive | 5.722 s |
| Complete PlayMode | 2026-09-04 09:05:08–09:05:12 | **7/7 passed**, 0 failed/skipped/inconclusive | 4.418 s |

Both final logs report exit code 0. Compilation completed and the final logs contain no C# compiler
errors or test exceptions. Unity logged an unavailable licensing access-token update during startup;
local licensing still permitted compilation and successful test completion.

The workspace adds 10 window test cases and 16 canvas test cases to the 162-case EditMode regression
suite. The rendering tests create and show the actual EditorWindow, send Layout/Repaint events, and
yield through Unity frames. They assert data preservation and absence of unexpected test logs.
They do not inspect screenshot pixels or demonstrate that a person can comfortably use every control.

### Workspace Window Coverage

- Pane bounds and non-overlap across compact and full-width sizes, including the 1000-pixel switch.
- Both chart views and all five inspector tabs render without changing chart JSON, buffer JSON, or
  the buffer's applied state. Optional context, check markers, and expanded buffer details are shown.
- Tap, hold, slide, flick, and banana properties render without altering selected data; compact chart
  and properties modes also render with the expanded temporary drawer.
- Changing the selected musical part changes selection while preserving chart and buffer data.
- Editing an applied banana's curve handle, checkpoint, and combo value validates and survives a
  JSON serialization round-trip. Unity Undo restores the original chart without changing the buffer.

### Workspace Canvas Coverage

- Visible slide hold bodies remain selectable when their start is offscreen; transition connectors
  can be selected across their span in either timeline orientation.
- Slide display corners form timed steps without changing authored nodes or chart JSON.
- A chart with 12 input positions maps its first and last lane centers correctly.
- Quadratic Bezier, cubic Bezier, and linear banana previews match runtime evaluation, including
  nonuniform control times. Generated quarter-beat checkpoints and short-note fallback positions
  match the runtime curve.
- Long temporary-note lines clip to the visible area before dash drawing.
- Duplication previews move endpoints and nodes by musical time across a tempo change while
  preserving the source note.

### Regression Coverage

The remaining EditMode suite covers existing timing, judgement, slide transitions, chart data and
persistence, interaction recording, tempo/calibration, quantization, duplication, and geometry.
The seven PlayMode tests cover gameplay scene startup and runtime-note spawning, FMOD song
preparation, lane feedback, speed-slider endpoints, and runtime slide geometry on flat/curved
playfields and after timeline jumps. PlayMode success is not an end-to-end authoring session.

## Reproduction and Artifacts

The final runs used
`C:/Program Files/Unity/Hub/Editor/6000.3.15f1/Editor/Unity.exe` with `-batchmode`,
`-projectPath C:/Users/User/Idiot_Tape`, `-runTests`, and `-testPlatform EditMode` or `PlayMode`.
Each run supplied `-testResults` and `-logFile` paths under:

`C:/Users/User/.codex/visualizations/2026/09/04/01a06b4c-8abc-7143-9fb3-3c81f313484c/verification/`

- `workspace-editmode-final.xml` and `workspace-editmode-final.log`
- `workspace-playmode.xml` and `workspace-playmode.log`

Workspace assertions are in `Assets/Tests/EditMode/ChartWorkspaceWindowTests.cs` and
`Assets/Tests/EditMode/ChartWorkspaceCanvasTests.cs`.

## Executed Native-Window Inspection

Five captures of the actual EditorWindow with an in-memory sample chart were visually inspected:

- `native-workspace/workspace-wide.png`: fixed transport, musical parts, selected step slide,
  note properties, and separate buffer/apply and asset-save controls.
- `native-workspace/workspace-parts.png`: horizontal musical-part rows and full interaction paths.
- `native-workspace/workspace-banana.png`: complete banana curve and selected curve-handle editor.
- `native-workspace/workspace-compact.png`: compact chart, top controls, and expanded buffer.
- `native-workspace/workspace-compact-inspector.png`: properties replacing the central chart with
  a return action and an independent scrollbar.

The fixed panes and primary controls remain within the captured window. The slide retains each
position until a timed perpendicular transition; temporary records use hollow/dashed marks.
Long hold, slide, and flick instructions were changed to wrapped lines after the first visual pass.
The compact expanded buffer reduces the available chart height; collapsing it restores chart space.
These samples support layout and readability inspection, not a measured authoring-speed claim.

Artifacts are under the verification directory above. The final capture log is
`workspace-capture-verified.log`; `ChartWorkspaceWindowTests.CaptureWorkspace` creates only transient
test data and captures the Unity view's render surface at its own backing scale.

## Manual Follow-Up

Not verified: representative mouse/keyboard authoring, comfort across every properties tab and display
theme, measured production speed, and physical-device touch. Automated and sampled visual checks
do not replace an interactive recording/correction session.

1. Inspect full-width and compact windows with all tabs, both views, all five note types, and the
   expanded temporary drawer. Check legibility, overlap, scrolling, selected-item visibility, and
   banana handle/checkpoint correction.
2. Record and correct a representative section, apply by append and scoped replacement, undo,
   validate, save, and leave/re-enter Play Mode. Confirm temporary records and unsaved asset edits
   remain understandable and all required controls can be operated with mouse and keyboard.
3. Check dense musical parts, long/offscreen notes, stepped slides, banana curves, check/reward
   markers, and tempo-change duplication previews for useful visual distinction during playback.
4. Measure the complete authoring iteration for `IT-P0-009`, record repeated friction, and validate
   the resulting chart on the target mobile device. Physical-touch feel and the provisional slide
   transition allowance require separate player evidence.

`IT-P0-009` remains `Ready` because authoring throughput has not been measured. `IT-P0-018` remains
`Implemented; verification pending` because representative interactive and device checks are open.
