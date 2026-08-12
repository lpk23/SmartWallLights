using System.Collections.Generic;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace SmartWallLights;

[HarmonyPatch(typeof(Designator_Build), nameof(Designator_Build.DesignateSingleCell))]
public static class Patch_DesignatorBuild_DesignateSingleCell
{
    private static readonly AccessTools.FieldRef<Designator_Place, Rot4> PlacingRot =
        AccessTools.FieldRefAccess<Designator_Place, Rot4>("placingRot");

    public static bool Prefix(Designator_Build __instance, IntVec3 c)
    {
        ThingDef lightDef = __instance.PlacingDef as ThingDef;
        if (!Designator_SmartWallLights.IsSupportedWallLight(lightDef) || !KeyBindingDefOf.QueueOrder.IsDown)
        {
            return true;
        }

        Rot4 rotation = PlacingRot(__instance);
        if (SmartWallLightsMod.Settings.debugLogging)
        {
            Log.Message($"[SmartWallLights] Harmony Prefix hit: wall light Shift placement at {c}, rotation={rotation}({rotation.AsInt}), placingDef={lightDef?.defName ?? "null"}.");
        }

        bool success = new Designator_SmartWallLights(lightDef, __instance.StuffDef).DesignateFromWallLampPlacement(c, rotation);
        if (SmartWallLightsMod.Settings.debugLogging)
        {
            Log.Message($"[SmartWallLights] Harmony Prefix finished: success={success}.");
        }

        return false;
    }
}

[HarmonyPatch(typeof(Designator_Build), nameof(Designator_Build.SelectedUpdate))]
public static class Patch_DesignatorBuild_SelectedUpdate
{
    private static readonly AccessTools.FieldRef<Designator_Place, Rot4> PlacingRot =
        AccessTools.FieldRefAccess<Designator_Place, Rot4>("placingRot");

    private static readonly Color PreviewColor = new Color(0.35f, 0.95f, 1f, 0.55f);
    private static readonly Color PreviewCellColor = new Color(0.35f, 0.95f, 1f, 0.85f);

    public static void Postfix(Designator_Build __instance)
    {
        ThingDef lightDef = __instance.PlacingDef as ThingDef;
        if (!KeyBindingDefOf.QueueOrder.IsDown || !Designator_SmartWallLights.IsSupportedWallLight(lightDef))
        {
            return;
        }

        Map map = Find.CurrentMap;
        IntVec3 mouseCell = UI.MouseCell();
        if (map == null || !mouseCell.InBounds(map))
        {
            return;
        }

        Rot4 rotation = PlacingRot(__instance);
        Designator_SmartWallLights smartDesignator = new Designator_SmartWallLights(lightDef, __instance.StuffDef);
        List<Designator_SmartWallLights.LightPlacementPreview> previews = smartDesignator.PreviewFromWallLightPlacement(mouseCell, rotation);
        if (previews.Count == 0)
        {
            return;
        }

        List<IntVec3> previewCells = new List<IntVec3>();
        foreach (Designator_SmartWallLights.LightPlacementPreview preview in previews)
        {
            previewCells.Add(preview.Position);
            GhostDrawer.DrawGhostThing(preview.Position, preview.Rotation, lightDef, null, PreviewColor, AltitudeLayer.Blueprint, null, drawPlaceWorkers: true, __instance.StuffDef);
        }

        GenDraw.DrawFieldEdges(previewCells, PreviewCellColor);
    }
}

[HarmonyPatch(typeof(Designator_Place), "DrawGhost")]
public static class Patch_DesignatorPlace_DrawGhost
{
    public static bool Prefix(Designator_Place __instance)
    {
        if (__instance is not Designator_Build buildDesignator || !KeyBindingDefOf.QueueOrder.IsDown)
        {
            return true;
        }

        ThingDef lightDef = buildDesignator.PlacingDef as ThingDef;
        return !Designator_SmartWallLights.IsSupportedWallLight(lightDef);
    }
}
