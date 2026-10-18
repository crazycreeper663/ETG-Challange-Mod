using System.Collections;
using System.Linq;
using Dungeonator;
using Pathfinding;
using UnityEngine;

namespace ChallengeRuns.Floors
{
    // Runs the wave fight: seal on entry, spawn floor-appropriate enemies wave by wave, unseal at the end.
    internal static class ArenaController
    {
        private const string BulletKinGuid = "01972dee89fc4404a5c408d50007dad5";
        private const int BaseEnemiesPerWave = 3;
        private const int MaxEnemiesPerWave = 12;
        private const float StuckWaveTimeout = 120f;
        private const int SupplyDropEvery = 10;
        private const int AmmoId = 78;
        private const int HalfHeartId = 73;

        // Bumped on every level load so coroutines from a previous floor stop themselves.
        private static int levelToken;

        public static void OnLevelLoaded()
        {
            levelToken++;
            if (!ChallengeManager.IsActive("arena")) return;

            var rooms = GameManager.Instance.Dungeon?.data?.rooms;
            var arena = rooms?.FirstOrDefault(ArenaRoom.Is);
            if (arena != null)
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
            int waves = ChallengeManager.ArenaWaves.Value;

            for (int wave = 1; wave <= waves; wave++)
            {
                Notify($"Wave {wave} / {waves}", wave == waves ? "Final wave!" : "Survive.");
                yield return new WaitForSeconds(2f);
                if (token != levelToken) yield break;

                int count = Mathf.Min(MaxEnemiesPerWave, BaseEnemiesPerWave + wave / 4);
                for (int i = 0; i < count; i++)
                {
                    SpawnEnemy(room);
                    yield return new WaitForSeconds(0.15f);
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

                if (wave % SupplyDropEvery == 0 && wave < waves)
                    DropSupplies(room);
            }

            room.UnsealRoom();
            Notify("Arena cleared!", "Chests, a shop and the bosses await.");
        }

        private static void SpawnEnemy(RoomHandler room)
        {
            var prefab = PickEnemy(GameManager.Instance.Dungeon);
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

        private static AIActor PickEnemy(Dungeon dungeon)
        {
            for (int tries = 0; tries < 10; tries++)
            {
                var entry = dungeon.GetWeightedProceduralEnemy();
                if (string.IsNullOrEmpty(entry.enemyGuid)) continue;
                var prefab = EnemyDatabase.GetOrLoadByGuid(entry.enemyGuid);
                if (prefab == null || prefab.GetComponent<CompanionController>() != null) continue;
                if (prefab.healthHaver != null && prefab.healthHaver.IsBoss) continue;
                return prefab;
            }
            return EnemyDatabase.GetOrLoadByGuid(BulletKinGuid);
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

        private static void Notify(string header, string text)
        {
            ChallengeRuns.Notify.Show(header, text);
        }
    }
}
