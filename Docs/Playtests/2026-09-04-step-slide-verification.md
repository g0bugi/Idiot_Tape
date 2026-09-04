# Idiot_Tape — Step Slide Verification — 2026-09-04

> Status: Automated verification passed; interactive and device verification pending
> Scope: Step-shaped slides, transition judgement, authoring path interaction, and terminal flicks

## Accepted Behavior

- `3 -> 7 -> 6` holds lane 3 until the lane-7 node time, then holds lane 7 until the lane-6 node time.
- Recording `3 -> 7 -> 6 -> 6` adds a final hold in lane 6 before normal completion.
- The editor and runtime draw the same timed steps. Reversing the time axis reverses their order;
  it does not create diagonal tracing. Curved playfield connectors follow one chart time.
- Ordinary lane changes use the current Good window, bounded by adjacent-node midpoints. Required
  checks wait for timely destination arrival; intermediate motion is permitted during that window.
- A terminal flick retains its motion requirement. Checks crossed during its final motion window
  wait for flick success; preceding nodes and checks before that window remain required.
- Existing authored times, node counts, IDs, and serialization stay intact. Render-only corners
  do not add chart nodes or rewards. Existing slide data now uses the step interpretation.

## Automated Coverage

The final Unity Test Runner runs executed the complete suites in Unity `6000.3.15f1`:

- EditMode: **162/162 passed**, zero failed or skipped.
- PlayMode: **7/7 passed**, zero failed or skipped, including three runtime slide-view tests.
- C# compilation succeeded. Final runner logs contain no C# compiler errors or test exceptions.
- `git diff --check` passed. Existing user changes were preserved; the unrelated scripting define
  changed automatically during Unity startup was restored to its preflight value.

Local runner evidence is under
`C:/Users/User/.codex/visualizations/2026/09/04/01a06b4c-8abc-7143-9fb3-3c81f313484c/verification/`:

- `step-slide-editmode-complete.xml` and `step-slide-editmode-complete.log`
- `step-slide-playmode-complete.xml` and `step-slide-playmode-complete.log`

Unity logged an unavailable licensing access-token update at startup; local licensing still
allowed compilation and both test runs to finish successfully.

Covered cases:

- Step targets immediately before, at, and after node times, including varying lane counts.
- Editor path topology in both timeline orientations and held-lane insertion without node collisions.
- Transition-window boundaries, same-lane nodes, dense transitions, and absolute-time offsets.
- Early/exact/late arrival, deferred events after frame hitches, handoff, reused contact IDs,
  historical ownership, and independent node/half-beat rewards.
- Terminal prerequisites, contact ownership, and quarter-beat checks during terminal flick motion.
- Runtime LineRenderer geometry on flat and curved playfields, restoration after timeline jumps,
  and final-transition arrow direction.
- Existing chart persistence, recorder, game-scene, FMOD playback, input feedback, and speed-slider
  regression coverage.

## Manual Follow-Up

Not verified: interactive chart-editor mouse operation, visual readability during live authoring,
and physical-device touch feel were not exercised by the automated suites.

1. **Authoring and shape:** Record `3 -> 7 -> 6 -> 6`; inspect both timeline orientations and the
   gameplay direction. Clicking a held segment should insert a same-lane node. Dragging a transition
   connector should move its destination node without adding a node. Check Undo and save/reload.
2. **Ordinary changes:** Try distant and dense transitions early, exactly on time, late, and beyond
   the deadline. Include finger handoff and a short release gap. A failure should produce one Miss
   and no later rewards.
3. **Endings:** Check a same-lane normal end, a normal end edited to another lane, and a terminal
   flick that crosses a quarter-beat check while the finger is between lanes. Confirm one end reward
   and failure for a missed prerequisite or invalid flick.
4. **Timeline stability:** Pause/resume, restart, and introduce a frame hitch around a transition.
   Confirm geometry restoration, inherited grade, and no missing or duplicate rewards. Repeat on
   the target mobile device to evaluate the provisional transition allowance.

This step-slide procedure supersedes the interpolated-lane insertion expectation in the earlier
`2026-09-01-non-banana-authoring-manual.md`. That earlier record remains preserved as its baseline.
