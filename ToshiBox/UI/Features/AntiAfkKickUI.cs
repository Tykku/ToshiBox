using Dalamud.Bindings.ImGui;
using ECommons.Configuration;
using FFXIVClientStructs.FFXIV.Client.UI;
using ToshiBox.Common;
using ToshiBox.Features;

namespace ToshiBox.UI.Features
{
    public class AntiAfkKickUI : IFeatureUI
    {
        private readonly AntiAfkKick _feature;
        private readonly Config _config;

        public AntiAfkKickUI(AntiAfkKick feature, Config config)
        {
            _feature = feature;
            _config = config;
        }

        public string Name => "Anti-AFK Kick";
        public string Group => "Features";
        public Dalamud.Interface.FontAwesomeIcon Icon => Dalamud.Interface.FontAwesomeIcon.Clock;

        public bool Enabled
        {
            get => _config.AntiAfkKickConfig.Enabled;
            set
            {
                _config.AntiAfkKickConfig.Enabled = value;
                _feature.IsEnabled();
                EzConfig.Save();
            }
        }

        public bool Visible => true;

        public void DrawSettings()
        {
            if (!_config.AntiAfkKickConfig.Enabled)
            {
                ImGui.TextDisabled("Enable the feature to adjust settings.");
                return;
            }

            ImGui.PushItemWidth(200f);
            int timerLimit = _config.AntiAfkKickConfig.TimerLimit;
            if (ImGui.SliderInt("AFK timer threshold (seconds)", ref timerLimit, 10, 270))
            {
                _config.AntiAfkKickConfig.TimerLimit = timerLimit;
                EzConfig.Save();
            }
            ImGui.PopItemWidth();

            ImGui.TextDisabled("Sends a silent LCtrl keypress to reset the AFK timer when it exceeds the threshold.");

#if DEBUG
            ImGui.Spacing();
            ImGui.Separator();
            ImGui.Spacing();

            unsafe
            {
                var t = UIModule.Instance()->GetInputTimerModule();
                ImGui.Text($"AfkTimer:          {t->AfkTimer:F1}s");
                ImGui.Text($"ContentInputTimer: {t->ContentInputTimer:F1}s");
                ImGui.Text($"InputTimer:        {t->InputTimer:F1}s");
            }
#endif
        }
    }
}
