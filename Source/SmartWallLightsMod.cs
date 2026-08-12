using UnityEngine;
using Verse;

namespace SmartWallLights;

public class SmartWallLightsMod : Mod
{
    private static SmartWallLightsSettings settings;

    public SmartWallLightsMod(ModContentPack content) : base(content)
    {
        settings = GetSettings<SmartWallLightsSettings>();
        settings.ClampValues();
    }

    public static SmartWallLightsSettings Settings
    {
        get
        {
            if (settings == null)
            {
                settings = new SmartWallLightsSettings();
                settings.ClampValues();
            }

            return settings;
        }
    }

    public override string SettingsCategory()
    {
        return "SmartWallLights";
    }

    public override void DoSettingsWindowContents(Rect inRect)
    {
        SmartWallLightsSettings current = Settings;
        Listing_Standard listing = new Listing_Standard();
        listing.Begin(inRect);

        listing.Label("SmartWallLights.Settings.Mode".Translate());
        if (listing.RadioButton("SmartWallLights.Settings.ModeEfficient".Translate(), current.placementMode == PlacementMode.Efficient))
        {
            current.placementMode = PlacementMode.Efficient;
        }

        if (listing.RadioButton("SmartWallLights.Settings.ModeFullSymmetry".Translate(), current.placementMode == PlacementMode.FullSymmetry))
        {
            current.placementMode = PlacementMode.FullSymmetry;
        }

        if (listing.RadioButton("SmartWallLights.Settings.ModeHybrid".Translate(), current.placementMode == PlacementMode.Hybrid))
        {
            current.placementMode = PlacementMode.Hybrid;
        }

        listing.GapLine();
        current.doorAvoidanceDistance = Mathf.RoundToInt(Slider(listing, "SmartWallLights.Settings.DoorAvoidance".Translate(current.doorAvoidanceDistance), current.doorAvoidanceDistance, 0f, 5f));
        current.minLampSpacing = Mathf.RoundToInt(Slider(listing, "SmartWallLights.Settings.MinSpacing".Translate(current.minLampSpacing), current.minLampSpacing, 1f, 20f));
        listing.CheckboxLabeled("SmartWallLights.Settings.DebugLogging".Translate(), ref current.debugLogging);

        listing.Gap();
        if (listing.ButtonText("SmartWallLights.Settings.Reset".Translate()))
        {
            current.placementMode = PlacementMode.Hybrid;
            current.doorAvoidanceDistance = 2;
            current.minLampSpacing = 8;
            current.debugLogging = false;
        }

        listing.End();
        current.ClampValues();
    }

    public override void WriteSettings()
    {
        Settings.ClampValues();
        base.WriteSettings();
    }

    private static float Slider(Listing_Standard listing, string label, float value, float min, float max)
    {
        listing.Label(label);
        Rect rect = listing.GetRect(24f);
        return Widgets.HorizontalSlider(rect, value, min, max, true);
    }
}
