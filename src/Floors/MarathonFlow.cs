using System.Collections.Generic;
using Dungeonator;
using UnityEngine;
using static ChallengeRuns.Floors.FlowFactory;

namespace ChallengeRuns.Floors
{
    // One long trail: every room has exactly one way forward, no branches or dead ends.
    // Chests, shops and NPC rooms are stops on the trail itself, spread out evenly.
    //
    //   Entrance - combat - combat - chest - combat - ... - shop - combat - ... - Boss - Boss - Boss - Exit
    internal static class MarathonFlow
    {
        private const int CombatRoomsPerChest = 15;

        public static DungeonFlow Build(FloorTemplate t, int combatRooms, int minShops, int maxShops, bool allowSideRooms)
        {
            var flow = CreateFlow(t, "ChallengeRuns_Marathon_" + t.Flow.name, keepSideRoomInjections: allowSideRooms);
            var entrance = Add(flow, Clone(flow, t.Entrance), null);
            flow.FirstNode = entrance;
            combatRooms = Mathf.Max(1, combatRooms);

            // Stops have to be rooms with 2+ doors so the trail can pass through them.
            var stops = new List<DungeonFlowNode>();
            int deadEndShops = 0;

            int shops = Random.Range(minShops, maxShops + 1);
            for (int i = 0; i < shops; i++)
            {
                var shop = t.MakePassThroughShop(flow);
                if (shop != null) stops.Add(shop);
                else deadEndShops++;
            }

            int chests = Mathf.Max(2, combatRooms / CombatRoomsPerChest);
            for (int i = 0; i < chests; i++)
                stops.Add(t.MakeChest(flow));

            foreach (var special in t.SpecialRooms)
            {
                if (special.roomCategory != PrototypeDungeonRoom.RoomCategory.SPECIAL) continue; // secret rooms can't sit on the path
                var npc = t.MakePassThrough(flow, special);
                if (npc != null) stops.Add(npc);
            }

            if (!allowSideRooms)
                foreach (var room in t.TakeBlueprintNpcRooms())
                    stops.Add(FloorTemplate.NodeFor(flow, room));

            Shuffle(stops);

            // Stop k goes right after combat room (k+1) * N / (stops+1), so they're evenly spaced.
            var trail = new List<DungeonFlowNode>();
            DungeonFlowNode cursor = entrance;
            int placed = 0;
            for (int i = 1; i <= combatRooms; i++)
            {
                cursor = Add(flow, NewNode(flow, PrototypeDungeonRoom.RoomCategory.NORMAL), cursor);
                trail.Add(cursor);
                while (placed < stops.Count && i >= (placed + 1) * combatRooms / (stops.Count + 1))
                    cursor = Add(flow, stops[placed++], cursor);
            }
            while (placed < stops.Count)
                cursor = Add(flow, stops[placed++], cursor);

            // Last resort: this floor has no shop with a second door, so it can only be a dead end.
            if (deadEndShops > 0)
            {
                Debug.LogWarning($"[Challenge Runs] No pass-through shop room on {t.Flow.name}; placing {deadEndShops} shop(s) as dead ends.");
                for (int i = 0; i < deadEndShops; i++)
                    Add(flow, t.MakeShop(flow), trail[Random.Range(0, trail.Count)]);
            }

            AttachBossChain(flow, t, cursor);
            return flow;
        }
    }
}
