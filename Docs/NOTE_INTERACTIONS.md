# Idiot_Tape — Note Interactions

> Status: Accepted for current prototype
> Last reviewed: 2026-09-04
> Applies to: The implemented prototype interaction set
> Authority: Player-visible tap, hold, slide, flick, and banana-note behavior

## Document Purpose

This document defines the note-interaction rules intentionally accepted for the next prototype
expansion.

The runtime and authoring tool implement this contract for prototype validation. Verification
evidence is recorded in `BACKLOG.md`; representative new-note Play Mode cases and physical-device
feel, including the revised step-slide transition, remain verification work.

Data representation belongs in `CHART_FORMAT.md`. Authoritative timing belongs in
`RHYTHM_SYSTEM.md`. Authoring workflow belongs in `CHART_AUTHORING.md`.


## Shared Interaction Contract

### Timing and Grades

- all note times, internal checks, reward ticks, and path nodes resolve to the authoritative song
  timeline
- tap, hold, and slide starts use the configurable Perfect, Good, and Miss windows
- successful hold and slide continuations inherit the grade established at their start
- a hold or slide continuation does not calculate a new Perfect or Good grade
- flick uses the full ordinary Good window as one successful Perfect window for the current
  prototype
- final judgement-window values remain provisional and configurable

### Score and Combo Units

A tap-equivalent result means the same score that an ordinary tap would award for the relevant
grade plus one integer combo.

For the accepted prototype rules:

- hold starts, half-beat reward ticks, and ends each award one tap-equivalent result
- slide starts, authored middle nodes, half-beat reward ticks, and ends each award one
  tap-equivalent result
- hold and slide continuation results use their start grade
- a slide middle node and a half-beat reward tick at the same chart time both award their result
- an ordinary flick awards one Perfect tap-equivalent result for the whole gesture, not separate
  start and end rewards
- banana start and end each award their own tap-equivalent result; checkpoints award fractional
  score and defer their integer combo bonus to the successful end
- these are prototype interaction rules, not a final production scoring formula

### Contact Ownership

- one contact may belong to at most one sustained interaction at a time
- separate contacts may play separate overlapping notes
- hold uses its starting contact and does not allow recovery or handoff after that contact fails
- slide may hand off between contacts under the slide-specific continuity rule
- a contact already owned by a sustained interaction cannot also trigger an unrelated note

### Presentation and Judgement Separation

Visual fill, glow, opacity, path animation, and failure color communicate interaction state but do
not determine judgement. A note's interaction state must remain recoverable from chart data,
authoritative song time, and timestamped input state.


## Tap

Tap is the current implemented interaction.

- it has one absolute hit time, one lane position, and one musical-part identity
- an eligible press inside the spatial and timing windows produces Perfect or Good
- passing the late Good boundary without a valid press produces Miss
- a successful tap awards one tap-equivalent result


## Hold

### Player Rule

A hold begins with a tap-style judgement and remains in its starting lane until its end.

- the finger may move inside the starting lane
- crossing into another resolved lane breaks the hold
- releasing outside the final grace interval breaks the hold immediately
- a broken hold cannot recover or transfer to another contact
- reaching the end while still valid succeeds automatically; releasing at the exact end is not
  required
- the end displays and scores with the grade established at the start

### Internal Checks and Rewards

- hold validity is checked on the global quarter-beat grid
- combo and score ticks occur on the global half-beat grid
- only grid points strictly after the start and before the end are internal ticks
- the start and end remain separate tap-equivalent results
- the global grid comes from the chart tempo map rather than from intervals accumulated from the
  hold start

### Early-Release Grace

For a hold whose musical duration is at least two beats, its final one beat is an early-release
grace interval.

If the player releases during that interval:

- no Miss is shown and the combo does not break
- reward ticks after the release are not awarded
- the hold end still awards its tap-equivalent result using the start grade
- the hold completes without requiring a replacement contact

Holds shorter than two beats do not use this grace rule. One beat is an accepted provisional
prototype value and must be evaluated in playtesting.

### Failure Presentation

A failed hold produces one Miss, breaks combo, stops all later results, and turns black. Its failed
body remains visible until the authored end time so the player can understand which interaction
failed.


## Slide

### Player Rule

A slide is a sequence of lane holds joined by timed lane changes.

