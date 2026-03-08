using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Colors;
using ECommons.Configuration;
using ToshiBox.Common;
using ToshiBox.Features;

namespace ToshiBox.UI.Features
{
    public class TurboHotbarsUI : IFeatureUI
    {
        private readonly TurboHotbars _feature;
        private readonly Config _config;

        public TurboHotbarsUI(TurboHotbars feature, Config config)
        {
            _feature = feature;
            _config = config;
        }

        public string Name => "Turbo Hotbars";

        public bool Enabled
        {
            get => _config.TurboHotbarsConfig.Enabled;
            set
            {
                _config.TurboHotbarsConfig.Enabled = value;
                _feature.IsEnabled();
                EzConfig.Save();
            }
        }

        public bool Visible => true;

        public void DrawSettings()
        {
            if (!_config.TurboHotbarsConfig.Enabled)
            {
                ImGui.TextColored(ImGuiColors.DalamudGrey, "Enable the feature to adjust settings.");
                return;
            }

            ImGui.PushItemWidth(250f);

            int interval = _config.TurboHotbarsConfig.Interval;
            if (ImGui.SliderInt("Repeat Interval (ms)", ref interval, 50, 1000))
            {
                _config.TurboHotbarsConfig.Interval = interval;
                EzConfig.Save();
            }

            int initialInterval = _config.TurboHotbarsConfig.InitialInterval;
            if (ImGui.SliderInt("Initial Delay (ms, 0 = same as interval)", ref initialInterval, 0, 1000))
            {
                _config.TurboHotbarsConfig.InitialInterval = initialInterval;
                EzConfig.Save();
            }

            bool enableOutOfCombat = _config.TurboHotbarsConfig.EnableOutOfCombat;
            if (ImGui.Checkbox("Enable Outside of Combat", ref enableOutOfCombat))
            {
                _config.TurboHotbarsConfig.EnableOutOfCombat = enableOutOfCombat;
                EzConfig.Save();
            }

            ImGui.PopItemWidth();
        }
    }
}
