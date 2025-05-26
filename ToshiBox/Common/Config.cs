using ECommons.Configuration;

namespace ToshiBox.Common;

public class Config : IEzConfig
{
    public MarketAdjusterConfiguration MarketAdjusterConfiguration = new();
}

public class MarketAdjusterConfiguration
{
    public int PriceReduction = 1;
    public int LowestAcceptablePrice = 100;
    public bool SeparateNQAndHQ = true;
    public int MaxPriceReduction = 0;
    public bool Enabled = true;
}
