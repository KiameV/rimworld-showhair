using RimWorld;
using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using Verse;

namespace ShowHair
{
    public class SettingsController : Mod
    {
        const string msgErrorDirty = "[Show Hair] A mod conflict caused an issue re-drawing pawns. See stack trace.";

        private Settings Settings;
        private Vector2 scrollPosition = new Vector2(0, 0);
        private Vector2 scrollPosition2 = new Vector2(0, 0);
        private ThingDef mouseOverThingDef, previousHatDef;
        private ThingWithComps pawnBackupHat;
        private Dictionary<ThingDef, ThingWithComps> spawnedHats = new Dictionary<ThingDef, ThingWithComps>();
        private HairDef mouseOverHairDef, previousHairDef, pawnBackupHairDef;
        private Pawn pawn;
        private Color originalColor;
        private bool putHatOnPawn = false;
        private float previousHatY, previousHairY;
        private FieldInfo wornApparelFI = typeof(Pawn_ApparelTracker).GetField("wornApparel", BindingFlags.NonPublic | BindingFlags.Instance);
        private FieldInfo innerListFI = typeof(ThingOwner<Apparel>).GetField("innerList", BindingFlags.NonPublic | BindingFlags.Instance);
        private string leftTableSearchBuffer = "", rightTableSearchBuffer = "";

        public SettingsController(ModContentPack content) : base(content)
        {
            Settings = base.GetSettings<Settings>();

            HairUtilityFactory.GetHairUtility();
        }

        public override string SettingsCategory()
        {
            return "ShowHair.ShowHair".Translate();
        }

