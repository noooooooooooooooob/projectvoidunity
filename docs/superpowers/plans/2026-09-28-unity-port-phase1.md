# 유니티 이식 1단계 구현 계획 — 전투 규칙 · 2.5D 전투 화면 · AI 스프라이트

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Godot 프로토타입 Project-Void 의 전투(규칙 + 2.5D 화면 + 단순 HUD)를 유니티 6000.3 URP 로 옮기고, 유닛 6종 스프라이트를 Unity AI 로 만들어 한 판을 끝까지 플레이할 수 있게 한다.

**Architecture:** 규칙 코어(`ProjectVoid.Combat`, 순수 C#)가 C# event 를 내고, `BattleEventRecorder` 가 스냅샷 이벤트로 기록, `BattlePlayback` 이 코루틴으로 순차 재생해 `Board3D`/`UnitView`/`BattleHud` 를 움직인다. 입력은 `BattleRoot` 가 받아 규칙을 호출하고 재생 중 잠근다. Godot 원본과 메서드 단위 1:1 대응.

**Tech Stack:** Unity 6000.3.25f1, URP 17.3, Input System 1.20, uGUI 2.0 + TextMeshPro, Unity Test Framework 1.6 (NUnit), Unity MCP (`mcp__unity-mcp__*`), Unity AI 생성 (`gpt-image-1-5`).

**Spec:** `docs/superpowers/specs/2026-09-28-unity-port-phase1-design.md`

**원본 (읽기 전용):** `C:\Users\User\Desktop\Godot\Project-Void` — 이하 `GODOT/` 로 표기. 규칙 판정의 기준은 언제나 GODOT 원본 코드다.

## Global Constraints

- 유니티 프로젝트 루트: `C:\Users\User\Desktop\Unity\project void test` (이하 `ROOT/`). 새 파일은 전부 `ROOT/Assets/_Project/` 아래.
- 규칙 수치·판정·난수 소비 순서는 GODOT 원본과 같게. 리팩토링·동작 개선 금지.
- 네임스페이스: 규칙 `ProjectVoid.Combat`, 화면 `ProjectVoid.View`, 에디터 `ProjectVoid.EditorTools`, 테스트 `ProjectVoid.Tests`.
- C# 명명: 런타임 클래스 멤버는 PascalCase 프로퍼티/메서드, ScriptableObject 직렬화 필드는 camelCase public 필드. Godot `snake_case` → `PascalCase`.
- 주석: 한국어, WHY 가 분명할 때만. `///` 요약은 public 타입·비자명 메서드에만.
- 에디터가 열려 있으므로 커맨드라인 테스트 러너는 쓸 수 없다. 컴파일·테스트는 아래 "검증 절차"로 Unity MCP 를 통해 한다.
- 커밋 메시지 끝에 `Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>`.

### 검증 절차 (모든 태스크의 "Run" 단계가 이것을 뜻한다)

1. `mcp__unity-mcp__Unity_RunCommand` 로 다음을 실행 (새 .cs 를 인식시키고 컴파일):
   ```csharp
   using UnityEditor;
   internal class CommandScript : IRunCommand
   {
       public void Execute(ExecutionResult result)
       {
           AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate);
           result.Log("refreshed");
       }
   }
   ```
2. 도메인 리로드가 끝나도록 5~10초 뒤 `mcp__unity-mcp__Unity_GetConsoleLogs` (`logTypes: "Error"`, `maxEntries: 20`). `error CS` 로 시작하는 컴파일 에러가 있으면 고치고 1로 돌아간다. MCP 호출이 리로드 중이라 실패하면 몇 초 뒤 다시 부른다.
3. 테스트 실행 (Task 1 이후): `Unity_RunCommand` 로
   ```csharp
   using UnityEditor.TestTools.TestRunner.Api;
   internal class CommandScript : IRunCommand
   {
       public void Execute(ExecutionResult result)
       {
           ProjectVoid.Tests.TestBridge.Run(TestMode.EditMode, "FILTER");
           result.Log("started");
       }
   }
   ```
   `FILTER` 는 테스트 전체 이름에 대한 정규식 (예: `ProjectVoid\.Tests\.UnitTests`). 전체는 빈 문자열 `""`.
4. `ROOT/Temp/ProjectVoidTestResults.txt` 를 Read. 첫 줄 `PASSED n / FAILED m / SKIPPED k`, 이후 실패 테스트 이름과 메시지.

## Review Focus

- HUD 버튼 위를 클릭했을 때 뒤의 보드 타일이 클릭되면 안 된다 — Task 13 `Board3D` 가 `EventSystem.IsPointerOverGameObject()` 로 거른다 (Task 16 수동 확인 항목).
- 카드를 드래그해 빈 곳(보드 밖)에 놓으면 선택이 취소되고 카드가 사용되지 않아야 한다 — Task 15 PlayMode 테스트 `DropOnNothingCancelsSelection`.
- 광역 공격으로 쓰러진 유닛은 더 이상 클릭·호버 대상이 아니고 그 칸은 비어 보여야 한다 — Task 13 테스트 `DeadUnitIsNotPickable`.
- 창 크기·비율이 바뀌면 보드 전체가 화면 안에 들어오도록 카메라를 다시 잡아야 한다 — Task 9 테스트 `CameraDistanceGrowsForNarrowAspect` + Task 15 `BattleRoot.Update` 의 해상도 감시.
- 승패 후 "다시 하기"로 재시작해도 이벤트 중복 구독이나 에러 없이 새 전투가 시작돼야 한다 — Task 15 PlayMode 테스트 `RestartStartsFreshBattle`.

## 파일 구조

```
Assets/_Project/
  Scripts/Combat/ProjectVoid.Combat.asmdef
  Scripts/Combat/Enums.cs              Team, AttackType, Shape, Phase, EnemyAction
  Scripts/Combat/Rng.cs                시드 난수 (Godot RandomNumberGenerator 대체)
  Scripts/Combat/Data/CardData.cs  UnitData.cs  AllyData.cs  EnemyData.cs  UnitPlacement.cs  EncounterData.cs
  Scripts/Combat/Unit.cs  TargetResolver.cs  BattleState.cs  EnemyBrain.cs
  Scripts/View/ProjectVoid.View.asmdef
  Scripts/View/BattleEvent.cs  BattleEventRecorder.cs  BoardLayout.cs
  Scripts/View/Coroutines.cs           트윈·Drain 도우미
  Scripts/View/ViewAssets.cs           폰트·머티리얼·기본 스프라이트 묶음
  Scripts/View/CellTag.cs  UnitView.cs  Board3D.cs  BattlePlayback.cs  BattleRoot.cs
  Scripts/View/UI/BattleHud.cs  CardButton.cs
  Scripts/Editor/ProjectVoid.Editor.asmdef
  Scripts/Editor/DataImporter.cs  PixelSpriteProcessor.cs  FontSetup.cs  BattleSceneBuilder.cs
  Data/Cards/*.asset  Data/Units/*.asset  Data/Encounters/skirmish.asset
  Art/Reference/format3.png  Art/Units/Raw/*.png  Art/Units/*.png  Art/Units/placeholder_unit.png
  Fonts/NotoSansKR.ttf  Fonts/NotoSansKR SDF.asset
  Materials/Tile.mat
  Scenes/Battle.unity
  Tests/EditMode/ProjectVoid.Tests.EditMode.asmdef  TestBridge.cs  Make.cs  (테스트 파일들)
  Tests/PlayMode/ProjectVoid.Tests.PlayMode.asmdef  BattleSmokeTests.cs
```

## Godot 테스트 이식 규칙 (Task 3~8 공통)

- GODOT `tests/test_X.gd` 의 `_test_foo_bar()` 하나 → `[Test] public void FooBar()` 하나. 이름은 PascalCase 로만 바꾼다. 빠뜨리는 케이스 없이 전부.
- `check("설명", cond)` → `Assert.IsTrue(cond, "설명")`.
- `check_eq("설명", actual, expected)` → `Assert.AreEqual(expected, actual, "설명")` (순서 주의: NUnit 은 expected 가 먼저).
- 헬퍼(`_ally`, `_enemy`, `_state`, `_placement`, `_rng` …)는 `Make` 정적 클래스(Task 2)의 같은 뜻 메서드로 바꾼다. 테스트 파일 전용 헬퍼가 더 필요하면 그 테스트 클래스 안에 private 으로 둔다.
- `rng.seed = N` → `new Rng(N)`. `state.ai_rng.state` 비교 → `state.AiRng.Draws` 비교.
- 신호 연결(`state.x.connect(func ...)`) → `state.X += (...) => log.Add(...)`.
- 난수 수열 차이: 특정 셔플 결과·특정 칸에 기대값을 건 단언이 이식 후 실패하고, 그 원인이 오직 `System.Random` 수열 차이일 때만(규칙 버그가 아님을 원본 코드와 대조해 확인한 뒤) C# 쪽 실제 값으로 기대값을 바꾸고 `// 난수 수열이 Godot 과 달라 기대값을 다시 산출 (원래: X)` 주석을 단다. 성질(개수, 순서 보존, 같은 시드면 같은 결과)을 검사하는 단언은 절대 바꾸지 않는다.

---

### Task 1: 어셈블리 · 테스트 브리지 · 기준 커밋

**Files:**
- Create: `Assets/_Project/Scripts/Combat/ProjectVoid.Combat.asmdef`
- Create: `Assets/_Project/Scripts/View/ProjectVoid.View.asmdef`
- Create: `Assets/_Project/Scripts/Editor/ProjectVoid.Editor.asmdef`
- Create: `Assets/_Project/Tests/EditMode/ProjectVoid.Tests.EditMode.asmdef`
- Create: `Assets/_Project/Tests/PlayMode/ProjectVoid.Tests.PlayMode.asmdef`
- Create: `Assets/_Project/Tests/EditMode/TestBridge.cs`
- Test: `Assets/_Project/Tests/EditMode/HarnessSmokeTests.cs`

**Interfaces:**
- Produces: `ProjectVoid.Tests.TestBridge.Run(TestMode mode, string filter)` — 결과를 `Temp/ProjectVoidTestResults.txt` 에 쓴다. 이후 모든 태스크의 검증 절차 3단계가 이것을 부른다.

- [ ] **Step 1: 기존 템플릿을 기준 커밋**

```bash
cd "/c/Users/User/Desktop/Unity/project void test"
git add -A
git commit -m "chore: add Unity project baseline

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

- [ ] **Step 2: asmdef 다섯 개 작성**

`Scripts/Combat/ProjectVoid.Combat.asmdef`:
```json
{
    "name": "ProjectVoid.Combat",
    "rootNamespace": "ProjectVoid.Combat",
    "references": [],
    "autoReferenced": true
}
```

`Scripts/View/ProjectVoid.View.asmdef`:
```json
{
    "name": "ProjectVoid.View",
    "rootNamespace": "ProjectVoid.View",
    "references": ["ProjectVoid.Combat", "Unity.InputSystem", "Unity.TextMeshPro", "UnityEngine.UI"],
    "autoReferenced": true
}
```

`Scripts/Editor/ProjectVoid.Editor.asmdef`:
```json
{
    "name": "ProjectVoid.Editor",
    "rootNamespace": "ProjectVoid.EditorTools",
    "references": ["ProjectVoid.Combat", "ProjectVoid.View", "Unity.TextMeshPro", "Unity.TextMeshPro.Editor", "Unity.InputSystem", "UnityEngine.UI"],
    "includePlatforms": ["Editor"],
    "autoReferenced": true
}
```

`Tests/EditMode/ProjectVoid.Tests.EditMode.asmdef`:
```json
{
    "name": "ProjectVoid.Tests.EditMode",
    "rootNamespace": "ProjectVoid.Tests",
    "references": ["ProjectVoid.Combat", "ProjectVoid.View", "ProjectVoid.Editor", "UnityEngine.TestRunner", "UnityEditor.TestRunner", "Unity.TextMeshPro", "Unity.InputSystem", "UnityEngine.UI"],
    "includePlatforms": ["Editor"],
    "overrideReferences": true,
    "precompiledReferences": ["nunit.framework.dll"],
    "autoReferenced": false,
    "defineConstraints": ["UNITY_INCLUDE_TESTS"]
}
```

`Tests/PlayMode/ProjectVoid.Tests.PlayMode.asmdef`:
```json
{
    "name": "ProjectVoid.Tests.PlayMode",
    "rootNamespace": "ProjectVoid.Tests",
    "references": ["ProjectVoid.Combat", "ProjectVoid.View", "UnityEngine.TestRunner", "Unity.TextMeshPro", "UnityEngine.UI"],
    "overrideReferences": true,
    "precompiledReferences": ["nunit.framework.dll"],
    "autoReferenced": false,
    "defineConstraints": ["UNITY_INCLUDE_TESTS"]
}
```

- [ ] **Step 3: TestBridge 작성**

`Tests/EditMode/TestBridge.cs`:
```csharp
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;

namespace ProjectVoid.Tests
{
    /// <summary>에디터를 연 채로 MCP 에서 테스트를 돌리고 결과를 파일로 남긴다 (커맨드라인 러너는 프로젝트 잠금 때문에 못 쓴다).</summary>
    [InitializeOnLoad]
    public static class TestBridge
    {
        public const string ResultPath = "Temp/ProjectVoidTestResults.txt";

        // PlayMode 실행은 도메인 리로드를 거치므로 리로드마다 콜백을 다시 건다.
        static TestBridge()
        {
            ScriptableObject.CreateInstance<TestRunnerApi>().RegisterCallbacks(new ResultWriter());
        }

        public static void Run(TestMode mode, string filter)
        {
            if (File.Exists(ResultPath))
            {
                File.Delete(ResultPath);
            }
            var testFilter = new Filter { testMode = mode };
            if (!string.IsNullOrEmpty(filter))
            {
                testFilter.groupNames = new[] { filter };
            }
            var settings = new ExecutionSettings(testFilter) { runSynchronously = mode == TestMode.EditMode };
            ScriptableObject.CreateInstance<TestRunnerApi>().Execute(settings);
        }

        private sealed class ResultWriter : ICallbacks
        {
            public void RunStarted(ITestAdaptor testsToRun) { }
            public void TestStarted(ITestAdaptor test) { }
            public void TestFinished(ITestResultAdaptor result) { }

            public void RunFinished(ITestResultAdaptor result)
            {
                int passed = 0, failed = 0, skipped = 0;
                var failures = new StringBuilder();
                Collect(result, ref passed, ref failed, ref skipped, failures);
                File.WriteAllText(ResultPath, $"PASSED {passed} / FAILED {failed} / SKIPPED {skipped}\n{failures}");
            }

            private static void Collect(ITestResultAdaptor node, ref int passed, ref int failed, ref int skipped, StringBuilder failures)
            {
                if (node.HasChildren)
                {
                    foreach (ITestResultAdaptor child in node.Children)
                    {
                        Collect(child, ref passed, ref failed, ref skipped, failures);
                    }
                    return;
                }
                switch (node.TestStatus)
                {
                    case TestStatus.Passed: passed++; break;
                    case TestStatus.Failed:
                        failed++;
                        failures.AppendLine($"FAIL {node.FullName}\n  {node.Message}");
                        break;
                    default: skipped++; break;
                }
            }
        }
    }
}
```

- [ ] **Step 4: 스모크 테스트 작성**

`Tests/EditMode/HarnessSmokeTests.cs`:
```csharp
using NUnit.Framework;

namespace ProjectVoid.Tests
{
    public class HarnessSmokeTests
    {
        [Test]
        public void HarnessRuns()
        {
            Assert.AreEqual(2, 1 + 1);
        }
    }
}
```

- [ ] **Step 5: 검증** — 검증 절차 1~4, FILTER `ProjectVoid\.Tests\.HarnessSmokeTests`. Expected: 컴파일 에러 0, 결과 파일 `PASSED 1 / FAILED 0 / SKIPPED 0`. 결과 파일이 생기지 않으면 `runSynchronously` 가 무시된 것이므로 몇 초 뒤 다시 Read 한다.

- [ ] **Step 6: Commit**

```bash
git add Assets/_Project
git commit -m "chore: add project assemblies and MCP test bridge

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 2: 열거형 · 난수 · 데이터 타입

**Files:**
- Create: `Assets/_Project/Scripts/Combat/Enums.cs`, `Assets/_Project/Scripts/Combat/Rng.cs`
- Create: `Assets/_Project/Scripts/Combat/Data/CardData.cs`, `UnitData.cs`, `AllyData.cs`, `EnemyData.cs`, `UnitPlacement.cs`, `EncounterData.cs`
- Create: `Assets/_Project/Tests/EditMode/Make.cs`
- Test: `Assets/_Project/Tests/EditMode/RngTests.cs`, `Assets/_Project/Tests/EditMode/DataTests.cs`

**Interfaces:**
- Produces:
  - `enum Team { Ally, Enemy }`, `enum AttackType { Melee, Ranged }`, `enum Shape { Single, Pierce, Sweep, Area, Line }`, `enum Phase { Standby, Draw, Action, BeforeEnd, AfterEnd }`, `enum EnemyAction { Attack, Defend, Rest, Move }` (Godot 원본과 같은 순서)
  - `Rng(int seed)`, `int Seed`, `int Draws`, `int RangeInclusive(int min, int max)`, `float NextFloat()`, `Rng Derive(string salt)`
  - `CardData : ScriptableObject { string id, displayName; int spCost=1; AttackType attackType; Shape shape; int attackRange=1; int damage; }`
  - `abstract UnitData : ScriptableObject { string id, displayName; int maxHp=10, speed=10; Sprite sprite; }`
  - `AllyData : UnitData { int maxSp=3; List<CardData> deck; }`
  - `EnemyData : UnitData { int attackDamage=5; AttackType attackType; Shape attackShape; int attackRange=1, blockAmount=5, restHeal=4; float moveChance=0.25f; }`
  - `[Serializable] UnitPlacement { UnitData unitData; Vector2Int cell; UnitPlacement(UnitData, Vector2Int) }`
  - `EncounterData : ScriptableObject { Vector2Int allyGrid=(3,3), enemyGrid=(3,3); List<UnitPlacement> allyUnits, enemyUnits; }`
  - 테스트 헬퍼: `Make.Asset<T>()`, `Make.Card(...)`, `Make.Ally(...)`, `Make.Enemy(...)`, `Make.Place(UnitData, int col, int row)`, `Make.Encounter(...)`, `Make.Cleanup()`

- [ ] **Step 1: 실패하는 테스트 작성**

`Tests/EditMode/RngTests.cs`:
```csharp
using NUnit.Framework;
using ProjectVoid.Combat;

namespace ProjectVoid.Tests
{
    public class RngTests
    {
        [Test]
        public void SameSeedSameSequence()
        {
            var a = new Rng(42);
            var b = new Rng(42);
            for (int i = 0; i < 20; i++)
            {
                Assert.AreEqual(a.RangeInclusive(0, 100), b.RangeInclusive(0, 100), $"draw {i}");
            }
        }

        [Test]
        public void RangeInclusiveIncludesBothEnds()
        {
            var rng = new Rng(7);
            bool sawMin = false, sawMax = false;
            for (int i = 0; i < 1000; i++)
            {
                int v = rng.RangeInclusive(0, 2);
                Assert.That(v, Is.InRange(0, 2));
                sawMin |= v == 0;
                sawMax |= v == 2;
            }
            Assert.IsTrue(sawMin && sawMax, "both ends appear");
        }

        [Test]
        public void DrawsCountsEveryCall()
        {
            var rng = new Rng(1);
            rng.RangeInclusive(0, 3);
            rng.NextFloat();
            Assert.AreEqual(2, rng.Draws);
        }

        [Test]
        public void DeriveIsStableAndIndependent()
        {
            Rng a = new Rng(5).Derive("enemy_ai");
            Rng b = new Rng(5).Derive("enemy_ai");
            var plain = new Rng(5);
            bool differs = false;
            for (int i = 0; i < 10; i++)
            {
                int va = a.RangeInclusive(0, 1000);
                Assert.AreEqual(va, b.RangeInclusive(0, 1000), "same seed and salt give same sequence");
                differs |= va != plain.RangeInclusive(0, 1000);
            }
            Assert.IsTrue(differs, "derived sequence differs from the parent seed");
        }
    }
}
```

`Tests/EditMode/DataTests.cs` (GODOT `tests/test_data.gd` 의 `card_defaults`, `ally_extends_unit_data` 이식 + 기본값 확인. `starter_cards_exist` 는 Task 6):
```csharp
using NUnit.Framework;
using ProjectVoid.Combat;
using UnityEngine;

namespace ProjectVoid.Tests
{
    public class DataTests
    {
        [TearDown]
        public void TearDown() => Make.Cleanup();

        [Test]
        public void CardDefaults()
        {
            var card = Make.Asset<CardData>();
            Assert.AreEqual(1, card.spCost, "sp_cost default");
            Assert.AreEqual(AttackType.Melee, card.attackType, "attack_type default");
            Assert.AreEqual(Shape.Single, card.shape, "shape default");
            Assert.AreEqual(1, card.attackRange, "attack_range default");
            Assert.AreEqual(0, card.damage, "damage default");
        }

        [Test]
        public void AllyExtendsUnitData()
        {
            var ally = Make.Asset<AllyData>();
            Assert.IsInstanceOf<UnitData>(ally, "AllyData is UnitData");
            Assert.AreEqual(3, ally.maxSp, "max_sp default");
            Assert.AreEqual(0, ally.deck.Count, "deck starts empty");
            Assert.AreEqual(10, ally.maxHp, "max_hp default");
            Assert.AreEqual(10, ally.speed, "speed default");
        }

        [Test]
        public void EnemyDefaults()
        {
            var enemy = Make.Asset<EnemyData>();
            Assert.AreEqual(5, enemy.attackDamage);
            Assert.AreEqual(1, enemy.attackRange);
            Assert.AreEqual(5, enemy.blockAmount);
            Assert.AreEqual(4, enemy.restHeal);
            Assert.AreEqual(0.25f, enemy.moveChance);
        }

        [Test]
        public void EncounterDefaults()
        {
            var encounter = Make.Asset<EncounterData>();
            Assert.AreEqual(new Vector2Int(3, 3), encounter.allyGrid);
            Assert.AreEqual(new Vector2Int(3, 3), encounter.enemyGrid);
        }
    }
}
```
GODOT `test_data.gd` 에 위에 없는 단언이 있으면 같은 테스트 메서드에 추가한다.

`Tests/EditMode/Make.cs`:
```csharp
using System.Collections.Generic;
using ProjectVoid.Combat;
using UnityEngine;

namespace ProjectVoid.Tests
{
    /// <summary>테스트용 데이터 생성기 (Godot 테스트의 _ally/_enemy/_placement 헬퍼 자리). 만든 에셋은 Cleanup 에서 지운다.</summary>
    public static class Make
    {
        private static readonly List<Object> Created = new List<Object>();

        public static T Asset<T>() where T : ScriptableObject
        {
            var asset = ScriptableObject.CreateInstance<T>();
            Created.Add(asset);
            return asset;
        }

        public static CardData Card(string id, int damage = 0, int spCost = 1, int attackRange = 1,
            AttackType attackType = AttackType.Melee, Shape shape = Shape.Single)
        {
            var card = Asset<CardData>();
            card.id = id;
            card.displayName = id;
            card.damage = damage;
            card.spCost = spCost;
            card.attackRange = attackRange;
            card.attackType = attackType;
            card.shape = shape;
            return card;
        }

        public static AllyData Ally(string id, int maxHp = 20, int speed = 10, int maxSp = 3, params CardData[] deck)
        {
            var data = Asset<AllyData>();
            data.id = id;
            data.displayName = id;
            data.maxHp = maxHp;
            data.speed = speed;
            data.maxSp = maxSp;
            data.deck = new List<CardData>(deck);
            return data;
        }

        public static EnemyData Enemy(string id, int maxHp = 20, int speed = 10, int attackDamage = 5, int attackRange = 1,
            AttackType attackType = AttackType.Melee, Shape attackShape = Shape.Single,
            int blockAmount = 5, int restHeal = 4, float moveChance = 0f)
        {
            var data = Asset<EnemyData>();
            data.id = id;
            data.displayName = id;
            data.maxHp = maxHp;
            data.speed = speed;
            data.attackDamage = attackDamage;
            data.attackRange = attackRange;
            data.attackType = attackType;
            data.attackShape = attackShape;
            data.blockAmount = blockAmount;
            data.restHeal = restHeal;
            data.moveChance = moveChance;
            return data;
        }

        public static UnitPlacement Place(UnitData data, int col, int row) => new UnitPlacement(data, new Vector2Int(col, row));

        public static EncounterData Encounter(Vector2Int allyGrid, Vector2Int enemyGrid,
            IEnumerable<UnitPlacement> allies, IEnumerable<UnitPlacement> enemies)
        {
            var encounter = Asset<EncounterData>();
            encounter.allyGrid = allyGrid;
            encounter.enemyGrid = enemyGrid;
            encounter.allyUnits = new List<UnitPlacement>(allies);
            encounter.enemyUnits = new List<UnitPlacement>(enemies);
            return encounter;
        }

        public static void Cleanup()
        {
            foreach (Object asset in Created)
            {
                if (asset != null)
                {
                    Object.DestroyImmediate(asset);
                }
            }
            Created.Clear();
        }
    }
}
```
주의: `Make.Enemy` 의 `moveChance` 기본값 0 은 테스트 편의용이다. 이식하는 Godot 헬퍼가 다른 값을 쓰면 그 값을 넘긴다. `EnemyData` 클래스 기본값은 Godot 과 같은 0.25.

- [ ] **Step 2: 검증 절차 1~2** — Expected: `Rng`, `CardData` 등이 없어 `error CS0246`.

- [ ] **Step 3: 구현 작성**

`Scripts/Combat/Enums.cs`:
```csharp
namespace ProjectVoid.Combat
{
    // 순서는 Godot 원본 enum 과 같다 (데이터 이식 시 숫자 값이 맞아야 한다).
    public enum Team { Ally, Enemy }
    public enum AttackType { Melee, Ranged }
    public enum Shape { Single, Pierce, Sweep, Area, Line }
    public enum Phase { Standby, Draw, Action, BeforeEnd, AfterEnd }
    public enum EnemyAction { Attack, Defend, Rest, Move }
}
```

`Scripts/Combat/Rng.cs`:
```csharp
using System;

namespace ProjectVoid.Combat
{
    /// <summary>시드를 고정할 수 있는 난수 생성기 (Godot RandomNumberGenerator 대체). 수열 자체는 Godot 과 다르다.</summary>
    public sealed class Rng
    {
        private readonly Random _random;

        public Rng(int seed)
        {
            Seed = seed;
            _random = new Random(seed);
        }

        public int Seed { get; }

        /// <summary>지금까지 뽑은 횟수. Godot 테스트가 rng.state 로 확인하던 "난수를 썼는가"를 대신한다.</summary>
        public int Draws { get; private set; }

        /// <summary>min 이상 max 이하 (Godot randi_range 처럼 양끝 포함).</summary>
        public int RangeInclusive(int min, int max)
        {
            Draws++;
            return _random.Next(min, max + 1);
        }

        /// <summary>0 이상 1 미만 (Godot randf).</summary>
        public float NextFloat()
        {
            Draws++;
            return (float)_random.NextDouble();
        }

        /// <summary>이 시드와 salt 로만 정해지는 새 생성기. string.GetHashCode 는 실행마다 달라질 수 있어 FNV-1a 로 직접 섞는다.</summary>
        public Rng Derive(string salt)
        {
            unchecked
            {
                uint hash = 2166136261;
                foreach (char c in salt)
                {
                    hash = (hash ^ c) * 16777619;
                }
                return new Rng((int)(hash ^ ((uint)Seed * 2654435761u)));
            }
        }
    }
}
```

`Scripts/Combat/Data/CardData.cs`:
```csharp
using UnityEngine;

namespace ProjectVoid.Combat
{
    /// <summary>카드 한 종류의 설계 데이터. 전투 중에는 이 에셋을 그대로 덱·손패·묘지에 넣어 돌려 쓰고 값은 바꾸지 않는다.</summary>
    [CreateAssetMenu(menuName = "Project Void/Card", fileName = "Card")]
    public sealed class CardData : ScriptableObject
    {
        public string id = "";
        public string displayName = "";
        public int spCost = 1;
        public AttackType attackType = AttackType.Melee;
        public Shape shape = Shape.Single;
        public int attackRange = 1;
        public int damage;
    }
}
```

`Scripts/Combat/Data/UnitData.cs`:
```csharp
using UnityEngine;

namespace ProjectVoid.Combat
{
    /// <summary>아군·적군 공통 설계 데이터. AllyData / EnemyData 가 상속한다.</summary>
    public abstract class UnitData : ScriptableObject
    {
        public string id = "";
        public string displayName = "";
        public int maxHp = 10;
        public int speed = 10;
        // Godot 에는 없는 표현용 필드. 비어 있으면 임시 실루엣을 쓴다.
        public Sprite sprite;
    }
}
```

`Scripts/Combat/Data/AllyData.cs`:
```csharp
using System.Collections.Generic;
using UnityEngine;

namespace ProjectVoid.Combat
{
    [CreateAssetMenu(menuName = "Project Void/Ally", fileName = "Ally")]
    public sealed class AllyData : UnitData
    {
        public int maxSp = 3;
        public List<CardData> deck = new List<CardData>();
    }
}
```

`Scripts/Combat/Data/EnemyData.cs`:
```csharp
using UnityEngine;

namespace ProjectVoid.Combat
{
    [CreateAssetMenu(menuName = "Project Void/Enemy", fileName = "Enemy")]
    public sealed class EnemyData : UnitData
    {
        public int attackDamage = 5;
        public AttackType attackType = AttackType.Melee;
        public Shape attackShape = Shape.Single;
        public int attackRange = 1;
        public int blockAmount = 5;
        public int restHeal = 4;
        // 0 이면 이동하지 않고 난수도 쓰지 않는다.
        public float moveChance = 0.25f;
    }
}
```

`Scripts/Combat/Data/UnitPlacement.cs`:
```csharp
using System;
using UnityEngine;

namespace ProjectVoid.Combat
{
    /// <summary>"어떤 유닛이 자기 편 격자의 어느 칸에 서는가" 한 줄. cell.x = 열(0 이 앞줄), cell.y = 행.</summary>
    [Serializable]
    public sealed class UnitPlacement
    {
        public UnitData unitData;
        public Vector2Int cell;

        public UnitPlacement() { }

        public UnitPlacement(UnitData unitData, Vector2Int cell)
        {
            this.unitData = unitData;
            this.cell = cell;
        }
    }
}
```

`Scripts/Combat/Data/EncounterData.cs`:
```csharp
using System.Collections.Generic;
using UnityEngine;

namespace ProjectVoid.Combat
{
    /// <summary>전투 한 판의 구성. 격자 x = 열 수, y = 행 수. 리스트 순서대로 유닛 id 가 매겨진다 (아군 먼저).</summary>
    [CreateAssetMenu(menuName = "Project Void/Encounter", fileName = "Encounter")]
    public sealed class EncounterData : ScriptableObject
    {
        public Vector2Int allyGrid = new Vector2Int(3, 3);
        public Vector2Int enemyGrid = new Vector2Int(3, 3);
        public List<UnitPlacement> allyUnits = new List<UnitPlacement>();
        public List<UnitPlacement> enemyUnits = new List<UnitPlacement>();
    }
}
```

- [ ] **Step 4: 검증** — 검증 절차 1~4, FILTER `ProjectVoid\.Tests\.(RngTests|DataTests)`. Expected: `PASSED 8 / FAILED 0`.

- [ ] **Step 5: Commit**

```bash
git add Assets/_Project
git commit -m "feat: add combat enums, seeded rng and data assets

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 3: Unit

**Files:**
- Create: `Assets/_Project/Scripts/Combat/Unit.cs`
- Test: `Assets/_Project/Tests/EditMode/UnitTests.cs` (GODOT `tests/test_unit.gd` 이식)

**Interfaces:**
- Consumes: Task 2 의 `UnitData`, `AllyData`, `CardData`, `Team`, `Rng`
- Produces: `Unit(int unitId, UnitData data, Team team, Vector2Int cell)`; 프로퍼티 `int UnitId`, `UnitData Data`, `Team Team`, `Vector2Int Cell {get;set;}`, `int Hp {get;set;}`, `int Block {get;set;}`, `int Sp {get;set;}`, `List<CardData> Deck, Hand, Discard, Exile`, `bool IsAlive`, `bool IsAlly`; 메서드 `TakeDamage(int)`, `Heal(int)`, `GainBlock(int)`, `ShuffleDeck(Rng)`, `int ReshuffleDiscard(Rng)`, `CardData DrawOne()`, `Draw(int count, Rng)`, `DiscardHand()`

- [ ] **Step 1: 실패하는 테스트 작성** — `Tests/EditMode/UnitTests.cs`:

```csharp
using NUnit.Framework;
using ProjectVoid.Combat;
using UnityEngine;

namespace ProjectVoid.Tests
{
    public class UnitTests
    {
        [TearDown]
        public void TearDown() => Make.Cleanup();

        private static Unit AllyWithDeck(int deckSize)
        {
            var deck = new CardData[deckSize];
            for (int i = 0; i < deckSize; i++)
            {
                deck[i] = Make.Card($"c{i}");
            }
            return new Unit(1, Make.Ally("a", maxHp: 20, maxSp: 3, deck: deck), Team.Ally, Vector2Int.zero);
        }

        [Test]
        public void BlockAbsorbsBeforeHp()
        {
            Unit unit = AllyWithDeck(0);
            unit.GainBlock(5);
            unit.TakeDamage(3);
            Assert.AreEqual(20, unit.Hp, "block absorbs damage, hp untouched");
            Assert.AreEqual(2, unit.Block, "block reduced by absorbed amount");
        }

        [Test]
        public void BlockDepletes()
        {
            Unit unit = AllyWithDeck(0);
            unit.GainBlock(4);
            unit.TakeDamage(10);
            Assert.AreEqual(14, unit.Hp, "leftover damage hits hp");
            Assert.AreEqual(0, unit.Block, "block fully spent");
        }

        [Test]
        public void HealCapsAtMax()
        {
            Unit unit = AllyWithDeck(0);
            unit.TakeDamage(5);
            unit.Heal(100);
            Assert.AreEqual(20, unit.Hp, "heal does not exceed max_hp");
        }

        [Test]
        public void DrawMovesCards()
        {
            Unit unit = AllyWithDeck(6);
            unit.Draw(4, new Rng(1));
            Assert.AreEqual(4, unit.Hand.Count, "hand has 4");
            Assert.AreEqual(2, unit.Deck.Count, "deck has 2 left");
        }

        [Test]
        public void DrawReshufflesDiscard()
        {
            Unit unit = AllyWithDeck(3);
            unit.Draw(3, new Rng(1));
            unit.DiscardHand();
            Assert.AreEqual(3, unit.Discard.Count, "discard holds 3 before redraw");
            unit.Draw(2, new Rng(1));
            Assert.AreEqual(2, unit.Hand.Count, "redraw pulls from reshuffled deck");
            Assert.AreEqual(0, unit.Discard.Count, "discard emptied by reshuffle");
            Assert.AreEqual(1, unit.Deck.Count, "deck keeps the remainder");
        }

        [Test]
        public void DrawStopsWhenBothEmpty()
        {
            Unit unit = AllyWithDeck(2);
            unit.Draw(5, new Rng(1));
            Assert.AreEqual(2, unit.Hand.Count, "draws only what exists");
        }

        [Test]
        public void DiscardHand()
        {
            Unit unit = AllyWithDeck(4);
            unit.Draw(4, new Rng(1));
            unit.DiscardHand();
            Assert.AreEqual(0, unit.Hand.Count, "hand cleared");
            Assert.AreEqual(4, unit.Discard.Count, "all cards moved to discard");
        }

        [Test]
        public void EnemyHasNoZones()
        {
            var unit = new Unit(2, Make.Enemy("e", maxHp: 15), Team.Enemy, Vector2Int.zero);
            Assert.AreEqual(0, unit.Deck.Count, "enemy deck empty");
            Assert.AreEqual(0, unit.Sp, "enemy sp zero");
            Assert.IsFalse(unit.IsAlly, "enemy is not ally");
        }

        [Test]
        public void DeckIsACopyOfTheAsset()
        {
            var card = Make.Card("x");
            AllyData data = Make.Ally("a", deck: new[] { card, card });
            var unit = new Unit(0, data, Team.Ally, Vector2Int.zero);
            unit.DrawOne();
            Assert.AreEqual(2, data.deck.Count, "drawing must not change the asset's deck");
        }
    }
}
```

- [ ] **Step 2: 검증 절차 1~2** — Expected: `Unit` 없음 컴파일 에러.

- [ ] **Step 3: 구현** — `Scripts/Combat/Unit.cs` (GODOT `Scripts/combat/unit.gd` 1:1):

```csharp
using System.Collections.Generic;
using UnityEngine;

namespace ProjectVoid.Combat
{
    /// <summary>전투 중인 유닛 한 명의 실제 상태. 이벤트는 내지 않는다 — BattleState 가 바꾼 뒤 알린다.</summary>
    public sealed class Unit
    {
        public Unit(int unitId, UnitData data, Team team, Vector2Int cell)
        {
            UnitId = unitId;
            Data = data;
            Team = team;
            Cell = cell;
            Hp = data.maxHp;
            if (data is AllyData ally)
            {
                Sp = ally.maxSp;
                // 에셋의 리스트를 직접 섞으면 원본 데이터가 바뀌므로 복사한다.
                Deck.AddRange(ally.deck);
            }
        }

        public int UnitId { get; }
        public UnitData Data { get; }
        public Team Team { get; }
        public Vector2Int Cell { get; set; }
        public int Hp { get; set; }
        public int Block { get; set; }
        public int Sp { get; set; }
        public List<CardData> Deck { get; } = new List<CardData>();
        public List<CardData> Hand { get; } = new List<CardData>();
        public List<CardData> Discard { get; } = new List<CardData>();
        public List<CardData> Exile { get; } = new List<CardData>();

        public bool IsAlive => Hp > 0;
        public bool IsAlly => Team == Team.Ally;

        public void TakeDamage(int amount)
        {
            int absorbed = Mathf.Min(Block, amount);
            Block -= absorbed;
            Hp = Mathf.Max(0, Hp - (amount - absorbed));
        }

        public void Heal(int amount)
        {
            Hp = Mathf.Min(Data.maxHp, Hp + amount);
        }

        public void GainBlock(int amount)
        {
            Block += amount;
        }

        public void ShuffleDeck(Rng rng)
        {
            Shuffle(Deck, rng);
        }

        /// <summary>묘지를 덱 뒤에 붙이고 섞는다. 덱이 비었을 때만 부른다. 옮긴 장수를 돌려준다.</summary>
        public int ReshuffleDiscard(Rng rng)
        {
            int count = Discard.Count;
            Deck.AddRange(Discard);
            Discard.Clear();
            Shuffle(Deck, rng);
            return count;
        }

        /// <summary>덱 맨 앞 한 장을 손패로. 덱이 비면 null. 리셔플은 하지 않는다 (BattleState 가 한 장마다 이벤트를 내려고 나눠 부른다).</summary>
        public CardData DrawOne()
        {
            if (Deck.Count == 0)
            {
                return null;
            }
            CardData card = Deck[0];
            Deck.RemoveAt(0);
            Hand.Add(card);
            return card;
        }

        /// <summary>이벤트 없는 드로우. 실제 전투는 같은 순서로 이벤트를 내는 BattleState 쪽을 쓴다.</summary>
        public void Draw(int count, Rng rng)
        {
            for (int i = 0; i < count; i++)
            {
                if (Deck.Count == 0)
                {
                    if (Discard.Count == 0)
                    {
                        return;
                    }
                    ReshuffleDiscard(rng);
                }
                DrawOne();
            }
        }

        public void DiscardHand()
        {
            Discard.AddRange(Hand);
            Hand.Clear();
        }

        // Fisher–Yates. Godot 과 같은 순서로 난수를 소비한다 (i 를 끝에서부터, j 는 0..i).
        private static void Shuffle(List<CardData> cards, Rng rng)
        {
            for (int i = cards.Count - 1; i > 0; i--)
            {
                int j = rng.RangeInclusive(0, i);
                (cards[i], cards[j]) = (cards[j], cards[i]);
            }
        }
    }
}
```

- [ ] **Step 4: 검증** — FILTER `ProjectVoid\.Tests\.UnitTests`. Expected: `PASSED 9 / FAILED 0`.

- [ ] **Step 5: Commit**

```bash
git add Assets/_Project
git commit -m "feat: port combat Unit with deck zones

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 4: TargetResolver

**Files:**
- Create: `Assets/_Project/Scripts/Combat/TargetResolver.cs`
- Test: `Assets/_Project/Tests/EditMode/TargetResolverTests.cs` (GODOT `tests/test_target_resolver.gd` 이식)

**Interfaces:**
- Consumes: `Unit`, `Team`, `AttackType`, `Shape`
- Produces: `TargetResolver(Vector2Int allyGrid, Vector2Int enemyGrid)`; `Vector2Int AllyGrid, EnemyGrid`; `static readonly Vector2Int[] MoveDirections`; `int RowsFor(Team)`, `Vector2Int GridFor(Team)`, `static float CenterOffset(int row, int rows)`, `int Reach(Unit attacker, Unit target)`, `int ReachCell(Unit attacker, Team targetTeam, Vector2Int targetCell)`, `bool IsBlocked(Unit target, IReadOnlyList<Unit> all)`, `bool IsCellBlocked(Team, Vector2Int, IReadOnlyList<Unit>)`, `bool IsValidTarget(Unit attacker, Unit target, AttackType, int attackRange, IReadOnlyList<Unit>)`, `bool IsValidCell(Unit attacker, Team, Vector2Int, AttackType, int attackRange, IReadOnlyList<Unit>)`, `List<Unit> ExpandShape(Unit primary, Shape, IReadOnlyList<Unit>)`, `List<Unit> ExpandShapeCell(Team, Vector2Int, Shape, IReadOnlyList<Unit>)`, `List<Vector2Int> ShapeCells(Vector2Int anchor, Shape, Vector2Int grid)`, `List<Vector2Int> MovableCells(Unit, IReadOnlyList<Unit>)`

- [ ] **Step 1: 실패하는 테스트 작성** — `Tests/EditMode/TargetResolverTests.cs`. GODOT `test_target_resolver.gd` 의 19개 케이스를 이식 규칙대로 전부 옮긴다: `ColDistance`, `RowDistanceSameSize`, `RowDistanceDifferentSize`, `RowDistanceOddEvenRoundsDown`, `MeleeBlockedByFront`, `MeleeUnblockedAfterFrontDies`, `RangedIgnoresBlocking`, `OutOfRangeRejected`, `ExpandPierce`, `ExpandSweep`, `ExpandArea`, `ExpandLine`, `IsValidCellAllowsEmptyCell`, `ExpandShapeCellHitsNeighborsFromEmptyAnchor`, `ShapeCellsIncludesEmptyCellsAndClipsToGrid`, `MovableCellsInTheMiddle`, `MovableCellsAtACorner`, `MovableCellsSkipLivingUnits`, `MovableCellsStayInOwnGrid`. 원본 헬퍼가 `Unit.new(id, data, team, cell)` 로 유닛을 직접 만들면 C# 도 `new Unit(...)` 로 만든다. 파일 골격과 첫 두 케이스 예:

```csharp
using System.Collections.Generic;
using NUnit.Framework;
using ProjectVoid.Combat;
using UnityEngine;

namespace ProjectVoid.Tests
{
    public class TargetResolverTests
    {
        [TearDown]
        public void TearDown() => Make.Cleanup();

        // 원본 _unit(id, team, cell, hp=10). UnitData 는 C# 에서 추상이라 편에 맞는 구체 타입을 쓴다.
        private static Unit NewUnit(int id, Team team, Vector2Int cell, int hp = 10)
        {
            UnitData data = team == Team.Ally ? Make.Ally($"u{id}", maxHp: hp) : (UnitData)Make.Enemy($"u{id}", maxHp: hp);
            return new Unit(id, data, team, cell);
        }

        [Test]
        public void ColDistance()
        {
            var resolver = new TargetResolver(new Vector2Int(3, 3), new Vector2Int(3, 3));
            Unit a = NewUnit(1, Team.Ally, new Vector2Int(0, 1));
            Unit e = NewUnit(2, Team.Enemy, new Vector2Int(0, 1));
            Assert.AreEqual(1, resolver.Reach(a, e), "front row vs front row is 1");
            Unit back = NewUnit(3, Team.Enemy, new Vector2Int(2, 1));
            Assert.AreEqual(3, resolver.Reach(a, back), "front vs enemy col2 is 3");
        }

        // 나머지 18개도 같은 방식으로 원본 _test_* 의 단언 전부를 옮긴다.
    }
}
```

- [ ] **Step 2: 검증 절차 1~2** — Expected: `TargetResolver` 없음 컴파일 에러.

- [ ] **Step 3: 구현** — `Scripts/Combat/TargetResolver.cs` (GODOT `Scripts/combat/target_resolver.gd` 1:1):

```csharp
using System.Collections.Generic;
using UnityEngine;

namespace ProjectVoid.Combat
{
    /// <summary>
    /// "누가 누구를 칠 수 있는가 / 누가 맞는가"를 계산하는 순수 계산기. 상태를 바꾸지 않는다.
    /// 좌표: 각 편이 자기 격자를 가진다. cell.x = 열 (0 이 상대와 가장 가까운 앞줄), cell.y = 행.
    /// </summary>
    public sealed class TargetResolver
    {
        // 위, 아래, 앞(적 쪽), 뒤 순서로 고정해 무작위 선택이 시드마다 재현되게 한다.
        public static readonly Vector2Int[] MoveDirections =
        {
            new Vector2Int(0, -1), new Vector2Int(0, 1), new Vector2Int(-1, 0), new Vector2Int(1, 0),
        };

        public TargetResolver(Vector2Int allyGrid, Vector2Int enemyGrid)
        {
            AllyGrid = allyGrid;
            EnemyGrid = enemyGrid;
        }

        public Vector2Int AllyGrid { get; }
        public Vector2Int EnemyGrid { get; }

        public int RowsFor(Team team) => team == Team.Ally ? AllyGrid.y : EnemyGrid.y;

        public Vector2Int GridFor(Team team) => team == Team.Ally ? AllyGrid : EnemyGrid;

        /// <summary>행 번호를 격자 가운데로부터의 거리로 바꾼다. 3행이면 0 → -1, 1 → 0, 2 → 1.</summary>
        public static float CenterOffset(int row, int rows) => row - (rows - 1) / 2f;

        public int Reach(Unit attacker, Unit target) => ReachCell(attacker, target.Team, target.Cell);

        /// <summary>거리 = (공격자 열 + 1 + 대상 열) + 내림(가운데 기준 행 차이). 칸에 유닛이 없어도 된다.</summary>
        public int ReachCell(Unit attacker, Team targetTeam, Vector2Int targetCell)
        {
            int colDistance = attacker.Cell.x + 1 + targetCell.x;
            float attackerOffset = CenterOffset(attacker.Cell.y, RowsFor(attacker.Team));
            float targetOffset = CenterOffset(targetCell.y, RowsFor(targetTeam));
            return colDistance + Mathf.FloorToInt(Mathf.Abs(attackerOffset - targetOffset));
        }

        public bool IsBlocked(Unit target, IReadOnlyList<Unit> allUnits) => IsCellBlocked(target.Team, target.Cell, allUnits);

        /// <summary>같은 편·같은 행에서 더 앞 열에 살아 있는 유닛이 있으면 근접 공격이 막힌다. 빈 칸도 막힐 수 있다.</summary>
        public bool IsCellBlocked(Team targetTeam, Vector2Int targetCell, IReadOnlyList<Unit> allUnits)
        {
            foreach (Unit unit in allUnits)
            {
                if (unit.Team != targetTeam || !unit.IsAlive)
                {
                    continue;
                }
                if (unit.Cell.y == targetCell.y && unit.Cell.x < targetCell.x)
                {
                    return true;
                }
            }
            return false;
        }

        public bool IsValidTarget(Unit attacker, Unit target, AttackType attackType, int attackRange, IReadOnlyList<Unit> allUnits)
        {
            if (!target.IsAlive)
            {
                return false;
            }
            return IsValidCell(attacker, target.Team, target.Cell, attackType, attackRange, allUnits);
        }

        public bool IsValidCell(Unit attacker, Team targetTeam, Vector2Int targetCell, AttackType attackType, int attackRange, IReadOnlyList<Unit> allUnits)
        {
            if (targetTeam == attacker.Team)
            {
                return false;
            }
            if (ReachCell(attacker, targetTeam, targetCell) > attackRange)
            {
                return false;
            }
            if (attackType == AttackType.Melee && IsCellBlocked(targetTeam, targetCell, allUnits))
            {
                return false;
            }
            return true;
        }

        public List<Unit> ExpandShape(Unit primary, Shape shape, IReadOnlyList<Unit> allUnits)
            => ExpandShapeCell(primary.Team, primary.Cell, shape, allUnits);

        /// <summary>겨냥한 칸 기준으로 실제 맞는 살아 있는 유닛들. 겨냥한 칸이 비어 있어도 범위 안 다른 유닛은 맞는다.</summary>
        public List<Unit> ExpandShapeCell(Team anchorTeam, Vector2Int anchorCell, Shape shape, IReadOnlyList<Unit> allUnits)
        {
            var hit = new List<Unit>();
            if (shape == Shape.Single)
            {
                foreach (Unit unit in allUnits)
                {
                    if (unit.Team == anchorTeam && unit.Cell == anchorCell && unit.IsAlive)
                    {
                        hit.Add(unit);
                    }
                }
                return hit;
            }
            foreach (Unit unit in allUnits)
            {
                if (unit.Team != anchorTeam || !unit.IsAlive)
                {
                    continue;
                }
                bool inShape = shape switch
                {
                    Shape.Pierce => unit.Cell.y == anchorCell.y,
                    Shape.Sweep => unit.Cell.x == anchorCell.x,
                    Shape.Area => InArea(unit.Cell, anchorCell),
                    Shape.Line => unit.Cell.y == anchorCell.y && unit.Cell.x <= anchorCell.x,
                    _ => false,
                };
                if (inShape)
                {
                    hit.Add(unit);
                }
            }
            return hit;
        }

        /// <summary>범위가 덮는 칸 전부 (유닛 유무 무관, 격자 밖 제외). 호버 미리보기용.</summary>
        public List<Vector2Int> ShapeCells(Vector2Int anchor, Shape shape, Vector2Int grid)
        {
            var cells = new List<Vector2Int>();
            switch (shape)
            {
                case Shape.Single:
                    cells.Add(anchor);
                    break;
                case Shape.Pierce:
                    for (int x = 0; x < grid.x; x++) cells.Add(new Vector2Int(x, anchor.y));
                    break;
                case Shape.Sweep:
                    for (int y = 0; y < grid.y; y++) cells.Add(new Vector2Int(anchor.x, y));
                    break;
                case Shape.Area:
                    for (int dx = 0; dx < 2; dx++)
                    {
                        for (int dy = 0; dy < 2; dy++)
                        {
                            var cell = anchor + new Vector2Int(dx, dy);
                            if (cell.x < grid.x && cell.y < grid.y) cells.Add(cell);
                        }
                    }
                    break;
                case Shape.Line:
                    for (int x = 0; x < anchor.x + 1; x++) cells.Add(new Vector2Int(x, anchor.y));
                    break;
            }
            return cells;
        }

        /// <summary>자기 편 격자 안, 살아 있는 유닛이 없는 상하좌우 칸 (MoveDirections 순서).</summary>
        public List<Vector2Int> MovableCells(Unit unit, IReadOnlyList<Unit> allUnits)
        {
            var cells = new List<Vector2Int>();
            Vector2Int grid = GridFor(unit.Team);
            foreach (Vector2Int direction in MoveDirections)
            {
                Vector2Int cell = unit.Cell + direction;
                if (cell.x < 0 || cell.y < 0 || cell.x >= grid.x || cell.y >= grid.y)
                {
                    continue;
                }
                if (Occupied(unit.Team, cell, allUnits))
                {
                    continue;
                }
                cells.Add(cell);
            }
            return cells;
        }

        private static bool InArea(Vector2Int cell, Vector2Int anchor)
            => cell.x >= anchor.x && cell.x <= anchor.x + 1 && cell.y >= anchor.y && cell.y <= anchor.y + 1;

        private static bool Occupied(Team team, Vector2Int cell, IReadOnlyList<Unit> allUnits)
        {
            foreach (Unit other in allUnits)
            {
                if (other.Team == team && other.Cell == cell && other.IsAlive)
                {
                    return true;
                }
            }
            return false;
        }
    }
}
```

- [ ] **Step 4: 검증** — FILTER `ProjectVoid\.Tests\.TargetResolverTests`. Expected: `PASSED 19 / FAILED 0`.

- [ ] **Step 5: Commit**

```bash
git add Assets/_Project
git commit -m "feat: port TargetResolver reach, blocking and shapes

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 5: BattleState · EnemyBrain

**Files:**
- Create: `Assets/_Project/Scripts/Combat/BattleState.cs`, `Assets/_Project/Scripts/Combat/EnemyBrain.cs`
- Modify: `Assets/_Project/Tests/EditMode/Make.cs` (`State` 추가)
- Test: `Assets/_Project/Tests/EditMode/BattleStateTests.cs` (GODOT `test_battle_state.gd`), `Assets/_Project/Tests/EditMode/EnemyBrainTests.cs` (GODOT `test_enemy_brain.gd`)

**Interfaces:**
- Consumes: `Unit`, `TargetResolver`, `EncounterData`, `Rng`, enums
- Produces:
  - `BattleState(EncounterData encounter, Rng rng)`; `const int DrawPerTurn = 4`
  - 이벤트 (Godot 신호와 1:1): `TurnStarted(Unit)`, `UnitDamaged(Unit, int)`, `UnitDied(Unit)`, `BattleEnded(bool)`, `LogMessage(string)`, `CardPlayed(Unit, CardData, Team, Vector2Int)`, `EnemyActed(Unit, EnemyAction, Unit)`, `UnitHealed(Unit, int)`, `BlockGained(Unit, int)`, `DeckReshuffled(Unit, int)`, `CardDrawn(Unit, CardData, int deckCount, int discardCount)`, `HandDiscarded(Unit, IReadOnlyList<CardData>, int discardCount)`, `PhaseStarted(Unit, Phase)`, `UnitMoved(Unit, Vector2Int from, Vector2Int to)`
  - 상태: `List<Unit> Units`, `TargetResolver Resolver`, `Rng Rng`, `Rng AiRng`, `int RoundIndex`, `List<Unit> Initiative`, `int TurnIndex`, `bool Finished`, `bool AllyWon`, `Phase CurrentPhase` — `RoundIndex/Initiative/TurnIndex/Finished/AllyWon` 은 Godot 테스트가 직접 조작하므로 public setter
  - 메서드: `List<Unit> LivingUnits(Team)`, `Unit CurrentUnit()`, `WriteLog(string)`, `ApplyDamage(Unit, int)`, `ApplyHeal(Unit, int)`, `ApplyBlock(Unit, int)`, `ReportEnemyAction(Unit, EnemyAction, Unit)`, `CheckEnd()`, `bool PlayCard(int handIndex, Team targetTeam, Vector2Int targetCell)`, `bool MoveUnit(Vector2Int toCell)`, `ApplyMove(Unit, Vector2Int)`, `StartBattle()`, `EndTurn()`
  - `static class EnemyBrain`: `const float RestThreshold = 0.3f`, `EnemyAction Decide(BattleState, Unit)`, `Unit FindTarget(BattleState, Unit)`, `void TakeTurn(BattleState, Unit)`
  - 테스트: `Make.State(EncounterData encounter, int seed)`

- [ ] **Step 1: Make 에 State 추가** — `Make.cs` 의 `Cleanup` 위에:

```csharp
        public static BattleState State(EncounterData encounter, int seed) => new BattleState(encounter, new Rng(seed));
```

- [ ] **Step 2: 실패하는 테스트 작성**
  - `BattleStateTests.cs`: GODOT `test_battle_state.gd` 11개 — `SetupPlacesUnits`, `PlayCardDamagesAndSpendsSp`, `PlayCardRejectedWithoutSp`, `PlayCardRejectedOnBlockedTarget`, `PlayCardRejectedWhenBattleFinished`, `PlayCardRejectedWhenNoCurrentActor`, `PlayCardRejectedWhenCurrentUnitIsEnemy`, `PlayCardRejectedWhenCurrentUnitIsDead`, `PlayCardRejectedOnHandIndexOutOfBounds`, `SweepHitsMultiple`, `BattleEndsWhenEnemiesWiped`. 원본 시드 12345 → `Make.State(encounter, 12345)`.
  - `EnemyBrainTests.cs`: GODOT `test_enemy_brain.gd` 9개 — `AttacksLowestHpTarget`, `RestsWhenBadlyHurt`, `DefendsWhenNoTargetInRange`, `AttackRespectsMeleeBlocking`, `DeterministicAcrossRuns`, `MovesWhenTheRollHits`, `BlockedMovePicksAnotherAction`, `ZeroChanceUsesNoRandomness`, `SameSeedSameMoves`. 원본 `_rng()` 시드 4242. `ZeroChanceUsesNoRandomness` 는 `state.ai_rng.state` 대신 `state.AiRng.Draws` 를 비교한다:

```csharp
        [Test]
        public void ZeroChanceUsesNoRandomness()
        {
            BattleState state = StateWith(Make.Ally("a", maxHp: 30, speed: 1, maxSp: 1), 0, 1, Enemy(5, 6, 5, 4));
            Unit foe = state.LivingUnits(Team.Enemy)[0];
            int before = state.AiRng.Draws;
            Assert.AreEqual(EnemyAction.Attack, EnemyBrain.Decide(state, foe), "zero chance keeps the old choice");
            Assert.AreEqual(before, state.AiRng.Draws, "zero chance draws no random number");
        }
```
  (`StateWith`, `Enemy` 는 원본 `_state`, `_enemy` 헬퍼를 옮긴 이 클래스의 private 헬퍼. 원본 `_enemy` 는 체력 20, 속도 99, 원거리 단일, move_chance 0 이다 — 원본 코드를 확인해 같은 값으로.)

- [ ] **Step 3: 검증 절차 1~2** — Expected: `BattleState`/`EnemyBrain` 없음 컴파일 에러.

- [ ] **Step 4: 구현** — `Scripts/Combat/BattleState.cs` (GODOT `Scripts/combat/battle_state.gd` 1:1):

```csharp
using System;
using System.Collections.Generic;
using UnityEngine;

namespace ProjectVoid.Combat
{
    /// <summary>
    /// 전투 한 판의 규칙과 상태. 화면 없이 끝까지 돌릴 수 있고, 상태가 바뀔 때마다 이벤트를 낸다.
    /// 흐름: StartBattle → (아군 차례에서 멈춤) → PlayCard·MoveUnit … → EndTurn → (적 차례 자동) → 다음 아군 차례에서 멈춤.
    /// 한 차례는 Standby → Draw → Action → BeforeEnd → AfterEnd 순서이고, 아군은 Action 에서 멈춘다.
    /// </summary>
    public sealed class BattleState
    {
        public const int DrawPerTurn = 4;

        public event Action<Unit> TurnStarted;
        public event Action<Unit, int> UnitDamaged;
        public event Action<Unit> UnitDied;
        public event Action<bool> BattleEnded;
        public event Action<string> LogMessage;
        public event Action<Unit, CardData, Team, Vector2Int> CardPlayed;
        public event Action<Unit, EnemyAction, Unit> EnemyActed;
        public event Action<Unit, int> UnitHealed;
        public event Action<Unit, int> BlockGained;
        public event Action<Unit, int> DeckReshuffled;
        public event Action<Unit, CardData, int, int> CardDrawn;
        public event Action<Unit, IReadOnlyList<CardData>, int> HandDiscarded;
        public event Action<Unit, Phase> PhaseStarted;
        public event Action<Unit, Vector2Int, Vector2Int> UnitMoved;

        public BattleState(EncounterData encounter, Rng rng)
        {
            Rng = rng;
            // 적 AI 는 따로 뽑은 시드를 쓴다: 적이 난수를 몇 번 쓰든 덱 섞기 순서가 바뀌지 않게.
            AiRng = rng.Derive("enemy_ai");
            Resolver = new TargetResolver(encounter.allyGrid, encounter.enemyGrid);

            int nextId = 0;
            foreach (UnitPlacement placement in encounter.allyUnits)
            {
                var ally = new Unit(nextId, placement.unitData, Team.Ally, placement.cell);
                ally.ShuffleDeck(rng);
                Units.Add(ally);
                nextId++;
            }
            foreach (UnitPlacement placement in encounter.enemyUnits)
            {
                Units.Add(new Unit(nextId, placement.unitData, Team.Enemy, placement.cell));
                nextId++;
            }
        }

        public List<Unit> Units { get; } = new List<Unit>();
        public TargetResolver Resolver { get; }
        public Rng Rng { get; }
        public Rng AiRng { get; }
        public int RoundIndex { get; set; }
        public List<Unit> Initiative { get; set; } = new List<Unit>();
        public int TurnIndex { get; set; } = -1;
        public bool Finished { get; set; }
        public bool AllyWon { get; set; }
        public Phase CurrentPhase { get; private set; } = Phase.Standby;

        public List<Unit> LivingUnits(Team team)
        {
            var alive = new List<Unit>();
            foreach (Unit unit in Units)
            {
                if (unit.IsAlive && unit.Team == team)
                {
                    alive.Add(unit);
                }
            }
            return alive;
        }

        public Unit CurrentUnit()
        {
            if (TurnIndex < 0 || TurnIndex >= Initiative.Count)
            {
                return null;
            }
            return Initiative[TurnIndex];
        }

        public void WriteLog(string text) => LogMessage?.Invoke(text);

        /// <summary>카드 공격과 적 공격이 모두 거치는 피해 통로. 쓰러지면 UnitDied 도 낸다.</summary>
        public void ApplyDamage(Unit target, int amount)
        {
            target.TakeDamage(amount);
            UnitDamaged?.Invoke(target, amount);
            if (!target.IsAlive)
            {
                UnitDied?.Invoke(target);
                WriteLog($"{target.Data.displayName} 쓰러짐");
            }
        }

        /// <summary>최대 체력에 막혀 실제로 오른 양으로 알린다.</summary>
        public void ApplyHeal(Unit target, int amount)
        {
            int before = target.Hp;
            target.Heal(amount);
            UnitHealed?.Invoke(target, target.Hp - before);
        }

        public void ApplyBlock(Unit target, int amount)
        {
            target.GainBlock(amount);
            BlockGained?.Invoke(target, amount);
        }

        public void ReportEnemyAction(Unit actor, EnemyAction action, Unit target) => EnemyActed?.Invoke(actor, action, target);

        public void CheckEnd()
        {
            if (Finished)
            {
                return;
            }
            bool alliesAlive = LivingUnits(Team.Ally).Count > 0;
            bool enemiesAlive = LivingUnits(Team.Enemy).Count > 0;
            if (alliesAlive && enemiesAlive)
            {
                return;
            }
            Finished = true;
            AllyWon = alliesAlive;
            BattleEnded?.Invoke(AllyWon);
        }

        /// <summary>
        /// 현재 아군이 손패 handIndex 번째 카드를 targetTeam 의 targetCell 에 쓴다. 칸이 비어 있어도 사거리·막힘을 통과하면 쓸 수 있다.
        /// 규칙에 맞지 않으면 아무것도 바꾸지 않고 false.
        /// </summary>
        public bool PlayCard(int handIndex, Team targetTeam, Vector2Int targetCell)
        {
            if (Finished)
            {
                return false;
            }
            Unit actor = CurrentUnit();
            if (actor == null || !actor.IsAlly || !actor.IsAlive)
            {
                return false;
            }
            if (handIndex < 0 || handIndex >= actor.Hand.Count)
            {
                return false;
            }
            CardData card = actor.Hand[handIndex];
            if (card.spCost > actor.Sp)
            {
                return false;
            }
            if (!Resolver.IsValidCell(actor, targetTeam, targetCell, card.attackType, card.attackRange, Units))
            {
                return false;
            }

            actor.Sp -= card.spCost;
            actor.Hand.RemoveAt(handIndex);
            actor.Discard.Add(card);
            // 피해 이벤트보다 먼저 나가야 화면이 돌진 → 피격 순으로 연출한다.
            CardPlayed?.Invoke(actor, card, targetTeam, targetCell);
            WriteLog($"{actor.Data.displayName} → {DescribeCell(targetTeam, targetCell)} ({card.displayName})");

            foreach (Unit victim in Resolver.ExpandShapeCell(targetTeam, targetCell, card.shape, Units))
            {
                ApplyDamage(victim, card.damage);
            }
            CheckEnd();
            return true;
        }

        private string DescribeCell(Team team, Vector2Int cell)
        {
            foreach (Unit unit in Units)
            {
                if (unit.Team == team && unit.Cell == cell && unit.IsAlive)
                {
                    return unit.Data.displayName;
                }
            }
            return "빈 칸";
        }

        /// <summary>현재 아군이 SP 1 로 상하좌우 빈 칸 하나 이동. 규칙에 맞지 않으면 false.</summary>
        public bool MoveUnit(Vector2Int toCell)
        {
            if (Finished)
            {
                return false;
            }
            Unit actor = CurrentUnit();
            if (actor == null || !actor.IsAlly || !actor.IsAlive)
            {
                return false;
            }
            if (actor.Sp < 1)
            {
                return false;
            }
            if (!Resolver.MovableCells(actor, Units).Contains(toCell))
            {
                return false;
            }
            actor.Sp -= 1;
            ApplyMove(actor, toCell);
            return true;
        }

        /// <summary>검사 없이 옮기고 알린다 (MoveUnit 과 EnemyBrain 의 통로).</summary>
        public void ApplyMove(Unit unit, Vector2Int toCell)
        {
            Vector2Int fromCell = unit.Cell;
            unit.Cell = toCell;
            UnitMoved?.Invoke(unit, fromCell, toCell);
            WriteLog($"{unit.Data.displayName} 이동");
        }

        public void StartBattle()
        {
            // 유닛이 없거나 이미 결판난 인카운터에서 빈 라운드만 무한 반복하지 않도록 먼저 확인한다.
            CheckEnd();
            StartRound();
            RunUntilPlayerInput();
        }

        public void EndTurn()
        {
            Unit actor = CurrentUnit();
            if (actor != null && actor.IsAlly)
            {
                EndPhases(actor);
            }
            RunUntilPlayerInput();
        }

        private void StartRound()
        {
            RoundIndex++;
            Initiative = ComputeInitiative();
            TurnIndex = -1;
        }

        private List<Unit> ComputeInitiative()
        {
            var alive = new List<Unit>();
            foreach (Unit unit in Units)
            {
                if (unit.IsAlive)
                {
                    alive.Add(unit);
                }
            }
            // 속도가 빠른 순, 같으면 번호가 작은 순 (전순서라 정렬 안정성과 무관하게 결과가 같다).
            alive.Sort((a, b) => a.Data.speed != b.Data.speed ? b.Data.speed.CompareTo(a.Data.speed) : a.UnitId.CompareTo(b.UnitId));
            return alive;
        }

        private void RunUntilPlayerInput()
        {
            while (!Finished)
            {
                TurnIndex++;
                if (TurnIndex >= Initiative.Count)
                {
                    StartRound();
                    continue;
                }
                Unit actor = Initiative[TurnIndex];
                if (!actor.IsAlive)
                {
                    continue;
                }
                StartPhases(actor);
                EnterPhase(actor, Phase.Action);
                if (actor.IsAlly)
                {
                    return;
                }
                EnemyBrain.TakeTurn(this, actor);
                CheckEnd();
                if (Finished)
                {
                    return;
                }
                EndPhases(actor);
            }
        }

        private void EnterPhase(Unit actor, Phase nextPhase)
        {
            CurrentPhase = nextPhase;
            PhaseStarted?.Invoke(actor, nextPhase);
        }

        private void StartPhases(Unit actor)
        {
            EnterPhase(actor, Phase.Standby);
            actor.Block = 0;
            if (actor.IsAlly)
            {
                actor.Sp = ((AllyData)actor.Data).maxSp;
            }
            // 초기화된 값이 기록되도록 스탠바이 처리 뒤에 알린다.
            TurnStarted?.Invoke(actor);
            EnterPhase(actor, Phase.Draw);
            if (actor.IsAlly)
            {
                DrawCards(actor, DrawPerTurn);
            }
        }

        private void EndPhases(Unit actor)
        {
            EnterPhase(actor, Phase.BeforeEnd);
            EnterPhase(actor, Phase.AfterEnd);
            if (actor.IsAlly)
            {
                DiscardHand(actor);
            }
        }

        // Unit.Draw 와 같은 순서로 진행하되 한 장마다 이벤트를 낸다.
        private void DrawCards(Unit actor, int count)
        {
            for (int i = 0; i < count; i++)
            {
                if (actor.Deck.Count == 0)
                {
                    if (actor.Discard.Count == 0)
                    {
                        return;
                    }
                    DeckReshuffled?.Invoke(actor, actor.ReshuffleDiscard(Rng));
                }
                CardData card = actor.DrawOne();
                CardDrawn?.Invoke(actor, card, actor.Deck.Count, actor.Discard.Count);
            }
        }

        private void DiscardHand(Unit actor)
        {
            var cards = new List<CardData>(actor.Hand);
            actor.DiscardHand();
            HandDiscarded?.Invoke(actor, cards, actor.Discard.Count);
        }
    }
}
```

`Scripts/Combat/EnemyBrain.cs` (GODOT `Scripts/combat/enemy_brain.gd` 1:1):

```csharp
using System.Collections.Generic;
using UnityEngine;

namespace ProjectVoid.Combat
{
    /// <summary>적 AI. 상태를 직접 바꾸지 않고 BattleState 의 Apply*/Report* 통로를 불러 이벤트가 빠짐없이 나가게 한다.</summary>
    public static class EnemyBrain
    {
        public const float RestThreshold = 0.3f;

        /// <summary>
        /// 먼저 moveChance 확률로 이동 (갈 칸이 없으면 공격·방어·휴식 중 무작위).
        /// 아니면 체력 낮음 → 휴식, 칠 대상 없음 → 방어, 그 외 → 공격.
        /// </summary>
        public static EnemyAction Decide(BattleState state, Unit actor)
        {
            var data = (EnemyData)actor.Data;
            if (data.moveChance > 0f && state.AiRng.NextFloat() < data.moveChance)
            {
                if (state.Resolver.MovableCells(actor, state.Units).Count > 0)
                {
                    return EnemyAction.Move;
                }
                return RandomFallback(state, actor);
            }
            float hpRatio = (float)actor.Hp / data.maxHp;
            if (hpRatio <= RestThreshold && data.restHeal > 0)
            {
                return EnemyAction.Rest;
            }
            if (FindTarget(state, actor) == null)
            {
                return EnemyAction.Defend;
            }
            return EnemyAction.Attack;
        }

        private static EnemyAction RandomFallback(BattleState state, Unit actor)
        {
            var choices = new List<EnemyAction>();
            if (FindTarget(state, actor) != null)
            {
                choices.Add(EnemyAction.Attack);
            }
            choices.Add(EnemyAction.Defend);
            choices.Add(EnemyAction.Rest);
            return choices[state.AiRng.RangeInclusive(0, choices.Count - 1)];
        }

        /// <summary>칠 수 있는 대상 중 체력이 가장 낮은 유닛, 같으면 UnitId 가 작은 유닛. 없으면 null.</summary>
        public static Unit FindTarget(BattleState state, Unit actor)
        {
            var data = (EnemyData)actor.Data;
            Unit best = null;
            foreach (Unit candidate in state.Units)
            {
                if (!state.Resolver.IsValidTarget(actor, candidate, data.attackType, data.attackRange, state.Units))
                {
                    continue;
                }
                if (best == null
                    || candidate.Hp < best.Hp
                    || (candidate.Hp == best.Hp && candidate.UnitId < best.UnitId))
                {
                    best = candidate;
                }
            }
            return best;
        }

        public static void TakeTurn(BattleState state, Unit actor)
        {
            var data = (EnemyData)actor.Data;
            switch (Decide(state, actor))
            {
                case EnemyAction.Rest:
                    state.ReportEnemyAction(actor, EnemyAction.Rest, null);
                    state.ApplyHeal(actor, data.restHeal);
                    state.WriteLog($"{data.displayName} 휴식");
                    break;
                case EnemyAction.Defend:
                    state.ReportEnemyAction(actor, EnemyAction.Defend, null);
                    state.ApplyBlock(actor, data.blockAmount);
                    state.WriteLog($"{data.displayName} 방어");
                    break;
                case EnemyAction.Attack:
                {
                    Unit target = FindTarget(state, actor);
                    if (target == null)
                    {
                        return;
                    }
                    state.ReportEnemyAction(actor, EnemyAction.Attack, target);
                    state.WriteLog($"{data.displayName} → {target.Data.displayName} 공격");
                    foreach (Unit victim in state.Resolver.ExpandShape(target, data.attackShape, state.Units))
                    {
                        state.ApplyDamage(victim, data.attackDamage);
                    }
                    break;
                }
                case EnemyAction.Move:
                {
                    List<Vector2Int> cells = state.Resolver.MovableCells(actor, state.Units);
                    if (cells.Count == 0)
                    {
                        return;
                    }
                    Vector2Int cell = cells[state.AiRng.RangeInclusive(0, cells.Count - 1)];
                    state.ReportEnemyAction(actor, EnemyAction.Move, null);
                    state.ApplyMove(actor, cell);
                    break;
                }
            }
        }
    }
}
```

- [ ] **Step 5: 검증** — FILTER `ProjectVoid\.Tests\.(BattleStateTests|EnemyBrainTests)`. Expected: `PASSED 20 / FAILED 0`. 실패가 나면 이식 규칙의 "난수 수열 차이" 절차 외에는 테스트를 고치지 말고 구현을 원본과 대조한다.

- [ ] **Step 6: Commit**

```bash
git add Assets/_Project
git commit -m "feat: port BattleState turn flow and EnemyBrain

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 6: 나머지 규칙 테스트 이식

**Files:**
- Test: `Assets/_Project/Tests/EditMode/MovementTests.cs` (GODOT `test_movement.gd`, 5개: `MoveSpendsSpAndMoves`, `RejectsInvalidCells`, `RejectsOutsideAnAllyTurn`, `MovesWhileSpLasts`, `ReachFollowsTheNewCell`)
- Test: `Assets/_Project/Tests/EditMode/TurnPhasesTests.cs` (GODOT `test_turn_phases.gd`, 4개: `AllyTurnStartOrder`, `EndTurnRunsEndPhasesThenEnemyTurn`, `StandbyResetsBeforeTurnStarted`, `BattleEndSkipsEndPhases`)
- Test: `Assets/_Project/Tests/EditMode/TurnOrderTests.cs` (GODOT `test_turn_order.gd`, 7개: `InitiativeSortedBySpeed`, `InitiativeTiesBrokenById`, `TurnStartRefillsSpAndDraws`, `EndTurnDiscardsHand`, `DeadUnitsAreSkipped`, `NewRoundAfterEveryoneActed`, `EmptyEncounterFinishesWithoutHanging`)
- Test: `Assets/_Project/Tests/EditMode/BattleSignalsTests.cs` (GODOT `test_battle_signals.gd`, 5개: `CardPlayedPrecedesDamage`, `EnemyAttackSignals`, `EnemyDefendSignals`, `EnemyRestSignals`, `EnemyRestReportsCappedAmount`)
- Test: `Assets/_Project/Tests/EditMode/CardZoneSignalsTests.cs` (GODOT `test_card_zone_signals.gd`, 6개: `DrawReportsCounts`, `ReshuffleHappensMidDraw`, `DrawStopsWhenDeckAndDiscardAreEmpty`, `HandDiscardedCarriesCards`, `StateDrawMatchesUnitDraw`, `EnemyMovesDoNotChangeDraws`)

**Interfaces:**
- Consumes: Task 5 의 `BattleState` 전체, `Make.State`
- Produces: 없음 (테스트만). 규칙 코드 수정이 필요해지면 그 수정은 원본과의 차이를 고치는 것이어야 한다.

- [ ] **Step 1: 다섯 파일을 이식 규칙대로 작성** (27개 케이스 전부). 신호 순서를 확인하는 테스트는 문자열 로그로 옮긴다. 예 — `BattleSignalsTests.CardPlayedPrecedesDamage` 의 이벤트 기록 방식:

```csharp
            var log = new List<string>();
            state.CardPlayed += (actor, card, team, cell) => log.Add("card_played");
            state.UnitDamaged += (unit, amount) => log.Add("unit_damaged");
            // ... 원본과 같은 행동 후
            Assert.AreEqual(0, log.IndexOf("card_played"), "card_played comes first");
```
`TurnPhasesTests` 는 `state.PhaseStarted += (u, p) => log.Add($"{u.Data.id}:{p}")` 처럼 원본이 쓰는 문자열과 같은 뜻의 문자열을 만든다 (enum 이름은 C# 이름 `Standby` 등이 된다 — 기대 문자열도 거기에 맞춘다).

- [ ] **Step 2: 검증** — FILTER `ProjectVoid\.Tests\.(MovementTests|TurnPhasesTests|TurnOrderTests|BattleSignalsTests|CardZoneSignalsTests)`. Expected: `PASSED 27 / FAILED 0`. 실패하면 구현을 원본과 대조해 고친다 (난수 수열 예외 절차만 테스트 수정 허용).

- [ ] **Step 3: 전체 규칙 테스트 재확인** — FILTER `""`. Expected: 지금까지 전부 통과 (1 + 8 + 9 + 19 + 20 + 27 = 84).

- [ ] **Step 4: Commit**

```bash
git add Assets/_Project
git commit -m "test: port movement, phase, order and signal rule tests

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 7: 시작 데이터 에셋 (카드 7 · 유닛 6 · skirmish)

**Files:**
- Create: `Assets/_Project/Scripts/Editor/DataImporter.cs`
- Create (생성물): `Assets/_Project/Data/Cards/{strike,cleave,skewer,shoot,piercing_shot,volley,blast}.asset`, `Assets/_Project/Data/Units/{vanguard,archer,scout,brute,sentry,stalker}.asset`, `Assets/_Project/Data/Encounters/skirmish.asset`
- Test: `Assets/_Project/Tests/EditMode/StarterDataTests.cs` (GODOT `test_data.gd` 의 `starter_cards_exist` + 헤드리스 한 판)

**Interfaces:**
- Consumes: Task 2 데이터 타입, Task 5 `BattleState`
- Produces: `DataImporter.ImportAll()` (메뉴 `Project Void/Import Starter Data`), 경로 상수 `DataImporter.CardsDir/UnitsDir/EncountersDir`, 에셋 `Assets/_Project/Data/Encounters/skirmish.asset` (Task 15 씬이 참조)

- [ ] **Step 1: 실패하는 테스트 작성** — `Tests/EditMode/StarterDataTests.cs`:

```csharp
using NUnit.Framework;
using ProjectVoid.Combat;
using UnityEditor;
using UnityEngine;

namespace ProjectVoid.Tests
{
    public class StarterDataTests
    {
        private static T Load<T>(string path) where T : Object => AssetDatabase.LoadAssetAtPath<T>(path);

        [Test]
        public void StarterCardsExist()
        {
            var strike = Load<CardData>("Assets/_Project/Data/Cards/strike.asset");
            Assert.IsNotNull(strike, "strike loads");
            Assert.AreEqual("strike", strike.id, "strike id");
            Assert.AreEqual(6, strike.damage, "strike damage");
            Assert.AreEqual(AttackType.Melee, strike.attackType, "strike is melee");
            var volley = Load<CardData>("Assets/_Project/Data/Cards/volley.asset");
            Assert.IsNotNull(volley, "volley loads");
            Assert.AreEqual(Shape.Sweep, volley.shape, "volley shape is SWEEP");
            Assert.AreEqual(4, volley.attackRange, "volley range");
            var blast = Load<CardData>("Assets/_Project/Data/Cards/blast.asset");
            Assert.IsNotNull(blast, "blast loads");
            Assert.AreEqual(Shape.Area, blast.shape, "blast shape is AREA");
            var skewer = Load<CardData>("Assets/_Project/Data/Cards/skewer.asset");
            Assert.IsNotNull(skewer, "skewer loads");
            Assert.AreEqual(Shape.Line, skewer.shape, "skewer shape is LINE");
        }

        [Test]
        public void UnitsMatchGodotValues()
        {
            var vanguard = Load<AllyData>("Assets/_Project/Data/Units/vanguard.asset");
            Assert.AreEqual("선봉", vanguard.displayName);
            Assert.AreEqual(30, vanguard.maxHp);
            Assert.AreEqual(12, vanguard.speed);
            Assert.AreEqual(8, vanguard.deck.Count);
            var scout = Load<AllyData>("Assets/_Project/Data/Units/scout.asset");
            Assert.AreEqual(6, scout.deck.Count);
            Assert.AreEqual(16, scout.speed);
            var sentry = Load<EnemyData>("Assets/_Project/Data/Units/sentry.asset");
            Assert.AreEqual(AttackType.Ranged, sentry.attackType);
            Assert.AreEqual(Shape.Sweep, sentry.attackShape);
            Assert.AreEqual(4, sentry.attackRange);
            Assert.AreEqual(8, sentry.blockAmount);
            Assert.AreEqual(3, sentry.restHeal);
        }

        [Test]
        public void SkirmishMatchesGodotLayout()
        {
            var skirmish = Load<EncounterData>("Assets/_Project/Data/Encounters/skirmish.asset");
            Assert.AreEqual(new Vector2Int(3, 3), skirmish.allyGrid);
            Assert.AreEqual(new Vector2Int(2, 2), skirmish.enemyGrid);
            Assert.AreEqual("vanguard", skirmish.allyUnits[0].unitData.id);
            Assert.AreEqual(new Vector2Int(0, 1), skirmish.allyUnits[0].cell);
            Assert.AreEqual(new Vector2Int(2, 0), skirmish.allyUnits[1].cell);
            Assert.AreEqual(new Vector2Int(1, 2), skirmish.allyUnits[2].cell);
            Assert.AreEqual("brute", skirmish.enemyUnits[0].unitData.id);
            Assert.AreEqual(new Vector2Int(0, 0), skirmish.enemyUnits[0].cell);
            Assert.AreEqual("stalker", skirmish.enemyUnits[1].unitData.id);
            Assert.AreEqual(new Vector2Int(1, 1), skirmish.enemyUnits[1].cell);
            Assert.AreEqual("sentry", skirmish.enemyUnits[2].unitData.id);
            Assert.AreEqual(new Vector2Int(1, 0), skirmish.enemyUnits[2].cell);
        }

        // 화면 없이 skirmish 를 끝까지 돌려 규칙 코어가 멈추지 않고 결판나는지 확인한다.
        [Test]
        public void SkirmishPlaysToAnEndHeadless([Values(1, 2, 3, 4, 5)] int seed)
        {
            var skirmish = Load<EncounterData>("Assets/_Project/Data/Encounters/skirmish.asset");
            var state = new BattleState(skirmish, new Rng(seed));
            state.StartBattle();
            for (int step = 0; step < 2000 && !state.Finished; step++)
            {
                if (!TryPlayAnyCard(state))
                {
                    state.EndTurn();
                }
            }
            Assert.IsTrue(state.Finished, "battle finishes");
        }

        private static bool TryPlayAnyCard(BattleState state)
        {
            Unit actor = state.CurrentUnit();
            for (int i = 0; i < actor.Hand.Count; i++)
            {
                Vector2Int grid = state.Resolver.EnemyGrid;
                for (int col = 0; col < grid.x; col++)
                {
                    for (int row = 0; row < grid.y; row++)
                    {
                        var cell = new Vector2Int(col, row);
                        if (state.Resolver.ExpandShapeCell(Team.Enemy, cell, actor.Hand[i].shape, state.Units).Count == 0)
                        {
                            continue;
                        }
                        if (state.PlayCard(i, Team.Enemy, cell))
                        {
                            return true;
                        }
                    }
                }
            }
            return false;
        }
    }
}
```

- [ ] **Step 2: 검증 절차 1~4** (FILTER `ProjectVoid\.Tests\.StarterDataTests`) — Expected: 컴파일은 되지만 에셋이 없어 FAIL (NullReference).

- [ ] **Step 3: 구현** — `Scripts/Editor/DataImporter.cs` (수치 출처: GODOT `Resources/cards/*.tres`, `Resources/units/*.tres`, `Resources/encounters/skirmish.tres`):

```csharp
using System.Collections.Generic;
using System.IO;
using ProjectVoid.Combat;
using UnityEditor;
using UnityEngine;

namespace ProjectVoid.EditorTools
{
    /// <summary>Godot Resources/*.tres 의 수치를 .asset 으로 옮긴다. 이미 있는 에셋은 값만 덮어써 GUID 와 sprite 지정을 유지한다.</summary>
    public static class DataImporter
    {
        public const string CardsDir = "Assets/_Project/Data/Cards";
        public const string UnitsDir = "Assets/_Project/Data/Units";
        public const string EncountersDir = "Assets/_Project/Data/Encounters";

        [MenuItem("Project Void/Import Starter Data")]
        public static void ImportAll()
        {
            Directory.CreateDirectory(CardsDir);
            Directory.CreateDirectory(UnitsDir);
            Directory.CreateDirectory(EncountersDir);

            CardData strike = Card("strike", "베기", 1, AttackType.Melee, Shape.Single, 2, 6);
            CardData cleave = Card("cleave", "횡베기", 2, AttackType.Melee, Shape.Sweep, 2, 4);
            CardData skewer = Card("skewer", "꿰뚫기", 2, AttackType.Melee, Shape.Line, 3, 4);
            CardData shoot = Card("shoot", "사격", 1, AttackType.Ranged, Shape.Single, 3, 4);
            CardData piercingShot = Card("piercing_shot", "관통사격", 2, AttackType.Ranged, Shape.Pierce, 3, 5);
            CardData volley = Card("volley", "일제사격", 2, AttackType.Ranged, Shape.Sweep, 4, 3);
            CardData blast = Card("blast", "폭발탄", 2, AttackType.Ranged, Shape.Area, 3, 3);

            AllyData vanguard = Ally("vanguard", "선봉", 30, 12, strike, strike, strike, cleave, cleave, shoot, skewer, skewer);
            AllyData archer = Ally("archer", "사수", 20, 10, shoot, shoot, shoot, volley, piercingShot, piercingShot, blast, blast);
            AllyData scout = Ally("scout", "정찰병", 22, 16, strike, strike, shoot, shoot, volley, cleave);

            EnemyData brute = Enemy("brute", "괴한", 28, 8, 7, AttackType.Melee, Shape.Single, 1, 6, 5);
            EnemyData sentry = Enemy("sentry", "보초", 24, 6, 5, AttackType.Ranged, Shape.Sweep, 4, 8, 3);
            EnemyData stalker = Enemy("stalker", "추적자", 18, 14, 4, AttackType.Ranged, Shape.Pierce, 3, 4, 4);

            var skirmish = LoadOrCreate<EncounterData>($"{EncountersDir}/skirmish.asset");
            skirmish.allyGrid = new Vector2Int(3, 3);
            skirmish.enemyGrid = new Vector2Int(2, 2);
            skirmish.allyUnits = new List<UnitPlacement>
            {
                new UnitPlacement(vanguard, new Vector2Int(0, 1)),
                new UnitPlacement(archer, new Vector2Int(2, 0)),
                new UnitPlacement(scout, new Vector2Int(1, 2)),
            };
            skirmish.enemyUnits = new List<UnitPlacement>
            {
                new UnitPlacement(brute, new Vector2Int(0, 0)),
                new UnitPlacement(stalker, new Vector2Int(1, 1)),
                new UnitPlacement(sentry, new Vector2Int(1, 0)),
            };
            EditorUtility.SetDirty(skirmish);
            AssetDatabase.SaveAssets();
        }

        private static CardData Card(string id, string displayName, int spCost, AttackType attackType, Shape shape, int attackRange, int damage)
        {
            var card = LoadOrCreate<CardData>($"{CardsDir}/{id}.asset");
            card.id = id;
            card.displayName = displayName;
            card.spCost = spCost;
            card.attackType = attackType;
            card.shape = shape;
            card.attackRange = attackRange;
            card.damage = damage;
            EditorUtility.SetDirty(card);
            return card;
        }

        private static AllyData Ally(string id, string displayName, int maxHp, int speed, params CardData[] deck)
        {
            var ally = LoadOrCreate<AllyData>($"{UnitsDir}/{id}.asset");
            ally.id = id;
            ally.displayName = displayName;
            ally.maxHp = maxHp;
            ally.speed = speed;
            ally.maxSp = 3;
            ally.deck = new List<CardData>(deck);
            EditorUtility.SetDirty(ally);
            return ally;
        }

        private static EnemyData Enemy(string id, string displayName, int maxHp, int speed, int attackDamage,
            AttackType attackType, Shape attackShape, int attackRange, int blockAmount, int restHeal)
        {
            var enemy = LoadOrCreate<EnemyData>($"{UnitsDir}/{id}.asset");
            enemy.id = id;
            enemy.displayName = displayName;
            enemy.maxHp = maxHp;
            enemy.speed = speed;
            enemy.attackDamage = attackDamage;
            enemy.attackType = attackType;
            enemy.attackShape = attackShape;
            enemy.attackRange = attackRange;
            enemy.blockAmount = blockAmount;
            enemy.restHeal = restHeal;
            enemy.moveChance = 0.25f;
            EditorUtility.SetDirty(enemy);
            return enemy;
        }

        private static T LoadOrCreate<T>(string path) where T : ScriptableObject
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset == null)
            {
                asset = ScriptableObject.CreateInstance<T>();
                AssetDatabase.CreateAsset(asset, path);
            }
            return asset;
        }
    }
}
```

- [ ] **Step 4: 에셋 생성** — 검증 절차 1~2 로 컴파일 후 `Unity_RunCommand`:

```csharp
internal class CommandScript : IRunCommand
{
    public void Execute(ExecutionResult result)
    {
        ProjectVoid.EditorTools.DataImporter.ImportAll();
        result.Log("imported");
    }
}
```

- [ ] **Step 5: 검증** — 검증 절차 3~4, FILTER `ProjectVoid\.Tests\.StarterDataTests`. Expected: `PASSED 8 / FAILED 0` (3 + 시드 5개).

- [ ] **Step 6: Commit**

```bash
git add Assets/_Project
git commit -m "feat: import starter cards, units and skirmish encounter

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 8: BattleEvent · BattleEventRecorder

**Files:**
- Create: `Assets/_Project/Scripts/View/BattleEvent.cs`, `Assets/_Project/Scripts/View/BattleEventRecorder.cs`
- Test: `Assets/_Project/Tests/EditMode/EventRecorderTests.cs` (GODOT `tests/test_event_recorder.gd`)

**Interfaces:**
- Consumes: `BattleState` 이벤트 전부
- Produces:
  - `enum BattleEventKind { TurnStarted, CardPlayed, EnemyActed, Damaged, Healed, BlockGained, Died, Log, BattleEnded, CardDrawn, DeckReshuffled, HandDiscarded, UnitMoved }`
  - `sealed class BattleEvent` — 필드 `Kind, Unit, Target, TargetTeam, TargetCell, Card, Action, Amount, Hp, Block, Text, AllyWon, RoundIndex, Order (List<Unit>), Alive (List<bool>), TurnIndex, Cards (List<CardData>), DeckCount, DiscardCount, FromCell, ToCell, Cell`
  - `BattleEventRecorder(BattleState state)`, `List<BattleEvent> TakeEvents()`

- [ ] **Step 1: 실패하는 테스트 작성** — `EventRecorderTests.cs`: GODOT `test_event_recorder.gd` 8개를 이식 규칙대로 — `StartBattleRecordsFirstTurn`, `EndTurnRecordsEnemiesInOrder`, `CardPlayRecordsActionThenDamage`, `KillRecordsDeathAndBattleEnd`, `DefendAndRestRecordSnapshots`, `CardZoneEventsSnapshot`, `AllyMoveRecordsMoveThenLog`, `EnemyMoveRecordsActionMoveLog`. 원본 시드 99. `event.kind == BattleEvent.Kind.X` → `e.Kind == BattleEventKind.X`. 테스트 클래스 상단에 `using ProjectVoid.View;`.

- [ ] **Step 2: 검증 절차 1~2** — Expected: `BattleEventRecorder` 없음 컴파일 에러.

- [ ] **Step 3: 구현**

`Scripts/View/BattleEvent.cs`:
```csharp
using System.Collections.Generic;
using ProjectVoid.Combat;
using UnityEngine;

namespace ProjectVoid.View
{
    // 새 종류는 뒤에만 추가한다.
    public enum BattleEventKind
    {
        TurnStarted, CardPlayed, EnemyActed, Damaged, Healed, BlockGained, Died, Log, BattleEnded,
        CardDrawn, DeckReshuffled, HandDiscarded, UnitMoved,
    }

    /// <summary>
    /// 규칙 이벤트 하나를 "그 순간의 값"과 함께 저장한 기록. 규칙은 한 번에 끝까지 계산하지만 화면은 천천히 재생하므로,
    /// 재생 시점에는 이미 바뀌어 있을 체력·칸·장수를 여기 복사해 둔다. 종류마다 쓰는 필드가 다르다.
    /// </summary>
    public sealed class BattleEvent
    {
        public BattleEvent(BattleEventKind kind)
        {
            Kind = kind;
        }

        public BattleEventKind Kind { get; }
        public Unit Unit;
        public Unit Target;
        public Team TargetTeam = Team.Ally;
        public Vector2Int TargetCell;
        public CardData Card;
        public EnemyAction Action = EnemyAction.Attack;
        public int Amount;
        public int Hp;
        public int Block;
        public string Text = "";
        public bool AllyWon;
        public int RoundIndex;
        public List<Unit> Order = new List<Unit>();
        public List<bool> Alive = new List<bool>();
        public int TurnIndex = -1;
        public List<CardData> Cards = new List<CardData>();
        public int DeckCount;
        public int DiscardCount;
        public Vector2Int FromCell;
        public Vector2Int ToCell;
        // 재생 때는 규칙의 칸이 이미 이동 뒤일 수 있어 기록 시점의 칸을 쓴다 (TurnStarted, Died).
        public Vector2Int Cell;
    }
}
```

`Scripts/View/BattleEventRecorder.cs`:
```csharp
using System.Collections.Generic;
using ProjectVoid.Combat;
using UnityEngine;

namespace ProjectVoid.View
{
    /// <summary>BattleState 이벤트를 구독해 스냅샷 기록으로 쌓는다. 화면은 TakeEvents 로 꺼내 재생한다.</summary>
    public sealed class BattleEventRecorder
    {
        private readonly BattleState _state;
        private List<BattleEvent> _events = new List<BattleEvent>();

        public BattleEventRecorder(BattleState state)
        {
            _state = state;
            state.TurnStarted += OnTurnStarted;
            state.CardPlayed += OnCardPlayed;
            state.EnemyActed += OnEnemyActed;
            state.UnitDamaged += OnUnitDamaged;
            state.UnitHealed += OnUnitHealed;
            state.BlockGained += OnBlockGained;
            state.UnitDied += OnUnitDied;
            state.LogMessage += OnLogMessage;
            state.BattleEnded += OnBattleEnded;
            state.CardDrawn += OnCardDrawn;
            state.DeckReshuffled += OnDeckReshuffled;
            state.HandDiscarded += OnHandDiscarded;
            state.UnitMoved += OnUnitMoved;
        }

        public List<BattleEvent> TakeEvents()
        {
            List<BattleEvent> taken = _events;
            _events = new List<BattleEvent>();
            return taken;
        }

        private void OnTurnStarted(Unit unit)
        {
            var e = new BattleEvent(BattleEventKind.TurnStarted)
            {
                Unit = unit, Hp = unit.Hp, Block = unit.Block, Cell = unit.Cell,
                RoundIndex = _state.RoundIndex, TurnIndex = _state.TurnIndex,
                Order = new List<Unit>(_state.Initiative),
                DeckCount = unit.Deck.Count, DiscardCount = unit.Discard.Count,
            };
            foreach (Unit member in _state.Initiative)
            {
                e.Alive.Add(member.IsAlive);
            }
            _events.Add(e);
        }

        private void OnCardPlayed(Unit actor, CardData card, Team targetTeam, Vector2Int targetCell)
        {
            _events.Add(new BattleEvent(BattleEventKind.CardPlayed)
            {
                Unit = actor, Card = card, TargetTeam = targetTeam, TargetCell = targetCell,
                Target = LivingUnitAt(targetTeam, targetCell),
                DeckCount = actor.Deck.Count, DiscardCount = actor.Discard.Count,
            });
        }

        private Unit LivingUnitAt(Team team, Vector2Int cell)
        {
            foreach (Unit unit in _state.Units)
            {
                if (unit.Team == team && unit.Cell == cell && unit.IsAlive)
                {
                    return unit;
                }
            }
            return null;
        }

        private void OnEnemyActed(Unit actor, EnemyAction action, Unit target)
            => _events.Add(new BattleEvent(BattleEventKind.EnemyActed) { Unit = actor, Action = action, Target = target });

        private void OnUnitDamaged(Unit unit, int amount)
            => _events.Add(new BattleEvent(BattleEventKind.Damaged) { Unit = unit, Amount = amount, Hp = unit.Hp, Block = unit.Block });

        private void OnUnitHealed(Unit unit, int amount)
            => _events.Add(new BattleEvent(BattleEventKind.Healed) { Unit = unit, Amount = amount, Hp = unit.Hp });

        private void OnBlockGained(Unit unit, int amount)
            => _events.Add(new BattleEvent(BattleEventKind.BlockGained) { Unit = unit, Amount = amount, Block = unit.Block });

        private void OnUnitDied(Unit unit)
            => _events.Add(new BattleEvent(BattleEventKind.Died) { Unit = unit, Cell = unit.Cell });

        private void OnLogMessage(string text)
            => _events.Add(new BattleEvent(BattleEventKind.Log) { Text = text });

        private void OnBattleEnded(bool allyWon)
            => _events.Add(new BattleEvent(BattleEventKind.BattleEnded) { AllyWon = allyWon });

        private void OnCardDrawn(Unit unit, CardData card, int deckCount, int discardCount)
            => _events.Add(new BattleEvent(BattleEventKind.CardDrawn) { Unit = unit, Card = card, DeckCount = deckCount, DiscardCount = discardCount });

        private void OnDeckReshuffled(Unit unit, int count)
            => _events.Add(new BattleEvent(BattleEventKind.DeckReshuffled)
            {
                Unit = unit, Amount = count, DeckCount = unit.Deck.Count, DiscardCount = unit.Discard.Count,
            });

        private void OnHandDiscarded(Unit unit, IReadOnlyList<CardData> cards, int discardCount)
            => _events.Add(new BattleEvent(BattleEventKind.HandDiscarded)
            {
                Unit = unit, Cards = new List<CardData>(cards), DiscardCount = discardCount, DeckCount = unit.Deck.Count,
            });

        private void OnUnitMoved(Unit unit, Vector2Int fromCell, Vector2Int toCell)
            => _events.Add(new BattleEvent(BattleEventKind.UnitMoved) { Unit = unit, FromCell = fromCell, ToCell = toCell });
    }
}
```

- [ ] **Step 4: 검증** — FILTER `ProjectVoid\.Tests\.EventRecorderTests`. Expected: `PASSED 8 / FAILED 0`.

- [ ] **Step 5: Commit**

```bash
git add Assets/_Project
git commit -m "feat: record battle events with snapshots for playback

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 9: BoardLayout

**Files:**
- Create: `Assets/_Project/Scripts/View/BoardLayout.cs`
- Test: `Assets/_Project/Tests/EditMode/BoardLayoutTests.cs` (GODOT `tests/test_board_layout.gd` + 추가 1)

**Interfaces:**
- Consumes: `Team`, `TargetResolver.CenterOffset`
- Produces: `BoardLayout(Vector2Int allyGrid, Vector2Int enemyGrid)`; 상수 `TileSize=1f, CellPitch=1.1f, SideGap=1.5f`; `Vector3 CellPosition(Team, Vector2Int)`, `float MinX(), MaxX(), Width(), Depth()`, `Vector3 Center()`, `static float CameraDistance(float boardWidth, float boardDepth, float verticalFovDeg, float aspect, float margin)`

좌표 변환: 유니티 카메라는 -Z 쪽에서 +Z 를 본다 (Godot 은 +Z 에서 -Z). x 는 그대로(아군 왼쪽 = -x), z 는 부호를 뒤집어 0행이 화면 안쪽(+z)이 된다.

- [ ] **Step 1: 실패하는 테스트 작성** — `Tests/EditMode/BoardLayoutTests.cs`:

```csharp
using NUnit.Framework;
using ProjectVoid.Combat;
using ProjectVoid.View;
using UnityEngine;

namespace ProjectVoid.Tests
{
    public class BoardLayoutTests
    {
        private const float Eps = 1e-4f;

        [Test]
        public void SidesAreMirrored()
        {
            var layout = new BoardLayout(new Vector2Int(3, 3), new Vector2Int(3, 3));
            Vector3 ally = layout.CellPosition(Team.Ally, new Vector2Int(0, 1));
            Vector3 enemy = layout.CellPosition(Team.Enemy, new Vector2Int(0, 1));
            Assert.Less(ally.x, 0f, "ally is on the left");
            Assert.Greater(enemy.x, 0f, "enemy is on the right");
            Assert.AreEqual(enemy.x, -ally.x, Eps, "same column mirrors");
            Assert.AreEqual(1.3f, enemy.x, Eps, "front column centre");
            Assert.AreEqual(0f, ally.y, "tile top is the floor");
        }

        [Test]
        public void ColZeroIsNearestTheGap()
        {
            var layout = new BoardLayout(new Vector2Int(3, 3), new Vector2Int(3, 3));
            Vector3 front = layout.CellPosition(Team.Ally, new Vector2Int(0, 1));
            Vector3 back = layout.CellPosition(Team.Ally, new Vector2Int(2, 1));
            Assert.Less(Mathf.Abs(front.x), Mathf.Abs(back.x), "col 0 is closer to the centre than col 2");
            Assert.AreEqual(2.2f, Mathf.Abs(back.x) - Mathf.Abs(front.x), Eps, "columns are one pitch apart");
        }

        // Godot 은 far.z < near.z 였다. 유니티는 카메라가 -Z 쪽이라 부호가 반대다.
        [Test]
        public void RowsGoIntoTheScreen()
        {
            var layout = new BoardLayout(new Vector2Int(3, 3), new Vector2Int(3, 3));
            Vector3 far = layout.CellPosition(Team.Ally, new Vector2Int(0, 0));
            Vector3 near = layout.CellPosition(Team.Ally, new Vector2Int(0, 2));
            Assert.Greater(far.z, near.z, "row 0 is farther from the camera");
            Assert.AreEqual(0f, layout.CellPosition(Team.Ally, new Vector2Int(0, 1)).z, Eps, "middle row is centred");
        }

        [Test]
        public void OddEvenRowsSitHalfACellApart()
        {
            var layout = new BoardLayout(new Vector2Int(3, 3), new Vector2Int(2, 2));
            Assert.AreEqual(0.55f, layout.CellPosition(Team.Enemy, new Vector2Int(0, 0)).z, Eps, "2-row side row 0");
            Assert.AreEqual(-0.55f, layout.CellPosition(Team.Enemy, new Vector2Int(0, 1)).z, Eps, "2-row side row 1");
        }

        [Test]
        public void Bounds()
        {
            var layout = new BoardLayout(new Vector2Int(3, 3), new Vector2Int(2, 2));
            Assert.AreEqual(-4.05f, layout.MinX(), Eps, "left edge");
            Assert.AreEqual(2.95f, layout.MaxX(), Eps, "right edge");
            Assert.AreEqual(7.0f, layout.Width(), Eps, "width");
            Assert.AreEqual(3.3f, layout.Depth(), Eps, "depth uses the taller side");
            Assert.AreEqual(-0.55f, layout.Center().x, Eps, "centre x");
        }

        [Test]
        public void CameraDistance()
        {
            Assert.AreEqual(1f, BoardLayout.CameraDistance(2f, 0f, 90f, 1f, 1f), Eps, "fits width exactly");
            float narrow = BoardLayout.CameraDistance(4f, 2f, 40f, 16f / 9f, 1.2f);
            float wide = BoardLayout.CameraDistance(8f, 2f, 40f, 16f / 9f, 1.2f);
            Assert.Greater(wide, narrow, "wider board needs a farther camera");
            float deep = BoardLayout.CameraDistance(1f, 6f, 40f, 16f / 9f, 1.2f);
            Assert.Greater(deep, BoardLayout.CameraDistance(1f, 0f, 40f, 16f / 9f, 1.2f), "a deep narrow board is framed by depth");
        }

        [Test]
        public void CameraDistanceGrowsForNarrowAspect()
        {
            float widescreen = BoardLayout.CameraDistance(7f, 3.3f, 40f, 16f / 9f, 1.8f);
            float portrait = BoardLayout.CameraDistance(7f, 3.3f, 40f, 9f / 16f, 1.8f);
            Assert.Greater(portrait, widescreen, "a narrow window pulls the camera back to keep the board in view");
        }
    }
}
```

- [ ] **Step 2: 검증 절차 1~2** — Expected: `BoardLayout` 없음 컴파일 에러.

- [ ] **Step 3: 구현** — `Scripts/View/BoardLayout.cs`:

```csharp
using ProjectVoid.Combat;
using UnityEngine;

namespace ProjectVoid.View
{
    /// <summary>
    /// 격자 칸 → 월드 좌표. 아군은 왼쪽(-x), 적은 오른쪽(+x), 양쪽 모두 col 0 이 가운데 틈 쪽.
    /// 행은 격자 가운데를 z=0 에 맞추고, 0행이 화면 안쪽(+z)이다 (카메라가 -Z 쪽에서 본다). 미러링은 여기서만 한다.
    /// </summary>
    public sealed class BoardLayout
    {
        public const float TileSize = 1f;
        public const float CellPitch = 1.1f;
        public const float SideGap = 1.5f;

        public BoardLayout(Vector2Int allyGrid, Vector2Int enemyGrid)
        {
            AllyGrid = allyGrid;
            EnemyGrid = enemyGrid;
        }

        public Vector2Int AllyGrid { get; }
        public Vector2Int EnemyGrid { get; }

        /// <summary>칸 윗면 중앙 (y = 0 이 바닥).</summary>
        public Vector3 CellPosition(Team team, Vector2Int cell)
        {
            float side = team == Team.Ally ? -1f : 1f;
            int rows = team == Team.Ally ? AllyGrid.y : EnemyGrid.y;
            float x = side * (SideGap / 2f + CellPitch / 2f + cell.x * CellPitch);
            float z = -TargetResolver.CenterOffset(cell.y, rows) * CellPitch;
            return new Vector3(x, 0f, z);
        }

        public float MinX() => -(SideGap / 2f + AllyGrid.x * CellPitch);

        public float MaxX() => SideGap / 2f + EnemyGrid.x * CellPitch;

        public float Width() => MaxX() - MinX();

        public float Depth() => Mathf.Max(AllyGrid.y, EnemyGrid.y) * CellPitch;

        public Vector3 Center() => new Vector3((MinX() + MaxX()) / 2f, 0f, 0f);

        /// <summary>보드 폭과 깊이가 margin 배 여유를 두고 화면에 들어오는 카메라 거리 (둘 중 먼 쪽).</summary>
        public static float CameraDistance(float boardWidth, float boardDepth, float verticalFovDeg, float aspect, float margin)
        {
            float halfVertical = verticalFovDeg * Mathf.Deg2Rad / 2f;
            float halfHorizontal = Mathf.Atan(Mathf.Tan(halfVertical) * aspect);
            float forWidth = boardWidth * margin / 2f / Mathf.Tan(halfHorizontal);
            float forDepth = boardDepth * margin / 2f / Mathf.Tan(halfVertical);
            return Mathf.Max(forWidth, forDepth);
        }
    }
}
```

- [ ] **Step 4: 검증** — FILTER `ProjectVoid\.Tests\.BoardLayoutTests`. Expected: `PASSED 7 / FAILED 0`.

- [ ] **Step 5: Commit**

```bash
git add Assets/_Project
git commit -m "feat: port board layout math to Unity coordinates

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 10: PixelSpriteProcessor (AI 이미지 → 64×64 픽셀아트)

**Files:**
- Create: `Assets/_Project/Scripts/Editor/PixelSpriteProcessor.cs`
- Test: `Assets/_Project/Tests/EditMode/PixelSpriteProcessorTests.cs`

**Interfaces:**
- Produces: `PixelSpriteProcessor.Size = 64`, `AlphaThreshold = 128`, `RawDir = "Assets/_Project/Art/Units/Raw"`, `OutDir = "Assets/_Project/Art/Units"`; `static Color32[] Pixelize(Color32[] source, int width, int height, int size, byte alphaThreshold)`; `static void KeyOut(Color32[] pixels, Color32 key, int tolerance)`; `static void ProcessFile(string rawAssetPath, string outAssetPath, bool keyOutGreen)`; `static void ApplySpriteSettings(string assetPath)`; 메뉴 `Project Void/Pixelize Raw Unit Sprites` → `ProcessAllRaw()`

- [ ] **Step 1: 실패하는 테스트 작성** — `Tests/EditMode/PixelSpriteProcessorTests.cs`:

```csharp
using NUnit.Framework;
using ProjectVoid.EditorTools;
using UnityEngine;

namespace ProjectVoid.Tests
{
    public class PixelSpriteProcessorTests
    {
        private static Color32[] Canvas(int w, int h) => new Color32[w * h];

        private static void Fill(Color32[] px, int w, int x0, int y0, int x1, int y1, Color32 c)
        {
            for (int y = y0; y <= y1; y++)
            {
                for (int x = x0; x <= x1; x++)
                {
                    px[y * w + x] = c;
                }
            }
        }

        [Test]
        public void CropsScalesAndAnchorsBottomCenter()
        {
            // 100x200 캔버스에 20x100 불투명 막대 → 긴 변 100 이 64 로. 폭은 round(20 * 64/100) = 13.
            Color32[] src = Canvas(100, 200);
            Fill(src, 100, 40, 50, 59, 149, new Color32(200, 30, 30, 255));
            Color32[] outPx = PixelSpriteProcessor.Pixelize(src, 100, 200, 64, 128);
            Assert.AreEqual(64 * 64, outPx.Length);
            int offset = (64 - 13) / 2;
            Assert.AreEqual(255, outPx[0 * 64 + offset].a, "bottom-left of the sprite sits on row 0");
            Assert.AreEqual(0, outPx[0 * 64 + offset - 1].a, "left of the sprite is clear");
            Assert.AreEqual(255, outPx[0 * 64 + offset + 12].a, "13 pixels wide");
            Assert.AreEqual(0, outPx[0 * 64 + offset + 13].a, "no wider than 13");
            Assert.AreEqual(255, outPx[63 * 64 + offset + 6].a, "fills the full height");
        }

        [Test]
        public void BinarizesAlpha()
        {
            Color32[] src = Canvas(64, 64);
            Fill(src, 64, 0, 0, 63, 63, new Color32(10, 10, 10, 200));
            src[10 * 64 + 10] = new Color32(10, 10, 10, 100);
            Color32[] outPx = PixelSpriteProcessor.Pixelize(src, 64, 64, 64, 128);
            Assert.AreEqual(255, outPx[5 * 64 + 5].a, "opaque enough becomes fully opaque");
            Assert.AreEqual(0, outPx[10 * 64 + 10].a, "semi-transparent becomes clear");
        }

        [Test]
        public void EmptySourceGivesClearCanvas()
        {
            Color32[] outPx = PixelSpriteProcessor.Pixelize(Canvas(32, 32), 32, 32, 64, 128);
            foreach (Color32 c in outPx)
            {
                Assert.AreEqual(0, c.a);
            }
        }

        [Test]
        public void KeyOutRemovesColorsNearTheKey()
        {
            var px = new[] { new Color32(0, 255, 0, 255), new Color32(10, 240, 12, 255), new Color32(200, 30, 30, 255) };
            PixelSpriteProcessor.KeyOut(px, new Color32(0, 255, 0, 255), 60);
            Assert.AreEqual(0, px[0].a, "exact key removed");
            Assert.AreEqual(0, px[1].a, "near key removed");
            Assert.AreEqual(255, px[2].a, "other colors kept");
        }
    }
}
```

- [ ] **Step 2: 검증 절차 1~2** — Expected: `PixelSpriteProcessor` 없음 컴파일 에러.

- [ ] **Step 3: 구현** — `Scripts/Editor/PixelSpriteProcessor.cs`:

```csharp
using System.IO;
using UnityEditor;
using UnityEngine;

namespace ProjectVoid.EditorTools
{
    /// <summary>AI 가 만든 큰 이미지를 레퍼런스(format3.png)와 같은 64×64 픽셀아트 스프라이트로 정리한다.</summary>
    public static class PixelSpriteProcessor
    {
        public const int Size = 64;
        public const byte AlphaThreshold = 128;
        public const string RawDir = "Assets/_Project/Art/Units/Raw";
        public const string OutDir = "Assets/_Project/Art/Units";

        /// <summary>
        /// 불투명 영역만 잘라 긴 변이 size 가 되게 최근접 축소하고, 발밑 중앙에 맞춰 size×size 캔버스에 놓는다.
        /// 알파는 0/255 로 이진화한다. 픽셀 순서는 Texture2D.GetPixels32 와 같다 (0 행이 아래).
        /// </summary>
        public static Color32[] Pixelize(Color32[] source, int width, int height, int size, byte alphaThreshold)
        {
            var output = new Color32[size * size];
            int minX = width, minY = height, maxX = -1, maxY = -1;
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    if (source[y * width + x].a < alphaThreshold)
                    {
                        continue;
                    }
                    minX = Mathf.Min(minX, x);
                    minY = Mathf.Min(minY, y);
                    maxX = Mathf.Max(maxX, x);
                    maxY = Mathf.Max(maxY, y);
                }
            }
            if (maxX < 0)
            {
                return output;
            }

            int boxWidth = maxX - minX + 1;
            int boxHeight = maxY - minY + 1;
            float scale = Mathf.Max(boxWidth, boxHeight) / (float)size;
            int outWidth = Mathf.Clamp(Mathf.RoundToInt(boxWidth / scale), 1, size);
            int outHeight = Mathf.Clamp(Mathf.RoundToInt(boxHeight / scale), 1, size);
            int offsetX = (size - outWidth) / 2;

            for (int oy = 0; oy < outHeight; oy++)
            {
                for (int ox = 0; ox < outWidth; ox++)
                {
                    int sx = Mathf.Min(minX + Mathf.FloorToInt((ox + 0.5f) * scale), maxX);
                    int sy = Mathf.Min(minY + Mathf.FloorToInt((oy + 0.5f) * scale), maxY);
                    Color32 c = source[sy * width + sx];
                    output[oy * size + offsetX + ox] = c.a >= alphaThreshold ? new Color32(c.r, c.g, c.b, 255) : default;
                }
            }
            return output;
        }

        /// <summary>key 색과의 RGB 차이 합이 tolerance 이하인 픽셀을 투명하게 (초록 배경 대안용).</summary>
        public static void KeyOut(Color32[] pixels, Color32 key, int tolerance)
        {
            for (int i = 0; i < pixels.Length; i++)
            {
                Color32 c = pixels[i];
                int diff = Mathf.Abs(c.r - key.r) + Mathf.Abs(c.g - key.g) + Mathf.Abs(c.b - key.b);
                if (diff <= tolerance)
                {
                    pixels[i] = default;
                }
            }
        }

        public static void ProcessFile(string rawAssetPath, string outAssetPath, bool keyOutGreen)
        {
            // 임포트 설정(Read/Write)에 기대지 않도록 PNG 를 직접 읽는다.
            var source = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            source.LoadImage(File.ReadAllBytes(rawAssetPath));
            Color32[] pixels = source.GetPixels32();
            if (keyOutGreen)
            {
                KeyOut(pixels, new Color32(0, 255, 0, 255), 120);
            }
            Color32[] result = Pixelize(pixels, source.width, source.height, Size, AlphaThreshold);
            Object.DestroyImmediate(source);

            var texture = new Texture2D(Size, Size, TextureFormat.RGBA32, false);
            texture.SetPixels32(result);
            texture.Apply();
            File.WriteAllBytes(outAssetPath, texture.EncodeToPNG());
            Object.DestroyImmediate(texture);

            AssetDatabase.ImportAsset(outAssetPath);
            ApplySpriteSettings(outAssetPath);
        }

        public static void ApplySpriteSettings(string assetPath)
        {
            var importer = (TextureImporter)AssetImporter.GetAtPath(assetPath);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.filterMode = FilterMode.Point;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.spritePixelsPerUnit = Size;
            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            settings.spriteAlignment = (int)SpriteAlignment.BottomCenter;
            importer.SetTextureSettings(settings);
            importer.SaveAndReimport();
        }

        [MenuItem("Project Void/Pixelize Raw Unit Sprites")]
        public static void ProcessAllRaw()
        {
            foreach (string path in Directory.GetFiles(RawDir, "*.png"))
            {
                string rawPath = path.Replace('\\', '/');
                ProcessFile(rawPath, $"{OutDir}/{Path.GetFileName(rawPath)}", false);
            }
        }
    }
}
```

- [ ] **Step 4: 검증** — FILTER `ProjectVoid\.Tests\.PixelSpriteProcessorTests`. Expected: `PASSED 4 / FAILED 0`.

- [ ] **Step 5: Commit**

```bash
git add Assets/_Project
git commit -m "feat: add pixel sprite processor for AI-generated units

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 11: 유닛 스프라이트 6종 생성 (Unity AI)

사용자는 이 대화에서 AI 생성 진행을 이미 승인했다 (생성 도구의 1회 동의 조건 충족). 생성은 크레딧을 쓰므로 재생성은 사용자가 요청한 유닛만.

**Files:**
- Create: `Assets/_Project/Art/Reference/format3.png`, `Assets/_Project/Art/Reference/format3_forComfyui.png`, `Assets/_Project/Art/Units/placeholder_unit.png`
- Create (생성물): `Assets/_Project/Art/Units/Raw/{vanguard,archer,scout,brute,sentry,stalker}.png`, `Assets/_Project/Art/Units/{...}.png`
- Modify: `Assets/_Project/Data/Units/*.asset` (`sprite` 지정)

**Interfaces:**
- Consumes: Task 7 유닛 에셋, Task 10 `PixelSpriteProcessor.ProcessFile/ApplySpriteSettings`
- Produces: 각 `UnitData.sprite` 가 64×64 Point 필터 스프라이트 (피벗 발밑 중앙, PPU 64). `Assets/_Project/Art/Units/placeholder_unit.png` (Task 13 `ViewAssets` 가 참조)

- [ ] **Step 1: 레퍼런스와 임시 실루엣 복사**

```bash
cd "/c/Users/User/Desktop/Unity/project void test"
mkdir -p Assets/_Project/Art/Reference Assets/_Project/Art/Units/Raw
cp "/c/Users/User/Desktop/그림/Aseprite/format3.png" Assets/_Project/Art/Reference/
cp "/c/Users/User/Desktop/그림/Aseprite/format3_forComfyui.png" Assets/_Project/Art/Reference/
cp "/c/Users/User/Desktop/Godot/Project-Void/Resources/sprites/placeholder_unit.png" Assets/_Project/Art/Units/
```
그 뒤 `Unity_RunCommand`:
```csharp
using UnityEditor;
internal class CommandScript : IRunCommand
{
    public void Execute(ExecutionResult result)
    {
        AssetDatabase.Refresh();
        ProjectVoid.EditorTools.PixelSpriteProcessor.ApplySpriteSettings("Assets/_Project/Art/Reference/format3.png");
        ProjectVoid.EditorTools.PixelSpriteProcessor.ApplySpriteSettings("Assets/_Project/Art/Units/placeholder_unit.png");
        var reference = AssetDatabase.LoadAssetAtPath<UnityEngine.Texture2D>("Assets/_Project/Art/Reference/format3.png");
        result.Log("reference texture id: {0}", reference.GetInstanceID());
    }
}
```
로그의 instance id 를 기록한다 (이하 `REF_ID`).

- [ ] **Step 2: 6종 병렬 생성** — 한 메시지에서 `mcp__unity-mcp__Unity_AssetGeneration_GenerateAsset` 6회 동시 호출. 공통 인자: `command: "GenerateSprite"`, `modelId: "gpt-image-1-5"`, `referenceImageInstanceId: REF_ID`, `waitForCompletion: true`, `savePath: "Assets/_Project/Art/Units/Raw/{id}.png"`. `prompt` = 공통 문장 + 유닛 문장:
  - 공통: `Pixel art game character sprite in the exact style of the reference image: same chibi proportions (big head, about 2.5 heads tall), 1-pixel black outline, pale skin with soft red rim shading, limited palette, 3/4 view facing right, full body standing idle, centered, on a plain flat white background. `
  - vanguard: `A heavily armored frontline warrior holding a round shield and a one-handed sword.`
  - archer: `A light-armored archer holding a longbow, quiver on the back.`
  - scout: `An agile hooded scout holding twin daggers.`
  - brute: `A hulking thug with a wooden club, bulky arms, scowling.`
  - sentry: `A helmeted guard holding a crossbow, sturdy stance.`
  - stalker: `A masked assassin with throwing knives, lean silhouette.`

- [ ] **Step 3: 배경 제거** — 6개 각각 `GenerateAsset` `command: "RemoveSpriteBackground"`, `targetAssetPath: "Assets/_Project/Art/Units/Raw/{id}.png"`, `modelId: "photoroom-bg-removal"`, `waitForCompletion: true` (병렬). 결과의 `AssetPath` 가 원본과 다르면 그 파일을 `Raw/{id}.png` 로 덮어쓴다 (원본은 `Raw/{id}_orig.png` 로 이름 변경해 보존).

- [ ] **Step 4: 원본 확인** — `Raw/*.png` 6장을 Read 로 열어 본다. 배경이 남았거나 캐릭터가 잘렸으면 해당 유닛만 Step 2 를 `referenceImageInstanceId` 를 `format3_forComfyui.png` 의 id 로, 배경 문구를 `on a plain flat pure green (#00FF00) background` 로 바꿔 재생성하고, Step 5 에서 그 유닛만 `ProcessFile(raw, out, true)` 로 처리한다.

- [ ] **Step 5: 64×64 로 정리** — `Unity_RunCommand`:
```csharp
internal class CommandScript : IRunCommand
{
    public void Execute(ExecutionResult result)
    {
        ProjectVoid.EditorTools.PixelSpriteProcessor.ProcessAllRaw();
        result.Log("pixelized");
    }
}
```
(`_orig` 파일이 있으면 `ProcessAllRaw` 가 같이 처리하므로, 처리 후 `Art/Units/*_orig.png` 결과물은 지운다.)

- [ ] **Step 6: 레퍼런스와 비교 시트 만들기**

```bash
cd "/c/Users/User/Desktop/Unity/project void test"
python -c "
from PIL import Image
names=['format3','vanguard','archer','scout','brute','sentry','stalker']
paths=['Assets/_Project/Art/Reference/format3.png']+['Assets/_Project/Art/Units/%s.png'%n for n in names[1:]]
imgs=[Image.open(p).convert('RGBA').resize((64,64),Image.NEAREST) for p in paths]
sheet=Image.new('RGBA',(64*len(imgs)*6,64*6),(60,60,70,255))
for i,im in enumerate(imgs): sheet.alpha_composite(im.resize((384,384),Image.NEAREST),(i*384,0))
sheet.save(r'C:\Users\User\AppData\Local\Temp\claude\C--Users-User-Desktop-Unity-project-void-test\a46c1d9a-1120-4467-a4f8-9c180e23ffa0\scratchpad\unit_sheet.png')
"
```
시트를 Read 로 확인하고 사용자에게 보여준다 (파일 경로 안내). **사용자가 괜찮다고 할 때까지 다음 단계로 가지 않는다.** 불만족 유닛은 Step 2~5 반복.

- [ ] **Step 7: UnitData 에 지정** — `Unity_RunCommand`:
```csharp
using UnityEditor;
using UnityEngine;
internal class CommandScript : IRunCommand
{
    public void Execute(ExecutionResult result)
    {
        foreach (string id in new[] { "vanguard", "archer", "scout", "brute", "sentry", "stalker" })
        {
            var data = AssetDatabase.LoadAssetAtPath<ProjectVoid.Combat.UnitData>($"Assets/_Project/Data/Units/{id}.asset");
            data.sprite = AssetDatabase.LoadAssetAtPath<Sprite>($"Assets/_Project/Art/Units/{id}.png");
            EditorUtility.SetDirty(data);
            result.Log("{0} -> {1}", id, data.sprite);
        }
        AssetDatabase.SaveAssets();
    }
}
```
Expected: 6줄 모두 sprite 가 null 이 아님.

- [ ] **Step 8: 회귀 확인** — 검증 절차 3~4, FILTER `""`. Expected: 전부 통과 (DataImporter 재실행 없이 sprite 만 바뀜).

- [ ] **Step 9: Commit**

```bash
git add Assets/_Project
git commit -m "art: generate unit sprites with Unity AI from the reference style

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 12: 한글 TMP 폰트 · 타일 머티리얼

**Files:**
- Create: `Assets/_Project/Fonts/NotoSansKR.ttf`
- Create: `Assets/_Project/Scripts/Editor/FontSetup.cs`
- Create (생성물): `Assets/_Project/Fonts/NotoSansKR SDF.asset`, `Assets/_Project/Fonts/NotoSansKR Overlay.mat`, `Assets/_Project/Materials/Tile.mat`, `Assets/TextMesh Pro/` (TMP Essential Resources)
- Test: `Assets/_Project/Tests/EditMode/FontSetupTests.cs`

**Interfaces:**
- Produces: `FontSetup.FontAssetPath = "Assets/_Project/Fonts/NotoSansKR SDF.asset"`, `FontSetup.OverlayMaterialPath = "Assets/_Project/Fonts/NotoSansKR Overlay.mat"`, `FontSetup.TileMaterialPath = "Assets/_Project/Materials/Tile.mat"`, 메뉴 `Project Void/Setup Fonts And Materials` → `FontSetup.Run()`

- [ ] **Step 1: 폰트 받기**

```bash
cd "/c/Users/User/Desktop/Unity/project void test"
mkdir -p Assets/_Project/Fonts Assets/_Project/Materials
curl -L -o Assets/_Project/Fonts/NotoSansKR.ttf "https://github.com/google/fonts/raw/main/ofl/notosanskr/NotoSansKR%5Bwght%5D.ttf"
ls -la Assets/_Project/Fonts/NotoSansKR.ttf
```
Expected: 1MB 이상. 실패하면 `cp /c/Windows/Fonts/malgun.ttf Assets/_Project/Fonts/NotoSansKR.ttf` 로 대체하고 사용자에게 "맑은 고딕으로 대체(로컬 전용, 배포 불가)"라고 알린다.

- [ ] **Step 2: TMP Essential Resources 경로 확인**

```bash
ls "/c/Users/User/Desktop/Unity/project void test/Library/PackageCache/" | grep ugui
ls "/c/Users/User/Desktop/Unity/project void test/Library/PackageCache/"com.unity.ugui*/"Package Resources"
```
Expected: `TMP Essential Resources.unitypackage` 가 보인다. (아래 코드의 경로는 `Packages/com.unity.ugui/Package Resources/...` 가상 경로를 쓴다.)

- [ ] **Step 3: 실패하는 테스트 작성** — `Tests/EditMode/FontSetupTests.cs`:

```csharp
using System.Collections.Generic;
using NUnit.Framework;
using ProjectVoid.EditorTools;
using TMPro;
using UnityEditor;
using UnityEngine;

namespace ProjectVoid.Tests
{
    public class FontSetupTests
    {
        [Test]
        public void KoreanFontCoversGameText()
        {
            var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontSetup.FontAssetPath);
            Assert.IsNotNull(font, "font asset exists");
            bool ok = font.HasCharacters("선봉사수정찰병괴한보초추적자베기횡베기꿰뚫기사격관통일제폭발탄차례종료승리패배이동방어휴식공격쓰러짐빈칸거리막힘", out List<char> missing, false, true);
            Assert.IsTrue(ok, "missing: " + (missing == null ? "" : new string(missing.ToArray())));
        }

        // HUD·힌트가 쓰는 기호. 빠진 기호가 있으면 이 테스트를 고치지 말고, 그 기호를 쓰는 코드(BattleHud.TurnBarText 의 ▶ →,
        // RefreshSp 의 ● ○, BattleRoot.HintText 의 ✓, 로그의 ― —)에서 ASCII(> - * O)로 바꾸고 이 목록에서도 뺀다.
        [Test]
        public void SymbolsAreCovered()
        {
            var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontSetup.FontAssetPath);
            bool ok = font.HasCharacters("▶→●○✓―—·", out List<char> missing, false, true);
            Assert.IsTrue(ok, "missing: " + (missing == null ? "" : new string(missing.ToArray())));
        }

        [Test]
        public void OverlayMaterialUsesOverlayShader()
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(FontSetup.OverlayMaterialPath);
            Assert.IsNotNull(material);
            Assert.AreEqual("TextMeshPro/Distance Field Overlay", material.shader.name);
        }

        [Test]
        public void TileMaterialHasEmission()
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(FontSetup.TileMaterialPath);
            Assert.IsNotNull(material);
            Assert.AreEqual("Universal Render Pipeline/Lit", material.shader.name);
            Assert.IsTrue(material.IsKeywordEnabled("_EMISSION"));
        }
    }
}
```

- [ ] **Step 4: 구현** — `Scripts/Editor/FontSetup.cs`:

```csharp
using System.IO;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;

