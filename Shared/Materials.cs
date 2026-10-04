using System;
using System.Collections.Generic;
using System.Linq;
using Il2Cpp;
using Il2CppTLD.Gear;
using Il2CppTLD.IntBackedUnit;
using UnityEngine;

namespace ImmersiveShared
{
    /// <summary>
    /// Crafting materials lying next to a station: counting them (so the recipe shows as craftable) and using them up.
    ///
    /// Materials come in pieces (a stack of 10 cloth is one item with 10 units) or by weight (kilograms).
    /// Carried materials are always used first. Of the ones lying around, the highest is used first — the top of a
    /// pile, then the table, the floor, under the table last (placed items don't fall, so taking from the bottom
    /// would leave the rest hanging in the air); at the same height the one farthest from the player goes first.
    /// </summary>
    internal static class Materials
    {
        private const float SameHeight = 0.03f; // items this close in height count as lying on the same surface

        private static bool IsMaterial(GearItem gi) => !gi.IsAttachedToPlacePoint();

        /// <summary>
        /// The order to use items lying around (InUseOrder), with stackable items (tea, coffee, ...) shown once per kind,
        /// like the game shows a stack in the inventory: the first of each kind is the one used. Pieces like meat stay
        /// separate (each has its own weight, as in the inventory).
        /// </summary>
        public static List<GearItem> OnePerStack(Nearby place, IEnumerable<GearItem> items)
        {
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            return InUseOrder(place, items).Where(g => g.m_StackableItem == null || seen.Add(Nearby.GearName(g))).ToList();
        }

        /// <summary>Food (something cookable) that just lies there: not cookware, not on a fire, not cooking in a pot.</summary>
        public static bool IsLooseFood(GearItem gi) =>
            gi.m_Cookable != null && gi.m_CookingPotItem == null && IsMaterial(gi) &&
            gi.transform.parent?.GetComponentInParent<CookingPotItem>() == null;

        public static int Units(GearItem gi) => gi.m_StackableItem != null ? Math.Max(gi.m_StackableItem.m_Units, 0) : 1;

        private static bool Matches(GearItem gi, string gearName, bool ignoreWornOut) =>
            IsMaterial(gi) && !(ignoreWornOut && gi.IsWornOut()) &&
            string.Equals(Nearby.GearName(gi), Nearby.GearName(gearName), StringComparison.OrdinalIgnoreCase);

        private static bool Matches(GearItem gi, GearItemData data, bool onlyDecomposable, bool ignoreWornOut)
        {
            if (!IsMaterial(gi) || (ignoreWornOut && gi.IsWornOut())) return false;
            if (onlyDecomposable && !Inventory.IsDecomposable(gi)) return false;
            var itemData = gi.GearItemData;
            if (itemData == null || data == null) return false;
            // The same kind of item: the same data, or (for a copy of the data) the same name.
            return itemData.Pointer == data.Pointer || string.Equals(itemData.name, data.name, StringComparison.OrdinalIgnoreCase);
        }

        // --- counting ---

        public static int CountUnits(Nearby place, string gearName, bool ignoreWornOut) =>
            place.Lying().Where(g => Matches(g, gearName, ignoreWornOut)).Sum(Units);

        public static int CountUnits(Nearby place, GearItemData data) =>
            place.Lying().Where(g => Matches(g, data, false, false)).Sum(Units);

        public static long CountWeight(Nearby place, GearItemData data, bool onlyDecomposable, bool ignoreWornOut) =>
            place.Lying().Where(g => Matches(g, data, onlyDecomposable, ignoreWornOut)).Sum(g => g.GetItemWeightKG(false).m_Units);

        public static GearItem AnyLying(Nearby place, string gearName, bool ignoreWornOut) =>
            place.Lying().FirstOrDefault(g => Matches(g, gearName, ignoreWornOut));

        public static string Describe(Nearby place)
        {
            var groups = place.Lying().Where(g => g.m_ToolsItem == null && IsMaterial(g))
                .GroupBy(Nearby.GearName).Select(g => $"{g.Key} x{g.Sum(Units)}{PowderNote(g)}").ToList();
            return groups.Count == 0 ? "none" : string.Join(", ", groups);
        }

