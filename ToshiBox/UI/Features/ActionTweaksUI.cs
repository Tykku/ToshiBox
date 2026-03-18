using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
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

        public string Name => "Action Tweaks : WARNING: DO NOT USE WITH NOCLIPPY, BOSSMOD ACTION TWEAKS, OR XIVALEXANDER!";
        public string SidebarName => "Action Tweaks";
        public bool HasEnabledToggle => false;
        public bool Enabled { get => false; set { } }
        public bool Visible => true;

        public void DrawSettings()
        {
            bool animLock = _config.ActionTweaksConfig.RemoveAnimationLockDelay;
            if (ImGui.Checkbox("Remove extra lag-induced animation lock delay from instant casts", ref animLock))
            {
                _config.ActionTweaksConfig.RemoveAnimationLockDelay = animLock;
                _feature.IsEnabled();
                EzConfig.Save();
            }
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip("Do NOT use with XivAlexander or NoClippy.\nThis will automatically disable itself if a conflicting plugin is detected.");

            if (animLock)
            {
                ImGui.PushItemWidth(250f);
                ImGui.Indent();
                int delayMax = _config.ActionTweaksConfig.AnimationLockDelayMax;
                if (ImGui.SliderInt("Max simulated delay (ms)", ref delayMax, 0, 50))
                {
                    _config.ActionTweaksConfig.AnimationLockDelayMax = delayMax;
                    EzConfig.Save();
                }
                if (ImGui.IsItemHovered())
                    ImGui.SetTooltip("Maximum simulated delay in ms.\n20ms enables triple-weaving.\nMinimum to prevent triple-weaving is 26ms.\nSetting this to 0 is very dangerous!");
                ImGui.Unindent();
                ImGui.PopItemWidth();
            }

            ImGui.Spacing();

            bool cooldown = _config.ActionTweaksConfig.RemoveCooldownDelay;
            if (ImGui.Checkbox("Remove extra framerate-induced cooldown delay", ref cooldown))
            {
                _config.ActionTweaksConfig.RemoveCooldownDelay = cooldown;
                _feature.IsEnabled();
                EzConfig.Save();
            }
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip("Adjusts cooldown and animation locks so queued actions resolve immediately,\nregardless of your current framerate.\nDefault is 100, anything else doesn't matter.");

            if (cooldown)
            {
                ImGui.PushItemWidth(250f);
                ImGui.Indent();
                int cooldownMax = _config.ActionTweaksConfig.CooldownDelayMax;
                if (ImGui.SliderInt("Max cooldown adjustment (ms)", ref cooldownMax, 0, 200))
                {
                    _config.ActionTweaksConfig.CooldownDelayMax = cooldownMax;
                    EzConfig.Save();
                }
                if (ImGui.IsItemHovered())
                    ImGui.SetTooltip("Maximum adjustment in ms applied to cooldowns and animation locks.\nHigher values help at very low framerates but may look suspicious on logs.");
                ImGui.Unindent();
                ImGui.PopItemWidth();
            }
        }
    }
}