namespace ProjectVoid.EditorTools
{
    /// <summary>한글 TMP 폰트 에셋(동적 아틀라스), 3D 라벨용 오버레이 머티리얼, 타일 머티리얼을 만든다.</summary>
    public static class FontSetup
    {
        public const string FontPath = "Assets/_Project/Fonts/NotoSansKR.ttf";
        public const string FontAssetPath = "Assets/_Project/Fonts/NotoSansKR SDF.asset";
        public const string OverlayMaterialPath = "Assets/_Project/Fonts/NotoSansKR Overlay.mat";
        public const string TileMaterialPath = "Assets/_Project/Materials/Tile.mat";
        private const string EssentialsPackage = "Packages/com.unity.ugui/Package Resources/TMP Essential Resources.unitypackage";

        [MenuItem("Project Void/Setup Fonts And Materials")]
        public static void Run()
        {
            if (!Directory.Exists("Assets/TextMesh Pro"))
            {
                AssetDatabase.ImportPackage(EssentialsPackage, false);
            }

            var fontAsset = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontAssetPath);
            if (fontAsset == null)
            {
                var font = AssetDatabase.LoadAssetAtPath<Font>(FontPath);
                fontAsset = TMP_FontAsset.CreateFontAsset(font, 90, 9, GlyphRenderMode.SDFAA, 2048, 2048, AtlasPopulationMode.Dynamic, true);
                fontAsset.name = "NotoSansKR SDF";
                AssetDatabase.CreateAsset(fontAsset, FontAssetPath);
                // 아틀라스와 머티리얼을 하위 에셋으로 넣어야 저장 후에도 참조가 살아 있다.
                fontAsset.atlasTextures[0].name = "NotoSansKR Atlas";
                AssetDatabase.AddObjectToAsset(fontAsset.atlasTextures[0], fontAsset);
                fontAsset.material.name = "NotoSansKR Material";
                AssetDatabase.AddObjectToAsset(fontAsset.material, fontAsset);
            }

