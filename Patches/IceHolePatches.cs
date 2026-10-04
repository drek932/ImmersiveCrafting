using HarmonyLib;
using Il2Cpp;
using ImmersiveShared;

namespace ImmersiveCrafting
{
    /// <summary>
    /// Cutting or clearing an ice fishing hole: the tool list is built from the inventory by name
    /// (GetHighestConditionGearThatMatchesName), so tools lying next to the hole are offered too.
    /// Fishing itself (tackle, bait, tip-ups) is not touched.
    /// </summary>
    internal static class IceHolePatches
    {
        [HarmonyPatch(typeof(Panel_IceFishingHoleClear), nameof(Panel_IceFishingHoleClear.UpdateAvailableTools))]
        private static class UpdateAvailableTools
        {
            private static void Prefix(Panel_IceFishingHoleClear __instance) =>
                Workspace.Enter(() => Workspace.IceHole(__instance));

            private static void Postfix(Panel_IceFishingHoleClear __instance)
            {
                Workspace.Exit();
                try
                {
                    var tools = __instance.m_AvailableTools;
                    int lying = 0;
                    if (tools != null)
                        for (int i = 0; i < tools.Count; i++)
                            if (Nearby.IsLyingAround(tools[i])) lying++;
                    if (lying > 0) Verbose.Log($"Ice hole: {tools.Count} tool(s) offered, {lying} of them lying nearby");
                }
                catch (System.Exception e)
                {
                    Workspace.LogOnce("IceHoleLog", "Ice hole tool list: " + e);
                }
            }
        }

        /// <summary>Logs the tool wear: the game degrades m_ToolUsed by progress × hole frozen × tool wear rate.</summary>
        [HarmonyPatch(typeof(Panel_IceFishingHoleClear), nameof(Panel_IceFishingHoleClear.OnBreakIceComplete))]
        private static class OnBreakIceComplete
        {
            private static void Prefix(Panel_IceFishingHoleClear __instance, bool success, float progress, out float __state)
            {
                __state = -1f;
                try
                {
                    var tool = __instance.m_ToolUsed;
                    if (tool == null) return;
                    __state = tool.CurrentHP;
                    var hole = __instance.m_IceFishingHole;
                    Verbose.Log($"Ice broken with {Nearby.GearName(tool)} ({(Nearby.IsLyingAround(tool) ? "lying nearby" : "carried")}): " +
                                 $"success {success}, progress {progress:0.00}, hole frozen {(hole != null ? hole.NormalizedFrozen.ToString("0.00") : "no hole")}, " +
                                 $"wear rate {(tool.m_ToolsItem != null ? tool.m_ToolsItem.m_DegradePerHourCrafting.ToString("0.00") : "-")}, HP {tool.CurrentHP:0.00}");
                }
                catch (System.Exception e)
                {
                    Workspace.LogOnce("IceBreakLog", "Ice break log: " + e);
                }
            }

            private static void Postfix(Panel_IceFishingHoleClear __instance, float __state)
            {
                try
                {
                    var tool = __instance.m_ToolUsed;
                    if (tool != null && __state >= 0f)
                        Verbose.Log($"  {Nearby.GearName(tool)} HP {__state:0.00} -> {tool.CurrentHP:0.00}");
                }
                catch (System.Exception e)
                {
                    Workspace.LogOnce("IceBreakLog2", "Ice break log: " + e);
                }
            }
        }

        [HarmonyPatch(typeof(Panel_IceFishingHoleClear), nameof(Panel_IceFishingHoleClear.CanBuildFishingHole))]
        private static class CanBuildFishingHole
        {
            private static void Prefix(Panel_IceFishingHoleClear __instance) =>
                Workspace.Enter(() => Workspace.IceHole(__instance));

            private static void Postfix() => Workspace.Exit();
        }
    }
}
