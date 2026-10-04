using System.Collections.Generic;
using MelonLoader;

namespace ImmersiveShared
{
    /// <summary>What the shared code (Shared/, the same files in ImmersiveCooking and ImmersiveCrafting) needs to know about this mod.</summary>
    internal static class Host
    {
        public static MelonLogger.Instance Log => ImmersiveCrafting.Main.Log;

        /// <summary>Prefix of this mod's text keys (Texts.cs), served through the game's localization.</summary>
        public const string KeyPrefix = "IMCR_";

        public static Dictionary<string, (string ru, string en)> Texts => ImmersiveCrafting.Texts.All;

        /// <summary>Materials lying around count and are used (the "materials" setting).</summary>
        public static bool CountsMaterials => ImmersiveCrafting.Main.Settings.materials;

        /// <summary>Drinking water lying around is not used for crafting: recipes take it from the inventory.</summary>
        public const bool TakesWater = false;

        /// <summary>
        /// Where materials for this crafting operation come from: the station it runs at. Recipes of the cooking menu
        /// are left to ImmersiveCooking.
        /// </summary>
        public static Nearby ConsumingPlace(Il2CppTLD.Gear.CraftingOperation operation) =>
            Workspace.IsCookingRecipe(operation) ? null : Workspace.Station(operation.m_Location);
    }
}
