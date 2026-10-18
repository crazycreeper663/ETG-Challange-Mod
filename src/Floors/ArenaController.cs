using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Dungeonator;
using Pathfinding;
using UnityEngine;
using Random = UnityEngine.Random;

namespace ChallengeRuns.Floors
{
    // Runs the wave fight: seal on entry, then spawn one combat room's worth of enemies at a time
    // (taken from the floor's own combat rooms), next wave once they're all dead, unseal at the end.
    internal static class ArenaController
    {
        private const string BulletKinGuid = "01972dee89fc4404a5c408d50007dad5";
        private const int MaxEnemiesPerWave = 20;
        private const int FallbackEnemiesPerWave = 6;
        private const int RoomPickAttempts = 15;
        private const float StuckWaveTimeout = 120f;
        private const int SupplyDropEvery = 10;
        private const int AmmoId = 78;
        private const int HalfHeartId = 73;

        // Set when an arena floor is built: the floor's normal combat rooms, used as wave templates.
        public static GenericRoomTable WaveRooms;

        // Bumped on every level load so coroutines from a previous floor stop themselves.
        private static int levelToken;

        public static void OnLevelLoaded()
        {
            levelToken++;
            if (!ChallengeManager.IsActive("arena")) return;

            var rooms = GameManager.Instance.Dungeon?.data?.rooms;
            var arena = rooms?.FirstOrDefault(ArenaRoom.Is);
            if (arena == null)
            {
                Debug.Log("[Challenge Runs] No arena room on this floor.");
                return;
            }
            Debug.Log("[Challenge Runs] Arena room found; waves start when you walk in.");
            Plugin.Instance.StartCoroutine(RunArena(arena, levelToken));
        }

        private static IEnumerator RunArena(RoomHandler room, int token)
        {
            while (!GameManager.Instance.AllPlayers.Any(p => p != null && p.CurrentRoom == room))
            {
                if (token != levelToken) yield break;
                yield return null;
            }

            room.SealRoom();
            var templates = WaveRoomTemplates();
            int waves = ChallengeManager.ArenaWaves.Value;

            for (int wave = 1; wave <= waves; wave++)
            {
                Notify.Show($"Wave {wave} / {waves}", wave == waves ? "Final wave!" : "Survive.");
                yield return new WaitForSeconds(2f);
                if (token != levelToken) yield break;

                var enemies = PickWave(templates, out string source);
                Debug.Log($"[Challenge Runs] Arena wave {wave}/{waves}: {enemies.Count} enemies ({source})");
                foreach (var prefab in enemies)
                {
                    TrySpawn(room, prefab);
                    yield return new WaitForSeconds(0.1f);
                }

                yield return new WaitForSeconds(1f);
                float elapsed = 0f;
                while (room.GetActiveEnemiesCount(RoomHandler.ActiveEnemyType.RoomClear) > 0)
                {
                    if (token != levelToken) yield break;
                    elapsed += Time.deltaTime;
                    if (elapsed > StuckWaveTimeout) EraseRemaining(room);
                    yield return null;
                }
                // Anything left that doesn't count for clearing (e.g. something a mod spawned) goes too.
                EraseRemaining(room);

                if (wave % SupplyDropEvery == 0 && wave < waves)
                    DropSupplies(room);
            }

            room.UnsealRoom();
            Notify.Show("Arena cleared!", "Chests, a shop and the bosses await.");
        }

        private static List<PrototypeDungeonRoom> WaveRoomTemplates()
        {
            if (WaveRooms == null) return new List<PrototypeDungeonRoom>();
            return FloorTemplate.AllRooms(WaveRooms)
                .Where(r => r.category == PrototypeDungeonRoom.RoomCategory.NORMAL)
                .ToList();
        }

        // One combat room's worth of enemies; random floor enemies if no room template works out.
        private static List<AIActor> PickWave(List<PrototypeDungeonRoom> templates, out string source)
        {
            for (int i = 0; i < RoomPickAttempts && templates.Count > 0; i++)
            {
                var room = templates[Random.Range(0, templates.Count)];
                var enemies = EnemiesIn(room);
                if (enemies.Count > 0)
                {
                    source = room.name;
                    return enemies.Take(MaxEnemiesPerWave).ToList();
                }
            }

            source = "random floor enemies";
            var fallback = new List<AIActor>();
            for (int i = 0; i < FallbackEnemiesPerWave; i++)
            {
                var prefab = PickProceduralEnemy();
                if (prefab != null) fallback.Add(prefab);
            }
            return fallback;
        }

