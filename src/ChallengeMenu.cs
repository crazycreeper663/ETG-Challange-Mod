using UnityEngine;

namespace ChallengeRuns
{
    // Simple IMGUI picker, toggled with F6 while in the Breach.
    public class ChallengeMenu : MonoBehaviour
    {
        public static KeyCode ToggleKey = KeyCode.F6;

        private bool visible;
        private string status = "";
        private Rect window = new Rect(40, 40, 420, 0);

        private void Update()
        {
            if (Input.GetKeyDown(ToggleKey))
                visible = !visible;
            if (visible && !ChallengeManager.CanEdit)
                visible = false;
        }

        private void OnGUI()
        {
            if (!visible) return;
            window = GUILayout.Window(GetInstanceID(), window, DrawWindow, "Challenge Runs");
        }

        private void DrawWindow(int id)
        {
            GUILayout.Label("Pick challenges for your next run:");
            foreach (var c in ChallengeManager.All)
            {
                bool now = GUILayout.Toggle(c.Enabled.Value, $" {c.Name}");
                if (now != c.Enabled.Value)
                    status = ChallengeManager.SetEnabled(c.Id, now);

                var style = new GUIStyle(GUI.skin.label) { wordWrap = true, fontSize = 11 };
                GUILayout.Label("     " + c.Description, style);
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
    }
}
