# 타격감 강화 2차 설계 (원거리 투사체 · 넉백 · 처치 슬로모션/화면 펄스)

날짜: 2026-09-29 · 브랜치: `unity-port-phase1`
앞선 설계: `2026-09-29-hit-impact-design.md`. 타격 타이밍, 흰 번쩍임, 불꽃, 효과음은 이미 구현했다.

사용자가 추천 순서(1→2→3)를 승인했고, 추가 질문 없이 진행하라고 했다.

## 1. 원거리 투사체

- **현재:** 원거리 공격도 근접처럼 0.4 앞으로 돌진한다. 타격은 돌진의 타격 시점에 들어간다.
- **변경:** 원거리(`AttackType.Ranged`)는 제자리에서 공격 프레임만 재생한다.
  - `UnitView.LungeToward(target, distance)` 에 거리 인자를 추가한다. 원거리는 -0.1(뒤로 물러나는 반동)을 준다.
  - 타격 시점(`AttackStrike`)에 투사체를 발사한다.
- **투사체** (`Projectile`, View):
  - 공격자 가슴 높이에서 대상 가슴 높이까지 날아간다.
  - 비행 시간 `FlightTime(distance) = clamp(distance / 16, 0.12, 0.35)`.
  - 약간 포물선으로 난다. 최고점 높이는 `distance * 0.06`.
  - 판은 카메라를 향한다. 판 위의 그림은 화면에서 진행 방향을 가리키도록 돌린다.
  - 도착하면 없어진다. 도착 순간 타격 묶음을 재생한다.
- **순수 함수:** `Projectile.PositionAt(from, to, arcHeight, t)`. t=0 은 from, t=1 은 to, 중간은 직선보다 높다.
- **그림:** Unity AI(GPT Image 1.5, `format3.png` 참조)로 픽셀 화살 한 장을 생성해 `Art/Effects/arrow.png` 로 둔다.
  - `ViewAssets.projectileSprite` 에 연결한다.
  - 비어 있으면 코드로 만든 흰 줄무늬를 쓴다(테스트·에셋 누락 대비).
- **비행 속도:** 투사체는 게임 시간을 쓴다. 히트스톱은 도착 뒤에 걸린다.

## 2. 피격 넉백

- 맞은 유닛을 공격자 반대 방향(바닥 평면)으로 밀었다가 되돌린다.
  - 피해 시작 후 `UnitMotion.KnockbackReach(t)` 로 움직인다: 0 → 1(t=0.15) → 0(t=0.6 이후).
- 거리 `BattlePlayback.KnockbackDistance(amount) = min(0.1 + 0.03 * amount, 0.35)`. 피해 0 이면 0 이다.
- 공격자 위치를 모르는 피해(공격 없이 나온 묶음)는 넉백하지 않는다.
- 광역 공격은 각 대상이 공격자 반대 방향으로 밀린다.
- 기존 좌우 흔들림(`_body.localPosition`)과 겹쳐도 된다. 넉백은 루트 `transform.position` 을 움직이고 끝나면 `HomePosition` 으로 정확히 돌아온다.

## 3. 처치 슬로모션 + 화면 펄스

- `BattleCamera.KillSlowMo()`:
  - 히트스톱이 끝난 뒤 `timeScale` 0.3 을 실제 시간 0.35초 유지하고 1 로 돌린다.
  - 히트스톱 중에 부르면 히트스톱이 끝난 다음 이어진다.
  - `OnDisable` 에서도 1 로 되돌린다.
- `ScreenPulse` (View, 카메라에 붙음):
  - 씬의 전역 `Volume` 을 찾아 런타임 프로필 사본(`volume.profile`)을 쓴다. 에셋 원본은 건드리지 않는다.
  - `Pulse(strength)`: 색수차 강도와 비네트 추가량을 strength 만큼 올렸다가, 실제 시간 0.4초 동안 기준값으로 되돌린다.
    - 색수차가 없으면 추가한다(기준 0).
    - 볼륨이 없으면 아무것도 하지 않는다.
  - 처치는 strength 1, 피해 8 이상 큰 타격은 0.5.
- View 어셈블리에 `Unity.RenderPipelines.Core.Runtime`, `Unity.RenderPipelines.Universal.Runtime` 참조를 추가한다.

## 테스트 (EditMode, 먼저 실패 확인)

- `Projectile.FlightTime`: 가까우면 하한, 멀면 상한, 중간은 비례.
- `Projectile.PositionAt`: 양 끝점 일치, 중간은 직선보다 높음.
- `UnitMotion.KnockbackReach`: 0 에서 0, 0.15 에서 1, 1 에서 0.
- `BattlePlayback.KnockbackDistance`: 0 피해는 0, 증가, 상한.
- `BattleCamera`:
  - 처치 슬로모션이 히트스톱 뒤 0.3 으로 이어지고, 끝나면 1 로 돌아온다.
  - 히트스톱 없이 불러도 동작한다.
- `ScreenPulse`: 볼륨이 있으면 펄스 직후 색수차가 오르고, 시간이 지나면 기준값으로 돌아온다. 볼륨이 없어도 예외가 없다.
- `ViewAssets.projectileSprite` 가 비어 있어도 투사체를 만들 수 있다.

## 범위 밖

카드별 투사체 모양(폭발탄 전용 등), 투사체 궤적 잔상, 넉백에 따른 칸 이동, 슬로모션 중 효과음 음높이 변화.
