using UnityEngine;

namespace CoffeeBean.Purchase
{
    /// <summary>可开关的模块日志。</summary>
    public static class IapLog
    {
        /// <summary>全局日志开关（默认开）。</summary>
        public static bool Enabled = true;

        public static void Log(string message)
        {
            if (Enabled) Debug.Log("[CoffeeBean.Purchase] " + message);
        }

        public static void Warn(string message)
        {
            if (Enabled) Debug.LogWarning("[CoffeeBean.Purchase] " + message);
        }

        public static void Error(string message)
        {
            if (Enabled) Debug.LogError("[CoffeeBean.Purchase] " + message);
        }
    }
}
