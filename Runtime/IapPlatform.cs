using UnityEngine;

namespace CoffeeBean.Purchase
{
    /// <summary>平台判断与商店 ID 解析。</summary>
    public static class IapPlatform
    {
        public static bool IsApple =>
            Application.platform == RuntimePlatform.IPhonePlayer ||
            Application.platform == RuntimePlatform.OSXPlayer ||
            Application.platform == RuntimePlatform.tvOS;

        public static bool IsAndroid => Application.platform == RuntimePlatform.Android;

        /// <summary>解析当前平台对应的商店 ID（Editor/其他平台默认用 Google ID）。</summary>
        public static string ResolveStoreId(IapProductDefinition definition)
        {
            return IsApple ? definition.appleProductId : definition.googleProductId;
        }

        /// <summary>从已缓存商品取当前平台 ID。</summary>
        public static string GetPlatformId(IapProduct product)
        {
            return IsApple ? product.AppleProductId : product.GoogleProductId;
        }
    }
}
