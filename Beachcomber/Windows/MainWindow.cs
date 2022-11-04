using System;
using System.Collections.Generic;
using System.Numerics;
using System.Text;
using Dalamud.Interface.Windowing;
using ImGuiNET;
using Dalamud.Logging;
using Beachcomber.Solver;
using System.Linq;

namespace Beachcomber.Windows;

public class MainWindow : Window, IDisposable
{
    private Plugin Plugin;
    private Reader reader;
    private Configuration config;
    private Dictionary<int,SuggestedSchedules?> scheduleSuggestions;
    private List<EndDaySummary> endDaySummaries;
    private int[] selectedSchedules = new int[7];
    private Dictionary<int, int> inventory = new Dictionary<int, int>();
    private Vector4 yellow = new Vector4(1f, 1f, .3f, 1f);
    private Vector4 green = new Vector4(.3f, 1f, .3f, 1f);
    private Vector4 red = new Vector4(1f, .3f, .3f, 1f);
    private bool showInventoryError = false;
    private bool showSupplyError = false;
    private bool showWorkshopError = false;

    private int makeupValue = 0;
    private int makeupGroove = 0;

    public MainWindow(Plugin plugin, Reader reader) : base(
        "Beachcomber", ImGuiWindowFlags.NoScrollbar)
    {
        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(425, 300),
            MaximumSize = new Vector2(float.MaxValue, float.MaxValue)
        };

