# ADR 0003 — Hidden Lane Grid as a Reversible Prototype Layout

> Status: Accepted for current prototype
> Date: 2026-08-25
> Decision scope: Initial spatial chart and input representation

## Context

Idiot_Tape must not assume that every chart uses a permanent traditional lane layout. The first
playable version still needs a simple, readable spatial representation that supports touch
judgement, keyboard development input, and fast chart recording.

## Decision

Use a chart-defined hidden lane grid for the current prototype.

The current sample uses eight positions. Lane dividers are not rendered. Runtime presentation and
touch judgement derive normalized horizontal positions from the chart's lane count. Number keys
`1` through `8` remain a development-only input and authoring path.

This decision does not establish eight lanes, fixed lanes, or lane indices as the permanent chart
coordinate system.

## Consequences

- the timing and judgement architecture can be validated without first finalizing freeform spatial data
- the chart authoring tool can record predictable keyboard positions
- runtime systems outside the explicit development keyboard path must not assume a universal count of eight
- future spatial experiments must preserve timing, note identity, and chart-data separation

## Reconsider When

- readability tests show that the hidden grid cannot express meaningful musical structure
- a validated chart requires movement or positions that lane indices cannot represent
- physical-device input testing demonstrates a different coordinate contract is necessary

## Related Documents

- `../GAME_DESIGN.md`
- `../CHART_FORMAT.md`
- `../PROTOTYPE_MILESTONE.md`

