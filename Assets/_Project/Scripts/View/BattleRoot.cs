using System;
using System.Collections;
using System.Collections.Generic;
using ProjectVoid.Combat;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ProjectVoid.View
{
    /// <summary>
    /// 전투 씬의 루트. 규칙·기록기·보드·HUD·재생을 만들어 잇고, 입력을 규칙 호출로 바꾼 뒤 그동안 쌓인 이벤트를 재생한다.
    /// 재생 중에는 입력을 잠근다.
    /// </summary>
    public sealed class BattleRoot : MonoBehaviour
    {
        public const float CameraPitchDeg = 44f;
        public const float CameraFovDeg = 40f;
        public const float CameraMargin = 1.8f;
        // Godot (0, 0, 0.6) 은 카메라 쪽. 유니티에서는 카메라가 -Z 쪽이다.
        private static readonly Vector3 CameraTargetOffset = new Vector3(0f, 0f, -0.6f);

        /// <summary>테스트가 씬을 띄우기 전에 켜면 연출 대기 없이 재생한다.</summary>
        public static bool ForceInstantPlayback;

        [SerializeField] private EncounterData encounter;
        [SerializeField] private ViewAssets assets = new ViewAssets();
        [SerializeField] private Camera battleCamera;
        [Tooltip("0 이면 매 판 무작위 시드")]
        [SerializeField] private int seed;

        public event Action<bool> BattleFinished;

        private BattleEventRecorder _recorder;
        private BattlePlayback _playback;
        private int _selectedCard = -1;
        private bool _busy;
        private bool _awaitingDrop;
        private bool _moveMode;
        private Vector2Int _screenSize;

        public BattleState State { get; private set; }
        public Board3D Board { get; private set; }
        public BattleHud Hud { get; private set; }
        public bool IsBusy => _busy;
        /// <summary>규칙 호출 뒤 이벤트를 재생하는 중이면 true. 전투가 끝나도 IsBusy 는 계속 true 라서 재생 완료는 이것으로 본다.</summary>
        public bool IsPlaying { get; private set; }
        public int SelectedCard => _selectedCard;

        private void Start()
        {
            State = new BattleState(encounter, new Rng(seed != 0 ? seed : Environment.TickCount));
            _recorder = new BattleEventRecorder(State);
            State.BattleEnded += won => BattleFinished?.Invoke(won);

            Board = new GameObject("Board").AddComponent<Board3D>();
            Board.transform.SetParent(transform, false);
            Board.Build(State, assets);
            Board.SyncFromState(State);
            BattleEnvironment.Build(transform, Board.Layout, encounter, assets.tileMaterial);

            Hud = new GameObject("Hud", typeof(RectTransform)).AddComponent<BattleHud>();
            Hud.transform.SetParent(transform, false);
            Hud.Build(assets);
            Board.UiDragActive = () => Hud.DragActiveThisFrame;

            _playback = gameObject.AddComponent<BattlePlayback>();
            _playback.Board = Board;
            _playback.Hud = Hud;
            _playback.Instant = ForceInstantPlayback;

            Board.CellClicked += OnCellClicked;
            Board.PickMissed += OnPickMissed;
            Board.CellHovered += OnCellHovered;
            Board.HoverCleared += OnHoverCleared;
            Hud.CardSelected += OnCardSelected;
            Hud.CardDropped += OnCardDropped;
            Hud.CardDragMoved += OnCardDragMoved;
            Hud.EndTurnPressed += OnEndTurnPressed;
            Hud.MoveModeToggled += OnMoveModeToggled;
            Hud.RestartPressed += Restart;

            FrameCamera();
            Run(State.StartBattle);
        }

        private void Update()
        {
            if (Screen.width != _screenSize.x || Screen.height != _screenSize.y)
            {
                FrameCamera();
            }
        }

        public void Restart()
        {
            SceneManager.LoadScene(gameObject.scene.buildIndex);
        }

        private void Run(Action action)
        {
            StartCoroutine(RunRoutine(action));
        }

        private IEnumerator RunRoutine(Action action)
        {
            IsPlaying = true;
            SetBusy(true);
            action();
            yield return _playback.Play(_recorder.TakeEvents());
            Board.SyncFromState(State);
            Hud.SyncFromState(State, _selectedCard);
            SetBusy(State.Finished);
            if (_moveMode && MovableCellsNow().Count == 0)
            {
                _moveMode = false;
            }
            RefreshHints();
            IsPlaying = false;
        }

        private void SetBusy(bool busy)
        {
            _busy = busy;
            if (busy)
            {
                _awaitingDrop = false;
                ClearSelection();
            }
            Board.InputEnabled = !busy;
            Hud.SetInteractive(!busy);
        }

        public void OnCardSelected(int index)
        {
            if (_busy)
            {
                return;
            }
            SelectCard(index);
            RefreshHints();
        }

        public void OnCardDropped(int index, Vector2 screenPosition)
        {
            if (_busy)
            {
                return;
            }
            SelectCard(index);
            RefreshHints();
            _awaitingDrop = true;
            Board.RequestPick(screenPosition);
        }

        public void OnCardDragMoved(Vector2 screenPosition)
        {
            Board.UpdatePointer(screenPosition);
        }

        public void OnPickMissed()
        {
            if (!_awaitingDrop)
            {
                return;
            }
            _awaitingDrop = false;
            ClearSelection();
        }

        public void OnCellClicked(Team team, Vector2Int cell)
        {
            bool fromDrop = _awaitingDrop;
            _awaitingDrop = false;
            if (_busy)
            {
                return;
            }
            if (_selectedCard < 0)
            {
                if (_moveMode && !fromDrop && team == Team.Ally)
                {
                    TryMove(cell);
                }
                return;
            }
            Unit actor = State.CurrentUnit();
            if (actor == null || team != Team.Enemy || _selectedCard >= actor.Hand.Count)
            {
                if (fromDrop)
                {
                    ClearSelection();
                }
                return;
            }
            CardData card = actor.Hand[_selectedCard];
            if (!State.Resolver.IsValidCell(actor, team, cell, card.attackType, card.attackRange, State.Units))
            {
                Hud.AppendLog("사용할 수 없는 위치");
                if (fromDrop)
                {
                    ClearSelection();
                }
                return;
            }
            int cardIndex = _selectedCard;
            Hud.SetPendingPlay(cardIndex);
            Run(() =>
            {
                if (!State.PlayCard(cardIndex, team, cell))
                {
                    Hud.AppendLog("사용할 수 없는 위치");
                }
            });
        }

        public void OnEndTurnPressed()
        {
            if (_busy)
            {
                return;
            }
            _moveMode = false;
            Run(State.EndTurn);
        }

        public void OnMoveModeToggled(bool on)
        {
            if (_busy)
            {
                Hud.SetMoveMode(_moveMode);
                return;
            }
            _moveMode = on;
            if (on)
            {
                ClearSelection();
                return;
            }
            RefreshHints();
        }

        private void SelectCard(int index)
        {
            _selectedCard = index;
            if (index < 0)
            {
                return;
            }
            _moveMode = false;
            Hud.SetMoveMode(false);
        }

        private void TryMove(Vector2Int cell)
        {
            if (!MovableCellsNow().Contains(cell))
            {
                return;
            }
            Run(() =>
            {
                if (!State.MoveUnit(cell))
                {
                    Hud.AppendLog("이동할 수 없는 칸");
                }
            });
        }

        private List<Vector2Int> MovableCellsNow()
        {
            Unit actor = State.CurrentUnit();
            if (_busy || State.Finished || actor == null || !actor.IsAlly || !actor.IsAlive || actor.Sp < 1)
            {
                return new List<Vector2Int>();
            }
            return State.Resolver.MovableCells(actor, State.Units);
        }

        private void RefreshHints()
        {
            List<Vector2Int> cells = MovableCellsNow();
            bool canMove = cells.Count > 0;
            Hud.SetMoveAvailable(canMove);
            Hud.SetMoveMode(_moveMode && canMove);
            if (_selectedCard >= 0)
            {
                if (Board.TryGetHover(out Team hoverTeam, out Vector2Int hoverCell))
                {
                    ApplyHoverPreview(hoverTeam, hoverCell);
                }
                else
                {
                    RefreshTargetHints();
                }
                return;
            }
            if (!_moveMode || !canMove)
            {
                Board.ClearTargetHints();
                return;
            }
            Board.ShowMoveHints(State.CurrentUnit().Team, cells);
        }

        private void ClearSelection()
        {
            _selectedCard = -1;
            Hud.ClearCardSelection();
            RefreshHints();
        }

        private void OnCellHovered(Team team, Vector2Int cell) => ApplyHoverPreview(team, cell);

        private void OnHoverCleared() => RefreshTargetHints();

        private void ApplyHoverPreview(Team team, Vector2Int cell)
        {
            RefreshTargetHints();
            if (_selectedCard < 0 || team != Team.Enemy)
            {
                return;
            }
            Unit actor = State.CurrentUnit();
            if (actor == null || !actor.IsAlly || _selectedCard >= actor.Hand.Count)
            {
                return;
            }
            CardData card = actor.Hand[_selectedCard];
            var hits = new Dictionary<Vector2Int, CellHint>();
            foreach (Vector2Int shapeCell in State.Resolver.ShapeCells(cell, card.shape, State.Resolver.EnemyGrid))
            {
                bool valid = State.Resolver.IsValidCell(actor, team, shapeCell, card.attackType, card.attackRange, State.Units);
                hits[shapeCell] = new CellHint(valid, HintText(State.Resolver.ReachCell(actor, team, shapeCell), card.attackRange, valid));
            }
            Board.ShowShapePreview(team, hits);
        }

        private void RefreshTargetHints()
        {
            Unit actor = State.CurrentUnit();
            if (_selectedCard < 0 || actor == null || !actor.IsAlly || _selectedCard >= actor.Hand.Count)
            {
                Board.ClearTargetHints();
                return;
            }
            CardData card = actor.Hand[_selectedCard];
            Vector2Int grid = State.Resolver.EnemyGrid;
            var hints = new Dictionary<Vector2Int, CellHint>();
            for (int row = 0; row < grid.y; row++)
            {
                for (int col = 0; col < grid.x; col++)
                {
                    var cell = new Vector2Int(col, row);
                    bool valid = State.Resolver.IsValidCell(actor, Team.Enemy, cell, card.attackType, card.attackRange, State.Units);
                    hints[cell] = new CellHint(valid, HintText(State.Resolver.ReachCell(actor, Team.Enemy, cell), card.attackRange, valid));
                }
            }
            Board.ShowTargetHints(Team.Enemy, hints);
        }

        private static string HintText(int distance, int attackRange, bool valid)
        {
            if (valid)
            {
                return $"✓ 거리 {distance}";
            }
            return distance > attackRange ? $"거리 {distance}" : "막힘";
        }

        private void FrameCamera()
        {
            _screenSize = new Vector2Int(Screen.width, Screen.height);
            BoardLayout layout = Board.Layout;
            float aspect = Screen.width / Mathf.Max(Screen.height, 1f);
            float distance = BoardLayout.CameraDistance(layout.Width(), layout.Depth(), CameraFovDeg, aspect, CameraMargin);
            Vector3 target = layout.Center() + CameraTargetOffset;
            float pitch = CameraPitchDeg * Mathf.Deg2Rad;
            battleCamera.fieldOfView = CameraFovDeg;
            battleCamera.transform.position = target + new Vector3(0f, Mathf.Sin(pitch) * distance, -Mathf.Cos(pitch) * distance);
            battleCamera.transform.LookAt(target, Vector3.up);
        }
    }
}
