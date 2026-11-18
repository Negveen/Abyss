#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEngine;

namespace YaraJ.CastleVillageBuilder
{
    /// <summary>
    /// Editor only. Castle & Village Builder' auto paths: joins houses up with paths painted on the land (Paint Land's paths, in
    /// a colour, or one of its textures), each new house to the nearest one already joined (so the village is all joined
    /// up, by the shortest ways), leaving from the front doors and curving round other houses.
    /// </summary>
    internal static class BuildingPaths
    {
        internal enum Look { Colour, Texture, Mesh }

        internal sealed class Style
        {
            public Look Look = Look.Colour;
            public Color Colour = new Color(0.62f, 0.42f, 0.27f);
            public int Layer = 1;              // which of Paint Land's 8 textures
            public float Width = 2.2f;
            public float Wobble = 0.35f;       // how much it wanders
            public float Longest = 60f;        // houses further apart than this aren't joined
            public bool ClearUnder = true;     // rub out the scatter kinds set to clear under paths
        }

        /// <summary>
        /// Joins the 'fresh' houses into the village's paths, like streets: each new house, nearest first, to the nearest
        /// point of the paths so far (or a house already joined), found round houses, castle walls (through their
        /// gateways) and water, keeping to gentle ground and joining onto paths already laid where it can, so they branch
        /// off one another instead of each running door to door. Returns what went wrong, or null.
        /// </summary>
        internal static string Connect(IList<ScatteredBuilding> fresh, IList<ScatteredBuilding> all, Style style)
        {
            List<List<Vector3>> routes = Plan(fresh, all, style);
            return routes.Count == 0 ? null : Paint(routes, style);
        }

        /// <summary>The routes Connect would paint (without painting them).</summary>
        internal static List<List<Vector3>> Plan(IList<ScatteredBuilding> fresh, IList<ScatteredBuilding> all, Style style)
        {
            var remaining = new List<ScatteredBuilding>();
            foreach (ScatteredBuilding h in fresh) if (h != null && !remaining.Contains(h)) remaining.Add(h);
            var routes = new List<List<Vector3>>();
            if (remaining.Count == 0) return routes;

            // The network so far: the doorsteps of the houses already joined (and every path laid now, as it's laid)
            var network = new List<Vector3>();
            foreach (ScatteredBuilding h in all) if (h != null && !remaining.Contains(h)) network.Add(Doorstep(h, out _));
            if (network.Count == 0)
            {
                network.Add(Doorstep(remaining[0], out _));
                remaining.RemoveAt(0);
            }

            var grid = new Grid(all, style);
            while (remaining.Count > 0)
            {
                // The closest unjoined house to any point of the network
                ScatteredBuilding next = null;
                Vector3 target = default;
                float best = float.MaxValue;
                foreach (ScatteredBuilding h in remaining)
                {
                    Vector3 step = Doorstep(h, out _);
                    foreach (Vector3 p in network)
                    {
                        float d = Flat(p - step).sqrMagnitude;
                        if (d < best) { best = d; next = h; target = p; }
                    }
                }
                if (next == null) break;
                remaining.Remove(next);
                Vector3 door = Doorstep(next, out Vector3 outward);
                if (Mathf.Sqrt(best) <= style.Longest)
                {
                    // Out from the door a little way first, then found to the network
                    Vector3 outside = door + outward * 1.6f;
                    List<Vector3> found = grid.Find(outside, target);
                    var route = new List<Vector3> { door };
                    if (found != null) route.AddRange(found);
                    else route.AddRange(new[] { outside, target });
                    route = Smooth(route, style);
                    routes.Add(route);
                    grid.Lay(route);
                    for (int i = 0; i < route.Count; i += 4) network.Add(route[i]);
                }
                network.Add(door);
            }
            return routes;
        }

        // Where a house's path starts: just out from its front door (its +x side), and which way is out
        internal static Vector3 Doorstep(ScatteredBuilding house, out Vector3 outward)
        {
            var shape = new MedievalHouseBuilder.Design { Width = Mathf.Max(1, house.cells.x), Length = Mathf.Max(1, house.cells.y) };
            outward = house.transform.rotation * Vector3.right;
            outward.y = 0f;
            outward.Normalize();
            return house.transform.position + house.transform.rotation * shape.DoorSpot + outward * 0.8f;
        }

