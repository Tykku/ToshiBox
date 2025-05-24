using Dalamud.Plugin;
using ECommons;
using ToshiBox.Common;
using ToshiBox.Features;

namespace ToshiBox;

public class ToshiBox : IDalamudPlugin
{
    public ToshiBox(IDalamudPluginInterface pluginInterface)
    {
        ECommonsMain.Init(pluginInterface, this);
        EventInstance = new Events();
        ConfigInstance = Config.LoadConfig();
        AutoRetainerListingInstance = new AutoRetainerListing(EventInstance, ConfigInstance);
        AutoRetainerListingInstance.Enable();
    }

    public Events EventInstance;

    public Config ConfigInstance;

    public AutoRetainerListing AutoRetainerListingInstance;
    public void Dispose()
    {
        AutoRetainerListingInstance.Disable();
        ECommonsMain.Dispose();
    }
}