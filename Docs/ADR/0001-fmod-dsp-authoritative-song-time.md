# ADR 0001 — FMOD DSP Clock as the Prototype Song Timeline

> Status: Accepted for current prototype
> Date: 2026-08-25
> Decision scope: Rhythm timing and FMOD playback

## Context

The prototype plays songs through FMOD. Gameplay scheduling, note presentation, input judgement,
pause, resume, restart, seeking, and authoring must not drift onto separate clocks.

FMOD exposes a millisecond timeline position suitable for seek and audition UI and a DSP sample
clock suitable for high-precision progression.

## Decision

Use the selected FMOD event as the audio source and derive authoritative gameplay song time from an
FMOD DSP-sample anchor plus the captured FMOD timeline position.

Use the millisecond timeline position for seeking, audition UI, and anchor capture. Do not poll it as
the per-frame authoritative judgement clock.

Recapture the DSP/timeline anchor whenever playback starts, restarts, pauses, resumes, or seeks.

## Consequences

- gameplay must not run a parallel accumulated `Time.deltaTime` or Unity DSP song clock
- input timestamps require centralized conversion into the FMOD-backed song timeline
- authoring metronome clicks must use FMOD while Unity built-in audio is disabled
- replacing FMOD requires preserving the single-clock contract and migrating all timing consumers together

## Reconsider When

- the project intentionally replaces FMOD as the audio backend
- FMOD DSP timing cannot meet verified mobile requirements
- a supported platform requires a different clock integration

Reconsideration requires full-song, pause/resume, restart, hitch, and physical-device verification.

## Related Documents

- `../RHYTHM_SYSTEM.md`
- `../ARCHITECTURE.md`