        private static string Paint(List<List<Vector3>> routes, Style style)
        {
            if (style.Look == Look.Mesh || !Ground.OnLand) return PaintMeshes(routes, style);
            string trouble = style.Look == Look.Colour ? PaintLandLink.LoadPaths() : PaintLandLink.LoadTextures();
            if (trouble != null) return trouble;
            if (PaintLandLink.Land == null) return "No land to paint the paths on.";

            PaintLandLink.BeginPaint("Auto Paths");
            bool clearing = style.ClearUnder && PaintLandLink.BeginClearing();
            try
            {
                float radius = style.Width * 0.5f;
                var colour = (Color32)style.Colour;
                colour.a = 255;
                foreach (List<Vector3> route in routes)
                {
                    foreach (Vector3 point in Resample(route, Mathf.Max(style.Width * 0.25f, 0.2f)))
                    {
                        Vector2 at = PaintLandLink.ToLand(point);
                        if (style.Look == Look.Colour) PaintLandLink.StampPath(at, radius, colour);
                        else PaintLandLink.StampTexture(at, radius, Mathf.Clamp(style.Layer, 1, 8));
                        if (clearing) PaintLandLink.ClearAt(point, radius);
                    }
                }
            }
            finally
            {
                PaintLandLink.EndPaint("Auto Paths", style.Look == Look.Texture);
                if (clearing) PaintLandLink.EndClearing();
            }
            return null;
        }

        // ---------------------------------------------------------------- Paths as meshes (on any ground)

        internal const string ContainerName = "Village Paths";

        // Each route as a strip of mesh laid on the ground, under the "Village Paths" object
        private static string PaintMeshes(List<List<Vector3>> routes, Style style)
        {
            BuilderSettings settings = BuilderSettings.Get();
            Material material = settings.pathMaterial != null ? settings.pathMaterial : PathMaterial(style.Colour);
            Transform parent = PathContainer();
            float half = style.Width * 0.5f;
            foreach (List<Vector3> route in routes)
            {
                List<Vector3> points = Resample(route, 0.5f);
                if (points.Count < 2) continue;
                var vertices = new List<Vector3>();
                var uvs = new List<Vector2>();
                var triangles = new List<int>();
                float along = 0f, lift = settings.pathLift + 0.002f * (parent.childCount % 8);
                Vector3 origin = points[0];
                for (int i = 0; i < points.Count; i++)
                {
                    Vector3 ahead = points[Mathf.Min(i + 1, points.Count - 1)] - points[Mathf.Max(i - 1, 0)];
                    ahead.y = 0f;
                    Vector3 side = Vector3.Cross(Vector3.up, ahead.sqrMagnitude > 1e-6f ? ahead.normalized : Vector3.forward);
                    if (i > 0) along += Flat(points[i] - points[i - 1]).magnitude;
                    for (int s = -1; s <= 1; s += 2)
                    {
                        Vector3 edge = points[i] + side * (half * s);
                        edge.y = Ground.At(edge, out float h) ? h : points[i].y;
                        vertices.Add(edge + Vector3.up * lift - origin);
                        uvs.Add(new Vector2(s < 0 ? 0f : 1f, along / Mathf.Max(style.Width, 0.1f)));
                    }
                    if (i == 0) continue;
                    int a = vertices.Count - 4;
                    triangles.AddRange(new[] { a, a + 2, a + 1, a + 1, a + 2, a + 3 });
                }
                var mesh = new Mesh { name = "Village Path" };
                mesh.SetVertices(vertices);
                mesh.SetUVs(0, uvs);
                mesh.SetTriangles(triangles, 0);
                mesh.RecalculateNormals();
                mesh.RecalculateTangents();
                mesh.RecalculateBounds();

                var path = new GameObject("Path " + (parent.childCount + 1));
                path.transform.SetParent(parent, false);
                path.transform.position = origin;
                path.AddComponent<MeshFilter>().sharedMesh = mesh;
                var renderer = path.AddComponent<MeshRenderer>();
                renderer.sharedMaterial = material;
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                UnityEditor.Undo.RegisterCreatedObjectUndo(path, "Auto Paths");

                if (style.ClearUnder)
                {
                    foreach (Vector3 p in Resample(route, 2f)) TerrainTrees.Clear(t => Flat(t - p).magnitude <= half + 0.5f, p, half + 0.5f);
                }
            }
            return null;
        }

