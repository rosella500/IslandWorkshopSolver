using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Reflection.PortableExecutable;
using Dalamud.Interface.Windowing;
using ImGuiNET;
using ImGuiScene;
using IslandWorkshopSolver.Solver;

namespace IslandWorkshopSolver.Windows;

public class MainWindow : Window, IDisposable
{
    private Plugin Plugin;
    private Reader reader;
    private Configuration config;
    private Dictionary<int,SuggestedSchedules?> scheduleSuggestions;

    public MainWindow(Plugin plugin, Reader reader) : base(
        "Island Sanctuary Workshop Solver")
    {
        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(375, 330),
            MaximumSize = new Vector2(float.MaxValue, float.MaxValue)
        };

        Plugin = plugin;
        this.reader = reader;
        config = plugin.Configuration;
        scheduleSuggestions = new Dictionary<int, SuggestedSchedules?>();
    }

    public void Dispose()
    {
       
    }

    public override void Draw()
    {
        if (ImGui.Button("Settings"))
        {
            Plugin.DrawConfigUI();
        }
        ImGui.SameLine();
        ImGui.Text("Cowries Earned: " + Solver.Solver.totalGross);

        ImGui.Spacing();

        if (ImGui.Button("Import supply"))
        {
            string[] products = reader.ExportIsleData().Split('\n', StringSplitOptions.None);
           
            try
            {
                Solver.Solver.Init(config);
                Solver.Solver.writeTodaySupply(products);
            }
            catch (Exception e)
            {
                Dalamud.Chat.PrintError(e.GetType() + ": " + e.Message + "\n" + e.StackTrace);
            }
        }
        ImGui.SameLine();
        if (ImGui.Button("Run Solver"))
        {
            //Dalamud.Chat.Print("Hitting button, "+rootPath);
            try
            {
                Solver.Solver.Init(config);
                List<(int day, SuggestedSchedules? sch)>? schedules = Solver.Solver.RunSolver();
                if(schedules!= null)
                {
                    foreach(var schedule in schedules)
                    {
                        scheduleSuggestions.Remove(schedule.day);
                        scheduleSuggestions.Add(schedule.day, schedule.sch);
                    }
                }
                
            }
            catch(Exception e)
            {
                Dalamud.Chat.PrintError(e.GetType() + ": " + e.Message + "\n" + e.StackTrace);
            }
        }
        ImGui.Spacing();

        // Create a new table to show relevant data.
        if(scheduleSuggestions.Count > 0 && ImGui.BeginTabBar("Suggested Schedules"))
        {
            foreach(var schedule in scheduleSuggestions)
            {
                if(ImGui.BeginTabItem("Day " + (schedule.Key + 1)))
                {
                    if(schedule.Value != null)
                    {
                        if (ImGui.BeginTable("Options", 4))
                        {
                            /*ImGui.TableSetupColumn("Confirmed", ImGuiTableColumnFlags.WidthStretch);*/
                            ImGui.TableSetupColumn("Select?", ImGuiTableColumnFlags.WidthFixed, 50);
                            ImGui.TableSetupColumn("Per Workshop", ImGuiTableColumnFlags.WidthFixed, 100);
                            ImGui.TableSetupColumn("Crafts to Make", ImGuiTableColumnFlags.WidthStretch, 250);
                            ImGui.TableHeadersRow();

                            var enumerator = schedule.Value.orderedSuggestions.GetEnumerator();
                            for (int i=0; i< config.suggestionsToShow && enumerator.MoveNext(); i++)
                            {
                                var suggestion = enumerator.Current;
                                int column = 0;
                                ImGui.TableNextRow();
                                /*ImGui.TableSetColumnIndex(column++);
                                ImGui.Text((!suggestion.Key.hasAnyUnsurePeaks()).ToString());*/
                                ImGui.TableSetColumnIndex(column++);
                                if(ImGui.Button("Select"))
                                {
                                    Solver.Solver.addDay(suggestion.Key.getItems(), schedule.Key);
                                }
                                ImGui.TableSetColumnIndex(column++);
                                ImGui.Text(suggestion.Value.ToString());
                                ImGui.TableSetColumnIndex(column++);
                                ImGui.Text(String.Join(", ", suggestion.Key.getItems()));
                            }
                            ImGui.EndTable();
                        }
                    }
                    else
                    {
                        ImGui.Text("Rest!!!");
                    }
                    ImGui.EndTabItem();
                }
            }
            ImGui.EndTabBar();
            ImGui.Separator();
        }
    }
}
