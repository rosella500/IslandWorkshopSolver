using System;
using System.Collections.Generic;
using System.Data.Common;
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
    private List<EndDaySummary> endDaySummaries;
    private int[] selectedSchedules = new int[7];

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
        Solver.Solver.Init(config);

        
        scheduleSuggestions = new Dictionary<int, SuggestedSchedules?>();
        endDaySummaries = Solver.Solver.importer.endDays;
    }

    public override void OnOpen()
    {
        string[] products = reader.ExportIsleData().Split('\n', StringSplitOptions.None);
        try
        {
            if(Solver.Solver.writeTodaySupply(products))
            {
                Solver.Solver.InitAfterWritingTodaysData();

                endDaySummaries = Solver.Solver.importer.endDays;

                base.OnOpen();
            }
        }
        catch (Exception e)
        {
            Dalamud.Chat.PrintError(e.GetType() + ": " + e.Message + "\n" + e.StackTrace);
        }
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
        ImGui.Text("Total Cowries this season: " + Solver.Solver.totalGross);
        ImGui.Spacing();
        if (ImGui.Button("Run Solver"))
        {
            //Dalamud.Chat.Print("Hitting button, "+rootPath);
            for(int c=0; c<selectedSchedules.Length;c++)
                selectedSchedules[c] = -1;
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
        if((scheduleSuggestions.Count > 0 || endDaySummaries.Count > 0) && ImGui.BeginTabBar("Workshop Schedules"))
        {
            for(int day=0;day<7;day++)
            {
                if(day <= Solver.Solver.currentDay && endDaySummaries.Count > day)
                {
                    if (ImGui.BeginTabItem("Day " + (day + 1)))
                    {
                        if (endDaySummaries[day].totalCraftedItems() > 0 && ImGui.BeginTable("Crafted", 4))
                        {
                            /*ImGui.TableSetupColumn("Confirmed", ImGuiTableColumnFlags.WidthStretch);*/
                            ImGui.TableSetupColumn("Item", ImGuiTableColumnFlags.WidthFixed, 200);
                            ImGui.TableSetupColumn("Total crafted", ImGuiTableColumnFlags.WidthStretch, 10);
                            ImGui.TableHeadersRow();

                            for(int i=0;i<Solver.Solver.items.Count; i++)
                            {
                                int column = 0;
                                if (endDaySummaries[day].getCrafted(i) > 0)
                                {
                                    ImGui.TableNextRow();
                                    /*ImGui.TableSetColumnIndex(column++);
                                    ImGui.Text((!suggestion.Key.hasAnyUnsurePeaks()).ToString());*/
                                    ImGui.TableSetColumnIndex(column++);
                                    ImGui.Text(Solver.Solver.items[i].item.ToString());
                                    ImGui.TableSetColumnIndex(column++);
                                    ImGui.Text("" + endDaySummaries[day].getCrafted(i));
                                }
                            }
                            ImGui.EndTable();
                            ImGui.Spacing();

                           
                            ImGui.Text("Earned today: " + endDaySummaries[day].endingGross);
                            ImGui.SameLine(200);
                            ImGui.Text("Used material value: " + (endDaySummaries[day].endingGross - endDaySummaries[day].endingNet));
                        }
                        else
                        {
                            ImGui.Text("Rested");
                        }
                        ImGui.EndTabItem();
                    }
                }
                else if(scheduleSuggestions.ContainsKey(day))
                {
                    var schedule = scheduleSuggestions[day];
                    if (ImGui.BeginTabItem("Day " + (day + 1)))
                    {
                        if (schedule != null)
                        {
                            if (ImGui.BeginTable("Options", 4))
                            {
                                /*ImGui.TableSetupColumn("Confirmed", ImGuiTableColumnFlags.WidthStretch);*/
                                ImGui.TableSetupColumn("Use?", ImGuiTableColumnFlags.WidthFixed, 50);
                                ImGui.TableSetupColumn("Per Workshop", ImGuiTableColumnFlags.WidthFixed, 100);
                                ImGui.TableSetupColumn("Crafts to Make", ImGuiTableColumnFlags.WidthStretch, 250);
                                ImGui.TableHeadersRow();

                                var enumerator = schedule.orderedSuggestions.GetEnumerator();

                                for (int i = 0; i < config.suggestionsToShow && enumerator.MoveNext(); i++)
                                {
                                    var suggestion = enumerator.Current;
                                    int column = 0;
                                    ImGui.TableNextRow();
                                    /*ImGui.TableSetColumnIndex(column++);
                                    ImGui.Text((!suggestion.Key.hasAnyUnsurePeaks()).ToString());*/
                                    ImGui.TableSetColumnIndex(column++);
                                    if (ImGui.RadioButton("##" + (i + 1), ref selectedSchedules[day], i))
                                    {
                                        Solver.Solver.setDay(suggestion.Key.getItems(), day);
                                    }
                                    ImGui.TableSetColumnIndex(column++);
                                    ImGui.Text(suggestion.Value.ToString());
                                    ImGui.TableSetColumnIndex(column++);
                                    if (suggestion.Key.getNumCrafts() > 0)
                                        ImGui.Text(String.Join(", ", suggestion.Key.getItems()));
                                    else
                                        ImGui.Text("Rest");
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
            }
            ImGui.EndTabBar();
            ImGui.Separator();
        }
    }
}
