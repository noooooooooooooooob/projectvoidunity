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
        public const float HitStopTime = 0.06f;
        public const float KillHitStopTime = 0.1f;
        public const float MaxKnockback = 0.35f;
        // 원거리는 앞으로 나가지 않고 쏘는 순간 살짝 뒤로 물러난다.
        public const float RangedRecoil = -0.1f;
        // 이 이상 피해면 화면 펄스를 약하게 준다.
        public const int BigHitDamage = 8;
        private const float BigHitPulse = 0.5f;
        private const float ChestHeight = UnitView.SpriteHeight * 0.55f;
        private static readonly Color DamageColor = new Color(1f, 0.35f, 0.3f);
        private static readonly Color HealColor = new Color(0.45f, 0.9f, 0.45f);
        private static readonly Color BlockColor = new Color(0.5f, 0.7f, 1f);

        public Board3D Board;
        public BattleHud Hud;
        /// <summary>없으면 카메라 연출 없이 재생한다.</summary>
        public BattleCamera CameraFx;
        /// <summary>없으면 소리 없이 재생한다.</summary>
        public BattleAudio Audio;
        /// <summary>없으면 화면 펄스 없이 재생한다.</summary>
        public ScreenPulse ScreenFx;
        /// <summary>원거리 화살. 머티리얼이 없으면 화살 없이 바로 맞는다.</summary>
        public Sprite ProjectileSprite;
        public Material ProjectileMaterial;
        /// <summary>true 면 대기와 트윈 없이 상태만 반영한다 (테스트).</summary>
        public bool Instant;

        // 지금 재생 중인 타격 묶음을 만든 공격의 종류. 공격 없이 나온 피해는 근접으로 친다.
        private AttackType _impactType = AttackType.Melee;
        // 타격 묶음을 만든 공격자의 자리. 넉백 방향을 정한다. 공격 없이 나온 피해는 넉백하지 않는다.
        private Vector3? _impactOrigin;

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
                    ReleaseCamera();
                    continue;
                }
                if (!KeepsCameraPush(e.Kind))
                {
                    ReleaseCamera();
                }
                switch (e.Kind)
                {
                    case BattleEventKind.TurnStarted: yield return TurnStarted(e); break;
                    case BattleEventKind.CardPlayed:
                    case BattleEventKind.EnemyActed:
                        int impact = !Instant && IsAttack(e) ? ImpactStart(events, i) : -1;
                        if (impact < 0)
                        {
                            yield return e.Kind == BattleEventKind.CardPlayed ? CardPlayed(e, null, null) : EnemyActed(e, null, null);
                            break;
                        }
                        // 돌진과 겹쳐 무기가 닿는 순간에 피해 묶음을 재생한다.
                        int end = impact;
                        while (end < events.Count && IsDamage(events[end].Kind))
                        {
                            end++;
                        }
                        List<BattleEvent> logs = events.GetRange(i + 1, impact - i - 1);
                        List<BattleEvent> hits = events.GetRange(impact, end - impact);
                        yield return e.Kind == BattleEventKind.CardPlayed ? CardPlayed(e, logs, hits) : EnemyActed(e, logs, hits);
                        ReleaseCamera();
                        i = end;
                        continue;
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
            ReleaseCamera();
        }

        /// <summary>돌진한 공격의 결과(피해·사망과 그 사이 로그)만 푸시인을 유지한다. 그 밖의 이벤트 전에는 구도를 되돌린다.</summary>
        public static bool KeepsCameraPush(BattleEventKind kind) =>
            kind == BattleEventKind.Damaged || kind == BattleEventKind.Died || kind == BattleEventKind.Log;

        /// <summary>공격 이벤트 뒤(로그는 건너뜀)에 이어지는 피해·사망 묶음의 첫 인덱스. 다른 이벤트가 끼거나 없으면 -1.</summary>
        public static int ImpactStart(List<BattleEvent> events, int attackIndex)
        {
            for (int i = attackIndex + 1; i < events.Count; i++)
            {
                BattleEventKind kind = events[i].Kind;
                if (IsDamage(kind))
                {
                    return i;
                }
                if (kind != BattleEventKind.Log)
                {
                    return -1;
                }
            }
            return -1;
        }

        /// <summary>피해량에 비례해 밀리는 거리. 막힌(0) 공격은 밀지 않는다.</summary>
        /// <summary>원거리 발사음은 화살이 나가는 순간에 낸다. 근접 휘두르기는 예비동작부터 들려야 타격을 이끈다.</summary>
        public static bool AttackSoundAtStrike(AttackType type) => type == AttackType.Ranged;

        public static float KnockbackDistance(int amount)
            => amount <= 0 ? 0f : Mathf.Min(0.1f + 0.03f * amount, MaxKnockback);

        private static bool IsDamage(BattleEventKind kind) => kind == BattleEventKind.Damaged || kind == BattleEventKind.Died;

        private static bool IsAttack(BattleEvent e)
            => e.Kind == BattleEventKind.CardPlayed || (e.Action == EnemyAction.Attack && e.Target != null);

        private static AttackType AttackTypeOf(Unit unit) => unit.Data is EnemyData enemy ? enemy.attackType : AttackType.Melee;

        private bool HasScreenFx => ScreenFx != null && !Instant;

        private Vector3 Knockback(UnitView view, int amount)
        {
            if (!_impactOrigin.HasValue)
            {
                return Vector3.zero;
            }
            Vector3 away = view.HomePosition - _impactOrigin.Value;
            away.y = 0f;
            return away.sqrMagnitude > 0f ? away.normalized * KnockbackDistance(amount) : Vector3.zero;
        }

        private bool HasAudio => Audio != null && Audio.Sounds != null && !Instant;

        private bool HasCameraFx => CameraFx != null && !Instant;

        private void ReleaseCamera()
        {
            if (HasCameraFx)
            {
                CameraFx.Release();
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

        // logs·hits 가 있으면 돌진의 타격 순간에 재생한다 (없으면 돌진만).
        private IEnumerator CardPlayed(BattleEvent e, List<BattleEvent> logs, List<BattleEvent> hits)
        {
            Hud.RemovePlayedCard(e);
            if (Instant)
            {
                yield break;
            }
            yield return Wait(PlayRemoveWait);
            UnitView view = Board.ViewFor(e.Unit);
            view.PopText(e.Card.displayName, Color.white);
            yield return Attack(view, Board.CellWorldPosition(e.TargetTeam, e.TargetCell), e.Card.attackType, logs, hits);
        }

        private IEnumerator EnemyActed(BattleEvent e, List<BattleEvent> logs, List<BattleEvent> hits)
        {
            if (Instant || e.Action == EnemyAction.Move)
            {
                yield break;
            }
            UnitView view = Board.ViewFor(e.Unit);
            if (e.Action == EnemyAction.Attack && e.Target != null)
            {
                yield return Attack(view, Board.ViewFor(e.Target).HomePosition, AttackTypeOf(e.Unit), logs, hits);
            }
            else
            {
                yield return view.Hop();
            }
        }

        private IEnumerator Attack(UnitView view, Vector3 target, AttackType type, List<BattleEvent> logs, List<BattleEvent> hits)
        {
            if (HasCameraFx)
            {
                CameraFx.PushToward(target);
            }
            if (HasAudio && !AttackSoundAtStrike(type))
            {
                Audio.Play(Audio.Sounds.ForAttack(type));
            }
            bool ranged = type == AttackType.Ranged;
            bool lunging = true;
            StartCoroutine(Countdown(view.LungeToward(target, ranged ? RangedRecoil : UnitView.LungeDistance), () => lunging = false));
            yield return Wait(UnitMotion.AttackStrike * view.AttackDuration);
            if (HasAudio && AttackSoundAtStrike(type))
            {
                Audio.Play(Audio.Sounds.ForAttack(type));
            }
            if (ranged && ProjectileMaterial != null)
            {
                Vector3 chest = Vector3.up * ChestHeight;
                yield return Projectile.Spawn(ProjectileSprite, ProjectileMaterial, view.HomePosition + chest, target + chest).Fly();
            }
            if (hits != null)
            {
                foreach (BattleEvent log in logs)
                {
                    Hud.AppendLog(log.Text);
                }
                _impactType = type;
                _impactOrigin = view.HomePosition;
                yield return PlayDamageBatch(hits);
                _impactType = AttackType.Melee;
                _impactOrigin = null;
            }
            while (lunging)
            {
                yield return null;
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
            view.PopText($"-{e.Amount}", DamageColor, e.Hp <= 0 ? UnitView.KillPopPunch : UnitView.DamagePopPunch);
            if (HasAudio)
            {
                Audio.Play(Audio.Sounds.ForImpact(_impactType, e.Amount, false));
            }
            if (HasCameraFx)
            {
                CameraFx.HitStop(HitStopTime);
                CameraFx.Shake(BattleCamera.ShakeForDamage(e.Amount, false));
            }
            if (HasScreenFx && e.Amount >= BigHitDamage)
            {
                ScreenFx.Pulse(BigHitPulse);
            }
            yield return view.FlashAndShake(Knockback(view, e.Amount));
            yield return Wait(Mathf.Max(0f, DamageTime - view.HitDuration));
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
            if (HasCameraFx)
            {
                CameraFx.HitStop(KillHitStopTime);
                CameraFx.Shake(BattleCamera.ShakeForDamage(0, true));
                CameraFx.KillSlowMo();
            }
            if (HasScreenFx)
            {
                ScreenFx.Pulse(1f);
            }
            if (HasAudio)
            {
                Audio.Play(Audio.Sounds.ForImpact(_impactType, 0, true));
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
