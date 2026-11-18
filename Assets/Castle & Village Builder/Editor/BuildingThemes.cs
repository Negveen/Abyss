#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace YaraJ.CastleVillageBuilder
{
    /// <summary>
    /// Editor only. The colour themes Castle & Village Builder paints its houses in (Medieval Village Kit/Building
    /// Themes.asset): each recolours the kit's roof tiles, wall plaster, timber and stone, keeping their texture (grey
    /// copies of the kit's textures, tinted). Each theme has its own copies of the kit's materials (Materials/Themes/).
    /// </summary>
    public sealed class BuildingThemes : ScriptableObject
    {
        [System.Serializable]
        public sealed class Theme
        {
            public string name = "New theme";
            [Tooltip("Used for new houses (picked at random from the ticked ones).")]
            public bool on = true;
            [Tooltip("The kit's own colours (the colours below aren't used).")]
            public bool original;
            [Tooltip("A castle stone colour (Castle Walls > Stone colour): it recolours only the stone, and isn't one of the house themes.")]
            public bool stoneOnly;
            public Color roof = new Color(0.78f, 0.36f, 0.22f);
            public Color walls = new Color(0.86f, 0.78f, 0.62f);
            public Color wood = new Color(0.5f, 0.36f, 0.25f);
            public Color stone = new Color(0.62f, 0.56f, 0.48f);
        }

        public List<Theme> themes = new List<Theme>();

        internal static string AssetPath => MedievalVillageSetup.KitFolder + "/Building Themes.asset";
        private static string ThemeMaterials => MedievalVillageSetup.KitFolder + "/Materials/Themes";
        private static string GreyFolder => MedievalVillageSetup.KitFolder + "/Textures/Themes";

        private enum Surface { Roof, Walls, Wood, Stone }

        // Which of the kit's materials each theme colour is for (and its texture), with how much darker it's tinted
        private static readonly Dictionary<string, (Surface surface, string texture, float shade)> Tinted =
            new Dictionary<string, (Surface, string, float)>
            {
                ["MI_RoundTiles"] = (Surface.Roof, "T_RoundTiles_BaseColor", 1f),
                ["MI_Plaster"] = (Surface.Walls, "T_Plaster_BaseColor", 1f),
                ["MI_WoodTrim"] = (Surface.Wood, "T_WoodTrim_BaseColor", 1f),
                ["MI_WoodTrim_Wear"] = (Surface.Wood, "T_WoodTrim_BaseColor", 0.85f),
                ["MI_UnevenBrick"] = (Surface.Stone, "T_UnevenBrick_BaseColor", 1f),
                ["MI_Brick"] = (Surface.Stone, "T_Brick_BaseColor", 1f),
                ["MI_RockTrim"] = (Surface.Stone, "T_RockTrim_BaseColor", 0.95f),
                ["MI_RedBrick"] = (Surface.Stone, "T_RedBrick_BaseColor", 0.9f),
            };

        private static BuildingThemes loaded;

        /// <summary>The castle's stone colours (Castle Walls > Stone colour), by plain names.</summary>
        internal static readonly (string name, Color colour)[] StonePalette =
        {
            ("Sandstone", C(0.78f, 0.68f, 0.52f)), ("Honey Limestone", C(0.82f, 0.71f, 0.48f)), ("Pale Limestone", C(0.85f, 0.83f, 0.77f)),
            ("Chalk White", C(0.92f, 0.91f, 0.87f)), ("Light Granite", C(0.7f, 0.69f, 0.67f)), ("Grey Granite", C(0.55f, 0.55f, 0.56f)),
            ("Dark Basalt", C(0.33f, 0.33f, 0.35f)), ("Charcoal", C(0.21f, 0.21f, 0.23f)), ("Slate Blue", C(0.44f, 0.5f, 0.59f)),
            ("Frost Grey", C(0.63f, 0.67f, 0.73f)), ("Mossy Green", C(0.5f, 0.56f, 0.42f)), ("Olive Fieldstone", C(0.57f, 0.55f, 0.4f)),
            ("Red Sandstone", C(0.67f, 0.4f, 0.32f)), ("Rose Granite", C(0.74f, 0.57f, 0.53f)), ("Baked Clay", C(0.73f, 0.47f, 0.34f)),
            ("Golden Ochre", C(0.8f, 0.63f, 0.36f)), ("Brown Fieldstone", C(0.5f, 0.4f, 0.3f)), ("Desert Tan", C(0.76f, 0.62f, 0.45f)),
        };

        internal const string CustomStone = "Custom Stone";

        /// <summary>The themes (made with some to start from, the first time).</summary>
        internal static BuildingThemes Get()
        {
            if (loaded != null) return loaded;
            loaded = AssetDatabase.LoadAssetAtPath<BuildingThemes>(AssetPath);
            if (loaded != null) { loaded.AddStones(); return loaded; }
            loaded = CreateInstance<BuildingThemes>();
            loaded.themes.AddRange(new[]
            {
                new Theme { name = "Original", original = true },
                Make("Terracotta", C(0.78f, 0.36f, 0.22f), C(0.86f, 0.78f, 0.62f), C(0.5f, 0.36f, 0.25f), C(0.62f, 0.56f, 0.48f)),
                Make("Slate", C(0.32f, 0.36f, 0.42f), C(0.84f, 0.84f, 0.82f), C(0.36f, 0.3f, 0.26f), C(0.5f, 0.5f, 0.52f)),
                Make("Moss", C(0.38f, 0.48f, 0.28f), C(0.78f, 0.76f, 0.62f), C(0.42f, 0.34f, 0.24f), C(0.5f, 0.52f, 0.44f)),
                Make("Ochre", C(0.6f, 0.3f, 0.18f), C(0.86f, 0.66f, 0.38f), C(0.42f, 0.28f, 0.2f), C(0.6f, 0.5f, 0.4f)),
                Make("Nordic", C(0.28f, 0.27f, 0.3f), C(0.9f, 0.9f, 0.88f), C(0.25f, 0.2f, 0.18f), C(0.55f, 0.55f, 0.55f)),
                Make("Coast", C(0.25f, 0.38f, 0.55f), C(0.93f, 0.91f, 0.85f), C(0.3f, 0.36f, 0.45f), C(0.62f, 0.6f, 0.56f)),
            });
            loaded.AddStones();
            AssetDatabase.CreateAsset(loaded, AssetPath);
            AssetDatabase.SaveAssets();
            return loaded;
        }

        // The tint that makes the stone look like a swatch's colour (the stone texture brightens what it's tinted)
        internal static Color StoneTint(Color swatch) { Color c = swatch * 0.62f; c.a = 1f; return c; }
        internal static Color StoneSwatch(Color tint) { Color c = tint / 0.62f; c.a = 1f; return c; }

        // The stone colours (added to themes made before there were any): kept stone-only and off for houses
        private void AddStones()
        {
            bool added = false;
            foreach (var (name, colour) in StonePalette)
            {
                Color wanted = StoneTint(colour);
                Theme found = Find(name);
                if (found != null)
                {
                    // (one made before the colours were matched to their swatches: made right)
                    if (found.stoneOnly && found.stone != wanted) { found.stone = wanted; Refresh(found); added = true; }
                    continue;
                }
                themes.Add(new Theme { name = name, on = false, stoneOnly = true, stone = wanted });
                added = true;
            }
            if (Find(CustomStone) == null) { themes.Add(new Theme { name = CustomStone, on = false, stoneOnly = true, stone = C(0.62f, 0.56f, 0.48f) }); added = true; }
            if (added) { EditorUtility.SetDirty(this); AssetDatabase.SaveAssetIfDirty(this); }
        }

        private static Color C(float r, float g, float b) => new Color(r, g, b);
        private static Theme Make(string name, Color roof, Color walls, Color wood, Color stone) =>
            new Theme { name = name, roof = roof, walls = walls, wood = wood, stone = stone };

        internal Theme Find(string name)
        {
            foreach (Theme t in themes) if (t != null && t.name == name) return t;
            return null;
        }

        /// <summary>One of the ticked themes, at random (null: none ticked).</summary>
        internal Theme Pick(System.Random random)
        {
            var ticked = new List<Theme>();
            foreach (Theme t in themes) if (t != null && t.on && !t.stoneOnly) ticked.Add(t);
            return ticked.Count > 0 ? ticked[random.Next(ticked.Count)] : null;
        }

        // ---------------------------------------------------------------- Painting a house

        /// <summary>Gives a house (or anything made of the kit's pieces) a theme's materials.</summary>
        internal static void Apply(GameObject house, string themeName, bool record)
        {
            BuildingThemes all = Get();
            Theme theme = all.Find(themeName);
            if (house.TryGetComponent(out ScatteredBuilding marker))
            {
                if (record) Undo.RecordObject(marker, "Theme");
                marker.theme = theme != null ? theme.name : "";
            }
            foreach (Renderer renderer in house.GetComponentsInChildren<Renderer>(true))
            {
                Material[] materials = renderer.sharedMaterials;
                bool changed = false;
                for (int i = 0; i < materials.Length; i++)
                {
                    Material now = materials[i];
                    if (now == null) continue;
                    string kitName = KitName(now);
                    if (!Tinted.ContainsKey(kitName)) continue;
                    if (theme != null && theme.stoneOnly && Tinted[kitName].surface != Surface.Stone) continue;   // (a stone colour: only the stone)
                    Material wanted = all.MaterialFor(theme, kitName);
                    if (wanted == null || wanted == now) continue;
                    materials[i] = wanted;
                    changed = true;
                }
                if (!changed) continue;
                if (record) Undo.RecordObject(renderer, "Theme");
                renderer.sharedMaterials = materials;
            }
        }

        // "MI_Plaster (Slate)" -> "MI_Plaster"
        private static string KitName(Material material)
        {
            string name = material.name;
            int bracket = name.IndexOf(" (", System.StringComparison.Ordinal);
            return bracket > 0 ? name.Substring(0, bracket) : name;
        }

        private static Material KitMaterial(string kitName) =>
            AssetDatabase.LoadAssetAtPath<Material>($"{MedievalVillageSetup.KitFolder}/Materials/{kitName}.mat");

        /// <summary>A theme's copy of one of the kit's materials (the kit's own for the Original theme, or none).</summary>
        private Material MaterialFor(Theme theme, string kitName)
        {
            Material kit = KitMaterial(kitName);
            if (theme == null || theme.original || kit == null) return kit;
            string folder = $"{ThemeMaterials}/{Safe(theme.name)}";
            string path = $"{folder}/{kitName} ({theme.name}).mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                EnsureFolder(folder);
                material = new Material(kit) { name = $"{kitName} ({theme.name})" };
                AssetDatabase.CreateAsset(material, path);
            }
            Paint(material, kit, theme, kitName);
            return material;
        }

        // The kit material's settings, with its texture's grey copy tinted the theme's colour
        private static void Paint(Material material, Material kit, Theme theme, string kitName)
        {
            (Surface surface, string texture, float shade) = Tinted[kitName];
            Texture2D grey = Grey(texture);
            if (grey == null) return;
            material.CopyPropertiesFromMaterial(kit);
            Color colour = surface == Surface.Roof ? theme.roof : surface == Surface.Walls ? theme.walls : surface == Surface.Wood ? theme.wood : theme.stone;
            // (The grey copy averages half brightness, so twice the colour brings it back to the colour on average)
            Color tint = colour * (2f * shade);
            tint.a = 1f;
            material.SetTexture("_BaseMap", grey);
            material.SetColor("_BaseColor", tint);
            material.enableInstancing = true;
            EditorUtility.SetDirty(material);
        }

        /// <summary>After a theme's colours change: its materials made again (houses using them change straight away).</summary>
        internal void Refresh(Theme theme)
        {
            if (theme == null || theme.original) return;
            foreach (string kitName in Tinted.Keys) MaterialFor(theme, kitName);
        }

        /// <summary>A theme renamed or removed: its materials go (houses wearing it keep them until re-themed).</summary>
        internal static void ForgetMaterials(string themeName)
        {
            string folder = $"{ThemeMaterials}/{Safe(themeName)}";
            if (AssetDatabase.IsValidFolder(folder)) AssetDatabase.DeleteAsset(folder);
        }

        private static string Safe(string name)
        {
            foreach (char c in Path.GetInvalidFileNameChars()) name = name.Replace(c, '_');
            return string.IsNullOrWhiteSpace(name) ? "Theme" : name.Trim();
        }

        private static void EnsureFolder(string folder)
        {
            if (AssetDatabase.IsValidFolder(folder)) return;
            string parent = Path.GetDirectoryName(folder).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(folder));
        }

        // ---------------------------------------------------------------- Grey copies of the kit's textures

        // A grey copy of one of the kit's colour textures: its light and dark kept, its colour taken out, scaled so it
        // averages half brightness (in linear light), so a tint shows as that colour on average
        private static Texture2D Grey(string texture)
        {
            string path = $"{GreyFolder}/{texture}_Grey.png";
            var grey = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (grey != null) return grey;
            var source = AssetDatabase.LoadAssetAtPath<Texture2D>($"{MedievalVillageSetup.KitFolder}/Textures/{texture}.png");
            if (source == null) return null;

            int width = source.width, height = source.height;
            RenderTexture rt = RenderTexture.GetTemporary(width, height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            Graphics.Blit(source, rt);
            RenderTexture before = RenderTexture.active;
            RenderTexture.active = rt;
            var read = new Texture2D(width, height, TextureFormat.RGBA32, false, false);
            read.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            read.Apply();
            RenderTexture.active = before;
            RenderTexture.ReleaseTemporary(rt);

            Color32[] pixels = read.GetPixels32();
            var light = new float[pixels.Length];
            double sum = 0;
            for (int i = 0; i < pixels.Length; i++)
            {
                Color c = ((Color)pixels[i]).linear;
                light[i] = 0.2126f * c.r + 0.7152f * c.g + 0.0722f * c.b;
                sum += light[i];
            }
            float mean = Mathf.Max((float)(sum / pixels.Length), 0.001f);
            for (int i = 0; i < pixels.Length; i++)
            {
                float v = Mathf.LinearToGammaSpace(Mathf.Clamp01(light[i] / (2f * mean)));
                byte b = (byte)Mathf.RoundToInt(v * 255f);
                pixels[i] = new Color32(b, b, b, 255);
            }
            read.SetPixels32(pixels);
            read.Apply();

            EnsureFolder(GreyFolder);
            File.WriteAllBytes(path, read.EncodeToPNG());
            Object.DestroyImmediate(read);
            AssetDatabase.ImportAsset(path);
            if (AssetImporter.GetAtPath(path) is TextureImporter importer)
            {
                importer.maxTextureSize = 2048;
                importer.mipmapEnabled = true;
                importer.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }
    }
}
#endif
