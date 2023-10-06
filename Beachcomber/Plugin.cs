using Dalamud.Game.Command;
using Dalamud.IoC;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using Dalamud.Interface.Windowing;
using Beachcomber.Windows;

namespace Beachcomber
{
    public sealed class Plugin : IDalamudPlugin
    {
        private const string CommandName = "/cowries";

        private DalamudPluginInterface PluginInterface { get; init; }
        private ICommandManager CommandManager { get; init; }
        public Configuration Configuration { get; init; }
        public WindowSystem WindowSystem = new("WorkshopSolver");

        public Window mainWindow;
        public Window configWindow;

        public Plugin(
            [RequiredVersion("1.0")] DalamudPluginInterface pluginInterface,
            [RequiredVersion("1.0")] ICommandManager commandManager)
        {
            PluginInterface = pluginInterface;
            CommandManager = commandManager;

            Configuration = PluginInterface.GetPluginConfig() as Configuration ?? new Configuration();
            Configuration.Initialize(PluginInterface);
            DalamudPlugins.Initialize(PluginInterface);
            Reader reader = new();

            configWindow = new ConfigWindow(this, PluginInterface.GetPluginConfigDirectory());
            WindowSystem.AddWindow(configWindow);
            mainWindow = new MainWindow(this, reader);
            WindowSystem.AddWindow(mainWindow);


            CommandManager.AddHandler(CommandName, new CommandInfo(OnCommand)
            {
                HelpMessage = "Open up the Beachcomber Solver menu."
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
                if (mainWindow != null)
                {
                    mainWindow.IsOpen = true;
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
                if (configWindow != null)
                {
                    configWindow.IsOpen = true;
                }
            }
        }
    }
}
