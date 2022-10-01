using System;
using System.Collections.Generic;
using System.Numerics;
using System.Text;
using Dalamud.Interface.Windowing;
using ImGuiNET;
using Dalamud.Logging;
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
            else
            {
                PluginLog.LogError("Failed to int today's supply. Init step wrong? No product info??");
            }
        }
        catch (Exception e)
        {
            PluginLog.LogError(e, "Error opening window and writing supply/initing");
        }


    }

    public void Dispose()
    {
       
    }

    private string JoinItems(string delimiter, List<Item> items)
    {
        StringBuilder sb = new StringBuilder();
        for (int i = 0; i < items.Count; i++)
        {
            sb.Append(ItemHelper.GetDisplayName(items[i]));
            if (i < items.Count - 1)
                sb.Append(delimiter);
        }
        return sb.ToString();
    }

    private void AddNewSuggestions(List<(int day, SuggestedSchedules? sch)>? schedules)
    {
        for (int c = 0; c < selectedSchedules.Length; c++)
            selectedSchedules[c] = -1;

        if (schedules != null)
        {
            foreach (var schedule in schedules)
            {
                scheduleSuggestions.Remove(schedule.day);
                scheduleSuggestions.Add(schedule.day, schedule.sch);
            }
        }

        foreach (var schedule in scheduleSuggestions)
        {
            int day = schedule.Key;
            if (Solver.Solver.schedulesPerDay.ContainsKey(day) && schedule.Value != null)
            {
                int i = 0;
                foreach (var suggestion in schedule.Value.orderedSuggestions)
                {
                    if (suggestion.Key.hasSameCrafts(Solver.Solver.schedulesPerDay[day].schedule.workshops[0]))
                    {
                        selectedSchedules[day] = i;
                        break;
                    }
                    i++;
                }
            }
        }
    }

    public override void Draw()
    {
        try
        {
            if (ImGui.Button("Run Solver"))
            {                
                try
                {
                    Solver.Solver.Init(config);
                    List<(int day, SuggestedSchedules? sch)>? schedules = Solver.Solver.RunSolver();
                    AddNewSuggestions(schedules);

                }
                catch (Exception e)
                {
                    PluginLog.LogError(e, "Error running solver.");
                }
            }
            ImGui.SameLine(100);
            string totalCowries = "Total cowries this season: " + Solver.Solver.totalGross;
            if (config.showNetCowries)
                totalCowries += " (" + Solver.Solver.totalNet + " net)";
            ImGui.Text(totalCowries);

            ImGui.GetContentRegionAvail();
            ImGui.SameLine(ImGui.GetContentRegionAvail().X - 40);
            if (ImGui.Button("Config"))
            {
                Plugin.DrawConfigUI();
            }
            ImGui.Spacing();

            // Create a new table to show relevant data.
            if ((scheduleSuggestions.Count > 0 || endDaySummaries.Count > 0) && ImGui.BeginTabBar("Workshop Schedules"))
            {
                for (int day = 0; day < 7; day++)
                {
                    if (day <= Solver.Solver.currentDay && endDaySummaries.Count > day)
                    {
                        if (ImGui.BeginTabItem("Day " + (day + 1)))
                        {
                            string title = "Crafted";
                            if (day == Solver.Solver.currentDay)
                                title = "Scheduled";
                            if (endDaySummaries[day].totalCraftedItems() > 0 && ImGui.BeginTable(title, 3))
                            {
                                ImGui.TableSetupColumn("Product", ImGuiTableColumnFlags.WidthFixed, 180);
                                ImGui.TableSetupColumn("Qty.", ImGuiTableColumnFlags.WidthFixed, 100);
                                ImGui.TableSetupColumn("Value", ImGuiTableColumnFlags.WidthFixed, 100);
                                ImGui.TableHeadersRow();


                                for (int i = 0; i < endDaySummaries[day].crafts.Count; i++)
                                {
                                    int column = 0;
                                    ImGui.TableNextRow();
                                    ImGui.TableSetColumnIndex(column++);
                                    ImGui.Text(ItemHelper.GetDisplayName(endDaySummaries[day].crafts[i]));
                                    ImGui.TableSetColumnIndex(column++);
                                    ImGui.Text((i==0?3:6).ToString()); //I'm just hard-coding in that these are efficient, idgaf
                                    ImGui.TableSetColumnIndex(column++);
                                    if(i < endDaySummaries[day].valuesPerCraft.Count)
                                        ImGui.Text(endDaySummaries[day].valuesPerCraft[i].ToString());
                                }
                                ImGui.EndTable();
                                ImGui.Spacing();


                                ImGui.Text("This day's value: " + endDaySummaries[day].endingGross);
                                ImGui.SameLine(200);
                                ImGui.Text("Used material value: " + (endDaySummaries[day].endingGross - endDaySummaries[day].endingNet));
                            }
                            else
                            {
                                if(day==Solver.Solver.currentDay)
                                    ImGui.Text("Resting");
                                else
                                    ImGui.Text("Rested");

                            }
                            ImGui.EndTabItem();
                        }
                    }
                    else if (scheduleSuggestions.ContainsKey(day))
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
                                    ImGui.TableSetupColumn("Weighted Value", ImGuiTableColumnFlags.WidthFixed, 100);
                                    ImGui.TableSetupColumn("Products to Make", ImGuiTableColumnFlags.WidthStretch, 250);
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
                                            if (scheduleSuggestions.Count > 1) //If we have multiple days, committing to this probably changed something
                                                AddNewSuggestions(Solver.Solver.calculateLastTwoDays());
                                        }
                                        ImGui.TableSetColumnIndex(column++);
                                        ImGui.Text(suggestion.Value.ToString());
                                        ImGui.TableSetColumnIndex(column++);
                                        if (suggestion.Key.getNumCrafts() > 0)
                                            ImGui.Text(JoinItems(" - ", suggestion.Key.getItems()));
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
        catch (Exception e)
        {
            PluginLog.LogError(e, "Error displaying schedule data.");
        }
    }
}