            if (AssetDatabase.LoadAssetAtPath<Material>(OverlayMaterialPath) == null)
            {
                // 머리 위 글자·HP 바가 유닛·타일에 가려지지 않도록 깊이 검사를 끈 셰이더 (Godot no_depth_test 대응).
                var overlay = new Material(fontAsset.material) { shader = Shader.Find("TextMeshPro/Distance Field Overlay") };
                // Godot 라벨 외곽선(outline_size 10) 대응.
                overlay.SetFloat(ShaderUtilities.ID_OutlineWidth, 0.2f);
                overlay.SetColor(ShaderUtilities.ID_OutlineColor, Color.black);
                AssetDatabase.CreateAsset(overlay, OverlayMaterialPath);
            }

            if (AssetDatabase.LoadAssetAtPath<Material>(TileMaterialPath) == null)
            {
                var tile = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                // 런타임에 발광 색만 바꾸므로 키워드를 에셋에 켜 둬야 셰이더 변형이 빌드에 포함된다.
                tile.EnableKeyword("_EMISSION");
                tile.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
                tile.SetColor("_EmissionColor", Color.black);
                AssetDatabase.CreateAsset(tile, TileMaterialPath);
            }

            AssetDatabase.SaveAssets();
        }
    }
}
```

- [ ] **Step 5: 실행** — 검증 절차 1~2 로 컴파일 후 `Unity_RunCommand` 로 `ProjectVoid.EditorTools.FontSetup.Run();`. TMP 패키지 임포트로 리로드가 일어나므로 5~10초 뒤 콘솔 에러를 확인하고, `Assets/TextMesh Pro` 가 생긴 뒤 폰트 에셋이 없으면 `Run()` 을 한 번 더 부른다.

- [ ] **Step 6: 검증** — FILTER `ProjectVoid\.Tests\.FontSetupTests`. Expected: `PASSED 4 / FAILED 0`.

- [ ] **Step 7: Commit**

```bash
git add Assets/_Project "Assets/TextMesh Pro" "Assets/TextMesh Pro.meta"
git commit -m "feat: add Korean TMP font, overlay and tile materials

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 13: UnitView · Board3D (2.5D 보드)

