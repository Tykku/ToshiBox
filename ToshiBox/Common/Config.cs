using System.Collections.Generic;
using ECommons.Configuration;

namespace ToshiBox.Common
{
    public class Config
    {
        public Dictionary<string, bool> SidebarGroupExpanded = new();
        public bool RepoMoveAnnouncementSeen = false;
        public AutoRetainerListingConfig AutoRetainerListingConfig = new();
        public AutoChestOpenConfig AutoChestOpenConfig = new();
        public TurboHotbarsConfig TurboHotbarsConfig = new();
        public CameraRelativeDashesConfig CameraRelativeDashesConfig = new();
        public AutoDismountConfig AutoDismountConfig = new();
        public ActionTimingsConfig ActionTimingsConfig = new();
        public NewActionTimingsConfig NewActionTimingsConfig = new();
        public MarketInsightsConfig MarketInsightsConfig = new();
        public AntiAfkConfig AntiAfkConfig = new();
    }

    public class MarketInsightsConfig
    {
        public string DataCenter = "";
        public int RefreshIntervalMinutes = 0;

        public string BestDealsHomeServer  = "";
        public int    BestDealsDiscount    = 70;
        public int    BestDealsMinMedian   = 50000;
        public int    BestDealsMaxBuyPrice = 20000;
        public int    BestDealsMinSales    = 20;
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

    public class CameraRelativeDashesConfig
    {
        public bool Enabled = false;
        public bool BlockBackwardDashes = false;
    }

    public class AutoDismountConfig
    {
        public bool Enabled = false;
    }

    public class ActionTimingsConfig
    {
        public bool RemoveAnimationLockDelay = false;
        public int AnimationLockDelayMax = 20;
        public bool UseSmoothedDelay = false;
    }

    public class NewActionTimingsConfig
    {
        public bool Enabled = false;
        public int SimulatedRttMs = 1;
        public bool UsePercentageReduction = false;
        public float AnimationLockPercent = 75f;
        public bool EnableIgnoreCasting = false;
        public Dictionary<uint, float> AnimationLockDatabase = new();
    }

    public class AntiAfkConfig
    {
        public bool Enabled = true;
        public int CheckInterval = 10;
        public int MaxIdle = 30;
    }
}
