# Authoring selection, take retry, and workflow simulation — 2026-09-12

## Scope and environment

The user requested the first authoring improvement bundle: multi-note selection/correction,
last-take retry, and beat-step editing shortcuts, followed by simulations of note creation and
correction workflows. The base commit was `0ac1645`; the preflight working tree was clean.
Unity `6000.3.15f1` on Windows was used. Production Snow/Pluto charts and scene references are
preserved; simulations use in-memory fixtures or uniquely named temporary test assets.

The implementation includes mixed temporary/applied selection, one-operation Undo, path-preserving
time/lane/part translation, non-mutating preview, and out-of-range rejection. Positive recording
pre-roll and automatic loop re-entry now use scheduled FMOD starts so retry does not inherit the
known live audition Seek anchor defect. General live/paused audition Seek is unchanged.

## Comparison method

The correction comparison replays both workflows against the same updated build and the same
12-note fixture. The old workflow uses the retained single-note millisecond buttons: select each
note and click `뒤로`, with the nudge prepared as 125 ms. The new workflow selects the current
part with Ctrl+A and moves it one 1/16-note grid step with Down at 120 BPM, 4/4. Setup is excluded
from both measurements; both must produce the same final note times.

Each click, keyboard chord, or drag is one logical input action; raw input-event counts are also
recorded. The fixture sends real `EditorWindow.SendEvent` IMGUI input through the rendered editor.
The stopwatch measures automation execution, not human recognition, decision, pointing, or
musical listening time. Results must not be presented as measured human authoring speed.

Additional simulations cover Shift-click and rectangle selection across temporary/applied notes,
numeric-field keyboard ownership, wide/compact views, and complete hold/slide/banana movement
through Unity Undo/Redo and asset save/reload. Method-level checks are labeled separately from
GUI input. Real FMOD PlayMode scenarios record all five note types, preserve an earlier take,
retry a later current-position/loop take, and apply/save/reload the resulting chart.

## Execution evidence

Artifacts live outside tracked project content:

`C:/Users/User/.codex/visualizations/2026/09/12/01a09432-dba1-7b82-ac37-50b4768ddfce/authoring-improvements/`

Set `IDIOT_TAPE_AUTHORING_EVIDENCE` to that directory. Run the project-version Unity executable
with `-batchmode -projectPath C:/Users/User/Idiot_Tape -runTests -testPlatform EditMode|PlayMode`,
one platform per process, and explicit `-testResults` / `-logFile` paths. Each process exit code
is recorded in the corresponding `.exit.txt`. Test XML and `workflow-simulation.tsv` provide
per-case results and measurement scope.

The initial compilation exposed three malformed GUIDs in newly added `.meta` files; those new
GUIDs were regenerated before further testing. No pre-existing asset GUID was changed.
The failed import/compilation log is retained as `editmode-initial.log` (exit 1, no tests run).

The corrected complete EditMode suite passed **328/328**, with no skipped cases, exit 0,
13.583496 seconds (06:59:22–06:59:36 UTC). Artifacts: `editmode-second.xml`, `.log`, `.exit.txt`.

The final complete EditMode suite passed **328/328**, with **0 failures and 0 skipped cases**,
exit 0, in **12.0602296 seconds** (07:07:42–07:07:54 UTC). Artifacts: `editmode-final.xml`,
`editmode-final.log`, `editmode-final.exit.txt`. This run includes the final replacement of repeated
applied-note `IndexOf` searches with a dictionary lookup during grouped transformation.

The table uses the latest completed row for each scenario in the append-only
`workflow-simulation.tsv`; earlier measurement and failure rows remain available there.

| Executed simulation | Result |
|---|---|
| Twelve taps: retained individual selection + millisecond correction | 24 input actions / 48 events; 610.110 ms automation |
| Same twelve taps: select current part + one grid move | 2 input actions / 4 events; 51.284 ms automation; identical final timing |
| Mixed applied/buffer Shift-click, time move, additive rectangle, lane move | 6 actions / 13 events; 79.930 ms automation; unselected note and buffer destinations checked |
| Numeric field focus and wide/compact repaint | 6 actions / 12 events; 755.630 ms automation; keyboard stayed in the field; selection/data preserved |
| Hold/slide/banana grouped correction, Undo/Redo, saved-asset round trip | Passed method-level and actual Unity persistence checks; 696.923 ms automation |
| Later current-position take retry, earlier-take preservation, apply/save/reload | Passed with real FMOD; 16 GUI actions / 32 events; 29245.336 ms automation; start/stop/retry commands were method-level |
| Later loop take retry, automatic re-entry, earlier-take preservation, apply/save/reload | Passed with real FMOD; 9 GUI actions / 18 events; 17768.585 ms automation; start/stop/retry commands were method-level |

