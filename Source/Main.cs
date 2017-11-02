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
    class Main
    {
        static Main()
        {
            var harmony = HarmonyInstance.Create("com.showhair.rimworld.mod");
            harmony.PatchAll(Assembly.GetExecutingAssembly());

            Log.Message("ShowHair: Adding Harmony Prefix to PawnRenderer.RenderPawnInternal.");
        }
    }

    [HarmonyPatch(typeof(PawnRenderer), "RenderPawnInternal", new Type[] { typeof(Vector3), typeof(Quaternion), typeof(bool), typeof(Rot4), typeof(Rot4), typeof(RotDrawMode), typeof(bool), typeof(bool) })]
    public static class Patch_PawnRenderer_RenderPawnInternal
    {
        /*private static FieldInfo PawnFieldInfo = null;
        private static FieldInfo EquipmentFieldInfo = null;

        public static void Prefix(PawnRenderer __instance, ref ThingWithComps __state)
        {
        if (SettingsController.HideAllHats)
        {
        __state = null;
        if (PawnFieldInfo == null)
        {
            PawnFieldInfo = typeof(PawnRenderer).GetField("pawn", BindingFlags.NonPublic | BindingFlags.Instance);
            EquipmentFieldInfo = typeof(Pawn_EquipmentTracker).GetField("equipment", BindingFlags.NonPublic | BindingFlags.Instance);
        }

        Pawn pawn = PawnFieldInfo.GetValue(__instance) as Pawn;
        if (pawn != null)
        {
            Log.Warning("pawn not null");
            ThingOwner<ThingWithComps> apparel = EquipmentFieldInfo.GetValue(pawn.equipment) as ThingOwner<ThingWithComps>;
            if (apparel != null)
            {
                Log.Warning("apparel not null. Count: " + apparel.Count);
                for (int i = 0; i < apparel.Count; ++i)
                {
                    if (apparel[i].def.apparel.LastLayer == ApparelLayer.Overhead)
                    {
                        __state = apparel[i];
                        pawn.equipment.Remove(apparel[i]);
                        break;
                    }
                }
            }
        }
        }
        }*/
        public static void Postfix(PawnRenderer __instance, Vector3 rootLoc, Quaternion quat, bool renderBody, Rot4 bodyFacing, Rot4 headFacing, RotDrawMode bodyDrawType, bool portrait, bool headStump)
        {
            /*if (SettingsController.HideAllHats && __state != null)
            {
                Pawn pawn = PawnFieldInfo.GetValue(__instance) as Pawn;
                pawn.equipment.AddEquipment(__state);
            }*/

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
                    s(instruction, "Pre");
#endif
                    instruction.operand = typeof(Patch_PawnRenderer_RenderPawnInternal).GetMethod(
                        nameof(Patch_PawnRenderer_RenderPawnInternal.HideHats), BindingFlags.Static | BindingFlags.NonPublic);
#if DEBUG
                    s(instruction);
#endif
                    yield return instruction;
                    i += 2;
                }
                else
                {
#if DEBUG
                    if (found && first)
                    {
                        s(instruction);
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

        static void s(CodeInstruction i, string pre = "")
        {
            Log.Warning("CodeInstruction: " + pre + " opCode: " + i.opcode + " operand: " + i.operand + " labels: " + s(i.labels));
        }

        static string s(IEnumerable<Label> labels)
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

        /*
        private static Type PawnRendererType = null;
        private static FieldInfo PawnFieldInfo;
       private static FieldInfo WoundOverlayFieldInfo;
        private static MethodInfo DrawEquipm entMethodInfo;
        private static FieldInfo PawnHeadOverlaysFieldInfo;

        private static void GetReflections()
        {
            if (PawnRendererType == null)
            {
                PawnRendererType = typeof(PawnRenderer);
                PawnFieldInfo = PawnRendererType.GetField("pawn", BindingFlags.NonPublic | BindingFlags.Instance);
                WoundOverlayFieldInfo = PawnRendererType.GetField("woundOverlays", BindingFlags.NonPublic | BindingFlags.Instance);
                DrawEquipmentMethodInfo = PawnRendererType.GetMethod("DrawEquipment", BindingFlags.NonPublic | BindingFlags.Instance);
                PawnHeadOverlaysFieldInfo = PawnRendererType.GetField("statusOverlays", BindingFlags.NonPublic | BindingFlags.Instance);
            }
        }

#if DEBUG
        static int count = 0;
        static bool first = true;
        const int COUNT_FOR_LOG = 120;
#endif
        public static bool Prefix(PawnRenderer __instance, Vector3 rootLoc, Quaternion quat, bool renderBody, Rot4 bodyFacing, Rot4 headFacing, RotDrawMode bodyDrawType, bool portrait, bool headStump)
        {
            GetReflections();
            
            if (!__instance.graphics.AllResolved)
            {
                __instance.graphics.ResolveAllGraphics();
            }

            Pawn pawn = PawnFieldInfo.GetValue(__instance) as Pawn;
#if DEBUG
            bool isPawn = pawn.NameStringShort.EqualsIgnoreCase("takuma");
            if (isPawn)
            {
                ++count;
                if (first)
                {
                    first = false;
                    Log.Warning("Takuma found");
                }
            }
#endif
            Mesh mesh = null;
            if (pawn != null && renderBody)
            {
                Vector3 loc = rootLoc;
                loc.y += 0.0078125f;
                if (bodyDrawType == RotDrawMode.Dessicated && !pawn.RaceProps.Humanlike && __instance.graphics.dessicatedGraphic != null && !portrait)
                {
                    __instance.graphics.dessicatedGraphic.Draw(loc, bodyFacing, pawn, 0f);
                }
                else
                {
                    if (pawn.RaceProps.Humanlike)
                    {
                        mesh = MeshPool.humanlikeBodySet.MeshAt(bodyFacing);
                    }
                    else
                    {
                        mesh = __instance.graphics.nakedGraphic.MeshAt(bodyFacing);
                    }
                    List<Material> list = __instance.graphics.MatsBodyBaseAt(bodyFacing, bodyDrawType);
                    for (int i = 0; i < list.Count; i++)
                    {
                        Material damagedMat = __instance.graphics.flasher.GetDamagedMat(list[i]);
                        GenDraw.DrawMeshNowOrLater(mesh, loc, quat, damagedMat, portrait);
                        loc.y += 0.00390625f;
                    }
                    if (bodyDrawType == RotDrawMode.Fresh)
                    {
                        Vector3 drawLoc = rootLoc;
                        drawLoc.y += 0.01953125f;
                        PawnWoundDrawer wound = WoundOverlayFieldInfo.GetValue(__instance) as PawnWoundDrawer;
                        wound?.RenderOverBody(drawLoc, mesh, quat, portrait);
                    }
                }
            }
            Vector3 vector = rootLoc;
            Vector3 a = rootLoc;
            if (bodyFacing != Rot4.North)
            {
                a.y += 0.02734375f;
                vector.y += 0.0234375f;
            }
            else
            {
                a.y += 0.0234375f;
                vector.y += 0.02734375f;
            }
            if (__instance.graphics.headGraphic != null)
            {
                Vector3 b = quat * __instance.BaseHeadOffsetAt(headFacing);
                Material material = __instance.graphics.HeadMatAt(headFacing, bodyDrawType, headStump);
                if (material != null)
                {
                    Mesh mesh2 = MeshPool.humanlikeHeadSet.MeshAt(headFacing);
                    GenDraw.DrawMeshNowOrLater(mesh2, a + b, quat, material, portrait);
                }
                Vector3 loc2 = rootLoc + b;
                loc2.y += 0.03125f;
                bool flag = false;
                bool forceShowHair = false;
                float hairLoc = 0;
                if (!SettingsController.HideAllHats && (!portrait || !Prefs.HatsOnlyOnMap))
                {
                    Mesh mesh3 = __instance.graphics.HairMeshSet.MeshAt(headFacing);
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
                            if (!forceShowHair)
                            {
                                forceShowHair = !SettingsController.HatsThatHideHair.Contains(sourceApparel.def);
                            }

                            if (!sourceApparel.def.apparel.hatRenderedFrontOfFace)
                            {
                                hairLoc = loc2.y;
                                flag = true;
                                Material material2 = apparelGraphics[j].graphic.MatAt(bodyFacing, null);
                                material2 = __instance.graphics.flasher.GetDamagedMat(material2);
                                GenDraw.DrawMeshNowOrLater(mesh3, loc2, quat, material2, portrait);
                            }
                            else
                            {
                                Material material3 = apparelGraphics[j].graphic.MatAt(bodyFacing, null);
                                material3 = __instance.graphics.flasher.GetDamagedMat(material3);
                                Vector3 loc3 = rootLoc + b;
                                loc3.y += ((!(bodyFacing == Rot4.North)) ? 0.03515625f : 0.00390625f);
                                hairLoc = loc3.y;
                                GenDraw.DrawMeshNowOrLater(mesh3, loc3, quat, material3, portrait);
                            }
                        }
                    }
                }
#if DEBUG
                if (isPawn && count > COUNT_FOR_LOG)
                {
                    Log.Warning("HideAllHats: " + SettingsController.HideAllHats + " forceShowHair: " + forceShowHair + " flag: " + flag + " bodyDrawType: " + bodyDrawType + " headStump: " + headStump);
                }
#endif

                if (hairLoc > 0)
                {
                    loc2.y = hairLoc - 0.01f;
                }
                
                if (SettingsController.HideAllHats || forceShowHair || (!flag && bodyDrawType != RotDrawMode.Dessicated && !headStump))
                {
                    Mesh mesh4 = __instance.graphics.HairMeshSet.MeshAt(headFacing);
                    Material mat = __instance.graphics.HairMatAt(headFacing);
                    GenDraw.DrawMeshNowOrLater(mesh4, loc2, quat, mat, portrait);
                }
            }
            if (renderBody)
            {
                for (int k = 0; k < __instance.graphics.apparelGraphics.Count; k++)
                {
                    ApparelGraphicRecord apparelGraphicRecord = __instance.graphics.apparelGraphics[k];
                    if (apparelGraphicRecord.sourceApparel.def.apparel.LastLayer == ApparelLayer.Shell)
                    {
                        Material material4 = apparelGraphicRecord.graphic.MatAt(bodyFacing, null);
                        material4 = __instance.graphics.flasher.GetDamagedMat(material4);
                        GenDraw.DrawMeshNowOrLater(mesh, vector, quat, material4, portrait);
                    }
                }
            }
            if (!portrait && pawn.RaceProps.Animal && pawn.inventory != null && pawn.inventory.innerContainer.Count > 0 && __instance.graphics.packGraphic != null)
            {
                Graphics.DrawMesh(mesh, vector, quat, __instance.graphics.packGraphic.MatAt(bodyFacing, null), 0);
            }
            if (!portrait)
            {
                DrawEquipmentMethodInfo?.Invoke(__instance, new object[] { rootLoc });
                if (pawn.apparel != null)
                {
                    List<Apparel> wornApparel = pawn.apparel.WornApparel;
                    for (int l = 0; l < wornApparel.Count; l++)
                    {
                        wornApparel[l].DrawWornExtras();
                    }
                }
                Vector3 bodyLoc = rootLoc;
                bodyLoc.y += 0.04296875f;

                PawnHeadOverlays headOverlay = PawnHeadOverlaysFieldInfo.GetValue(__instance) as PawnHeadOverlays;
                headOverlay?.RenderStatusOverlays(bodyLoc, quat, MeshPool.humanlikeHeadSet.MeshAt(headFacing));
            }

#if DEBUG
            if (isPawn && count > COUNT_FOR_LOG)
            {
                count = 0;
            }
#endif


            /*SettingsController.InitializeAllHats();

            if (!__instance.graphics.AllResolved)
            {
                __instance.graphics.ResolveAllGraphics();
            }

            Pawn pawn = (Pawn)PawnFieldInfo?.GetValue(__instance);
            Mesh mesh = null;
            float maxY = rootLoc.y;
            if (pawn != null && renderBody)
            {
                Vector3 loc = rootLoc;
                loc.y += 0.0046875f;
                if (bodyDrawType == RotDrawMode.Dessicated && !pawn.RaceProps.Humanlike && __instance.graphics.dessicatedGraphic != null && !portrait)
                {
                    __instance.graphics.dessicatedGraphic.Draw(loc, bodyFacing, pawn);
                }
                else
                {
                    if (pawn.RaceProps.Humanlike)
                    {
                        mesh = MeshPool.humanlikeBodySet.MeshAt(bodyFacing);
                    }
                    else
                    {
                        mesh = __instance.graphics.nakedGraphic.MeshAt(bodyFacing);
                    }
                    List<Material> list = __instance.graphics.MatsBodyBaseAt(bodyFacing, bodyDrawType);
                    for (int i = 0; i < list.Count; i++)
                    {
                        Material damagedMat = __instance.graphics.flasher.GetDamagedMat(list[i]);
                        GenDraw.DrawMeshNowOrLater(mesh, loc, quat, damagedMat, portrait);
                        loc.y += 0.0046875f;
                    }
                    if (bodyDrawType == RotDrawMode.Fresh)
                    {
                        Vector3 drawLoc = rootLoc;
                        drawLoc.y += 0.01875f;

                        PawnWoundDrawer woundDrawer = (PawnWoundDrawer)WoundOverlayFieldInfo?.GetValue(__instance);
                        woundDrawer?.RenderOverBody(drawLoc, mesh, quat, portrait);
                    }
                }
            }
            Vector3 vector = rootLoc;
            Vector3 a = rootLoc;
            if (bodyFacing != Rot4.North)
            {
                a.y += 0.0281250011f;
                vector.y += 0.0234375f;
            }
            else
            {
                a.y += 0.0234375f;
                vector.y += 0.0281250011f;
            }
            if (__instance.graphics.headGraphic != null)
            {
                Vector3 b = quat * __instance.BaseHeadOffsetAt(headFacing);
                Material material = __instance.graphics.HeadMatAt(headFacing, bodyDrawType, headStump);
                if (material != null)
                {
                    Mesh mesh2 = MeshPool.humanlikeHeadSet.MeshAt(headFacing);
                    GenDraw.DrawMeshNowOrLater(mesh2, a + b, quat, material, portrait);
                }
                Vector3 hairLoc = rootLoc + b;
                hairLoc.y += 0.0328125022f;
                if (bodyDrawType != RotDrawMode.Dessicated && !headStump)
                {
                    bool drawHair = true;
                    if (!SettingsController.HideAllHats)
                    {
                        foreach (Apparel ap in pawn.apparel.WornApparel)
                        {
                            ApparelProperties p = ap.def.apparel;
                            if (p.LastLayer == ApparelLayer.Overhead && 
                                !String.IsNullOrEmpty(p.wornGraphicPath))
                            {
                                drawHair = !SettingsController.HatsThatHideHair.Contains(ap.def);
                            }
                        }
                    }
                    if (drawHair || (portrait && Prefs.HatsOnlyOnMap))
                    {
                        Mesh mesh4 = __instance.graphics.HairMeshSet.MeshAt(headFacing);
                        Material mat = __instance.graphics.HairMatAt(headFacing);
                        GenDraw.DrawMeshNowOrLater(mesh4, hairLoc, quat, mat, portrait);
                    }
                }
                if (!SettingsController.HideAllHats && (!portrait || !Prefs.HatsOnlyOnMap))
                {
                    Mesh mesh3 = __instance.graphics.HairMeshSet.MeshAt(headFacing);
                    List<ApparelGraphicRecord> apparelGraphics = __instance.graphics.apparelGraphics;
                    for (int j = 0; j < apparelGraphics.Count; j++)
                    {
                        if (apparelGraphics[j].sourceApparel.def.apparel.LastLayer == ApparelLayer.Overhead)
                        {
                            Material material2 = apparelGraphics[j].graphic.MatAt(bodyFacing, null);
                            material2 = __instance.graphics.flasher.GetDamagedMat(material2);
                            Vector3 hatLoc = hairLoc;
                            hatLoc.y += 0.001f * (j + 1);
                            GenDraw.DrawMeshNowOrLater(mesh3, hatLoc, quat, material2, portrait);
                        }
                    }
                }
            }
            if (renderBody)
            {
                for (int k = 0; k < __instance.graphics.apparelGraphics.Count; k++)
                {
                    ApparelGraphicRecord apparelGraphicRecord = __instance.graphics.apparelGraphics[k];
                    if (apparelGraphicRecord.sourceApparel.def.apparel.LastLayer == ApparelLayer.Shell)
                    {
                        Material material3 = apparelGraphicRecord.graphic.MatAt(bodyFacing, null);
                        material3 = __instance.graphics.flasher.GetDamagedMat(material3);
                        GenDraw.DrawMeshNowOrLater(mesh, vector, quat, material3, portrait);
                    }
                }
            }
            if (!portrait && pawn.RaceProps.Animal && pawn.inventory != null && pawn.inventory.innerContainer.Count > 0)
            {
                Graphics.DrawMesh(mesh, vector, quat, __instance.graphics.packGraphic.MatAt(pawn.Rotation, null), 0);
            }
            if (!portrait)
            {
                DrawEquipmentMethodInfo?.Invoke(__instance, new object[] { rootLoc });

                if (pawn.apparel != null)
                {
                    List<Apparel> wornApparel = pawn.apparel.WornApparel;
                    for (int l = 0; l < wornApparel.Count; l++)
                    {
                        wornApparel[l].DrawWornExtras();
                    }
                }
                Vector3 bodyLoc = rootLoc;
                bodyLoc.y += 0.0421875f;

                ((PawnHeadOverlays)PawnHeadOverlaysFieldInfo?.GetValue(__instance))?.
                    RenderStatusOverlays(bodyLoc, quat, MeshPool.humanlikeHeadSet.MeshAt(headFacing));
            }* /
            return false;
        }
    */
    }
}