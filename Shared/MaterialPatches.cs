using System;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using Il2Cpp;
using Il2CppTLD.Gear;
using Il2CppTLD.IntBackedUnit;

namespace ImmersiveShared
{
    /// <summary>
    /// Materials and ingredients lying around where the player works (shared by ImmersiveCrafting and ImmersiveCooking).
    ///
    /// Counting: recipe checks, "2/3" labels and ingredient lines count through the inventory; inside this mod's
    /// Workspace scope those counts get the items lying around added (if Host.CountsMaterials).
    /// Using up: the game uses materials step by step in CraftingOperation.ConsumeMaterialsUsedForCrafting, asking the
    /// inventory to remove pieces, weight, powder, liquid or water. For an operation this mod handles
    /// (Host.ConsumingPlace), the part the inventory can't cover is taken from the items lying around (see Materials)
    /// right before that, and the request passed on to the game shrinks by the same amount. Nothing is moved into the
    /// inventory. Drinking water only if Host.TakesWater.
    /// Both mods patch these game methods; each adds only inside its own scope, so together nothing counts twice.
    /// </summary>
    internal static class MaterialPatches
    {
        // Set while this mod asks the inventory itself: those answers must be the carried amount only.
        private static bool askingInventory;

        private static Nearby CountingPlace()
        {
            if (askingInventory) return null;
            var place = Workspace.Current;
            if (place == null) return null;
            return Host.CountsMaterials ? place : null;
        }

        /// <summary>Units the player carries (the inventory alone, whatever scope is open).</summary>
        public static int CarriedUnits(Inventory inventory, string gearName, bool ignoreWornOut)
        {
            askingInventory = true;
            try { return inventory.NumGearInInventory(gearName, ignoreWornOut); }
            finally { askingInventory = false; }
        }

        private static long CarriedWeight(Inventory inventory, GearItemData data, bool onlyDecomposable, bool ignoreWornOut)
        {
            askingInventory = true;
            try { return inventory.GetTotalAmountInInventory(data, onlyDecomposable, ignoreWornOut).m_Units; }
            finally { askingInventory = false; }
        }

        private static bool IsPlayerInventory(Inventory inventory)
        {
            var player = GameManager.GetInventoryComponent();
            return player != null && inventory != null && inventory.Pointer == player.Pointer;
        }

        private static void Guard(string what, Action action)
        {
            try
            {
                action();
            }
            catch (Exception e)
            {
                Workspace.LogOnce("Materials:" + what, $"Materials ({what}): {e}");
            }
        }

        // --- counts ---

        /// <summary>Units in a list: the labels and the quantity selector count the inventory's item list.</summary>
        [HarmonyPatch]
        private static class NumGearInListByName
        {
            private static MethodBase TargetMethod() =>
                AccessTools.GetDeclaredMethods(typeof(GearItemObjectExtensions))
                    .First(m => m.Name == nameof(GearItemObjectExtensions.NumGearInList) && m.GetParameters().Length == 3);

            private static void Postfix(Il2CppSystem.Collections.Generic.List<GearItemObject> __0, string __1, bool __2, ref int __result)
            {
                var place = CountingPlace();
                if (place == null || !IsInventoryList(__0)) return;
                int count = __result;
                Guard("NumGearInList", () => count += Materials.CountUnits(place, __1, __2));
                __result = count;
            }
        }

        [HarmonyPatch]
        private static class NumGearInListByData
        {
            private static MethodBase TargetMethod() =>
                AccessTools.GetDeclaredMethods(typeof(GearItemObjectExtensions))
                    .First(m => m.Name == nameof(GearItemObjectExtensions.NumGearInList) && m.GetParameters().Length == 2);

            private static void Postfix(Il2CppSystem.Collections.Generic.List<GearItemObject> __0, GearItemData __1, ref int __result)
            {
                var place = CountingPlace();
                if (place == null || !IsInventoryList(__0)) return;
                int count = __result;
                Guard("NumGearInList2", () => count += Materials.CountUnits(place, __1));
                __result = count;
            }
        }

