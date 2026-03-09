using System;
using System.Collections.Generic;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using ECommons.Configuration;
using ToshiBox.Common;
using ToshiBox.Insights;
using ToshiBox.UI;

namespace ToshiBox.UI.Features
{
    public class MarketInsightsUI : IFeatureUI
    {
        private readonly InsightsEngine _engine;
        private readonly BestDealsEngine _bestDealsEngine;
        private readonly Config _config;

        // UI state
        private int _selectedDcIndex;
        private int _selectedCategoryIndex;
        private int _expandedItemIndex = -1;
        private int _bestDealsExpandedIndex = -1;

        // Persistent working lists — rebuilt only when the source snapshot changes.
        // Sorting mutates these in-place; the originals in the snapshot are never touched.
        private readonly List<MarketItemData> _hotItemsWorking = new();
        private readonly List<MarketItemData> _catItemsWorking = new();
        private readonly List<BestDealItem>   _dealsWorking    = new();

        private InsightsSnapshot?  _lastHotSnap;
        private InsightsSnapshot?  _lastCatSnap;
        private BestDealsSnapshot? _lastDealsSnap;
        private string               _lastCatName = string.Empty;

        // Best Deals parameter state (mirrors engine properties, kept in sync on change)
        private string _bdHomeServer  = string.Empty;
        private int    _bdDiscount;
        private int    _bdMinMedian;
        private int    _bdMaxBuyPrice;
        private int    _bdMinSales;

        private static readonly string[] DcNames = { "Aether", "Primal", "Crystal", "Dynamis" };
        private static readonly Vector2 IconSm  = new(24, 24);
        private static readonly Vector2 IconRow = new(32, 32);

        public MarketInsightsUI(InsightsEngine engine, BestDealsEngine bestDealsEngine, Config config)
        {
            _engine          = engine;
            _bestDealsEngine = bestDealsEngine;
            _config          = config;

            // Restore saved DC selection — default to Aether (index 0) if not found
            _selectedDcIndex = Array.IndexOf(DcNames, config.MarketInsightsConfig.DataCenter);
            if (_selectedDcIndex < 0) _selectedDcIndex = 0;

            // Ensure engine is pointed at the right DC
            engine.SelectedDataCenter = DcNames[_selectedDcIndex];

            // Restore Best Deals params from config
            var cfg       = config.MarketInsightsConfig;
            _bdHomeServer  = cfg.BestDealsHomeServer;
            _bdDiscount    = cfg.BestDealsDiscount;
            _bdMinMedian   = cfg.BestDealsMinMedian;
            _bdMaxBuyPrice = cfg.BestDealsMaxBuyPrice;
            _bdMinSales    = cfg.BestDealsMinSales;
        }

        public string Name            => "Market Insights";
        public bool   Visible         => true;
        public bool   HasEnabledToggle => false;
        public bool   Enabled          { get => true; set { } }

        public void DrawSettings()
        {
            DrawControlBar();
            ImGui.Spacing();
            ImGui.Separator();
            ImGui.Spacing();

            var snapshot = _engine.CurrentSnapshot;

            if (snapshot.IsLoading && snapshot.FetchedAt == DateTime.MinValue)
            {
                ImGui.Spacing();
                ImGui.TextColored(Theme.Accent, "Loading marketplace data...");
                ImGui.Spacing();
                ImGui.TextColored(Theme.TextMuted, "This may take a moment.");
                return;
            }

            if (snapshot.ErrorMessage != null && snapshot.HottestItems.Count == 0)
            {
                ImGui.Spacing();
                ImGui.TextColored(Theme.Error, snapshot.ErrorMessage);
                ImGui.Spacing();
                if (Theme.PrimaryButton("Retry", new Vector2(100, 30)))
                    _engine.TriggerRefresh();
                return;
            }

            DrawSubTabs(snapshot);
        }

        // ──────────────────────────────────────────────
        // Control Bar
        // ──────────────────────────────────────────────

        private void DrawControlBar()
        {
            ImGui.TextColored(Theme.TextSecondary, "Data Center:");
            ImGui.SameLine(0, Theme.PadSmall);
            ImGui.SetNextItemWidth(120f);
            if (ImGui.Combo("##DcSelect", ref _selectedDcIndex, DcNames, DcNames.Length))
            {
                var dc = DcNames[_selectedDcIndex];
                _engine.SelectedDataCenter = dc;
                _config.MarketInsightsConfig.DataCenter = dc;
                EzConfig.Save();
            }

            ImGui.SameLine(0, Theme.PadLarge);

            var snapshot = _engine.CurrentSnapshot;
            if (snapshot.FetchedAt > DateTime.MinValue)
            {
                var ago = DateTime.UtcNow - snapshot.FetchedAt;
                string agoText;
                if (ago.TotalSeconds < 60)        agoText = "just now";
                else if (ago.TotalMinutes < 60)   agoText = string.Concat(((int)ago.TotalMinutes).ToString(), "m ago");
                else                              agoText = string.Concat(((int)ago.TotalHours).ToString(), "h ago");

                ImGui.TextColored(Theme.TextMuted, "Updated:");
                ImGui.SameLine(0, Theme.PadSmall);
                ImGui.TextColored(Theme.TextSecondary, agoText);
                ImGui.SameLine(0, Theme.PadLarge);
            }

            if (_engine.IsRefreshing) ImGui.BeginDisabled();
            if (Theme.SecondaryButton("Refresh", new Vector2(80, 0)))
            {
                if (!string.IsNullOrWhiteSpace(_engine.SelectedDataCenter))
                    _engine.TriggerRefresh();
            }
            if (_engine.IsRefreshing) ImGui.EndDisabled();

            if (_engine.IsRefreshing)
            {
                ImGui.SameLine(0, Theme.Pad);
                ImGui.TextColored(Theme.Accent, _engine.StatusMessage);
            }
            else if (snapshot.ErrorMessage != null && snapshot.HottestItems.Count > 0)
            {
                ImGui.SameLine(0, Theme.Pad);
                ImGui.TextColored(Theme.Warning, snapshot.ErrorMessage);
            }
        }

