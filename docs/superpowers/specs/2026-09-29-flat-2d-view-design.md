# 2D 전투 화면 설계 (Battle2D 씬)

날짜: 2026-09-29 · 브랜치: `unity-port-phase1`

## 목적

지금의 3D(원근 카메라, 3D 바닥·벽·조명, 카메라를 향한 판 유닛) 전투 화면과 **비슷한 구도**를 진짜 2D로 표현한 버전을 만든다. 둘을 비교해 고를 수 있도록 **3D 씬은 그대로 두고** `Battle2D` 씬을 따로 둔다.

사용자 결정:
- 3D 는 유지하고 2D 는 별도 씬으로 둔다.
- 배경은 한 장을 새로 생성한다.
- 구도는 지금처럼 위에서 비스듬히 내려다보는 느낌으로 한다.

성공 기준:
- `Battle2D` 씬에서 전투를 끝까지 플레이할 수 있다.
  - 카드 드래그·클릭, 호버 힌트, 이동, 적 턴이 모두 된다.
- 기존 연출이 그대로 동작한다: 타격 타이밍, 흰 번쩍임, 불꽃, 화살, 넉백, 카메라 흔들림·히트스톱·슬로모션, 화면 펄스, 효과음.
- 원근이 없다. 먼 행의 칸과 유닛도 앞 행과 같은 크기로 그려진다.
- 3D 씬 동작과 기존 테스트는 바뀌지 않는다.

## 접근: 같은 보드에 "평면 투영" 옵션

클래스를 새로 만들지 않고 투영 방식만 고른다.

- 새 enum `BoardProjection { Perspective3D, Flat2D }`.
- `BattleRoot` 에 직렬화 필드 `projection` 을 둔다. 기본값은 `Perspective3D` 라 기존 씬은 그대로다.
- 보드(`Board3D`), 유닛(`UnitView`), 환경, 카메라 배치는 `projection` 에 따라 기하만 바꾼다.
- 클릭 판정은 지금처럼 타일 콜라이더 레이캐스트를 쓴다. 따라서 호버·드래그·힌트·상태 색 로직을 공유한다.
- `Board3D` 라는 이름은 유지한다. 이름을 바꾸면 테스트·코드 전반이 흔들려서 이번 범위 밖으로 둔다. 클래스 요약 주석에 두 투영을 모두 그린다고 적는다.

### 좌표 (Flat2D)

화면 평면은 XY 이고, 카메라는 -Z 쪽에서 +Z 를 보는 정사영(orthographic) 카메라다.

- `BoardLayout.ToFlat(Vector3 layoutPosition)` 은 레이아웃 좌표 (x, 0, z) 를 (x, z × `FlatRowScale`, z × `FlatDepthPerUnit`) 로 바꾼다.
  - `FlatRowScale` = 0.6: 행 간격을 눌러 내려다보는 느낌을 준다.
  - `FlatDepthPerUnit` = 0.1: 먼 행이 더 깊이(z) 있어 앞 행 유닛이 뒤 행 위에 그려진다.
- `Board3D.CellWorldPosition(Team, Vector2Int)` 는 투영을 반영한 칸 중심(발 위치)을 돌려준다.
  - 보드 안에서 `Layout.CellPosition` 을 쓰던 곳을 모두 이것으로 바꾼다.
  - `BattlePlayback` 에서 원거리 목표에 쓰던 `Board.Layout.CellPosition` 도 이것으로 바꾼다.

### 타일 (Flat2D)

- `Quad` 를 쓴다. 크기는 TileSize × (TileSize × FlatRowScale) 이고, 회전 없이 카메라를 향한다.
- 칸 중심보다 z 로 +0.02 뒤에 둔다. 유닛 발이 타일 위에 그려지게 하기 위해서다.
- 머티리얼 복사, 텍스처, 상태 색(`SetTileState`), `CellTag`, 힌트 라벨은 3D 와 같다.
- `Quad` 의 `MeshCollider` 를 남겨 레이캐스트 클릭에 쓴다.

### 유닛 (Flat2D)

- `UnitView.Setup(unit, sprite, assets, projection)` 에 투영 인자를 추가한다. 기본값은 `Perspective3D` 다.
- Flat2D 에서는:
  - 몸에 `Billboard`(세로축 회전·20° 기울임)를 달지 않는다. 판이 그대로 카메라를 본다.
  - 발밑 그림자·진영 고리는 XY 평면에 세로를 `FlatRowScale` 로 눌러 놓는다. z 는 발보다 +0.01 뒤다.
  - 머리 위 이름·HP 는 지금처럼 `Billboard` 를 쓴다. 정사영 카메라라 그대로 정면을 본다.
