# AGENTS.md

## Project and scope

`Idiot_Tape` is a Unity/C# mobile rhythm-game prototype. Note layout and movement should
communicate musical structure. The current hidden lane grid is reversible; do not assume a
permanent lane count, coordinate system, chart format, or production note taxonomy.

Prioritize rhythm accuracy, responsive input, readability, musical-part presentation, fast
chart iteration, synchronization, and mobile feasibility. Add production systems such as
accounts, monetization, progression, or backend infrastructure only when requested.
Use the official project name; old names belong only in historical or migration references.

## Working with the user

- Treat requests to implement, fix, review, or improve as instructions to finish the work.
  For straightforward tasks, inspect and proceed; for broad changes, state the approach first.
- Resolve routine choices from the repository and accepted contracts. Ask only when an
  unresolved choice materially changes the requested behavior and cannot be inferred safely.
- Follow-up corrections refine the active task unless the user explicitly replaces it.
  Do not expand a concrete request into unrelated cleanup or a new production system.
- Answer in the user's language. Keep progress and completion reports concise and concrete;
  do not repeat the request, plan, or unchanged status.

## Read before editing

1. Inspect `git status --short` and preserve existing user changes.
2. Inspect the affected implementation, callers, tests, and serialized references. Search for
   an existing owner before adding a manager, service, data model, or parallel implementation.
3. Use [Docs/README.md](Docs/README.md) to select the owning document and relevant sections.
   Read only task-relevant contracts; do not load all of `Docs/`, the backlog, ADRs, or past
   playtests for every task. Reuse context already read unless files or task scope changed.
4. Use [Docs/DEVELOPMENT_WORKFLOW.md](Docs/DEVELOPMENT_WORKFLOW.md) for the applicable
   verification procedure. Documentation-only work uses its documentation checks.

The user's task defines scope; accepted project contracts define intended behavior; code
defines current implementation. If they disagree, make the smallest coherent requested
change and report material discrepancies. An outdated document does not authorize an
unrelated rewrite. Update contracts only for intentional decisions.

## Gameplay invariants

- One authoritative song timeline drives scheduling, visual position, input judgement,
  offsets, pause, and restart. Never accumulate `Time.deltaTime` as song time.
- Recover note positions from chart timing and current song time after a hitch; animation
  must not change judgement. Read `Docs/RHYTHM_SYSTEM.md` for timing-sensitive changes.
- Chart data is independent of runtime GameObjects. Do not author the core chart by
  manually placing notes in a scene. Whole-song lightweight data is acceptable; active
  note objects have separate lifetimes. Read `Docs/CHART_FORMAT.md` for data changes.
- Use `Docs/NOTE_INTERACTIONS.md` for note behavior and `Docs/ARCHITECTURE.md` when changing
  core responsibilities. Keep state ownership explicit, dependencies focused, and timing
  and judgement logic testable. Avoid unnecessary globals, singletons, inheritance,
  service locators, generic frameworks, and duplicate sources of truth.

## Unity and Git safety

- Preserve unrelated edits, serialized Inspector values, `.meta` files, and GUIDs. Avoid
  unrelated asset moves, renames, scene/prefab edits, and hierarchy changes.
- Migrate serialized field changes when needed, for example with `FormerlySerializedAs`.
  Manually edit Unity YAML only for a clear reason with a verifiable result.
- Do not upgrade Unity unless requested. Change packages only when required by the task;
  determine the project-wide effect before changing `ProjectSettings`.
- Do not directly edit generated `Library/`, `Temp/`, `Logs/`, `obj/`, `*.csproj`, or
  `*.sln` except for a specifically justified task. Do not commit generated caches.
- Do not discard user work, destructively reset, force-push, rewrite history, delete
  branches, or delete user files merely because they appear unused.
- Do not create a commit unless the user explicitly asks.

## C# and performance

Use conventional Unity C#, descriptive names, Allman braces, and one blank line immediately
inside blocks where practical:

```csharp
if (isPlaying)
{

    UpdateNotes();

}
```

Comments should explain timing assumptions, architecture, unusual Unity behavior, or other
non-obvious reasons. Avoid comments that restate code.

In per-frame, per-note, spawning, and input paths, avoid unnecessary allocations, LINQ,
repeated component/global searches, and uncontrolled Instantiate/Destroy loops. Optimize
measured or structurally clear scaling problems without speculative micro-optimization.

## Completion

Implement the requested behavior, preserve relevant existing behavior and user work, resolve
new known errors, run applicable checks, and review the final diff. Never claim unexecuted
verification passed. Report required checks that could not run as `Not verified: <reason>`.

In a compact report, cover what changed and why, important files, actual verification,
unverified behavior, and material limitations or follow-up work. Combine these when clear;
six separate sections and empty “no issues” lists are not required.
