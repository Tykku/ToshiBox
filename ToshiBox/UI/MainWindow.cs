using System.Collections.Generic;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Colors;
using Dalamud.Interface.Windowing;
using ECommons;

namespace ToshiBox.UI
{
    public class MainWindow : Window
    {
        private readonly IReadOnlyList<IFeatureUI> _features;
        private IFeatureUI? _selectedFeature;

        public MainWindow(IReadOnlyList<IFeatureUI> features)
            : base("ToshiBox Settings")
        {
            _features = features;
            this.SetMinSize(new System.Numerics.Vector2(580, 250));
        }

        public override void Draw()
        {
            ImGui.BeginChild("ToshiBox_MainChild", new System.Numerics.Vector2(0, 0), false);

            float leftWidth = 250f;

            ImGui.BeginChild("LeftPanel", new System.Numerics.Vector2(leftWidth, 0), true);
            ImGui.TextColored(ImGuiColors.DalamudWhite, "Features");
            ImGui.Separator();
            DrawFeatureList();
            ImGui.EndChild();

            ImGui.SameLine();

            ImGui.BeginChild("RightPanel", new System.Numerics.Vector2(0, 0), true);
            ImGui.TextColored(ImGuiColors.DalamudWhite, "Settings");
            ImGui.Separator();
            DrawSettingsPanel();
            ImGui.EndChild();

            ImGui.EndChild();
        }

        private void DrawFeatureList()
        {
            float columnWidth = ImGui.GetColumnWidth();
            float checkboxWidth = ImGui.GetFrameHeight();
            float spacing = ImGui.GetStyle().ItemSpacing.X;
            float selectableWidth = columnWidth - checkboxWidth - spacing;
            var darkerBg = new System.Numerics.Vector4(0.15f, 0.15f, 0.15f, 1.0f);

            foreach (var feature in _features)
            {
                ImGui.PushStyleColor(ImGuiCol.ChildBg, darkerBg);
                ImGui.PushStyleVar(ImGuiStyleVar.FramePadding, new System.Numerics.Vector2(4, 4));
                ImGui.BeginChild(feature.Name + "_Group", new System.Numerics.Vector2(columnWidth, 40), true, ImGuiWindowFlags.None);

                bool enabled = feature.Enabled;
                ImGui.Checkbox("##" + feature.Name, ref enabled);
                if (ImGui.IsItemDeactivatedAfterEdit())
                    feature.Enabled = enabled;

                ImGui.SameLine();

                bool selected = _selectedFeature == feature;
                if (ImGui.Selectable(feature.Name, selected, ImGuiSelectableFlags.None, new System.Numerics.Vector2(selectableWidth, 0)))
                    _selectedFeature = feature;

                ImGui.EndChild();
                ImGui.PopStyleVar();
                ImGui.PopStyleColor();
                ImGui.Spacing();
            }
        }

        private void DrawSettingsPanel()
        {
            if (_selectedFeature == null)
            {
                ImGui.TextColored(ImGuiColors.DalamudGrey, "Select a feature on the left to see its settings.");
                return;
            }

            ImGui.Text(_selectedFeature.Name);
            ImGui.Separator();
            _selectedFeature.DrawSettings();
        }
    }
}