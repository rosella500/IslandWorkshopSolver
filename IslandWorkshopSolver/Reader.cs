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

namespace IslandWorkshopSolver
{
    // Lifted entirely from Rietty's AutoMammet, who apparently took it from Otter. Bless you both. <3
    public class Reader
    {
        [Signature("E8 ?? ?? ?? ?? 8B 50 10")]
        private readonly unsafe delegate* unmanaged<IntPtr> readerInstance = null!;

        private readonly IReadOnlyList<string> items;
        private readonly IReadOnlyList<string> popularities;
        private readonly IReadOnlyList<string> supplies;
        private readonly IReadOnlyList<string> shifts;
        private readonly ExcelSheet<MJICraftworksPopularity> sheet;

        public Reader(DalamudPluginInterface pluginInterface)
        {
            SignatureHelper.Initialise(this);
            items = DalamudPlugins.GameData.GetExcelSheet<MJICraftworksObject>()!.Select(o => o.Item.Value?.Name.ToString() ?? string.Empty)
               .Where(s => s.Length > 0).Prepend(string.Empty).ToArray();

            var addon = DalamudPlugins.GameData.GetExcelSheet<Addon>()!;
            shifts = Enumerable.Range(15186, 5).Select(i => addon.GetRow((uint)i)!.Text.ToString()).ToArray();
            supplies = Enumerable.Range(15181, 5).Reverse().Select(i => addon.GetRow((uint)i)!.Text.ToString()).ToArray();
            popularities = Enumerable.Range(15177, 4).Select(i => addon.GetRow((uint)i)!.Text.ToString()).Prepend(string.Empty).ToArray();
            


            var firstItem = DalamudPlugins.GameData.GetExcelSheet<MJICraftworksObject>()!.Where(s => (s.Item.Value?.RowId ?? 0) == 37662).FirstOrDefault();
            PluginLog.Debug("excel sheet row? {0}, {1}, {2}, {3}, {4}, {5}, {6}, {7}, {8}, {9}, {10}, {11}, {12}, {13}, {14}, {15}", firstItem.Item.Value!.Name, firstItem.Item.Value.RowId, firstItem.Unknown1, firstItem.Unknown2, firstItem.Unknown3, 
                firstItem.Unknown4, firstItem.Unknown5, firstItem.Unknown6, firstItem.Unknown7, firstItem.Unknown8, firstItem.Unknown9, firstItem.Unknown10, 
                firstItem.Unknown11, firstItem.Unknown12, firstItem.Unknown13, firstItem.Unknown14);

            ItemHelper.InitFromGameData(items);
            //Maps material ID to value
            var rareMats = DalamudPlugins.GameData.GetExcelSheet<MJIDisposalShopItem>()!.Where(i => i.Unknown1 == 0).ToDictionary(i => i.Unknown0, i=> i.Unknown2);
            RareMaterialHelper.InitFromGameData(rareMats);


            var supplyMods = DalamudPlugins.GameData.GetExcelSheet<MJICraftworksSupplyDefine>()!.ToDictionary(i => i.RowId, i => i.Unknown1);
            SupplyHelper.InitFromGameData(supplyMods);

            var popMods = DalamudPlugins.GameData.GetExcelSheet<MJICraftworksPopularityType>()!.ToDictionary(i => i.RowId, i => i.Unknown0);
            PopularityHelper.InitFromGameData(popMods);

            sheet = DalamudPlugins.GameData.GetExcelSheet<MJICraftworksPopularity>()!;
        }

        public unsafe string ExportIsleData()
        {
            var instance = readerInstance();
            if (instance == IntPtr.Zero)
                return string.Empty;

            var currentPopularity = sheet.GetRow(*(byte*)(instance + 0x2E8))!;
            var nextPopularity = sheet.GetRow(*(byte*)(instance + 0x2E9))!;

            var sb = new StringBuilder(64 * 128);
            for (var i = 1; i < items.Count; ++i)
            {
                sb.Append(items[i]);
                sb.Append('\t');
                sb.Append(GetPopularity(currentPopularity, i));
                sb.Append('\t');
                var supply = *(byte*)(instance + 0x2EA + i);
                var shift = supply & 0x7;
                supply = (byte)(supply >> 4);
                sb.Append(supply);
                sb.Append('\t');
                sb.Append(shift);
                sb.Append('\t');
                sb.Append(GetPopularity(nextPopularity, i));
                sb.Append('\n');
            }

            return sb.ToString();
        }

        private int GetPopularity(MJICraftworksPopularity pop, int idx)
        {
            var val = (byte?)pop.GetType().GetProperty($"Unknown{idx}", BindingFlags.Instance | BindingFlags.Public | BindingFlags.GetProperty)?.GetValue(pop, null);
            return val == null ? 0 : val.Value; // string.Empty : popularities[val.Value];
        }
    }
}
