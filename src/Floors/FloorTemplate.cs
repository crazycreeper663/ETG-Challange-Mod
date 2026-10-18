using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Dungeonator;
using HarmonyLib;
using UnityEngine;

namespace ChallengeRuns.Floors
{
    // Pulls the pieces we need (entrance, boss pool, shop, NPC/secret rooms) out of the
    // floor's original flow so custom layouts stay floor-appropriate.
    internal class FloorTemplate
    {
        public DungeonFlow Flow;
        public GlobalDungeonData.ValidTilesets Tileset;
        public DungeonFlowNode Entrance;
        public DungeonFlowNode Boss;
        public DungeonFlowNode BossFoyer;
        public DungeonFlowNode Shop;
        public GenericRoomTable CombatRoomTable;
        public readonly List<DungeonFlowNode> SpecialRooms = new List<DungeonFlowNode>();

        private bool madeFirstChest;

        public static FloorTemplate Analyze(DungeonFlow flow)
        {
            var nodes = flow.AllNodes;
            if (nodes == null || nodes.Count == 0) return null;

            var entrance = flow.FirstNode ?? nodes.FirstOrDefault(n => n.roomCategory == PrototypeDungeonRoom.RoomCategory.ENTRANCE);
            var bosses = nodes.Where(n => n.roomCategory == PrototypeDungeonRoom.RoomCategory.BOSS).ToList();
            var boss = bosses.FirstOrDefault(b => HasDescendant(flow, b, PrototypeDungeonRoom.RoomCategory.EXIT)) ?? bosses.FirstOrDefault();
            if (entrance == null || boss == null) return null;

            var dungeon = GameManager.Instance.CurrentlyGeneratingDungeonPrefab;
            if (dungeon == null) dungeon = GameManager.Instance.Dungeon;
            if (dungeon == null) return null;

            var t = new FloorTemplate { Flow = flow, Tileset = dungeon.tileIndices.tilesetId, Entrance = entrance, Boss = boss };

            var bossParent = flow.GetNodeFromGuid(boss.parentNodeGuid);
            if (bossParent != null && bossParent.roomCategory == PrototypeDungeonRoom.RoomCategory.CONNECTOR && bossParent.overrideExactRoom != null)
                t.BossFoyer = bossParent;

            var bossTail = new HashSet<DungeonFlowNode>();
            CollectSubtree(flow, boss, bossTail);

            foreach (var node in nodes)
            {
                if (node == entrance || node == t.BossFoyer || bossTail.Contains(node)) continue;
                if (IsShop(node))
                {
                    if (t.Shop == null && (node.overrideExactRoom != null || node.overrideRoomTable != null)) t.Shop = node;
                }
                else if ((node.roomCategory == PrototypeDungeonRoom.RoomCategory.SPECIAL ||
                          node.roomCategory == PrototypeDungeonRoom.RoomCategory.SECRET) &&
                         (node.overrideExactRoom != null || node.overrideRoomTable != null))
                {
                    t.SpecialRooms.Add(node);
                }
            }

            t.CombatRoomTable = flow.fallbackRoomTable ?? nodes
                .Where(n => n.roomCategory == PrototypeDungeonRoom.RoomCategory.NORMAL && n.overrideRoomTable != null)
                .GroupBy(n => n.overrideRoomTable)
                .OrderByDescending(g => g.Count())
                .Select(g => g.Key)
                .FirstOrDefault();

            return t;
        }

        public DungeonFlowNode MakeShop(DungeonFlow flow)
        {
            if (Shop != null) return FlowFactory.Clone(flow, Shop);
            return FlowFactory.NewNode(flow, PrototypeDungeonRoom.RoomCategory.SPECIAL, table: SharedAssets.ShopRoomTable);
        }

        // A shop variant with 2+ doors so it can sit on a linear trail; null if the floor has none.
        public DungeonFlowNode MakePassThroughShop(DungeonFlow flow)
        {
            var room = PickWithDoors(Shop != null ? RoomsOf(Shop) : new List<PrototypeDungeonRoom>(), 2)
                       ?? PickWithDoors(RoomsOf(SharedAssets.ShopRoomTable), 2);
            return room == null ? null : FlowFactory.NewNode(flow, PrototypeDungeonRoom.RoomCategory.SPECIAL, room);
        }

        // Same idea for an NPC/shrine room; null if every variant is a dead end.
        public DungeonFlowNode MakePassThrough(DungeonFlow flow, DungeonFlowNode special)
        {
            var room = PickWithDoors(RoomsOf(special), 2);
            if (room == null) return null;
            var node = FlowFactory.Clone(flow, special);
            node.overrideExactRoom = room;
            node.overrideRoomTable = null;
            return node;
        }

