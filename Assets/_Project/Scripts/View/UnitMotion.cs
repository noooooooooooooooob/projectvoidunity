using UnityEngine;

namespace ProjectVoid.View
{
    /// <summary>
    /// 한 장짜리 스프라이트에 생기를 주는 자세 곡선. 진행률 t(0→1)를 넣으면 자세가 나온다.
    /// 자세는 발을 축으로 한 늘이기/누르기(stretch)와 기울기(lean, 양수 = 바라보는 방향의 뒤쪽)뿐이다.
    /// </summary>
    public static class UnitMotion
    {
        public struct Pose
        {
            public float stretch;
            public float lean;
        }

        public const float IdlePeriod = 1.6f;
        public const float IdleStretch = 0.03f;
        public const float DeathLean = 85f;

        public const float AttackWindupEnd = 0.3f;
        public const float AttackStrike = 0.5f;
        public const float HopCrouch = 0.2f;
        public const float HopPeak = 0.55f;
        public const float HopLand = 0.9f;
        public const float HitImpact = 0.15f;

        private static readonly float[] AttackTimes = { 0f, AttackWindupEnd, AttackStrike, 0.75f, 1f };
        private static readonly float[] AttackStretch = { 0f, -0.12f, 0.12f, -0.03f, 0f };
        private static readonly float[] AttackLean = { 0f, 8f, -10f, 2f, 0f };
        private static readonly float[] LungeTimes = { 0f, AttackWindupEnd, AttackStrike, 1f };
        private static readonly float[] LungeValues = { 0f, 0f, 1f, 0f };

        private static readonly float[] HopTimes = { 0f, HopCrouch, 0.35f, HopPeak, 0.85f, HopLand, 1f };
        private static readonly float[] HopStretch = { 0f, -0.15f, 0.1f, 0.03f, 0.02f, -0.12f, 0f };
        private static readonly float[] HopHeightTimes = { 0f, HopCrouch, HopPeak, 0.85f, 1f };
        private static readonly float[] HopHeightValues = { 0f, 0f, 0.25f, 0f, 0f };

        private static readonly float[] HitTimes = { 0f, HitImpact, 0.5f, 1f };
        private static readonly float[] HitStretch = { 0f, -0.1f, 0.03f, 0f };
        private static readonly float[] HitLean = { 0f, 12f, -3f, 0f };

        public static Pose Idle(float time, float phase)
            => new Pose { stretch = IdleStretch * Mathf.Sin(2f * Mathf.PI * time / IdlePeriod + phase) };

        public static Pose Attack(float t)
            => new Pose { stretch = Eval(AttackTimes, AttackStretch, t), lean = Eval(AttackTimes, AttackLean, t) };

        /// <summary>공격할 때 몸이 앞으로 나간 정도 (0 = 제자리, 1 = 끝까지).</summary>
        public static float LungeReach(float t) => Eval(LungeTimes, LungeValues, t);

        public static Pose Hop(float t) => new Pose { stretch = Eval(HopTimes, HopStretch, t) };

        public static float HopHeight(float t) => Eval(HopHeightTimes, HopHeightValues, t);

        public static Pose Hit(float t)
            => new Pose { stretch = Eval(HitTimes, HitStretch, t), lean = Eval(HitTimes, HitLean, t) };

        // 처음엔 천천히, 끝에서 빠르게 넘어간다.
        public static Pose Death(float t) => new Pose { lean = DeathLean * t * t };

        // 키 사이를 부드럽게 잇는다. 키마다 속도가 0 이 되어 동작의 "멈칫"이 생긴다.
        private static float Eval(float[] times, float[] values, float t)
        {
            t = Mathf.Clamp01(t);
            for (int i = 1; i < times.Length; i++)
            {
                if (t <= times[i])
                {
                    float local = Mathf.InverseLerp(times[i - 1], times[i], t);
                    return Mathf.Lerp(values[i - 1], values[i], Mathf.SmoothStep(0f, 1f, local));
                }
            }
            return values[values.Length - 1];
        }
    }
}
