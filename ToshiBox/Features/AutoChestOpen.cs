using Dalamud.Game.ClientState.Objects.Enums;
using ECommons.DalamudServices;
using ECommons.GameHelpers;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Game.Control;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using FFXIVClientStructs.FFXIV.Component.GUI;
using Lumina.Excel.Sheets;
using System.Numerics;
using Dalamud.Plugin.Services;
using ECommons.Automation.NeoTaskManager;
using ToshiBox.Common;

namespace ToshiBox.Features
{
    public unsafe class AutoChestOpen
    {
        private readonly Config _config;
        private readonly TaskManager taskManager;

        private static DateTime NextOpenTime = DateTime.Now;
        private static ulong LastChestId = 0;
        private static readonly Random Rand = new();
        private static DateTime CloseWindowTime = DateTime.Now;

        private ushort? _lastContentFinderId = null;
        private bool _isHighEndDuty = false;

        public AutoChestOpen(Events events, Config config)
        {
            _config = config;
            taskManager = new TaskManager();
        }

        public void IsEnabled()
        {
            if (_config.AutoRetainerListingConfig.Enabled)
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
            Svc.Framework.Update += RunFeature;
        }

        public void Disable()
        {
            Svc.Framework.Update -= RunFeature;
            taskManager.Abort();
        }

        private void RunFeature(IFramework framework)
        {
            CloseWindow();

            if (Svc.Condition[Dalamud.Game.ClientState.Conditions.ConditionFlag.BetweenAreas])
                return;

            ushort currentContentFinderId = GameMain.Instance()->CurrentContentFinderConditionId;
            if (_lastContentFinderId != currentContentFinderId)
            {
                _lastContentFinderId = currentContentFinderId;
                var sheet = Svc.Data.GetExcelSheet<ContentFinderCondition>();
                if (sheet?.GetRow(currentContentFinderId) is { } row)
                {
                    _isHighEndDuty = row.HighEndDuty;
                }
                else
                {
                    _isHighEndDuty = false;
                }
            }

            if (!_config.AutoChestOpenConfig.OpenInHighEndDuty && _isHighEndDuty)
                return;

            var player = Player.Object;
            if (player == null) return;

            var treasure = Svc.Objects.FirstOrDefault(o =>
            {
                if (o == null) return false;

                var requiredDistance = player.HitboxRadius + o.HitboxRadius + _config.AutoChestOpenConfig.Distance;
                if (Vector3.DistanceSquared(player.Position, o.Position) > requiredDistance * requiredDistance)
                    return false;

                var obj = (FFXIVClientStructs.FFXIV.Client.Game.Object.GameObject*)(void*)o.Address;
                if (!obj->GetIsTargetable()) return false;
                if ((ObjectKind)obj->ObjectKind != ObjectKind.Treasure) return false;

                foreach (var item in Loot.Instance()->Items)
                    if (item.ChestObjectId == o.GameObjectId)
                        return false;

                return true;
            });

            if (treasure == null) return;
            if (DateTime.Now < NextOpenTime) return;
            if (treasure.GameObjectId == LastChestId && DateTime.Now - NextOpenTime < TimeSpan.FromSeconds(10)) return;

            NextOpenTime = DateTime.Now.AddSeconds(_config.AutoChestOpenConfig.Delay + Rand.NextDouble());
            LastChestId = treasure.GameObjectId;

            try
            {
                Svc.Targets.Target = treasure;
                TargetSystem.Instance()->InteractWithObject(
                    (FFXIVClientStructs.FFXIV.Client.Game.Object.GameObject*)(void*)treasure.Address);

                if (_config.AutoChestOpenConfig.CloseLootWindow)
                {
                    CloseWindowTime = DateTime.Now.AddSeconds(0.5);
                }
            }
            catch (Exception ex)
            {
                Svc.Log.Error(ex, "Failed to open the chest!");
            }
        }

        private static void CloseWindow()
        {
            if (CloseWindowTime < DateTime.Now) return;

            var addonPtr = Svc.GameGui.GetAddonByName("NeedGreed", 1);
            if (addonPtr != IntPtr.Zero)
            {
                var needGreedWindow = (AtkUnitBase*)addonPtr;
                if (needGreedWindow != null && needGreedWindow->IsVisible)
                {
                    needGreedWindow->Close(true);
                }
            }
        }
    }
}
