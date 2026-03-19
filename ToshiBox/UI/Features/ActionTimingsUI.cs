using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using ECommons.Configuration;
using ToshiBox.Common;
using ToshiBox.Features;

namespace ToshiBox.UI.Features
{
    public class ActionTimingsUI : IFeatureUI
    {
        private readonly ActionTimings _feature;
        private readonly Config _config;

        public ActionTimingsUI(ActionTimings feature, Config config)
        {
            _feature = feature;
            _config = config;
        }

        public string Name => "Action Timings : WARNING: DO NOT USE WITH NOCLIPPY, BOSSMOD ACTION TWEAKS, OR XIVALEXANDER!";
        public string SidebarName => "Action Timings";
        public bool HasEnabledToggle => false;
        public bool Enabled { get => false; set { } }
        public bool Visible => true;

        public void DrawSettings()
        {
            bool animLock = _config.ActionTimingsConfig.RemoveAnimationLockDelay;
            if (ImGui.Checkbox("Remove extra lag-induced animation lock delay from instant casts", ref animLock))
            {
                _config.ActionTimingsConfig.RemoveAnimationLockDelay = animLock;
                _feature.IsEnabled();
                EzConfig.Save();
            }
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip("Do NOT use with XivAlexander or NoClippy.\nThis will automatically disable itself if a conflicting plugin is detected.");

            if (animLock)
            {
                ImGui.PushItemWidth(250f);
                ImGui.Indent();
                int delayMax = _config.ActionTimingsConfig.AnimationLockDelayMax;
                if (ImGui.SliderInt("Max simulated delay (ms)", ref delayMax, 0, 50))
                {
                    _config.ActionTimingsConfig.AnimationLockDelayMax = delayMax;
                    EzConfig.Save();
                }
                if (ImGui.IsItemHovered())
                    ImGui.SetTooltip("Maximum simulated delay in ms.\n20ms enables triple-weaving.\nMinimum to prevent triple-weaving is 26ms.");
                bool smoothed = _config.ActionTimingsConfig.UseSmoothedDelay;
                if (ImGui.Checkbox("Use smoothed delay (enable with high jitter in ping)", ref smoothed))
                {
                    _config.ActionTimingsConfig.UseSmoothedDelay = smoothed;
                    EzConfig.Save();
                }
                if (ImGui.IsItemHovered())
                    ImGui.SetTooltip("Smooths delay over recent actions instead of reacting per-action.\nMore stable on unstable connections.");
                ImGui.Unindent();
                ImGui.PopItemWidth();
            }

            ImGui.Spacing();

            bool cooldown = _config.ActionTimingsConfig.RemoveCooldownDelay;
            if (ImGui.Checkbox("Remove extra framerate-induced cooldown delay", ref cooldown))
            {
                _config.ActionTimingsConfig.RemoveCooldownDelay = cooldown;
                _feature.IsEnabled();
                EzConfig.Save();
            }
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip("Adjusts cooldown and animation locks so queued actions resolve immediately,\nregardless of your current framerate.");

            if (cooldown)
                _config.ActionTimingsConfig.CooldownDelayMax = 100;
        }
    }
}