using RimWorld;
using System;
using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace ShowHair
{
    public class SettingsController : Mod
    {
        private Settings Settings;
        private Vector2 scrollPosition = new Vector2(0, 0);
        private Vector2 scrollPosition2 = new Vector2(0, 0);

        public SettingsController(ModContentPack content) : base(content)
        {
            Settings = base.GetSettings<Settings>();
        }

        public override string SettingsCategory()
        {
            return "ShowHair.ShowHair".Translate();
        }

        public override void DoSettingsWindowContents(Rect rect)
        {
            Settings.Initialize();

            float y = 60f;
            Widgets.CheckboxLabeled(new Rect(0, y, 250, 22), "ShowHair.OnlyApplyToColonists".Translate(), ref Settings.OnlyApplyToColonists);
            y += 30;
            Widgets.CheckboxLabeled(new Rect(0, y, 250, 22), "ShowHair.HideAllHats".Translate(), ref Settings.HideAllHats);
            y += 30;

            if (!Settings.HideAllHats)
            {
                Widgets.CheckboxLabeled(new Rect(0, y, 250, 22), "ShowHair.ShowHatsOnlyWhenDrafted".Translate(), ref Settings.ShowHatsOnlyWhenDrafted);
                y += 40;

                DrawTable(0f, y, 300f, ref scrollPosition, "ShowHair.SelectHatsWhichHideHair", new List<ThingDef>(Settings.HatsThatHide.Keys), Settings.HatsThatHide);
                DrawTable(340f, y, 300f, ref scrollPosition2, "ShowHair.SelectHairThatWillBeHidden", new List<HairDef>(Settings.HairToHide.Keys), Settings.HairToHide);
            }
            else
            {
                Settings.ShowHatsOnlyWhenDrafted = false;
            }
        }

        private void DrawTable<T>(float x, float y, float width, ref Vector2 scroll, string header, ICollection<T> labels, Dictionary<T, bool> items) where T : Def
        {
            const float ROW_HEIGHT = 28;
            GUI.BeginGroup(new Rect(x, y, width, 400));
            Text.Font = GameFont.Medium;
            Widgets.Label(new Rect(0, 0, width, 40), header.Translate());
            Widgets.BeginScrollView(new Rect(0, 50, width, 300), ref scroll, new Rect(0, 0, width - 16, items.Count * ROW_HEIGHT + 40));
            Text.Font = GameFont.Small;

            int index = 0;
            bool b, orig;
            foreach (T t in labels)
            {
                y = index * ROW_HEIGHT;
                ++index;
                Widgets.Label(new Rect(0, y, 200, 22), t.label + ":");

                b = orig = items[t];
                Widgets.Checkbox(new Vector2(220, y - 1), ref b);
                if (b != orig)
                {
                    items[t] = b;
                }
            }
            Widgets.EndScrollView();
            GUI.EndGroup();
        }
    }

    class Settings : ModSettings
    {
        public static bool OnlyApplyToColonists = false;
        public static bool HideAllHats = false;
        public static bool ShowHatsOnlyWhenDrafted = false;

        public static Dictionary<ThingDef, bool> HatsThatHide = new Dictionary<ThingDef, bool>();
        public static Dictionary<HairDef, bool> HairToHide = new Dictionary<HairDef, bool>();

        private static List<string> hatsThatHide = null;
        private static List<string> hairToHide = null;

        public override void ExposeData()
        {
            base.ExposeData();

            if (Scribe.mode == LoadSaveMode.Saving)
            {
                hatsThatHide = new List<string>();
                foreach (KeyValuePair<ThingDef, bool> kv in HatsThatHide)
                    if (kv.Value)
                        hatsThatHide.Add(kv.Key.defName);

                hairToHide = new List<string>();
                foreach (KeyValuePair<HairDef, bool> kv in HairToHide)
                    if (kv.Value)
                        hairToHide.Add(kv.Key.defName);
            }

            Scribe_Collections.Look(ref hairToHide, "HairToHide", LookMode.Value);
            Scribe_Collections.Look(ref hatsThatHide, "HatsThatHide", LookMode.Value);
            Scribe_Values.Look<bool>(ref HideAllHats, "HideAllHats", false, false);
            Scribe_Values.Look<bool>(ref OnlyApplyToColonists, "OnlyApplyToColonists", false, false);
            Scribe_Values.Look<bool>(ref ShowHatsOnlyWhenDrafted, "ShowHatsOnlyWhenDrafted", false, false);

            if (Scribe.mode == LoadSaveMode.Saving)
            {
                hatsThatHide.Clear();
                hatsThatHide = null;
                hairToHide.Clear();
                hairToHide = null;
            }
        }

        private static bool isInitialized = false;
        internal static void Initialize()
        {
            int defCount = 0;
            if (!isInitialized)
            {
                foreach (ThingDef d in DefDatabase<ThingDef>.AllDefs)
                {
                    ++defCount;

                    if (d.apparel != null &&
                        IsHeadwear(d.apparel) &&
                        !String.IsNullOrEmpty(d.apparel.wornGraphicPath))
                    {
                        bool selected = false;
                        if (hatsThatHide != null)
                        {
                            foreach (string s in hatsThatHide)
                            {
                                if (s.Equals(d.defName))
                                {
                                    selected = true;
                                    break;
                                }
                            }
                        }
                        HatsThatHide[d] = selected;
                    }
                }

                foreach (HairDef d in DefDatabase<HairDef>.AllDefs)
                {
                    ++defCount;
                    bool selected = false;
                    if (hairToHide != null)
                    {
                        foreach (string s in hairToHide)
                        {
                            if (s.Equals(d.defName))
                            {
                                selected = true;
                                break;
                            }
                        }
                    }
                    HairToHide[d] = selected;
                }

                if (defCount > 0)
                    isInitialized = true;

                if (isInitialized)
                {
                    if (hairToHide != null)
                    {
                        hairToHide.Clear();
                        hairToHide = null;
                    }

                    if (hatsThatHide != null)
                    {
                        hatsThatHide.Clear();
                        hatsThatHide = null;
                    }
                }
            }
        }

        public static bool IsHeadwear(ApparelProperties apparelProperties)
        {
            if (apparelProperties.LastLayer == ApparelLayerDefOf.Overhead)
            {
                return true;
            }
            for (int i = 0; i < apparelProperties.bodyPartGroups.Count; ++i)
            {
                var group = apparelProperties.bodyPartGroups[i];
                if (group == BodyPartGroupDefOf.FullHead || group == BodyPartGroupDefOf.UpperHead || group == BodyPartGroupDefOf.Eyes)
                {
                    return true;
                }
            }
            return false;
        }
    }
}