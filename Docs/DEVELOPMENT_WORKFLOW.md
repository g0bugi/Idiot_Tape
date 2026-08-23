# Idiot_Tape — Development Workflow

## Document Purpose

This document defines the expected workflow for repository changes.

The goal is to prevent:

- accidental unrelated changes
- unverified implementations
- broken Unity serialization
- duplicate systems
- destructive Git operations
- false claims that something was tested


# 1. Preflight

Before implementation:

1. inspect the repository status
2. identify user changes already present
3. inspect relevant existing files
4. search for related implementations and references
5. read the relevant project documentation
6. determine the smallest reasonable change scope

Do not assume a clean working tree.

Do not overwrite existing changes simply because they are unrelated to the current task.


# 2. Understand Before Replacing

Before replacing an existing implementation:

1. identify what calls it
2. identify what references it
3. inspect related serialized Unity assets
4. determine whether the old behavior is still required
5. determine whether migration is needed

Do not delete old code first and investigate dependencies afterward.


# 3. Implementation Scope

Prefer the smallest coherent implementation.

A coherent change may include multiple files when those files form one responsibility.

Do not artificially restrict a task to one file when doing so would create bad architecture.

However, do not use a small feature request as an excuse for a broad cleanup.


# 4. Existing User Changes

Existing local modifications belong to the user unless the task explicitly concerns them.

Do not:

- reset them
- revert them
- overwrite them blindly
- stash them without a reason
- include them in unrelated cleanup

If the requested change must touch a file that already contains user modifications,
preserve those modifications carefully.


# 5. Unity Asset Safety

When modifying Unity assets:

- preserve `.meta` files
- preserve GUID relationships
- preserve unrelated serialized fields
- avoid unnecessary asset moves
- avoid unnecessary asset renames
- avoid unnecessary hierarchy reorganization

After serialized changes, check for:

- missing references
- lost Inspector values
- unexpected prefab overrides
- broken ScriptableObject references


# 6. Serialized C# Fields

Treat serialized fields as persistent project data.

Before renaming or changing the type of a serialized field, consider existing:

- prefabs
- scenes
- ScriptableObjects

If existing serialized values must survive, use an appropriate migration strategy.

Do not treat a private `[SerializeField]` rename as a purely internal code rename.


# 7. Unity Version and Packages

Do not change the Unity version unless explicitly requested.

Do not add or upgrade a package simply because it makes implementation easier.

When a new dependency is genuinely required:

1. confirm the project does not already provide equivalent functionality
2. explain why the dependency is needed
3. keep the dependency change limited to the task


# 8. Generated Files

Do not intentionally modify or commit Unity-generated caches and IDE project files such as:

```text
Library/
Temp/
Logs/
obj/
*.csproj
*.sln
```

unless a specific task explicitly concerns those files.


# 9. Coding

During implementation:

- follow project naming and formatting conventions
- preserve responsibility boundaries
- avoid duplicate sources of truth
- avoid introducing timing behavior into unrelated visual components
- avoid unnecessary per-frame allocations
- keep magic timing constants centralized or configurable where appropriate

Do not add defensive complexity for impossible hypothetical conditions without evidence
the project needs it.


# 10. Compilation

After C# changes, verify that the project has no new compilation errors.

Do not consider a code change complete while known compile errors caused by that change remain.

If compilation cannot actually be executed in the current environment, report that fact explicitly.

Example:

```text
Not verified: Unity compilation could not be executed in the current environment.
```


# 11. Automated Tests

Run relevant existing tests after changing covered behavior.

Add or update tests when:

- changing deterministic timing calculations
- changing judgement rules
- fixing a reproducible regression
- changing data validation
- changing isolated logic that can reasonably be tested

Do not create meaningless tests only to increase test count.

After automated Unity tests finish:

- confirm the Unity process exited normally
- check that `Assets/_Recovery` was not created from an interrupted Test Runner scene
- do not commit `Assets/_Recovery` or its `.meta` file
- inspect recovery scenes in Unity before deleting them, because an interactive editor crash can place
  unsaved user work there
- `Temp/__Backupscenes` is generated editor state and must remain outside version control


# 12. Unity Play Mode Verification

When gameplay behavior changes and the environment supports Unity execution:

1. enter the relevant scene
2. reproduce or exercise the affected behavior
3. check expected behavior
4. check nearby existing behavior for regression
5. inspect the Unity Console afterward

A successful compile is not equivalent to a successful gameplay test.


# 13. Rhythm-System Verification

Changes involving timing require additional care.

Read `RHYTHM_SYSTEM.md`.

At minimum, consider:

- exact timing behavior
- early / late behavior
- frame-rate independence
- pause / resume
- restart
- offset behavior
- frame hitch recovery
- synchronization later in the song

Do not validate a synchronization change using only the first few seconds when full-song drift
could be relevant.


# 14. Chart Changes

When chart data or chart parsing changes:

verify, where relevant:

- old supported chart data still loads
- invalid data fails clearly
- note ordering remains valid
- timing values remain unchanged unless intentionally migrated
- musical-part references remain valid
- schema changes are intentional

Do not silently reinterpret existing chart files after a breaking format change.


# 15. Performance Work

Do not describe something as a performance improvement solely because the code looks faster.

When the task specifically concerns performance, use measurable evidence when practical.

Relevant evidence may include:

- Unity Profiler
- allocation measurements
- frame timing
- active GameObject counts
- reproduction with a dense chart

Do not optimize unrelated code without evidence or a clear scaling problem.


# 16. Final Diff Review

Before considering the task finished, review the final changes.

Check:

- which files changed
- whether every change belongs to the task
- whether user changes were accidentally overwritten
- whether debug code remains
- whether temporary files were added
- whether serialized assets changed unexpectedly
- whether documentation should be updated

If command-line Git is available, useful checks may include:

```text
git status --short
git diff --check
git diff
```

Use them as inspection tools, not as permission to modify unrelated work.


# 17. Git Safety

Do not perform destructive Git operations without explicit user intent.

Do not:

- force-push
- rewrite history
- hard-reset user work
- delete branches
- discard unrelated changes
- automatically clean untracked user files

Do not commit unless explicitly requested.

A successful local implementation does not require creating a commit.


# 18. Documentation Updates

Update project documentation when the task intentionally changes a documented contract.

Examples:

- rhythm clock architecture changed
- chart schema intentionally changed
- a design decision became final
- ownership of a major system changed

Do not rewrite documentation to legitimize an accidental implementation discrepancy.


# 19. Required Completion Report

At the end of the task, report:

## Changed

Describe the behavior that changed.


## Important Files

List the important files modified.


## Implementation

Briefly explain how the new behavior works.


## Verified

List only checks that were actually executed.


## Not Verified

List relevant checks that could not be executed.

If everything relevant was verified, say so.


## Remaining Issues

Describe known limitations, risks, TODOs, or follow-up work.

Do not invent remaining issues merely to populate this section.


# 20. Definition of Done

A task is done when:

- the requested behavior exists
- the implementation fits the existing project
- known errors introduced by the task are resolved
- relevant testing has been performed where available
- the final diff has been reviewed
- unrelated work was preserved
- unperformed verification has been reported accurately
