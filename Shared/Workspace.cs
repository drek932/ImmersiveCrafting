using System;
using System.Collections.Generic;
using Il2Cpp;
using UnityEngine;

namespace ImmersiveShared
{
    /// <summary>
    /// While the game looks something up for the player's work (a tool, a material, an ingredient), this holds the place
    /// the player works at, so the inventory lookups can fall back to the items lying around it.
    /// Scopes nest; the outermost one decides the place. Outside a scope the inventory works as usual.
    /// Each mod adds its own places in another part of this class (Workspace.Crafting.cs / Workspace.Cooking.cs).
    /// </summary>
    internal static partial class Workspace
    {
        private static int depth;
        private static Func<Nearby> resolver;
        private static Nearby current;
        private static bool resolved;

        /// <summary>Called when the outermost scope opens (true) or closes (false); a mod may implement it.</summary>
        static partial void ScopeChanged(bool open);

        public static void Enter(Func<Nearby> placeResolver)
        {
            if (depth++ > 0) return;
            resolver = placeResolver;
            resolved = false;
            current = null;
            ScopeChanged(true);
        }

        public static void Exit()
        {
            if (depth == 0 || --depth > 0) return;
            resolver = null;
            current = null;
            ScopeChanged(false);
        }

        /// <summary>The place of the current scope, or null outside a scope / when the feature doesn't apply.</summary>
        public static Nearby Current
        {
            get
            {
                if (depth == 0) return null;
                if (!resolved)
                {
                    resolved = true;
                    try
                    {
                        current = resolver?.Invoke();
                    }
                    catch (Exception e)
                    {
                        LogOnce("resolve:" + e.GetType().Name, "Could not find the work place: " + e);
                        current = null;
                    }
                }
                return current;
            }
        }

        /// <summary>Called every frame: a scope never outlives a frame, even if a game function threw inside it.</summary>
        public static void EndFrame()
        {
            if (depth == 0) return;
            LogOnce("unbalanced", "A lookup scope was left open (a game function failed inside it); closed.");
            depth = 0;
            resolver = null;
            current = null;
            ScopeChanged(false);
        }

        /// <summary>
        /// The whole fire (campfire, stove, fireplace, barrel) for an object the player interacted with, which may be
        /// just a part of it (a cooking slot, the fire itself): the object with the fire interaction, else the object.
        /// </summary>
        public static GameObject FireRoot(GameObject go)
        {
            if (go == null) return null;
            var fireplace = go.GetComponentInParent<FireplaceInteraction>();
            if (fireplace == null) fireplace = go.GetComponentInChildren<FireplaceInteraction>();
            var root = fireplace != null ? fireplace.gameObject : go;
            MsgOnce("fireRoot:" + go.Pointer,
                $"Fire object '{go.name}' -> area from '{root.name}' ({(fireplace != null ? fireplace.GetIl2CppType().Name : "no fire interaction")})");
            return root;
        }

        // --- logging ---

        private static readonly HashSet<string> logged = new();

        public static void LogOnce(string key, string message)
        {
            if (logged.Add(key)) Host.Log.Warning(message);
        }

        [System.Diagnostics.Conditional("VERBOSE")]
        public static void MsgOnce(string key, string message)
        {
            if (logged.Add(key)) Verbose.Log(message);
        }
    }
}
