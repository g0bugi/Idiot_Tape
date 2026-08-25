# Idiot_Tape — Judgement and Combo HUD Feedback Check

> Status: Completed desktop validation
> Date: 2026-08-25
> Backlog relation: Direct user-requested readability refinement
> Hypothesis relation: `H-READ-001`

## Goal

Verify that combo and judgement feedback is larger, higher on the screen, and more rewarding than
the previous presentation without changing judgement timing or obscuring the judgement line.

## Build and Environment

- Git state: working tree with the HUD feedback change; no commit created
- Unity: `6000.3.15f1`
- Scene: `Gameplay`
- Chart: `SnowPrototypeChart`
- Song event: `event:/Music/KIRARA/Snow`
- Rendered comparison: 1280 x 720 PlayMode camera capture
- Validation environment: Windows desktop batch PlayMode

## Change Under Test

- combo moved from the lower judgement-line area to the upper-middle feedback area
- combo number increased to 88 reference-resolution pixels with a smaller `COMBO` label
- judgement text moved above the judgement line and increased to 72 reference-resolution pixels
- both feedback elements use a dark outline for contrast
- combo and judgement use a short rise and scale punch
- judgement colors distinguish `PERFECT`, `GOOD`, and `MISS`

## Procedure

1. Build the gameplay, editor, and PlayMode test assemblies.
2. Run `GameplaySceneSmokeTests` through Unity 6000.3.15f1.
3. Let the smoke test trigger the first playable note through the development lane input.
4. Capture the Gameplay camera after the HUD entry animation.
5. Compare the capture with the previous `Logs/GameplayPreview.png` image from 2026-08-24.
6. Inspect the Unity test result and generated log.

## Observed Result

- the combo number and label are clearly separated from the judgement line
- the judgement result is readable near the visual center without covering the hit position
- the yellow combo and cyan `GOOD` result remain legible on the black playfield
- the dark outline preserves the glyph shape against bright notes and effects
- the static captured frame shows a substantially stronger visual hierarchy than the previous HUD
- judgement timing, score, combo increment, instrument feedback, hit effect, line reaction, pause,
  and resume continued to pass the existing smoke flow

During verification, the smoke test exposed two pre-existing brittle assumptions: it required the
first hit to be `PERFECT` and expected a fixed prototype note ID while the Gameplay scene uses the
Snow chart. The test now accepts either valid hit grade and verifies pooled feedback by its runtime
name prefix, including inactive short-lived effects.

## Automated and Build Checks

- `IdiotTape.Gameplay.csproj`: passed, 0 warnings, 0 errors
- `IdiotTape.Gameplay.Editor.csproj`: passed, 0 warnings, 0 errors
- `IdiotTape.Gameplay.PlayModeTests.csproj`: passed, 0 warnings, 0 errors
- Unity PlayMode suite: 3 passed, 0 failed
- Unity result: `Logs/HudFeedbackAllPlayModeResults.xml` in the local generated logs
- rendered capture: `Logs/GameplayPreview.png` in the local generated logs
- `Assets/_Recovery`: not created

## Result

- Outcome: `Pass` for desktop static layout, contrast, and smoke behavior
- Hypothesis impact: improves the readability baseline for `H-READ-001`
- Follow-up: evaluate repeated-hit animation intensity and thumb-obstructed readability on a physical mobile device

## Not Verified

- not verified on physical mobile hardware
- not verified across multiple mobile aspect ratios or safe areas
- the scale-punch animation was not evaluated by an external player
- this check does not establish final HUD art direction, font, localization, or accessibility settings
