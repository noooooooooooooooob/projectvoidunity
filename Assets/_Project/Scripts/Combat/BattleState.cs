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
