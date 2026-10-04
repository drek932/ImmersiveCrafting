using HarmonyLib;
using Il2Cpp;
using ImmersiveShared;

namespace ImmersiveCrafting
{
    /// <summary>
    /// The game asks the inventory for a tool by name. Inside a Workspace scope (crafting at a station, clearing
    /// an ice hole), a tool the player doesn't carry is taken from the ones lying around the place instead.
    /// Tools in the inventory always come first.
    /// </summary>
    internal static class InventoryPatches
    {
        private static bool IsPlayerInventory(Inventory inventory)
        {
            var player = GameManager.GetInventoryComponent();
            return player != null && inventory != null && inventory.Pointer == player.Pointer;
        }

        private static GearItem FromNearby(Inventory inventory, string gearName, string how)
        {
            var place = Workspace.Current;
            if (place == null || !IsPlayerInventory(inventory)) return null;
            var gi = place.FindTool(gearName);
            if (gi != null) Workspace.NoteSupplied(place, gi, how);
            return gi;
        }

        [HarmonyPatch(typeof(Inventory), nameof(Inventory.HasNonRuinedItem))]
        private static class HasNonRuinedItem
        {
            private static void Postfix(Inventory __instance, string gearName, ref bool __result)
            {
                if (__result) return;
                try
                {
                    if (FromNearby(__instance, gearName, "counts as carried") != null) __result = true;
                }
                catch (System.Exception e)
                {
                    Workspace.LogOnce("HasNonRuinedItem", "HasNonRuinedItem: " + e);
                }
            }
        }

        [HarmonyPatch(typeof(Inventory), nameof(Inventory.GetNonRuinedItem))]
        private static class GetNonRuinedItem
        {
            private static void Postfix(Inventory __instance, string gearName, ref GearItem __result)
            {
                if (__result != null) return;
                try
                {
                    __result = FromNearby(__instance, gearName, "selected as the tool");
                }
                catch (System.Exception e)
                {
                    Workspace.LogOnce("GetNonRuinedItem", "GetNonRuinedItem: " + e);
                }
            }
        }

        [HarmonyPatch(typeof(Inventory), nameof(Inventory.GetHighestConditionGearThatMatchesName))]
        private static class GetHighestConditionGearThatMatchesName
        {
            private static void Postfix(Inventory __instance, string name, ref GearItem __result)
            {
                if (__result != null) return;
                try
                {
                    __result = FromNearby(__instance, name, "offered as a tool");
                }
                catch (System.Exception e)
                {
                    Workspace.LogOnce("GetHighestCondition", "GetHighestConditionGearThatMatchesName: " + e);
                }
            }
        }
    }
}
