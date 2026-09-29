using NUnit.Framework;
using ProjectVoid.View;
using UnityEngine;

namespace ProjectVoid.Tests
{
    public class ProjectileTests
    {
        [Test]
        public void FlightTimeScalesWithDistanceWithinLimits()
        {
            Assert.AreEqual(Projectile.MinFlight, Projectile.FlightTime(0.1f), 1e-5f, "short shots still read");
            Assert.AreEqual(Projectile.MaxFlight, Projectile.FlightTime(100f), 1e-5f, "long shots do not drag");
            Assert.Less(Projectile.FlightTime(3f), Projectile.FlightTime(5f));
        }

        [Test]
        public void FliesFromShooterToTargetInAnArc()
        {
            var from = new Vector3(-3f, 1f, 0f);
            var to = new Vector3(3f, 1f, 1f);
            Assert.Less(Vector3.Distance(from, Projectile.PositionAt(from, to, 0.5f, 0f)), 1e-4f);
            Assert.Less(Vector3.Distance(to, Projectile.PositionAt(from, to, 0.5f, 1f)), 1e-4f);
            Assert.Greater(Projectile.PositionAt(from, to, 0.5f, 0.5f).y, Vector3.Lerp(from, to, 0.5f).y + 0.4f, "arcs above the line");
        }

        // 화살 그림이 없어도 전투가 멈추면 안 된다: 코드로 만든 줄무늬로 대신한다.
        [Test]
        public void SpawnsWithoutASprite()
        {
            Projectile projectile = Projectile.Spawn(null, TestAssets.Load().unitMaterial, Vector3.zero, Vector3.right * 4f);
            Assert.IsNotNull(projectile);
            Assert.IsNotNull(projectile.GetComponentInChildren<Renderer>().sharedMaterial.GetTexture("_BaseMap"));
            Assert.AreEqual(0, projectile.GetComponentsInChildren<Collider>().Length, "must not catch clicks");
            Object.DestroyImmediate(projectile.gameObject);
        }
    }
}