- the start, every authored middle node, and the normal end occupy explicit lanes
- between nodes, the player holds the previous node's lane
- each node's time is the beat at which the player changes to that node's lane; the target does not
  drift diagonally between the two lanes during the preceding interval
- the player may move through intermediate positions during the bounded transition allowance
  defined in `RHYTHM_SYSTEM.md`; an ordinary lane change does not require flick speed or direction
- reaching the normal end while a valid contact is present succeeds automatically
- successful middle nodes and the end inherit the start grade

For example, a `3 -> 7 -> 6` slide means hold lane 3, change to lane 7 at its authored time, hold
lane 7, then change to lane 6 at its authored time. The normal recording sequence `3 -> 7 -> 6 -> 6`
adds a final hold in lane 6 before completion.

### Shape and Reading

With time on the vertical axis, draw each lane hold vertically and each lane change horizontally
at its authored node time. Reversing the chart's time direction for gameplay reverses the vertical
ordering, but must not turn the holds into diagonal tracking segments. With time on the horizontal
axis, the same step shape is transposed.

The corner at the old lane and new node time is generated presentation geometry, not an extra
authored node or reward. Show the destination node and distinguish a normal end from a terminal
flick. Banana remains the interaction for continuous free-curve tracing.

### Checks, Nodes, and Rewards

- held-lane validity is checked on the global quarter-beat grid, with the transition allowance
  defined in `RHYTHM_SYSTEM.md`
- combo and score ticks occur on the global half-beat grid
- every authored middle node also requires arrival in its destination lane and awards one
  tap-equivalent result
- a node and a half-beat tick at the same time share the positional check but each award their own
  tap-equivalent result
- any failed internal check or required node produces one Miss, breaks combo, terminates the whole
  slide, and prevents all later results
- a failed slide turns black and remains visible until its authored end time

### Contact Handoff

A slide may transfer between contacts.

- a replacement contact may enter the held lane or the current transition corridor before or after
  the previous contact is released
- outside a transition allowance, at least one eligible contact must occupy the held lane at each
  required check
- during a transition allowance, a replacement contact must reach the destination lane within the
  allowed time; unresolved checks wait for that arrival or the deadline
- a required check with no qualifying contact fails once its allowed time is exhausted
- each participating contact still belongs to no more than one sustained interaction at a time

### Normal and Flick Endings

A normal slide ends by pressing its current lane a second time during authoring. In play, a valid
contact at that end lane and time completes it automatically. If the author later edits the normal
end to a different lane, it is a final timed lane change with the same transition allowance.

A slide may instead end with a terminal flick. The final authored lane transition becomes the
flick itself:

```text
previous slide node -> final lane = terminal flick and slide completion
```

The final lane is not followed by a second, separate flick. A terminal flick may cross multiple
lanes and follows the flick motion-success contract below. It awards only the slide's one end
result, and that result inherits the slide start grade rather than becoming an additional Perfect
flick result.


## Flick

### Player Rule

A flick has a start lane, a different end lane, and one judgement time.

- an end lane to the left produces a left flick
- an end lane to the right produces a right flick
- both ordinary and slide-terminal flicks may cross multiple lanes
- entering the authored end lane with the required horizontal motion and speed completes the flick
- success is confirmed before release; lifting the finger is not required
- the current prototype supports horizontal flicks only

The start and end should both have clear lane-anchored visual markers so the player can read where
to begin and where to move.

### Timing and Failure

- the start lane must acquire an eligible contact inside the ordinary Good window
- the judgement time is the first timestamp at which the motion satisfies its direction, distance,
  speed, and target-lane conditions
- every valid completion inside the ordinary Good window is displayed and scored as Perfect
- a small motion dead zone prevents touch jitter from becoming an immediate failure
- crossing that dead zone in the opposite horizontal direction produces Miss
- failing to reach the end lane before the late boundary produces Miss
- vertical movement is ignored while the contact remains inside a generous interaction region

An unowned contact that was already down may begin a flick. A contact owned by another sustained
note may not also judge the flick.

### Development Input

Editor and PC playtesting may use one dedicated helper key that directly succeeds the currently
eligible flick. This shortcut validates scheduling, state, score, combo, and presentation only. It
does not validate gesture direction, distance, speed, contact ownership, or mobile touch feel.


