# Idiot_Tape — Start Sync Regression Verification — 2026-09-04

> Status: Automated tests, sampled PCM output, visual review, and preservation checks passed; physical-device and full-song verification outstanding
> Scope: Scheduled-start compatibility with existing playback/recording timing, including nonzero targets
> Authority: Verification evidence; supersedes the synchronization conclusion of the earlier start-flow record

## Finding

The user reported that music played ahead of the chart after the explicit-start/count-in change.
The new scheduled path compensated FMOD Studio's native startup delay while ordinary Restart
and authoring recording retained their existing timebase. That made music approximately 149 ms
earlier relative to existing authored timing. The measured value describes this environment; it
is not a calibration constant to add to code or chart data.

The previous [start-flow verification](2026-09-04-gameplay-start-flow-verification.md) passed
268 EditMode and 11 PlayMode tests. Those tests compared the cached raw FMOD Studio timeline with
the new signed clock but did not inspect decoded leaf-channel positions or compare actual output
phase against the established Restart/authoring baseline. The later diagnostic leaf-position
checks also could not establish output-phase compatibility by themselves. Those historical pass
counts do not represent verification of this correction.

## Output Evidence

Artifacts are under:

```text
C:/Users/User/.codex/visualizations/2026/09/04/01a06b4c-8abc-7143-9fb3-3c81f313484c/verification/start-sync-fix/
```

The corrected captures in `output-final/correlation-results.txt` show that the approximately
149 ms early-music regression is removed. Ordinary Restart and the corrected schedule retain a
measured difference of approximately 21.33 ms, with scheduled content later. This is a residual
backend buffer-phase difference, not exact zero-offset agreement or a physical-latency result.

| Corrected capture | Requested delay | Source position relative to chart clock, both windows |
|---|---|---|
| Ordinary immediate Restart baseline | Immediate | −149.333 ms |
| Scheduled target zero | 2 seconds | −170.667 ms |
| Scheduled target zero, repeated | 3.8 seconds | −170.667 ms |
| Scheduled 60% target, 293.1342 seconds | 2 seconds | −170.833 ms |
| Scheduled 90% target, 439.7013 seconds | 0.1 seconds, extended to backend minimum | −170.917 ms |
| Scheduled target zero | 0.1 seconds, extended to backend minimum | −170.667 ms |

The five scheduled conditions agree within approximately 0.25 ms, including nonzero target
rounding to FMOD's millisecond seek representation. Every capture reports zero callback faults
and clock failures, with no DSP clock gaps. For the three long preparations, a pilot scheduled
at −1 second appears at −0.999979 seconds, one 48 kHz sample from its intended position. The
pilot independently checks captured-clock alignment; it does not cancel the music's native delay.

The following earlier captures establish why the correction was needed:

`output-schedule-order/correlation-results.txt` compares captured master-mix PCM with the original
Drums waveform. Positive `source_ahead_seconds` means source content is ahead of chart time;
negative means behind. Two windows were measured per capture: 0.35–1.25 and 1.6–2.7 seconds after
the scheduled origin.

| Earlier implementation / capture | Source position relative to chart clock | Interpretation |
|---|---|---|
| Ordinary immediate Restart | −149.333 ms in both windows; correlation approximately 0.935 | Existing playback/recording phase |
| Compensated scheduled start, target zero, two starts | 0 ms in both windows; correlation approximately 0.934–0.936 | Music advanced approximately 149 ms relative to Restart |
| Compensated schedule after target-order correction, 60% target | −0.167 ms in both windows | The nonzero target plays the intended source segment |
| Compensated schedule after target-order correction, 90% target | −0.250 ms in both windows after narrowing the search | Repeated musical material requires checking the expected local match |

These captures isolate the regression and command ordering; they predate the timebase compatibility
correction. They are not its final acceptance results. `native-immediate.jsonl` independently
reproduces the ordinary Restart transport and its native startup phase.

The test-only capture DSP passes every master channel through unchanged and records front L/R
before final output downmix. The observed master input had six channels even though the selected
output was stereo. Captures contain preallocated floating-point PCM plus per-block DSP clocks,
signal offsets, lengths, and input-channel metadata. No callback logging, file I/O, or Unity calls
are used. The original stereo-only attempt captured no samples and was corrected before the
waveform evidence above; those unusable files are not evidence of synchronization.

The Drums stem is isolated for comparison with its source WAV, resampled to the capture rate for
correlation. The measured captures have no callback faults or clock failures and contiguous DSP
clock steps. Repeated musical phrases can produce a stronger correlation one bar away in a broad
search; results must be checked in local windows around the expected source position. This is
master-mix output evidence, not a microphone, speaker, display, or physical-input latency test.

