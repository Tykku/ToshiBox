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
        bool Enabled { get; set; }
        bool Visible { get; }
        bool HasEnabledToggle => true;
        void DrawSettings();
    }

    public class MainWindow
    {
        private readonly IReadOnlyList<IFeatureUI> _features;
        private readonly Dictionary<string, IFeatureUI> _featuresByName;
        private readonly Config _config;

        private string _selectedPage = string.Empty;

        public bool IsOpen;

        private const float SidebarWidth = 200f;

        private static readonly (string Group, FontAwesomeIcon GroupIcon, string[] Pages)[] Groups =
        {
            ("Features", FontAwesomeIcon.Cogs,   new[] { "Auto Retainer Listing", "Auto Chest Open", "Action Tweaks", "Action Timings : WARNING: DO NOT USE WITH NOCLIPPY, BOSSMOD ACTION TWEAKS, OR XIVALEXANDER!" }),
            ("Tools",    FontAwesomeIcon.Wrench,  new[] { "Market Insights" }),
            ("Games",    FontAwesomeIcon.Gamepad, new[] { "Killer Sudoku" }),
        };

        private static readonly Dictionary<string, FontAwesomeIcon> PageIcons = new()
        {
            ["Auto Retainer Listing"] = FontAwesomeIcon.Tag,
            ["Auto Chest Open"]       = FontAwesomeIcon.BoxOpen,
            ["Action Tweaks"]         = FontAwesomeIcon.Gauge,
            ["Action Timings : WARNING: DO NOT USE WITH NOCLIPPY, BOSSMOD ACTION TWEAKS, OR XIVALEXANDER!"] = FontAwesomeIcon.Bolt,
            ["Market Insights"]       = FontAwesomeIcon.ChartLine,
            ["Killer Sudoku"]         = FontAwesomeIcon.Th,
        };

        public MainWindow(IReadOnlyList<IFeatureUI> features, Config config)
        {
            _features = features;
            _config   = config;

            _featuresByName = new Dictionary<string, IFeatureUI>();
            foreach (var f in features)
                _featuresByName[f.Name] = f;
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
                // Ensure selected page is still visible; clear if not
                if (!string.IsNullOrEmpty(_selectedPage) &&
                    _featuresByName.TryGetValue(_selectedPage, out var sel) && !sel.Visible)
                    _selectedPage = string.Empty;

                foreach (var (group, groupIcon, pages) in Groups)
                {
                    if (!_config.SidebarGroupExpanded.ContainsKey(group))
                        _config.SidebarGroupExpanded[group] = true;

                    var expanded = _config.SidebarGroupExpanded[group];
                    if (Theme.SidebarGroupHeader(group, ref expanded, groupIcon))
                    {
                        ImGui.Spacing();
                        foreach (var page in pages)
                        {
                            // Skip if the feature isn't visible (e.g. AutoRetainerListing when disabled)
                            if (_featuresByName.TryGetValue(page, out var feature) && !feature.Visible)
                                continue;

                            var icon = PageIcons.GetValueOrDefault(page, (FontAwesomeIcon)0);
                            var label = feature?.SidebarName ?? page;
                            if (Theme.SidebarItem(label, _selectedPage == page, icon))
                                _selectedPage = page;
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

            // Page header
            Theme.SectionHeader(_selectedPage);
            ImGui.Spacing();
            ImGui.Separator();
            ImGui.Spacing();

            // Enable toggle (above settings) — skipped for features that don't need it
            if (feature.HasEnabledToggle)
            {
                var enabled = feature.Enabled;
                if (Theme.ToggleSwitch("feature_enabled", enabled ? "Enabled" : "Disabled", ref enabled))
                    feature.Enabled = enabled;

                ImGui.Spacing();
                ImGui.Separator();
                ImGui.Spacing();
            }

            // Feature settings
            Theme.PushFrameStyle();
            feature.DrawSettings();
            Theme.PopFrameStyle();
        }
    }
}