**Files:**
- Create: `Assets/_Project/Scripts/View/ViewAssets.cs`, `Coroutines.cs`, `CellTag.cs`, `Billboard.cs`, `UnitView.cs`, `Board3D.cs`
- Test: `Assets/_Project/Tests/EditMode/BoardViewTests.cs`, `Assets/_Project/Tests/EditMode/TestAssets.cs`

**Interfaces:**
- Consumes: `BattleState`, `BoardLayout`, `FontSetup` 경로 상수, `Assets/_Project/Art/Units/placeholder_unit.png`
- Produces:
  - `[Serializable] ViewAssets { TMP_FontAsset font; Material overlayTextMaterial; Material tileMaterial; Sprite placeholderSprite; Sprite White; Sprite Shadow; }`
  - `Coroutines.Tween(float duration, Action<float> step)`, `Coroutines.Drain(IEnumerator routine)`
  - `CellTag : MonoBehaviour { Team team; Vector2Int cell; }`, `Billboard : MonoBehaviour { bool yAxisOnly; }`
  - `UnitView : MonoBehaviour` — `Setup(Unit, Sprite, ViewAssets)`, `Unit Unit`, `Vector3 HomePosition`, `CellTag PickTag`, `string StatText`, `float HpFillWidth`, `SetStats(int hp, int maxHp, int block)`, `SetHp(int, int)`, `SetBlock(int)`, `SetHome(Vector3)`, `IEnumerator SlideTo(Vector3)`, `ResetPose()`, `SetAlive(bool)`, `IEnumerator LungeToward(Vector3)`, `IEnumerator Hop()`, `IEnumerator FlashAndShake()`, `PopText(string, Color)`, `IEnumerator FadeOut()`, 상수 `FlashTime = 0.24f`
  - `Board3D : MonoBehaviour` — `enum TileState { Base, Empty, Current, Valid, Invalid, Movable, ShapeHit, ShapeOut }`; 이벤트 `CellClicked(Team, Vector2Int)`, `PickMissed()`, `CellHovered(Team, Vector2Int)`, `HoverCleared()`; `BoardLayout Layout`, `bool InputEnabled`, `Func<bool> UiDragActive`; `Build(BattleState, ViewAssets)`, `SyncFromState(BattleState)`, `ShowCurrent(Team, Vector2Int)`, `MarkEmpty(Team, Vector2Int)`, `ShowTargetHints(Team, Dictionary<Vector2Int, CellHint>)`, `ClearTargetHints()`, `ShowShapePreview(Team, Dictionary<Vector2Int, CellHint>)`, `ShowMoveHints(Team, List<Vector2Int>)`, `IEnumerator MoveView(Unit, Vector2Int from, Vector2Int to, bool animate)`, `UnitView ViewFor(Unit)`, `int TileCount`, `TileState GetTileState(Team, Vector2Int)`, `bool TryGetHover(out Team, out Vector2Int)`, `TextMeshPro HintLabel(Team, Vector2Int)`, `RequestPick(Vector2 screenPos)`, `UpdatePointer(Vector2 screenPos)`, `bool PickAt(Ray, out Team, out Vector2Int)`
  - `readonly struct CellHint { bool Valid; string Text; }`

