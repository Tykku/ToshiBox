using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Colors;
using ECommons.Configuration;
using ToshiBox.Common;
using ToshiBox.Features;

namespace ToshiBox.UI.Features;

public class AntiAfkKickUI : IFeatureUI
{
    private readonly AntiAfkKick _feature;
    private readonly Config _config;

    public AntiAfkKickUI(AntiAfkKick feature, Config config)
    {
        _feature = feature;
        _config = config;
    }

    public string Name => "AntiAfk";
    public bool Enabled
    {
        get => _config.AntiAfkConfig.Enabled;
        set
        {
            _config.AntiAfkConfig.Enabled = value;
            _feature.IsEnabled();
            EzConfig.Save();
        }
    }

    public bool Visible => true;
    public void DrawSettings()
    {
        if (!_config.AntiAfkConfig.Enabled)
        {
            ImGui.TextColored(ImGuiColors.DalamudGrey, "Enable this feature to adjust settings.");
            return;
        }
        
        ImGui.PushItemWidth(250f);

        int checkInterval = _config.AntiAfkConfig.CheckInterval;
        if (ImGui.SliderInt("Check Interval (seconds)", ref checkInterval, 5, 30))
        {
            _config.AntiAfkConfig.CheckInterval = checkInterval; // Convert back to milliseconds
            EzConfig.Save();
        }
        
        int maxIdle = _config.AntiAfkConfig.MaxIdle;
        if (ImGui.SliderInt("Max Idle (seconds)", ref maxIdle, 10, 120))
        {
            _config.AntiAfkConfig.MaxIdle = maxIdle; // Convert back to milliseconds
            EzConfig.Save();
        }
        
        ImGui.PopItemWidth();
    }
}