using UnityEngine;

namespace ProjectVoid.View
{
    /// <summary>
    /// 전투 카메라의 타격 연출. 기본 구도(SetBase) 위에 푸시인·흔들림을 더하고, 히트스톱으로 시간을 잠깐 멈춘다.
    /// 히트스톱 중에도 움직여야 하므로 실제 시간(unscaled)으로 돈다.
    /// </summary>
    public sealed class BattleCamera : MonoBehaviour
    {
        // 타격 지점까지 거리 중 이만큼만 다가간다. 보드 전체가 화면에서 벗어나지 않을 정도.
        public const float PushFraction = 0.12f;
        public const float PushSharpness = 8f;
        public const float MaxShakeOffset = 0.25f;
        // 초당 줄어드는 흔들림 세기. 1 이 0.4초 만에 사라진다.
        public const float ShakeDecay = 2.5f;
        public const float HitStopScale = 0.05f;
        public const float SlowMoScale = 0.3f;
        // 처치 슬로모션을 실제 시간으로 유지하는 길이.
        public const float SlowMoTime = 0.35f;
        public const int ShakeDamageCap = 10;
        private const float ShakeBase = 0.2f;
        private const float ShakePerDamage = 0.7f;
        private const float KillShake = 1f;
        private const float SettleEpsilon = 1e-5f;

        private Vector3 _basePosition;
        private Quaternion _baseRotation = Quaternion.identity;
        private Vector3 _push;
        private Vector3 _pushTarget;
        private float _trauma;
        private float _hitStopLeft;
        // HitStop 을 건 프레임의 시간은 이미 흘러간 것이라 멈춤 시간에서 빼지 않는다.
        private bool _hitStopJustStarted;
        private bool _slowMoPending;
        private float _slowMoLeft;

        /// <summary>피해량에 비례하되 상한이 있는 흔들림 세기 (0..1). 처치는 항상 가장 세다.</summary>
        public static float ShakeForDamage(int amount, bool died)
        {
            if (died)
            {
                return KillShake;
            }
            return ShakeBase + ShakePerDamage * Mathf.Min(amount, ShakeDamageCap) / ShakeDamageCap;
        }

        public void SetBase(Vector3 position, Quaternion rotation)
        {
            _basePosition = position;
            _baseRotation = rotation;
            Apply(Vector3.zero);
        }

        public void PushToward(Vector3 point)
        {
            _pushTarget = (point - _basePosition) * PushFraction;
        }

        public void Release()
        {
            _pushTarget = Vector3.zero;
        }

        public void Shake(float strength)
        {
            _trauma = Mathf.Clamp01(Mathf.Max(_trauma, strength));
        }

        public void HitStop(float seconds)
        {
            _hitStopLeft = Mathf.Max(_hitStopLeft, seconds);
            _hitStopJustStarted = true;
            Time.timeScale = HitStopScale;
        }

        /// <summary>처치 연출: 히트스톱이 끝나면(없으면 바로) 잠깐 느리게 흐른다.</summary>
        public void KillSlowMo()
        {
            _slowMoPending = true;
        }

        public void Tick(float unscaledDeltaTime)
        {
            if (_hitStopJustStarted)
            {
                _hitStopJustStarted = false;
            }
            else if (_hitStopLeft > 0f)
            {
                _hitStopLeft -= unscaledDeltaTime;
                if (_hitStopLeft <= 0f)
                {
                    _hitStopLeft = 0f;
                    Time.timeScale = _slowMoLeft > 0f ? SlowMoScale : 1f;
                }
            }
            if (_hitStopLeft <= 0f)
            {
                if (_slowMoPending)
                {
                    _slowMoPending = false;
                    _slowMoLeft = SlowMoTime;
                    Time.timeScale = SlowMoScale;
                }
                else if (_slowMoLeft > 0f)
                {
                    _slowMoLeft -= unscaledDeltaTime;
                    if (_slowMoLeft <= 0f)
                    {
                        _slowMoLeft = 0f;
                        Time.timeScale = 1f;
                    }
                }
            }

            _push = Vector3.Lerp(_push, _pushTarget, 1f - Mathf.Exp(-PushSharpness * unscaledDeltaTime));
            if ((_push - _pushTarget).sqrMagnitude < SettleEpsilon * SettleEpsilon)
            {
                _push = _pushTarget;
            }

            Vector3 shake = Vector3.zero;
            if (_trauma > 0f)
            {
                _trauma = Mathf.Max(0f, _trauma - ShakeDecay * unscaledDeltaTime);
                // 제곱해야 약한 타격은 잔잔하고 센 타격만 크게 튄다.
                Vector2 direction = Random.insideUnitCircle.normalized;
                shake = _baseRotation * new Vector3(direction.x, direction.y, 0f) * (MaxShakeOffset * _trauma * _trauma);
            }
            Apply(_push + shake);
        }

        private void Apply(Vector3 offset)
        {
            transform.SetPositionAndRotation(_basePosition + offset, _baseRotation);
        }

        private void LateUpdate()
        {
            Tick(Time.unscaledDeltaTime);
        }

        private void OnDisable()
        {
            if (_hitStopLeft > 0f || _slowMoLeft > 0f || _slowMoPending)
            {
                _hitStopLeft = 0f;
                _slowMoLeft = 0f;
                _slowMoPending = false;
                Time.timeScale = 1f;
            }
        }
    }
}
