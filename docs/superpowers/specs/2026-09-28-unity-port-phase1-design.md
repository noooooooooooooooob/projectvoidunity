# 유니티 이식 1단계 설계 — 전투 규칙 · 2.5D 전투 화면 · AI 스프라이트

- 작성일: 2026-09-28
- 대상: Godot 프로토타입 `C:\Users\User\Desktop\Godot\Project-Void` 를 유니티 `project void test` 로 이식
- 상태: 승인됨 (2026-09-28)
- 구현 계획: `docs/superpowers/plans/2026-09-28-unity-port-phase1.md` — 계획 단계에서 정한 차이는 §12
- 원본 설계: Godot 저장소 `docs/superpowers/specs/` 의 전투(09-12) · 2.5D(09-13) · 카드 연출(09-13) · 이동/턴 단계(09-14) · 맵(09-16) 문서

## 1. 배경과 목표

Godot 4.7 로 만든 턴제 덱빌딩 전투 프로토타입(게임 코드 약 5,200줄, 헤드리스 테스트 27파일)을 유니티 6000.3 (URP) 에서 똑같이 만든다. 최종 목표는 전투 + 카드 연출 + 맵 전체 이식이며, 3단계로 나눈다.

| 단계 | 내용 |
|---|---|
| **1 (이 문서)** | 전투 규칙 전체 + 2.5D 전투 화면 + 단순 HUD + AI 유닛 스프라이트 6종. 한 판 플레이 가능 |
| 2 | 카드 연출: 부채꼴 손패, 드래그 조준 화살표, 덱·묘지 더미, 드로우·리셔플·버리기 연출 |
| 3 | 맵: 분기 노드 맵, 인카운터 생성, 런 상태, GameRoot |

**성공 기준 (1단계)**

1. Godot 규칙 테스트 8파일의 모든 케이스가 NUnit EditMode 로 이식되어 통과한다 (난수 수열 의존 기대값은 §4.3 대로 재산출).
2. `Battle` 씬을 실행해 skirmish 인카운터를 카드 사용 · 이동 · 차례 종료로 끝까지 플레이할 수 있고, 승/패 배너가 뜬다.
3. 유닛 6종이 `format3.png` 레퍼런스 느낌의 64×64 픽셀아트 스프라이트로 보인다.
4. 플레이 중 콘솔 에러 0건.

## 2. 확정 사항 (2026-09-28 사용자 결정)

| 항목 | 결정 |
|---|---|
| 범위 | 전체 이식, 단계별. 1단계 = 규칙 + 2.5D + 스프라이트 |
| 규칙 | Godot 규칙·수치를 그대로. 규칙 코어는 엔진 비의존 순수 C# |
| 스프라이트 | A안: `format3.png` 를 참조 이미지로 Unity AI 생성 → 배경 제거 → 64×64 픽셀 정리. 필요 시 `format3_forComfyui.png`(초록 배경) 사용 |
| 진영 구분 | Godot 의 유닛 색조(파랑/빨강 틴트)는 쓰지 않는다. 실제 아트를 살리고 발밑 그림자 색으로 구분 |
| 적 방향 | 스프라이트는 오른쪽 방향 1장. 적은 `flipX` |
| UI | uGUI + TextMeshPro, 한글 폰트 Noto Sans KR |
| 버전 관리 | git 초기화 (기존 Plastic 워크스페이스와 서로 무시) |

## 3. 아키텍처

Godot 의 3계층(표현 / 규칙 코어 / 데이터)을 그대로 쓴다.

```
규칙 코어 (ProjectVoid.Combat 어셈블리, UnityEngine 은 ScriptableObject 데이터 참조만)
   BattleState / Unit / TargetResolver / EnemyBrain
   │ C# event (동기)
   ▼
BattleEventRecorder ── List<BattleEvent> (신호 시점 스냅샷)
   │
   ▼
BattleRoot (입력 잠금) ──► BattlePlayback (코루틴 순차 재생) ──► Board3D / UnitView / BattleHud
   ▲                                                                   │
   └──── 타일 클릭 · 카드 선택/드롭 · 이동 모드 · 차례 종료 ◄────────────┘
```

규칙은 표현을 모른다. 표현은 규칙 상태를 읽기만 하고, 변경은 `StartBattle()` / `PlayCard()` / `MoveUnit()` / `EndTurn()` 호출로만 한다.

