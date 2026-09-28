using System.Collections.Generic;
using ProjectVoid.Combat;
using ProjectVoid.View;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;

namespace ProjectVoid.EditorTools
{
    /// <summary>Battle 씬을 코드로 만든다 (카메라, 조명, EventSystem, BattleRoot + 참조 연결) 그리고 빌드 목록 첫 칸에 넣는다.</summary>
    public static class BattleSceneBuilder
    {
        public const string ScenePath = "Assets/_Project/Scenes/Battle.unity";

        [MenuItem("Project Void/Build Battle Scene")]
        public static void Build()
        {
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var cameraObject = new GameObject("Main Camera") { tag = "MainCamera" };
            var camera = cameraObject.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.08f, 0.08f, 0.11f);
            cameraObject.AddComponent<AudioListener>();

            var lightObject = new GameObject("Directional Light");
            var light = lightObject.AddComponent<Light>();
            light.type = LightType.Directional;
            // 방 전체는 어둡게 두고 배경의 스포트라이트가 명암을 만든다 (레퍼런스: 어두운 실내 + 빛 조각).
            light.intensity = 0.45f;
            light.shadows = LightShadows.Soft;
            lightObject.transform.rotation = Quaternion.Euler(55f, -40f, 0f);
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.16f, 0.17f, 0.2f);

            new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));

            var rootObject = new GameObject("BattleRoot");
            var root = rootObject.AddComponent<BattleRoot>();
            var serialized = new SerializedObject(root);
            serialized.FindProperty("encounter").objectReferenceValue =
                AssetDatabase.LoadAssetAtPath<EncounterData>($"{DataImporter.EncountersDir}/skirmish.asset");
            serialized.FindProperty("battleCamera").objectReferenceValue = camera;
            SerializedProperty assets = serialized.FindProperty("assets");
            assets.FindPropertyRelative("font").objectReferenceValue = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontSetup.FontAssetPath);
            assets.FindPropertyRelative("overlayTextMaterial").objectReferenceValue = AssetDatabase.LoadAssetAtPath<Material>(FontSetup.OverlayMaterialPath);
            assets.FindPropertyRelative("tileMaterial").objectReferenceValue = AssetDatabase.LoadAssetAtPath<Material>(FontSetup.TileMaterialPath);
            assets.FindPropertyRelative("unitMaterial").objectReferenceValue = AssetDatabase.LoadAssetAtPath<Material>(FontSetup.UnitMaterialPath);
            assets.FindPropertyRelative("placeholderSprite").objectReferenceValue =
                AssetDatabase.LoadAssetAtPath<Sprite>($"{PixelSpriteProcessor.OutDir}/placeholder_unit.png");
            serialized.ApplyModifiedPropertiesWithoutUndo();

            System.IO.Directory.CreateDirectory("Assets/_Project/Scenes");
            EditorSceneManager.SaveScene(scene, ScenePath);

            var scenes = new List<EditorBuildSettingsScene> { new EditorBuildSettingsScene(ScenePath, true) };
            foreach (EditorBuildSettingsScene existing in EditorBuildSettings.scenes)
            {
                if (existing.path != ScenePath)
                {
                    scenes.Add(existing);
                }
            }
            EditorBuildSettings.scenes = scenes.ToArray();
        }
    }
}