        // ──────────────────────────────────────────────
        // Sub-tabs
        // ──────────────────────────────────────────────

        private void DrawSubTabs(InsightsSnapshot snapshot)
        {
            if (!ImGui.BeginTabBar("InsightsSubTabs"))
                return;

            if (ImGui.BeginTabItem("Overview"))
            {
                DrawOverview(snapshot);
                ImGui.EndTabItem();
            }

            if (ImGui.BeginTabItem("Hot Items"))
            {
                DrawHotItems(snapshot);
                ImGui.EndTabItem();
            }

            if (ImGui.BeginTabItem("Categories"))
            {
                DrawCategories(snapshot);
                ImGui.EndTabItem();
            }

            if (ImGui.BeginTabItem("Best Deals"))
            {
                DrawBestDeals();
                ImGui.EndTabItem();
            }

            ImGui.EndTabBar();
        }

        // ──────────────────────────────────────────────
        // Overview
        // ──────────────────────────────────────────────

        private void DrawOverview(InsightsSnapshot snapshot)
        {
            if (snapshot.CategorySummaries.Count == 0)
            {
                ImGui.TextColored(Theme.TextMuted, "No data yet — enter a DC name and click Refresh.");
                return;
            }

            ImGui.BeginChild("OverviewScroll", Vector2.Zero, false);

            ImGui.Spacing();
            DrawOverviewHeader(snapshot);
            ImGui.Spacing();
            ImGui.Spacing();

            if (snapshot.HottestItems.Count > 0)
            {
                Theme.SectionHeader("Top Movers", Theme.Accent);
                ImGui.Spacing();
                DrawTopMoversRow(snapshot);
                ImGui.Spacing();
                ImGui.Spacing();
            }

            Theme.SectionHeader("Category Breakdown", Theme.Gold);
            ImGui.Spacing();

            for (var i = 0; i < snapshot.CategorySummaries.Count; i++)
            {
                ImGui.PushID(i);
                DrawCategoryCard(snapshot.CategorySummaries[i]);
                ImGui.PopID();
                ImGui.Spacing();
            }

            ImGui.EndChild();
        }

        private static void DrawOverviewHeader(InsightsSnapshot snapshot)
        {
            var drawList = ImGui.GetWindowDrawList();
            var pos      = ImGui.GetCursorScreenPos();
            var avail    = ImGui.GetContentRegionAvail().X;
            const float headerH = 52f;

            drawList.AddRectFilled(
                pos, new Vector2(pos.X + avail, pos.Y + headerH),
                ImGui.ColorConvertFloat4ToU32(new Vector4(0.10f, 0.12f, 0.18f, 1.00f)), 6f);

            drawList.AddRectFilled(
                pos, new Vector2(pos.X + avail, pos.Y + 2),
                ImGui.ColorConvertFloat4ToU32(Theme.Gold), 6f);

            var title = string.Concat("Marketplace Report  —  ", snapshot.DataCenterName);
            drawList.AddText(new Vector2(pos.X + Theme.PadLarge, pos.Y + 8f),
                ImGui.ColorConvertFloat4ToU32(Theme.Gold), title);

            var totalVelocity = 0f;
            var totalGilVol   = 0f;
            var totalItems    = 0;
            for (var i = 0; i < snapshot.CategorySummaries.Count; i++)
            {
                totalVelocity += snapshot.CategorySummaries[i].TotalDailyVelocity;
                totalGilVol   += snapshot.CategorySummaries[i].EstimatedDailyGilVolume;
                totalItems    += snapshot.CategorySummaries[i].ItemCount;
            }

            var statsText = string.Concat(
                totalItems.ToString(), " items tracked  |  ~",
                FormatNumber(totalVelocity), " units/day  |  ~",
                FormatGil(totalGilVol), " gil/day estimated volume");
            drawList.AddText(new Vector2(pos.X + Theme.PadLarge, pos.Y + 28f),
                ImGui.ColorConvertFloat4ToU32(Theme.TextSecondary), statsText);

            ImGui.Dummy(new Vector2(avail, headerH));
        }

