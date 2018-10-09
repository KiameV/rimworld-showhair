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
            Settings.Initialize();
        }
    }
    
    [HarmonyPatch(typeof(SavedGameLoaderNow), "LoadGameFromSaveFileNow")]
    static class Patch_SavedGameLoader_LoadGameFromSaveFileNow
    {
        [HarmonyPriority(Priority.Last)]
        static void Postfix()
        {
            Settings.Initialize();
        }
    }

    [HarmonyPatch(
        typeof(PawnRenderer), "RenderPawnInternal", 
        new Type[] { typeof(Vector3), typeof(float), typeof(bool), typeof(Rot4), typeof(Rot4), typeof(RotDrawMode), typeof(bool), typeof(bool) })]
    public static class Patch_PawnRenderer_RenderPawnInternal
    {
        private static FieldInfo PawnFI = typeof(PawnRenderer).GetField("pawn", BindingFlags.NonPublic | BindingFlags.Instance);
        private static bool isDrafted = false;
        public static void Prefix(PawnRenderer __instance, ref Pawn __state)
        {
            __state = PawnFI.GetValue(__instance) as Pawn;
            if (__state != null && Settings.ShowHatsOnlyWhenDrafted && __instance != null)
            {
                isDrafted = false;
                if (__state.Faction == Faction.OfPlayer && __state.RaceProps.Humanlike)
                {
                    isDrafted = __state.Drafted;
                }
            }
        }

        public static void Postfix(PawnRenderer __instance, ref Pawn __state, Vector3 rootLoc, float angle, bool renderBody, Rot4 bodyFacing, Rot4 headFacing, RotDrawMode bodyDrawType, bool portrait, bool headStump)
        {
            if (__state != null && __instance.graphics.headGraphic != null)
            {
#if DEBUG
                bool isPawn = false;
                if (__state.Faction.def == FactionDefOf.PlayerColony || __state.Faction.def == FactionDefOf.PlayerTribe)
                    isPawn = __state.Name.ToStringShort.Equals("Happy");
#endif
                Quaternion quad = Quaternion.AngleAxis(angle, Vector3.up);
                Vector3 b = quad * __instance.BaseHeadOffsetAt(headFacing);
                Vector3 loc2 = rootLoc + b;
                bool forceShowHair = false;
                bool hideHats = HideHats(portrait);
                float hairLoc = 0;
                bool flag = false;
                List<ApparelGraphicRecord> apparelGraphics = __instance.graphics.apparelGraphics;
                for (int j = 0; j < apparelGraphics.Count; j++)
                {
                    Apparel sourceApparel = apparelGraphics[j].sourceApparel;
                    if (sourceApparel.def.apparel.LastLayer == ApparelLayerDefOf.Overhead)
                    {
#if DEBUG && T
                        int i = 10324;
                        Log.ErrorOnce("---hats:", i);
                        ++i;
                        foreach (KeyValuePair<ThingDef, bool> kv in Settings.HatsThatHide)
                        {
                            Log.ErrorOnce(kv.Key + "  " + kv.Value, i);
                            ++i;
                        }
                        Log.ErrorOnce("---Hair:", i);
                        ++i;
                        foreach (KeyValuePair<HairDef, bool> kv in Settings.HairThatShows)
                        {
                            Log.ErrorOnce(kv.Key + "  " + kv.Value, i);
                            ++i;
                        }
#endif
#if DEBUG
                        if (isPawn)
                        {
                            if (!Settings.HatsThatHide.TryGetValue(sourceApparel.def, out bool bb))
                                bb = false;
                            Log.Warning("Force no hair --- HatsThatHide[" + sourceApparel.def + "] = " + bb);
                        }
#endif
                        if (!hideHats)
                        {
                            if (!Settings.HatsThatHide.TryGetValue(sourceApparel.def, out bool force))
                                force = false;
                            forceShowHair = !force;
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
#if DEBUG && T
                if (isPawn && pawn.Name.ToStringShort.Equals("Happy"))
                {
                    Log.Warning(pawn.Name.ToStringShort + " Hair: " + pawn.story.hairDef.defName + " HideAllHats: " + Settings.HideAllHats + " forceShowHair: " + forceShowHair + " flag: " + flag + " bodyDrawType: " + bodyDrawType + " headStump: " + headStump);
                }
#endif
                if ((!flag && bodyDrawType != RotDrawMode.Dessicated && !headStump) || 
                    (!hideHats && Settings.HairToHide.TryGetValue(__state.story.hairDef, out bool v) && v))
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
                        GenDraw.DrawMeshNowOrLater(mesh4, loc2, quad, mat, portrait);
                    }
                }
            }
        }

        private static bool HideHats(bool portrait)
        {
            bool result;
            if (Settings.ShowHatsOnlyWhenDrafted)
            {
                result = !isDrafted;
            }
            else
            {
                result = Settings.HideAllHats || (portrait && Prefs.HatsOnlyOnMap);
            }
#if DEBUG && T
            Log.Warning(
                "Result: " + result +
                "- Settings.HideAllHats: " + Settings.HideAllHats + " Portrait: " + portrait + " Prefs.HatsOnlyOnMap: " + Prefs.HatsOnlyOnMap);
#endif
            return result;
        }

        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            MethodInfo hatsOnlyOnMap = AccessTools.Property(typeof(Prefs), nameof(Prefs.HatsOnlyOnMap)).GetGetMethod();
            List<CodeInstruction> instructionList = instructions.ToList();
#if DEBUG && TRANSPILER
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
#if DEBUG && TRANSPILER
                    printTranspiler(instruction, "Pre");
#endif
                    instruction.operand = typeof(Patch_PawnRenderer_RenderPawnInternal).GetMethod(
                        nameof(Patch_PawnRenderer_RenderPawnInternal.HideHats), BindingFlags.Static | BindingFlags.NonPublic);
#if DEBUG && TRANSPILER
                    printTranspiler(instruction);
#endif
                    yield return instruction;
                    i += 2;
                }
                else
                {
#if DEBUG && TRANSPILER
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

#if DEBUG && TRANSPILER
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