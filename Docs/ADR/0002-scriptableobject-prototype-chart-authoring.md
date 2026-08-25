# ADR 0002 — ScriptableObject Chart Data for the Current Prototype

> Status: Accepted for current prototype
> Date: 2026-08-25
> Decision scope: Chart authoring representation

## Context

The project needs fast in-Editor chart iteration while the permanent serialization format and
production content pipeline remain unresolved.

Core chart data must remain separate from runtime note GameObjects and must be selectable without
changing the gameplay scene or hard-coding a song path in playback code.

## Decision

Use `PrototypeChart` ScriptableObject assets as the current authoring and runtime-loading
representation.

Store absolute note times, chart-defined lane count, musical parts, activation windows, tempo
sections, FMOD event path, and optional stem mappings in the chart asset as currently implemented.

Do not treat ScriptableObject as the guaranteed permanent production format.

## Consequences

- chart changes participate in Unity serialization, Undo, asset saving, GUID references, and merge behavior
- authoring tools must not mutate runtime note GameObjects as the chart source of truth
- schema-breaking changes require explicit migration care even before an external file format exists
- a later JSON, binary, or converted runtime format must preserve stable timing and chart identity semantics

## Reconsider When

- chart collaboration or source-control merging becomes a measured bottleneck
- external authoring or user-generated charts become a real requirement
- runtime loading or build size demonstrates a need for preprocessing
- the current authoring representation prevents a validated gameplay requirement

## Related Documents

- `../CHART_FORMAT.md`
- `../CHART_AUTHORING.md`

