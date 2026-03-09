using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using ECommons.DalamudServices;
using Lumina.Excel;
using Lumina.Excel.Sheets;
using ToshiBox.Common;

namespace ToshiBox.Insights
{
    /// <summary>
    /// Background data orchestrator for the Market Insights feature.
    /// Directly queries Universalis per category, enriches with Lumina names,
    /// and exposes an InsightsSnapshot for lock-free UI reads.
    /// Persists the last successful snapshot to disk so data is available instantly on next load.
    /// </summary>
    public sealed class InsightsEngine : IDisposable
    {
        private readonly UniversalisService universalisService;
        private readonly ItemCategoryProvider categoryProvider;
        private readonly ExcelSheet<Item> itemSheet;
        private readonly string cacheFilePath;

        private InsightsSnapshot currentSnapshot;
        public InsightsSnapshot CurrentSnapshot => Volatile.Read(ref currentSnapshot);

        private CancellationTokenSource? refreshCts;
        private volatile bool isRefreshing;
        public bool IsRefreshing => isRefreshing;

        private string selectedDataCenter;
        public string SelectedDataCenter
        {
            get => selectedDataCenter;
            set => selectedDataCenter = value;
        }

        private string statusMessage = string.Empty;
        public string StatusMessage => statusMessage;

        public InsightsEngine(Config config)
        {
            universalisService = new UniversalisService();
            categoryProvider   = new ItemCategoryProvider();
            itemSheet          = Svc.Data.GetExcelSheet<Item>()!;

            cacheFilePath = Path.Combine(
                Svc.PluginInterface.ConfigDirectory.FullName,
                "insights_cache.json");

            selectedDataCenter = config.MarketInsightsConfig.DataCenter;

            // Load cached snapshot immediately so the UI has data on first open
            var cached = LoadCache();
            currentSnapshot = cached ?? new InsightsSnapshot
            {
                IsLoading      = false,
                FetchedAt      = DateTime.MinValue,
                DataCenterName = selectedDataCenter,
            };

            if (cached != null)
                statusMessage = $"Last updated: {cached.FetchedAt.ToLocalTime():HH:mm:ss} (cached)";
        }

        /// <summary>
        /// Manually triggers a data refresh. Safe to call from the UI thread.
        /// Cancels any in-progress refresh before starting a new one.
        /// </summary>
        public void TriggerRefresh()
        {
            if (isRefreshing) return;
            isRefreshing = true;

            refreshCts?.Cancel();
            refreshCts?.Dispose();
            refreshCts = new CancellationTokenSource();
            var ct = refreshCts.Token;
            var dc = selectedDataCenter;

            Task.Run(async () =>
            {
                try
                {
                    var snapshot = await FetchSnapshotAsync(dc, ct);
                    Interlocked.Exchange(ref currentSnapshot, snapshot);
                    SaveCache(snapshot);
                    statusMessage = $"Last updated: {DateTime.Now:HH:mm:ss}";
                }
                catch (OperationCanceledException)
                {
                    statusMessage = "Refresh cancelled.";
                }
                catch (Exception ex)
                {
                    Svc.Log.Error(ex, "[Insights] Refresh failed");
                    var errorSnapshot = new InsightsSnapshot
                    {
                        FetchedAt      = DateTime.UtcNow,
                        DataCenterName = dc,
                        ErrorMessage   = string.Concat("Refresh failed: ", ex.Message),
                    };
                    Interlocked.Exchange(ref currentSnapshot, errorSnapshot);
                    statusMessage = "Refresh failed.";
                }
                finally
                {
                    isRefreshing = false;
                }
            }, ct);
        }

