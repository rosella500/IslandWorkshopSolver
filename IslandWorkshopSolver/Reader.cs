using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Dalamud.Plugin;
using System.Reflection;
using Dalamud.Utility.Signatures;
using Lumina.Excel.GeneratedSheets;
using Lumina.Excel;

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
                sb.Append(supplies[supply]);
                sb.Append('\t');
                sb.Append(shifts[shift]);
                sb.Append('\t');
                sb.Append(GetPopularity(nextPopularity, i));
                sb.Append('\n');
            }

            return sb.ToString();
        }

        private string GetPopularity(MJICraftworksPopularity pop, int idx)
        {
            var val = (byte?)pop.GetType().GetProperty($"Unknown{idx}", BindingFlags.Instance | BindingFlags.Public | BindingFlags.GetProperty)?.GetValue(pop, null);
            return val == null ? string.Empty : popularities[val.Value];
        }
    }
}
