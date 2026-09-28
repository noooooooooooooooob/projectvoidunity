using System.Collections.Generic;
using NUnit.Framework;
using ProjectVoid.Combat;
using UnityEngine;

namespace ProjectVoid.Tests
{
    public class TurnPhasesTests
    {
        [TearDown]
        public void TearDown() => Make.Cleanup();

        // 원본 _state: 아군 a(체력 30, 속도 10, SP 3, 카드 6장) vs 적 e(체력 20, 속도 1, 이동 0, (0,1)), 시드 11.
        private static BattleState NewState(Vector2Int allyCell)
        {
            var deck = new CardData[6];
            for (int i = 0; i < 6; i++)
            {
                deck[i] = Make.Card($"c{i}");
            }
            EncounterData encounter = Make.Encounter(new Vector2Int(3, 3), new Vector2Int(3, 3),
                new[] { new UnitPlacement(Make.Ally("a", maxHp: 30, speed: 10, maxSp: 3, deck: deck), allyCell) },
                new[] { Make.Place(Make.Enemy("e", maxHp: 20, speed: 1, moveChance: 0f), 0, 1) });
            return Make.State(encounter, 11);
        }

        // 원본은 Godot enum 키(STANDBY 등)를 썼다. 여기서는 C# 이름(Standby 등)이 기록된다.
        private static List<string> Record(BattleState state)
        {
            var seen = new List<string>();
            state.PhaseStarted += (unit, phase) => seen.Add($"phase:{unit.Data.id}:{phase}");
            state.TurnStarted += unit => seen.Add($"turn:{unit.Data.id}");
            state.CardDrawn += (unit, card, deck, discard) => seen.Add($"drawn:{unit.Data.id}");
            state.HandDiscarded += (unit, cards, discard) => seen.Add($"discarded:{unit.Data.id}");
            return seen;
        }

        [Test]
        public void AllyTurnStartOrder()
        {
            BattleState state = NewState(new Vector2Int(0, 1));
            List<string> seen = Record(state);
            state.StartBattle();
            CollectionAssert.AreEqual(new[]
            {
                "phase:a:Standby", "turn:a", "phase:a:Draw", "drawn:a", "drawn:a", "drawn:a", "drawn:a", "phase:a:Action",
            }, seen, "ally turn start phases");
            Assert.AreEqual(Phase.Action, state.CurrentPhase, "waiting in the action phase");
        }

        [Test]
        public void EndTurnRunsEndPhasesThenEnemyTurn()
        {
            BattleState state = NewState(new Vector2Int(0, 1));
            state.StartBattle();
            List<string> seen = Record(state);
            state.EndTurn();
            CollectionAssert.AreEqual(new[]
            {
                "phase:a:BeforeEnd", "phase:a:AfterEnd", "discarded:a",
                "phase:e:Standby", "turn:e", "phase:e:Draw", "phase:e:Action", "phase:e:BeforeEnd", "phase:e:AfterEnd",
                "phase:a:Standby", "turn:a", "phase:a:Draw", "drawn:a", "drawn:a", "drawn:a", "drawn:a", "phase:a:Action",
            }, seen, "end phases, enemy phases, next ally start");
        }

        [Test]
        public void StandbyResetsBeforeTurnStarted()
        {
            BattleState state = NewState(new Vector2Int(2, 1));
            state.StartBattle();
            Unit ally = state.LivingUnits(Team.Ally)[0];
            ally.Block = 7;
            ally.Sp = 0;
            var seen = new Dictionary<string, int[]>();
            state.PhaseStarted += (unit, phase) =>
            {
                if (unit == ally && phase == Phase.Standby)
                {
                    seen["standby"] = new[] { unit.Block, unit.Sp };
                }
            };
            state.TurnStarted += unit =>
            {
                if (unit == ally)
                {
                    seen["turn"] = new[] { unit.Block, unit.Sp };
                }
            };
            state.EndTurn();
            Assert.AreEqual(7, seen["standby"][0], "block still old at standby signal");
            Assert.AreEqual(0, seen["standby"][1], "sp still old at standby signal");
            Assert.AreEqual(0, seen["turn"][0], "block reset by turn_started");
            Assert.AreEqual(3, seen["turn"][1], "sp refilled by turn_started");
        }

        [Test]
        public void BattleEndSkipsEndPhases()
        {
            BattleState state = NewState(new Vector2Int(0, 1));
            state.StartBattle();
            state.LivingUnits(Team.Ally)[0].TakeDamage(29);
            List<string> seen = Record(state);
            state.EndTurn();
            CollectionAssert.AreEqual(new[]
            {
                "phase:a:BeforeEnd", "phase:a:AfterEnd", "discarded:a", "phase:e:Standby", "turn:e", "phase:e:Draw", "phase:e:Action",
            }, seen, "stops after the enemy action phase");
            Assert.IsTrue(state.Finished, "battle finished");
        }
    }
}
