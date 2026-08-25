# AGENTS.md

## Project

Project name: `Idiot_Tape`

Idiot_Tape is a Unity/C# mobile rhythm game prototype.

The defining gameplay concept is that note layout and movement should visually reflect
the musical structure of the song.

The game must not be designed under the assumption that every chart uses a fixed,
traditional lane layout.

The current priority is validating the core rhythm-game experience, not building
large production systems.


## Purpose of This File

This file defines repository-wide working rules for Codex.

Keep this file focused on durable engineering rules.

Detailed design and implementation guidance lives in `Docs/`.

Before making significant changes, read the documentation relevant to the task.


## Documentation Map

Read the relevant documents before working on the corresponding system.

- `Docs/README.md`
  - Documentation authority and routing
  - Status vocabulary
  - Current versus future interpretation rules

- `Docs/GAME_DESIGN.md`
  - Core game concept
  - Prototype goals
  - Gameplay design principles
  - Current design decisions and unresolved questions

- `Docs/PROTOTYPE_MILESTONE.md`
  - Current validation hypotheses
  - P0/P1 scope
  - Milestone gates and exit criteria

- `Docs/RHYTHM_SYSTEM.md`
  - Authoritative rhythm clock
  - Audio synchronization
  - Note timing
  - Judgement timing
  - Pause, resume, offsets, and drift prevention

- `Docs/CHART_FORMAT.md`
  - Chart data responsibilities
  - Timing representation
  - Musical-part metadata
  - Runtime versus authoring data
  - Chart validation rules

- `Docs/CHART_AUTHORING.md`
  - Current Play Mode authoring workflow
  - Metronome, quantization, and tempo-calibration constraints
  - Tool capabilities and known limitations

- `Docs/ARCHITECTURE.md`
  - Major runtime systems
  - System responsibilities
  - Dependency rules
  - Data flow

- `Docs/DEVELOPMENT_WORKFLOW.md`
  - Required preflight checks
  - Unity safety rules
  - Testing and verification
  - Git safety
  - Definition of Done

- `Docs/BACKLOG.md`
  - Current milestone work items
  - Dependencies, scope, and acceptance evidence

- `Docs/Playtests/`
  - Manual validation records
  - Physical-device, timing, readability, and authoring evidence

- `Docs/ADR/`
  - Accepted consequential technical decisions
  - Tradeoffs and reconsideration conditions


## Instruction Priority

The user's current task defines what should be changed.

Project documentation defines how the relevant system is intended to behave.

The current repository defines what is actually implemented.

If these disagree:

1. Do not silently perform a large migration or rewrite.
2. Identify the discrepancy.
3. Make the smallest change necessary for the requested task unless the task explicitly
   requires resolving the discrepancy.
4. Report important documentation-versus-code inconsistencies at the end of the task.

Do not treat an outdated document as permission to overwrite working code unrelated to
the current request.


## Before Editing

Before changing code or Unity assets:

1. Inspect the relevant existing implementation.
2. Search for related classes, prefabs, scenes, data files, and references.
3. Read the relevant document under `Docs/`.
4. Check the current repository status.
5. Preserve unrelated user changes.

Do not create a new manager, service, data model, or parallel implementation before checking
whether an equivalent system already exists.


## Change Scope

Prefer the smallest coherent change that fully solves the task.

Do not:

- refactor unrelated systems
- rename unrelated files
- reorganize folders without a clear need
- replace working implementations merely because another design seems cleaner
- introduce frameworks for hypothetical future requirements
- modify unrelated scenes, prefabs, assets, or project settings

Large architectural changes should be made only when the task actually requires them.


## Prototype Scope

The project is currently a prototype / demo.

Prioritize validating:

1. rhythm accuracy
2. input responsiveness
3. chart readability
4. instrument / musical-layer-based note presentation
5. fast chart iteration
6. synchronization stability
7. mobile feasibility

Do not prematurely add systems such as:

- accounts
- monetization
- gacha
- character collection
- progression frameworks
- live-service infrastructure
- unnecessary backend systems
- large generic UI frameworks

unless explicitly requested.


## Rhythm-System Invariants

Timing correctness is a non-negotiable requirement.

All timing-sensitive gameplay systems must derive from a single authoritative song timeline.

This includes:

- chart progression
- note scheduling
- note visual position
- input judgement
- pause / resume behavior
- gameplay timing offsets

Do not create the authoritative rhythm timeline by accumulating `Time.deltaTime`.

Frame timing may be used to update rendering, but gameplay state must remain derived from
the authoritative song time.

A frame hitch must not permanently move notes away from their correct musical timing.

Note positions should be recoverable from chart timing plus current song time rather than
depending only on the previous frame's transform.

Do not sacrifice timing correctness for animation or visual effects.

Read `Docs/RHYTHM_SYSTEM.md` before changing timing-sensitive code.


## Chart Rules

Chart data is data.

Do not author the core chart by manually placing every gameplay note as a GameObject
inside a Unity scene.

Do not make chart data depend on runtime GameObject references.

A song may contain many notes and may last several minutes.

Keeping lightweight chart data for the entire song in memory is acceptable.

Runtime note GameObjects and chart data must have separate lifetimes.

