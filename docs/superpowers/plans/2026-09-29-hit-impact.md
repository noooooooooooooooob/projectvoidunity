# 타격감 강화 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 피해 연출을 공격의 타격 순간에 터뜨리고, 흰 번쩍임·불꽃·튀는 피해 숫자·효과음을 더한다.

**Architecture:**
- `BattlePlayback` 이 공격 이벤트 뒤의 타격 묶음을 미리 찾아, 돌진을 병렬로 돌리면서 타격 시점에 피해 묶음을 재생한다.
- 피격 이펙트는 `UnitView` 가 맡는다.
- 소리는 새 `BattleSounds`(클립 묶음 에셋)와 `BattleAudio`(재생기)가 맡는다.

**Tech Stack:** Unity 6000.3 URP, NUnit(EditMode/PlayMode), Unity MCP, Unity AI `elevenlabs-sound-effects-v2`.

**Spec:** `docs/superpowers/specs/2026-09-29-hit-impact-design.md`

## Global Constraints

- 타격 시점 = `UnitMotion.AttackStrike * view.AttackDuration`.
- 흰 번쩍임 `HitWhiteTime` = 0.06초, 실제 시간 기준.
- 피해 숫자 시작 배율: 일반 1.6, 처치 2.0. 0.15초 동안 1로 줄어든다.
- 효과음 음높이 ±8% 무작위. Instant 재생은 소리·연출 없음.
- 검증 절차는 `docs/superpowers/plans/2026-09-28-unity-port-phase1.md` 의 "검증 절차"와 같다: Unity MCP 로 Refresh → 콘솔 에러 확인 → `TestBridge.Run` → `Temp/ProjectVoidTestResults.txt`.
- 커밋은 사용자가 요청할 때만 한다.

## Review Focus

1. **공격 대상이 이미 죽어 타격 묶음이 없는 경우:** 돌진만 하고 멈추지 않아야 한다. `ImpactStart` 가 -1 인 경로이며 Task 1 테스트에서 다룬다.
2. **광역 공격(여러 유닛 동시 피격):** 모든 유닛이 같은 순간에 맞는다. 기존 `PlayDamageBatch` 병렬 재생을 그대로 쓴다.
3. **히트스톱 중 흰 번쩍임:** 실제 시간으로 꺼져야 한다. 멈춘 채 흰색으로 남으면 안 된다. Task 2 테스트에서 다룬다.
4. **소리 에셋이 비어 있는 경우:** 예외 없이 무음으로 재생해야 한다. Task 3 테스트에서 다룬다.
5. **Instant 재생:** 기존 결과와 같아야 하고 소리도 없어야 한다. Task 3 테스트에서 다룬다.

---

### Task 1: 타격 묶음 찾기 + 타격 시점 재생 (`BattlePlayback`)

**Files:**
- Modify: `Assets/_Project/Scripts/View/BattlePlayback.cs`
- Test: `Assets/_Project/Tests/EditMode/PlaybackTests.cs`

**Interfaces:**
- Produces:
  - `public static int ImpactStart(List<BattleEvent> events, int attackIndex)`
  - `private AttackType _impactType` (Task 3 의 소리가 쓴다)

- [ ] **Step 1: 실패 테스트** — `PlaybackTests` 에 추가:

```csharp
private static BattleEvent Ev(BattleEventKind kind) => new BattleEvent(kind);

[Test]
public void ImpactStartFindsTheHitBatchAfterAnAttack()
{
    var hit = new List<BattleEvent> { Ev(BattleEventKind.CardPlayed), Ev(BattleEventKind.Damaged) };
    Assert.AreEqual(1, BattlePlayback.ImpactStart(hit, 0));
    var logged = new List<BattleEvent> { Ev(BattleEventKind.CardPlayed), Ev(BattleEventKind.Log), Ev(BattleEventKind.Died) };
    Assert.AreEqual(2, BattlePlayback.ImpactStart(logged, 0), "logs between attack and hit are skipped");
    var miss = new List<BattleEvent> { Ev(BattleEventKind.CardPlayed), Ev(BattleEventKind.Log) };
    Assert.AreEqual(-1, BattlePlayback.ImpactStart(miss, 0), "no hit");
    var other = new List<BattleEvent> { Ev(BattleEventKind.EnemyActed), Ev(BattleEventKind.BlockGained), Ev(BattleEventKind.Damaged) };
    Assert.AreEqual(-1, BattlePlayback.ImpactStart(other, 0), "another event breaks the attack");
}
```

