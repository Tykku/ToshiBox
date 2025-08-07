using System.Text.RegularExpressions;
using Dalamud.Bindings.ImGui;
using Dalamud.Game.Addon.Lifecycle;
using Dalamud.Game.Addon.Lifecycle.AddonArgTypes;
using Dalamud.Game.Network.Structures;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Game.Text.SeStringHandling.Payloads;
using Dalamud.Memory;
using Dalamud.Utility;
using ECommons;
using ECommons.Automation.NeoTaskManager;
using ECommons.DalamudServices;
using ECommons.Logging;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using FFXIVClientStructs.FFXIV.Component.GUI;
using ToshiBox.Common;
using Callback = ECommons.Automation.Callback;

namespace ToshiBox.Features;

public partial class AutoRetainerListing
{
    private readonly Events _events;
    private readonly Config _config;
    private readonly TaskManager taskManager;
    
    public AutoRetainerListing(Events events, Config config)
    {
        _events = events;
        _config = config;
        taskManager = new TaskManager();
    }

    private static int CurrentItemPrice;
    private static int CurrentMarketLowestPrice;
    private static uint CurrentItemSearchItemID;
    private static bool IsCurrentItemHQ;
    private static unsafe RetainerManager.Retainer* CurrentRetainer;
    
    public void IsEnabled()
    {
        if (_config.AutoRetainerListingConfig.Enabled)
        {
            Enable();
            Helpers.PrintGlowshi("If you know you know is enabled",45);

        }
        else
        {
            Disable();
            Helpers.PrintGlowshi("If you know you know is disabled",14);
        }
    }
    
    public void Enable()
    {
        Svc.AddonLifecycle.RegisterListener(AddonEvent.PostSetup, "RetainerSellList", OnRetainerSellList); // List of items
        Svc.AddonLifecycle.RegisterListener(AddonEvent.PostSetup, "RetainerSell", OnRetainerSell);
        Svc.AddonLifecycle.RegisterListener(AddonEvent.PreFinalize, "RetainerSell", OnRetainerSell);
        _events.ListingsStart += OnListingsStart;
        _events.ListingsEnd += OnListingsEnd;
    }

    public void Disable()
    {
        Svc.AddonLifecycle.UnregisterListener(OnRetainerSellList);
        Svc.AddonLifecycle.UnregisterListener(OnRetainerSell);
        _events.ListingsStart -= OnListingsStart;
        _events.ListingsEnd -= OnListingsEnd; 
        taskManager.Abort();
    }

    private static bool SearchRunning;
    private void OnListingsStart()
    {
        Svc.Log.Info("search running!");
        SearchRunning = true;
    }

    private void OnListingsEnd(IReadOnlyList<IMarketBoardItemListing> listings)
    {
        Svc.Log.Info("search done!");
        SearchRunning = false;
    }

    private void OnRetainerSell(AddonEvent eventType, AddonArgs addonInfo)
    {
        if (ImGui.GetIO().KeyShift) return;
            switch (eventType)
        {
            case AddonEvent.PostSetup:
                if (taskManager.IsBusy) return;
                taskManager.Enqueue(ClickComparePrice);
                taskManager.DefaultConfiguration.AbortOnTimeout = false;
                taskManager.EnqueueDelay(500);
                taskManager.Enqueue(GetLowestPrice);
                taskManager.DefaultConfiguration.AbortOnTimeout = true;
                taskManager.EnqueueDelay(100);
                taskManager.Enqueue(FillLowestPrice);
                break;
            case AddonEvent.PreFinalize:
                if (taskManager.NumQueuedTasks <= 1)
                    taskManager.Abort();
                break;
        }
    }

    private unsafe void OnRetainerSellList(AddonEvent type, AddonArgs args)
    {
        var activeRetainer = RetainerManager.Instance()->GetActiveRetainer();
        if (CurrentRetainer == null || CurrentRetainer != activeRetainer)
            CurrentRetainer = activeRetainer;
        else
            return;

        for (var i = 0; i < activeRetainer->MarketItemCount; i++)
        {
            EnqueueSingleItem(i);
            CurrentMarketLowestPrice = 0;
        }
    }
    private bool AbortIfShiftHeld()
    {
        if (ImGui.GetIO().KeyShift)
        {
            Helpers.PrintToshi("Aborting task queue: Left Shift was held.");
            taskManager.Abort();
            return false;
        }
        return true;
    }

