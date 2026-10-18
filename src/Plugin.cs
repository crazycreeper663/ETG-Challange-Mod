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
        public const string VERSION = "0.2.0"; // keep in sync with <Version> in the .csproj

        public static Plugin Instance { get; private set; }

        public void Awake()
        {
            Instance = this;
            ChallengeManager.BindConfig(Config);
            new Harmony(GUID).PatchAll();
        }

        public void Start()
        {
            ETGModMainBehaviour.WaitForGameManagerStart(OnGameManagerStart);
        }

        private void OnGameManagerStart(GameManager gm)
        {
            gm.OnNewLevelFullyLoaded += ChallengeManager.OnLevelLoaded;
            gameObject.AddComponent<ChallengeMenu>();
            ConsoleCommands.Register();
            ETGModConsole.Log($"{NAME} v{VERSION} loaded. Press F6 in the Breach or type 'challenge help'.");
        }

        public void Update()
        {
            ChallengeManager.Tick();
        }
    }
}
