using System;
using System.Diagnostics;
using System.Numerics;
using Dalamud.Interface.Windowing;
using ImGuiNET;
using Microsoft.VisualBasic;

namespace IslandWorkshopSolver.Windows;

public class ConfigWindow : Window, IDisposable
{
    private Configuration Configuration;

    public ConfigWindow(Plugin plugin, string rootPath) : base(
        "Island Sanctuary Solver Configuration")
    {
        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(300, 200),
            MaximumSize = new Vector2(float.MaxValue, float.MaxValue)
        };

        Configuration = plugin.Configuration;
        Configuration.rootPath = rootPath;
    }

    public void Dispose() { }

    public override void Draw()
    {
        int sugg = Configuration.suggestionsToShow;
        ImGui.PushItemWidth(100);
        if(ImGui.InputInt("Suggestions to show", ref sugg))
        {
            Configuration.suggestionsToShow = sugg;
            Configuration.Save();
        }
        ImGui.Spacing();
        float matWeight = Configuration.materialValue;
        if (ImGui.SliderFloat("Material weight", ref matWeight, 0.0f, 1.0f))
        {
            Configuration.materialValue = matWeight;
            Configuration.Save();
        }
        ImGui.Spacing();
        int rank = Configuration.islandRank;
        if (ImGui.InputInt("Island rank", ref rank))
        {
            Configuration.islandRank = rank;
            Configuration.Save();
        }
        ImGui.Spacing();
        int bonus = Configuration.workshopBonus;
        if (ImGui.InputInt("Workshop bonus", ref bonus))
        {
            Configuration.workshopBonus = bonus;
            Configuration.Save();
        }
        ImGui.Spacing();
        int groove = Configuration.maxGroove;
        if (ImGui.InputInt("Max groove", ref groove))
        {
            Configuration.maxGroove = groove;
            Configuration.Save();
        }
        ImGui.Spacing();

        bool verbCalc = Configuration.verboseCalculatorLogging;
        if (ImGui.Checkbox("Verbose calculator logging ", ref verbCalc))
        {
            Configuration.verboseCalculatorLogging = verbCalc;
            Configuration.Save();
        }
        ImGui.Spacing();

        bool verbSolv = Configuration.verboseSolverLogging;
        if (ImGui.Checkbox("Verbose solver logging ", ref verbSolv))
        {
            Configuration.verboseSolverLogging = verbSolv;
            Configuration.Save();
        }
        ImGui.Spacing();

        bool verbRest = Configuration.verboseRestDayLogging;
        if (ImGui.Checkbox("Verbose rest day logging ", ref verbRest))
        {
            Configuration.verboseRestDayLogging = verbRest;
            Configuration.Save();
        }
        ImGui.Spacing();

        if (Configuration.day == 0 && Configuration.unknownD2Items != null)
        {
            var enumerator = Configuration.unknownD2Items.GetEnumerator();
            while (enumerator.MoveNext())
            {
                bool strong = enumerator.Current.Value;
                if (ImGui.Checkbox(enumerator.Current.Key + " strong? ", ref strong))
                {
                    Configuration.unknownD2Items[enumerator.Current.Key] = strong;
                    Configuration.Save();
                }
                ImGui.Spacing();
            }
        }
    }
}
