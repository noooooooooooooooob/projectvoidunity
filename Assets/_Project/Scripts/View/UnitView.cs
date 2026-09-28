using System.Collections;
using ProjectVoid.Combat;
using TMPro;
using UnityEngine;

namespace ProjectVoid.View
{
    /// <summary>유닛 하나의 2.5D 표현: 빌보드 스프라이트, 머리 위 이름·HP 바·수치, 발밑 그림자, 클릭 판정 상자, 연출.</summary>
    public sealed class UnitView : MonoBehaviour
    {
        public const float SpriteHeight = 1.6f;
        public const float OverheadY = 2.0f;
        public const float HpBarWidth = 0.9f;
        public const float HpBarHeight = 0.1f;
        public const float LungeDistance = 0.4f;
        public const float ActionTime = 0.25f;
        public const float FlashTime = 0.24f;
        public const float PopTime = 0.6f;
        public const float FadeTime = 0.4f;
        public const float MoveTime = 0.25f;

        private static readonly Color FlashColor = new Color(1f, 0.45f, 0.45f);
        private static readonly Color AllyShadowColor = new Color(0.25f, 0.45f, 1f);
        private static readonly Color EnemyShadowColor = new Color(1f, 0.3f, 0.25f);

        private ViewAssets _assets;
        private SpriteRenderer _sprite;
        private Transform _overhead;
        private TextMeshPro _nameLabel;
        private TextMeshPro _statLabel;
        private SpriteRenderer _hpBack;
        private SpriteRenderer _hpFill;
        private SpriteRenderer _shadow;
        private BoxCollider _pickCollider;
        private int _hp;
        private int _maxHp = 1;
        private int _block;

        public Unit Unit { get; private set; }
        public Vector3 HomePosition { get; private set; }
        public CellTag PickTag { get; private set; }
        public string StatText => _statLabel.text;
        public float HpFillWidth => _hpFill.transform.localScale.x;

        public void Setup(Unit unit, Sprite sprite, ViewAssets assets)
        {
            Unit = unit;
            _assets = assets;

            var spriteObject = new GameObject("Sprite");
            spriteObject.transform.SetParent(transform, false);
            _sprite = spriteObject.AddComponent<SpriteRenderer>();
            _sprite.sprite = sprite;
            // 스프라이트는 오른쪽을 본다. 적은 왼쪽(아군 쪽)을 보도록 뒤집는다.
            _sprite.flipX = !unit.IsAlly;
            float spriteHeight = sprite.bounds.size.y;
            spriteObject.transform.localScale = Vector3.one * (SpriteHeight / spriteHeight);
            spriteObject.AddComponent<Billboard>().yAxisOnly = true;

            var shadowObject = new GameObject("Shadow");
            shadowObject.transform.SetParent(transform, false);
            shadowObject.transform.localPosition = new Vector3(0f, 0.01f, 0f);
            shadowObject.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            shadowObject.transform.localScale = new Vector3(0.9f, 0.5f, 1f);
            _shadow = shadowObject.AddComponent<SpriteRenderer>();
            _shadow.sprite = assets.Shadow;
            _shadow.color = unit.IsAlly ? AllyShadowColor : EnemyShadowColor;

            _overhead = new GameObject("Overhead").transform;
            _overhead.SetParent(transform, false);
            _overhead.localPosition = new Vector3(0f, OverheadY, 0f);
            _overhead.gameObject.AddComponent<Billboard>();
            _nameLabel = MakeLabel(unit.Data.displayName, 1.5f, new Vector3(0f, 0.28f, 0f));
            _hpBack = MakeBar(new Color(0.1f, 0.1f, 0.1f, 0.85f), 10);
            _hpFill = MakeBar(new Color(0.35f, 0.85f, 0.4f), 11);
            _statLabel = MakeLabel("", 1.2f, new Vector3(0f, -0.18f, 0f));

            _pickCollider = gameObject.AddComponent<BoxCollider>();
            _pickCollider.size = new Vector3(0.8f, SpriteHeight, 0.4f);
            _pickCollider.center = new Vector3(0f, SpriteHeight / 2f, 0f);
            PickTag = gameObject.AddComponent<CellTag>();

            SetStats(unit.Hp, unit.Data.maxHp, unit.Block);
        }

        public void SetStats(int hp, int maxHp, int block)
        {
            _hp = hp;
            _maxHp = Mathf.Max(maxHp, 1);
            _block = block;
            RefreshStats();
        }

        public void SetHp(int hp, int maxHp)
        {
            _hp = hp;
            _maxHp = Mathf.Max(maxHp, 1);
            RefreshStats();
        }

        public void SetBlock(int block)
        {
            _block = block;
            RefreshStats();
        }

        public void SetHome(Vector3 worldPosition)
        {
            HomePosition = worldPosition;
            transform.position = worldPosition;
        }

        public IEnumerator SlideTo(Vector3 worldPosition)
        {
            HomePosition = worldPosition;
            Vector3 start = transform.position;
            yield return Coroutines.Tween(MoveTime, t => transform.position = Vector3.Lerp(start, worldPosition, t));
        }

