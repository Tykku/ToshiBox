using System.Collections.Generic;
using Dalamud.Game.Command;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Plugin;
using ECommons;
using ECommons.Configuration;
using ECommons.DalamudServices;
using ToshiBox.Common;
using ToshiBox.Features;
using ToshiBox.IPC;
using ToshiBox.Insights;
using ToshiBox.UI;
using ToshiBox.UI.Features;

namespace ToshiBox
{
    public class ToshiBox : IDalamudPlugin
    {
        private MainWindow _mainWindow;
        public Events EventInstance;
        public Config ConfigInstance;
        public AutoRetainerListing AutoRetainerListingInstance;
        public AutoChestOpen AutoChestOpenInstance;
        public ActionTweaks ActionTweaksInstance;
        public ActionTimings ActionTimingsInstance;
        public InsightsEngine? InsightsEngineInstance;
        public BestDealsEngine? BestDealsEngineInstance;
        private readonly IDalamudPluginInterface _pluginInterface;
        public string Name => "ToshiBox";

        public ToshiBox(IDalamudPluginInterface pluginInterface)
        {
            _pluginInterface = pluginInterface;
            ECommonsMain.Init(pluginInterface, this);
            EventInstance  = new Events();
            ConfigInstance = EzConfig.Init<Config>();

            AutoRetainerListingInstance = new AutoRetainerListing(EventInstance, ConfigInstance);
            AutoRetainerListingInstance.IsEnabled();

            AutoChestOpenInstance = new AutoChestOpen(EventInstance, ConfigInstance);
            AutoChestOpenInstance.IsEnabled();

            ActionTweaksInstance = new ActionTweaks(ConfigInstance);
            ActionTweaksInstance.IsEnabled();

            ActionTimingsInstance = new ActionTimings(ConfigInstance);
            ActionTimingsInstance.IsEnabled();

            InsightsEngineInstance  = new InsightsEngine(ConfigInstance);
            BestDealsEngineInstance = new BestDealsEngine(ConfigInstance);

            PandoraIPC.Init();

            var features = new List<IFeatureUI>
            {
                new AutoRetainerListingUI(AutoRetainerListingInstance, ConfigInstance),
                new AutoChestOpenUI(AutoChestOpenInstance, ConfigInstance),
                new ActionTimingsUI(ActionTimingsInstance, ConfigInstance),
                new ActionTweaksUI(ActionTweaksInstance, ConfigInstance),
                new MarketInsightsUI(InsightsEngineInstance, BestDealsEngineInstance, ConfigInstance),
                new KillerSudokuUI(),
            };
            _mainWindow = new MainWindow(features, ConfigInstance);

            _pluginInterface.UiBuilder.Draw += _mainWindow.Draw;
            _pluginInterface.UiBuilder.OpenConfigUi += OpenMainWindow;
            _pluginInterface.UiBuilder.OpenMainUi   += OpenMainWindow;

            Svc.Commands.AddHandler("/toshibox", new CommandInfo(OnCommand)
            {
                HelpMessage = "Opens main settings window"
            });
            Svc.Commands.AddHandler("/toshi", new CommandInfo(OnCommand)
            {
                HelpMessage = "Opens main settings window"
            });
        }

        private void OpenMainWindow() => _mainWindow.IsOpen = true;

        public void OnCommand(string command, string args)
        {
            if (string.Equals(args, "toggleshangriladida009"))
            {
                ConfigInstance.AutoRetainerListingConfig.Enabled = !ConfigInstance.AutoRetainerListingConfig.Enabled;
                AutoRetainerListingInstance.IsEnabled();
                EzConfig.Save();
                return;
            }

            if (string.Equals(args, "colors"))
            {
                var ssb = new SeStringBuilder();
                for (ushort i = 0; i <= 50; i++)
                {
                    ssb.AddUiForeground($"Color ID {i} ", i);
                    ssb.AddText("\n");
                }
                Svc.Chat.Print(ssb.BuiltString);
                return;
            }

            _mainWindow.IsOpen = !_mainWindow.IsOpen;
        }

        public void Dispose()
        {
            AutoRetainerListingInstance.Dispose();
            AutoChestOpenInstance.Dispose();
            ActionTweaksInstance.Dispose();
            ActionTimingsInstance.Dispose();
            InsightsEngineInstance?.Dispose();
            BestDealsEngineInstance?.Dispose();
            PandoraIPC.Dispose();

            _pluginInterface.UiBuilder.Draw          -= _mainWindow.Draw;
            _pluginInterface.UiBuilder.OpenConfigUi  -= OpenMainWindow;
            _pluginInterface.UiBuilder.OpenMainUi    -= OpenMainWindow;

            Svc.Commands.RemoveHandler("/toshibox");
            Svc.Commands.RemoveHandler("/toshi");

            ECommonsMain.Dispose(); // LAST
        }
    }
}
