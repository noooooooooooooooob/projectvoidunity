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

    /// <summary>
    /// 진영 타일, 유닛 뷰, 타일 상태 표시, 마우스 클릭·호버 판정.
    /// 이름과 달리 두 방식을 모두 그린다: Perspective3D(바닥에 박힌 3D 판) 와 Flat2D(화면을 향한 평면 판, 정사영).
    /// </summary>
    public sealed class Board3D : MonoBehaviour
    {
        public enum TileState { Base, Empty, Current, Valid, Invalid, Movable, ShapeHit, ShapeOut }

        // 2D 에서 타일은 발보다 이만큼 뒤에 둬서 유닛 발이 타일 위에 그려지게 한다.
        public const float FlatTileDepth = 0.02f;
        // 레퍼런스처럼 바닥에 박힌 얇은 판. 칸 사이 틈(CellPitch - TileSize)으로 바닥이 보여 테두리 역할을 한다.
        public const float TileThickness = 0.08f;
        private const float RayLength = 100f;
        // 아군 칸은 밝은 콘크리트, 적 칸은 어두운 금속 판.
        private static readonly Color AllyTileColor = new Color(0.52f, 0.53f, 0.55f);
        private static readonly Color EnemyTileColor = new Color(0.3f, 0.31f, 0.35f);
        // 텍스처가 이미 제 색을 가지므로 살짝만 눌러 준다.
        private static readonly Color TexturedTileTint = new Color(0.85f, 0.85f, 0.88f);
        private static readonly Color CurrentEmission = new Color(1f, 0.82f, 0.3f);
        private static readonly Color ValidEmission = new Color(0.45f, 0.85f, 0.45f);
        private static readonly Color MoveEmission = new Color(0.45f, 0.65f, 1f);
        private static readonly Color ShapeHitEmission = new Color(0.95f, 0.95f, 0.95f);
        private static readonly Color ShapeOutEmission = new Color(1f, 0.5f, 0.15f);
        // 2D 칸 표시: 흰 표시에 곱하는 색. 알파가 곧 진하기라 빈 칸은 테두리만 은은하게 남는다.
        private static readonly Color FlatEmptyColor = new Color(1f, 1f, 1f, 0.14f);
        private static readonly Color FlatBaseColor = new Color(1f, 1f, 1f, 0.26f);
        private static readonly Color FlatInvalidColor = new Color(0.15f, 0.15f, 0.18f, 0.4f);
        private const float FlatHighlightAlpha = 0.6f;
        private const int FlatMarkingPixels = 32;
        private const int FlatMarkingBorder = 2;
        private const float FlatMarkingFill = 0.45f;
        private static Texture2D _flatMarking;
        private static Material _flatMarkingMaterial;
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
        private Texture2D _allyTileTexture;
        private Texture2D _enemyTileTexture;
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

        public BoardProjection Projection { get; private set; }

        /// <summary>2D 에서 뒤 행 유닛은 바닥 원근에 맞춰 조금 작게. 3D 는 카메라 원근이 맡으므로 1.</summary>
        public float UnitScale(Team team, Vector2Int cell)
            => Projection == BoardProjection.Flat2D ? Layout.FlatScaleAt(Layout.CellPosition(team, cell).z) : 1f;

        /// <summary>투영을 반영한 칸 중심 (유닛 발 위치).</summary>
        public Vector3 CellWorldPosition(Team team, Vector2Int cell) => Layout.WorldCell(team, cell, Projection);

        public void Build(BattleState state, ViewAssets assets, Texture2D allyTileTexture = null, Texture2D enemyTileTexture = null,
            BoardProjection projection = BoardProjection.Perspective3D)
        {
            _assets = assets;
            Projection = projection;
            _allyTileTexture = allyTileTexture;
            _enemyTileTexture = enemyTileTexture;
            Layout = new BoardLayout(state.Resolver.AllyGrid, state.Resolver.EnemyGrid);
            BuildSide(Team.Ally, Layout.AllyGrid);
            BuildSide(Team.Enemy, Layout.EnemyGrid);
            foreach (Unit unit in state.Units)
            {
                var viewObject = new GameObject($"Unit {unit.UnitId} {unit.Data.id}");
                viewObject.transform.SetParent(transform, false);
                var view = viewObject.AddComponent<UnitView>();
                view.Setup(unit, unit.Data.sprite != null ? unit.Data.sprite : assets.placeholderSprite, assets, projection);
                view.SetHome(CellWorldPosition(unit.Team, unit.Cell));
                view.transform.localScale = Vector3.one * UnitScale(unit.Team, unit.Cell);
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
                view.SetHome(CellWorldPosition(unit.Team, unit.Cell));
                view.transform.localScale = Vector3.one * UnitScale(unit.Team, unit.Cell);
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
            view.transform.localScale = Vector3.one * UnitScale(unit.Team, toCell);
            Vector3 target = CellWorldPosition(unit.Team, toCell);
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
        public Renderer TileRenderer(Team team, Vector2Int cell) => _tiles[(team, cell)];

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
            if (Projection == BoardProjection.Flat2D)
            {
                _tiles[(team, cell)].sharedMaterial.color = FlatTileColor(state);
                return;
            }
            Material material = _tiles[(team, cell)].sharedMaterial;
            Color baseColor = TileTexture(team) != null ? TexturedTileTint : team == Team.Ally ? AllyTileColor : EnemyTileColor;
            Color albedo = baseColor;
            Color emission = Color.black;
            // 발광은 판 무늬가 비칠 만큼만. 빈 칸도 바닥 구멍처럼 꺼지지 않게 조금만 어둡게.
            switch (state)
            {
                case TileState.Empty: albedo = Darkened(baseColor, 0.2f); break;
                case TileState.Current: emission = CurrentEmission * 0.4f; break;
                case TileState.Valid: emission = ValidEmission * 0.4f; break;
                case TileState.Invalid: albedo = Darkened(baseColor, 0.6f); break;
                case TileState.Movable: emission = MoveEmission * 0.35f; break;
                case TileState.ShapeHit: emission = ShapeHitEmission * 0.45f; break;
                case TileState.ShapeOut: emission = ShapeOutEmission * 0.4f; break;
            }
            material.SetColor(BaseColorId, albedo);
            material.SetColor(EmissionColorId, emission);
        }

        private static Color FlatTileColor(TileState state)
        {
            switch (state)
            {
                case TileState.Base: return FlatBaseColor;
                case TileState.Invalid: return FlatInvalidColor;
                case TileState.Current: return WithAlpha(CurrentEmission, FlatHighlightAlpha);
                case TileState.Valid: return WithAlpha(ValidEmission, FlatHighlightAlpha);
                case TileState.Movable: return WithAlpha(MoveEmission, FlatHighlightAlpha);
                case TileState.ShapeHit: return WithAlpha(ShapeHitEmission, FlatHighlightAlpha);
                case TileState.ShapeOut: return WithAlpha(ShapeOutEmission, FlatHighlightAlpha);
                default: return FlatEmptyColor;
            }
        }

        private static Color WithAlpha(Color color, float alpha) => new Color(color.r, color.g, color.b, alpha);

        // 칸 네 모서리를 바닥 원근에 올린 사다리꼴. 모두 같은 깊이(발보다 조금 뒤)라 화면 평면에 눕는다. 앞면은 카메라(-Z) 쪽.
        private Mesh FlatTileMesh(Team team, Vector2Int cell)
        {
            Vector3 center = Layout.CellPosition(team, cell);
            float half = BoardLayout.TileSize / 2f;
            float depth = center.z * BoardLayout.FlatDepthPerUnit + FlatTileDepth;
            Vector3 Corner(float dx, float dz)
            {
                Vector3 point = Layout.FlatFloorPoint(center.x + dx, center.z + dz);
                return new Vector3(point.x, point.y, depth);
            }
            var mesh = new Mesh { name = $"Tile {team} {cell.x},{cell.y}" };
            mesh.vertices = new[] { Corner(-half, -half), Corner(half, -half), Corner(half, half), Corner(-half, half) };
            mesh.uv = new[] { new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(0f, 1f) };
            mesh.triangles = new[] { 0, 3, 2, 0, 2, 1 };
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        // 반투명 표시용 공용 머티리얼 (타일마다 복사해 색만 바꾼다). 스프라이트 셰이더라 조명 없이 제 색 그대로다.
        private static Material FlatMarkingMaterial()
        {
            if (_flatMarkingMaterial == null)
            {
                _flatMarkingMaterial = new Material(Shader.Find("Sprites/Default")) { mainTexture = FlatMarking() };
            }
            return _flatMarkingMaterial;
        }

        // 흰 표시: 테두리는 진하고 안쪽은 옅다. 캐릭터와 비슷한 픽셀 크기라 점 필터로 그린다.
        private static Texture2D FlatMarking()
        {
            if (_flatMarking != null)
            {
                return _flatMarking;
            }
            const int size = FlatMarkingPixels;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    bool border = x < FlatMarkingBorder || y < FlatMarkingBorder || x >= size - FlatMarkingBorder || y >= size - FlatMarkingBorder;
                    texture.SetPixel(x, y, new Color(1f, 1f, 1f, border ? 1f : FlatMarkingFill));
                }
            }
            texture.Apply();
            _flatMarking = texture;
            return _flatMarking;
        }

        private Texture2D TileTexture(Team team) => team == Team.Ally ? _allyTileTexture : _enemyTileTexture;

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
                    Vector3 top = CellWorldPosition(team, cell);

                    GameObject tile;
                    if (Projection == BoardProjection.Flat2D)
                    {
                        // 바닥 원근을 따르는 사다리꼴 표시. 같은 메시로 클릭도 판정한다.
                        tile = new GameObject();
                        tile.transform.SetParent(transform, false);
                        Mesh mesh = FlatTileMesh(team, cell);
                        tile.AddComponent<MeshFilter>().sharedMesh = mesh;
                        tile.AddComponent<MeshRenderer>();
                        tile.AddComponent<MeshCollider>().sharedMesh = mesh;
                    }
                    else
                    {
                        tile = GameObject.CreatePrimitive(PrimitiveType.Cube);
                        tile.transform.SetParent(transform, false);
                        tile.transform.localScale = new Vector3(BoardLayout.TileSize, TileThickness, BoardLayout.TileSize);
                        tile.transform.position = top - new Vector3(0f, TileThickness / 2f, 0f);
                    }
                    tile.name = $"Tile {team} {col},{row}";
                    Renderer renderer;
                    if (Projection == BoardProjection.Flat2D)
                    {
                        renderer = tile.GetComponent<MeshRenderer>();
                        renderer.sharedMaterial = new Material(FlatMarkingMaterial());
                        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                    }
                    else
                    {
                        renderer = tile.GetComponent<Renderer>();
                        // 타일마다 색이 달라지므로 머티리얼을 복사해 쓴다.
                        renderer.sharedMaterial = new Material(_assets.tileMaterial);
                        if (TileTexture(team) != null)
                        {
                            renderer.sharedMaterial.mainTexture = TileTexture(team);
                        }
                    }
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
