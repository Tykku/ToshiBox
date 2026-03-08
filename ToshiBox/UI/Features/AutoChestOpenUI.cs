using System;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Colors;
using ECommons.Configuration;
using ToshiBox.Common;
using ToshiBox.Features;

namespace ToshiBox.UI.Features
{
    public class AutoChestOpenUI : IFeatureUI
    {
        private readonly AutoChestOpen _feature;
        private readonly Config _config;

        public AutoChestOpenUI(AutoChestOpen feature, Config config)
        {
            _feature = feature;
            _config = config;
        }

        public string Name => "Auto Chest Open";

        public bool Enabled
        {
            get => _config.AutoChestOpenConfig.Enabled;
            set
            {
                _config.AutoChestOpenConfig.Enabled = value;
                _feature.IsEnabled();
                EzConfig.Save();
            }
        }

        public bool Visible => true;

        public void DrawSettings()
        {
            if (!_config.AutoChestOpenConfig.Enabled)
            {
                ImGui.TextColored(ImGuiColors.DalamudGrey, "Enable the feature to adjust settings.");
                return;
            }

            ImGui.PushItemWidth(250f);

            float distance = _config.AutoChestOpenConfig.Distance;
            if (ImGui.SliderFloat("Distance (yalms)", ref distance, 0f, 3f, "%.1f"))
            {
                distance = (float)Math.Round(distance * 10f) / 10f;
                _config.AutoChestOpenConfig.Distance = distance;
                EzConfig.Save();
            }

            float delay = _config.AutoChestOpenConfig.Delay;
            if (ImGui.SliderFloat("Delay (seconds)", ref delay, 0f, 2f, "%.1f"))
            {
                delay = (float)Math.Round(delay * 10f) / 10f;
                _config.AutoChestOpenConfig.Delay = delay;
                EzConfig.Save();
            }

            bool openInHighEnd = _config.AutoChestOpenConfig.OpenInHighEndDuty;
            if (ImGui.Checkbox("Open Chests in High End Duties", ref openInHighEnd))
            {
                _config.AutoChestOpenConfig.OpenInHighEndDuty = openInHighEnd;
                EzConfig.Save();
            }

            bool closeLootWindow = _config.AutoChestOpenConfig.CloseLootWindow;
            if (ImGui.Checkbox("Close Loot Window After Opening", ref closeLootWindow))
            {
                _config.AutoChestOpenConfig.CloseLootWindow = closeLootWindow;
                EzConfig.Save();
            }

            ImGui.PopItemWidth();
        }
    }
}