        private static void DrawTopMoversRow(InsightsSnapshot snapshot)
        {
            var count  = Math.Min(5, snapshot.HottestItems.Count);
            var avail  = ImGui.GetContentRegionAvail().X;
            var lineH  = ImGui.GetTextLineHeight();

            ImGui.TextColored(Theme.TextMuted, "#");
            ImGui.SameLine(40);
            ImGui.TextColored(Theme.TextMuted, "Item");
            ImGui.SameLine(avail * 0.38f);
            ImGui.TextColored(Theme.TextMuted, "Sale Velocity");
            ImGui.SameLine(avail * 0.58f);
            ImGui.TextColored(Theme.TextMuted, "Gil Volume");
            ImGui.SameLine(avail * 0.78f);
            ImGui.TextColored(Theme.TextMuted, "Avg Price");
            ImGui.Separator();

            for (var i = 0; i < count; i++)
            {
                var item = snapshot.HottestItems[i];
                ImGui.PushID(1000 + i);

                ImGui.TextColored(Theme.TextMuted, (i + 1).ToString());
                ImGui.SameLine(40);

                Theme.DrawGameIcon(item.IconId, IconRow);
                ImGui.SameLine(0, Theme.PadSmall);

                var cursorY = ImGui.GetCursorPosY();
                ImGui.SetCursorPosY(cursorY + (IconRow.Y - lineH) / 2);
                ImGui.TextColored(Theme.TextPrimary,
                    string.IsNullOrEmpty(item.ItemName) ? string.Concat("Item #", item.ItemId.ToString()) : item.ItemName);

                ImGui.SameLine(avail * 0.38f);
                ImGui.SetCursorPosY(cursorY + (IconRow.Y - lineH) / 2);
                DrawBoxedValue(string.Concat(FormatNumber(item.RegularSaleVelocity), "/day"), Theme.Accent);

                ImGui.SameLine(avail * 0.58f);
                ImGui.SetCursorPosY(cursorY + (IconRow.Y - lineH) / 2);
                DrawBoxedValue(string.Concat(FormatGil(item.EstimatedDailyGilVolume), " gil/day"), Theme.Success);

                ImGui.SameLine(avail * 0.78f);
                ImGui.SetCursorPosY(cursorY + (IconRow.Y - lineH) / 2);
                DrawBoxedValue(FormatGil(item.CurrentAveragePrice), Theme.Gold);

                ImGui.SetCursorPosY(cursorY + IconRow.Y + 4);
                ImGui.PopID();
            }
            ImGui.Spacing();
        }

        private static void DrawCategoryCard(CategorySummary cat)
        {
            var drawList = ImGui.GetWindowDrawList();
            var pos      = ImGui.GetCursorScreenPos();
            var avail    = ImGui.GetContentRegionAvail().X;
            const float cardH = 76f;

            drawList.AddRectFilled(pos, new Vector2(pos.X + avail, pos.Y + cardH),
                ImGui.ColorConvertFloat4ToU32(Theme.CardBg), 4f);
            drawList.AddRectFilled(pos, new Vector2(pos.X + 3, pos.Y + cardH),
                ImGui.ColorConvertFloat4ToU32(Theme.Gold), 4f);

            var leftX = pos.X + Theme.PadLarge;
            var topY  = pos.Y + Theme.Pad;
            var subY  = topY + ImGui.GetTextLineHeightWithSpacing();

            drawList.AddText(new Vector2(leftX, topY),
                ImGui.ColorConvertFloat4ToU32(Theme.Gold), cat.CategoryName);
            drawList.AddText(new Vector2(leftX, subY),
                ImGui.ColorConvertFloat4ToU32(Theme.TextMuted),
                string.Concat(cat.ItemCount.ToString(), " items tracked"));

            var midX = pos.X + avail * 0.30f;
            drawList.AddText(new Vector2(midX, topY),
                ImGui.ColorConvertFloat4ToU32(Theme.TextSecondary), "Daily Volume");
            DrawBoxedValueAt(drawList, new Vector2(midX, subY),
                string.Concat("~", FormatNumber(cat.TotalDailyVelocity), " units"), Theme.Accent);

            var gilX = pos.X + avail * 0.52f;
            drawList.AddText(new Vector2(gilX, topY),
                ImGui.ColorConvertFloat4ToU32(Theme.TextSecondary), "Gil Volume");
            DrawBoxedValueAt(drawList, new Vector2(gilX, subY),
                string.Concat("~", FormatGil(cat.EstimatedDailyGilVolume)), Theme.Success);

            if (cat.TopItem != null)
            {
                var rightLabelX = pos.X + avail * 0.72f;
                drawList.AddText(new Vector2(rightLabelX, topY),
                    ImGui.ColorConvertFloat4ToU32(Theme.TextSecondary), "Top Seller");

                ImGui.SetCursorScreenPos(new Vector2(rightLabelX, subY - 2f));
                Theme.DrawGameIcon(cat.TopItem.IconId, IconSm);

                var topName = cat.TopItem.ItemName;
                if (topName.Length > 18) topName = string.Concat(topName.AsSpan(0, 16), "..");
                drawList.AddText(
                    new Vector2(rightLabelX + IconSm.X + Theme.PadSmall, subY - 2f + (IconSm.Y - ImGui.GetTextLineHeight()) / 2),
                    ImGui.ColorConvertFloat4ToU32(Theme.TextPrimary), topName);
            }

            ImGui.SetCursorScreenPos(new Vector2(pos.X, pos.Y + cardH));
        }

        // ──────────────────────────────────────────────
        // Hot Items
        // ──────────────────────────────────────────────

        private void DrawHotItems(InsightsSnapshot snapshot)
        {
            ImGui.Spacing();

            if (snapshot.HottestItems.Count == 0)
            {
                ImGui.TextColored(Theme.TextMuted, "No data — enter a DC name and click Refresh.");
                return;
            }

            if (!ReferenceEquals(snapshot, _lastHotSnap))
            {
                _hotItemsWorking.Clear();
                _hotItemsWorking.AddRange(snapshot.HottestItems);
                _lastHotSnap = snapshot;
                _expandedItemIndex = -1;
            }

            DrawItemTable(_hotItemsWorking, showCategory: true, "HotItemsTable", ref _expandedItemIndex);
        }

