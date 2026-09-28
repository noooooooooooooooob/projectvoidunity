using NUnit.Framework;

namespace ProjectVoid.Tests
{
    public class HarnessSmokeTests
    {
        [Test]
        public void HarnessRuns()
        {
            Assert.AreEqual(2, 1 + 1);
        }
    }
}
