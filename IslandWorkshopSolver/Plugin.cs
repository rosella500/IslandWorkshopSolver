using Dalamud.Game.Command;
using Dalamud.IoC;
using Dalamud.Plugin;
using System.IO;
using System.Reflection;
using Dalamud.Interface.Windowing;
using IslandWorkshopSolver.Windows;

namespace IslandWorkshopSolver
{
    public sealed class Plugin : IDalamudPlugin
    {
        public string Name => "Island Sanctuary Workshop Solver";
        private const string CommandName = "/workshop";

        private DalamudPluginInterface PluginInterface { get; init; }
        private CommandManager CommandManager { get; init; }
        public Configuration Configuration { get; init; }
        public WindowSystem WindowSystem = new("WorkshopSolver");

        public Plugin(
            [RequiredVersion("1.0")] DalamudPluginInterface pluginInterface,
            [RequiredVersion("1.0")] CommandManager commandManager)
        {
            PluginInterface = pluginInterface;
            CommandManager = commandManager;

            Configuration = PluginInterface.GetPluginConfig() as Configuration ?? new Configuration();
            Configuration.Initialize(PluginInterface);
            Dalamud.Initialize(PluginInterface);
            Reader reader = new Reader(this.PluginInterface);

            WindowSystem.AddWindow(new ConfigWindow(this, PluginInterface.AssemblyLocation.Directory?.FullName!));
            WindowSystem.AddWindow(new MainWindow(this, reader));

            CommandManager.AddHandler(CommandName, new CommandInfo(OnCommand)
            {
                HelpMessage = "Open up the Island Sanctuary Solver menu."
            });

            PluginInterface.UiBuilder.Draw += DrawUI;
            PluginInterface.UiBuilder.OpenConfigUi += DrawConfigUI;
        }

        public void Dispose()
        {
            WindowSystem.RemoveAllWindows();
            CommandManager.RemoveHandler(CommandName);
        }

        private void OnCommand(string command, string args)
        {
            // in response to the slash command, just display our main ui
            if (WindowSystem != null)
            {
                var window = WindowSystem.GetWindow("Island Sanctuary Workshop Solver");
                if (window != null)
                {
                    window.IsOpen = true;
                }
            }
        }

        private void DrawUI()
        {
            WindowSystem.Draw();
        }

        public void DrawConfigUI()
        {
            if (WindowSystem != null)
            {
                var window = WindowSystem.GetWindow("Island Sanctuary Solver Configuration");
                if (window != null)
                {
                    window.IsOpen = true;
                }
            }
        }
    }
}