        /// <summary>
        /// Rubs out the mesh paths under a brush (its middle and radius, across the ground): just the part under it, so a
        /// path can be shortened or cut in two; one with nothing left goes altogether. Undoable. How many were changed.
        /// </summary>
        internal static int CutPaths(Vector3 centre, float radius)
        {
            Transform container = null;
            foreach (GameObject root in UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects())
                if (root.name == ContainerName) { container = root.transform; break; }
            if (container == null) return 0;
            int changed = 0;
            float r2 = radius * radius;
            foreach (MeshFilter filter in container.GetComponentsInChildren<MeshFilter>())
            {
                Mesh mesh = filter.sharedMesh;
                if (mesh == null) continue;
                // (a quick look first: is the brush anywhere near it)
                Bounds b = filter.GetComponent<Renderer>().bounds;
                b.Expand(new Vector3(radius * 2f, 1000f, radius * 2f));
                if (!b.Contains(centre)) continue;

                Vector3[] vertices = mesh.vertices;
                int[] triangles = mesh.triangles;
                var kept = new List<int>(triangles.Length);
                Transform t = filter.transform;
                for (int i = 0; i < triangles.Length; i += 3)
                {
                    Vector3 middle = t.TransformPoint((vertices[triangles[i]] + vertices[triangles[i + 1]] + vertices[triangles[i + 2]]) / 3f);
                    if (Flat(middle - centre).sqrMagnitude <= r2) continue;
                    kept.Add(triangles[i]); kept.Add(triangles[i + 1]); kept.Add(triangles[i + 2]);
                }
                if (kept.Count == triangles.Length) continue;
                changed++;
                if (kept.Count == 0) { UnityEditor.Undo.DestroyObjectImmediate(filter.gameObject); continue; }
                var cut = Object.Instantiate(mesh);
                cut.name = mesh.name;
                cut.SetTriangles(kept, 0);
                cut.RecalculateBounds();
                UnityEditor.Undo.RecordObject(filter, "Rub Out Path");
                filter.sharedMesh = cut;
            }
            if (container.childCount == 0) UnityEditor.Undo.DestroyObjectImmediate(container.gameObject);
            return changed;
        }

        private static Transform PathContainer()
        {
            foreach (GameObject root in UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects())
            {
                if (root.name == ContainerName) return root.transform;
            }
            var made = new GameObject(ContainerName);
            UnityEditor.Undo.RegisterCreatedObjectUndo(made, "Auto Paths");
            return made.transform;
        }

        // A plain material in a path colour (one for each colour, kept with the kit's made materials)
        private static Material PathMaterial(Color colour)
        {
            string folder = MedievalVillageSetup.KitFolder + "/Generated";
            if (!UnityEditor.AssetDatabase.IsValidFolder(folder)) UnityEditor.AssetDatabase.CreateFolder(MedievalVillageSetup.KitFolder, "Generated");
            string path = $"{folder}/Path {ColorUtility.ToHtmlStringRGB(colour)}.mat";
            var material = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material != null) return material;
            material = new Material(MedievalVillageSetup.BuildingShader) { name = "Path " + ColorUtility.ToHtmlStringRGB(colour) };
            material.SetColor("_BaseColor", colour);
            material.SetFloat("_Smoothness", 0.05f);
            UnityEditor.AssetDatabase.CreateAsset(material, path);
            return material;
        }