Godot 과 다른 점 (스펙 §6 반영): 피격 번쩍임은 흰색(HDR 2.0) 대신 붉은색 `(1, 0.45, 0.45)` — `SpriteRenderer.color` 는 1 을 넘길 수 없다. 유닛 색 틴트 없음, 진영은 발밑 그림자 색(아군 파랑·적 빨강). 스프라이트 피벗이 발밑이라 스프라이트 기준 위치는 (0,0,0).

- [ ] **Step 1: 테스트 에셋 헬퍼와 실패하는 테스트 작성**

`Tests/EditMode/TestAssets.cs`:
```csharp
using ProjectVoid.EditorTools;
using ProjectVoid.View;
using TMPro;
using UnityEditor;
using UnityEngine;

namespace ProjectVoid.Tests
{
    public static class TestAssets
    {
        public static ViewAssets Load() => new ViewAssets
        {
            font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontSetup.FontAssetPath),
            overlayTextMaterial = AssetDatabase.LoadAssetAtPath<Material>(FontSetup.OverlayMaterialPath),
            tileMaterial = AssetDatabase.LoadAssetAtPath<Material>(FontSetup.TileMaterialPath),
            placeholderSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/_Project/Art/Units/placeholder_unit.png"),
        };
    }
}
```

`Tests/EditMode/BoardViewTests.cs`:
```csharp
using NUnit.Framework;
using ProjectVoid.Combat;
using ProjectVoid.View;
using UnityEngine;

namespace ProjectVoid.Tests
{
    public class BoardViewTests
    {
        private GameObject _root;

        [TearDown]
        public void TearDown()
        {
            if (_root != null)
            {
                Object.DestroyImmediate(_root);
            }
            Make.Cleanup();
        }

        private (BattleState state, Board3D board) BuildBoard()
        {
            var encounter = Make.Encounter(new Vector2Int(3, 3), new Vector2Int(2, 2),
                new[] { Make.Place(Make.Ally("a", maxHp: 20, speed: 5, deck: new[] { Make.Card("c", damage: 1), Make.Card("c2", damage: 1), Make.Card("c3", damage: 1), Make.Card("c4", damage: 1) }), 0, 1) },
                new[] { Make.Place(Make.Enemy("e", maxHp: 20, speed: 1), 0, 0), Make.Place(Make.Enemy("f", maxHp: 20, speed: 1), 1, 1) });
            BattleState state = Make.State(encounter, 3);
            _root = new GameObject("BoardTestRoot");
            var board = _root.AddComponent<Board3D>();
            board.Build(state, TestAssets.Load());
            return (state, board);
        }

        [Test]
        public void BuildCreatesTilesForBothSides()
        {
            (_, Board3D board) = BuildBoard();
            Assert.AreEqual(13, board.TileCount, "3x3 + 2x2 tiles");
        }

        [Test]
        public void SyncMarksCurrentOccupiedAndEmpty()
        {
            (BattleState state, Board3D board) = BuildBoard();
            state.StartBattle();
            board.SyncFromState(state);
            Assert.AreEqual(Board3D.TileState.Current, board.GetTileState(Team.Ally, new Vector2Int(0, 1)), "acting ally glows");
            Assert.AreEqual(Board3D.TileState.Base, board.GetTileState(Team.Enemy, new Vector2Int(0, 0)), "occupied enemy tile");
            Assert.AreEqual(Board3D.TileState.Empty, board.GetTileState(Team.Enemy, new Vector2Int(1, 0)), "empty tile");
        }

        [Test]
        public void DeadUnitIsNotPickable()
        {
            (BattleState state, Board3D board) = BuildBoard();
            Unit enemy = state.LivingUnits(Team.Enemy)[0];
            enemy.Hp = 0;
            board.SyncFromState(state);
            Assert.IsFalse(board.ViewFor(enemy).gameObject.activeSelf, "dead view hidden");
            Assert.AreEqual(Board3D.TileState.Empty, board.GetTileState(Team.Enemy, enemy.Cell), "its tile looks empty");
            Vector3 top = board.Layout.CellPosition(Team.Enemy, enemy.Cell);
            bool hit = board.PickAt(new Ray(top + Vector3.up * 5f, Vector3.down), out Team team, out Vector2Int cell);
            Assert.IsTrue(hit, "the tile under the dead unit is still pickable");
            Assert.AreEqual(Team.Enemy, team);
            Assert.AreEqual(enemy.Cell, cell);
        }

        [Test]
        public void MissingSpriteFallsBackToPlaceholder()
        {
            (BattleState state, Board3D board) = BuildBoard();
            UnitView view = board.ViewFor(state.Units[0]);
            Assert.IsNotNull(view.GetComponentInChildren<SpriteRenderer>().sprite);
        }

        [Test]
        public void StatTextShowsHpAndBlock()
        {
            (BattleState state, Board3D board) = BuildBoard();
            UnitView view = board.ViewFor(state.Units[0]);
            view.SetStats(12, 30, 4);
            Assert.AreEqual("12/30  방4", view.StatText);
            view.SetStats(10, 20, 0);
            Assert.AreEqual("10/20", view.StatText);
            Assert.AreEqual(UnitView.HpBarWidth * 0.5f, view.HpFillWidth, 1e-4f, "hp bar shrinks with hp");
        }

        [Test]
        public void MoveViewWithoutAnimationJumpsHome()
        {
            (BattleState state, Board3D board) = BuildBoard();
            Unit ally = state.Units[0];
            Coroutines.Drain(board.MoveView(ally, new Vector2Int(0, 1), new Vector2Int(1, 1), false));
            Assert.AreEqual(board.Layout.CellPosition(Team.Ally, new Vector2Int(1, 1)), board.ViewFor(ally).HomePosition);
            Assert.AreEqual(Board3D.TileState.Empty, board.GetTileState(Team.Ally, new Vector2Int(0, 1)));
            Assert.AreEqual(Board3D.TileState.Current, board.GetTileState(Team.Ally, new Vector2Int(1, 1)));
        }
    }
}
```

- [ ] **Step 2: 검증 절차 1~2** — Expected: `Board3D` 등 없음 컴파일 에러.

- [ ] **Step 3: 구현**

