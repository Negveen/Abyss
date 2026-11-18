#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace YaraJ.CastleVillageBuilder
{
    // The Details tab's props: barrels, sacks, hay, market stalls, wells, lamps, braziers, weapon racks... (each made once,
    // as a prefab, in a few variants) and fences drawn along a line
    internal static partial class CastleFeatures
    {
        internal const string DetailsContainer = "Village Details";

        internal enum Detail
        {
            Barrel, BarrelStack, Sacks, HayBale, Crates, Wagon, MarketStall, TableAndStools, Woodpile, Well, Signpost,
            LampPost, Brazier, BannerPole, Bench, Planter, Cypress, WeaponRack, ArcheryTarget, LooseBricks
        }

        internal enum Fence { Wooden, Picket, LowStoneWall, Hedge, IronRailing }

        internal sealed class DetailInfo
        {
            public string Name, Tip, Group;
            public float Radius;           // the ground it takes (m), kept clear of others
            public int Variants = 1;       // how many different ones are made
            public float? LightAt;         // a lantern or fire this high up (for a real light)
            public string[] Kit;           // or the kit's own models
        }

        internal static readonly DetailInfo[] Details =
        {
            new DetailInfo { Name = "Barrel", Group = "Village", Radius = 0.45f, Variants = 3, Tip = "A wooden barrel with iron hoops." },
            new DetailInfo { Name = "Barrel Stack", Group = "Village", Radius = 0.9f, Variants = 3, Tip = "A few barrels together, one on top." },
            new DetailInfo { Name = "Sacks", Group = "Village", Radius = 0.7f, Variants = 3, Tip = "A pile of grain sacks." },
            new DetailInfo { Name = "Hay Bale", Group = "Village", Radius = 0.7f, Variants = 3, Tip = "Hay bales, one or stacked." },
            new DetailInfo { Name = "Crates", Group = "Village", Radius = 0.8f, Kit = new[] { "Prop_Crate" }, Tip = "The kit's crates, one or stacked." },
            new DetailInfo { Name = "Wagon", Group = "Village", Radius = 2.2f, Kit = new[] { "Prop_Wagon" }, Tip = "The kit's wagon." },
            new DetailInfo { Name = "Market Stall", Group = "Village", Radius = 1.8f, Variants = 4, Tip = "A stall with a striped canopy and produce on its table." },
            new DetailInfo { Name = "Table & Stools", Group = "Village", Radius = 1.3f, Variants = 3, Tip = "A tavern table with stools round it." },
            new DetailInfo { Name = "Woodpile", Group = "Village", Radius = 1f, Variants = 2, Tip = "Stacked logs between stakes." },
            new DetailInfo { Name = "Well", Group = "Village", Radius = 1.3f, Tip = "A stone well with a roof, crank and bucket." },
            new DetailInfo { Name = "Signpost", Group = "Village", Radius = 0.6f, Variants = 3, Tip = "A post with pointing boards." },
            new DetailInfo { Name = "Lamp Post", Group = "Lights & Decor", Radius = 0.5f, LightAt = 3.65f, Tip = "An iron lamp post with a lantern." },
            new DetailInfo { Name = "Brazier", Group = "Lights & Decor", Radius = 0.6f, LightAt = 1.3f, Tip = "An iron fire bowl of glowing coals." },
            new DetailInfo { Name = "Banner Pole", Group = "Lights & Decor", Radius = 0.6f, Tip = "A banner in the castle's colours (Castle Walls > Details)." },
            new DetailInfo { Name = "Bench", Group = "Lights & Decor", Radius = 0.9f, Tip = "A wooden bench on stone legs." },
            new DetailInfo { Name = "Planter", Group = "Lights & Decor", Radius = 0.7f, Tip = "A stone planter with a clipped shrub." },
            new DetailInfo { Name = "Cypress", Group = "Lights & Decor", Radius = 0.7f, Variants = 3, Tip = "A tall, slim tree." },
            new DetailInfo { Name = "Weapon Rack", Group = "Castle", Radius = 0.9f, Variants = 2, Tip = "A rack of spears." },
            new DetailInfo { Name = "Archery Target", Group = "Castle", Radius = 0.8f, Tip = "A straw target on a stand." },
            new DetailInfo { Name = "Loose Bricks", Group = "Castle", Radius = 0.6f, Kit = new[] { "Prop_Brick1", "Prop_Brick2", "Prop_Brick3", "Prop_Brick4" }, Tip = "The kit's loose bricks." },
        };

        internal static readonly string[] FenceNames = { "Wooden Fence", "Picket Fence", "Low Stone Wall", "Hedge Row", "Iron Railing" };

        private static string DetailFolder => MedievalVillageSetup.KitFolder + "/Generated/Details";

        /// <summary>One of a detail's prefabs (made the first time it's wanted), or the kit's model for kit ones.</summary>
        internal static GameObject DetailPrefab(Detail kind, int variant)
        {
            DetailInfo info = Details[(int)kind];
            if (info.Kit != null)
                return AssetDatabase.LoadAssetAtPath<GameObject>($"{MedievalVillageSetup.KitFolder}/Prefabs/{info.Kit[variant % info.Kit.Length]}.prefab");
            variant %= Mathf.Max(1, info.Variants);
            string path = $"{DetailFolder}/{info.Name} {variant + 1}.prefab";
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab != null) return prefab;

            if (!AssetDatabase.IsValidFolder(MedievalVillageSetup.KitFolder + "/Generated")) AssetDatabase.CreateFolder(MedievalVillageSetup.KitFolder, "Generated");
            if (!AssetDatabase.IsValidFolder(DetailFolder)) AssetDatabase.CreateFolder(MedievalVillageSetup.KitFolder + "/Generated", "Details");
            var m = new Mesher(Vector3.zero);
            BuildDetail(kind, m, new System.Random(1000 + (int)kind * 17 + variant * 131));
            GameObject go = m.Build(info.Name, null);
            if (go == null) return null;
            SimpleCollider(go);
            Mesh mesh = go.GetComponent<MeshFilter>().sharedMesh;
            AssetDatabase.CreateAsset(mesh, $"{DetailFolder}/{info.Name} {variant + 1}.asset");
            prefab = PrefabUtility.SaveAsPrefabAsset(go, path);
            Object.DestroyImmediate(go);
            return prefab;
        }

        // A box for it to bump into, rather than its every bar and baluster (far cheaper for physics)
        private static void SimpleCollider(GameObject go)
        {
            var mc = go.GetComponent<MeshCollider>();
            if (mc != null) Object.DestroyImmediate(mc, true);
            if (go.GetComponent<BoxCollider>() == null) go.AddComponent<BoxCollider>();
        }

        /// <summary>The props already made (from before they were optimized): a box collider each, saved.</summary>
        internal static int RefreshDetailPrefabs()
        {
            if (!AssetDatabase.IsValidFolder(DetailFolder)) return 0;
            int done = 0;
            foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { DetailFolder }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                GameObject contents = PrefabUtility.LoadPrefabContents(path);
                if (contents.GetComponent<MeshCollider>() != null)
                {
                    SimpleCollider(contents);
                    PrefabUtility.SaveAsPrefabAsset(contents, path);
                    done++;
                }
                PrefabUtility.UnloadPrefabContents(contents);
            }
            return done;
        }

        /// <summary>A kind's variants (or kit models).</summary>
        internal static int VariantCount(Detail kind)
        {
            DetailInfo info = Details[(int)kind];
            return info.Kit != null ? info.Kit.Length : Mathf.Max(1, info.Variants);
        }

        // ---------------------------------------------------------------- The props (built round their foot, facing +z)

        private static Material Cloth(string name, Color c) => CastleWallBuilder.DetailMaterial(name, c, 0f, 0.08f);

        private static void BuildDetail(Detail kind, Mesher m, System.Random rnd)
        {
            float R(float a, float b) => a + (float)rnd.NextDouble() * (b - a);
            switch (kind)
            {
                case Detail.Barrel: Barrel(m, Vector3.zero, R(0.85f, 1.05f)); break;
                case Detail.BarrelStack:
                {
                    float h = 0.95f;
                    var spots = new[] { new Vector3(-0.38f, 0f, -0.22f), new Vector3(0.38f, 0f, -0.22f), new Vector3(0f, 0f, 0.42f) };
                    foreach (Vector3 p in spots) Barrel(m, p + new Vector3(R(-0.05f, 0.05f), 0f, R(-0.05f, 0.05f)), h);
                    if (rnd.NextDouble() < 0.75) Barrel(m, new Vector3(0f, h, 0f), h * 0.92f);
                    break;
                }
                case Detail.Sacks:
                {
                    int n = 3 + rnd.Next(3);
                    for (int i = 0; i < n; i++)
                    {
                        float a = i * 2.4f + R(0f, 0.6f), r = i == 0 ? 0f : R(0.3f, 0.5f);
                        Sack(m, new Vector3(Mathf.Cos(a) * r, 0f, Mathf.Sin(a) * r), R(0.85f, 1.1f));
                    }
                    break;
                }
                case Detail.HayBale:
                {
                    int n = 1 + rnd.Next(3);
                    for (int i = 0; i < n; i++)
                    {
                        Vector3 at = i == 0 ? Vector3.zero : i == 1 ? new Vector3(0f, 0f, 0.6f) : new Vector3(0f, 0.6f, 0.3f);
                        HayBale(m, at, Quaternion.Euler(0f, R(-8f, 8f), 0f));
                    }
                    break;
                }
                case Detail.MarketStall: MarketStall(m, rnd); break;
                case Detail.TableAndStools: TableAndStools(m, rnd); break;
                case Detail.Woodpile: Woodpile(m, rnd); break;
                case Detail.Well: Well(m); break;
                case Detail.Signpost: Signpost(m, rnd); break;
                case Detail.LampPost: LampPost(m, Vector3.zero, 3.4f); break;
                case Detail.Brazier: Brazier(m); break;
                case Detail.BannerPole: BannerPole(m, Vector3.zero, Quaternion.identity, new Settings()); break;
                case Detail.Bench: Bench(m, Vector3.zero, Quaternion.identity); break;
                case Detail.Planter: Planter(m, Vector3.zero, Quaternion.identity); break;
                case Detail.Cypress: Cypress(m, Vector3.zero, R(3.2f, 4.6f)); break;
                case Detail.WeaponRack: WeaponRack(m, rnd); break;
                case Detail.ArcheryTarget: ArcheryTarget(m); break;
            }
        }

        private static void Barrel(Mesher m, Vector3 foot, float h)
        {
            Material hoop = m.Iron;
            float rb = 0.3f * h, rm = 0.36f * h;
            m.Cylinder(foot, rb, rm, foot.y, foot.y + h * 0.5f, 14, m.Wood, m.Wood, false);
            m.Cylinder(foot, rm, rb, foot.y + h * 0.5f, foot.y + h, 14, m.Wood, m.Wood, true);
            foreach (float t in new[] { 0.12f, 0.88f })
            {
                float r = Mathf.Lerp(rb, rm, 1f - Mathf.Abs(t - 0.5f) * 2f) + 0.012f;
                m.Cylinder(foot, r, r, foot.y + h * t - 0.03f, foot.y + h * t + 0.03f, 14, hoop, hoop, false);
            }
            m.Cylinder(foot, rm + 0.012f, rm + 0.012f, foot.y + h * 0.47f, foot.y + h * 0.53f, 14, hoop, hoop, false);
        }

        private static void Sack(Mesher m, Vector3 foot, float s)
        {
            Material sack = Cloth("Sack Cloth", new Color(0.62f, 0.52f, 0.36f));
            m.Cylinder(foot, 0.24f * s, 0.29f * s, foot.y, foot.y + 0.25f * s, 10, sack, sack, false);
            m.Cylinder(foot, 0.29f * s, 0.17f * s, foot.y + 0.25f * s, foot.y + 0.55f * s, 10, sack, sack, false);
            m.Cylinder(foot, 0.07f * s, 0.07f * s, foot.y + 0.55f * s, foot.y + 0.6f * s, 8, sack, sack, false);
            m.Cylinder(foot, 0.09f * s, 0.02f * s, foot.y + 0.6f * s, foot.y + 0.7f * s, 8, sack, sack, true);
        }

        private static void HayBale(Mesher m, Vector3 foot, Quaternion turn)
        {
            Material hay = Cloth("Hay", new Color(0.8f, 0.68f, 0.35f));
            Material twine = Cloth("Twine", new Color(0.4f, 0.3f, 0.18f));
            m.Block(foot + Vector3.up * 0.3f, new Vector3(1f, 0.6f, 0.55f), turn, hay, hay, false);
            foreach (float x in new[] { -0.25f, 0.25f })
                m.Block(foot + turn * new Vector3(x, 0.3f, 0f), new Vector3(0.04f, 0.62f, 0.57f), turn, twine, twine, false);
        }

        private static void MarketStall(Mesher m, System.Random rnd)
        {
            var colours = new[]
            {
                (new Color(0.7f, 0.12f, 0.1f), new Color(0.92f, 0.88f, 0.78f)), (new Color(0.15f, 0.3f, 0.62f), new Color(0.92f, 0.88f, 0.78f)),
                (new Color(0.2f, 0.45f, 0.22f), new Color(0.9f, 0.82f, 0.55f)), (new Color(0.85f, 0.62f, 0.12f), new Color(0.7f, 0.15f, 0.1f))
            };
            var (ca, cb) = colours[rnd.Next(colours.Length)];
            Material a = Cloth("Canopy " + ColorUtility.ToHtmlStringRGB(ca), ca), b = Cloth("Canopy " + ColorUtility.ToHtmlStringRGB(cb), cb);
            const float W = 1.3f, D = 0.8f;
            // Posts, the table and its front panel
            foreach (var (x, z, h) in new[] { (-W, D, 2.15f), (W, D, 2.15f), (-W, -D, 2.5f), (W, -D, 2.5f) })
                m.Block(new Vector3(x, h * 0.5f, z), new Vector3(0.1f, h, 0.1f), Quaternion.identity, m.Wood, m.Wood, false);
            m.Block(new Vector3(0f, 0.9f, 0.1f), new Vector3(W * 2f, 0.08f, 1.15f), Quaternion.identity, m.Wood, m.Wood, true);
            m.Block(new Vector3(0f, 0.45f, 0.66f), new Vector3(W * 2f, 0.85f, 0.05f), Quaternion.identity, m.Wood, m.Wood, true);
            // The striped canopy, sloping to the front, with a hanging edge
            const int Stripes = 7;
            for (int i = 0; i < Stripes; i++)
            {
                float x0 = -W - 0.15f + (W * 2f + 0.3f) * i / Stripes, x1 = -W - 0.15f + (W * 2f + 0.3f) * (i + 1) / Stripes;
                Material c = i % 2 == 0 ? a : b;
                Vector3 n = new Vector3(0f, 1f, 0.2f).normalized;
                m.Face(new Vector3(x0, 2.55f, -D - 0.15f), new Vector3(x1, 2.55f, -D - 0.15f), new Vector3(x1, 2.2f, D + 0.3f), new Vector3(x0, 2.2f, D + 0.3f), n, c);
                m.Face(new Vector3(x0, 2.55f, -D - 0.15f), new Vector3(x0, 2.2f, D + 0.3f), new Vector3(x1, 2.2f, D + 0.3f), new Vector3(x1, 2.55f, -D - 0.15f), -n, c);
                m.Block(new Vector3((x0 + x1) * 0.5f, 2.08f, D + 0.3f), new Vector3(x1 - x0, 0.24f, 0.02f), Quaternion.identity, c, c, true);
            }
            // Produce in trays on the table
            var produce = new[] { new Color(0.75f, 0.12f, 0.1f), new Color(0.35f, 0.6f, 0.15f), new Color(0.9f, 0.55f, 0.1f), new Color(0.55f, 0.35f, 0.65f), new Color(0.85f, 0.75f, 0.3f) };
            for (int i = 0; i < 3; i++)
            {
                float x = -0.8f + i * 0.8f;
                Color pc = produce[rnd.Next(produce.Length)];
                Material fill = Cloth("Produce " + ColorUtility.ToHtmlStringRGB(pc), pc);
                m.Block(new Vector3(x, 1.0f, 0.15f), new Vector3(0.65f, 0.12f, 0.5f), Quaternion.identity, m.Wood, m.Wood, false);
                m.Block(new Vector3(x, 1.08f, 0.15f), new Vector3(0.58f, 0.06f, 0.44f), Quaternion.identity, fill, fill, false);
            }
            Barrel(m, new Vector3(W + 0.45f, 0f, 0.3f), 0.8f);
            Sack(m, new Vector3(-W - 0.4f, 0f, 0.35f), 0.9f);
        }

        private static void TableAndStools(Mesher m, System.Random rnd)
        {
            m.Block(new Vector3(0f, 0.75f, 0f), new Vector3(1.5f, 0.07f, 0.85f), Quaternion.identity, m.Wood, m.Wood, true);
            foreach (var (x, z) in new[] { (-0.65f, -0.33f), (0.65f, -0.33f), (-0.65f, 0.33f), (0.65f, 0.33f) })
                m.Block(new Vector3(x, 0.36f, z), new Vector3(0.08f, 0.72f, 0.08f), Quaternion.identity, m.Wood, m.Wood, false);
            int stools = 2 + rnd.Next(3);
            var spots = new[] { new Vector3(-0.45f, 0f, 0.75f), new Vector3(0.45f, 0f, -0.75f), new Vector3(0.45f, 0f, 0.75f), new Vector3(-0.45f, 0f, -0.75f), new Vector3(1.05f, 0f, 0f) };
            for (int i = 0; i < stools; i++)
            {
                Vector3 p = spots[i] + new Vector3((float)rnd.NextDouble() * 0.1f, 0f, (float)rnd.NextDouble() * 0.1f);
                m.Cylinder(p, 0.19f, 0.19f, 0.4f, 0.46f, 10, m.Wood, m.Wood, true);
                for (int k = 0; k < 3; k++)
                {
                    Vector3 d = Quaternion.Euler(0f, k * 120f, 0f) * Vector3.forward;
                    m.Tube(p + d * 0.16f, p + d * 0.12f + Vector3.up * 0.41f, 0.025f, 5, m.Wood, null);
                }
            }
            // A jug and mugs
            m.Cylinder(new Vector3(0.2f, 0f, 0.1f), 0.08f, 0.06f, 0.785f, 1.0f, 8, m.Trim, m.Trim, true);
            m.Cylinder(new Vector3(-0.3f, 0f, -0.15f), 0.045f, 0.045f, 0.785f, 0.9f, 8, m.Wood, m.Wood, true);
        }

        private static void Woodpile(Mesher m, System.Random rnd)
        {
            Material bark = Cloth("Bark", new Color(0.3f, 0.22f, 0.15f));
            Material ends = Cloth("Log Ends", new Color(0.66f, 0.5f, 0.32f));
            int rows = 3 + rnd.Next(2);
            const float L = 1.5f;
            for (int row = 0; row < rows; row++)
            {
                int count = 5 - row;
                for (int i = 0; i < count; i++)
                {
                    float r = 0.1f + (float)rnd.NextDouble() * 0.03f;
                    float z = (i - (count - 1) * 0.5f) * 0.22f, y = 0.11f + row * 0.19f;
                    float shift = (float)rnd.NextDouble() * 0.12f - 0.06f;
                    m.Tube(new Vector3(-L * 0.5f + shift, y, z), new Vector3(L * 0.5f + shift, y, z), r, 7, bark, ends);
                }
            }
            foreach (float z in new[] { -0.62f, 0.62f })
                foreach (float x in new[] { -0.45f, 0.45f })
                    m.Block(new Vector3(x, 0.5f, z), new Vector3(0.07f, 1f, 0.07f), Quaternion.identity, m.Wood, m.Wood, false);
        }

        private static void Well(Mesher m)
        {
            Vector3 c = Vector3.zero;
            m.Cylinder(c, 0.85f, 0.85f, 0f, 0.8f, 24, m.Trim, m.Stone, false);
            m.Cylinder(c, 0.62f, 0.62f, 0.8f, 0.1f, 24, m.Stone, m.Stone, false, true);
            m.Ring(new Vector3(0f, 0.8f, 0f), 0.62f, 0.88f, 24, m.Trim);
            m.Disc(new Vector3(0f, 0.35f, 0f), 0.62f, 24, m.Water);
            foreach (float x in new[] { -0.78f, 0.78f })
                m.Block(new Vector3(x, 1.5f, 0f), new Vector3(0.12f, 1.4f, 0.12f), Quaternion.identity, m.Wood, m.Wood, false);
            // A little tiled roof
            Material tiles = AssetDatabase.LoadAssetAtPath<Material>(MedievalVillageSetup.KitFolder + "/Materials/MI_RoundTiles.mat") ?? m.Wood;
            foreach (int s in new[] { -1, 1 })
            {
                Vector3 n = new Vector3(0f, 1f, s * 1.1f).normalized;
                m.Face(new Vector3(-1.05f, 2.65f, 0f), new Vector3(1.05f, 2.65f, 0f), new Vector3(1.05f, 2.15f, s * 0.85f), new Vector3(-1.05f, 2.15f, s * 0.85f), n, tiles);
                m.Face(new Vector3(-1.05f, 2.6f, 0f), new Vector3(-1.05f, 2.1f, s * 0.85f), new Vector3(1.05f, 2.1f, s * 0.85f), new Vector3(1.05f, 2.6f, 0f), -n, m.Wood);
            }
            m.Block(new Vector3(0f, 2.2f, 0f), new Vector3(1.75f, 0.1f, 0.1f), Quaternion.identity, m.Wood, m.Wood, true);
            // The crank, rope and bucket
            m.Tube(new Vector3(-0.85f, 1.85f, 0f), new Vector3(0.95f, 1.85f, 0f), 0.06f, 8, m.Wood, m.Wood);
            m.Tube(new Vector3(0.95f, 1.85f, 0f), new Vector3(0.95f, 1.6f, 0.15f), 0.025f, 6, m.Iron, null);
            m.Tube(new Vector3(0f, 1.82f, 0f), new Vector3(0f, 1.32f, 0f), 0.015f, 5, Cloth("Twine", new Color(0.4f, 0.3f, 0.18f)), null);
            m.Cylinder(new Vector3(0f, 0f, 0f), 0.13f, 0.16f, 1.05f, 1.32f, 10, m.Wood, m.Wood, false);
            m.Cylinder(new Vector3(0f, 0f, 0f), 0.165f, 0.165f, 1.2f, 1.24f, 10, m.Iron, m.Iron, false);
        }

        private static void Signpost(Mesher m, System.Random rnd)
        {
            m.Block(new Vector3(0f, 1.15f, 0f), new Vector3(0.12f, 2.3f, 0.12f), Quaternion.identity, m.Wood, m.Wood, false);
            m.Cylinder(Vector3.zero, 0.1f, 0.01f, 2.3f, 2.45f, 4, m.Wood, m.Wood, true, false, 45f);
            int boards = 2 + rnd.Next(2);
            for (int i = 0; i < boards; i++)
            {
                float yaw = (float)rnd.NextDouble() * 360f;
                Quaternion t = Quaternion.Euler(0f, yaw, 0f);
                float y = 2.0f - i * 0.3f;
                m.Block(t * new Vector3(0.45f, 0f, 0.08f) + Vector3.up * y, new Vector3(0.85f, 0.2f, 0.04f), t, m.Wood, m.Wood, true);
                // (its pointed end)
                Vector3 tip = t * new Vector3(0.98f, 0f, 0.08f) + Vector3.up * y, top = t * new Vector3(0.875f, 0.1f, 0.08f) + Vector3.up * y, bottom = t * new Vector3(0.875f, -0.1f, 0.08f) + Vector3.up * y;
                Vector3 n = t * Vector3.forward;
                m.Triangle(top + n * 0.02f, tip + n * 0.02f, bottom + n * 0.02f, n, m.Wood);
                m.Triangle(top - n * 0.02f, bottom - n * 0.02f, tip - n * 0.02f, -n, m.Wood);
            }
        }

        private static void Brazier(Mesher m)
        {
            Material coals = CastleWallBuilder.DetailMaterial("Brazier Coals", new Color(0.9f, 0.35f, 0.08f), 0f, 0.3f, new Color(1f, 0.38f, 0.08f) * 3.5f);
            for (int k = 0; k < 3; k++)
            {
                Vector3 d = Quaternion.Euler(0f, k * 120f, 0f) * Vector3.forward;
                m.Tube(d * 0.42f, d * 0.25f + Vector3.up * 0.8f, 0.03f, 6, m.Iron, m.Iron);
            }
            m.Cylinder(Vector3.zero, 0.2f, 0.45f, 0.75f, 1.0f, 16, m.Iron, m.Iron, false);
            m.Cylinder(Vector3.zero, 0.44f, 0.2f, 1.0f, 0.78f, 16, m.Iron, m.Iron, false, true);
            m.Disc(new Vector3(0f, 0.93f, 0f), 0.4f, 16, coals);
            for (int i = 0; i < 6; i++)
            {
                Vector3 p = Quaternion.Euler(0f, i * 60f + 20f, 0f) * Vector3.forward * (i % 2 == 0 ? 0.18f : 0.08f);
                m.Block(p + Vector3.up * 0.98f, new Vector3(0.1f, 0.08f, 0.1f), Quaternion.Euler(10f * i, i * 37f, 0f), coals, coals, false);
            }
        }

        private static void WeaponRack(Mesher m, System.Random rnd)
        {
            foreach (float x in new[] { -0.7f, 0.7f })
                m.Block(new Vector3(x, 0.65f, 0f), new Vector3(0.09f, 1.3f, 0.09f), Quaternion.identity, m.Wood, m.Wood, false);
            m.Tube(new Vector3(-0.75f, 1.25f, -0.06f), new Vector3(0.75f, 1.25f, -0.06f), 0.04f, 8, m.Wood, m.Wood);
            m.Block(new Vector3(0f, 0.3f, 0.05f), new Vector3(1.5f, 0.06f, 0.25f), Quaternion.identity, m.Wood, m.Wood, true);
            int spears = 4 + rnd.Next(3);
            for (int i = 0; i < spears; i++)
            {
                float x = -0.55f + i * 1.1f / (spears - 1);
                Vector3 foot = new Vector3(x, 0.33f, 0.08f), top = new Vector3(x + (float)rnd.NextDouble() * 0.06f, 2.1f, -0.12f);
                m.Tube(foot, top, 0.022f, 6, m.Wood, m.Wood);
                Vector3 dir = (top - foot).normalized;
                m.Tube(top, top + dir * 0.22f, 0.04f, 4, m.Iron, m.Iron);
            }
        }

        private static void ArcheryTarget(Mesher m)
        {
            Material straw = Cloth("Hay", new Color(0.8f, 0.68f, 0.35f));
            Material red = Cloth("Target Red", new Color(0.7f, 0.12f, 0.1f)), white = Cloth("Target White", new Color(0.92f, 0.9f, 0.84f)), gold = Cloth("Target Gold", new Color(0.92f, 0.72f, 0.15f));
            Vector3 centre = new Vector3(0f, 1.25f, 0f);
            Vector3 n = Quaternion.Euler(-12f, 0f, 0f) * Vector3.forward;
            // Legs: two in front, one behind
            m.Tube(new Vector3(-0.5f, 0f, 0.25f), centre + new Vector3(-0.3f, 0.3f, -0.05f), 0.035f, 6, m.Wood, m.Wood);
            m.Tube(new Vector3(0.5f, 0f, 0.25f), centre + new Vector3(0.3f, 0.3f, -0.05f), 0.035f, 6, m.Wood, m.Wood);
            m.Tube(new Vector3(0f, 0f, -0.75f), centre + new Vector3(0f, 0.4f, -0.15f), 0.035f, 6, m.Wood, m.Wood);
            // The straw boss (a thick disc) and its painted rings
            m.UprightDisc(centre - n * 0.1f, -n, 0.62f, 28, straw);
            const int Sides = 28;
            Vector3 u = Vector3.Cross(n, Vector3.up).normalized, v = Vector3.Cross(u, n);
            for (int i = 0; i < Sides; i++)
            {
                float a0 = i * 2f * Mathf.PI / Sides, a1 = (i + 1) * 2f * Mathf.PI / Sides;
                Vector3 d0 = u * Mathf.Cos(a0) + v * Mathf.Sin(a0), d1 = u * Mathf.Cos(a1) + v * Mathf.Sin(a1);
                m.Face(centre + d0 * 0.62f, centre + d1 * 0.62f, centre - n * 0.1f + d1 * 0.62f, centre - n * 0.1f + d0 * 0.62f, d0, d1, d1, d0, straw);
            }
            m.UprightDisc(centre, n, 0.62f, Sides, straw);
            m.UprightDisc(centre + n * 0.004f, n, 0.5f, Sides, white);
            m.UprightDisc(centre + n * 0.008f, n, 0.38f, Sides, red);
            m.UprightDisc(centre + n * 0.012f, n, 0.24f, Sides, white);
            m.UprightDisc(centre + n * 0.016f, n, 0.12f, Sides, gold);
        }

        // ---------------------------------------------------------------- Fences (along a drawn line, in spans)

        /// <summary>A fence along a line: one object per span between posts (so rubbing out takes spans), under 'parent'.</summary>
        internal static List<GameObject> BuildFence(List<Vector3> line, Fence kind, SurfaceAt ground, Transform parent, string theme)
        {
            var made = new List<GameObject>();
            if (line == null || line.Count < 2) return made;
            float span = kind == Fence.Hedge || kind == Fence.LowStoneWall ? 2.5f : 2f;
            // Posts evenly along the line, on the ground
            var posts = new List<Vector3>();
            float total = 0f;
            for (int i = 1; i < line.Count; i++) total += Flat(line[i] - line[i - 1]).magnitude;
            if (total < 0.5f) return made;
            int count = Mathf.Max(1, Mathf.RoundToInt(total / span));
            for (int k = 0; k <= count; k++)
            {
                float d = total * k / count;
                Vector3 p = line[line.Count - 1];
                for (int i = 1; i < line.Count; i++)
                {
                    float seg = Flat(line[i] - line[i - 1]).magnitude;
                    if (d <= seg || i == line.Count - 1) { p = Vector3.Lerp(line[i - 1], line[i], seg > 0f ? Mathf.Clamp01(d / seg) : 0f); break; }
                    d -= seg;
                }
                p.y = ground(p) ?? p.y;
                posts.Add(p);
            }
            for (int k = 0; k < posts.Count - 1; k++)
            {
                Vector3 a = posts[k], b = posts[k + 1];
                var m = new Mesher(Flat((a + b) * 0.5f));
                FenceSpan(m, kind, a, b, k == 0, ground);
                GameObject go = m.Build(FenceNames[(int)kind], parent);
                if (go == null) continue;
                SimpleCollider(go);
                MeshMerger.OptimizeProp(go, false);
                CastleWallBuilder.Finish(go, new CastleWallBuilder.Settings { Theme = theme });
                made.Add(go);
            }
            return made;
        }

        private static void FenceSpan(Mesher m, Fence kind, Vector3 a, Vector3 b, bool first, SurfaceAt ground)
        {
            Vector3 run = b - a;
            Vector3 dir = run.normalized, flat = Flat(run).normalized;
            Quaternion along = Quaternion.LookRotation(dir), upright = Quaternion.LookRotation(flat);
            float length = run.magnitude;
            switch (kind)
            {
                case Fence.Wooden:
                case Fence.Picket:
                {
                    Material wood = kind == Fence.Picket ? CastleWallBuilder.DetailMaterial("Painted Wood", new Color(0.88f, 0.86f, 0.8f), 0f, 0.2f) : m.Wood;
                    float h = kind == Fence.Picket ? 0.95f : 1.1f;
                    if (first) m.Block(a + Vector3.up * (h * 0.5f - 0.1f), new Vector3(0.12f, h + 0.2f, 0.12f), upright, wood, wood, false);
                    m.Block(b + Vector3.up * (h * 0.5f - 0.1f), new Vector3(0.12f, h + 0.2f, 0.12f), upright, wood, wood, false);
                    foreach (float y in kind == Fence.Picket ? new[] { 0.25f, 0.7f } : new[] { 0.45f, 0.9f })
                        m.Block((a + b) * 0.5f + Vector3.up * y, new Vector3(0.05f, 0.1f, length), along, wood, wood, true);
                    if (kind == Fence.Picket)
                    {
                        int pickets = Mathf.FloorToInt(length / 0.16f);
                        for (int i = 1; i < pickets; i++)
                        {
                            Vector3 p = Vector3.Lerp(a, b, (float)i / pickets) + Flat(Vector3.Cross(Vector3.up, flat)) * 0.04f;
                            m.Block(p + Vector3.up * 0.45f, new Vector3(0.08f, 0.9f, 0.025f), Quaternion.LookRotation(Vector3.Cross(Vector3.up, flat)), wood, wood, false);
                            m.Cylinder(p, 0.055f, 0.005f, p.y + 0.9f, p.y + 0.98f, 4, wood, wood, true, false, 45f + Vector3.SignedAngle(Vector3.forward, flat, Vector3.up));
                        }
                    }
                    break;
                }
                case Fence.LowStoneWall:
                    m.SlopedWall(Flat(a), Flat(b), a.y + 0.85f, b.y + 0.85f, Mathf.Min(a.y, b.y) - 0.4f, 0.5f, m.Stone, m.Trim);
                    break;
                case Fence.Hedge:
                    m.Block((a + b) * 0.5f + Vector3.up * 0.55f, new Vector3(0.85f, 1.1f, length + 0.4f), upright, m.Hedge, m.Hedge, true);
                    break;
                case Fence.IronRailing:
                {
                    if (first) m.Block(a + Vector3.up * 0.55f, new Vector3(0.1f, 1.1f, 0.1f), upright, m.Iron, m.Iron, false);
                    m.Block(b + Vector3.up * 0.55f, new Vector3(0.1f, 1.1f, 0.1f), upright, m.Iron, m.Iron, false);
                    m.Block((a + b) * 0.5f + Vector3.up * 1.0f, new Vector3(0.05f, 0.05f, length), along, m.Iron, m.Iron, true);
                    m.Block((a + b) * 0.5f + Vector3.up * 0.15f, new Vector3(0.05f, 0.05f, length), along, m.Iron, m.Iron, true);
                    int bars = Mathf.FloorToInt(length / 0.14f);
                    for (int i = 1; i < bars; i++)
                    {
                        Vector3 p = Vector3.Lerp(a, b, (float)i / bars);
                        m.Tube(p, p + Vector3.up * 1.12f, 0.012f, 4, m.Iron, null);
                        m.Cylinder(p, 0.03f, 0.002f, p.y + 1.1f, p.y + 1.2f, 4, m.Iron, m.Iron, true);
                    }
                    break;
                }
            }
        }
    }
}
#endif
