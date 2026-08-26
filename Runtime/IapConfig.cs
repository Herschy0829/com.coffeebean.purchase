using System.Collections.Generic;
using UnityEngine;

namespace CoffeeBean
{
    /// <summary>
    /// 支付模块配置（Excel 生成的产物：.asset 供运行时读取，JSON 为旁证/CI 用）。
    /// 全局设置 + 商品列表。
    /// </summary>
    [CreateAssetMenu(fileName = "IapConfig", menuName = "CoffeeBean/Purchase Config")]
    public sealed class IapConfig : ScriptableObject
    {
        [Header("全局设置")]
        [Tooltip("服务器二次确认总开关；关闭时购买成功后直接完成，无需服务器")]
        public bool serverVerifyEnabled;

        [Tooltip("服务器验证超时（秒）")]
        public float verifyTimeoutSeconds = 10f;

        [Tooltip("服务器验证失败后的重试次数")]
        public int verifyRetryCount = 3;

        [Tooltip("打包前是否强制重新解析 Excel（见设计 §5）")]
        public bool forceReparseOnBuild = true;

        [Header("商品")]
        [Tooltip("由 Excel 生成，勿手改")]
        public List<IapProductDefinition> products = new List<IapProductDefinition>();

        /// <summary>序列化为 JSON（与 .asset 内容一致）。</summary>
        public string ToJson() => JsonUtility.ToJson(this, true);

        /// <summary>从 JSON 恢复配置实例（仅内存，不创建资产）。</summary>
        public static IapConfig FromJson(string json)
        {
            var config = CreateInstance<IapConfig>();
            JsonUtility.FromJsonOverwrite(json, config);
            return config;
        }

        /// <summary>按内部 ID 查找商品定义。</summary>
        public IapProductDefinition FindByInternalId(string internalId)
        {
            if (internalId == null) return null;
            foreach (var p in products)
                if (p != null && string.Equals(p.internalId, internalId, System.StringComparison.OrdinalIgnoreCase))
                    return p;
            return null;
        }

        /// <summary>按平台 ID 查找商品定义（Google / Apple 任一匹配）。</summary>
        public IapProductDefinition FindByPlatformId(string platformProductId)
        {
            if (platformProductId == null) return null;
            foreach (var p in products)
            {
                if (p == null) continue;
                if (string.Equals(p.googleProductId, platformProductId, System.StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(p.appleProductId, platformProductId, System.StringComparison.OrdinalIgnoreCase))
                    return p;
            }
            return null;
        }
    }
}
