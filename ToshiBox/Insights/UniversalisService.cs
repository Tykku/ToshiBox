using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using ECommons.DalamudServices;

namespace ToshiBox.Insights
{
    /// <summary>
    /// HTTP client for the Universalis API. Handles batching, response parsing,
    /// and error handling for market board data queries.
    /// </summary>
    public sealed class UniversalisService : IDisposable
    {
        private const string BaseUrl = "https://universalis.app/api/v2";
        private const int RequestTimeoutMs = 15000;
        private const int MaxItemsPerBatch = 100;
        private const int InterBatchDelayMs = 75;
        private const int MaxRetries = 2;
        private const int RetryDelayMs = 2000;
        private const int MaxResponseSizeBytes = 5 * 1024 * 1024; // 5MB — 100-item batches can be large

        private readonly HttpClient httpClient;

        public UniversalisService()
        {
            httpClient = new HttpClient
            {
                Timeout = TimeSpan.FromMilliseconds(RequestTimeoutMs),
            };
            httpClient.DefaultRequestHeaders.Add("User-Agent", "ToshiBox-FFXIV-Plugin");
        }

        // ──────────────────────────────────────────────
        // Market Data (batched)
        // ──────────────────────────────────────────────

        /// <summary>
        /// Fetches market data for items on a single data center.
        /// Automatically batches into groups of MaxItemsPerBatch items per request.
        /// Uses noListings=1 to skip the full listings array — we only need aggregate stats.
        /// Failed batches are retried, then fall back to individual item queries.
        /// </summary>
        public async Task<List<MarketItemData>> GetMarketDataAsync(
            string dcName, IReadOnlyList<uint> itemIds, CancellationToken ct = default,
            Action<int, int>? onBatch = null)
        {
            if (itemIds.Count == 0) return new List<MarketItemData>();

            var results = new List<MarketItemData>();
            var totalBatches = Math.Max(1, (itemIds.Count + MaxItemsPerBatch - 1) / MaxItemsPerBatch);

            for (var i = 0; i < itemIds.Count; i += MaxItemsPerBatch)
            {
                ct.ThrowIfCancellationRequested();

                if (i > 0)
                    await Task.Delay(InterBatchDelayMs, ct);

                var end = Math.Min(i + MaxItemsPerBatch, itemIds.Count);
                var sb = new System.Text.StringBuilder((end - i) * 8);
                for (var j = i; j < end; j++)
                {
                    if (j > i) sb.Append(',');
                    sb.Append(itemIds[j]);
                }

                var url = string.Concat(BaseUrl, "/", dcName, "/", sb.ToString(), "?noListings=1");
                var success = false;

                for (var attempt = 0; attempt <= MaxRetries; attempt++)
                {
                    if (attempt > 0)
                    {
                        var delay = RetryDelayMs * attempt;
                        Svc.Log.Information(
                            $"[Insights] Retry {attempt}/{MaxRetries} for batch {i / MaxItemsPerBatch} after {delay}ms");
                        await Task.Delay(delay, ct);
                    }

                    var (json, statusCode) = await FetchJsonWithStatusAsync(url, ct);
                    if (json != null)
                    {
                        ParseMarketDataResponse(json, results);
                        success = true;
                        break;
                    }

                    Svc.Log.Warning(
                        $"[Insights] Batch {i / MaxItemsPerBatch} attempt {attempt}: HTTP {statusCode}. IDs: {sb}");

                    if (statusCode == 404) break;
                }

                if (!success)
                {
                    Svc.Log.Information(
                        $"[Insights] Batch {i / MaxItemsPerBatch} failed after retries, falling back to individual queries");
                    for (var j = i; j < end; j++)
                    {
                        ct.ThrowIfCancellationRequested();
                        await Task.Delay(InterBatchDelayMs, ct);

                        var singleUrl = string.Concat(BaseUrl, "/", dcName, "/", itemIds[j].ToString(), "?noListings=1");
                        var (singleJson, _) = await FetchJsonWithStatusAsync(singleUrl, ct);
                        if (singleJson != null)
                            ParseMarketDataResponse(singleJson, results);
                    }
                }

                onBatch?.Invoke(i / MaxItemsPerBatch + 1, totalBatches);
            }

            return results;
        }

