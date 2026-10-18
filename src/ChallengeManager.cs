using System;
using System.Collections.Generic;
using System.Linq;
using BepInEx.Configuration;
using UnityEngine;

namespace ChallengeRuns
{
    public class Challenge
    {
        public string Id;
        public string Name;
        public string Description;
        public string[] IncompatibleWith = new string[0];
        public ConfigEntry<bool> Enabled;

        // Called once per player at the start of a run.
        public Action<PlayerController> OnRunStart;
    }

    public static class ChallengeManager
    {
        public const float EnemyHealthMultiplier = 2f;
        public const float GlassCannonDamageMultiplier = 2f;

        public static readonly List<Challenge> All = new List<Challenge>
        {
            new Challenge
            {
                Id = "glass",
                Name = "Glass Cannon",
                Description = "One heart, no armor, but you deal double damage.",
                OnRunStart = ApplyGlassCannon,
            },
            new Challenge
            {
                Id = "noheal",
                Name = "No Healing",
                Description = "Hearts and healing effects do nothing.",
            },
            new Challenge
            {
                Id = "starter",
                Name = "Starter Gun Only",
                Description = "You cannot pick up any new guns.",
                IncompatibleWith = new[] { "roulette" },
            },
            new Challenge
            {
                Id = "roulette",
                Name = "Gun Roulette",
                Description = "Your gun is replaced with a random one every time you clear a room. No switching.",
                IncompatibleWith = new[] { "starter" },
                OnRunStart = p => p.inventory.GunLocked.SetOverride(Plugin.GUID, true),
            },
            new Challenge
            {
                Id = "sponge",
                Name = "Bullet Sponges",
                Description = "All enemies and bosses have double health.",
            },
            new Challenge
            {
                Id = "blankless",
                Name = "Blankless",
                Description = "You can never hold blanks.",
            },
            new Challenge
            {
                Id = "marathon",
                Name = "Marathon",
                Description = "Each floor is one long trail of 100 rooms with chests, shops and NPCs along the way, ending in every boss the floor can have.",
                IncompatibleWith = new[] { "arena" },
            },
            new Challenge
            {
                Id = "arena",
                Name = "Arena",
                Description = "Each floor is one huge room with 50 enemy waves. Survive to reach chest rooms, a shop and every boss the floor can have.",
                IncompatibleWith = new[] { "marathon" },
            },
        };

        public static ConfigEntry<int> MarathonRooms;
        public static ConfigEntry<int> MarathonMinShops;
        public static ConfigEntry<int> MarathonMaxShops;
        public static ConfigEntry<bool> MarathonAllowSideRooms;
        public static ConfigEntry<int> ArenaWaves;
        public static ConfigEntry<int> ArenaChestRooms;
        public static ConfigEntry<bool> ArenaAllowSideRooms;

        // The run's challenge set is locked in when the run starts, so toggling
        // the menu mid-run (e.g. via console) can't change an in-progress run.
        private static readonly HashSet<string> activeThisRun = new HashSet<string>();
        private static readonly HashSet<PlayerController> initializedPlayers = new HashSet<PlayerController>();
        private static bool runLocked;

        // True while the death/victory screen is up: the run is over, so the next one
        // (quick restart or back to the Breach) can be set up from there.
        public static bool RunEnded { get; private set; }

        // Set while the mod itself hands out a gun, so Starter Only doesn't block it.
        public static bool GrantingGun;

        public static void BindConfig(ConfigFile config)
        {
            foreach (var c in All)
                c.Enabled = config.Bind("Challenges", c.Id, false, $"{c.Name}: {c.Description}");

            MarathonRooms = config.Bind("Marathon", "CombatRoomsPerFloor", 100,
                "Combat rooms per floor. Lower this if floors fail to generate.");
            MarathonMinShops = config.Bind("Marathon", "MinShops", 2, "Fewest shops per floor.");
            MarathonMaxShops = config.Bind("Marathon", "MaxShops", 5, "Most shops per floor.");
            MarathonAllowSideRooms = config.Bind("Marathon", "AllowInjectedSideRooms", false,
                "Let the game add its usual extra rooms (some NPCs, secret floor entrances, etc.) as small side branches. " +
                "Off keeps the floor a single trail with no forks.");
            ArenaWaves = config.Bind("Arena", "Waves", 50, "Enemy waves per floor.");
            ArenaChestRooms = config.Bind("Arena", "ChestRooms", 3, "Chest rooms unlocked after the waves (max 5).");
            ArenaAllowSideRooms = config.Bind("Arena", "AllowInjectedSideRooms", false,
                "Let the game add its usual extra rooms (secret rooms, shrines, some NPCs, etc.) around the arena floor. " +
                "Off keeps the floor to the arena, chests, shop and bosses, with the floor's NPCs on the reward trail, " +
                "and makes arena floors load far more reliably.");
        }

        // Floor 1 generates before the run's first level-loaded event, so lock the
        // challenge set at whichever happens first.
        public static void EnsureRunLocked()
        {
            if (runLocked) return;
            activeThisRun.Clear();
            foreach (var c in All.Where(c => c.Enabled.Value))
                activeThisRun.Add(c.Id);
            runLocked = true;
        }

        // A run floor is being built (also the first floor of a quick restart): the run is no longer
        // over, and whatever was still running on the previous floor has to stop.
        public static void OnFloorGenerating()
        {
            RunEnded = false;
            EnsureRunLocked();
            Floors.ArenaController.Stop();
        }