    private void EnqueueSingleItem(int index)
    {
        taskManager.Enqueue(() => !SearchRunning);

        taskManager.Enqueue(AbortIfShiftHeld);
        taskManager.Enqueue(() => ClickSellingItem(index));
        taskManager.EnqueueDelay(100);

        taskManager.Enqueue(AbortIfShiftHeld);
        taskManager.Enqueue(ClickAdjustPrice);
        taskManager.EnqueueDelay(100);

        taskManager.Enqueue(AbortIfShiftHeld);
        taskManager.Enqueue(ClickComparePrice);
        taskManager.EnqueueDelay(500);

        taskManager.Enqueue(AbortIfShiftHeld);
        taskManager.DefaultConfiguration.AbortOnTimeout = false;
        taskManager.Enqueue(GetLowestPrice);

        taskManager.DefaultConfiguration.AbortOnTimeout = true;
        taskManager.EnqueueDelay(100);

        taskManager.Enqueue(AbortIfShiftHeld);
        taskManager.Enqueue(FillLowestPrice);
        taskManager.EnqueueDelay(800);
    }


    private static unsafe void GetListings()
    {
        var container = InventoryManager.Instance()->GetInventoryContainer(InventoryType.RetainerMarket);
        for (var i = 0; i < container->Size; i++)
        {
            var item = container->GetInventorySlot(i);
            if (item == null) continue;
        }
        Enumerable.Range(0, (int)container->Size).Count(i => container->GetInventorySlot(i) != null);
    }

    private static unsafe int GetListingsCount()
    {
        var container = InventoryManager.Instance()->GetInventoryContainer(InventoryType.RetainerMarket);
        return container != null ? Enumerable.Range(0, (int)container->Size).Count(i => container->GetInventorySlot(i) != null) : 0;
    }

    private static unsafe bool? ClickSellingItem(int index)
    {
        if (GenericHelpers.TryGetAddonByName<AtkUnitBase>("RetainerSellList", out var addon) && GenericHelpers.IsAddonReady(addon))
        {
            Callback.Fire(addon, true, 0, index, 1);
            return true;
        }

        return false;
    }

    private static unsafe bool? ClickAdjustPrice()
    {
        if (GenericHelpers.TryGetAddonByName<AtkUnitBase>("ContextMenu", out var addon) && GenericHelpers.IsAddonReady(addon) && !SearchRunning)
        {
            Callback.Fire(addon, true, 0, 0, 0, 0, 0);
            return true;
        }

        return false;
    }

    private static unsafe bool? ClickComparePrice()
    {
        if (GenericHelpers.TryGetAddonByName<AtkUnitBase>("RetainerSell", out var addon) && GenericHelpers.IsAddonReady(addon) && !SearchRunning)
        {
            CurrentItemPrice = addon->AtkValues[5].Int;
            IsCurrentItemHQ = MemoryHelper.ReadSeStringNullTerminated((nint)addon->AtkValues[1].String.Value).TextValue.Contains(''); // hq symbol

            Callback.Fire(addon, true, 4);
            return true;
        }

        return false;
    }

    private unsafe bool? GetLowestPrice()
    {
        if (GenericHelpers.TryGetAddonByName<AtkUnitBase>("ItemSearchResult", out var addon) && GenericHelpers.IsAddonReady(addon))
        {
            CurrentItemSearchItemID = AgentItemSearch.Instance()->ResultItemId;
            var searchResult = addon->GetTextNodeById(29)->NodeText.GetText();
            if (string.IsNullOrEmpty(searchResult)) return false;

            if (int.Parse(AutoRetainerPriceAdjustRegex().Replace(searchResult, "")) == 0)
            {
                CurrentMarketLowestPrice = 0;
                addon->Close(true);
                return true;
            }

            if (_config.AutoRetainerListingConfig.SeparateNQAndHQ && IsCurrentItemHQ)
            {
                var foundHQItem = false;
                for (var i = 1; i <= 12 && !foundHQItem; i++)
                {
                    var listing = addon->UldManager.NodeList[5]->GetAsAtkComponentNode()->Component->UldManager.NodeList[i]->GetAsAtkComponentNode()->Component->UldManager.NodeList;
                    if (listing[13]->GetAsAtkImageNode()->AtkResNode.IsVisible())
                    {
                        var priceText = listing[10]->GetAsAtkTextNode()->NodeText.GetText();
                        if (int.TryParse(AutoRetainerPriceAdjustRegex().Replace(priceText, ""), out CurrentMarketLowestPrice)) foundHQItem = true;
                    }
                }

                if (!foundHQItem)
                {
                    var priceText = addon->UldManager.NodeList[5]->GetAsAtkComponentNode()->Component->UldManager.NodeList[1]->GetAsAtkComponentNode()->Component->UldManager.NodeList[10]->GetAsAtkTextNode()->NodeText.GetText();
                    if (!int.TryParse(AutoRetainerPriceAdjustRegex().Replace(priceText, ""), out CurrentMarketLowestPrice)) return false;
                }
            }
            else
            {
                var priceText = addon->UldManager.NodeList[5]->GetAsAtkComponentNode()->Component->UldManager.NodeList[1]->GetAsAtkComponentNode()->Component->UldManager.NodeList[10]->GetAsAtkTextNode()->NodeText.GetText();
                if (!int.TryParse(AutoRetainerPriceAdjustRegex().Replace(priceText, ""), out CurrentMarketLowestPrice)) return false;
            }

            addon->Close(true);
            return true;
        }

        return false;
    }

