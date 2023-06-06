using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Dalamud.Utility.Signatures;
using Lumina.Excel.GeneratedSheets;
using Lumina.Excel;
using Dalamud.Logging;
using Beachcomber.Solver;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Client.Game.MJI;

namespace Beachcomber
{
    // Lifted entirely from Rietty's AutoMammet, who apparently took it from Otter. Bless you both. <3
    public class Reader
    {
        private readonly IReadOnlyList<string> items;
        private readonly IReadOnlyList<string> popularities;
        private readonly IReadOnlyList<string> supplies;
        private readonly IReadOnlyList<string> shifts;
        private readonly ExcelSheet<MJICraftworksPopularity> sheet;
        private int lastValidDay = -1;
        private int lastHash = -1;

        public Reader()
        {
            SignatureHelper.Initialise(this);
            items = DalamudPlugins.GameData.GetExcelSheet<MJICraftworksObject>()!.Where(o=> o.UnkData4[0].Amount >0).Select(o => o.Item.Value?.Name.ToString() ?? string.Empty)
               .Prepend(string.Empty).ToArray();
            var itemSheet = DalamudPlugins.GameData.GetExcelSheet<Lumina.Excel.GeneratedSheets.Item>()!;
            //This will probably need to be changed if we get new mats/crafts
            var materialNames = Enumerable.Range(37551, 61).Select(i => itemSheet.GetRow((uint)i)!.Name.ToString()).ToList();
            for (int i = 0; i < 6; i++)//Add 6 dummy items in between Milk and Resin
                materialNames.Add("None");
            materialNames.AddRange(Enumerable.Range(39224, 9).Select(i => itemSheet.GetRow((uint)i)!.Name.ToString()));
            materialNames.AddRange(Enumerable.Range(39887, 91-76+1).Select(i => itemSheet.GetRow((uint)i)!.Name.ToString()));

            var addon = DalamudPlugins.GameData.GetExcelSheet<Addon>()!;
            shifts = Enumerable.Range(15186, 5).Select(i => addon.GetRow((uint)i)!.Text.ToString()).ToArray();
            supplies = Enumerable.Range(15181, 5).Reverse().Select(i => addon.GetRow((uint)i)!.Text.ToString()).ToArray();
            popularities = Enumerable.Range(15177, 4).Select(i => addon.GetRow((uint)i)!.Text.ToString()).Prepend(string.Empty).ToArray();
            
            var craftIDs = DalamudPlugins.GameData.GetExcelSheet<MJICraftworksObject>()!.Select(o => o.Item.Value?.RowId ?? 0)
               .Where(r => r > 0).ToArray();
            List<string> craftNames = new();
            foreach (var craft in craftIDs)
            {
                string name = itemSheet.GetRow((uint)craft)!.Name.ToString();
                craftNames.Add(name);
            }

            ItemHelper.InitFromGameData(craftNames);
            //Maps material ID to value
            var rareMats = DalamudPlugins.GameData.GetExcelSheet<MJIDisposalShopItem>()!.Where(i => i.Unknown1 == 0).ToDictionary(i => i.Unknown0, i=> i.Unknown2);
            RareMaterialHelper.InitFromGameData(rareMats, materialNames);

            var supplyMods = DalamudPlugins.GameData.GetExcelSheet<MJICraftworksSupplyDefine>()!.ToDictionary(i => i.RowId, i => i.Ratio);
            SupplyHelper.InitFromGameData(supplyMods);

            var popMods = DalamudPlugins.GameData.GetExcelSheet<MJICraftworksPopularityType>()!.ToDictionary(i => i.RowId, i => i.Ratio);
            PopularityHelper.InitFromGameData(popMods);

            var validItemRows = DalamudPlugins.GameData.GetExcelSheet<MJICraftworksObject>()!.Where(o => o.UnkData4[0].Amount > 0);

            //"Isleworks Pumpkin Pudding", 37662, firstItem.Item.Value!.Name, firstItem.Item.Value.RowId
            //4, 0, 0,  cat1, cat2, ? firstItem.Unknown1, firstItem.Unknown2, firstItem.Unknown3
            //45, 3, 56, 1, 60, 1, mat1 id, mat1 q, etc. firstItem.Unknown4, firstItem.Unknown5, firstItem.Unknown6, firstItem.Unknown7, firstItem.Unknown8, firstItem.Unknown9
            //0, 0, 8, 6, 78 ?? rank, hours, basevalue firstItem.Unknown10, firstItem.Unknown11, firstItem.Unknown12, firstItem.Unknown13, firstItem.Unknown14
            //Items.Add(new ItemInfo(PumpkinPudding, Confections, Invalid, 78, 6, 8, new Dictionary<RareMaterial, int>() { { Pumpkin, 3 }, { Egg, 1 }, { Milk, 1 } }));

            List<ItemInfo> itemInfos = new();
            int itemIndex = 0;
            foreach (var item in validItemRows)
            {
                Dictionary<Material, int> mats = new();
                foreach(var mat in item.UnkData4)
                {
                    if(mat.Amount > 0)
                        mats.Add((Material)mat.Material, mat.Amount);
                }

                if(Enum.IsDefined((Solver.Item)itemIndex))
                {

                    itemInfos.Add(new ItemInfo((Solver.Item)itemIndex, (ItemCategory)(item.Theme[0].Value!.RowId), (ItemCategory)item.Theme[1].Value!.RowId, item.Value, item.CraftingTime, item.LevelReq, mats));
                    PluginLog.Verbose("Adding item {6} as {0} with material count {1} and categories {2}({4}), {3}({5})", (Solver.Item)itemIndex, mats.Count, item.Theme[0].Value!.RowId, item.Theme[1].Value!.RowId,
                        (ItemCategory)(item.Theme[0].Value!.RowId), (ItemCategory)item.Theme[1].Value!.RowId, item.Item.Value!.Name.ToString());

                    itemIndex++;
                }
                
            }

            Solver.Solver.InitItemsFromGameData(itemInfos);

            sheet = DalamudPlugins.GameData.GetExcelSheet<MJICraftworksPopularity>()!;
        }

