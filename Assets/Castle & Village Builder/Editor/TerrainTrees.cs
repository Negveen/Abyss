#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace YaraJ.CastleVillageBuilder
{
    /// <summary>Editor only. Takes a Unity Terrain's trees out from under what the builder puts down (Builder Settings).</summary>
    internal static class TerrainTrees
    {
        /// <summary>Every tree within 'reach' of a point for which 'inside' (given its place in the world) says yes.</summary>
        internal static void Clear(System.Func<Vector3, bool> inside, Vector3 near, float reach)
        {
            if (!BuilderSettings.Get().removeTerrainTrees) return;
            foreach (Terrain terrain in Terrain.activeTerrains)
            {
                TerrainData data = terrain != null ? terrain.terrainData : null;
                if (data == null || data.treeInstanceCount == 0) continue;
                Vector3 origin = terrain.transform.position, size = data.size;
                if (near.x + reach < origin.x || near.x - reach > origin.x + size.x || near.z + reach < origin.z || near.z - reach > origin.z + size.z) continue;

                TreeInstance[] trees = data.treeInstances;
                var kept = new List<TreeInstance>(trees.Length);
                foreach (TreeInstance tree in trees)
                {
                    Vector3 world = origin + Vector3.Scale(tree.position, size);
                    if ((world - near).sqrMagnitude <= reach * reach && inside(world)) continue;
                    kept.Add(tree);
                }
                if (kept.Count == trees.Length) continue;
                Undo.RegisterCompleteObjectUndo(data, "Clear Terrain Trees");
                data.SetTreeInstances(kept.ToArray(), true);
                terrain.Flush();
            }
        }
    }
}
#endif
