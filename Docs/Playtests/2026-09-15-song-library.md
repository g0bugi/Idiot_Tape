# Song library implementation and verification — 2026-09-15

## Scope and entry

Unity 6000.3.15f1, Windows Editor, working tree with pre-existing authoring and start/results
changes retained; no commit created. Open `Assets/Scenes/SongLibrary.unity` and enter Play Mode.
It is also the first enabled build scene. Gameplay direct entry remains available for authoring.

Implemented song/chart separation, explicit catalog enrollment, search/order/difficulty filtering,
chart selection, speed/count-in preferences, preview ranges, pooled rows, asynchronous cover
loading, additive gameplay launch, cancellation/failure recovery, retry and selection restoration.
Snow has 272 playable authored notes; Pluto's enrolled chart is empty and its Play button is
disabled. The older generated `PrototypeChart.asset` fixture is not enrolled or migrated.

Song assets own display/audio data. Existing chart GUIDs, notes and timing remain unchanged.
The local catalog retains lightweight chart data; audio events are prepared only for preview/
playback, and covers are loaded for the selected/visible set. The initial songs have no supplied
cover art and display typographic placeholders. No ranks, saved best scores, or difficulty ratings
were invented.

## Executed checks

- Full Edit Mode suite: **338 passed, 0 failed**, `editmode.xml`. Includes song identity,
  duplicate validation, shared metadata/legacy fallback, request snapshots, 3/30/300 song queries,
  existing chart-authoring and timing/judgement tests.
- Final broad Play Mode suite: **10 passed, 0 failed**, `playmode-final.xml`. Covers the existing
  standalone start, scene/audio, slider and results flows, plus the library's end-to-end path,
  range preview looping, virtualized rows and asynchronous artwork replacement/disposal.
- After removing a per-frame preview enumerator allocation and adding the menu/gameplay listener
  handoff, all **3 library tests passed again**, `playmode-listener.xml`. They assert that the
  library listener is disabled during gameplay and restored on return. The final log has no FMOD
  missing-listener warning. `setup-listener.log` records successful catalog/scene regeneration
  and normal batch exit 0. Final metadata, document-link and diff-whitespace checks passed.
- The preceding regression run also passed both `ChartAuthoringRecordingWorkflowTests` cases
  (`playmode-regression.xml`, 10 tests total in that run). These are additional authoring checks,
  not part of the final 10-test filter.
- Library flow: rapid Snow/Pluto/Snow selection; search/no matches; prepare/cancel; duplicate
  launch rejection; committed settings; results/retry; count-in cancel/return; selection/query
  restoration; a temporary second-song chart; invalid-chart preparation recovery. The second-song
  fixture is test-only and does not publish invented Pluto notes.
- Preview range: configure 30–31 seconds, verify the FMOD cursor enters that range and wraps,
  then stop and verify the prepared event is released. This uses scheduled starts, not live Seek.
- Artwork: import three temporary sprites, replace the target during an outstanding request,
  check that only the latest requested sprite is published, and dispose during a subsequent load.
  Temporary test assets are removed by the test.
- 3/30/300 song UI fixtures: **9 pooled rows** at the test viewport for all three sizes; scrolling
  reaches the last entry and filtering can locate Track 299. Editor managed-memory samples include
  preallocated fixtures and Editor overhead; they are not mobile memory or frame-time benchmarks.
- Unity renders inspected at 1280×720, 1920×1080, 2340×1080 and 1024×768. Selected and unplayable
  states remain readable without overlapping controls. Captures use the actual UI with a temporary
  camera target, not a browser reconstruction.

## Reproduction and artifacts

Evidence directory on this machine:

```text
C:/Users/User/.codex/visualizations/2026/09/12/01a09491-aa79-73a3-9272-94bfc14104c3/song-library-verification/
```

PowerShell, with `$songEvidence` set to that directory:

```powershell
$songUnity = 'C:/Program Files/Unity/Hub/Editor/6000.3.15f1/Editor/Unity.exe'
$env:IDIOT_TAPE_LIBRARY_EVIDENCE = $songEvidence
$songArguments = @('-batchmode', '-projectPath', 'C:/Users/User/Idiot_Tape',
  '-runTests', '-testPlatform', 'PlayMode',
  '-testFilter', 'SongLibraryFlowTests|SongArtworkLoaderTests|GameplayResultsFlowTests|GameplayStartHudTests|GameplayStartFlowTests|NoteSpeedSliderTests|GameplaySceneSmokeTests',
  '-testResults', "$songEvidence/playmode-final.xml", '-logFile', "$songEvidence/playmode-final.log")
Start-Process $songUnity -ArgumentList $songArguments -WindowStyle Hidden -Wait
$songArguments = @('-batchmode', '-projectPath', 'C:/Users/User/Idiot_Tape',
  '-runTests', '-testPlatform', 'EditMode',
  '-testResults', "$songEvidence/editmode.xml", '-logFile', "$songEvidence/editmode.log")
Start-Process $songUnity -ArgumentList $songArguments -WindowStyle Hidden -Wait
```

Catalog/scene setup uses `-executeMethod IdiotTape.EditorTools.SongLibrarySetup.Rebuild` with
`-batchmode -quit -projectPath ... -logFile ...`. In the Editor, the same command is available at
**Tools > Idiot Tape > Rebuild Song Library**. Only catalog refresh runs before a player build;
that callback does not open or save scenes. Add a chart to the catalog and rebuild after changing
its summary data. A cover resource path points to a Sprite beneath Resources, without extension.

The first setup attempt failed because Unity cannot add a scene beside an unsaved untitled scene.
The first runtime test then caught a missing catalog scene reference. Setup now selects the
appropriate scene-creation mode and resolves the catalog asset after scene opening; subsequent
setup and runtime checks passed. These failures were corrected, not counted as passing runs.
Log review also found an FMOD missing-listener warning in the initial library. The library now
owns a preview listener and hands off to the gameplay scene listener; the targeted rerun above
verified that correction. Unity-generated scripting-define changes were restored to the
preflight settings, while the intended first build-scene change was retained.

## Limits

Follow-up correction: an interactive Editor run exposed a missing CanvasRenderer on MusicCurve,
which broke GraphicRaycaster pointer processing, and a missing display camera. The tests above
invoked selection/start directly and injected a camera for layout captures, so their passing
results did not verify ordinary interactive Game-view input/rendering. The corrective evidence
is recorded in [song-library input repair](2026-09-15-song-library-input-repair.md).

- Not verified: physical mobile touch feel, device safe areas, audio latency, sustained frame
  timing and device memory. Editor rendering and bounded UI pools do not establish these.
- Not verified: an uninterrupted eight-minute Snow attempt. The existing result regression
  reaches the real audio tail through the scheduled-target API; the library test separately
  exercises result navigation using a controlled completed state.
- No Android/iOS player build was run. Local chart data is resident; asynchronous chart bundles,
  downloadable catalogs and large-library audio-bank packaging remain outside this increment.
- The known live-Seek issue remains separate; neither gameplay nor preview added a live-seek path.