## Correction

- Restore the default Studio scheduling property and anchor scheduled chart time at the same Core
  gate used for the event. Remove the scheduled-only native-delay compensation so starting through
  preparation preserves the established playback/recording phase.
- Read native streaming settings and DSP/Studio buffering only to establish minimum scheduling
  lead: one preparation budget plus buffer/update lead. Extend very short requests when needed;
  ordinary bar-based delays remain data driven. Callers use the returned actual anchor.
- Start the event paused, then apply a nonzero timeline target before flushing and setting the
  future Core gate. Previously setting the target while stopped allowed Studio's later unpause to
  replace that gate. A two-second nonzero preparation exposed approximately 1.638 seconds of early
  playback; the earlier 0.1-second test expanded to backend minimum lead and hid the large error.
- Keep the chart's note times, tempo map, first-downbeat offset, and stem metadata unchanged.
  Preparation still uses the selected chart and note speed, with no song identity or measured
  latency constant in the implementation.

`native-scheduling-findings.md` and the `native-matrix-*.jsonl` artifacts record five native
target/delay conditions and the paused-start ordering comparisons. `native-compatibility-final.jsonl`
checks the corrected phase convention in a separate native process. These transport experiments
support the correction; corrected Unity output is recorded above and complete regression suites
remain a separate gate.

## Final Verification Status

| Gate | Status |
|---|---|
| Confirm phase difference between ordinary Restart and compensated scheduled output | Confirmed by PCM correlation |
| Confirm nonzero scheduled gate replacement and corrected command order | Confirmed by native transport comparisons and sampled source PCM |
| Complete corrected PlayMode suite | **12/12 passed**, 0 failed or skipped; 49.3860607 seconds; Unity exit code 0 (`playmode-complete.xml/log`) |
| Complete corrected EditMode suite | **268/268 passed**, 0 failed or skipped; 10.696882 seconds; Unity exit code 0 (`editmode-complete.xml/log`) |
| Corrected output phase versus ordinary Restart, repeated zero starts | Verified; early regression removed, approximately 21.33 ms residual phase difference retained |
| Corrected output phase at 60% and 90%, including short and longer preparation | Verified in the five-condition matrix above; approximately 0.25 ms spread |
| START/count-in presentation and first-note approach after the correction | Root visually reviewed all three final captures; SPEED mouse click/drag assertions passed |
| Final chart/settings preservation and diff review | Passed; current chart SHA and original project-setting bytes preserved; `git diff --check` exit code 0 |

Final UI captures are `ui-final/01-ready.png`, `02-opening-note-at-top.png`, and
`03-opening-note-approaching.png`. The review confirms the ready controls, separate COUNT-IN
choice and SPEED control, first note at the entrance followed by downward approach, and the
small READY counter below the judgement line.

The first complete corrected PlayMode attempt (`playmode-final.xml`) passed 11 of 12 tests.
The remaining start-flow test completed its behavioral assertions but failed its final log check
on one WASAPI starvation warning. Test-only capture metadata memory was reduced and diagnostic
logging moved outside the DSP lock; the complete rerun without heavy PCM capture passed 12/12.
PCM acceptance comes from the separate `output-final` captures, not from disabling capture in
the complete regression run.

Both final logs contain no C# compiler errors, unhandled errors, or exceptions. The final PlayMode
run has no WASAPI starvation warning; one existing FMOD Studio Listener warning occurred during
cleanup. The result is not described as warning-free.

The regression baseline chart SHA-256 in `hashes-before.json` is
`E3B12292DE5EF8A1B526FCD3A389C97EDC0C3A8C641AE6DB56E0021C408E58AF`.
This is the user's current chart, distinct from the earlier start-flow baseline. Final preservation
was checked against this baseline rather than restoring the older chart. `preservation-final.json`
confirms that both the chart hash and the original project-setting bytes match. After every Unity
process exited, the test-created SENTIS define change was identified and the settings backup was
restored byte for byte. Final review found no changes under ProjectSettings, Scenes, FMOD, or
Packages, and no `Assets/_Recovery` directory. The production diff was reviewed by the parent task.

## Remaining Limits

The separate existing live `Seek` finding remains open: seeking near 293.134 seconds could capture
a stale clock origin and leave the song clock near 6.027 seconds five seconds later. The earlier
explicit-target experiment was reverted. Correcting nonzero `SchedulePlay` ordering does not prove
live or paused `Seek` correct; follow-up work must define and verify that transition contract.

Not verified: physical-device audio/display/input latency, human audible timing assessment after
the correction, a continuous full-song playthrough, and playback of every song. Generic chart and
backend configuration are implementation properties, not evidence that every track or device has
been measured. No zero-latency claim is made.
