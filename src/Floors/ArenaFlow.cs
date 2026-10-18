using System.Linq;
using Dungeonator;
using UnityEngine;
using static ChallengeRuns.Floors.FlowFactory;

namespace ChallengeRuns.Floors
{
    //   Entrance - ARENA -+- chest rooms
    //                     +- shop
    //                     +- NPC rooms (spare doors)
    //                     +- Boss - Boss - ... - Exit
    //
    // ArenaController seals the arena when you walk in and only unseals it after the last wave.
    internal static class ArenaFlow
    {
        public static DungeonFlow Build(FloorTemplate t, int chestRooms, bool allowSideRooms)
        {
            // The game's injected side rooms (secret rooms and the like) made most layouts fail to
            // generate around the arena, so by default they're left out, as on Marathon floors.
            var flow = CreateFlow(t, "ChallengeRuns_Arena_" + t.Flow.name, keepSideRoomInjections: allowSideRooms);
            var entrance = Add(flow, Clone(flow, t.Entrance), null);
            flow.FirstNode = entrance;

            var arena = Add(flow, NewNode(flow, PrototypeDungeonRoom.RoomCategory.NORMAL, ArenaRoom.Get()), entrance);

            // The arena has 8 doors: 1 entrance, 1 shop, 1 boss chain, up to 5 chest rooms; NPCs get what's left.
            int chests = System.Math.Min(chestRooms, ArenaRoom.ExitCount - 3);
            for (int i = 0; i < chests; i++)
                Add(flow, t.MakeChest(flow), arena);
            Add(flow, t.MakeShop(flow), arena);

            if (!allowSideRooms)
                AddNpcRooms(flow, t, arena, ArenaRoom.ExitCount - 3 - chests);

            AttachBossChain(flow, t, arena);
            t.CommitBlueprintChanges();

            // Each wave is one of this floor's normal combat rooms' worth of enemies.
            ArenaController.WaveRooms = t.CombatRoomTable;
            return flow;
        }

        // The NPCs the game assigned to this floor for the run (Winchester, Gunsling King, ...) would
        // otherwise be injected as side rooms; they go behind the arena's spare doors instead.
        private static void AddNpcRooms(DungeonFlow flow, FloorTemplate t, DungeonFlowNode arena, int spareDoors)
        {
            // Optional extra: if it fails, the floor is still built, just without these NPCs.
            try
            {
                var rooms = t.TakeBlueprintNpcRooms(minDoors: 1);
                foreach (var room in rooms.Take(spareDoors))
                    Add(flow, FloorTemplate.NodeFor(flow, room), arena);
                if (rooms.Count > spareDoors)
                    Debug.LogWarning($"[Challenge Runs] Only {spareDoors} spare arena door(s) for {rooms.Count} NPC rooms; lower [Arena] ChestRooms to fit them all.");
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[Challenge Runs] Couldn't add this floor's NPCs to the arena: {e}");
            }
        }
    }
}
