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

        private void DrawFeatureList()
        {
            float columnWidth = ImGui.GetColumnWidth();
            float checkboxWidth = ImGui.GetFrameHeight();
            float spacing = ImGui.GetStyle().ItemSpacing.X;
            float selectableWidth = columnWidth - checkboxWidth - spacing;

            // Auto Retainer Listing (only show if enabled)
            if (_config.AutoRetainerListingConfig.Enabled)
            {
                bool enabled = _config.AutoRetainerListingConfig.Enabled;
                if (ImGui.Checkbox("##AutoRetainerEnabled", ref enabled))
                {
                    _config.AutoRetainerListingConfig.Enabled = enabled;
                    _autoRetainerListing.IsEnabled();
                    EzConfig.Save();
                }

                ImGui.SameLine();

                bool selected = _selectedFeature == SelectedFeature.AutoRetainerListing;
                if (ImGui.Selectable("Auto Retainer Listing", selected, ImGuiSelectableFlags.None, new(selectableWidth, 0)))
                {
                    _selectedFeature = SelectedFeature.AutoRetainerListing;
                }
            }

            // Auto Chest Open
            {
                bool enabled = _config.AutoChestOpenConfig.Enabled;
                if (ImGui.Checkbox("##AutoChestOpenEnabled", ref enabled))
                {
                    _config.AutoChestOpenConfig.Enabled = enabled;
                    _autoChestOpen.IsEnabled();
                    EzConfig.Save();
                }

                ImGui.SameLine();

                bool selected = _selectedFeature == SelectedFeature.AutoChestOpen;
                if (ImGui.Selectable("Auto Chest Open", selected, ImGuiSelectableFlags.None, new(selectableWidth, 0)))
                {
                    _selectedFeature = SelectedFeature.AutoChestOpen;
                }
            }
        }




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

    }
}