        // ──────────────────────────────────────────────
        // Shared HTTP Fetch
        // ──────────────────────────────────────────────

        private async Task<(string? Json, int StatusCode)> FetchJsonWithStatusAsync(string url, CancellationToken ct)
        {
            try
            {
                var response = await httpClient.GetAsync(url, ct);
                var statusCode = (int)response.StatusCode;

                if (!response.IsSuccessStatusCode) return (null, statusCode);

                if (response.Content.Headers.ContentLength > MaxResponseSizeBytes)
                    return (null, statusCode);

                var json = await response.Content.ReadAsStringAsync(ct);
                return json.Length > MaxResponseSizeBytes ? (null, statusCode) : (json, statusCode);
            }
            catch (TaskCanceledException)
            {
                Svc.Log.Warning($"[Insights] Request timed out: {url}");
                return (null, 408);
            }
            catch (HttpRequestException ex)
            {
                Svc.Log.Warning(ex, $"[Insights] Request failed: {url}");
                return (null, 0);
            }
        }

        // ──────────────────────────────────────────────
        // JSON Parsing
        // ──────────────────────────────────────────────

        private static void ParseMarketDataResponse(string json, List<MarketItemData> results)
        {
            try
            {
                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;

                if (root.TryGetProperty("items", out var itemsObj) &&
                    itemsObj.ValueKind == JsonValueKind.Object)
                {
                    foreach (var prop in itemsObj.EnumerateObject())
                    {
                        var data = ParseSingleMarketItem(prop.Value);
                        if (data != null) results.Add(data);
                    }
                }
                else
                {
                    var data = ParseSingleMarketItem(root);
                    if (data != null) results.Add(data);
                }
            }
            catch (Exception ex)
            {
                Svc.Log.Warning(ex, "[Insights] Failed to parse market data response");
            }
        }

        private static MarketItemData? ParseSingleMarketItem(JsonElement el)
        {
            if (!el.TryGetProperty("itemID", out var idEl)) return null;

            var worldPrices = new List<WorldPriceSnapshot>();
            if (el.TryGetProperty("worldUploadTimes", out var worldTimes) &&
                worldTimes.ValueKind == JsonValueKind.Object)
            {
                foreach (var wt in worldTimes.EnumerateObject())
                {
                    if (int.TryParse(wt.Name, out var wId) && wt.Value.TryGetInt64(out var ts))
                    {
                        worldPrices.Add(new WorldPriceSnapshot
                        {
                            WorldId = wId,
                            LastUploadTime = ts,
                        });
                    }
                }
            }

            return new MarketItemData
            {
                ItemId = idEl.GetUInt32(),
                CurrentAveragePrice = GetFloat(el, "currentAveragePrice"),
                CurrentAveragePriceNQ = GetFloat(el, "currentAveragePriceNQ"),
                CurrentAveragePriceHQ = GetFloat(el, "currentAveragePriceHQ"),
                MinPrice = GetFloat(el, "minPrice"),
                MaxPrice = GetFloat(el, "maxPrice"),
                RegularSaleVelocity = GetFloat(el, "regularSaleVelocity"),
                NqSaleVelocity = GetFloat(el, "nqSaleVelocity"),
                HqSaleVelocity = GetFloat(el, "hqSaleVelocity"),
                UnitsForSale = GetInt(el, "unitsForSale"),
                UnitsSold = GetInt(el, "unitsSold"),
                ListingsCount = GetInt(el, "listingsCount"),
                WorldPrices = worldPrices,
            };
        }

        private static float GetFloat(JsonElement el, string prop)
            => el.TryGetProperty(prop, out var v) && v.TryGetSingle(out var f) ? f : 0;

        private static int GetInt(JsonElement el, string prop)
            => el.TryGetProperty(prop, out var v) && v.TryGetInt32(out var i) ? i : 0;

        public void Dispose() => httpClient.Dispose();
    }
}
