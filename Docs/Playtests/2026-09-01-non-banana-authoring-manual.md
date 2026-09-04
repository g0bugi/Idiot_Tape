# Idiot_Tape — Non-Banana Authoring Manual Verification — 2026-09-01

> Status: Automated checks passed; interactive author verification pending
> Scope: Tap, hold, normal slide, terminal-flick slide, and ordinary flick

## Automated Baseline

- Unity: `6000.3.15f1`
- EditMode result: 88/88 passed
- Added coverage:
  - `3 -> 3` creates a hold
  - `3 -> 1 -> 1` creates a normal slide with an explicit close node
  - `3 -> 1 -> 0` converts the final transition without adding a time or node
  - ordinary flick creation and outward boundary rejection
  - tap, hold, slide, and flick fields survive asset save and reload

## Manual Setup

1. Open `Assets/Scenes/Gameplay.unity`.
2. Enter Play Mode.
3. Open `Tools > Idiot Tape > 채보 제작 도구`.
4. Select a disposable or copied `PrototypeChart`. Do not use irreplaceable chart data for the
   first verification.
5. Select one musical part and set a short two- to four-bar loop with an active part window.
6. Turn off `적용 후 임시 기록 비우기` for the first pass so the buffered and applied shapes can
   be compared side by side.
7. Choose a visible quantization grid such as `1/16음표`.

## Tap

1. Select `탭` recording mode.
2. Start loop recording and press `2`, `4`, and `6` on three audible beats.
3. Stop recording.
4. Confirm three separate tap markers appear at the recorded times and lanes.
5. Select each buffered tap and confirm its time, lane, and musical part are correct.

Expected: each key press creates exactly one tap; no sustained body or endpoint is shown.

## Hold

1. Select `홀드·슬라이드` recording mode.
2. During recording, press `3` to start and press `3` again one or two beats later.
3. Stop recording and select the resulting hold.
4. Confirm a start, horizontal body, and end are visible in both timeline orientations.
5. In the detail canvas, drag the start and end left/right. Confirm they snap to the selected grid.
6. Hold `Alt` while dragging and confirm the point can move without snapping.
7. Drag either point vertically and confirm the entire hold stays in one lane.

Expected: the hold remains valid, its end stays after its start, and both endpoints share one lane.

## Normal Slide

1. Record `3 -> 1 -> 1` with clear gaps between key presses.
2. Confirm the result is one slide, not separate tap notes.
3. Confirm the timeline shows the complete path and the detail canvas shows all authored nodes.
4. Drag the start, middle, and end nodes horizontally and vertically.
5. Confirm horizontal movement snaps to the active grid unless `Alt` is held.
6. Click directly on a line segment away from an existing node.
7. Confirm a new middle node is inserted at a snapped time and at the interpolated lane.
8. Confirm cyan quarter-beat checks, orange half-beat rewards, and large authored nodes are visible.

Expected: node times remain strictly increasing and the final node remains the slide end.

## Terminal-Flick Slide

1. Record `3 -> 1 -> 0`.
2. Confirm `0` closes the slide immediately without creating an additional node or later time.
3. Confirm the final marker is orange and `끝 동작` reads `종단 플릭`.
4. Drag the final point to another lane and confirm the terminal segment remains a lane transition.
5. Change `끝 동작` between `일반 종료` and `종단 플릭` and run `차트 검사`.

Expected: a valid final lane transition passes validation; an invalid same-lane terminal segment
reports a contextual validation error.

## Ordinary Flick

1. Select `플릭` mode and choose `오른쪽`.
2. Record a flick from a non-boundary lane such as `4`.
3. Confirm the flick initially ends in lane 5 and appears as a directional line.
4. Select it and drag the orange endpoint to lane 7.
5. Attempt to drag the endpoint onto the start lane.
6. Confirm the tool rejects the same-lane result and displays an explanation.
7. With right direction still selected, try recording from lane 8.

Expected: the non-adjacent endpoint is retained; outward lane-8 creation is rejected without adding
an invalid note.

## Apply, Re-Edit, Undo, and Save

1. Press `기록 적용` and confirm `차트 검사` passes.
2. Select an applied hold, slide, or flick in either timeline.
3. Confirm the applied-note editor opens even if the temporary buffer is cleared.
4. Change a time, lane, endpoint, or slide node and confirm the applied timeline updates.
5. Use Unity Undo and Redo and confirm the shape and serialized values follow the operation.
6. Press `에셋 저장`.
7. Exit Play Mode, re-enter Play Mode, reopen the tool, and select the same chart.
8. Confirm all edited note types, times, lanes, slide nodes, and flick endpoints remain intact.
9. Run the chart in gameplay and confirm the notes schedule at the saved positions.

## Record Results

- Tester:
- Chart copy:
- Loop bars:
- Total authoring time:
- Correction time by note type:
- Unexpected extra or missing notes:
- Undo/Redo result:
- Save/re-enter result:
- Gameplay replay result:
- Most awkward remaining interaction:
- Unity Console errors or warnings:
