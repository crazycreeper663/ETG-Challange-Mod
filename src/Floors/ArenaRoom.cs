using System.Collections.Generic;
using Dungeonator;
using UnityEngine;

namespace ChallengeRuns.Floors
{
    // One big open room with a few pillars for cover and two doors per side.
    // Built in code so it picks up whatever tileset the current floor uses.
    internal static class ArenaRoom
    {
        public const string Name = "ChallengeRuns_Arena";
        public const int Width = 44;
        public const int Height = 34;
        public const int ExitCount = 8;

        private static PrototypeDungeonRoom cached;

        public static PrototypeDungeonRoom Get() => cached ?? (cached = Build());

        public static bool Is(RoomHandler room) =>
            room?.area?.prototypeRoom != null && room.area.prototypeRoom.name == Name;

        private static PrototypeDungeonRoom Build()
        {
            var room = ScriptableObject.CreateInstance<PrototypeDungeonRoom>();
            room.name = Name;
            room.GUID = System.Guid.NewGuid().ToString();
            room.RoomId = Random.Range(10000, 1000000);
            room.category = PrototypeDungeonRoom.RoomCategory.NORMAL;
            room.subCategoryNormal = PrototypeDungeonRoom.RoomNormalSubCategory.COMBAT;
            room.subCategoryBoss = PrototypeDungeonRoom.RoomBossSubCategory.FLOOR_BOSS;
            room.subCategorySpecial = PrototypeDungeonRoom.RoomSpecialSubCategory.UNSPECIFIED_SPECIAL;
            room.subCategorySecret = PrototypeDungeonRoom.RoomSecretSubCategory.UNSPECIFIED_SECRET;
            room.injectionFlags = new RuntimeInjectionFlags { CastleFireplace = false, ShopAnnexed = false };
            room.pits = new List<PrototypeRoomPitEntry>();
            room.placedObjects = new List<PrototypePlacedObjectData>();
            room.placedObjectPositions = new List<Vector2>();
            room.additionalObjectLayers = new List<PrototypeRoomObjectLayer>();
            room.eventTriggerAreas = new List<PrototypeEventTriggerArea>();
            room.roomEvents = new List<RoomEventDefinition>(); // sealing is handled by ArenaController
            room.paths = new List<SerializedPath>();
            room.prerequisites = new List<DungeonPrerequisite>();
            room.excludedOtherRooms = new List<PrototypeDungeonRoom>();
            room.rectangularFeatures = new List<PrototypeRectangularFeature>();
            room.exitData = new PrototypeRoomExitData { exits = new List<PrototypeRoomExit>() };
            room.usesProceduralDecoration = true;
            room.usesProceduralLighting = true;
            room.allowWallDecoration = true;
            room.allowFloorDecoration = true;
            room.customAmbientLight = Color.white;
            room.CustomMusicEvent = string.Empty;
            room.CustomMusicSwitch = string.Empty;
            room.rewardChestSpawnPosition = new IntVector2(-1, -1);
            room.overrideRoomVisualType = -1;
            room.Width = Width;
            room.Height = Height;

            room.FullCellData = new PrototypeDungeonRoomCellData[Width * Height];
            for (int x = 0; x < Width; x++)
                for (int y = 0; y < Height; y++)
                    room.FullCellData[y * Width + x] = Cell(IsPillar(x, y) ? CellType.WALL : CellType.FLOOR);

            foreach (int x in new[] { Width / 3, Width * 2 / 3 })
            {
                AddExit(room, new Vector2(x, Height), DungeonData.Direction.NORTH);
                AddExit(room, new Vector2(x, 0), DungeonData.Direction.SOUTH);
            }
            foreach (int y in new[] { Height / 3, Height * 2 / 3 })
            {
                AddExit(room, new Vector2(Width, y), DungeonData.Direction.EAST);
                AddExit(room, new Vector2(0, y), DungeonData.Direction.WEST);
            }

            room.OnBeforeSerialize();
            room.UpdatePrecalculatedData();
            return room;
        }

        // Four 3x3 pillars, one in each quadrant.
        private static bool IsPillar(int x, int y)
        {
            foreach (int cx in new[] { Width / 4, Width * 3 / 4 })
                foreach (int cy in new[] { Height / 4, Height * 3 / 4 })
                    if (Mathf.Abs(x - cx) <= 1 && Mathf.Abs(y - cy) <= 1)
                        return true;
            return false;
        }

        private static void AddExit(PrototypeDungeonRoom room, Vector2 location, DungeonData.Direction direction)
        {
            var exit = new PrototypeRoomExit(direction, location)
            {
                exitType = PrototypeRoomExit.ExitType.NO_RESTRICTION,
                containsDoor = true,
            };
            var margin = direction == DungeonData.Direction.EAST || direction == DungeonData.Direction.WEST
                ? new Vector2(0, 1)
                : new Vector2(1, 0);
            exit.containedCells.Add(location + margin);
            room.exitData.exits.Add(exit);
        }

        private static PrototypeDungeonRoomCellData Cell(CellType type)
        {
            return new PrototypeDungeonRoomCellData(string.Empty, type)
            {
                state = type,
                diagonalWallType = DiagonalWallType.NONE,
                breakable = false,
                str = string.Empty,
                conditionalOnParentExit = false,
                conditionalCellIsPit = false,
                parentExitIndex = -1,
                containsManuallyPlacedLight = false,
                lightPixelsOffsetY = 0,
                lightStampIndex = 0,
                doesDamage = false,
                damageDefinition = new CellDamageDefinition { damageTypes = CoreDamageTypes.None, respectsFlying = true },
                appearance = new PrototypeDungeonRoomCellAppearance
                {
                    overrideDungeonMaterialIndex = -1,
                    IsPhantomCarpet = false,
                    ForceDisallowGoop = false,
                    OverrideFloorType = CellVisualData.CellFloorType.Stone,
                    globalOverrideIndices = new PrototypeIndexOverrideData { indices = new List<int>(0) },
                },
                ForceTileNonDecorated = false,
                additionalPlacedObjectIndices = new List<int> { -1 },
                placedObjectRUBELIndex = -1,
            };
        }
    }
}
