using HarmonyLib;

namespace ChallengeRuns
{
    [HarmonyPatch(typeof(HealthHaver), nameof(HealthHaver.ApplyHealing))]
    internal static class NoHealingPatch
    {
        private static bool Prefix(HealthHaver __instance)
        {
            if (!ChallengeManager.IsActive("noheal")) return true;
            return __instance.GetComponent<PlayerController>() == null;
        }
    }

    [HarmonyPatch(typeof(Gun), nameof(Gun.Pickup))]
    internal static class StarterOnlyPatch
    {
        private static bool Prefix(Gun __instance, PlayerController player)
        {
            if (!ChallengeManager.IsActive("starter") || ChallengeManager.GrantingGun) return true;
            if (player != null && player.inventory != null && player.inventory.ContainsGun(__instance.PickupObjectId))
                return true; // let ammo top-ups through

            GameUIRoot.Instance?.notificationController?.DoCustomNotification(
                "Starter Gun Only", "You can't pick up new guns!", null, -1);
            return false;
        }
    }

    // The Ammonomicon's death/victory pages set themselves up here, which is when the run is over.
    [HarmonyPatch(typeof(AmmonomiconDeathPageController), nameof(AmmonomiconDeathPageController.DoInitialize))]
    internal static class RunEndedPatch
    {
        private static void Postfix(AmmonomiconDeathPageController __instance)
        {
            if (!GameManager.HasInstance || GameManager.Instance.IsFoyer) return;
            bool everyoneDead = true;
            foreach (var player in GameManager.Instance.AllPlayers)
                if (player != null && player.healthHaver != null && player.healthHaver.IsAlive)
                    everyoneDead = false;
            if (__instance.isVictoryPage || everyoneDead)
                ChallengeManager.OnRunEnded();
        }
    }

    // HealthHaver.Start runs after difficulty scaling is set up, so changes here stick.
    [HarmonyPatch(typeof(HealthHaver), "Start")]
    internal static class EnemySpawnPatch
    {
        private static void Postfix(HealthHaver __instance)
        {
            var enemy = __instance.aiActor;
            if (enemy == null || !enemy.IsNormalEnemy || enemy.CompanionOwner != null) return;

            if (ChallengeManager.IsActive("jammed") && !enemy.IsBlackPhantom)
                enemy.BecomeBlackPhantom();

            if (ChallengeManager.IsActive("sponge"))
                __instance.SetHealthMaximum(__instance.GetMaxHealth() * ChallengeManager.EnemyHealthMultiplier, null, true);
        }
    }
}