Do not assume:

- a fixed number of lanes
- a fixed set of note positions
- a specific final chart serialization format
- a specific final set of note types

unless those decisions have been explicitly documented.

Read `Docs/CHART_FORMAT.md` before changing chart representation.


## Unity Safety

Treat Unity serialized assets carefully.

Do not unnecessarily rename or move:

- scenes
- prefabs
- ScriptableObjects
- materials
- audio assets
- folders containing referenced assets

Preserve `.meta` files for existing Unity assets.

Do not regenerate GUID relationships unnecessarily.

When renaming serialized C# fields, preserve existing serialized Inspector data where required,
for example through an appropriate migration mechanism such as `FormerlySerializedAs`.

Do not manually edit Unity YAML assets unless there is a clear reason and the result can be
safely verified.

Do not upgrade the Unity Editor version unless explicitly requested.

Do not add, remove, or upgrade Unity packages unless the task requires it.

Do not modify `ProjectSettings` merely to make implementation easier without first determining
the effect on the rest of the project.

Do not directly edit generated project artifacts such as:

- `Library/`
- `Temp/`
- `Logs/`
- `obj/`
- generated `.csproj` files
- generated `.sln` files

unless there is a specific, justified task requiring it.


## Architecture

Prefer:

- explicit responsibilities
- clear state ownership
- data-driven gameplay
- small focused classes
- explicit dependencies
- testable timing and judgement logic
- separation between gameplay data and visual GameObjects

Avoid:

- unnecessary global managers
- unnecessary singletons
- excessive inheritance
- service-locator-style hidden dependencies
- speculative generic frameworks
- premature dependency injection frameworks
- duplicate sources of truth

A prototype should remain simple, but prototype code is not disposable by default.

Read `Docs/ARCHITECTURE.md` before introducing or significantly changing a core system.


## C# Style

Use clear, conventional Unity C#.

Use Allman-style braces.

Keep one blank line immediately inside code blocks where practical.

Example:

```csharp
if (isPlaying)
{

    UpdateNotes();

}
```

Prefer descriptive names over abbreviations.

Avoid comments that merely restate the code.

Comments should primarily explain:

- timing assumptions
- synchronization behavior
- architectural intent
- unusual Unity behavior
- non-obvious reasons for implementation decisions


## Performance

The final target includes mobile devices.

Avoid unnecessary allocations and repeated expensive work in gameplay hot paths.

Pay particular attention to code that runs:

- every frame
- for every active note
- for every input
- during note spawning
- during judgement searches

Avoid obvious hot-path problems such as:

- repeated unnecessary `GetComponent` calls
- repeated LINQ allocations in gameplay loops
- unnecessary per-frame collection creation
- uncontrolled `Instantiate` / `Destroy` loops

Do not perform speculative micro-optimization.

Optimize measured or structurally obvious scaling problems first.


## Testing and Verification

Do not claim a check was performed unless it was actually performed.

After implementation, perform every relevant verification that the current environment allows.

At minimum:

1. Check for compile errors.
2. Run relevant automated tests if they exist.
3. Verify affected runtime behavior in Unity Play Mode when possible.
4. Check the Unity Console for new errors.
5. Review the final diff.
6. Check that unrelated files were not modified unnecessarily.

Timing-sensitive changes require additional verification described in
`Docs/RHYTHM_SYSTEM.md`.

If a required verification cannot be performed, explicitly report it as:

`Not verified: <reason>`

Never describe unexecuted testing as successful testing.


## Git Safety

Before editing, inspect the current working tree.

Do not:

- discard unrelated local changes
- use destructive reset operations on user work
- force-push
- rewrite Git history
- delete branches
- delete user-created files because they appear unused
- commit generated Unity caches

Do not create a commit unless the user explicitly asks for one.

Do not revert files merely because they contain changes unrelated to the current task.

Review the final diff before considering the task complete.


## Documentation Maintenance

When a task intentionally changes a documented architectural or gameplay contract,
update the relevant document in `Docs/` as part of the same change.

Do not update documentation merely to make it agree with an accidental implementation.

Documentation should describe intentional project decisions.


## Communication

For straightforward tasks, inspect the repository and implement directly.

Do not ask questions that can be safely answered by inspecting the project.

Ask for clarification only when an unresolved decision materially affects the requested behavior
and cannot be safely inferred from the repository or documentation.

For large or architectural tasks, state the intended approach before making broad changes.


## Definition of Done

A task is complete only when:

- the requested behavior is implemented
- the implementation respects relevant project documentation
- existing relevant behavior is preserved
- no new known compile errors remain
- relevant verification has been performed where possible
- the final diff has been reviewed
- unrelated changes have not been discarded or overwritten
- unverified behavior has been clearly reported

At the end of a development task, summarize:

1. what changed
2. important files changed
3. how the implementation works
4. what was actually tested or verified
5. what was not verified
6. remaining limitations or follow-up work


## Project Naming

The official project name is:

`Idiot_Tape`

Do not refer to the current project as:

- `TapsonicRevival`
- `Tapsonic Revival`
- `탭소닉 리바이벌`

except when referring to historical names, migration, or legacy files.
