# Idiot_Tape — Authoring Stability Verification — 2026-09-04

> Status: Automated stability regressions passed; long-session and device verification pending
> Scope: Workspace navigation, authoring state, Undo, and playback commands
> Authority: Regression evidence; gameplay and chart-format contracts are unchanged

## Reported Failure and Reproduction

The user reported that `파트 개요` could be entered but `노트 편집` could not be restored.
Both buttons were independent toggles using the same pre-click boolean. On a return click, the
first toggle set Vertical, then the still-selected second toggle overwrote it with Horizontal.

Before the fix, an actual EditorWindow MouseDown/MouseUp regression failed on the first return
click: expected `Vertical`, observed `Horizontal`. The original render-only workspace checks set
the view directly, so they did not exercise this transition.

## Implemented Corrections

- A single toolbar owns the selected chart view and updates it once per interaction.
- The tempo tab preserves the author's foldout choice instead of reopening it on every repaint.
- Recording lifecycle resets discard unfinished hold/slide/banana input and stale input/drag
  state while preserving completed temporary records without quantizing them. Ordinary EditMode
  chart drags remain active. Fresh recording starts and window closure use the same reset.
- The playback button and Space use one command for stopped, playing, and paused audio.
  Text-field editing and tempo-tap handling retain their keyboard priority.
- Applying a buffer records chart changes, the buffer's applied state, selection, and optional
  buffer clearing in one Undo group. Undo and Redo restore both objects together.
- Opening banana handle/checkpoint details preserves valid boundary values. The numeric limits
  apply only when the author actually edits those fields.

The horizontal overview's clipped-row input was also reviewed. Two real-click tests outside its
viewport passed on the existing implementation, so no speculative Canvas change was made.

## Executed Verification

Environment: Unity 6000.3.15f1, Windows, uncommitted working tree. Existing unrelated changes were
preserved. Tests use temporary charts/windows and the existing FMOD Snow event; they do not save
changes to production chart assets.

| Run | Result |
|---|---|
| Original view-toggle reproduction | 1 expected regression failure before correction |
| Complete EditMode, 09:31:58–09:32:07 UTC | **202/202 passed**, 0 failed or skipped |
| Complete PlayMode, 09:34:04–09:34:09 UTC | **8/8 passed**, 0 failed or skipped |

The 14 additional EditMode cases cover:

- repeated real-click view switching, all inspector tabs with a selected buffer, tempo folding,
  and compact inspector/chart return;
- recording shutdown for hold/slide/banana, window closure, preservation of ordinary editor drags,
  and clicks outside a scrolled overview;
- apply/Undo/Redo with buffer retention and automatic clearing;
- actual banana-detail foldout clicks with near-boundary handle and checkpoint values, for both
  applied and temporary notes, while asserting unchanged chart and buffer data.

The new PlayMode test exercises Space start/pause/resume after Stop using prepared FMOD audio,
repeats the cycle, and checks that typing Space in a field does not consume the input or pause audio.
Both final Unity runs exited normally, with no new compiler errors or unexpected test logs.
An earlier PlayMode attempt lacked a listener in its temporary test setup; the fixture now supplies
its own FMOD Studio Listener, and the full suite was rerun successfully.

## Artifacts and Reproduction

Artifacts are under:

`C:/Users/User/.codex/visualizations/2026/09/04/01a06b4c-8abc-7143-9fb3-3c81f313484c/verification/`

- `workspace-stability-repro.xml` / `.log`
- `workspace-stability-integrated.xml` / `.log`
- `workspace-stability-playmode-final.xml` / `.log`

The runner uses `-batchmode -projectPath C:/Users/User/Idiot_Tape -runTests` with
`-testPlatform EditMode` or `PlayMode`. The initial reproduction additionally filters to
`ChartWorkspaceWindowTests.ViewButtonsSwitchBothWaysRepeatedlyThroughMouseClicks`.

Important implementation files: `PrototypeChartRecorderWindow.Workspace.cs`,
`PrototypeChartRecorderWindow.Inspector.cs`, and `PrototypeChartRecorderWindow.cs` under `Assets/Editor`.
Regression coverage is in `ChartWorkspaceWindowTests`, `ChartWorkspaceInputTests`,
`ChartWorkspaceDataSafetyTests`, and `ChartWorkspacePlaybackTests`.

## Remaining Verification

Not verified: long authoring sessions, measured production speed, every domain-reload preference
on a live user project, and physical-device touch. Lifecycle tests exercise the reset paths and
data preservation directly; they do not constitute a manual Play Mode exit/re-entry session.
