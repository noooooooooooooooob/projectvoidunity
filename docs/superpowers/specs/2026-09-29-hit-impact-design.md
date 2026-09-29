# 타격감 강화 설계 (타격 타이밍 · 피격 이펙트 · 효과음)

날짜: 2026-09-29 · 브랜치: `unity-port-phase1`

## 목적

전투에서 "때렸다"는 느낌을 키운다. 이미 있는 카메라 연출(푸시인·흔들림·히트스톱, `BattleCamera`)과 늘린 공격/피격 프레임(공격 1.0초, 피격 0.8초) 위에 세 가지를 더한다.

1. 피해 연출이 공격이 끝난 뒤가 아니라 **무기가 닿는 순간**에 터진다.
2. 피격 순간이 눈에 확 띈다 (흰 번쩍임, 불꽃, 튀는 피해 숫자).
3. 공격·타격·처치에 효과음이 난다.

성공 기준: 플레이 중 공격자의 타격 자세와 피격·히트스톱·흔들림·타격음이 한 순간에 겹친다. 연출을 끈 Instant 재생(테스트)은 지금과 같은 결과를 낸다.

## 범위 밖

배경음악, 카드별·유닛 무기별 효과음, 화면 후처리 펄스, 이동·방어·회복 효과음.

## 1. 타격 타이밍 (`BattlePlayback`)

현재: `CardPlayed`/`EnemyActed` 가 돌진(`UnitView.LungeToward`)을 끝까지 기다린 뒤, 다음 이벤트인 피해 묶음(`Damaged`/`Died`)을 재생한다.

변경:

- 공격 이벤트(`CardPlayed`, 또는 `Action == Attack` 인 `EnemyActed`) 뒤에 `Log` 만 건너뛰고 `Damaged`/`Died` 가 오면 그것이 이 공격의 **타격 묶음**이다.
- 순수 함수 `BattlePlayback.ImpactStart(List<BattleEvent> events, int attackIndex)` 가 타격 묶음의 첫 인덱스를 돌려준다. 없으면 -1.
- 타격 묶음이 있으면:
  1. 돌진을 별도 코루틴으로 시작한다.
  2. 타격 시점까지 기다린다: `UnitMotion.AttackStrike * view.AttackDuration`. 공격 1.0초 기준 0.5초.
  3. 사이의 `Log` 를 붙이고 타격 묶음을 재생한다(기존 `PlayDamageBatch`).
  4. 돌진과 타격 묶음이 모두 끝나면 다음 이벤트로 간다.
- 타격 묶음이 없으면(빗나감) 지금처럼 돌진만 한다.
- 히트스톱은 전역 `timeScale` 이라 공격자도 타격 자세로 멈춘다. 의도한 동작이다.
- Instant 모드는 지금과 같이 순서대로 즉시 반영한다.

## 2. 피격 이펙트 (`UnitView`)

- **흰 번쩍임**: 피격 시작 후 `HitWhiteTime` = 0.06초 동안 몸 머티리얼 발광(`_EmissionColor`)을 흰색으로 켠다. 유닛마다 머티리얼 인스턴스가 따로라 다른 유닛에 번지지 않는다. 그 뒤에는 기존 붉은 번쩍임이 이어진다.
  - 판정은 실제 시간 기준이다. 히트스톱 중(`timeScale` 0.05)에도 0.06초가 지나면 꺼진다.
- **타격 불꽃**: 맞은 유닛 가슴 높이에서 `ParticleSystem` 한 번 터뜨림.
  - 네모 픽셀 모양, 흰·주황, 12개 정도, 수명 0.25초.
  - `useUnscaledTime` 으로 돌려 히트스톱 중에도 날아간다.
  - 유닛마다 하나씩 만들어 재사용한다.
- **피해 숫자 튀기기**: `PopText` 에 크기 인자를 받아 처음 0.15초 동안 1.6배에서 1배로 줄어든다. 처치는 2배에서 시작한다.

## 3. 효과음

### 에셋

- `BattleSounds` (ScriptableObject, `Assets/_Project/Data/Audio/battle_sounds.asset`) 필드:
  `meleeSwing`, `meleeHit`, `rangedShot`, `rangedHit`, `blocked`, `kill` (모두 `AudioClip`).
- 클립은 Unity AI `elevenlabs-sound-effects-v2` 로 6개 생성해 `Assets/_Project/Audio/` 에 둔다. 각 0.5~1초.
- `ViewAssets` 에 `BattleSounds sounds` 필드를 추가한다. 씬의 `BattleRoot` 에 연결한다.

### 고르기 (순수 함수, 테스트 대상)

- `BattleSounds.ForAttack(AttackType)`: 근접은 `meleeSwing`, 원거리는 `rangedShot`.
- `BattleSounds.ForImpact(AttackType, int amount, bool died)`:
  - 처치면 `kill`
  - 그렇지 않고 피해 0이면 `blocked`
  - 그 밖에는 공격 종류에 따라 `meleeHit` / `rangedHit`
- 비어 있는 클립은 null 이고 재생하지 않는다.

### 재생

- `BattleAudio` (MonoBehaviour, 전투 카메라에 붙음): `AudioSource` 하나로 `PlayOneShot`, 음높이 ±8% 무작위.
- `BattlePlayback` 에 `BattleAudio Audio` 필드. null 이거나 Instant 이면 소리를 내지 않는다.
- 공격 종류:
  - `CardPlayed` 는 `e.Card.attackType`.
  - `EnemyActed` 는 `((EnemyData)e.Unit.Data).attackType`.
  - 타격 묶음 재생에 넘겨 `Damaged`/`Died` 에서 쓴다.
  - 공격 없이 나온 피해 묶음은 근접으로 친다.
- 한 묶음에서 여러 유닛이 동시에 맞아도 타격음은 유닛마다 낸다. `PlayOneShot` 이 겹쳐 재생하므로 광역 공격이 더 요란해지는 건 의도한 것이다.

## 테스트 (EditMode, 먼저 실패 확인)

- `ImpactStart`:
  - 공격 바로 뒤 `Damaged`
  - `Log` 를 건너뛴 `Damaged`
  - 피해 없음(-1)
  - 다른 이벤트가 끼면 -1
- `BattleSounds.ForAttack` / `ForImpact` 의 각 분기.
- `UnitView`:
  - 피격 시작 직후 발광이 흰색이고, 0.06초 뒤 꺼진다.
  - 피격 시 불꽃 파티클이 방출된다.
- Instant 재생에서 `BattleAudio` 가 아무것도 재생하지 않는다(재생 횟수 카운터).
- 기존 테스트 전체 통과. PlayMode 스모크(Instant) 통과.

## 확인 (수동)

- 플레이 모드 캡처로 타격 순간의 흰 번쩍임·불꽃을 확인한다.
- 소리와 체감 타이밍은 사용자가 직접 플레이해 확인한다.