        private async Task<InsightsSnapshot> FetchSnapshotAsync(string dc, CancellationToken ct)
        {
            var categories     = categoryProvider.GetCategories();
            var categoryNames  = categoryProvider.GetCategoryNames();
            var allItems       = new List<MarketItemData>();
            var itemsByCategory = new Dictionary<string, List<MarketItemData>>();

            var categoryIndex  = 0;
            var totalCategories = categoryNames.Length;

            foreach (var categoryName in categoryNames)
            {
                ct.ThrowIfCancellationRequested();

                categoryIndex++;
                statusMessage = $"Scanning {categoryName}... {categoryIndex}/{totalCategories}";
                Svc.Log.Information($"[Insights] {statusMessage}");

                if (!categories.TryGetValue(categoryName, out var ids) || ids.Count == 0)
                    continue;

                var categoryItems = await universalisService.GetMarketDataAsync(dc, ids, ct);

                foreach (var item in categoryItems)
                {
                    item.CategoryName = categoryName;
                    EnrichItemInfo(item);
                }

                itemsByCategory[categoryName] = categoryItems;
                allItems.AddRange(categoryItems);
            }

            statusMessage = "Building rankings...";

            var filtered = new List<MarketItemData>(allItems.Count);
            for (var i = 0; i < allItems.Count; i++)
            {
                var item = allItems[i];
                if (item.RegularSaleVelocity > 0 || item.CurrentAveragePrice > 0)
                    filtered.Add(item);
            }

            filtered.Sort((a, b) => b.RegularSaleVelocity.CompareTo(a.RegularSaleVelocity));
            var hottestItems = TakeTop(filtered, 50);

            filtered.Sort((a, b) => b.EstimatedDailyGilVolume.CompareTo(a.EstimatedDailyGilVolume));
            var highestGilVolume = TakeTop(filtered, 50);

            filtered.Sort((a, b) => b.CurrentAveragePrice.CompareTo(a.CurrentAveragePrice));
            var mostExpensive = TakeTop(filtered, 50);

            var categorySummaries = new List<CategorySummary>();
            foreach (var categoryName in categoryNames)
            {
                if (!itemsByCategory.TryGetValue(categoryName, out var catItems) || catItems.Count == 0)
                    continue;

                float totalVelocity = 0, totalGil = 0, totalPrice = 0;
                MarketItemData? topItem = null;
                var topVelocity = -1f;

                foreach (var item in catItems)
                {
                    totalVelocity += item.RegularSaleVelocity;
                    totalGil      += item.EstimatedDailyGilVolume;
                    totalPrice    += item.CurrentAveragePrice;

                    if (item.RegularSaleVelocity > topVelocity)
                    {
                        topVelocity = item.RegularSaleVelocity;
                        topItem     = item;
                    }
                }

                categorySummaries.Add(new CategorySummary
                {
                    CategoryName           = categoryName,
                    ItemCount              = catItems.Count,
                    TotalDailyVelocity     = totalVelocity,
                    AveragePrice           = catItems.Count > 0 ? totalPrice / catItems.Count : 0,
                    EstimatedDailyGilVolume = totalGil,
                    TopItem                = topItem,
                });
            }

            return new InsightsSnapshot
            {
                FetchedAt        = DateTime.UtcNow,
                DataCenterName   = dc,
                HottestItems     = hottestItems,
                HighestGilVolume = highestGilVolume,
                MostExpensive    = mostExpensive,
                CategorySummaries = categorySummaries,
                ItemsByCategory  = itemsByCategory,
            };
        }

        // ──────────────────────────────────────────────
        // Disk Cache
        // ──────────────────────────────────────────────

        private static readonly JsonSerializerOptions CacheJsonOptions = new()
        {
            WriteIndented = false,
        };

        private InsightsSnapshot? LoadCache()
        {
            try
            {
                if (!File.Exists(cacheFilePath)) return null;
                var json = File.ReadAllText(cacheFilePath);
                return JsonSerializer.Deserialize<InsightsSnapshot>(json, CacheJsonOptions);
            }
            catch (Exception ex)
            {
                Svc.Log.Warning(ex, "[Insights] Failed to load cache");
                return null;
            }
        }

        private void SaveCache(InsightsSnapshot snapshot)
        {
            try
            {
                var json = JsonSerializer.Serialize(snapshot, CacheJsonOptions);
                File.WriteAllText(cacheFilePath, json);
            }
            catch (Exception ex)
            {
                Svc.Log.Warning(ex, "[Insights] Failed to save cache");
            }
        }

        // ──────────────────────────────────────────────
        // Helpers
        // ──────────────────────────────────────────────

        private void EnrichItemInfo(MarketItemData data)
        {
            var item = itemSheet.GetRowOrDefault(data.ItemId);
            if (item == null) return;
            data.ItemName = item.Value.Name.ExtractText();
            data.IconId   = (uint)item.Value.Icon;
        }

        private static List<MarketItemData> TakeTop(List<MarketItemData> sorted, int count)
        {
            var take   = Math.Min(count, sorted.Count);
            var result = new List<MarketItemData>(take);
            for (var i = 0; i < take; i++)
                result.Add(sorted[i]);
            return result;
        }

        public void Dispose()
        {
            refreshCts?.Cancel();
            refreshCts?.Dispose();
            universalisService.Dispose();
        }
    }
}
