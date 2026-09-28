using System.Collections.Generic;
using ProjectVoid.Combat;
using UnityEngine;

namespace ProjectVoid.Tests
{
    /// <summary>테스트용 데이터 생성기 (Godot 테스트의 _ally/_enemy/_placement 헬퍼 자리). 만든 에셋은 Cleanup 에서 지운다.</summary>
    public static class Make
    {
        private static readonly List<Object> Created = new List<Object>();

        public static T Asset<T>() where T : ScriptableObject
        {
            var asset = ScriptableObject.CreateInstance<T>();
            Created.Add(asset);
            return asset;
        }

        public static CardData Card(string id, int damage = 0, int spCost = 1, int attackRange = 1,
            AttackType attackType = AttackType.Melee, Shape shape = Shape.Single)
        {
            var card = Asset<CardData>();
            card.id = id;
            card.displayName = id;
            card.damage = damage;
            card.spCost = spCost;
            card.attackRange = attackRange;
            card.attackType = attackType;
            card.shape = shape;
            return card;
        }

        public static AllyData Ally(string id, int maxHp = 20, int speed = 10, int maxSp = 3, params CardData[] deck)
        {
            var data = Asset<AllyData>();
            data.id = id;
            data.displayName = id;
            data.maxHp = maxHp;
            data.speed = speed;
            data.maxSp = maxSp;
            data.deck = new List<CardData>(deck);
            return data;
        }

        public static EnemyData Enemy(string id, int maxHp = 20, int speed = 10, int attackDamage = 5, int attackRange = 1,
            AttackType attackType = AttackType.Melee, Shape attackShape = Shape.Single,
            int blockAmount = 5, int restHeal = 4, float moveChance = 0f)
        {
            var data = Asset<EnemyData>();
            data.id = id;
            data.displayName = id;
            data.maxHp = maxHp;
            data.speed = speed;
            data.attackDamage = attackDamage;
            data.attackRange = attackRange;
            data.attackType = attackType;
            data.attackShape = attackShape;
            data.blockAmount = blockAmount;
            data.restHeal = restHeal;
            data.moveChance = moveChance;
            return data;
        }

        public static UnitPlacement Place(UnitData data, int col, int row) => new UnitPlacement(data, new Vector2Int(col, row));

        public static EncounterData Encounter(Vector2Int allyGrid, Vector2Int enemyGrid,
            IEnumerable<UnitPlacement> allies, IEnumerable<UnitPlacement> enemies)
        {
            var encounter = Asset<EncounterData>();
            encounter.allyGrid = allyGrid;
            encounter.enemyGrid = enemyGrid;
            encounter.allyUnits = new List<UnitPlacement>(allies);
            encounter.enemyUnits = new List<UnitPlacement>(enemies);
            return encounter;
        }

        public static BattleState State(EncounterData encounter, int seed) => new BattleState(encounter, new Rng(seed));

        public static void Cleanup()
        {
            foreach (Object asset in Created)
            {
                if (asset != null)
                {
                    Object.DestroyImmediate(asset);
                }
            }
            Created.Clear();
        }
    }
}
