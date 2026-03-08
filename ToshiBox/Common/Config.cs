using ECommons.Configuration;

namespace ToshiBox.Common
{
    public class Config
    {
        public AutoRetainerListingConfig AutoRetainerListingConfig = new();
        public AutoChestOpenConfig AutoChestOpenConfig = new();
        public TurboHotbarsConfig TurboHotbarsConfig = new();
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

}

