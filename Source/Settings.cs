using System.Collections.Generic;
using System.Text;
using UnityEngine;
using Verse;

namespace ShowHair
{
    public class SettingsController : Mod
    {
        private Vector2 scrollPosition = new Vector2(0, 0);
        private static Dictionary<ThingDef, bool> apparelThatHidesHats = null;
        public static Dictionary<ThingDef, bool> ApparelThatHidesHats
        {
            get
            {
                if (apparelThatHidesHats == null || apparelThatHidesHats.Count == 0)
                {
                    HatsThatHideHair.Clear();
                    Dictionary<ThingDef, bool> d = new Dictionary<ThingDef, bool>();
                    foreach (ThingDef td in DefDatabase<ThingDef>.AllDefs)
                    {
                        if (td.apparel != null && td.apparel.LastLayer == RimWorld.ApparelLayer.Overhead)
                        {
                            bool hide = Settings.LoadedHairHideHats.Contains(td.defName);
                            d.Add(td, hide);
                            if (hide)
                                HatsThatHideHair.Add(td);
                        }
                    }
                    apparelThatHidesHats = d;
                }
                return apparelThatHidesHats;
            }
        }

        public static HashSet<ThingDef> HatsThatHideHair;

        static SettingsController()
        {
            HatsThatHideHair = new HashSet<ThingDef>();
        }

        public SettingsController(ModContentPack content) : base(content)
        {
            base.GetSettings<Settings>();
        }

        public override string SettingsCategory()
        {
            return "Show Hair with Hats";
        }

        public override void DoSettingsWindowContents(Rect rect)
        {
            Rect outer = new Rect(0, 60, 600, 400);
            GUI.BeginGroup(outer);
            Text.Font = GameFont.Medium;
            Widgets.Label(new Rect(0, 0, 500, 40), "Select the hats that will hide hair when worn");
            Widgets.BeginScrollView(outer, ref scrollPosition, new Rect(0, 0, 595, ApparelThatHidesHats.Count * 30));
            Text.Font = GameFont.Small;

            int index = 0;
            Dictionary<ThingDef, bool> changes = new Dictionary<ThingDef, bool>();
            foreach (KeyValuePair<ThingDef, bool> kv in ApparelThatHidesHats)
            {
                int y = index * 30 + 50;
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

            foreach(KeyValuePair<ThingDef, bool> kv in changes)
            {
                apparelThatHidesHats[kv.Key] = kv.Value;
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
    }

    class Settings : ModSettings
    {
        internal static List<string> LoadedHairHideHats = new List<string>();
        private const char delimiter = '^';
        public override void ExposeData()
        {
            base.ExposeData();

            if (Scribe.mode == LoadSaveMode.Saving)
            {
                LoadedHairHideHats = new List<string>(SettingsController.HatsThatHideHair.Count);
                foreach (ThingDef d in SettingsController.HatsThatHideHair)
                {
                    LoadedHairHideHats.Add(d.defName);
                }
            }
            
            Scribe_Collections.Look(ref LoadedHairHideHats, "ShowHair.HatsThatHideHair", LookMode.Value, new Object[0]);
        }
    }
}