        // Every enemy the room would spawn, including its reinforcement layers.
        private static List<AIActor> EnemiesIn(PrototypeDungeonRoom room)
        {
            var placed = new List<PrototypePlacedObjectData>();
            if (room.placedObjects != null) placed.AddRange(room.placedObjects);
            if (room.additionalObjectLayers != null)
                foreach (var layer in room.additionalObjectLayers)
                    if (layer?.placedObjects != null) placed.AddRange(layer.placedObjects);

            var result = new List<AIActor>();
            foreach (var obj in placed)
            {
                if (obj == null) continue;
                var prefab = UsableEnemy(EnemyGuidOf(obj));
                if (prefab != null) result.Add(prefab);
            }
            return result;
        }

        private static string EnemyGuidOf(PrototypePlacedObjectData obj)
        {
            if (!string.IsNullOrEmpty(obj.enemyBehaviourGuid)) return obj.enemyBehaviourGuid;

            // "Random enemy" slots pick one of several variants when the room loads; do the same.
            var tiers = obj.placeableContents?.variantTiers;
            if (tiers == null) return null;
            var options = tiers.Where(v => v != null && !string.IsNullOrEmpty(v.enemyPlaceableGuid)).ToList();
            return options.Count == 0 ? null : options[Random.Range(0, options.Count)].enemyPlaceableGuid;
        }

        private static AIActor UsableEnemy(string guid)
        {
            if (string.IsNullOrEmpty(guid)) return null;
            var prefab = EnemyDatabase.GetOrLoadByGuid(guid);
            if (prefab == null || prefab.GetComponent<CompanionController>() != null) return null;
            if (prefab.healthHaver != null && prefab.healthHaver.IsBoss) return null;
            // Turrets and the like don't count towards clearing a room, so they'd never let a wave end.
            if (prefab.IgnoreForRoomClear) return null;
            return prefab;
        }

        private static AIActor PickProceduralEnemy()
        {
            for (int tries = 0; tries < 10; tries++)
            {
                var prefab = UsableEnemy(GameManager.Instance.Dungeon.GetWeightedProceduralEnemy().enemyGuid);
                if (prefab != null) return prefab;
            }
            return EnemyDatabase.GetOrLoadByGuid(BulletKinGuid);
        }

        // A failed spawn just means one fewer enemy; it must never stop the wave loop while the room is sealed.
        private static void TrySpawn(RoomHandler room, AIActor prefab)
        {
            try
            {
                Spawn(room, prefab);
            }
            catch (Exception e)
            {
                Debug.LogError($"[Challenge Runs] Couldn't spawn {prefab?.name}: {e}");
            }
        }

        private static void Spawn(RoomHandler room, AIActor prefab)
        {
            if (prefab == null || prefab.specRigidbody == null) return;

            var clearance = prefab.specRigidbody.UnitDimensions.ToIntVector2(VectorConversions.Ceil);
            var data = GameManager.Instance.Dungeon.data;
            CellValidator validator = c =>
            {
                for (int x = 0; x < clearance.x; x++)
                    for (int y = 0; y < clearance.y; y++)
                        if (data.isTopWall(c.x + x, c.y + y)) return false;
                // Don't drop enemies right on top of a player.
                return GameManager.Instance.AllPlayers.All(p => p == null ||
                    Vector2.Distance(c.ToVector2(), p.CenterPosition) > 5f);
            };

            var cell = room.GetRandomAvailableCell(clearance, CellTypes.FLOOR, false, validator);
            if (cell.HasValue)
                AIActor.Spawn(prefab, cell.Value, room, true, AIActor.AwakenAnimationType.Spawn, true);
        }

        private static void EraseRemaining(RoomHandler room)
        {
            var enemies = room.GetActiveEnemies(RoomHandler.ActiveEnemyType.All);
            if (enemies == null) return;
            foreach (var enemy in enemies.ToList())
                if (enemy != null && enemy.IsNormalEnemy)
                    enemy.EraseFromExistence(false);
        }

        private static void DropSupplies(RoomHandler room)
        {
            Vector3 center = room.GetCenterCell().ToVector2();
            LootEngine.SpawnItem(PickupObjectDatabase.GetById(AmmoId).gameObject, center, Vector2.left, 1f);
            LootEngine.SpawnItem(PickupObjectDatabase.GetById(HalfHeartId).gameObject, center, Vector2.right, 1f);
        }
    }
}