        //This is in one method because island rank has a default invalid value whereas landmarks might just not be built
        public unsafe (int rank, int maxGroove) GetIslandRankAndMaxGroove()
        {
            if (MJIManager.Instance() == null)
                return (-1, -1);


            var currentRank = MJIManager.Instance()->IslandState.CurrentRank;
            int completedLandmarks = 0;
            for (int i = 0; i < /*MJILandmarkPlacements.Slots*/ 5; i++)
            {
                PluginLog.Verbose("Landmark {0} ID {1}, placement {4}, under construction {2}, hours to complete {3}", i,
                     MJIManager.Instance()->IslandState.LandmarkIds[i], MJIManager.Instance()->IslandState.LandmarkUnderConstruction[i], MJIManager.Instance()->IslandState.LandmarkHoursToCompletion[i], MJIManager.Instance()->LandmarkPlacementsSpan[i].LandmarkId);
                if (MJIManager.Instance()->IslandState.LandmarkIds[i] != 0)
                {
                    if (MJIManager.Instance()->IslandState.LandmarkUnderConstruction[i] == 0)
                        completedLandmarks++;
                }

            }
            var tension = DalamudPlugins.GameData.GetExcelSheet<MJICraftworksTension>()!;
            var maxGroove = tension.GetRow((uint)completedLandmarks)!.Unknown0;

            PluginLog.Debug("Found {0} completed landmarks, setting max groove to {1}", completedLandmarks, maxGroove);

            PluginLog.Debug("Island rank {0}", currentRank);
            return (currentRank, maxGroove);
        }

        public unsafe WorkshopInfo? GetWorkshopInfo()
        {
            if (MJIManager.Instance() == null)
                return null;

            int minLevel = 999;
            bool showError = false;
            int numWorkshops = 0;

            for (int i = 0; i < /*MJIWorkshops.MaxWorkshops*/ 4; i++)
            {
                if (MJIManager.Instance()->IslandState.Workshops.PlaceId[i] != 0)
                {
                    PluginLog.Verbose("Workshop {0} level {1}, under construction {2}, hours to complete {3}, placeID {4}", i,
                    MJIManager.Instance()->IslandState.Workshops.BuildingLevel[i] + 1, MJIManager.Instance()->IslandState.Workshops.UnderConstruction[i],
                    MJIManager.Instance()->IslandState.Workshops.HoursToCompletion[i], MJIManager.Instance()->IslandState.Workshops.PlaceId[i]);
                    numWorkshops++;
                    if (MJIManager.Instance()->IslandState.Workshops.UnderConstruction[i] == 0)
                        minLevel = Math.Min(minLevel, MJIManager.Instance()->IslandState.Workshops.BuildingLevel[i]);
                    else if (MJIManager.Instance()->IslandState.Workshops.HoursToCompletion[i] == 0)
                        showError = true;
                }
                else
                {
                    PluginLog.Verbose("Workshop {0} not built", i);
                }
            }

            int bonus = -1;

            if (minLevel == 999)
                return null;

            minLevel++; //Level appears to be 0-indexed but data is 1-indexed, so
            var workshopBonusSheet = DalamudPlugins.GameData.GetExcelSheet<MJICraftworksRankRatio>()!;
            bonus = workshopBonusSheet.GetRow((uint)minLevel)!.Ratio;


            PluginLog.Debug("Found min workshop rank of {0} with {2} workshops, setting bonus to {1}", minLevel, bonus, numWorkshops);
            WorkshopInfo workshopInfo = new() { NumWorkshops = numWorkshops, ShowError = showError, WorkshopBonus = bonus };
            return workshopInfo;
        }

