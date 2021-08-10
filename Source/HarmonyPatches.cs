using HarmonyLib;
using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using UnityEngine;
using Verse;

namespace ShowHair
{
    [StaticConstructorOnStartup]
    class HarmonyPatches
    {
        static HarmonyPatches()
        {
            if (ModLister.GetActiveModWithIdentifier("CETeam.CombatExtended") != null)
            {
                Log.Error("[Show Hair With Hats] IS NOT COMPATABLE WITH COMBAT EXTENDED.");
            }

            var harmony = new Harmony("com.showhair.rimworld.mod");
            harmony.PatchAll(Assembly.GetExecutingAssembly());
        }
    }

    [HarmonyPatch(typeof(Game), "InitNewGame")]
    static class Patch_Game_InitNewGame
    {
        static void Postfix()
        {
            Settings.Initialize();
            //Patch_PawnRenderer_DrawHeadHair.Initialize();
        }
    }

    [HarmonyPatch(typeof(SavedGameLoaderNow), "LoadGameFromSaveFileNow")]
    static class Patch_SavedGameLoader_LoadGameFromSaveFileNow
    {
        [HarmonyPriority(Priority.Last)]
        static void Postfix()
        {
            Settings.Initialize();
            //Patch_PawnRenderer_DrawHeadHair.Initialize();

        }
    }

    [HarmonyPatch(typeof(Pawn_DraftController), "set_Drafted")]
    static class Patch_Pawn_DraftController
    {
        static void Postfix(Pawn_DraftController __instance)
        {
            var p = __instance.pawn;
            if (p.IsColonist && !p.Dead && p.def.race.Humanlike)
            {
                PortraitsCache.SetDirty(p);
                GlobalTextureAtlasManager.TryMarkPawnFrameSetDirty(p);
            }
        }
    }

    /*[HarmonyPatch(typeof(Pawn), "SpawnSetup")]
    static class Patch_Pawn_TickRare
    {
        static void Postfix(Pawn __instance)
        {
            if (__instance.RaceProps.Humanlike)
            {
                if (__instance.TryGetComp<CompCeilingDetect>() == null)
                {
                    var fi = typeof(Pawn).GetField("comps", BindingFlags.NonPublic | BindingFlags.Instance);
                    List<ThingComp> comps = (List<ThingComp>)fi.GetValue(__instance);
                    var c = (ThingComp)Activator.CreateInstance(typeof(CompCeilingDetect));
                    c.parent = __instance;
                    comps.Add(c);
                    c.Initialize(new CompProperties_CeilingDetect());
                    if (comps != null)
                        comps.Add(c);
                    else
                    {
                        comps = new List<ThingComp>() { c };
                        fi.SetValue(__instance, comps);
                    }
                }
            }
        }
    }*/

   /* public class AAA
    {
        private void DrawHeadHair(Vector3 rootLoc, Vector3 headOffset, float angle, Rot4 bodyFacing, Rot4 headFacing, RotDrawMode bodyDrawType, PawnRenderFlags flags)
        {
            Vector3 onHeadLoc = rootLoc + headOffset;
            onHeadLoc.y += 0.0289575271f;
            List<ApparelGraphicRecord> apparelGraphics = null;
            Quaternion quat = Quaternion.AngleAxis(angle, Vector3.up);
            bool flag = false;
            bool flag2 = bodyFacing == Rot4.North;
            bool flag3 = flags.FlagSet(PawnRenderFlags.Headgear) && (!flags.FlagSet(PawnRenderFlags.Portrait) || !Prefs.HatsOnlyOnMap || flags.FlagSet(PawnRenderFlags.StylingStation));
            Patch_PawnRenderer_DrawHeadHair.HideHats(ref flag, ref flag2, ref flag3, bodyFacing, this);
        }
    }*/

    [HarmonyPatch(typeof(PawnRenderer), "DrawHeadHair")]
    public static class Patch_PawnRenderer_DrawHeadHair
    {
        private static bool isDrafted;
        private static Pawn pawn;
        private static List<ApparelGraphicRecord> apparelGraphics;
        private static PawnRenderFlags flags;

        // Used for children pawns
        //private static bool typesInitialized = false;
        //private static MethodInfo getBodySizeScalingMI = null;
        //private static MethodInfo getModifiedHairMeshSetMI = null;
        //private static bool hasAlienRaces = false;

