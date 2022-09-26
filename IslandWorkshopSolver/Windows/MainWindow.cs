using System;
using System.Numerics;
using Dalamud.Interface.Windowing;
using ImGuiNET;
using ImGuiScene;

namespace IslandWorkshopSolver.Windows;

public class MainWindow : Window, IDisposable
{
    private Plugin Plugin;
    private string rootPath;

    public MainWindow(Plugin plugin, string root) : base(
        "Island Sanctuary Workshop Solver", ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse)
    {
        rootPath = root;
        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(375, 330),
            MaximumSize = new Vector2(float.MaxValue, float.MaxValue)
        };

        Plugin = plugin;
    }

    public void Dispose()
    {
       
    }

    public override void Draw()
    {
        ImGui.Text($"The random config bool is {Plugin.Configuration.SomePropertyToBeSavedAndWithADefault}");

        if (ImGui.Button("Show HashSettings"))
        {
            Plugin.DrawConfigUI();
        }

        ImGui.Spacing();
        if (ImGui.Button("Run Solver"))
        {
            Dalamud.Chat.Print("Hitting button, "+rootPath);
            try
            {
                Solver.Solver.RunSolver(rootPath);
            }
            catch(Exception e)
            {
                Dalamud.Chat.PrintError(e.GetType() + ": " + e.Message + "\n" + e.StackTrace);
            }
        }
    }
}