        // ──────────────────────────────────────────────
        // Categories
        // ──────────────────────────────────────────────

        private void DrawCategories(InsightsSnapshot snapshot)
        {
            ImGui.Spacing();

            var summaries = snapshot.CategorySummaries;
            if (summaries.Count == 0)
            {
                ImGui.TextColored(Theme.TextMuted, "No data — enter a DC name and click Refresh.");
                return;
            }

            var categoryNames = new string[summaries.Count];
            for (var i = 0; i < summaries.Count; i++)
                categoryNames[i] = summaries[i].CategoryName;

            if (_selectedCategoryIndex >= categoryNames.Length)
                _selectedCategoryIndex = 0;

            ImGui.TextColored(Theme.TextSecondary, "Category:");
            ImGui.SameLine(0, Theme.PadSmall);
            ImGui.SetNextItemWidth(200);
            ImGui.Combo("##CatSelect", ref _selectedCategoryIndex, categoryNames, categoryNames.Length);
            ImGui.Spacing();

            var catName = categoryNames[_selectedCategoryIndex];
            var catSummary = summaries[_selectedCategoryIndex];

            ImGui.TextColored(Theme.TextMuted, string.Concat(
                catSummary.ItemCount.ToString(), " items  |  ~",
                FormatNumber(catSummary.TotalDailyVelocity), " units/day  |  ~",
                FormatGil(catSummary.EstimatedDailyGilVolume), " gil/day"));
            ImGui.Spacing();

            if (!ReferenceEquals(snapshot, _lastCatSnap) || catName != _lastCatName)
            {
                _catItemsWorking.Clear();
                if (snapshot.ItemsByCategory.TryGetValue(catName, out var catItems))
                    _catItemsWorking.AddRange(catItems);
                _lastCatSnap = snapshot;
                _lastCatName = catName;
                _expandedItemIndex = -1;
            }

            if (_catItemsWorking.Count > 0)
                DrawItemTable(_catItemsWorking, showCategory: false, "CategoriesTable", ref _expandedItemIndex);
            else
                ImGui.TextColored(Theme.TextMuted, "No items in this category.");
        }

        // ──────────────────────────────────────────────
        // Shared Item Table
        // ──────────────────────────────────────────────

