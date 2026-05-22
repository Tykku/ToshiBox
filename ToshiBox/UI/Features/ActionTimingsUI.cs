using System;
using Dalamud.Bindings.ImGui;
using ECommons.Configuration;
using ToshiBox.Common;
using ToshiBox.Features;

namespace ToshiBox.UI.Features
{
    public class ActionTimingsUI : IFeatureUI
    {
        private readonly NewActionTimings _feature;
        private readonly Config _config;
        private NewActionTimingsConfig Cfg => _config.NewActionTimingsConfig;

        public ActionTimingsUI(NewActionTimings feature, Config config)
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
            bool enabled = Cfg.Enabled;
            if (ImGui.Checkbox("Enable animation lock + cooldown delay reduction", ref enabled))
            {
                Cfg.Enabled = enabled;
                _feature.IsEnabled();
                EzConfig.Save();
            }
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip("Do NOT use with XivAlexander, NoClippy, or BossMod action tweaks.");

            if (enabled)
            {
                ImGui.Indent();
                ImGui.PushItemWidth(250f);

                bool usePct = Cfg.UsePercentageReduction;
                if (ImGui.Checkbox("Use percentage reduction instead of RTT correction\nWarning: setting percent to 100 is basically cheating\nI am not responsible for being banned", ref usePct))
                {
                    Cfg.UsePercentageReduction = usePct;
                    EzConfig.Save();
                }

                if (usePct)
                {
                    float pct = Cfg.AnimationLockPercent;
                    if (ImGui.SliderFloat("% Reduction", ref pct, 1f, 100f, "%.0f%%"))
                    {
                        Cfg.AnimationLockPercent = Math.Clamp(MathF.Round(pct), 1f, 100f);
                        EzConfig.Save();
                    }
                }
                else
                {
                    int simulatedRtt = Cfg.SimulatedRttMs;
                    if (ImGui.SliderInt("Simulated RTT (ms)", ref simulatedRtt, 1, 50))
                    {
                        Cfg.SimulatedRttMs = Math.Clamp(simulatedRtt, 1, 50);
                        EzConfig.Save();
                    }
                    if (ImGui.IsItemHovered())
                        ImGui.SetTooltip("The minimum simulated ping floor in ms.\n1ms = near-maximum reduction.");
                }

                bool ignoreCast = Cfg.EnableIgnoreCasting;
                if (ImGui.Checkbox("Allow cast & limit break animation lock to be reduced (try off first)", ref ignoreCast))
                {
                    Cfg.EnableIgnoreCasting = ignoreCast;
                    EzConfig.Save();
                }

                ImGui.PopItemWidth();
                ImGui.Unindent();
            }
        }
    }
}
