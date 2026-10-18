using System.Linq;

namespace ChallengeRuns
{
    internal static class ConsoleCommands
    {
        public static void Register()
        {
            ETGModConsole.Commands.AddGroup("challenge", args => Help());
            var group = ETGModConsole.Commands.GetGroup("challenge");

            group.AddUnit("help", args => Help());
            group.AddUnit("list", args => List());
            group.AddUnit("on", args => Toggle(args, true));
            group.AddUnit("off", args => Toggle(args, false));
            group.AddUnit("clear", args =>
            {
                if (!ChallengeManager.CanEdit) { ETGModConsole.Log("Challenges can only be changed in the Breach."); return; }
                foreach (var c in ChallengeManager.All) c.Enabled.Value = false;
                ETGModConsole.Log("All challenges disabled.");
            });
        }

        private static void Help()
        {
            ETGModConsole.Log("challenge list            - show challenges and their state");
            ETGModConsole.Log("challenge on <id> [id..]  - enable challenges");
            ETGModConsole.Log("challenge off <id> [id..] - disable challenges");
            ETGModConsole.Log("challenge clear           - disable everything");
            ETGModConsole.Log("Or press F6 in the Breach for the menu.");
        }

        private static void List()
        {
            foreach (var c in ChallengeManager.All)
            {
                string state = c.Enabled.Value ? "[ON] " : "[off]";
                if (ChallengeManager.IsActive(c.Id)) state += " (active this run)";
                ETGModConsole.Log($"{state} {c.Id,-10} {c.Name} - {c.Description}");
            }
        }

        private static void Toggle(string[] args, bool enabled)
        {
            if (args.Length == 0) { Help(); return; }
            foreach (var id in args.Select(a => a.ToLowerInvariant()))
                ETGModConsole.Log(ChallengeManager.SetEnabled(id, enabled));
        }
    }
}
