using System;
using MelonLoader;
using ImmersiveShared;

[assembly: MelonInfo(typeof(ImmersiveCrafting.Main), "ImmersiveCrafting", "1.0.0", "drek932")]
[assembly: MelonGame("Hinterland", "TheLongDark")]

namespace ImmersiveCrafting
{
    /// <summary>
    /// Items lying next to where you craft count as if you carried them:
    ///  - tools and materials near a workbench, ammo workbench, furniture workbench, forge or fire (crafting at a fire),
    ///    including small containers at a workbench (its own drawer);
    ///  - tools near an ice fishing hole are usable to clear or cut the hole.
    /// Works alone or together with ImmersiveCooking (the cooking menu's recipes are left to that mod).
    ///
    ///  Core/    — settings, texts, finding items nearby (Nearby, Materials, Workspace);
    ///  Patches/ — hooks into game functions (Harmony).
    /// </summary>
    public sealed class Main : MelonMod
    {
        internal static MelonLogger.Instance Log;
        internal static Settings Settings;

        public override void OnInitializeMelon()
        {
            Log = LoggerInstance;
            Settings = new Settings();
            Settings.AddToModSettings("Immersive Crafting");
            ModSettingsPatches.Apply(HarmonyInstance);
            Log.Msg("Settings: " + Settings.Describe());
        }

        public override void OnUpdate()
        {
            // A lookup scope never outlives a frame, even if a game function threw inside it.
            Workspace.EndFrame();
        }

        public override void OnLateUpdate()
        {
            try
            {
                ModSettingsPatches.LateUpdate();
            }
            catch (Exception)
            {
                // Interface not ready yet (loading screens).
            }
        }
    }
}
