using System.Collections.Generic;
using UnityEngine;

namespace ProjectVoid.Combat
{
    /// <summary>전투 중인 유닛 한 명의 실제 상태. 이벤트는 내지 않는다 — BattleState 가 바꾼 뒤 알린다.</summary>
    public sealed class Unit
    {
        public Unit(int unitId, UnitData data, Team team, Vector2Int cell)
        {
            UnitId = unitId;
            Data = data;
            Team = team;
            Cell = cell;
            Hp = data.maxHp;
            if (data is AllyData ally)
            {
                Sp = ally.maxSp;
                // 에셋의 리스트를 직접 섞으면 원본 데이터가 바뀌므로 복사한다.
                Deck.AddRange(ally.deck);
            }
        }

        public int UnitId { get; }
        public UnitData Data { get; }
        public Team Team { get; }
        public Vector2Int Cell { get; set; }
        public int Hp { get; set; }
        public int Block { get; set; }
        public int Sp { get; set; }
        public List<CardData> Deck { get; } = new List<CardData>();
        public List<CardData> Hand { get; } = new List<CardData>();
        public List<CardData> Discard { get; } = new List<CardData>();
        public List<CardData> Exile { get; } = new List<CardData>();

        public bool IsAlive => Hp > 0;
        public bool IsAlly => Team == Team.Ally;

        public void TakeDamage(int amount)
        {
            int absorbed = Mathf.Min(Block, amount);
            Block -= absorbed;
            Hp = Mathf.Max(0, Hp - (amount - absorbed));
        }

        public void Heal(int amount)
        {
            Hp = Mathf.Min(Data.maxHp, Hp + amount);
        }

        public void GainBlock(int amount)
        {
            Block += amount;
        }

        public void ShuffleDeck(Rng rng)
        {
            Shuffle(Deck, rng);
        }

        /// <summary>묘지를 덱 뒤에 붙이고 섞는다. 덱이 비었을 때만 부른다. 옮긴 장수를 돌려준다.</summary>
        public int ReshuffleDiscard(Rng rng)
        {
            int count = Discard.Count;
            Deck.AddRange(Discard);
            Discard.Clear();
            Shuffle(Deck, rng);
            return count;
        }

        /// <summary>덱 맨 앞 한 장을 손패로. 덱이 비면 null. 리셔플은 하지 않는다 (BattleState 가 한 장마다 이벤트를 내려고 나눠 부른다).</summary>
        public CardData DrawOne()
        {
            if (Deck.Count == 0)
            {
                return null;
            }
            CardData card = Deck[0];
            Deck.RemoveAt(0);
            Hand.Add(card);
            return card;
        }

        /// <summary>이벤트 없는 드로우. 실제 전투는 같은 순서로 이벤트를 내는 BattleState 쪽을 쓴다.</summary>
        public void Draw(int count, Rng rng)
        {
            for (int i = 0; i < count; i++)
            {
                if (Deck.Count == 0)
                {
                    if (Discard.Count == 0)
                    {
                        return;
                    }
                    ReshuffleDiscard(rng);
                }
                DrawOne();
            }
        }

        public void DiscardHand()
        {
            Discard.AddRange(Hand);
            Hand.Clear();
        }

        // Fisher–Yates. Godot 과 같은 순서로 난수를 소비한다 (i 를 끝에서부터, j 는 0..i).
        private static void Shuffle(List<CardData> cards, Rng rng)
        {
            for (int i = cards.Count - 1; i > 0; i--)
            {
                int j = rng.RangeInclusive(0, i);
                (cards[i], cards[j]) = (cards[j], cards[i]);
            }
        }
    }
}
