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
