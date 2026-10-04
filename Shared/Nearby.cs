using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Il2Cpp;
using UnityEngine;

namespace ImmersiveShared
{
    /// <summary>
    /// A place the player works at (a workbench, a fishing hole, a fire) and the items lying around it.
    /// Distance is measured from the edge of the object's colliders, so a long workbench counts along its whole length.
    /// </summary>
    internal sealed class Nearby
    {
        private const float CacheSeconds = 0.5f;

        public readonly string Label;

        private readonly Bounds[] boxes;
        private readonly Vector3 center;
        private readonly float radius;

        private Dictionary<string, GearItem> toolsByName;
        private float toolsBuiltAt = float.MinValue;

        private Nearby(string label, Bounds[] boxes, Vector3 center, float radius)
        {
            Label = label;
            this.boxes = boxes;
            this.center = center;
            this.radius = radius;
        }

        public static Nearby AroundPoint(string label, Vector3 point, float radius) =>
            new(label, Array.Empty<Bounds>(), point, radius);

        /// <summary>Solid colliders of the object; its renderers if it has none; its position as the last resort.</summary>
        public static Nearby AroundObject(string label, GameObject go, float radius)
        {
            var boxes = new List<Bounds>();
            foreach (var c in go.GetComponentsInChildren<Collider>())
                if (c != null && c.enabled && !c.isTrigger) boxes.Add(c.bounds);
            if (boxes.Count == 0)
                foreach (var r in go.GetComponentsInChildren<Renderer>())
                    if (r != null && r.enabled) boxes.Add(r.bounds);
            return new Nearby(label, boxes.ToArray(), go.transform.position, radius);
        }

        public float Distance(Vector3 p)
        {
            if (boxes.Length == 0) return Vector3.Distance(center, p);
            float best = float.MaxValue;
            foreach (var b in boxes) best = Math.Min(best, Vector3.Distance(b.ClosestPoint(p), p));
            return best;
        }

        public bool Contains(Vector3 p) => Distance(p) <= radius;

        /// <summary>
        /// Items lying in the world (not carried, not in a container) within reach, matching the filter.
        /// "quick" is checked first, before the costlier checks: every call walks all gear of the scene.
        /// </summary>
        public List<GearItem> Items(Func<GearItem, bool> filter, Func<GearItem, bool> quick = null)
        {
            var watch = System.Diagnostics.Stopwatch.StartNew();
            var result = new List<GearItem>();
            var all = GearManager.m_Gear;
            if (all == null) return result;
            int count = all.Count;
            for (int i = 0; i < count; i++)
            {
                var gi = all[i];
                if (gi == null || (quick != null && !quick(gi))) continue;
                if (!IsLyingAround(gi)) continue;
                if (!Contains(gi.transform.position)) continue;
                if (filter == null || filter(gi)) result.Add(gi);
            }
            LastScan = $"{count} items scanned in {watch.Elapsed.TotalMilliseconds:0.0} ms";
            return result;
        }

        /// <summary>Size and duration of the latest Items() scan, for the log.</summary>
        public static string LastScan { get; private set; } = "";

        private List<GearItem> lying;
        private float lyingBuiltAt = float.MinValue;

        /// <summary>
        /// All items within reach, kept for half a second: the crafting menu asks about every recipe in a row.
        /// With WithSmallContainers, also the items in small containers there (after the ones lying openly).
        /// </summary>
        public List<GearItem> Lying()
        {
            if (lying == null || Time.realtimeSinceStartup - lyingBuiltAt > CacheSeconds)
            {
                lying = Items(null);
                containerOf.Clear();
                if (WithSmallContainers) AddSmallContainerItems(lying);
                lyingBuiltAt = Time.realtimeSinceStartup;
            }
            // Used up or picked up meanwhile.
            lying.RemoveAll(g => g == null || !(containerOf.ContainsKey(g.Pointer) ? g.m_InsideContainer : IsLyingAround(g)));
            return lying;
        }

        /// <summary>Forget cached items (after something was used up).</summary>
        public void Invalidate()
        {
            lying = null;
            toolsByName = null;
        }

        // --- small containers (a workbench's own box and the like) ---

        /// <summary>The heaviest load a container may take to count as small (the workbench box holds 5 kg).</summary>
        public const float SmallContainerKg = 5f;

