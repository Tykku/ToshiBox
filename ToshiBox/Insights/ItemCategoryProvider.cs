using System;
using System.Collections.Generic;
using ECommons.DalamudServices;
using Lumina.Excel.Sheets;

namespace ToshiBox.Insights
{
    /// <summary>
    /// Provides curated sets of item IDs for market analysis.
    /// Uses Lumina sheets to identify items by category rather than hard-coding IDs,
    /// so the plugin stays current across game patches.
    ///
    /// Categories are defined by ItemSearchCategory RowIds from the game's
    /// market board category system.
    /// </summary>
    public sealed class ItemCategoryProvider
    {
        private const int MaxItemsPerCategory = 80;

        private const int MinItemLevelForEndgame = 580;

        private static readonly HashSet<string> ItemLevelExemptCategories = new()
        {
            "Crystals", "Dyes", "Furnishings", "Gardening",
            "Minions", "Orchestrion Rolls", "Mounts & Bardings",
            "Seasonal", "Miscellany", "Ingredients",
        };

        private static readonly (string Name, int[] SearchCategoryIds)[] CategoryDefinitions =
        {
            ("Meals",               new[] { 45, 46 }),
            ("Medicine",            new[] { 43 }),
            ("Ingredients",         new[] { 44 }),
            ("Materia",             new[] { 57 }),
            ("Crafting Mats",       new[] { 47, 48, 49, 50, 51, 52, 53, 55, 56 }),
            ("Gear",                new[] { 31, 32, 33, 34, 35, 36, 37, 38, 39, 40, 41, 42 }),
            ("Dyes",                new[] { 54 }),
            ("Furnishings",         new[] { 65, 66, 67, 68, 69, 70, 71, 72 }),
            ("Crystals",            new[] { 58, 59 }),
            ("Minions",             new[] { 75 }),
            ("Mounts & Bardings",   new[] { 90 }),
        };

        private readonly Dictionary<string, List<uint>> categoryItemIds = new();

        public ItemCategoryProvider()
        {
            BuildCategories();
        }

        public IReadOnlyDictionary<string, List<uint>> GetCategories() => categoryItemIds;

        public string[] GetCategoryNames()
        {
            var names = new string[CategoryDefinitions.Length];
            for (var i = 0; i < CategoryDefinitions.Length; i++)
                names[i] = CategoryDefinitions[i].Name;
            return names;
        }

        private void BuildCategories()
        {
            try
            {
                var itemSheet = Svc.Data.GetExcelSheet<Item>();
                if (itemSheet == null) return;

                var catLookup = new Dictionary<int, string>();
                foreach (var (catName, catIds) in CategoryDefinitions)
                {
                    for (var i = 0; i < catIds.Length; i++)
                        catLookup[catIds[i]] = catName;
                }

                var rawItems = new Dictionary<string, List<(uint RowId, int ItemLevel)>>();
                var totalSheetRows = 0;

                foreach (var item in itemSheet)
                {
                    totalSheetRows++;
                    var searchCatId = (int)item.ItemSearchCategory.RowId;
                    if (searchCatId == 0) continue;

                    if (!catLookup.TryGetValue(searchCatId, out var catName))
                        continue;

                    var name = item.Name.ExtractText();
                    if (string.IsNullOrEmpty(name)) continue;

                    if (!rawItems.TryGetValue(catName, out var list))
                    {
                        list = new List<(uint, int)>();
                        rawItems[catName] = list;
                    }

                    var itemLevel = (int)item.LevelItem.RowId;
                    list.Add((item.RowId, itemLevel));
                }

                foreach (var (catName, items) in rawItems)
                {
                    List<uint> finalIds;

                    if (ItemLevelExemptCategories.Contains(catName))
                    {
                        items.Sort((a, b) => b.RowId.CompareTo(a.RowId));
                        var count = Math.Min(MaxItemsPerCategory, items.Count);
                        finalIds = new List<uint>(count);
                        for (var i = 0; i < count; i++)
                            finalIds.Add(items[i].RowId);
                    }
                    else
                    {
                        var endgameItems = new List<(uint RowId, int ItemLevel)>();
                        for (var i = 0; i < items.Count; i++)
                        {
                            if (items[i].ItemLevel >= MinItemLevelForEndgame)
                                endgameItems.Add(items[i]);
                        }

                        if (endgameItems.Count < 10)
                        {
                            Svc.Log.Information(
                                $"[Insights] Category '{catName}' has only {endgameItems.Count} endgame items (iLvl >= {MinItemLevelForEndgame}), falling back to newest items by RowId");
                            items.Sort((a, b) => b.RowId.CompareTo(a.RowId));
                            var count = Math.Min(MaxItemsPerCategory, items.Count);
                            finalIds = new List<uint>(count);
                            for (var i = 0; i < count; i++)
                                finalIds.Add(items[i].RowId);
                        }
                        else
                        {
                            endgameItems.Sort((a, b) =>
                            {
                                var cmp = b.ItemLevel.CompareTo(a.ItemLevel);
                                return cmp != 0 ? cmp : b.RowId.CompareTo(a.RowId);
                            });

                            var count = Math.Min(MaxItemsPerCategory, endgameItems.Count);
                            finalIds = new List<uint>(count);
                            for (var i = 0; i < count; i++)
                                finalIds.Add(endgameItems[i].RowId);
                        }
                    }

                    categoryItemIds[catName] = finalIds;
                    Svc.Log.Information($"[Insights] Category '{catName}': {items.Count} total -> {finalIds.Count} selected");
                }

                var totalItems = 0;
                foreach (var kvp in categoryItemIds)
                    totalItems += kvp.Value.Count;

                Svc.Log.Information($"[Insights] Lumina Item sheet has {totalSheetRows} total rows");
                Svc.Log.Information($"[Insights] Built {categoryItemIds.Count} categories with {totalItems} total items");
            }
            catch (Exception ex)
            {
                Svc.Log.Error(ex, "[Insights] Failed to build item categories from Lumina");
            }
        }
    }
}
