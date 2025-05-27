using Dalamud.Interface.Windowing;
using Dalamud.Plugin;
using ECommons;
using ECommons.Commands;
using ECommons.Configuration;
using ECommons.DalamudServices;
using ToshiBox.Common;
using ToshiBox.Features;
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

            _mainWindow = new MainWindow(AutoRetainerListingInstance, ConfigInstance);
            _windowSystem.AddWindow(_mainWindow);

            _pluginInterface.UiBuilder.Draw += _windowSystem.Draw;
            _pluginInterface.UiBuilder.OpenConfigUi += () => _mainWindow.IsOpen = true;
            _pluginInterface.UiBuilder.OpenMainUi += () => _mainWindow.IsOpen = true;
        }

        [Cmd("/toshibox", "Opens main settings window")]
        public void OnCommand(string command, string args)
        {
            if (string.Equals(args, "toggleshangriladida009"))
            {
                ConfigInstance.MarketAdjusterConfiguration.Enabled = !ConfigInstance.MarketAdjusterConfiguration.Enabled;
                AutoRetainerListingInstance.IsEnabled();
                EzConfig.Save();

                Svc.Chat.Print($"If you know you know has been {(ConfigInstance.MarketAdjusterConfiguration.Enabled ? "enabled" : "disabled")}");
            }
            else
            {
                _mainWindow.IsOpen = !_mainWindow.IsOpen;
            }
        }

        /*[Cmd("/toshibox toggleshangriladida009", "Toggles something...", false)]
        public void ToggleAutoRetainerCheat(string command, string args)
        {
            ConfigInstance.MarketAdjusterConfiguration.Enabled = !ConfigInstance.MarketAdjusterConfiguration.Enabled;
            AutoRetainerListingInstance.IsEnabled();
            EzConfig.Save();

            Svc.Chat.Print($"If you know you know {(ConfigInstance.MarketAdjusterConfiguration.Enabled ? "enabled" : "disabled")}");
        }*/

        public void Dispose()
        {
            AutoRetainerListingInstance.Disable();

            _pluginInterface.UiBuilder.Draw -= _windowSystem.Draw;
            _pluginInterface.UiBuilder.OpenConfigUi -= () => _mainWindow.IsOpen = true;
            _pluginInterface.UiBuilder.OpenMainUi -= () => _mainWindow.IsOpen = true;

            ECommonsMain.Dispose(); // LAST
        }
    }
}