        private static void DrawItemTable(List<MarketItemData> items, bool showCategory, string tableId, ref int expandedIndex)
        {
            const ImGuiTableFlags tableFlags =
                ImGuiTableFlags.Sortable      |
                ImGuiTableFlags.SizingFixedFit |
                ImGuiTableFlags.BordersInnerV  |
                ImGuiTableFlags.ScrollY;

            var colCount = showCategory ? 6 : 5;

            ImGui.BeginChild(string.Concat(tableId, "Scroll"), Vector2.Zero, false);

            if (!ImGui.BeginTable(tableId, colCount, tableFlags))
            {
                ImGui.EndChild();
                return;
            }

            ImGui.TableSetupScrollFreeze(0, 1);
            ImGui.TableSetupColumn("#",             ImGuiTableColumnFlags.WidthFixed | ImGuiTableColumnFlags.NoSort, 36f);
            ImGui.TableSetupColumn("Item",          ImGuiTableColumnFlags.WidthStretch, 1f);
            if (showCategory)
                ImGui.TableSetupColumn("Category",  ImGuiTableColumnFlags.WidthFixed, 0f);
            ImGui.TableSetupColumn("Velocity",
                ImGuiTableColumnFlags.WidthFixed | ImGuiTableColumnFlags.DefaultSort | ImGuiTableColumnFlags.PreferSortDescending, 0f, 1u);
            ImGui.TableSetupColumn("Avg Price",
                ImGuiTableColumnFlags.WidthFixed | ImGuiTableColumnFlags.PreferSortDescending, 0f, 2u);
            ImGui.TableSetupColumn("Daily Gil Vol",
                ImGuiTableColumnFlags.WidthFixed | ImGuiTableColumnFlags.PreferSortDescending, 0f, 3u);
            ImGui.TableHeadersRow();

            var specs = ImGui.TableGetSortSpecs();
            if (specs.SpecsDirty)
            {
                var uid  = (int)specs.Specs.ColumnUserID;
                var desc = specs.Specs.SortDirection == ImGuiSortDirection.Descending;
                items.Sort((a, b) =>
                {
                    var va = uid == 2 ? a.CurrentAveragePrice
                           : uid == 3 ? a.EstimatedDailyGilVolume
                           : a.RegularSaleVelocity;
                    var vb = uid == 2 ? b.CurrentAveragePrice
                           : uid == 3 ? b.EstimatedDailyGilVolume
                           : b.RegularSaleVelocity;
                    return desc ? vb.CompareTo(va) : va.CompareTo(vb);
                });
                specs.SpecsDirty = false;
            }

            var lineH  = ImGui.GetTextLineHeight();
            var velCol = showCategory ? 3 : 2;

            for (var i = 0; i < items.Count; i++)
            {
                var item       = items[i];
                var isExpanded = expandedIndex == i;

                ImGui.PushID(i);
                ImGui.TableNextRow(ImGuiTableRowFlags.None, IconRow.Y + 4);

                // # ─────────────────────────────────
                ImGui.TableSetColumnIndex(0);
                var rowTopY = ImGui.GetCursorPosY();
                var centerY = rowTopY + (IconRow.Y - lineH) * 0.5f;
                ImGui.SetCursorPosX(ImGui.GetCursorPosX() + Theme.PadSmall);
                ImGui.SetCursorPosY(centerY);
                ImGui.TextColored(Theme.TextMuted, (i + 1).ToString());

                // Item ─────────────────────────────
                ImGui.TableSetColumnIndex(1);
                var col1Pos = ImGui.GetCursorPos();

                var nameText = string.IsNullOrEmpty(item.ItemName)
                    ? string.Concat("Item #", item.ItemId.ToString())
                    : item.ItemName;

                // Selectable at natural row top — click detection without inflating row height
                if (ImGui.Selectable(string.Concat("##item", i.ToString()),
                    isExpanded, ImGuiSelectableFlags.SpanAllColumns, new Vector2(0, IconRow.Y)))
                {
                    expandedIndex = isExpanded ? -1 : i;
                }

                // Draw icon + text on top of the selectable
                ImGui.SetCursorPos(col1Pos);
                Theme.DrawGameIcon(item.IconId, IconRow);
                ImGui.SameLine(0, Theme.PadSmall);
                ImGui.SetCursorPosY(centerY);
                ImGui.TextColored(Theme.TextPrimary, nameText);

                // Category ─────────────────────────
                if (showCategory)
                {
                    ImGui.TableSetColumnIndex(2);
                    ImGui.SetCursorPosY(centerY);
                    ImGui.TextColored(Theme.TextSecondary, TruncateCategory(item.CategoryName ?? string.Empty));
                }

                // Velocity ─────────────────────────
                ImGui.TableSetColumnIndex(velCol);
                ImGui.SetCursorPosY(centerY);
                DrawBoxedValue(string.Concat(FormatNumber(item.RegularSaleVelocity), "/day"), Theme.Accent);

                // Avg Price ────────────────────────
                ImGui.TableSetColumnIndex(velCol + 1);
                ImGui.SetCursorPosY(centerY);
                DrawBoxedValue(FormatGil(item.CurrentAveragePrice), Theme.Gold);

                // Daily Gil Vol ────────────────────
                ImGui.TableSetColumnIndex(velCol + 2);
                ImGui.SetCursorPosY(centerY);
                DrawBoxedValue(FormatGil(item.EstimatedDailyGilVolume), Theme.Success);

                // Expanded detail row ──────────────
                if (isExpanded)
                {
                    ImGui.TableNextRow();
                    ImGui.TableSetColumnIndex(1);
                    ImGui.Indent(Theme.PadSmall);
                    ImGui.Spacing();
                    ImGui.TextColored(Theme.TextMuted, "Details:");
                    ImGui.Spacing();

                    Theme.KeyValue("Min Price:",         FormatGil(item.MinPrice),                            Theme.Success);
                    Theme.KeyValue("Max Price:",         FormatGil(item.MaxPrice),                            Theme.Error);
                    Theme.KeyValue("Listings:",          item.ListingsCount.ToString(),                       Theme.TextPrimary);
                    Theme.KeyValue("Units For Sale:",    FormatNumber(item.UnitsForSale),                     Theme.TextPrimary);
                    Theme.KeyValue("Units Sold:",        FormatNumber(item.UnitsSold),                        Theme.Accent);
                    if (item.HqSaleVelocity > 0)
                        Theme.KeyValue("HQ Velocity:",  string.Concat(FormatNumber(item.HqSaleVelocity), "/day"), Theme.Accent);
                    if (item.NqSaleVelocity > 0)
                        Theme.KeyValue("NQ Velocity:",  string.Concat(FormatNumber(item.NqSaleVelocity), "/day"), Theme.TextSecondary);

                    ImGui.Spacing();
                    ImGui.Unindent(Theme.PadSmall);
                }

                ImGui.PopID();
            }

            ImGui.EndTable();
            ImGui.EndChild();
        }

        // ──────────────────────────────────────────────
        // Best Deals (Saddlebag Exchange)
        // ──────────────────────────────────────────────

