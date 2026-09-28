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
