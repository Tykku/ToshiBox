using Dalamud.Interface.Colors;
using ImGuiNET;
using System;
using Dalamud.Interface.Windowing;
using ECommons.Configuration;
using ToshiBox.Common;
using ToshiBox.Features;

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
            : base("ToshiBox Settings", ImGuiWindowFlags.None)
        {
            _autoRetainerListing = autoRetainerListing;
            _autoChestOpen = autoChestOpen;
            _config = config;
            Flags |= ImGuiWindowFlags.NoScrollbar;
        }

        #region Draw Method

        public override void Draw()
        {
            ImGui.BeginChild("ToshiBox_MainChild", new System.Numerics.Vector2(0, 0), false);
            ImGui.Columns(2, null, true);
            ImGui.SetColumnWidth(0, 250);

            // Header row
            ImGui.TextColored(ImGuiColors.DalamudWhite, "Features");
            ImGui.NextColumn();
            ImGui.TextColored(ImGuiColors.DalamudWhite, "Settings");
            ImGui.NextColumn();

            ImGui.Separator();
            ImGui.NextColumn();
            ImGui.NextColumn();

            DrawFeatureList();
            ImGui.NextColumn();
            DrawSettingsPanel();

            ImGui.Columns(1);
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
        {
            ImGui.Checkbox(checkboxId, ref enabled);
            if (ImGui.IsItemDeactivatedAfterEdit())
            {
                onToggle();
                EzConfig.Save();
            }

            ImGui.SameLine();

            bool selected = _selectedFeature == feature;
            if (ImGui.Selectable(label, selected, ImGuiSelectableFlags.None, new(selectableWidth, 0)))
            {
                _selectedFeature = feature;
            }
        }
        ImGui.EndChild();
        ImGui.PopStyleVar();
        ImGui.PopStyleColor();
        ImGui.Spacing();
    }

    // For AutoRetainerListing, hide entire feature when disabled
    {
        bool enabled = _config.AutoRetainerListingConfig.Enabled;
        if (enabled) // only draw if enabled
        {
            DrawFeature("Auto Retainer Listing", "##AutoRetainerEnabled", ref enabled,
                SelectedFeature.AutoRetainerListing, () =>
                {
                    _config.AutoRetainerListingConfig.Enabled = enabled;
                    _autoRetainerListing.IsEnabled();
                });
        }
    }

    // For other features, always draw with frame regardless of enabled state
    {
        bool enabled = _config.AutoChestOpenConfig.Enabled;
        DrawFeature("Auto Chest Open", "##AutoChestOpenEnabled", ref enabled, SelectedFeature.AutoChestOpen,
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

        #region AutoRetainerListing

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

        #region AutoChestOpen

        private void DrawAutoChestOpenSettings()
        {
            if (!_config.AutoChestOpenConfig.Enabled)
            {
                ImGui.TextColored(ImGuiColors.DalamudGrey, "Enable the feature to adjust settings.");
                return;
            }

            ImGui.PushItemWidth(250f);

            //Distance to open chest
            float distance = _config.AutoChestOpenConfig.Distance;
            if (ImGui.SliderFloat("Distance (yalms)", ref distance, 0f, 3f, "%.1f"))
            {
                distance = (float)Math.Round(distance * 10f) / 10f;
                _config.AutoChestOpenConfig.Distance = distance;
                EzConfig.Save();
            }

            //Delay to open chest
            float delay = _config.AutoChestOpenConfig.Delay;
            if (ImGui.SliderFloat("Delay (seconds)", ref delay, 0f, 2f, "%.1f"))
            {
                delay = (float)Math.Round(delay * 10f) / 10f;
                _config.AutoChestOpenConfig.Delay = delay;
                EzConfig.Save();
            }

            // Checkbox for High End Duty option
            bool openInHighEnd = _config.AutoChestOpenConfig.OpenInHighEndDuty;
            if (ImGui.Checkbox("Open Chests in High End Duties", ref openInHighEnd))
            {
                _config.AutoChestOpenConfig.OpenInHighEndDuty = openInHighEnd;
                EzConfig.Save();
            }

            // Checkbox for closing loot window
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