        public override void DoSettingsWindowContents(Rect rect)
        {
            Settings.Initialize();

            Settings.OptionsOpen = !this.putHatOnPawn;

            if (Current.Game != null && this.pawn == null)
            {
                foreach (Pawn p in PawnsFinder.All_AliveOrDead)
                {
                    if (p.Faction != Faction.OfPlayer && 
                        p.def.race.Humanlike && 
                        !p.health.Downed &&
                        p.equipment != null)
                    {
                        this.pawn = p;
                        this.originalColor = p.story.hairColor;
                        foreach(ThingWithComps t in p.apparel.WornApparel)
                        {
                            if (t.def.apparel?.layers.Contains(ApparelLayerDefOf.Overhead) == true)
                            {
                                this.pawnBackupHat = t;
                                this.RemoveApparel(pawnBackupHat);
                                try
                                {
                                    this.pawn.Drawer.renderer.graphics.SetAllGraphicsDirty();
                                }
                                catch
                                {
                                    Log.ErrorOnce(msgErrorDirty, msgErrorDirty.GetHashCode());
                                }
                                break;
                            }
                        }
                        PortraitsCache.SetDirty(this.pawn);
                        break;
                    }
                }
            }
            else if (Current.Game == null)
            {
                this.pawn = null;
            }

            float y = 60f;
            Widgets.CheckboxLabeled(new Rect(0, y, 250, 22), "ShowHair.OnlyApplyToColonists".Translate(), ref Settings.OnlyApplyToColonists);
            y += 30;
            Widgets.CheckboxLabeled(new Rect(0, y, 250, 22), "ShowHair.HideAllHats".Translate(), ref Settings.HideAllHats);
            y += 30;
            Widgets.CheckboxLabeled(new Rect(0, y, 250, 22), "ShowHair.UseDontShaveHead".Translate(), ref Settings.UseDontShaveHead);
            y += 30;

            if (!Settings.HideAllHats)
            {
                Widgets.CheckboxLabeled(new Rect(0, y, 250, 22), "ShowHair.ShowHatsOnlyWhenDrafted".Translate(), ref Settings.ShowHatsOnlyWhenDrafted);
                y += 30;

                if (!Settings.ShowHatsOnlyWhenDrafted)
                {
                    Widgets.Label(new Rect(0, y, 225, 22), "ShowHair.HideHatsIndoors".Translate());
                    string label;
                    if (Settings.Indoors == Indoors.ShowHats)
                        label = "Off";
                    else if (Settings.Indoors == Indoors.HideHats)
                        label = "ShowHair.HideHatsIndoors";
                    else
                        label = "ShowHair.HideHatsIndoorsShowWhenDrafted";
                    if (Widgets.ButtonText(new Rect(235, y, 200, 22), label.Translate()))
                    {
                        Find.WindowStack.Add(new FloatMenu(new List<FloatMenuOption>()
                        {
                            new FloatMenuOption("Off".Translate(), delegate() {Settings.Indoors = Indoors.ShowHats; }),
                            new FloatMenuOption("ShowHair.HideHatsIndoors".Translate(), delegate() {Settings.Indoors = Indoors.HideHats; }),
                            new FloatMenuOption("ShowHair.HideHatsIndoorsShowWhenDrafted".Translate(), delegate() {Settings.Indoors = Indoors.ShowHatsWhenDrafted; }),
                        }));
                    }
                    y += 30;
                    /*
                    {
                        if (Settings.HideHatsIndoors)
                        {
                            Widgets.CheckboxLabeled(new Rect(10, y, 300, 22), "ShowHair.UpdatePortrait".Translate(), ref Settings.UpdatePortrait);
                            y += 30;
                            Widgets.CheckboxLabeled(new Rect(10, y, 300, 22), "ShowHair.HideHatsIndoorsShowWhenDrafted".Translate(), ref Settings.ShowHatsWhenDraftedIndoors);
                            y += 30;
                            Widgets.CheckboxLabeled(new Rect(10, y, 300, 22), "ShowHair.ShowHatsUnderNaturalRoof".Translate(), ref Settings.HideHatsNaturalRoof);
                            y += 30;
                        }
                    }*/
                }
                y += 10;

                DrawTable(0f, y, 320f, ref scrollPosition, ref previousHatY, "ShowHair.Hats", "ShowHair.HatsDesc", ref leftTableSearchBuffer, Settings.HatsThatHide.Keys, null, Settings.HatsThatHide);
                DrawTable(340f, y, 300f, ref scrollPosition2, ref previousHairY, "ShowHair.HairThatWillBeHidden", "", ref rightTableSearchBuffer, Settings.HairToHide.Keys, Settings.HairToHide, null);

                if (this.mouseOverThingDef != null)
                {
                    if (this.putHatOnPawn)
                    {
                        if (this.previousHatDef != this.mouseOverThingDef)
                        {
                            if (this.previousHatDef != null)
                            {
                                if (this.spawnedHats.TryGetValue(this.previousHatDef, out ThingWithComps t))
                                    this.RemoveApparel(t);
                            }

                            if (this.mouseOverThingDef != null)
                            {
                                this.previousHatDef = this.mouseOverThingDef;
                                if (!spawnedHats.TryGetValue(this.mouseOverThingDef, out ThingWithComps thing))
                                {
                                    ThingDef stuff = GenStuff.RandomStuffFor(this.mouseOverThingDef);
                                    thing = ThingMaker.MakeThing(this.mouseOverThingDef, stuff) as ThingWithComps;
                                    thing.TryGetComp<CompQuality>()?.SetQuality(QualityUtility.GenerateQualityRandomEqualChance(), ArtGenerationContext.Colony);
                                    thing.stackCount = 1;
                                    this.spawnedHats.Add(thing.def, thing);
                                }
                                this.AddApparel(thing);
                                try
                                {
                                    this.pawn.Drawer.renderer.graphics.SetAllGraphicsDirty();
                                }
                                catch
                                {
                                    Log.ErrorOnce(msgErrorDirty, msgErrorDirty.GetHashCode() + 1);
                                }
                                PortraitsCache.SetDirty(this.pawn);
                            }
                        }
                    }
                    else
                    {
                        //Widgets.ThingIcon(new Rect(700f, y + 50, 50, 50), this.mouseOverThingDef);
                    }
                }
                if (this.pawn != null && this.mouseOverHairDef != null)
                {
                    if (this.mouseOverHairDef != this.previousHairDef)
                    {
                        this.previousHairDef = this.mouseOverHairDef;
                        if (this.mouseOverHairDef != null)
                        {
                            this.pawnBackupHairDef = this.pawn.story.hairDef;

                            this.pawn.story.hairDef = this.mouseOverHairDef;
                            try
                            {
                                this.pawn.Drawer.renderer.graphics.ResolveAllGraphics();
                            }
                            catch
                            {
                                Log.ErrorOnce(msgErrorDirty, msgErrorDirty.GetHashCode() + 9);
                            }
                            PortraitsCache.SetDirty(this.pawn);
                        }
                    }
                }

                if (pawn != null)
                {
                    y -= 60;
                    DrawPortraitWidget(630f, y + 150f);
                    bool b = this.putHatOnPawn;
                    Widgets.CheckboxLabeled(new Rect(650f, y + 350, 150, 30), "ShowHair.PutHatOnPawn".Translate(), ref this.putHatOnPawn);
                    if (b != this.putHatOnPawn)
                    {
                        Stack<Apparel> toRemove = new Stack<Apparel>();
                        foreach (Apparel a in this.pawn.apparel.WornApparel)
                        {
                            var layers = a.def.apparel?.layers;
                            if (layers != null && (layers.Contains(ApparelLayerDefOf.Overhead)))
                                toRemove.Push(a);
                        }
                        foreach (Apparel a in toRemove)
                            this.pawn.apparel.Remove(a);
                        try
                        {
                            this.pawn.Drawer.renderer.graphics.SetAllGraphicsDirty();
                        }
                        catch
                        {
                            Log.ErrorOnce(msgErrorDirty, msgErrorDirty.GetHashCode() + 2);
                        }
                        PortraitsCache.SetDirty(this.pawn);
                    }
                    Widgets.Label(new Rect(600f, y + 400, 75, 30), "ShowHair.HairColor".Translate());
                    if (Widgets.ButtonText(new Rect(680, y + 400, 50, 30), "ShowHair.WhiteHairColor".Translate()))
                    {
                        this.pawn.story.hairColor = Color.white;
                        try
                        {
                            this.pawn.Drawer.renderer.graphics.ResolveAllGraphics();
                        }
                        catch
                        {
                            Log.ErrorOnce(msgErrorDirty, msgErrorDirty.GetHashCode() + 5);
                        }
                        PortraitsCache.SetDirty(this.pawn);
                    }
                    if (Widgets.ButtonText(new Rect(740, y + 400, 50, 30), "ShowHair.YellowHairColor".Translate()))
                    {
                        this.pawn.story.hairColor = Color.yellow;
                        try
                        {
                            this.pawn.Drawer.renderer.graphics.ResolveAllGraphics();
                        }
                        catch
                        {
                            Log.ErrorOnce(msgErrorDirty, msgErrorDirty.GetHashCode() + 6);
                        }
                        PortraitsCache.SetDirty(this.pawn);
                    }
                    if (Widgets.ButtonText(new Rect(800, y + 400, 50, 30), "ShowHair.GreenHairColor".Translate()))
                    {
                        this.pawn.story.hairColor = Color.green;
                        try
                        {
                            this.pawn.Drawer.renderer.graphics.ResolveAllGraphics();
                        }
                        catch
                        {
                            Log.ErrorOnce(msgErrorDirty, msgErrorDirty.GetHashCode() + 7);
                        }
                        PortraitsCache.SetDirty(this.pawn);
                    }
                }
                else
                {
                    Widgets.Label(new Rect(650f, y + 150f, 200, 30), "ShowHair.StartGameToSeePawn".Translate());
                }
            }
        }

