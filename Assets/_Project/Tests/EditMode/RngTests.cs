using NUnit.Framework;
using ProjectVoid.Combat;

namespace ProjectVoid.Tests
{
    public class RngTests
    {
        [Test]
        public void SameSeedSameSequence()
        {
            var a = new Rng(42);
            var b = new Rng(42);
            for (int i = 0; i < 20; i++)
            {
                Assert.AreEqual(a.RangeInclusive(0, 100), b.RangeInclusive(0, 100), $"draw {i}");
            }
        }

        [Test]
        public void RangeInclusiveIncludesBothEnds()
        {
            var rng = new Rng(7);
            bool sawMin = false, sawMax = false;
            for (int i = 0; i < 1000; i++)
            {
                int v = rng.RangeInclusive(0, 2);
                Assert.That(v, Is.InRange(0, 2));
                sawMin |= v == 0;
                sawMax |= v == 2;
            }
            Assert.IsTrue(sawMin && sawMax, "both ends appear");
        }

        [Test]
        public void DrawsCountsEveryCall()
        {
            var rng = new Rng(1);
            rng.RangeInclusive(0, 3);
            rng.NextFloat();
            Assert.AreEqual(2, rng.Draws);
        }

        [Test]
        public void DeriveIsStableAndIndependent()
        {
            Rng a = new Rng(5).Derive("enemy_ai");
            Rng b = new Rng(5).Derive("enemy_ai");
            var plain = new Rng(5);
            bool differs = false;
            for (int i = 0; i < 10; i++)
            {
                int va = a.RangeInclusive(0, 1000);
                Assert.AreEqual(va, b.RangeInclusive(0, 1000), "same seed and salt give same sequence");
                differs |= va != plain.RangeInclusive(0, 1000);
            }
            Assert.IsTrue(differs, "derived sequence differs from the parent seed");
        }
    }
}