        /// <summary>" (powder TYPE 0.250 kg)" for powder containers, so the log shows what they hold.</summary>
        private static string PowderNote(IEnumerable<GearItem> items)
        {
            var powder = items.Where(g => g.m_PowderItem != null).ToList();
            if (powder.Count == 0) return "";
            var type = powder[0].m_PowderItem.m_Type;
            float kg = powder.Sum(g => g.m_PowderItem.m_Weight.ToQuantity(1f));
            return $" (powder {(type != null ? type.name : "no type")} {kg:0.000} kg)";
        }

        // --- using up ---

        public static long CountPowder(Nearby place, PowderType type) =>
            place.Lying().Where(g => IsPowder(g, type)).Sum(g => g.m_PowderItem.m_Weight.m_Units);

        private static bool IsPowder(GearItem gi, PowderType type) =>
            IsMaterial(gi) && gi.m_PowderItem != null && SameAsset(gi.m_PowderItem.m_Type, type) &&
            gi.m_PowderItem.m_Weight.m_Units > 0;

        /// <summary>
        /// The same liquid / powder type. The game loads these types through a cache (LiquidType.GetCachedLiquidType),
        /// so a recipe and an item may hold two copies of the same type: compare by name when the objects differ.
        /// </summary>
        private static bool SameAsset(UnityEngine.Object a, UnityEngine.Object b) =>
            a != null && b != null && (a.Pointer == b.Pointer || string.Equals(a.name, b.name, StringComparison.OrdinalIgnoreCase));


        /// <summary>
        /// Puts "units" of an item lying around into the inventory: the item itself if that is all of it, otherwise
        /// the needed part split off its stack (the rest stays where it lies). Returns the item in the inventory.
        /// </summary>
        public static GearItem TakeIntoInventory(Nearby place, GearItem gi, int units)
        {
            var player = GameManager.GetPlayerManagerComponent();
            int have = Units(gi);
            GearItem moved;
            if (gi.m_StackableItem != null && units > 0 && units < have)
            {
                var part = gi.InstantiateStackable(units);
                if (part == null) return null;
                gi.m_StackableItem.m_Units = have - units;
                moved = player.AddItemToPlayerInventory(part, false, false);
                Verbose.Log($"Took {units} of {have} x {Nearby.GearName(gi)} lying near the {place.Label} (the rest stays)");
            }
            else
            {
                moved = player.AddItemToPlayerInventory(gi, false, false);
                Verbose.Log($"Took {Nearby.GearName(gi)} lying near the {place.Label} (same object {moved != null && moved.Pointer == gi.Pointer})");
            }
            place.Invalidate();
            return moved;
        }

        /// <summary>
        /// Lying openly before stored in a small container; then the highest first; at the same height, the farthest
        /// from the player first.
        /// </summary>
        public static List<GearItem> InUseOrder(Nearby place, IEnumerable<GearItem> items)
        {
            var player = GameManager.GetPlayerTransform();
            Vector3 from = player != null ? player.position : Vector3.zero;
            return items
                .OrderBy(g => place != null && place.IsInContainer(g) ? 1 : 0)
                .ThenByDescending(g => Mathf.Round(Bottom(g) / SameHeight))
                .ThenByDescending(g => Vector3.Distance(g.transform.position, from))
                .ToList();
        }

        private static float Bottom(GearItem gi)
        {
            var renderer = gi.GetComponentInChildren<Renderer>();
            return renderer != null ? renderer.bounds.min.y : gi.transform.position.y;
        }

        /// <summary>Takes up to "count" units lying around; returns how many were taken.</summary>
        public static int TakeUnits(Nearby place, string gearName, int count, bool ignoreWornOut)
        {
            int taken = 0;
            foreach (var gi in InUseOrder(place, place.Lying().Where(g => Matches(g, gearName, ignoreWornOut))))
            {
                if (taken >= count) break;
                int units = Units(gi);
                int take = Math.Min(units, count - taken);
                if (take <= 0) continue;
                if (take == units)
                    place.Destroy(gi);
                else
                    gi.m_StackableItem.m_Units = units - take;
                taken += take;
                Verbose.Log($"Used {take} x {Nearby.GearName(gearName)} lying near the {place.Label} " +
                             (take == units ? "(item used up)" : $"({units} -> {units - take})") + $" at height {Bottom(gi):0.00}");
            }
            if (taken > 0) place.Invalidate();
            return taken;
        }

