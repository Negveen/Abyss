#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace YaraJ.CastleVillageBuilder
{
    /// <summary>
    /// Editor only. Builds the Castle Features tab's pieces, each as one solid mesh in the kit's stone (so the colour
    /// themes recolour them): upper areas (raised terraces on retaining walls, with grand stairs, balustrades, lamps and
    /// planters), grand stairs, stone arch or wooden bridges, dressed streets (lamp posts, banners, hedges, cypresses,
    /// benches), round plazas with a fountain, and a great keep.
    /// </summary>
    internal static partial class CastleFeatures
    {
        internal const string ContainerName = "Castle Features";

        internal enum Tool { UpperArea, Stairs, Bridge, Street, Plaza, Keep }
        internal enum Edge { Balustrade, Battlements, Hedge, Plain }
        internal enum StairSides { None, Front, FrontAndBack, AllSides }
        internal enum Rail { Balustrade, Wall, None }
        internal enum BridgeStyle { StoneArch, Wooden }
        internal enum Foot { Straight, Round, Flared }
        internal enum StairShape { Straight, Curved, DoubleCurved, Freehand }

        internal sealed class Settings
        {
            // Upper area
            public float TerraceHeight = 4f;
            public Edge TerraceEdge = Edge.Balustrade;
            public StairSides TerraceStairs = StairSides.Front;
            public bool TerraceLamps = true, TerracePlanters = true;
            public bool Freehand = true;          // draw its outline (or drag a rectangle)
            public bool LockToWalls = true;       // the outline locks onto castle walls and towers near the mouse
            public float LockReach = 3f;          // how near (m, from the wall's face)
            // Stairs (also the upper area's)
            public float StairWidth = 4f;
            public Rail StairRail = Rail.Balustrade;
            public Foot StairFoot = Foot.Round;   // how the stairs start: straight, a round fan, or flared and bowed
            public bool Landings = true, StairLamps = true;
            public StairShape Stairs = StairShape.Straight; // straight, curved, double curved (a horseshoe), or drawn freehand
            public float StairTurn = 90f;         // curved: how far round they sweep (degrees)
            public float DoubleTurn = 140f;       // double curved: how far round each flight sweeps (wide, like a horseshoe)
            public bool DoubleInward;             // double curved: the flights curve towards each other going down (their feet meet, their tops apart)
            public bool CurveRight = true;        // curved: which way they sweep (going up)
            public bool ConnectStairs = true;     // stairs ending near an upper area's edge snap onto it, square, and open its railing
            // Bridge
            public BridgeStyle Bridge = BridgeStyle.StoneArch;
            public float BridgeWidth = 4f;
            public Rail BridgeRail = Rail.Wall;
            public float ArchSpan = 12f;
            public bool BridgeLamps = true;
            // Street
            public bool Lamps = true, Banners = true, Hedges = true, Trees = true, Benches = true;
            public float Spacing = 9f, StreetHalf = 3f;
            public bool BothSides = true;
            // Plaza
            public bool Fountain = true, PlazaLamps = true;
            public bool PlazaFreehand;            // draw its outline (or drag out a circle)
            public int PlazaSteps = 2;
            // Keep
            public int KeepCells = 6, KeepStoreys = 7;
            public CastleWallBuilder.TowerStyle KeepStyle = CastleWallBuilder.TowerStyle.Turreted;
            // Shared
            public bool Lights;   // real lights in the lanterns (costly: use a few)
            public Color BannerColour = new Color(0.55f, 0.08f, 0.1f), BannerTrim = new Color(0.9f, 0.72f, 0.25f);
            public string Theme;
        }

        /// <summary>The ground (or a walkable feature) height at a point, null if there's none.</summary>
        internal delegate float? SurfaceAt(Vector3 world);

        private const float Riser = 0.17f, Footing = 1.5f;

        // ================================================================ Upper area

        /// <summary>
        /// A raised paved area on stone retaining walls over a rectangle (two opposite corners, turned 'yaw'), 'height'
        /// above the highest ground under it, with grand stairs up on the sides asked for ('front' is the side facing
        /// 'viewer'), its edge finished as asked, and lamps and planters.
        /// </summary>
        internal static GameObject UpperArea(Vector3 a, Vector3 b, float yaw, Vector3 viewer, Settings s, SurfaceAt ground, Transform parent)
        {
            Quaternion turn = Quaternion.Euler(0f, yaw, 0f);
            Vector3 d = Quaternion.Inverse(turn) * Flat(b - a);
            float x0 = Mathf.Min(0f, d.x), x1 = Mathf.Max(0f, d.x), z0 = Mathf.Min(0f, d.z), z1 = Mathf.Max(0f, d.z);
            if (x1 - x0 < 4f || z1 - z0 < 4f) return null;
            Vector3 Corner(float x, float z) => Flat(a) + turn * new Vector3(x, 0f, z);
            Vector3 centre = Corner((x0 + x1) * 0.5f, (z0 + z1) * 0.5f);
            Vector3 right = turn * Vector3.right, forward = turn * Vector3.forward;
            float hx = (x1 - x0) * 0.5f, hz = (z1 - z0) * 0.5f;

            Range(ground, centre, right, forward, hx, hz, out float low, out float high);
            float top = high + Mathf.Max(1f, s.TerraceHeight), bottom = low - Footing;

            var m = new Mesher(centre);
            // The body: retaining walls of stone, paved on top
            m.Block(new Vector3(centre.x, (top + bottom) * 0.5f, centre.z), new Vector3(hx * 2f, top - bottom, hz * 2f), turn, m.Paving, m.Stone, false);
            // A broader plinth along the foot, and a coping round the top
            m.Block(new Vector3(centre.x, (bottom + low + 0.9f) * 0.5f, centre.z), new Vector3(hx * 2f + 0.3f, low + 0.9f - bottom, hz * 2f + 0.3f), turn, m.Trim, m.Stone, false);
            float coping = 0.28f;
            foreach (var (mid, along, half, outward) in Sides(centre, right, forward, hx, hz))
                m.Block(Y(mid + outward * 0.05f, top - coping * 0.5f + 0.06f), new Vector3(half * 2f + 0.2f, coping, 0.42f), Quaternion.LookRotation(outward), m.Trim, m.Trim, true);

            // Stairs up, on the sides asked for (the front: the side most towards whoever's looking)
            var sides = Sides(centre, right, forward, hx, hz);
            int front = 0;
            float best = -2f;
            for (int i = 0; i < 4; i++)
            {
                float dot = Vector3.Dot(sides[i].outward, Flat(viewer - centre).normalized);
                if (dot > best) { best = dot; front = i; }
            }
            var stairSides = new List<int>();
            switch (s.TerraceStairs)
            {
                case StairSides.Front: stairSides.Add(front); break;
                case StairSides.FrontAndBack: stairSides.Add(front); stairSides.Add((front + 2) % 4); break;
                case StairSides.AllSides: for (int i = 0; i < 4; i++) stairSides.Add(i); break;
            }
            float stairWidth = s.StairWidth;
            var lights = new List<Vector3>();
            var edges = new List<(Vector3 p, Vector3 q, Vector3 outward, List<Vector2> gaps)>();
            for (int i = 0; i < 4; i++)
            {
                var (mid, along, half, outward) = sides[i];
                bool stairs = stairSides.Contains(i) && half * 2f >= stairWidth + 2f;
                float gap = stairs ? stairWidth * 0.5f + 0.5f : 0f;
                Vector3 edge = Y(mid - outward * 0.2f, top);
                // The edge (its own piece, so stairs added later can open it), with an opening for these stairs
                var gaps = new List<Vector2>();
                if (gap > 0f) gaps.Add(new Vector2(half - gap, half + gap));
                edges.Add((edge - along * half, edge + along * half, outward, gaps));
                if (gap > 0f)
                {
                    Vector3 topOfStairs = Y(mid, top);
                    float run = StepsFor(top - (ground(mid + outward * 4f) ?? low)) * 0.34f;
                    float baseY = ground(mid + outward * run) ?? low;
                    Flight(m, Y(mid + outward * run, baseY), topOfStairs, stairWidth, s.StairRail, s.StairFoot, s.Landings, ground, lights, s.StairLamps, s);
                }
            }

            // Lamps at the corners, planters along the edges
            if (s.TerraceLamps)
            {
                foreach (Vector2 c in new[] { new Vector2(-1, -1), new Vector2(1, -1), new Vector2(1, 1), new Vector2(-1, 1) })
                {
                    Vector3 p = Y(centre + right * (c.x * (hx - 0.7f)) + forward * (c.y * (hz - 0.7f)), top);
                    lights.Add(LampPost(m, p, 3.4f));
                }
            }

            GameObject built = m.Build("Upper Area", parent);
            AddLights(built, lights, s);
            foreach (var e in edges) BuildEdge(built.transform, e.p, e.q, e.outward, s.TerraceEdge, s.TerracePlanters, e.gaps, s.Theme);
            return Finish(built, CastleFeature.Kind.UpperArea, true, a, b, yaw, s);
        }

        /// <summary>
        /// A raised paved area over any drawn outline (a closed shape, in the order drawn). The sides marked 'locked' run
        /// against a castle wall: they get no edge, coping, stairs or lamps (the wall is their edge), and tuck a little into
        /// its stone. The rest are finished like an upper area's, with grand stairs on the sides asked for.
        /// </summary>
        internal static GameObject UpperAreaOutline(List<Vector3> drawn, List<bool> drawnLocked, Vector3 viewer, Settings s, SurfaceAt ground, Transform parent)
        {
            if (drawn == null || drawn.Count < 3) return null;
            Simplify(drawn, drawnLocked, out List<Vector3> pts, out List<bool> locked);
            if (pts.Count < 3) return null;
            if (SignedArea(pts) < 0f) { pts.Reverse(); locked.Reverse(); }
            float area = SignedArea(pts);
            if (area < 12f) return null;
            int n = pts.Count;
            // (a side is against the wall when both its ends are)
            var against = new bool[n];
            for (int i = 0; i < n; i++) against[i] = locked[i] && locked[(i + 1) % n];

            // How high: the given height over the highest ground inside it
            float low = float.MaxValue, high = float.MinValue;
            Vector3 lo = pts[0], hi = pts[0];
            foreach (Vector3 p in pts) { lo = Vector3.Min(lo, p); hi = Vector3.Max(hi, p); }
            void Sample(Vector3 p) { float? g = ground(p); if (g.HasValue) { low = Mathf.Min(low, g.Value); high = Mathf.Max(high, g.Value); } }
            foreach (Vector3 p in pts) Sample(p);
            float stepX = Mathf.Max(1.5f, (hi.x - lo.x) / 24f), stepZ = Mathf.Max(1.5f, (hi.z - lo.z) / 24f);
            for (float x = lo.x; x <= hi.x; x += stepX)
                for (float z = lo.z; z <= hi.z; z += stepZ)
                    if (Inside(pts, new Vector3(x, 0f, z))) Sample(new Vector3(x, 0f, z));
            if (low == float.MaxValue) return null;
            float top = high + Mathf.Max(1f, s.TerraceHeight), bottom = low - Footing;

            Vector3 centre = Vector3.zero;
            foreach (Vector3 p in pts) centre += Flat(p);
            centre /= n;
            var m = new Mesher(centre);
            var lights = new List<Vector3>();

            // The paved top, and the retaining walls round it
            foreach (int[] t in Triangulate(pts)) m.Triangle(Y(pts[t[0]], top), Y(pts[t[1]], top), Y(pts[t[2]], top), Vector3.up, m.Paving);
            var outward = new Vector3[n];
            for (int i = 0; i < n; i++)
            {
                Vector3 a = Flat(pts[i]), b = Flat(pts[(i + 1) % n]);
                Vector3 d = (b - a).normalized;
                outward[i] = new Vector3(d.z, 0f, -d.x);
                m.Face(Y(a, bottom), Y(b, bottom), Y(b, top), Y(a, top), outward[i], m.Stone);
                if (against[i]) continue;
                float length = Vector3.Distance(a, b);
                Quaternion turn = Quaternion.LookRotation(d);
                Vector3 mid = (a + b) * 0.5f;
                m.Block(Y(mid + outward[i] * 0.08f, (bottom + low + 0.9f) * 0.5f), new Vector3(0.4f, low + 0.9f - bottom, length + 0.2f), turn, m.Trim, m.Stone, false);
                m.Block(Y(mid + outward[i] * 0.05f, top - 0.08f), new Vector3(0.42f, 0.28f, length + 0.2f), turn, m.Trim, m.Trim, true);
            }

            // Stairs on the free sides that are long enough: the one most towards whoever's looking, and as asked
            var free = new List<int>();
            for (int i = 0; i < n; i++)
                if (!against[i] && Vector3.Distance(Flat(pts[i]), Flat(pts[(i + 1) % n])) >= s.StairWidth + 2f) free.Add(i);
            var stairSides = new List<int>();
            if (free.Count > 0 && s.TerraceStairs != StairSides.None)
            {
                Vector3 look = Flat(viewer - centre).normalized;
                free.Sort((x, y) => Vector3.Dot(outward[y], look).CompareTo(Vector3.Dot(outward[x], look)));
                stairSides.Add(free[0]);
                if (s.TerraceStairs == StairSides.FrontAndBack && free.Count > 1) stairSides.Add(free[free.Count - 1]);
                if (s.TerraceStairs == StairSides.AllSides)
                    foreach (int i in free) if (!stairSides.Contains(i) && stairSides.Count < 6) stairSides.Add(i);
            }

            var freeEdges = new List<(Vector3 p, Vector3 q, Vector3 outward, List<Vector2> gaps)>();
            for (int i = 0; i < n; i++)
            {
                if (against[i]) continue;
                Vector3 a = Flat(pts[i]), b = Flat(pts[(i + 1) % n]);
                Vector3 along = (b - a).normalized;
                float half = Vector3.Distance(a, b) * 0.5f;
                Vector3 mid = (a + b) * 0.5f;
                Vector3 edge = Y(mid - outward[i] * 0.2f, top);
                // (the ends stop short where the next side is against the wall, so the balustrade doesn't run into it)
                float startCut = locked[i] ? 0.4f : 0f, endCut = locked[(i + 1) % n] ? 0.4f : 0f;
                if (stairSides.Contains(i))
                {
                    float gap = s.StairWidth * 0.5f + 0.5f;
                    float from0 = half - startCut;
                    freeEdges.Add((edge - along * from0, edge + along * (half - endCut), outward[i], new List<Vector2> { new Vector2(from0 - gap, from0 + gap) }));
                    float run = StepsFor(top - (ground(mid + outward[i] * 4f) ?? low)) * 0.34f;
                    float baseY = ground(mid + outward[i] * run) ?? low;
                    Flight(m, Y(mid + outward[i] * run, baseY), Y(mid, top), s.StairWidth, s.StairRail, s.StairFoot, s.Landings, ground, lights, s.StairLamps, s);
                }
                else freeEdges.Add((edge - along * (half - startCut), edge + along * (half - endCut), outward[i], new List<Vector2>()));
            }

            // Lamps at the outer corners (where two free sides meet and the outline turns outward)
            if (s.TerraceLamps)
            {
                for (int i = 0; i < n; i++)
                {
                    int prev = (i + n - 1) % n;
                    if (against[i] || against[prev]) continue;
                    Vector3 dIn = Flat(pts[i] - pts[prev]).normalized, dOut = Flat(pts[(i + 1) % n] - pts[i]).normalized;
                    if (Vector3.Cross(dIn, dOut).y > -0.25f) continue;      // (only corners that turn sharply outward)
                    Vector3 inward = -(outward[i] + outward[prev]).normalized;
                    lights.Add(LampPost(m, Y(Flat(pts[i]) + inward * 0.9f, top), 3.4f));
                }
            }

            GameObject built = m.Build("Upper Area", parent);
            AddLights(built, lights, s);
            foreach (var e in freeEdges) BuildEdge(built.transform, e.p, e.q, e.outward, s.TerraceEdge, s.TerracePlanters, e.gaps, s.Theme);
            return Finish(built, CastleFeature.Kind.UpperArea, true, pts[0], pts[n - 1], 0f, s);
        }

        // A drawn outline made tidy: free runs straightened (only real bends kept), runs along a wall kept every couple
        // of metres (so they follow its curve), the last point dropped if it's back at the first
        private static void Simplify(List<Vector3> points, List<bool> locks, out List<Vector3> outPoints, out List<bool> outLocks)
        {
            outPoints = new List<Vector3>();
            outLocks = new List<bool>();
            int count = points.Count;
            if (count > 2 && Flat(points[count - 1] - points[0]).magnitude < 1.5f) count--;
            int start = 0;
            while (start < count)
            {
                bool l = locks[start];
                int end = start;
                while (end + 1 < count && locks[end + 1] == l) end++;
                var run = points.GetRange(start, end - start + 1);
                List<Vector3> kept;
                if (l)
                {
                    kept = new List<Vector3> { run[0] };
                    for (int i = 1; i < run.Count; i++)
                        if (Flat(run[i] - kept[kept.Count - 1]).magnitude >= 2f || i == run.Count - 1) kept.Add(run[i]);
                }
                else kept = Douglas(run, 0.45f);
                foreach (Vector3 p in kept)
                {
                    if (outPoints.Count > 0 && Flat(p - outPoints[outPoints.Count - 1]).magnitude < 0.6f) continue;
                    outPoints.Add(p);
                    outLocks.Add(l);
                }
                start = end + 1;
            }
        }

        private static List<Vector3> Douglas(List<Vector3> run, float tolerance)
        {
            if (run.Count < 3) return new List<Vector3>(run);
            Vector3 a = Flat(run[0]), b = Flat(run[run.Count - 1]);
            int far = 0;
            float most = 0f;
            for (int i = 1; i < run.Count - 1; i++)
            {
                Vector3 p = Flat(run[i]);
                Vector3 ab = b - a;
                float t = ab.sqrMagnitude > 1e-6f ? Mathf.Clamp01(Vector3.Dot(p - a, ab) / ab.sqrMagnitude) : 0f;
                float d = Vector3.Distance(p, a + ab * t);
                if (d > most) { most = d; far = i; }
            }
            if (most <= tolerance) return new List<Vector3> { run[0], run[run.Count - 1] };
            List<Vector3> left = Douglas(run.GetRange(0, far + 1), tolerance), right = Douglas(run.GetRange(far, run.Count - far), tolerance);
            left.RemoveAt(left.Count - 1);
            left.AddRange(right);
            return left;
        }

        // The area the outline encloses, positive when it runs anticlockwise seen from above
        private static float SignedArea(List<Vector3> pts)
        {
            float a = 0f;
            for (int i = 0; i < pts.Count; i++)
            {
                Vector3 p = pts[i], q = pts[(i + 1) % pts.Count];
                a += p.x * q.z - q.x * p.z;
            }
            return a * 0.5f;
        }

        private static bool Inside(List<Vector3> pts, Vector3 p)
        {
            bool inside = false;
            for (int i = 0, j = pts.Count - 1; i < pts.Count; j = i++)
            {
                if ((pts[i].z > p.z) != (pts[j].z > p.z) &&
                    p.x < (pts[j].x - pts[i].x) * (p.z - pts[i].z) / (pts[j].z - pts[i].z) + pts[i].x) inside = !inside;
            }
            return inside;
        }

        // The outline cut into triangles (ear by ear; it can be any shape that doesn't cross itself)
        private static List<int[]> Triangulate(List<Vector3> pts)
        {
            var result = new List<int[]>();
            var left = new List<int>();
            for (int i = 0; i < pts.Count; i++) left.Add(i);
            int guard = pts.Count * pts.Count;
            while (left.Count > 3 && guard-- > 0)
            {
                bool clipped = false;
                for (int k = 0; k < left.Count; k++)
                {
                    int ia = left[(k + left.Count - 1) % left.Count], ib = left[k], ic = left[(k + 1) % left.Count];
                    Vector3 a = pts[ia], b = pts[ib], c = pts[ic];
                    if ((b.x - a.x) * (c.z - a.z) - (b.z - a.z) * (c.x - a.x) <= 1e-5f) continue;   // (a hollow corner: not an ear)
                    bool empty = true;
                    foreach (int other in left)
                    {
                        if (other == ia || other == ib || other == ic) continue;
                        if (InTriangle(pts[other], a, b, c)) { empty = false; break; }
                    }
                    if (!empty) continue;
                    result.Add(new[] { ia, ib, ic });
                    left.RemoveAt(k);
                    clipped = true;
                    break;
                }
                if (!clipped) break;
            }
            if (left.Count == 3) result.Add(new[] { left[0], left[1], left[2] });
            else for (int k = 1; k + 1 < left.Count; k++) result.Add(new[] { left[0], left[k], left[k + 1] });   // (a tangled outline: a fan)
            return result;
        }

        private static bool InTriangle(Vector3 p, Vector3 a, Vector3 b, Vector3 c)
        {
            float Side(Vector3 u, Vector3 v, Vector3 w) => (v.x - u.x) * (w.z - u.z) - (v.z - u.z) * (w.x - u.x);
            float d1 = Side(a, b, p), d2 = Side(b, c, p), d3 = Side(c, a, p);
            return d1 >= 0f && d2 >= 0f && d3 >= 0f;
        }

        // ================================================================ Stairs

        /// <summary>Grand stairs from 'bottom' up to 'top' (the top step meets 'top'), straight.</summary>
        internal static GameObject Stairs(Vector3 bottom, Vector3 top, Settings s, SurfaceAt ground, Transform parent, out List<Vector3> line)
        {
            line = null;
            if (top.y < bottom.y) { Vector3 t = top; top = bottom; bottom = t; }
            if (top.y - bottom.y < 0.4f || Flat(top - bottom).magnitude < 1f) return null;
            var m = new Mesher(Flat((top + bottom) * 0.5f));
            var lights = new List<Vector3>();
            line = Flight(m, bottom, top, s.StairWidth, s.StairRail, s.StairFoot, s.Landings, ground, lights, s.StairLamps, s);
            GameObject built = m.Build("Grand Stairs", parent);
            AddLights(built, lights, s);
            return Finish(built, CastleFeature.Kind.Stairs, false, bottom, top, 0f, s);
        }

        /// <summary>
        /// Grand stairs along a drawn path (any shape: they curve as they climb), from its lower end up to its higher one,
        /// the steps spread evenly along it. Null (with why) if it's too flat or too short for the climb.
        /// </summary>
        internal static GameObject StairsAlong(List<Vector3> drawn, Settings s, SurfaceAt ground, Transform parent, out string why, out List<Vector3> path, out float topY)
        {
            why = null; path = null; topY = 0f;
            if (drawn == null || drawn.Count < 2) { why = "Draw a longer path."; return null; }
            var line = Chaikin(Douglas(drawn, 0.3f), 2, false);
            float yA = drawn[0].y, yB = drawn[drawn.Count - 1].y;
            if (yA > yB) { line.Reverse(); float t = yA; yA = yB; yB = t; }
            if (yB - yA < 0.4f) { why = "Stairs need to climb: end the path higher up than it starts (on a terrace, a wall walk, a hill...)."; return null; }
            var m = new Mesher(Flat(line[line.Count / 2]));
            var lights = new List<Vector3>();
            if (!StepsAlong(m, line, yA, yB, s.StairWidth, s.StairRail, s.StairFoot, s.Landings, ground, lights, s.StairLamps, out why)) return null;
            path = line; topY = yB;
            GameObject built = m.Build("Grand Stairs", parent);
            AddLights(built, lights, s);
            return Finish(built, CastleFeature.Kind.Stairs, false, Y(line[0], yA), Y(line[line.Count - 1], yB), 0f, s);
        }

        /// <summary>
        /// Curved grand stairs arriving at 'top', climbing towards 'climb' (flat) as they get there: one flight sweeping
        /// round to one side, or two mirrored flights sweeping out either way and meeting at the top (a horseshoe). They're
        /// as long as the climb needs; 'lines' are their paths (foot first), for opening railings.
        /// </summary>
        internal static GameObject CurvedStairs(Vector3 top, Vector3 climb, float bottomGuess, bool twin, Settings s, SurfaceAt ground, Transform parent,
                                                out List<List<Vector3>> lines, out string why)
        {
            lines = new List<List<Vector3>>();
            why = null;
            climb = Flat(climb).normalized;
            if (climb.sqrMagnitude < 0.5f) { why = "Drag in the direction the stairs should climb."; return null; }
            if (top.y - bottomGuess < 0.4f) { why = "Stairs need to climb: end higher up than you start (on a terrace, a wall walk, a hill...)."; return null; }
            Vector3 across = Vector3.Cross(Vector3.up, climb);
            var m = new Mesher(Flat(top));
            var lights = new List<Vector3>();
            // Each flight: which side of the middle it's on (0 for a single one) and which way it bends going down
            var flights = twin ? new[] { 1, -1 } : new[] { 0 };
            foreach (int k in flights)
            {
                int side = !twin ? (s.CurveRight ? 1 : -1) : s.DoubleInward ? -k : k;
                float apart = s.StairWidth * 0.5f + 0.5f;   // (twin flights side by side where they're closest, their inner walls touching)
                // How long: worked out from the climb (the ground at its foot read again once the foot's known)
                float yBottom = bottomGuess;
                List<Vector3> path = null;
                for (int pass = 0; pass < 3; pass++)
                {
                    int steps = StepsFor(top.y - yBottom);
                    int straight = steps - FootSteps(s.StairFoot, steps);
                    int landingCount = s.Landings ? Mathf.Max(0, (straight - 1) / PerLanding) : 0;
                    float length = straight * 0.34f + landingCount * LandingLength;
                    // (the sweep kept to a radius at least about 0.6 of their width, so the inside of the bend stays a proper step)
                    float bendable = Mathf.Max(0.5f, length - 2.5f);
                    float widest = bendable / (0.6f * Mathf.Max(1f, s.StairWidth)) * Mathf.Rad2Deg;
                    float turn = Mathf.Clamp(Mathf.Min(twin ? s.DoubleTurn : s.StairTurn, widest), 0f, twin && s.DoubleInward ? 120f : 270f);
                    path = Arc(Flat(top), climb, side, length, turn);
                    if (twin)
                    {
                        // Outward: their tops side by side. Inward: their feet side by side (so their tops are as far apart as they bend)
                        float drift = Vector3.Dot(Flat(path[0] - path[path.Count - 1]), across);
                        float shift = s.DoubleInward ? k * apart - drift : k * apart;
                        for (int i = 0; i < path.Count; i++) path[i] += across * shift;
                    }
                    yBottom = ground(path[0]) ?? yBottom;
                    if (top.y - yBottom < 0.4f) break;
                }
                if (top.y - yBottom < 0.4f) continue;
                if (StepsAlong(m, path, yBottom, top.y, s.StairWidth, s.StairRail, s.StairFoot, s.Landings, ground, lights, s.StairLamps, out why)) lines.Add(path);
            }
            if (lines.Count == 0) { why = why ?? "Too little climb here for stairs."; return null; }
            GameObject built = m.Build(twin ? "Double Curved Stairs" : "Curved Stairs", parent);
            AddLights(built, lights, s);
            return Finish(built, CastleFeature.Kind.Stairs, false, Y(lines[0][0], bottomGuess), top, 0f, s);
        }

        /// <summary>How wide double curved stairs are where they arrive (both flights): wider when they curve inward.</summary>
        internal static float DoubleWidth(Settings s, float rise)
        {
            float apart = s.StairWidth * 0.5f + 0.5f;
            if (!s.DoubleInward) return (apart + s.StairWidth * 0.5f) * 2f;
            float length = StepsFor(Mathf.Max(0.4f, rise)) * 0.34f, bendable = Mathf.Max(0.5f, length - 2.5f);
            float turn = Mathf.Min(s.DoubleTurn, 120f, bendable / (0.6f * Mathf.Max(1f, s.StairWidth)) * Mathf.Rad2Deg) * Mathf.Deg2Rad;
            float drift = turn > 0.01f ? bendable / turn * (1f - Mathf.Cos(Mathf.Min(turn, Mathf.PI * 0.5f))) + (turn > Mathf.PI * 0.5f ? bendable / turn * Mathf.Sin(turn - Mathf.PI * 0.5f) : 0f) : 0f;
            return (apart + drift + s.StairWidth * 0.5f) * 2f;
        }

        // A path that arrives at 'end' heading 'climb', sweeping round 'turn' degrees over 'length' metres (bending
        // towards 'side' of the way up as you go down), foot first
        private static List<Vector3> Arc(Vector3 end, Vector3 climb, int side, float length, float turn)
        {
            const float Step = 0.25f;
            int count = Mathf.Max(2, Mathf.CeilToInt(length / Step));
            // (the last stretch, a stair's width or so, runs straight in, so the top meets its edge square)
            int straightIn = Mathf.Min(count / 3, Mathf.CeilToInt(2.5f / Step));
            var back = new List<Vector3> { Flat(end) };
            Vector3 heading = -climb, at = Flat(end);
            float turnPer = turn / Mathf.Max(1, count - straightIn);
            for (int i = 0; i < count; i++)
            {
                if (i >= straightIn) heading = Quaternion.AngleAxis(-side * turnPer, Vector3.up) * heading;
                at += heading * (length / count);
                back.Add(at);
            }
            back.Reverse();
            return back;
        }

        /// <summary>
        /// Where stairs ending near 'point' would connect to an upper area: the nearest edge within 'reach', the spot on it
        /// (kept clear of its corners, at its height) and which way is out. False if there's none near.
        /// </summary>
        internal static bool SnapToEdge(Vector3 point, float width, float reach, out Vector3 top, out Vector3 outward)
        {
            top = point; outward = Vector3.forward;
            float best = reach;
            bool found = false;
            foreach (CastleFeatureEdge edge in Object.FindObjectsByType<CastleFeatureEdge>(FindObjectsSortMode.None))
            {
                if (edge == null) continue;
                float edgeY = edge.from.y;
                if (point.y > edgeY + 1.5f || point.y < edgeY - 12f) continue;
                Vector3 p = Flat(edge.from), q = Flat(edge.to);
                float length = Vector3.Distance(p, q);
                if (length < 1f) continue;
                float d = DistanceToSegment(point, p, q);
                if (d > best) continue;
                Vector3 dir = (q - p) / length;
                float margin = width * 0.5f + 0.6f;
                float t = length > margin * 2f ? Mathf.Clamp(Vector3.Dot(Flat(point) - p, dir), margin, length - margin) : length * 0.5f;
                best = d;
                top = Y(p + dir * t, edgeY);
                outward = Flat(edge.outward).normalized;
                found = true;
            }
            return found;
        }

        /// <summary>
        /// Whether a point is on (or by) an upper area built before its edges were kept as their own pieces: stairs can't
        /// connect to it (its railing is part of it) until it's built again.
        /// </summary>
        internal static bool OnOlderUpperArea(Vector3 point)
        {
            foreach (RaycastHit h in Physics.SphereCastAll(new Ray(point + Vector3.up * 3f, Vector3.down), 2.5f, 6f, ~0, QueryTriggerInteraction.Ignore))
            {
                CastleFeature f = h.collider.GetComponentInParent<CastleFeature>();
                if (f != null && f.kind == CastleFeature.Kind.UpperArea && f.GetComponentInChildren<CastleFeatureEdge>() == null) return true;
            }
            return false;
        }

        /// <summary>A drawn stair path whose high end is near an upper area's edge, made to arrive at it square (or as drawn, if none).</summary>
        internal static List<Vector3> ConnectPath(List<Vector3> path, float width)
        {
            if (path == null || path.Count < 2) return path;
            var result = new List<Vector3>(path);
            bool topAtEnd = result[result.Count - 1].y >= result[0].y;
            if (!topAtEnd) result.Reverse();
            if (!SnapToEdge(result[result.Count - 1], width, 3f, out Vector3 top, out Vector3 outward)) return path;
            // Drop the end of the path near the edge (and over the upper area), then bend smoothly from the way it was going
            // into a square arrival: a gentle curve as long as the stairs are wide, straight in over the last metres
            float lead = Mathf.Max(3f, width * 1.1f);
            while (result.Count > 2 && (Vector3.Dot(Flat(result[result.Count - 1] - top), outward) < lead * 0.6f
                                        || Flat(result[result.Count - 1] - top).magnitude < lead)) result.RemoveAt(result.Count - 1);
            Vector3 p0 = Flat(result[result.Count - 1]), p3 = Flat(top);
            Vector3 heading = Flat(result[result.Count - 1] - result[Mathf.Max(0, result.Count - 2)]);
            heading = heading.sqrMagnitude > 1e-4f ? heading.normalized : (p3 - p0).normalized;
            float span = Vector3.Distance(p0, p3);
            Vector3 p1 = p0 + heading * (span * 0.4f), p2 = p3 + outward * Mathf.Max(span * 0.45f, Mathf.Min(2.5f, span * 0.5f));
            float y0 = result[result.Count - 1].y;
            const int Pieces = 16;
            for (int i = 1; i < Pieces; i++)
            {
                float t = (float)i / Pieces, u = 1f - t;
                Vector3 p = u * u * u * p0 + 3f * u * u * t * p1 + 3f * u * t * t * p2 + t * t * t * p3;
                result.Add(Y(p, y0));
            }
            result.Add(top);
            if (!topAtEnd) result.Reverse();
            return result;
        }

        private static int StepsFor(float rise) => Mathf.Max(1, Mathf.CeilToInt(rise / Riser));

        private const int PerLanding = 12;
        private const float LandingLength = 1.5f, MinTread = 0.27f, FootRing = 0.5f;

        // How many steps of the climb are the curved start (the rest go along the line)
        private static int FootSteps(Foot foot, int steps) => foot == Foot.Straight ? 0 : Mathf.Clamp(steps - 1, 0, 3);

        // A straight flight up to 'top' from towards 'bottom': as long as the climb needs (an easy step), ending at 'top'
        private static List<Vector3> Flight(Mesher m, Vector3 bottom, Vector3 top, float width, Rail rail, Foot foot, bool landings,
                                   SurfaceAt ground, List<Vector3> lights, bool lamps, Settings s)
        {
            Vector3 dir = Flat(top - bottom).normalized;
            if (dir.sqrMagnitude < 0.5f) dir = Vector3.forward;
            float rise = top.y - bottom.y;
            int steps = StepsFor(rise);
            int straight = steps - FootSteps(foot, steps);
            int landingCount = landings ? Mathf.Max(0, (straight - 1) / PerLanding) : 0;
            float tread = Mathf.Clamp((Flat(top - bottom).magnitude - landingCount * LandingLength) / Mathf.Max(1, straight), 0.3f, 0.42f);
            float length = straight * tread + landingCount * LandingLength;
            var line = new List<Vector3> { Flat(top) - dir * length, Flat(top) };
            StepsAlong(m, line, bottom.y, top.y, width, rail, foot, landings, ground, lights, lamps, out _);
            return line;
        }

        // Steps up along a line (flat points, its foot first) from 'yBottom' to 'yTop': solid steps paved on top, landings
        // every so many, side walls (or low walls with balustrades) following them, the curved start in front of the foot,
        // and lamps either side of it. False (with why) if the line's too short for the climb.
        private static bool StepsAlong(Mesher m, List<Vector3> line, float yBottom, float yTop, float width, Rail rail, Foot foot,
                                       bool landings, SurfaceAt ground, List<Vector3> lights, bool lamps, out string why)
        {
            why = null;
            var stations = new List<float> { 0f };
            for (int i = 1; i < line.Count; i++) stations.Add(stations[i - 1] + Flat(line[i] - line[i - 1]).magnitude);
            float total = stations[stations.Count - 1];
            Vector3 At(float d, out Vector3 tangent)
            {
                d = Mathf.Clamp(d, 0f, total);
                for (int i = 1; i < line.Count; i++)
                {
                    if (d > stations[i] && i < line.Count - 1) continue;
                    float seg = stations[i] - stations[i - 1];
                    tangent = seg > 1e-4f ? Flat(line[i] - line[i - 1]) / seg : Vector3.forward;
                    return Flat(line[i - 1]) + tangent * (d - stations[i - 1]);
                }
                tangent = Vector3.forward;
                return Flat(line[0]);
            }

            float rise = yTop - yBottom;
            int steps = StepsFor(rise);
            float riser = rise / steps;
            int footSteps = FootSteps(foot, steps);
            int straight = steps - footSteps;
            int landingCount = landings ? Mathf.Max(0, (straight - 1) / PerLanding) : 0;
            float tread = (total - landingCount * LandingLength) / Mathf.Max(1, straight);
            if (tread < MinTread && landingCount > 0) { landingCount = 0; tread = total / Mathf.Max(1, straight); }
            if (tread < MinTread)
            {
                why = $"Too steep for that length: a {rise:F1} m climb needs about {straight * MinTread:F0} m of stairs. Draw a longer (or winding) path.";
                return false;
            }
            float half = width * 0.5f;

            // Their sides (and their walls') along the line, worked out all the way along so that on a bend tighter than
            // they're wide the inside never folds back on itself: it pinches in instead, like a spiral stair's
            const float Sample = 0.25f;
            int samples = Mathf.Max(2, Mathf.CeilToInt(total / Sample) + 1);
            var centres = new Vector3[samples];
            var tangents = new Vector3[samples];
            for (int i = 0; i < samples; i++) centres[i] = At(total * i / (samples - 1), out tangents[i]);
            var sideLines = new Dictionary<float, Vector3[]>();
            Vector3 Edge(float d, float off)
            {
                if (!sideLines.TryGetValue(off, out Vector3[] pts))
                {
                    pts = new Vector3[samples];
                    for (int i = 0; i < samples; i++)
                    {
                        Vector3 p = centres[i] + Vector3.Cross(Vector3.up, tangents[i]) * off;
                        if (i > 0 && Vector3.Dot(p - pts[i - 1], tangents[i]) < 0.005f) p = pts[i - 1];
                        pts[i] = p;
                    }
                    sideLines[off] = pts;
                }
                float f = total > 0f ? Mathf.Clamp01(d / total) * (samples - 1) : 0f;
                int j = Mathf.Min(samples - 2, Mathf.FloorToInt(f));
                return Vector3.Lerp(pts[j], pts[j + 1], f - j);
            }

            // How deep they reach: under the lowest ground along them
            float low = yBottom;
            for (float d = 0f; d <= total; d += 1.5f)
            {
                Vector3 p = At(d, out Vector3 t), n = Vector3.Cross(Vector3.up, t);
                for (int k = -1; k <= 1; k++) { float? g = ground(p + n * (k * half)); if (g.HasValue) low = Mathf.Min(low, g.Value); }
            }
            float baseY = low - Footing;

            // The steps: each a solid slab, paved on top, its riser and sides in stone (curved ones in short pieces)
            float along = 0f;
            for (int i = 0; i < straight; i++)
            {
                float y = yBottom + (footSteps + i + 1) * riser;
                bool landing = landings && landingCount > 0 && (i + 1) % PerLanding == 0 && i + 1 < straight;
                float len = tread + (landing ? LandingLength : 0f);
                StepSlab(m, At, Edge, along, along + len, y, baseY, half, i == straight - 1);
                along += len;
            }

            // The curved start, in front of the foot
            Vector3 foot0 = At(0f, out Vector3 dir0), across0 = Vector3.Cross(Vector3.up, dir0);
            float footReach = 0f, footWide = 0f;
            for (int k = 0; k < footSteps; k++)
            {
                float y = yBottom + (footSteps - k) * riser;
                if (foot == Foot.Round)
                {
                    float r = half + (k + 1) * FootRing;
                    m.HalfDisc(Y(foot0, y), -dir0, r, baseY, y, m.Paving, m.Stone);
                    footReach = Mathf.Max(footReach, r - half * 0.2f); footWide = Mathf.Max(footWide, r - half);
                }
                else
                {
                    // Flared: each lower step wider, its front bowed out and its ends swept round
                    float w = half + (k + 1) * 0.65f, d = (k + 1) * 0.42f, sag = w * 0.16f;
                    var outline = new List<Vector3>();
                    Vector3 P(float x, float z) => foot0 + across0 * x - dir0 * z;
                    outline.Add(P(w, -0.05f));
                    const int Round = 6;
                    for (int j = 0; j <= Round; j++)    // (the right end, swept round to the front)
                    {
                        float a = Mathf.PI * 0.5f * j / Round;
                        outline.Add(P(w - d * 0.6f + Mathf.Cos(a) * d * 0.6f, Mathf.Sin(a) * d));
                    }
                    const int Bow = 14;
                    for (int j = 1; j < Bow; j++)       // (the bowed front)
                    {
                        float x = Mathf.Lerp(w - d * 0.6f, -(w - d * 0.6f), (float)j / Bow);
                        float u = x / (w - d * 0.6f);
                        outline.Add(P(x, d + sag * (1f - u * u)));
                    }
                    for (int j = Round; j >= 0; j--)    // (the left end)
                    {
                        float a = Mathf.PI * 0.5f * j / Round;
                        outline.Add(P(-(w - d * 0.6f) - Mathf.Cos(a) * d * 0.6f, Mathf.Sin(a) * d));
                    }
                    outline.Add(P(-w, -0.05f));
                    m.Prism(outline, baseY, y, m.Paving, m.Stone);
                    footReach = Mathf.Max(footReach, d + sag); footWide = Mathf.Max(footWide, w - half);
                }
            }

            // Side walls with a coping (or a low wall with a balustrade on it), following the steps
            if (rail != Rail.None)
            {
                float wallUp = rail == Rail.Wall ? 0.95f : 0.3f;
                float yFirst = yBottom + (footSteps + 1) * riser;
                float RailY(float d) => Mathf.Lerp(yFirst, yTop, total > 0f ? d / total : 1f) + wallUp;
                float piece = line.Count <= 2 ? total : 1.2f;
                float chunk = line.Count <= 2 ? total : 3.6f;
                for (int sgn = -1; sgn <= 1; sgn += 2)
                {
                    float off = sgn * (half + 0.25f);
                    for (float d = 0f; d < total - 0.01f; d += piece)
                    {
                        float e = Mathf.Min(total, d + piece);
                        Vector3 wa = Edge(d, off), wb = Edge(e, off);
                        if (Flat(wb - wa).sqrMagnitude < 0.0004f) continue;   // (pinched in on a tight bend)
                        m.SlopedWall(wa, wb, RailY(d), RailY(e), baseY, 0.5f, m.Stone, m.Trim);
                    }
                    if (rail != Rail.Balustrade) continue;
                    for (float d = 0f; d < total - 0.01f; d += chunk)
                    {
                        float e = Mathf.Min(total, d + chunk);
                        Vector3 ba = Edge(d, off), bb = Edge(e, off);
                        if (Flat(bb - ba).sqrMagnitude < 0.04f) continue;
                        Balustrade(m, Y(ba, RailY(d) + 0.12f), Y(bb, RailY(e) + 0.12f));
                    }
                }
            }

            if (lamps)
            {
                foreach (int sgn in new[] { -1, 1 })
                {
                    Vector3 p = foot0 - dir0 * (footReach * 0.5f + 0.3f) + across0 * (sgn * (half + footWide + 0.9f));
                    float y = ground(p) ?? yBottom;
                    lights.Add(LampPost(m, Y(p, y), 3.4f));
                }
            }
            return true;
        }

        // One step (or landing): a slab from 'd0' to 'd1' along the line, paved on top at 'y', down to 'baseY'
        private delegate Vector3 Along(float d, out Vector3 tangent);
        private static void StepSlab(Mesher m, Along at, System.Func<float, float, Vector3> edge, float d0, float d1, float y, float baseY, float half, bool last)
        {
            int pieces = Mathf.Max(1, Mathf.CeilToInt((d1 - d0) / 0.5f));
            for (int k = 0; k < pieces; k++)
            {
                float a = Mathf.Lerp(d0, d1, (float)k / pieces), b = Mathf.Lerp(d0, d1, (float)(k + 1) / pieces);
                Vector3 pa = at(a, out Vector3 ta), pb = at(b, out Vector3 tb);
                Vector3 na = Vector3.Cross(Vector3.up, ta) * half, nb = Vector3.Cross(Vector3.up, tb) * half;
                Vector3 la = edge(a, -half), ra = edge(a, half), lb = edge(b, -half), rb = edge(b, half);
                m.Face(Y(la, y), Y(ra, y), Y(rb, y), Y(lb, y), Vector3.up, m.Paving);
                m.Face(Y(la, baseY), Y(lb, baseY), Y(lb, y), Y(la, y), -(na + nb), m.Stone);
                m.Face(Y(ra, baseY), Y(ra, y), Y(rb, y), Y(rb, baseY), na + nb, m.Stone);
                if (k == 0) m.Face(Y(la, baseY), Y(la, y), Y(ra, y), Y(ra, baseY), -ta, m.Stone);
                if (k == pieces - 1 && last) m.Face(Y(lb, baseY), Y(rb, baseY), Y(rb, y), Y(lb, y), tb, m.Stone);
            }
        }

        // A line with its corners rounded off ('closed': it goes round, back to its start)
        private static List<Vector3> Chaikin(List<Vector3> points, int passes, bool closed)
        {
            var result = new List<Vector3>(points);
            for (int p = 0; p < passes && result.Count >= 3; p++)
            {
                var next = new List<Vector3>();
                int n = result.Count, segs = closed ? n : n - 1;
                if (!closed) next.Add(result[0]);
                for (int i = 0; i < segs; i++)
                {
                    Vector3 a = result[i], b = result[(i + 1) % n];
                    next.Add(Vector3.Lerp(a, b, 0.25f));
                    next.Add(Vector3.Lerp(a, b, 0.75f));
                }
                if (!closed) next.Add(result[n - 1]);
                result = next;
            }
            return result;
        }

        // ================================================================ Bridge

        /// <summary>A bridge from 'a' to 'b' (its deck meets both): stone arches on piers, or a wooden trestle.</summary>
        internal static GameObject Bridge(Vector3 a, Vector3 b, Settings s, SurfaceAt ground, Transform parent)
        {
            Vector3 flat = Flat(b - a);
            float length = flat.magnitude;
            if (length < 3f) return null;
            Vector3 dir = flat / length, side = Vector3.Cross(Vector3.up, dir);
            float half = s.BridgeWidth * 0.5f;
            var m = new Mesher(Flat((a + b) * 0.5f));
            var lights = new List<Vector3>();
            float Deck(float x) => Mathf.Lerp(a.y, b.y, x / length);
            Vector3 At(float x) => Flat(a) + dir * x;
            float GroundUnder(float x)
            {
                float lowest = float.MaxValue;
                for (int k = -1; k <= 1; k++) { float? g = ground(At(x) + side * (k * half)); if (g.HasValue) lowest = Mathf.Min(lowest, g.Value); }
                return lowest == float.MaxValue ? Deck(x) - 8f : lowest;
            }

            if (s.Bridge == BridgeStyle.StoneArch)
            {
                // The body: its underside arched between piers (or down into the ground at the ends)
                const float Abutment = 1.5f, Pier = 1.8f, Slab = 0.9f;
                int spans = Mathf.Max(1, Mathf.RoundToInt((length - Abutment * 2f) / Mathf.Max(4f, s.ArchSpan)));
                float spanLength = (length - Abutment * 2f - (spans - 1) * Pier) / spans;
                float Under(float x)
                {
                    float deep = GroundUnder(x) - Footing;
                    if (x <= Abutment || x >= length - Abutment || spanLength < 2f) return Mathf.Min(deep, Deck(x) - Slab);
                    float local = x - Abutment;
                    int j = Mathf.Min(spans - 1, Mathf.FloorToInt(local / (spanLength + Pier)));
                    float inSpan = local - j * (spanLength + Pier);
                    if (inSpan > spanLength) return Mathf.Min(deep, Deck(x) - Slab);   // (a pier)
                    float mid = Abutment + j * (spanLength + Pier) + spanLength * 0.5f;
                    float crown = Deck(mid) - Slab;
                    float room = crown - (GroundUnder(mid) + 0.3f);
                    if (room < 1f) return Deck(x) - Slab;                                   // (too low for an arch: a flat slab)
                    float rise = Mathf.Min(spanLength * 0.5f, room);
                    float u = (inSpan - spanLength * 0.5f) / (spanLength * 0.5f);
                    float arch = crown - rise + rise * Mathf.Sqrt(Mathf.Max(0f, 1f - u * u));
                    return Mathf.Max(deep, arch);
                }
                const float Step = 0.5f;
                int n = Mathf.Max(2, Mathf.CeilToInt(length / Step));
                for (int i = 0; i < n; i++)
                {
                    float xa = length * i / n, xb = length * (i + 1) / n;
                    Vector3 pa = At(xa), pb = At(xb);
                    float ta = Deck(xa), tb = Deck(xb), ua = Under(xa), ub = Under(xb);
                    // Sides
                    m.Face(Y(pa - side * half, ua), Y(pb - side * half, ub), Y(pb - side * half, tb), Y(pa - side * half, ta), -side, m.Stone);
                    m.Face(Y(pa + side * half, ua), Y(pa + side * half, ta), Y(pb + side * half, tb), Y(pb + side * half, ub), side, m.Stone);
                    // Paved top, and the arched underside
                    m.Face(Y(pa - side * half, ta), Y(pb - side * half, tb), Y(pb + side * half, tb), Y(pa + side * half, ta), Vector3.up, m.Paving);
                    m.Face(Y(pa - side * half, ua), Y(pa + side * half, ua), Y(pb + side * half, ub), Y(pb - side * half, ub), Vector3.down, m.Stone);
                }
                // A carved band along each side, just under the parapets
                for (int sgn = -1; sgn <= 1; sgn += 2)
                    m.SlopedWall(At(0f) + side * (sgn * (half + 0.06f)), At(length) + side * (sgn * (half + 0.06f)), a.y - 0.1f, b.y - 0.1f, a.y - 0.45f, 0.14f, m.Trim, m.Trim);
            }
            else
            {
                // Wooden: planks across, two beams under them, and trestles of posts down to the ground
                Quaternion slope = Quaternion.LookRotation(Y(dir * length, b.y - a.y).normalized);
                float run = Vector3.Distance(Y(At(0f), a.y), Y(At(length), b.y));
                int planks = Mathf.CeilToInt(run / 0.32f);
                for (int i = 0; i < planks; i++)
                {
                    float x = (i + 0.5f) * length / planks;
                    m.Block(Y(At(x), Deck(x) - 0.06f), new Vector3(s.BridgeWidth + (i % 3 == 0 ? 0.12f : 0f), 0.12f, run / planks - 0.025f), slope, m.Wood, m.Wood, true);
                }
                foreach (int sgn in new[] { -1, 1 })
                    m.Block(Y(At(length * 0.5f) + side * (sgn * (half - 0.35f)), (a.y + b.y) * 0.5f - 0.32f), new Vector3(0.25f, 0.4f, run), slope, m.Wood, m.Wood, true);
                for (float x = 2f; x < length - 1f; x += 4f)
                {
                    float deck = Deck(x) - 0.5f, foot = GroundUnder(x) - 1f;
                    if (deck - foot < 0.5f) continue;
                    foreach (int sgn in new[] { -1, 1 })
                        m.Block(new Vector3(At(x).x, (deck + foot) * 0.5f, At(x).z) + side * (sgn * (half - 0.35f)), new Vector3(0.3f, deck - foot, 0.3f), Quaternion.LookRotation(dir), m.Wood, m.Wood, true);
                    m.Block(Y(At(x), deck - 0.15f), new Vector3(s.BridgeWidth - 0.2f, 0.25f, 0.25f), Quaternion.LookRotation(dir), m.Wood, m.Wood, true);
                    if (deck - foot > 2f)
                    {
                        Vector3 low = Y(At(x) - side * (half - 0.35f), foot + 1f), high = Y(At(x) + side * (half - 0.35f), deck - 0.3f);
                        m.Block((low + high) * 0.5f, new Vector3(0.18f, 0.18f, Vector3.Distance(low, high)), Quaternion.LookRotation(high - low), m.Wood, m.Wood, true);
                    }
                }
            }

            // Parapets or railings along both sides
            for (int sgn = -1; sgn <= 1; sgn += 2)
            {
                Vector3 p0 = At(0f) + side * (sgn * (half - 0.2f)), p1 = At(length) + side * (sgn * (half - 0.2f));
                if (s.Bridge == BridgeStyle.Wooden)
                {
                    if (s.BridgeRail == Rail.None) continue;
                    WoodRail(m, Y(p0, a.y), Y(p1, b.y));
                }
                else if (s.BridgeRail == Rail.Wall) m.SlopedWall(p0, p1, a.y + 1f, b.y + 1f, a.y - 0.2f, 0.4f, m.Stone, m.Trim);
                else if (s.BridgeRail == Rail.Balustrade) Balustrade(m, Y(p0, a.y), Y(p1, b.y));
            }

            // Lanterns at both ends, and along long bridges
            if (s.BridgeLamps)
            {
                var spots = new List<float> { 0.8f, length - 0.8f };
                for (float x = 12f; x < length - 6f; x += 12f) spots.Add(x);
                foreach (float x in spots)
                    foreach (int sgn in new[] { -1, 1 })
                    {
                        float h = s.BridgeRail == Rail.Wall && s.Bridge == BridgeStyle.StoneArch ? Deck(x) + 1f : Deck(x);
                        lights.Add(LampPost(m, Y(At(x) + side * (sgn * (half - 0.2f)), h), s.BridgeRail == Rail.Wall ? 2.2f : 3.2f));
                    }
            }

            GameObject built = m.Build(s.Bridge == BridgeStyle.Wooden ? "Wooden Bridge" : "Stone Bridge", parent);
            AddLights(built, lights, s);
            return Finish(built, CastleFeature.Kind.Bridge, true, a, b, 0f, s);
        }

        // ================================================================ Street dressing

        /// <summary>Lamp posts, banners, hedges, cypresses and benches along both sides of a drawn line.</summary>
        internal static GameObject Street(List<Vector3> line, Settings s, SurfaceAt ground, Transform parent)
        {
            if (line == null || line.Count < 2) return null;
            float total = 0f;
            for (int i = 1; i < line.Count; i++) total += Flat(line[i] - line[i - 1]).magnitude;
            if (total < 3f) return null;
            var m = new Mesher(Flat(line[line.Count / 2]));
            var lights = new List<Vector3>();
            Vector3 PointAt(float d, out Vector3 t)
            {
                for (int i = 1; i < line.Count; i++)
                {
                    float seg = Flat(line[i] - line[i - 1]).magnitude;
                    if (d <= seg || i == line.Count - 1)
                    {
                        t = seg > 0.001f ? Flat(line[i] - line[i - 1]) / seg : Vector3.forward;
                        return Flat(line[i - 1]) + t * Mathf.Min(d, seg);
                    }
                    d -= seg;
                }
                t = Vector3.forward;
                return Flat(line[line.Count - 1]);
            }
            float? Surface(Vector3 p) => ground(p);
            var random = new System.Random(Mathf.RoundToInt(line[0].x * 13f + line[0].z * 7f));
            int index = 0;
            var sides = s.BothSides ? new[] { -1, 1 } : new[] { 1 };
            float spacing = Mathf.Max(3f, s.Spacing);
            for (float d = spacing * 0.5f; d <= total; d += spacing, index++)
            {
                Vector3 p = PointAt(d, out Vector3 t);
                Vector3 side = Vector3.Cross(Vector3.up, t);
                foreach (int sgn in sides)
                {
                    Vector3 edge = p + side * (sgn * s.StreetHalf);
                    Vector3 outward = side * sgn;
                    float? y = Surface(edge);
                    if (!y.HasValue) continue;
                    if (s.Lamps) lights.Add(LampPost(m, Y(edge, y.Value), 3.6f));
                    // Between the lamps: banners, cypresses and benches by turns, and hedges along the edge
                    Vector3 between = PointAt(Mathf.Min(total, d + spacing * 0.5f), out _) + side * (sgn * (s.StreetHalf + 0.2f));
                    float? yb = Surface(between);
                    if (yb.HasValue && d + spacing * 0.5f <= total)
                    {
                        int pick = (index + (sgn > 0 ? 0 : 1)) % 3;
                        if (pick == 0 && s.Banners) BannerPole(m, Y(between, yb.Value), Quaternion.LookRotation(t), s);
                        else if (pick == 1 && s.Trees) Cypress(m, Y(between + outward * 0.9f, yb.Value), 3.2f + (float)random.NextDouble() * 1.4f);
                        else if (pick == 2 && s.Benches) Bench(m, Y(between + outward * 0.5f, yb.Value), Quaternion.LookRotation(-outward));
                    }
                    if (s.Hedges)
                    {
                        for (float h = d + 0.9f; h < Mathf.Min(total, d + spacing - 0.9f); h += 1.2f)
                        {
                            Vector3 hp = PointAt(h, out Vector3 ht) + Vector3.Cross(Vector3.up, ht) * (sgn * (s.StreetHalf + 1.5f));
                            float? hy = Surface(hp);
                            if (hy.HasValue) m.Block(Y(hp, hy.Value + 0.42f), new Vector3(0.9f, 0.9f, 1.25f), Quaternion.LookRotation(ht), m.Hedge, m.Hedge, true);
                        }
                    }
                }
            }
            GameObject built = m.Build("Street", parent);
            AddLights(built, lights, s);
            return Finish(built, CastleFeature.Kind.Street, false, line[0], line[line.Count - 1], 0f, s);
        }

        // ================================================================ Plaza

        /// <summary>A round paved plaza, raised on ring steps, with a fountain in the middle and lamps round it.</summary>
        internal static GameObject Plaza(Vector3 centre, float radius, Settings s, SurfaceAt ground, Transform parent)
        {
            if (radius < 3f) return null;
            float low = float.MaxValue, high = float.MinValue;
            for (int i = 0; i < 16; i++)
            {
                for (float r = 0f; r <= radius + s.PlazaSteps * 0.7f; r += Mathf.Max(1f, radius / 3f))
                {
                    float? g = ground(centre + Quaternion.Euler(0f, i * 22.5f, 0f) * Vector3.forward * r);
                    if (!g.HasValue) continue;
                    low = Mathf.Min(low, g.Value);
                    high = Mathf.Max(high, g.Value);
                }
            }
            if (low == float.MaxValue) return null;
            float top = high + 0.12f + s.PlazaSteps * Riser, bottom = low - Footing;
            var m = new Mesher(Flat(centre));
            var lights = new List<Vector3>();
            Vector3 c = Flat(centre);
            int sides = Mathf.Clamp(Mathf.RoundToInt(radius * 6f), 24, 96);
            for (int k = s.PlazaSteps; k >= 1; k--)
                m.Cylinder(Y(c, bottom), radius + k * 0.7f, radius + k * 0.7f, bottom, top - k * Riser, sides, m.Paving, m.Stone, true);
            m.Cylinder(Y(c, bottom), radius, radius, bottom, top, sides, m.Paving, m.Stone, true);

            if (s.Fountain) Fountain(m, c, top, Mathf.Clamp(radius * 0.32f, 1.6f, 4f));
            if (s.PlazaLamps)
            {
                int count = Mathf.Clamp(Mathf.RoundToInt(radius * 2f * Mathf.PI / 10f), 4, 12);
                for (int i = 0; i < count; i++)
                {
                    Vector3 p = c + Quaternion.Euler(0f, i * 360f / count + 180f / count, 0f) * Vector3.forward * (radius - 0.8f);
                    lights.Add(LampPost(m, Y(p, top), 3.4f));
                }
            }
            GameObject built = m.Build("Plaza", parent);
            AddLights(built, lights, s);
            return Finish(built, CastleFeature.Kind.Plaza, true, centre, centre + Vector3.right * radius, 0f, s);
        }

        /// <summary>A paved plaza over any drawn outline (smoothed), raised on ring steps that follow its shape, with a fountain and lamps.</summary>
        internal static GameObject PlazaOutline(List<Vector3> drawn, Settings s, SurfaceAt ground, Transform parent)
        {
            if (drawn == null || drawn.Count < 3) return null;
            var simple = Douglas(drawn, 0.5f);
            if (simple.Count > 2 && Flat(simple[simple.Count - 1] - simple[0]).magnitude < 1.5f) simple.RemoveAt(simple.Count - 1);
            if (simple.Count < 3) return null;
            List<Vector3> outline = Chaikin(simple, 3, true);
            for (int i = 0; i < outline.Count; i++) outline[i] = Flat(outline[i]);
            if (SignedArea(outline) < 0f) outline.Reverse();
            if (SignedArea(outline) < 10f) return null;

            float ringOut = s.PlazaSteps * 0.7f;
            List<Vector3> outer = Offset(outline, ringOut);
            float low = float.MaxValue, high = float.MinValue;
            Vector3 lo = outer[0], hi = outer[0];
            foreach (Vector3 p in outer) { lo = Vector3.Min(lo, p); hi = Vector3.Max(hi, p); }
            void Sample(Vector3 p) { float? g = ground(p); if (g.HasValue) { low = Mathf.Min(low, g.Value); high = Mathf.Max(high, g.Value); } }
            foreach (Vector3 p in outer) Sample(p);
            float stepX = Mathf.Max(1.5f, (hi.x - lo.x) / 24f), stepZ = Mathf.Max(1.5f, (hi.z - lo.z) / 24f);
            for (float x = lo.x; x <= hi.x; x += stepX)
                for (float z = lo.z; z <= hi.z; z += stepZ)
                    if (Inside(outer, new Vector3(x, 0f, z))) Sample(new Vector3(x, 0f, z));
            if (low == float.MaxValue) return null;
            float top = high + 0.12f + s.PlazaSteps * Riser, bottom = low - Footing;

            Vector3 centre = Centroid(outline);
            var m = new Mesher(centre);
            var lights = new List<Vector3>();
            for (int k = s.PlazaSteps; k >= 1; k--) m.Prism(Offset(outline, k * 0.7f), bottom, top - k * Riser, m.Paving, m.Stone);
            m.Prism(outline, bottom, top, m.Paving, m.Stone);

            // The fountain in the middle, if it fits there
            if (s.Fountain && Inside(outline, centre))
            {
                float room = float.MaxValue;
                for (int i = 0; i < outline.Count; i++) room = Mathf.Min(room, DistanceToSegment(centre, outline[i], outline[(i + 1) % outline.Count]));
                float r = Mathf.Clamp(room * 0.4f, 1.2f, 4f);
                if (room > r + 1f) Fountain(m, centre, top, r);
            }
            // Lamps along its edge, every 10 m or so
            if (s.PlazaLamps)
            {
                List<Vector3> inner = Offset(outline, -0.8f);
                float perimeter = 0f;
                for (int i = 0; i < inner.Count; i++) perimeter += Flat(inner[(i + 1) % inner.Count] - inner[i]).magnitude;
                int count = Mathf.Clamp(Mathf.RoundToInt(perimeter / 10f), 3, 24);
                float next = perimeter / count * 0.5f, walked = 0f;
                for (int i = 0; i < inner.Count && lights.Count < count; i++)
                {
                    Vector3 a = inner[i], b = inner[(i + 1) % inner.Count];
                    float seg = Flat(b - a).magnitude;
                    while (next <= walked + seg && lights.Count < count)
                    {
                        lights.Add(LampPost(m, Y(Vector3.Lerp(a, b, (next - walked) / Mathf.Max(seg, 1e-4f)), top), 3.4f));
                        next += perimeter / count;
                    }
                    walked += seg;
                }
            }
            GameObject built = m.Build("Plaza", parent);
            AddLights(built, lights, s);
            return Finish(built, CastleFeature.Kind.Plaza, true, outline[0], outline[outline.Count - 1], 0f, s);
        }

        // A stone fountain: a rimmed basin with a pool, a column with a bowl of water on top
        private static void Fountain(Mesher m, Vector3 c, float top, float r)
        {
            c = Flat(c);
            m.Cylinder(Y(c, top), r, r, top, top + 0.6f, 48, m.Trim, m.Trim, false);
            m.Ring(Y(c, top + 0.6f), r - 0.35f, r, 48, m.Trim);
            m.Cylinder(Y(c, top), r - 0.35f, r - 0.35f, top + 0.6f, top + 0.05f, 48, m.Stone, m.Stone, false, true);
            m.Disc(Y(c, top + 0.42f), r - 0.35f, 48, m.Water);
            m.Cylinder(Y(c, top), 0.32f, 0.26f, top, top + 1.7f, 16, m.Trim, m.Trim, false);
            m.Cylinder(Y(c, top + 1.7f), 0.3f, 1.05f, top + 1.7f, top + 2.05f, 32, m.Trim, m.Trim, false);
            m.Ring(Y(c, top + 2.05f), 0.9f, 1.05f, 32, m.Trim);
            m.Disc(Y(c, top + 1.98f), 0.9f, 32, m.Water);
            m.Cylinder(Y(c, top + 2.05f), 0.14f, 0.05f, top + 1.98f, top + 2.6f, 12, m.Trim, m.Trim, true);
        }

        // An outline (anticlockwise) pushed out by 'by' metres (in, if negative), its corners mitred (but not too far)
        private static List<Vector3> Offset(List<Vector3> pts, float by)
        {
            int n = pts.Count;
            var result = new List<Vector3>(n);
            for (int i = 0; i < n; i++)
            {
                Vector3 prev = pts[(i + n - 1) % n], p = pts[i], next = pts[(i + 1) % n];
                Vector3 d0 = Flat(p - prev).normalized, d1 = Flat(next - p).normalized;
                Vector3 n0 = new Vector3(d0.z, 0f, -d0.x), n1 = new Vector3(d1.z, 0f, -d1.x);
                Vector3 bis = (n0 + n1).normalized;
                if (bis.sqrMagnitude < 0.5f) bis = n1;
                float scale = 1f / Mathf.Max(0.35f, Vector3.Dot(bis, n1));
                result.Add(Flat(p) + bis * (by * scale));
            }
            return result;
        }

        private static Vector3 Centroid(List<Vector3> pts)
        {
            float a = 0f, cx = 0f, cz = 0f;
            for (int i = 0; i < pts.Count; i++)
            {
                Vector3 p = pts[i], q = pts[(i + 1) % pts.Count];
                float cross = p.x * q.z - q.x * p.z;
                a += cross; cx += (p.x + q.x) * cross; cz += (p.z + q.z) * cross;
            }
            if (Mathf.Abs(a) < 1e-4f) return Flat(pts[0]);
            return new Vector3(cx / (3f * a), 0f, cz / (3f * a));
        }

        private static float DistanceToSegment(Vector3 p, Vector3 a, Vector3 b)
        {
            p = Flat(p); a = Flat(a); b = Flat(b);
            Vector3 ab = b - a;
            float t = ab.sqrMagnitude > 1e-6f ? Mathf.Clamp01(Vector3.Dot(p - a, ab) / ab.sqrMagnitude) : 0f;
            return Vector3.Distance(p, a + ab * t);
        }

        // ================================================================ Keep

        /// <summary>A great keep: one of the tower styles, much broader and taller than the watch towers.</summary>
        internal static GameObject Keep(Vector3 centre, float yaw, Settings s, CastleWallBuilder.Settings castle, CastleWallBuilder.GroundAt ground, Transform parent)
        {
            CastleWallBuilder.Settings k = castle.Copy();
            k.TowerCells = Mathf.Clamp(s.KeepCells, 3, 9);
            k.Storeys = Mathf.Clamp(s.KeepStoreys, 3, 14);
            k.TowerExtra = 0;
            k.Style = s.KeepStyle;
            k.TowerRoofs = true;
            var holder = new GameObject("Keep");
            holder.transform.SetParent(parent, false);
            GameObject tower = CastleWallBuilder.Tower(centre, yaw, k, ground, holder.transform);
            if (tower == null) { Object.DestroyImmediate(holder); return null; }
            return Finish(holder, CastleFeature.Kind.Keep, false, centre, centre, yaw, s);
        }

        // ================================================================ Upper areas' edges (and opening them for stairs)

        // One side's edge as its own piece, from p to q (at the top), with its openings ('gaps': from-to, metres along it)
        // and planters along it (not in the openings)
        private static GameObject BuildEdge(Transform parent, Vector3 p, Vector3 q, Vector3 outward, Edge style, bool planters, List<Vector2> gaps, string theme)
        {
            float length = Flat(q - p).magnitude;
            if (length < 0.3f) return null;
            Vector3 dir = Flat(q - p) / length;
            var open = new List<Vector2>(gaps ?? new List<Vector2>());
            open.Sort((x, y) => x.x.CompareTo(y.x));
            var m = new Mesher(Flat((p + q) * 0.5f));
            Vector3 At(float d) => p + dir * d;
            float from = 0f;
            foreach (Vector2 g in open)
            {
                if (g.x > from + 0.4f) EdgeAlong(m, At(from), At(Mathf.Min(g.x, length)), outward, style);
                from = Mathf.Max(from, g.y);
            }
            if (length > from + 0.4f) EdgeAlong(m, At(from), At(length), outward, style);
            if (planters && length > 6f)
            {
                for (float t = 3f; t <= length - 3f; t += 6f)
                {
                    bool inGap = false;
                    foreach (Vector2 g in open) if (t > g.x - 1.2f && t < g.y + 1.2f) inGap = true;
                    if (!inGap) Planter(m, At(t) - outward * 0.9f, Quaternion.LookRotation(outward));
                }
            }
            GameObject go = m.Build("Edge", parent);
            if (go == null)
            {
                go = new GameObject("Edge");     // (nothing to show, but kept: it remembers the edge)
                go.transform.SetParent(parent, false);
                go.transform.position = Flat((p + q) * 0.5f);
            }
            var edge = go.AddComponent<CastleFeatureEdge>();
            edge.from = p; edge.to = q; edge.outward = outward;
            edge.style = (int)style; edge.planters = planters; edge.gaps = open; edge.theme = theme;
            CastleWallBuilder.Finish(go, new CastleWallBuilder.Settings { Theme = theme });
            return go;
        }

        /// <summary>
        /// Opens the railings of upper areas where stairs (along 'line', flat points from foot to top, arriving at 'topY',
        /// 'width' wide) cross their edges or arrive at them: each edge met is rebuilt with a gap there (undoably).
        /// Returns how many were opened.
        /// </summary>
        internal static int OpenEdgesFor(List<Vector3> line, float topY, float width)
        {
            if (line == null || line.Count < 2) return 0;
            int opened = 0;
            Vector3 topPoint = Flat(line[line.Count - 1]);
            foreach (CastleFeatureEdge edge in Object.FindObjectsByType<CastleFeatureEdge>(FindObjectsSortMode.None))
            {
                if (edge == null || Mathf.Abs(edge.from.y - topY) > 1.3f) continue;
                Vector3 p = Flat(edge.from), q = Flat(edge.to);
                float length = Vector3.Distance(p, q);
                if (length < 0.3f) continue;
                Vector3 dir = (q - p) / length;
                // Where the stairs cross it (the last crossing, nearest their top), or else where their top arrives at it
                float? at = null;
                for (int i = 1; i < line.Count; i++)
                    if (Cross(Flat(line[i - 1]), Flat(line[i]), p, q, out Vector3 hit)) at = Vector3.Dot(hit - p, dir);
                if (!at.HasValue && DistanceToSegment(topPoint, p, q) < 1.3f) at = Mathf.Clamp(Vector3.Dot(topPoint - p, dir), 0f, length);
                if (!at.HasValue) continue;
                float half = width * 0.5f + 0.45f;
                var gaps = new List<Vector2>(edge.gaps) { new Vector2(at.Value - half, at.Value + half) };
                GameObject made = BuildEdge(edge.transform.parent, edge.from, edge.to, edge.outward, (Edge)edge.style, edge.planters, gaps, edge.theme);
                if (made != null) Undo.RegisterCreatedObjectUndo(made, "Open Railing For Stairs");
                Undo.DestroyObjectImmediate(edge.gameObject);
                opened++;
            }
            return opened;
        }

        // Whether two flat segments cross, and where
        private static bool Cross(Vector3 a, Vector3 b, Vector3 c, Vector3 d, out Vector3 hit)
        {
            hit = default;
            Vector3 r = b - a, s = d - c;
            float denom = r.x * s.z - r.z * s.x;
            if (Mathf.Abs(denom) < 1e-6f) return false;
            Vector3 ca = c - a;
            float t = (ca.x * s.z - ca.z * s.x) / denom, u = (ca.x * r.z - ca.z * r.x) / denom;
            if (t < 0f || t > 1f || u < 0f || u > 1f) return false;
            hit = a + r * t;
            return true;
        }

        // ================================================================ Pieces

        // Along an edge (from p to q, its outside 'outward'): a balustrade, battlements, a hedge, or nothing
        private static void EdgeAlong(Mesher m, Vector3 p, Vector3 q, Vector3 outward, Edge edge)
        {
            if (Flat(q - p).magnitude < 0.5f) return;
            switch (edge)
            {
                case Edge.Balustrade: Balustrade(m, p, q); break;
                case Edge.Battlements:
                {
                    Vector3 dir = Flat(q - p).normalized;
                    float length = Flat(q - p).magnitude;
                    Quaternion turn = Quaternion.LookRotation(dir);
                    m.Block((p + q) * 0.5f + Vector3.up * 0.35f, new Vector3(0.45f, 0.7f, length), turn, m.Trim, m.Stone, true);
                    int merlons = Mathf.Max(1, Mathf.FloorToInt(length / 1.5f));
                    for (int i = 0; i < merlons; i++)
                        m.Block(p + dir * ((i + 0.5f) * length / merlons) + Vector3.up * 1.05f, new Vector3(0.45f, 0.7f, 0.8f), turn, m.Trim, m.Stone, true);
                    break;
                }
                case Edge.Hedge:
                {
                    Vector3 dir = Flat(q - p).normalized;
                    m.Block((p + q) * 0.5f + Vector3.up * 0.45f, new Vector3(0.75f, 0.9f, Flat(q - p).magnitude), Quaternion.LookRotation(dir), m.Hedge, m.Hedge, true);
                    break;
                }
            }
        }

        // A stone balustrade from p to q (the line it stands on: it can slope): pedestals, balusters, a rail on top
        private static void Balustrade(Mesher m, Vector3 p, Vector3 q)
        {
            Vector3 run = q - p;
            float length = run.magnitude;
            if (length < 0.4f) return;
            Vector3 dir = run / length, flatDir = Flat(run).normalized;
            Quaternion along = Quaternion.LookRotation(dir), upright = Quaternion.LookRotation(flatDir);
            const float Height = 0.95f;
            m.Block((p + q) * 0.5f + Vector3.up * 0.07f, new Vector3(0.36f, 0.14f, length), along, m.Trim, m.Trim, true);
            m.Block((p + q) * 0.5f + Vector3.up * (Height - 0.07f), new Vector3(0.42f, 0.14f, length + 0.1f), along, m.Trim, m.Trim, true);
            int pedestals = Mathf.Max(1, Mathf.CeilToInt(length / 3.2f));
            for (int i = 0; i <= pedestals; i++)
            {
                Vector3 at = p + run * ((float)i / pedestals);
                m.Block(at + Vector3.up * (Height * 0.5f + 0.04f), new Vector3(0.36f, Height + 0.08f, 0.36f), upright, m.Trim, m.Trim, true);
            }
            int balusters = Mathf.FloorToInt(length / 0.3f);
            for (int i = 1; i < balusters; i++)
            {
                Vector3 at = p + run * ((float)i / balusters);
                if (Mathf.Repeat(i * 0.3f, length / pedestals) < 0.25f) continue;
                m.Cylinder(at + Vector3.up * 0.14f, 0.07f, 0.09f, at.y + 0.14f, at.y + Height * 0.5f, 8, m.Trim, m.Trim, false);
                m.Cylinder(at + Vector3.up * (Height * 0.5f), 0.09f, 0.06f, at.y + Height * 0.5f, at.y + Height - 0.14f, 8, m.Trim, m.Trim, false);
            }
        }

        private static void WoodRail(Mesher m, Vector3 p, Vector3 q)
        {
            Vector3 run = q - p;
            float length = run.magnitude;
            Quaternion along = Quaternion.LookRotation(run / length);
            int posts = Mathf.Max(1, Mathf.CeilToInt(length / 2f));
            for (int i = 0; i <= posts; i++)
                m.Block(p + run * ((float)i / posts) + Vector3.up * 0.5f, new Vector3(0.14f, 1.05f, 0.14f), Quaternion.LookRotation(Flat(run)), m.Wood, m.Wood, true);
            m.Block((p + q) * 0.5f + Vector3.up * 1.0f, new Vector3(0.1f, 0.12f, length), along, m.Wood, m.Wood, true);
            m.Block((p + q) * 0.5f + Vector3.up * 0.55f, new Vector3(0.08f, 0.1f, length), along, m.Wood, m.Wood, true);
        }

        // An iron lamp post with a lantern (returns where its light is)
        private static Vector3 LampPost(Mesher m, Vector3 foot, float height)
        {
            m.Cylinder(foot, 0.16f, 0.12f, foot.y, foot.y + 0.35f, 8, m.Iron, m.Iron, true);
            m.Cylinder(foot, 0.06f, 0.05f, foot.y + 0.35f, foot.y + height, 8, m.Iron, m.Iron, true);
            Vector3 head = foot + Vector3.up * height;
            m.Block(head + Vector3.up * 0.02f, new Vector3(0.34f, 0.05f, 0.34f), Quaternion.identity, m.Iron, m.Iron, false);
            m.Block(head + Vector3.up * 0.25f, new Vector3(0.24f, 0.4f, 0.24f), Quaternion.Euler(0f, 45f, 0f), m.Glow, m.Glow, false);
            m.Cylinder(head + Vector3.up * 0.45f, 0.26f, 0.02f, head.y + 0.45f, head.y + 0.72f, 4, m.Iron, m.Iron, true, false, 45f);
            return head + Vector3.up * 0.25f;
        }

        private static void BannerPole(Mesher m, Vector3 foot, Quaternion facing, Settings s)
        {
            Material cloth = CastleWallBuilder.DetailMaterial("Banner " + ColorUtility.ToHtmlStringRGB(s.BannerColour), s.BannerColour, 0f, 0.1f);
            Material trim = CastleWallBuilder.DetailMaterial("Banner Trim " + ColorUtility.ToHtmlStringRGB(s.BannerTrim), s.BannerTrim, 0.4f, 0.4f);
            m.Cylinder(foot, 0.14f, 0.1f, foot.y, foot.y + 0.3f, 8, m.Iron, m.Iron, true);
            m.Cylinder(foot, 0.05f, 0.045f, foot.y + 0.3f, foot.y + 4.6f, 8, m.Iron, m.Iron, true);
            Vector3 arm = foot + Vector3.up * 4.3f;
            m.Block(arm, new Vector3(1.1f, 0.06f, 0.06f), facing, m.Iron, m.Iron, true);
            m.Block(arm + Vector3.down * 1.15f, new Vector3(0.9f, 2.2f, 0.04f), facing, cloth, cloth, true);
            m.Block(arm + Vector3.down * 2.3f, new Vector3(0.9f, 0.1f, 0.05f), facing, trim, trim, true);
            m.Block(arm + Vector3.down * 0.95f, new Vector3(0.38f, 0.38f, 0.06f), facing * Quaternion.Euler(0f, 0f, 45f), trim, trim, true);
        }

        private static void Cypress(Mesher m, Vector3 foot, float height)
        {
            m.Cylinder(foot, 0.1f, 0.08f, foot.y, foot.y + 0.5f, 6, m.Wood, m.Wood, true);
            m.Cylinder(foot, 0.42f, 0.55f, foot.y + 0.35f, foot.y + height * 0.35f, 10, m.Hedge, m.Hedge, false);
            m.Cylinder(foot, 0.55f, 0.04f, foot.y + height * 0.35f, foot.y + height, 10, m.Hedge, m.Hedge, true);
        }

        private static void Bench(Mesher m, Vector3 foot, Quaternion facing)
        {
            Vector3 right = facing * Vector3.right, back = facing * Vector3.back;
            foreach (int sgn in new[] { -1, 1 })
                m.Block(foot + right * (sgn * 0.7f) + Vector3.up * 0.22f, new Vector3(0.14f, 0.44f, 0.45f), facing, m.Trim, m.Trim, true);
            m.Block(foot + Vector3.up * 0.47f, new Vector3(1.7f, 0.07f, 0.48f), facing, m.Wood, m.Wood, false);
            m.Block(foot + back * 0.22f + Vector3.up * 0.8f, new Vector3(1.7f, 0.42f, 0.06f), facing, m.Wood, m.Wood, false);
        }

        private static void Planter(Mesher m, Vector3 foot, Quaternion facing)
        {
            m.Block(foot + Vector3.up * 0.3f, new Vector3(1.1f, 0.6f, 1.1f), facing, m.Trim, m.Trim, true);
            m.Block(foot + Vector3.up * 0.85f, new Vector3(0.95f, 0.6f, 0.95f), facing * Quaternion.Euler(0f, 45f, 0f), m.Hedge, m.Hedge, true);
        }

        // Real lights in the lanterns, if asked for (each one costs: they're kept few and short)
        private static void AddLights(GameObject built, List<Vector3> spots, Settings s)
        {
            if (!s.Lights || built == null) return;
            foreach (Vector3 spot in spots)
            {
                var go = new GameObject("Lantern Light");
                go.transform.SetParent(built.transform, false);
                go.transform.position = spot;
                var l = go.AddComponent<Light>();
                l.type = LightType.Point;
                l.color = new Color(1f, 0.68f, 0.38f);
                l.range = 8f;
                l.intensity = 1.8f;
                l.shadows = LightShadows.None;
            }
        }

        private static GameObject Finish(GameObject built, CastleFeature.Kind kind, bool walkable, Vector3 from, Vector3 to, float yaw, Settings s)
        {
            if (built == null) return null;
            var feature = built.AddComponent<CastleFeature>();
            feature.kind = kind;
            feature.walkable = walkable;
            feature.from = from;
            feature.to = to;
            feature.yaw = yaw;
            CastleWallBuilder.Finish(built, new CastleWallBuilder.Settings { Theme = s.Theme });
            return built;
        }

        // ================================================================ Helpers

        private static Vector3 Flat(Vector3 v) => new Vector3(v.x, 0f, v.z);
        private static Vector3 Y(Vector3 v, float y) => new Vector3(v.x, y, v.z);

        // The four sides of a rectangle: their middles, which way along, half their length, and which way out
        private static List<(Vector3 mid, Vector3 along, float half, Vector3 outward)> Sides(Vector3 centre, Vector3 right, Vector3 forward, float hx, float hz) =>
            new List<(Vector3, Vector3, float, Vector3)>
            {
                (centre + forward * hz, right, hx, forward),
                (centre + right * hx, -forward, hz, right),
                (centre - forward * hz, -right, hx, -forward),
                (centre - right * hx, forward, hz, -right),
            };

        private static void Range(SurfaceAt ground, Vector3 centre, Vector3 right, Vector3 forward, float hx, float hz, out float low, out float high)
        {
            low = float.MaxValue; high = float.MinValue;
            int nx = Mathf.Max(2, Mathf.CeilToInt(hx)), nz = Mathf.Max(2, Mathf.CeilToInt(hz));
            for (int i = 0; i <= nx; i++)
                for (int j = 0; j <= nz; j++)
                {
                    float? g = ground(centre + right * (hx * (2f * i / nx - 1f)) + forward * (hz * (2f * j / nz - 1f)));
                    if (!g.HasValue) continue;
                    low = Mathf.Min(low, g.Value);
                    high = Mathf.Max(high, g.Value);
                }
            if (low == float.MaxValue) { low = high = centre.y; }
        }

        private static float MinUnder(SurfaceAt ground, Vector3 start, Vector3 dir, Vector3 side, float length, float half)
        {
            float low = float.MaxValue;
            for (float d = -1f; d <= length; d += 1.5f)
                for (int k = -1; k <= 1; k++)
                {
                    float? g = ground(start + dir * d + side * (k * half));
                    if (g.HasValue) low = Mathf.Min(low, g.Value);
                }
            return low == float.MaxValue ? start.y : low;
        }

        // ================================================================ Making the meshes

        // A mesh being made, in the world (around 'origin'), in several materials: one solid object with a collider
        private sealed class Mesher
        {
            private readonly Vector3 origin;
            private readonly List<Vector3> vertices = new List<Vector3>();
            private readonly List<Vector3> normals = new List<Vector3>();
            private readonly List<Vector2> uvs = new List<Vector2>();
            private readonly List<Material> materials = new List<Material>();
            private readonly List<List<int>> triangles = new List<List<int>>();

            public readonly Material Stone, Paving, Trim, Wood, Iron, Glow, Hedge, Water;

            public Mesher(Vector3 origin)
            {
                this.origin = new Vector3(origin.x, 0f, origin.z);
                string kit = MedievalVillageSetup.KitFolder + "/Materials/";
                Stone = AssetDatabase.LoadAssetAtPath<Material>(kit + "MI_UnevenBrick.mat");
                Paving = AssetDatabase.LoadAssetAtPath<Material>(kit + "MI_Brick.mat");
                Trim = AssetDatabase.LoadAssetAtPath<Material>(kit + "MI_RockTrim.mat");
                Wood = AssetDatabase.LoadAssetAtPath<Material>(kit + "MI_WoodTrim.mat");
                Iron = CastleWallBuilder.DetailMaterial("Castle Iron", new Color(0.12f, 0.12f, 0.13f), 0.7f, 0.35f);
                Glow = CastleWallBuilder.DetailMaterial("Lantern Glow", new Color(1f, 0.78f, 0.45f), 0f, 0.6f, new Color(1f, 0.6f, 0.25f) * 3f);
                Hedge = CastleWallBuilder.DetailMaterial("Hedge", new Color(0.2f, 0.38f, 0.14f), 0f, 0.1f);
                Water = CastleWallBuilder.DetailMaterial("Fountain Water", new Color(0.16f, 0.38f, 0.46f), 0.1f, 0.95f);
            }

            private List<int> For(Material m)
            {
                int i = materials.IndexOf(m);
                if (i >= 0) return triangles[i];
                materials.Add(m);
                triangles.Add(new List<int>());
                return triangles[triangles.Count - 1];
            }

            private float TileOf(Material m) => m == Stone || m == Paving ? 4f : m == Trim || m == Wood ? 2f : 1f;

            // A four-cornered face, facing 'normal' (its texture laid flat on it, in metres)
            public void Face(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 normal, Material m) =>
                Face(a, b, c, d, normal, normal, normal, normal, m);

            public void Face(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 na, Vector3 nb, Vector3 nc, Vector3 nd, Material m)
            {
                Vector3 n = (na + nb + nc + nd).normalized;
                Vector3 u = Mathf.Abs(n.y) > 0.7f ? Vector3.right : Vector3.Cross(Vector3.up, n).normalized;
                Vector3 v = Vector3.Cross(n, u);
                float tile = TileOf(m);
                int i = vertices.Count;
                foreach (Vector3 p in new[] { a, b, c, d })
                {
                    vertices.Add(p - origin);
                    uvs.Add(new Vector2(Vector3.Dot(p, u), Vector3.Dot(p, v)) / tile);
                }
                normals.Add(na.normalized); normals.Add(nb.normalized); normals.Add(nc.normalized); normals.Add(nd.normalized);
                List<int> into = For(m);
                if (Vector3.Dot(Vector3.Cross(b - a, c - a), n) >= 0f) into.AddRange(new[] { i, i + 1, i + 2, i, i + 2, i + 3 });
                else into.AddRange(new[] { i, i + 2, i + 1, i, i + 3, i + 2 });
            }

            public void Triangle(Vector3 a, Vector3 b, Vector3 c, Vector3 normal, Material m)
            {
                Vector3 u = Mathf.Abs(normal.y) > 0.7f ? Vector3.right : Vector3.Cross(Vector3.up, normal).normalized;
                Vector3 v = Vector3.Cross(normal, u);
                float tile = TileOf(m);
                int i = vertices.Count;
                foreach (Vector3 p in new[] { a, b, c })
                {
                    vertices.Add(p - origin);
                    normals.Add(normal);
                    uvs.Add(new Vector2(Vector3.Dot(p, u), Vector3.Dot(p, v)) / tile);
                }
                List<int> into = For(m);
                if (Vector3.Dot(Vector3.Cross(b - a, c - a), normal) >= 0f) into.AddRange(new[] { i, i + 1, i + 2 });
                else into.AddRange(new[] { i, i + 2, i + 1 });
            }

            // A box (centre, size, turn): its top in one material, its sides (and bottom, if asked) in another
            public void Block(Vector3 centre, Vector3 size, Quaternion turn, Material top, Material sides, bool bottom)
            {
                Vector3 h = size * 0.5f;
                Vector3 P(float x, float y, float z) => centre + turn * new Vector3(x * h.x, y * h.y, z * h.z);
                Vector3 r = turn * Vector3.right, up = turn * Vector3.up, f = turn * Vector3.forward;
                Face(P(-1, 1, -1), P(-1, 1, 1), P(1, 1, 1), P(1, 1, -1), up, top);
                Face(P(1, -1, -1), P(1, 1, -1), P(1, 1, 1), P(1, -1, 1), r, sides);
                Face(P(-1, -1, 1), P(-1, 1, 1), P(-1, 1, -1), P(-1, -1, -1), -r, sides);
                Face(P(-1, -1, 1), P(1, -1, 1), P(1, 1, 1), P(-1, 1, 1), f, sides);
                Face(P(1, -1, -1), P(-1, -1, -1), P(-1, 1, -1), P(1, 1, -1), -f, sides);
                if (bottom) Face(P(-1, -1, -1), P(1, -1, -1), P(1, -1, 1), P(-1, -1, 1), -up, sides);
            }

            // An upright round (or many-sided) piece, from 'y0' to 'y1', its radius going from r0 to r1; 'inward' faces in
            public void Cylinder(Vector3 at, float r0, float r1, float y0, float y1, int sides, Material top, Material side, bool cap, bool inward = false, float turn = 0f)
            {
                Vector3 c = new Vector3(at.x, 0f, at.z);
                float slope = (r0 - r1) / Mathf.Max(0.001f, Mathf.Abs(y1 - y0));
                for (int i = 0; i < sides; i++)
                {
                    float a0 = (i * 360f / sides + turn) * Mathf.Deg2Rad, a1 = ((i + 1) * 360f / sides + turn) * Mathf.Deg2Rad;
                    Vector3 d0 = new Vector3(Mathf.Sin(a0), 0f, Mathf.Cos(a0)), d1 = new Vector3(Mathf.Sin(a1), 0f, Mathf.Cos(a1));
                    Vector3 n0 = (d0 + Vector3.up * slope).normalized * (inward ? -1f : 1f), n1 = (d1 + Vector3.up * slope).normalized * (inward ? -1f : 1f);
                    Face(c + d0 * r0 + Vector3.up * y0, c + d1 * r0 + Vector3.up * y0, c + d1 * r1 + Vector3.up * y1, c + d0 * r1 + Vector3.up * y1, n0, n1, n1, n0, side);
                    if (cap && r1 > 0.001f) Triangle(c + Vector3.up * y1, c + d0 * r1 + Vector3.up * y1, c + d1 * r1 + Vector3.up * y1, Vector3.up, top);
                }
            }

            public void Disc(Vector3 at, float r, int sides, Material m)
            {
                for (int i = 0; i < sides; i++)
                {
                    float a0 = i * 2f * Mathf.PI / sides, a1 = (i + 1) * 2f * Mathf.PI / sides;
                    Triangle(at, at + new Vector3(Mathf.Sin(a0), 0f, Mathf.Cos(a0)) * r, at + new Vector3(Mathf.Sin(a1), 0f, Mathf.Cos(a1)) * r, Vector3.up, m);
                }
            }

            public void Ring(Vector3 at, float inner, float outer, int sides, Material m)
            {
                for (int i = 0; i < sides; i++)
                {
                    float a0 = i * 2f * Mathf.PI / sides, a1 = (i + 1) * 2f * Mathf.PI / sides;
                    Vector3 d0 = new Vector3(Mathf.Sin(a0), 0f, Mathf.Cos(a0)), d1 = new Vector3(Mathf.Sin(a1), 0f, Mathf.Cos(a1));
                    Face(at + d0 * inner, at + d0 * outer, at + d1 * outer, at + d1 * inner, Vector3.up, m);
                }
            }

            // An upright solid over any outline (flat points, any order round), from y0 up to y1: its top and its sides
            public void Prism(List<Vector3> outline, float y0, float y1, Material top, Material side)
            {
                var pts = new List<Vector3>(outline.Count);
                foreach (Vector3 p in outline) pts.Add(Flat(p));
                if (SignedArea(pts) < 0f) pts.Reverse();
                foreach (int[] t in Triangulate(pts)) Triangle(Y(pts[t[0]], y1), Y(pts[t[1]], y1), Y(pts[t[2]], y1), Vector3.up, top);
                for (int i = 0; i < pts.Count; i++)
                {
                    Vector3 a = pts[i], b = pts[(i + 1) % pts.Count];
                    Vector3 d = Flat(b - a);
                    if (d.sqrMagnitude < 1e-6f) continue;
                    d.Normalize();
                    Face(Y(a, y0), Y(b, y0), Y(b, y1), Y(a, y1), new Vector3(d.z, 0f, -d.x), side);
                }
            }

            // Half a disc, standing from y0 to y1, its round side facing 'facing'
            // A round bar from a to b (any direction), its ends capped
            public void Tube(Vector3 a, Vector3 b, float r, int sides, Material side, Material ends)
            {
                Vector3 axis = (b - a).normalized;
                if (axis.sqrMagnitude < 0.5f) return;
                Vector3 u = Vector3.Cross(axis, Mathf.Abs(axis.y) < 0.9f ? Vector3.up : Vector3.right).normalized, v = Vector3.Cross(axis, u);
                for (int i = 0; i < sides; i++)
                {
                    float a0 = i * 2f * Mathf.PI / sides, a1 = (i + 1) * 2f * Mathf.PI / sides;
                    Vector3 d0 = u * Mathf.Cos(a0) + v * Mathf.Sin(a0), d1 = u * Mathf.Cos(a1) + v * Mathf.Sin(a1);
                    Face(a + d0 * r, b + d0 * r, b + d1 * r, a + d1 * r, d0, d0, d1, d1, side);
                    if (ends != null)
                    {
                        Triangle(a, a + d1 * r, a + d0 * r, -axis, ends);
                        Triangle(b, b + d0 * r, b + d1 * r, axis, ends);
                    }
                }
            }

            // A flat round disc standing up (facing 'normal'), for targets and the like
            public void UprightDisc(Vector3 centre, Vector3 normal, float r, int sides, Material m)
            {
                Vector3 u = Vector3.Cross(normal, Vector3.up).normalized, v = Vector3.Cross(u, normal);
                for (int i = 0; i < sides; i++)
                {
                    float a0 = i * 2f * Mathf.PI / sides, a1 = (i + 1) * 2f * Mathf.PI / sides;
                    Triangle(centre, centre + (u * Mathf.Cos(a0) + v * Mathf.Sin(a0)) * r, centre + (u * Mathf.Cos(a1) + v * Mathf.Sin(a1)) * r, normal, m);
                }
            }

            public void HalfDisc(Vector3 centre, Vector3 facing, float r, float y0, float y1, Material top, Material side)
            {
                Vector3 c = new Vector3(centre.x, 0f, centre.z);
                Vector3 across = Vector3.Cross(Vector3.up, facing).normalized;
                const int Segments = 20;
                for (int i = 0; i < Segments; i++)
                {
                    float a0 = Mathf.PI * i / Segments, a1 = Mathf.PI * (i + 1) / Segments;
                    Vector3 d0 = across * Mathf.Cos(a0) + facing * Mathf.Sin(a0), d1 = across * Mathf.Cos(a1) + facing * Mathf.Sin(a1);
                    Face(c + d0 * r + Vector3.up * y0, c + d1 * r + Vector3.up * y0, c + d1 * r + Vector3.up * y1, c + d0 * r + Vector3.up * y1, d0, d1, d1, d0, side);
                    Triangle(c + Vector3.up * y1, c + d0 * r + Vector3.up * y1, c + d1 * r + Vector3.up * y1, Vector3.up, top);
                }
            }

            // A wall from p0 to p1 (its middle line), its top sloping from y0 to y1, standing on 'yBase', 'thick' through,
            // with a coping slab along its top
            public void SlopedWall(Vector3 p0, Vector3 p1, float y0, float y1, float yBase, float thick, Material body, Material coping)
            {
                Vector3 dir = Flat(p1 - p0).normalized, side = Vector3.Cross(Vector3.up, dir) * (thick * 0.5f);
                Face(Y(p0 - side, yBase), Y(p1 - side, yBase), Y(p1 - side, y1), Y(p0 - side, y0), -side, body);
                Face(Y(p0 + side, yBase), Y(p0 + side, y0), Y(p1 + side, y1), Y(p1 + side, yBase), side, body);
                Face(Y(p0 - side, yBase), Y(p0 - side, y0), Y(p0 + side, y0), Y(p0 + side, yBase), -dir, body);
                Face(Y(p1 - side, yBase), Y(p1 + side, yBase), Y(p1 + side, y1), Y(p1 - side, y1), dir, body);
                Vector3 wide = side * 1.25f;
                Face(Y(p0 - wide, y0 + 0.12f), Y(p0 + wide, y0 + 0.12f), Y(p1 + wide, y1 + 0.12f), Y(p1 - wide, y1 + 0.12f), Vector3.up, coping);
                Face(Y(p0 - wide, y0), Y(p1 - wide, y1), Y(p1 - wide, y1 + 0.12f), Y(p0 - wide, y0 + 0.12f), -side, coping);
                Face(Y(p0 + wide, y0), Y(p0 + wide, y0 + 0.12f), Y(p1 + wide, y1 + 0.12f), Y(p1 + wide, y1), side, coping);
            }

            public GameObject Build(string name, Transform parent)
            {
                if (vertices.Count == 0) return null;
                var mesh = new Mesh { name = name };
                if (vertices.Count > 65000) mesh.indexFormat = IndexFormat.UInt32;
                mesh.SetVertices(vertices);
                mesh.SetNormals(normals);
                mesh.SetUVs(0, uvs);
                mesh.subMeshCount = triangles.Count;
                for (int i = 0; i < triangles.Count; i++) mesh.SetTriangles(triangles[i], i);
                mesh.RecalculateTangents();
                MeshUtility.Optimize(mesh);   // (vertices reordered for the GPU)
                mesh.RecalculateBounds();
                var go = new GameObject(name);
                go.transform.SetParent(parent, false);
                go.transform.position = origin;
                go.AddComponent<MeshFilter>().sharedMesh = mesh;
                go.AddComponent<MeshRenderer>().sharedMaterials = materials.ToArray();
                go.AddComponent<MeshCollider>().sharedMesh = mesh;
                return go;
            }
        }
    }
}
#endif
