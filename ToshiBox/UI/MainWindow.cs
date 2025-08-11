using System;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Colors;
using Dalamud.Interface.Windowing;
using ECommons;
using ECommons.Configuration;
using ECommons.Logging;
using ToshiBox.Common;
using ToshiBox.Features;

// for SetMinSize

namespace ToshiBox.UI
{
    public class MainWindow : Window
    {
        private readonly AutoRetainerListing _autoRetainerListing;
        private readonly AutoChestOpen _autoChestOpen;
        private readonly Config _config;

        private enum SelectedFeature
        {
            None,
            AutoRetainerListing,
            AutoChestOpen,
        }

        private SelectedFeature _selectedFeature = SelectedFeature.None;

        public MainWindow(AutoRetainerListing autoRetainerListing, AutoChestOpen autoChestOpen, Config config)
            : base("ToshiBox Settings")
        {
            _autoRetainerListing = autoRetainerListing;
            _autoChestOpen = autoChestOpen;
            _config = config;

            this.SetMinSize(new System.Numerics.Vector2(580, 250));
        }

        #region Draw Method

        public override void Draw()
        {
            // If Auto Retainer Listing is disabled while selected, clear selection
            if (_selectedFeature == SelectedFeature.AutoRetainerListing && !_config.AutoRetainerListingConfig.Enabled)
                _selectedFeature = SelectedFeature.None;

            // Make the main child dynamically take the available width
            ImGui.BeginChild("ToshiBox_MainChild", new System.Numerics.Vector2(0, 0), false);

            // Left panel fixed width
            float leftWidth = 250f;

            ImGui.BeginChild("LeftPanel", new System.Numerics.Vector2(leftWidth, 0), true);
            ImGui.TextColored(ImGuiColors.DalamudWhite, "Features");
            ImGui.Separator();
            DrawFeatureList();
            ImGui.EndChild();

            ImGui.SameLine();

            // Right panel fills the remaining width automatically
            ImGui.BeginChild("RightPanel", new System.Numerics.Vector2(0, 0), true);
            ImGui.TextColored(ImGuiColors.DalamudWhite, "Settings");
            ImGui.Separator();
            DrawSettingsPanel();
            ImGui.EndChild();

            ImGui.EndChild();
        }

        #endregion

        #region Feature List

        private void DrawFeatureList()
        {
            float columnWidth = ImGui.GetColumnWidth();
            float checkboxWidth = ImGui.GetFrameHeight();
            float spacing = ImGui.GetStyle().ItemSpacing.X;
            float selectableWidth = columnWidth - checkboxWidth - spacing;
            var darkerBg = new System.Numerics.Vector4(0.15f, 0.15f, 0.15f, 1.0f);

            void DrawFeature(string label, string checkboxId, ref bool enabled, SelectedFeature feature, Action onToggle)
            {
                ImGui.PushStyleColor(ImGuiCol.ChildBg, darkerBg);
                ImGui.PushStyleVar(ImGuiStyleVar.FramePadding, new System.Numerics.Vector2(4, 4));
                ImGui.BeginChild(label + "_Group", new System.Numerics.Vector2(columnWidth, 40), true, ImGuiWindowFlags.None);

                ImGui.Checkbox(checkboxId, ref enabled);
                if (ImGui.IsItemDeactivatedAfterEdit())
                {
                    onToggle();
                    EzConfig.Save();
                }

                ImGui.SameLine();

                bool selected = _selectedFeature == feature;
                if (ImGui.Selectable(label, selected, ImGuiSelectableFlags.None, new System.Numerics.Vector2(selectableWidth, 0)))
                {
                    _selectedFeature = feature;
                    PluginLog.Debug($"Selected feature changed to: {_selectedFeature}");
                }

                ImGui.EndChild();
                ImGui.PopStyleVar();
                ImGui.PopStyleColor();
                ImGui.Spacing();
            }

            // Auto Retainer Listing: only draw when enabled; hide when unchecked
            {
                bool enabled = _config.AutoRetainerListingConfig.Enabled;
                if (enabled)
                {
                    DrawFeature("Auto Retainer Listing", "##AutoRetainerEnabled", ref enabled,
                        SelectedFeature.AutoRetainerListing,
                        () =>
                        {
                            _config.AutoRetainerListingConfig.Enabled = enabled;
                            _autoRetainerListing.IsEnabled();
                            if (!enabled && _selectedFeature == SelectedFeature.AutoRetainerListing)
                                _selectedFeature = SelectedFeature.None;
                        });
                }
                else
                {
                    // Ensure selection is cleared if it was selected while hidden
                    if (_selectedFeature == SelectedFeature.AutoRetainerListing)
                        _selectedFeature = SelectedFeature.None;
                }
            }

            // Auto Chest Open: unchanged (still visible even if unchecked)
            {
                bool enabled = _config.AutoChestOpenConfig.Enabled;
                DrawFeature("Auto Chest Open", "##AutoChestOpenEnabled", ref enabled,
                    SelectedFeature.AutoChestOpen,
                    () =>
                    {
                        _config.AutoChestOpenConfig.Enabled = enabled;
                        _autoChestOpen.IsEnabled();
                    });
            }
        }