`Scripts/View/ViewAssets.cs`:
```csharp
using System;
using TMPro;
using UnityEngine;

namespace ProjectVoid.View
{
    /// <summary>화면 요소들이 함께 쓰는 에셋 묶음. BattleRoot 가 인스펙터에서 받아 나눠 준다.</summary>
    [Serializable]
    public sealed class ViewAssets
    {
        public TMP_FontAsset font;
        public Material overlayTextMaterial;
        public Material tileMaterial;
        public Sprite placeholderSprite;

        [NonSerialized] private Sprite _white;
        [NonSerialized] private Sprite _shadow;

        /// <summary>HP 바용 1×1 흰 스프라이트 (1 유닛 크기).</summary>
        public Sprite White => _white != null ? _white : _white = CreateWhite();

        /// <summary>가운데가 진하고 가장자리가 투명한 원형 그림자 (1 유닛 폭). 색은 SpriteRenderer.color 로 입힌다.</summary>
        public Sprite Shadow => _shadow != null ? _shadow : _shadow = CreateShadow();

        private static Sprite CreateWhite()
        {
            var texture = new Texture2D(1, 1, TextureFormat.RGBA32, false);
            texture.SetPixel(0, 0, Color.white);
            texture.Apply();
            return Sprite.Create(texture, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 1f);
        }

        private static Sprite CreateShadow()
        {
            const int size = 64;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            var center = new Vector2((size - 1) / 2f, (size - 1) / 2f);
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float t = Mathf.Clamp01(Vector2.Distance(new Vector2(x, y), center) / (size / 2f));
                    texture.SetPixel(x, y, new Color(1f, 1f, 1f, 0.5f * (1f - t)));
                }
            }
            texture.Apply();
            return Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
        }
    }
}
```

`Scripts/View/Coroutines.cs`:
```csharp
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace ProjectVoid.View
{
    public static class Coroutines
    {
        /// <summary>duration 동안 0→1 진행률로 step 을 부르고, 마지막에 1 로 한 번 더 부른다.</summary>
        public static IEnumerator Tween(float duration, Action<float> step)
        {
            float elapsed = 0f;
            while (elapsed < duration)
            {
                step(elapsed / duration);
                yield return null;
                elapsed += Time.deltaTime;
            }
            step(1f);
        }

        /// <summary>
        /// 중첩 IEnumerator 를 대기 없이 끝까지 돌린다 (즉시 재생 테스트용). WaitForSeconds 같은 대기 객체는 건너뛴다.
        /// Tween 처럼 시간이 흘러야 끝나는 루틴을 넘기면 멈추지 않으므로 상한을 둔다.
        /// </summary>
        public static void Drain(IEnumerator routine)
        {
            var stack = new Stack<IEnumerator>();
            stack.Push(routine);
            for (int guard = 0; stack.Count > 0; guard++)
            {
                if (guard > 100000)
                {
                    throw new InvalidOperationException("Drain did not finish; a time-based routine was drained.");
                }
                IEnumerator top = stack.Peek();
                if (!top.MoveNext())
                {
                    stack.Pop();
                    continue;
                }
                if (top.Current is IEnumerator nested)
                {
                    stack.Push(nested);
                }
            }
        }
    }
}
```

`Scripts/View/CellTag.cs`:
```csharp
using ProjectVoid.Combat;
using UnityEngine;

namespace ProjectVoid.View
{
    /// <summary>클릭 판정용 콜라이더에 붙여 "어느 편의 어느 칸인가"를 알려 준다 (Godot set_meta 대응).</summary>
    public sealed class CellTag : MonoBehaviour
    {
        public Team team;
        public Vector2Int cell;
    }
}
```

`Scripts/View/Billboard.cs`:
```csharp
using UnityEngine;

namespace ProjectVoid.View
{
    /// <summary>카메라를 향해 돈다. yAxisOnly 면 세로축으로만 돌아 캐릭터가 뒤로 눕지 않는다 (Godot BILLBOARD_FIXED_Y).</summary>
    public sealed class Billboard : MonoBehaviour
    {
        public bool yAxisOnly;

        private void LateUpdate()
        {
            Camera camera = Camera.main;
            if (camera == null)
            {
                return;
            }
            if (!yAxisOnly)
            {
                transform.rotation = camera.transform.rotation;
                return;
            }
            Vector3 forward = camera.transform.forward;
            forward.y = 0f;
            if (forward.sqrMagnitude > 0f)
            {
                transform.rotation = Quaternion.LookRotation(forward);
            }
        }
    }
}
```

`Scripts/View/UnitView.cs`:
```csharp
using System.Collections;
using ProjectVoid.Combat;
using TMPro;
using UnityEngine;

namespace ProjectVoid.View
{
    /// <summary>유닛 하나의 2.5D 표현: 빌보드 스프라이트, 머리 위 이름·HP 바·수치, 발밑 그림자, 클릭 판정 상자, 연출.</summary>
    public sealed class UnitView : MonoBehaviour
    {
        public const float SpriteHeight = 1.6f;
        public const float OverheadY = 2.0f;
        public const float HpBarWidth = 0.9f;
        public const float HpBarHeight = 0.1f;
        public const float LungeDistance = 0.4f;
        public const float ActionTime = 0.25f;
        public const float FlashTime = 0.24f;
        public const float PopTime = 0.6f;
        public const float FadeTime = 0.4f;
        public const float MoveTime = 0.25f;

        private static readonly Color FlashColor = new Color(1f, 0.45f, 0.45f);
        private static readonly Color AllyShadowColor = new Color(0.25f, 0.45f, 1f);
        private static readonly Color EnemyShadowColor = new Color(1f, 0.3f, 0.25f);

        private ViewAssets _assets;
        private SpriteRenderer _sprite;
        private Transform _overhead;
        private TextMeshPro _nameLabel;
        private TextMeshPro _statLabel;
        private SpriteRenderer _hpBack;
        private SpriteRenderer _hpFill;
        private SpriteRenderer _shadow;
        private BoxCollider _pickCollider;
        private int _hp;
        private int _maxHp = 1;
        private int _block;

        public Unit Unit { get; private set; }
        public Vector3 HomePosition { get; private set; }
        public CellTag PickTag { get; private set; }
        public string StatText => _statLabel.text;
        public float HpFillWidth => _hpFill.transform.localScale.x;

        public void Setup(Unit unit, Sprite sprite, ViewAssets assets)
        {
            Unit = unit;
            _assets = assets;

            var spriteObject = new GameObject("Sprite");
            spriteObject.transform.SetParent(transform, false);
            _sprite = spriteObject.AddComponent<SpriteRenderer>();
            _sprite.sprite = sprite;
            // 스프라이트는 오른쪽을 본다. 적은 왼쪽(아군 쪽)을 보도록 뒤집는다.
            _sprite.flipX = !unit.IsAlly;
            float spriteHeight = sprite.bounds.size.y;
            spriteObject.transform.localScale = Vector3.one * (SpriteHeight / spriteHeight);
            spriteObject.AddComponent<Billboard>().yAxisOnly = true;

            var shadowObject = new GameObject("Shadow");
            shadowObject.transform.SetParent(transform, false);
            shadowObject.transform.localPosition = new Vector3(0f, 0.01f, 0f);
            shadowObject.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            shadowObject.transform.localScale = new Vector3(0.9f, 0.5f, 1f);
            _shadow = shadowObject.AddComponent<SpriteRenderer>();
            _shadow.sprite = assets.Shadow;
            _shadow.color = unit.IsAlly ? AllyShadowColor : EnemyShadowColor;

            _overhead = new GameObject("Overhead").transform;
            _overhead.SetParent(transform, false);
            _overhead.localPosition = new Vector3(0f, OverheadY, 0f);
            _overhead.gameObject.AddComponent<Billboard>();
            _nameLabel = MakeLabel(unit.Data.displayName, 1.5f, new Vector3(0f, 0.28f, 0f));
            _hpBack = MakeBar(new Color(0.1f, 0.1f, 0.1f, 0.85f), 10);
            _hpFill = MakeBar(new Color(0.35f, 0.85f, 0.4f), 11);
            _statLabel = MakeLabel("", 1.2f, new Vector3(0f, -0.18f, 0f));

            _pickCollider = gameObject.AddComponent<BoxCollider>();
            _pickCollider.size = new Vector3(0.8f, SpriteHeight, 0.4f);
            _pickCollider.center = new Vector3(0f, SpriteHeight / 2f, 0f);
            PickTag = gameObject.AddComponent<CellTag>();

            SetStats(unit.Hp, unit.Data.maxHp, unit.Block);
        }

        public void SetStats(int hp, int maxHp, int block)
        {
            _hp = hp;
            _maxHp = Mathf.Max(maxHp, 1);
            _block = block;
            RefreshStats();
        }

        public void SetHp(int hp, int maxHp)
        {
            _hp = hp;
            _maxHp = Mathf.Max(maxHp, 1);
            RefreshStats();
        }

        public void SetBlock(int block)
        {
            _block = block;
            RefreshStats();
        }

        public void SetHome(Vector3 worldPosition)
        {
            HomePosition = worldPosition;
            transform.position = worldPosition;
        }

        public IEnumerator SlideTo(Vector3 worldPosition)
        {
            HomePosition = worldPosition;
            Vector3 start = transform.position;
            yield return Coroutines.Tween(MoveTime, t => transform.position = Vector3.Lerp(start, worldPosition, t));
        }

        /// <summary>연출이 중간에 끊겨도 제자리·원래 색으로 돌린다.</summary>
        public void ResetPose()
        {
            transform.position = HomePosition;
            _sprite.transform.localPosition = Vector3.zero;
            _sprite.color = Color.white;
        }

        public void SetAlive(bool alive)
        {
            gameObject.SetActive(alive);
        }

        public IEnumerator LungeToward(Vector3 worldTarget)
        {
            Vector3 direction = worldTarget - HomePosition;
            direction.y = 0f;
            if (direction.sqrMagnitude > 0f)
            {
                direction.Normalize();
            }
            Vector3 home = HomePosition;
            Vector3 lunge = home + direction * LungeDistance;
            yield return Coroutines.Tween(ActionTime / 2f, t => transform.position = Vector3.Lerp(home, lunge, t));
            yield return Coroutines.Tween(ActionTime / 2f, t => transform.position = Vector3.Lerp(lunge, home, t));
        }

        public IEnumerator Hop()
        {
            Transform sprite = _sprite.transform;
            var up = new Vector3(0f, 0.25f, 0f);
            yield return Coroutines.Tween(ActionTime / 2f, t => sprite.localPosition = Vector3.Lerp(Vector3.zero, up, t));
            yield return Coroutines.Tween(ActionTime / 2f, t => sprite.localPosition = Vector3.Lerp(up, Vector3.zero, t));
        }

        // Godot: 색 번쩍임 2회 + 좌우 흔들림(0.08, -0.08, 0.05, 0)을 FLASH_TIME 동안 동시에.
        public IEnumerator FlashAndShake()
        {
            float[] keys = { 0f, 0.08f, -0.08f, 0.05f, 0f };
            Transform sprite = _sprite.transform;
            yield return Coroutines.Tween(FlashTime, t =>
            {
                int quarter = Mathf.Min((int)(t * 4f), 3);
                _sprite.color = quarter % 2 == 0 && t < 1f ? FlashColor : Color.white;
                float local = t * 4f - quarter;
                sprite.localPosition = new Vector3(Mathf.Lerp(keys[quarter], keys[quarter + 1], local), 0f, 0f);
            });
        }

        public void PopText(string text, Color color)
        {
            TextMeshPro label = MakeLabel(text, 2.6f, new Vector3(0f, 0.5f, 0f));
            label.color = color;
            StartCoroutine(PopRoutine(label));
        }

        private IEnumerator PopRoutine(TextMeshPro label)
        {
            Vector3 start = label.transform.localPosition;
            Color color = label.color;
            yield return Coroutines.Tween(PopTime, t =>
            {
                label.transform.localPosition = start + new Vector3(0f, 0.6f * t, 0f);
                label.color = new Color(color.r, color.g, color.b, 1f - t);
            });
            Destroy(label.gameObject);
        }

        public IEnumerator FadeOut()
        {
            _pickCollider.enabled = false;
            _hpBack.enabled = false;
            _hpFill.enabled = false;
            _shadow.enabled = false;
            yield return Coroutines.Tween(FadeTime, t =>
            {
                float alpha = 1f - t;
                _sprite.color = new Color(1f, 1f, 1f, alpha);
                _nameLabel.alpha = alpha;
                _statLabel.alpha = alpha;
            });
            gameObject.SetActive(false);
        }

        private void RefreshStats()
        {
            _statLabel.text = _block > 0 ? $"{_hp}/{_maxHp}  방{_block}" : $"{_hp}/{_maxHp}";
            float ratio = Mathf.Clamp01((float)_hp / _maxHp);
            Transform fill = _hpFill.transform;
            fill.localScale = new Vector3(HpBarWidth * ratio, HpBarHeight, 1f);
            fill.localPosition = new Vector3(-HpBarWidth * (1f - ratio) / 2f, 0f, 0f);
            _hpFill.enabled = ratio > 0f;
        }

        private TextMeshPro MakeLabel(string text, float fontSize, Vector3 localPosition)
        {
            var labelObject = new GameObject("Label");
            labelObject.transform.SetParent(_overhead, false);
            labelObject.transform.localPosition = localPosition;
            var label = labelObject.AddComponent<TextMeshPro>();
            label.font = _assets.font;
            label.fontSharedMaterial = _assets.overlayTextMaterial;
            label.fontSize = fontSize;
            label.alignment = TextAlignmentOptions.Center;
            label.rectTransform.sizeDelta = new Vector2(4f, 0.6f);
            label.text = text;
            return label;
        }

        private SpriteRenderer MakeBar(Color color, int sortingOrder)
        {
            var barObject = new GameObject("Bar");
            barObject.transform.SetParent(_overhead, false);
            barObject.transform.localScale = new Vector3(HpBarWidth, HpBarHeight, 1f);
            var bar = barObject.AddComponent<SpriteRenderer>();
            bar.sprite = _assets.White;
            bar.color = color;
            bar.sortingOrder = sortingOrder;
            return bar;
        }
    }
}
```

`Scripts/View/Board3D.cs`:
```csharp
using System;
using System.Collections;
using System.Collections.Generic;
using ProjectVoid.Combat;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace ProjectVoid.View
{
    public readonly struct CellHint
    {
        public CellHint(bool valid, string text)
        {
            Valid = valid;
            Text = text;
        }

        public bool Valid { get; }
        public string Text { get; }
    }

    /// <summary>3D 진영 타일, 유닛 뷰, 타일 상태 표시, 마우스 클릭·호버 판정.</summary>
    public sealed class Board3D : MonoBehaviour
    {
        public enum TileState { Base, Empty, Current, Valid, Invalid, Movable, ShapeHit, ShapeOut }

        public const float TileThickness = 0.1f;
        private const float RayLength = 100f;
        private static readonly Color AllyTileColor = new Color(0.36f, 0.44f, 0.55f);
        private static readonly Color EnemyTileColor = new Color(0.55f, 0.38f, 0.38f);
        private static readonly Color CurrentEmission = new Color(1f, 0.82f, 0.3f);
        private static readonly Color ValidEmission = new Color(0.45f, 0.85f, 0.45f);
        private static readonly Color MoveEmission = new Color(0.45f, 0.65f, 1f);
        private static readonly Color ShapeHitEmission = new Color(0.95f, 0.95f, 0.95f);
        private static readonly Color ShapeOutEmission = new Color(1f, 0.5f, 0.15f);
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");

        public event Action<Team, Vector2Int> CellClicked;
        public event Action PickMissed;
        public event Action<Team, Vector2Int> CellHovered;
        public event Action HoverCleared;

        private readonly Dictionary<(Team, Vector2Int), Renderer> _tiles = new Dictionary<(Team, Vector2Int), Renderer>();
        private readonly Dictionary<(Team, Vector2Int), TileState> _tileStates = new Dictionary<(Team, Vector2Int), TileState>();
        private readonly Dictionary<(Team, Vector2Int), TextMeshPro> _hints = new Dictionary<(Team, Vector2Int), TextMeshPro>();
        private readonly Dictionary<Unit, UnitView> _views = new Dictionary<Unit, UnitView>();
        private ViewAssets _assets;
        private Vector2 _pendingClick;
        private bool _hasPendingClick;
        private Vector2 _pointer;
        private bool _hasHover;
        private Team _hoverTeam;
        private Vector2Int _hoverCell;

        public BoardLayout Layout { get; private set; }
        public bool InputEnabled { get; set; }
        /// <summary>HUD 카드를 끌고 있거나 이번 프레임에 놓았으면 true. 그때의 마우스 떼기는 보드 클릭이 아니다 (놓기는 RequestPick 으로 따로 온다).</summary>
        public Func<bool> UiDragActive { get; set; }
        public int TileCount => _tiles.Count;

        public void Build(BattleState state, ViewAssets assets)
        {
            _assets = assets;
            Layout = new BoardLayout(state.Resolver.AllyGrid, state.Resolver.EnemyGrid);
            BuildSide(Team.Ally, Layout.AllyGrid);
            BuildSide(Team.Enemy, Layout.EnemyGrid);
            foreach (Unit unit in state.Units)
            {
                var viewObject = new GameObject($"Unit {unit.UnitId} {unit.Data.id}");
                viewObject.transform.SetParent(transform, false);
                var view = viewObject.AddComponent<UnitView>();
                view.Setup(unit, unit.Data.sprite != null ? unit.Data.sprite : assets.placeholderSprite, assets);
                view.SetHome(Layout.CellPosition(unit.Team, unit.Cell));
                Tag(view.PickTag, unit.Team, unit.Cell);
                _views[unit] = view;
            }
        }

        /// <summary>규칙 상태로 모든 뷰와 타일을 맞춘다 (재생이 끝난 뒤 어긋남 정리).</summary>
        public void SyncFromState(BattleState state)
        {
            ClearTargetHints();
            foreach ((Team, Vector2Int) key in new List<(Team, Vector2Int)>(_tiles.Keys))
            {
                SetTileState(key.Item1, key.Item2, TileState.Empty);
            }
            foreach (Unit unit in state.Units)
            {
                UnitView view = _views[unit];
                view.SetHome(Layout.CellPosition(unit.Team, unit.Cell));
                Tag(view.PickTag, unit.Team, unit.Cell);
                view.ResetPose();
                view.SetStats(unit.Hp, unit.Data.maxHp, unit.Block);
                view.SetAlive(unit.IsAlive);
                if (unit.IsAlive)
                {
                    SetTileState(unit.Team, unit.Cell, TileState.Base);
                }
            }
            Unit actor = state.CurrentUnit();
            if (actor != null && !state.Finished && actor.IsAlive)
            {
                SetTileState(actor.Team, actor.Cell, TileState.Current);
            }
        }

        public void ShowCurrent(Team team, Vector2Int cell)
        {
            foreach ((Team, Vector2Int) key in new List<(Team, Vector2Int)>(_tileStates.Keys))
            {
                if (_tileStates[key] == TileState.Current)
                {
                    SetTileState(key.Item1, key.Item2, TileState.Base);
                }
            }
            SetTileState(team, cell, TileState.Current);
        }

        public void MarkEmpty(Team team, Vector2Int cell) => SetTileState(team, cell, TileState.Empty);

        public void ShowTargetHints(Team team, Dictionary<Vector2Int, CellHint> hints)
        {
            ClearTargetHints();
            foreach (KeyValuePair<Vector2Int, CellHint> pair in hints)
            {
                SetTileState(team, pair.Key, pair.Value.Valid ? TileState.Valid : TileState.Invalid);
                ShowHint(team, pair.Key, pair.Value.Text);
            }
        }

        public void ClearTargetHints()
        {
            foreach (KeyValuePair<(Team, Vector2Int), TextMeshPro> pair in _hints)
            {
                pair.Value.gameObject.SetActive(false);
                (Team team, Vector2Int cell) = pair.Key;
                TileState current = _tileStates.TryGetValue(pair.Key, out TileState s) ? s : TileState.Empty;
                if (current == TileState.Valid || current == TileState.Invalid || current == TileState.ShapeHit || current == TileState.ShapeOut)
                {
                    SetTileState(team, cell, OccupiedByLivingView(team, cell) ? TileState.Base : TileState.Empty);
                }
                else if (current == TileState.Movable)
                {
                    SetTileState(team, cell, TileState.Empty);
                }
            }
        }

        public void ShowShapePreview(Team team, Dictionary<Vector2Int, CellHint> hits)
        {
            foreach (KeyValuePair<Vector2Int, CellHint> pair in hits)
            {
                SetTileState(team, pair.Key, pair.Value.Valid ? TileState.ShapeHit : TileState.ShapeOut);
                ShowHint(team, pair.Key, pair.Value.Text);
            }
        }

        public void ShowMoveHints(Team team, List<Vector2Int> cells)
        {
            ClearTargetHints();
            foreach (Vector2Int cell in cells)
            {
                SetTileState(team, cell, TileState.Movable);
            }
        }

        public IEnumerator MoveView(Unit unit, Vector2Int fromCell, Vector2Int toCell, bool animate)
        {
            SetTileState(unit.Team, fromCell, TileState.Empty);
            SetTileState(unit.Team, toCell, TileState.Current);
            UnitView view = ViewFor(unit);
            Tag(view.PickTag, unit.Team, toCell);
            Vector3 target = Layout.CellPosition(unit.Team, toCell);
            if (animate)
            {
                yield return view.SlideTo(target);
            }
            else
            {
                view.SetHome(target);
            }
        }

        public UnitView ViewFor(Unit unit) => _views.TryGetValue(unit, out UnitView view) ? view : null;

        public TileState GetTileState(Team team, Vector2Int cell) => _tileStates[(team, cell)];

        public TextMeshPro HintLabel(Team team, Vector2Int cell) => _hints[(team, cell)];

        public bool TryGetHover(out Team team, out Vector2Int cell)
        {
            team = _hoverTeam;
            cell = _hoverCell;
            return _hasHover;
        }

        /// <summary>카드를 놓은 화면 위치를 다음 Update 에서 판정한다 (결과는 CellClicked 또는 PickMissed).</summary>
        public void RequestPick(Vector2 screenPosition)
        {
            _pendingClick = screenPosition;
            _hasPendingClick = true;
        }

        /// <summary>카드를 끄는 동안 호버 위치를 HUD 가 알려 준다.</summary>
        public void UpdatePointer(Vector2 screenPosition)
        {
            _pointer = screenPosition;
        }

        public bool PickAt(Ray ray, out Team team, out Vector2Int cell)
        {
            Physics.SyncTransforms();
            if (Physics.Raycast(ray, out RaycastHit hit, RayLength) && hit.collider.TryGetComponent(out CellTag tag))
            {
                team = tag.team;
                cell = tag.cell;
                return true;
            }
            team = default;
            cell = default;
            return false;
        }

        private void Update()
        {
            if (_hasPendingClick)
            {
                _hasPendingClick = false;
                if (PickScreen(_pendingClick, out Team team, out Vector2Int cell))
                {
                    CellClicked?.Invoke(team, cell);
                }
                else
                {
                    PickMissed?.Invoke();
                }
            }
            if (!InputEnabled)
            {
                return;
            }
            Mouse mouse = Mouse.current;
            if (mouse == null)
            {
                return;
            }
            bool dragging = UiDragActive != null && UiDragActive();
            if (!dragging)
            {
                _pointer = mouse.position.ReadValue();
            }
            bool overUi = EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
            if (mouse.leftButton.wasReleasedThisFrame && !dragging && !overUi)
            {
                RequestPick(_pointer);
            }
            UpdateHover();
        }

        private void UpdateHover()
        {
            if (!PickScreen(_pointer, out Team team, out Vector2Int cell))
            {
                if (_hasHover)
                {
                    _hasHover = false;
                    HoverCleared?.Invoke();
                }
                return;
            }
            if (_hasHover && team == _hoverTeam && cell == _hoverCell)
            {
                return;
            }
            _hasHover = true;
            _hoverTeam = team;
            _hoverCell = cell;
            CellHovered?.Invoke(team, cell);
        }

        private bool PickScreen(Vector2 screenPosition, out Team team, out Vector2Int cell)
        {
            Camera camera = Camera.main;
            if (camera == null)
            {
                team = default;
                cell = default;
                return false;
            }
            return PickAt(camera.ScreenPointToRay(screenPosition), out team, out cell);
        }

        private bool OccupiedByLivingView(Team team, Vector2Int cell)
        {
            foreach (Unit unit in _views.Keys)
            {
                if (unit.Team == team && unit.Cell == cell && unit.IsAlive)
                {
                    return true;
                }
            }
            return false;
        }

        private void ShowHint(Team team, Vector2Int cell, string text)
        {
            TextMeshPro label = _hints[(team, cell)];
            label.text = text;
            label.gameObject.SetActive(true);
        }

        private void SetTileState(Team team, Vector2Int cell, TileState state)
        {
            _tileStates[(team, cell)] = state;
            Material material = _tiles[(team, cell)].sharedMaterial;
            Color baseColor = team == Team.Ally ? AllyTileColor : EnemyTileColor;
            Color albedo = baseColor;
            Color emission = Color.black;
            switch (state)
            {
                case TileState.Empty: albedo = Darkened(baseColor, 0.45f); break;
                case TileState.Current: emission = CurrentEmission * 0.8f; break;
                case TileState.Valid: emission = ValidEmission * 0.8f; break;
                case TileState.Invalid: albedo = Darkened(baseColor, 0.6f); break;
                case TileState.Movable: emission = MoveEmission * 0.6f; break;
                case TileState.ShapeHit: emission = ShapeHitEmission * 1.0f; break;
                case TileState.ShapeOut: emission = ShapeOutEmission * 0.8f; break;
            }
            material.SetColor(BaseColorId, albedo);
            material.SetColor(EmissionColorId, emission);
        }

        // Godot Color.darkened: 검정 쪽으로 amount 만큼.
        private static Color Darkened(Color color, float amount)
            => new Color(color.r * (1f - amount), color.g * (1f - amount), color.b * (1f - amount), color.a);

        private void BuildSide(Team team, Vector2Int grid)
        {
            for (int row = 0; row < grid.y; row++)
            {
                for (int col = 0; col < grid.x; col++)
                {
                    var cell = new Vector2Int(col, row);
                    Vector3 top = Layout.CellPosition(team, cell);

                    GameObject tile = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    tile.name = $"Tile {team} {col},{row}";
                    tile.transform.SetParent(transform, false);
                    tile.transform.localScale = new Vector3(BoardLayout.TileSize, TileThickness, BoardLayout.TileSize);
                    tile.transform.position = top - new Vector3(0f, TileThickness / 2f, 0f);
                    var renderer = tile.GetComponent<Renderer>();
                    // 타일마다 색이 달라지므로 머티리얼을 복사해 쓴다.
                    renderer.sharedMaterial = new Material(_assets.tileMaterial);
                    Tag(tile.AddComponent<CellTag>(), team, cell);

                    var hintObject = new GameObject($"Hint {team} {col},{row}");
                    hintObject.transform.SetParent(transform, false);
                    hintObject.transform.position = top + new Vector3(0f, UnitView.OverheadY + 0.6f, 0f);
                    hintObject.AddComponent<Billboard>();
                    var hint = hintObject.AddComponent<TextMeshPro>();
                    hint.font = _assets.font;
                    hint.fontSharedMaterial = _assets.overlayTextMaterial;
                    hint.fontSize = 1.6f;
                    hint.alignment = TextAlignmentOptions.Center;
                    hint.rectTransform.sizeDelta = new Vector2(3f, 0.6f);
                    hintObject.SetActive(false);

                    _tiles[(team, cell)] = renderer;
                    _hints[(team, cell)] = hint;
                    SetTileState(team, cell, TileState.Empty);
                }
            }
        }

        private static void Tag(CellTag tag, Team team, Vector2Int cell)
        {
            tag.team = team;
            tag.cell = cell;
        }
    }
}
```

- [ ] **Step 4: 검증** — FILTER `ProjectVoid\.Tests\.BoardViewTests`. Expected: `PASSED 6 / FAILED 0`. 에디트 모드에서 레이캐스트가 안 맞으면 (`DeadUnitIsNotPickable` 실패) `PickAt` 앞의 `Physics.SyncTransforms()` 가 불렸는지 확인하고, 그래도 안 되면 테스트에서 `Physics.simulationMode = SimulationMode.Script` 후 `Physics.Simulate(0.01f)` 를 한 번 불러 콜라이더를 등록한다.

- [ ] **Step 5: Commit**

