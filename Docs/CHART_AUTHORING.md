# Idiot_Tape — 채보 제작 도구 설명서

> Status: Current
> Last reviewed: 2026-09-12
> Applies to: The current Play Mode prototype authoring tool
> Authority: Current authoring workflow and tool limitations

## Document Purpose

현재 Unity 채보 제작 도구의 사용 설명서입니다. 처음 녹음하고 수정한 뒤 저장·게임 확인까지
이어지는 순서와, 다중 선택·일괄 수정·마지막 테이크 재녹음 기능을 설명합니다.

실제 Unity 화면을 따라 읽으려면 이 PC에 보관된
[화면으로 보는 채보 제작 가이드](C:/Users/User/.codex/visualizations/2026/09/04/01a06b4c-8abc-7143-9fb3-3c81f313484c/authoring-guide/guide.html)를
사용합니다. 2026-09-12에 최신 사용법을 반영한 로컬 HTML 페이지이며, 같은 폴더에 이미지와
`guide.md`가 있습니다. 이 경로는 저장소 밖의 로컬 사본을 가리킵니다.

- [도구 열기](#opening-the-tool) → [기본 작업 순서](#recommended-working-loop)
- [노트 종류별 입력](#recording-modes) · [단축키 모음](#keyboard-and-mouse-reference)
- [여러 노트 함께 수정](#selecting-and-correcting-a-group) · [마지막 테이크 재녹음](#retrying-the-last-take)
- [반영·저장·게임 확인](#apply-and-replay-safety) · [막혔을 때 확인할 것](#troubleshooting)

데이터 규칙은 [CHART_FORMAT.md](CHART_FORMAT.md), 시간·판정 규칙은
[RHYTHM_SYSTEM.md](RHYTHM_SYSTEM.md)를 따릅니다. 이 설명서는 현재 도구의 조작 방법을 다룹니다.

## Authoring Invariants

- recorded input timestamps are converted through the FMOD DSP-backed song timeline
- the metronome and count-in are authoring guides, not authoritative gameplay clocks
- a temporary recording buffer does not modify the chart until the author explicitly applies it
- quantization changes authoring data only and preserves the original recorded timestamp for restoration
- chart changes use Unity Undo where supported and require explicit asset saving
- tempo calibration preview never changes chart data; apply changes the tempo map while preserving
  absolute note times and existing explicit banana checkpoints
- the tool does not manually place runtime note GameObjects into the gameplay scene

## Opening the Tool

1. Unity 메뉴에서 `Tools > Idiot Tape > 채보 제작 도구`를 엽니다.
2. 상단 `차트`에서 편집할 `PrototypeChart` 에셋을 선택합니다. 차트에는 음악 파트, 템포,
   사용할 FMOD 이벤트가 설정되어 있어야 합니다.
3. 오른쪽 `도구` 탭에서 `게임플레이 씬 열기` → `플레이 모드 시작`을 누릅니다.
   녹음과 미리 듣기는 Play Mode에서 동작합니다. 기존 노트의 편집·저장은 Play Mode 밖에서도 가능합니다.
4. `도구`의 오디오 상태가 `준비 완료`이고 상단 버튼이 `재생`으로 바뀌면 녹음할 수 있습니다.
   상단 `준비`는 준비 상태에서 `도구` 탭을 여는 버튼이며, 그 버튼만으로 Play Mode가 시작되지는 않습니다.

창이 좁으면 `작업 준비` 또는 `속성 열기`로 속성 영역을 열고, `← 채보로 돌아가기`로 캔버스에
돌아옵니다. 차트의 템포·음원 설정이 맞지 않으면 먼저 해당 설정을 확인합니다.

도구가 음원을 준비하거나 재생하면 게임의 입력·노트 생성을 멈추고 채보 작업용으로 사용합니다.
완성한 채보를 게임에서 확인하려면 [반영·저장·게임 확인](#apply-and-replay-safety)의 순서대로
**도구 창을 닫은 뒤** Play Mode를 다시 시작합니다.

## Recommended Working Loop

처음에는 짧은 구간을 한 파트씩 만드는 순서로 진행합니다.

1. **파트 선택**: 왼쪽에서 녹음할 음악 파트를 선택합니다. `선택 파트만 듣기`와 `전체 소리 듣기`로
   소리를 확인합니다. 듣기 설정과 게임에서 그 파트가 활성화되는 구간은 별개입니다.
2. **구간·박자 설정**: 상단의 반복 시작 마디와 마디 수를 입력한 뒤 `설정`을 누릅니다.
   `스냅`을 원하는 음표 간격으로 정하고 `카운트인`을 확인합니다. 스냅을 고르는 것만으로 기존
   노트 시간이 바뀌지는 않습니다. `메트로놈` 체크를 꺼도 카운트인 클릭은 들립니다.
3. **시작 위치·유형 선택**: 녹화 버튼 옆에서 `현재 위치부터`, `루프부터`, `처음부터` 중 하나를
   고르고 중앙의 `녹화 유형`을 선택합니다. 홀드는 `홀드·슬라이드` 모드에서 만듭니다.
4. **녹음**: `● 녹화` 또는 `R`을 누릅니다. 카운트인·프리롤을 지나 녹화 상태가 된 다음,
   도구 창에 포커스를 두고 숫자 키로 입력합니다. 구체적인 키 순서는 [노트별 예시](#recording-modes)를 봅니다.
5. **녹음 종료·재시도**: `■ 녹화 중지` 또는 `Esc`로 입력을 끝냅니다. 음악도 멈추려면 `정지`를
   누릅니다. 방금 녹음한 묶음을 다시 만들려면 중지 후 `Shift+R`을 사용합니다.
6. **수정**: 노트를 클릭하고 오른쪽 `노트` 탭에서 시간·위치·경로를 고칩니다. 여러 노트는
   Shift+클릭이나 빈 곳에서 드래그로 선택하고 [일괄 수정](#selecting-and-correcting-a-group)합니다.
7. **차트에 반영**: 하단 `임시 기록`의 개수와 내용을 확인합니다. 새 노트를 더하려면 `추가`를
   고른 뒤 `차트에 반영`을 누릅니다. 구간을 교체할 때는 [교체 범위](#apply-and-replay-safety)를 먼저 확인합니다.
8. **검사·저장**: 상단 `검사` 결과를 확인하고 `저장 필요`를 눌러 `저장됨` 상태로 만듭니다.
   `저장됨`이어도 반영하지 않은 임시 기록이 남아 있을 수 있으므로 하단을 함께 확인합니다.
9. **게임 확인**: 도구를 닫고 Play Mode에서 나온 뒤, 게임 씬이 같은 차트를 참조하는지 확인하고
   Play Mode를 다시 시작합니다. 자세한 순서는 [아래](#apply-and-replay-safety)에 있습니다.

검사 오류는 하단 상태 메시지를 보고 해당 기록을 수정합니다. 반영을 생략하고 `저장 필요`만
눌러서는 임시 기록이 채보 에셋에 저장되지 않습니다.

## Keyboard and Mouse Reference

아래 단축키는 **채보 제작 도구 창에 포커스를 두고** 사용합니다. `R`·`Shift+R`·편집 단축키는
숫자·텍스트 필드 편집을 마친 뒤 캔버스의 노트를 클릭해 포커스를 돌리고 사용합니다.
녹음 중에는 숫자 필드에 입력해도 노트가 기록될 수 있으므로, 값을 고치기 전에 녹음을 중지합니다.

| 조작 | 동작 |
|---|---|
| `Space` | 녹음·카운트인·프리롤 중이 아닐 때 재생·일시정지·이어 듣기. 박자 연속 측정 중에는 다운비트 탭 입력 |
| `R` | 녹음·카운트인·프리롤을 중지한 상태에서 상단에서 선택한 시작 방식으로 녹음 시작 |
| `Shift+R` | 중지한 마지막 미반영 테이크 재녹음 |
| `Esc` | 녹음·카운트인 중지. 완료 기록은 유지하고 미완성 홀드·슬라이드·바나나는 폐기 |
| `1`–`8` | 녹음 중 해당 입력 위치에 노트/경로 입력. 차트의 위치 수 안에서 사용 |
| `0` | 슬라이드의 마지막 이동을 종단 플릭으로 확정. 별도 입력 시각을 기록하지 않음 |
| 클릭 / `Shift+클릭` | 하나 선택 / 선택에 추가·해제 |
| 빈 곳에서 드래그 / `Shift+드래그` | 영역 선택 / 기존 선택에 영역 추가 |
| `Ctrl+A` (`Cmd+A`) | 현재 파트의 전체 노트 선택. 화면 밖 노트와 임시 기록도 포함 |
| 방향키 | 현재 뷰의 시간·위치 방향으로 이동. 방향은 [일괄 수정 표](#selecting-and-correcting-a-group) 참조 |
| `Alt` + 시간 방향키 | 설정한 `미세 이동(ms)`만큼 이동 |
| `Delete` | 선택 노트 삭제. 반영된 차트 노트가 포함되면 확인 후 삭제 |
| `Ctrl+Z` / `Ctrl+Y` | Unity Undo / Redo. 일괄 수정은 한 번에 되돌림 |
| 마우스 휠 / `Ctrl`(`Cmd`)+휠 | 시간 범위 이동 / 마우스 위치를 기준으로 확대·축소 |

방향키 편집과 전체 선택은 녹음·카운트인·박자 연속 측정 중에 작동하지 않습니다.
`Alt`의 의미도 구분합니다. **시간 방향키와 함께** 쓰면 ms 이동이고, **상세 경로의 점을 드래그할 때**
쓰면 박자 스냅을 해제합니다. 빈 캔버스를 그냥 클릭하면 선택 해제와 재생 위치 이동이 일어나므로,
영역 선택은 실제로 드래그해서 사용합니다.

## Current Workspace Layout

구현 상태는 `Implemented; verification pending`입니다. 자동화 검증과 일부 실제 창 조작은
[2026-09-12 검증 기록](Playtests/2026-09-12-authoring-workflow-improvements.md)에 있고,
대표 구간을 사람이 제작하는 전체 시간과 장시간 사용성 검증은 남아 있습니다.
전체 검증 상태는 [백로그](BACKLOG.md#current-evidence-snapshot)를 참고합니다.

| 영역 | 사용하는 기능 |
|---|---|
| 상단 | 차트 선택, 재생·정지, 녹화와 시작 방식, 테이크 재녹음, 현재 마디·박, BPM, 저장 상태. 다음 줄에서 반복 구간·스냅·메트로놈·카운트인·검사를 설정 |
| 왼쪽 | 녹음·편집할 파트 선택, 선택 파트 또는 전체 소리 듣기, 파트 설정·음량 접근 |
| 중앙 `노트 편집` | 시간이 아래로 흐르는 선택 파트의 채보. `다른 파트`로 주변 노트를 옅게 표시하고, 경로와 판정점을 확인 |
| 중앙 `파트 개요` | 시간이 오른쪽으로 흐르는 파트별 개요. 각 파트의 활성 구간·노트 경로·복제 미리보기 확인 |
| 오른쪽 `노트` | 하나 선택하면 종류별 시간·위치·경로 수정. 여러 개 선택하면 일괄 이동·파트 변경·미리보기·적용·삭제 |
| 오른쪽 `파트` | 게임 활성 구간과 파트 음량 설정 |
| 오른쪽 `녹화` | 카운트인, 메트로놈 음량·출력 보정, 자동·수동 박자 보정 설정 |
| 오른쪽 `박자` | 시작점·BPM 보정, 다운비트 연속 측정, 후보 메트로놈 미리 듣기와 적용 |
| 오른쪽 `도구` | 씬·Play Mode 준비, 탐색, 구간 복제, 반영 노트 보정·삭제, 검사, 활성 구간 정리 |
| 하단 `임시 기록` | 기록 개수, 버리기, 추가·구간 교체, 차트에 반영. 펼치면 목록·원본 시간 복원·전체 박자 보정·누락 활성 구간 추가·반영 후 비우기 |

중앙 아래의 `−`·`+`, `재생선`, `구간`, `재생선 따라가기`로 표시 범위를 조절합니다.
노트를 선택하면 `노트` 속성이 열립니다. 창 너비가 1000 미만이면 캔버스와 속성이 한 영역을
번갈아 쓰며, `속성 열기`와 `← 채보로 돌아가기`로 전환합니다. 상단·왼쪽·하단 조작부는 유지됩니다.

## Current Capabilities

### Playback and Navigation

- Korean-language play, pause, restart, seek, and repeated-range controls
- while recording/count-in is idle, Space and the playback button share start/pause/resume behavior,
  including after Stop; Space in a text field remains text input, and tempo-tap capture keeps its
  dedicated Space action. Stop recording with Esc or `■ 녹화 중지` before using the Space transport.
- separate recording starts from the current position, a repeated range, or the beginning
- bar and beat display derived from chart tempo data
- bar-number loop entry and bar-aligned loop snapping
- a bar-first loop workflow using start bar plus bar count
- second-based controls under an advanced authoring foldout
- horizontal timeline and vertical chart-sheet views
- playback auto-follow in both views derived from FMOD song time
- timeline click-to-seek using the selected shared snap/quantization grid
- mouse-wheel navigation and pointer-centered Ctrl/Cmd-wheel zoom

### Recording and Editing

- chart-defined musical-part selection
- number keys `1` through `8` as hidden-position recording input
- a temporary recording buffer that does not modify the chart until explicitly applied
- individual and grouped timing, lane, and part edits
- shared-grid and millisecond timing nudges through buttons and view-aware arrow keys
- Shift-click selection and empty-space rectangle selection of temporary/applied notes
- current-part select-all, grouped preview, transactional edits, and grouped deletion with Undo
- last-take retry that preserves earlier buffered input and restores the original recording setup
- appending notes or replacing recorded parts inside the selected loop
- optional activation-window creation when recorded notes fall outside existing part windows
- direct selection of buffered notes from either timeline view
- direct selection and Undo-supported deletion of applied notes from either timeline view
- full hold, slide, flick, and banana shapes in both timeline views rather than start-only markers
- shared detail editing for buffered and applied tap/hold/slide/flick/banana notes, with a path canvas where
  the interaction has endpoints or nodes
- direct time-and-lane dragging for hold endpoints and slide nodes, plus direct flick endpoint dragging
- banana curve-handle dragging in the selected-note canvas, with detailed handle/checkpoint fields
- immediate Undo-supported editing of applied notes of every supported type before explicit save
- confirmation-protected deletion of a selected part's applied notes inside the current loop
- live preview and Undo-supported quantization of one selected part's applied notes across the full
  chart or current loop, using the same quantization settings as the recording buffer
- confirmation before chart changes discard buffered notes
- protection against accidental repeated application of the same buffer

### Selecting and Correcting a Group

**빠르게 한 칸 옮기기**

1. 왼쪽에서 작업할 파트를 고릅니다.
2. Shift+클릭으로 노트를 더하거나 빼고, 빈 캔버스에서 드래그해 영역을 선택합니다. 선택한
   노트에 테두리가 나타나고 오른쪽에 `N개 선택`이 표시됩니다.
3. 아래 표의 방향키 또는 `한 칸 앞`·`한 칸 뒤`·`위치 −`·`위치 +` 버튼으로 이동합니다.
4. 잘못 옮겼으면 `Ctrl+Z`로 그 묶음 전체를 한 번에 되돌립니다.

현재 파트를 모두 옮기려면 `Ctrl+A` / `Cmd+A` 후 방향키를 누릅니다. **화면 밖 노트와 임시 기록도
모두 포함**하므로 보이는 몇 개만 바꾸려면 영역 선택을 사용합니다. 예를 들어 120 BPM·4/4에서
상단 스냅이 `1/16음표`이면, `노트 편집`의 아래 방향키 한 번으로 전체가 0.125초 늦춰집니다.

| 뷰 | 한 스냅 앞 / 뒤로 이동 | 입력 위치 -1 / +1 |
|---|---|---|
| 세로 `노트 편집` | 위 / 아래 | 왼쪽 / 오른쪽 |
| 가로 `파트 개요` | 왼쪽 / 오른쪽 | 위 / 아래 |

**이동량·파트를 지정해서 수정하기**

1. 두 개 이상 선택하고 오른쪽 `노트` 탭을 엽니다.
2. `이동(4분음표 박)`에 박자 이동량을 넣습니다. `1`은 4분음표 한 박, `0.25`는 그 1/4이며,
   음수는 앞쪽 이동입니다. 필요하면 `추가 이동(ms)`와 `위치 이동`을 더합니다.
3. 다른 파트로 바꾸려면 `음악 파트`를 고릅니다. 위치·시간만 바꿀 때는 `기존 파트 유지`로 둡니다.
4. `미리보기`로 주황색 예상 위치를 확인한 뒤 `선택에 적용`을 누릅니다. 미리보기만으로는
   원본이 바뀌지 않습니다. 방향키와 한 칸 이동 버튼은 미리보기 단계 없이 바로 수정합니다.
5. 반영된 노트는 저장하고, 임시 기록은 필요한 수정을 마친 뒤 `차트에 반영`하고 저장합니다.

선택·이동 범위는 다음과 같습니다.

- `노트 편집`의 영역 선택은 현재 파트, `파트 개요`는 드래그를 시작한 파트 행 안에서 작동합니다.
  노트의 시작점뿐 아니라 표시된 경로가 영역과 겹쳐도 선택됩니다. Shift+드래그는 선택에 추가합니다.
- 왼쪽 파트를 바꾸면 선택이 해제됩니다. 시간순 정렬이 바뀌어도 선택한 노트 자체를 따라갑니다.
- 박자 이동은 가장 이른 선택 노트를 기준으로 시간 이동량을 구해 모두에게 똑같이 적용합니다.
  템포 경계를 지나도 묶음의 시간 간격과 홀드·슬라이드·바나나 길이를 늘이거나 줄이지 않습니다.
- 위치 이동은 슬라이드 노드, 플릭 끝점, 바나나 핸들·체크포인트를 함께 옮깁니다.
  일부라도 시간·위치 범위를 벗어나면 전체 수정을 거절합니다. 하단 메시지를 보고 이동량을 줄입니다.
- 반영된 노트와 임시 노트를 함께 선택해도 임시 기록이 자동 반영되지는 않습니다. 변경 전 전체를
  검사하고 한 번에 수정하며, 한 번의 Undo로 복원됩니다. 임시 기록의 원본 입력 시각도 유지됩니다.
- 파트나 시간을 바꿔도 활성 구간이 자동으로 늘어나지는 않습니다. 필요하면 `파트` 탭에서
  `현재 루프를 활성 구간으로`를 사용해 게임에서 사용할 구간을 맞춥니다.

ms 단위로 조금만 옮길 때는 노트 하나를 선택해 `미세 이동(ms)` 값을 설정한 뒤, 원하는 묶음을
선택하고 Alt+시간 방향키를 사용합니다. 템포 정보가 없으면 박자 이동을 사용할 수 없으므로
Alt+방향키의 ms 이동을 사용합니다.

### Retrying the Last Take

테이크는 **녹화 시작부터 중지까지 만든 한 묶음**입니다. 자동으로 반복한 여러 루프도 중간에
중지하지 않았다면 같은 테이크에 속합니다.

1. 녹음을 잘못했으면 `■ 녹화 중지` 또는 `Esc`를 누릅니다.
2. 상단 `테이크 재녹음` 또는 `Shift+R`을 누릅니다. 마지막 테이크의 임시 노트만 제거하고
   같은 시작 지점과 설정으로 다시 녹음합니다.
3. 카운트인·프리롤 후 다시 입력합니다. 이전에 중지해 두었던 다른 테이크는 그대로 남습니다.
4. 재녹음 자체를 취소하고 이전 기록을 복구하려면 재녹음 시작 변경까지 `Ctrl+Z`로 되돌립니다.
   시작 직후 새 입력 전에는 한 번으로 복구하지만, 이후 노트를 추가하거나 수정했다면 그 작업들도
   순서대로 되돌려야 합니다. 재녹음 변경을 Undo하면 진행 중인 대체 녹음도 취소됩니다.
   Redo는 기록 변경만 되돌리고 음악이나 녹음을 자동으로 시작하지 않습니다.

복원되는 설정은 파트, 녹화 유형, 플릭 기본 방향, 시작 방식·위치, 반복 구간, 카운트인,
녹음 중 메트로놈 사용 여부, 박자 보정 설정입니다. `현재 위치부터`로 시작했던 테이크도
현재 멈춘 위치가 아니라 **그 테이크를 처음 시작한 위치**로 돌아갑니다.

이 기능은 같은 차트의 **아직 반영하지 않은 마지막 테이크**에만 사용할 수 있습니다.
차트에 반영한 테이크는 다시 지우지 않으며, 차트를 바꾸면 재녹음 이력이 해제됩니다.
원래 파트와 템포 정보, 준비된 FMOD 재생이 필요합니다. 재생 예약에 실패하면 이전 기록을 유지합니다.
새 설정으로 별도 테이크를 추가하려는 경우에는 원하는 설정을 고르고 `R` 또는 `● 녹화`를 사용합니다.

### Pattern Duplication

- bar-aligned duplication of the selected musical part from the current loop
- a target start bar and repeat count, with the next bar selected by default
- ghost-note preview plus source, generated, inactive, and occupied-target counts before apply
- conflict policies that abort, replace the selected part in the target range, or keep existing notes
- optional target activation-window creation and normalization
- stable new note IDs, global time sorting, chart validation, explicit saving, and Unity Undo
- rejection when source and target overlap or when the target uses a different time signature

### Musical-Part Audition

- chart-defined stem volume audition
- selected-part soloing
- creating the selected part's activation window from the current loop
- activation-window overlap validation
- an Editor normalization command that merges duplicate, overlapping, or adjacent windows for the same part

### Count-In, Metronome, and Quantization

- one- or multi-bar count-in with weak clicks and accented downbeats
- the top `메트로놈` checkbox controls ongoing recording clicks; count-in clicks play independently
- starting another take or a standalone-count-in loop resets the metronome cursor;
  its first recording click is scheduled ahead with the count-in on the same DSP clock
- metronome beats continue through audio time zero before a delayed bar-1 downbeat, preserving
  beat spacing and accents without changing the chart's first-downbeat time
- musical pre-roll when enough earlier song time exists, using a fresh DSP-scheduled start for
  explicit attempts and automatic loop re-entry
- virtual negative-song-time count-in when requested pre-roll extends before song time zero
- loop recording that repeats through pre-roll rather than jumping directly to the first note
- tempo-aware quantization to straight or triplet grids
- configurable quantization strength, maximum correction distance, input-time advance, and near-simultaneous chord grouping
- optional automatic quantization when recording stops
- preservation and restoration of the original DSP-backed input time after quantization

### Tempo Calibration

- default `현재 BPM 고정 · 시작점만 보정` mode preserves the selected chart's BPM;
  disable it to estimate BPM and origin together
- manual two-anchor calibration
- successive-bar downbeat tapping
- least-squares BPM and first-downbeat derivation across captured anchors
- candidate-grid metronome preview
- millisecond phase adjustment
- an explicit Undo-supported tempo-map apply step for a chart with exactly one tempo section;
  it preserves absolute note times and explicit banana checkpoints while changing the musical grid

For fixed-BPM origin calibration, set `시작 마디` to the first bar you will tap and
`탭 간격(마디)` to the number of bars between taps (1 for every bar, 4 for every fourth bar).
Start playback/capture and press Space on each requested bar's first beat. After at least two
taps, the tool averages `measured song time - beat index * seconds per beat` across all taps;
it reports the fixed BPM, candidate first-downbeat time, and RMS residual. Stop capture, audition
the candidate metronome, optionally adjust the millisecond phase, apply, then save the chart.
Capture numbering is locked while measuring. Use a constant-tempo section before a song's
tempo change. RMS measures disagreement between taps, not absolute accuracy: consistent human
reaction delay remains and needs listening/phase verification. Applying fixed-BPM calibration
does not write the BPM field. Manual A/B anchors remain available in a foldout.

The tempo pane stacks labeled inputs, transport actions, and wrapped summaries vertically within
the available inspector width, reserving room for its vertical scrollbar.

### Data Safety

- Unity Undo for supported chart changes
- applying temporary records, their applied state, and optional buffer clearing share one Undo/Redo
  operation, so restoring the chart also restores the corresponding recording buffer
- incomplete hold/slide/banana input is transient session state rather than serialized Undo data;
  completed temporary records remain serialized and Undo-supported
- missing or unknown musical-part IDs are displayed as unresolved; merely showing a list or note
  inspector never assigns them to the first part
- temporary records and the complete proposed chart are validated before apply changes the chart;
  rejection preserves the chart, temporary records, applied state, and Undo history
- explicit asset saving
- visual separation of applied notes and buffered notes
- a timeline showing beat/bar lines, playhead, loop range, musical-part activation, applied notes, and buffered notes

## Metronome and Count-In Timing

The authoring metronome is an Editor guide backed by the shared runtime `FmodMetronome`;
it never replaces FMOD song time as the input-recording source. Its scheduling rules are defined
in `RHYTHM_SYSTEM.md`.

When count-in extends before song time zero, virtual negative chart time is used only to place guide
clicks and schedule the real song start on one FMOD DSP clock. It does not create negative runtime
note times or a second gameplay timeline.

Positive pre-roll also uses the existing scheduled-playback API rather than live audition `Seek`.
Its first guide click follows the returned DSP start clock; subsequent click scheduling uses signed
timeline time so the preparation lead is retained before the gate. Recording activates only after
the scheduled start has been reached and the target song time arrives. This does not fix or verify
the separate live/paused navigation path.

## Apply and Replay Safety

**반영과 저장의 차이**

| 지금 수정한 것 | 저장까지 필요한 조작 |
|---|---|
| 녹음으로 만든 임시 기록 | 하단 `차트에 반영` → 검사 → 상단 `저장 필요` |
| 이미 반영된 차트 노트 | 수정이 차트에 바로 적용됨 → 검사 → 상단 `저장 필요` |
| 임시·반영 노트 혼합 선택 | 차트 쪽은 바로 수정되지만 임시 쪽은 별도 반영 필요 → 검사 → 저장 |
| 일괄 수정·박자 보정 등의 미리보기만 한 상태 | 해당 기능의 적용 버튼을 눌러 확정한 뒤 위 절차 진행 |

`저장됨`은 **차트 에셋의 저장 상태**입니다. 하단에 남은 미반영 기록까지 저장됐다는 뜻은 아닙니다.
임시 기록을 펼쳐 `반영 후 비우기`를 켜면 성공적으로 반영한 기록을 비웁니다. 꺼 두면 기록이 남고,
같은 버퍼를 다시 반영하려 할 때 중복 반영 확인이 나옵니다. `버리기`는 임시 기록 전체를 비우므로
마지막 테이크만 다시 만들 때는 `테이크 재녹음`을 사용합니다.

**추가와 구간 교체**

- `추가`: 기존 채보를 유지하고 임시 기록을 더합니다.
- `이 구간의 기록 파트 교체`: 임시 기록에 들어 있는 **모든 파트**를 대상으로, 기존 노트의
  시작 시각이 현재 루프의 `[시작, 끝)`에 있으면 교체합니다. 왼쪽에서 선택한 한 파트만의 작업이
  아닙니다. 다른 파트도 녹음해 두었다면 반영 전에 펼친 임시 목록과 반복 구간을 확인합니다.
- 활성 구간이 부족하면 하단의 `누락 활성 구간 추가` 옵션 또는 `파트` 탭의 구간 설정을 확인합니다.
  선택 파트만 듣는 설정은 활성 구간을 만들어 주지 않습니다.

반영 전에는 임시 기록과 완성될 차트 전체를 검사합니다. 오류가 나면 차트와 기록을 유지하므로
하단 메시지에서 지적한 파트·시각·경로를 고친 뒤 다시 반영합니다. 성공한 반영과 버퍼 비우기는
하나의 Undo/Redo로 복원됩니다. 파트를 알 수 없는 기록은 올바른 파트를 직접 지정해야 하며,
이름이 `Drum`이거나 시작 시각이 0이라는 이유만으로 원래 의도를 추정해 고치지 않습니다.

**게임에서 확인하기**

1. 필요한 임시 기록을 모두 반영하고 `검사` 후 `저장 필요`를 눌러 저장합니다.
2. 채보 제작 도구 창을 닫고 Unity의 Play Mode를 종료합니다.
3. Gameplay 씬에서 `GameplaySession` 컴포넌트의 `Chart`가 방금 저장한 에셋인지 확인합니다.
   도구 상단에서 차트를 바꿔도 게임 씬의 `Chart` 참조가 자동으로 바뀌지는 않습니다.
   다르면 Play Mode 밖에서 올바른 에셋을 지정합니다.
4. 채보 도구를 닫아 둔 상태로 Play Mode를 다시 시작합니다. 창이 열려 있으면 도구가 음원을
   준비하며 게임 세션을 다시 비활성화할 수 있습니다.
5. 게임 화면에서 노트 속도와 준비 길이를 정하고 `START` 또는 Enter/Space로 시작합니다.
   편집한 노트의 타이밍·경로·파트 활성 구간을 플레이로 확인합니다.

이렇게 다시 진입해야 저장한 차트에서 게임의 노트와 진행 상태를 새로 만듭니다. 도구의
`재생`·`녹화` 버튼은 게임 화면의 START와 별도 동작입니다.


## Current Interaction Authoring

This section defines the implemented workflow for `NOTE_INTERACTIONS.md`. Its deterministic data
and preservation paths have automated coverage; authoring speed and physical play feel still need
the manual evidence listed in `BACKLOG.md`.

### Recording Modes

`녹화 유형` 메뉴는 `탭`, `홀드·슬라이드`, `플릭`, `바나나` 네 가지이며, 이 메뉴로 다섯 종류의
노트를 만듭니다. 숫자열과 숫자패드를 사용할 수 있고, 입력 위치는 차트에 존재하는 위치 중
1–8번까지입니다. 아래 예시는 해당 번호의 위치가 있는 차트를 기준으로 합니다.

| 만들 노트 | 메뉴 | 음악에 맞춰 누를 키 예시 | 결과 |
|---|---|---|---|
| 탭 | `탭` | `3` | 누른 시각에 3번 위치 탭 |
| 홀드 | `홀드·슬라이드` | 시작에 `3`, 끝에 다시 `3` | 두 입력 시각 사이를 유지하는 홀드 |
| 슬라이드 | `홀드·슬라이드` | `3 → 5 → 2 → 2` | 3번에서 시작, 5번·2번으로 이동, 마지막 2번 입력에 종료 |
| 종단 플릭 슬라이드 | `홀드·슬라이드` | `3 → 1 → 0` | 마지막 3→1 이동을 플릭으로 확정. 판정 시각은 `1` 입력 시각 |
| 플릭 | `플릭` | 방향 선택 후 `3` | 누른 시각에 3번에서 선택 방향의 인접 위치로 향하는 플릭 |
| 바나나 | `바나나` | 시작에 `3`, 끝에 `6` | 두 입력을 시작·끝으로 하는 곡선 노트. 곡선은 이후 수정 |

화살표는 차례로 **눌렀다 떼는 별도 입력**을 뜻합니다. 홀드·슬라이드를 만들 때 키를 계속 누르고
있다가 떼는 방식이 아닙니다. 완성되지 않은 홀드·슬라이드·바나나는 녹음 중지·루프 종료 시
기록으로 남지 않으므로 끝 입력을 먼저 합니다.

#### Tap Recording

- 만들 위치의 숫자를 한 번씩 누릅니다. 각 입력의 FMOD 곡 시각으로 임시 탭이 생성됩니다.
- 녹음 후 박자 보정·파트 지정·차트 반영·저장은 다른 노트와 같습니다.

#### Slide and Hold Recording

1. 시작할 위치의 숫자를 누릅니다.
2. 다른 위치로 옮기는 시점에 그 숫자를 누릅니다. 그전까지는 이전 위치를 유지합니다.
3. **현재 마지막 위치의 숫자를 다시** 누르면 그 시각에 종료됩니다. 한 번도 옮기지 않았다면
   홀드가 됩니다. 예를 들어 `3 → 3`을 네 쌍 입력하면 홀드 네 개가 만들어집니다.

마지막 이동을 종단 플릭으로 만들려면 다른 위치로 한 번 이상 이동한 뒤 `0`을 누릅니다.
예를 들어 `3 → 1 → 0`의 종료·플릭 시각은 **1을 누른 시각**입니다. `0`은 새 시각·노드를
추가하지 않고 마지막 이동을 플릭으로 바꾸며, 마지막 위치에서 더 유지하던 임시 구간은 제거합니다.
일반 플릭과 종단 플릭 모두 여러 위치를 건너뛸 수 있습니다.

#### Flick Recording

1. `플릭` 모드에서 기본 방향을 `왼쪽` 또는 `오른쪽`으로 고릅니다.
2. 판정시킬 박자에 시작 위치 숫자를 한 번 누릅니다. 선택 방향의 인접 위치에 끝점이 생깁니다.
3. 여러 위치를 건너뛰는 플릭은 녹음 후 노트를 선택하고 상세 경로의 끝점을 다른 위치로 옮깁니다.

가장자리에서 바깥을 향하는 입력은 거절됩니다. 예를 들어 1번 위치에서는 왼쪽 플릭을 만들 수
없습니다. PC 게임 테스트용 플릭 성공 보조 키는 채보 녹음이나 실제 제스처 검증용이 아닙니다.

### Post-Recording Slide Editing

- start, middle, and end nodes can be selected and dragged to another lane or time
- clicking a hold body creates a new middle node at the clicked absolute time and its held lane
- the new node time uses the active quantization/snap rules
- holding `Alt` while dragging or inserting bypasses musical snapping for a precise free-time edit
- transition connectors represent the destination node's existing time; their visual corners are
  not separate editable or reward-bearing nodes; clicking a connector selects its destination node
  for dragging rather than inserting a duplicate-time node
- the middle-node insertion button also begins in the preceding held lane; insertion requires the
  existing 10 ms minimum spacing from adjacent nodes
- start/end time collisions and non-increasing node times are rejected
- quarter-beat validity checks, half-beat reward ticks, and authored middle-node rewards are visible
  in preview
- a middle node and half-beat tick at the same time remain separate rewards even if the preview
  shares one positional marker

### Applied Interaction Editing

Applied tap, hold, slide, flick, and banana notes remain editable after the temporary recording buffer is
cleared. Selecting an applied note opens type-specific detail editing, including the path canvas
where the interaction has endpoints or nodes. Changes write to the selected `PrototypeChart`
immediately through Unity Undo, mark the chart dirty, run contextual chart validation, and still
require the top `저장 필요` action for permanent persistence.

The horizontal and vertical timelines draw each supported interaction shape:

- hold start, body, and end
- every slide hold body, timed lane-change connector, and normal or flick terminal marker
- flick start-to-end direction
- banana curve and its start/end markers, with optional explicit checkpoint markers

In the vertical timeline, slide holds are vertical and lane-change connectors are horizontal. In
the horizontal timeline and path canvas, time runs horizontally, so that geometry is transposed.
An authored node marks arrival in the new lane. Changing the view, zoom, or scroll direction does
not change the node times, and preview check/reward marks follow the held lane rather than a
diagonal interpolation. The normal-end node can be edited to a different lane; that represents a
final lane change at the end time.

The selected-note canvas provides detailed path editing while the center retains the complete
chart context. The same properties are available for temporary and applied banana notes.

### Banana Authoring

`바나나` 모드에서 시작 박자에 첫 숫자, 끝 박자에 두 번째 숫자를 누릅니다. 완성한 노트를
선택하고 오른쪽 `노트` 탭의 경로 캔버스에서 곡선 핸들을 드래그해 모양을 다듬습니다.
`곡선 핸들`과 체크포인트 상세 항목을 펼치면 수치로 수정할 수 있습니다.
중앙 타임라인의 체크포인트를 직접 드래그하는 기능은 없으므로 개별 시각·위치는 상세 필드에서 고칩니다.
곡선 모양과 체크포인트 데이터는 별개이며, 핸들을 옮겼다고 기존 체크포인트가 자동 재생성되지는 않습니다.

- in banana recording mode, the first lane key places the start and the second places the end
- closing a banana creates one editable curve handle and explicit global quarter-beat checkpoints
- place tap-style start and end points at authored lanes and absolute times
- adjust one or two curve handles in normalized playfield space through canvas dragging or the
  `곡선 핸들` detail fields; dragging a handle preserves the authored checkpoint data
- generate checkpoints from the chart tempo map at a quarter-beat default or an eighth-beat option
- allow generated subdivision or count to be changed without changing the maximum bonus combo
- allow individual checkpoint addition, deletion, timing correction, and position correction
- preview the complete curve and explicit checkpoints in the chart and selected-note canvas;
  unfold checkpoint fields for generation and individual correction
- inspect missed/successful charge presentation and resulting bonus behavior during gameplay;
  the authoring canvas does not simulate a player's trace or charge result
- reject temporary-buffer apply with contextual validation errors when endpoints, curve data,
  checkpoint ordering, or reward limits are invalid; the author can correct the temporary fields
  without changing the existing chart

### Overlap and Future Composition

The tool does not reject notes solely because they share a lane and time. It also does not yet
merge them automatically.

A future workflow may combine a flick ending at one lane/time with a slide beginning there to form
a slide that starts with a flick. This is a future possibility only; the current interaction work
must not add speculative merge UI or a generic composition system.

## Troubleshooting

| 상황 | 확인할 것과 다음 조작 |
|---|---|
| `녹화`가 비활성화되어 있음 | 상단 차트 선택, 음악 파트 존재, Gameplay 씬·Play Mode, `도구`의 `준비 완료` 상태를 확인합니다. 준비가 끝나지 않으면 차트의 FMOD 이벤트와 씬의 재생 컴포넌트를 확인합니다. |
| 카운트인이나 박자 이동을 사용할 수 없음 | 차트에 유효한 템포 정보가 있는지 확인합니다. 숫자·텍스트 필드 편집 중이면 캔버스로 포커스를 돌립니다. 박자 이동 대신 ms 이동이 필요하면 Alt+시간 방향키를 사용합니다. |
| 숫자를 눌렀는데 노트가 남지 않음 | 카운트인 이후 실제 녹음 중인지, 번호가 차트의 입력 위치 범위 안인지 확인합니다. 홀드·슬라이드·바나나는 끝 입력이 있어야 완료됩니다. |
| `Space`로 녹음이 중지되지 않음 | `Esc` 또는 `■ 녹화 중지`를 사용합니다. 곡까지 멈추려면 `정지`를 누릅니다. |
| `테이크 재녹음`이 비활성화됨 | 먼저 녹음을 중지합니다. 같은 차트의 마지막 미반영 테이크인지, 원래 파트·템포가 있고 음원이 준비됐는지 확인합니다. 반영한 테이크는 이 기능으로 다시 만들 수 없습니다. |
| 일괄 수정이 거절됨 | 하단 메시지를 확인합니다. 홀드 끝이나 곡선 핸들 등 경로 일부가 범위를 벗어날 수 있으므로 이동량을 줄이거나 선택을 나눕니다. |
| 파트를 바꾼 노트가 보이지 않음 | 이동시킨 음악 파트를 왼쪽에서 선택하거나 `파트 개요`에서 확인합니다. 활성 구간도 새 파트에 맞게 설정합니다. |
| `저장됨`인데 게임에서 새 노트가 없음 | 하단 임시 기록의 반영 여부를 확인하고 다시 저장합니다. 도구를 닫고 게임의 `Chart` 참조를 확인한 뒤 Play Mode를 재진입합니다. |
| 게임 START나 노트 생성이 동작하지 않음 | 채보 도구를 닫은 상태로 Play Mode를 다시 시작합니다. 도구가 열려 있으면 작업용으로 게임 세션을 비활성화할 수 있습니다. |
| 위치 이동 후 소리와 표시가 어긋남 | 일반 재생·일시정지 상태의 Seek는 알려진 문제입니다. 이번에 검증한 녹음 시작·테이크 재시도와 구분하고, [현재 제한](#current-limitations)을 참고합니다. |

오류가 나면 현재 임시 기록을 먼저 확인합니다. `버리기`나 새 차트 선택으로 전체 기록을 비우기 전에,
오류가 난 노트만 고칠 수 있는지 또는 마지막 테이크 재녹음으로 해결할 수 있는지 판단합니다.

## Current Limitations

The live `Seek` transport exists but has a recorded stale-anchor/streaming finding. Corrected
scheduled starts do not establish live/paused seek correctness; see
[IT-P0-003](BACKLOG.md#it-p0-003--verify-pause-resume-restart-and-seek-boundaries).
Do not treat authoring navigation availability as proof of synchronization.

The current tool does not provide:

- waveforms
- automatic beat analysis
- drag editing of activation-window handles
- keyboard recording beyond the first eight development positions
- a proven variable-tempo workflow beyond direct tempo-section data
- direct checkpoint dragging on the central timeline; individual checkpoint correction uses the
  selected-note fields

These limitations are not automatic feature requests. Prioritize improvements using the measured
authoring cost recorded by `BACKLOG.md` item `IT-P0-009`.

## Authoring Verification

When changing the tool or chart workflow:

1. run relevant EditMode tests
2. verify buffer, apply, Undo, validation, and explicit save behavior
3. confirm absolute note times change only when intended
4. confirm tempo previews do not mutate the chart before apply
5. confirm the edited chart can be replayed after leaving and re-entering Play Mode
6. check the Unity Console
7. record a measured authoring session when the change claims to improve workflow speed

For the workspace layout, also exercise all five right-hand tabs, a selected note in both chart
views, an expanded temporary drawer, and the below-1000-pixel properties switch. Check that viewing
other parts, showing check markers, navigating, or opening panels never changes absolute note times.

The current automated workspace coverage renders both views, all five tabs, and all five note
types in Unity EditorWindow tests, including 700- and 1200-pixel window widths. It checks chart and
buffer preservation, pane bounds, applied-banana serialization and Undo, path selection, and preview
agreement with runtime geometry. These checks do not establish visual readability, complete mouse
and keyboard operation, or an improvement in production speed. Executed results and the remaining
manual checks are recorded in
`Playtests/2026-09-04-authoring-workspace-verification.md`.

The repeated-hold serialization failure, unresolved-part preservation, and transactional buffer
apply regression checks are tracked separately in
`Playtests/2026-09-04-recording-serialization-verification.md`. This evidence does not replace a
timed authoring session or a measurement of physical input/audio latency.

Selection, grouped correction, last-take retry, and their workflow simulations are tracked in the
[2026-09-12 workflow record](Playtests/2026-09-12-authoring-workflow-improvements.md). Use its actual
run results and limitations; automated action counts or simulated elapsed time do not establish
the human authoring-speed hypothesis in `IT-P0-009`.

Use `Playtests/TEMPLATE.md` for authoring evidence.
