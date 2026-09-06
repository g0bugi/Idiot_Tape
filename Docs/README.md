# Idiot_Tape — Documentation Guide

> Status: Current
> Last reviewed: 2026-09-05
> Authority: Document ownership, routing, and interpretation

## Start Here

Use the row matching the requested change. Read the owning sections and affected code first;
follow another document only when the task crosses its boundary. This is a routing table,
not an instruction to read every listed file. Already-read, unchanged context need not be
loaded again.

| Task | Owning document | Implementation entry points |
|---|---|---|
| Player experience, layout, or scope | [GAME_DESIGN.md](GAME_DESIGN.md) | `GameplaySession`, `PlayfieldPresenter` |
| Note rules, score, contact ownership | [NOTE_INTERACTIONS.md](NOTE_INTERACTIONS.md) | `GameplaySession`, `JudgementEvaluator`, `NoteInteractionMath` |
| Playback, start/count-in, input timing, pause, seek, offsets | [RHYTHM_SYSTEM.md](RHYTHM_SYSTEM.md) | `FmodSongPlayback`, `SongTimelineMath`, `GameplayStartPlan`, `FmodMetronome` |
| Chart fields, compatibility, validation | [CHART_FORMAT.md](CHART_FORMAT.md) | `PrototypeChart`, `ChartTempoMap` |
| Recorder, workspace, correction, Undo/apply/save | [CHART_AUTHORING.md](CHART_AUTHORING.md) | `PrototypeChartRecorderWindow` partials and `Chart*Utility` helpers |
| Core ownership or dependencies | [ARCHITECTURE.md](ARCHITECTURE.md) | Current implementation and assembly maps |
| Choosing work or judging milestone completion | [PROTOTYPE_MILESTONE.md](PROTOTYPE_MILESTONE.md), relevant [BACKLOG.md](BACKLOG.md) item | Gate, hypothesis, and work-item IDs |
| Verification and repository procedure | [DEVELOPMENT_WORKFLOW.md](DEVELOPMENT_WORKFLOW.md) | `Assets/Tests/EditMode`, `Assets/Tests/PlayMode` |
| A specific regression or manual session | Linked record under [Playtests/](Playtests/) | Commands, observations, and artifact paths in that record |
| Revisiting a consequential decision | Relevant [ADR/](ADR/) entry | FMOD clock (0001), ScriptableObject charts (0002), hidden lanes (0003) |

Runtime sources live under `Assets/Scripts/Audio` and `Assets/Scripts/Gameplay`; authoring
sources live under `Assets/Editor`. For a narrow change, search headings or symbols before
reading large files. Full-document review is appropriate when changing the document itself
or a contract that spans its sections.

Repository-wide rules live in [../AGENTS.md](../AGENTS.md). Do not duplicate the routing
table or detailed procedures there.

## Authority and Status

The user's request defines scope. Accepted design/technical contracts define intended
behavior; the repository shows implementation. Milestone and backlog documents define
priority and acceptance, while playtests record evidence. ADRs explain accepted decisions
and reconsideration conditions; they are not optional advice or a reason to create a new
system. Report conflicts and make the smallest requested change.

| Label | Meaning |
|---|---|
| `Current` | Contract or process currently in force; not proof that every feature is verified |
| `Accepted for current prototype` | Chosen for this prototype; replace only through an intentional decision |
| `Draft` / `Proposed` | Discussion or candidate direction, not settled implementation scope |
| `Open` | Unresolved decision, not an automatic request to implement it |
| `Superseded` | Historical material; follow the linked replacement |

Within a document, distinguish `Current implementation`, `Contract`, `Current milestone`,
`Future possibility`, and `Open decision`. Work-item statuses, including
`Implemented; verification pending`, are defined in [BACKLOG.md](BACKLOG.md#status-vocabulary).
An automated pass does not establish feel, authoring speed, full-song stability, or device latency.

## Maintenance

- Keep each rule in its owning document; link to it instead of copying its full explanation.
- Update implementation descriptions from inspected code. Preserve intended rules and mark
  known gaps rather than rewriting a contract to legitimize a defect.
- Keep acceptance status and an evidence index in `BACKLOG.md`. Detailed test counts,
  commands, measurements, and failure history belong in dated `Playtests/` records.
  Cite a historical run as historical; never imply it was rerun for a documentation edit.
- Preserve dated playtest evidence. Add a new record or explicit correction/supersession
  when evidence changes; use [Playtests/TEMPLATE.md](Playtests/TEMPLATE.md) selectively.
- Update milestone gates when scope changes and ADRs when consequential decisions change.
  Retain stable decision/work-item IDs and use Git for ordinary edit history.
- Add new documents to this routing table only when they own a distinct responsibility.
  Do not add summary copies that create another status source.