```bash
git add Assets/_Project
git commit -m "feat: add 2.5D board, tiles and billboard unit views

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 14: BattleHud (uGUI)

**Files:**
- Create: `Assets/_Project/Scripts/View/UI/CardButton.cs`, `Assets/_Project/Scripts/View/UI/BattleHud.cs`
- Test: `Assets/_Project/Tests/EditMode/BattleHudTests.cs`

**Interfaces:**
- Consumes: `BattleEvent`, `BattleState`, `ViewAssets`
- Produces:
  - `CardButton : MonoBehaviour` — `CardData Card`, `bool Interactable`, `Setup(CardData, ViewAssets)`, `SetAffordable(bool)`, `SetSelected(bool)`, `SetPending(bool)`; 이벤트 `Clicked(CardButton)`, `DragBegan(CardButton)`, `Dragged(CardButton, Vector2)`, `DragEnded(CardButton, Vector2)`
  - `BattleHud : MonoBehaviour` — 이벤트 `CardSelected(int)`, `CardDropped(int, Vector2)`, `CardDragMoved(Vector2)`, `EndTurnPressed()`, `MoveModeToggled(bool)`, `RestartPressed()`; 메서드 `Build(ViewAssets)`, `SyncFromState(BattleState, int selectedCard)`, `ShowTurn(BattleEvent)`, `SetInteractive(bool)`, `SetMoveAvailable(bool)`, `SetMoveMode(bool)`, `ClearCardSelection()`, `SetPendingPlay(int)`, `DrawCard(BattleEvent)`, `Reshuffle(BattleEvent)`, `DiscardHand(BattleEvent)`, `RemovePlayedCard(BattleEvent)`, `ApplyMove(BattleEvent)`, `AppendLog(string)`, `ShowBanner(bool)`, `static string TurnBarText(int round, List<Unit> order, List<bool> alive, int turnIndex)`; 조회 `TurnText`, `SpText`, `LogText`, `HandCount`, `EndTurnEnabled`, `MoveEnabled`, `MovePressed`, `BannerVisible`, `BannerText`, `DragActiveThisFrame`

1단계 손패는 가로 버튼 줄이다. 드로우·리셔플·버리기 "연출"은 2단계 — 여기서는 버튼 추가/비우기만 한다 (`Reshuffle` 은 아무것도 하지 않는다).

- [ ] **Step 1: 실패하는 테스트 작성** — `Tests/EditMode/BattleHudTests.cs`:

```csharp
using System.Collections.Generic;
using NUnit.Framework;
using ProjectVoid.Combat;
using ProjectVoid.View;
using UnityEngine;

namespace ProjectVoid.Tests
{
    public class BattleHudTests
    {
        private GameObject _root;

        [TearDown]
        public void TearDown()
        {
            if (_root != null)
            {
                Object.DestroyImmediate(_root);
            }
            Make.Cleanup();
        }

        private BattleHud NewHud()
        {
            _root = new GameObject("HudTestRoot");
            var hud = _root.AddComponent<BattleHud>();
            hud.Build(TestAssets.Load());
            return hud;
        }

        private static Unit NewUnit(int id, Team team) =>
            new Unit(id, team == Team.Ally ? Make.Ally($"u{id}") : (UnitData)Make.Enemy($"u{id}"), team, Vector2Int.zero);

        [Test]
        public void TurnBarMarksCurrentAndSkipsDead()
        {
            var order = new List<Unit> { NewUnit(0, Team.Ally), NewUnit(1, Team.Enemy), NewUnit(2, Team.Ally) };
            string text = BattleHud.TurnBarText(2, order, new List<bool> { true, false, true }, 2);
            StringAssert.StartsWith("R2  ", text);
            StringAssert.Contains("▶u2", text, "current unit marked");
            StringAssert.DoesNotContain("u1", text, "dead unit hidden");
            StringAssert.Contains("u0", text, "acted unit still listed");
        }

        [Test]
        public void DrawCardAddsButtonsAndDimsUnaffordable()
        {
            BattleHud hud = NewHud();
            Unit ally = NewUnit(0, Team.Ally);
            ally.Sp = 1;
            hud.DrawCard(new BattleEvent(BattleEventKind.CardDrawn) { Unit = ally, Card = Make.Card("cheap", spCost: 1) });
            hud.DrawCard(new BattleEvent(BattleEventKind.CardDrawn) { Unit = ally, Card = Make.Card("dear", spCost: 2) });
            Assert.AreEqual(2, hud.HandCount);
            Assert.IsTrue(hud.IsCardAffordable(0));
            Assert.IsFalse(hud.IsCardAffordable(1));
        }

        [Test]
        public void DiscardHandClearsButtons()
        {
            BattleHud hud = NewHud();
            Unit ally = NewUnit(0, Team.Ally);
            hud.DrawCard(new BattleEvent(BattleEventKind.CardDrawn) { Unit = ally, Card = Make.Card("c") });
            hud.DiscardHand(new BattleEvent(BattleEventKind.HandDiscarded) { Unit = ally });
            Assert.AreEqual(0, hud.HandCount);
        }

        [Test]
        public void SyncShowsHandAndSpOfCurrentAlly()
        {
            var encounter = Make.Encounter(new Vector2Int(3, 3), new Vector2Int(3, 3),
                new[] { Make.Place(Make.Ally("a", speed: 9, deck: new[] { Make.Card("c1"), Make.Card("c2"), Make.Card("c3"), Make.Card("c4"), Make.Card("c5") }), 0, 1) },
                new[] { Make.Place(Make.Enemy("e", speed: 1), 0, 1) });
            BattleState state = Make.State(encounter, 3);
            state.StartBattle();
            BattleHud hud = NewHud();
            hud.SyncFromState(state, -1);
            Assert.AreEqual(4, hud.HandCount, "draws four");
            StringAssert.Contains("3 / 3", hud.SpText);
            StringAssert.Contains("▶a", hud.TurnText);
        }

        [Test]
        public void InteractiveTogglesButtons()
        {
            BattleHud hud = NewHud();
            hud.SetInteractive(false);
            Assert.IsFalse(hud.EndTurnEnabled);
            hud.SetInteractive(true);
            Assert.IsTrue(hud.EndTurnEnabled);
            hud.SetMoveAvailable(false);
            Assert.IsFalse(hud.MoveEnabled);
            hud.SetMoveAvailable(true);
            Assert.IsTrue(hud.MoveEnabled);
        }

        [Test]
        public void BannerShowsResult()
        {
            BattleHud hud = NewHud();
            Assert.IsFalse(hud.BannerVisible);
            hud.ShowBanner(true);
            Assert.IsTrue(hud.BannerVisible);
            Assert.AreEqual("승리!", hud.BannerText);
        }

        [Test]
        public void LogAppendsLines()
        {
            BattleHud hud = NewHud();
            hud.AppendLog("첫 줄");
            hud.AppendLog("둘째 줄");
            Assert.AreEqual("첫 줄\n둘째 줄\n", hud.LogText);
        }
    }
}
```
(`IsCardAffordable(int)` 도 조회 API 에 포함한다.)

- [ ] **Step 2: 검증 절차 1~2** — Expected: `BattleHud` 없음 컴파일 에러.

- [ ] **Step 3: 구현**

`Scripts/View/UI/CardButton.cs`:
```csharp
using System;
using ProjectVoid.Combat;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace ProjectVoid.View
{
    /// <summary>손패 카드 한 장. 클릭(카드 → 적 선택)과 드래그(적 위에 놓기) 둘 다 받는다.</summary>
    public sealed class CardButton : MonoBehaviour, IPointerClickHandler, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        public const float Width = 150f;
        public const float Height = 190f;
        private const float LiftHeight = 24f;
        private static readonly Color MeleeBorder = new Color(0.85f, 0.45f, 0.3f);
        private static readonly Color RangedBorder = new Color(0.35f, 0.6f, 0.95f);
        private static readonly Color FaceColor = new Color(0.16f, 0.16f, 0.2f);

        public event Action<CardButton> Clicked;
        public event Action<CardButton> DragBegan;
        public event Action<CardButton, Vector2> Dragged;
        public event Action<CardButton, Vector2> DragEnded;

        private RectTransform _face;
        private CanvasGroup _group;
        private bool _affordable = true;
        private bool _pending;

        public CardData Card { get; private set; }
        public bool Interactable { get; set; } = true;
        public bool Affordable => _affordable;

        public void Setup(CardData card, ViewAssets assets)
        {
            Card = card;
            var layout = gameObject.AddComponent<LayoutElement>();
            layout.preferredWidth = Width;
            layout.preferredHeight = Height;
            _group = gameObject.AddComponent<CanvasGroup>();

            var faceObject = new GameObject("Face", typeof(RectTransform), typeof(Image));
            _face = (RectTransform)faceObject.transform;
            _face.SetParent(transform, false);
            _face.anchorMin = Vector2.zero;
            _face.anchorMax = Vector2.one;
            _face.offsetMin = Vector2.zero;
            _face.offsetMax = Vector2.zero;
            faceObject.GetComponent<Image>().color = card.attackType == AttackType.Melee ? MeleeBorder : RangedBorder;

            var inner = new GameObject("Inner", typeof(RectTransform), typeof(Image));
            var innerRect = (RectTransform)inner.transform;
            innerRect.SetParent(_face, false);
            innerRect.anchorMin = Vector2.zero;
            innerRect.anchorMax = Vector2.one;
            innerRect.offsetMin = new Vector2(5f, 5f);
            innerRect.offsetMax = new Vector2(-5f, -5f);
            inner.GetComponent<Image>().color = FaceColor;

            string type = card.attackType == AttackType.Melee ? "근접" : "원거리";
            MakeText(innerRect, assets.font, card.displayName, 30f, new Vector2(0f, 45f));
            MakeText(innerRect, assets.font, $"SP {card.spCost}", 24f, new Vector2(0f, 0f));
            MakeText(innerRect, assets.font, $"{type} · 거리 {card.attackRange}\n피해 {card.damage}", 20f, new Vector2(0f, -50f));
        }

        public void SetAffordable(bool affordable)
        {
            _affordable = affordable;
            Refresh();
        }

        public void SetSelected(bool selected)
        {
            _face.anchoredPosition = new Vector2(0f, selected ? LiftHeight : 0f);
        }

        public void SetPending(bool pending)
        {
            _pending = pending;
            Refresh();
        }

        private void Refresh()
        {
            _group.alpha = _pending ? 0.2f : _affordable ? 1f : 0.45f;
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (Interactable && _affordable && !_pending)
            {
                Clicked?.Invoke(this);
            }
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            if (Interactable && _affordable && !_pending)
            {
                DragBegan?.Invoke(this);
            }
        }

        public void OnDrag(PointerEventData eventData)
        {
            Dragged?.Invoke(this, eventData.position);
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            DragEnded?.Invoke(this, eventData.position);
        }

        private static void MakeText(RectTransform parent, TMP_FontAsset font, string value, float size, Vector2 position)
        {
            var textObject = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
            var rect = (RectTransform)textObject.transform;
            rect.SetParent(parent, false);
            rect.sizeDelta = new Vector2(Width - 16f, 60f);
            rect.anchoredPosition = position;
            var text = textObject.GetComponent<TextMeshProUGUI>();
            text.font = font;
            text.text = value;
            text.fontSize = size;
            text.alignment = TextAlignmentOptions.Center;
            text.raycastTarget = false;
        }
    }
}
```

`Scripts/View/UI/BattleHud.cs`:
```csharp
using System;
using System.Collections.Generic;
using System.Text;
using ProjectVoid.Combat;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ProjectVoid.View
{
    /// <summary>
    /// 전투 HUD: 위 행동 순서, 아래 손패(버튼 줄), 오른쪽 아래 SP·이동·차례 종료, 왼쪽 로그, 가운데 승패 배너.
    /// 코드로 구성한다 (Godot UnitView 처럼 씬 파일 없이).
    /// </summary>
    public sealed class BattleHud : MonoBehaviour
    {
        private static readonly Color CurrentTurnColor = new Color(1f, 0.82f, 0.3f);
        private static readonly Color ActedColor = new Color(0.5f, 0.5f, 0.5f);
        private static readonly Color ButtonColor = new Color(0.22f, 0.24f, 0.3f);
        private static readonly Color PressedColor = new Color(0.3f, 0.5f, 0.9f);

        public event Action<int> CardSelected;
        public event Action<int, Vector2> CardDropped;
        public event Action<Vector2> CardDragMoved;
        public event Action EndTurnPressed;
        public event Action<bool> MoveModeToggled;
        public event Action RestartPressed;

        private readonly List<CardButton> _cards = new List<CardButton>();
        private readonly StringBuilder _log = new StringBuilder();
        private ViewAssets _assets;
        private TextMeshProUGUI _turnLabel;
        private TextMeshProUGUI _spLabel;
        private TextMeshProUGUI _logLabel;
        private ScrollRect _logScroll;
        private RectTransform _hand;
        private Button _moveButton;
        private Button _endTurnButton;
        private GameObject _banner;
        private TextMeshProUGUI _bannerLabel;
        private bool _interactive;
        private bool _moveAvailable;
        private bool _movePressed;
        private bool _dragging;
        private int _dragEndFrame = -1;

        public string TurnText => _turnLabel.text;
        public string SpText => _spLabel.gameObject.activeSelf ? _spLabel.text : "";
        public string LogText => _log.ToString();
        public int HandCount => _cards.Count;
        public bool EndTurnEnabled => _endTurnButton.interactable;
        public bool MoveEnabled => _moveButton.interactable;
        public bool MovePressed => _movePressed;
        public bool BannerVisible => _banner.activeSelf;
        public string BannerText => _bannerLabel.text;
        public bool DragActiveThisFrame => _dragging || _dragEndFrame == Time.frameCount;

        public bool IsCardAffordable(int index) => _cards[index].Affordable;

        public void Build(ViewAssets assets)
        {
            _assets = assets;
            var canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            gameObject.AddComponent<GraphicRaycaster>();
            var root = (RectTransform)transform;

            _turnLabel = Text(root, "", 30f, TextAlignmentOptions.Left);
            Anchor(_turnLabel.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(24f, -70f), new Vector2(-24f, -10f));

            _logScroll = BuildLog(root);

            _hand = Panel(root, "Hand", new Color(0f, 0f, 0f, 0f));
            Anchor(_hand, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(-560f, 16f), new Vector2(560f, 16f + CardButton.Height + 30f));
            var layout = _hand.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 12f;
            layout.childAlignment = TextAnchor.LowerCenter;
            // 크기는 각 카드의 LayoutElement(선호 크기)를 따른다.
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;

            _spLabel = Text(root, "", 28f, TextAlignmentOptions.Center);
            Anchor(_spLabel.rectTransform, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-440f, 20f), new Vector2(-300f, 170f));
            _moveButton = MakeButton(root, "이동", new Vector2(-290f, 100f), new Vector2(-160f, 160f), OnMoveClicked);
            _endTurnButton = MakeButton(root, "차례 종료", new Vector2(-290f, 20f), new Vector2(-24f, 90f), () => EndTurnPressed?.Invoke());

            _banner = BuildBanner(root);
            _banner.SetActive(false);
            RefreshSp(null);
            ApplyInteractive();
        }

        public void SyncFromState(BattleState state, int selectedCard)
        {
            Unit actor = state.Finished ? null : state.CurrentUnit();
            RefreshSp(actor);
            if (actor != null && actor.IsAlly)
            {
                SetCards(actor.Hand, actor.Sp, selectedCard);
            }
            else
            {
                ClearHand();
            }
            if (state.Finished)
            {
                _turnLabel.text = state.AllyWon ? "승리!" : "패배...";
                return;
            }
            var alive = new List<bool>();
            foreach (Unit member in state.Initiative)
            {
                alive.Add(member.IsAlive);
            }
            _turnLabel.text = TurnBarText(state.RoundIndex, state.Initiative, alive, state.TurnIndex);
        }

        public void ShowTurn(BattleEvent e)
        {
            _turnLabel.text = TurnBarText(e.RoundIndex, e.Order, e.Alive, e.TurnIndex);
            ClearHand();
            RefreshSp(e.Unit.IsAlly ? e.Unit : null);
        }

        public void SetInteractive(bool enabled)
        {
            _interactive = enabled;
            ApplyInteractive();
        }

        public void SetMoveAvailable(bool available)
        {
            _moveAvailable = available;
            ApplyInteractive();
        }

        public void SetMoveMode(bool on)
        {
            _movePressed = on;
            _moveButton.GetComponent<Image>().color = on ? PressedColor : ButtonColor;
        }

        public void ClearCardSelection()
        {
            foreach (CardButton card in _cards)
            {
                card.SetSelected(false);
            }
        }

        public void SetPendingPlay(int index)
        {
            if (index >= 0 && index < _cards.Count)
            {
                _cards[index].SetPending(true);
            }
        }

        public void DrawCard(BattleEvent e)
        {
            AddCard(e.Card, e.Card.spCost <= e.Unit.Sp);
        }

        public void Reshuffle(BattleEvent e)
        {
            // 1단계에는 더미 표시가 없다 (2단계 카드 연출에서 채운다).
        }

        public void DiscardHand(BattleEvent e)
        {
            ClearHand();
        }

        public void RemovePlayedCard(BattleEvent e)
        {
            int index = _cards.FindIndex(c => c.Card == e.Card);
            if (index >= 0)
            {
                DestroySafe(_cards[index].gameObject);
                _cards.RemoveAt(index);
            }
            RefreshAffordable(e.Unit.Sp);
            RefreshSp(e.Unit);
        }

        public void ApplyMove(BattleEvent e)
        {
            if (!e.Unit.IsAlly)
            {
                return;
            }
            RefreshAffordable(e.Unit.Sp);
            RefreshSp(e.Unit);
        }

        public void AppendLog(string text)
        {
            _log.Append(text).Append('\n');
            _logLabel.text = _log.ToString();
            Canvas.ForceUpdateCanvases();
            _logScroll.verticalNormalizedPosition = 0f;
        }

        public void ShowBanner(bool allyWon)
        {
            _bannerLabel.text = allyWon ? "승리!" : "패배...";
            _banner.SetActive(true);
        }

        public static string TurnBarText(int roundIndex, List<Unit> order, List<bool> alive, int turnIndex)
        {
            var parts = new List<string>();
            for (int i = 0; i < order.Count; i++)
            {
                if (!alive[i])
                {
                    continue;
                }
                string name = order[i].Data.displayName;
                if (i < turnIndex)
                {
                    parts.Add($"<color=#{ColorUtility.ToHtmlStringRGB(ActedColor)}>{name}</color>");
                }
                else if (i == turnIndex)
                {
                    parts.Add($"<b><color=#{ColorUtility.ToHtmlStringRGB(CurrentTurnColor)}>▶{name}</color></b>");
                }
                else
                {
                    parts.Add(name);
                }
            }
            return $"R{roundIndex}  {string.Join(" → ", parts)}";
        }

        private void SetCards(List<CardData> hand, int sp, int selected)
        {
            ClearHand();
            for (int i = 0; i < hand.Count; i++)
            {
                AddCard(hand[i], hand[i].spCost <= sp);
                _cards[i].SetSelected(i == selected);
            }
        }

        private void AddCard(CardData card, bool affordable)
        {
            var cardObject = new GameObject($"Card {card.id}", typeof(RectTransform));
            cardObject.transform.SetParent(_hand, false);
            var button = cardObject.AddComponent<CardButton>();
            button.Setup(card, _assets);
            button.SetAffordable(affordable);
            button.Interactable = _interactive;
            button.Clicked += OnCardClicked;
            button.DragBegan += _ => _dragging = true;
            button.Dragged += (_, position) => CardDragMoved?.Invoke(position);
            button.DragEnded += OnCardDragEnded;
            _cards.Add(button);
        }

        private void ClearHand()
        {
            foreach (CardButton card in _cards)
            {
                DestroySafe(card.gameObject);
            }
            _cards.Clear();
        }

        // 에디트 모드 테스트에서는 Destroy 를 쓸 수 없다.
        private static void DestroySafe(GameObject target)
        {
            if (Application.isPlaying)
            {
                Destroy(target);
            }
            else
            {
                DestroyImmediate(target);
            }
        }

        private void RefreshAffordable(int sp)
        {
            foreach (CardButton card in _cards)
            {
                card.SetAffordable(card.Card.spCost <= sp);
            }
        }

        private void OnCardClicked(CardButton button)
        {
            int index = _cards.IndexOf(button);
            ClearCardSelection();
            button.SetSelected(true);
            CardSelected?.Invoke(index);
        }

        private void OnCardDragEnded(CardButton button, Vector2 position)
        {
            if (!_dragging)
            {
                return;
            }
            _dragging = false;
            _dragEndFrame = Time.frameCount;
            CardDropped?.Invoke(_cards.IndexOf(button), position);
        }

        private void OnMoveClicked()
        {
            SetMoveMode(!_movePressed);
            MoveModeToggled?.Invoke(_movePressed);
        }

        private void RefreshSp(Unit actor)
        {
            bool visible = actor != null && actor.IsAlly;
            _spLabel.gameObject.SetActive(visible);
            if (!visible)
            {
                return;
            }
            int maxSp = ((AllyData)actor.Data).maxSp;
            string pips = new string('●', actor.Sp) + new string('○', Mathf.Max(maxSp - actor.Sp, 0));
            _spLabel.text = $"SP\n{pips}\n{actor.Sp} / {maxSp}";
        }

        private void ApplyInteractive()
        {
            _endTurnButton.interactable = _interactive;
            _moveButton.interactable = _interactive && _moveAvailable;
            foreach (CardButton card in _cards)
            {
                card.Interactable = _interactive;
            }
        }

        private ScrollRect BuildLog(RectTransform root)
        {
            RectTransform frame = Panel(root, "Log", new Color(0f, 0f, 0f, 0.35f));
            Anchor(frame, new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(16f, 260f), new Vector2(536f, -80f));
            var scroll = frame.gameObject.AddComponent<ScrollRect>();
            frame.gameObject.AddComponent<RectMask2D>();
            scroll.horizontal = false;

            _logLabel = Text(frame, "", 22f, TextAlignmentOptions.TopLeft);
            RectTransform content = _logLabel.rectTransform;
            content.anchorMin = new Vector2(0f, 1f);
            content.anchorMax = new Vector2(1f, 1f);
            content.pivot = new Vector2(0.5f, 1f);
            content.offsetMin = new Vector2(12f, 0f);
            content.offsetMax = new Vector2(-12f, 0f);
            var fitter = _logLabel.gameObject.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            scroll.content = content;
            return scroll;
        }

        private GameObject BuildBanner(RectTransform root)
        {
            RectTransform banner = Panel(root, "Banner", new Color(0f, 0f, 0f, 0.7f));
            Anchor(banner, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-320f, -140f), new Vector2(320f, 140f));
            _bannerLabel = Text(banner, "", 72f, TextAlignmentOptions.Center);
            Anchor(_bannerLabel.rectTransform, new Vector2(0f, 0.4f), new Vector2(1f, 1f), Vector2.zero, Vector2.zero);
            MakeButton(banner, "다시 하기", new Vector2(-120f, 20f), new Vector2(120f, 90f), () => RestartPressed?.Invoke(), new Vector2(0.5f, 0f));
            return banner.gameObject;
        }

        private Button MakeButton(RectTransform parent, string label, Vector2 min, Vector2 max, Action onClick, Vector2? anchor = null)
        {
            RectTransform rect = Panel(parent, label, ButtonColor);
            Vector2 a = anchor ?? new Vector2(1f, 0f);
            Anchor(rect, a, a, min, max);
            var button = rect.gameObject.AddComponent<Button>();
            button.onClick.AddListener(() => onClick());
            TextMeshProUGUI text = Text(rect, label, 28f, TextAlignmentOptions.Center);
            Anchor(text.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            return button;
        }

        private static RectTransform Panel(RectTransform parent, string name, Color color)
        {
            var panel = new GameObject(name, typeof(RectTransform), typeof(Image));
            var rect = (RectTransform)panel.transform;
            rect.SetParent(parent, false);
            panel.GetComponent<Image>().color = color;
            return rect;
        }

        private TextMeshProUGUI Text(RectTransform parent, string value, float size, TextAlignmentOptions alignment)
        {
            var textObject = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
            var rect = (RectTransform)textObject.transform;
            rect.SetParent(parent, false);
            var text = textObject.GetComponent<TextMeshProUGUI>();
            text.font = _assets.font;
            text.fontSize = size;
            text.alignment = alignment;
            text.richText = true;
            text.raycastTarget = false;
            text.text = value;
            return text;
        }

        private static void Anchor(RectTransform rect, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
        {
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = offsetMin;
            rect.offsetMax = offsetMax;
        }
    }
}
```
- [ ] **Step 4: 검증** — FILTER `ProjectVoid\.Tests\.BattleHudTests`. Expected: `PASSED 7 / FAILED 0`. (기호 `▶●○→` 는 Task 12 `FontSetupTests.SymbolsAreCovered` 가 확인했다. 그 테스트 때문에 기호를 바꿨다면 여기 코드와 기대 문자열도 같은 대체 기호를 쓴다.)

- [ ] **Step 5: Commit**

```bash
git add Assets/_Project
git commit -m "feat: add battle HUD with hand buttons, SP, log and banner

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 15: BattlePlayback · BattleRoot · Battle 씬 · PlayMode 스모크

**Files:**
- Create: `Assets/_Project/Scripts/View/BattlePlayback.cs`, `Assets/_Project/Scripts/View/BattleRoot.cs`
- Create: `Assets/_Project/Scripts/Editor/BattleSceneBuilder.cs`
- Create (생성물): `Assets/_Project/Scenes/Battle.unity`
- Test: `Assets/_Project/Tests/EditMode/PlaybackTests.cs`, `Assets/_Project/Tests/PlayMode/BattleSmokeTests.cs`

**Interfaces:**
- Consumes: Task 8~14 전부, `Assets/_Project/Data/Encounters/skirmish.asset`
- Produces:
  - `BattlePlayback : MonoBehaviour` — `Board3D Board`, `BattleHud Hud`, `bool Instant`, `IEnumerator Play(List<BattleEvent>)`, 대기 상수 (Godot 값)
  - `BattleRoot : MonoBehaviour` — `static bool ForceInstantPlayback`, `event Action<bool> BattleFinished`, `BattleState State`, `Board3D Board`, `BattleHud Hud`, `bool IsBusy`, `int SelectedCard`, 입력 처리 `OnCardSelected(int)`, `OnCardDropped(int, Vector2)`, `OnCardDragMoved(Vector2)`, `OnPickMissed()`, `OnCellClicked(Team, Vector2Int)`, `OnEndTurnPressed()`, `OnMoveModeToggled(bool)`, `Restart()`
  - `BattleSceneBuilder.Build()` (메뉴 `Project Void/Build Battle Scene`), `BattleSceneBuilder.ScenePath = "Assets/_Project/Scenes/Battle.unity"`

- [ ] **Step 1: 실패하는 테스트 작성**

`Tests/EditMode/PlaybackTests.cs` — 즉시 재생으로 한 판의 이벤트를 모두 재생해도 예외가 없고 화면이 규칙 상태와 맞는지:
```csharp
using NUnit.Framework;
using ProjectVoid.Combat;
using ProjectVoid.View;
using UnityEngine;

namespace ProjectVoid.Tests
{
    public class PlaybackTests
    {
        private GameObject _root;

        [TearDown]
        public void TearDown()
        {
            if (_root != null)
            {
                Object.DestroyImmediate(_root);
            }
            Make.Cleanup();
        }

        [Test]
        public void InstantPlaybackMirrorsTheRules()
        {
            var encounter = Make.Encounter(new Vector2Int(3, 3), new Vector2Int(2, 2),
                new[] { Make.Place(Make.Ally("a", maxHp: 30, speed: 9, deck: new[] { Make.Card("hit", damage: 50, attackRange: 5, attackType: AttackType.Ranged), Make.Card("hit2", damage: 50, attackRange: 5, attackType: AttackType.Ranged), Make.Card("hit3", damage: 50, attackRange: 5, attackType: AttackType.Ranged), Make.Card("hit4", damage: 50, attackRange: 5, attackType: AttackType.Ranged) }), 0, 1) },
                new[] { Make.Place(Make.Enemy("e", maxHp: 10, speed: 1), 0, 0) });
            BattleState state = Make.State(encounter, 8);
            var recorder = new BattleEventRecorder(state);

            _root = new GameObject("PlaybackTestRoot");
            var board = new GameObject("Board").AddComponent<Board3D>();
            board.transform.SetParent(_root.transform);
            board.Build(state, TestAssets.Load());
            var hud = new GameObject("Hud").AddComponent<BattleHud>();
            hud.transform.SetParent(_root.transform);
            hud.Build(TestAssets.Load());
            var playback = _root.AddComponent<BattlePlayback>();
            playback.Board = board;
            playback.Hud = hud;
            playback.Instant = true;

            state.StartBattle();
            Coroutines.Drain(playback.Play(recorder.TakeEvents()));
            Assert.AreEqual(4, hud.HandCount, "drawn cards appear");

            Assert.IsTrue(state.PlayCard(0, Team.Enemy, new Vector2Int(0, 0)));
            Coroutines.Drain(playback.Play(recorder.TakeEvents()));
            Unit enemy = state.Units[1];
            Assert.IsFalse(board.ViewFor(enemy).gameObject.activeSelf, "killed enemy hidden");
            Assert.IsTrue(hud.BannerVisible, "victory banner");
            StringAssert.Contains("전투 종료 — 승리", hud.LogText);
        }
    }
}
```