        private static List<PrototypeDungeonRoom> RoomsOf(DungeonFlowNode node)
        {
            if (node.overrideExactRoom != null) return new List<PrototypeDungeonRoom> { node.overrideExactRoom };
            return RoomsOf(node.overrideRoomTable);
        }

        public static List<PrototypeDungeonRoom> AllRooms(GenericRoomTable table) => RoomsOf(table);

        private static List<PrototypeDungeonRoom> RoomsOf(GenericRoomTable table)
        {
            var rooms = new List<PrototypeDungeonRoom>();
            if (table != null) CollectRooms(table, rooms);
            return rooms;
        }

        private static PrototypeDungeonRoom PickWithDoors(List<PrototypeDungeonRoom> rooms, int minDoors)
        {
            var ok = rooms.Where(r => r.exitData?.exits != null && r.exitData.exits.Count >= minDoors).ToList();
            return ok.Count == 0 ? null : ok[UnityEngine.Random.Range(0, ok.Count)];
        }

        // Same pattern ExpandTheGungeon uses for multiple chest rooms in one flow:
        // only the first is a REWARD node, the rest are connectors using the reward room.
        public DungeonFlowNode MakeChest(DungeonFlow flow)
        {
            var category = madeFirstChest ? PrototypeDungeonRoom.RoomCategory.CONNECTOR : PrototypeDungeonRoom.RoomCategory.REWARD;
            madeFirstChest = true;
            return FlowFactory.NewNode(flow, category, SharedAssets.RewardRoom);
        }

        public DungeonFlowNode MakeBossFoyer(DungeonFlow flow)
        {
            if (BossFoyer != null) return FlowFactory.Clone(flow, BossFoyer);
            return FlowFactory.NewNode(flow, PrototypeDungeonRoom.RoomCategory.CONNECTOR, SharedAssets.BossFoyer);
        }

        // Every distinct boss this floor can roll, one room variant each, in random order.
        public List<PrototypeDungeonRoom> OneRoomPerBoss()
        {
            // The game picks a floor's boss from BossManager (one room table per boss), not from
            // the flow's boss node, so that's the real list of options.
            var result = new List<PrototypeDungeonRoom>();
            foreach (var table in BossTablesForFloor())
            {
                var rooms = RoomsOf(table);
                if (rooms.Count > 0) result.Add(rooms[UnityEngine.Random.Range(0, rooms.Count)]);
            }
            if (result.Count == 0) result = BossRoomsFromNode();
            FlowFactory.Shuffle(result);
            return result;
        }

        // BossManager's data and methods are private in the game, and the game refuses direct calls
        // to them (MethodAccessException), so everything here goes through reflection.
        private List<GenericRoomTable> BossTablesForFloor()
        {
            var tables = new List<GenericRoomTable>();
            var manager = Traverse.Create(GameManager.Instance).Property("BossManager").GetValue();
            if (manager == null) return tables;

            if (Traverse.Create(manager).Field("BossFloorData").GetValue() is IEnumerable floors)
            {
                foreach (var floor in floors)
                {
                    if (!CoversThisFloor(floor)) continue;
                    if (Traverse.Create(floor).Field("Bosses").GetValue() is IEnumerable bosses)
                        foreach (var boss in bosses)
                            AddBossTable(tables, boss, () => Traverse.Create(boss).Method("GlobalPrereqsValid").GetValue<bool>());
                }
            }

            // Rare bosses that can replace the usual pick on this floor.
            if (Traverse.Create(manager).Field("OverrideBosses").GetValue() is IEnumerable overrides)
                foreach (var boss in overrides)
                    if (CoversThisFloor(boss))
                        AddBossTable(tables, boss, () => Traverse.Create(boss).Method("GlobalPrereqsValid", new object[] { Tileset }).GetValue<bool>());

            return tables.Distinct().ToList();
        }

        private bool CoversThisFloor(object entry) =>
            entry != null && (Traverse.Create(entry).Field("AssociatedTilesets").GetValue<GlobalDungeonData.ValidTilesets>() & Tileset) != 0;

        private static void AddBossTable(List<GenericRoomTable> tables, object boss, Func<bool> prerequisitesMet)
        {
            if (boss == null) return;
            var table = Traverse.Create(boss).Field("TargetRoomTable").GetValue<GenericRoomTable>();
            if (table != null && SafeCheck(prerequisitesMet)) tables.Add(table);
        }

