#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace YaraJ.CastleVillageBuilder
{
    /// <summary>
    /// Sets up the Medieval Village MegaKit (Medieval Village Kit, CC0, by Quaternius) for this project: URP
    /// materials made from its textures (Materials/), its normal maps marked as such, and its models using those
    /// materials, with colliders. Run from Tools > Castle & Village Builder > Set Up Medieval Village Kit; Castle & Village Builder runs it
    /// on its own the first time it's opened.
    /// </summary>
    internal static class MedievalVillageSetup
    {
        /// <summary>
        /// Where Castle & Village Builder is in the project (Assets/Castle & Village Builder, unless it's been moved): found
        /// from where its editor scripts are.
        /// </summary>
        public static string PackageFolder
        {
            get
            {
                if (packageFolder != null && AssetDatabase.IsValidFolder(packageFolder)) return packageFolder;
                packageFolder = "Assets/Castle & Village Builder";
                foreach (string guid in AssetDatabase.FindAssets("YaraJ.CastleVillageBuilder.Editor t:AssemblyDefinitionAsset"))
                {
                    string path = AssetDatabase.GUIDToAssetPath(guid);
                    if (Path.GetFileNameWithoutExtension(path) != "YaraJ.CastleVillageBuilder.Editor") continue;
                    packageFolder = Path.GetDirectoryName(Path.GetDirectoryName(path)).Replace(Path.DirectorySeparatorChar, '/');
                    break;
                }
                return packageFolder;
            }
        }
        private static string packageFolder;

        public static string KitFolder => PackageFolder + "/Medieval Village Kit";
        public static string ModelsFolder => KitFolder + "/Models";
        private static string TexturesFolder => KitFolder + "/Textures";
        private static string MaterialsFolder => KitFolder + "/Materials";

        // Each of the kit's materials: its colour texture (or a colour), normal map, smoothness and metalness
        private struct Look
        {
            public string BaseMap, NormalMap;
            public Color Colour;
            public float Smoothness, Metallic;
            public Look(string baseMap, string normalMap, float smoothness = 0.15f, float metallic = 0f, Color? colour = null)
            {
                BaseMap = baseMap; NormalMap = normalMap; Smoothness = smoothness; Metallic = metallic; Colour = colour ?? Color.white;
            }
        }

        private static readonly Dictionary<string, Look> Looks = new Dictionary<string, Look>
        {
            ["MI_WoodTrim"] = new Look("T_WoodTrim_BaseColor", "T_WoodTrim_Normal", 0.18f),
            ["MI_WoodTrim_Wear"] = new Look("T_WoodTrim_BaseColor", "T_WoodTrim_Normal", 0.12f, 0f, new Color(0.85f, 0.82f, 0.78f)),
            ["MI_Plaster"] = new Look("T_Plaster_BaseColor", "T_Plaster_Normal", 0.08f),
            ["MI_RockTrim"] = new Look("T_RockTrim_BaseColor", "T_RockTrim_Normal", 0.12f),
            ["MI_Brick"] = new Look("T_Brick_BaseColor", "T_Brick_Normal", 0.1f),
            ["MI_RedBrick"] = new Look("T_RedBrick_BaseColor", "T_Brick_Normal", 0.1f),
            ["MI_UnevenBrick"] = new Look("T_UnevenBrick_BaseColor", "T_UnevenBrick_Normal", 0.1f),
            ["MI_RoundTiles"] = new Look("T_RoundTiles_BaseColor", "T_RoundTiles_Normal", 0.22f),
            ["MI_WindowGlass"] = new Look(null, null, 0.9f, 0.1f, new Color(0.16f, 0.22f, 0.27f)),
            ["MI_MetalOrnaments"] = new Look(null, null, 0.45f, 0.85f, new Color(0.22f, 0.21f, 0.2f)),
            ["MI_VineLeaf"] = new Look("T_VineLeaf", null, 0.1f, 0f, new Color(0.33f, 0.52f, 0.2f)),
            ["MI_Vine"] = new Look("T_VineLeaf", null, 0.1f, 0f, new Color(0.33f, 0.52f, 0.2f)),
        };

        public static bool IsSetUp => AssetDatabase.LoadAssetAtPath<Material>($"{MaterialsFolder}/MI_WoodTrim.mat") != null;

        [MenuItem("Tools/Castle & Village Builder/Set Up Medieval Village Kit", priority = 20)]
        public static void SetUp()
        {
            if (!AssetDatabase.IsValidFolder(ModelsFolder))
            {
                Debug.LogError($"The Medieval Village kit isn't in {KitFolder}.");
                return;
            }
            try
            {
                AssetDatabase.StartAssetEditing();
                SetUpTextures();
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
            }

            Dictionary<string, Material> materials = MakeMaterials();

            try
            {
                AssetDatabase.StartAssetEditing();
                foreach (string guid in AssetDatabase.FindAssets("t:Model", new[] { ModelsFolder }))
                {
                    SetUpModel(AssetDatabase.GUIDToAssetPath(guid), materials);
                }
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
            }
            AssetDatabase.SaveAssets();
            UseBuildingShader();
            AddSurfaceDepth();
            WeatherByDefault();
            Debug.Log("Medieval Village kit set up: materials in " + MaterialsFolder + ".");
        }

        // ---------------------------------------------------------------- Surface depth

        // The surfaces given depth: their colour texture, the kit's occlusion (an ORM texture's red), how deep, how bumpy
        private static readonly (string material, string baseMap, string orm, float depth, float bump)[] Depths =
        {
            ("MI_UnevenBrick", "T_UnevenBrick_BaseColor", null, 0.035f, 1.6f),
            ("MI_Brick", "T_Brick_BaseColor", null, 0.025f, 1.5f),
            ("MI_RedBrick", "T_RedBrick_BaseColor", null, 0.025f, 1.5f),
            ("MI_RockTrim", "T_RockTrim_BaseColor", "T_RockTrim_ORM", 0.02f, 1.5f),
            ("MI_Plaster", "T_Plaster_BaseColor", "T_Plaster_ORM", 0.01f, 1.3f),
            ("MI_WoodTrim", "T_WoodTrim_BaseColor", "T_WoodTrim_ORM", 0.015f, 1.4f),
            ("MI_WoodTrim_Wear", "T_WoodTrim_BaseColor", "T_WoodTrim_ORM", 0.015f, 1.4f),
        };

        /// <summary>
        /// Gives the stone, brick, plaster and wood depth: a height map (made from each colour texture: mortar and cracks
        /// are low) so the surface shifts as you look across it, an occlusion map so the gaps are shaded, and stronger
        /// normals. On the kit's materials and every colour theme's copies of them (walls, castle features and houses).
        /// </summary>
        [MenuItem("Tools/Castle & Village Builder/Add Surface Depth", priority = 22)]
        internal static void AddSurfaceDepth()
        {
            var maps = new Dictionary<string, (Texture2D height, Texture2D occlusion)>();
            foreach (var d in Depths)
            {
                if (maps.ContainsKey(d.baseMap)) continue;
                var source = AssetDatabase.LoadAssetAtPath<Texture2D>($"{TexturesFolder}/{d.baseMap}.png");
                if (source == null) continue;
                var orm = d.orm != null ? AssetDatabase.LoadAssetAtPath<Texture2D>($"{TexturesFolder}/{d.orm}.png") : null;
                maps[d.baseMap] = MakeDepthMaps(source, orm, d.baseMap.Replace("_BaseColor", ""));
            }

            int done = 0;
            foreach (string guid in AssetDatabase.FindAssets("t:Material", new[] { KitFolder }))
            {
                var material = AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(guid));
                if (material == null || !material.HasProperty("_ParallaxMap")) continue;
                foreach (var d in Depths)
                {
                    // (the kit's own, and the themes' copies: "MI_Brick (Moss)")
                    if (material.name != d.material && !material.name.StartsWith(d.material + " (")) continue;
                    if (!maps.TryGetValue(d.baseMap, out var m) || m.height == null) break;
                    material.SetTexture("_ParallaxMap", m.height);
                    material.SetFloat("_Parallax", d.depth);
                    material.EnableKeyword("_PARALLAXMAP");
                    material.SetTexture("_OcclusionMap", m.occlusion);
                    material.SetFloat("_OcclusionStrength", 1f);
                    material.EnableKeyword("_OCCLUSIONMAP");
                    if (material.GetTexture("_BumpMap") != null) material.SetFloat("_BumpScale", d.bump);
                    EditorUtility.SetDirty(material);
                    done++;
                    break;
                }
            }
            AssetDatabase.SaveAssets();
            Debug.Log($"Castle & Village Builder: surface depth (height, occlusion, stronger normals) on {done} materials.");
        }

        // ---------------------------------------------------------------- Weathering

        private static readonly string[] Weathers = { "MI_UnevenBrick", "MI_Brick", "MI_RedBrick", "MI_RockTrim" };

        /// <summary>The kit's stone materials (and every colour theme's copies of them) that weather.</summary>
        private static List<Material> weathered;
        private static bool watching;

        internal static List<Material> WeatheredMaterials()
        {
            // (found once, and again when the project changes: the window asks for them every time it draws)
            if (weathered != null && !weathered.Exists(m => m == null)) return weathered;
            if (!watching) { watching = true; EditorApplication.projectChanged += () => weathered = null; }
            var list = new List<Material>();
            foreach (string guid in AssetDatabase.FindAssets("t:Material", new[] { KitFolder + "/Materials" }))
            {
                var m = AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(guid));
                if (m == null || !m.HasProperty("_ClearCoatMask") || m.shader != BuildingShader) continue;
                foreach (string w in Weathers)
                    if (m.name == w || m.name.StartsWith(w + " (")) { list.Add(m); break; }
            }
            list.Sort((a, b) => a.name.Length.CompareTo(b.name.Length));   // (the kit's own first)
            weathered = list;
            return list;
        }

        /// <summary>Floors and paving (the moss on them is set apart from the walls').</summary>
        internal static bool IsPaving(Material m) => m.name == "MI_Brick" || m.name.StartsWith("MI_Brick (");

        /// <summary>The stone's look: weathered or not, grime, colour variation, and the moss colour (its amounts are set apart).</summary>
        internal static void SetWeathering(Material m, bool on, float grime, float variation, Color mossColour)
        {
            if (on) m.EnableKeyword("_WEATHERED"); else m.DisableKeyword("_WEATHERED");
            m.SetFloat("_Weathered", on ? 1f : 0f);
            m.SetFloat("_ClearCoatMask", grime);
            m.SetFloat("_DetailAlbedoMapScale", variation);
            Color c = m.GetColor("_SpecColor");
            m.SetColor("_SpecColor", new Color(mossColour.r, mossColour.g, mossColour.b, c.a));
            EditorUtility.SetDirty(m);
        }

        /// <summary>The moss: how much on top of things, how much in the joints, and how big its patches are.</summary>
        internal static void SetMoss(Material m, float onTops, float inJoints, float patchSize)
        {
            m.SetFloat("_ClearCoatSmoothness", onTops);
            m.SetFloat("_DetailNormalMapScale", patchSize);
            Color c = m.GetColor("_SpecColor");
            m.SetColor("_SpecColor", new Color(c.r, c.g, c.b, inJoints));
            EditorUtility.SetDirty(m);
        }

        internal static void Weather(Material m, float grime, float wallMoss, float floorMoss, float variation, Color mossColour)
        {
            SetWeathering(m, true, grime, variation, mossColour);
            bool floor = IsPaving(m);
            SetMoss(m, floor ? floorMoss : wallMoss, floor ? floorMoss * 0.8f : wallMoss, floor ? 1.4f : 1f);
        }

        /// <summary>Weathers the stone the first time (lived-in), leaving any that's been set alone.</summary>
        internal static void WeatherByDefault()
        {
            foreach (Material m in WeatheredMaterials())
                if (m.GetFloat("_Weathered") < 0.5f && m.GetFloat("_ClearCoatMask") == 0f && m.GetFloat("_ClearCoatSmoothness") == 0f)
                    Weather(m, 0.55f, 0.35f, 0.15f, 0.6f, new Color(0.27f, 0.36f, 0.14f));
            AssetDatabase.SaveAssets();
        }

        // A height map and an occlusion map from a colour texture (read through the GPU, so it needn't be readable)
        private static (Texture2D height, Texture2D occlusion) MakeDepthMaps(Texture2D source, Texture2D orm, string name)
        {
            string heightPath = $"{TexturesFolder}/{name}_Height.png", occlusionPath = $"{TexturesFolder}/{name}_Occlusion.png";
            int size = Mathf.Min(1024, Mathf.Max(source.width, source.height));
            float[] lum = ReadChannel(source, size, false);
            float[] ao = orm != null ? ReadChannel(orm, size, true) : null;

            // Height: brightness, stretched to its range (the darkest few percent: the lowest, the mortar), lightly smoothed
            var sorted = (float[])lum.Clone();
            System.Array.Sort(sorted);
            float lo = sorted[(int)(sorted.Length * 0.03f)], hi = sorted[(int)(sorted.Length * 0.97f)];
            var height = new float[lum.Length];
            for (int i = 0; i < lum.Length; i++)
            {
                float h = Mathf.Clamp01((lum[i] - lo) / Mathf.Max(0.001f, hi - lo));
                height[i] = h * h * (3f - 2f * h);
            }
            height = Blur(height, size, 1);
            // Occlusion: lower than what's round it is shaded (and the kit's own, where there is one)
            float[] around = Blur(height, size, Mathf.Max(3, size / 128));
            var occlusion = new float[lum.Length];
            for (int i = 0; i < lum.Length; i++)
            {
                float cavity = 1f - Mathf.Clamp01((around[i] - height[i]) * 2.2f);
                occlusion[i] = Mathf.Lerp(0.35f, 1f, cavity) * (ao != null ? Mathf.Lerp(0.5f, 1f, ao[i]) : 1f);
            }
            return (SaveGrey(height, size, heightPath), SaveGrey(occlusion, size, occlusionPath));
        }

        private static float[] ReadChannel(Texture2D texture, int size, bool red)
        {
            var rt = RenderTexture.GetTemporary(size, size, 0, RenderTextureFormat.ARGB32, red ? RenderTextureReadWrite.Linear : RenderTextureReadWrite.sRGB);
            Graphics.Blit(texture, rt);
            RenderTexture was = RenderTexture.active;
            RenderTexture.active = rt;
            var read = new Texture2D(size, size, TextureFormat.RGBA32, false, red);
            read.ReadPixels(new Rect(0, 0, size, size), 0, 0);
            read.Apply();
            RenderTexture.active = was;
            RenderTexture.ReleaseTemporary(rt);
            Color[] px = read.GetPixels();
            Object.DestroyImmediate(read);
            var values = new float[px.Length];
            for (int i = 0; i < px.Length; i++) values[i] = red ? px[i].r : px[i].r * 0.299f + px[i].g * 0.587f + px[i].b * 0.114f;
            return values;
        }

        // A wrapping box blur (textures tile, so their edges blur into each other)
        private static float[] Blur(float[] values, int size, int radius)
        {
            var across = new float[values.Length];
            var result = new float[values.Length];
            float count = radius * 2 + 1;
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float sum = 0f;
                    for (int k = -radius; k <= radius; k++) sum += values[y * size + ((x + k) % size + size) % size];
                    across[y * size + x] = sum / count;
                }
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float sum = 0f;
                    for (int k = -radius; k <= radius; k++) sum += across[(((y + k) % size + size) % size) * size + x];
                    result[y * size + x] = sum / count;
                }
            return result;
        }

        private static Texture2D SaveGrey(float[] values, int size, string path)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGB24, false, true);
            var px = new Color[values.Length];
            for (int i = 0; i < values.Length; i++) px[i] = new Color(values[i], values[i], values[i]);
            tex.SetPixels(px);
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(path);
            if (AssetImporter.GetAtPath(path) is TextureImporter importer)
            {
                importer.sRGBTexture = false;
                importer.wrapMode = TextureWrapMode.Repeat;
                importer.textureCompression = TextureImporterCompression.CompressedHQ;
                importer.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        private static void SetUpTextures()
        {
            foreach (string guid in AssetDatabase.FindAssets("t:Texture2D", new[] { TexturesFolder }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (!(AssetImporter.GetAtPath(path) is TextureImporter importer)) continue;
                string name = Path.GetFileNameWithoutExtension(path);
                bool normal = name.EndsWith("_Normal");
                bool data = name.EndsWith("_ORM") || name.EndsWith("_Roughness") || name.EndsWith("_Height") || name.EndsWith("_Occlusion") || name.Contains("Noise") || name.Contains("Wear");
                bool changed = false;
                if (normal && importer.textureType != TextureImporterType.NormalMap) { importer.textureType = TextureImporterType.NormalMap; changed = true; }
                if (data && importer.sRGBTexture) { importer.sRGBTexture = false; changed = true; }
                if (name == "T_VineLeaf" && !importer.alphaIsTransparency) { importer.alphaIsTransparency = true; changed = true; }
                if (importer.maxTextureSize > 2048) { importer.maxTextureSize = 2048; changed = true; }
                if (changed) importer.SaveAndReimport();
            }
        }

        private static Dictionary<string, Material> MakeMaterials()
        {
            if (!AssetDatabase.IsValidFolder(MaterialsFolder)) AssetDatabase.CreateFolder(KitFolder, "Materials");
            Shader lit = Shader.Find("Universal Render Pipeline/Lit");
            Shader building = BuildingShader;
            var made = new Dictionary<string, Material>();
            foreach (var pair in Looks)
            {
                string path = $"{MaterialsFolder}/{pair.Key}.mat";
                var material = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (material == null)
                {
                    material = new Material(WetsInRain(pair.Key) ? building : lit) { name = pair.Key };
                    AssetDatabase.CreateAsset(material, path);
                }
                else if (material.shader != (WetsInRain(pair.Key) ? building : lit)) material.shader = WetsInRain(pair.Key) ? building : lit;

                Look look = pair.Value;
                Texture2D baseMap = Texture(look.BaseMap);
                material.SetTexture("_BaseMap", baseMap);
                material.SetColor("_BaseColor", look.Colour);
                Texture2D normalMap = Texture(look.NormalMap);
                material.SetTexture("_BumpMap", normalMap);
                material.SetFloat("_BumpScale", 1f);
                if (normalMap != null) material.EnableKeyword("_NORMALMAP");
                else material.DisableKeyword("_NORMALMAP");
                material.SetFloat("_Smoothness", look.Smoothness);
                material.SetFloat("_Metallic", look.Metallic);
                if (pair.Key == "MI_VineLeaf" || pair.Key == "MI_Vine")
                {
                    material.SetFloat("_AlphaClip", 1f);
                    material.SetFloat("_Cutoff", 0.5f);
                    material.EnableKeyword("_ALPHATEST_ON");
                    material.SetFloat("_Cull", 0f);
                }
                material.enableInstancing = true;
                EditorUtility.SetDirty(material);
                made[pair.Key] = material;
            }
            return made;
        }

        /// <summary>The shader the kit's surfaces (and the castle's) use: Lit, wet in the rain (Shaders/BuildingLit.shader).</summary>
        internal static Shader BuildingShader
        {
            get
            {
                Shader shader = Shader.Find("Custom/Building Lit");
                return shader != null ? shader : Shader.Find("Universal Render Pipeline/Lit");
            }
        }

        /// <summary>Whether a material gets wet in the rain (not glass, see-through leaves, or flames).</summary>
        internal static bool WetsInRain(string materialName)
        {
            string n = materialName.ToLowerInvariant();
            return !(n.Contains("glass") || n.Contains("vine") || n.Contains("flame") || n.Contains("window dark"));
        }

        /// <summary>
        /// Every material of the kit (its own, the colour themes', and the castle's made ones) that uses plain Lit, moved
        /// onto the wet-in-rain Building Lit (its colours, textures and settings kept: they're the same properties).
        /// </summary>
        [MenuItem("Tools/Castle & Village Builder/Make Buildings Wet In The Rain", priority = 21)]
        internal static void UseBuildingShader()
        {
            Shader lit = Shader.Find("Universal Render Pipeline/Lit"), building = BuildingShader;
            if (building == null || building == lit) return;
            int moved = 0;
            foreach (string guid in AssetDatabase.FindAssets("t:Material", new[] { KitFolder }))
            {
                var material = AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(guid));
                // (the hidden insides were unlit: lit now, so they fog in the distance and darken at night)
                if (material != null && material.name == "House Interior" && material.shader != building)
                {
                    material.shader = building;
                    material.SetColor("_BaseColor", new Color(0.035f, 0.03f, 0.025f));
                    material.SetFloat("_Smoothness", 0f);
                    EditorUtility.SetDirty(material);
                    moved++;
                    continue;
                }
                if (material == null || material.shader != lit || !WetsInRain(material.name)) continue;
                material.shader = building;
                EditorUtility.SetDirty(material);
                moved++;
            }
            if (moved > 0) AssetDatabase.SaveAssets();
            Debug.Log($"Buildings wet in the rain: {moved} material{(moved == 1 ? "" : "s")} moved onto Custom/Building Lit.");
        }

        private static Texture2D Texture(string name) =>
            string.IsNullOrEmpty(name) ? null : AssetDatabase.LoadAssetAtPath<Texture2D>($"{TexturesFolder}/{name}.png");

        private static void SetUpModel(string path, Dictionary<string, Material> materials)
        {
            if (!(AssetImporter.GetAtPath(path) is ModelImporter importer)) return;
            bool changed = false;
            if (!importer.addCollider) { importer.addCollider = true; changed = true; }
            if (importer.animationType != ModelImporterAnimationType.None) { importer.animationType = ModelImporterAnimationType.None; changed = true; }
            if (importer.importAnimation) { importer.importAnimation = false; changed = true; }
            if (importer.importCameras || importer.importLights) { importer.importCameras = false; importer.importLights = false; changed = true; }
            if (importer.materialImportMode != ModelImporterMaterialImportMode.ImportStandard) { importer.materialImportMode = ModelImporterMaterialImportMode.ImportStandard; changed = true; }

            // The model's materials, by name, to the kit's ("MI_WoodTrim.001" and the like too)
            foreach (Object asset in AssetDatabase.LoadAllAssetRepresentationsAtPath(path))
            {
                if (!(asset is Material embedded)) continue;
                string name = embedded.name;
                int dot = name.IndexOf('.');
                string key = dot > 0 ? name.Substring(0, dot) : name;
                if (!materials.TryGetValue(key, out Material material)) continue;
                var id = new AssetImporter.SourceAssetIdentifier(typeof(Material), name);
                if (importer.GetExternalObjectMap().TryGetValue(id, out Object current) && current == material) continue;
                importer.AddRemap(id, material);
                changed = true;
            }
            if (changed) importer.SaveAndReimport();
        }
    }
}
#endif
