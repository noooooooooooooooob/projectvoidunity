using NUnit.Framework;
using ProjectVoid.View;

namespace ProjectVoid.Tests
{
    public class UnitMotionTests
    {
        private static void AssertRest(UnitMotion.Pose pose, string what)
        {
            Assert.AreEqual(0f, pose.stretch, 1e-4f, what + " stretch");
            Assert.AreEqual(0f, pose.lean, 1e-4f, what + " lean");
        }

        [Test]
        public void IdleBreathesGentlyAndUnitsAreOutOfSync()
        {
            float a = UnitMotion.Idle(0.4f, 0f).stretch;
            float b = UnitMotion.Idle(0.8f, 0f).stretch;
            Assert.AreNotEqual(a, b, "changes over time");
            for (float time = 0f; time < 3f; time += 0.1f)
            {
                Assert.LessOrEqual(System.Math.Abs(UnitMotion.Idle(time, 0f).stretch), 0.05f, "subtle");
            }
            Assert.AreNotEqual(UnitMotion.Idle(0.4f, 0f).stretch, UnitMotion.Idle(0.4f, 1.3f).stretch, "phase offsets units");
        }

        [Test]
        public void ActionsStartAndEndAtRest()
        {
            AssertRest(UnitMotion.Attack(0f), "attack start");
            AssertRest(UnitMotion.Attack(1f), "attack end");
            AssertRest(UnitMotion.Hop(0f), "hop start");
            AssertRest(UnitMotion.Hop(1f), "hop end");
            AssertRest(UnitMotion.Hit(0f), "hit start");
            AssertRest(UnitMotion.Hit(1f), "hit end");
            Assert.AreEqual(0f, UnitMotion.LungeReach(0f), 1e-4f);
            Assert.AreEqual(0f, UnitMotion.LungeReach(1f), 1e-4f);
            Assert.AreEqual(0f, UnitMotion.HopHeight(0f), 1e-4f);
            Assert.AreEqual(0f, UnitMotion.HopHeight(1f), 1e-4f);
        }

        // 공격: 뒤로 젖히며 움츠렸다가(예비동작) 앞으로 늘어나며 찌른다.
        [Test]
        public void AttackWindsUpThenStrikes()
        {
            UnitMotion.Pose windup = UnitMotion.Attack(UnitMotion.AttackWindupEnd);
            Assert.Less(windup.stretch, 0f, "squashes while winding up");
            Assert.Greater(windup.lean, 0f, "leans back");
            Assert.AreEqual(0f, UnitMotion.LungeReach(UnitMotion.AttackWindupEnd), 1e-4f, "stays home during the windup");
            UnitMotion.Pose strike = UnitMotion.Attack(UnitMotion.AttackStrike);
            Assert.Greater(strike.stretch, 0f, "stretches on the strike");
            Assert.Less(strike.lean, 0f, "leans forward");
            Assert.AreEqual(1f, UnitMotion.LungeReach(UnitMotion.AttackStrike), 1e-4f, "fully extended on the strike");
        }

        [Test]
        public void HopCrouchesBeforeJumpingAndSquashesOnLanding()
        {
            Assert.Less(UnitMotion.Hop(UnitMotion.HopCrouch).stretch, 0f, "crouch");
            Assert.AreEqual(0f, UnitMotion.HopHeight(UnitMotion.HopCrouch), 1e-4f, "still on the ground while crouching");
            Assert.Greater(UnitMotion.HopHeight(UnitMotion.HopPeak), 0.2f, "in the air");
            Assert.Less(UnitMotion.Hop(UnitMotion.HopLand).stretch, 0f, "squash on landing");
        }

        [Test]
        public void HitKnocksTheUnitBack()
        {
            UnitMotion.Pose impact = UnitMotion.Hit(UnitMotion.HitImpact);
            Assert.Greater(impact.lean, 5f, "leans back from the blow");
            Assert.Less(impact.stretch, 0f, "squashed by the impact");
        }

        [Test]
        public void DeathTopplesOver()
        {
            AssertRest(UnitMotion.Death(0f), "death start");
            Assert.GreaterOrEqual(UnitMotion.Death(1f).lean, 70f, "ends lying down");
        }
    }
}
