using System;
using System.Collections.Generic;

namespace ToshiBox.Insights
{
    /// <summary>
    /// A single flip opportunity returned by the Saddlebag Exchange bestdeals endpoint.
    /// The item is currently listed cheaply on <see cref="WorldName"/> (another world in your DC),
    /// and its median sell price on your home server is much higher.
    /// </summary>
    public sealed class BestDealItem
    {
        public uint ItemId { get; init; }
        public string ItemName { get; set; } = string.Empty;
        public uint IconId { get; set; }

        /// <summary>Current cheapest listing price on the source world (buy here).</summary>
        public int MinPrice { get; init; }

        /// <summary>World where the cheap listing exists.</summary>
        public string WorldName { get; init; } = string.Empty;

        /// <summary>Median NQ price on the home server (sell here).</summary>
        public int MedianNQ { get; init; }

        /// <summary>Median HQ price on the home server.</summary>
        public int MedianHQ { get; init; }

        public float AverageNQ { get; init; }
        public float AverageHQ { get; init; }

        public int SalesAmountNQ { get; init; }
        public int SalesAmountHQ { get; init; }
        public int QuantitySoldNQ { get; init; }
        public int QuantitySoldHQ { get; init; }

        /// <summary>Discount vs. home-server median, as reported by Saddlebag (e.g. 45 = 45% off).</summary>
        public float Discount { get; init; }

        public long LastUploadTime { get; init; }

        /// <summary>
        /// Current lowest listing price on the home server fetched from Universalis.
        /// 0 means no active listings were found.
        /// </summary>
        public int HomeMinPrice { get; set; }

        /// <summary>
        /// Estimated profit per unit: home server lowest listing minus the buy price.
        /// Falls back to the best median if no home listings were found.
        /// </summary>
        public int PotentialProfit
        {
            get
            {
                var sellAt = HomeMinPrice > 0
                    ? HomeMinPrice
                    : (MedianHQ > MedianNQ && MedianHQ > 0 ? MedianHQ : MedianNQ);
                return sellAt - MinPrice;
            }
        }
    }

    public sealed class BestDealsSnapshot
    {
        public DateTime FetchedAt { get; init; }
        public string HomeServer { get; init; } = string.Empty;
        public List<BestDealItem> Deals { get; init; } = new();
        public string? ErrorMessage { get; init; }
    }
}