The measured twelve-note correction uses 22 fewer actions (**91.7% fewer**) for this prepared
scenario. It does not estimate total chart-production speed or compare two different Unity builds.
All measurement rows are retained in `workflow-simulation.tsv` with their evidence scope.

The first complete PlayMode run produced 14 passes, one failure, and three explicitly excluded
Seek diagnostics (exit 2). The failure was an FMOD missing-listener warning between the two new
fixtures, after the loop scenario's recording, retry, apply, and save checks had executed. The
fixture listener lifetime was extended across the PlayMode run instead of suppressing the warning.
Artifacts: `playmode-initial.xml`, `.log`, `.exit.txt`; 128.7751172 seconds.

The final complete PlayMode run passed all **15 enabled tests**, with **0 failures and 3 Explicit
skips**, exit 0, in **128.6103029 seconds** (07:04:38–07:06:47 UTC). Artifacts:
`playmode-final.xml`, `playmode-final.log`, `playmode-final.exit.txt`. Both new later-target retry
scenarios passed, including automatic loop re-entry, preserved earlier input, and chart
apply/save/reload. The skipped diagnostics are the existing explicit authoring-loop seek,
live forward/backward seek, and paused-seek checks in `FmodSeekVerificationTests`; they did not pass
in this run because they were not executed.

After that PlayMode run, the only code change was the applied-note lookup replacement described
above. The final 328-test EditMode run covers that last change. PlayMode was not rerun after the
lookup-only edit, so its recorded result applies to the preceding build; no recording or playback
code changed between those builds.

## Rendered and native UI verification

A non-batch Unity session ran
`-executeMethod IdiotTape.Gameplay.Tests.ChartAuthoringWorkflowSimulationTests.CaptureSimulationWorkspace`
with the same project, evidence-directory environment variable, and `-logFile` pointing to
`visual.log`. It rendered a synthetic in-memory 12-tap chart at 1200×800 and 700×600.
The three captured images were opened and visually inspected:

- `workflow-wide.png`: four selected notes, shared-grid buttons, bulk offsets, part choice,
  preview/apply, and the transport retry button are visible without clipping.
- `workflow-compact.png`: the transport wraps into the compact layout; retry remains visible
  and the canvas retains its part selector and properties toggle.
- `workflow-compact-inspector.png`: the full group inspector fits in the compact properties
  view, including the return-to-chart button and preview/apply controls.

Native Windows mouse and keyboard input then exercised the actual rendered Unity utility window:
clicking the first note opened its single-note properties at 1 second; Ctrl+A displayed
`12개 선택`; Down moved the group by one grid step; Ctrl+Z restored the preceding positions;
an empty-space rectangle enclosing the first four notes displayed `4개 선택`. These observations
supplement the numerical assertions in the automated tests. They are not a human authoring-time
measurement. The temporary window and this task's Unity session were closed normally afterward.

## Preservation and log review

The six original chart-data and scene assets match the preflight SHA-256 hashes. Existing asset
GUIDs are unchanged, all eight new `.meta` GUIDs are valid, and no temporary `__Authoring*` folder
or `Assets/_Recovery` remains. ProjectSettings matches its preflight bytes after the rendered
Editor session restored the scripting define removed by batch initialization.

Final batch logs contain no test failure or C# compilation error. Unity licensing startup messages
occurred before successful test execution. The rendered session also logged one
`Attempted to call .Dispose on an already disposed CancellationTokenSource` line among Android
extension device scans, without a stack trace; its source is not established. No authoring
exception occurred during the exercised UI actions. Logs are retained rather than described as
warning-free. No commit was created.

## Limits

These simulations do not establish the time needed for a person to compose and refine a
representative musical section. The full `IT-P0-009` authoring-throughput gate remains open.
Physical touch feel, display/speaker latency, complete-song stability, and general live/paused
audition Seek repair are outside this verification. Automated temporary-asset round trips are
separate from a human-driven Play Mode exit/re-entry workflow.
