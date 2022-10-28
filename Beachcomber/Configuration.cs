using Dalamud.Configuration;
using Dalamud.Plugin;
using Beachcomber.Solver;
using System;
using System.Collections.Generic;

namespace Beachcomber
{
    [Serializable]
    public class Configuration : IPluginConfiguration
    {
        public int Version { get; set; } = 0;
        public int suggestionsToShow { get; set; } = 10;
        public float materialValue { get; set; } = 0.5f;
        public int islandRank { get; set; } = 1;
        public int workshopBonus { get; set; } = 100;
        public int maxGroove { get; set; } = 10;
        public bool showNetCowries { get; set; } = false;
        public bool enforceRestDays { get; set; } = true;
        public string rootPath { get; set; } = "";

        public string[] flavorText { get; set; } = new string[3] { "Isleworks", "Island", "Sanctuary" };
        public bool onlySuggestMaterialsOwned { get; set; } = false;


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

        public string RemoveFilteredWords(string s)
        {
            foreach (var word in flavorText)
                if(word.Length > 0)
                    s = s.Replace(word, "");

            s = s.Replace("  ", " ").Trim(); //Removing a word from the middle of a string will cause double spaces, which we don't want

            return s;
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
