using System;
using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace ShowHair
{
    public class SettingsController : Mod
    {
        public static Dictionary<ThingDef, bool> AllHatsAndDoHidesHair = new Dictionary<ThingDef, bool>();
        public static bool HideAllHats { get { return Settings.hideAllHats; } }
        public static HashSet<ThingDef> HatsThatHideHair { get { return Settings.HatsThatHideHair; } }

        private static Settings Settings;
        private Vector2 scrollPosition = new Vector2(0, 0);

        public SettingsController(ModContentPack content) : base(content)
        {
            Settings = base.GetSettings<Settings>();
        }

        internal static void InitializeAllHats()
        {
            if (AllHatsAndDoHidesHair.Count == 0)
            {
                foreach (ThingDef td in DefDatabase<ThingDef>.AllDefs)
                {
                    if (td.apparel != null && 
                        td.apparel.LastLayer == RimWorld.ApparelLayer.Overhead &&
                        !String.IsNullOrEmpty(td.apparel.wornGraphicPath))
                    {
                        bool hide = Settings.LoadedHairHideHats.Contains(td.defName);
                        AllHatsAndDoHidesHair.Add(td, hide);
                        if (hide)
                        {
                            Settings.HatsThatHideHair.Add(td);
                        }
                    }
                }
            }
        }

        public override string SettingsCategory()
        {
            return "ShowHair.ShowHair".Translate();
        }

        public override void DoSettingsWindowContents(Rect rect)
        {
            InitializeAllHats();
            GUI.BeginGroup(new Rect(0, 60, 602, 450));

            GUI.BeginGroup(new Rect(0, 0, 140, 30));
            Widgets.Label(new Rect(0, 1, 100, 22), "ShowHair.HideAllHats".Translate() + ":");
            Widgets.Checkbox(new Vector2(120, 0), ref Settings.hideAllHats);
            GUI.EndGroup();

            if (!HideAllHats)
            {
                Rect outer = new Rect(0, 80, 600, 400);
                GUI.BeginGroup(outer);
                Text.Font = GameFont.Medium;
                Widgets.Label(new Rect(0, 0, 500, 40), "ShowHair.SelectHatsWhichHideHair".Translate());
                Widgets.BeginScrollView(new Rect(0, 50, 500, 300), ref scrollPosition, new Rect(0, 0, 484, AllHatsAndDoHidesHair.Count * 30 + 40));
                Text.Font = GameFont.Small;

                int index = 0;
                Dictionary<ThingDef, bool> changes = new Dictionary<ThingDef, bool>();
                foreach (KeyValuePair<ThingDef, bool> kv in AllHatsAndDoHidesHair)
                {
                    int y = index * 30;
                    ++index;
                    Widgets.Label(new Rect(0, y, 200, 22), kv.Key.label + ":");

                    bool b = kv.Value;
                    Widgets.Checkbox(new Vector2(220, y - 1), ref b);
                    if (b != kv.Value)
                    {
                        changes.Add(kv.Key, b);
                    }
                }
                Widgets.EndScrollView();
                GUI.EndGroup();

                foreach (KeyValuePair<ThingDef, bool> kv in changes)
                {
                    AllHatsAndDoHidesHair[kv.Key] = kv.Value;
                    if (kv.Value)
                    {
                        Settings.HatsThatHideHair.Add(kv.Key);
                    }
                    else
                    {
                        Settings.HatsThatHideHair.Remove(kv.Key);
                    }
                }
            }
            GUI.EndGroup();
        }
    }

    class Settings : ModSettings
    {
        public static HashSet<ThingDef> HatsThatHideHair = new HashSet<ThingDef>();
        public static bool hideAllHats = false;
        public static List<string> loadedHairHideHats = new List<string>(0);

        internal static List<string> LoadedHairHideHats
        {
            get
            {
                if (loadedHairHideHats == null)
                    return new List<string>(0);
                return loadedHairHideHats;
            }
        }
        public override void ExposeData()
        {
            base.ExposeData();

            if (Scribe.mode == LoadSaveMode.Saving)
            {
                loadedHairHideHats = new List<string>(HatsThatHideHair.Count);
                foreach (ThingDef d in HatsThatHideHair)
                {
                    loadedHairHideHats.Add(d.defName);
                }
            }
            
            Scribe_Collections.Look(ref loadedHairHideHats, "ShowHair.HatsThatHideHair", LookMode.Value);
            if (loadedHairHideHats == null)
            {
                loadedHairHideHats = new List<string>(0);
            }
            
            Scribe_Values.Look<bool>(ref hideAllHats, "ShowHair.HideAllHats", false, false);
        }
    }
}