        // Fallback for floors BossManager doesn't cover: read the pool off the flow's boss node.
        private List<PrototypeDungeonRoom> BossRoomsFromNode()
        {
            var result = new List<PrototypeDungeonRoom>();
            if (Boss.overrideExactRoom != null)
            {
                result.Add(Boss.overrideExactRoom);
                return result;
            }

            var table = Boss.overrideRoomTable;
            if (table == null) return result;

            // Vanilla boss pools nest one sub-table per boss (e.g. bosstable_01_bulletking).
            if (table.includedRoomTables != null)
            {
                foreach (var sub in table.includedRoomTables)
                {
                    var rooms = new List<PrototypeDungeonRoom>();
                    CollectRooms(sub, rooms);
                    if (rooms.Count > 0) result.Add(rooms[UnityEngine.Random.Range(0, rooms.Count)]);
                }
            }

            // Rooms listed directly: group variants of the same boss together.
            var direct = new List<PrototypeDungeonRoom>();
            CollectDirectRooms(table, direct);
            foreach (var group in direct.GroupBy(BossKey))
            {
                var rooms = group.ToList();
                result.Add(rooms[UnityEngine.Random.Range(0, rooms.Count)]);
            }
            return result;
        }

        // The NPC rooms (Winchester, Gunsling King, Chancelot, ...) the game assigned to this floor
        // at the start of the run. They normally hang off the layout as side rooms, so they're
        // taken out of the run's blueprint and placed by the layout instead: Marathon needs rooms
        // with two doors (trail stops), Arena takes any (behind a spare arena door).
        private static readonly Dictionary<List<RuntimeInjectionMetadata>, List<PrototypeDungeonRoom>> takenBlueprints =
            new Dictionary<List<RuntimeInjectionMetadata>, List<PrototypeDungeonRoom>>();

        // What each emptied blueprint held, so it can be put back if the floor ends up using its normal layout.
        private static readonly Dictionary<List<RuntimeInjectionMetadata>, List<RuntimeInjectionMetadata>> originalBlueprints =
            new Dictionary<List<RuntimeInjectionMetadata>, List<RuntimeInjectionMetadata>>();

        private List<RuntimeInjectionMetadata> pendingEmptyBlueprint;

        // Picks the trail-friendly NPC rooms; the blueprint itself is only emptied by
        // CommitBlueprintChanges once the whole floor has been built.
        public List<PrototypeDungeonRoom> TakeBlueprintNpcRooms(int minDoors)
        {
            var blueprint = MetaInjectionData.CurrentRunBlueprint;
            if (blueprint == null || !blueprint.ContainsKey(Tileset)) return new List<PrototypeDungeonRoom>();

            // If the floor gets regenerated, hand back what was taken the first time.
            var entries = blueprint[Tileset];
            if (entries != null && takenBlueprints.TryGetValue(entries, out var alreadyTaken)) return alreadyTaken;

            var rooms = new List<PrototypeDungeonRoom>();
            if (entries != null)
            {
                foreach (var meta in entries)
                {
                    var room = PickNpcRoom(meta, minDoors);
                    if (room != null) rooms.Add(room);
                }
            }

            pendingEmptyBlueprint = new List<RuntimeInjectionMetadata>();
            takenBlueprints[pendingEmptyBlueprint] = rooms;
            originalBlueprints[pendingEmptyBlueprint] = entries;
            return rooms;
        }

        public void CommitBlueprintChanges()
        {
            if (pendingEmptyBlueprint == null || MetaInjectionData.CurrentRunBlueprint == null) return;
            MetaInjectionData.CurrentRunBlueprint[Tileset] = pendingEmptyBlueprint;
            pendingEmptyBlueprint = null;
        }

        // Undoes CommitBlueprintChanges for this floor, for when it gets its normal layout after all.
        public void RestoreBlueprint()
        {
            var blueprint = MetaInjectionData.CurrentRunBlueprint;
            if (blueprint == null || !blueprint.ContainsKey(Tileset)) return;
            var current = blueprint[Tileset];
            if (current != null && originalBlueprints.TryGetValue(current, out var original))
                blueprint[Tileset] = original;
        }

        private static PrototypeDungeonRoom PickNpcRoom(RuntimeInjectionMetadata meta, int minDoors)
        {
            var options = meta.AssignedModifierData != null
                ? new List<ProceduralFlowModifierData> { meta.AssignedModifierData }
                : meta.injectionData?.InjectionData?.Where(m => m != null).ToList() ?? new List<ProceduralFlowModifierData>();
            FlowFactory.Shuffle(options);

            foreach (var mod in options)
            {
                if (mod.prerequisites != null && !SafeCheck(() => mod.prerequisites.All(p => p.CheckConditionsFulfilled())))
                    continue;
                var rooms = mod.exactRoom != null ? new List<PrototypeDungeonRoom> { mod.exactRoom } : RoomsOf(mod.roomTable);
                var room = PickWithDoors(rooms, minDoors);
                if (room != null) return room;
            }
            return null;
        }