- [ ] **Step 2: 실패 확인.** `ImpactStart` 를 `=> -1` 로 두고 컴파일한 뒤 실행한다. 첫 단언이 실패해야 한다.

- [ ] **Step 3: 구현**

```csharp
public static int ImpactStart(List<BattleEvent> events, int attackIndex)
{
    for (int i = attackIndex + 1; i < events.Count; i++)
    {
        BattleEventKind kind = events[i].Kind;
        if (kind == BattleEventKind.Damaged || kind == BattleEventKind.Died) return i;
        if (kind != BattleEventKind.Log) return -1;
    }
    return -1;
}
```

`Play` 루프에서 `CardPlayed`/`EnemyActed` 는 Instant 가 아니고 타격 묶음이 있으면 다음 순서로 처리한다.
1. 사이의 `Log` 와 타격 묶음을 잘라 공통 `Attack(view, target, type, logs, batch)` 에 넘긴다.
2. `Attack` 은 돌진을 `StartCoroutine` 으로 시작하고, 타격 시점까지 `WaitForSeconds` 로 기다린다.
3. 그다음 로그를 붙이고 `_impactType = type` 를 정한 뒤 `PlayDamageBatch(batch)` 를 기다린다.
4. 돌진이 끝나길 기다린다.
5. 루프 인덱스는 묶음 끝으로 옮긴다.

타격 묶음이 없으면 `Attack` 에 null 을 넘겨 돌진만 한다.

- [ ] **Step 4: 전체 EditMode 통과 확인.**

### Task 2: 피격 이펙트 (`UnitView`)

**Files:**
- Modify: `Assets/_Project/Scripts/View/UnitView.cs`, `Assets/_Project/Scripts/View/BattlePlayback.cs`
- Test: `Assets/_Project/Tests/EditMode/BoardViewTests.cs`

**Interfaces:**
- Produces:
  - `public const float HitWhiteTime = 0.06f;`
  - `public void StartImpact()`: 흰 발광을 켜고 불꽃을 방출한다.
  - `public void TickFlash(float realtime)`
  - `public ParticleSystem Sparks { get; }`
  - `public static float PopScale(float t, float punch)`
  - `public void PopText(string text, Color color, float punch = 1f)`
  - `public const float DamagePopPunch = 1.6f, KillPopPunch = 2f;`

- [ ] **Step 1: 실패 테스트** — `BoardViewTests` 에 추가:

```csharp
[Test]
public void ImpactFlashesWhiteBrieflyAndThrowsSparks()
{
    (BattleState state, Board3D board) = BuildBoard();
    UnitView view = board.ViewFor(state.Units[0]);
    view.StartImpact();
    Assert.AreEqual(Color.white, view.BodyMaterial.GetColor("_EmissionColor"), "white flash");
    Assert.Greater(view.Sparks.particleCount, 0, "sparks burst");
    view.TickFlash(Time.realtimeSinceStartup + UnitView.HitWhiteTime + 0.01f);
    Assert.AreEqual(Color.black, view.BodyMaterial.GetColor("_EmissionColor"), "flash ends on real time");
}

[Test]
public void DamageNumberPunchesThenSettles()
{
    Assert.AreEqual(1.6f, UnitView.PopScale(0f, 1.6f), 1e-4f);
    Assert.AreEqual(1f, UnitView.PopScale(0.5f, 1.6f), 1e-4f);
    Assert.Greater(UnitView.KillPopPunch, UnitView.DamagePopPunch);
}
```

- [ ] **Step 2: 실패 확인.** 빈 구현으로 컴파일한 뒤 실행한다.

- [ ] **Step 3: 구현**
  - `Setup` 에서 `_bodyMaterial.EnableKeyword("_EMISSION")` 과 검은 발광으로 시작한다.
  - `Sparks` 는 가슴 높이(`SpriteHeight * 0.55`)에 만든다. 설정: `useUnscaledTime`, 수명 0.25, 속도 2~4, 크기 0.06~0.1, 흰·주황, `playOnAwake=false`, 방출 모듈 끔, 기존 dust 와 같은 URP Particles/Unlit 머티리얼.
  - `StartImpact` 는 발광을 흰색으로 하고, `_whiteUntil = realtimeSinceStartup + HitWhiteTime` 을 기록하고, `Sparks.Emit(12)` 을 호출한다.
  - `Update` 에서 `TickFlash(Time.realtimeSinceStartup)` 을 부른다.
  - `PopScale(t, punch) = Lerp(punch, 1, t / (PopPunchTime / PopTime))` (0..1 로 자름).
  - `FlashAndShake` 시작에 `StartImpact()` 를 호출한다.
  - `BattlePlayback.Damaged` 는 `e.Hp <= 0` 이면 `KillPopPunch`, 아니면 `DamagePopPunch` 를 넘긴다.