### 3.1 폴더와 어셈블리

```
Assets/_Project/
  Scripts/Combat/            ProjectVoid.Combat.asmdef
    BattleState.cs Unit.cs TargetResolver.cs EnemyBrain.cs
    Data/ CardData.cs UnitData.cs AllyData.cs EnemyData.cs EncounterData.cs UnitPlacement.cs
  Scripts/View/              ProjectVoid.View.asmdef (→ Combat, Unity.InputSystem, TMP)
    BattleEvent.cs BattleEventRecorder.cs BattlePlayback.cs
    BattleRoot.cs Board3D.cs BoardLayout.cs UnitView.cs Billboard.cs
  Scripts/UI/                (View 어셈블리에 포함)
    BattleHud.cs
  Scripts/Editor/            ProjectVoid.Editor.asmdef (Editor 전용)
    PixelSpriteProcessor.cs  DataImporter.cs(초기 .asset 생성용, 1회성)
  Data/Cards/ Data/Units/ Data/Encounters/   *.asset
  Art/Reference/format3.png
  Art/Units/{id}.png         (최종 64×64)
  Art/Units/Raw/{id}.png     (AI 원본 보존)
  Fonts/NotoSansKR SDF.asset
  Scenes/Battle.unity
  Tests/EditMode/  ProjectVoid.Tests.EditMode.asmdef
  Tests/PlayMode/  ProjectVoid.Tests.PlayMode.asmdef
```

### 3.2 Godot → C# 대응 규칙

| Godot | C# |
|---|---|
| `class_name X extends RefCounted` | `public sealed class X` |
| `Resource` (.tres) | `ScriptableObject` (.asset) |
| `signal foo(a, b)` | `public event Action<A, B> Foo;` |
| `Vector2i` | `UnityEngine.Vector2Int` |
| `RandomNumberGenerator` (시드) | `System.Random` (시드 주입) |
| `StringName` id | `string` |
| enum | 같은 이름·순서의 C# enum |
| `snake_case` 함수 | `PascalCase` 메서드, 동작은 1:1 |

메서드 단위로 Godot 원본과 1:1 대응시켜, 원본 파일을 옆에 펴 놓고 비교 가능하게 한다. 동작 개선이나 리팩토링은 하지 않는다.

## 4. 규칙 코어

### 4.1 이식 대상

Godot `Scripts/combat/` 전체와 `data/` 전체. 규칙 내용(거리 계산, 전열 블로킹, 카드 모양 SINGLE/PIERCE/SWEEP/AREA/LINE, 턴 단계 STANDBY→DRAW→ACTION→BEFORE_END→AFTER_END, 드로우 4장·리셔플, SP, 이동, 적 AI 의 `move_chance` 판정과 휴식→방어→공격 규칙)은 Godot 원본 코드와 원본 설계 문서가 기준이며, 이 문서는 다시 적지 않는다.

신호는 현재 Godot `battle_state.gd` 에 있는 것 전부를 event 로 옮긴다 (턴 시작, 단계 시작, 카드 사용, 적 행동, 피해, 회복, 방어도, 사망, 로그, 전투 종료, 드로우, 리셔플, 손패 버리기, 이동).

### 4.2 데이터

Godot `.tres` 의 수치를 그대로 `.asset` 으로 옮긴다: 카드 7종(strike, cleave, skewer, shoot, piercing_shot, volley, blast), 아군 3종(vanguard, archer, scout — 덱 구성 포함), 적 3종(brute, sentry, stalker), 인카운터 `skirmish` (아군 3×3, 적 2×2, 배치 좌표 동일). 생성은 에디터 스크립트 `DataImporter` 로 한 번 하고, 이후에는 인스펙터에서 편집한다. `UnitData` 에 `Sprite sprite` 필드를 추가한다 (Godot 에는 없음, 표현용).

### 4.3 난수

`System.Random` 은 Godot RNG 와 수열이 다르다. 같은 시드라도 Godot 과 같은 판이 나오지 않는다. 테스트 중 특정 난수 결과에 기대값을 건 케이스(셔플 순서, 적 이동 확률)는 "Godot 에서 이 테스트가 검증하려던 성질"을 유지하도록 C# 쪽에서 기대값을 다시 산출하고, 그 사실을 테스트 주석에 남긴다. 난수 소비 순서(어떤 호출이 몇 번 뽑는지)는 Godot 과 같게 유지한다.