## Banana

### Gameplay Purpose

A banana note is a free curved path intended to express melodic or instrumental motion outside a
fixed-lane path. Only its start and end are lane-constrained. Its middle curve uses normalized
playfield space.

### Player Rule

- the start and end use tap-style lane and timing judgement
- the player traces the curve between them
- internal checkpoints test whether an eligible contact is inside the curve corridor
- a successful checkpoint adds charge and one tenth of the ordinary Perfect-tap score, but no
  immediate combo
- missing the curve at a checkpoint does not display Miss and does not break combo
- after leaving or releasing, the player may re-enter and earn later checkpoints
- the end still requires a valid touch in its authored lane and timing window
- missing the end produces Miss and breaks combo

Only one eligible contact contributes to a banana note at a time, and that contact cannot
simultaneously own another sustained interaction.

### Charge and Combo Settlement

Checkpoint count controls tracking resolution. It must not directly control the maximum combo
reward.

Each banana note therefore has a separate maximum bonus-combo value. The initial prototype maps
successful checkpoint ratio to an integer tier:

```text
trackingRatio = successfulCheckpoints / totalCheckpoints
bonusCombo = floor(trackingRatio * maximumBonusCombo)
```

- the successful start immediately awards its ordinary tap combo
- start and end score independently with their own tap-style timing grade
- checkpoints fill the note's charge presentation and add their fractional score
- a successful end awards its ordinary tap combo and then the charged integer bonus combo
- a missed end discards the unclaimed bonus combo; score already earned by checkpoints remains
- zero successful checkpoints therefore still allow the end's ordinary one-combo award

For example, if the start raises combo to 37 and the note has a maximum bonus of four, a perfect
trace followed by a successful end raises combo to 42. With no checkpoint success, the same end
raises it only to 38.

The banana body should visualize charge directly, such as by filling an initially transparent
interior or leaving visible gaps where checkpoints were missed. Miss text is not shown for an
uncharged checkpoint.

### Checkpoint Authoring

- checkpoints are generated from the chart tempo map rather than fixed seconds
- the initial workflow provides quarter-beat and eighth-beat generation, with quarter-beat as the
  default
- the author may change the generated count or subdivision
- the author may add, remove, or reposition individual checkpoints
- checkpoint times remain explicit deterministic chart times after authoring


## Accepted Authoring Summary

The detailed workflow is owned by `CHART_AUTHORING.md`.

- tap recording uses lane keys `1` through `8`
- slide recording begins on a lane key, adds a node whenever another lane key is pressed, and ends
  normally when the current lane key is pressed again
- a different lane key records the lane-change beat, after the previous lane's hold
- a slide with no movement nodes becomes a hold
- pressing `0` after a final lane transition does not add a time or node; it retroactively marks
  that last transition as a terminal flick and closes the slide
- stopping recording with an incomplete slide discards that incomplete slide
- ordinary flick recording creates an adjacent end lane by default, but the author may later drag
  the end to any different lane
- banana authoring places start and end lanes, edits one or two curve handles, generates musical
  checkpoints, and permits manual checkpoint correction


## Future Possibility: Composed Slides

A future editor may combine an ordinary flick end with a slide start at the same lane and time to
create a slide that begins with a flick. This could support shapes that the live slide recorder
cannot create directly.

This is not part of the accepted implementation scope. The current tool must not auto-merge such
notes, and the runtime must not gain a speculative generic composition framework merely for this
possibility.

The chart validator must not reject notes solely because different interactions share a lane and
time. Until composition is implemented, the chart author owns avoidance of unplayable input
conflicts.


## Prototype Verification Questions

- can players distinguish hold, slide, flick, and banana behavior before reaching the judgement
  line?
- do global quarter-beat checks and half-beat rewards feel musically aligned through tempo changes?
- is one-beat hold release grace forgiving without trivializing short holds?
- can slide handoff work without allowing one contact to own multiple interactions?
- can players read lane holds and transition beats from the step shape, and does the transition
  allowance feel fair without permitting early diagonal travel?
- do distant horizontal flicks remain readable and reliable on a physical mobile device?
- does banana charge communicate tracking quality without making missed checkpoints feel like
  unexplained input loss?
- can the author record and repair each interaction fast enough for representative song sections?
