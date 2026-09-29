using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace ProjectVoid.View
{
    /// <summary>
    /// 큰 타격·처치 때 화면 가장자리를 조이고 색을 번지게 했다가 되돌린다.
    /// 씬 볼륨의 런타임 사본(volume.profile)만 바꿔서 저장된 프로필 에셋은 그대로다.
    /// </summary>
    public sealed class ScreenPulse : MonoBehaviour
    {
        public const float Duration = 0.4f;
        public const float MaxChromatic = 1f;
        public const float VignetteBoost = 0.25f;

        private ChromaticAberration _chromatic;
        private Vignette _vignette;
        private float _baseChromatic;
        private float _baseVignette;
        private float _level;
        private bool _bound;

        /// <summary>볼륨이 null 이면 펄스는 아무 일도 하지 않는다.</summary>
        public void Bind(Volume volume)
        {
            _bound = true;
            _chromatic = null;
            _vignette = null;
            if (volume == null)
            {
                return;
            }
            VolumeProfile profile = volume.profile;
            if (!profile.TryGet(out _chromatic))
            {
                _chromatic = profile.Add<ChromaticAberration>();
            }
            _chromatic.active = true;
            _chromatic.intensity.overrideState = true;
            _baseChromatic = _chromatic.intensity.value;
            if (profile.TryGet(out _vignette))
            {
                _vignette.intensity.overrideState = true;
                _baseVignette = _vignette.intensity.value;
            }
        }

        public void Pulse(float strength)
        {
            if (_chromatic == null)
            {
                return;
            }
            _level = Mathf.Max(_level, Mathf.Clamp01(strength));
            Apply();
        }

        public void Tick(float unscaledDeltaTime)
        {
            if (_chromatic == null || _level <= 0f)
            {
                return;
            }
            _level = Mathf.Max(0f, _level - unscaledDeltaTime / Duration);
            Apply();
        }

        private void Apply()
        {
            _chromatic.intensity.value = Mathf.Clamp01(_baseChromatic + MaxChromatic * _level);
            if (_vignette != null)
            {
                _vignette.intensity.value = _baseVignette + VignetteBoost * _level;
            }
        }

        private void Start()
        {
            if (!_bound)
            {
                Bind(FindFirstObjectByType<Volume>());
            }
        }

        // 슬로모션·히트스톱 중에도 제 속도로 가라앉아야 한다.
        private void LateUpdate()
        {
            Tick(Time.unscaledDeltaTime);
        }
    }
}
