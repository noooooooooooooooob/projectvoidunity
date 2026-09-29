using UnityEngine;

namespace ProjectVoid.View
{
    /// <summary>전투 효과음 재생기. 같은 소리가 반복돼도 덜 기계적으로 들리게 음높이를 조금씩 흔든다.</summary>
    public sealed class BattleAudio : MonoBehaviour
    {
        public const float PitchJitter = 0.08f;

        public BattleSounds Sounds;
        private AudioSource _source;

        /// <summary>실제로 재생한 횟수 (테스트용).</summary>
        public int PlayCount { get; private set; }

        public void Play(AudioClip clip)
        {
            if (clip == null)
            {
                return;
            }
            if (_source == null)
            {
                _source = gameObject.AddComponent<AudioSource>();
                _source.playOnAwake = false;
                _source.spatialBlend = 0f;
            }
            _source.pitch = 1f + Random.Range(-PitchJitter, PitchJitter);
            _source.PlayOneShot(clip);
            PlayCount++;
        }
    }
}