- 나머지(시트 애니메이션, 흰 번쩍임, 불꽃, 넉백, 사망)는 공유한다.
  - 넉백·돌진 방향은 기존대로 y 성분을 버린 수평 방향이다. 2D 에서는 화면 좌우 방향이 된다.

### 환경·배경 (Flat2D)

- 3D 환경(`BattleEnvironment`: 바닥·벽·스포트라이트·빛기둥·소품)은 만들지 않는다.
- `EncounterData.flatBackdrop` (Sprite) 을 새로 둔다.
  - 있으면 카메라 화면을 덮도록 배경 `SpriteRenderer` 를 z = +5 에 놓는다.
  - 없으면 단색 배경을 쓴다.
- 배경은 Unity AI(GPT Image 1.5, `format3.png` 참조)로 한 장 생성한다.
  - 내용: 비스듬히 내려다본 픽셀아트 창고 방. 가운데는 보드가 놓일 빈 바닥이고, 가장자리에 상자·드럼통이 있다.
  - 파일: `Art/Backgrounds/warehouse_2d.png`.
- 조명: `Battle2D` 씬의 디렉셔널 라이트를 +Z(화면 안쪽)를 향하게 둔다. 유닛·타일(Lit 머티리얼)이 고르게 밝도록 하기 위해서다.
- 후처리 볼륨(비네트·블룸, 화면 펄스)은 그대로 쓴다.

### 카메라 (Flat2D)

- `BattleRoot.FrameCamera` 가 Flat2D 이면 카메라를 정사영으로 바꾼다.
  - 보드 중심의 (x, y) 에서 z = -10 에 두고 +Z 를 보게 한다.
  - `orthographicSize` = `BoardLayout.FlatOrthoSize(width, height, aspect, margin)`. 보드 폭·높이에 여유를 둔다.
  - 계산 결과는 `BattleCamera.SetBase` 로 넘긴다.
- `BattleCamera` 의 흔들림·히트스톱·슬로모션은 그대로 쓴다.
  - 푸시인은 정사영에서 확대가 아니라 목표 쪽으로 조금 움직이는 패닝이 된다. 이번 범위에서는 그대로 둔다.

### 씬

- `Battle2D.unity` 는 `Battle.unity` 를 복제해 에디터 스크립트로 만든다.
  - `BattleRoot.projection = Flat2D`
  - 디렉셔널 라이트 방향 조정
  - 빌드 설정에 추가
- 인카운터(`skirmish.asset`)는 공유하고 `flatBackdrop` 만 채운다.

## 테스트 (EditMode, 먼저 실패 확인)

- `BoardLayout.ToFlat`:
  - x 는 그대로다.
  - 먼 행(0행)은 앞 행보다 y 가 크고 z 도 크다.
  - 같은 행이면 아군·적 칸의 y 가 같다.
- `BoardLayout.FlatOrthoSize`:
  - 좁은 화면일수록 크다.
  - 보드 폭·높이가 모두 화면 안에 들어간다.
- `Board3D` Flat2D 빌드:
  - 칸 수는 같다.
  - 타일이 카메라를 향한다(법선이 -Z).
  - 유닛 위치가 `CellWorldPosition` 과 같다.
  - -Z 에서 쏜 레이가 해당 칸을 집는다.
  - 먼 행 칸이 앞 행과 같은 크기다(원근 없음).
- `UnitView` Flat2D:
  - 몸에 `Billboard` 가 없다.
  - 그림자가 XY 평면에 있다.
- 기존 3D 테스트는 전부 그대로 통과해야 한다(기본값 `Perspective3D`).
- PlayMode:
  - `Battle2D` 씬을 Instant 로 끝까지 플레이한다(기존 스모크와 같은 방식).
  - 카메라가 정사영인지 확인한다.

## 확인 (수동)

- 플레이 캡처로 3D 와 나란히 비교한다.
- 연출 체감은 사용자가 직접 플레이해 확인한다.

## 범위 밖

- `Board3D` 이름 변경
- 2D 전용 소품 배치(배경 그림에 포함)
- 정사영 줌 푸시인
- 2D 전용 조명 연출(빛기둥·먼지)
- 행 기울임(평행사변형 타일)
