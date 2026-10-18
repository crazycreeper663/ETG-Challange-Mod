using Dungeonator;
using static ChallengeRuns.Floors.FlowFactory;

namespace ChallengeRuns.Floors
{
    //   Entrance - ARENA -+- chest rooms
    //                     +- shop
    //                     +- Boss - Boss - ... - Exit
    //
    // ArenaController seals the arena when you walk in and only unseals it after the last wave.
    internal static class ArenaFlow
    {
        public static DungeonFlow Build(FloorTemplate t, int chestRooms)
        {
            var flow = CreateFlow(t, "ChallengeRuns_Arena_" + t.Flow.name);
            var entrance = Add(flow, Clone(flow, t.Entrance), null);
            flow.FirstNode = entrance;

            var arena = Add(flow, NewNode(flow, PrototypeDungeonRoom.RoomCategory.NORMAL, ArenaRoom.Get()), entrance);

            // The arena has 8 doors: 1 entrance, 1 shop, 1 boss chain, up to 5 chest rooms.
            for (int i = 0; i < System.Math.Min(chestRooms, ArenaRoom.ExitCount - 3); i++)
                Add(flow, t.MakeChest(flow), arena);
            Add(flow, t.MakeShop(flow), arena);

            AttachBossChain(flow, t, arena);

            // Each wave is one of this floor's normal combat rooms' worth of enemies.
            ArenaController.WaveRooms = t.CombatRoomTable;
            return flow;
        }
    }
}
