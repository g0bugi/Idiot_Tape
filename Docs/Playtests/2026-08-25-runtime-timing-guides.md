# Runtime Timing Guides — 2026-08-25

> Status: Passed in Unity Editor automation
> Unity: 6000.3.15f1
> Platform: Windows Editor, 1280×720 gameplay-camera capture
> Chart: `SnowPrototypeChart`
> Song event: `event:/Music/KIRARA/Snow`

## Scope

- Display chart tempo-map bar and beat guides during gameplay.
- Keep bar guides slightly thicker and more visible than beat guides.
- Curve each guide toward the judgement-line shape as it approaches.
- Remove each guide when its absolute beat time reaches the judgement line.
- Preserve authoritative FMOD song-time ownership.

## Evidence

- EditMode: 47/47 tests passed.
- PlayMode: 3/3 tests passed.
- The gameplay smoke test confirmed active bar and beat guide views and confirmed that the first
  bar guide no longer existed after its beat time passed.
- `PlayfieldGeometryTests` confirmed a guide starts flat and ends exactly on the judgement-line
  center and edge positions.
- `Logs/GameplayPreview.png` was captured from the gameplay camera after live FMOD playback and
  visually inspected. Beat guides were thin and subdued, bar guides were more legible, and the
  guides remained behind notes and hit feedback.
- Unity logs contained no C# compilation errors, null-reference exceptions, or assertion failures.

## Timing Review

- Guide scheduling uses `ChartTempoMap` absolute beat times.
- Per-frame guide position uses `beatTime - songTime` and the chart visual lead time.
- No accumulated `Time.deltaTime` movement or additional song clock was introduced.
- Restart clears active guide views and resets the timing-guide scheduler.

## Not Verified

- Not verified on physical mobile hardware.
- Not verified with a chart containing an actual tempo or time-signature change.
- Not verified through a full-song manual visual pass; automated PlayMode coverage exercised the
  opening gameplay interval.

