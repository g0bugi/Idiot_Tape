# Idiot_Tape — Documentation Guide

> Status: Current
> Last reviewed: 2026-08-25
> Applies to: The current Unity prototype
> Authority: Documentation routing and precedence

## Purpose

This file explains which project document to read and what kind of decision each document owns.

The documents are intentionally separated by responsibility. A backlog item does not override a
game-design or timing contract, and a temporary implementation detail does not silently become a
permanent design decision.

## Authority Order

For a development task, use the following order:

1. the user's current request defines the requested change
2. accepted design and technical contracts define the intended behavior
3. the current repository defines the implementation that must be preserved or migrated
4. the current milestone and backlog define delivery priority and task scope
5. playtest records and ADRs provide supporting evidence and decision history

If these disagree, report the discrepancy and make the smallest change required by the task.

## Document Map

| Document | Responsibility | Read when |
|---|---|---|
| `GAME_DESIGN.md` | Player experience, design pillars, non-goals, open design decisions | Changing player-visible gameplay |
| `PROTOTYPE_MILESTONE.md` | Current validation target, scope, gates, exit criteria | Choosing or evaluating current work |
| `RHYTHM_SYSTEM.md` | Authoritative timing and synchronization contract | Changing any rhythm-sensitive behavior |
| `CHART_FORMAT.md` | Chart data contract and runtime interpretation | Changing chart data or validation |
| `CHART_AUTHORING.md` | Current chart-authoring workflow and tool limitations | Changing or using the authoring tool |
| `ARCHITECTURE.md` | System boundaries, ownership, dependencies, current implementation map | Changing system responsibilities |
| `BACKLOG.md` | Small executable work items and their acceptance evidence | Starting or completing a milestone task |
| `DEVELOPMENT_WORKFLOW.md` | Repository change, verification, Unity, and Git procedure | Making any repository change |
| `Playtests/` | Session-specific observations and evidence | Running or reviewing manual tests |
| `ADR/` | Accepted technical decisions and reconsideration conditions | Revisiting a consequential technical choice |

Repository-wide Codex rules remain in `../AGENTS.md`.

## Status Vocabulary

Documents and decisions use these terms consistently:

- `Current`: describes the contract or process currently in force
- `Draft`: usable for discussion but not yet an accepted contract
- `Proposed`: a candidate that must not be implemented as settled without approval
- `Accepted for current prototype`: intentionally chosen for the prototype but open to later replacement
- `Open`: not yet decided
- `Superseded`: retained only for decision history

## Current, Contract, and Future

When a document contains more than one time horizon, prefer explicit labels:

- `Current implementation`: what exists in the repository now
- `Contract`: behavior that changes must preserve
- `Current milestone`: what is being validated next
- `Future possibility`: non-binding design space
- `Open decision`: a choice Codex must not silently finalize

## Maintenance Rules

- Update `PROTOTYPE_MILESTONE.md` when the validation target changes.
- Update `BACKLOG.md` when task scope, status, dependencies, or evidence changes.
- Add a playtest record for each meaningful manual validation session.
- Update a durable contract only when the project intentionally changes that contract.
- Add or supersede an ADR when a consequential technical decision changes.
- Prefer Git history over manually synchronized cross-document version numbers.
- When adding a document, add it to this map and to `../AGENTS.md` if Codex needs an explicit routing rule.