        private bool TryGetInnerList(out List<Apparel> l)
        {
            l = null;
            var wornApparel = this.wornApparelFI.GetValue(this.pawn.apparel) as ThingOwner<Apparel>;
            if (wornApparel == null)
            {
                Log.Error("Failed to get apparel");
                return false;
            }

            l = this.innerListFI.GetValue(wornApparel) as List<Apparel>;
            if (l == null)
            {
                Log.Error("Failed to get inner list");
                return false;
            }
            return true;
        }

        private void AddApparel(ThingWithComps t)
        {
            if (this.TryGetInnerList(out var l))
                l.Add(t as Apparel);
        }

        private bool ContainsApparel(ThingWithComps t)
        {
            if (this.TryGetInnerList(out var l))
                return l.Contains(t as Apparel);
            return false;
        }

        private void RemoveApparel(ThingWithComps t)
        {
            if (this.TryGetInnerList(out var l))
                l.Remove(t as Apparel);
        }

        public override void WriteSettings()
        {
            base.WriteSettings();
            Settings.OptionsOpen = false;
            if (this.pawn != null)
            {
                this.pawn.story.hairColor = this.originalColor;
                if (this.spawnedHats?.Count > 0)
                {
                    foreach(var h in this.spawnedHats.Values)
                    {
                        this.RemoveApparel(h);
                        h.Destroy(DestroyMode.Vanish);
                    }
                }
                this.spawnedHats.Clear();
                if (this.pawnBackupHat != null && !this.ContainsApparel(this.pawnBackupHat))
                {
                    this.AddApparel(this.pawnBackupHat);
                }

                if (this.pawnBackupHairDef != null)
                    this.pawn.story.hairDef = this.pawnBackupHairDef;

                try
                {
                    this.pawn.Drawer.renderer.graphics.ResolveAllGraphics();
                }
                catch
                {
                    Log.ErrorOnce(msgErrorDirty, msgErrorDirty.GetHashCode() + 8);
                }
                PortraitsCache.SetDirty(this.pawn);
            }
        }

