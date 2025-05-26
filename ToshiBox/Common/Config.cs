using Dalamud.Configuration;
using Dalamud.Plugin;
using Newtonsoft.Json;
using ToshiBox.Features;

namespace ToshiBox.Common;

public class Config : IPluginConfiguration
{
    public int Version { get; set; } = 1;

    public AutoRetainerListing.MarketAdjusterConfiguration MarketAdjusterConfiguration { get; set; } = new();

    [JsonIgnore]
    private IDalamudPluginInterface? _pluginInterface;

    public void Initialize(IDalamudPluginInterface pluginInterface)
    {
        _pluginInterface = pluginInterface;
    }

    public void Save()
    {
        _pluginInterface?.SavePluginConfig(this);
    }

    public static Config Load(IDalamudPluginInterface pluginInterface)
    {
        var config = pluginInterface.GetPluginConfig() as Config ?? new Config();
        config.Initialize(pluginInterface);
        return config;
    }
}
