#if COFFEEBEAN_CORE
// CoffeeBean 模块标识 + Core 生命周期集成。
// 本文件所在的 Bridge 程序集仅在安装 Core 时编译（asmdef defineConstraints），
// 因此模块本身不依赖 Core 也能独立工作。
using CoffeeBean;

[assembly: CoffeeBeanModule(
    "com.coffeebean.purchase",
    "0.1.3",
    DisplayName = "Purchase",
    Description = "In-app purchase module based on Unity IAP 5.4.",
    Dependencies = new[] { "com.coffeebean.core" }
)]

namespace CoffeeBean.Purchase
{
    /// <summary>Core 集成：把 IapService 注册进服务注册表，其他模块可通过 context.Services.Get&lt;IapService&gt;() 使用。</summary>
    public sealed class PurchaseModule : ICoffeeBeanModule
    {
        public void OnLoad(CoffeeBeanContext context)
        {
            context.Services.Register(IapService.Instance);
            context.Log("CoffeeBean.Purchase integrated (IapService registered).");
        }

        public void OnStart()
        {
        }

        public void OnShutdown()
        {
        }
    }
}
#endif