        Plugin = plugin;
        this.reader = reader;
        config = plugin.Configuration;
        Solver.Solver.Init(config, this);

        
        scheduleSuggestions = new Dictionary<int, SuggestedSchedules?>();
        endDaySummaries = Solver.Solver.Importer.endDays;
    }

    public override void OnOpen()
    {
        (int day, string data) islandData = reader.ExportIsleData();
        (int maybeRank, int maybeGroove) = reader.GetIslandRankAndMaxGroove();
        bool changedConfig = false;
        if(maybeRank > 0)
        {
            config.islandRank = maybeRank;
            config.maxGroove = maybeGroove;
            changedConfig = true;
        }
        string[] products = islandData.data.Split('\n', StringSplitOptions.None);
        if(reader.GetInventory(out var maybeInv))
            inventory = maybeInv;
        (int maybeWorkshopBonus, bool workshopError) = reader.GetWorkshopBonus();
        if (maybeWorkshopBonus > -1)
        {
            changedConfig = true;
            showWorkshopError = workshopError;
            config.workshopBonus = maybeWorkshopBonus; 
        }
        if(changedConfig)
            config.Save();
        showSupplyError = false;
        try
        {
            if(Solver.Solver.WriteTodaySupply(islandData.day, products))
            {
                Solver.Solver.InitAfterWritingTodaysData();


                base.OnOpen();
            }
            else
            {
                showSupplyError = true;
            }
        }
        catch (Exception e)
        {
            PluginLog.LogError(e, "Error opening window and writing supply/initing");
            DalamudPlugins.Chat.PrintError("Error opening window. See /xllog for more info.");
            IsOpen = false;
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
            scheduleSuggestions.Clear();
            foreach (var schedule in schedules)
            {
                scheduleSuggestions.Add(schedule.day, schedule.sch);
            }
        }

        foreach (var schedule in scheduleSuggestions)
        {
            int day = schedule.Key;
            if (Solver.Solver.SchedulesPerDay.ContainsKey(day) && schedule.Value != null)
            {
                int i = 0;
                foreach (var suggestion in schedule.Value.orderedSuggestions)
                {
                    if (suggestion.Key.HasSameCrafts(Solver.Solver.SchedulesPerDay[day].schedule.workshops[0]))
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
        if(showSupplyError)
        {
            ImGui.TextColored(red, "Can't import supply!! \nPlease talk to the Tactful Taskmaster on your Island Sanctuary, \nopen the Supply/Demand window, then hit this button.");
            ImGui.Spacing();
            if (ImGui.Button("Reimport Supply"))
            {
                OnOpen();
            }
            return;
        }
        if(showWorkshopError)
        {
            ImGui.TextColored(yellow, "Warning: You have a workshop ready to upgrade that has not been confirmed. Please examine all your workshop placards and keep an eye out for excited mammets.");
            ImGui.Spacing();
        }
        try
        {

            endDaySummaries = Solver.Solver.Importer.endDays;
            float buttonWidth = ImGui.GetContentRegionAvail().X / 6;
            if (ImGui.Button("Run Solver", new Vector2(buttonWidth, 0f)))
            {                
                try
                {
                    Solver.Solver.Init(config, this);
                    showInventoryError = false;
                    if (reader.GetInventory(out var maybeInv))
                        inventory = maybeInv;
                    if (config.onlySuggestMaterialsOwned && inventory.Count == 0)
                    {
                        scheduleSuggestions.Clear();
                        showInventoryError = true;
                    }
                    else
                    {
                        List<(int day, SuggestedSchedules? sch)>? schedules = Solver.Solver.RunSolver(inventory);
                        AddNewSuggestions(schedules);
                    }
                    
                }
                catch (Exception e)
                {
                    PluginLog.LogError(e, "Error running solver.");
                }
            }
            ImGui.SameLine(buttonWidth+20);
            string totalCowries = "Total cowries this season: " + Solver.Solver.TotalGross;
            if (config.showNetCowries)
                totalCowries += " (" + Solver.Solver.TotalNet + " net)";
            ImGui.Text(totalCowries);

            ImGui.GetContentRegionAvail();
            
            ImGui.SameLine(ImGui.GetContentRegionAvail().X - buttonWidth + 10);
            if (ImGui.Button("Config", new Vector2(buttonWidth,0f)))
            {
                Plugin.DrawConfigUI();
            }
            ImGui.Spacing();

            if(showInventoryError)
            {
                ImGui.TextColored(red, "Inventory uninitialized. Open your Isleventory and view all tabs");
                ImGui.TextColored(red, "or turn off \"Must have rare materials\" in configs.");
                ImGui.Spacing();
            }

            if (scheduleSuggestions.Count > 1)
            {
                ImGui.TextColored(yellow, "There are suggestions for multiple days available!");
                ImGui.Spacing();
                ImGui.TextWrapped("These schedules affect each other! Select the highest-value schedules first to get better recommendations for the worse day(s).");
                ImGui.Spacing();
            
            }

            // Create a new table to show relevant data.
            if ((scheduleSuggestions.Count > 0 || endDaySummaries.Count > 0 || config.day == 6) && ImGui.BeginTabBar("Workshop Schedules"))
            {
                for (int day = 0; day < 7; day++)
                {
                    if (day <= Solver.Solver.CurrentDay && endDaySummaries.Count > day)
                    {
                        if (ImGui.BeginTabItem("Day " + (day + 1)))
                        {
                            string title = "Crafted";
                            if (day == Solver.Solver.CurrentDay)
                                title = "Scheduled";
                            if (endDaySummaries[day].crafts.Count > 0 && ImGui.BeginTable(title, 3))
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
                            else if (endDaySummaries[day].endingGross > 0)
                            {
                                int grooveYesterday = 0;
                                if (day > 0)
                                {
                                    grooveYesterday = endDaySummaries[day - 1].endingGroove;
                                }
                                int grooveToday = endDaySummaries[day].endingGroove - grooveYesterday;

                                ImGui.Text("Made " + endDaySummaries[day].endingGross + " cowries and " + grooveToday + " groove");
                                
                            }
                            else
                            {
                                if(day==Solver.Solver.CurrentDay)
                                    ImGui.Text("Resting");
                                else
                                    ImGui.Text("Rested");

                                if(day > 0 && config.allowOverwritingDays)
                                {
                                    ImGui.Spacing();
                                    ImGui.Text("Is this wrong? Please enter your value and groove for the day");
                                    ImGui.PushItemWidth(200);
                                    ImGui.Spacing();
                                    ImGui.InputInt("Total cowries", ref makeupValue);
                                    ImGui.Spacing();
                                    ImGui.InputInt("Groove generated", ref makeupGroove);
                                    ImGui.Spacing();
                                    if (ImGui.Button("Save"))
                                    {
                                        PluginLog.Debug("Adding stub value");
                                        Solver.Solver.AddStubValue(day, makeupGroove, makeupValue);
                                        makeupGroove = 0;
                                        makeupValue = 0;
                                    }
                                    ImGui.PopItemWidth();
                                }
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
                                if(selectedSchedules[day] >= 0)
                                {
                                    var matsRequired = Solver.Solver.GetScheduledMatsNeeded();
                                    if (matsRequired != null)
                                    {
                                        ImGui.Spacing();
                                        if(inventory.Count == 0)
                                        {
                                            ImGui.TextColored(yellow, "Open Isleventory and view all tabs to check materials required against materials you have.");
                                            ImGui.Spacing();
                                            ImGui.TextWrapped(ConvertMatsToString(matsRequired));
                                            if (ImGui.IsItemHovered())
                                            {
                                                ImGui.SetTooltip("Starred items are rare and come from the Granary, Pasture, or Cropland");
                                            }
                                        }
                                        else
                                        {
                                            string matsNeeded = "Materials needed: ";
                                            float currentX = ImGui.CalcTextSize(matsNeeded).X;
                                            float availableX = ImGui.GetContentRegionAvail().X;
                                            ImGui.Text(matsNeeded);
                                            foreach (var mat in matsRequired)
                                            {

                                                bool isRare = RareMaterialHelper.GetMaterialValue(mat.Key, out _);
                                                string matStr = mat.Value + "x " + RareMaterialHelper.GetDisplayName(mat.Key) + (isRare ? "*" : ""); 
                                                if (!mat.Equals(matsRequired.Last()))
                                                    matStr += ", ";
                                                Vector4 color = red;
                                                string tooltip = "";
                                                if(inventory.ContainsKey((int)mat.Key))
                                                {
                                                    int itemsHeld = inventory[(int)mat.Key];
                                                    if (itemsHeld >= mat.Value)
                                                        color = green;
                                                    else if (itemsHeld*2 >= mat.Value)
                                                        color = yellow;
                                                    tooltip = "Owned: " + itemsHeld + ". ";
                                                }
                                                currentX += ImGui.CalcTextSize(matStr).X;
                                                if (currentX < availableX)
                                                    ImGui.SameLine(0f, 0f);
                                                else
                                                    currentX = ImGui.CalcTextSize(matStr).X;

                                                ImGui.TextColored(color, matStr);
                                                if (isRare)
                                                    tooltip += "Starred items are rare and come from the Granary, Pasture, or Cropland";
                                                if (ImGui.IsItemHovered())
                                                {
                                                    ImGui.SetTooltip(tooltip);
                                                }
                                            }
                                        }                                        
                                        ImGui.Spacing();
                                    }
                                }
                                if (ImGui.BeginTable("Options", 4, ImGuiTableFlags.ScrollY | ImGuiTableFlags.Resizable))
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
                                            if (reader.GetInventory(out var maybeInv))
                                                inventory = maybeInv;

                                            Solver.Solver.SetDay(suggestion.Key.GetItems(), day);
                                            AddNewSuggestions(Solver.Solver.RunSolver(inventory));
                                        }
                                        ImGui.TableSetColumnIndex(column++);
                                        ImGui.Text(suggestion.Value.ToString());
                                        ImGui.TableSetColumnIndex(column++);
                                        if (suggestion.Key.GetNumCrafts() > 0)
                                            ImGui.Text(JoinItems(" - ", suggestion.Key.GetItems()));
                                        else
                                            ImGui.Text("Rest");
                                    }
                                    ImGui.EndTable();
                                }
                                ImGui.Spacing();
                            }
                            else
                            {
                                ImGui.Text("Rest!!!");
                            }
                            ImGui.EndTabItem();
                        }
                    }
                }

                if(config.day == 6)
                {
                    if (ImGui.BeginTabItem("Day 1 Next Week"))
                    {
                        ImGui.Text("Always rest Day 1!");
                    }
                }

                ImGui.EndTabBar();
            }
        }
        catch (Exception e)
        {
            PluginLog.LogError(e, "Error displaying schedule data.");
        }
    }

    private string ConvertMatsToString(IOrderedEnumerable<KeyValuePair<Material, int>> orderedDict)
    {
        var matsEnum = orderedDict.GetEnumerator();
        StringBuilder matsStr = new StringBuilder("Materials needed: ");
        while (matsEnum.MoveNext())
        {
            matsStr.Append(matsEnum.Current.Value).Append("x ").Append(RareMaterialHelper.GetDisplayName(matsEnum.Current.Key));
            if (RareMaterialHelper.GetMaterialValue(matsEnum.Current.Key, out _))
                matsStr.Append('*');
            if (!matsEnum.Current.Equals(orderedDict.Last()))
                matsStr.Append(", ");
        }
        return matsStr.ToString();
    }
}