        private void DrawBestDeals()
        {
            var snap = _bestDealsEngine.CurrentSnapshot;

            ImGui.Spacing();

            // ── Parameters ───────────────────────────
            Theme.SectionHeader("Search Parameters", Theme.Gold);
            ImGui.Spacing();

            Theme.PushFrameStyle();

            ImGui.TextColored(Theme.TextSecondary, "Home Server:");
            Theme.HelpMarker("Your character's home world — deals are priced against its median.");
            ImGui.SameLine(0, Theme.PadSmall);
            ImGui.SetNextItemWidth(160);
            var homeServerBuf = _bdHomeServer;
            if (ImGui.InputText("##BDServer", ref homeServerBuf, 64))
            {
                _bdHomeServer = homeServerBuf;
                _config.MarketInsightsConfig.BestDealsHomeServer = homeServerBuf;
                EzConfig.Save();
            }

            ImGui.SameLine(0, Theme.PadLarge);
            ImGui.TextColored(Theme.TextSecondary, "Min Discount:");
            Theme.HelpMarker("Items must be listed this % cheaper than your server's median. 70 = 30% off.");
            ImGui.SameLine(0, Theme.PadSmall);
            ImGui.SetNextItemWidth(70);
            if (ImGui.InputInt("##BDDiscount", ref _bdDiscount, 0))
            {
                _bdDiscount = Math.Clamp(_bdDiscount, 1, 99);
                _config.MarketInsightsConfig.BestDealsDiscount = _bdDiscount;
                EzConfig.Save();
            }
            ImGui.SameLine(0, 2);
            ImGui.TextColored(Theme.TextMuted, "%");

            ImGui.Spacing();

            ImGui.TextColored(Theme.TextSecondary, "Min Median:");
            Theme.HelpMarker("Ignore items whose home-server median is below this price.");
            ImGui.SameLine(0, Theme.PadSmall);
            ImGui.SetNextItemWidth(110);
            if (ImGui.InputInt("##BDMedian", ref _bdMinMedian, 0))
            {
                _bdMinMedian = Math.Max(0, _bdMinMedian);
                _config.MarketInsightsConfig.BestDealsMinMedian = _bdMinMedian;
                EzConfig.Save();
            }

            ImGui.SameLine(0, Theme.PadLarge);
            ImGui.TextColored(Theme.TextSecondary, "Max Buy Price:");
            Theme.HelpMarker("Skip items whose listing price exceeds this amount.");
            ImGui.SameLine(0, Theme.PadSmall);
            ImGui.SetNextItemWidth(110);
            if (ImGui.InputInt("##BDMaxBuy", ref _bdMaxBuyPrice, 0))
            {
                _bdMaxBuyPrice = Math.Max(1, _bdMaxBuyPrice);
                _config.MarketInsightsConfig.BestDealsMaxBuyPrice = _bdMaxBuyPrice;
                EzConfig.Save();
            }

            ImGui.SameLine(0, Theme.PadLarge);
            ImGui.TextColored(Theme.TextSecondary, "Min Sales (7d):");
            Theme.HelpMarker("Minimum number of sales in the last 7 days on your home server.");
            ImGui.SameLine(0, Theme.PadSmall);
            ImGui.SetNextItemWidth(70);
            if (ImGui.InputInt("##BDSales", ref _bdMinSales, 0))
            {
                _bdMinSales = Math.Max(1, _bdMinSales);
                _config.MarketInsightsConfig.BestDealsMinSales = _bdMinSales;
                EzConfig.Save();
            }

            Theme.PopFrameStyle();

            ImGui.Spacing();

            // ── Action bar ───────────────────────────
            var noServer = string.IsNullOrWhiteSpace(_bdHomeServer);
            if (noServer) ImGui.BeginDisabled();
            if (_bestDealsEngine.IsRefreshing) ImGui.BeginDisabled();

            if (Theme.PrimaryButton("Search Deals", new Vector2(130, 0)))
            {
                _bestDealsEngine.HomeServer     = _bdHomeServer;
                _bestDealsEngine.Discount       = _bdDiscount;
                _bestDealsEngine.MinMedianPrice = _bdMinMedian;
                _bestDealsEngine.MaxBuyPrice    = _bdMaxBuyPrice;
                _bestDealsEngine.MinSales       = _bdMinSales;
                _bestDealsEngine.TriggerRefresh();
            }

            if (_bestDealsEngine.IsRefreshing) ImGui.EndDisabled();
            if (noServer) ImGui.EndDisabled();

            if (noServer)
            {
                ImGui.SameLine(0, Theme.PadLarge);
                ImGui.TextColored(Theme.Warning, "Enter your home server name to search.");
            }
            else if (_bestDealsEngine.IsRefreshing)
            {
                ImGui.SameLine(0, Theme.Pad);
                ImGui.TextColored(Theme.Accent, _bestDealsEngine.StatusMessage);
            }
            else if (snap.FetchedAt > DateTime.MinValue)
            {
                ImGui.SameLine(0, Theme.PadLarge);
                ImGui.TextColored(Theme.TextMuted, _bestDealsEngine.StatusMessage);
            }

            ImGui.Spacing();

            if (_bestDealsEngine.IsRefreshing)
            {
                ImGui.ProgressBar(_bestDealsEngine.RefreshProgress, new Vector2(-1, 0));
                ImGui.Spacing();
            }

            ImGui.Separator();
            ImGui.Spacing();

            // ── Results ──────────────────────────────
            if (snap.ErrorMessage != null)
            {
                ImGui.TextColored(Theme.Error, snap.ErrorMessage);
                return;
            }

            if (snap.FetchedAt == DateTime.MinValue)
            {
                ImGui.TextColored(Theme.TextMuted, "Set your parameters above and click Search Deals.");
                ImGui.Spacing();
                ImGui.TextColored(Theme.TextMuted, "Data is sourced from Saddlebag Exchange — no login required.");
                return;
            }

            if (snap.Deals.Count == 0)
            {
                ImGui.TextColored(Theme.TextMuted, "No deals found. Try lowering Discount %, Min Median, or Min Sales.");
                return;
            }

            if (!ReferenceEquals(snap, _lastDealsSnap))
            {
                _dealsWorking.Clear();
                _dealsWorking.AddRange(snap.Deals);
                _lastDealsSnap = snap;
                _bestDealsExpandedIndex = -1;
            }

            DrawBestDealsTable(_dealsWorking, ref _bestDealsExpandedIndex);
        }

