using System;
using System.Collections.Generic;
using System.Linq;
using Il2Cpp;
using ImmersiveCrafting;

namespace ImmersiveShared
{
    /// <summary>ImmersiveCrafting's places: crafting stations (workbenches, forge, a fire) and ice fishing holes.</summary>
    internal static partial class Workspace
    {
        // --- crafting stations ---

        private static IntPtr stationKey;
        private static float stationRadius;
        private static Nearby station;

        /// <summary>
        /// Set by ImmersiveCooking while its cooking menu checks recipes (they are crafting recipes too). Read through the
        /// AppDomain so the two mods don't reference each other: during those checks this mod counts nothing.
        /// </summary>
        private const string CookingMenuBusy = "ImmersiveCooking.MenuScope";

        public static bool CookingIsCounting() => AppDomain.CurrentDomain.GetData(CookingMenuBusy) is bool busy && busy;

        /// <summary>True if the operation is a recipe being prepared in the cooking menu: that belongs to ImmersiveCooking.</summary>
        public static bool IsCookingRecipe(Il2CppTLD.Gear.CraftingOperation operation) =>
            operation != null && InterfaceManager.TryGetPanel<Panel_Cooking>(out var cooking) && cooking != null &&
            cooking.m_RecipePrepOperation != null && cooking.m_RecipePrepOperation.Pointer == operation.Pointer;

        /// <summary>The workbench / ammo workbench / furniture workbench / forge / fire the crafting menu is open at.</summary>
        public static Nearby CraftingStation()
        {
            if (!Main.Settings.stations || CookingIsCounting()) return null;
            if (!InterfaceManager.TryGetPanel<Panel_Crafting>(out var panel) || panel == null) return null;
            var place = Station(panel.m_CurrentLocation);

            // The panel keeps its last location; don't use it when the player has walked away from that station.
            var player = GameManager.GetPlayerTransform();
            if (place != null && player != null && place.Distance(player.position) > Main.Settings.stationRadius + 3f) return null;
            return place;
        }

        /// <summary>The area around a crafting location if it is one of the stations, else null.</summary>
        public static Nearby Station(CraftingLocationInterface location)
        {
            if (!Main.Settings.stations || location == null) return null;
            var component = StationOf(location);
            if (component == null) return null;

            // Crafting at a fire uses its own reach setting.
            bool isFire = component.TryCast<Fire>() != null;
            bool isForge = component.TryCast<Forge>() != null;
            float radius = isFire ? Main.Settings.fireRadius : isForge ? Main.Settings.forgeRadius : Main.Settings.stationRadius;
            if (station == null || component.Pointer != stationKey || radius != stationRadius)
            {
                station = isFire
                    ? Nearby.AroundObject("fire", FireRoot(component.gameObject), radius)
                    : Nearby.AroundObject(component.GetIl2CppType().Name, isForge ? ForgeRoot(component.gameObject) : component.gameObject, radius);
                // Workbenches also use small containers next to them (their own box); fires don't.
                station.WithSmallContainers = !isFire && Main.Settings.smallContainers;
                stationKey = component.Pointer;
                stationRadius = radius;
                supplied.Clear();
                var tools = station.Lying().Where(g => g.m_ToolsItem != null).Select(Nearby.GearName).ToList();
                Verbose.Log("Crafting at " + station.Describe() + "; tools lying there: " +
                             (tools.Count == 0 ? "none" : string.Join(", ", tools)) +
                             (Main.Settings.materials ? "; other items: " + Materials.Describe(station) : ""));
            }
            return station;
        }

        /// <summary>Forget the station when the crafting menu closes, so the next opening logs what lies there.</summary>
        public static void CraftingClosed() => station = null;

        /// <summary>
        /// The whole forge: the Forge component sits on a small part of it (a 0.4 x 0.2 m box was measured), so the area
        /// is taken from the highest parent still named like the forge.
        /// </summary>
        private static UnityEngine.GameObject ForgeRoot(UnityEngine.GameObject go)
        {
            var t = go.transform;
            while (t.parent != null && t.parent.name.IndexOf("forge", StringComparison.OrdinalIgnoreCase) >= 0) t = t.parent;
            MsgOnce("forgeRoot:" + go.Pointer, $"Forge part '{go.name}' (parent '{(go.transform.parent != null ? go.transform.parent.name : "none")}') -> area from '{t.name}'");
            return t.gameObject;
        }

        private static UnityEngine.Component StationOf(CraftingLocationInterface location)
        {
            UnityEngine.Component c = location.TryCast<WorkBench>();
            if (c != null) return c;
            c = location.TryCast<AmmoWorkBench>();
            if (c != null) return c;
            c = location.TryCast<FurnitureWorkBench>();
            if (c != null) return c;
            c = location.TryCast<Forge>();
            if (c != null) return c;
            return location.TryCast<Fire>();
        }

        // --- ice fishing holes ---

        /// <summary>The hole being cleared, or the player's position when a new hole is cut.</summary>
        public static Nearby IceHole(Panel_IceFishingHoleClear panel)
        {
            if (!Main.Settings.iceHoles) return null;
            float radius = Main.Settings.iceHoleRadius;
            var hole = panel != null && !panel.m_IsBuildingHole ? panel.m_IceFishingHole : null;
            var player = GameManager.GetPlayerTransform();
            // The panel keeps its last hole; when cutting a new one elsewhere that hole is far away.
            if (hole != null && (player == null || UnityEngine.Vector3.Distance(hole.transform.position, player.position) < 5f))
                return Nearby.AroundPoint("ice hole", hole.transform.position, radius);
            return player != null ? Nearby.AroundPoint("new ice hole", player.position, radius) : null;
        }

        // --- which tools from the world were used ---

        private static readonly HashSet<string> supplied = new();

        /// <summary>One log line per tool and place: which item from the world the game was given.</summary>
        public static void NoteSupplied(Nearby place, Il2Cpp.GearItem gi, string how)
        {
            var key = place.Label + "|" + Nearby.GearName(gi) + "|" + how;
            if (!supplied.Add(key)) return;
            Verbose.Log("Using " + Nearby.GearName(gi) + " lying near the " + place.Label + " (" + how + ")");
        }
    }
}