## 5. 이벤트 기록 · 재생

- `BattleEvent`: Godot `battle_event.gd` 의 `Kind` 13종과 스냅샷 필드(hp, block, 라운드, 행동 순서와 생존 여부, 턴 인덱스, 덱·묘지 장수, 이동 전후 칸, 당시 칸 등)를 그대로. 드로우 · 리셔플 · 버리기도 1단계에서 기록한다 (2단계가 그대로 사용).
- `BattleEventRecorder`: `BattleState` event 를 구독해 스냅샷을 만든다. `TakeEvents()` 로 비우며 반환.
- `BattlePlayback`: 코루틴으로 한 개씩 재생. 동시 광역 피해 묶음은 병렬 재생 후 모두 끝날 때까지 대기. 대기 상수(`ENEMY_TURN_PAUSE` 0.2, `DAMAGE_TIME` 0.5 등)는 Godot 값 그대로. `instant` 플래그면 대기 없이 즉시 상태만 반영 (테스트용). 1단계에서 드로우/리셔플/버리기 이벤트는 HUD 손패 갱신만 한다.
- `BattleRoot`: 입력 → 규칙 호출 → `TakeEvents()` → 재생, 재생 중 입력 잠금, 끝나면 규칙 상태로 전체 동기화 후 잠금 해제. 흐름은 Godot `battle_root.gd` 의 `_run` 과 같다.

## 6. 2.5D 화면

- **BoardLayout**: `TILE_SIZE` 1.0, `CELL_PITCH` 1.1, `SIDE_GAP` 1.5. 아군 좌측·col 0 이 중앙 쪽이 되도록 미러링은 여기서만. 좌표계는 유니티 기준(Y 위, 보드는 XZ 평면)으로 옮긴다.
- **Board3D**: 타일 = 얇은 Cube(두께 0.1) + URP Lit 머티리얼 인스턴스. 진영 기본색과 상태 8종(BASE, EMPTY, CURRENT, VALID, INVALID, MOVABLE, SHAPE_HIT, SHAPE_OUT)의 Emission 색은 Godot 값 그대로. 타일 위 이유 글자는 TMP 3D. 클릭 · 호버는 Input System `Mouse.current` + `Physics.Raycast`, 레이어 `Tile` / `UnitPick` 분리. `CellClicked`, `PickMissed`, `CellHovered`, `HoverCleared` event.
- **UnitView**: `SpriteRenderer` (Point 필터, 기준점 발밑) + `Billboard` (카메라 방향 Y축 회전만). 높이 1.6 월드 단위로 스케일. 머리 위 이름 · HP 바 · 방어도, 발밑 그림자 원판(아군 청색 / 적 적색 계열). 연출 — 돌진(0.4), 튀어오르기, 피격 번쩍임+흔들림, 떠오르는 숫자, 사망 페이드, 이동 미끄러짐 — 은 코루틴 직접 구현, 시간 상수는 Godot 값. 스프라이트가 비어 있으면 임시 실루엣으로 대체.
- **카메라**: 원근, 피치 44°, FOV 40°, 여백 1.8, 타깃 오프셋 (0, 0, 0.6) 상당. 보드 크기에 맞춰 거리 자동 계산 (Godot `_frame_camera`).
- **입력**: 카드 버튼 클릭 → 적 타일 클릭, 또는 카드 버튼 드래그 → 적 위 드롭(1단계는 화살표 없음, 빈 곳이면 취소). 이동 모드 토글 → 파란 칸 클릭. 호버 시 카드 범위 미리보기와 사거리 이유 글자.

## 7. HUD (uGUI + TMP)

- 상단: 라운드 · 행동 순서 바 (현재 차례 강조색 `(1.0, 0.82, 0.3)`, 행동 완료 회색, 사망 유닛은 Godot 처럼 목록에서 뺀다)
- 하단 중앙: 손패 = 카드 버튼 줄 (이름, SP 비용, 근접/원거리 테두리색, SP 부족 흐림, 선택 시 들어올림)
- 우하단: SP, 이동 모드 토글, 차례 종료
- 좌측: 자동 스크롤 로그
- 승패 배너 + "다시 하기" (씬 재시작)
- 한글: Noto Sans KR 로 TMP SDF 폰트 에셋 생성 (동적 아틀라스)

