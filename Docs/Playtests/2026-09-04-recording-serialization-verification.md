# Idiot_Tape — Recording Serialization Verification — 2026-09-04

> Status: Automated regression checks passed; manual authoring verification pending
> Scope: Repeated sustained-note recording, musical-part preservation, and buffer apply safety
> Authority: Regression evidence; gameplay timing and chart-format contracts are unchanged

## Reported Failure

The author selected Synth and pressed lane `4` eight times, intending four separate holds starting
from bar 9. The screenshot showed three temporary slides at time zero, lane 1, part Drum, followed
by one Synth hold at approximately 14.964 seconds, lane 4. Buffer rows are sorted by start time;
their displayed `001`–`004` numbers do not establish input order. The screenshot does not expose
the surviving hold's end time, so its exact duration was not verified.

The original live buffer was unavailable because the user's Unity session had ended. No production
chart asset was modified for diagnosis, and no automatic reconstruction of the missing timestamps
or part assignments was attempted.

## Isolated Diagnosis Already Executed

Unity 6000.3.15f1 reproduced the failure in a separate project using the copied recording utility,
a field-compatible note-model stub, real EditorWindow serialization, `Undo.RecordObject`,
`Undo.FlushUndoRecordObjects`, and editor-update boundaries.

- Input: eight lane-index-3 events for `synth`, at 15, 16.85, 18.7, 20.55, 22.4, 24.25, 26.1,
  and 27.95 seconds.
- Inline serialization converted a null pending interaction into a default object during Undo.
  The sequence produced one Synth hold and three slides starting at zero, lane index 0, with no
  musical-part ID.
- A managed-reference comparison preserved null and produced four Synth holds. This comparison
  established the serialization mechanism; it is not the product fix or a product regression pass.
- In the product UI, an unresolved part lookup selected index 0 and wrote that selection back while
  displaying the row. Drum is index 0 in the user's chart. The note inspector shared this implicit
  assignment behavior.

Diagnosis artifact:

`C:/Users/User/.codex/visualizations/2026/09/04/01a06b4c-8abc-7143-9fb3-3c81f313484c/hold-diagnosis-repro/summary.json`

This reproduces the failure mechanism, not the exact contents or ending times of the user's lost
temporary records.

## Implemented Corrections

- Keep incomplete hold/slide/banana input outside Unity serialization and Undo. Only completed
  temporary records participate in buffer persistence and Undo/Redo. Lifecycle exits still discard
  incomplete interactions rather than inventing an ending.
- Preserve missing and unknown musical-part IDs while showing the temporary list or selected-note
  properties. The author must explicitly choose a valid part to change that reference.
- Validate temporary records and a separate complete candidate chart before applying. A rejected
  apply must leave source notes, activation windows, buffer contents, applied state, selection, and
  Undo history unchanged. Successful append/replacement and optional clearing remain one Undo group.
- Preserve replacement scope: all parts present in the buffer, with existing notes selected by
  start time in the half-open loop interval. The left-hand part selection does not narrow this set.

Implementation files: `PrototypeChartRecorderWindow.cs`, `PrototypeChartRecorderWindow.Inspector.cs`,
and `ChartActivationWindowUtility.cs` under `Assets/Editor`.

## Product Verification

Environment: Unity 6000.3.15f1, Windows, uncommitted working tree. The tests use temporary charts
and recorder windows; the original Snow chart asset's hash remained unchanged. The earlier
202/202 EditMode and 8/8 PlayMode stability runs did not cover the reported serialization failure;
the following results are from the corrected product.

| Run | Result |
|---|---|
| Complete EditMode, 11:55:54–11:56:03 UTC | **230/230 passed**, 0 failed or skipped; 9.0178171 seconds |
| Complete PlayMode, 12:01:41–12:01:54 UTC | **9/9 passed**, 0 failed or skipped; 12.8950887 seconds |

The final EditMode run compiled successfully and exited normally with code 0. Its 28 new cases
cover:

- seven recorder-serialization cases: eight lane-4 inputs across editor frames, completed
  hold/slide/banana persistence, recording Undo/Redo, incomplete-input discard, and clearing an
  empty buffer before the next hold;
- four actual-window part-identity cases: unresolved references in the temporary drawer and
  selected-note properties, applied-note cache preservation, unrelated field edits, and charts
  without defined parts;
- seventeen apply-safety cases: invalid hold/slide timing in append and replacement, missing or
  unknown parts, mismatched part/lane data, null records/path nodes, invalid enums or timestamps,
  invalid retained chart data, a valid scoped replacement with activation-window creation and
  Undo, and legacy tap records without full interaction data.

The first EditMode run passed 225/230. Five new JSON round-trip tests failed because their
temporary in-memory chart reference was not restored by the fixture. Reconnecting only that
fixture reference after deserialization corrected the test setup; the complete final suite then
passed. This was distinct from the product's null pending-interaction failure.

The new PlayMode case schedules the real Snow FMOD event at 16 seconds, bar 9 of a temporary
120 BPM chart, waits for scheduled playback to start, then supplies eight `Alpha4` key events
through the recorder's keyboard handler over more than eight seconds. It verifies four separate
Synth holds in lane 4, each spanning two seconds within the test's 80 ms frame tolerance, with
start/end times bounded by the actual song-clock readings around each input. It also checks
record count and pending state after every key, and that recording leaves the chart unapplied.
This is automated input through the FMOD-backed path, not a human keyboard or latency test.

The first PlayMode run passed 8/9: diagnostic logs showed song time zero for its first two
recording events, which were supplied before scheduled playback had started. The corrected
fixture waits for the scheduled start and actual song-clock progression. A subsequent run passed
the recording assertions but failed its final unexpected-log check on an FMOD command-buffer
growth warning. The fixture now limits its frame rate to 60, sets vSync to zero for the test, and
restores both values in cleanup. The final complete suite passed without adding log suppression.
The product's FMOD playback and timing implementation were not changed for these fixture
corrections.

Both final Unity runs exited normally with code 0 and no compiler errors or exceptions. The final
PlayMode log is not warning-free: an FMOD command-buffer growth warning remains in
`GameplaySceneSmokeTests.GameplaySceneStartsAndSpawnsRuntimeNotes` output, and the recording test's
output contains a listener warning, apparently during cleanup. The new recording test's assertions and
`LogAssert.NoUnexpectedReceived` check passed before cleanup. No `Assets/_Recovery` content was
created, no temporary test assets remained, and Unity's automatic project-setting change was
restored byte-for-byte to its pre-run value.

Artifacts are under:

`C:/Users/User/.codex/visualizations/2026/09/04/01a06b4c-8abc-7143-9fb3-3c81f313484c/verification/recording-fix/`

- `editmode-final.xml` / `editmode-final.log`
- `playmode-verified.xml` / `playmode-verified.log`

Regression sources are `ChartRecordingSerializationTests`, `ChartWorkspacePartIdentityTests`,
and `ChartRecordingApplyValidationTests` in `Assets/Tests/EditMode`, plus
`ChartRecordingPlaybackTests` in `Assets/Tests/PlayMode`.

## Remaining Verification and Recovery Limits

Not verified: a fresh manual Synth recording through the live FMOD input path, exact four-beat
duration in the user's original buffer, physical-device input, measured authoring throughput, and
all Unity domain-reload preference combinations.

The fix prevents new corruption. It does not infer the intended timestamps or part of an old,
already-labeled Drum record. Changing its part to Synth alone would not repair an incorrect
zero start time or slide path; the author must inspect the record or re-record it.
