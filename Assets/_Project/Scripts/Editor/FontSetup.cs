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
            tile.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
            // URP 는 발광 색이 완전한 검정이면 _EMISSION 을 꺼 버린다. 눈에 안 보이는 값으로 둔다.
            tile.SetColor("_EmissionColor", new Color(0.001f, 0.001f, 0.001f));
            // 런타임에 발광 색만 바꾸므로 키워드를 에셋에 켜 둬야 셰이더 변형이 빌드에 포함된다. 다른 설정 뒤에 켠다.
            tile.EnableKeyword("_EMISSION");
            EditorUtility.SetDirty(tile);
            AssetDatabase.SaveAssetIfDirty(tile);

            AssetDatabase.SaveAssets();
        }
    }
}