- [ ] **Step 4: 전체 EditMode 통과 확인.**

### Task 3: 효과음 (`BattleSounds`, `BattleAudio`, 연결, 생성)

**Files:**
- Create: `Assets/_Project/Scripts/View/BattleSounds.cs`, `Assets/_Project/Scripts/View/BattleAudio.cs`, `Assets/_Project/Tests/EditMode/BattleSoundsTests.cs`
- Modify: `ViewAssets.cs`, `BattleRoot.cs`, `BattlePlayback.cs`, `Tests/EditMode/PlaybackTests.cs`
- Create (생성물): `Assets/_Project/Audio/{melee_swing,melee_hit,ranged_shot,ranged_hit,blocked,kill}`, `Assets/_Project/Data/Audio/battle_sounds.asset`

**Interfaces:**
- Produces:
  - `BattleSounds : ScriptableObject`
    - 필드: `meleeSwing, meleeHit, rangedShot, rangedHit, blocked, kill`
    - `AudioClip ForAttack(AttackType)`
    - `AudioClip ForImpact(AttackType, int amount, bool died)`
  - `BattleAudio : MonoBehaviour`
    - `BattleSounds Sounds`
    - `int PlayCount`
    - `void Play(AudioClip clip)`: null 무시, 음높이 ±8%
  - `BattlePlayback.Audio` 필드

- [ ] **Step 1: 실패 테스트** — `BattleSoundsTests`:

```csharp
[Test]
public void PicksSoundByAttackTypeDamageAndKill()
{
    var s = ScriptableObject.CreateInstance<BattleSounds>();
    s.meleeSwing = AudioClip.Create("ms", 10, 1, 44100, false);
    s.rangedShot = AudioClip.Create("rs", 10, 1, 44100, false);
    s.meleeHit = AudioClip.Create("mh", 10, 1, 44100, false);
    s.rangedHit = AudioClip.Create("rh", 10, 1, 44100, false);
    s.blocked = AudioClip.Create("b", 10, 1, 44100, false);
    s.kill = AudioClip.Create("k", 10, 1, 44100, false);
    Assert.AreSame(s.meleeSwing, s.ForAttack(AttackType.Melee));
    Assert.AreSame(s.rangedShot, s.ForAttack(AttackType.Ranged));
    Assert.AreSame(s.meleeHit, s.ForImpact(AttackType.Melee, 5, false));
    Assert.AreSame(s.rangedHit, s.ForImpact(AttackType.Ranged, 5, false));
    Assert.AreSame(s.blocked, s.ForImpact(AttackType.Ranged, 0, false), "fully blocked");
    Assert.AreSame(s.kill, s.ForImpact(AttackType.Melee, 0, true), "kill wins");
}

[Test]
public void MissingClipIsSilent()
{
    var audio = new GameObject("a").AddComponent<BattleAudio>();
    audio.Play(null);
    Assert.AreEqual(0, audio.PlayCount);
    Object.DestroyImmediate(audio.gameObject);
}
```

`PlaybackTests.InstantPlaybackLeavesTheCameraAlone` 에 `BattleAudio` 를 연결하고 `Assert.AreEqual(0, audio.PlayCount)` 를 추가한다.

- [ ] **Step 2: 실패 확인.** 빈 구현으로 컴파일한 뒤 실행한다.
- [ ] **Step 3: 구현 + 연결.**
  - `BattleRoot` 는 카메라에 `BattleAudio` 를 붙이고(`GetComponent` 가 없으면 추가), `Sounds = assets.sounds` 로 정하고, 재생기에 넘긴다.
  - `Attack` 은 시작할 때 `ForAttack`, `Damaged` 는 `ForImpact(_impactType, amount, false)`, `Died` 는 `ForImpact(_impactType, 0, true)` 를 재생한다.
- [ ] **Step 4: 전체 EditMode + PlayMode 통과 확인.**
- [ ] **Step 5: 효과음 6개 생성.** `GenerateSound`, `elevenlabs-sound-effects-v2`, 0.5~1초로 만든다. `battle_sounds.asset` 을 만들어 연결하고, 씬 `BattleRoot.assets.sounds` 에 지정한 뒤 씬을 저장한다.
- [ ] **Step 6: 플레이 모드에서 공격을 실행하고 타격 순간을 캡처해 흰 번쩍임과 불꽃을 확인한다.**
