using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace ProjectVoid.EditorTools
{
    /// <summary>실내 분위기용 후처리 볼륨(비네트·블룸·색 보정)과 PC 렌더러의 SSAO(구석 그늘)를 만든다.</summary>
    public static class AtmosphereSetup
    {
        public const string ProfilePath = "Assets/_Project/Settings/BattleVolume.asset";
        public const string PcRendererPath = "Assets/Settings/PC_Renderer.asset";

        [MenuItem("Project Void/Setup Atmosphere")]
        public static void Run()
        {
            Directory.CreateDirectory("Assets/_Project/Settings");
            var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(ProfilePath);
            if (profile == null)
            {
                profile = ScriptableObject.CreateInstance<VolumeProfile>();
                AssetDatabase.CreateAsset(profile, ProfilePath);
            }

            // 가장자리를 어둡게 해 방 안에 있는 느낌.
            Vignette vignette = Get<Vignette>(profile);
            vignette.intensity.Override(0.38f);
            vignette.smoothness.Override(0.45f);
            // 창문·스포트라이트가 은은하게 번지게.
            Bloom bloom = Get<Bloom>(profile);
            bloom.threshold.Override(0.9f);
            bloom.intensity.Override(0.5f);
            bloom.scatter.Override(0.7f);
            // 채도를 낮추고 살짝 차가운 톤.
            ColorAdjustments color = Get<ColorAdjustments>(profile);
            color.saturation.Override(-18f);
            color.contrast.Override(8f);
            color.colorFilter.Override(new Color(0.92f, 0.96f, 1f));
            EditorUtility.SetDirty(profile);

            AddAmbientOcclusion();
            AssetDatabase.SaveAssets();
        }

        private static T Get<T>(VolumeProfile profile) where T : VolumeComponent
        {
            if (!profile.TryGet(out T component))
            {
                component = profile.Add<T>(true);
                component.name = typeof(T).Name;
                // 하위 에셋으로 넣어야 저장 후에도 남는다.
                AssetDatabase.AddObjectToAsset(component, profile);
            }
            return component;
        }

        // SSAO 렌더러 기능은 공개 타입이 아니라서 이름으로 찾아 만든다.
        private static void AddAmbientOcclusion()
        {
            var data = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(PcRendererPath);
            if (data.rendererFeatures.Exists(f => f != null && f.GetType().Name == "ScreenSpaceAmbientOcclusion"))
            {
                return;
            }
            Type type = typeof(UniversalRendererData).Assembly.GetType("UnityEngine.Rendering.Universal.ScreenSpaceAmbientOcclusion");
            var feature = (ScriptableRendererFeature)ScriptableObject.CreateInstance(type);
            feature.name = "ScreenSpaceAmbientOcclusion";
            AssetDatabase.AddObjectToAsset(feature, data);
            AssetDatabase.TryGetGUIDAndLocalFileIdentifier(feature, out string _, out long localId);
            data.rendererFeatures.Add(feature);
            // 렌더러는 기능 목록과 m_RendererFeatureMap(로컬 파일 ID)이 같은 길이여야 한다.
            var serialized = new SerializedObject(data);
            SerializedProperty map = serialized.FindProperty("m_RendererFeatureMap");
            map.arraySize++;
            map.GetArrayElementAtIndex(map.arraySize - 1).longValue = localId;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(data);
        }
    }
}