        // A route's corners rounded off (and a gentle wander, as asked), keeping its ends where they are
        private static List<Vector3> Smooth(List<Vector3> route, Style style)
        {
            var points = new List<Vector3>(route);
            for (int pass = 0; pass < 3 && points.Count >= 3; pass++)
            {
                var smooth = new List<Vector3> { points[0] };
                for (int i = 0; i < points.Count - 1; i++)
                {
                    smooth.Add(Vector3.Lerp(points[i], points[i + 1], 0.25f));
                    smooth.Add(Vector3.Lerp(points[i], points[i + 1], 0.75f));
                }
                smooth.Add(points[points.Count - 1]);
                points = smooth;
            }
            if (style.Wobble <= 0f || points.Count < 3) return points;
            float length = 0f;
            for (int i = 1; i < points.Count; i++) length += Flat(points[i] - points[i - 1]).magnitude;
            float sofar = 0f, seed = (points[0].x * 0.37f + points[0].z * 0.71f) % 50f;
            for (int i = 1; i < points.Count - 1; i++)
            {
                sofar += Flat(points[i] - points[i - 1]).magnitude;
                Vector3 way = Flat(points[i + 1] - points[i - 1]).normalized;
                Vector3 side = Vector3.Cross(Vector3.up, way);
                float fade = Mathf.Clamp01(Mathf.Min(sofar, length - sofar) / 4f); // (none at the ends)
                points[i] += side * ((Mathf.PerlinNoise(seed, sofar / 10f) - 0.5f) * style.Wobble * style.Width * 1.5f * fade);
            }
            return points;
        }

        // Points along a route, 'spacing' apart
        private static List<Vector3> Resample(List<Vector3> route, float spacing)
        {
            var points = new List<Vector3>();
            if (route.Count == 0) return points;
            points.Add(route[0]);
            float carry = 0f;
            for (int i = 1; i < route.Count; i++)
            {
                Vector3 a = route[i - 1], b = route[i];
                float length = Flat(b - a).magnitude;
                float d = spacing - carry;
                while (d <= length)
                {
                    points.Add(Vector3.Lerp(a, b, d / length));
                    d += spacing;
                }
                carry = length - (d - spacing);
            }
            points.Add(route[route.Count - 1]);
            return points;
        }

        // ---------------------------------------------------------------- Finding the way (A*, on a 1 m grid)

        private sealed class Grid
        {
            private const float CellSize = 1f;
            private readonly IList<ScatteredBuilding> houses;
            private readonly List<CastlePiece> castle = new List<CastlePiece>();
            private readonly float margin;
            private readonly Dictionary<Vector2Int, bool> blocked = new Dictionary<Vector2Int, bool>();
            private readonly Dictionary<Vector2Int, float> heights = new Dictionary<Vector2Int, float>();
            private readonly HashSet<Vector2Int> laid = new HashSet<Vector2Int>();

            public Grid(IList<ScatteredBuilding> houses, Style style)
            {
                this.houses = houses;
                margin = style.Width * 0.5f + 0.4f;
                foreach (CastlePiece piece in Object.FindObjectsByType<CastlePiece>(FindObjectsSortMode.None)) castle.Add(piece);
            }

            private static Vector2Int Cell(Vector3 p) => new Vector2Int(Mathf.RoundToInt(p.x / CellSize), Mathf.RoundToInt(p.z / CellSize));
            private static Vector3 Centre(Vector2Int c) => new Vector3(c.x * CellSize, 0f, c.y * CellSize);

            private float Height(Vector2Int c)
            {
                if (heights.TryGetValue(c, out float h)) return h;
                h = Ground.At(Centre(c), out float ground) ? ground : float.NaN;
                heights[c] = h;
                return h;
            }

            // Can't be walked through: off the land, in water, in a house, or in a castle wall or tower (but a wall's
            // gateway can be)
            private bool Blocked(Vector2Int c)
            {
                if (blocked.TryGetValue(c, out bool b)) return b;
                Vector3 p = Centre(c);
                float h = Height(c);
                b = float.IsNaN(h) || Ground.UnderWater(new Vector3(p.x, h, p.z), 0.05f);
                if (!b)
                {
                    foreach (ScatteredBuilding house in houses)
                    {
                        if (house == null) continue;
                        Vector3 local = Quaternion.Euler(0f, -house.transform.eulerAngles.y, 0f) * (p - Flat(house.transform.position));
                        if (Mathf.Abs(local.x) <= house.footprint.x * 0.5f + margin && Mathf.Abs(local.z) <= house.footprint.y * 0.5f + margin) { b = true; break; }
                    }
                }
                if (!b)
                {
                    foreach (CastlePiece piece in castle)
                    {
                        if (piece == null) continue;
                        if (piece.tower || piece.joint)
                        {
                            if (Flat(p - piece.transform.position).magnitude <= piece.Radius + margin) { b = true; break; }
                            continue;
                        }
                        if (piece.path == null || piece.path.Count < 2) continue;
                        CastleWallBuilder.DistanceAlong(piece.path, piece.closed, p, out float away);
                        if (away <= piece.width * 0.5f + margin && !CastlePiece.KeepsGrassOff(p, 0f)) { b = true; break; }
                    }
                }
                blocked[c] = b;
                return b;
            }

