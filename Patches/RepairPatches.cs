using System;
using System.Collections.Generic;
using HarmonyLib;
using Il2Cpp;
using ImmersiveShared;

namespace ImmersiveCrafting
{
    /// <summary>
    /// Repairs at a workbench, ammo workbench, furniture workbench or forge (the repair panel opened from the bench or
    /// from the inventory, the player standing there): tools lying there are offered after the carried ones, and
    /// materials lying there count and are used up after the carried ones, top of a pile first.
    /// The panel lists tools itself (RefreshTools walks the inventory), checks materials with
    /// Inventory.GearInInventory(name, units) and takes them with RemoveGearFromInventory.
    /// </summary>
    internal static class RepairPatches
    {
        private static void Enter() => Workspace.Enter(Workspace.RepairStation);

        [HarmonyPatch(typeof(Panel_Repair), nameof(Panel_Repair.RefreshTools))]
        private static class RefreshTools
        {
            private static void Postfix(Panel_Repair __instance)
            {
                try
                {
                    var place = Workspace.RepairStation();
                    var tools = __instance.m_Tools;
                    if (place == null || tools == null) return;
                    var listed = new HashSet<IntPtr>();
                    for (int i = 0; i < tools.Count; i++) if (tools[i] != null) listed.Add(tools[i].Pointer);
                    int added = 0;
                    foreach (var gi in place.Lying())
                    {
                        if (gi == null || gi.m_ToolsItem == null || gi.IsWornOut()) continue;
                        var go = gi.gameObject;
                        if (listed.Contains(go.Pointer) || !__instance.ToolCanRepairSelectedItem(go)) continue;
                        tools.Add(go);
                        listed.Add(go.Pointer);
                        added++;
                    }
                    // The game's list starts with an empty entry ("no tool"); it stays chosen when nothing was carried.
                    int chosen = __instance.m_SelectedToolIndex;
                    if (added > 0 && (chosen < 0 || chosen >= tools.Count || tools[chosen] == null))
                        for (int i = 0; i < tools.Count; i++)
                            if (tools[i] != null)
                            {
                                __instance.m_SelectedToolIndex = i;
                                break;
                            }
                    if (added > 0) Workspace.MsgOnce("repairTools:" + place.Label + added, $"Repair: {added} tool(s) lying near the {place.Label} offered");
                }
                catch (Exception e)
                {
                    Workspace.LogOnce("RepairTools", "Repair tools: " + e);
                }
            }
        }

        [HarmonyPatch(typeof(Panel_Repair), nameof(Panel_Repair.RefreshMaterialsTable))]
        private static class RefreshMaterialsTable
        {
            private static void Prefix() => Enter();
            private static void Postfix() => Workspace.Exit();
        }

        [HarmonyPatch(typeof(Panel_Repair), nameof(Panel_Repair.OnRepair))]
        private static class OnRepair
        {
            private static void Prefix() => Enter();
            private static void Postfix() => Workspace.Exit();
        }

        /// <summary>The game uses up the materials (and wears the tool) inline in RepairFinished, not through ConsumeMaterialsUsedForRepair.</summary>
        [HarmonyPatch(typeof(Panel_Repair), nameof(Panel_Repair.RepairFinished))]
        private static class RepairFinished
        {
            private static void Prefix()
            {
                try
                {
                    MaterialPatches.BeginUsingUp(Workspace.RepairStation());
                }
                catch (Exception e)
                {
                    Workspace.LogOnce("RepairConsume", "Repair materials: " + e);
                }
            }

            private static void Postfix() => MaterialPatches.EndUsingUp();
        }

        /// <summary>The repair panel's "is there enough of it" (by name): lying materials count after the carried ones.</summary>
        [HarmonyPatch(typeof(Inventory), nameof(Inventory.GearInInventory), typeof(string), typeof(int))]
        private static class GearInInventoryByName
        {
            private static void Postfix(Inventory __instance, string gearName, int numUnits, ref GearItem __result)
            {
                if (__result != null || string.IsNullOrEmpty(gearName) || !Host.CountsMaterials) return;
                var place = Workspace.Current;
                var player = GameManager.GetInventoryComponent();
                if (place == null || player == null || __instance.Pointer != player.Pointer) return;
                try
                {
                    int total = MaterialPatches.CarriedUnits(__instance, gearName, false) + Materials.CountUnits(place, gearName, false);
                    if (total >= numUnits) __result = Materials.AnyLying(place, gearName, false);
                }
                catch (Exception e)
                {
                    Workspace.LogOnce("RepairGearInInventory", "Repair materials check: " + e);
                }
            }
        }
    }
}