        private static void DrawBestDealsTable(List<BestDealItem> deals, ref int expandedIndex)
        {
            const ImGuiTableFlags tableFlags =
                ImGuiTableFlags.Sortable      |
                ImGuiTableFlags.SizingFixedFit |
                ImGuiTableFlags.BordersInnerV  |
                ImGuiTableFlags.ScrollY;

            ImGui.BeginChild("BestDealsScroll", Vector2.Zero, false);

            if (!ImGui.BeginTable("BestDealsTable", 7, tableFlags))
            {
                ImGui.EndChild();
                return;
            }

            // Columns: #, Item (stretch), Buy On, List Price, Home Median, Discount, Est. Profit
            ImGui.TableSetupScrollFreeze(0, 1);
            ImGui.TableSetupColumn("#",           ImGuiTableColumnFlags.WidthFixed | ImGuiTableColumnFlags.NoSort, 36f);
            ImGui.TableSetupColumn("Item",        ImGuiTableColumnFlags.WidthStretch, 1f);
            ImGui.TableSetupColumn("Buy On",      ImGuiTableColumnFlags.WidthFixed | ImGuiTableColumnFlags.NoSort, 0f);
            ImGui.TableSetupColumn("List Price",
                ImGuiTableColumnFlags.WidthFixed | ImGuiTableColumnFlags.PreferSortDescending, 0f, 1u);
            ImGui.TableSetupColumn("Home Min",
                ImGuiTableColumnFlags.WidthFixed | ImGuiTableColumnFlags.PreferSortDescending, 0f, 2u);
            ImGui.TableSetupColumn("Discount",
                ImGuiTableColumnFlags.WidthFixed | ImGuiTableColumnFlags.DefaultSort | ImGuiTableColumnFlags.PreferSortDescending, 0f, 3u);
            ImGui.TableSetupColumn("Est. Profit",
                ImGuiTableColumnFlags.WidthFixed | ImGuiTableColumnFlags.PreferSortDescending, 0f, 4u);
            ImGui.TableHeadersRow();

            var specs = ImGui.TableGetSortSpecs();
            if (specs.SpecsDirty)
            {
                var uid  = (int)specs.Specs.ColumnUserID;
                var desc = specs.Specs.SortDirection == ImGuiSortDirection.Descending;
                deals.Sort((a, b) =>
                {
                    var homeMinA = a.HomeMinPrice > 0 ? a.HomeMinPrice : (a.MedianHQ > a.MedianNQ && a.MedianHQ > 0 ? a.MedianHQ : a.MedianNQ);
                    var homeMinB = b.HomeMinPrice > 0 ? b.HomeMinPrice : (b.MedianHQ > b.MedianNQ && b.MedianHQ > 0 ? b.MedianHQ : b.MedianNQ);
                    float va = uid == 1 ? a.MinPrice : uid == 2 ? homeMinA : uid == 4 ? a.PotentialProfit : a.Discount;
                    float vb = uid == 1 ? b.MinPrice : uid == 2 ? homeMinB : uid == 4 ? b.PotentialProfit : b.Discount;
                    return desc ? vb.CompareTo(va) : va.CompareTo(vb);
                });
                specs.SpecsDirty = false;
            }

            for (var i = 0; i < deals.Count; i++)
            {
                var deal       = deals[i];
                var isExpanded = expandedIndex == i;

                ImGui.PushID(i);
                ImGui.TableNextRow(ImGuiTableRowFlags.None, IconRow.Y + 4);

                // # ─────────────────────────────────
                ImGui.TableSetColumnIndex(0);
                var lineH   = ImGui.GetTextLineHeight();
                var rowTopY = ImGui.GetCursorPosY();
                var centerY = rowTopY + (IconRow.Y - lineH) * 0.5f;
                ImGui.SetCursorPosX(ImGui.GetCursorPosX() + Theme.PadSmall);
                ImGui.SetCursorPosY(centerY);
                ImGui.TextColored(Theme.TextMuted, (i + 1).ToString());

                // Item ─────────────────────────────
                ImGui.TableSetColumnIndex(1);
                var col1Pos = ImGui.GetCursorPos();

                var nameText = string.IsNullOrEmpty(deal.ItemName)
                    ? string.Concat("Item #", deal.ItemId.ToString())
                    : deal.ItemName;

                // Selectable at natural row top — click detection without inflating row height
                if (ImGui.Selectable(string.Concat("##bd", i.ToString()),
                    isExpanded, ImGuiSelectableFlags.SpanAllColumns, new Vector2(0, IconRow.Y)))
                {
                    expandedIndex = isExpanded ? -1 : i;
                }

                // Draw icon + text on top of the selectable
                ImGui.SetCursorPos(col1Pos);
                Theme.DrawGameIcon(deal.IconId, IconRow);
                ImGui.SameLine(0, Theme.PadSmall);
                ImGui.SetCursorPosY(centerY);
                ImGui.TextColored(Theme.TextPrimary, nameText);

                // Buy On ───────────────────────────
                ImGui.TableSetColumnIndex(2);
                ImGui.SetCursorPosY(centerY);
                ImGui.TextColored(Theme.TextSecondary, deal.WorldName);

                // List Price ───────────────────────
                ImGui.TableSetColumnIndex(3);
                ImGui.SetCursorPosY(centerY);
                DrawBoxedValue(FormatGil(deal.MinPrice), Theme.Warning);

                // Home Min ────────────────────────
                var homeMin = deal.HomeMinPrice > 0 ? deal.HomeMinPrice
                    : (deal.MedianHQ > deal.MedianNQ && deal.MedianHQ > 0 ? deal.MedianHQ : deal.MedianNQ);
                ImGui.TableSetColumnIndex(4);
                ImGui.SetCursorPosY(centerY);
                DrawBoxedValue(FormatGil(homeMin), Theme.TextSecondary);

                // Discount ─────────────────────────
                ImGui.TableSetColumnIndex(5);
                ImGui.SetCursorPosY(centerY);
                DrawBoxedValue(string.Concat(deal.Discount.ToString("F0"), "% off"), Theme.Accent);

                // Est. Profit ──────────────────────
                var profitColor = deal.PotentialProfit >= 0 ? Theme.Success : Theme.Error;
                ImGui.TableSetColumnIndex(6);
                ImGui.SetCursorPosY(centerY);
                var profitStr = deal.PotentialProfit >= 0
                    ? string.Concat("+", FormatGil(deal.PotentialProfit))
                    : string.Concat("-", FormatGil(-deal.PotentialProfit));
                DrawBoxedValue(profitStr, profitColor);

                // Expanded detail row ──────────────
                if (isExpanded)
                {
                    ImGui.TableNextRow();
                    ImGui.TableSetColumnIndex(1);
                    ImGui.Indent(Theme.PadSmall);
                    ImGui.Spacing();

                    if (deal.SalesAmountNQ > 0)
                        Theme.KeyValue("NQ sales (7d):", string.Concat(deal.SalesAmountNQ.ToString(), "   qty sold: ", deal.QuantitySoldNQ.ToString()), Theme.TextPrimary);
                    if (deal.SalesAmountHQ > 0)
                        Theme.KeyValue("HQ sales (7d):", string.Concat(deal.SalesAmountHQ.ToString(), "   qty sold: ", deal.QuantitySoldHQ.ToString()), Theme.TextPrimary);
                    if (deal.MedianNQ > 0)
                        Theme.KeyValue("NQ median:", FormatGil(deal.MedianNQ), Theme.Gold);
                    if (deal.MedianHQ > 0)
                        Theme.KeyValue("HQ median:", FormatGil(deal.MedianHQ), Theme.Gold);

                    var uploadAge = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() - deal.LastUploadTime;
                    var ageMin    = uploadAge / 60000;
                    var ageStr    = ageMin < 60
                        ? string.Concat(ageMin.ToString(), "m ago")
                        : string.Concat((ageMin / 60).ToString(), "h ago");
                    Theme.KeyValue("Listing age:", ageStr, Theme.TextMuted);

                    ImGui.Spacing();
                    ImGui.Unindent(Theme.PadSmall);
                }

                ImGui.PopID();
            }

            ImGui.EndTable();
            ImGui.EndChild();
        }