        /// <summary>Takes up to "amount" of weight from items lying around; returns how much was taken.</summary>
        public static long TakeWeight(Nearby place, GearItemData data, long amount, bool onlyDecomposable, bool ignoreWornOut)
        {
            long taken = 0;
            foreach (var gi in InUseOrder(place, place.Lying().Where(g => Matches(g, data, onlyDecomposable, ignoreWornOut))))
            {
                if (taken >= amount) break;
                long before = gi.GetItemWeightKG(false).m_Units;
                long removed = Inventory.RemoveAmountFromGearItem(gi, new ItemWeight(amount - taken)).m_Units;
                if (removed <= 0) continue;
                taken += removed;
                long left = gi.GetItemWeightKG(false).m_Units;
                if (left <= 0) place.Destroy(gi);
                Verbose.Log($"Used {removed} weight units of {Nearby.GearName(gi)} lying near the {place.Label} " +
                             $"({before} -> {Math.Max(left, 0)}{(left <= 0 ? ", used up" : "")})");
            }
            if (taken > 0) place.Invalidate();
            return taken;
        }

        /// <summary>Takes up to "amount" of a powder from containers lying around; an emptied one is removed, as the game does.</summary>
        public static long TakePowder(Nearby place, PowderType type, long amount)
        {
            long taken = 0;
            foreach (var gi in InUseOrder(place, place.Lying().Where(g => IsPowder(g, type))))
            {
                if (taken >= amount) break;
                var powder = gi.m_PowderItem;
                long before = powder.m_Weight.m_Units;
                long removed = powder.RemovePowder(new ItemWeight(amount - taken)).m_Units;
                if (removed <= 0) continue;
                taken += removed;
                long left = powder.m_Weight.m_Units;
                if (left <= 0) place.Destroy(gi);
                Verbose.Log($"Used {removed} weight units of {type.name} from {Nearby.GearName(gi)} lying near the {place.Label} " +
                             $"({before} -> {Math.Max(left, 0)}{(left <= 0 ? ", used up" : "")})");
            }
            if (taken > 0) place.Invalidate();
            return taken;
        }

        // --- liquids: anything the game treats as a liquid container (see Liquid) ---

        private static bool IsLiquid(GearItem gi, LiquidType type) =>
            IsMaterial(gi) && Liquid.Of(gi) is { } l && SameAsset(l.Type, type) && l.Volume > 0;

        public static long CountLiquid(Nearby place, LiquidType type) =>
            place.Lying().Where(g => IsLiquid(g, type)).Sum(g => Liquid.Of(g).Volume);

        /// <summary>
        /// Takes up to "volume" of a liquid (lamp oil, broth, cooking oil, ...) from what lies around. An emptied container
        /// stays (a can, a bottle) unless the game removes that kind when empty (broth, a bottle of water).
        /// </summary>
        public static long TakeLiquid(Nearby place, LiquidType type, long volume) =>
            TakeFrom(place, place.Lying().Where(g => IsLiquid(g, type)), volume, type.name);

        // --- drinkable water: liquid containers with drinkable water, and dropped water (WaterSupply) ---

        /// <summary>
        /// Drinkable water itself. Not LiquidType.IsPotable: broth and other drinks are "potable" too, but only water
        /// goes through the game's water supply (DeductWaterFromInventory).
        /// </summary>
        public static bool IsDrinkingWater(LiquidType type) => type != null && SameAsset(type, LiquidType.GetPotableWater());

        private static bool IsDrinkableWater(GearItem gi)
        {
            if (!IsMaterial(gi)) return false;
            if (Liquid.Of(gi) is { } l) return IsDrinkingWater(l.Type) && l.Volume > 0;
            var water = gi.m_WaterSupply;
            return water != null && water.m_WaterQuality == LiquidQuality.Potable && water.m_VolumeInLiters.m_Units > 0;
        }

        private static long WaterIn(GearItem gi) =>
            Liquid.Of(gi) is { } l ? l.Volume : gi.m_WaterSupply.m_VolumeInLiters.m_Units;

        public static long CountDrinkableWater(Nearby place) => place.Lying().Where(IsDrinkableWater).Sum(WaterIn);

