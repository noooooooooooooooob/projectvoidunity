using NUnit.Framework;
using ProjectVoid.Combat;
using ProjectVoid.View;
using UnityEngine;

namespace ProjectVoid.Tests
{
    public class BattleSoundsTests
    {
        private static AudioClip Clip(string name) => AudioClip.Create(name, 10, 1, 44100, false);

        [Test]
        public void PicksSoundByAttackTypeDamageAndKill()
        {
            var s = ScriptableObject.CreateInstance<BattleSounds>();
            s.meleeSwing = Clip("ms");
            s.rangedShot = Clip("rs");
            s.meleeHit = Clip("mh");
            s.rangedHit = Clip("rh");
            s.blocked = Clip("b");
            s.kill = Clip("k");
            Assert.AreSame(s.meleeSwing, s.ForAttack(AttackType.Melee));
            Assert.AreSame(s.rangedShot, s.ForAttack(AttackType.Ranged));
            Assert.AreSame(s.meleeHit, s.ForImpact(AttackType.Melee, 5, false));
            Assert.AreSame(s.rangedHit, s.ForImpact(AttackType.Ranged, 5, false));
            Assert.AreSame(s.blocked, s.ForImpact(AttackType.Ranged, 0, false), "fully blocked");
            Assert.AreSame(s.kill, s.ForImpact(AttackType.Melee, 0, true), "kill wins");
            Object.DestroyImmediate(s);
        }

        // 소리 에셋을 아직 안 넣었어도 전투는 무음으로 돌아가야 한다.
        [Test]
        public void MissingClipIsSilent()
        {
            var audio = new GameObject("a").AddComponent<BattleAudio>();
            audio.Play(null);
            Assert.AreEqual(0, audio.PlayCount);
            Object.DestroyImmediate(audio.gameObject);
        }
    }
}
