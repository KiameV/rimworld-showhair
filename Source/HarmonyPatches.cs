using Harmony;
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
            var harmony = HarmonyInstance.Create("com.showhair.rimworld.mod");
            harmony.PatchAll(Assembly.GetExecutingAssembly());

            Log.Message(
                "Show Hair Harmony Patches:" + Environment.NewLine +
                "  Transpiler:" + Environment.NewLine +
                "    PawnRenderer.RenderPawnInternal" + Environment.NewLine +
                "  Postfix:" + Environment.NewLine +
                "    PawnRenderer.RenderPawnInternal" + Environment.NewLine +
                "    Game.InitNewGame" + Environment.NewLine +
                "    SavedGameLoader.LoadGameFromSaveFile");
        }
    }

    [HarmonyPatch(typeof(Game), "InitNewGame")]
    static class Patch_Game_InitNewGame
    {
        static void Postfix()
        {
            SettingsController.InitializeAllHats();
        }
    }
    
    [HarmonyPatch(typeof(SavedGameLoader), "LoadGameFromSaveFile")]
    static class Patch_SavedGameLoader_LoadGameFromSaveFile
    {
        static void Postfix()
        {
            SettingsController.InitializeAllHats();
        }
    }

    [HarmonyPatch(typeof(PawnRenderer), "RenderPawnInternal", new Type[] { typeof(Vector3), typeof(Quaternion), typeof(bool), typeof(Rot4), typeof(Rot4), typeof(RotDrawMode), typeof(bool), typeof(bool) })]
    public static class Patch_PawnRenderer_RenderPawnInternal
    {
        public static void Postfix(PawnRenderer __instance, Vector3 rootLoc, Quaternion quat, bool renderBody, Rot4 bodyFacing, Rot4 headFacing, RotDrawMode bodyDrawType, bool portrait, bool headStump)
        {
            if (__instance.graphics.headGraphic != null)
            {
                Vector3 b = quat * __instance.BaseHeadOffsetAt(headFacing);
                Vector3 loc2 = rootLoc + b;
                bool forceShowHair = false;
                bool hideHats = HideHats(portrait);
                float hairLoc = 0;
                bool flag = false;
                List<ApparelGraphicRecord> apparelGraphics = __instance.graphics.apparelGraphics;
                for (int j = 0; j < apparelGraphics.Count; j++)
                {
                    Apparel sourceApparel = apparelGraphics[j].sourceApparel;
                    if (sourceApparel.def.apparel.LastLayer == ApparelLayer.Overhead)
                    {
#if DEBUG
                        if (isPawn && count > COUNT_FOR_LOG)
                        {
                            Log.Warning("Force no hair: " + SettingsController.HatsThatHideHair.Contains(sourceApparel.def));
                        }
#endif
                        if (!hideHats)
                        {
                            forceShowHair = !SettingsController.HatsThatHideHair.Contains(sourceApparel.def);
                        }

                        if (!sourceApparel.def.apparel.hatRenderedFrontOfFace)
                        {
                            flag = true;
                            loc2.y += 0.03125f;
                            hairLoc = loc2.y;
                        }
                        else
                        {
                            Vector3 loc3 = rootLoc + b;
                            loc3.y += ((!(bodyFacing == Rot4.North)) ? 0.03515625f : 0.00390625f);
                            hairLoc = loc3.y;
                        }
                    }
                }
#if DEBUG
                if (isPawn && count > COUNT_FOR_LOG)
                {
                    Log.Warning("HideAllHats: " + SettingsController.HideAllHats + " forceShowHair: " + forceShowHair + " flag: " + flag + " bodyDrawType: " + bodyDrawType + " headStump: " + headStump);
                }
#endif

                if (!flag && bodyDrawType != RotDrawMode.Dessicated && !headStump)
                {
                    // Hair was already rendered
                }
                else if ((hideHats || forceShowHair) && hairLoc > 0)
                {
                    if (hairLoc > 0.001f)
                    {
                         loc2.y = hairLoc - 0.001f;

                        Mesh mesh4 = __instance.graphics.HairMeshSet.MeshAt(headFacing);
                        Material mat = __instance.graphics.HairMatAt(headFacing);
                        GenDraw.DrawMeshNowOrLater(mesh4, loc2, quat, mat, portrait);
                    }
                }
            }
        }

        private static bool HideHats(bool port)
        {
            bool result = SettingsController.HideAllHats || (port && Prefs.HatsOnlyOnMap);
#if DEBUG
            Log.Warning(
                "Result: " + result +
                "- Settings.HideAllHats: " + SettingsController.HideAllHats + " Portrait: " + portrait + " Prefs.HatsOnlyOnMap: " + Prefs.HatsOnlyOnMap);
#endif
            return result;
        }

        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            MethodInfo hatsOnlyOnMap = AccessTools.Property(typeof(Prefs), nameof(Prefs.HatsOnlyOnMap)).GetGetMethod();
            List<CodeInstruction> instructionList = instructions.ToList();
#if DEBUG
            bool firstAfterFound = true;
#endif
            bool found = false;
            for (int i = 0; i < instructionList.Count; ++i)
            {
                CodeInstruction instruction = instructionList[i];
                if (instructionList.Count > i + 2 &&
                    instructionList[i + 2].opcode == OpCodes.Call &&
                    instructionList[i + 2].operand == hatsOnlyOnMap)
                {
                    found = true;

                    yield return instruction;

                    // Skip brfalse IL_0267 (5 nop)
                    /*for (int nop = 0; nop < 5; ++nop)
                    {
                        yield return new CodeInstruction(OpCodes.Nop);
                    }*/

                    // Call RenderHat
                    instruction = instructionList[i + 2];
#if DEBUG
                    printTranspiler(instruction, "Pre");
#endif
                    instruction.operand = typeof(Patch_PawnRenderer_RenderPawnInternal).GetMethod(
                        nameof(Patch_PawnRenderer_RenderPawnInternal.HideHats), BindingFlags.Static | BindingFlags.NonPublic);
#if DEBUG
                    printTranspiler(instruction);
#endif
                    yield return instruction;
                    i += 2;
                }
                else
                {
#if DEBUG
                    if (found && first)
                    {
                        printTranspiler(instruction);
                        first = false;
                    }
#endif
                    yield return instruction;
                }
            }
            if (!found)
            {
                Log.Error("Show Hair or Hide All Hats could not inject itself properly. This is due to other mods modifying the same code this mod needs to modify.");
            }
        }

#if DEBUG
        static void printTranspiler(CodeInstruction i, string pre = "")
        {
            Log.Warning("CodeInstruction: " + pre + " opCode: " + i.opcode + " operand: " + i.operand + " labels: " + s(i.labels));
        }

        static string printTranspiler(IEnumerable<Label> labels)
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