        private static bool IsInventoryList(Il2CppSystem.Collections.Generic.List<GearItemObject> list)
        {
            var items = GameManager.GetInventoryComponent()?.m_Items;
            return list != null && items != null && list.Pointer == items.Pointer;
        }

        /// <summary>Weight of a material (recipes measured in kilograms).</summary>
        [HarmonyPatch(typeof(Inventory), nameof(Inventory.GetTotalAmountInInventory))]
        private static class GetTotalAmountInInventory
        {
            private static void Postfix(Inventory __instance, GearItemData __0, bool __1, bool __2, ref ItemWeight __result)
            {
                var place = CountingPlace();
                if (place == null || !IsPlayerInventory(__instance)) return;
                long units = __result.m_Units;
                Guard("GetTotalAmount", () => units += Materials.CountWeight(place, __0, __1, __2));
                __result = new ItemWeight(units);
            }
        }

        /// <summary>"Is there enough of it": the recipe check for materials counted in pieces.</summary>
        [HarmonyPatch]
        private static class GearInInventory
        {
            private static MethodBase TargetMethod() =>
                AccessTools.GetDeclaredMethods(typeof(Inventory)).First(m =>
                    m.Name == nameof(Inventory.GearInInventory) && m.GetParameters().Length == 3 &&
                    m.GetParameters()[0].ParameterType == typeof(GearItem));

            private static void Postfix(Inventory __instance, GearItem __0, int __1, bool __2, ref GearItem __result)
            {
                if (__result != null || __0 == null) return;
                var place = CountingPlace();
                if (place == null || !IsPlayerInventory(__instance)) return;
                GearItem found = null;
                Guard("GearInInventory", () =>
                {
                    var name = Nearby.GearName(__0);
                    int total = CarriedUnits(__instance, name, __2) + Materials.CountUnits(place, name, __2);
                    if (total >= __1) found = Materials.AnyLying(place, name, __2);
                });
                if (found != null) __result = found;
            }
        }

        /// <summary>Powder (gunpowder, salt). PlayerManager.GetTotalPowderWeight asks this too.</summary>
        [HarmonyPatch(typeof(Inventory), nameof(Inventory.GetTotalPowderWeight))]
        private static class GetTotalPowderWeight
        {
            private static void Postfix(Inventory __instance, PowderType __0, ref ItemWeight __result)
            {
                var place = CountingPlace();
                if (place == null || !IsPlayerInventory(__instance)) return;
                long units = __result.m_Units;
                Guard("GetTotalPowder", () => units += Materials.CountPowder(place, __0));
                __result = new ItemWeight(units);
            }
        }

        /// <summary>Liquids (lamp oil). PlayerManager.GetTotalLiters asks this too.</summary>
        [HarmonyPatch(typeof(Inventory), nameof(Inventory.GetTotalLiquidVolume))]
        private static class GetTotalLiquidVolume
        {
            private static void Postfix(Inventory __instance, LiquidType __0, ref ItemLiquidVolume __result)
            {
                var place = CountingPlace();
                if (place == null || !IsPlayerInventory(__instance)) return;
                long units = __result.m_Units;
                Guard("GetTotalLiquid", () => units += Materials.CountLiquid(place, __0));
                __result = new ItemLiquidVolume(units);
            }
        }

        // --- using up ---

        private static Nearby consuming;

        /// <summary>Work other than crafting (repairs) uses up materials at this place until EndUsingUp; null: none.</summary>
        public static void BeginUsingUp(Nearby place) => consuming = Host.CountsMaterials ? place : null;

        public static void EndUsingUp() => consuming = null;

        [HarmonyPatch(typeof(CraftingOperation), nameof(CraftingOperation.ConsumeMaterialsUsedForCrafting))]
        private static class ConsumeMaterialsUsedForCrafting
        {
            private static void Prefix(CraftingOperation __instance)
            {
                consuming = null;
                if (!Host.CountsMaterials) return;
                // Each mod uses up only for its own work: crafting at its stations, cooking for the cooking menu's recipes.
                Guard("Consume", () => consuming = Host.ConsumingPlace(__instance));
            }

