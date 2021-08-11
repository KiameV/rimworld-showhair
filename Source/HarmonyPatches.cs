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
        private static Rot4 headFacing;
        private static bool skipDontShaveHead;

        [HarmonyPriority(Priority.First)]
        public static void Prefix(PawnRenderer __instance, Pawn ___pawn, Vector3 rootLoc, Vector3 headOffset, float angle, Rot4 bodyFacing, Rot4 headFacing, RotDrawMode bodyDrawType, PawnRenderFlags flags)
        {
            pawn = ___pawn;
            if (pawn == null || __instance == null)
                return;

            isDrafted = pawn.RaceProps.Humanlike && pawn.Drafted;

            apparelGraphics = __instance.graphics.apparelGraphics;

            Patch_PawnRenderer_DrawHeadHair.flags = flags;
            Patch_PawnRenderer_DrawHeadHair.headFacing = headFacing;
            skipDontShaveHead = false;
        }

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
            MethodInfo drawMeshNowOrLater = AccessTools.Method(typeof(GenDraw), nameof(GenDraw.DrawMeshNowOrLater), new Type[] { typeof(Mesh), typeof(Vector3), typeof(Quaternion), typeof(Material), typeof(bool) });
            MethodInfo drawMeshNowOrLaterPatch =
                        typeof(Patch_PawnRenderer_DrawHeadHair).GetMethod(
                        nameof(Patch_PawnRenderer_DrawHeadHair.DrawMeshNowOrLaterPatch), BindingFlags.Static | BindingFlags.NonPublic);

            bool found1 = false, found2 = false;
            int drawFound = 0;
            for (int i = 0; i < il.Count; ++i)
            {
                // Inject after the show/hide flags are set but before they're used
                if (!found1 && il[i].opcode == OpCodes.Call && il[i].OperandIs(get_IdeologyActive))
                {
                    found1 = true;

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
                if (il[i].opcode == OpCodes.Call && il[i].OperandIs(drawMeshNowOrLater))
                {
                    ++drawFound;
                    if (drawFound == 3)
                    {
                        found2 = true;
                        il[i].operand = drawMeshNowOrLaterPatch;
                    }
                }
                yield return il[i];
            }
            if (!found1 && !found2)
            {
                Log.Error("Show Hair or Hide All Hats could not inject itself properly. This is due to other mods modifying the same code this mod needs to modify.");
            }
        }

        private static void DrawMeshNowOrLaterPatch(Mesh mesh, Vector3 loc, Quaternion quat, Material mat, bool drawNow)
        {
            //Log.Warning($"DrawMeshNowOrLaterPatch {mat.name}");
            if (!skipDontShaveHead && Settings.UseDontShaveHead && HairUtilityFactory.GetHairUtility().TryGetCustomHairMat(pawn, headFacing, out Material m))
            {
                mat = m;
                //Log.Warning($"-UseDontShaveHead {mat.name}");
            }
            GenDraw.DrawMeshNowOrLater(mesh, loc, quat, mat, drawNow);
        }

        public static void HideHats(ref bool hideHair, ref bool hideBeard, ref bool showHat, Rot4 bodyFacing)
        {
            try
            {
                //Log.Error($"Start {pawn.Name.ToStringShort} hideHair:{hideHair}  hideBeard:{hideBeard}  showHat:{showHat}");
                // Determine if hat should be shown
                if (Settings.OptionsOpen ||
                    flags.FlagSet(PawnRenderFlags.Portrait) && Prefs.HatsOnlyOnMap)
                {
                    showHat = false;
                    hideHair = false;
                    //Log.Error($"0 {pawn.Name.ToStringShort} hideHair:{hideHair}  hideBeard:{hideBeard}  showHat:{showHat}");
                    return;
                }

                if (showHat == false ||
                    Settings.OnlyApplyToColonists && pawn.Faction.IsPlayer == false)
                {
                    //Log.Error($"1 {pawn.Name.ToStringShort} hideHair:{hideHair}  hideBeard:{hideBeard}  showHat:{showHat}");
                    return;
                }

                if (hideHair == false)
                {
                    CheckHideHat(ref hideHair, ref showHat, true);
                    //Log.Error($"2 {pawn.Name.ToStringShort} hideHair:{hideHair}  hideBeard:{hideBeard}  showHat:{showHat}");
                    return;
                }

                hideHair = false;

                if (Settings.HideAllHats)
                {
                    showHat = false;
                    //Log.Error($"3 {pawn.Name.ToStringShort} hideHair:{hideHair}  hideBeard:{hideBeard}  showHat:{showHat}");
                    return;
                }

                if (Settings.ShowHatsOnlyWhenDrafted)
                {
                    showHat = isDrafted;
                    //Log.Error($"4.a {pawn.Name.ToStringShort} hideHair:{hideHair}  hideBeard:{hideBeard}  showHat:{showHat}");
                }
                else if (showHat && Settings.HideHatsIndoors)
                {
                    CompCeilingDetect comp = pawn.GetComp<CompCeilingDetect>();
                    if (comp != null && comp.IsIndoors)
                    {
                        showHat = false;
                        hideHair = false;
                        return;
                        //Log.Error($"4.b {pawn.Name.ToStringShort} hideHair:{hideHair}  hideBeard:{hideBeard}  showHat:{showHat}");
                    }
                }

                if (Settings.HairToHide.TryGetValue(pawn.story.hairDef, out bool hide) && hide)
                {
                    hideHair = true;
                    showHat = true;
                    //Log.Error($"5 {pawn.Name.ToStringShort} hideHair:{hideHair}  hideBeard:{hideBeard}  showHat:{showHat}");
                    return;
                }

                CheckHideHat(ref hideHair, ref showHat, false);
            }
            finally
            {
                hideBeard = hideHair;
                if (!hideBeard)
                    hideBeard = bodyFacing == Rot4.North;
                skipDontShaveHead = !showHat;
            }
            //Log.Error($"Final {pawn.Name.ToStringShort} hideHair:{hideHair}  hideBeard:{hideBeard}  showHat:{showHat}");
        }

        private static void CheckHideHat(ref bool hideHair, ref bool showHat, bool skipHatsThatHide)
        {
            bool hide;
            Apparel apparel;
            ThingDef def;
            for (int j = 0; j < apparelGraphics.Count; j++)
            {
                apparel = apparelGraphics[j].sourceApparel;
                def = apparel.def;
                if (Settings.IsHeadwear(apparel.def.apparel))
                {
                    //Log.Error("Last Layer " + def.defName);
                    if (Settings.HatsToHide.TryGetValue(def, out hide) && hide)
                    {
                        hideHair = false;
                        showHat = false;
                        //Log.Error($"6 {pawn.Name.ToStringShort} hideHair:{hideHair}  hideBeard:{hideBeard}  showHat:{showHat}");
                        return;
                    }
                    if (!skipHatsThatHide && Settings.HatsThatHide.TryGetValue(def, out hide) && hide)
                    {
                        hideHair = true;
                        showHat = true;
                        //Log.Error($"7 {pawn.Name.ToStringShort} hideHair:{hideHair}  hideBeard:{hideBeard}  showHat:{showHat}");
                        return;
                    }
                }
            }
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