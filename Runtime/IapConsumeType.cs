namespace CoffeeBean
{
    /// <summary>
    /// 商品类型（对应 Excel 的 ConsumeType_i）。
    /// v1 仅支持 Consumable / NonConsumable；Subscription 保留枚举值但校验会拦截。
    /// </summary>
    public enum IapConsumeType
    {
        /// <summary>消耗型：可重复购买，购买后核销一次（金币、道具）。</summary>
        Consumable = 0,

        /// <summary>非消耗型：永久解锁，一次购买终身拥有（去广告、关卡解锁）。</summary>
        NonConsumable = 1,

        /// <summary>订阅：v1 暂不支持，预留。</summary>
        Subscription = 2,
    }
}