        private void DrawPortraitWidget(float left, float top)
        {
            // Portrait
            Rect rect = new Rect(left, top, 192f, 192f);

            // Draw the pawn's portrait
            GUI.BeginGroup(rect);
            Vector2 size = new Vector2(128f, 180f);
            Rect position = new Rect(rect.width * 0.5f - size.x * 0.5f, 10f + rect.height * 0.5f - size.y * 0.5f, size.x, size.y);
            RenderTexture image = PortraitsCache.Get(this.pawn, size, Rot4.South);
            GUI.DrawTexture(position, image);
            GUI.EndGroup();
        }

        private void DrawTable<T>(float x, float y, float width, ref Vector2 scroll, ref float innerY, string header, string headerDesc, ref string searchBuffer, ICollection<T> labels, Dictionary<T, bool> items, Dictionary<T, HatHideEnum> items2 = null) where T : Def
        {
            Text.Font = GameFont.Small;
            const float ROW_HEIGHT = 32;
            GUI.BeginGroup(new Rect(x, y, width, 400));
            Widgets.Label(new Rect(0, 0, width - 100, 20), header.Translate());
            if (items2 != null)
            {
                if (Widgets.ButtonText(new Rect(width - 100, 0, 100, 24), ((searchBuffer != "") ? "ShowHair.SetFiltered" : "ShowHair.SetAll").Translate()))
                {
                    string sb = searchBuffer;
                    Find.WindowStack.Add(new FloatMenu(new List<FloatMenuOption>()
                    {
                        new FloatMenuOption(HatHideEnum.ShowsHair.ToString().Translate(), () =>
                        {
                            this.SetHatHideEnum(sb, items2, HatHideEnum.ShowsHair);
                        }),
                        new FloatMenuOption(HatHideEnum.HideHat.ToString().Translate(), () =>
                        {
                            this.SetHatHideEnum(sb, items2, HatHideEnum.HideHat);
                        }),
                        new FloatMenuOption(HatHideEnum.HidesAllHair.ToString().Translate(), () =>
                        {
                            this.SetHatHideEnum(sb, items2, HatHideEnum.HidesAllHair);
                        }),
                        new FloatMenuOption(HatHideEnum.HidesHairShowBeard.ToString().Translate(), () =>
                        {
                            this.SetHatHideEnum(sb, items2, HatHideEnum.HidesHairShowBeard);
                        }),
                        new FloatMenuOption(HatHideEnum.OnlyDraftSH.ToString().Translate(), () =>
                        {
                            this.SetHatHideEnum(sb, items2, HatHideEnum.OnlyDraftSH);
                        }),
                        new FloatMenuOption(HatHideEnum.OnlyDraftHH.ToString().Translate(), () =>
                        {
                            this.SetHatHideEnum(sb, items2, HatHideEnum.OnlyDraftHH);
                        }),
                        new FloatMenuOption(HatHideEnum.OnlyDraftHHSB.ToString().Translate(), () =>
                        {
                            this.SetHatHideEnum(sb, items2, HatHideEnum.OnlyDraftHHSB);
                        }),
                    }));
                }
            }
            Text.Font = GameFont.Tiny;
            Widgets.Label(new Rect(0, 20, width - 65, 20), headerDesc.Translate());
            Text.Font = GameFont.Small;
            Rect rect = new Rect(0, 0, width - 16, innerY);
            searchBuffer = Widgets.TextArea(new Rect(0, 40f, 200f, 28f), searchBuffer);
            if (Widgets.ButtonText(new Rect(220, 40, 28, 28), "X"))
                searchBuffer = "";
            Widgets.BeginScrollView(new Rect(0, 75, width, 300), ref scroll, rect);

            bool isMouseInside = Mouse.IsOver(rect);

            innerY = 0;
            int index = 0;
            foreach (T t in labels)
            {
                if (!this.MatchesSearch(searchBuffer, t))
                    continue;

                innerY = index * ROW_HEIGHT;
                ++index;

                float innerX = 0;
                if (t is ThingDef tdd)
                {
                    Widgets.ThingIcon(new Rect(x, innerY - 2, ROW_HEIGHT - 2, ROW_HEIGHT - 2), tdd);
                    innerX += ROW_HEIGHT - 2;
                }

                rect = new Rect(innerX, innerY, 184, ROW_HEIGHT);
                if (isMouseInside)
                {
                    if (Mouse.IsOver(rect))
                    {
                        if (t is ThingDef td)
                            this.mouseOverThingDef = td;
                        else if (t is HairDef hd)
                            this.mouseOverHairDef = hd;//GraphicDatabase.Get<Graphic_Multi>(hd.texPath, ShaderDatabase.Transparent, Vector2.one, Color.white);
                    }
                }
                Widgets.Label(rect, ((this.pawn != null && IsSelected(t)) ? "* " : "") + t.label + ":");
                innerX += 184;

                if (items != null)
                {
                    bool b, orig;
                    b = orig = items[t];
                    Widgets.Checkbox(new Vector2(innerX, innerY - 1), ref b);
                    if (b != orig)
                    {
                        items[t] = b;
                        if (this.pawn != null)
                        {
                            try
                            {
                                this.pawn.Drawer.renderer.graphics.SetAllGraphicsDirty();
                            }
                            catch
                            {
                                Log.ErrorOnce(msgErrorDirty, msgErrorDirty.GetHashCode() + 3);
                            }
                            PortraitsCache.SetDirty(this.pawn);
                        }
                        break;
                    }
                }
                else if (items2 != null)
                {
                    Text.Font = GameFont.Tiny;
                    bool changed = false;
                    if (Widgets.ButtonText(new Rect(innerX, innerY, 90, 26), items2[t].ToString().Translate()))
                    {
                        List<FloatMenuOption> l = new List<FloatMenuOption>()
                        {
                            new FloatMenuOption(HatHideEnum.ShowsHair.ToString().Translate(), () =>
                            {
                                items2[t] = HatHideEnum.ShowsHair;
                                changed = true;
                            }),
                            new FloatMenuOption(HatHideEnum.HideHat.ToString().Translate(), () =>
                            {
                                items2[t] = HatHideEnum.HideHat;
                                changed = true;
                            }),
                            new FloatMenuOption(HatHideEnum.HidesAllHair.ToString().Translate(), () =>
                            { 
                                items2[t] = HatHideEnum.HidesAllHair;
                                changed = true;
                            }),
                            new FloatMenuOption(HatHideEnum.HidesHairShowBeard.ToString().Translate(), () =>
                            {
                                items2[t] = HatHideEnum.HidesHairShowBeard;
                                changed = true;
                            }),
                            new FloatMenuOption(HatHideEnum.OnlyDraftSH.ToString().Translate(), () =>
                            {
                                items2[t] = HatHideEnum.OnlyDraftSH;
                                changed = true;
                            }),
                            new FloatMenuOption(HatHideEnum.OnlyDraftHH.ToString().Translate(), () =>
                            {
                                items2[t] = HatHideEnum.OnlyDraftHH;
                                changed = true;
                            }),
                            new FloatMenuOption(HatHideEnum.OnlyDraftHHSB.ToString().Translate(), () =>
                            {
                                items2[t] = HatHideEnum.OnlyDraftHHSB;
                                changed = true;
                            }),
                        };
                        Find.WindowStack.Add(new FloatMenu(l));
                    }
                    if (changed && this.pawn != null)
                    {
                        try
                        {
                            this.pawn.Drawer.renderer.graphics.SetAllGraphicsDirty();
                        }
                        catch
                        {
                            Log.ErrorOnce(msgErrorDirty, msgErrorDirty.GetHashCode() + 4);
                        }
                        PortraitsCache.SetDirty(this.pawn);
                    }
                    Text.Font = GameFont.Small;
                }
            }
            Widgets.EndScrollView();
            GUI.EndGroup();
            innerY += ROW_HEIGHT;
        }

