using Dalamud.Interface.Windowing;
using Dalamud.Plugin;
using Dalamud.Game.Command;
using ECommons;
using ToshiBox.Common;
using ToshiBox.Features;
using ToshiBox.UI;
using Dalamud.Plugin.Services;

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
        private readonly ICommandManager _commandManager;

        public string Name => "ToshiBox";

        public ToshiBox(IDalamudPluginInterface pluginInterface, ICommandManager commandManager)
        {
            _pluginInterface = pluginInterface;
            _commandManager = commandManager;

            ECommonsMain.Init(pluginInterface, this);

            EventInstance = new Events();
            ConfigInstance = Config.LoadConfig();

            AutoRetainerListingInstance = new AutoRetainerListing(EventInstance, ConfigInstance);
            AutoRetainerListingInstance.Enable();

            _mainWindow = new MainWindow(AutoRetainerListingInstance, ConfigInstance);
            _windowSystem.AddWindow(_mainWindow);

            pluginInterface.UiBuilder.Draw += _windowSystem.Draw;
            pluginInterface.UiBuilder.OpenConfigUi += () => _mainWindow.IsOpen = true;

            // Register the /toshibox command
            _commandManager.AddHandler("/toshibox", new CommandInfo(OpenConfig)
            {
                HelpMessage = "Open the ToshiBox settings window"
            });
        }

        private void OpenConfig(string command, string args)
        {
            _mainWindow.IsOpen = !_mainWindow.IsOpen;
        }

        public void Dispose()
        {
            AutoRetainerListingInstance.Disable();
            _pluginInterface.UiBuilder.Draw -= _windowSystem.Draw;
            _pluginInterface.UiBuilder.OpenConfigUi -= () => _mainWindow.IsOpen = true;
            _commandManager.RemoveHandler("/toshibox");

            ECommonsMain.Dispose(); // LAST
        }
    }
}