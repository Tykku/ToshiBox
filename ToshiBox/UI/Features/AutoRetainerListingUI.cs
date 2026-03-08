using System;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Colors;
using ECommons.Configuration;
using ToshiBox.Common;
using ToshiBox.Features;

namespace ToshiBox.UI.Features
{
    public class AutoRetainerListingUI : IFeatureUI
    {
        private readonly AutoRetainerListing _feature;
        private readonly Config _config;

        public AutoRetainerListingUI(AutoRetainerListing feature, Config config)
        {
            _feature = feature;
            _config = config;
        }

        public string Name => "Auto Retainer Listing";
        public bool Visible => _feature.ShowInList;

        public bool Enabled
        {
            get => _config.AutoRetainerListingConfig.Enabled;
            set
            {
                _config.AutoRetainerListingConfig.Enabled = value;
                _feature.IsEnabled();
                EzConfig.Save();
            }
        }

        public void DrawSettings()
        {
            if (!_config.AutoRetainerListingConfig.Enabled)
            {
                ImGui.TextColored(ImGuiColors.DalamudGrey, "Enable the feature to adjust settings.");
                return;
            }

            ImGui.PushItemWidth(250f);

            int priceReduction = _config.AutoRetainerListingConfig.PriceReduction;
            if (ImGui.InputInt("Price Reduction", ref priceReduction))
            {
                _config.AutoRetainerListingConfig.PriceReduction = Math.Max(0, priceReduction);
                EzConfig.Save();
            }

            int lowestPrice = _config.AutoRetainerListingConfig.LowestAcceptablePrice;
            if (ImGui.InputInt("Lowest Acceptable Price", ref lowestPrice))
            {
                _config.AutoRetainerListingConfig.LowestAcceptablePrice = Math.Max(0, lowestPrice);
                EzConfig.Save();
            }

            int maxReduction = _config.AutoRetainerListingConfig.MaxPriceReduction;
            if (ImGui.InputInt("Max Price Reduction (0 = no limit)", ref maxReduction))
            {
                _config.AutoRetainerListingConfig.MaxPriceReduction = Math.Max(0, maxReduction);
                EzConfig.Save();
            }

            bool separateNQHQ = _config.AutoRetainerListingConfig.SeparateNQAndHQ;
            if (ImGui.Checkbox("Separate NQ and HQ", ref separateNQHQ))
            {
                _config.AutoRetainerListingConfig.SeparateNQAndHQ = separateNQHQ;
                EzConfig.Save();
            }

            ImGui.PopItemWidth();
        }
    }
}