        private bool MatchesSearch<T>(string searchBuffer, T t) where T : Def
        {
            return searchBuffer == "" || t.label.ToLower().Contains(searchBuffer);
        }

        private void SetHatHideEnum<T>(string searchBuffer, Dictionary<T, HatHideEnum> items, HatHideEnum v) where T : Def
        {
            foreach (T t in new List<T>(items.Keys))
            {
                if (this.MatchesSearch(searchBuffer, t))
                {
                    items[t] = v;
                }
            }
        }

        private bool IsSelected(Def def)
        {
            if (def is ThingDef)
                return def == this.mouseOverThingDef;
            return def == this.mouseOverHairDef;
        }
    }

    public enum HatHideEnum
    {
        ShowsHair,
        HidesAllHair,
        HidesHairShowBeard,
        HideHat,
        OnlyDraftSH,
        OnlyDraftHH,
        OnlyDraftHHSB
    }

    public enum Indoors
    {
        ShowHats,
        HideHats,
        ShowHatsWhenDrafted
    }

    class Settings : ModSettings
    {
        public static bool OnlyApplyToColonists = false;
        public static bool HideAllHats = false;
        public static bool ShowHatsOnlyWhenDrafted = false;
        public static bool ShowHatsWhenDraftedIndoors = false;
        public static bool UpdatePortrait = false;
        public static bool OptionsOpen = false;
        public static Indoors Indoors = Indoors.ShowHats;

