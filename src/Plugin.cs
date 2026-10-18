using BepInEx;
using HarmonyLib;
using UnityEngine;

namespace ChallengeRuns
{
    [BepInPlugin(GUID, NAME, VERSION)]
    [BepInDependency(ETGModMainBehaviour.GUID)]
    public class Plugin : BaseUnityPlugin
    {
        public const string GUID = "oakesnoah.etg.challengeruns";
        public const string NAME = "Challenge Runs";
        public const string VERSION = "0.3.5"; // keep in sync with <Version> in the .csproj

        public static Plugin Instance { get; private set; }

        public void Awake()
        {
            Instance = this;
            ChallengeManager.BindConfig(Config);
            ApplyPatches();
        }

        // Patch class by class so one failure (e.g. another mod or a game update changing a
        // method) only disables that feature instead of every patch after it.
        private void ApplyPatches()
        {
            var harmony = new Harmony(GUID);
            foreach (var type in AccessTools.GetTypesFromAssembly(typeof(Plugin).Assembly))
            {
                if (type.GetCustomAttributes(typeof(HarmonyPatch), false).Length == 0) continue;
                try
                {
                    harmony.CreateClassProcessor(type).Patch();
                }
                catch (System.Exception e)
                {
                    Logger.LogError($"Patch {type.Name} failed, that feature is disabled: {e}");
                }
            }
        }

        public void Start()
        {
            ETGModMainBehaviour.WaitForGameManagerStart(OnGameManagerStart);
        }

        private void OnGameManagerStart(GameManager gm)
        {
            HookLevelLoads(gm);
            gameObject.AddComponent<ChallengeMenu>();
            ConsoleCommands.Register();
            ETGModConsole.Log($"{NAME} v{VERSION} loaded. Press F6 in the Breach or on the game over screen, or type 'challenge help'.");
        }

        // The game makes a new GameManager every time you go back to the Breach, so this also runs from
        // GameManagerAwakePatch. Removing first means a GameManager never gets it twice.
        public static void HookLevelLoads(GameManager gm)
        {
            gm.OnNewLevelFullyLoaded -= ChallengeManager.OnLevelLoaded;
            gm.OnNewLevelFullyLoaded += ChallengeManager.OnLevelLoaded;
        }

        public void Update()
        {
            ChallengeManager.Tick();
        }
    }
}