            private static void Postfix() => consuming = null;
        }

        [HarmonyPatch(typeof(Inventory), nameof(Inventory.RemoveGearFromInventory))]
        private static class RemoveGearFromInventory
        {
            private static void Prefix(Inventory __instance, string __0, ref int __1, bool __2)
            {
                var place = consuming;
                if (place == null || __1 <= 0 || !IsPlayerInventory(__instance)) return;
                int wanted = __1;
                int taken = 0;
                Guard("RemoveGear", () =>
                {
                    int carried = CarriedUnits(__instance, __0, __2);
                    if (wanted > carried) taken = Materials.TakeUnits(place, __0, wanted - carried, __2);
                });
                __1 = Math.Max(0, wanted - taken);
            }
        }

        [HarmonyPatch(typeof(Inventory), nameof(Inventory.RemoveAmountFromInventory))]
        private static class RemoveAmountFromInventory
        {
            private static void Prefix(Inventory __instance, GearItemData __0, ref ItemWeight __1, bool __2, bool __3)
            {
                var place = consuming;
                if (place == null || __1.m_Units <= 0 || !IsPlayerInventory(__instance)) return;
                long wanted = __1.m_Units;
                long taken = 0;
                Guard("RemoveAmount", () =>
                {
                    long carried = CarriedWeight(__instance, __0, __2, __3);
                    if (wanted > carried) taken = Materials.TakeWeight(place, __0, wanted - carried, __2, __3);
                });
                __1 = new ItemWeight(Math.Max(0, wanted - taken));
            }
        }

        private static long powderTaken, liquidTaken;

        [HarmonyPatch(typeof(PlayerManager), nameof(PlayerManager.DeductPowderFromInventory))]
        private static class DeductPowderFromInventory
        {
            private static void Prefix(ref ItemWeight __0, PowderType __1)
            {
                powderTaken = 0;
                var place = consuming;
                if (place == null || __0.m_Units <= 0 || __1 == null) return;
                long wanted = __0.m_Units;
                long taken = 0;
                Guard("DeductPowder", () =>
                {
                    long carried = CarriedPowder(__1);
                    if (wanted > carried) taken = Materials.TakePowder(place, __1, wanted - carried);
                });
                powderTaken = taken;
                __0 = new ItemWeight(Math.Max(0, wanted - taken));
            }

            // The game returns what it deducted; add what came from the items lying around.
            private static void Postfix(ref ItemWeight __result)
            {
                if (powderTaken > 0) __result = new ItemWeight(__result.m_Units + powderTaken);
                powderTaken = 0;
            }
        }

        [HarmonyPatch(typeof(PlayerManager), nameof(PlayerManager.DeductLiquidFromInventory))]
        private static class DeductLiquidFromInventory
        {
            private static void Prefix(ref ItemLiquidVolume __0, LiquidType __1)
            {
                liquidTaken = 0;
                var place = consuming;
                // Water goes on to DeductWaterFromInventory, which knows the carried water supply.
                if (place == null || __0.m_Units <= 0 || __1 == null || Materials.IsDrinkingWater(__1)) return;
                long wanted = __0.m_Units;
                long taken = 0;
                Guard("DeductLiquid", () =>
                {
                    long carried = CarriedLiquid(__1);
                    if (wanted > carried) taken = Materials.TakeLiquid(place, __1, wanted - carried);
                });
                liquidTaken = taken;
                __0 = new ItemLiquidVolume(Math.Max(0, wanted - taken));
            }

            private static void Postfix(ref ItemLiquidVolume __result)
            {
                if (liquidTaken > 0) __result = new ItemLiquidVolume(__result.m_Units + liquidTaken);
                liquidTaken = 0;
            }
        }

        private static long waterTaken;

