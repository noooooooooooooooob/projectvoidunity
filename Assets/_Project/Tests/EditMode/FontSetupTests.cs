using NUnit.Framework;
using ProjectVoid.EditorTools;
using TMPro;
using UnityEditor;
using UnityEngine;

namespace ProjectVoid.Tests
{
    public class FontSetupTests
    {
        private static string Missing(uint[] codes)
        {
            var text = new System.Text.StringBuilder();
            foreach (uint code in codes ?? new uint[0])
            {
                text.Append(char.ConvertFromUtf32((int)code));
            }
            return text.ToString();
        }

        [Test]
        public void KoreanFontCoversGameText()
        {
            var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontSetup.FontAssetPath);
            Assert.IsNotNull(font, "font asset exists");
            bool ok = font.HasCharacters("선봉사수정찰병괴한보초추적자베기횡베기꿰뚫기사격관통일제폭발탄차례종료승리패배이동방어휴식공격쓰러짐빈칸거리막힘", out uint[] missing, false, true);
            Assert.IsTrue(ok, "missing: " + Missing(missing));
        }

        // HUD·힌트가 쓰는 기호. 빠진 기호가 있으면 이 테스트를 고치지 말고, 그 기호를 쓰는 코드(BattleHud.TurnBarText 의 ▶ →,
        // RefreshSp 의 ● ○, BattleRoot.HintText 의 ✓, 로그의 ― —)에서 ASCII(> - * O)로 바꾸고 이 목록에서도 뺀다.
        [Test]
        public void SymbolsAreCovered()
        {
            var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontSetup.FontAssetPath);
            bool ok = font.HasCharacters("▶→●○✓―—·", out uint[] missing, false, true);
            Assert.IsTrue(ok, "missing: " + Missing(missing));
        }

        [Test]
        public void UnitMaterialIsAlphaClippedLitAndTwoSided()
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(FontSetup.UnitMaterialPath);
            Assert.IsNotNull(material);
            Assert.AreEqual("Universal Render Pipeline/Lit", material.shader.name);
            Assert.IsTrue(material.IsKeywordEnabled("_ALPHATEST_ON"));
            Assert.AreEqual(0f, material.GetFloat("_Cull"), "both faces render (enemies are mirrored)");
        }

        // 발광 키워드가 URP 의 머티리얼 검증(임포트·인스펙터가 부름)을 거친 뒤에도 남는지. GI 플래그가 None 이면 URP 가 꺼 버린다.
        private static bool EmissionSurvivesValidation(Material material)
        {
            var copy = new Material(material);
            BaseShaderGUI.SetMaterialKeywords(copy);
            bool enabled = copy.IsKeywordEnabled("_EMISSION");
            Object.DestroyImmediate(copy);
            return enabled;
        }

        // 흰 번쩍임은 런타임에 발광 색만 바꾼다. 에셋에 키워드가 꺼지면 빌드에서 그 셰이더 변형이 빠져 번쩍임이 사라진다.
        [Test]
        public void UnitMaterialKeepsTheEmissionVariantForHitFlash()
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(FontSetup.UnitMaterialPath);
            Assert.IsTrue(material.IsKeywordEnabled("_EMISSION"));
            Assert.IsTrue(EmissionSurvivesValidation(material), "URP validation keeps emission on");
            Assert.Less(material.GetColor("_EmissionColor").maxColorComponent, 0.01f, "no visible glow at rest");
        }

        [Test]
        public void OverlayMaterialUsesOverlayShader()
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(FontSetup.OverlayMaterialPath);
            Assert.IsNotNull(material);
            Assert.AreEqual("TextMeshPro/Distance Field Overlay", material.shader.name);
        }

        [Test]
        public void TileMaterialHasEmission()
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(FontSetup.TileMaterialPath);
            Assert.IsNotNull(material);
            Assert.AreEqual("Universal Render Pipeline/Lit", material.shader.name);
            Assert.IsTrue(material.IsKeywordEnabled("_EMISSION"));
            Assert.IsTrue(EmissionSurvivesValidation(material), "tile highlights survive URP validation");
        }
    }
}
