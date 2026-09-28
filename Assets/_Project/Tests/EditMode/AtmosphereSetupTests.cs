using NUnit.Framework;
using ProjectVoid.EditorTools;
using UnityEditor;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace ProjectVoid.Tests
{
    public class AtmosphereSetupTests
    {
        [Test]
        public void VolumeProfileDarkensEdgesAndSoftensLights()
        {
            var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(AtmosphereSetup.ProfilePath);
            Assert.IsNotNull(profile, "profile asset exists");
            Assert.IsTrue(profile.TryGet(out Vignette vignette) && vignette.intensity.value > 0.2f, "vignette closes the room in");
            Assert.IsTrue(profile.TryGet(out Bloom bloom) && bloom.intensity.value > 0f, "windows and lamps glow");
            Assert.IsTrue(profile.TryGet(out ColorAdjustments color) && color.saturation.value < 0f, "muted colours");
        }

        // 벽·바닥 모서리와 소품 밑에 그늘이 없어 실내처럼 보이지 않았다.
        [Test]
        public void PcRendererHasAmbientOcclusion()
        {
            var renderer = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(AtmosphereSetup.PcRendererPath);
            Assert.IsNotNull(renderer);
            bool hasSsao = renderer.rendererFeatures.Exists(f => f != null && f.GetType().Name == "ScreenSpaceAmbientOcclusion" && f.isActive);
            Assert.IsTrue(hasSsao, "SSAO renderer feature is on");
        }
    }
}
