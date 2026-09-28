using System;
using System.Collections.Generic;
using System.Text;
using ProjectVoid.Combat;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ProjectVoid.View
{
    /// <summary>
    /// 전투 HUD: 위 행동 순서, 아래 손패(버튼 줄), 오른쪽 아래 SP·이동·차례 종료, 왼쪽 로그, 가운데 승패 배너.
    /// 코드로 구성한다 (Godot UnitView 처럼 씬 파일 없이).
    /// </summary>
    public sealed class BattleHud : MonoBehaviour
    {
        private static readonly Color CurrentTurnColor = new Color(1f, 0.82f, 0.3f);
        private static readonly Color ActedColor = new Color(0.5f, 0.5f, 0.5f);
        private static readonly Color ButtonColor = new Color(0.22f, 0.24f, 0.3f);
        private static readonly Color PressedColor = new Color(0.3f, 0.5f, 0.9f);

        public event Action<int> CardSelected;
        public event Action<int, Vector2> CardDropped;
        public event Action<Vector2> CardDragMoved;
        public event Action EndTurnPressed;
        public event Action<bool> MoveModeToggled;
        public event Action RestartPressed;

        private readonly List<CardButton> _cards = new List<CardButton>();
        private readonly StringBuilder _log = new StringBuilder();
        private ViewAssets _assets;
        private TextMeshProUGUI _turnLabel;
        private TextMeshProUGUI _spLabel;
        private TextMeshProUGUI _logLabel;
        private ScrollRect _logScroll;
        private RectTransform _hand;
        private Button _moveButton;
        private Button _endTurnButton;
        private AimArrow _arrow;
        private GameObject _banner;
        private TextMeshProUGUI _bannerLabel;
        private bool _interactive;
        private bool _moveAvailable;
        private bool _movePressed;
        private bool _dragging;
        private int _dragEndFrame = -1;

        public string TurnText => _turnLabel.text;
        public string SpText => _spLabel.gameObject.activeSelf ? _spLabel.text : "";
        public string LogText => _log.ToString();
        public int HandCount => _cards.Count;
        public bool EndTurnEnabled => _endTurnButton.interactable;
        public bool MoveEnabled => _moveButton.interactable;
        public bool MovePressed => _movePressed;
        public bool BannerVisible => _banner.activeSelf;
        public string BannerText => _bannerLabel.text;
        public bool DragActiveThisFrame => _dragging || _dragEndFrame == Time.frameCount;
        public bool IsAiming => _arrow.IsAiming;

        public bool IsCardAffordable(int index) => _cards[index].Affordable;

        public void Build(ViewAssets assets)
        {
            _assets = assets;
            var canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            gameObject.AddComponent<GraphicRaycaster>();
            var root = (RectTransform)transform;

            _turnLabel = Text(root, "", 30f, TextAlignmentOptions.Left);
            Anchor(_turnLabel.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(24f, -70f), new Vector2(-24f, -10f));

            _logScroll = BuildLog(root);

            _hand = Panel(root, "Hand", new Color(0f, 0f, 0f, 0f));
            Anchor(_hand, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(-560f, 16f), new Vector2(560f, 16f + CardButton.Height + 30f));
            var layout = _hand.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 12f;
            layout.childAlignment = TextAnchor.LowerCenter;
            // 크기는 각 카드의 LayoutElement(선호 크기)를 따른다.
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;

            _spLabel = Text(root, "", 28f, TextAlignmentOptions.Center);
            Anchor(_spLabel.rectTransform, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-440f, 20f), new Vector2(-300f, 170f));
            _moveButton = MakeButton(root, "이동", new Vector2(-290f, 100f), new Vector2(-160f, 160f), OnMoveClicked);
            _endTurnButton = MakeButton(root, "차례 종료", new Vector2(-290f, 20f), new Vector2(-24f, 90f), () => EndTurnPressed?.Invoke());

            // CanvasRenderer 가 없으면 UI 그래픽이 그려지지 않는다 (AddComponent 로는 자동으로 붙지 않았다).
            var arrowObject = new GameObject("AimArrow", typeof(RectTransform), typeof(CanvasRenderer));
            var arrowRect = (RectTransform)arrowObject.transform;
            arrowRect.SetParent(root, false);
            Anchor(arrowRect, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            _arrow = arrowObject.AddComponent<AimArrow>();

            _banner = BuildBanner(root);
            _banner.SetActive(false);
            RefreshSp(null);
            ApplyInteractive();
        }

        public void SyncFromState(BattleState state, int selectedCard)
        {
            Unit actor = state.Finished ? null : state.CurrentUnit();
            RefreshSp(actor);
            if (actor != null && actor.IsAlly)
            {
                SetCards(actor.Hand, actor.Sp, selectedCard);
            }
            else
            {
                ClearHand();
            }
            if (state.Finished)
            {
                _turnLabel.text = state.AllyWon ? "승리!" : "패배...";
                return;
            }
            var alive = new List<bool>();
            foreach (Unit member in state.Initiative)
            {
                alive.Add(member.IsAlive);
            }
            _turnLabel.text = TurnBarText(state.RoundIndex, state.Initiative, alive, state.TurnIndex);
        }

        public void ShowTurn(BattleEvent e)
        {
            _turnLabel.text = TurnBarText(e.RoundIndex, e.Order, e.Alive, e.TurnIndex);
            ClearHand();
            RefreshSp(e.Unit.IsAlly ? e.Unit : null);
        }

        public void SetInteractive(bool enabled)
        {
            _interactive = enabled;
            ApplyInteractive();
        }

        public void SetMoveAvailable(bool available)
        {
            _moveAvailable = available;
            ApplyInteractive();
        }

        public void SetMoveMode(bool on)
        {
            _movePressed = on;
            _moveButton.GetComponent<Image>().color = on ? PressedColor : ButtonColor;
        }

        public void ClearCardSelection()
        {
            foreach (CardButton card in _cards)
            {
                card.SetSelected(false);
            }
        }

        public void SetPendingPlay(int index)
        {
            if (index >= 0 && index < _cards.Count)
            {
                _cards[index].SetPending(true);
            }
        }

        public void DrawCard(BattleEvent e)
        {
            AddCard(e.Card, e.Card.spCost <= e.Unit.Sp);
        }

        public void Reshuffle(BattleEvent e)
        {
            // 1단계에는 더미 표시가 없다 (2단계 카드 연출에서 채운다).
        }

        public void DiscardHand(BattleEvent e)
        {
            ClearHand();
        }

        public void RemovePlayedCard(BattleEvent e)
        {
            int index = _cards.FindIndex(c => c.Card == e.Card);
            if (index >= 0)
            {
                DestroySafe(_cards[index].gameObject);
                _cards.RemoveAt(index);
            }
            RefreshAffordable(e.Unit.Sp);
            RefreshSp(e.Unit);
        }

        public void ApplyMove(BattleEvent e)
        {
            if (!e.Unit.IsAlly)
            {
                return;
            }
            RefreshAffordable(e.Unit.Sp);
            RefreshSp(e.Unit);
        }

        public void AppendLog(string text)
        {
            _log.Append(text).Append('\n');
            _logLabel.text = _log.ToString();
            Canvas.ForceUpdateCanvases();
            _logScroll.verticalNormalizedPosition = 0f;
        }

        public void ShowBanner(bool allyWon)
        {
            _bannerLabel.text = allyWon ? "승리!" : "패배...";
            _banner.SetActive(true);
        }

        public static string TurnBarText(int roundIndex, List<Unit> order, List<bool> alive, int turnIndex)
        {
            var parts = new List<string>();
            for (int i = 0; i < order.Count; i++)
            {
                if (!alive[i])
                {
                    continue;
                }
                string name = order[i].Data.displayName;
                if (i < turnIndex)
                {
                    parts.Add($"<color=#{ColorUtility.ToHtmlStringRGB(ActedColor)}>{name}</color>");
                }
                else if (i == turnIndex)
                {
                    parts.Add($"<b><color=#{ColorUtility.ToHtmlStringRGB(CurrentTurnColor)}>▶{name}</color></b>");
                }
                else
                {
                    parts.Add(name);
                }
            }
            return $"R{roundIndex}  {string.Join(" → ", parts)}";
        }

        private void SetCards(List<CardData> hand, int sp, int selected)
        {
            ClearHand();
            for (int i = 0; i < hand.Count; i++)
            {
                AddCard(hand[i], hand[i].spCost <= sp);
                _cards[i].SetSelected(i == selected);
            }
        }

        private void AddCard(CardData card, bool affordable)
        {
            var cardObject = new GameObject($"Card {card.id}", typeof(RectTransform));
            cardObject.transform.SetParent(_hand, false);
            var button = cardObject.AddComponent<CardButton>();
            button.Setup(card, _assets);
            button.SetAffordable(affordable);
            button.Interactable = _interactive;
            button.Clicked += OnCardClicked;
            // Godot 처럼 끌기 시작한 카드를 선택해 드래그 중 범위 미리보기가 그 카드를 따르게 한다.
            button.DragBegan += dragged =>
            {
                _dragging = true;
                OnCardClicked(dragged);
            };
            button.Dragged += OnCardDragged;
            button.DragEnded += OnCardDragEnded;
            _cards.Add(button);
        }

        private void ClearHand()
        {
            // 끄던 카드가 사라지면 화살표도 같이 치운다.
            _dragging = false;
            _arrow.Hide();
            foreach (CardButton card in _cards)
            {
                DestroySafe(card.gameObject);
            }
            _cards.Clear();
        }

        // 에디트 모드 테스트에서는 Destroy 를 쓸 수 없다.
        private static void DestroySafe(GameObject target)
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

        private void RefreshAffordable(int sp)
        {
            foreach (CardButton card in _cards)
            {
                card.SetAffordable(card.Card.spCost <= sp);
            }
        }

        private void OnCardClicked(CardButton button)
        {
            int index = _cards.IndexOf(button);
            ClearCardSelection();
            button.SetSelected(true);
            CardSelected?.Invoke(index);
        }

        private void OnCardDragged(CardButton button, Vector2 position)
        {
            if (!_dragging)
            {
                return;
            }
            _arrow.Show(button.AimOrigin, position);
            CardDragMoved?.Invoke(position);
        }

        private void OnCardDragEnded(CardButton button, Vector2 position)
        {
            if (!_dragging)
            {
                return;
            }
            _dragging = false;
            _arrow.Hide();
            _dragEndFrame = Time.frameCount;
            CardDropped?.Invoke(_cards.IndexOf(button), position);
        }

        private void OnMoveClicked()
        {
            SetMoveMode(!_movePressed);
            MoveModeToggled?.Invoke(_movePressed);
        }

        private void RefreshSp(Unit actor)
        {
            bool visible = actor != null && actor.IsAlly;
            _spLabel.gameObject.SetActive(visible);
            if (!visible)
            {
                return;
            }
            int maxSp = ((AllyData)actor.Data).maxSp;
            string pips = new string('●', actor.Sp) + new string('○', Mathf.Max(maxSp - actor.Sp, 0));
            _spLabel.text = $"SP\n{pips}\n{actor.Sp} / {maxSp}";
        }

        private void ApplyInteractive()
        {
            _endTurnButton.interactable = _interactive;
            _moveButton.interactable = _interactive && _moveAvailable;
            foreach (CardButton card in _cards)
            {
                card.Interactable = _interactive;
            }
        }

        private ScrollRect BuildLog(RectTransform root)
        {
            RectTransform frame = Panel(root, "Log", new Color(0f, 0f, 0f, 0.35f));
            // 폭 360: 1920 기준 아군 보드 왼쪽 끝(약 x 390)을 가리지 않는다.
            Anchor(frame, new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(16f, 260f), new Vector2(360f, -80f));
            var scroll = frame.gameObject.AddComponent<ScrollRect>();
            frame.gameObject.AddComponent<RectMask2D>();
            scroll.horizontal = false;

            _logLabel = Text(frame, "", 22f, TextAlignmentOptions.TopLeft);
            RectTransform content = _logLabel.rectTransform;
            content.anchorMin = new Vector2(0f, 1f);
            content.anchorMax = new Vector2(1f, 1f);
            content.pivot = new Vector2(0.5f, 1f);
            content.offsetMin = new Vector2(12f, 0f);
            content.offsetMax = new Vector2(-12f, 0f);
            var fitter = _logLabel.gameObject.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            scroll.content = content;
            return scroll;
        }

        private GameObject BuildBanner(RectTransform root)
        {
            RectTransform banner = Panel(root, "Banner", new Color(0f, 0f, 0f, 0.7f));
            Anchor(banner, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-320f, -140f), new Vector2(320f, 140f));
            _bannerLabel = Text(banner, "", 72f, TextAlignmentOptions.Center);
            Anchor(_bannerLabel.rectTransform, new Vector2(0f, 0.4f), new Vector2(1f, 1f), Vector2.zero, Vector2.zero);
            MakeButton(banner, "다시 하기", new Vector2(-120f, 20f), new Vector2(120f, 90f), () => RestartPressed?.Invoke(), new Vector2(0.5f, 0f));
            return banner.gameObject;
        }

        private Button MakeButton(RectTransform parent, string label, Vector2 min, Vector2 max, Action onClick, Vector2? anchor = null)
        {
            RectTransform rect = Panel(parent, label, ButtonColor);
            Vector2 a = anchor ?? new Vector2(1f, 0f);
            Anchor(rect, a, a, min, max);
            var button = rect.gameObject.AddComponent<Button>();
            button.onClick.AddListener(() => onClick());
            TextMeshProUGUI text = Text(rect, label, 28f, TextAlignmentOptions.Center);
            Anchor(text.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            return button;
        }

        private static RectTransform Panel(RectTransform parent, string name, Color color)
        {
            var panel = new GameObject(name, typeof(RectTransform), typeof(Image));
            var rect = (RectTransform)panel.transform;
            rect.SetParent(parent, false);
            panel.GetComponent<Image>().color = color;
            return rect;
        }

        private TextMeshProUGUI Text(RectTransform parent, string value, float size, TextAlignmentOptions alignment)
        {
            var textObject = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
            var rect = (RectTransform)textObject.transform;
            rect.SetParent(parent, false);
            var text = textObject.GetComponent<TextMeshProUGUI>();
            text.font = _assets.font;
            text.fontSize = size;
            text.alignment = alignment;
            text.richText = true;
            text.raycastTarget = false;
            text.text = value;
            return text;
        }

        private static void Anchor(RectTransform rect, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
        {
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = offsetMin;
            rect.offsetMax = offsetMax;
        }
    }
}
