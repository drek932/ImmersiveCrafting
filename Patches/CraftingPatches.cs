using System;
using HarmonyLib;
using Il2Cpp;
using Il2CppInterop.Runtime;
using Il2CppTLD.Gear;
using ImmersiveShared;

namespace ImmersiveCrafting
{
    /// <summary>
    /// Crafting at a workbench, ammo workbench, furniture workbench or forge: tools lying next to it count as carried.
    ///
    /// The game checks and picks tools by name through the inventory (HasNonRuinedItem / GetNonRuinedItem), so those
    /// calls are wrapped in a Workspace scope. The "choose one of several tools" row collects its options by walking
    /// the inventory itself; tools lying around are added to that list in AddToolOptions.
    /// The chosen tool is handed to the crafting as the item itself and wears where it lies.
    /// </summary>
    internal static class CraftingPatches
    {
        private static void Enter() => Workspace.Enter(Workspace.CraftingStation);

        [HarmonyPatch(typeof(BlueprintData), nameof(BlueprintData.HasRequiredTool))]
        private static class HasRequiredTool
        {
            private static void Prefix() => Enter();
            private static void Postfix() => Workspace.Exit();
        }

        [HarmonyPatch(typeof(BlueprintData), nameof(BlueprintData.GetToolsAvailableToCraft))]
        private static class GetToolsAvailableToCraft
        {
            private static void Prefix() => Enter();
            private static void Postfix() => Workspace.Exit();
        }

        [HarmonyPatch(typeof(CraftingRequirementTool), nameof(CraftingRequirementTool.Enable))]
        private static class SingleToolEnable
        {
            private static void Prefix() => Enter();
            private static void Postfix() => Workspace.Exit();
        }

        [HarmonyPatch(typeof(CraftingRequirementMultiTool), nameof(CraftingRequirementMultiTool.Enable))]
        private static class MultiToolEnable
        {
            private static void Prefix() => Enter();
            private static void Postfix() => Workspace.Exit();
        }

        [HarmonyPatch(typeof(CraftingRequirementMultiTool), nameof(CraftingRequirementMultiTool.RefreshDisplayed))]
        private static class MultiToolRefresh
        {
            private static void Prefix() => Enter();
            private static void Postfix() => Workspace.Exit();
        }

        [HarmonyPatch(typeof(Panel_Crafting), nameof(Panel_Crafting.Enable), new[] { typeof(bool) })]
        private static class PanelEnable
        {
            private static void Postfix(bool enable)
            {
                if (!enable) Workspace.CraftingClosed();
            }
        }

        [HarmonyPatch(typeof(CraftingRequirementMultiTool), nameof(CraftingRequirementMultiTool.AddToolOptions))]
        private static class AddToolOptions
        {
            private static void Postfix(CraftingRequirementMultiTool __instance, GearItem gi)
            {
                try
                {
                    var place = Workspace.Current;
                    if (place == null || gi == null) return;
                    var name = Nearby.GearName(gi);
                    var lying = place.FindTool(name);
                    if (lying == null) return;
                    if (ToolOptions.Offer(__instance.m_ToolOptions, name, lying))
                        Workspace.NoteSupplied(place, lying, "offered among the tools");
                }
                catch (Exception e)
                {
                    Workspace.LogOnce("AddToolOptions", "AddToolOptions: " + e);
                }
            }
        }
    }

    /// <summary>
    /// CraftingRequirementMultiTool.ToolOption is a struct (GearItem m_GearItem, bool m_InInventory) that
    /// Il2CppInterop doesn't marshal reliably inside a list, so list entries are read and written in memory.
    /// The layout is taken from the game's metadata at runtime.
    /// </summary>
    internal static unsafe class ToolOptions
    {
        private const int ListItemsOffset = 16, ListSizeOffset = 24, ArrayDataOffset = 32, ObjectHeader = 16;

        private static bool layoutReady;
        private static int entrySize, gearOffset, inInventoryOffset;

        private static bool Layout()
        {
            if (layoutReady) return entrySize > 0;
            layoutReady = true;

            var cls = Il2CppClassPointerStore<CraftingRequirementMultiTool.ToolOption>.NativeClassPtr;
            uint align = 0;
            entrySize = IL2CPP.il2cpp_class_value_size(cls, ref align);
            var gearField = IL2CPP.GetIl2CppField(cls, "m_GearItem");
            var invField = IL2CPP.GetIl2CppField(cls, "m_InInventory");
            if (entrySize <= 0 || gearField == IntPtr.Zero || invField == IntPtr.Zero)
            {
                Main.Log.Warning("ToolOption layout not found; tools lying around are not offered in multi-tool recipes");
                entrySize = 0;
                return false;
            }
            // Field offsets of a value type include the object header; list entries are stored without it.
            gearOffset = (int)IL2CPP.il2cpp_field_get_offset(gearField) - ObjectHeader;
            inInventoryOffset = (int)IL2CPP.il2cpp_field_get_offset(invField) - ObjectHeader;
            Verbose.Log($"ToolOption layout: size {entrySize}, gear +{gearOffset}, in inventory +{inInventoryOffset}");
            return true;
        }

        private static int Count(IntPtr list) => *(int*)((byte*)list + ListSizeOffset);

        private static byte* Entry(IntPtr list, int index)
        {
            var items = *(IntPtr*)((byte*)list + ListItemsOffset);
            return (byte*)items + ArrayDataOffset + index * entrySize;
        }

        /// <summary>
        /// Puts the tool lying around into the options for this tool name, unless the player carries one:
        /// replaces the "not carried" placeholder, or adds an entry. True if the list changed.
        /// </summary>
        public static bool Offer(Il2CppSystem.Collections.Generic.List<CraftingRequirementMultiTool.ToolOption> options,
            string name, GearItem lying)
        {
            if (options == null || !Layout()) return false;
            var list = options.Pointer;

            int placeholder = -1;
            for (int i = 0; i < Count(list); i++)
            {
                byte* e = Entry(list, i);
                var ptr = *(IntPtr*)(e + gearOffset);
                if (ptr == IntPtr.Zero) continue;
                if (!string.Equals(Nearby.GearName(new GearItem(ptr)), name, StringComparison.OrdinalIgnoreCase)) continue;
                if (e[inInventoryOffset] != 0) return false; // carried: the game uses that one
                placeholder = i;
            }

            if (placeholder < 0)
            {
                options.Add(new CraftingRequirementMultiTool.ToolOption()); // grows the list, overwritten below
                placeholder = Count(list) - 1;
            }

            byte* entry = Entry(list, placeholder);
            var items = *(IntPtr*)((byte*)list + ListItemsOffset);
            IL2CPP.il2cpp_gc_wbarrier_set_field(items, (IntPtr)(entry + gearOffset), lying.Pointer);
            entry[inInventoryOffset] = 1;
            return true;
        }
    }
}
