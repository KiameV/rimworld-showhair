using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace ShowHair
{
    public class SettingsController : Mod
    {
        private Vector2 scrollPosition = new Vector2(0, 0);

        internal static Dictionary<ThingDef, bool> AllHatsAndDoHidesHair = new Dictionary<ThingDef, bool>();

        public static HashSet<ThingDef> HatsThatHideHair = new HashSet<ThingDef>();
        public static bool HideAllHats { get; internal set;}

        static SettingsController()
        {
            HideAllHats = false;
        }

        public SettingsController(ModContentPack content) : base(content)
        {
            base.GetSettings<Settings>();
        }

        internal static void InitializeAllHats()
        {
            if (AllHatsAndDoHidesHair.Count == 0)
            {
                foreach (ThingDef td in DefDatabase<ThingDef>.AllDefs)
                {
                    if (td.apparel != null && td.apparel.LastLayer == RimWorld.ApparelLayer.Overhead)
                    {
                        bool hide = Settings.LoadedHairHideHats.Contains(td.defName);
                        AllHatsAndDoHidesHair.Add(td, hide);
                        if (hide)
                            HatsThatHideHair.Add(td);
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
            bool b = HideAllHats;
            Widgets.Checkbox(new Vector2(120, 0), ref b);
            HideAllHats = b;
            GUI.EndGroup();

            if (!HideAllHats)
            {
                Rect outer = new Rect(0, 80, 600, 400);
                GUI.BeginGroup(outer);
                Text.Font = GameFont.Medium;
                Widgets.Label(new Rect(0, 0, 500, 40), "ShowHair.SelectHatsWhichHideHair".Translate());
                Widgets.BeginScrollView(new Rect(0, 0, 584, 500), ref scrollPosition, new Rect(0, 0, 600, AllHatsAndDoHidesHair.Count * 30));
                Text.Font = GameFont.Small;

                int index = 0;
                Dictionary<ThingDef, bool> changes = new Dictionary<ThingDef, bool>();
                foreach (KeyValuePair<ThingDef, bool> kv in AllHatsAndDoHidesHair)
                {
                    int y = index * 30 + 50;
                    ++index;
                    Widgets.Label(new Rect(0, y, 200, 22), kv.Key.label + ":");

                    b = kv.Value;
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
                        HatsThatHideHair.Add(kv.Key);
                    }
                    else
                    {
                        HatsThatHideHair.Remove(kv.Key);
                    }
                }
            }
            GUI.EndGroup();
        }
    }

    class Settings : ModSettings
    {
        private static List<string> loadedHairHideHats = new List<string>(0);
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
                loadedHairHideHats = new List<string>(SettingsController.HatsThatHideHair.Count);
                foreach (ThingDef d in SettingsController.HatsThatHideHair)
                {
                    loadedHairHideHats.Add(d.defName);
                }
            }
            
            Scribe_Collections.Look(ref loadedHairHideHats, "ShowHair.HatsThatHideHair", LookMode.Value, new Object[0]);

            bool hideAllHats = SettingsController.HideAllHats;
            Scribe_Values.Look<bool>(ref hideAllHats, "ShowHair.HideAllHats", false, false);

            if (Scribe.mode == LoadSaveMode.LoadingVars)
            {
                SettingsController.HideAllHats = hideAllHats;
            }
        }
    }
}