        public static Challenge Get(string id) => All.FirstOrDefault(c => c.Id == id);

        public static bool IsActive(string id) => activeThisRun.Contains(id);

        public static bool InBreach => GameManager.HasInstance && GameManager.Instance.IsFoyer;

        public static bool CanEdit => !GameManager.HasInstance || GameManager.Instance.IsFoyer || RunEnded;

        public static void OnRunEnded()
        {
            if (RunEnded) return;
            RunEnded = true;
            Floors.ArenaController.Stop();
            // Unlock so whatever is picked now applies when the next run generates.
            runLocked = false;
        }

        public static string SetEnabled(string id, bool enabled)
        {
            var c = Get(id);
            if (c == null) return $"Unknown challenge '{id}'.";
            if (!CanEdit) return "Challenges can only be changed in the Breach or on the game over screen.";

            c.Enabled.Value = enabled;
            if (enabled)
            {
                foreach (var otherId in c.IncompatibleWith)
                {
                    var other = Get(otherId);
                    if (other != null && other.Enabled.Value)
                    {
                        other.Enabled.Value = false;
                        return $"{c.Name} enabled ({other.Name} disabled - incompatible).";
                    }
                }
            }
            return $"{c.Name} {(enabled ? "enabled" : "disabled")}.";
        }

        public static void OnLevelLoaded()
        {
            RunEnded = false;
            // Every level load, the Breach included: waves from the floor you left must stop, and the
            // next floor starts with a clean count of generation tries.
            Floors.ArenaController.Stop();
            Floors.FlowPatch.ResetTries();
            if (InBreach)
            {
                activeThisRun.Clear();
                initializedPlayers.Clear();
                runLocked = false;
                return;
            }

            // A fresh PlayerController is spawned for each run (including quick restarts).
            var primary = GameManager.Instance.PrimaryPlayer;
            if (primary != null && !initializedPlayers.Contains(primary))
                StartRun();

            Floors.ArenaController.OnLevelLoaded();
        }

        private static void StartRun()
        {
            EnsureRunLocked();
            initializedPlayers.Clear();

            foreach (var player in GameManager.Instance.AllPlayers)
            {
                if (player == null) continue;
                initializedPlayers.Add(player);
                player.OnRoomClearEvent += OnRoomCleared;
                foreach (var c in All.Where(c => IsActive(c.Id) && c.OnRunStart != null))
                    c.OnRunStart(player);
            }

            if (activeThisRun.Count > 0)
            {
                var names = string.Join(", ", All.Where(c => IsActive(c.Id)).Select(c => c.Name).ToArray());
                ETGModConsole.Log($"[Challenge Runs] Active: {names}");
            }
        }

        public static void Tick()
        {
            if (!IsActive("blankless") || !GameManager.HasInstance) return;
            foreach (var player in GameManager.Instance.AllPlayers)
                if (player != null && player.Blanks > 0)
                    player.Blanks = 0;
        }

        private static void ApplyGlassCannon(PlayerController player)
        {
            var hh = player.healthHaver;
            if (player.characterIdentity == PlayableCharacters.Robot)
            {
                hh.Armor = 1f;
            }
            else
            {
                hh.SetHealthMaximum(1f);
                hh.ForceSetCurrentHealth(1f);
                hh.Armor = 0f;
            }

            player.ownerlessStatModifiers.Add(new StatModifier
            {
                statToBoost = PlayerStats.StatType.Damage,
                modifyType = StatModifier.ModifyMethod.MULTIPLICATIVE,
                amount = GlassCannonDamageMultiplier,
            });
            player.stats.RecalculateStats(player, false, false);
        }

        private static void OnRoomCleared(PlayerController player)
        {
            if (!IsActive("roulette") || player == null || player.IsGhost) return;

            // Same gun pool chests draw from; lower qualities come up more often.
            var quality = RouletteQualities[UnityEngine.Random.Range(0, RouletteQualities.Length)];
            var prefab = LootEngine.GetItemOfTypeAndQuality<Gun>(quality, GameManager.Instance.RewardManager.GunsLootTable, false);
            if (prefab == null) return;

            var oldGuns = player.inventory.AllGuns.ToList();

            player.inventory.GunLocked.RemoveOverride(Plugin.GUID);
            GrantingGun = true;
            try
            {
                LootEngine.TryGivePrefabToPlayer(prefab.gameObject, player, false);
            }
            finally
            {
                GrantingGun = false;
            }

            // Once the old guns are gone the new one is the only gun left, so it's the one in hand.
            if (player.inventory.AllGuns.Any(g => !oldGuns.Contains(g)))
                foreach (var g in oldGuns)
                    player.inventory.RemoveGunFromInventory(g);
            player.inventory.GunLocked.SetOverride(Plugin.GUID, true);
        }

        private static readonly PickupObject.ItemQuality[] RouletteQualities =
        {
            PickupObject.ItemQuality.D, PickupObject.ItemQuality.D, PickupObject.ItemQuality.D,
            PickupObject.ItemQuality.C, PickupObject.ItemQuality.C, PickupObject.ItemQuality.C,
            PickupObject.ItemQuality.B, PickupObject.ItemQuality.B,
            PickupObject.ItemQuality.A,
            PickupObject.ItemQuality.S,
        };
    }
}