            /// <summary>The cells a route covers count as path from now on (later routes join onto them, cheaply).</summary>
            public void Lay(List<Vector3> route)
            {
                foreach (Vector3 p in Resample(route, CellSize * 0.5f))
                {
                    Vector2Int c = Cell(p);
                    laid.Add(c);
                }
            }

            /// <summary>A way from one point to another (null if there's none near enough), through cell middles.</summary>
            public List<Vector3> Find(Vector3 from, Vector3 to)
            {
                Vector2Int start = Cell(from), goal = Cell(to);
                if (start == goal) return new List<Vector3> { to };
                int pad = 25;
                int minX = Mathf.Min(start.x, goal.x) - pad, maxX = Mathf.Max(start.x, goal.x) + pad;
                int minY = Mathf.Min(start.y, goal.y) - pad, maxY = Mathf.Max(start.y, goal.y) + pad;

                var cost = new Dictionary<Vector2Int, float> { [start] = 0f };
                var came = new Dictionary<Vector2Int, Vector2Int>();
                var open = new Heap();
                open.Push(start, Guess(start, goal));
                var done = new HashSet<Vector2Int>();
                int budget = 60000;
                while (open.Count > 0 && budget-- > 0)
                {
                    Vector2Int c = open.Pop();
                    if (c == goal || (laid.Contains(c) && Vector2Int.Distance(c, goal) < 2f)) { goal = c; break; }
                    if (!done.Add(c)) continue;
                    float hc = Height(c);
                    for (int dx = -1; dx <= 1; dx++)
                    {
                        for (int dy = -1; dy <= 1; dy++)
                        {
                            if (dx == 0 && dy == 0) continue;
                            var n = new Vector2Int(c.x + dx, c.y + dy);
                            if (n.x < minX || n.x > maxX || n.y < minY || n.y > maxY || done.Contains(n)) continue;
                            // (the cells round the start and the end are always open: they're at doors and paths)
                            bool nearEnds = Vector2Int.Distance(n, start) < 2f || Vector2Int.Distance(n, goal) < 2f;
                            if (!nearEnds && Blocked(n)) continue;
                            float step = (dx != 0 && dy != 0) ? 1.414f : 1f;
                            float hn = Height(n);
                            float climb = float.IsNaN(hn) || float.IsNaN(hc) ? 0f : Mathf.Abs(hn - hc) / (step * CellSize);
                            float move = step * (1f + climb * climb * 6f) * (laid.Contains(n) ? 0.35f : 1f);
                            float total = cost[c] + move;
                            if (cost.TryGetValue(n, out float had) && had <= total) continue;
                            cost[n] = total;
                            came[n] = c;
                            open.Push(n, total + Guess(n, goal));
                        }
                    }
                }
                if (!came.ContainsKey(goal) && goal != start) return null;

                var cells = new List<Vector2Int> { goal };
                for (Vector2Int c = goal; came.TryGetValue(c, out Vector2Int prev);) { cells.Add(prev); c = prev; }
                cells.Reverse();
                var points = new List<Vector3>();
                // (every other cell, ends exact: the smoothing does the rest)
                for (int i = 0; i < cells.Count; i += 2) points.Add(Centre(cells[i]));
                points[0] = Flat(from);
                Vector3 end = Centre(cells[cells.Count - 1]);
                if (Flat(points[points.Count - 1] - end).sqrMagnitude > 0.01f) points.Add(end);
                if (goal == Cell(to)) points[points.Count - 1] = Flat(to);
                return points;
            }

            private static float Guess(Vector2Int a, Vector2Int b)
            {
                int dx = Mathf.Abs(a.x - b.x), dy = Mathf.Abs(a.y - b.y);
                return (dx + dy) + (1.414f - 2f) * Mathf.Min(dx, dy);
            }
        }

        // A plain binary heap of cells by priority (smallest first)
        private sealed class Heap
        {
            private readonly List<(Vector2Int cell, float priority)> items = new List<(Vector2Int, float)>();
            public int Count => items.Count;