        public static int DoorCount(PrototypeDungeonRoom room) => room?.exitData?.exits?.Count ?? 0;

        public static DungeonFlowNode NodeFor(DungeonFlow flow, PrototypeDungeonRoom room) =>
            FlowFactory.NewNode(flow, room.category, room);

        private static bool SafeCheck(Func<bool> check)
        {
            try
            {
                return check();
            }
            catch (Exception)
            {
                return true;
            }
        }

        private static string BossKey(PrototypeDungeonRoom room)
        {
            var placed = new List<PrototypePlacedObjectData>();
            if (room.placedObjects != null) placed.AddRange(room.placedObjects);
            if (room.additionalObjectLayers != null)
                foreach (var layer in room.additionalObjectLayers)
                    if (layer.placedObjects != null) placed.AddRange(layer.placedObjects);

            foreach (var obj in placed)
            {
                if (string.IsNullOrEmpty(obj.enemyBehaviourGuid)) continue;
                var enemy = EnemyDatabase.GetOrLoadByGuid(obj.enemyBehaviourGuid);
                if (enemy != null && enemy.healthHaver != null && enemy.healthHaver.IsBoss)
                    return obj.enemyBehaviourGuid;
            }
            return room.name;
        }

        private static void CollectRooms(GenericRoomTable table, List<PrototypeDungeonRoom> into)
        {
            CollectDirectRooms(table, into);
            if (table.includedRoomTables != null)
                foreach (var sub in table.includedRoomTables)
                    if (sub != null) CollectRooms(sub, into);
        }

        private static void CollectDirectRooms(GenericRoomTable table, List<PrototypeDungeonRoom> into)
        {
            if (table?.includedRooms?.elements == null) return;
            foreach (var weighted in table.includedRooms.elements)
                if (weighted.room != null && weighted.weight > 0f && PrerequisitesMet(weighted.room))
                    into.Add(weighted.room);
        }

        private static bool PrerequisitesMet(PrototypeDungeonRoom room) =>
            room.prerequisites == null || SafeCheck(() => room.prerequisites.All(p => p.CheckConditionsFulfilled()));

        private static bool IsShop(DungeonFlowNode node)
        {
            if (node.roomCategory != PrototypeDungeonRoom.RoomCategory.SPECIAL) return false;
            if (node.overrideRoomTable != null && node.overrideRoomTable.name.ToLowerInvariant().Contains("shop")) return true;
            var room = node.overrideExactRoom;
            return room != null && room.subCategorySpecial == PrototypeDungeonRoom.RoomSpecialSubCategory.STANDARD_SHOP;
        }

        private static void CollectSubtree(DungeonFlow flow, DungeonFlowNode node, HashSet<DungeonFlowNode> into)
        {
            if (!into.Add(node)) return;
            foreach (var guid in node.childNodeGuids)
            {
                var child = flow.GetNodeFromGuid(guid);
                if (child != null) CollectSubtree(flow, child, into);
            }
        }

        private static bool HasDescendant(DungeonFlow flow, DungeonFlowNode node, PrototypeDungeonRoom.RoomCategory category)
        {
            var all = new HashSet<DungeonFlowNode>();
            CollectSubtree(flow, node, all);
            return all.Any(n => n != node && n.roomCategory == category);
        }
    }

    internal static class SharedAssets
    {
        private static PrototypeDungeonRoom rewardRoom, bossFoyer;
        private static GenericRoomTable shopRoomTable;

        // Unity's null check (not ??), so an asset that got unloaded is loaded again instead of reused.
        public static PrototypeDungeonRoom RewardRoom => rewardRoom ? rewardRoom : (rewardRoom = Load<PrototypeDungeonRoom>("reward room"));
        public static PrototypeDungeonRoom BossFoyer => bossFoyer ? bossFoyer : (bossFoyer = Load<PrototypeDungeonRoom>("boss foyer"));
        public static GenericRoomTable ShopRoomTable => shopRoomTable ? shopRoomTable : (shopRoomTable = Load<GenericRoomTable>("Shop Room Table"));

        private static T Load<T>(string name) where T : UnityEngine.Object
        {
            var asset = ResourceManager.LoadAssetBundle("shared_auto_002").LoadAsset<T>(name);
            if (asset == null) Debug.LogError($"[Challenge Runs] Missing shared asset '{name}'");
            return asset;
        }
    }
}
