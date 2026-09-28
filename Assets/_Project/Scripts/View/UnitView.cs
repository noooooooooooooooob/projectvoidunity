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
        // 발을 축으로 카메라 반대쪽으로 눕히는 각도. 44° 로 내려다볼 때 판이 덜 눌려 보인다.
        public const float BodyTiltDeg = 20f;

        private static readonly Color FlashColor = new Color(1f, 0.45f, 0.45f);
        private static readonly Color ContactShadowColor = new Color(0f, 0f, 0f, 0.7f);
        private static readonly Color AllyRingColor = new Color(0.3f, 0.55f, 1f, 0.8f);
        private static readonly Color EnemyRingColor = new Color(1f, 0.3f, 0.25f, 0.8f);
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        private ViewAssets _assets;
        // 발 위치를 축으로 도는 피벗. 연출(튀어오르기·흔들림)은 이 피벗을 움직인다.
        private Transform _body;
        private Material _bodyMaterial;
        private Transform _overhead;
        private TextMeshPro _nameLabel;
        private TextMeshPro _statLabel;
        private SpriteRenderer _hpBack;
        private SpriteRenderer _hpFill;
        private SpriteRenderer _shadow;
        private SpriteRenderer _ring;
        private BoxCollider _pickCollider;
        private int _hp;
        private int _maxHp = 1;
        private int _block;

        public Unit Unit { get; private set; }
        public Vector3 HomePosition { get; private set; }
        public CellTag PickTag { get; private set; }
        public string StatText => _statLabel.text;
        public float HpFillWidth => _hpFill.transform.localScale.x;
        public MeshRenderer BodyRenderer { get; private set; }
        public Material BodyMaterial => _bodyMaterial;
        public Color ShadowColor => _shadow.color;
        public Color RingColor => _ring.color;

        public void Setup(Unit unit, Sprite sprite, ViewAssets assets)
        {
            Unit = unit;
            _assets = assets;

            _body = new GameObject("Body").transform;
            _body.SetParent(transform, false);
            var billboard = _body.gameObject.AddComponent<Billboard>();
            billboard.yAxisOnly = true;
            billboard.tiltDeg = BodyTiltDeg;

            // SpriteRenderer 는 Lit 조명·그림자를 제대로 받지 못해 판에 텍스처를 입힌 사각형으로 그린다.
            GameObject quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            quad.name = "Sprite";
            quad.transform.SetParent(_body, false);
            // 클릭 판정은 유닛 루트의 상자가 맡는다. Destroy 는 프레임 끝이라 먼저 꺼 둔다.
            Collider quadCollider = quad.GetComponent<Collider>();
            quadCollider.enabled = false;
            DestroyImmediateOrLater(quadCollider);
            float aspect = sprite.rect.width / sprite.rect.height;
            // 스프라이트는 오른쪽을 본다. 적은 왼쪽(아군 쪽)을 보도록 좌우를 뒤집는다 (머티리얼이 양면이라 음수 스케일 가능).
            float facing = unit.IsAlly ? 1f : -1f;
            quad.transform.localScale = new Vector3(SpriteHeight * aspect * facing, SpriteHeight, 1f);
            quad.transform.localPosition = new Vector3(0f, SpriteHeight / 2f, 0f);
            _bodyMaterial = new Material(assets.unitMaterial);
            _bodyMaterial.SetTexture("_BaseMap", sprite.texture);
            BodyRenderer = quad.GetComponent<MeshRenderer>();
            BodyRenderer.sharedMaterial = _bodyMaterial;
            BodyRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.TwoSided;
            BodyRenderer.receiveShadows = false;

            _shadow = FloorDecal("Shadow", assets.Shadow, new Vector3(0.9f, 0.5f, 1f), 0.01f);
            _shadow.color = ContactShadowColor;
            _ring = FloorDecal("Ring", assets.Ring, new Vector3(0.95f, 0.6f, 1f), 0.012f);
            _ring.color = unit.IsAlly ? AllyRingColor : EnemyRingColor;

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
            _body.localPosition = Vector3.zero;
            _bodyMaterial.SetColor(BaseColorId, Color.white);
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
            Transform sprite = _body;
            var up = new Vector3(0f, 0.25f, 0f);
            yield return Coroutines.Tween(ActionTime / 2f, t => sprite.localPosition = Vector3.Lerp(Vector3.zero, up, t));
            yield return Coroutines.Tween(ActionTime / 2f, t => sprite.localPosition = Vector3.Lerp(up, Vector3.zero, t));
        }

        // Godot: 색 번쩍임 2회 + 좌우 흔들림(0.08, -0.08, 0.05, 0)을 FLASH_TIME 동안 동시에.
        public IEnumerator FlashAndShake()
        {
            float[] keys = { 0f, 0.08f, -0.08f, 0.05f, 0f };
            Transform sprite = _body;
            yield return Coroutines.Tween(FlashTime, t =>
            {
                int quarter = Mathf.Min((int)(t * 4f), 3);
                _bodyMaterial.SetColor(BaseColorId, quarter % 2 == 0 && t < 1f ? FlashColor : Color.white);
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
            _ring.enabled = false;
            yield return Coroutines.Tween(FadeTime, t =>
            {
                float alpha = 1f - t;
                // 알파 잘라내기 머티리얼이라 알파를 내리면 픽셀이 점점 사라진다.
                _bodyMaterial.SetColor(BaseColorId, new Color(1f, 1f, 1f, alpha));
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

        private SpriteRenderer FloorDecal(string name, Sprite sprite, Vector3 scale, float height)
        {
            var decalObject = new GameObject(name);
            decalObject.transform.SetParent(transform, false);
            decalObject.transform.localPosition = new Vector3(0f, height, 0f);
            decalObject.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            decalObject.transform.localScale = scale;
            var decal = decalObject.AddComponent<SpriteRenderer>();
            decal.sprite = sprite;
            return decal;
        }

        private static void DestroyImmediateOrLater(Object target)
        {
            if (Application.isPlaying)
            {
                Destroy(target);
            }
            else
            {
                DestroyImmediate(target);
            }
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
