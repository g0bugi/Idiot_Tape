# Song library input/rendering repair — 2026-09-15

## Reported defects and corrections

The user's interactive screenshot and Editor.log exposed MissingComponentException on
MusicCurve, raised by GraphicRaycaster while processing pointer input. The runtime curve had
no CanvasRenderer. GameplayMenuCurve now requires CanvasRenderer and disables raycastTarget
in Awake because the curve is decorative. This also protects other runtime creation sites.

SongLibrary's overlay Canvas could draw without a camera, but the ordinary Game view displayed
"No cameras rendering". The existing preview listener root now also owns a URP-compatible
camera: Display 1, solid background, culling mask zero, no render texture. The existing flow
disables that root after Gameplay loads and restores it before Gameplay unloads. No temporary
test camera is needed for the real selection screen.

The FMOD Play-in-Editor platform's Overlay override was disabled. Live Update and other FMOD
platform/audio settings were preserved. Scene changes were made through SongLibrarySetup and
Unity serialization; existing scene/asset GUIDs were retained.

## Verification

Unity 6000.3.15f1, Windows. No live user Editor instance was interrupted.

- Scene setup compiled and exited normally, code 0 (`setup.log`).
- Five existing targeted Play Mode tests passed in `playmode.xml`: three library-flow cases,
  the results flow, and start-HUD controls. The new pointer case initially failed in batch mode
  because Game-view focus rules suppressed synthetic mouse input.
- The pointer test now clones InputSettings for the test only, permits synthetic input in the
  background, and restores the original settings/devices/preferences in finally. Production
  input/focus settings are unchanged. A test enum spelling error was corrected before rerun.
- The next batch run passed clicking, selection and dragging but could not produce a real display
  screenshot. It was not reported as a passing run.
- Final **ordinary Editor** run: `SongLibraryPointerTests` **1 passed, 0 failed** (`interactive.xml`).
  InputSystem mouse state events drive EventSystem/GraphicRaycaster/Button/ScrollRect normally;
  it does not invoke button callbacks directly. Checks include speed increment, Pluto/Snow
  selection, disabled empty-chart Play, 30-song dragging without accidental selection, input-field
  focus, Play button launch and Escape/count-in cancellation back to selection. Camera activation
  is asserted before gameplay, during gameplay and after return.
- The actual 2560×1440 display was captured with ScreenCapture, without adding a camera or
  switching the overlay Canvas to camera mode. Visual inspection confirms the curve is rendered,
  the logo is unobstructed and no missing-camera notice is present. Preview is intentionally off
  in this interaction fixture, so duration may show "길이 확인 전".
- Final runtime log checked for the reported missing-component/input exceptions and missing
  listener warning. No recurrence. Final diff, metadata and documentation links checked.

The earlier library screenshots were layout-only camera renders; they did not establish normal
interactive rendering or pointer handling. This record corrects that verification gap rather
than reinterpreting the previous test passes.

## Artifacts and reproduction

```text
C:/Users/User/.codex/visualizations/2026/09/12/01a09491-aa79-73a3-9272-94bfc14104c3/song-library-repair/
  setup.log
  playmode.xml / playmode.log
  pointer-final.xml / pointer-final.log  (batch capture failure)
  interactive.xml / interactive.log    (final pass)
  actual-library-screen.png            (native 2560×1440 display)
```

With `$repairEvidence` set to that directory, the final run used:

```powershell
$env:IDIOT_TAPE_REPAIR_EVIDENCE = $repairEvidence
$repairArguments = @('-projectPath', 'C:/Users/User/Idiot_Tape',
  '-runTests', '-testPlatform', 'PlayMode', '-testFilter', 'SongLibraryPointerTests',
  '-testResults', "$repairEvidence/interactive.xml", '-logFile', "$repairEvidence/interactive.log")
Start-Process 'C:/Program Files/Unity/Hub/Editor/6000.3.15f1/Editor/Unity.exe' -ArgumentList $repairArguments -WindowStyle Hidden -Wait
```

Use ordinary Editor mode when requesting the native screenshot. The optional screenshot path
is controlled by the environment variable; the pointer/camera assertions also run without it.

Not verified: physical mobile touch behavior or an Android/iOS build. No timing or scoring rules
were changed. Existing user authoring work and start/results changes were preserved.
