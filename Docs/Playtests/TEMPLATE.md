# Idiot_Tape — Playtest Record

> Status: Template
> Applies to: One manual validation session
> Authority: Evidence only; this file does not redefine game or technical contracts

Use only sections relevant to the session; omit unrelated device, rhythm, readability, or
authoring sections instead of filling them with placeholders. Retain metadata, goal/procedure,
expected/observed results, outcome, and material unverified checks. Use `Not recorded` for
missing relevant facts; never infer test success from an implementation or an earlier run.

## Record Metadata

- Date and local time:
- Tester or observer:
- Backlog item IDs:
- Hypothesis IDs:
- Git commit or build identifier:
- Unity version:
- FMOD integration and bank version, if relevant:
- Scene:
- Chart asset and song event:
- Test duration:

## Environment

- Device model:
- OS version:
- Display resolution and refresh mode:
- Input method:
- Audio output route:
- Headphones, speaker, Bluetooth, or other latency-relevant details:
- Target and observed frame-rate information:
- Development or release build:

Use `Not recorded` instead of guessing missing environment information.

## Goal

Describe the one main question this session is intended to answer.

## Preconditions

- required chart and song state
- relevant settings and offsets
- required build or scene setup
- known pre-existing issues that could affect the result

## Procedure

1. Describe reproducible actions.
2. Include song positions or musical sections when relevant.
3. Include restart, pause, hitch, or device conditions when relevant.

## Expected Result

State the observable result expected from the owning contract or backlog item.

## Observed Result

Record what actually happened. Separate direct observation from interpretation.

## Rhythm Evidence

Complete this section for timing-sensitive sessions.

| Checkpoint | Song position or musical section | Observation or measurement | Pass / Fail / Inconclusive |
|---|---|---|---|
| Beginning |  |  |  |
| Middle |  |  |  |
| End |  |  |  |
| After pause/resume |  |  |  |
| After restart |  |  |  |
| After frame hitch |  |  |  |

- Judgement offset and sign, if any:
- Input timestamp source:
- Suspected input latency:
- Suspected output latency:
- Persistent drift observed:

## Readability and Musical-Part Evidence

- Could the player anticipate recurring spatial patterns?
- Could the player distinguish active and inactive notes without relying only on color?
- Which musical-part changes did the player notice?
- Could the player explain why misses occurred?
- Which sections became visual noise?
- Direct player wording, kept brief:

## Authoring Evidence

Complete this section for chart-tool sessions.

- Section authored or corrected:
- Musical duration or bar count:
- Total elapsed authoring time:
- Number of play-edit-replay iterations:
- Most expensive repeated action:
- Validation errors encountered:
- Data loss, Undo, save, or buffer issues:

## Automated and Build Checks

List only checks actually run.

- Compilation:
- EditMode tests:
- PlayMode tests:
- Build:
- Unity Console:

## Issues Found

| Severity | Reproduction summary | Expected | Actual | Proposed backlog item |
|---|---|---|---|---|
|  |  |  |  |  |

## Result

- Outcome: `Pass`, `Fail`, or `Inconclusive`
- Hypothesis impact:
- Recommended decision:
- Follow-up backlog IDs:

## Not Verified

List relevant checks that were not performed and why.

## Attachments

- logs:
- screenshots or video:
- profiler capture:
- build artifact:

Do not commit large generated captures without an intentional repository-storage decision.