        public unsafe bool GetInventory(out Dictionary<int, int> inventory)
        {
            inventory = new Dictionary<int, int>();
            var uiModulePtr = DalamudPlugins.GameGui.GetUIModule();
            if (uiModulePtr == IntPtr.Zero)
                return false;

            var agentModule = ((UIModule*)uiModulePtr)->GetAgentModule();
            if (agentModule == null)
                return false;

            var mjiPouch = agentModule->GetAgentMJIPouch();
            if (mjiPouch == null)
                return false;

            if (mjiPouch->InventoryData == null)
                return false;

            int totalItems = 0;
            for (ulong i = 0; i < mjiPouch->InventoryData->Inventory.Size(); i++)
            {
                var invItem = mjiPouch->InventoryData->Inventory.Get(i);
                PluginLog.Verbose("MJI Pouch inventory item: name {2}, slotIndex {0}, stack {1}", invItem.SlotIndex, invItem.StackSize, invItem.Name);
                totalItems += invItem.StackSize;
                if (inventory.ContainsKey(invItem.SlotIndex))
                    PluginLog.Warning("Duplicate ID {0} detected: {1}", invItem.SlotIndex, invItem.Name);
                else
                    inventory.Add(invItem.SlotIndex, invItem.StackSize);
            }

            return totalItems > 0;
        }

        public unsafe (int,string) ExportIsleData()
        {
            if (MJIManager.Instance() == null)
                return (-1, "");


            byte currentPop = MJIManager.Instance()->CurrentPopularity;
            if (currentPop == 0)
            {
                PluginLog.Debug("No current pop, getting from importer");
                
                if (Solver.Solver.Importer.popularityIndex == 127)
                    PluginLog.Debug("Importer doesn't have pop either");
                else
                    currentPop = Solver.Solver.Importer.popularityIndex;
            }
            var currentPopularity = sheet.GetRow(currentPop)!; 
            var nextPopularity = sheet.GetRow(MJIManager.Instance()->NextPopularity)!; 
            PluginLog.Information("Current pop index {0}, next pop index {1}", currentPop, nextPopularity.RowId);

            var sb = new StringBuilder(64 * 128);
            int numNE = 0;
            for (var i = 1; i < items.Count; ++i)
            {
                sb.Append(items[i]);
                sb.Append('\t');
                sb.Append(GetPopularity(currentPopularity, i));
                sb.Append('\t');
                var supply = (int)MJIManager.Instance()->GetSupplyForCraftwork((uint)i);
                var shift = (int)MJIManager.Instance()->GetDemandShiftForCraftwork((uint)i);
                if (supply == (int)Supply.Nonexistent)
                    numNE++;
                sb.Append(supply);
                sb.Append('\t');
                sb.Append(shift);
                sb.Append('\t');
                sb.Append(GetPopularity(nextPopularity, i));
                sb.Append('\n');
            }

            

            string returnStr = sb.ToString();
            int newHash = returnStr.GetHashCode();
            if (numNE == items.Count - 1)
                PluginLog.Warning("Reading invalid supply data (all Nonexistent). Need to talk to the mammet");
            else if (lastHash == -1 || lastHash != newHash)
            {
                int currentDay = Solver.Solver.GetCurrentDay();
                PluginLog.Debug("New valid supply data detected! Previous hash: {0}, day {1}, Current hash: {2}, day {3})", lastHash, lastValidDay, newHash, currentDay) ; 
                PluginLog.Verbose("{0}", returnStr);

                lastHash = newHash;
                lastValidDay = currentDay;
            }
            else
                PluginLog.LogDebug("Reading same valid supply data as before, not updating hash or day");

            return (lastValidDay, sb.ToString());
        }

        private static int GetPopularity(MJICraftworksPopularity pop, int idx)
        {
            var val = pop.Popularity[idx].Value!;

            //var val = (byte?)pop.GetType().GetProperty($"Unknown{idx}", BindingFlags.Instance | BindingFlags.Public | BindingFlags.GetProperty)?.GetValue(pop, null);
            PluginLog.LogVerbose("Getting popularity {3} for index {0}: {2} ({1})", idx, val.Ratio, val.RowId, pop.RowId);
            return val == null ? 0 : (int)val.RowId; // string.Empty : popularities[val.Value];
        }
    }
}