        #endregion

        #region Settings Panel

        private void DrawSettingsPanel()
        {
            ImGui.Text($"Selected feature: {_selectedFeature}");

            switch (_selectedFeature)
            {
                case SelectedFeature.AutoRetainerListing:
                    DrawAutoRetainerListingSettings();
                    break;
                case SelectedFeature.AutoChestOpen:
                    DrawAutoChestOpenSettings();
                    break;
                default:
                    ImGui.TextColored(ImGuiColors.DalamudGrey, "Select a feature on the left to see its settings.");
                    break;
            }
        }

        #endregion

        #region AutoRetainerListing Settings

        private void DrawAutoRetainerListingSettings()
        {
            if (!_config.AutoRetainerListingConfig.Enabled)
            {
                ImGui.TextColored(ImGuiColors.DalamudGrey, "Enable the feature to adjust settings.");
                return;
            }

            ImGui.PushItemWidth(250f);

            int priceReduction = _config.AutoRetainerListingConfig.PriceReduction;
            if (ImGui.InputInt("Price Reduction", ref priceReduction))
            {
                _config.AutoRetainerListingConfig.PriceReduction = Math.Max(0, priceReduction);
                EzConfig.Save();
            }

            int lowestPrice = _config.AutoRetainerListingConfig.LowestAcceptablePrice;
            if (ImGui.InputInt("Lowest Acceptable Price", ref lowestPrice))
            {
                _config.AutoRetainerListingConfig.LowestAcceptablePrice = Math.Max(0, lowestPrice);
                EzConfig.Save();
            }

            int maxReduction = _config.AutoRetainerListingConfig.MaxPriceReduction;
            if (ImGui.InputInt("Max Price Reduction (0 = no limit)", ref maxReduction))
            {
                _config.AutoRetainerListingConfig.MaxPriceReduction = Math.Max(0, maxReduction);
                EzConfig.Save();
            }

            bool separateNQHQ = _config.AutoRetainerListingConfig.SeparateNQAndHQ;
            if (ImGui.Checkbox("Separate NQ and HQ", ref separateNQHQ))
            {
                _config.AutoRetainerListingConfig.SeparateNQAndHQ = separateNQHQ;
                EzConfig.Save();
            }

            ImGui.PopItemWidth();
        }

        #endregion

        #region AutoChestOpen Settings

        private void DrawAutoChestOpenSettings()
        {
            // Temporarily force enabled = true for testing so settings show:
            bool enabled = true; // _config.AutoChestOpenConfig.Enabled;
            if (!enabled)
            {
                ImGui.TextColored(ImGuiColors.DalamudGrey, "Enable the feature to adjust settings.");
                return;
            }

            ImGui.PushItemWidth(250f);

            float distance = _config.AutoChestOpenConfig.Distance;
            if (ImGui.SliderFloat("Distance (yalms)", ref distance, 0f, 3f, "%.1f"))
            {
                distance = (float)Math.Round(distance * 10f) / 10f;
                _config.AutoChestOpenConfig.Distance = distance;
                EzConfig.Save();
            }

            float delay = _config.AutoChestOpenConfig.Delay;
            if (ImGui.SliderFloat("Delay (seconds)", ref delay, 0f, 2f, "%.1f"))
            {
                delay = (float)Math.Round(delay * 10f) / 10f;
                _config.AutoChestOpenConfig.Delay = delay;
                EzConfig.Save();
            }

            bool openInHighEnd = _config.AutoChestOpenConfig.OpenInHighEndDuty;
            if (ImGui.Checkbox("Open Chests in High End Duties", ref openInHighEnd))
            {
                _config.AutoChestOpenConfig.OpenInHighEndDuty = openInHighEnd;
                EzConfig.Save();
            }

            bool closeLootWindow = _config.AutoChestOpenConfig.CloseLootWindow;
            if (ImGui.Checkbox("Close Loot Window After Opening", ref closeLootWindow))
            {
                _config.AutoChestOpenConfig.CloseLootWindow = closeLootWindow;
                EzConfig.Save();
            }

            ImGui.PopItemWidth();
        }

        #endregion
    }
}