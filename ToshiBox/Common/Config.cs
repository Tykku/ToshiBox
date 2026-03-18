using System.Collections.Generic;
using ECommons.Configuration;

namespace ToshiBox.Common
{
    public class Config
    {
        public Dictionary<string, bool> SidebarGroupExpanded = new();
        public AutoRetainerListingConfig AutoRetainerListingConfig = new();
        public AutoChestOpenConfig AutoChestOpenConfig = new();
        public TurboHotbarsConfig TurboHotbarsConfig = new();
        public ActionTweaksConfig ActionTweaksConfig = new();
        public MarketInsightsConfig MarketInsightsConfig = new();
    }

    public class MarketInsightsConfig
    {
        public string DataCenter = "";
        public int RefreshIntervalMinutes = 0; // 0 = manual only

        // Best Deals (Saddlebag Exchange)
        public string BestDealsHomeServer  = "";
        public int    BestDealsDiscount    = 70;    // minimum discount % (70 = 30% off)
        public int    BestDealsMinMedian   = 50000; // min median price on home server
        public int    BestDealsMaxBuyPrice = 20000; // max gil to spend per item
        public int    BestDealsMinSales    = 20;    // min sales in last 7 days
    }

    public class AutoRetainerListingConfig
    {
        public bool Enabled = false;
        public int PriceReduction = 1;
        public int LowestAcceptablePrice = 100;
        public int MaxPriceReduction = 0;
        public bool SeparateNQAndHQ = true;
    }

    public class AutoChestOpenConfig
    {
        public bool Enabled = false;
        public bool CloseLootWindow = false;
        public bool OpenInHighEndDuty = false;

        public float Distance { get; set; } = 1.0f;
        public float Delay { get; set; } = 0.0f;
    }

    public class TurboHotbarsConfig
    {
        public bool Enabled = false;
        public int Interval = 100;
        public int InitialInterval = 200;
        public bool EnableOutOfCombat = false;
    }

    public class ActionTweaksConfig
    {
        public bool RemoveAnimationLockDelay = false;
        public int AnimationLockDelayMax = 20;
        public bool RemoveCooldownDelay = false;
        public int CooldownDelayMax = 100;
    }

}

