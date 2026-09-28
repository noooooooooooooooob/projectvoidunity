using ProjectVoid.EditorTools;
using ProjectVoid.View;
using TMPro;
using UnityEditor;
using UnityEngine;

namespace ProjectVoid.Tests
{
    public static class TestAssets
    {
        public static ViewAssets Load() => new ViewAssets
        {
            font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontSetup.FontAssetPath),
            overlayTextMaterial = AssetDatabase.LoadAssetAtPath<Material>(FontSetup.OverlayMaterialPath),
            tileMaterial = AssetDatabase.LoadAssetAtPath<Material>(FontSetup.TileMaterialPath),
            unitMaterial = AssetDatabase.LoadAssetAtPath<Material>(FontSetup.UnitMaterialPath),
            placeholderSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/_Project/Art/Units/placeholder_unit.png"),
        };
    }
}