## 8. 스프라이트 생성 파이프라인

1. `format3.png` → `Art/Reference/` 임포트.
2. `gpt-image-1-5`, `GenerateSprite`, 참조 이미지 = 레퍼런스, 6종 병렬. 공통 프롬프트: "pixel art character sprite in the exact style of the reference: same chibi proportions (large head, ~2.5 heads tall), 1px black outline, pale skin palette with red rim shading, 3/4 view facing right, full body standing, plain background". 유닛별 추가:
   - vanguard 선봉: heavy armored warrior with shield and one-handed sword
   - archer 사수: light-armored archer holding a bow
   - scout 정찰병: agile hooded scout with twin daggers
   - brute 괴한: hulking thug with a wooden club
   - sentry 보초: guard in a helmet holding a crossbow
   - stalker 추적자: masked assassin with throwing knives
3. `RemoveSpriteBackground`. 잔여 배경이 심하면 초록 배경 레퍼런스로 재생성 후 크로마 제거.
4. `PixelSpriteProcessor` (에디터 메뉴): 알파 기준 내용 영역 크롭 → 발밑 중앙 기준 64×64 캔버스에 최근접 축소 → 알파 0/255 이진화. 원본은 `Raw/` 에 보존. 임포트 설정: Sprite, Point, 압축 없음, PPU 64, 피벗 Bottom Center.
5. 레퍼런스와 나란히 캡처해 사용자 확인. 불만족 유닛만 재생성.

## 9. 테스트

- **EditMode** (NUnit): Godot `test_battle_state`, `test_unit`, `test_target_resolver`, `test_enemy_brain`, `test_movement`, `test_turn_phases`, `test_turn_order`, `test_data` 의 모든 케이스 이식. 추가로 `BattleEventRecorder` (`test_event_recorder` 이식), `BoardLayout` (`test_board_layout` 이식).
- **PlayMode**: `Battle` 씬 로드 → `instant` 재생 → 스크립트로 카드 사용·차례 종료를 반복해 전투 종료까지 진행, 배너 표시와 예외 없음 확인 (스모크 1개).
- **수동/MCP 확인**: 카메라 캡처로 화면 확인, 콘솔 에러 0건.

## 10. 오류 처리

규칙이 거부하는 입력(사거리 밖, SP 부족, 막힌 칸, 차례 아님)은 Godot 과 같이 규칙이 판정하고 로그 이벤트로 알린다. 표현 쪽은 예외를 던지지 않고 무시한다. 스프라이트 누락은 임시 실루엣 대체.

## 11. 범위 밖 (1단계)

부채꼴 손패·조준 화살표·더미·카드 이동 연출(2단계), 맵·런·인카운터 생성·GameRoot(3단계), 전투 배경 이미지, 사운드, 파티클, 스프라이트 애니메이션 프레임, Godot MCP 브리지 addons(유니티는 Unity MCP 사용).

## 12. 계획 단계에서 정한 차이

| 항목 | 스펙 원안 | 계획 | 이유 |
|---|---|---|---|
| 피격 번쩍임 색 | Godot 과 같은 흰색 번쩍임 | 붉은색 `(1, 0.45, 0.45)` | `SpriteRenderer.color` 는 1 을 넘길 수 없어 흰 스프라이트 위 흰 번쩍임이 안 보인다 |
| 클릭 판정 레이어 | `Tile` / `UnitPick` 레이어 분리 | 레이어 없이 `CellTag` 컴포넌트로 판정 | 씬에 판정용 콜라이더만 있어 레이어 설정이 불필요 |
| 테스트 이식 범위 | 규칙 8파일 + 기록기 + 레이아웃 | 여기에 `test_battle_signals`, `test_card_zone_signals` 추가 | 둘 다 규칙 신호 순서를 검증하는 규칙 테스트 |
| 테스트 실행 | (미정) | `TestBridge` 로 MCP 에서 실행, 결과는 `Temp/ProjectVoidTestResults.txt` | 에디터가 열려 있어 커맨드라인 러너를 쓸 수 없다 |
| 스프라이트 모델 | `gpt-image-1-5` | 동일, 배경 제거는 `photoroom-bg-removal` | — |
