using NUnit.Framework;
using ProjectVoid.View;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace ProjectVoid.Tests
{
    public class ScreenPulseTests
    {
        private GameObject _object;
        private VolumeProfile _shared;

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_object);
            Object.DestroyImmediate(_shared);
        }

        [Test]
        public void PulseKicksChromaticAndVignetteThenSettles()
        {
            _object = new GameObject("PulseTest");
            _shared = ScriptableObject.CreateInstance<VolumeProfile>();
            Vignette baseVignette = _shared.Add<Vignette>(true);
            baseVignette.intensity.Override(0.3f);
            var volume = _object.AddComponent<Volume>();
            volume.isGlobal = true;
            volume.sharedProfile = _shared;
            var pulse = _object.AddComponent<ScreenPulse>();
            pulse.Bind(volume);

            pulse.Pulse(1f);
            Assert.IsTrue(volume.profile.TryGet(out ChromaticAberration chromatic), "adds chromatic aberration");
            Assert.Greater(chromatic.intensity.value, 0.5f, "colour split on impact");
            Assert.IsTrue(volume.profile.TryGet(out Vignette vignette));
            Assert.Greater(vignette.intensity.value, 0.4f, "edges close in");
            pulse.Tick(1f);
            Assert.AreEqual(0f, chromatic.intensity.value, 1e-4f, "settles");
            Assert.AreEqual(0.3f, vignette.intensity.value, 1e-4f, "back to the scene vignette");
            Assert.AreEqual(0.3f, baseVignette.intensity.value, 1e-4f, "the saved profile asset is untouched");
        }

        [Test]
        public void WithoutAVolumePulseDoesNothing()
        {
            _object = new GameObject("PulseTest");
            var pulse = _object.AddComponent<ScreenPulse>();
            pulse.Bind(null);
            Assert.DoesNotThrow(() => pulse.Pulse(1f));
            Assert.DoesNotThrow(() => pulse.Tick(0.1f));
        }
    }
}
