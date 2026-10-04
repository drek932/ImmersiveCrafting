using HarmonyLib;
using Il2Cpp;
using Il2CppTLD.Gear;
using ImmersiveShared;

namespace ImmersiveCrafting
{
    /// <summary>
    /// The crafting menu's material checks run inside a Workspace scope for the station the menu is open at, so the
    /// shared counting (MaterialPatches) adds the materials lying there: the recipe check, the "2/3" labels and the
    /// quantity selector.
    /// </summary>
    internal static class CraftingMenuScopes
    {
        private static void Enter() => Workspace.Enter(Workspace.CraftingStation);

        [HarmonyPatch(typeof(BlueprintData), nameof(BlueprintData.HasRequiredMaterials))]
        private static class HasRequiredMaterials
        {
            private static void Prefix() => Enter();
            private static void Postfix() => Workspace.Exit();
        }

        [HarmonyPatch(typeof(CraftingRequirementMaterial), nameof(CraftingRequirementMaterial.Enable))]
        private static class MaterialEnable
        {
            private static void Prefix() => Enter();
            private static void Postfix() => Workspace.Exit();
        }

        /// <summary>Powder (gunpowder) has its own line in the menu: "0.01 / 0.50 kg".</summary>
        [HarmonyPatch(typeof(CraftingRequirementMaterial), nameof(CraftingRequirementMaterial.EnableForPowder))]
        private static class MaterialEnableForPowder
        {
            private static void Prefix() => Enter();
            private static void Postfix() => Workspace.Exit();
        }

        /// <summary>Liquids (lamp oil) too.</summary>
        [HarmonyPatch(typeof(CraftingRequirementMaterial), nameof(CraftingRequirementMaterial.EnableForLiquid))]
        private static class MaterialEnableForLiquid
        {
            private static void Prefix() => Enter();
            private static void Postfix() => Workspace.Exit();
        }

        [HarmonyPatch(typeof(CraftingRequirementMaterial), nameof(CraftingRequirementMaterial.ShowRequiredCountLabel))]
        private static class ShowRequiredCountLabel
        {
            private static void Prefix() => Enter();
            private static void Postfix() => Workspace.Exit();
        }

        [HarmonyPatch(typeof(CraftingRequirementMaterial), nameof(CraftingRequirementMaterial.ShowRequiredAmountLabel))]
        private static class ShowRequiredAmountLabel
        {
            private static void Prefix() => Enter();
            private static void Postfix() => Workspace.Exit();
        }

        [HarmonyPatch(typeof(CraftingRequirementQuantitySelect), nameof(CraftingRequirementQuantitySelect.Enable))]
        private static class QuantitySelectEnable
        {
            private static void Prefix() => Enter();
            private static void Postfix() => Workspace.Exit();
        }
    }
}
