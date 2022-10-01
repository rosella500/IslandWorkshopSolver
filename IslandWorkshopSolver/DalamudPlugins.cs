using Dalamud.Data;
using Dalamud.Game.Gui;
using Dalamud.IoC;
using Dalamud.Plugin;

// ReSharper disable AutoPropertyCanBeMadeGetOnly.Local

namespace IslandWorkshopSolver;

public class DalamudPlugins
{
    // Thanks to Otter!
    public static void Initialize(DalamudPluginInterface pluginInterface)
        => pluginInterface.Create<DalamudPlugins>();

    // @formatter:off
    [PluginService][RequiredVersion("1.0")] public static DataManager GameData { get; private set; } = null!;
    [PluginService][RequiredVersion("1.0")] public static ChatGui Chat { get; private set; } = null!;
    // @formatter:on
}
