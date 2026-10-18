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
        // Every failed generation try asks for a new layout, and the game never gives up on its own.
        // After a few failures a smaller floor is built; after more, the floor's normal layout is used
        // rather than leaving the loading screen up forever.
        private const int SmallerAfterFailures = 3;
        private const int NormalFloorAfterFailures = 8;

        private static int tries;
        private static Dungeon triesFor;

        public static void ResetTries()
        {
            tries = 0;
            triesFor = null;
        }

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

            var floor = GameManager.Instance.CurrentlyGeneratingDungeonPrefab;
            if (floor != triesFor)
            {
                triesFor = floor;
                tries = 0;
            }
            int failures = tries++;

            try
            {
                var template = FloorTemplate.Analyze(__result);
                if (template == null) return;

                if (failures >= NormalFloorAfterFailures)
                {
                    if (failures == NormalFloorAfterFailures)
                        ETGModConsole.Log($"[Challenge Runs] This floor failed to generate {failures} times as a " +
                            $"{(marathon ? "Marathon" : "Arena")} floor; using the normal layout for it.");
                    template.RestoreBlueprint();
                    return;
                }

                bool smaller = failures >= SmallerAfterFailures;
                if (smaller && failures == SmallerAfterFailures)
                    Debug.LogWarning($"[Challenge Runs] Floor failed to generate {failures} times; trying a smaller one.");

                int marathonRooms = ChallengeManager.MarathonRooms.Value;
                __result = marathon
                    ? MarathonFlow.Build(template, smaller ? Mathf.Max(10, marathonRooms / 2) : marathonRooms,
                        ChallengeManager.MarathonMinShops.Value, ChallengeManager.MarathonMaxShops.Value,
                        ChallengeManager.MarathonAllowSideRooms.Value)
                    : ArenaFlow.Build(template, ChallengeManager.ArenaChestRooms.Value,
                        ChallengeManager.ArenaAllowSideRooms.Value, includeNpcs: !smaller);
            }
            catch (Exception e)
            {
                Debug.LogError($"[Challenge Runs] Failed to build custom floor, using the normal one: {e}");
            }
        }
    }
}
