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
            Patch_PawnRenderer_DrawHeadHair.Initialize();
        }
    }

    [HarmonyPatch(typeof(SavedGameLoaderNow), "LoadGameFromSaveFileNow")]
    static class Patch_SavedGameLoader_LoadGameFromSaveFileNow
    {
        [HarmonyPriority(Priority.Last)]
        static void Postfix()
        {
            Settings.Initialize();
            Patch_PawnRenderer_DrawHeadHair.Initialize();

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
        private static bool typesInitialized = false;
        private static MethodInfo getBodySizeScalingMI = null;
        private static MethodInfo getModifiedHairMeshSetMI = null;
        private static bool hasAlienRaces = false;

        public static void Initialize()
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
        }

        [HarmonyPriority(Priority.High)]
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            //MethodInfo hatsOnlyOnMap = AccessTools.Property(typeof(Prefs), nameof(Prefs.HatsOnlyOnMap)).GetGetMethod();
            List<CodeInstruction> il = instructions.ToList();
#if DEBUG && TRANSPILER
            bool firstAfterFound = true;
#endif

            /*MethodInfo hairInfo = AccessTools.Property(typeof(PawnGraphicSet), nameof(PawnGraphicSet.HairMeshSet)).GetGetMethod();

            List<CodeInstruction> instructionList = instructions.ToList();

            for (int i = 0; i < instructionList.Count; i++)
            {
                CodeInstruction instruction = instructionList[i];

                if (i + 4 < instructionList.Count && instructionList[i + 2].OperandIs(hairInfo))
                {
                    yield return new CodeInstruction(OpCodes.Ldarg_S, 7) { labels = instruction.ExtractLabels() }; // renderFlags
                    yield return new CodeInstruction(OpCodes.Ldarg_0);
                    yield return new CodeInstruction(OpCodes.Ldfld, AccessTools.Field(typeof(PawnRenderer), name: "pawn"));
                    yield return new CodeInstruction(OpCodes.Ldarg_S, operand: 5); //headfacing
                    yield return new CodeInstruction(OpCodes.Ldarg_0);
                    yield return new CodeInstruction(OpCodes.Ldfld, AccessTools.Field(typeof(PawnRenderer), nameof(PawnRenderer.graphics)));
                    instruction = new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(Patch_PawnRenderer_DrawHeadHair), nameof(HideHats)));
                    instructionList.RemoveRange(i, count: 4);
                }

                yield return instruction;
            }*/

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

                    il[i].opcode = OpCodes.Ldloca_S;
                    il[i].operand = 3;
                    yield return il[i];
                    yield return new CodeInstruction(OpCodes.Ldloca_S, operand: 4);
                    yield return new CodeInstruction(OpCodes.Ldloca_S, operand: 5);
                    yield return new CodeInstruction(OpCodes.Call, hideHats);
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
        /*public static void Print(IEnumerable<CodeInstruction> instructions)
        {
            StringBuilder sb = new StringBuilder("Code:");
            uint i = 0;
            foreach (var c in instructions)
            {
                sb.AppendLine();
                if (c.opcode == OpCodes.Call ||
                    c.opcode == OpCodes.Callvirt)
                { // you can make certain operations more visible
                    sb.Append($"{i}: {c}");
                }
                else
                {
                    sb.Append($"{i}: {c}");
                }
                ++i;
            }
            Log.Error(sb.ToString()); // or just print the entire thing out to copy to a text editor.
        }*/

        public static void Prefix(PawnRenderer __instance, Pawn ___pawn, Vector3 rootLoc, Vector3 headOffset, float angle, Rot4 bodyFacing, Rot4 headFacing, RotDrawMode bodyDrawType, PawnRenderFlags flags)
        {
            pawn = ___pawn;
            if (pawn == null || __instance == null)
                return;

            isDrafted =
                Settings.ShowHatsOnlyWhenDrafted &&
                pawn.Faction == Faction.OfPlayer &&
                pawn.RaceProps.Humanlike;

            apparelGraphics = __instance.graphics.apparelGraphics;

            Patch_PawnRenderer_DrawHeadHair.flags = flags;
        }
        //Vector3 rootLoc, float angle, bool renderBody, Rot4 bodyFacing, Rot4 headFacing, RotDrawMode bodyDrawType, bool portrait, bool headStump
        /*public static void Postfix(PawnRenderer __instance, Vector3 rootLoc, float angle, bool renderBody, Rot4 bodyFacing, RotDrawMode bodyDrawType, PawnRenderFlags flags)
        {
            /*if (pawn != null && __instance.graphics.headGraphic != null)
            {
#if DEBUG
                bool isPawn = false;
                if (__state.Faction.def == FactionDefOf.PlayerColony || __state.Faction.def == FactionDefOf.PlayerTribe)
                    isPawn = __state.Name.ToStringShort.Equals("Happy");
#endif
                Quaternion quad = Quaternion.AngleAxis(angle, Vector3.up);
                Vector3 b = quad * __instance.BaseHeadOffsetAt(bodyFacing);
                Vector3 loc2 = rootLoc + b;
                bool forceShowHair = false;
                bool hideHats = HideHats(flags.FlagSet(PawnRenderFlags.Portrait));
                float hairLoc = 0;
                bool flag = false;
                List<ApparelGraphicRecord> apparelGraphics = __instance.graphics.apparelGraphics;
                bool offsetApplied = false;
                for (int j = 0; j < apparelGraphics.Count; j++)
                {
                    Apparel sourceApparel = apparelGraphics[j].sourceApparel;
                    if (Settings.IsHeadwear(sourceApparel.def.apparel))
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

                        if (!offsetApplied)
                        {
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
                            offsetApplied = true;
                        }
                    }
                }
#if DEBUG && T
                if (isPawn && pawn.Name.ToStringShort.Equals("Happy"))
                {
                    Log.Warning(pawn.Name.ToStringShort + " Hair: " + pawn.story.hairDef.defName + " HideAllHats: " + Settings.HideAllHats + " forceShowHair: " + forceShowHair + " flag: " + flag + " bodyDrawType: " + bodyDrawType + " headStump: " + headStump);
                }
#endif
                if ((!flag && bodyDrawType != RotDrawMode.Dessicated && !flags.FlagSet(PawnRenderFlags.HeadStump)) || 
                    (!hideHats && Settings.HairToHide.TryGetValue(pawn.story.hairDef, out bool v) && v))
                {
                    // Hair was already rendered
                }
                else if ((hideHats || forceShowHair) && hairLoc > 0)
                {
					if (hairLoc > 0.001f)
					{
						loc2.y = hairLoc - 0.001f;

						Material mat = __instance.graphics.HairMatAt(bodyFacing, true);
                        if (getBodySizeScalingMI != null && getModifiedHairMeshSetMI != null)
                        {
                            Vector3 scaledHairLoc = new Vector3(b.x, b.y, b.z);
                            float scale = (float)getBodySizeScalingMI.Invoke(null, new object[] { pawn.ageTracker.CurLifeStage.bodySizeFactor, pawn });
                            scaledHairLoc.x *= scale;
                            scaledHairLoc.z *= scale;
                            scaledHairLoc += rootLoc;
                            scaledHairLoc.y = loc2.y;
                            GraphicMeshSet meshSet = (GraphicMeshSet)getModifiedHairMeshSetMI.Invoke(null, new object[] { scale, pawn });
                            GenDraw.DrawMeshNowOrLater(meshSet.MeshAt(bodyFacing), scaledHairLoc, quad, mat, flags.FlagSet(PawnRenderFlags.DrawNow));
                        }
                        else if (hasAlienRaces && DrawAlienPawn(pawn, bodyFacing, loc2, quad, mat, flags.FlagSet(PawnRenderFlags.Portrait)))
                        {
                            ;
                        }
                        else
                        {
                            GenDraw.DrawMeshNowOrLater(__instance.graphics.HairMeshSet.MeshAt(bodyFacing), loc2, quad, mat, flags.FlagSet(PawnRenderFlags.Portrait));
                        }
                    }
                }
            }* /
        }*/

        private static bool DrawAlienPawn(Pawn pawn, Rot4 headFacing, Vector3 loc2, Quaternion quad, Material mat, bool portrait)
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
        }

        private static FieldInfo alienPortraitHeadGraphicsFI = null;
        private static FieldInfo alienHeadGraphicsFI = null;
        private static FieldInfo hairSetAverageFI = null;

        private static Dictionary<Pawn, bool> previousHatConfig = new Dictionary<Pawn, bool>();

        public static void HideHats(ref bool hideHair, ref bool hideBeard, ref bool hideHat)
        {
            if (Settings.OptionsOpen ||
                flags.FlagSet(PawnRenderFlags.Portrait) && Prefs.HatsOnlyOnMap)
            {
                hideHair = false;
                hideHat = true;
            }

            if (Settings.OnlyApplyToColonists && pawn.Faction != Faction.OfPlayer)
                return;

            if (Settings.HideAllHats)
            {
                hideHair = false;
                hideHat = true;
            }
            if (Settings.ShowHatsOnlyWhenDrafted)
            {
                hideHair = isDrafted;
                hideHat = !isDrafted;
            }

            bool hide;
            Apparel apparel;
            ThingDef def;
            if (Settings.HairToHide.TryGetValue(pawn.story.hairDef, out hide) && hide)
            {
                hideHair = true;
                hideHat = false;
                return;
            }
            for (int j = 0; j < apparelGraphics.Count; j++)
            {
                apparel = apparelGraphics[j].sourceApparel;
                def = apparel.def;
                if (Settings.IsHeadwear(apparel.def.apparel))
                {
                    if (Settings.HatsToHide.TryGetValue(def, out hide) && hide)
                    {
                        hideHair = false;
                        hideHat = true;
                        return;
                    }
                    if (Settings.HatsThatHide.TryGetValue(def, out hide) && hide)
                    {
                        hideHair = true;
                        hideHat = false;
                        return;
                    }
                }
            }

            /*if (Settings.HideHatsIndoors)
            {
                if (pawn.Drafted && Settings.ShowHatsWhenDraftedIndoors)
                {
                   // return false;
                }

                bool hideHat = false;
                RoofDef roofDef = pawn.Map?.roofGrid.RoofAt(pawn.Position);
                if (roofDef != null)
                {
                    if (roofDef.isNatural)
                    {
                        hideHat = Settings.HideHatsNaturalRoof;
                    }
                    else
                    {
                        hideHat = true;
                    }
                }
                if (!isPortrait && Settings.UpdatePortrait && pawn.Faction == Faction.OfPlayer)
                {
                    if (!previousHatConfig.TryGetValue(pawn, out bool wasHidden) || wasHidden != hideHat)
                    {
                        PortraitsCache.SetDirty(pawn);
                        previousHatConfig[pawn] = hideHat;
                    }
                }
#if DEBUG && T
            Log.Warning(
                "Result: " + result +
                "- Settings.HideAllHats: " + Settings.HideAllHats + " Portrait: " + portrait + " Prefs.HatsOnlyOnMap: " + Prefs.HatsOnlyOnMap);
#endif
                //return hideHat;*/        //return isPortrait && Prefs.HatsOnlyOnMap;
        }
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
#if DEBUG && TRANSPILER
#endif
    }
}