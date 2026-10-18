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
        // Run after other mods' patches here (e.g. layout mods) so a Marathon/Arena floor isn't
        // swapped out again afterwards.
        [HarmonyPriority(Priority.Last)]
        private static void Postfix(ref DungeonFlow __result)
        {
            if (__result == null || !GameManager.HasInstance) return;
            // Skips the Breach, tutorial, Resourceful Rat's lair, character Pasts, etc.
            if (GameManager.Instance.CurrentLevelOverrideState != GameManager.LevelOverrideState.NONE) return;

            ChallengeManager.OnFloorGenerating();
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
                    : ArenaFlow.Build(template, ChallengeManager.ArenaChestRooms.Value,
                        ChallengeManager.ArenaAllowSideRooms.Value);
            }
            catch (Exception e)
            {
                Debug.LogError($"[Challenge Runs] Failed to build custom floor, using the normal one: {e}");
            }
        }
    }
}
