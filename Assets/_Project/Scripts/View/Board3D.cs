using System;
using System.Collections;
using System.Collections.Generic;
using ProjectVoid.Combat;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace ProjectVoid.View
{
    public readonly struct CellHint
    {
        public CellHint(bool valid, string text)
        {
            Valid = valid;
            Text = text;
        }

        public bool Valid { get; }
        public string Text { get; }
    }

    /// <summary>3D 진영 타일, 유닛 뷰, 타일 상태 표시, 마우스 클릭·호버 판정.</summary>
    public sealed class Board3D : MonoBehaviour
    {
        public enum TileState { Base, Empty, Current, Valid, Invalid, Movable, ShapeHit, ShapeOut }

        // 레퍼런스처럼 바닥에 박힌 얇은 판. 칸 사이 틈(CellPitch - TileSize)으로 바닥이 보여 테두리 역할을 한다.
        public const float TileThickness = 0.04f;
        private const float RayLength = 100f;
        // 아군 칸은 밝은 콘크리트, 적 칸은 어두운 금속 판.
        private static readonly Color AllyTileColor = new Color(0.52f, 0.53f, 0.55f);
        private static readonly Color EnemyTileColor = new Color(0.3f, 0.31f, 0.35f);
        private static readonly Color CurrentEmission = new Color(1f, 0.82f, 0.3f);
        private static readonly Color ValidEmission = new Color(0.45f, 0.85f, 0.45f);
        private static readonly Color MoveEmission = new Color(0.45f, 0.65f, 1f);
        private static readonly Color ShapeHitEmission = new Color(0.95f, 0.95f, 0.95f);
        private static readonly Color ShapeOutEmission = new Color(1f, 0.5f, 0.15f);
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");

        public event Action<Team, Vector2Int> CellClicked;
        public event Action PickMissed;
        public event Action<Team, Vector2Int> CellHovered;
        public event Action HoverCleared;

        private readonly Dictionary<(Team, Vector2Int), Renderer> _tiles = new Dictionary<(Team, Vector2Int), Renderer>();
        private readonly Dictionary<(Team, Vector2Int), TileState> _tileStates = new Dictionary<(Team, Vector2Int), TileState>();
        private readonly Dictionary<(Team, Vector2Int), TextMeshPro> _hints = new Dictionary<(Team, Vector2Int), TextMeshPro>();
        private readonly Dictionary<Unit, UnitView> _views = new Dictionary<Unit, UnitView>();
        private ViewAssets _assets;
        private Vector2 _pendingClick;
        private bool _hasPendingClick;
        private Vector2 _pointer;
        private bool _pressStartedOnBoard;
        private bool _hasHover;
        private Team _hoverTeam;
        private Vector2Int _hoverCell;

        public BoardLayout Layout { get; private set; }
        public bool InputEnabled { get; set; }
        /// <summary>HUD 카드를 끌고 있거나 이번 프레임에 놓았으면 true. 그때의 마우스 떼기는 보드 클릭이 아니다 (놓기는 RequestPick 으로 따로 온다).</summary>
        public Func<bool> UiDragActive { get; set; }
        public int TileCount => _tiles.Count;

        public void Build(BattleState state, ViewAssets assets)
        {
            _assets = assets;
            Layout = new BoardLayout(state.Resolver.AllyGrid, state.Resolver.EnemyGrid);
            BuildSide(Team.Ally, Layout.AllyGrid);
            BuildSide(Team.Enemy, Layout.EnemyGrid);
            foreach (Unit unit in state.Units)
            {
                var viewObject = new GameObject($"Unit {unit.UnitId} {unit.Data.id}");
                viewObject.transform.SetParent(transform, false);
                var view = viewObject.AddComponent<UnitView>();
                view.Setup(unit, unit.Data.sprite != null ? unit.Data.sprite : assets.placeholderSprite, assets);
                view.SetHome(Layout.CellPosition(unit.Team, unit.Cell));
                Tag(view.PickTag, unit.Team, unit.Cell);
                _views[unit] = view;
            }
        }

        /// <summary>규칙 상태로 모든 뷰와 타일을 맞춘다 (재생이 끝난 뒤 어긋남 정리).</summary>
        public void SyncFromState(BattleState state)
        {
            ClearTargetHints();
            foreach ((Team, Vector2Int) key in new List<(Team, Vector2Int)>(_tiles.Keys))
            {
                SetTileState(key.Item1, key.Item2, TileState.Empty);
            }
            foreach (Unit unit in state.Units)
            {
                UnitView view = _views[unit];
                view.SetHome(Layout.CellPosition(unit.Team, unit.Cell));
                Tag(view.PickTag, unit.Team, unit.Cell);
                view.ResetPose();
                view.SetStats(unit.Hp, unit.Data.maxHp, unit.Block);
                view.SetAlive(unit.IsAlive);
                if (unit.IsAlive)
                {
                    SetTileState(unit.Team, unit.Cell, TileState.Base);
                }
            }
            Unit actor = state.CurrentUnit();
            if (actor != null && !state.Finished && actor.IsAlive)
            {
                SetTileState(actor.Team, actor.Cell, TileState.Current);
            }
        }

        public void ShowCurrent(Team team, Vector2Int cell)
        {
            foreach ((Team, Vector2Int) key in new List<(Team, Vector2Int)>(_tileStates.Keys))
            {
                if (_tileStates[key] == TileState.Current)
                {
                    SetTileState(key.Item1, key.Item2, TileState.Base);
                }
            }
            SetTileState(team, cell, TileState.Current);
        }

        public void MarkEmpty(Team team, Vector2Int cell) => SetTileState(team, cell, TileState.Empty);

        public void ShowTargetHints(Team team, Dictionary<Vector2Int, CellHint> hints)
        {
            ClearTargetHints();
            foreach (KeyValuePair<Vector2Int, CellHint> pair in hints)
            {
                SetTileState(team, pair.Key, pair.Value.Valid ? TileState.Valid : TileState.Invalid);
                ShowHint(team, pair.Key, pair.Value.Text);
            }
        }

        public void ClearTargetHints()
        {
            foreach (KeyValuePair<(Team, Vector2Int), TextMeshPro> pair in _hints)
            {
                pair.Value.gameObject.SetActive(false);
                (Team team, Vector2Int cell) = pair.Key;
                TileState current = _tileStates.TryGetValue(pair.Key, out TileState s) ? s : TileState.Empty;
                if (current == TileState.Valid || current == TileState.Invalid || current == TileState.ShapeHit || current == TileState.ShapeOut)
                {
                    SetTileState(team, cell, OccupiedByLivingView(team, cell) ? TileState.Base : TileState.Empty);
                }
                else if (current == TileState.Movable)
                {
                    SetTileState(team, cell, TileState.Empty);
                }
            }
        }

        public void ShowShapePreview(Team team, Dictionary<Vector2Int, CellHint> hits)
        {
            foreach (KeyValuePair<Vector2Int, CellHint> pair in hits)
            {
                SetTileState(team, pair.Key, pair.Value.Valid ? TileState.ShapeHit : TileState.ShapeOut);
                ShowHint(team, pair.Key, pair.Value.Text);
            }
        }

        public void ShowMoveHints(Team team, List<Vector2Int> cells)
        {
            ClearTargetHints();
            foreach (Vector2Int cell in cells)
            {
                SetTileState(team, cell, TileState.Movable);
            }
        }

        public IEnumerator MoveView(Unit unit, Vector2Int fromCell, Vector2Int toCell, bool animate)
        {
            SetTileState(unit.Team, fromCell, TileState.Empty);
            SetTileState(unit.Team, toCell, TileState.Current);
            UnitView view = ViewFor(unit);
            Tag(view.PickTag, unit.Team, toCell);
            Vector3 target = Layout.CellPosition(unit.Team, toCell);
            if (animate)
            {
                yield return view.SlideTo(target);
            }
            else
            {
                view.SetHome(target);
            }
        }

        public UnitView ViewFor(Unit unit) => _views.TryGetValue(unit, out UnitView view) ? view : null;

        public TileState GetTileState(Team team, Vector2Int cell) => _tileStates[(team, cell)];

        public TextMeshPro HintLabel(Team team, Vector2Int cell) => _hints[(team, cell)];

        public bool TryGetHover(out Team team, out Vector2Int cell)
        {
            team = _hoverTeam;
            cell = _hoverCell;
            return _hasHover;
        }

        /// <summary>카드를 놓은 화면 위치를 다음 Update 에서 판정한다 (결과는 CellClicked 또는 PickMissed).</summary>
        public void RequestPick(Vector2 screenPosition)
        {
            _pendingClick = screenPosition;
            _hasPendingClick = true;
        }

        /// <summary>카드를 끄는 동안 호버 위치를 HUD 가 알려 준다.</summary>
        public void UpdatePointer(Vector2 screenPosition)
        {
            _pointer = screenPosition;
        }

        public bool PickAt(Ray ray, out Team team, out Vector2Int cell)
        {
            Physics.SyncTransforms();
            if (Physics.Raycast(ray, out RaycastHit hit, RayLength) && hit.collider.TryGetComponent(out CellTag tag))
            {
                team = tag.team;
                cell = tag.cell;
                return true;
            }
            team = default;
            cell = default;
            return false;
        }

        private void Update()
        {
            if (_hasPendingClick)
            {
                _hasPendingClick = false;
                if (PickScreen(_pendingClick, out Team team, out Vector2Int cell))
                {
                    CellClicked?.Invoke(team, cell);
                }
                else
                {
                    PickMissed?.Invoke();
                }
            }
            if (!InputEnabled)
            {
                return;
            }
            Mouse mouse = Mouse.current;
            if (mouse == null)
            {
                return;
            }
            bool overUi = EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
            HandlePointer(mouse.position.ReadValue(), mouse.leftButton.wasPressedThisFrame, mouse.leftButton.wasReleasedThisFrame, overUi);
        }

        public bool HasPendingPick => _hasPendingClick;

        /// <summary>이번 프레임의 왼쪽 버튼 상태로 클릭·호버를 판정한다 (Update 가 부르며, 테스트가 직접 부를 수 있다).</summary>
        public void HandlePointer(Vector2 position, bool pressedThisFrame, bool releasedThisFrame, bool overUi)
        {
            bool dragging = UiDragActive != null && UiDragActive();
            if (!dragging)
            {
                _pointer = position;
            }
            // 누름이 HUD 에서 시작됐으면 보드 위에서 떼도 클릭이 아니다 (Godot 은 GUI 가 누름을 가져가 이런 일이 없었다).
            if (pressedThisFrame)
            {
                _pressStartedOnBoard = !overUi && !dragging;
            }
            if (releasedThisFrame)
            {
                if (_pressStartedOnBoard && !dragging && !overUi)
                {
                    RequestPick(_pointer);
                }
                _pressStartedOnBoard = false;
            }
            UpdateHover();
        }

        private void UpdateHover()
        {
            if (!PickScreen(_pointer, out Team team, out Vector2Int cell))
            {
                if (_hasHover)
                {
                    _hasHover = false;
                    HoverCleared?.Invoke();
                }
                return;
            }
            if (_hasHover && team == _hoverTeam && cell == _hoverCell)
            {
                return;
            }
            _hasHover = true;
            _hoverTeam = team;
            _hoverCell = cell;
            CellHovered?.Invoke(team, cell);
        }

        private bool PickScreen(Vector2 screenPosition, out Team team, out Vector2Int cell)
        {
            Camera camera = Camera.main;
            if (camera == null)
            {
                team = default;
                cell = default;
                return false;
            }
            return PickAt(camera.ScreenPointToRay(screenPosition), out team, out cell);
        }

        private bool OccupiedByLivingView(Team team, Vector2Int cell)
        {
            foreach (Unit unit in _views.Keys)
            {
                if (unit.Team == team && unit.Cell == cell && unit.IsAlive)
                {
                    return true;
                }
            }
            return false;
        }

        private void ShowHint(Team team, Vector2Int cell, string text)
        {
            TextMeshPro label = _hints[(team, cell)];
            label.text = text;
            label.gameObject.SetActive(true);
        }

        private void SetTileState(Team team, Vector2Int cell, TileState state)
        {
            _tileStates[(team, cell)] = state;
            Material material = _tiles[(team, cell)].sharedMaterial;
            Color baseColor = team == Team.Ally ? AllyTileColor : EnemyTileColor;
            Color albedo = baseColor;
            Color emission = Color.black;
            switch (state)
            {
                case TileState.Empty: albedo = Darkened(baseColor, 0.45f); break;
                case TileState.Current: emission = CurrentEmission * 0.8f; break;
                case TileState.Valid: emission = ValidEmission * 0.8f; break;
                case TileState.Invalid: albedo = Darkened(baseColor, 0.6f); break;
                case TileState.Movable: emission = MoveEmission * 0.6f; break;
                case TileState.ShapeHit: emission = ShapeHitEmission * 1.0f; break;
                case TileState.ShapeOut: emission = ShapeOutEmission * 0.8f; break;
            }
            material.SetColor(BaseColorId, albedo);
            material.SetColor(EmissionColorId, emission);
        }

        // Godot Color.darkened: 검정 쪽으로 amount 만큼.
        private static Color Darkened(Color color, float amount)
            => new Color(color.r * (1f - amount), color.g * (1f - amount), color.b * (1f - amount), color.a);

        private void BuildSide(Team team, Vector2Int grid)
        {
            for (int row = 0; row < grid.y; row++)
            {
                for (int col = 0; col < grid.x; col++)
                {
                    var cell = new Vector2Int(col, row);
                    Vector3 top = Layout.CellPosition(team, cell);

                    GameObject tile = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    tile.name = $"Tile {team} {col},{row}";
                    tile.transform.SetParent(transform, false);
                    tile.transform.localScale = new Vector3(BoardLayout.TileSize, TileThickness, BoardLayout.TileSize);
                    tile.transform.position = top - new Vector3(0f, TileThickness / 2f, 0f);
                    var renderer = tile.GetComponent<Renderer>();
                    // 타일마다 색이 달라지므로 머티리얼을 복사해 쓴다.
                    renderer.sharedMaterial = new Material(_assets.tileMaterial);
                    Tag(tile.AddComponent<CellTag>(), team, cell);

                    var hintObject = new GameObject($"Hint {team} {col},{row}");
                    hintObject.transform.SetParent(transform, false);
                    hintObject.transform.position = top + new Vector3(0f, UnitView.OverheadY + 0.6f, 0f);
                    hintObject.AddComponent<Billboard>();
                    var hint = hintObject.AddComponent<TextMeshPro>();
                    hint.font = _assets.font;
                    hint.fontSharedMaterial = _assets.overlayTextMaterial;
                    hint.fontSize = 1.6f;
                    hint.alignment = TextAlignmentOptions.Center;
                    hint.rectTransform.sizeDelta = new Vector2(3f, 0.6f);
                    hintObject.SetActive(false);

                    _tiles[(team, cell)] = renderer;
                    _hints[(team, cell)] = hint;
                    SetTileState(team, cell, TileState.Empty);
                }
            }
        }

        private static void Tag(CellTag tag, Team team, Vector2Int cell)
        {
            tag.team = team;
            tag.cell = cell;
        }
    }
}
