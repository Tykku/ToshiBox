using System.Collections.Generic;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using ECommons.Configuration;
using ToshiBox.Common;

namespace ToshiBox.UI
{
    public interface IFeatureUI
    {
        string Name { get; }
        string SidebarName => Name;
        string Group { get; }
        FontAwesomeIcon Icon { get; }
        bool Enabled { get; set; }
        bool Visible { get; }
        bool HasEnabledToggle => true;
        void DrawSettings();
    }

    public class MainWindow
    {
        private readonly IReadOnlyList<IFeatureUI> _features;
        private readonly Dictionary<string, IFeatureUI> _featuresByName;
        private readonly List<string> _groups;
        private readonly Dictionary<string, List<IFeatureUI>> _featuresByGroup;
        private readonly Config _config;

        private string _selectedPage = string.Empty;

        public bool IsOpen;

        private const float SidebarWidth = 200f;

        private static readonly Dictionary<string, FontAwesomeIcon> GroupIcons = new()
        {
            ["Features"] = FontAwesomeIcon.Cogs,
            ["Tools"]    = FontAwesomeIcon.Wrench,
            ["Games"]    = FontAwesomeIcon.Gamepad,
            ["Debug"]    = FontAwesomeIcon.Bug,
        };

        public MainWindow(IReadOnlyList<IFeatureUI> features, Config config)
        {
            _features = features;
            _config   = config;

            _featuresByName  = new Dictionary<string, IFeatureUI>();
            _groups          = new List<string>();
            _featuresByGroup = new Dictionary<string, List<IFeatureUI>>();

            foreach (var f in features)
            {
                _featuresByName[f.Name] = f;
                if (!_featuresByGroup.ContainsKey(f.Group))
                {
                    _groups.Add(f.Group);
                    _featuresByGroup[f.Group] = new List<IFeatureUI>();
                }
                _featuresByGroup[f.Group].Add(f);
            }
        }

        public void Draw()
        {
            if (!IsOpen) return;

            ImGui.SetNextWindowSize(new Vector2(780, 480), ImGuiCond.FirstUseEver);
            ImGui.SetNextWindowSizeConstraints(new Vector2(580, 300), new Vector2(float.MaxValue, float.MaxValue));

            ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, new Vector2(12, 12));
            if (!ImGui.Begin("ToshiBox###ToshiBoxMain", ref IsOpen))
            {
                ImGui.PopStyleVar();
                ImGui.End();
                return;
            }
            ImGui.PopStyleVar();

            ImGui.PushStyleVar(ImGuiStyleVar.ChildRounding, Theme.RoundingLarge);

            DrawSidebar();
            ImGui.SameLine(0, 5);

            if (ImGui.BeginChild("TBBody", ImGui.GetContentRegionAvail(), true))
                DrawContent();
            ImGui.EndChild();

            ImGui.PopStyleVar();

            ImGui.End();
        }

        private void DrawSidebar()
        {
            ImGui.PushStyleColor(ImGuiCol.ChildBg, Theme.SidebarBg);
            ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, new Vector2(6, 8));

            if (ImGui.BeginChild("TBSidebar", new Vector2(SidebarWidth, -1), true))
            {
                if (!string.IsNullOrEmpty(_selectedPage) &&
                    _featuresByName.TryGetValue(_selectedPage, out var sel) && !sel.Visible)
                    _selectedPage = string.Empty;

                foreach (var group in _groups)
                {
                    if (!_config.SidebarGroupExpanded.ContainsKey(group))
                        _config.SidebarGroupExpanded[group] = true;

                    var expanded  = _config.SidebarGroupExpanded[group];
                    var groupIcon = GroupIcons.GetValueOrDefault(group, (FontAwesomeIcon)0);

                    if (Theme.SidebarGroupHeader(group, ref expanded, groupIcon))
                    {
                        ImGui.Spacing();
                        foreach (var feature in _featuresByGroup[group])
                        {
                            if (!feature.Visible) continue;
                            if (Theme.SidebarItem(feature.SidebarName, _selectedPage == feature.Name, feature.Icon))
                                _selectedPage = feature.Name;
                        }
                        ImGui.Spacing();
                    }

                    if (expanded != _config.SidebarGroupExpanded[group])
                    {
                        _config.SidebarGroupExpanded[group] = expanded;
                        EzConfig.Save();
                    }
                }
            }
            ImGui.EndChild();

            ImGui.PopStyleVar();
            ImGui.PopStyleColor();
        }

        private void DrawContent()
        {
            if (string.IsNullOrEmpty(_selectedPage))
            {
                var avail = ImGui.GetContentRegionAvail();
                var text  = "Select a feature from the sidebar.";
                var ts    = ImGui.CalcTextSize(text);
                ImGui.SetCursorPos(new Vector2((avail.X - ts.X) / 2, (avail.Y - ts.Y) / 2));
                ImGui.TextColored(Theme.TextMuted, text);
                return;
            }

            if (!_featuresByName.TryGetValue(_selectedPage, out var feature))
                return;

            Theme.SectionHeader(_selectedPage);
            ImGui.Spacing();
            ImGui.Separator();
            ImGui.Spacing();

            if (feature.HasEnabledToggle)
            {
                var enabled = feature.Enabled;
                if (Theme.ToggleSwitch("feature_enabled", enabled ? "Enabled" : "Disabled", ref enabled))
                    feature.Enabled = enabled;

                ImGui.Spacing();
                ImGui.Separator();
                ImGui.Spacing();
            }

            Theme.PushFrameStyle();
            feature.DrawSettings();
            Theme.PopFrameStyle();
        }
    }
}
