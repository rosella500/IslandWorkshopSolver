using Dalamud.Configuration;
using Dalamud.Plugin;
using IslandWorkshopSolver.Solver;
using System;
using System.Collections.Generic;

namespace IslandWorkshopSolver
{
    [Serializable]
    public class Configuration : IPluginConfiguration
    {
        public int Version { get; set; } = 0;
        public int suggestionsToShow { get; set; } = 10;
        public float materialValue { get; set; } = 0.5f;
        public int islandRank { get; set; } = 10;
        public int workshopBonus { get; set; } = 120;
        public int maxGroove { get; set; } = 35;
        public bool verboseSolverLogging { get; set; } = false;
        public bool verboseCalculatorLogging { get; set; } = false;
        public bool verboseRestDayLogging { get; set; } = false;    
        public string rootPath { get; set; } = "";

        public Dictionary<Item, bool>? unknownD2Items { get; set; } = null;
        private int _day;
        public int day
        {
            get { return _day; }
            set
            {
                _day = value;
                if (_day != 0 && unknownD2Items != null)
                {
                    unknownD2Items.Clear();
                    unknownD2Items = null;
                }
                if (_day == 0 && unknownD2Items == null)
                    unknownD2Items = new Dictionary<Item, bool>();
            }
        }

        // the below exist just to make saving less cumbersome
        [NonSerialized]
        private DalamudPluginInterface? PluginInterface;

        public void Initialize(DalamudPluginInterface pluginInterface)
        {
            PluginInterface = pluginInterface;
        }

        public void Save()
        {
            PluginInterface!.SavePluginConfig(this);
        }
    }
}