        /// <summary>
        /// Drinkable water used by a recipe: water lying around FIRST, then the carried supply (the one exception to
        /// "what you carry is used first": the bottles by the fire are emptied before the water in the pack).
        /// </summary>
        [HarmonyPatch(typeof(PlayerManager), nameof(PlayerManager.DeductWaterFromInventory))]
        private static class DeductWaterFromInventory
        {
            private static void Prefix(ref ItemLiquidVolume __0, LiquidQuality __1)
            {
                waterTaken = 0;
                var place = consuming;
                if (!Host.TakesWater || place == null || __0.m_Units <= 0 || __1 != LiquidQuality.Potable) return;
                long wanted = __0.m_Units;
                long taken = 0;
                Guard("DeductWater", () => taken = Materials.TakeDrinkableWater(place, wanted));
                waterTaken = taken;
                __0 = new ItemLiquidVolume(Math.Max(0, wanted - taken));
            }

            private static void Postfix(ref ItemLiquidVolume __result)
            {
                if (waterTaken > 0) __result = new ItemLiquidVolume(__result.m_Units + waterTaken);
                waterTaken = 0;
            }
        }

        private static long CarriedPowder(PowderType type)
        {
            askingInventory = true;
            try { return GameManager.GetInventoryComponent().GetTotalPowderWeight(type).m_Units; }
            finally { askingInventory = false; }
        }

        private static long CarriedLiquid(LiquidType type)
        {
            askingInventory = true;
            try { return GameManager.GetInventoryComponent().GetTotalLiquidVolume(type).m_Units; }
            finally { askingInventory = false; }
        }

        /// <summary>
        /// What a blueprint would take from the items lying around, worked out the way using up does it above (each
        /// requirement: the part the inventory can't cover, from the items lying around) with Materials.Pretend set: the
        /// Take functions tell BeforeTake / AfterTake and change nothing. ImmersiveCooking uses it to show a recipe's
        /// ingredients flying in while it is still prepared (the game uses them up only when the preparation ends).
        /// </summary>
        public static void PretendUse(Nearby place, BlueprintData bp)
        {
            if (place == null || bp == null) return;
            var inventory = GameManager.GetInventoryComponent();
            if (inventory == null) return;
            Materials.Pretend = true;
            try
            {
                if (bp.m_RequiredGear != null)
                    foreach (var req in bp.m_RequiredGear)
                    {
                        if (req?.m_Item == null) continue;
                        if (req.m_Units == BlueprintData.RequiredGearItem.Units.Kilograms)
                        {
                            long carried = CarriedWeight(inventory, req.m_Item.GearItemData, false, false);
                            if (req.m_Weight.m_Units > carried) Materials.TakeWeight(place, req.m_Item.GearItemData, req.m_Weight.m_Units - carried, false, false);
                        }
                        else
                        {
                            string name = Nearby.GearName(req.m_Item);
                            int carried = CarriedUnits(inventory, name, false);
                            if (req.m_Count > carried) Materials.TakeUnits(place, name, req.m_Count - carried, false);
                        }
                    }
                if (bp.m_RequiredLiquid != null)
                    foreach (var l in bp.m_RequiredLiquid)
                    {
                        if (l?.m_Liquid == null) continue;
                        long wanted = l.m_Volume.m_Units;
                        if (Materials.IsDrinkingWater(l.m_Liquid))
                        {
                            // Water lying around first, as DeductWaterFromInventory above.
                            if (!Host.TakesWater || wanted <= 0) continue;
                            Materials.TakeDrinkableWater(place, wanted);
                        }
                        else
                        {
                            long carried = CarriedLiquid(l.m_Liquid);
                            if (wanted > carried) Materials.TakeLiquid(place, l.m_Liquid, wanted - carried);
                        }
                    }
                if (bp.m_RequiredPowder != null)
                    foreach (var p in bp.m_RequiredPowder)
                    {
                        if (p?.m_Powder == null) continue;
                        long carried = CarriedPowder(p.m_Powder);
                        if (p.m_Quantity.m_Units > carried) Materials.TakePowder(place, p.m_Powder, p.m_Quantity.m_Units - carried);
                    }
            }
            catch (Exception e)
            {
                Workspace.LogOnce("PretendUse", "Working out what a recipe takes: " + e);
            }
            finally
            {
                Materials.Pretend = false;
            }
        }
    }
}
