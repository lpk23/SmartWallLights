using Verse;

namespace SmartWallLights;

public enum PlacementMode
{
    Efficient,
    FullSymmetry,
    Hybrid
}

public class SmartWallLightsSettings : ModSettings
{
    public PlacementMode placementMode = PlacementMode.Hybrid;
    public int doorAvoidanceDistance = 2;
    public int minLampSpacing = 8;
    public bool debugLogging;

    public override void ExposeData()
    {
        Scribe_Values.Look(ref placementMode, "placementMode", PlacementMode.Hybrid);
        Scribe_Values.Look(ref doorAvoidanceDistance, "doorAvoidanceDistance", 2);
        Scribe_Values.Look(ref minLampSpacing, "minLampSpacing", 8);
        Scribe_Values.Look(ref debugLogging, "debugLogging", defaultValue: false);
        ClampValues();
    }

    public void ClampValues()
    {
        doorAvoidanceDistance = Clamp(doorAvoidanceDistance, 0, 5);
        minLampSpacing = Clamp(minLampSpacing, 1, 20);
    }

    private static int Clamp(int value, int min, int max)
    {
        if (value < min)
        {
            return min;
        }

        if (value > max)
        {
            return max;
        }

        return value;
    }
}
