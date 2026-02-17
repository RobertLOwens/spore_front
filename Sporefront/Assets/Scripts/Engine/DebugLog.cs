using UnityEngine;

namespace Sporefront.Engine
{
    public static class DebugLog
    {
        public static bool Enabled = true;

        [System.Diagnostics.Conditional("DEBUG")]
        public static void Log(string message)
        {
            if (Enabled)
                Debug.Log(message);
        }
    }
}
