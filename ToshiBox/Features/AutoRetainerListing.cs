using System.Text.RegularExpressions;
using Dalamud.Game.Addon.Lifecycle;
using Dalamud.Game.Addon.Lifecycle.AddonArgTypes;
using Dalamud.Game.Network.Structures;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Game.Text.SeStringHandling.Payloads;
using Dalamud.Memory;
using ECommons;
using ECommons.Automation;
using ECommons.Automation.NeoTaskManager;
using ECommons.DalamudServices;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using FFXIVClientStructs.FFXIV.Component.GUI;
using ImGuiNET;
using ToshiBox.Common;

namespace ToshiBox.Features;

public partial class AutoRetainerListing
{
    private readonly Events _events;
    private readonly Config _config;
    private readonly TaskManager taskManager;
    
    public AutoRetainerListing(Common.Events events, Config config)
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
        if (_config.MarketAdjusterConfiguration.Enabled)
        {
            Enable();
        }
        else
        {
            Disable();
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

    private void EnqueueSingleItem(int index)
    {
        taskManager.Enqueue(() => !SearchRunning);
        taskManager.Enqueue(() => ClickSellingItem(index));
        taskManager.EnqueueDelay(100);
        taskManager.Enqueue(ClickAdjustPrice);
        taskManager.EnqueueDelay(100);
        taskManager.Enqueue(ClickComparePrice);
        taskManager.EnqueueDelay(500);
        taskManager.DefaultConfiguration.AbortOnTimeout = false;
        taskManager.Enqueue(GetLowestPrice);
        taskManager.DefaultConfiguration.AbortOnTimeout = true;
        taskManager.EnqueueDelay(100);
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

            if (_config.MarketAdjusterConfiguration.SeparateNQAndHQ && IsCurrentItemHQ)
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

            if (CurrentMarketLowestPrice - _config.MarketAdjusterConfiguration.PriceReduction < _config.MarketAdjusterConfiguration.LowestAcceptablePrice)
            {
                var message = GetSeString("Item is listed lower than minimum price, skipping", SeString.CreateItemLink(CurrentItemSearchItemID, IsCurrentItemHQ ? ItemPayload.ItemKind.Hq : ItemPayload.ItemKind.Normal), CurrentMarketLowestPrice, CurrentItemPrice, _config.MarketAdjusterConfiguration.LowestAcceptablePrice);
                Svc.Chat.Print(message);

                Callback.Fire((AtkUnitBase*)addon, true, 1);
                ui->Close(true);

                return true;
            }

            if (_config.MarketAdjusterConfiguration.MaxPriceReduction != 0 &&
                CurrentItemPrice - CurrentMarketLowestPrice > _config.MarketAdjusterConfiguration.LowestAcceptablePrice)
            {
                var message = GetSeString("Item has exceeded maximum acceptable price reduction, skipping",
                    SeString.CreateItemLink(CurrentItemSearchItemID,
                        IsCurrentItemHQ ? ItemPayload.ItemKind.Hq : ItemPayload.ItemKind.Normal),
                    CurrentMarketLowestPrice, CurrentItemPrice, _config.MarketAdjusterConfiguration.MaxPriceReduction);
                Svc.Chat.Print(message);

                Callback.Fire((AtkUnitBase*)addon, true, 1);
                ui->Close(true);

                return true;
            }

            priceComponent->SetValue(CurrentMarketLowestPrice - _config.MarketAdjusterConfiguration.PriceReduction);
            Callback.Fire((AtkUnitBase*)addon, true, 0);
            ui->Close(true);

            return true;
        }

        return false;
    }

    private readonly Dictionary<string, string> resourceData = new Dictionary<string, string>();
    private readonly Dictionary<string, string> fbResourceData =new Dictionary<string, string>();

    public SeString GetSeString(string key, params object[] args)
    {
        var format = resourceData.TryGetValue(key, out var resValue) ? resValue : fbResourceData.GetValueOrDefault(key);
        var ssb = new SeStringBuilder();
        var lastIndex = 0;

        if (format == null)
        {
            // Expecting at least one SeString argument
            var itemLink = args.FirstOrDefault(a => a is SeString) as SeString;
            if (itemLink == null) itemLink = new SeStringBuilder().AddText("Unknown Item").Build();

            format = "{0} not found on market, hold left shift to manually post";
            args = new object[] { itemLink }; // Use only the item link
        }

        ssb.AddUiForeground($"[{nameof(ToshiBox)}]", 34);

        foreach (var match in SeStringRegex().Matches(format).Cast<Match>())
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
    
