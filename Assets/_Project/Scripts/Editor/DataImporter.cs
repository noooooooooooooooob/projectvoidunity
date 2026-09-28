using System.Collections.Generic;
using System.IO;
using ProjectVoid.Combat;
using UnityEditor;
using UnityEngine;

namespace ProjectVoid.EditorTools
{
    /// <summary>Godot Resources/*.tres 의 수치를 .asset 으로 옮긴다. 이미 있는 에셋은 값만 덮어써 GUID 와 sprite 지정을 유지한다.</summary>
    public static class DataImporter
    {
        public const string CardsDir = "Assets/_Project/Data/Cards";
        public const string UnitsDir = "Assets/_Project/Data/Units";
        public const string EncountersDir = "Assets/_Project/Data/Encounters";

        [MenuItem("Project Void/Import Starter Data")]
        public static void ImportAll()
        {
            Directory.CreateDirectory(CardsDir);
            Directory.CreateDirectory(UnitsDir);
            Directory.CreateDirectory(EncountersDir);

            CardData strike = Card("strike", "베기", 1, AttackType.Melee, Shape.Single, 2, 6);
            CardData cleave = Card("cleave", "횡베기", 2, AttackType.Melee, Shape.Sweep, 2, 4);
            CardData skewer = Card("skewer", "꿰뚫기", 2, AttackType.Melee, Shape.Line, 3, 4);
            CardData shoot = Card("shoot", "사격", 1, AttackType.Ranged, Shape.Single, 3, 4);
            CardData piercingShot = Card("piercing_shot", "관통사격", 2, AttackType.Ranged, Shape.Pierce, 3, 5);
            CardData volley = Card("volley", "일제사격", 2, AttackType.Ranged, Shape.Sweep, 4, 3);
            CardData blast = Card("blast", "폭발탄", 2, AttackType.Ranged, Shape.Area, 3, 3);

            AllyData vanguard = Ally("vanguard", "선봉", 30, 12, strike, strike, strike, cleave, cleave, shoot, skewer, skewer);
            AllyData archer = Ally("archer", "사수", 20, 10, shoot, shoot, shoot, volley, piercingShot, piercingShot, blast, blast);
            AllyData scout = Ally("scout", "정찰병", 22, 16, strike, strike, shoot, shoot, volley, cleave);

            EnemyData brute = Enemy("brute", "괴한", 28, 8, 7, AttackType.Melee, Shape.Single, 1, 6, 5);
            EnemyData sentry = Enemy("sentry", "보초", 24, 6, 5, AttackType.Ranged, Shape.Sweep, 4, 8, 3);
            EnemyData stalker = Enemy("stalker", "추적자", 18, 14, 4, AttackType.Ranged, Shape.Pierce, 3, 4, 4);

            var skirmish = LoadOrCreate<EncounterData>($"{EncountersDir}/skirmish.asset");
            skirmish.allyGrid = new Vector2Int(3, 3);
            skirmish.enemyGrid = new Vector2Int(2, 2);
            skirmish.allyUnits = new List<UnitPlacement>
            {
                new UnitPlacement(vanguard, new Vector2Int(0, 1)),
                new UnitPlacement(archer, new Vector2Int(2, 0)),
                new UnitPlacement(scout, new Vector2Int(1, 2)),
            };
            skirmish.enemyUnits = new List<UnitPlacement>
            {
                new UnitPlacement(brute, new Vector2Int(0, 0)),
                new UnitPlacement(stalker, new Vector2Int(1, 1)),
                new UnitPlacement(sentry, new Vector2Int(1, 0)),
            };
            EditorUtility.SetDirty(skirmish);
            AssetDatabase.SaveAssets();
        }

        private static CardData Card(string id, string displayName, int spCost, AttackType attackType, Shape shape, int attackRange, int damage)
        {
            var card = LoadOrCreate<CardData>($"{CardsDir}/{id}.asset");
            card.id = id;
            card.displayName = displayName;
            card.spCost = spCost;
            card.attackType = attackType;
            card.shape = shape;
            card.attackRange = attackRange;
            card.damage = damage;
            EditorUtility.SetDirty(card);
            return card;
        }

        private static AllyData Ally(string id, string displayName, int maxHp, int speed, params CardData[] deck)
        {
            var ally = LoadOrCreate<AllyData>($"{UnitsDir}/{id}.asset");
            ally.id = id;
            ally.displayName = displayName;
            ally.maxHp = maxHp;
            ally.speed = speed;
            ally.maxSp = 3;
            ally.deck = new List<CardData>(deck);
            EditorUtility.SetDirty(ally);
            return ally;
        }

        private static EnemyData Enemy(string id, string displayName, int maxHp, int speed, int attackDamage,
            AttackType attackType, Shape attackShape, int attackRange, int blockAmount, int restHeal)
        {
            var enemy = LoadOrCreate<EnemyData>($"{UnitsDir}/{id}.asset");
            enemy.id = id;
            enemy.displayName = displayName;
            enemy.maxHp = maxHp;
            enemy.speed = speed;
            enemy.attackDamage = attackDamage;
            enemy.attackType = attackType;
            enemy.attackShape = attackShape;
            enemy.attackRange = attackRange;
            enemy.blockAmount = blockAmount;
            enemy.restHeal = restHeal;
            enemy.moveChance = 0.25f;
            EditorUtility.SetDirty(enemy);
            return enemy;
        }

        private static T LoadOrCreate<T>(string path) where T : ScriptableObject
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset == null)
            {
                asset = ScriptableObject.CreateInstance<T>();
                AssetDatabase.CreateAsset(asset, path);
            }
            return asset;
        }
    }
}