        /*public static void Initialize()
        {
            if (!typesInitialized)
            {
                typesInitialized = true;
                try
                {
                    typesInitialized = true;
                    foreach (var mod in LoadedModManager.RunningMods)
                    {
                        if (mod.Name.IndexOf("Children, school and learning") != -1)
                        {
                            Assembly assembly = mod.assemblies.loadedAssemblies.FirstOrDefault(a => a.GetName().Name == "ChildrenHelperClasses");
                            if (assembly != null)
                            {
                                Type type = assembly?.GetType("Children.ChildrenHarmony+PawnRenderer_RenderPawnInternal_Patch");
                                if (type != null)
                                {
                                    getBodySizeScalingMI = type.GetMethod("GetBodysizeScaling", BindingFlags.NonPublic | BindingFlags.Static);//pawn.ageTracker.get_CurLifeStage().bodySizeFactor
                                    getModifiedHairMeshSetMI = type.GetMethod("GetModifiedHairMeshSet", BindingFlags.NonPublic | BindingFlags.Static);// (bodySizeFactor, pawn).MeshAt(headFacing);
                                }
                            }
                        }
                        else if (mod.Name.IndexOf("Humanoid Alien Races") == 0)
                        {
                            hasAlienRaces = true;
                        }
                    }
                }
                catch (Exception e)
                {
                    Log.Warning("Failed to patch [Children, school and learning]\n" + e.GetType().Name + " " + e.Message);
                }
            }
        }*/

        [HarmonyPriority(Priority.First)]
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            List<CodeInstruction> il = instructions.ToList();
#if DEBUG && TRANSPILER
            bool firstAfterFound = true;
#endif

            MethodInfo get_IdeologyActive = AccessTools.Property(typeof(ModsConfig), nameof(ModsConfig.IdeologyActive))?.GetGetMethod();
            MethodInfo hideHats = 
                        typeof(Patch_PawnRenderer_DrawHeadHair).GetMethod(
                        nameof(Patch_PawnRenderer_DrawHeadHair.HideHats), BindingFlags.Static | BindingFlags.Public);
            
            bool found = false;
            for (int i = 0; i < il.Count; ++i)
            {
                if (il[i].opcode == OpCodes.Call && il[i].OperandIs(get_IdeologyActive))
                {
                    found = true;

                    // Override this instruction as it's the goto for the end of the if clause
                    il[i].opcode = OpCodes.Ldloca_S;
                    il[i].operand = 2;
                    yield return il[i];
                    yield return new CodeInstruction(OpCodes.Ldloca_S, 3);
                    yield return new CodeInstruction(OpCodes.Ldloca_S, 4);
                    yield return new CodeInstruction(OpCodes.Ldarg_S, 4);
                    yield return new CodeInstruction(OpCodes.Call, hideHats);
                    // Create the overridden instruction
                    yield return new CodeInstruction(OpCodes.Call, get_IdeologyActive);
                    ++i;

                }
                yield return il[i];
            }
            if (!found)
            {
                Log.Error("Show Hair or Hide All Hats could not inject itself properly. This is due to other mods modifying the same code this mod needs to modify.");
            }
        }

        [HarmonyPriority(Priority.First)]
        public static void Prefix(PawnRenderer __instance, Pawn ___pawn, Vector3 rootLoc, Vector3 headOffset, float angle, Rot4 bodyFacing, Rot4 headFacing, RotDrawMode bodyDrawType, PawnRenderFlags flags)
        {
            pawn = ___pawn;
            if (pawn == null || __instance == null)
                return;

            isDrafted = pawn.RaceProps.Humanlike && pawn.Drafted;

            apparelGraphics = __instance.graphics.apparelGraphics;

            Patch_PawnRenderer_DrawHeadHair.flags = flags;
        }

