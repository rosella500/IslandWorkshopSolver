using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Dalamud.Plugin;
using System.Reflection;
using Dalamud.Utility.Signatures;
using Lumina.Excel.GeneratedSheets;
using Lumina.Excel;
using Dalamud.Logging;
using IslandWorkshopSolver.Solver;
using Lumina;
using FFXIVClientStructs.FFXIV.Client.Game;
using Dalamud.Game;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;

namespace IslandWorkshopSolver
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

        public Reader(DalamudPluginInterface pluginInterface)
        {
            SignatureHelper.Initialise(this);
            items = DalamudPlugins.GameData.GetExcelSheet<MJICraftworksObject>()!.Select(o => o.Item.Value?.Name.ToString() ?? string.Empty)
               .Where(s => s.Length > 0).Prepend(string.Empty).ToArray();
            var itemSheet = DalamudPlugins.GameData.GetExcelSheet<Lumina.Excel.GeneratedSheets.Item>()!;
            string[] itemNames = Enumerable.Range(37551, 61).Select(i => itemSheet.GetRow((uint)i)!.Name.ToString()).ToArray();
            var addon = DalamudPlugins.GameData.GetExcelSheet<Addon>()!;
            shifts = Enumerable.Range(15186, 5).Select(i => addon.GetRow((uint)i)!.Text.ToString()).ToArray();
            supplies = Enumerable.Range(15181, 5).Reverse().Select(i => addon.GetRow((uint)i)!.Text.ToString()).ToArray();
            popularities = Enumerable.Range(15177, 4).Select(i => addon.GetRow((uint)i)!.Text.ToString()).Prepend(string.Empty).ToArray();
            
            var validCrafts = DalamudPlugins.GameData.GetExcelSheet<MJICraftworksObject>()!.Select(o => o.Item.Value?.Name.ToString() ?? string.Empty)
               .Where(s => s.Length > 0).ToArray();

            ItemHelper.InitFromGameData(validCrafts);
            //Maps material ID to value
            var rareMats = DalamudPlugins.GameData.GetExcelSheet<MJIDisposalShopItem>()!.Where(i => i.Unknown1 == 0).ToDictionary(i => i.Unknown0, i=> i.Unknown2);
            RareMaterialHelper.InitFromGameData(rareMats, itemNames);

            var supplyMods = DalamudPlugins.GameData.GetExcelSheet<MJICraftworksSupplyDefine>()!.ToDictionary(i => i.RowId, i => i.Unknown1);
            SupplyHelper.InitFromGameData(supplyMods);

            var popMods = DalamudPlugins.GameData.GetExcelSheet<MJICraftworksPopularityType>()!.ToDictionary(i => i.RowId, i => i.Unknown0);
            PopularityHelper.InitFromGameData(popMods);

            var validItemRows = DalamudPlugins.GameData.GetExcelSheet<MJICraftworksObject>()!.Where(s => (s.Item.Value?.Name.ToString() ?? string.Empty).Length > 0);

            //"Isleworks Pumpkin Pudding", 37662, firstItem.Item.Value!.Name, firstItem.Item.Value.RowId
            //4, 0, 0,  cat1, cat2, ? firstItem.Unknown1, firstItem.Unknown2, firstItem.Unknown3
            //45, 3, 56, 1, 60, 1, mat1 id, mat1 q, etc. firstItem.Unknown4, firstItem.Unknown5, firstItem.Unknown6, firstItem.Unknown7, firstItem.Unknown8, firstItem.Unknown9
            //0, 0, 8, 6, 78 ?? rank, hours, basevalue firstItem.Unknown10, firstItem.Unknown11, firstItem.Unknown12, firstItem.Unknown13, firstItem.Unknown14
            //Items.Add(new ItemInfo(PumpkinPudding, Confections, Invalid, 78, 6, 8, new Dictionary<RareMaterial, int>() { { Pumpkin, 3 }, { Egg, 1 }, { Milk, 1 } }));

            List<ItemInfo> itemInfos = new List<ItemInfo>();
            int itemIndex = 0;
            foreach (var item in validItemRows)
            {
                Dictionary<Material, int> mats = new Dictionary<Material, int>();
                if(item.Unknown5 > 0)
                    mats.Add((Material)item.Unknown4, item.Unknown5);
                if (item.Unknown7 > 0)
                    mats.Add((Material)item.Unknown6, item.Unknown7);
                if (item.Unknown9 > 0)
                    mats.Add((Material)item.Unknown8, item.Unknown9);

                if(Enum.IsDefined((Solver.Item)itemIndex))
                {
                    itemInfos.Add(new ItemInfo((Solver.Item)itemIndex, (ItemCategory)item.Unknown1, (ItemCategory)item.Unknown2, item.Unknown14, item.Unknown13, item.Unknown12, mats));
                    PluginLog.Verbose("Adding item {0} with material count {1}", (Solver.Item)itemIndex, mats.Count);

                    itemIndex++;
                }
                
            }

            Solver.Solver.InitItemsFromGameData(itemInfos);

            sheet = DalamudPlugins.GameData.GetExcelSheet<MJICraftworksPopularity>()!;
        }

        public unsafe int GetIslandRank()
        {
            if (MJIManager.Instance() == null)
                return -1;

            var currentRank = MJIManager.Instance()->CurrentRank;

            PluginLog.Verbose("Current rank? {0}", currentRank);
            return currentRank;
        }

        public unsafe Dictionary<int, int>? GetInventory()
        {
            var uiModulePtr = DalamudPlugins.GameGui.GetUIModule();
            if (uiModulePtr == IntPtr.Zero)
                return null;

            var agentModule = ((UIModule*)uiModulePtr)->GetAgentModule();
            if (agentModule == null)
                return null;

            var mjiPouch = agentModule->GetAgentMJIPouch();
            if (mjiPouch == null)
                return null;


            var inventory = new Dictionary<int, int>();
            if (mjiPouch->InventoryData == null)
                return inventory;

            for (ulong i = 0; i < mjiPouch->InventoryData->Inventory.Size() && i <= (int)Material.Milk; i++)
            {
                var invItem = mjiPouch->InventoryData->Inventory.Get(i);
                PluginLog.Verbose("MJI Pouch inventory item: name {2}, slotIndex {0}, stack {1}", invItem.SlotIndex, invItem.StackSize, invItem.Name);
                inventory.Add(invItem.SlotIndex, invItem.StackSize);
            }

            return inventory;
        }

        public unsafe (int,string) ExportIsleData()
        {
            if (MJIManager.Instance() == null)
                return (-1, ""); 


            var currentPopularity = sheet.GetRow(MJIManager.Instance()->CurrentPopularity)!; 
            var nextPopularity = sheet.GetRow(MJIManager.Instance()->NextPopularity)!; 
            PluginLog.Information("Current pop index {0}, next pop index {1}", currentPopularity.RowId, nextPopularity.RowId);

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
                PluginLog.Information("New valid supply data detected! Previous hash: {0}, day {1}, Current hash: {2}, day {3}\n{4}", lastHash, lastValidDay, newHash, currentDay, returnStr);

                lastHash = newHash;
                lastValidDay = currentDay;
            }
            else
                PluginLog.LogDebug("Reading same valid supply data as before, not updating hash or day");

            return (lastValidDay, sb.ToString());
        }

        private int GetPopularity(MJICraftworksPopularity pop, int idx)
        {
            var val = (byte?)pop.GetType().GetProperty($"Unknown{idx}", BindingFlags.Instance | BindingFlags.Public | BindingFlags.GetProperty)?.GetValue(pop, null);
            return val == null ? 0 : val.Value; // string.Empty : popularities[val.Value];
        }
    }
}
