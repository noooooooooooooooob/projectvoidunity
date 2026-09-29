using System;
using TMPro;
using UnityEngine;

namespace ProjectVoid.View
{
    /// <summary>화면 요소들이 함께 쓰는 에셋 묶음. BattleRoot 가 인스펙터에서 받아 나눠 준다.</summary>
    [Serializable]
    public sealed class ViewAssets
    {
        public TMP_FontAsset font;
        public Material overlayTextMaterial;
        public Material tileMaterial;
        public Material unitMaterial;
        public Sprite placeholderSprite;
        public BattleSounds sounds;
        public Sprite projectileSprite;

        [NonSerialized] private Sprite _white;
        [NonSerialized] private Sprite _shadow;
        [NonSerialized] private Sprite _ring;

        /// <summary>HP 바용 1×1 흰 스프라이트 (1 유닛 크기).</summary>
        public Sprite White => _white != null ? _white : _white = CreateWhite();

        /// <summary>가운데가 진하고 가장자리가 투명한 원형 그림자 (1 유닛 폭). 색은 SpriteRenderer.color 로 입힌다.</summary>
        public Sprite Shadow => _shadow != null ? _shadow : _shadow = CreateShadow();

        /// <summary>발밑 진영 표시용 얇은 고리 (1 유닛 폭). 색은 SpriteRenderer.color 로 입힌다.</summary>
        public Sprite Ring => _ring != null ? _ring : _ring = CreateRing();

        private static Sprite CreateRing()
        {
            const int size = 64;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            var center = new Vector2((size - 1) / 2f, (size - 1) / 2f);
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float r = Vector2.Distance(new Vector2(x, y), center) / (size / 2f);
                    // 반지름 0.8~0.95 사이만 불투명, 가장자리는 부드럽게.
                    float alpha = Mathf.Clamp01(1f - Mathf.Abs(r - 0.875f) / 0.075f);
                    texture.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
                }
            }
            texture.Apply();
            return Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
        }

        private static Sprite CreateWhite()
        {
            var texture = new Texture2D(1, 1, TextureFormat.RGBA32, false);
            texture.SetPixel(0, 0, Color.white);
            texture.Apply();
            return Sprite.Create(texture, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 1f);
        }

        private static Sprite CreateShadow()
        {
            const int size = 64;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            var center = new Vector2((size - 1) / 2f, (size - 1) / 2f);
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float t = Mathf.Clamp01(Vector2.Distance(new Vector2(x, y), center) / (size / 2f));
                    texture.SetPixel(x, y, new Color(1f, 1f, 1f, 0.5f * (1f - t)));
                }
            }
            texture.Apply();
            return Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
        }
    }
}
