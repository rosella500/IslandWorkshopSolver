using Dalamud.IoC;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;

// ReSharper disable AutoPropertyCanBeMadeGetOnly.Local

namespace Beachcomber;

public class DalamudPlugins
{
    // Thanks to Otter!
    public static void Initialize(DalamudPluginInterface pluginInterface)
        => pluginInterface.Create<DalamudPlugins>();

    // @formatter:off
    [PluginService][RequiredVersion("1.0")] public static IDataManager GameData { get; private set; } = null!;
    [PluginService][RequiredVersion("1.0")] public static IChatGui Chat { get; private set; } = null!;
    [PluginService][RequiredVersion("1.0")] public static IGameGui GameGui { get; private set; } = null!;
    [PluginService][RequiredVersion("1.0")] public static IPluginLog pluginLog { get; private set; } = null!;
    // @formatter:on
}