        public static void HideHats(ref bool hideHair, ref bool hideBeard, ref bool showHat, Rot4 bodyFacing)
        {
            //Log.Error($"Start {pawn.Name.ToStringShort} hideHair:{hideHair}  hideBeard:{hideBeard}  showHat:{showHat}");
            // Determine if hat should be shown
            if (Settings.OptionsOpen ||
                flags.FlagSet(PawnRenderFlags.Portrait) && Prefs.HatsOnlyOnMap)
            {
                showHat = false;
                hideHair = false;
                hideBeard = hideHair;
                if (!hideBeard)
                    hideBeard = bodyFacing == Rot4.North;
                //Log.Error($"0 {pawn.Name.ToStringShort} hideHair:{hideHair}  hideBeard:{hideBeard}  showHat:{showHat}");
                return;
            }

            if (showHat == false || hideHair == false ||
                Settings.OnlyApplyToColonists && pawn.Faction.IsPlayer == false)
            {
                //Log.Error($"1 {pawn.Name.ToStringShort} hideHair:{hideHair}  hideBeard:{hideBeard}  showHat:{showHat}");
                return;
            }

            hideHair = false;
            hideBeard = bodyFacing == Rot4.North;

            if (Settings.HideAllHats)
            {
                showHat = false;
                hideBeard = hideHair;
                if (!hideBeard)
                    hideBeard = bodyFacing == Rot4.North;
                //Log.Error($"2 {pawn.Name.ToStringShort} hideHair:{hideHair}  hideBeard:{hideBeard}  showHat:{showHat}");
                return;
            }

            if (Settings.ShowHatsOnlyWhenDrafted)
            {
                showHat = isDrafted;
                //Log.Error($"3 {pawn.Name.ToStringShort} hideHair:{hideHair}  hideBeard:{hideBeard}  showHat:{showHat}");
            }
            else if (showHat && Settings.HideHatsIndoors)
            {
                CompCeilingDetect comp = pawn.GetComp<CompCeilingDetect>();
                if (comp != null && comp.IsIndoors)
                {
                    showHat = false;
                    hideHair = false;
                    //Log.Error($"finally {pawn.Name.ToStringShort} hideHair:{hideHair}  hideBeard:{hideBeard}  showHat:{showHat}");
                }
            }

            bool hide;
            Apparel apparel;
            ThingDef def;
            for (int j = 0; j < apparelGraphics.Count; j++)
            {
                apparel = apparelGraphics[j].sourceApparel;
                def = apparel.def;
                if (Settings.IsHeadwear(apparel.def.apparel))
                {
                    if (Settings.HatsToHide.TryGetValue(def, out hide) && hide)
                    {
                        hideHair = false;
                        showHat = false;
                        //Log.Error($"4 {pawn.Name.ToStringShort} hideHair:{hideHair}  hideBeard:{hideBeard}  showHat:{showHat}");
                        return;
                    }
                    if (Settings.HatsThatHide.TryGetValue(def, out hide) && hide)
                    {
                        hideHair = true;
                        showHat = true;
                        //Log.Error($"5 {pawn.Name.ToStringShort} hideHair:{hideHair}  hideBeard:{hideBeard}  showHat:{showHat}");
                        return;
                    }
                }
            }
            if (Settings.HairToHide.TryGetValue(pawn.story.hairDef, out hide) && hide)
            {
                hideHair = true;
                showHat = true;
                //Log.Error($"6 {pawn.Name.ToStringShort} hideHair:{hideHair}  hideBeard:{hideBeard}  showHat:{showHat}");
                return;
            }
            hideBeard = hideHair;
            if (!hideBeard)
                hideBeard = bodyFacing == Rot4.North;

            //Log.Error($"{pawn.Name.ToStringShort} hideHair:{hideHair}  hideBeard:{hideBeard}  showHat:{showHat}");
        }

#if DEBUG && TRANSPILER
        static void printTranspiler(CodeInstruction i, string pre = "")
        {
            Log.Warning("CodeInstruction: " + pre + " opCode: " + i.opcode + " operand: " + i.operand + " labels: " + printLabels(i.ExtractLabels()));
        }

        static string printLabels(IEnumerable<Label> labels)
        {
            System.Text.StringBuilder sb = new System.Text.StringBuilder();
            if (labels == null)
            {
                sb.Append("<null labels>");
            }
            else
            {
                foreach (Label l in labels)
                {
                    if (sb.Length > 0)
                    {
                        sb.Append(", ");
                    }
                    sb.Append(l);
                }
            }
            if (sb.Length == 0)
            {
                sb.Append("<empty labels>");
            }
            return sb.ToString();
        }
#endif
            }
}