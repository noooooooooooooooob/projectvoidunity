using System.Collections.Generic;
using ProjectVoid.Combat;
using UnityEngine;

namespace ProjectVoid.View
{
    // 새 종류는 뒤에만 추가한다.
    public enum BattleEventKind
    {
        TurnStarted, CardPlayed, EnemyActed, Damaged, Healed, BlockGained, Died, Log, BattleEnded,
        CardDrawn, DeckReshuffled, HandDiscarded, UnitMoved,
    }

    /// <summary>
    /// 규칙 이벤트 하나를 "그 순간의 값"과 함께 저장한 기록. 규칙은 한 번에 끝까지 계산하지만 화면은 천천히 재생하므로,
    /// 재생 시점에는 이미 바뀌어 있을 체력·칸·장수를 여기 복사해 둔다. 종류마다 쓰는 필드가 다르다.
    /// </summary>
    public sealed class BattleEvent
    {
        public BattleEvent(BattleEventKind kind)
        {
            Kind = kind;
        }

        public BattleEventKind Kind { get; }
        public Unit Unit;
        public Unit Target;
        public Team TargetTeam = Team.Ally;
        public Vector2Int TargetCell;
        public CardData Card;
        public EnemyAction Action = EnemyAction.Attack;
        public int Amount;
        public int Hp;
        public int Block;
        public string Text = "";
        public bool AllyWon;
        public int RoundIndex;
        public List<Unit> Order = new List<Unit>();
        public List<bool> Alive = new List<bool>();
        public int TurnIndex = -1;
        public List<CardData> Cards = new List<CardData>();
        public int DeckCount;
        public int DiscardCount;
        public Vector2Int FromCell;
        public Vector2Int ToCell;
        // 재생 때는 규칙의 칸이 이미 이동 뒤일 수 있어 기록 시점의 칸을 쓴다 (TurnStarted, Died).
        public Vector2Int Cell;
    }
}
