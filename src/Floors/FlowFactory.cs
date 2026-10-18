using System.Collections.Generic;
using Dungeonator;
using UnityEngine;

namespace ChallengeRuns.Floors
{
    // Helpers for building DungeonFlows in code. Field defaults mirror the vanilla flow assets.
    internal static class FlowFactory
    {
        // The floor's injection data is what brings in NPCs, secret rooms, the rat, etc.
        // Most of it hangs extra rooms off the layout as side branches; with
        // keepSideRoomInjections off, only injections that add no door (teleporter-linked) are kept.
        public static DungeonFlow CreateFlow(FloorTemplate template, string name, bool keepSideRoomInjections = true)
        {
            var src = template.Flow;
            var flow = ScriptableObject.CreateInstance<DungeonFlow>();
            flow.name = name;
            flow.fallbackRoomTable = template.CombatRoomTable;
            flow.phantomRoomTable = src.phantomRoomTable;
            flow.evolvedRoomTable = src.evolvedRoomTable;
            flow.subtypeRestrictions = new List<DungeonFlowSubtypeRestriction>(0);
            flow.flowInjectionData = FilterModifiers(src.flowInjectionData, keepSideRoomInjections);
            flow.sharedInjectionData = new List<SharedInjectionData>(0);
            if (src.sharedInjectionData != null)
                foreach (var shared in src.sharedInjectionData)
                    if (shared != null)
                        flow.sharedInjectionData.Add(keepSideRoomInjections ? shared : DoorlessCopy(shared));
            flow.Initialize();
            return flow;
        }

        private static List<ProceduralFlowModifierData> FilterModifiers(List<ProceduralFlowModifierData> src, bool keepAll)
        {
            var result = new List<ProceduralFlowModifierData>();
            if (src == null) return result;
            foreach (var mod in src)
                if (mod != null && (keepAll || AddsNoDoor(mod)))
                    result.Add(mod);
            return result;
        }

        private static bool AddsNoDoor(ProceduralFlowModifierData mod)
        {
            if (mod.IsWarpWing) return true;
            if (mod.placementRules == null || mod.placementRules.Count == 0) return false;
            foreach (var rule in mod.placementRules)
                if (rule != ProceduralFlowModifierData.FlowModifierPlacementType.NO_LINKS) return false;
            return true;
        }

        // Shared injection data are shared assets, so filter a copy rather than the original.
        private static SharedInjectionData DoorlessCopy(SharedInjectionData src)
        {
            var copy = ScriptableObject.CreateInstance<SharedInjectionData>();
            copy.name = src.name + " (trail only)";
            copy.UseInvalidWeightAsNoInjection = src.UseInvalidWeightAsNoInjection;
            copy.PreventInjectionOfFailedPrerequisites = src.PreventInjectionOfFailedPrerequisites;
            copy.IsNPCCell = src.IsNPCCell;
            copy.IgnoreUnmetPrerequisiteEntries = src.IgnoreUnmetPrerequisiteEntries;
            copy.OnlyOne = src.OnlyOne;
            copy.ChanceToSpawnOne = src.ChanceToSpawnOne;
            copy.InjectionData = FilterModifiers(src.InjectionData, false);
            copy.AttachedInjectionData = new List<SharedInjectionData>();
            if (src.AttachedInjectionData != null)
                foreach (var attached in src.AttachedInjectionData)
                    if (attached != null) copy.AttachedInjectionData.Add(DoorlessCopy(attached));
            return copy;
        }

        public static DungeonFlowNode NewNode(DungeonFlow flow, PrototypeDungeonRoom.RoomCategory category,
            PrototypeDungeonRoom room = null, GenericRoomTable table = null)
        {
            return new DungeonFlowNode(flow)
            {
                isSubchainStandin = false,
                nodeType = DungeonFlowNode.ControlNodeType.ROOM,
                roomCategory = category,
                percentChance = 1f,
                priority = DungeonFlowNode.NodePriority.MANDATORY,
                overrideExactRoom = room,
                overrideRoomTable = table,
                capSubchain = false,
                subchainIdentifier = string.Empty,
                limitedCopiesOfSubchain = false,
                maxCopiesOfSubchain = 1,
                subchainIdentifiers = new List<string>(0),
                receivesCaps = false,
                isWarpWingEntrance = false,
                handlesOwnWarping = false,
                forcedDoorType = DungeonFlowNode.ForcedDoorType.NONE,
                loopForcedDoorType = DungeonFlowNode.ForcedDoorType.NONE,
                nodeExpands = false,
                initialChainPrototype = "n",
                chainRules = new List<ChainRule>(0),
                minChainLength = 3,
                maxChainLength = 8,
                minChildrenToBuild = 1,
                maxChildrenToBuild = 1,
                canBuildDuplicateChildren = false,
                guidAsString = System.Guid.NewGuid().ToString(),
                parentNodeGuid = string.Empty,
                childNodeGuids = new List<string>(0),
                loopTargetNodeGuid = string.Empty,
                loopTargetIsOneWay = false,
                flow = flow,
            };
        }

        public static DungeonFlowNode Clone(DungeonFlow flow, DungeonFlowNode src)
        {
            var node = NewNode(flow, src.roomCategory, src.overrideExactRoom, src.overrideRoomTable);
            node.percentChance = src.percentChance;
            node.priority = src.priority;
            node.receivesCaps = src.receivesCaps;
            node.isWarpWingEntrance = src.isWarpWingEntrance;
            node.handlesOwnWarping = src.handlesOwnWarping;
            node.forcedDoorType = src.forcedDoorType;
            node.loopForcedDoorType = src.loopForcedDoorType;
            return node;
        }

        public static DungeonFlowNode Add(DungeonFlow flow, DungeonFlowNode node, DungeonFlowNode parent)
        {
            flow.AddNodeToFlow(node, parent);
            return node;
        }

        // Copies every descendant of srcNode (in the template flow) under dstParent.
        public static void CloneSubtree(DungeonFlow src, DungeonFlowNode srcNode, DungeonFlow dst, DungeonFlowNode dstParent)
        {
            foreach (var guid in srcNode.childNodeGuids)
            {
                var child = src.GetNodeFromGuid(guid);
                if (child == null) continue;
                var copy = Add(dst, Clone(dst, child), dstParent);
                CloneSubtree(src, child, dst, copy);
            }
        }

        // Entrance -> ... -> parent -> [foyer -> boss] x N -> original post-boss rooms (exit, etc).
        public static void AttachBossChain(DungeonFlow flow, FloorTemplate t, DungeonFlowNode parent)
        {
            var bossRooms = t.OneRoomPerBoss();
            DungeonFlowNode cursor = parent;
            DungeonFlowNode lastBoss;

            if (bossRooms.Count <= 1)
            {
                // Single-boss floors (Forge, Bullet Hell) keep their bespoke boss setup untouched.
                if (t.BossFoyer != null) cursor = Add(flow, Clone(flow, t.BossFoyer), cursor);
                lastBoss = Add(flow, Clone(flow, t.Boss), cursor);
            }
            else
            {
                lastBoss = null;
                foreach (var room in bossRooms)
                {
                    cursor = Add(flow, t.MakeBossFoyer(flow), cursor);
                    cursor = lastBoss = Add(flow, NewNode(flow, PrototypeDungeonRoom.RoomCategory.BOSS, room), cursor);
                }
            }

            CloneSubtree(t.Flow, t.Boss, flow, lastBoss);
        }

        public static void Shuffle<T>(IList<T> list)
        {
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = Random.Range(0, i + 1);
                var tmp = list[i];
                list[i] = list[j];
                list[j] = tmp;
            }
        }
    }
}