        /// <summary>연출이 중간에 끊겨도 제자리·원래 색으로 돌린다.</summary>
        public void ResetPose()
        {
            transform.position = HomePosition;
            _sprite.transform.localPosition = Vector3.zero;
            _sprite.color = Color.white;
        }

        public void SetAlive(bool alive)
        {
            gameObject.SetActive(alive);
        }

        public IEnumerator LungeToward(Vector3 worldTarget)
        {
            Vector3 direction = worldTarget - HomePosition;
            direction.y = 0f;
            if (direction.sqrMagnitude > 0f)
            {
                direction.Normalize();
            }
            Vector3 home = HomePosition;
            Vector3 lunge = home + direction * LungeDistance;
            yield return Coroutines.Tween(ActionTime / 2f, t => transform.position = Vector3.Lerp(home, lunge, t));
            yield return Coroutines.Tween(ActionTime / 2f, t => transform.position = Vector3.Lerp(lunge, home, t));
        }

        public IEnumerator Hop()
        {
            Transform sprite = _sprite.transform;
            var up = new Vector3(0f, 0.25f, 0f);
            yield return Coroutines.Tween(ActionTime / 2f, t => sprite.localPosition = Vector3.Lerp(Vector3.zero, up, t));
            yield return Coroutines.Tween(ActionTime / 2f, t => sprite.localPosition = Vector3.Lerp(up, Vector3.zero, t));
        }

        // Godot: 색 번쩍임 2회 + 좌우 흔들림(0.08, -0.08, 0.05, 0)을 FLASH_TIME 동안 동시에.
        public IEnumerator FlashAndShake()
        {
            float[] keys = { 0f, 0.08f, -0.08f, 0.05f, 0f };
            Transform sprite = _sprite.transform;
            yield return Coroutines.Tween(FlashTime, t =>
            {
                int quarter = Mathf.Min((int)(t * 4f), 3);
                _sprite.color = quarter % 2 == 0 && t < 1f ? FlashColor : Color.white;
                float local = t * 4f - quarter;
                sprite.localPosition = new Vector3(Mathf.Lerp(keys[quarter], keys[quarter + 1], local), 0f, 0f);
            });
        }

        public void PopText(string text, Color color)
        {
            TextMeshPro label = MakeLabel(text, 2.6f, new Vector3(0f, 0.5f, 0f));
            label.color = color;
            StartCoroutine(PopRoutine(label));
        }

        private IEnumerator PopRoutine(TextMeshPro label)
        {
            Vector3 start = label.transform.localPosition;
            Color color = label.color;
            yield return Coroutines.Tween(PopTime, t =>
            {
                label.transform.localPosition = start + new Vector3(0f, 0.6f * t, 0f);
                label.color = new Color(color.r, color.g, color.b, 1f - t);
            });
            Destroy(label.gameObject);
        }

        public IEnumerator FadeOut()
        {
            _pickCollider.enabled = false;
            _hpBack.enabled = false;
            _hpFill.enabled = false;
            _shadow.enabled = false;
            yield return Coroutines.Tween(FadeTime, t =>
            {
                float alpha = 1f - t;
                _sprite.color = new Color(1f, 1f, 1f, alpha);
                _nameLabel.alpha = alpha;
                _statLabel.alpha = alpha;
            });
            gameObject.SetActive(false);
        }

        private void RefreshStats()
        {
            _statLabel.text = _block > 0 ? $"{_hp}/{_maxHp}  방{_block}" : $"{_hp}/{_maxHp}";
            float ratio = Mathf.Clamp01((float)_hp / _maxHp);
            Transform fill = _hpFill.transform;
            fill.localScale = new Vector3(HpBarWidth * ratio, HpBarHeight, 1f);
            fill.localPosition = new Vector3(-HpBarWidth * (1f - ratio) / 2f, 0f, 0f);
            _hpFill.enabled = ratio > 0f;
        }

        private TextMeshPro MakeLabel(string text, float fontSize, Vector3 localPosition)
        {
            var labelObject = new GameObject("Label");
            labelObject.transform.SetParent(_overhead, false);
            labelObject.transform.localPosition = localPosition;
            var label = labelObject.AddComponent<TextMeshPro>();
            label.font = _assets.font;
            label.fontSharedMaterial = _assets.overlayTextMaterial;
            label.fontSize = fontSize;
            label.alignment = TextAlignmentOptions.Center;
            label.rectTransform.sizeDelta = new Vector2(4f, 0.6f);
            label.text = text;
            return label;
        }

        private SpriteRenderer MakeBar(Color color, int sortingOrder)
        {
            var barObject = new GameObject("Bar");
            barObject.transform.SetParent(_overhead, false);
            barObject.transform.localScale = new Vector3(HpBarWidth, HpBarHeight, 1f);
            var bar = barObject.AddComponent<SpriteRenderer>();
            bar.sprite = _assets.White;
            bar.color = color;
            bar.sortingOrder = sortingOrder;
            return bar;
        }
    }
}
