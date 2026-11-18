#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace YaraJ.CastleVillageBuilder
{
    /// <summary>
    /// Editor only. Castle & Village Builder's own settings (Builder Settings.asset, in the package's folder): the plants
    /// and rocks it can put round houses (as well as Paint Land's scatter kinds, if Paint Land's in the project), and how
    /// its paths look when they're made as meshes (with no Paint Land land to paint them on).
    /// </summary>
    public sealed class BuilderSettings : ScriptableObject
    {
        [System.Serializable]
        public class NatureKind
        {
            public string name = "Bushes";
            [Tooltip("The prefabs to pick from, at random.")]
            public List<GameObject> prefabs = new List<GameObject>();
            [Tooltip("Random size, from-to (1 = the prefab's own size).")]
            public Vector2 size = new Vector2(0.8f, 1.2f);
            [Tooltip("How many in each clump, from-to.")]
            public Vector2Int clump = new Vector2Int(1, 3);
            [Tooltip("Not on ground steeper than this (degrees).")]
            [Range(0f, 90f)] public float maxSlope = 35f;
            [Tooltip("Tilt with the ground (0: stand straight up, 1: lean with the slope).")]
            [Range(0f, 1f)] public float followSlope = 0.3f;
        }

        [Tooltip("Build on Paint Land's land, and use its paths, textures and scatter, when it's in the scene (if Paint Land's in the project). Off: always build on any ground with a collider (a Unity Terrain, say).")]
        public bool usePaintLand = true;

        [Tooltip("Plants and rocks to put round houses (Houses > Nature).")]
        public List<NatureKind> nature = new List<NatureKind>();

        [Header("Paths (as meshes)")]
        [Tooltip("The material of paths made as meshes (none: a plain one in the path's colour).")]
        public Material pathMaterial;
        [Tooltip("How far mesh paths sit above the ground (m), so they don't flicker into it.")]
        [Range(0.005f, 0.2f)] public float pathLift = 0.03f;

        [Header("Performance")]
        [Tooltip("Fuse each wall, tower and house's many small pieces (stone blocks, the kit's modules, torches, banners...) into one mesh: thousands of objects become one, and it looks the same. Turn off to keep every piece as its own object.")]
        public bool mergeDetails = true;

        [Header("Unity Terrain")]
        [Tooltip("Take a Unity Terrain's trees from under new houses, walls and paths.")]
        public bool removeTerrainTrees = true;

        internal static string AssetPath => MedievalVillageSetup.PackageFolder + "/Builder Settings.asset";

        private static BuilderSettings cached;

        /// <summary>The settings (made the first time they're asked for).</summary>
        internal static BuilderSettings Get()
        {
            if (cached != null) return cached;
            cached = AssetDatabase.LoadAssetAtPath<BuilderSettings>(AssetPath);
            if (cached != null) return cached;
            cached = CreateInstance<BuilderSettings>();
            // To start with, the kit's own props (add your plants and rocks too)
            cached.nature.Add(Kit("Crates & Wagons", new Vector2Int(1, 2), "Prop_Crate", "Prop_Crate", "Prop_Wagon"));
            cached.nature.Add(Kit("Loose Bricks", new Vector2Int(2, 4), "Prop_Brick1", "Prop_Brick2", "Prop_Brick3", "Prop_Brick4"));
            AssetDatabase.CreateAsset(cached, AssetPath);
            AssetDatabase.SaveAssets();
            return cached;
        }

        private static NatureKind Kit(string name, Vector2Int clump, params string[] models)
        {
            var kind = new NatureKind { name = name, clump = clump, size = new Vector2(0.9f, 1.1f), followSlope = 0.6f };
            foreach (string model in models)
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{MedievalVillageSetup.ModelsFolder}/{model}.fbx");
                if (prefab != null) kind.prefabs.Add(prefab);
            }
            return kind;
        }
    }
}
#endif