        public static bool UseDontShaveHead = true;

        public static Dictionary<ThingDef, HatHideEnum> HatsThatHide = new Dictionary<ThingDef, HatHideEnum>();
        public static Dictionary<HairDef, bool> HairToHide = new Dictionary<HairDef, bool>();

        private static ToSave ToSave = null;

        public override void ExposeData()
        {
            base.ExposeData();

            if (ToSave == null)
                ToSave = new ToSave();

            if (Scribe.mode == LoadSaveMode.Saving)
            {
                ToSave.Clear();

                if (Current.Game != null)
                {
                    foreach (var p in PawnsFinder.AllMaps) {
                        if (p.IsColonist && !p.Dead && p.def.race.Humanlike)
                        {
                            PortraitsCache.SetDirty(p);
                            GlobalTextureAtlasManager.TryMarkPawnFrameSetDirty(p);
                        }
                    }
                }

                foreach (KeyValuePair<ThingDef, HatHideEnum> kv in HatsThatHide)
                {
                    switch (kv.Value)
                    {
                        case HatHideEnum.HidesAllHair:
                            ToSave.hatsThatHideHair.Add(kv.Key.defName);
                            break;
                        case HatHideEnum.HidesHairShowBeard:
                            ToSave.hatsToHideShowBeards.Add(kv.Key.defName);
                            break;
                        case HatHideEnum.HideHat:
                            ToSave.hatsToHide.Add(kv.Key.defName);
                            break;
                        case HatHideEnum.OnlyDraftSH:
                            ToSave.hatsToHideUnlessDraftedSH.Add(kv.Key.defName);
                            break;
                        case HatHideEnum.OnlyDraftHH:
                            ToSave.hatsToHideUnlessDraftedHH.Add(kv.Key.defName);
                            break;
                        case HatHideEnum.OnlyDraftHHSB:
                            ToSave.hatsToHideUnlessDraftedHHSB.Add(kv.Key.defName);
                            break;
                        default: // ShowHair, do nothing
                            break;
                    }
                }

                ToSave.hairToHide = new List<string>();
                foreach(KeyValuePair<HairDef, bool> kv in HairToHide)
                    if (kv.Value)
                        ToSave.hairToHide.Add(kv.Key.defName);
            }

            Scribe_Collections.Look(ref ToSave.hatsThatHideHair, "HatsThatHide", LookMode.Value);
            Scribe_Collections.Look(ref ToSave.hatsToHideShowBeards, "HatsToHideShowBeards", LookMode.Value);
            Scribe_Collections.Look(ref ToSave.hatsToHideUnlessDraftedSH, "HatsToHideUnlessDraftedSH", LookMode.Value);
            Scribe_Collections.Look(ref ToSave.hatsToHideUnlessDraftedHH, "HatsToHideUnlessDraftedHH", LookMode.Value);
            Scribe_Collections.Look(ref ToSave.hatsToHideUnlessDraftedHHSB, "hatsToHideUnlessDraftedHHSB", LookMode.Value);
            Scribe_Collections.Look(ref ToSave.hatsToHide, "HatsToHide", LookMode.Value);
            Scribe_Collections.Look(ref ToSave.hairToHide, "HairToHide", LookMode.Value);
            Scribe_Values.Look<bool>(ref HideAllHats, "HideAllHats", false, false);
            Scribe_Values.Look<bool>(ref OnlyApplyToColonists, "OnlyApplyToColonists", false, false);
            Scribe_Values.Look<bool>(ref ShowHatsOnlyWhenDrafted, "ShowHatsOnlyWhenDrafted", false, false);
            Scribe_Values.Look<bool>(ref ShowHatsWhenDraftedIndoors, "ShowHatsWhenDraftedIndoors", false, false);
            Scribe_Values.Look<Indoors>(ref Indoors, "Indoors", Indoors.ShowHats, false);
            if (Scribe.mode != LoadSaveMode.Saving)
            {
                bool b = false;
                Scribe_Values.Look<bool>(ref b, "HideHatsIndoors", false, false);
                if (b)
                    Indoors = Indoors.HideHats;
            }
            Scribe_Values.Look<bool>(ref UpdatePortrait, "UpdatePortrait", false, false);
            Scribe_Values.Look<bool>(ref UseDontShaveHead, "UseDontShaveHead", true, false);

            if (Scribe.mode == LoadSaveMode.Saving)
            {
                ToSave?.Clear();
                ToSave = null;
            }

            OptionsOpen = false;
        }

