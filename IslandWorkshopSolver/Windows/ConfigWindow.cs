using System;
using System.Numerics;
using Dalamud.Interface.Windowing;
using ImGuiNET;

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
        ImGui.PushItemWidth(140);
        if (ImGui.CollapsingHeader("Island Configuration", ImGuiTreeNodeFlags.DefaultOpen))
        {
            int rank = Configuration.islandRank;
            if (ImGui.InputInt("Island rank", ref rank))
            {
                Configuration.islandRank = rank;
                Configuration.Save();
            }
            ImGui.Spacing();
            int currentLevel = (Configuration.workshopBonus - 100) / 10;

            if (ImGui.Combo("Workshop level", ref currentLevel, new string[3] { "Workshop I", "Workshop II", "Workshop III" }, 3))
            {
                Configuration.workshopBonus = currentLevel * 10 + 100;
                Configuration.Save();
            }
            ImGui.Spacing();
            int groove = Configuration.maxGroove;
            if (ImGui.InputInt("Max groove", ref groove))
            {
                Configuration.maxGroove = groove;
                Configuration.Save();
            }
        }
        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();
        if (ImGui.CollapsingHeader("Solver Configuration", ImGuiTreeNodeFlags.DefaultOpen))
        {
            int sugg = Configuration.suggestionsToShow;
            if (ImGui.InputInt("Suggestions to show", ref sugg))
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
            if (ImGui.IsItemHovered())
            {
                ImGui.SetTooltip("Material weight is how much you care about the sale price of rare mats." +
                    "\n1 means \"I sell all excess mats and only care about total cowries.\"" +
                    "\n0 means \"I only care about getting the highest workshop revenue.\"" +
                    "\n0.5 is a nice balance. Ctrl + click to type an exact value");
            }
            ImGui.Spacing();
            bool showNet = Configuration.showNetCowries;
            if (ImGui.Checkbox("Show net cowries", ref showNet))
            {
                Configuration.showNetCowries = showNet;
                Configuration.Save();
            }
            if (ImGui.IsItemHovered())
            {
                ImGui.SetTooltip("Net cowries is workshop revenue minus the value you would've gotten from exporting rare materials directly");
            }

            bool enforceRest = Configuration.enforceRestDays;
            if (ImGui.Checkbox("Enforce rest days", ref enforceRest))
            {
                Configuration.enforceRestDays = enforceRest;
                Configuration.Save();
            }
            if (ImGui.IsItemHovered())
            {
                ImGui.SetTooltip("Force you to rest one and only one day (other than day 1).");
            }
        }
        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();
        if (ImGui.CollapsingHeader("Advanced Configuration"))
        {
            string flavorText = String.Join(", ", Configuration.flavorText);
            if (ImGui.InputText("Words to not display", ref flavorText, 100))
            {
                Configuration.flavorText = flavorText.Split(",");
                for (int i = 0; i < Configuration.flavorText.Length; i++)
                    Configuration.flavorText[i] = Configuration.flavorText[i].Trim();
                Configuration.Save();
            }
            if (ImGui.IsItemHovered())
            {
                ImGui.SetTooltip("A list of extra words, separated by commas, to ignore when displaying the names of items.");
            }
            if (Configuration.day == 0 && Configuration.unknownD2Items != null && Configuration.unknownD2Items.Count > 0)
            {
                ImGui.Spacing();
                ImGui.Text(Solver.Solver.GetD2PeakDesc());
                ImGui.Spacing();
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
}
