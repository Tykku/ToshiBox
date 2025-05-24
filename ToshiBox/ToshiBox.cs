using Dalamud.Plugin;
using ECommons;
using ToshiBox.Common;
using ToshiBox.Features;
using ToshiBox.Features.Test;

namespace ToshiBox;

public class ToshiBox : IDalamudPlugin
{
    public ToshiBox(IDalamudPluginInterface pluginInterface)
    {
        ECommonsMain.Init(pluginInterface, this);
        HelloInstance = new Hello();
        EventInstance = new Events();
        ConfigInstance = Config.LoadConfig();
        AutoRetainerListingInstance = new AutoRetainerListing(EventInstance, ConfigInstance);
        AutoRetainerListingInstance.Enable();
    }

    public Hello HelloInstance;

    public Events EventInstance;

    public Config ConfigInstance;

    public AutoRetainerListing AutoRetainerListingInstance;
    public void Dispose()
    {
        AutoRetainerListingInstance.Disable();
        ECommonsMain.Dispose();
    }
}