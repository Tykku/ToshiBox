using ImGuiNET;
using Dalamud.Interface.Windowing;
using ToshiBox.Common;
using ToshiBox.Features;
using System.Numerics;

namespace ToshiBox.UI
{
    public class MainWindow : Window
    {
        private readonly AutoRetainerListing _feature;
        private readonly Config _config;

        public MainWindow(AutoRetainerListing feature, Config config) 
            : base("ToshiBox Settings", ImGuiWindowFlags.None)
        {
            _feature = feature;
            _config = config;
            Flags |= ImGuiWindowFlags.NoScrollbar;
        }
        
        public override void Draw()
        {
            float maxLabelWidth = ImGui.CalcTextSize("Enable Market Adjuster").X;
            float leftColumnWidth = maxLabelWidth + 40f;

            ImGui.Columns(2, null, true);
            ImGui.SetColumnWidth(0, leftColumnWidth);

            ImGui.Text("Features");
            ImGui.Separator();

            bool marketAdjusterEnabled = _config.MarketAdjusterConfiguration.Enabled;
            if (ImGui.Checkbox("Enable Market Adjuster", ref marketAdjusterEnabled))
            {
                _config.MarketAdjusterConfiguration.Enabled = marketAdjusterEnabled;
                _config.Save();
            }
            
            ImGui.NextColumn();

            ImGui.Text("Auto Retainer Listing Settings");
            ImGui.Separator();

            float inputWidth = 250f;
            ImGui.PushItemWidth(inputWidth);

            int priceReduction = _config.MarketAdjusterConfiguration.PriceReduction;
            if (ImGui.InputInt("Price Reduction", ref priceReduction))
            {
                if (priceReduction < 0) priceReduction = 0;
                _config.MarketAdjusterConfiguration.PriceReduction = priceReduction;
                _config.Save();
            }

            int lowestPrice = _config.MarketAdjusterConfiguration.LowestAcceptablePrice;
            if (ImGui.InputInt("Lowest Acceptable Price", ref lowestPrice))
            {
                if (lowestPrice < 0) lowestPrice = 0;
                _config.MarketAdjusterConfiguration.LowestAcceptablePrice = lowestPrice;
                _config.Save();
            }
            
            int maxReduction = _config.MarketAdjusterConfiguration.MaxPriceReduction;
            if (ImGui.InputInt("Max Price Reduction (0 = no limit)", ref maxReduction))
            {
                if (maxReduction < 0) maxReduction = 0;
                _config.MarketAdjusterConfiguration.MaxPriceReduction = maxReduction;
                _config.Save();
            }


            bool separateNQHQ = _config.MarketAdjusterConfiguration.SeparateNQAndHQ;
            if (ImGui.Checkbox("Separate NQ and HQ", ref separateNQHQ))
            {
                _config.MarketAdjusterConfiguration.SeparateNQAndHQ = separateNQHQ;
                _config.Save();
            }
            
            ImGui.PopItemWidth();
            ImGui.Columns(1); 
        }
    }
}