        // ──────────────────────────────────────────────
        // Boxed Value Renderers
        // ──────────────────────────────────────────────

        private static void DrawBoxedValue(string text, Vector4 color)
        {
            var drawList  = ImGui.GetWindowDrawList();
            var pos       = ImGui.GetCursorScreenPos();
            var textSize  = ImGui.CalcTextSize(text);
            var padding   = new Vector2(4, 1);
            var boxMax    = new Vector2(pos.X + textSize.X + padding.X * 2, pos.Y + textSize.Y + padding.Y * 2);

            drawList.AddRectFilled(pos, boxMax, ImGui.ColorConvertFloat4ToU32(new Vector4(color.X, color.Y, color.Z, 0.12f)), 3f);
            drawList.AddRect(pos, boxMax, ImGui.ColorConvertFloat4ToU32(new Vector4(color.X, color.Y, color.Z, 0.55f)), 3f);
            drawList.AddText(new Vector2(pos.X + padding.X, pos.Y + padding.Y), ImGui.ColorConvertFloat4ToU32(color), text);
            ImGui.Dummy(new Vector2(textSize.X + padding.X * 2, textSize.Y + padding.Y * 2));
        }

        private static void DrawBoxedValueAt(ImDrawListPtr drawList, Vector2 pos, string text, Vector4 color)
        {
            var textSize = ImGui.CalcTextSize(text);
            var padding  = new Vector2(5, 2);
            var boxMax   = new Vector2(pos.X + textSize.X + padding.X * 2, pos.Y + textSize.Y + padding.Y * 2);

            drawList.AddRectFilled(pos, boxMax, ImGui.ColorConvertFloat4ToU32(new Vector4(color.X, color.Y, color.Z, 0.12f)), 3f);
            drawList.AddRect(pos, boxMax, ImGui.ColorConvertFloat4ToU32(new Vector4(color.X, color.Y, color.Z, 0.55f)), 3f);
            drawList.AddText(new Vector2(pos.X + padding.X, pos.Y + padding.Y), ImGui.ColorConvertFloat4ToU32(color), text);
        }

        // ──────────────────────────────────────────────
        // Formatting Helpers
        // ──────────────────────────────────────────────

        private static string FormatNumber(float value)
        {
            if (value >= 1_000_000) return string.Concat((value / 1_000_000f).ToString("F1"), "M");
            if (value >= 1_000)    return string.Concat((value / 1_000f).ToString("F1"),     "K");
            return value.ToString("F0");
        }

        private static string FormatGil(float value)
        {
            if (value >= 1_000_000_000) return string.Concat((value / 1_000_000_000f).ToString("F1"), "B");
            if (value >= 1_000_000)     return string.Concat((value / 1_000_000f).ToString("F1"),     "M");
            if (value >= 1_000)         return string.Concat((value / 1_000f).ToString("F1"),         "K");
            return ((int)value).ToString("N0");
        }

        private static string TruncateCategory(string name) => name switch
        {
            "Mounts & Bardings" => "Mounts",
            "Crafting Mats"     => "Crafting",
            _ => name.Length > 14 ? string.Concat(name.AsSpan(0, 12), "..") : name,
        };
    }
}
