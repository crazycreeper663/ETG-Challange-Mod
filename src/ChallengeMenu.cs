using System.Collections.Generic;
using UnityEngine;

namespace ChallengeRuns
{
    // Simple IMGUI picker. Toggled with F6 in the Breach; opens by itself on the
    // death/victory screen so the next run can be set up before restarting.
    public class ChallengeMenu : MonoBehaviour
    {
        public static KeyCode ToggleKey = KeyCode.F6;

        private bool visible;
        private bool autoOpenedThisRunEnd;
        private string status = "";
        private Rect window = new Rect(40, 40, 420, 0);

        // The game's own UI (dfGUI) also reacts to clicks, so it is switched off while the
        // mouse is over this window. Otherwise clicking a checkbox could also click, say,
        // the Quick Restart button underneath.
        private readonly List<dfInputManager> blockedInput = new List<dfInputManager>();

        private void Update()
        {
            if (ChallengeManager.RunEnded && !autoOpenedThisRunEnd)
            {
                autoOpenedThisRunEnd = true;
                visible = true;
                status = "";
            }
            if (!ChallengeManager.RunEnded)
                autoOpenedThisRunEnd = false;

            if (Input.GetKeyDown(ToggleKey))
                visible = !visible;
            if (visible && !ChallengeManager.CanEdit)
                visible = false;
            if (!visible)
                SetGameInputBlocked(false);
        }

        private void OnGUI()
        {
            if (!visible) return;
            if (Event.current.type == EventType.Repaint)
                SetGameInputBlocked(window.Contains(Event.current.mousePosition));
            window = GUILayout.Window(GetInstanceID(), window, DrawWindow, "Challenge Runs");
        }

        private void OnDisable() => SetGameInputBlocked(false);

        private void DrawWindow(int id)
        {
            GUILayout.Label(ChallengeManager.RunEnded
                ? "Run over! Pick challenges for your next run, then Quick Restart or head back to the Breach:"
                : "Pick challenges for your next run:");

            var descriptionStyle = new GUIStyle(GUI.skin.label) { wordWrap = true, fontSize = 11 };
            foreach (var c in ChallengeManager.All)
            {
                bool now = GUILayout.Toggle(c.Enabled.Value, $" {c.Name}");
                if (now != c.Enabled.Value)
                    status = ChallengeManager.SetEnabled(c.Id, now);
                GUILayout.Label("     " + c.Description, descriptionStyle);
            }

            GUILayout.Space(6);
            if (!string.IsNullOrEmpty(status))
                GUILayout.Label(status);

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Clear all"))
            {
                foreach (var c in ChallengeManager.All) c.Enabled.Value = false;
                status = "All challenges disabled.";
            }
            if (GUILayout.Button("Close (F6)"))
                visible = false;
            GUILayout.EndHorizontal();

            GUI.DragWindow();
        }

        private void SetGameInputBlocked(bool blocked)
        {
            if (blocked == (blockedInput.Count > 0)) return;
            if (blocked)
            {
                foreach (var input in FindObjectsOfType<dfInputManager>())
                {
                    if (!input.enabled) continue;
                    input.enabled = false;
                    blockedInput.Add(input);
                }
            }
            else
            {
                foreach (var input in blockedInput)
                    if (input != null) input.enabled = true;
                blockedInput.Clear();
            }
        }
    }
}
