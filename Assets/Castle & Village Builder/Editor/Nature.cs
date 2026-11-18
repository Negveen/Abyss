#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace YaraJ.CastleVillageBuilder
{
    /// <summary>
    /// Editor only. The plants and rocks the builder can put round houses: Paint Land's scatter kinds (when its land is
    /// the ground) and the builder's own (Builder Settings). Paint Land's are put down by its Scatter brush's own code
    /// when it has it (so they're part of its scatter); the rest go under the "Village Plants" object in the scene.
    /// </summary>
    internal static class Nature
    {
        internal const string ContainerName = "Village Plants";

        /// <summary>Every kind there is to put round houses.</summary>
        internal static List<NatureKind> Kinds(bool paintLand)
        {
            var kinds = paintLand ? PaintLandLink.Kinds() : new List<NatureKind>();
            foreach (BuilderSettings.NatureKind own in BuilderSettings.Get().nature)
            {
                if (own == null) continue;
                var kind = new NatureKind { Name = own.name, MaxSlope = own.maxSlope, Size = own.size, Source = own };
                foreach (GameObject prefab in own.prefabs) if (prefab != null) kind.Prefabs.Add(prefab);
                if (kind.Prefabs.Count > 0 && kinds.TrueForAll(k => k.Name != kind.Name)) kinds.Add(kind);
            }
            return kinds;
        }

        /// <summary>A clump of a kind at a spot on the ground (undoable). Never null.</summary>
        internal static List<GameObject> PlaceClump(NatureKind kind, Vector3 ground, Vector3 normal)
        {
            if (!(kind.Source is BuilderSettings.NatureKind))
            {
                List<GameObject> theirs = PaintLandLink.PlaceClump(kind, ground, normal, 0.45f);
                if (theirs != null) return theirs;
            }

            var own = kind.Source as BuilderSettings.NatureKind;
            Vector2Int clump = own != null ? own.clump : new Vector2Int(1, 3);
            float lean = own != null ? own.followSlope : 0.3f;
            int count = Random.Range(Mathf.Max(1, clump.x), Mathf.Max(1, clump.x, clump.y) + 1);
            var placed = new List<GameObject>();
            Transform parent = Container();
            for (int i = 0; i < count; i++)
            {
                Vector3 at = ground;
                Vector3 up = normal;
                if (i > 0)
                {
                    Vector2 off = Random.insideUnitCircle * 0.9f;
                    if (!Ground.At(ground + new Vector3(off.x, 0f, off.y), out at, out up)) continue;
                    if (Vector3.Angle(up, Vector3.up) > kind.MaxSlope) continue;
                }
                GameObject prefab = kind.Prefabs[Random.Range(0, kind.Prefabs.Count)];
                var thing = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
                if (thing == null) continue;
                Quaternion tilt = Quaternion.FromToRotation(Vector3.up, Vector3.Slerp(Vector3.up, up, lean));
                thing.transform.SetPositionAndRotation(at, tilt * Quaternion.Euler(0f, Random.Range(0f, 360f), 0f) * prefab.transform.localRotation);
                thing.transform.localScale = prefab.transform.localScale * Random.Range(kind.Size.x, Mathf.Max(kind.Size.x, kind.Size.y));
                Undo.RegisterCreatedObjectUndo(thing, "Plant Round House");
                placed.Add(thing);
            }
            return placed;
        }

        private static Transform Container()
        {
            foreach (GameObject root in UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects())
            {
                if (root.name == ContainerName) return root.transform;
            }
            var made = new GameObject(ContainerName);
            Undo.RegisterCreatedObjectUndo(made, "Plant Round House");
            return made.transform;
        }
    }
}
#endif
