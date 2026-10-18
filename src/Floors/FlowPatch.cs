using System;
using Dungeonator;
using HarmonyLib;
using UnityEngine;

namespace ChallengeRuns.Floors
{
    // The generator asks the floor's settings for a random layout; we swap in our own,
    // built from that original so rooms, enemies and bosses stay floor-appropriate.
    [HarmonyPatch(typeof(SemioticDungeonGenSettings), "GetRandomFlow")]
    internal static class FlowPatch
    {
        private static void Postfix(ref DungeonFlow __result)
        {
            if (__result == null || !GameManager.HasInstance) return;
            // Skips the Breach, tutorial, Resourceful Rat's lair, character Pasts, etc.
            if (GameManager.Instance.CurrentLevelOverrideState != GameManager.LevelOverrideState.NONE) return;

            ChallengeManager.EnsureRunLocked();
            bool marathon = ChallengeManager.IsActive("marathon");
            bool arena = ChallengeManager.IsActive("arena");
            if (!marathon && !arena) return;

            try
            {
                var template = FloorTemplate.Analyze(__result);
                if (template == null) return;

                __result = marathon
                    ? MarathonFlow.Build(template, ChallengeManager.MarathonRooms.Value,
                        ChallengeManager.MarathonMinShops.Value, ChallengeManager.MarathonMaxShops.Value,
                        ChallengeManager.MarathonAllowSideRooms.Value)
                    : ArenaFlow.Build(template, ChallengeManager.ArenaChestRooms.Value);
            }
            catch (Exception e)
            {
                Debug.LogError($"[Challenge Runs] Failed to build custom floor, using the normal one: {e}");
            }
        }
    }
}