    private unsafe bool? FillLowestPrice()
        {
            if (GenericHelpers.TryGetAddonByName<AddonRetainerSell>("RetainerSell", out var addon) && GenericHelpers.IsAddonReady(&addon->AtkUnitBase))
            {
                var ui = &addon->AtkUnitBase;
                var priceComponent = addon->AskingPrice;

                if (CurrentMarketLowestPrice - _config.AutoRetainerListingConfig.PriceReduction < _config.AutoRetainerListingConfig.LowestAcceptablePrice)
                {
                    SeString message;

                    if (CurrentMarketLowestPrice != 0)
                    {
                        message = GetSeString("ItemIsListedLower",
                            SeString.CreateItemLink(CurrentItemSearchItemID,
                                IsCurrentItemHQ ? ItemKind.Hq : ItemKind.Normal),
                            CurrentMarketLowestPrice, CurrentItemPrice,
                            _config.AutoRetainerListingConfig.LowestAcceptablePrice);
                    }
                    else // CurrentMarketLowestPrice == 0
                    {
                        message = GetSeString("MissingItem", SeString.CreateItemLink(CurrentItemSearchItemID));

                    }

                    Svc.Chat.Print(message);
                    PluginLog.Debug(message.TextValue);
                    Callback.Fire((AtkUnitBase*)addon, true, 1);
                    ui->Close(true);
                    return true;
                }


                if (_config.AutoRetainerListingConfig.MaxPriceReduction != 0 &&
                    CurrentItemPrice - CurrentMarketLowestPrice > _config.AutoRetainerListingConfig.MaxPriceReduction)
                {
                    var message = GetSeString("ItemExceededMaxPriceReduction",
                        SeString.CreateItemLink(CurrentItemSearchItemID, IsCurrentItemHQ ? ItemKind.Hq : ItemKind.Normal),
                        CurrentMarketLowestPrice, CurrentItemPrice, _config.AutoRetainerListingConfig.MaxPriceReduction);

                    Svc.Chat.Print(message);
                    Callback.Fire((AtkUnitBase*)addon, true, 1);
                    ui->Close(true);
                    return true;
                }

                priceComponent->SetValue(CurrentMarketLowestPrice - _config.AutoRetainerListingConfig.PriceReduction);
                Callback.Fire((AtkUnitBase*)addon, true, 0);
                ui->Close(true);
                return true;
            }
            return false;
        }

        private static readonly Dictionary<string, string> resourceData = new()
        {
            { "ItemIsListedLower", "{0} is listed lower than minimum price, skipping it (Market: {1}, Minimum in settings: {3})" },
            { "ItemExceededMaxPriceReduction", "Item has exceeded maximum acceptable price reduction, skipping {0} (Lowest: {1}, Current: {2}, Max Reduction: {3})" },
            { "MissingItem", "{0} not found on market, hold left shift to manually post" },
        };

        private static readonly Dictionary<string, string> fbResourceData = new();

        public SeString GetSeString(string key, params object[] args)
        {
            var format = resourceData.TryGetValue(key, out var resValue) ? resValue : fbResourceData.GetValueOrDefault(key);

            if (format == null)
            {
                var itemLink = args.FirstOrDefault(a => a is SeString) as SeString ?? new SeStringBuilder().AddText("Unknown Item").Build();
                format = "{0} not found on market, hold left shift to manually post";
                args = new object[] { itemLink };
            }

            var ssb = new SeStringBuilder();
            int lastIndex = 0;

            ssb.AddUiForeground($"[{nameof(ToshiBox)}]", 48);

            foreach (Match match in SeStringRegex().Matches(format))
            {
                ssb.AddUiForeground(format[lastIndex..match.Index], 2);
                lastIndex = match.Index + match.Length;

                if (int.TryParse(match.Groups[1].Value, out var argIndex) && argIndex >= 0 && argIndex < args.Length)
                {
                    if (args[argIndex] is SeString seString)
                    {
                        ssb.Append(seString);
                    }
                    else
                    {
                        ssb.AddUiForeground(args[argIndex]?.ToString() ?? string.Empty, 2);
                    }
                }
            }

            ssb.AddUiForeground(format[lastIndex..], 2);

            return ssb.Build();
        }

        [GeneratedRegex("\\{(\\d+)\\}")]
        private static partial Regex SeStringRegex();

        [GeneratedRegex("[^0-9]")]
        private static partial Regex AutoRetainerPriceAdjustRegex();
    }
