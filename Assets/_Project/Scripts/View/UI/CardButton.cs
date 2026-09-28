using System;
using ProjectVoid.Combat;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace ProjectVoid.View
{
    /// <summary>손패 카드 한 장. 클릭(카드 → 적 선택)과 드래그(적 위에 놓기) 둘 다 받는다.</summary>
    public sealed class CardButton : MonoBehaviour, IPointerClickHandler, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        public const float Width = 150f;
        public const float Height = 190f;
        private const float LiftHeight = 24f;
        private static readonly Color MeleeBorder = new Color(0.85f, 0.45f, 0.3f);
        private static readonly Color RangedBorder = new Color(0.35f, 0.6f, 0.95f);
        private static readonly Color FaceColor = new Color(0.16f, 0.16f, 0.2f);

        public event Action<CardButton> Clicked;
        public event Action<CardButton> DragBegan;
        public event Action<CardButton, Vector2> Dragged;
        public event Action<CardButton, Vector2> DragEnded;

        private RectTransform _face;
        private CanvasGroup _group;
        private bool _affordable = true;
        private bool _pending;

        public CardData Card { get; private set; }
        public bool Interactable { get; set; } = true;
        public bool Affordable => _affordable;

        /// <summary>조준 화살표가 시작하는 카드 윗면 중앙의 화면 좌표 (Screen Space Overlay 기준).</summary>
        public Vector2 AimOrigin
        {
            get
            {
                var corners = new Vector3[4];
                _face.GetWorldCorners(corners);
                return RectTransformUtility.WorldToScreenPoint(null, (corners[1] + corners[2]) / 2f);
            }
        }

        public void Setup(CardData card, ViewAssets assets)
        {
            Card = card;
            var layout = gameObject.AddComponent<LayoutElement>();
            layout.preferredWidth = Width;
            layout.preferredHeight = Height;
            _group = gameObject.AddComponent<CanvasGroup>();

            var faceObject = new GameObject("Face", typeof(RectTransform), typeof(Image));
            _face = (RectTransform)faceObject.transform;
            _face.SetParent(transform, false);
            _face.anchorMin = Vector2.zero;
            _face.anchorMax = Vector2.one;
            _face.offsetMin = Vector2.zero;
            _face.offsetMax = Vector2.zero;
            faceObject.GetComponent<Image>().color = card.attackType == AttackType.Melee ? MeleeBorder : RangedBorder;

            var inner = new GameObject("Inner", typeof(RectTransform), typeof(Image));
            var innerRect = (RectTransform)inner.transform;
            innerRect.SetParent(_face, false);
            innerRect.anchorMin = Vector2.zero;
            innerRect.anchorMax = Vector2.one;
            innerRect.offsetMin = new Vector2(5f, 5f);
            innerRect.offsetMax = new Vector2(-5f, -5f);
            inner.GetComponent<Image>().color = FaceColor;

            string type = card.attackType == AttackType.Melee ? "근접" : "원거리";
            MakeText(innerRect, assets.font, card.displayName, 30f, new Vector2(0f, 45f));
            MakeText(innerRect, assets.font, $"SP {card.spCost}", 24f, new Vector2(0f, 0f));
            MakeText(innerRect, assets.font, $"{type} · 거리 {card.attackRange}\n피해 {card.damage}", 20f, new Vector2(0f, -50f));
        }

        public void SetAffordable(bool affordable)
        {
            _affordable = affordable;
            Refresh();
        }

        public void SetSelected(bool selected)
        {
            _face.anchoredPosition = new Vector2(0f, selected ? LiftHeight : 0f);
        }

        public void SetPending(bool pending)
        {
            _pending = pending;
            Refresh();
        }

        private void Refresh()
        {
            _group.alpha = _pending ? 0.2f : _affordable ? 1f : 0.45f;
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (Interactable && _affordable && !_pending)
            {
                Clicked?.Invoke(this);
            }
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            if (Interactable && _affordable && !_pending)
            {
                DragBegan?.Invoke(this);
            }
        }

        public void OnDrag(PointerEventData eventData)
        {
            Dragged?.Invoke(this, eventData.position);
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            DragEnded?.Invoke(this, eventData.position);
        }

        private static void MakeText(RectTransform parent, TMP_FontAsset font, string value, float size, Vector2 position)
        {
            var textObject = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
            var rect = (RectTransform)textObject.transform;
            rect.SetParent(parent, false);
            rect.sizeDelta = new Vector2(Width - 16f, 60f);
            rect.anchoredPosition = position;
            var text = textObject.GetComponent<TextMeshProUGUI>();
            text.font = font;
            text.text = value;
            text.fontSize = size;
            text.alignment = TextAlignmentOptions.Center;
            text.raycastTarget = false;
        }
    }
}