            public void Push(Vector2Int cell, float priority)
            {
                items.Add((cell, priority));
                int i = items.Count - 1;
                while (i > 0)
                {
                    int parent = (i - 1) / 2;
                    if (items[parent].priority <= items[i].priority) break;
                    (items[parent], items[i]) = (items[i], items[parent]);
                    i = parent;
                }
            }

            public Vector2Int Pop()
            {
                Vector2Int top = items[0].cell;
                items[0] = items[items.Count - 1];
                items.RemoveAt(items.Count - 1);
                int i = 0;
                while (true)
                {
                    int l = i * 2 + 1, r = l + 1, smallest = i;
                    if (l < items.Count && items[l].priority < items[smallest].priority) smallest = l;
                    if (r < items.Count && items[r].priority < items[smallest].priority) smallest = r;
                    if (smallest == i) break;
                    (items[smallest], items[i]) = (items[i], items[smallest]);
                    i = smallest;
                }
                return top;
            }
        }

        /// <summary>The points a path between two houses' doors would be painted along (without painting it).</summary>
        internal static List<Vector3> RouteBetween(ScatteredBuilding a, ScatteredBuilding b, IList<ScatteredBuilding> all, Style style) => Route(a, b, all, style);

        // The points along a path from one house's door to another's, close enough together to paint it in one: a
        // curve leaving each door straight out, wandering a little, swung out sideways if it would cut through a house
        private static List<Vector3> Route(ScatteredBuilding a, ScatteredBuilding b, IList<ScatteredBuilding> all, Style style)
        {
            Vector3 p0 = Doorstep(a, out Vector3 outA), p3 = Doorstep(b, out Vector3 outB);
            float distance = Vector3.Distance(Flat(p0), Flat(p3));
            float reach = Mathf.Clamp(distance * 0.35f, 2f, 10f);
            Vector3 across = Vector3.Cross(Vector3.up, Flat(p3 - p0).normalized);
            float seed = (a.GetInstanceID() * 0.137f + b.GetInstanceID() * 0.311f) % 100f;

            List<Vector3> best = null;
            int fewest = int.MaxValue;
            foreach (float swing in new[] { 0f, 6f, -6f, 12f, -12f, 20f, -20f })
            {
                Vector3 p1 = p0 + outA * reach + across * swing, p2 = p3 + outB * reach + across * swing;
                var points = new List<Vector3>();
                int steps = Mathf.Clamp(Mathf.CeilToInt((distance + Mathf.Abs(swing) * 2f) / Mathf.Max(style.Width * 0.25f, 0.2f)), 4, 4000);
                int blocked = 0;
                for (int i = 0; i <= steps; i++)
                {
                    float t = i / (float)steps;
                    Vector3 p = Bezier(p0, p1, p2, p3, t);
                    // (A gentle wander, none at the doors)
                    float wander = (Mathf.PerlinNoise(seed, t * distance / 12f) - 0.5f) * 2f * style.Wobble * style.Width * 2f * Mathf.Sin(t * Mathf.PI);
                    p += across * wander;
                    points.Add(p);
                    if (t > 0.04f && t < 0.96f && InsideHouse(p, all, style.Width * 0.5f)) blocked++;
                }
                if (blocked == 0) return points;
                if (blocked < fewest) { fewest = blocked; best = points; }
            }
            return best;
        }

        private static bool InsideHouse(Vector3 point, IList<ScatteredBuilding> all, float margin)
        {
            foreach (ScatteredBuilding house in all)
            {
                if (house == null) continue;
                Vector3 local = Quaternion.Euler(0f, -house.transform.eulerAngles.y, 0f) * (point - house.transform.position);
                if (Mathf.Abs(local.x) <= house.footprint.x * 0.5f + margin && Mathf.Abs(local.z) <= house.footprint.y * 0.5f + margin) return true;
            }
            return false;
        }

        private static Vector3 Bezier(Vector3 a, Vector3 b, Vector3 c, Vector3 d, float t)
        {
            float u = 1f - t;
            return u * u * u * a + 3f * u * u * t * b + 3f * u * t * t * c + t * t * t * d;
        }

        private static Vector3 Flat(Vector3 v) => new Vector3(v.x, 0f, v.z);
    }
}
#endif
