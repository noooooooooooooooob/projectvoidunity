using System.IO;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;

namespace ProjectVoid.EditorTools
{
    /// <summary>한글 TMP 폰트 에셋(동적 아틀라스), 3D 라벨용 오버레이 머티리얼, 타일 머티리얼을 만든다.</summary>
    public static class FontSetup
    {
        public const string FontPath = "Assets/_Project/Fonts/NotoSansKR.ttf";
        public const string FontAssetPath = "Assets/_Project/Fonts/NotoSansKR SDF.asset";
        public const string OverlayMaterialPath = "Assets/_Project/Fonts/NotoSansKR Overlay.mat";
        public const string TileMaterialPath = "Assets/_Project/Materials/Tile.mat";
        public const string UnitMaterialPath = "Assets/_Project/Materials/UnitSprite.mat";
        private const string EssentialsPackage = "Packages/com.unity.ugui/Package Resources/TMP Essential Resources.unitypackage";

        [MenuItem("Project Void/Setup Fonts And Materials")]
        public static void Run()
        {
            if (!Directory.Exists("Assets/TextMesh Pro"))
            {
                AssetDatabase.ImportPackage(EssentialsPackage, false);
            }

            var fontAsset = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontAssetPath);
            if (fontAsset == null)
            {
                var font = AssetDatabase.LoadAssetAtPath<Font>(FontPath);
                fontAsset = TMP_FontAsset.CreateFontAsset(font, 90, 9, GlyphRenderMode.SDFAA, 2048, 2048, AtlasPopulationMode.Dynamic, true);
                fontAsset.name = "NotoSansKR SDF";
                AssetDatabase.CreateAsset(fontAsset, FontAssetPath);
                // 아틀라스와 머티리얼을 하위 에셋으로 넣어야 저장 후에도 참조가 살아 있다.
                fontAsset.atlasTextures[0].name = "NotoSansKR Atlas";
                AssetDatabase.AddObjectToAsset(fontAsset.atlasTextures[0], fontAsset);
                fontAsset.material.name = "NotoSansKR Material";
                AssetDatabase.AddObjectToAsset(fontAsset.material, fontAsset);
            }

            if (AssetDatabase.LoadAssetAtPath<Material>(OverlayMaterialPath) == null)
            {
                // 머리 위 글자·HP 바가 유닛·타일에 가려지지 않도록 깊이 검사를 끈 셰이더 (Godot no_depth_test 대응).
                var overlay = new Material(fontAsset.material) { shader = Shader.Find("TextMeshPro/Distance Field Overlay") };
                // Godot 라벨 외곽선(outline_size 10) 대응.
                overlay.SetFloat(ShaderUtilities.ID_OutlineWidth, 0.2f);
                overlay.SetColor(ShaderUtilities.ID_OutlineColor, Color.black);
                AssetDatabase.CreateAsset(overlay, OverlayMaterialPath);
            }

            var tile = AssetDatabase.LoadAssetAtPath<Material>(TileMaterialPath);
            if (tile == null)
            {
                tile = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                AssetDatabase.CreateAsset(tile, TileMaterialPath);
            }
            // URP 의 머티리얼 검증은 GI 플래그가 None 이면 발광이 없다고 보고 _EMISSION 을 끈다. 실시간 발광으로 표시해 둔다.
            tile.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            // URP 는 발광 색이 완전한 검정이면 _EMISSION 을 꺼 버린다. 눈에 안 보이는 값으로 둔다.
            tile.SetColor("_EmissionColor", new Color(0.001f, 0.001f, 0.001f));
            // 런타임에 발광 색만 바꾸므로 키워드를 에셋에 켜 둬야 셰이더 변형이 빌드에 포함된다. 다른 설정 뒤에 켠다.
            tile.EnableKeyword("_EMISSION");
            EditorUtility.SetDirty(tile);
            AssetDatabase.SaveAssetIfDirty(tile);

            SetupUnitMaterial();
            AssetDatabase.SaveAssets();
        }

        // 유닛 스프라이트용: 장면 조명을 받고 그림자를 드리우는 Lit, 투명 픽셀은 잘라내고, 적은 좌우 반전(음수 스케일)이라 양면.
        private static void SetupUnitMaterial()
        {
            var unit = AssetDatabase.LoadAssetAtPath<Material>(UnitMaterialPath);
            if (unit == null)
            {
                unit = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                AssetDatabase.CreateAsset(unit, UnitMaterialPath);
            }
            unit.SetColor("_BaseColor", Color.white);
            unit.SetFloat("_AlphaClip", 1f);
            unit.SetFloat("_Cutoff", 0.5f);
            unit.SetFloat("_Cull", 0f);
            // 픽셀아트가 번들거리지 않게 광택·반사를 끈다.
            unit.SetFloat("_Smoothness", 0f);
            unit.SetFloat("_SpecularHighlights", 0f);
            unit.SetFloat("_EnvironmentReflections", 0f);
            unit.renderQueue = (int)UnityEngine.Rendering.RenderQueue.AlphaTest;
            // 피격 흰 번쩍임은 런타임에 발광 색만 바꾼다. 키워드를 에셋에 켜 둬야 빌드에 그 셰이더 변형이 남는다 (Tile.mat 과 같은 이유).
            unit.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            unit.SetColor("_EmissionColor", new Color(0.001f, 0.001f, 0.001f));
            // 키워드는 다른 설정 뒤에 켜야 저장된다 (Tile.mat 과 같은 이유).
            unit.EnableKeyword("_ALPHATEST_ON");
            unit.EnableKeyword("_SPECULARHIGHLIGHTS_OFF");
            unit.EnableKeyword("_ENVIRONMENTREFLECTIONS_OFF");
            unit.EnableKeyword("_EMISSION");
            EditorUtility.SetDirty(unit);
            AssetDatabase.SaveAssetIfDirty(unit);
        }
    }
}
