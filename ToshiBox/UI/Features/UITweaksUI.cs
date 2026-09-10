using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Colors;
using ECommons.Configuration;
using ToshiBox.Common;
using ToshiBox.Features;

namespace ToshiBox.UI.Features
{
    public class UITweaksUI : IFeatureUI
    {
        private readonly UITweaks _feature;
        private readonly Config _config;

        public UITweaksUI(UITweaks feature, Config config)
        {
            _feature = feature;
            _config = config;
        }

        public string Name => "UI Tweaks";
        public string Group => "Features";
        public Dalamud.Interface.FontAwesomeIcon Icon => Dalamud.Interface.FontAwesomeIcon.PaintBrush;
        public bool HasEnabledToggle => false;
        public bool Enabled { get => false; set { } }
        public bool Visible => true;

        public void DrawSettings()
        {
            ImGui.TextColored(ImGuiColors.DalamudWhite, "Party List: Buffs on Left");
            ImGui.Separator();
            ImGui.Spacing();

            var enabled = _config.UITweaksConfig.PartyListBuffsOnLeft;
            if (Theme.ToggleSwitch("partylist_buffs_left", enabled ? "Enabled" : "Disabled", ref enabled))
            {
                _config.UITweaksConfig.PartyListBuffsOnLeft = enabled;
                _feature.IsEnabled();
                EzConfig.Save();
            }

            if (_config.UITweaksConfig.PartyListBuffsOnLeft)
            {
                ImGui.Spacing();
                ImGui.Indent();

                var healerOnly = _config.UITweaksConfig.PartyListBuffsOnLeftHealerOnly;
                if (ImGui.Checkbox("Only on healer jobs", ref healerOnly))
                {
                    _config.UITweaksConfig.PartyListBuffsOnLeftHealerOnly = healerOnly;
                    EzConfig.Save();
                }

                var dutyOnly = _config.UITweaksConfig.PartyListBuffsOnLeftDutyOnly;
                if (ImGui.Checkbox("Only while bound by a duty", ref dutyOnly))
                {
                    _config.UITweaksConfig.PartyListBuffsOnLeftDutyOnly = dutyOnly;
                    EzConfig.Save();
                }

                ImGui.Unindent();
            }

            ImGui.Spacing();
            ImGui.TextDisabled("Swaps the party list's buff/debuff so buffs sit on the left side of each row instead of the right.");
        }
    }
}