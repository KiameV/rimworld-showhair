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

        [HarmonyPriority(Priority.VeryHigh)]
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
                    yield return new CodeInstruction(OpCodes.Ldarg_S, (byte)4);
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

            isDrafted =
                pawn.Faction == Faction.OfPlayer &&
                pawn.RaceProps.Humanlike && 
                pawn.Drafted;

            apparelGraphics = __instance.graphics.apparelGraphics;

            Patch_PawnRenderer_DrawHeadHair.flags = flags;
        }

        /*private static bool DrawAlienPawn(Pawn pawn, Rot4 headFacing, Vector3 loc2, Quaternion quad, Material mat, bool portrait)
        {
            foreach (var comp in pawn.AllComps)
            {
                if (comp.GetType().Name.IndexOf("AlienComp") >= 0)
                {
                    if (alienPortraitHeadGraphicsFI == null)
                    {
                        alienPortraitHeadGraphicsFI = comp.GetType().GetField("alienPortraitHeadGraphics", BindingFlags.Public | BindingFlags.Instance);
                        if (alienPortraitHeadGraphicsFI == null)
                        {
                            string msg = "Failed to get alienPortraitHeadGraphics";
                            Log.ErrorOnce(msg, msg.GetHashCode());
                            return false;
                        }
                        alienHeadGraphicsFI = comp.GetType().GetField("alienHeadGraphics", BindingFlags.Public | BindingFlags.Instance);
                        if (alienHeadGraphicsFI == null)
                        {
                            string msg = "Failed to get alienHeadGraphics";
                            Log.ErrorOnce(msg, msg.GetHashCode());
                            return false;
                        }
                        hairSetAverageFI = alienHeadGraphicsFI.GetValue(comp).GetType().GetField("hairSetAverage", BindingFlags.Public | BindingFlags.Instance);
                        if (hairSetAverageFI == null)
                        {
                            string msg = "Failed to get hairSetAverage";
                            Log.ErrorOnce(msg, msg.GetHashCode());
                            return false;
                        }
                    }

                    GraphicMeshSet m;
                    if (portrait)
                    {
                        var f = alienPortraitHeadGraphicsFI.GetValue(comp);
                        if (f == null)
                        {
                            string msg = "Failed to get alienPortraitHeadGraphics from comp";
                            Log.ErrorOnce(msg, msg.GetHashCode());
                            return false;
                        }
                        m = hairSetAverageFI.GetValue(f) as GraphicMeshSet;
                        if (m == null)
                        {
                            string msg = "Failed to get mesh from from alienPortraitHeadGraphics";
                            Log.ErrorOnce(msg, msg.GetHashCode());
                            return false;
                        }
                    }
                    else
                    {
                        var f = alienHeadGraphicsFI.GetValue(comp);
                        if (f == null)
                        {
                            string msg = "Failed to get alienHeadGraphics from comp";
                            Log.ErrorOnce(msg, msg.GetHashCode());
                            return false;
                        }
                        m = hairSetAverageFI.GetValue(f) as GraphicMeshSet;
                        if (m == null)
                        {
                            string msg = "Failed to get mesh from from hairSetAverage";
                            Log.ErrorOnce(msg, msg.GetHashCode());
                            return false;
                        }
                    }
                    GenDraw.DrawMeshNowOrLater(m.MeshAt(headFacing), loc2, quad, mat, portrait);
                    return true;
                }
            }
            return false;
        }*/

        //private static FieldInfo alienPortraitHeadGraphicsFI = null;
        //private static FieldInfo alienHeadGraphicsFI = null;
        //private static FieldInfo hairSetAverageFI = null;

        //private static Dictionary<Pawn, bool> previousHatConfig = new Dictionary<Pawn, bool>();

        public static void HideHats(ref bool hideHair, ref bool hideBeard, ref bool showHat, Rot4 bodyFacing)
        {
            hideHair = false;
            for (int i = 0; i > 100; ++i)
                Log.Error(":");
            // Determine if hat should be shown
            if (Settings.OptionsOpen ||
                flags.FlagSet(PawnRenderFlags.Portrait) && Prefs.HatsOnlyOnMap)
            {
                showHat = false;
                hideBeard = bodyFacing == Rot4.North;
                //Log.Error($"0  hideHair:{hideHair}  hideBeard:{hideBeard}  showHat:{showHat}");
                return;
            }

            if (Settings.OnlyApplyToColonists && pawn.Faction != Faction.OfPlayer)
            {
                //Log.Error($"1  hideHair:{hideHair}  hideBeard:{hideBeard}  showHat:{showHat}");
                return;
            }

            if (Settings.HideAllHats)
            {
                showHat = false;
                hideHair = false;
                hideBeard = bodyFacing == Rot4.North;
                //Log.Error($"2  hideHair:{hideHair}  hideBeard:{hideBeard}  showHat:{showHat}");
                return;
            }

            if (Settings.ShowHatsOnlyWhenDrafted)
            {
                showHat = isDrafted;
                if (!showHat)
                {
                    hideHair = false;
                    hideBeard = bodyFacing == Rot4.North;
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
                        hideBeard = bodyFacing == Rot4.North;
                        //Log.Error($"4  hideHair:{hideHair}  hideBeard:{hideBeard}  showHat:{showHat}");
                        return;
                    }
                    if (Settings.HatsThatHide.TryGetValue(def, out hide) && hide)
                    {
                        hideHair = true;
                        showHat = true;
                        hideBeard = true;
                        //Log.Error($"5  hideHair:{hideHair}  hideBeard:{hideBeard}  showHat:{showHat}");
                        return;
                    }
                }
            }
            if (Settings.HairToHide.TryGetValue(pawn.story.hairDef, out hide) && hide)
            {
                hideHair = true;
                showHat = true;
                hideBeard = true;
                //Log.Error($"6  hideHair:{hideHair}  hideBeard:{hideBeard}  showHat:{showHat}");
                return;
            }

            //Log.Error($"7  hideHair:{hideHair}  hideBeard:{hideBeard}  showHat:{showHat}");
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