        /// <summary>Also use the items in small, unlocked, already searched containers within reach.</summary>
        public bool WithSmallContainers { get; set; }

        private readonly Dictionary<IntPtr, Container> containerOf = new();
        private List<Container> smallContainers;

        public bool IsInContainer(GearItem gi) => gi != null && containerOf.ContainsKey(gi.Pointer);

        private void AddSmallContainerItems(List<GearItem> result)
        {
            if (smallContainers == null)
            {
                smallContainers = new List<Container>();
                long limit = new Il2CppTLD.IntBackedUnit.ItemWeight((long)(SmallContainerKg * 1e9)).m_Units;
                foreach (var c in UnityEngine.Object.FindObjectsOfType<Container>())
                    if (c != null && c.gameObject.activeInHierarchy && c.m_Capacity.m_Units > 0 && c.m_Capacity.m_Units <= limit &&
                        Contains(c.transform.position))
                        smallContainers.Add(c);
                Verbose.Log($"Small containers near the {Label}: " + (smallContainers.Count == 0 ? "none"
                    : string.Join(", ", smallContainers.Select(c => $"'{c.name}' ({c.m_Capacity.m_Units / 1e9:0.#} kg, " +
                                                                   $"{(c.IsLocked() ? "locked" : c.IsInspected() ? "searched" : "not searched yet")})"))));
            }

            var list = new Il2CppSystem.Collections.Generic.List<GearItem>();
            foreach (var c in smallContainers)
            {
                // A locked or never searched container: its content isn't known to the player.
                if (c == null || c.IsLocked() || !c.IsInspected()) continue;
                list.Clear();
                c.GetItems(list);
                for (int i = 0; i < list.Count; i++)
                {
                    var gi = list[i];
                    if (gi == null || containerOf.ContainsKey(gi.Pointer)) continue;
                    containerOf[gi.Pointer] = c;
                    result.Add(gi);
                }
            }
        }

        /// <summary>Removes a used-up item: from its container if it was in one, otherwise from the world.</summary>
        public void Destroy(GearItem gi)
        {
            if (containerOf.TryGetValue(gi.Pointer, out var c) && c != null)
            {
                containerOf.Remove(gi.Pointer);
                c.DestroyGear(gi);
                c.UpdateActiveMeshFromTotalWeight();
            }
            else
            {
                GearManager.DestroyGearObject(gi);
            }
        }

        /// <summary>
        /// A usable (not worn out) item with this gear name within reach, or null. Of several, the same order as for
        /// materials: the highest, then the farthest from the player (Materials.InUseOrder).
        /// </summary>
        public GearItem FindTool(string gearName)
        {
            if (string.IsNullOrEmpty(gearName)) return null;
            if (toolsByName == null || Time.realtimeSinceStartup - toolsBuiltAt > CacheSeconds)
            {
                toolsByName = new Dictionary<string, GearItem>(StringComparer.OrdinalIgnoreCase);
                foreach (var group in Lying().Where(g => !g.IsWornOut()).GroupBy(GearName, StringComparer.OrdinalIgnoreCase))
                    toolsByName[group.Key] = Materials.InUseOrder(this, group)[0];
                toolsBuiltAt = Time.realtimeSinceStartup;
            }
            return toolsByName.TryGetValue(GearName(gearName), out var found) ? found : null;
        }

        public static bool IsLyingAround(GearItem gi) =>
            gi != null && !gi.m_InPlayerInventory && !gi.m_InsideContainer && gi.gameObject.activeInHierarchy;

        public static string GearName(GearItem gi) => gi == null ? "" : GearName(gi.name);

        public static string GearName(string name)
        {
            if (name == null) return "";
            int clone = name.IndexOf("(Clone)", StringComparison.Ordinal);
            return (clone >= 0 ? name.Substring(0, clone) : name).Trim();
        }

        public string Describe()
        {
            var inv = CultureInfo.InvariantCulture;
            string shape = boxes.Length == 0
                ? "point " + center.ToString("F1")
                : boxes.Length + " box(es), size " + string.Join(" + ", boxes.Select(b => b.size.ToString("F1")));
            return Label + " (" + shape + ", reach " + radius.ToString("0.0", inv) + " m)";
        }
    }
}