        /// <summary>Takes up to "volume" of drinkable water lying around.</summary>
        public static long TakeDrinkableWater(Nearby place, long volume) =>
            TakeFrom(place, place.Lying().Where(IsDrinkableWater), volume, "water");

        // --- unsafe water (to boil): liquid containers with unsafe water, and dropped unsafe water ---

        private static bool IsUnsafeWater(GearItem gi)
        {
            if (!IsMaterial(gi)) return false;
            if (Liquid.Of(gi) is { } l) return SameAsset(l.Type, LiquidType.GetNonPotableWater()) && l.Volume > 0;
            var water = gi.m_WaterSupply;
            return water != null && water.m_WaterQuality == LiquidQuality.NonPotable && water.m_VolumeInLiters.m_Units > 0;
        }

        public static long CountUnsafeWater(Nearby place) => place.Lying().Where(IsUnsafeWater).Sum(WaterIn);

        public static long TakeUnsafeWater(Nearby place, long volume) =>
            TakeFrom(place, place.Lying().Where(IsUnsafeWater), volume, "unsafe water");

        private static long TakeFrom(Nearby place, IEnumerable<GearItem> items, long volume, string what)
        {
            long taken = 0;
            foreach (var gi in InUseOrder(place, items))
            {
                if (taken >= volume) break;
                long before = WaterIn(gi);
                long removed;
                bool destroy;
                if (Liquid.Of(gi) is { } l)
                {
                    removed = l.Remove(volume - taken);
                    destroy = l.Volume <= 0 && l.DestroyWhenEmpty;
                }
                else
                {
                    // Dropped water: just a volume.
                    removed = Math.Min(before, volume - taken);
                    gi.m_WaterSupply.m_VolumeInLiters = new ItemLiquidVolume(before - removed);
                    destroy = before - removed <= 0;
                }
                if (removed <= 0) continue;
                taken += removed;
                if (destroy) place.Destroy(gi);
                Verbose.Log($"Used {removed} volume units of {what} from {Nearby.GearName(gi)} lying near the {place.Label} " +
                             $"({before} -> {Math.Max(before - removed, 0)}{(destroy ? ", removed" : "")})");
            }
            if (taken > 0) place.Invalidate();
            return taken;
        }
    }

    /// <summary>
    /// A liquid in an item, whichever kind the game uses (all are ILiquidContainer in the game):
    /// LiquidItem (bottles, cans), DrinkItem (broth, cooking oil, drinks — the volume comes from the food's weight),
    /// InsulatedFlask. The game counts all of them in Inventory.GetTotalLiquidVolume.
    /// </summary>
    internal sealed class Liquid
    {
        public LiquidType Type { get; private set; }
        public bool DestroyWhenEmpty { get; private set; }
        private Func<long> volumeOf;
        private Func<long, long> remove;

        public long Volume => volumeOf();
        public long Remove(long volume) => remove(volume);

        public static Liquid Of(GearItem gi)
        {
            if (gi == null) return null;
            var bottle = gi.m_LiquidItem;
            if (bottle != null)
                return new Liquid
                {
                    Type = bottle.m_LiquidType,
                    DestroyWhenEmpty = bottle.ShouldDestroyWhenEmpty,
                    volumeOf = () => bottle.m_Liquid.m_Units,
                    remove = v => bottle.RemoveLiquid(new ItemLiquidVolume(v), out _).m_Units,
                };
            var drink = gi.GetComponent<DrinkItem>();
            if (drink != null)
                return new Liquid
                {
                    Type = drink.m_LiquidType,
                    DestroyWhenEmpty = drink.ShouldDestroyWhenEmpty,
                    volumeOf = () => drink.GetVolumeLitres().m_Units,
                    remove = v => drink.RemoveLiquid(new ItemLiquidVolume(v), out _).m_Units,
                };
            var flask = gi.GetComponent<InsulatedFlask>();
            if (flask != null)
                return new Liquid
                {
                    Type = flask.LiquidType,
                    DestroyWhenEmpty = flask.ShouldDestroyWhenEmpty,
                    volumeOf = () => flask.GetVolumeLitres().m_Units,
                    remove = v => flask.RemoveLiquid(new ItemLiquidVolume(v), out _).m_Units,
                };
            return null;
        }
    }
}
