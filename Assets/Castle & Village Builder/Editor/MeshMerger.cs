#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace YaraJ.CastleVillageBuilder
{
    /// <summary>
    /// Editor only. Fuses the many small pieces a wall, tower or house is built from (stone blocks, the kit's modules,
    /// torches, banners, ivy...) into one mesh per material on a single "Merged" child, with one collider made of just the
    /// pieces that had colliders. Thousands of objects become one, and it looks exactly the same. Lights, and anything
    /// else that isn't plain mesh, are left as they are.
    /// </summary>
    internal static class MeshMerger
    {
        internal const string MergedName = "Merged";

        /// <summary>Merges what's under 'root' (not 'root' itself). Undoable if 'undo'. How many pieces went into it.</summary>
        internal static int Merge(GameObject root, bool undo)
        {
            if (root == null) return 0;
            // (prefab instances with parts under them, like the kit's models, are unpacked: their parts can't go otherwise)
            var unpack = new List<GameObject>();
            foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
            {
                if (t == root.transform || t.childCount == 0 || !PrefabUtility.IsOutermostPrefabInstanceRoot(t.gameObject)) continue;
                unpack.Add(t.gameObject);
            }
            foreach (GameObject instance in unpack)
                PrefabUtility.UnpackPrefabInstance(instance, PrefabUnpackMode.Completely, undo ? InteractionMode.UserAction : InteractionMode.AutomatedAction);
            Matrix4x4 toRoot = root.transform.worldToLocalMatrix;
            var render = new Dictionary<Material, List<CombineInstance>>();
            var collide = new List<CombineInstance>();
            var merged = new List<MeshFilter>();
            var flipped = new Dictionary<Mesh, Mesh>();

            foreach (MeshFilter filter in root.GetComponentsInChildren<MeshFilter>(true))
            {
                if (filter.gameObject == root || filter.sharedMesh == null) continue;
                if (filter.transform.parent != null && filter.name == MergedName && filter.transform.parent.gameObject == root) continue;
                if (!Mergeable(filter.gameObject)) continue;
                var renderer = filter.GetComponent<MeshRenderer>();
                if (renderer == null || !renderer.enabled || !filter.gameObject.activeInHierarchy) continue;
                if (renderer.shadowCastingMode != ShadowCastingMode.On) continue;   // (a house's dark inside and the like: kept as they are)

                Matrix4x4 m = toRoot * filter.transform.localToWorldMatrix;
                Mesh mesh = filter.sharedMesh;
                // (a mirrored piece: its triangles turned round, or it'd show inside out)
                if (m.determinant < 0f)
                {
                    if (!flipped.TryGetValue(mesh, out Mesh flip)) flipped[mesh] = flip = Flip(mesh);
                    mesh = flip;
                }
                Material[] materials = renderer.sharedMaterials;
                for (int i = 0; i < mesh.subMeshCount; i++)
                {
                    Material material = materials.Length > 0 ? materials[Mathf.Min(i, materials.Length - 1)] : null;
                    if (material == null) continue;
                    if (!render.TryGetValue(material, out var list)) render[material] = list = new List<CombineInstance>();
                    list.Add(new CombineInstance { mesh = mesh, subMeshIndex = i, transform = m });
                }
                Collider c = filter.GetComponent<Collider>();
                if (c != null && c.enabled && !c.isTrigger)
                    for (int i = 0; i < mesh.subMeshCount; i++) collide.Add(new CombineInstance { mesh = mesh, subMeshIndex = i, transform = m });
                merged.Add(filter);
            }
            if (merged.Count < 2) return 0;

            // One mesh: a submesh for each material
            var parts = new List<CombineInstance>();
            var partMaterials = new List<Material>();
            foreach (var pair in render)
            {
                var part = new Mesh { indexFormat = IndexFormat.UInt32 };
                part.CombineMeshes(pair.Value.ToArray(), true, true);
                parts.Add(new CombineInstance { mesh = part, transform = Matrix4x4.identity });
                partMaterials.Add(pair.Key);
            }
            var combined = new Mesh { name = root.name + " (merged)", indexFormat = IndexFormat.UInt32 };
            combined.CombineMeshes(parts.ToArray(), false, false);
            foreach (CombineInstance p in parts) Object.DestroyImmediate(p.mesh);
            MeshUtility.Optimize(combined);
            combined.RecalculateBounds();

            var go = new GameObject(MergedName);
            go.transform.SetParent(root.transform, false);
            go.AddComponent<MeshFilter>().sharedMesh = combined;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterials = partMaterials.ToArray();
            if (collide.Count > 0)
            {
                var collision = new Mesh { name = root.name + " (collision)", indexFormat = IndexFormat.UInt32 };
                collision.CombineMeshes(collide.ToArray(), true, true);
                collision.RecalculateBounds();
                go.AddComponent<MeshCollider>().sharedMesh = collision;
            }
            GameObjectUtility.SetStaticEditorFlags(go, GameObjectUtility.GetStaticEditorFlags(root) | StaticEditorFlags.BatchingStatic | StaticEditorFlags.OccludeeStatic | StaticEditorFlags.OccluderStatic);
            if (undo) Undo.RegisterCreatedObjectUndo(go, "Optimize");

            // The pieces go (anything else on them, like a light, stays)
            foreach (MeshFilter filter in merged)
            {
                GameObject piece = filter.gameObject;
                if (Bare(piece))
                {
                    if (undo) Undo.DestroyObjectImmediate(piece); else Object.DestroyImmediate(piece);
                    continue;
                }
                foreach (Component c in new Component[] { piece.GetComponent<Collider>(), filter.GetComponent<MeshRenderer>(), filter })
                    if (c != null) { if (undo) Undo.DestroyObjectImmediate(c); else Object.DestroyImmediate(c); }
            }
            // (and empty groups left behind)
            foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
            {
                if (t == null || t == root.transform || t.childCount > 0 || t.GetComponents<Component>().Length > 1) continue;
                if (undo) Undo.DestroyObjectImmediate(t.gameObject); else Object.DestroyImmediate(t.gameObject);
            }
            return merged.Count;
        }

        // Whether a piece is plain mesh (a renderer, a mesh, maybe a collider): those are merged
        private static bool Mergeable(GameObject go)
        {
            if (go.GetComponentInParent<CastleFeatureEdge>() != null) return false;   // (kept apart, so stairs can open them)
            foreach (Component c in go.GetComponents<Component>())
            {
                if (c == null) return false;
                if (c is Transform || c is MeshFilter || c is MeshRenderer || c is Collider || c is LODGroup) continue;
                return false;
            }
            return true;
        }

        // Nothing but a mesh and what draws it, and nothing under it
        private static bool Bare(GameObject go)
        {
            if (go.transform.childCount > 0) return false;
            foreach (Component c in go.GetComponents<Component>())
                if (!(c is Transform || c is MeshFilter || c is MeshRenderer || c is Collider || c is LODGroup)) return false;
            return true;
        }

        private static Mesh Flip(Mesh mesh)
        {
            Mesh copy = Object.Instantiate(mesh);
            for (int s = 0; s < copy.subMeshCount; s++)
            {
                int[] tris = copy.GetTriangles(s);
                for (int i = 0; i < tris.Length; i += 3) { int t = tris[i]; tris[i] = tris[i + 1]; tris[i + 1] = t; }
                copy.SetTriangles(tris, s);
            }
            return copy;
        }

        // ---------------------------------------------------------------- The scene, all at once

        /// <summary>
        /// Optimizes what the builder has already put in the open scene: every wall, tower and house merged, props given
        /// distance culling and static flags. Undoable (one step).
        /// </summary>
        [MenuItem("Tools/Castle & Village Builder/Optimize Scene", priority = 23)]
        internal static void OptimizeScene()
        {
            // (the active scene's only)
            var active = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            var roots = new List<GameObject>();
            foreach (CastlePiece p in Object.FindObjectsByType<CastlePiece>(FindObjectsInactive.Include, FindObjectsSortMode.None)) if (p.gameObject.scene == active) roots.Add(p.gameObject);
            foreach (ScatteredBuilding h in Object.FindObjectsByType<ScatteredBuilding>(FindObjectsInactive.Include, FindObjectsSortMode.None)) if (h.gameObject.scene == active) roots.Add(h.gameObject);
            Undo.IncrementCurrentGroup();
            Undo.SetCurrentGroupName("Optimize Castle & Village");
            int group = Undo.GetCurrentGroup();
            int pieces = 0, merged = 0, props = 0;
            try
            {
                for (int i = 0; i < roots.Count; i++)
                {
                    if (EditorUtility.DisplayCancelableProgressBar("Optimizing", $"{roots[i].name} ({i + 1} of {roots.Count})", (float)i / roots.Count)) break;
                    int n = Merge(roots[i], true);
                    if (n > 0) { pieces += n; merged++; }
                }
                CastleFeatures.RefreshDetailPrefabs();
            foreach (GameObject root in UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects())
                {
                    if (root.name != CastleFeatures.DetailsContainer) continue;
                    foreach (Transform prop in root.transform)
                        if (!prop.name.StartsWith("Fence ")) { OptimizeProp(prop.gameObject, true); props++; }
                }
            }
            finally { EditorUtility.ClearProgressBar(); }
            Undo.CollapseUndoOperations(group);
            Debug.Log($"Castle & Village Builder: merged {pieces} pieces into {merged} objects (walls, towers and houses), and optimized {props} props.");
        }

        /// <summary>A prop: static, and culled when it's only a speck on screen (it fades out at a distance).</summary>
        internal static void OptimizeProp(GameObject prop, bool undo)
        {
            var renderers = prop.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0) return;
            if (undo) Undo.RegisterFullObjectHierarchyUndo(prop, "Optimize");
            foreach (Transform t in prop.GetComponentsInChildren<Transform>(true))
                GameObjectUtility.SetStaticEditorFlags(t.gameObject, StaticEditorFlags.BatchingStatic | StaticEditorFlags.OccludeeStatic);
            LODGroup lod = prop.GetComponent<LODGroup>() ?? prop.AddComponent<LODGroup>();
            lod.SetLODs(new[] { new LOD(0.012f, renderers) });
            lod.fadeMode = LODFadeMode.CrossFade;
            lod.animateCrossFading = true;
            lod.RecalculateBounds();
        }
    }
}
#endif