`Tests/PlayMode/BattleSmokeTests.cs`:
```csharp
using System.Collections;
using NUnit.Framework;
using ProjectVoid.Combat;
using ProjectVoid.View;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace ProjectVoid.Tests
{
    public class BattleSmokeTests
    {
        [UnitySetUp]
        public IEnumerator SetUp()
        {
            BattleRoot.ForceInstantPlayback = true;
            SceneManager.LoadScene("Battle");
            yield return null;
            yield return null;
        }

        [TearDown]
        public void TearDown()
        {
            BattleRoot.ForceInstantPlayback = false;
        }

        private static BattleRoot Root => Object.FindFirstObjectByType<BattleRoot>();

        private static IEnumerator WaitIdle(BattleRoot root)
        {
            for (int i = 0; i < 600 && root.IsBusy && !root.State.Finished; i++)
            {
                yield return null;
            }
        }

        [UnityTest]
        public IEnumerator PlaysSkirmishToTheEnd()
        {
            BattleRoot root = Root;
            yield return WaitIdle(root);
            for (int step = 0; step < 400 && !root.State.Finished; step++)
            {
                if (!TryPlayCard(root))
                {
                    root.OnEndTurnPressed();
                }
                yield return null;
                yield return WaitIdle(root);
            }
            Assert.IsTrue(root.State.Finished, "battle finishes");
            Assert.IsTrue(root.Hud.BannerVisible, "banner shown");
        }

        [UnityTest]
        public IEnumerator DropOnNothingCancelsSelection()
        {
            BattleRoot root = Root;
            yield return WaitIdle(root);
            Unit actor = root.State.CurrentUnit();
            int handBefore = actor.Hand.Count;
            root.OnCardDropped(0, new Vector2(-5000f, -5000f));
            yield return null;
            yield return null;
            Assert.AreEqual(-1, root.SelectedCard, "selection cleared");
            Assert.AreEqual(handBefore, actor.Hand.Count, "no card was used");
        }

        [UnityTest]
        public IEnumerator RestartStartsFreshBattle()
        {
            BattleRoot first = Root;
            yield return WaitIdle(first);
            first.OnEndTurnPressed();
            yield return WaitIdle(first);
            first.Restart();
            yield return null;
            yield return null;
            BattleRoot fresh = Root;
            Assert.AreNotSame(first, fresh, "scene reloaded");
            yield return WaitIdle(fresh);
            Assert.AreEqual(1, fresh.State.RoundIndex, "new battle starts at round 1");
        }

        private static bool TryPlayCard(BattleRoot root)
        {
            BattleState state = root.State;
            Unit actor = state.CurrentUnit();
            if (actor == null || !actor.IsAlly)
            {
                return false;
            }
            for (int i = 0; i < actor.Hand.Count; i++)
            {
                CardData card = actor.Hand[i];
                if (card.spCost > actor.Sp)
                {
                    continue;
                }
                Vector2Int grid = state.Resolver.EnemyGrid;
                for (int col = 0; col < grid.x; col++)
                {
                    for (int row = 0; row < grid.y; row++)
                    {
                        var cell = new Vector2Int(col, row);
                        if (!state.Resolver.IsValidCell(actor, Team.Enemy, cell, card.attackType, card.attackRange, state.Units)
                            || state.Resolver.ExpandShapeCell(Team.Enemy, cell, card.shape, state.Units).Count == 0)
                        {
                            continue;
                        }
                        root.OnCardSelected(i);
                        root.OnCellClicked(Team.Enemy, cell);
                        return true;
                    }
                }
            }
            return false;
        }
    }
}
```

- [ ] **Step 2: 검증 절차 1~2** — Expected: `BattlePlayback`/`BattleRoot` 없음 컴파일 에러.

- [ ] **Step 3: 구현**

`Scripts/View/BattlePlayback.cs` (GODOT `Scripts/view/battle_playback.gd` 1:1):
```csharp
using System.Collections;
using System.Collections.Generic;
using ProjectVoid.Combat;
using UnityEngine;

namespace ProjectVoid.View
{
    /// <summary>기록된 이벤트를 하나씩 연출한다. 연속된 피해·사망 이벤트는 유닛별로 묶어 동시에 재생한다 (광역 공격이 한꺼번에 보이게).</summary>
    public sealed class BattlePlayback : MonoBehaviour
    {
        public const float EnemyTurnPause = 0.2f;
        public const float DamageTime = 0.5f;
        public const float StatPopTime = 0.4f;
        public const float DrawWait = 0.12f;
        public const float ReshuffleWait = 0.45f;
        public const float DiscardWait = 0.35f;
        public const float PlayRemoveWait = 0.2f;
        private static readonly Color DamageColor = new Color(1f, 0.35f, 0.3f);
        private static readonly Color HealColor = new Color(0.45f, 0.9f, 0.45f);
        private static readonly Color BlockColor = new Color(0.5f, 0.7f, 1f);

        public Board3D Board;
        public BattleHud Hud;
        /// <summary>true 면 대기와 트윈 없이 상태만 반영한다 (테스트).</summary>
        public bool Instant;

        public IEnumerator Play(List<BattleEvent> events)
        {
            int i = 0;
            while (i < events.Count)
            {
                BattleEvent e = events[i];
                if (e.Kind == BattleEventKind.Damaged || e.Kind == BattleEventKind.Died)
                {
                    var batch = new List<BattleEvent>();
                    while (i < events.Count && (events[i].Kind == BattleEventKind.Damaged || events[i].Kind == BattleEventKind.Died))
                    {
                        batch.Add(events[i]);
                        i++;
                    }
                    yield return PlayDamageBatch(batch);
                    continue;
                }
                switch (e.Kind)
                {
                    case BattleEventKind.TurnStarted: yield return TurnStarted(e); break;
                    case BattleEventKind.CardPlayed: yield return CardPlayed(e); break;
                    case BattleEventKind.EnemyActed: yield return EnemyActed(e); break;
                    case BattleEventKind.Healed: yield return Healed(e); break;
                    case BattleEventKind.BlockGained: yield return BlockGained(e); break;
                    case BattleEventKind.Log: Hud.AppendLog(e.Text); break;
                    case BattleEventKind.BattleEnded:
                        Hud.ShowBanner(e.AllyWon);
                        Hud.AppendLog($"전투 종료 — {(e.AllyWon ? "승리" : "패배")}");
                        break;
                    case BattleEventKind.CardDrawn:
                        Hud.DrawCard(e);
                        yield return Wait(DrawWait);
                        break;
                    case BattleEventKind.DeckReshuffled:
                        Hud.Reshuffle(e);
                        yield return Wait(ReshuffleWait);
                        break;
                    case BattleEventKind.HandDiscarded:
                        Hud.DiscardHand(e);
                        yield return Wait(DiscardWait);
                        break;
                    case BattleEventKind.UnitMoved: yield return UnitMoved(e); break;
                }
                i++;
            }
        }

        private IEnumerator PlayDamageBatch(List<BattleEvent> batch)
        {
            var byUnit = new Dictionary<Unit, List<BattleEvent>>();
            var order = new List<Unit>();
            foreach (BattleEvent e in batch)
            {
                if (!byUnit.TryGetValue(e.Unit, out List<BattleEvent> list))
                {
                    list = new List<BattleEvent>();
                    byUnit[e.Unit] = list;
                    order.Add(e.Unit);
                }
                list.Add(e);
            }
            if (Instant)
            {
                foreach (Unit unit in order)
                {
                    yield return PlayUnitDamage(byUnit[unit]);
                }
                yield break;
            }
            int remaining = order.Count;
            foreach (Unit unit in order)
            {
                StartCoroutine(Countdown(PlayUnitDamage(byUnit[unit]), () => remaining--));
            }
            while (remaining > 0)
            {
                yield return null;
            }
        }

        private static IEnumerator Countdown(IEnumerator routine, System.Action done)
        {
            yield return routine;
            done();
        }

        private IEnumerator PlayUnitDamage(List<BattleEvent> unitEvents)
        {
            foreach (BattleEvent e in unitEvents)
            {
                yield return e.Kind == BattleEventKind.Damaged ? Damaged(e) : Died(e);
            }
        }

        private IEnumerator TurnStarted(BattleEvent e)
        {
            Board.ShowCurrent(e.Unit.Team, e.Cell);
            Board.ViewFor(e.Unit).SetStats(e.Hp, e.Unit.Data.maxHp, e.Block);
            Hud.ShowTurn(e);
            Hud.AppendLog($"― {e.Unit.Data.displayName} 차례");
            if (!e.Unit.IsAlly)
            {
                yield return Wait(EnemyTurnPause);
            }
        }

        private IEnumerator CardPlayed(BattleEvent e)
        {
            Hud.RemovePlayedCard(e);
            if (Instant)
            {
                yield break;
            }
            yield return Wait(PlayRemoveWait);
            UnitView view = Board.ViewFor(e.Unit);
            view.PopText(e.Card.displayName, Color.white);
            yield return view.LungeToward(Board.Layout.CellPosition(e.TargetTeam, e.TargetCell));
        }

        private IEnumerator EnemyActed(BattleEvent e)
        {
            if (Instant || e.Action == EnemyAction.Move)
            {
                yield break;
            }
            UnitView view = Board.ViewFor(e.Unit);
            if (e.Action == EnemyAction.Attack && e.Target != null)
            {
                yield return view.LungeToward(Board.ViewFor(e.Target).HomePosition);
            }
            else
            {
                yield return view.Hop();
            }
        }

        private IEnumerator UnitMoved(BattleEvent e)
        {
            Hud.ApplyMove(e);
            yield return Board.MoveView(e.Unit, e.FromCell, e.ToCell, !Instant);
        }

        private IEnumerator Damaged(BattleEvent e)
        {
            UnitView view = Board.ViewFor(e.Unit);
            view.SetStats(e.Hp, e.Unit.Data.maxHp, e.Block);
            Hud.AppendLog($"{e.Unit.Data.displayName} 에게 {e.Amount} 피해");
            if (Instant)
            {
                yield break;
            }
            view.PopText($"-{e.Amount}", DamageColor);
            yield return view.FlashAndShake();
            yield return Wait(DamageTime - UnitView.FlashTime);
        }

        private IEnumerator Healed(BattleEvent e)
        {
            UnitView view = Board.ViewFor(e.Unit);
            view.SetHp(e.Hp, e.Unit.Data.maxHp);
            if (Instant)
            {
                yield break;
            }
            view.PopText($"+{e.Amount}", HealColor);
            yield return Wait(StatPopTime);
        }

        private IEnumerator BlockGained(BattleEvent e)
        {
            UnitView view = Board.ViewFor(e.Unit);
            view.SetBlock(e.Block);
            if (Instant)
            {
                yield break;
            }
            view.PopText($"+{e.Amount} 방어", BlockColor);
            yield return Wait(StatPopTime);
        }

        private IEnumerator Died(BattleEvent e)
        {
            Board.MarkEmpty(e.Unit.Team, e.Cell);
            UnitView view = Board.ViewFor(e.Unit);
            if (Instant)
            {
                view.SetAlive(false);
                yield break;
            }
            yield return view.FadeOut();
        }

        private IEnumerator Wait(float seconds)
        {
            if (Instant || seconds <= 0f)
            {
                yield break;
            }
            yield return new WaitForSeconds(seconds);
        }
    }
}
```

`Scripts/View/BattleRoot.cs` (GODOT `Scripts/view/battle_root.gd` 1:1, 배경 이미지 제외):
```csharp
using System;
using System.Collections;
using System.Collections.Generic;
using ProjectVoid.Combat;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ProjectVoid.View
{
    /// <summary>
    /// 전투 씬의 루트. 규칙·기록기·보드·HUD·재생을 만들어 잇고, 입력을 규칙 호출로 바꾼 뒤 그동안 쌓인 이벤트를 재생한다.
    /// 재생 중에는 입력을 잠근다.
    /// </summary>
    public sealed class BattleRoot : MonoBehaviour
    {
        public const float CameraPitchDeg = 44f;
        public const float CameraFovDeg = 40f;
        public const float CameraMargin = 1.8f;
        // Godot (0, 0, 0.6) 은 카메라 쪽. 유니티에서는 카메라가 -Z 쪽이다.
        private static readonly Vector3 CameraTargetOffset = new Vector3(0f, 0f, -0.6f);

        /// <summary>테스트가 씬을 띄우기 전에 켜면 연출 대기 없이 재생한다.</summary>
        public static bool ForceInstantPlayback;

        [SerializeField] private EncounterData encounter;
        [SerializeField] private ViewAssets assets = new ViewAssets();
        [SerializeField] private Camera battleCamera;
        [Tooltip("0 이면 매 판 무작위 시드")]
        [SerializeField] private int seed;

        public event Action<bool> BattleFinished;

        private BattleEventRecorder _recorder;
        private BattlePlayback _playback;
        private int _selectedCard = -1;
        private bool _busy;
        private bool _awaitingDrop;
        private bool _moveMode;
        private Vector2Int _screenSize;

        public BattleState State { get; private set; }
        public Board3D Board { get; private set; }
        public BattleHud Hud { get; private set; }
        public bool IsBusy => _busy;
        public int SelectedCard => _selectedCard;

        private void Start()
        {
            State = new BattleState(encounter, new Rng(seed != 0 ? seed : Environment.TickCount));
            _recorder = new BattleEventRecorder(State);
            State.BattleEnded += won => BattleFinished?.Invoke(won);

            Board = new GameObject("Board").AddComponent<Board3D>();
            Board.transform.SetParent(transform, false);
            Board.Build(State, assets);
            Board.SyncFromState(State);

            Hud = new GameObject("Hud", typeof(RectTransform)).AddComponent<BattleHud>();
            Hud.transform.SetParent(transform, false);
            Hud.Build(assets);
            Board.UiDragActive = () => Hud.DragActiveThisFrame;

            _playback = gameObject.AddComponent<BattlePlayback>();
            _playback.Board = Board;
            _playback.Hud = Hud;
            _playback.Instant = ForceInstantPlayback;

            Board.CellClicked += OnCellClicked;
            Board.PickMissed += OnPickMissed;
            Board.CellHovered += OnCellHovered;
            Board.HoverCleared += OnHoverCleared;
            Hud.CardSelected += OnCardSelected;
            Hud.CardDropped += OnCardDropped;
            Hud.CardDragMoved += OnCardDragMoved;
            Hud.EndTurnPressed += OnEndTurnPressed;
            Hud.MoveModeToggled += OnMoveModeToggled;
            Hud.RestartPressed += Restart;

            FrameCamera();
            Run(State.StartBattle);
        }

        private void Update()
        {
            if (Screen.width != _screenSize.x || Screen.height != _screenSize.y)
            {
                FrameCamera();
            }
        }

        public void Restart()
        {
            SceneManager.LoadScene(gameObject.scene.buildIndex);
        }

        private void Run(Action action)
        {
            StartCoroutine(RunRoutine(action));
        }

        private IEnumerator RunRoutine(Action action)
        {
            SetBusy(true);
            action();
            yield return _playback.Play(_recorder.TakeEvents());
            Board.SyncFromState(State);
            Hud.SyncFromState(State, _selectedCard);
            SetBusy(State.Finished);
            if (_moveMode && MovableCellsNow().Count == 0)
            {
                _moveMode = false;
            }
            RefreshHints();
        }

        private void SetBusy(bool busy)
        {
            _busy = busy;
            if (busy)
            {
                _awaitingDrop = false;
                ClearSelection();
            }
            Board.InputEnabled = !busy;
            Hud.SetInteractive(!busy);
        }

        public void OnCardSelected(int index)
        {
            if (_busy)
            {
                return;
            }
            SelectCard(index);
            RefreshHints();
        }

        public void OnCardDropped(int index, Vector2 screenPosition)
        {
            if (_busy)
            {
                return;
            }
            SelectCard(index);
            RefreshHints();
            _awaitingDrop = true;
            Board.RequestPick(screenPosition);
        }

        public void OnCardDragMoved(Vector2 screenPosition)
        {
            Board.UpdatePointer(screenPosition);
        }

        public void OnPickMissed()
        {
            if (!_awaitingDrop)
            {
                return;
            }
            _awaitingDrop = false;
            ClearSelection();
        }

        public void OnCellClicked(Team team, Vector2Int cell)
        {
            bool fromDrop = _awaitingDrop;
            _awaitingDrop = false;
            if (_busy)
            {
                return;
            }
            if (_selectedCard < 0)
            {
                if (_moveMode && !fromDrop && team == Team.Ally)
                {
                    TryMove(cell);
                }
                return;
            }
            Unit actor = State.CurrentUnit();
            if (actor == null || team != Team.Enemy || _selectedCard >= actor.Hand.Count)
            {
                if (fromDrop)
                {
                    ClearSelection();
                }
                return;
            }
            CardData card = actor.Hand[_selectedCard];
            if (!State.Resolver.IsValidCell(actor, team, cell, card.attackType, card.attackRange, State.Units))
            {
                Hud.AppendLog("사용할 수 없는 위치");
                if (fromDrop)
                {
                    ClearSelection();
                }
                return;
            }
            int cardIndex = _selectedCard;
            Hud.SetPendingPlay(cardIndex);
            Run(() =>
            {
                if (!State.PlayCard(cardIndex, team, cell))
                {
                    Hud.AppendLog("사용할 수 없는 위치");
                }
            });
        }

        public void OnEndTurnPressed()
        {
            if (_busy)
            {
                return;
            }
            _moveMode = false;
            Run(State.EndTurn);
        }

        public void OnMoveModeToggled(bool on)
        {
            if (_busy)
            {
                Hud.SetMoveMode(_moveMode);
                return;
            }
            _moveMode = on;
            if (on)
            {
                ClearSelection();
                return;
            }
            RefreshHints();
        }

        private void SelectCard(int index)
        {
            _selectedCard = index;
            if (index < 0)
            {
                return;
            }
            _moveMode = false;
            Hud.SetMoveMode(false);
        }

        private void TryMove(Vector2Int cell)
        {
            if (!MovableCellsNow().Contains(cell))
            {
                return;
            }
            Run(() =>
            {
                if (!State.MoveUnit(cell))
                {
                    Hud.AppendLog("이동할 수 없는 칸");
                }
            });
        }

        private List<Vector2Int> MovableCellsNow()
        {
            Unit actor = State.CurrentUnit();
            if (_busy || State.Finished || actor == null || !actor.IsAlly || !actor.IsAlive || actor.Sp < 1)
            {
                return new List<Vector2Int>();
            }
            return State.Resolver.MovableCells(actor, State.Units);
        }

        private void RefreshHints()
        {
            List<Vector2Int> cells = MovableCellsNow();
            bool canMove = cells.Count > 0;
            Hud.SetMoveAvailable(canMove);
            Hud.SetMoveMode(_moveMode && canMove);
            if (_selectedCard >= 0)
            {
                if (Board.TryGetHover(out Team hoverTeam, out Vector2Int hoverCell))
                {
                    ApplyHoverPreview(hoverTeam, hoverCell);
                }
                else
                {
                    RefreshTargetHints();
                }
                return;
            }
            if (!_moveMode || !canMove)
            {
                Board.ClearTargetHints();
                return;
            }
            Board.ShowMoveHints(State.CurrentUnit().Team, cells);
        }

        private void ClearSelection()
        {
            _selectedCard = -1;
            Hud.ClearCardSelection();
            RefreshHints();
        }

        private void OnCellHovered(Team team, Vector2Int cell) => ApplyHoverPreview(team, cell);

        private void OnHoverCleared() => RefreshTargetHints();

        private void ApplyHoverPreview(Team team, Vector2Int cell)
        {
            RefreshTargetHints();
            if (_selectedCard < 0 || team != Team.Enemy)
            {
                return;
            }
            Unit actor = State.CurrentUnit();
            if (actor == null || !actor.IsAlly || _selectedCard >= actor.Hand.Count)
            {
                return;
            }
            CardData card = actor.Hand[_selectedCard];
            var hits = new Dictionary<Vector2Int, CellHint>();
            foreach (Vector2Int shapeCell in State.Resolver.ShapeCells(cell, card.shape, State.Resolver.EnemyGrid))
            {
                bool valid = State.Resolver.IsValidCell(actor, team, shapeCell, card.attackType, card.attackRange, State.Units);
                hits[shapeCell] = new CellHint(valid, HintText(State.Resolver.ReachCell(actor, team, shapeCell), card.attackRange, valid));
            }
            Board.ShowShapePreview(team, hits);
        }

        private void RefreshTargetHints()
        {
            Unit actor = State.CurrentUnit();
            if (_selectedCard < 0 || actor == null || !actor.IsAlly || _selectedCard >= actor.Hand.Count)
            {
                Board.ClearTargetHints();
                return;
            }
            CardData card = actor.Hand[_selectedCard];
            Vector2Int grid = State.Resolver.EnemyGrid;
            var hints = new Dictionary<Vector2Int, CellHint>();
            for (int row = 0; row < grid.y; row++)
            {
                for (int col = 0; col < grid.x; col++)
                {
                    var cell = new Vector2Int(col, row);
                    bool valid = State.Resolver.IsValidCell(actor, Team.Enemy, cell, card.attackType, card.attackRange, State.Units);
                    hints[cell] = new CellHint(valid, HintText(State.Resolver.ReachCell(actor, Team.Enemy, cell), card.attackRange, valid));
                }
            }
            Board.ShowTargetHints(Team.Enemy, hints);
        }

        private static string HintText(int distance, int attackRange, bool valid)
        {
            if (valid)
            {
                return $"✓ 거리 {distance}";
            }
            return distance > attackRange ? $"거리 {distance}" : "막힘";
        }

        private void FrameCamera()
        {
            _screenSize = new Vector2Int(Screen.width, Screen.height);
            BoardLayout layout = Board.Layout;
            float aspect = Screen.width / Mathf.Max(Screen.height, 1f);
            float distance = BoardLayout.CameraDistance(layout.Width(), layout.Depth(), CameraFovDeg, aspect, CameraMargin);
            Vector3 target = layout.Center() + CameraTargetOffset;
            float pitch = CameraPitchDeg * Mathf.Deg2Rad;
            battleCamera.fieldOfView = CameraFovDeg;
            battleCamera.transform.position = target + new Vector3(0f, Mathf.Sin(pitch) * distance, -Mathf.Cos(pitch) * distance);
            battleCamera.transform.LookAt(target, Vector3.up);
        }
    }
}
```
(`✓` 는 Task 12 `SymbolsAreCovered` 가 확인한다.)

`Scripts/Editor/BattleSceneBuilder.cs`:
```csharp
using System.Collections.Generic;
using ProjectVoid.Combat;
using ProjectVoid.View;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;

namespace ProjectVoid.EditorTools
{
    /// <summary>Battle 씬을 코드로 만든다 (카메라, 조명, EventSystem, BattleRoot + 참조 연결) 그리고 빌드 목록 첫 칸에 넣는다.</summary>
    public static class BattleSceneBuilder
    {
        public const string ScenePath = "Assets/_Project/Scenes/Battle.unity";

        [MenuItem("Project Void/Build Battle Scene")]
        public static void Build()
        {
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var cameraObject = new GameObject("Main Camera") { tag = "MainCamera" };
            var camera = cameraObject.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.08f, 0.08f, 0.11f);
            cameraObject.AddComponent<AudioListener>();

            var lightObject = new GameObject("Directional Light");
            var light = lightObject.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.2f;
            lightObject.transform.rotation = Quaternion.Euler(50f, -30f, 0f);

            new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));

            var rootObject = new GameObject("BattleRoot");
            var root = rootObject.AddComponent<BattleRoot>();
            var serialized = new SerializedObject(root);
            serialized.FindProperty("encounter").objectReferenceValue =
                AssetDatabase.LoadAssetAtPath<EncounterData>($"{DataImporter.EncountersDir}/skirmish.asset");
            serialized.FindProperty("battleCamera").objectReferenceValue = camera;
            SerializedProperty assets = serialized.FindProperty("assets");
            assets.FindPropertyRelative("font").objectReferenceValue = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontSetup.FontAssetPath);
            assets.FindPropertyRelative("overlayTextMaterial").objectReferenceValue = AssetDatabase.LoadAssetAtPath<Material>(FontSetup.OverlayMaterialPath);
            assets.FindPropertyRelative("tileMaterial").objectReferenceValue = AssetDatabase.LoadAssetAtPath<Material>(FontSetup.TileMaterialPath);
            assets.FindPropertyRelative("placeholderSprite").objectReferenceValue =
                AssetDatabase.LoadAssetAtPath<Sprite>($"{PixelSpriteProcessor.OutDir}/placeholder_unit.png");
            serialized.ApplyModifiedPropertiesWithoutUndo();

            System.IO.Directory.CreateDirectory("Assets/_Project/Scenes");
            EditorSceneManager.SaveScene(scene, ScenePath);

            var scenes = new List<EditorBuildSettingsScene> { new EditorBuildSettingsScene(ScenePath, true) };
            foreach (EditorBuildSettingsScene existing in EditorBuildSettings.scenes)
            {
                if (existing.path != ScenePath)
                {
                    scenes.Add(existing);
                }
            }
            EditorBuildSettings.scenes = scenes.ToArray();
        }
    }
}
```
(`ProjectVoid.Editor.asmdef` 는 이미 `Unity.InputSystem`, `UnityEngine.UI` 를 참조한다.)

- [ ] **Step 4: 씬 생성** — 검증 절차 1~2 로 컴파일 후 `Unity_RunCommand` 로 `ProjectVoid.EditorTools.BattleSceneBuilder.Build();`. 열려 있던 SampleScene 에 저장 안 된 변경이 있으면 먼저 사용자에게 묻는다 (`NewScene` 이 현재 씬을 닫는다).

- [ ] **Step 5: EditMode 검증** — FILTER `ProjectVoid\.Tests\.PlaybackTests`. Expected: `PASSED 1 / FAILED 0`.

- [ ] **Step 6: PlayMode 검증** — `Unity_RunCommand` 로 `ProjectVoid.Tests.TestBridge.Run(TestMode.PlayMode, "");` 실행. 플레이 모드 진입·도메인 리로드가 끝날 때까지 기다린 뒤 (`Temp/ProjectVoidTestResults.txt` 가 생길 때까지 몇 초 간격으로 Read) 결과 확인. Expected: `PASSED 3 / FAILED 0`.

- [ ] **Step 7: Commit**

```bash
git add Assets/_Project ProjectSettings/EditorBuildSettings.asset
git commit -m "feat: wire battle root, playback and Battle scene

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 16: 실제 화면 확인 · 마무리

**Files:**
- Modify (필요 시): `UnitView.cs` / `Board3D.cs` / `BattleHud.cs` 의 글자 크기·위치 상수만

- [ ] **Step 1: 전체 테스트** — 검증 절차 3~4, EditMode FILTER `""` 그리고 PlayMode FILTER `""`. Expected: 모두 통과.

- [ ] **Step 2: 플레이 모드로 화면 캡처** — `Unity_RunCommand` 로 `EditorSceneManager.OpenScene(BattleSceneBuilder.ScenePath); EditorApplication.isPlaying = true;` → 5초 뒤 `Unity_RunCommand` 로 `Camera.main.gameObject.GetInstanceID()` 를 얻어 `Unity_Camera_Capture` 로 캡처. (주의: `Unity_Camera_Capture` 는 카메라 렌더만 찍으므로 Screen Space Overlay HUD 는 보이지 않는다. HUD 확인은 사용자에게 에디터 Game 뷰를 봐 달라고 하거나, `Unity_RunCommand` 로 `ScreenCapture.CaptureScreenshot("Temp/battle.png")` 후 다음 프레임에 Read.)

- [ ] **Step 3: 확인 항목**
  - 보드 전체가 화면 안, 아군 왼쪽·적 오른쪽, 스프라이트가 발로 타일 위에 서 있고 흐리지 않음(Point)
  - 머리 위 이름·HP 바·수치가 읽히는 크기 (너무 크거나 작으면 `MakeLabel` 글자 크기 상수 조정)
  - 한글이 네모(□)로 나오지 않음
  - HUD 위 클릭이 보드로 새지 않음 (Review Focus 1: 사용자에게 카드 버튼 클릭 → 적 타일 클릭, 차례 종료 버튼 클릭 시 뒤 타일 반응 없음을 직접 확인 요청)
  - `Unity_GetConsoleLogs` (`logTypes: "Error,Warning"`) 에 이 프로젝트 코드에서 난 에러 0건

- [ ] **Step 4: 플레이 모드 종료** — `EditorApplication.isPlaying = false;`

- [ ] **Step 5: 리뷰 요청** — `superpowers:requesting-code-review` 스킬로 1단계 전체 리뷰.

- [ ] **Step 6: Commit** (조정한 것이 있으면)

```bash
git add Assets/_Project
git commit -m "chore: tune battle view label sizes after visual check

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```