        private static bool isInitialized = false;
        internal static void Initialize()
        {
            int defCount = 0;
            if (!isInitialized)
            {
                if (ToSave == null)
                    ToSave = new ToSave();

                foreach (ThingDef d in DefDatabase<ThingDef>.AllDefs)
                {
                    ++defCount;

                    if (d.apparel == null ||
                        !IsHeadwear(d.apparel) ||
                        (String.IsNullOrEmpty(d.apparel.wornGraphicPath) &&
                         d.apparel.wornGraphicPaths?.Count == 0))
                    {
                        continue;
                    }

                    HatHideEnum e = HatHideEnum.ShowsHair;
                    if (ToSave.hatsThatHideHair?.Contains(d.defName) == true)
                        e = HatHideEnum.HidesAllHair;
                    else if (ToSave.hatsToHideShowBeards?.Contains(d.defName) == true)
                        e = HatHideEnum.HidesHairShowBeard;
                    else if (ToSave.hatsToHide?.Contains(d.defName) == true)
                        e = HatHideEnum.HideHat;
                    else if (ToSave.hatsToHideUnlessDraftedSH?.Contains(d.defName) == true)
                        e = HatHideEnum.OnlyDraftSH;
                    else if (ToSave.hatsToHideUnlessDraftedHH?.Contains(d.defName) == true)
                        e = HatHideEnum.OnlyDraftHH;
                    else if (ToSave.hatsToHideUnlessDraftedHHSB?.Contains(d.defName) == true)
                        e = HatHideEnum.OnlyDraftHHSB;
                    HatsThatHide[d] = e;
                }

                foreach (HairDef d in DefDatabase<HairDef>.AllDefs)
                {
                    ++defCount;
                    bool selected = false;
                    if (ToSave.hairToHide != null)
                    {
                        foreach (string s in ToSave.hairToHide)
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
                    ToSave?.Clear();
                    ToSave = null;
                }
            }
        }

        public static bool IsHeadwear(ApparelProperties apparelProperties)
        {
            if (apparelProperties == null)
                return false;
            if (apparelProperties.LastLayer == ApparelLayerDefOf.Overhead || apparelProperties.LastLayer == ApparelLayerDefOf.EyeCover)
                return true;
            foreach (var g in apparelProperties.bodyPartGroups)
            {
                if (g == BodyPartGroupDefOf.FullHead || g == BodyPartGroupDefOf.UpperHead || g == BodyPartGroupDefOf.Eyes)
                {
                    return true;
                }
            }
            return false;
        }
    }

    class ToSave
    {
        public List<string> hatsThatHideHair = null;
        public List<string> hatsToHide = null;
        public List<string> hatsToHideShowBeards = null;
        public List<string> hatsToHideUnlessDraftedSH = null;
        public List<string> hatsToHideUnlessDraftedHH = null;
        public List<string> hatsToHideUnlessDraftedHHSB = null;
        public List<string> hairToHide = null;

        public ToSave()
        {
            hatsThatHideHair = new List<string>();
            hatsToHide = new List<string>();
            hatsToHideShowBeards = new List<string>();
            hatsToHideUnlessDraftedSH = new List<string>();
            hatsToHideUnlessDraftedHH = new List<string>();
            hatsToHideUnlessDraftedHHSB = new List<string>();
            hairToHide = new List<string>();
        }

        public void Clear()
        {
            hatsThatHideHair?.Clear();
            hatsToHide?.Clear();
            hatsToHideShowBeards?.Clear();
            hatsToHideUnlessDraftedSH?.Clear();
            hatsToHideUnlessDraftedHH?.Clear();
            hatsToHideUnlessDraftedHHSB?.Clear();
            hairToHide?.Clear();
        }
    }
}