using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Colors;
using ECommons.Configuration;
using ToshiBox.Common;
using ToshiBox.Features;

namespace ToshiBox.UI.Features
{
    public class ActionTweaksUI : IFeatureUI
    {
        private readonly ActionTweaks _feature;
        private readonly Config _config;

        public ActionTweaksUI(ActionTweaks feature, Config config)
        {
            _feature = feature;
            _config = config;
        }

        public string Name => "Action Tweaks";
        public bool HasEnabledToggle => false;
        public bool Enabled { get => false; set { } }
        public bool Visible => true;

        public void DrawSettings()
        {
            ImGui.TextColored(ImGuiColors.DalamudWhite, "Turbo Hotbars");
            ImGui.Separator();
            ImGui.Spacing();

            var enabled = _config.TurboHotbarsConfig.Enabled;
            if (Theme.ToggleSwitch("turbo_enabled", enabled ? "Enabled" : "Disabled", ref enabled))
            {
                _config.TurboHotbarsConfig.Enabled = enabled;
                _feature.IsEnabled();
                EzConfig.Save();
            }

            ImGui.Spacing();

            if (_config.TurboHotbarsConfig.Enabled)
            {
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

            ImGui.Spacing();
            ImGui.Spacing();

            ImGui.TextColored(ImGuiColors.DalamudWhite, "Camera Relative Dashes");
            ImGui.Separator();
            ImGui.Spacing();

            var dashEnabled = _config.CameraRelativeDashesConfig.Enabled;
            if (Theme.ToggleSwitch("dash_enabled", dashEnabled ? "Enabled" : "Disabled", ref dashEnabled))
            {
                _config.CameraRelativeDashesConfig.Enabled = dashEnabled;
                _feature.IsEnabled();
                EzConfig.Save();
            }

            if (_config.CameraRelativeDashesConfig.Enabled)
            {
                ImGui.Spacing();
                ImGui.Indent();
                bool blockBackward = _config.CameraRelativeDashesConfig.BlockBackwardDashes;
                if (ImGui.Checkbox("Block Backward Dashes", ref blockBackward))
                {
                    _config.CameraRelativeDashesConfig.BlockBackwardDashes = blockBackward;
                    EzConfig.Save();
                }
                if (ImGui.IsItemHovered())
                    ImGui.SetTooltip("Disables camera-relative behavior for backward dashes like Elusive Jump.");
                ImGui.Unindent();
            }

            ImGui.Spacing();
            ImGui.Spacing();

            ImGui.TextColored(ImGuiColors.DalamudWhite, "Auto Dismount");
            ImGui.Separator();
            ImGui.Spacing();

            var dismountEnabled = _config.AutoDismountConfig.Enabled;
            if (Theme.ToggleSwitch("dismount_enabled", dismountEnabled ? "Enabled" : "Disabled", ref dismountEnabled))
            {
                _config.AutoDismountConfig.Enabled = dismountEnabled;
                _feature.IsEnabled();
                EzConfig.Save();
            }
        }
    }
}
