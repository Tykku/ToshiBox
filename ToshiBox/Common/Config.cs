using ECommons.DalamudServices;
using Newtonsoft.Json;
using ToshiBox.Features;

namespace ToshiBox.Common;

public class Config
{
    public static string FilePath => Svc.PluginInterface.ConfigFile.FullName;

    public void SaveConfig()
    {
        var json = JsonConvert.SerializeObject(this, Formatting.Indented);
        File.WriteAllText(FilePath, json);
    }

    public static Config LoadConfig()
    {
        if (File.Exists(FilePath))
        {
            var json = JsonConvert.DeserializeObject<Config>(FilePath);
            if (json != null) return json;
        }

        return new Config();
    }

    public AutoRetainerListing.MarketAdjusterConfiguration MarketAdjusterConfiguration { get; set; } =
        new AutoRetainerListing.MarketAdjusterConfiguration();
}