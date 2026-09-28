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
