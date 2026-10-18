using System.Globalization;
using ModSettings;

namespace ImmersiveCrafting
{
    /// <summary>Settings: Mods/ImmersiveCrafting.json, editable in the ModSettings menu (texts in Texts.cs).</summary>
    internal sealed class Settings : JsonModSettings
    {
        [Section("IMCR_SECTION_STATIONS", Localize = true)]
        [Name("IMCR_STATIONS", Localize = true)]
        [Description("IMCR_STATIONS_DESC")]
        public bool stations = true;

        [Name("IMCR_MATERIALS", Localize = true)]
        [Description("IMCR_MATERIALS_DESC")]
        public bool materials = true;

        [Name("IMCR_REPAIR", Localize = true)]
        [Description("IMCR_REPAIR_DESC")]
        public bool repair = true;

        [Name("IMCR_SMALL_CONTAINERS", Localize = true)]
        [Description("IMCR_SMALL_CONTAINERS_DESC")]
        public bool smallContainers = true;

        [Name("IMCR_STATION_RADIUS", Localize = true)]
        [Description("IMCR_STATION_RADIUS_DESC")]
        [Slider(0.5f, 3f, 26, NumberFormat = "{0:0.0}")]
        public float stationRadius = 1f;

        [Name("IMCR_FORGE_RADIUS", Localize = true)]
        [Description("IMCR_FORGE_RADIUS_DESC")]
        [Slider(0.5f, 3f, 26, NumberFormat = "{0:0.0}")]
        public float forgeRadius = 2.5f;

        [Name("IMCR_FIRE_RADIUS", Localize = true)]
        [Description("IMCR_FIRE_RADIUS_DESC")]
        [Slider(0.5f, 3f, 26, NumberFormat = "{0:0.0}")]
        public float fireRadius = 1f;

        [Section("IMCR_SECTION_ICE", Localize = true)]
        [Name("IMCR_ICE", Localize = true)]
        [Description("IMCR_ICE_DESC")]
        public bool iceHoles = true;

        [Name("IMCR_ICE_RADIUS", Localize = true)]
        [Description("IMCR_ICE_RADIUS_DESC")]
        [Slider(0.5f, 3f, 26, NumberFormat = "{0:0.0}")]
        public float iceHoleRadius = 1f;

        public string Describe() => string.Format(CultureInfo.InvariantCulture,
            "workbenches: tools {0}, materials {1}, repairs {8}, small containers {2}, reach {3:0.0} m, at a forge {7:0.0} m, at a fire {4:0.0} m; ice holes {5} ({6:0.0} m)",
            OnOff(stations), OnOff(materials), OnOff(smallContainers), stationRadius, fireRadius, OnOff(iceHoles), iceHoleRadius, forgeRadius, OnOff(repair));

        private static string OnOff(bool on) => on ? "on" : "off";

        protected override void OnConfirm()
        {
            base.OnConfirm();
            Main.Log.Msg("Settings: " + Describe());
        }
    }
}
