using System;
using System.Collections.Generic;
using System.Linq;
using Dungeonator;
using UnityEngine;

namespace ChallengeRuns.Floors
{
    // Pulls the pieces we need (entrance, boss pool, shop, NPC/secret rooms) out of the
    // floor's original flow so custom layouts stay floor-appropriate.
    internal class FloorTemplate
    {
        public DungeonFlow Flow;
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

            var t = new FloorTemplate { Flow = flow, Entrance = entrance, Boss = boss };

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
            var room = PickPassThrough(Shop != null ? RoomsOf(Shop) : new List<PrototypeDungeonRoom>())
                       ?? PickPassThrough(RoomsOf(SharedAssets.ShopRoomTable));
            return room == null ? null : FlowFactory.NewNode(flow, PrototypeDungeonRoom.RoomCategory.SPECIAL, room);
        }

        // Same idea for an NPC/shrine room; null if every variant is a dead end.
        public DungeonFlowNode MakePassThrough(DungeonFlow flow, DungeonFlowNode special)
        {
            var room = PickPassThrough(RoomsOf(special));
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

        private static List<PrototypeDungeonRoom> RoomsOf(GenericRoomTable table)
        {
            var rooms = new List<PrototypeDungeonRoom>();
            if (table != null) CollectRooms(table, rooms);
            return rooms;
        }

        private static PrototypeDungeonRoom PickPassThrough(List<PrototypeDungeonRoom> rooms)
        {
            var ok = rooms.Where(r => r.exitData?.exits != null && r.exitData.exits.Count >= 2).ToList();
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

            FlowFactory.Shuffle(result);
            return result;
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

        private static bool PrerequisitesMet(PrototypeDungeonRoom room)
        {
            if (room.prerequisites == null) return true;
            try
            {
                return room.prerequisites.All(p => p.CheckConditionsFulfilled());
            }
            catch (Exception)
            {
                return true;
            }
        }

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

        public static PrototypeDungeonRoom RewardRoom => rewardRoom ?? (rewardRoom = Load<PrototypeDungeonRoom>("reward room"));
        public static PrototypeDungeonRoom BossFoyer => bossFoyer ?? (bossFoyer = Load<PrototypeDungeonRoom>("boss foyer"));
        public static GenericRoomTable ShopRoomTable => shopRoomTable ?? (shopRoomTable = Load<GenericRoomTable>("Shop Room Table"));

        private static T Load<T>(string name) where T : UnityEngine.Object
        {
            var asset = ResourceManager.LoadAssetBundle("shared_auto_002").LoadAsset<T>(name);
            if (asset == null) Debug.LogError($"[Challenge Runs] Missing shared asset '{name}'");
            return asset;
        }
    }
}
