using HarmonyLib;
using Verse;

namespace SmartWallLights;

[StaticConstructorOnStartup]
public static class Bootstrap
{
    static Bootstrap()
    {
        new Harmony("evg.smartwalllights").PatchAll();
        Log.Message("[SmartWallLights] Harmony patches applied.");
    }
}
