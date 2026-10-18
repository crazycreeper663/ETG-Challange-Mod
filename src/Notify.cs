using System;
using System.Runtime.CompilerServices;

namespace ChallengeRuns
{
    // The game can refuse calls to its non-public members (MethodAccessException), and when it
    // does, the whole calling method fails. The pop-up call lives in its own non-inlined method
    // so a refusal only loses the pop-up; the message still goes to the console.
    internal static class Notify
    {
        public static void Show(string header, string text)
        {
            try
            {
                ShowPopup(header, text);
            }
            catch (Exception)
            {
                ETGModConsole.Log($"{header}: {text}");
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void ShowPopup(string header, string text) =>
            GameUIRoot.Instance?.notificationController?.DoCustomNotification(header, text, null, -1);
    }
}
