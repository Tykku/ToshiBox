using System.Numerics;
using Dalamud.Bindings.ImGui;
using ECommons.Configuration;
using ToshiBox.Common;

namespace ToshiBox.UI
{
    public class AnnouncementWindow
    {
        private readonly Config _config;
        public bool IsOpen;

        public AnnouncementWindow(Config config)
        {
            _config = config;
        }

        public void Draw()
        {
            if (!IsOpen) return;

            var viewport = ImGui.GetMainViewport();
            var center   = viewport.GetCenter();
            ImGui.SetNextWindowPos(center, ImGuiCond.Always, new Vector2(0.5f, 0.5f));
            ImGui.SetNextWindowSize(new Vector2(480, 0), ImGuiCond.Always);

            ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, new Vector2(20, 20));
            var flags = ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoCollapse |
                        ImGuiWindowFlags.NoMove   | ImGuiWindowFlags.AlwaysAutoResize |
                        ImGuiWindowFlags.NoScrollbar;

            if (ImGui.Begin("ToshiBox - Important Notice###ToshiBoxAnnouncement", flags))
            {
                ImGui.TextColored(new Vector4(1f, 0.85f, 0.3f, 1f), "ToshiBox is moving to a new home!");
                ImGui.Spacing();
                ImGui.TextWrapped("The plugin repository has been relocated. Please update your Dalamud custom plugin source to the new URL:");
                ImGui.Spacing();
                ImGui.TextColored(new Vector4(0.4f, 0.75f, 1f, 1f), "https://toshibox.tykku.com");
                ImGui.Spacing();
                ImGui.Separator();
                ImGui.Spacing();
                ImGui.TextWrapped("You will need to add the new source in Dalamud settings to continue receiving updates after the migration is complete.");
                ImGui.Spacing();

                var btnWidth = 120f;
                ImGui.SetCursorPosX((ImGui.GetContentRegionAvail().X - btnWidth) / 2f + ImGui.GetStyle().WindowPadding.X);
                if (ImGui.Button("Got it!", new Vector2(btnWidth, 0)))
                {
                    _config.RepoMoveAnnouncementSeen = true;
                    IsOpen = false;
                    EzConfig.Save();
                }
            }
            ImGui.PopStyleVar();
            ImGui.End();
        }
    }
}
