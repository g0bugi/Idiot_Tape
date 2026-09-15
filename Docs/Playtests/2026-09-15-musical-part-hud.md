# Persistent musical-part HUD — 2026-09-15

The upper-left gameplay label now follows activation windows at the existing FMOD
timeline position, independently of note judgements. GameplaySession owns
MusicalPartDisplayState; the HUD only presents changed labels with its existing
persistent entrance animation. Part IDs determine changes, in chart definition order.
Overlaps display together, gaps reconstruct the last active section, and attempt reset
clears both the state and label. Score and judgement rules are unchanged.

## Verification

Unity 6000.3.15f1 on Windows, batch Test Runner:

- EditMode filter `MusicalPartDisplayTests`: 1 passed, 0 failed. Exact boundaries,
  overlap, empty opening, gaps, jumps into gaps, adjacent same-part windows and reset.
- PlayMode filter `GameplayStartHudTests|GameplayResultsFlowTests`: 2 passed, 0 failed.
  Persistent label beyond the old fade duration, pause/resume, replacement and clear;
  actual Gameplay scene shows Drum before input, preserves it after a scored hit and
  missed note, and clears it on result retry. Existing start/results checks also pass.
- Unity compilation and `git diff --check` passed. No C# errors or exceptions found
  in the two run logs. Both Unity processes exited after their tests.

Commands use `-batchmode -projectPath C:/Users/User/Idiot_Tape -runTests
-testPlatform <platform> -testFilter <filter> -testResults <xml> -logFile <log>`.
Evidence: `C:/Users/User/AppData/Local/Temp/idiot-part-hud/`, files `edit.xml`,
`edit.log`, `play.xml`, `play.log`. Generated removal of the Standalone
SENTIS_ANALYTICS_ENABLED define was verified against the pre-run copy and restored.

Not verified: manual full-song visual review and physical mobile-device playback.
The PlayMode checks are automated; no new screenshot or device build was produced.
