#if UNITY_EDITOR
using UnityEngine;

namespace YaraJ.CastleVillageBuilder
{
    /// <summary>
    /// Editor only. The ground the builder puts things on: Paint Land's land when it's in the scene (through
    /// PaintLandLink), otherwise anything with a collider (a Unity Terrain, a mesh...), leaving out what the builder
    /// itself made (houses, walls, paths) and things scattered on the ground (plants, rocks).
    /// </summary>
    internal static class Ground
    {
        /// <summary>Is the ground Paint Land's land? (Read again with Refresh, when a stroke starts.)</summary>
        internal static bool OnLand { get; private set; }

        /// <summary>Looks again for Paint Land's land in the scene. True if it's the ground.</summary>
        internal static bool Refresh() => OnLand = PaintLandLink.Available();

        internal static bool Raycast(Ray ray, out Vector3 point, out Vector3 normal)
        {
            if (OnLand && PaintLandLink.Raycast(ray, out point, out normal))
            {
                // (an upper area, a bridge or a plaza on the land is ground too, if it's nearer)
                if (WalkableHit(ray, Vector3.Distance(ray.origin, point), out RaycastHit on)) { point = on.point; normal = on.normal; }
                return true;
            }
            foreach (RaycastHit h in SortedHits(ray, 5000f))
            {
                if (Skipped(h.collider.transform)) continue;
                point = h.point;
                normal = h.normal;
                return true;
            }
            point = default;
            normal = Vector3.up;
            return false;
        }

        internal static bool At(Vector3 world, out float height)
        {
            bool found = At(world, out Vector3 ground, out _);
            height = ground.y;
            return found;
        }

        /// <summary>The ground straight under (or over) a point, and which way it faces there.</summary>
        internal static bool At(Vector3 world, out Vector3 ground, out Vector3 normal)
        {
            if (OnLand)
            {
                bool found = PaintLandLink.GroundAt(world, out ground, out normal);
                var down = new Ray(new Vector3(world.x, world.y + 500f, world.z), Vector3.down);
                float reach = found ? 500f + world.y - ground.y : 1500f;
                if (WalkableHit(down, reach, out RaycastHit on)) { ground = on.point; normal = on.normal; return true; }
                return found;
            }
            var ray = new Ray(new Vector3(world.x, world.y + 500f, world.z), Vector3.down);
            foreach (RaycastHit h in SortedHits(ray, 1500f))
            {
                if (Skipped(h.collider.transform)) continue;
                ground = h.point;
                normal = h.normal;
                return true;
            }
            ground = world;
            normal = Vector3.up;
            return false;
        }

        /// <summary>Is a point under Paint Land's water (or within 'margin' of it)? (False with no Paint Land.)</summary>
        internal static bool UnderWater(Vector3 point, float margin) => OnLand && PaintLandLink.UnderWater(point, margin);

        private static RaycastHit[] SortedHits(Ray ray, float distance)
        {
            RaycastHit[] hits = Physics.RaycastAll(ray, distance, ~0, QueryTriggerInteraction.Ignore);
            System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
            return hits;
        }

        // The nearest walkable castle feature (an upper area, a bridge, a plaza) along a ray, within 'reach'
        private static bool WalkableHit(Ray ray, float reach, out RaycastHit hit)
        {
            hit = default;
            float best = reach;
            bool found = false;
            foreach (RaycastHit h in Physics.RaycastAll(ray, reach, ~0, QueryTriggerInteraction.Ignore))
            {
                CastleFeature f = h.collider.GetComponentInParent<CastleFeature>();
                if (f == null || !f.walkable || h.distance >= best) continue;
                best = h.distance;
                hit = h;
                found = true;
            }
            return found;
        }

        /// <summary>
        /// Not ground: the builder's own houses, walls and paths, its plants, Paint Land's scatter, and castle features
        /// that aren't walkable (upper areas, bridges and plazas are ground: houses and props can go on them).
        /// </summary>
        internal static bool Skipped(Transform t)
        {
            if (PaintLandLink.IsScattered(t)) return true;
            CastleFeature feature = t != null ? t.GetComponentInParent<CastleFeature>() : null;
            if (feature != null) return !feature.walkable;
            for (; t != null; t = t.parent)
            {
                if (t.TryGetComponent(out ScatteredBuilding _) || t.TryGetComponent(out CastlePiece _)) return true;
                if (t.parent == null && (t.name == CastleVillageBuilderWindow.ContainerName || t.name == CastleWallBuilder.ContainerName || t.name == BuildingPaths.ContainerName || t.name == Nature.ContainerName || t.name == CastleFeatures.DetailsContainer)) return true;
            }
            return false;
        }
    }
}
#endif
