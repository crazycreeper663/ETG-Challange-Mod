using System.Collections.Generic;
using System.Linq;
using Dungeonator;
using UnityEngine;
using static ChallengeRuns.Floors.FlowFactory;

namespace ChallengeRuns.Floors
{
    //   Entrance - ARENA - Boss - Boss - ... - Exit
    //                |
    //              chest - chest - chest - NPC - shop (- one-door NPC)
    //
    // Rooms crowded around the arena on all sides made most layouts fail to generate, so the rewards
    // form one trail off a single arena door, like a short Marathon. Only one-door NPC rooms that
    // can't sit on that trail get an arena door of their own, and at most a couple.
    //
    // ArenaController seals the arena when you walk in and only unseals it after the last wave.
    internal static class ArenaFlow
    {
        private const int MaxChestRooms = 5;
        private const int MaxNpcRoomsOnArena = 2;

        public static DungeonFlow Build(FloorTemplate t, int chestRooms, bool allowSideRooms, bool includeNpcs)
        {
            // The game's injected side rooms (secret rooms and the like) are left out by default, as on
            // Marathon floors.
            var flow = CreateFlow(t, "ChallengeRuns_Arena_" + t.Flow.name, keepSideRoomInjections: allowSideRooms);
            var entrance = Add(flow, Clone(flow, t.Entrance), null);
            flow.FirstNode = entrance;

            var arena = Add(flow, NewNode(flow, PrototypeDungeonRoom.RoomCategory.NORMAL, ArenaRoom.Get()), entrance);

            // Trail rooms need a second door to lead on; one room without one can end the trail.
            var trail = new List<DungeonFlowNode>();
            for (int i = 0; i < Mathf.Clamp(chestRooms, 0, MaxChestRooms); i++)
                trail.Add(t.MakeChest(flow));

            var oneDoorNpcs = new List<DungeonFlowNode>();
            if (!allowSideRooms && includeNpcs)
            {
                foreach (var room in NpcRooms(t))
                {
                    var node = FloorTemplate.NodeFor(flow, room);
                    if (FloorTemplate.DoorCount(room) >= 2) trail.Add(node);
                    else oneDoorNpcs.Add(node);
                }
            }
            Shuffle(trail);

            DungeonFlowNode trailEnd = null;
            var shop = t.MakePassThroughShop(flow);
            if (shop != null) trail.Add(shop);
            else trailEnd = t.MakeShop(flow);
            if (trailEnd == null && oneDoorNpcs.Count > 0)
            {
                trailEnd = oneDoorNpcs[0];
                oneDoorNpcs.RemoveAt(0);
            }

            DungeonFlowNode cursor = arena;
            foreach (var stop in trail)
                cursor = Add(flow, stop, cursor);
            if (trailEnd != null)
                Add(flow, trailEnd, cursor);

            foreach (var npc in oneDoorNpcs.Take(MaxNpcRoomsOnArena))
                Add(flow, npc, arena);
            if (oneDoorNpcs.Count > MaxNpcRoomsOnArena)
                Debug.LogWarning($"[Challenge Runs] Left out {oneDoorNpcs.Count - MaxNpcRoomsOnArena} NPC room(s) on this arena floor.");

            AttachBossChain(flow, t, arena);
            t.CommitBlueprintChanges();

            // Each wave is one of this floor's normal combat rooms' worth of enemies.
            ArenaController.WaveRooms = t.CombatRoomTable;
            return flow;
        }

        // The NPCs the game assigned to this floor for the run (Winchester, Gunsling King, ...); they'd
        // otherwise be injected as side rooms wherever they fit.
        private static List<PrototypeDungeonRoom> NpcRooms(FloorTemplate t)
        {
            // Optional extra: if it fails, the floor is still built, just without these NPCs.
            try
            {
                return t.TakeBlueprintNpcRooms(minDoors: 1);
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[Challenge Runs] Couldn't add this floor's NPCs to the arena floor: {e}");
                return new List<PrototypeDungeonRoom>();
            }
        }
    }
}
