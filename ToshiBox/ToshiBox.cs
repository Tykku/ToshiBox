using Dalamud.Game.Command;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin;
using ECommons;
using ECommons.Commands;
using ECommons.Configuration;
using ECommons.DalamudServices;
using ToshiBox.Common;
using ToshiBox.Features;
using ToshiBox.IPC;
using ToshiBox.UI;

namespace ToshiBox
{
    public class ToshiBox : IDalamudPlugin
    {
        private readonly WindowSystem _windowSystem = new("ToshiBox");
        private MainWindow _mainWindow;
        public Events EventInstance;
        public Config ConfigInstance;
        public AutoRetainerListing AutoRetainerListingInstance;
        public AutoChestOpen AutoChestOpenInstance;
        private readonly IDalamudPluginInterface _pluginInterface;
        public string Name => "ToshiBox";

        public ToshiBox(IDalamudPluginInterface pluginInterface)
        {
            _pluginInterface = pluginInterface;
            // Initialize ECommons and config
            ECommonsMain.Init(pluginInterface, this);
            EventInstance = new Events();
            ConfigInstance = EzConfig.Init<Config>();

            AutoRetainerListingInstance = new AutoRetainerListing(EventInstance, ConfigInstance);
            AutoRetainerListingInstance.IsEnabled();

            AutoChestOpenInstance = new AutoChestOpen(EventInstance, ConfigInstance);
            AutoChestOpenInstance.IsEnabled();

            PandoraIPC.Init();

            _mainWindow = new MainWindow(AutoRetainerListingInstance, AutoChestOpenInstance, ConfigInstance);
            _windowSystem.AddWindow(_mainWindow);

            _pluginInterface.UiBuilder.Draw += _windowSystem.Draw;
            _pluginInterface.UiBuilder.OpenConfigUi += () => _mainWindow.IsOpen = true;
            _pluginInterface.UiBuilder.OpenMainUi += () => _mainWindow.IsOpen = true;

            Svc.Commands.AddHandler("/toshibox", new CommandInfo(OnCommand)
            {
                HelpMessage = "Opens main settings window"
            });
            Svc.Commands.AddHandler("/toshi", new CommandInfo(OnCommand)
            {
                HelpMessage = "Opens main settings window"
            });
        }

        public void OnCommand(string command, string args)
        {
            if (string.Equals(args, "toggleshangriladida009"))
            {
                ConfigInstance.AutoRetainerListingConfig.Enabled = !ConfigInstance.AutoRetainerListingConfig.Enabled;
                AutoRetainerListingInstance.IsEnabled();
                EzConfig.Save();
            }

            if (string.Equals(args, "colors"))
            {
                var ssb = new SeStringBuilder();
                for (ushort i = 0; i <= 50; i++) {
                    ssb.AddUiForeground($"Color ID {i} ", i);
                    ssb.AddText("\n");
                }
                Svc.Chat.Print(ssb.BuiltString);
            }
            else
            {
                _mainWindow.IsOpen = !_mainWindow.IsOpen;
            }
        }

        public void Dispose()
        {
            AutoRetainerListingInstance.Disable();
            PandoraIPC.Dispose();
            _pluginInterface.UiBuilder.Draw -= _windowSystem.Draw;
            _pluginInterface.UiBuilder.OpenConfigUi -= () => _mainWindow.IsOpen = true;
            _pluginInterface.UiBuilder.OpenMainUi -= () => _mainWindow.IsOpen = true;

            Svc.Commands.RemoveHandler("/toshibox");
            Svc.Commands.RemoveHandler("/toshi");

            ECommonsMain.Dispose(); // LAST
        }
    }
}
