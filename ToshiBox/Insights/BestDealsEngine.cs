using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
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
    /// Calls the Saddlebag Exchange /api/ffxiv/bestdeals endpoint to find flip opportunities:
    /// items listed cheaply on other worlds in your DC that sell for more on your home server.
    /// </summary>
    public sealed class BestDealsEngine : IDisposable
    {
        private const string ApiUrl = "https://api.saddlebagexchange.com/api/ffxiv/bestdeals";
        private const int RequestTimeoutMs = 20000;

        private readonly HttpClient httpClient;
        private readonly UniversalisService universalisService;
        private readonly ExcelSheet<Item> itemSheet;

        private BestDealsSnapshot currentSnapshot;
        public BestDealsSnapshot CurrentSnapshot => Volatile.Read(ref currentSnapshot);

        private CancellationTokenSource? refreshCts;
        private volatile bool isRefreshing;
        public bool IsRefreshing => isRefreshing;

        private string statusMessage = string.Empty;
        public string StatusMessage => statusMessage;

        private float refreshProgress;
        public float RefreshProgress => refreshProgress;

        // Parameters — updated by the UI before triggering a refresh
        public string HomeServer       { get; set; }
        public int    Discount         { get; set; }
        public int    MinMedianPrice   { get; set; }
        public int    MaxBuyPrice      { get; set; }
        public int    MinSales         { get; set; }

        public BestDealsEngine(Config config)
        {
            var cfg = config.MarketInsightsConfig;

            HomeServer     = cfg.BestDealsHomeServer;
            Discount       = cfg.BestDealsDiscount;
            MinMedianPrice = cfg.BestDealsMinMedian;
            MaxBuyPrice    = cfg.BestDealsMaxBuyPrice;
            MinSales       = cfg.BestDealsMinSales;

            httpClient = new HttpClient { Timeout = TimeSpan.FromMilliseconds(RequestTimeoutMs) };
            httpClient.DefaultRequestHeaders.Add("User-Agent", "ToshiBox-FFXIV-Plugin");
            universalisService = new UniversalisService();

            itemSheet = Svc.Data.GetExcelSheet<Item>()!;

            currentSnapshot = new BestDealsSnapshot
            {
                FetchedAt  = DateTime.MinValue,
                HomeServer = HomeServer,
            };
        }

        // ──────────────────────────────────────────────
        // Public API
        // ──────────────────────────────────────────────

        public void TriggerRefresh()
        {
            if (isRefreshing) return;
            if (string.IsNullOrWhiteSpace(HomeServer)) return;

            isRefreshing = true;

            refreshCts?.Cancel();
            refreshCts?.Dispose();
            refreshCts = new CancellationTokenSource();
            var ct = refreshCts.Token;

            // Snapshot parameters at trigger time
            var homeServer     = HomeServer;
            var discount       = Discount;
            var minMedian      = MinMedianPrice;
            var maxBuyPrice    = MaxBuyPrice;
            var minSales       = MinSales;

            refreshProgress = 0f;

            Task.Run(async () =>
            {
                try
                {
                    statusMessage = "Contacting Saddlebag Exchange...";
                    refreshProgress = 0.05f;
                    var snapshot = await FetchSnapshotAsync(homeServer, discount, minMedian, maxBuyPrice, minSales, ct);
                    Interlocked.Exchange(ref currentSnapshot, snapshot);
                    refreshProgress = 1f;
                    statusMessage = snapshot.ErrorMessage != null
                        ? "Request failed."
                        : $"Last updated: {DateTime.Now:HH:mm:ss}  —  {snapshot.Deals.Count} deals found";
                }
                catch (OperationCanceledException)
                {
                    statusMessage = "Refresh cancelled.";
                }
                catch (Exception ex)
                {
                    Svc.Log.Error(ex, "[BestDeals] Refresh failed");
                    Interlocked.Exchange(ref currentSnapshot, new BestDealsSnapshot
                    {
                        FetchedAt  = DateTime.UtcNow,
                        HomeServer = homeServer,
                        ErrorMessage = string.Concat("Request failed: ", ex.Message),
                    });
                    statusMessage = "Refresh failed.";
                }
                finally
                {
                    isRefreshing = false;
                }
            }, ct);
        }

        // ──────────────────────────────────────────────
        // Fetch
        // ──────────────────────────────────────────────

        private async Task<BestDealsSnapshot> FetchSnapshotAsync(
            string homeServer, int discount, int minMedian, int maxBuyPrice, int minSales,
            CancellationToken ct)
        {
            var requestBody = JsonSerializer.Serialize(new
            {
                home_server  = homeServer,
                discount     = discount,
                medianPrice  = minMedian,
                salesAmount  = minSales,
                maxBuyPrice  = maxBuyPrice,
                filters      = new[] { 0 },   // 0 = all categories
                hq_only      = false,
            });

            using var content  = new StringContent(requestBody, Encoding.UTF8, "application/json");
            using var response = await httpClient.PostAsync(ApiUrl, content, ct);

            var json = await response.Content.ReadAsStringAsync(ct);
            refreshProgress = 0.60f;

            if (!response.IsSuccessStatusCode)
            {
                Svc.Log.Warning($"[BestDeals] HTTP {(int)response.StatusCode}: {json}");
                return new BestDealsSnapshot
                {
                    FetchedAt    = DateTime.UtcNow,
                    HomeServer   = homeServer,
                    ErrorMessage = $"API returned {(int)response.StatusCode}. Check home server name.",
                };
            }

            var deals = ParseResponse(json);
            EnrichWithLumina(deals);

            // Fetch current lowest listings on the home server from Universalis
            if (deals.Count > 0)
            {
                statusMessage = "Fetching home server prices...";
                refreshProgress = 0.70f;
                var itemIds = new List<uint>(deals.Count);
                foreach (var d in deals) itemIds.Add(d.ItemId);

                var homeData = await universalisService.GetMarketDataAsync(homeServer, itemIds, ct);
                var homeMin  = new Dictionary<uint, int>(homeData.Count);
                foreach (var d in homeData)
                    if (d.MinPrice > 0) homeMin[d.ItemId] = (int)d.MinPrice;

                foreach (var deal in deals)
                    if (homeMin.TryGetValue(deal.ItemId, out var p)) deal.HomeMinPrice = p;
            }

            Svc.Log.Information($"[BestDeals] {deals.Count} deals returned for {homeServer}");

            return new BestDealsSnapshot
            {
                FetchedAt  = DateTime.UtcNow,
                HomeServer = homeServer,
                Deals      = deals,
            };
        }

        // ──────────────────────────────────────────────
        // Parsing
        // ──────────────────────────────────────────────

        private static List<BestDealItem> ParseResponse(string json)
        {
            var result = new List<BestDealItem>();
            try
            {
                using var doc  = JsonDocument.Parse(json);
                var root = doc.RootElement;

                // Response shape: { "data": [ {...}, ... ] }
                if (!root.TryGetProperty("data", out var dataArr) ||
                    dataArr.ValueKind != JsonValueKind.Array)
                    return result;

                foreach (var el in dataArr.EnumerateArray())
                {
                    var item = ParseDealItem(el);
                    if (item != null) result.Add(item);
                }
            }
            catch (Exception ex)
            {
                Svc.Log.Warning(ex, "[BestDeals] Failed to parse response");
            }
            return result;
        }

        private static BestDealItem? ParseDealItem(JsonElement el)
        {
            if (!el.TryGetProperty("itemID", out var idEl)) return null;

            return new BestDealItem
            {
                ItemId         = idEl.GetUInt32(),
                ItemName       = GetString(el, "itemName"),
                MinPrice       = GetInt(el, "minPrice"),
                WorldName      = GetString(el, "worldName"),
                MedianNQ       = GetInt(el, "medianNQ"),
                MedianHQ       = GetInt(el, "medianHQ"),
                AverageNQ      = GetFloat(el, "averageNQ"),
                AverageHQ      = GetFloat(el, "averageHQ"),
                SalesAmountNQ  = GetInt(el, "salesAmountNQ"),
                SalesAmountHQ  = GetInt(el, "salesAmountHQ"),
                QuantitySoldNQ = GetInt(el, "quantitySoldNQ"),
                QuantitySoldHQ = GetInt(el, "quantitySoldHQ"),
                Discount       = GetFloat(el, "discount"),
                LastUploadTime = GetLong(el, "lastUploadTime"),
            };
        }

        private void EnrichWithLumina(List<BestDealItem> deals)
        {
            foreach (var deal in deals)
            {
                // Use Lumina name + icon if Saddlebag didn't return a name
                var row = itemSheet.GetRowOrDefault(deal.ItemId);
                if (row == null) continue;
                if (string.IsNullOrEmpty(deal.ItemName))
                    deal.ItemName = row.Value.Name.ExtractText();
                deal.IconId = (uint)row.Value.Icon;
            }
        }

        // ──────────────────────────────────────────────
        // JSON helpers
        // ──────────────────────────────────────────────

        private static string GetString(JsonElement el, string prop)
            => el.TryGetProperty(prop, out var v) && v.ValueKind == JsonValueKind.String
                ? v.GetString() ?? string.Empty : string.Empty;

        private static int GetInt(JsonElement el, string prop)
            => el.TryGetProperty(prop, out var v) && v.TryGetInt32(out var i) ? i : 0;

        private static long GetLong(JsonElement el, string prop)
            => el.TryGetProperty(prop, out var v) && v.TryGetInt64(out var l) ? l : 0;

        private static float GetFloat(JsonElement el, string prop)
            => el.TryGetProperty(prop, out var v) && v.TryGetSingle(out var f) ? f : 0;

        public void Dispose()
        {
            refreshCts?.Cancel();
            refreshCts?.Dispose();
            httpClient.Dispose();
            universalisService.Dispose();
        }
    }
}
