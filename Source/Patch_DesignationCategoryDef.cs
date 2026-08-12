using System.Linq;
using HarmonyLib;
using Verse;

namespace SmartWallLights;

[HarmonyPatch(typeof(DesignationCategoryDef), "ResolveDesignators")]
public static class Patch_DesignationCategoryDef_ResolveDesignators
{
    public static void Postfix(DesignationCategoryDef __instance)
    {
        if (__instance.defName != "Furniture")
        {
            return;
        }

        if (__instance.AllResolvedDesignators.Any(designator => designator is Designator_SmartWallLights))
        {
            return;
        }

        __instance.AllResolvedDesignators.Add(new Designator_SmartWallLights
        {
            isOrder = true
        });
    }
}
