using System.Numerics;
using Dalamud.Bindings.ImGui;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Game.Event;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;

namespace ToshiBox.UI.Features
{
    public unsafe class DebugUI : IFeatureUI
    {
        public string Name           => "Debug";
        public bool Enabled          { get => true; set { } }
        public bool Visible          => true;
        public bool HasEnabledToggle => false;

        private static readonly Vector4 ValueColor = new(1f, 0.85f, 0.4f, 1f);

        public void DrawSettings()
        {
            Row("CurrentContentFinderConditionId", GameMain.Instance()->CurrentContentFinderConditionId.ToString());
            Row("EventFramework.GetCurrentContentId", EventFramework.GetCurrentContentId().ToString());
            Row("EventFramework.GetCurrentContentType", EventFramework.GetCurrentContentType().ToString());

            var agent = AgentLookingForGroup.Instance();
            Row("AgentLFG.PartyContent.PartyContentId", agent != null ? agent->PartyContent.PartyContentId.ToString() : "n/a");
            Row("AgentLFG.ContentUI.PartyContent.PartyContentId", agent != null ? agent->ContentUI.PartyContent.PartyContentId.ToString() : "n/a");
        }

        private static void Row(string label, string value)
        {
            ImGui.Text(label);
            ImGui.SameLine();
            ImGui.TextColored(ValueColor, value);
        }
    }
}