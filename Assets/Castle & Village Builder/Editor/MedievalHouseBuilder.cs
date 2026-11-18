#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace YaraJ.CastleVillageBuilder
{
    /// <summary>
    /// Editor only. Puts whole houses together from the Medieval Village kit's modules (Medieval Village Kit):
    /// stone, timber or plaster walls on a 2 m grid, windows and shutters, a front door, wooden floors, a stone footing
    /// under the ground floor (for sloping ground), a round-tiled roof with its gable ends, and maybe a chimney. Used by
    /// Tools > Castle & Village Builder.
    /// </summary>
    internal static class MedievalHouseBuilder
    {
        internal enum Style { Stone, Timber, Plaster }

        /// <summary>What one house looks like (all the random choices made, so it can be made again).</summary>
        internal sealed class Design
        {
            public int Width = 3, Length = 4, Storeys = 1; // 2 m cells across (the roof's span) and along (its ridge)
            public Style Style;
            public bool RoundOpenings;                      // round-topped door and windows, or flat
            public int WindowShape;                         // 0 wide flat, 1 wide round, 2 thin round
            public int Door = 1;                            // which of the kit's doors (1, 2, 4 or 8)
            public bool Chimney, ChimneyStone;
            public int Seed;
            public float WindowChance = 0.55f, ShutterChance = 0.4f;
            public bool Footing = true;
            public bool HideInterior = true;              // no rooms inside: a dark box behind the walls (windows and doors look dark, not empty)
            public string Theme;                            // its colour theme (BuildingThemes)

            public Vector2 Footprint => new Vector2(Width * Cell + 2f * Overhang, Length * Cell + 2f * Overhang);
            /// <summary>Where the front door is, from the middle of the house at ground-floor level (its +x side).</summary>
            public Vector3 DoorSpot => new Vector3(Width * Cell * 0.5f, 0f, DoorZ(Length));
        }

        /// <summary>What can be rolled.</summary>
        internal sealed class Ranges
        {
            public Vector2Int Width = new Vector2Int(2, 4), Length = new Vector2Int(2, 6), Storeys = new Vector2Int(1, 2);
            public bool Stone = true, Timber = true, Plaster = true;
            public float WindowChance = 0.55f, ShutterChance = 0.4f, ChimneyChance = 0.6f;
            public bool Footing = true;
            public bool HideInterior = true;
        }

        internal const float Cell = 2f;      // the kit's grid
        internal const float Storey = 3f;    // how tall each floor is
        private const float Overhang = 0.8f; // how far the roof reaches past the walls

        // The roofs the kit has, by span (in cells): which lengths come with it
        private static readonly Dictionary<int, int[]> RoofLengths = new Dictionary<int, int[]>
        {
            [2] = new[] { 2, 3, 4 },
            [3] = new[] { 3, 4, 5, 6, 7 },
            [4] = new[] { 4, 5, 6, 7 },
        };

        // How far along the front the door is (it's in the middle cell)
        private static float DoorZ(int length) => -length * Cell * 0.5f + Cell * (length / 2 + 0.5f);

        internal static Design Roll(Ranges ranges, int seed)
        {
            var random = new System.Random(seed);
            float Chance() => (float)random.NextDouble();
            int Between(Vector2Int range) => random.Next(Mathf.Min(range.x, range.y), Mathf.Max(range.x, range.y) + 1);

            var design = new Design { Seed = seed };
            design.Width = Mathf.Clamp(Between(ranges.Width), 2, 4);
            int[] lengths = RoofLengths[design.Width];
            var fits = new List<int>();
            foreach (int l in lengths) if (l >= Mathf.Min(ranges.Length.x, ranges.Length.y) && l <= Mathf.Max(ranges.Length.x, ranges.Length.y)) fits.Add(l);
            if (fits.Count == 0)
            {
                // None in range: the nearest the kit has
                int wanted = Between(ranges.Length), best = lengths[0];
                foreach (int l in lengths) if (Mathf.Abs(l - wanted) < Mathf.Abs(best - wanted)) best = l;
                fits.Add(best);
            }
            design.Length = fits[random.Next(fits.Count)];
            design.Storeys = Mathf.Clamp(Between(ranges.Storeys), 1, 3);

            var styles = new List<Style>();
            if (ranges.Stone) styles.Add(Style.Stone);
            if (ranges.Timber) styles.Add(Style.Timber);
            if (ranges.Plaster) styles.Add(Style.Plaster);
            if (styles.Count == 0) styles.Add(Style.Timber);
            design.Style = styles[random.Next(styles.Count)];

            design.RoundOpenings = Chance() < 0.5f;
            design.WindowShape = random.Next(3);
            design.Door = new[] { 1, 2, 4, 8 }[random.Next(4)];
            design.Chimney = Chance() < ranges.ChimneyChance;
            design.ChimneyStone = Chance() < 0.5f;
            design.WindowChance = ranges.WindowChance;
            design.ShutterChance = ranges.ShutterChance;
            design.Footing = ranges.Footing;
            design.HideInterior = ranges.HideInterior;
            design.Theme = BuildingThemes.Get().Pick(random)?.name;
            return design;
        }

        // ---------------------------------------------------------------- The kit's pieces, measured

        // How the kit's pieces are laid out in their own space, measured from them (rather than assumed): which way a
        // wall runs and which way its outside faces, which way a roof's ridge runs, and where the door sits on its hinge
        private sealed class Kit
        {
            public readonly Dictionary<string, GameObject> Pieces = new Dictionary<string, GameObject>();
            public Vector3 WallAlong, WallOut, RoofRidge;
            public Quaternion WallTurn, RoofTurn; // turn a piece so a wall faces +z (outwards) and a roof's ridge runs along z

            public GameObject Get(string name)
            {
                if (Pieces.TryGetValue(name, out GameObject piece)) return piece;
                piece = AssetDatabase.LoadAssetAtPath<GameObject>($"{MedievalVillageSetup.ModelsFolder}/{name}.fbx");
                Pieces[name] = piece;
                return piece;
            }
        }

        private static Kit kit;

        private static Kit Pieces()
        {
            if (kit != null && kit.Get("Wall_Plaster_Straight") != null) return kit;
            kit = new Kit();
            GameObject wall = kit.Get("Wall_Plaster_Straight");
            GameObject inset = kit.Get("Wall_Plaster_Door_RoundInset");
            GameObject roof = kit.Get("Roof_RoundTiles_4x6");
            if (wall == null || roof == null) return kit;

            Bounds w = LocalBounds(wall);
            kit.WallAlong = Mathf.Abs(w.size.x) >= Mathf.Abs(w.size.z) ? Vector3.right : Vector3.forward;
            Vector3 across = kit.WallAlong == Vector3.right ? Vector3.forward : Vector3.right;
            // The inset doorway reaches into the house, so the outside is the other way from its middle
            Bounds i = inset != null ? LocalBounds(inset) : w;
            float inside = Vector3.Dot(i.center, across);
            kit.WallOut = inside > 0f ? -across : across;
            kit.WallTurn = Quaternion.Inverse(Quaternion.LookRotation(kit.WallOut, Vector3.up));

            Bounds r = LocalBounds(roof);
            kit.RoofRidge = r.size.z >= r.size.x ? Vector3.forward : Vector3.right;
            kit.RoofTurn = Quaternion.Inverse(Quaternion.LookRotation(kit.RoofRidge, Vector3.up));
            return kit;
        }

        /// <summary>The bounds of a piece's meshes as it stands when put down unturned (its own import turn and scale
        /// included, so up is up).</summary>
        private static Bounds LocalBounds(GameObject piece)
        {
            var bounds = new Bounds();
            bool any = false;
            Matrix4x4 toRoot = Matrix4x4.Translate(-piece.transform.position);
            foreach (MeshFilter filter in piece.GetComponentsInChildren<MeshFilter>(true))
            {
                if (filter.sharedMesh == null) continue;
                Matrix4x4 m = toRoot * filter.transform.localToWorldMatrix;
                Bounds b = filter.sharedMesh.bounds;
                for (int c = 0; c < 8; c++)
                {
                    Vector3 corner = b.center + Vector3.Scale(b.extents, new Vector3((c & 1) == 0 ? -1 : 1, (c & 2) == 0 ? -1 : 1, (c & 4) == 0 ? -1 : 1));
                    Vector3 p = m.MultiplyPoint3x4(corner);
                    if (!any) { bounds = new Bounds(p, Vector3.zero); any = true; }
                    else bounds.Encapsulate(p);
                }
            }
            return bounds;
        }

        /// <summary>True if the kit is in the project (and its models have imported).</summary>
        internal static bool KitReady => Pieces().Get("Wall_Plaster_Straight") != null;

        // ---------------------------------------------------------------- Building

        /// <summary>Makes the house as a new object (its middle at the bottom of its ground floor, its door facing +x).</summary>
        internal static GameObject Build(Design design, Transform parent)
        {
            Kit k = Pieces();
            var random = new System.Random(design.Seed * 31 + 7);
            float Chance() => (float)random.NextDouble();

            var house = new GameObject($"House {design.Width * 2}x{design.Length * 2}");
            if (parent != null) house.transform.SetParent(parent, false);
            var marker = house.AddComponent<ScatteredBuilding>();
            marker.footprint = design.Footprint;
            marker.seed = design.Seed;
            marker.cells = new Vector3Int(design.Width, design.Length, design.Storeys);

            float halfW = design.Width * Cell * 0.5f, halfL = design.Length * Cell * 0.5f;
            string opening = design.RoundOpenings ? "Round" : "Flat";
            string[] windowWall = { "Window_Wide_Flat", "Window_Wide_Round", "Window_Thin_Round" };
            string[] windowPiece = { "Window_Wide_Flat1", "Window_Wide_Round1", "Window_Thin_Round1" };
            string[] shutters = { "WindowShutters_Wide_Flat", "WindowShutters_Wide_Round", "WindowShutters_Thin_Round" };
            int doorCell = design.Length / 2; // the middle cell of the +x side

            for (int storey = 0; storey < design.Storeys; storey++)
            {
                float y = storey * Storey;
                bool ground = storey == 0;
                Transform level = Group(house.transform, ground ? "Ground Floor" : $"Floor {storey + 1}");

                // The walls round the outside, cell by cell: (edge, cell) -> position and outward direction
                foreach ((Vector3 at, Vector3 outward, bool front, int index) in Edges(design))
                {
                    bool door = ground && front && index == doorCell;
                    bool window = !door && Chance() < design.WindowChance;
                    string wall = WallPiece(design.Style, ground, door, window, opening, windowWall[design.WindowShape], random);
                    Vector3 spot = at + Vector3.up * y;
                    Place(k, wall, level, spot, outward, k.WallTurn);
                    if (window)
                    {
                        Place(k, windowPiece[design.WindowShape], level, spot, outward, k.WallTurn);
                        if (Chance() < design.ShutterChance)
                        {
                            Place(k, shutters[design.WindowShape] + (Chance() < 0.6f ? "_Open" : "_Closed"), level, spot, outward, k.WallTurn);
                        }
                    }
                    if (door) PlaceDoor(k, $"Door_{design.Door}_{opening}", $"DoorFrame_{opening}_{(design.Style == Style.Stone ? "Brick" : "WoodDark")}", level, spot, outward);
                }

                // The corner posts
                foreach (Vector3 corner in new[] { V(halfW, halfL), V(-halfW, halfL), V(halfW, -halfL), V(-halfW, -halfL) })
                {
                    Place(k, "Corner_Exterior_Wood", level, corner + Vector3.up * y, corner.normalized, k.WallTurn);
                }

                // The floor (the one above is this one's ceiling), unless the inside is hidden
                if (!design.HideInterior)
                for (int x = 0; x < design.Width; x++)
                {
                    for (int z = 0; z < design.Length; z++)
                    {
                        Vector3 middle = new Vector3(-halfW + Cell * (x + 0.5f), y + 0.02f, -halfL + Cell * (z + 0.5f));
                        Place(k, ground ? "Floor_WoodDark" : "Floor_WoodLight", level, middle, Vector3.forward, Quaternion.identity);
                    }
                }
            }

            // A stone footing all round, under the ground floor, so the house sits on sloping ground without gaps
            if (design.Footing)
            {
                Transform footing = Group(house.transform, "Footing");
                foreach ((Vector3 at, Vector3 outward, bool front, int index) in Edges(design))
                {
                    Place(k, "Wall_UnevenBrick_Straight", footing, at + Vector3.down * (Storey - 0.02f), outward, k.WallTurn);
                }
            }

            // The roof, its gable ends and the chimney
            Transform roof = Group(house.transform, "Roof");
            float top = design.Storeys * Storey;
            Place(k, $"Roof_RoundTiles_{design.Width * 2}x{design.Length * 2}", roof, Vector3.up * top, Vector3.forward, k.RoofTurn);
            string gable = $"Roof_Front_Brick{design.Width * 2}";
            Place(k, gable, roof, new Vector3(0f, top, halfL), Vector3.forward, k.WallTurn);
            Place(k, gable, roof, new Vector3(0f, top, -halfL), Vector3.back, k.WallTurn);
            if (design.Chimney)
            {
                float side = Chance() < 0.5f ? 1f : -1f;
                Place(k, design.ChimneyStone ? "Prop_Chimney" : "Prop_Chimney2", roof,
                      new Vector3(side * (halfW - 1f), top - 0.2f, halfL - 1f), Vector3.forward, Quaternion.identity);
            }

            foreach (Transform t in house.GetComponentsInChildren<Transform>(true))
            {
                GameObjectUtility.SetStaticEditorFlags(t.gameObject, StaticEditorFlags.BatchingStatic | StaticEditorFlags.OccluderStatic |
                                                                     StaticEditorFlags.OccludeeStatic | StaticEditorFlags.ContributeGI |
                                                                     StaticEditorFlags.ReflectionProbeStatic);
            }
            // The inside hidden: a dark box just inside the walls, from the ground floor up to the roof, so looking in at a
            // window or the door shows darkness rather than an empty shell
            if (design.HideInterior)
            {
                var inside = GameObject.CreatePrimitive(PrimitiveType.Cube);
                inside.name = "Interior (hidden)";
                Object.DestroyImmediate(inside.GetComponent<Collider>());
                inside.transform.SetParent(house.transform, false);
                float wallIn = 0.34f; // (the kit's walls reach 0.31 m in from their line)
                float tall = design.Storeys * Storey;
                inside.transform.localPosition = new Vector3(0f, tall * 0.5f, 0f);
                inside.transform.localScale = new Vector3(halfW * 2f - wallIn * 2f, tall - 0.02f, halfL * 2f - wallIn * 2f);
                var renderer = inside.GetComponent<MeshRenderer>();
                renderer.sharedMaterial = InteriorMaterial();
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                GameObjectUtility.SetStaticEditorFlags(inside, StaticEditorFlags.BatchingStatic);
            }

            if (BuilderSettings.Get().mergeDetails) MeshMerger.Merge(house, false);
            BuildingThemes.Apply(house, design.Theme, false);
            return house;
        }

        private static Vector3 V(float x, float z) => new Vector3(x, 0f, z);

        // The dark of a hidden inside (made the first time)
        private static Material InteriorMaterial()
        {
            string folder = MedievalVillageSetup.KitFolder + "/Generated";
            string path = folder + "/House Interior.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material != null) return material;
            if (!AssetDatabase.IsValidFolder(folder)) AssetDatabase.CreateFolder(MedievalVillageSetup.KitFolder, "Generated");
            // (lit, so it fogs over in the distance and goes darker still at night)
            material = new Material(MedievalVillageSetup.BuildingShader) { name = "House Interior", enableInstancing = true };
            material.SetFloat("_Smoothness", 0f);
            material.SetColor("_BaseColor", new Color(0.035f, 0.03f, 0.025f));
            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        // Each 2 m stretch of the outside walls: its middle, which way is out, whether it's on the front (+x) side, and
        // which cell along that side it is
        private static IEnumerable<(Vector3, Vector3, bool, int)> Edges(Design design)
        {
            float halfW = design.Width * Cell * 0.5f, halfL = design.Length * Cell * 0.5f;
            for (int z = 0; z < design.Length; z++)
            {
                float along = -halfL + Cell * (z + 0.5f);
                yield return (V(halfW, along), Vector3.right, true, z);
                yield return (V(-halfW, along), Vector3.left, false, z);
            }
            for (int x = 0; x < design.Width; x++)
            {
                float along = -halfW + Cell * (x + 0.5f);
                yield return (V(along, halfL), Vector3.forward, false, x);
                yield return (V(along, -halfL), Vector3.back, false, x);
            }
        }

        private static string WallPiece(Style style, bool ground, bool door, bool window, string opening, string windowWall, System.Random random)
        {
            bool stone = style == Style.Stone && ground;
            if (door) return stone ? $"Wall_UnevenBrick_Door_{opening}" : $"Wall_Plaster_Door_{opening}";
            if (window) return (stone ? "Wall_UnevenBrick_" : "Wall_Plaster_") + windowWall;
            if (stone) return "Wall_UnevenBrick_Straight";
            if (ground) return style == Style.Timber && random.NextDouble() < 0.3 ? "Wall_Plaster_WoodGrid" : "Wall_Plaster_Straight_Base";
            return style == Style.Plaster ? "Wall_Plaster_Straight" : "Wall_Plaster_WoodGrid";
        }

        private static Transform Group(Transform parent, string name)
        {
            Transform found = parent.Find(name);
            if (found != null) return found;
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            return go.transform;
        }

        // A piece at 'at' (in the house's space), turned to face 'outward' (after 'turn' sets it facing +z)
        private static GameObject Place(Kit k, string name, Transform parent, Vector3 at, Vector3 outward, Quaternion turn)
        {
            GameObject piece = k.Get(name);
            if (piece == null)
            {
                Debug.LogWarning($"Castle & Village Builder: the kit has no {name}.");
                return null;
            }
            var go = (GameObject)PrefabUtility.InstantiatePrefab(piece, parent);
            outward.y = 0f;
            Quaternion facing = outward.sqrMagnitude > 0.0001f ? Quaternion.LookRotation(outward.normalized, Vector3.up) : Quaternion.identity;
            go.transform.localRotation = facing * turn * piece.transform.localRotation;
            go.transform.localPosition = at;
            return go;
        }

        // ---------------------------------------------------------------- For other builders (castle walls)

        internal enum Turn { Wall, Roof, None }

        /// <summary>
        /// One of the kit's pieces at 'at' in the parent's space: a wall facing 'outward', a roof with its ridge along
        /// 'outward', or anything else turned to face 'outward' as it comes. 'stretch' scales it as it stands: x along a
        /// wall (or across), y up, z through it.
        /// </summary>
        internal static GameObject Piece(string name, Transform parent, Vector3 at, Vector3 outward, Turn turn, Vector3 stretch)
        {
            Kit k = Pieces();
            Quaternion t = turn == Turn.Wall ? k.WallTurn : turn == Turn.Roof ? k.RoofTurn : Quaternion.identity;
            GameObject go = Place(k, name, parent, at, outward, t);
            if (go == null || stretch == Vector3.one) return go;
            GameObject source = k.Get(name);
            // As it stands (after the wall turn), x runs along a wall; in the piece's own space its axes are turned by
            // its import (the kit's are turned -90 about x), so each stretch goes onto whichever of its own axes lines up
            Quaternion own = source.transform.localRotation;
            Vector3 along = Quaternion.Inverse(t) * Vector3.right;
            Vector3 through = Quaternion.Inverse(t) * Vector3.forward;
            Vector3 scale = source.transform.localScale;
            Stretch(ref scale, Quaternion.Inverse(own) * along, stretch.x);
            Stretch(ref scale, Quaternion.Inverse(own) * Vector3.up, stretch.y);
            Stretch(ref scale, Quaternion.Inverse(own) * through, stretch.z);
            go.transform.localScale = scale;
            return go;
        }

        private static void Stretch(ref Vector3 scale, Vector3 axis, float by)
        {
            float x = Mathf.Abs(axis.x), y = Mathf.Abs(axis.y), z = Mathf.Abs(axis.z);
            if (x >= y && x >= z) scale.x *= by;
            else if (y >= z) scale.y *= by;
            else scale.z *= by;
        }

        /// <summary>How far a wall piece's outside face stands out from where it's put (along its outward direction).</summary>
        internal static float WallFaceOut
        {
            get
            {
                Kit k = Pieces();
                GameObject wall = k.Get("Wall_UnevenBrick_Straight");
                if (wall == null) return 0.1f;
                Bounds b = LocalBounds(wall);
                float most = float.MinValue;
                for (int c = 0; c < 8; c++)
                {
                    Vector3 corner = b.center + Vector3.Scale(b.extents, new Vector3((c & 1) == 0 ? -1 : 1, (c & 2) == 0 ? -1 : 1, (c & 4) == 0 ? -1 : 1));
                    most = Mathf.Max(most, Vector3.Dot(corner, k.WallOut));
                }
                return most;
            }
        }

        /// <summary>The size of one of the kit's pieces as it stands (0 if it's missing).</summary>
        internal static Vector3 PieceSize(string name)
        {
            GameObject piece = Pieces().Get(name);
            return piece != null ? LocalBounds(piece).size : Vector3.zero;
        }

        // The doorway (1.3 m wide in the kit's walls) gets its frame, which narrows it to the door's width, and the door
        // hung in it: each moved along the wall till its middle is the doorway's (measured where it ends up, since the
        // door is made hanging from its hinge, not round its middle)
        private static void PlaceDoor(Kit k, string door, string frame, Transform parent, Vector3 at, Vector3 outward)
        {
            Vector3 along = parent.TransformDirection(Vector3.Cross(Vector3.up, outward.normalized)).normalized;
            Vector3 want = parent.TransformPoint(at);
            foreach (string name in new[] { frame, door })
            {
                GameObject go = Place(k, name, parent, at, outward, k.WallTurn);
                if (go == null) continue;
                Renderer[] renderers = go.GetComponentsInChildren<Renderer>();
                if (renderers.Length == 0) continue;
                Bounds b = renderers[0].bounds;
                foreach (Renderer r in renderers) b.Encapsulate(r.bounds);
                go.transform.position += along * Vector3.Dot(want - b.center, along);
            }
        }
    }
}
#endif
