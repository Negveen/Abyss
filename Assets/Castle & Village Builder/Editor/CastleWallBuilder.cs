#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace YaraJ.CastleVillageBuilder
{
    /// <summary>
    /// Editor only. Builds castle walls for Castle & Village Builder' Castle Walls tab, in the Medieval Village kit's stone: smooth
    /// walls made as one piece along their whole length (any shape, mitred at the bends, their tops and walkways following
    /// the ground in a smooth line, the stone the same size everywhere), square or truly round towers (a pointed roof or
    /// battlements), solid stone battlements, and gateways cut through. The stone is the kit's own materials, so the house
    /// colour themes recolour it.
    /// </summary>
    internal static class CastleWallBuilder
    {
        internal const string ContainerName = "Castle Walls";

        internal enum TowerStyle { Square, Round, Octagon, Tapered, Turreted, Stepped, Hoarded, Mixed }

        internal sealed class Settings
        {
            public int Storeys = 4;           // how high the walls are, in 3 m storeys
            public int Thickness = 1;         // how wide the walkway along the top is, in 2 m cells
            public int TowerCells = 3;        // how wide the towers are, in 2 m cells
            public int TowerExtra = 2;        // how many storeys the towers stand above the walls
            public bool TowerRoofs = true;    // pointed roofs on the towers (or battlements)
            public bool Battlements = true;   // battlements along the walls
            public float TowerEvery = 40f;    // more towers along long walls, this far apart (0: only where clicked)
            public string Theme;              // the colour theme (BuildingThemes) for the stone
            public bool WatchTowers = true;   // towers along the walls (or none)
            public TowerStyle Style = TowerStyle.Square;  // what the watch towers look like
            public bool RoundTowers { get => Style == TowerStyle.Round; set => Style = value ? TowerStyle.Round : TowerStyle.Square; }
            public Settings Copy() => (Settings)MemberwiseClone();
            public float Bend = 6f;           // curved walls: how far apart the points along them are (shorter is smoother)
            public float GateWidth = 3.4f;    // gateways: how wide (m)
            public float GateHeight = 5f;     // gateways: how high, to the top of the arch (m)
            public Details Detail = new Details();
        }

        /// <summary>The little things that bring a castle to life (Castle Walls > Look > Details).</summary>
        internal sealed class Details
        {
            // Defences
            public bool ArrowSlits = true; public float SlitEvery = 6f;
            public int Openings = 2;           // what the openings are: 0 arrow slits, 1 windows, 2 windows high and slits low
            public bool Shutters = true;       // wooden shutters on some windows
            public bool GatePassage = true;    // stone arch rings, ribs, murder holes, grooves and torches in the gateways
            public bool Buttresses = false; public float ButtressEvery = 14f;
            public bool Corbels = true;        // a carved band and corbels under the battlements
            public bool Plinth = true;         // a broader stone course along the foot of the walls
            public bool Portcullis = true;     // an iron grate half raised in each gateway
            // Gateways
            public bool GateDoors = true;      // heavy wooden doors, swung open against the inside
            public bool Clutter = true;        // crates and a cart inside the gates
            // Life
            public bool Banners = true;
            public Color BannerColour = new Color(0.55f, 0.08f, 0.1f), BannerTrim = new Color(0.9f, 0.72f, 0.25f);
            public bool Flags = true;
            public bool Torches = true; public float TorchEvery = 16f; public bool TorchLights;
            public bool Stairs = true;         // stone stairs up to the walkway, on the inside
            // Nature
            public bool Ivy = true; public float IvyAmount = 0.25f;
            public bool Rubble = true; public float RubbleAmount = 0.35f;
        }

        private const float Storey = 3f;
        private const float Tile = 4f;           // metres of wall each repeat of the stone texture covers (stones the size they are on the houses)
        private const float Footing = 2f;        // how far walls and towers reach down into the ground
        private const float ParapetHeight = 1f, ParapetDepth = 0.55f, MerlonHeight = 0.95f;
        private const string TowerRoof = "Roof_Tower_RoundTiles";
        private static string StonePath => MedievalVillageSetup.KitFolder + "/Materials/MI_UnevenBrick.mat";
        private static string PavingPath => MedievalVillageSetup.KitFolder + "/Materials/MI_Brick.mat";

        /// <summary>The ground height at a point (null: off the land).</summary>
        internal delegate float? GroundAt(Vector3 world);

        internal static float TowerHalf(Settings s) => s.TowerCells;            // (cells of 2 m: half its width in metres)
        internal static float WallThickness(Settings s) => s.Thickness * 2f + (s.Battlements ? ParapetDepth * 2f : 0f);

        // ================================================================ Walls

        /// <summary>
        /// A wall along a line of points (any shape: a straight run, a curve, a ring), as one smooth piece. Its ends are
        /// trimmed back 'startInset' and 'endInset' metres (into the towers there, so there's no gap); a closed one goes
        /// right round. A gateway goes through its middle if asked. Null if there's too little left of it.
        /// </summary>
        internal static GameObject Run(List<Vector3> path, float startInset, float endInset, bool closed, List<float> gateways, Settings s, GroundAt ground, Transform parent,
                                       Vector3? inside = null, int innerSide = 0)
        {
            var points = new List<Vector3>();
            foreach (Vector3 p in path) if (points.Count == 0 || Flat(p - points[points.Count - 1]).magnitude > 0.05f) points.Add(Flat(p));
            if (closed && points.Count > 2 && Flat(points[0] - points[points.Count - 1]).magnitude < 0.05f) points.RemoveAt(points.Count - 1);
            if (points.Count < 2 || (closed && points.Count < 3)) return null;

            // A gateway runs straight through: the line's bends near each one are taken out (a bend in a thick wall's
            // passage would fold its stone back on itself), and the gateways found again on the straightened line
            if (gateways != null && gateways.Count > 0)
            {
                float thick = s.Thickness + (s.Battlements ? ParapetDepth : 0f);
                float clear = Mathf.Clamp(s.GateWidth, 2f, 8f) * 0.5f + thick + 1.5f;
                var spots = new List<Vector3>();
                foreach (float g in gateways) spots.Add(Flat(PointAlong(points, closed, g, out _)));
                for (int i = points.Count - 1; i >= 0; i--)
                {
                    if (!closed && (i == 0 || i == points.Count - 1)) continue;
                    if (points.Count <= (closed ? 3 : 2)) break;
                    foreach (Vector3 spot in spots)
                    {
                        if (Flat(points[i] - spot).magnitude < clear) { points.RemoveAt(i); break; }
                    }
                }
                var moved = new List<float>();
                foreach (Vector3 spot in spots) moved.Add(DistanceAlong(points, closed, spot, out _));
                gateways = moved;
                path = new List<Vector3>(points); // (kept straightened, so the gateways stay where they are)
            }

            // How far along each point is, and the whole length
            var along = new List<float> { 0f };
            for (int i = 1; i < points.Count; i++) along.Add(along[i - 1] + (points[i] - points[i - 1]).magnitude);
            float total = closed ? along[along.Count - 1] + (points[0] - points[points.Count - 1]).magnitude : along[along.Count - 1];
            float from = closed ? 0f : Mathf.Max(0f, startInset), to = closed ? total : total - Mathf.Max(0f, endInset);
            if (to - from < 1f) return null;

            float half = s.Thickness + (s.Battlements ? ParapetDepth : 0f);   // the outside faces, from the middle
            float walk = s.Thickness;                                       // the walkway's edges
            float height = s.Storeys * Storey;

            // The gateways: each an arch (straight sides, a round top) centred where asked, kept apart and off the ends
            var gates = new List<Arch>();
            if (gateways != null)
            {
                float width = Mathf.Clamp(s.GateWidth, 2f, 8f);
                float tall = Mathf.Clamp(s.GateHeight, width * 0.5f + 1f, Mathf.Max(width * 0.5f + 1f, height - 1.2f));
                var wanted = new List<float>(gateways);
                wanted.Sort();
                foreach (float centre in wanted)
                {
                    float w = Mathf.Min(width, (to - from) - 1f);
                    if (w < 1.6f) continue;
                    float c = closed ? Mathf.Repeat(centre, total) : Mathf.Clamp(centre, from + w * 0.5f + 0.4f, to - w * 0.5f - 0.4f);
                    if (closed && (c - w * 0.5f < 0.3f || c + w * 0.5f > total - 0.3f)) c = Mathf.Clamp(c, w * 0.5f + 0.3f, total - w * 0.5f - 0.3f);
                    if (gates.Count > 0 && c - w * 0.5f < gates[gates.Count - 1].End + 1f) continue; // (no room beside the last)
                    float floor = ground(PointAt(points, along, total, closed, c, out _, out _)) ?? 0f;
                    gates.Add(new Arch { Centre = c, Radius = w * 0.5f, Floor = floor, Spring = floor + Mathf.Max(0.8f, tall - w * 0.5f) });
                }
            }

            // The openings, cut right through the wall (Details > Windows and slits): every so often along it, off the
            // gateways and the ends, a row near the top and another lower down on tall walls; each a narrow or wide
            // arched window with a sill, or an arrow slit, as the kind asks
            var windows = new List<Arch>();
            Details detail = s.Detail;
            if (detail != null && detail.ArrowSlits && detail.SlitEvery > 0.5f)
            {
                var pick = new System.Random(Seed(path) ^ 0x5f3759);
                for (float d = from + detail.SlitEvery * 0.5f; d < to; d += detail.SlitEvery)
                {
                    bool wide = pick.NextDouble() < 0.35;
                    float room = wide ? 0.7f : 0.45f;
                    if (d - room < from + 1.4f || d + room > to - 1.4f) continue;
                    bool near = false;
                    foreach (Arch g in gates) if (Mathf.Abs(d - g.Centre) < g.Radius + room + 1.4f) near = true;
                    if (near) continue;
                    // (the ground here, smoothed as the wall's top is, so the openings sit the same way under it)
                    float sum = 0f;
                    int count = 0;
                    for (float o = -3f; o <= 3.01f; o += 1.5f)
                    {
                        float? gr = ground(PointAt(points, along, total, closed, d + o, out _, out _));
                        if (gr.HasValue) { sum += gr.Value; count++; }
                    }
                    if (count == 0) continue;
                    float wallTop = sum / count + height;
                    float here = ground(PointAt(points, along, total, closed, d, out _, out _)) ?? sum / count;
                    var rows = new List<float> { wallTop - 2.4f };
                    if (wallTop - here > 9f) rows.Add(here + (wallTop - here) * 0.45f);
                    for (int row = 0; row < rows.Count; row++)
                    {
                        float centre = rows[row];
                        if (OpeningIsWindow(detail, row))
                        {
                            float sill = centre - 0.9f, spring = sill + 1.8f - room;
                            if (spring + room > wallTop - 0.8f) continue;
                            windows.Add(new Arch { Centre = d, Radius = room, Floor = sill, Sill = sill, Spring = spring, Window = true });
                        }
                        else
                        {
                            const float slit = 0.11f;
                            float sill = centre - 0.7f;
                            if (centre + 0.7f > wallTop - 0.6f || sill < here + 0.5f) continue;
                            windows.Add(new Arch { Centre = d, Radius = slit, Floor = sill, Sill = sill, Spring = centre + 0.7f - slit, Window = true, Slit = true });
                        }
                    }
                }
            }
            var holes = new List<Arch>(gates);
            holes.AddRange(windows);

            // Where it's made: about every metre, at each bend, and closely round each arch
            var stations = new List<float>();
            for (float d = from; d < to; d += 1f) stations.Add(d);
            for (int i = 0; i < points.Count; i++) if (along[i] > from && along[i] < to) stations.Add(along[i]);
            stations.Add(to);
            foreach (Arch a in holes)
            {
                int steps = Mathf.Max(8, Mathf.CeilToInt(a.Radius * 2f / 0.2f));
                for (int k = 0; k <= steps; k++) stations.Add(Mathf.Lerp(a.Start, a.End, k / (float)steps));
            }
            stations.Sort();
            for (int i = stations.Count - 1; i > 0; i--)
            {
                if (stations[i] - stations[i - 1] >= 0.03f) continue;
                // (keep an arch's edges, and the wall's very end)
                bool keepThis = IsEdge(holes, stations[i]) || (!closed && i == stations.Count - 1);
                stations.RemoveAt(keepThis ? i - 1 : i);
            }
            if (closed && stations.Count > 1 && total - stations[stations.Count - 1] < 0.05f) stations.RemoveAt(stations.Count - 1);

            // At a sharp bend the faces swing out (outside) and in (inside) a long way from the corner: anything made closer
            // to the corner than that would fold back on itself (a hole, smeared stone), so nothing's made there but the
            // corner itself
            for (int c = 0; c < points.Count; c++)
            {
                if (!closed && (c == 0 || c == points.Count - 1)) continue;
                Vector3 inDir = (points[c] - points[(c - 1 + points.Count) % points.Count]).normalized;
                Vector3 outDir = (points[(c + 1) % points.Count] - points[c]).normalized;
                float turn = Vector3.Angle(inDir, outDir) * Mathf.Deg2Rad;
                if (turn < 0.03f) continue; // (all but the straightest: a thick wall folds even at gentle bends)
                float reach = Mathf.Min(half * Mathf.Tan(turn * 0.5f) + 0.1f, half * 3f);
                float corner = along[c];
                stations.RemoveAll(st =>
                {
                    float d = Mathf.Abs(st - corner);
                    if (closed) d = Mathf.Min(d, total - d);
                    return d > 0.02f && d < reach && !IsEdge(holes, st) && st > from + 0.01f && st < to - 0.01f;
                });
            }

            int n = stations.Count;
            var at = new Vector3[n];
            var side = new Vector3[n];     // across the wall, and how far a face is out at a bend (mitred)
            var lean = new Vector3[n];     // which way the faces look
            var tangent = new Vector3[n];
            var groundMid = new float[n];
            var bottom = new float[n];
            for (int i = 0; i < n; i++)
            {
                at[i] = PointAt(points, along, total, closed, stations[i], out tangent[i], out Vector3 mitre);
                lean[i] = Vector3.Cross(tangent[i], Vector3.down).normalized;
                side[i] = mitre;
                float c = ground(at[i]) ?? 0f;
                float l = ground(at[i] + side[i] * half) ?? c, r = ground(at[i] - side[i] * half) ?? c;
                groundMid[i] = c;
                bottom[i] = Mathf.Min(c, Mathf.Min(l, r)) - Footing;
            }

            // The top follows the ground smoothly (an average over a few metres), never stepping
            var top = new float[n];
            for (int i = 0; i < n; i++)
            {
                float sum = 0f, weight = 0f;
                for (int j = 0; j < n; j++)
                {
                    float d = Mathf.Abs(stations[j] - stations[i]);
                    if (closed) d = Mathf.Min(d, total - d);
                    if (d > 4f) continue;
                    float w = 1f - d / 4.5f;
                    sum += groundMid[j] * w;
                    weight += w;
                }
                top[i] = sum / Mathf.Max(weight, 0.0001f) + height;
            }

            // Each face's own length along it, for its stone (so it isn't stretched round the outside of a bend, or squashed
            // round the inside): the outsides, then the parapets' insides
            float[] offsets = { half, -half, walk, -walk };
            var faceU = new float[4][];
            for (int f = 0; f < 4; f++)
            {
                faceU[f] = new float[n + 1];
                float sum = stations[0];
                faceU[f][0] = sum;
                for (int i = 1; i <= n; i++)
                {
                    int k = i % n;
                    sum += Vector3.Distance(at[i - 1] + side[i - 1] * offsets[f], at[k] + side[k] * offsets[f]);
                    faceU[f][i] = sum;
                }
            }

            Vector3 origin = at[0];
            var b = new Shape(origin);
            float parapet(int i) => top[i] + (s.Battlements ? ParapetHeight : 0f);

            int segments = closed ? n : n - 1;
            for (int i = 0; i < segments; i++)
            {
                int j = (i + 1) % n;
                float d0 = stations[i], d1 = j == 0 ? total : stations[j];
                float u0 = d0 / Tile, u1 = d1 / Tile;
                // The openings this stretch is in, lowest first (a gateway, or windows and slits one above another)
                List<Arch> over = ArchesOver(holes, d0, d1);
                foreach (float face in new[] { 1f, -1f })
                {
                    Vector3 a0 = at[i] + side[i] * (half * face), a1 = at[j] + side[j] * (half * face);
                    Vector3 n0 = lean[i] * face, n1 = lean[j] * face;
                    int fi = face > 0f ? 0 : 1, pi = face > 0f ? 2 : 3;
                    float fu0 = faceU[fi][i] / Tile, fu1 = faceU[fi][i + 1] / Tile, pu0 = faceU[pi][i] / Tile, pu1 = faceU[pi][i + 1] / Tile;
                    // The outside faces, from the footing up to the parapet's top, broken by each opening on the way
                    float from0 = bottom[i], from1 = bottom[j];
                    foreach (Arch arch in over)
                    {
                        if (arch.Window)
                        {
                            b.Quad(Y(a0, from0), Y(a1, from1), Y(a1, arch.Sill), Y(a0, arch.Sill), n0, n1, n1, n0,
                                   new Vector2(fu0, from0 / Tile), new Vector2(fu1, from1 / Tile), new Vector2(fu1, arch.Sill / Tile), new Vector2(fu0, arch.Sill / Tile), false);
                        }
                        from0 = arch.Height(d0);
                        from1 = arch.Height(d1);
                    }
                    b.Quad(Y(a0, from0), Y(a1, from1), Y(a1, parapet(j)), Y(a0, parapet(i)), n0, n1, n1, n0,
                           new Vector2(fu0, from0 / Tile), new Vector2(fu1, from1 / Tile), new Vector2(fu1, parapet(j) / Tile), new Vector2(fu0, parapet(i) / Tile), false);
                    if (s.Battlements)
                    {
                        // The parapet: its inside face and its top
                        Vector3 w0 = at[i] + side[i] * (walk * face), w1 = at[j] + side[j] * (walk * face);
                        b.Quad(Y(w0, top[i]), Y(w1, top[j]), Y(w1, parapet(j)), Y(w0, parapet(i)), -n0, -n1, -n1, -n0,
                               new Vector2(pu0, top[i] / Tile), new Vector2(pu1, top[j] / Tile), new Vector2(pu1, parapet(j) / Tile), new Vector2(pu0, parapet(i) / Tile), false);
                        b.Quad(Y(w0, parapet(i)), Y(w1, parapet(j)), Y(a1, parapet(j)), Y(a0, parapet(i)), Vector3.up, Vector3.up, Vector3.up, Vector3.up,
                               new Vector2(u0, 0f), new Vector2(u1, 0f), new Vector2(u1, ParapetDepth / Tile), new Vector2(u0, ParapetDepth / Tile), false);
                    }
                }
                // The walkway along the top
                Vector3 l0 = at[i] + side[i] * walk, l1 = at[j] + side[j] * walk, r0 = at[i] - side[i] * walk, r1 = at[j] - side[j] * walk;
                b.Quad(Y(r0, top[i]), Y(r1, top[j]), Y(l1, top[j]), Y(l0, top[i]), Vector3.up, Vector3.up, Vector3.up, Vector3.up,
                       new Vector2(u0, 0f), new Vector2(u1, 0f), new Vector2(u1, walk * 2f / Tile), new Vector2(u0, walk * 2f / Tile), true);
                foreach (Arch arch in over)
                {
                    // Each opening's head (its underside, facing in toward the middle of its curve), and its sill (a
                    // window's, the wall's own stone) or floor (a gateway's, paved), right through the wall
                    float low0 = arch.Height(d0), low1 = arch.Height(d1);
                    Vector3 o0 = at[i] + side[i] * half, o1 = at[j] + side[j] * half, i0 = at[i] - side[i] * half, i1 = at[j] - side[j] * half;
                    Vector3 in0 = arch.Inward(d0, tangent[i]), in1 = arch.Inward(d1, tangent[j]);
                    b.Quad(Y(i0, low0), Y(i1, low1), Y(o1, low1), Y(o0, low0), in0, in1, in1, in0,
                           new Vector2(u0, 0f), new Vector2(u1, 0f), new Vector2(u1, half * 2f / Tile), new Vector2(u0, half * 2f / Tile), false);
                    float floorY = arch.Window ? arch.Sill : arch.Floor + 0.04f;
                    b.Quad(Y(i0, floorY), Y(i1, floorY), Y(o1, floorY), Y(o0, floorY), Vector3.up, Vector3.up, Vector3.up, Vector3.up,
                           new Vector2(u0, 0f), new Vector2(u1, 0f), new Vector2(u1, half * 2f / Tile), new Vector2(u0, half * 2f / Tile), !arch.Window);
                }
            }

            // Each gateway's sides: the wall's cut ends, facing into the way through, up to where the arch starts
            for (int i = 0; i < n; i++)
            {
                foreach (Arch a in holes)
                {
                    bool start = Mathf.Abs(stations[i] - a.Start) < 0.02f, end = Mathf.Abs(stations[i] - a.End) < 0.02f;
                    if (start || end) Cap(b, at[i], side[i], half, a.Window ? a.Sill : bottom[i], a.Spring, start ? tangent[i] : -tangent[i]);
                }
            }
            // The ends of a wall that doesn't go round (usually inside a tower; seen when there isn't one)
            if (!closed)
            {
                Cap(b, at[0], side[0], half, bottom[0], parapet(0), -tangent[0]);
                Cap(b, at[n - 1], side[n - 1], half, bottom[n - 1], parapet(n - 1), tangent[n - 1]);
            }

            GameObject wall = b.Build(gates.Count > 0 ? "Wall with Gate" : "Wall", parent);

            // Merlons along both parapets, about every 2 m
            if (s.Battlements)
            {
                int count = Mathf.Max(1, Mathf.RoundToInt((to - from) / 2f));
                float step = (to - from) / count;
                for (int m = 0; m < count; m++)
                {
                    float d = from + step * (m + 0.5f);
                    Vector3 p = PointAt(points, along, total, closed, d, out Vector3 t, out _);
                    float y = Interpolate(stations, top, d, closed, total) + ParapetHeight + MerlonHeight * 0.5f;
                    Vector3 across = Vector3.Cross(t, Vector3.down).normalized;
                    foreach (float face in new[] { 1f, -1f })
                    {
                        Vector3 spot = p + across * (face * (walk + ParapetDepth * 0.5f));
                        Block(wall.transform, Y(spot, y) - origin, new Vector3(Mathf.Min(1.1f, step * 0.55f), MerlonHeight, ParapetDepth), Quaternion.LookRotation(across * face, Vector3.up));
                    }
                }
            }

            // Which side is the inside (for stairs, torches, doors): as asked, or toward 'inside', or the middle of a ring
            int inner = innerSide != 0 ? innerSide : 1;
            if (innerSide == 0)
            {
                Vector3 centre = Vector3.zero;
                foreach (Vector3 p in points) centre += p;
                Vector3 toward = inside.HasValue ? Flat(inside.Value) : centre / points.Count;
                Vector3 mid = PointAt(points, along, total, closed, (from + to) * 0.5f, out Vector3 tMid, out _);
                float dot = Vector3.Dot(Vector3.Cross(tMid, Vector3.down).normalized, toward - mid);
                if (Mathf.Abs(dot) > 0.5f) inner = dot > 0f ? 1 : -1;
            }
            if (s.Detail != null)
            {
                WallDetails(wall.transform, origin, d => PointAt(points, along, total, closed, d, out _, out _),
                            d => { PointAt(points, along, total, closed, d, out Vector3 t, out _); return t; },
                            d => Interpolate(stations, top, d, closed, total), from, to, half, walk, height, gates, inner, s, ground, windows,
                            Seed(path));
            }

            var piece = wall.AddComponent<CastlePiece>();
            piece.inner = inner;
            piece.tower = false;
            piece.path = new List<Vector3>(path);
            piece.closed = closed;
            piece.fromHalf = startInset;
            piece.toHalf = endInset;
            piece.from = path[0];
            piece.to = path[path.Count - 1];
            piece.gates = new List<float>();
            foreach (Arch a in gates) piece.gates.Add(a.Centre);
            piece.gate = gates.Count > 0;
            if (gates.Count > 0) piece.gateWidth = gates[0].Radius * 2f;
            piece.width = half * 2f;
            Vector3 lo = Vector3.positiveInfinity, hi = Vector3.negativeInfinity;
            foreach (Vector3 p in at) { lo = Vector3.Min(lo, p); hi = Vector3.Max(hi, p); }
            piece.footprint = new Vector2(hi.x - lo.x + half * 2f, hi.z - lo.z + half * 2f);
            if (BuilderSettings.Get().mergeDetails) MeshMerger.Merge(wall, false);
            Finish(wall, s);
            return wall;
        }

        // A gateway through a wall: centred 'Centre' metres along it, 'Radius' half its width; straight sides from the
        // ground ('Floor') up to 'Spring', then a half-round top
        private sealed class Arch
        {
            public float Centre, Radius, Floor, Spring;
            public bool Window;   // a window: raised off the ground, the wall solid below it
            public bool Slit;     // (a window) an arrow slit
            public float Sill;    // (a window) the bottom of its opening
            public float Start => Centre - Radius;
            public float End => Centre + Radius;

            /// <summary>The height of the arch's underside at a distance along the wall.</summary>
            public float Height(float distance)
            {
                float x = Mathf.Clamp(distance - Centre, -Radius, Radius);
                return Spring + Mathf.Sqrt(Mathf.Max(0f, Radius * Radius - x * x));
            }

            /// <summary>Which way the underside looks there: in, toward the middle of its curve.</summary>
            public Vector3 Inward(float distance, Vector3 along)
            {
                float x = Mathf.Clamp(distance - Centre, -Radius, Radius);
                float y = Height(distance) - Spring;
                return -(along * x + Vector3.up * y).normalized;
            }
        }

        // The arch a stretch of wall (from one distance along it to another) is over, if any
        // Every opening a stretch of wall is in, lowest first
        private static List<Arch> ArchesOver(List<Arch> holes, float d0, float d1)
        {
            var over = new List<Arch>();
            foreach (Arch a in holes) if (d0 >= a.Start - 0.01f && d1 <= a.End + 0.01f) over.Add(a);
            over.Sort((x, y) => (x.Window ? x.Sill : x.Floor).CompareTo(y.Window ? y.Sill : y.Floor));
            return over;
        }

        private static Arch ArchOver(List<Arch> gates, float d0, float d1)
        {
            foreach (Arch a in gates) if (d0 >= a.Start - 0.01f && d1 <= a.End + 0.01f) return a;
            return null;
        }

        private static bool IsEdge(List<Arch> gates, float d)
        {
            foreach (Arch a in gates) if (Mathf.Abs(d - a.Start) < 0.02f || Mathf.Abs(d - a.End) < 0.02f) return true;
            return false;
        }

        /// <summary>Where a point is along a wall's line (metres from its start), and how far from the line it is.</summary>
        internal static float DistanceAlong(List<Vector3> path, bool closed, Vector3 point, out float away)
        {
            float best = float.MaxValue, at = 0f, sum = 0f;
            int count = path.Count, segments = closed ? count : count - 1;
            Vector3 p = Flat(point);
            for (int i = 0; i < segments; i++)
            {
                Vector3 a = Flat(path[i]), b = Flat(path[(i + 1) % count]);
                Vector3 ab = b - a;
                float length = ab.magnitude;
                float t = length > 0.0001f ? Mathf.Clamp01(Vector3.Dot(p - a, ab) / (length * length)) : 0f;
                float d = (a + ab * t - p).magnitude;
                if (d < best) { best = d; at = sum + t * length; }
                sum += length;
            }
            away = best;
            return at;
        }

        /// <summary>The length of a wall's line.</summary>
        internal static float Length(List<Vector3> path, bool closed)
        {
            float sum = 0f;
            for (int i = 1; i < path.Count; i++) sum += Flat(path[i] - path[i - 1]).magnitude;
            if (closed && path.Count > 2) sum += Flat(path[0] - path[path.Count - 1]).magnitude;
            return sum;
        }

        /// <summary>The point a distance along a wall's line (on the ground, from above), and which way it runs there.</summary>
        internal static Vector3 PointAlong(List<Vector3> path, bool closed, float distance, out Vector3 direction)
        {
            var points = new List<Vector3>();
            foreach (Vector3 p in path) points.Add(Flat(p));
            var along = new List<float> { 0f };
            for (int i = 1; i < points.Count; i++) along.Add(along[i - 1] + (points[i] - points[i - 1]).magnitude);
            float total = closed ? along[along.Count - 1] + (points[0] - points[points.Count - 1]).magnitude : along[along.Count - 1];
            return PointAt(points, along, total, closed, distance, out direction, out _);
        }

        /// <summary>
        /// How a gateway of these settings would sit in a wall: its arch's outline on the wall's face, from one foot round
        /// to the other (for showing where one would go).
        /// </summary>
        internal static List<Vector3> ArchOutline(List<Vector3> path, bool closed, float centre, float faceOut, Settings s, GroundAt ground)
        {
            float width = Mathf.Clamp(s.GateWidth, 2f, 8f), radius = width * 0.5f;
            float tall = Mathf.Clamp(s.GateHeight, radius + 1f, Mathf.Max(radius + 1f, s.Storeys * Storey - 1.2f));
            Vector3 middle = PointAlong(path, closed, centre, out Vector3 along);
            Vector3 across = Vector3.Cross(along, Vector3.down).normalized;
            float floor = ground(middle) ?? 0f, spring = floor + Mathf.Max(0.8f, tall - radius);
            var outline = new List<Vector3>();
            for (int k = 0; k <= 24; k++)
            {
                float x = Mathf.Lerp(-radius, radius, k / 24f);
                float y = k == 0 || k == 24 ? floor : spring + Mathf.Sqrt(Mathf.Max(0f, radius * radius - x * x));
                Vector3 p = PointAlong(path, closed, centre + x, out _) + across * faceOut;
                outline.Add(new Vector3(p.x, y, p.z));
            }
            return outline;
        }

        /// <summary>Older callers: a wall with a gate in its middle (or none).</summary>
        internal static GameObject Run(List<Vector3> path, float startInset, float endInset, bool closed, bool gateway, Settings s, GroundAt ground, Transform parent)
        {
            List<float> gates = null;
            if (gateway)
            {
                float total = Length(path, closed);
                float from = closed ? 0f : startInset, to = closed ? total : total - endInset;
                gates = new List<float> { (from + to) * 0.5f };
            }
            return Run(path, startInset, endInset, closed, gates, s, ground, parent);
        }

        /// <summary>A straight wall between two towers (their middles and half widths), for older callers.</summary>
        internal static GameObject Wall(Vector3 from, Vector3 to, float fromHalf, float toHalf, bool gateway, Settings s, GroundAt ground, Transform parent) =>
            Run(new List<Vector3> { from, to }, fromHalf - 0.3f, toHalf - 0.3f, false, gateway, s, ground, parent);

        // A cross-section of the wall at a point (an end, or a gateway's side), facing 'outward'
        private static void Cap(Shape b, Vector3 at, Vector3 side, float half, float low, float high, Vector3 outward)
        {
            Vector3 l = at + side * half, r = at - side * half;
            float w = half * 2f / Tile;
            b.Quad(Y(r, low), Y(l, low), Y(l, high), Y(r, high), outward, outward, outward, outward,
                   new Vector2(0f, low / Tile), new Vector2(w, low / Tile), new Vector2(w, high / Tile), new Vector2(0f, high / Tile), false);
        }

        // The point 'distance' along a line of points, which way it runs there, and (at a bend) how far out to the left a
        // face has to be for it to stay the wall's thickness away (the bend mitred)
        private static Vector3 PointAt(List<Vector3> points, List<float> along, float total, bool closed, float distance, out Vector3 tangent, out Vector3 mitre)
        {
            int count = points.Count, segments = closed ? count : count - 1;
            if (closed) distance = Mathf.Repeat(distance, total);
            distance = Mathf.Clamp(distance, 0f, total);
            for (int i = 0; i < segments; i++)
            {
                int j = (i + 1) % count;
                float start = along[i], end = j == 0 ? total : along[j];
                if (distance > end + 0.001f && i < segments - 1) continue;
                Vector3 direction = (points[j] - points[i]).normalized;
                Vector3 left = Vector3.Cross(direction, Vector3.down).normalized;
                tangent = direction;
                mitre = left;
                // At a bend (its corner point), the faces meet halfway between the two ways
                bool atStart = Mathf.Abs(distance - start) < 0.01f && (i > 0 || closed);
                bool atEnd = Mathf.Abs(distance - end) < 0.01f && (j > 0 || closed) && (closed || j < count - 1 || false);
                if (atStart || atEnd)
                {
                    int corner = atStart ? i : j;
                    int before = (corner - 1 + count) % count, after = (corner + 1) % count;
                    if (closed || (corner > 0 && corner < count - 1))
                    {
                        Vector3 inDir = (points[corner] - points[before]).normalized, outDir = (points[after] - points[corner]).normalized;
                        Vector3 inLeft = Vector3.Cross(inDir, Vector3.down).normalized, outLeft = Vector3.Cross(outDir, Vector3.down).normalized;
                        Vector3 mid = (inLeft + outLeft).normalized;
                        float cos = Mathf.Max(0.35f, Vector3.Dot(mid, outLeft));
                        mitre = mid / cos;
                        tangent = (inDir + outDir).normalized;
                    }
                }
                float t = end - start > 0.0001f ? (distance - start) / (end - start) : 0f;
                return Vector3.Lerp(points[i], points[j], Mathf.Clamp01(t));
            }
            tangent = Vector3.forward;
            mitre = Vector3.left;
            return points[count - 1];
        }

        private static float Interpolate(List<float> stations, float[] values, float distance, bool closed, float total)
        {
            int n = stations.Count;
            for (int i = 0; i < n - 1; i++)
            {
                if (distance <= stations[i + 1]) return Mathf.Lerp(values[i], values[i + 1], Mathf.InverseLerp(stations[i], stations[i + 1], distance));
            }
            if (closed && n > 1) return Mathf.Lerp(values[n - 1], values[0], Mathf.InverseLerp(stations[n - 1], total, distance));
            return values[n - 1];
        }

        // ================================================================ Towers

        /// <summary>A watch tower standing at 'centre' (square, turned 'yaw' degrees to line up with its walls, or round).</summary>
        internal static GameObject Tower(Vector3 centre, float yaw, Settings settings, GroundAt ground, Transform parent)
        {
            // Mixed: each tower one of the styles, picked from where it stands (so it's the same if made again)
            TowerStyle style = settings.Style;
            if (style == TowerStyle.Mixed)
            {
                var styles = new[] { TowerStyle.Square, TowerStyle.Round, TowerStyle.Octagon, TowerStyle.Tapered, TowerStyle.Turreted, TowerStyle.Stepped, TowerStyle.Hoarded };
                style = styles[Mathf.Abs(Seed(new List<Vector3> { centre })) % styles.Length];
            }
            Settings s = settings.Copy();
            s.Style = style;

            float half = TowerHalf(s), width = half * 2f;
            Quaternion turn = Quaternion.Euler(0f, yaw, 0f);
            float middle = ground(centre) ?? centre.y, low = middle;
            for (int a = 0; a < 8; a++)
            {
                Vector3 edge = turn * (Quaternion.Euler(0f, a * 45f, 0f) * Vector3.forward) * half * 1.3f;
                float? h = ground(centre + edge);
                if (h.HasValue) low = Mathf.Min(low, h.Value);
            }
            float bottom = low - Footing;
            float top = middle + (s.Storeys + s.TowerExtra) * Storey;
            Vector3 origin = Flat(centre);
            var b = new Shape(origin);
            int roundSides = Mathf.Clamp(Mathf.RoundToInt(2f * Mathf.PI * half / 0.9f), 16, 48);
            var surrounds = new List<(Vector3 face, Vector3 outward, Arch hole)>();
            float floorY = middle + 0.02f;

            // The top's shape (for its roof or battlements): how many sides, turned how, how far out its faces are
            int topSides = 4;
            float topAngle = yaw + 45f, topHalf = half, roofAt = top;
            float detailHalf = half, detailTop = top;
            switch (style)
            {
                case TowerStyle.Round:
                case TowerStyle.Hoarded:
                    Shell(b, origin, roundSides, 0f, half, bottom, top, TowerWall, floorY, TowerOpenings(s, roundSides, half, top, middle, yaw), surrounds);
                    topSides = roundSides; topAngle = 0f;
                    break;
                case TowerStyle.Octagon:
                    Shell(b, origin, 8, yaw, half, bottom, top, TowerWall, floorY, TowerOpenings(s, 8, half, top, middle), surrounds);
                    topSides = 8; topAngle = yaw;
                    break;
                case TowerStyle.Tapered:
                {
                    // A flared foot: wide at the ground, drawing in to the tower's width a third of the way up
                    float flareTop = middle + (top - middle) * 0.35f;
                    Prism(b, origin, roundSides, 0f, half * 1.35f, half * 1.35f, bottom, middle, false);
                    Prism(b, origin, roundSides, 0f, half * 1.35f, half, middle, flareTop, false);
                    Shell(b, origin, roundSides, 0f, half, flareTop, top, TowerWall, flareTop + 0.02f, TowerOpenings(s, roundSides, half, top, flareTop - 2f, yaw), surrounds);
                    topSides = roundSides; topAngle = 0f;
                    break;
                }
                case TowerStyle.Stepped:
                {
                    // A broad lower tier to just above the walls, a narrower upper one standing a storey higher
                    float lowerHalf = half * 1.15f, upperHalf = half * 0.72f;
                    float ledge = Mathf.Min(middle + s.Storeys * Storey + 1.5f, top - 1f);
                    float upperTop = top + Storey;
                    Shell(b, origin, 4, yaw + 45f, lowerHalf, bottom, ledge, TowerWall, floorY, TowerOpenings(s, 4, lowerHalf, ledge, middle), surrounds);
                    Prism(b, origin, 4, yaw + 45f, upperHalf, upperHalf, ledge, upperTop, true);
                    topHalf = upperHalf; roofAt = upperTop;
                    detailHalf = lowerHalf; detailTop = ledge;
                    break;
                }
                default: // Square, Turreted
                    Shell(b, origin, 4, yaw + 45f, half, bottom, top, TowerWall, floorY, TowerOpenings(s, 4, half, top, middle), surrounds);
                    break;
            }

            GameObject tower = b.Build("Tower", parent);
            Transform t = tower.transform;

            switch (style)
            {
                case TowerStyle.Stepped:
                    // Battlements round the ledge, then the upper tier's top
                    PolygonBattlements(t, 4, yaw + 45f, half * 1.15f, detailTop);
                    TowerTop(t, s, origin, 4, yaw + 45f, topHalf, roofAt);
                    break;
                case TowerStyle.Turreted:
                {
                    // Battlements round the top, and a little round turret on each corner, corbelled out
                    PolygonBattlements(t, 4, yaw + 45f, half, top);
                    const float turretR = 1.15f;
                    var turrets = new Shape(origin);
                    var turretTops = new List<Vector3>();
                    foreach (Vector2 c in new[] { new Vector2(1, 1), new Vector2(1, -1), new Vector2(-1, 1), new Vector2(-1, -1) })
                    {
                        Vector3 at = origin + turn * new Vector3(c.x * (half - 0.2f), 0f, c.y * (half - 0.2f));
                        Prism(turrets, at, 14, 0f, 0.35f, turretR, top - 4.2f, top - 3.2f, false);
                        Prism(turrets, at, 14, 0f, turretR, turretR, top - 3.2f, top + 2.4f, true);
                        turretTops.Add(at);
                    }
                    turrets.Build("Turrets", t).transform.SetParent(t, true);
                    foreach (Vector3 at in turretTops)
                    {
                        if (s.TowerRoofs) Roof(t, at, turretR * 2f, top + 2.45f);
                        else RoundBattlements(t, at - origin, turretR, top + 2.4f, 0.7f);
                    }
                    break;
                }
                case TowerStyle.Hoarded:
                {
                    // A wooden gallery overhanging the top on brackets, with a roof over it
                    Material wood = DetailMaterial("Castle Wood", new Color(0.33f, 0.21f, 0.12f), 0f, 0.15f);
                    Material dark = DetailMaterial("Castle Wood Dark", new Color(0.22f, 0.14f, 0.08f), 0f, 0.1f);
                    float outR = half + 0.7f, galleryTop = top + 2.3f;
                    int boards = Mathf.Clamp(Mathf.RoundToInt(2f * Mathf.PI * outR / 0.9f), 16, 48);
                    float chord = 2f * outR * Mathf.Sin(Mathf.PI / boards) + 0.06f;
                    for (int i = 0; i < boards; i++)
                    {
                        float a = (i + 0.5f) * Mathf.PI * 2f / boards;
                        var d = new Vector3(Mathf.Sin(a), 0f, Mathf.Cos(a));
                        Quaternion face = Quaternion.LookRotation(d, Vector3.up);
                        Block(t, d * outR + Vector3.up * (top + 1.1f), new Vector3(chord, 2.2f, 0.14f), face, wood);              // the boards
                        Block(t, d * (outR - 0.35f) + Vector3.up * (top - 0.05f), new Vector3(chord, 0.12f, 0.75f), face, dark);  // its floor
                        if (i % 2 == 0)
                        {
                            Block(t, d * (half + 0.35f) + Vector3.up * (top - 0.55f), new Vector3(0.18f, 0.9f, 0.7f), face, dark);   // a bracket
                            Block(t, d * (outR + 0.02f) + Vector3.up * (top + 1.5f), new Vector3(0.35f, 0.5f, 0.06f), face,          // a loophole
                                  DetailMaterial("Castle Dark", new Color(0.05f, 0.045f, 0.04f), 0f, 0f));
                        }
                    }
                    Roof(t, origin, outR * 2f, galleryTop);
                    roofAt = galleryTop;
                    topHalf = outR;
                    break;
                }
                default:
                    TowerTop(t, s, origin, topSides, topAngle, topHalf, roofAt);
                    break;
            }

            if (s.Detail != null)
            {
                float flag = style == TowerStyle.Turreted && s.TowerRoofs ? top + 0.1f
                           : style == TowerStyle.Hoarded || (s.TowerRoofs && style != TowerStyle.Turreted) ? RoofPeak(topHalf * 2f, roofAt) : roofAt;
                TowerDetails(t, origin, detailHalf, detailTop, middle, yaw, s, ground, Seed(new List<Vector3> { centre }), surrounds, flag);
            }

            var piece = tower.AddComponent<CastlePiece>();
            piece.tower = true;
            piece.width = style == TowerStyle.Stepped ? half * 2.3f : width;
            piece.round = IsRound(style);
            // (its shape is built turned, the object isn't: the ground it covers, square to the world, holds it whichever way)
            float across = style == TowerStyle.Tapered ? width * 1.35f : piece.width;
            float reach = piece.round ? across : across * (Mathf.Abs(Mathf.Cos(yaw * Mathf.Deg2Rad)) + Mathf.Abs(Mathf.Sin(yaw * Mathf.Deg2Rad)));
            piece.footprint = new Vector2(reach, reach);
            if (BuilderSettings.Get().mergeDetails) MeshMerger.Merge(tower, false);
            Finish(tower, s);
            return tower;
        }

        /// <summary>Round in plan (walls meet its curve), for the styles that are.</summary>
        internal static bool IsRound(TowerStyle style) => style == TowerStyle.Round || style == TowerStyle.Tapered || style == TowerStyle.Hoarded || style == TowerStyle.Octagon;

        // A tower's top: a pointed roof, or battlements round its rim
        private static void TowerTop(Transform t, Settings s, Vector3 origin, int sides, float angle, float half, float top)
        {
            if (s.TowerRoofs) Roof(t, origin, half * 2f, top);
            else if (sides > 8) RoundBattlements(t, Vector3.zero, half, top, 1.1f);
            else PolygonBattlements(t, sides, angle, half, top);
        }

        // The kit's pointed tower roof, sized to sit on a top 'across' metres wide, at 'at' (in the world), 'top' high
        private static void Roof(Transform t, Vector3 at, float across, float top)
        {
            Vector3 size = MedievalHouseBuilder.PieceSize(TowerRoof);
            float fit = size.x > 0.01f ? (across + 0.8f) / Mathf.Max(size.x, size.z) : 1f;
            GameObject roof = MedievalHouseBuilder.Piece(TowerRoof, t, Vector3.zero, Vector3.forward, MedievalHouseBuilder.Turn.None, Vector3.one * fit);
            if (roof != null) roof.transform.position = new Vector3(at.x, top + 0.05f, at.z);
        }

        private static float RoofPeak(float across, float top)
        {
            Vector3 size = MedievalHouseBuilder.PieceSize(TowerRoof);
            float fit = size.x > 0.01f ? (across + 0.8f) / Mathf.Max(size.x, size.z) : 1f;
            return top + size.y * fit * 0.92f;
        }

        // Battlements round a polygon's top: one along each side (its faces 'half' out, turned 'angle' degrees)
        private static void PolygonBattlements(Transform t, int sides, float angle, float half, float top)
        {
            float side = 2f * half * Mathf.Tan(Mathf.PI / sides);
            for (int i = 0; i < sides; i++)
            {
                float a = (angle + (i + 0.5f) * 360f / sides) * Mathf.Deg2Rad;
                var outward = new Vector3(Mathf.Sin(a), 0f, Mathf.Cos(a));
                Battlement(t, outward * half + Vector3.up * top, outward, side + 0.05f);
            }
        }

        // Battlements round a circle's rim ('centre' in the tower's space): a ring of parapet blocks, merlons about every 2 m
        private static void RoundBattlements(Transform t, Vector3 centre, float half, float top, float merlonWidth)
        {
            int blocks = Mathf.Clamp(Mathf.RoundToInt(2f * Mathf.PI * half / 1.2f), 8, 40);
            float chord = 2f * half * Mathf.Sin(Mathf.PI / blocks) + 0.08f;
            float depth = Mathf.Min(ParapetDepth, half * 0.35f);
            int merlons = Mathf.Max(4, Mathf.RoundToInt(2f * Mathf.PI * half / 2f));
            for (int i = 0; i < blocks; i++)
            {
                float a = (i + 0.5f) * Mathf.PI * 2f / blocks;
                var d = new Vector3(Mathf.Sin(a), 0f, Mathf.Cos(a));
                Block(t, centre + d * (half * Mathf.Cos(Mathf.PI / blocks) - depth * 0.5f) + Vector3.up * (top + ParapetHeight * 0.5f), new Vector3(chord, ParapetHeight, depth), Quaternion.LookRotation(d, Vector3.up));
            }
            for (int i = 0; i < merlons; i++)
            {
                float a = (i + 0.5f) * Mathf.PI * 2f / merlons;
                var d = new Vector3(Mathf.Sin(a), 0f, Mathf.Cos(a));
                Block(t, centre + d * (half - depth * 0.5f) + Vector3.up * (top + ParapetHeight + MerlonHeight * 0.5f), new Vector3(merlonWidth, MerlonHeight, depth), Quaternion.LookRotation(d, Vector3.up));
            }
        }

        // ---------------------------------------------------------------- A tower's walls, with openings right through

        private const float TowerWall = 0.9f; // how thick a tower's walls are (a room inside, seen through its openings)

        /// <summary>An opening in one of a tower's sides: which side, and the arch (its middle along the side is 0).</summary>
        private sealed class TowerHole
        {
            public int Face;
            public Arch Hole;
        }

        // Where a tower's openings go: rows down from near its top, on four sides (all of a square's, every other of an
        // octagon's, four evenly round a round one), each a window (if the side's wide enough) or an arrow slit
        private static List<TowerHole> TowerOpenings(Settings s, int sides, float half, float yTop, float yLow, float yaw = 0f)
        {
            var holes = new List<TowerHole>();
            Details k = s.Detail;
            if (k == null || !k.ArrowSlits) return holes;
            float faceLength = 2f * half * Mathf.Tan(Mathf.PI / sides);
            var faces = new List<int>();
            if (sides <= 4) for (int i = 0; i < sides; i++) faces.Add(i);
            else if (sides <= 8) for (int i = 0; i < sides; i += 2) faces.Add(i);
            else
            {
                // (round: the four ways at 45 degrees to the walls' line, so they look out rather than into a wall)
                for (int i = 0; i < 4; i++)
                {
                    float way = Mathf.Repeat(yaw + 45f + i * 90f, 360f);
                    faces.Add(Mathf.RoundToInt(way / (360f / sides) - 0.5f + sides) % sides);
                }
            }
            foreach (int face in faces)
            {
                int row = 0;
                for (float y = yTop - 2.6f; y > yLow + 3f; y -= 4.5f, row++)
                {
                    // (a window as wide as the side has room for: narrower on a round tower's narrow sides)
                    float r = Mathf.Min(0.45f, faceLength * 0.5f - 0.08f);
                    if (OpeningIsWindow(k, row) && r >= 0.3f)
                    {
                        float sill = y - 0.9f;
                        holes.Add(new TowerHole { Face = face, Hole = new Arch { Centre = 0f, Radius = r, Floor = sill, Sill = sill, Spring = sill + 1.8f - r, Window = true } });
                    }
                    else
                    {
                        const float slit = 0.11f;
                        holes.Add(new TowerHole { Face = face, Hole = new Arch { Centre = 0f, Radius = slit, Floor = y - 0.7f, Sill = y - 0.7f, Spring = y + 0.7f - slit, Window = true, Slit = true } });
                    }
                }
            }
            return holes;
        }

        /// <summary>
        /// A tower's body: a polygon of 'sides' sides in plan (its faces 'half' out from the middle, its corners turned
        /// 'angle' degrees; many sides: smooth), from 'y0' to 'y1', its walls 'thick' thick round a room with a floor at
        /// 'floorY', the openings cut right through (sills, heads and sides of stone), paved on top. Where each opening
        /// is on the outside is added to 'surrounds' (in the world: the middle of its side, which way out, the arch).
        /// </summary>
        private static void Shell(Shape b, Vector3 centre, int sides, float angle, float half, float y0, float y1, float thick, float floorY,
                                  List<TowerHole> holes, List<(Vector3 face, Vector3 outward, Arch hole)> surrounds)
        {
            float slice = Mathf.PI / sides, cos = Mathf.Cos(slice);
            float inner = Mathf.Max(half - thick, 0.4f);
            float faceLength = 2f * half * Mathf.Tan(slice), innerLength = 2f * inner * Mathf.Tan(slice);
            bool smooth = sides >= 12;
            for (int i = 0; i < sides; i++)
            {
                float a0 = angle * Mathf.Deg2Rad + i * 2f * slice, a1 = a0 + 2f * slice, am = a0 + slice;
                var d0 = new Vector3(Mathf.Sin(a0), 0f, Mathf.Cos(a0));
                var d1 = new Vector3(Mathf.Sin(a1), 0f, Mathf.Cos(a1));
                var dm = new Vector3(Mathf.Sin(am), 0f, Mathf.Cos(am));
                Vector3 along = (d1 - d0).normalized;
                Vector3 outerMid = centre + dm * half, innerMid = centre + dm * inner;
                var mine = new List<Arch>();
                foreach (TowerHole h in holes) if (h.Face == i) mine.Add(h.Hole);
                float u = i * faceLength;

                SideWithHoles(b, outerMid, along, faceLength, y0, y1, dm, d0, d1, smooth, false, mine, u);
                SideWithHoles(b, innerMid, along, innerLength, floorY, y1, dm, d0, d1, smooth, true, mine, u);

                foreach (Arch hole in mine)
                {
                    // The way through: the head's underside and the sill, across the wall's thickness, and the sides
                    var cuts = ArchCuts(hole);
                    for (int k = 0; k < cuts.Count - 1; k++)
                    {
                        float c0 = cuts[k], c1 = cuts[k + 1];
                        Vector3 o0 = outerMid + along * c0, o1 = outerMid + along * c1, i0 = innerMid + along * c0, i1 = innerMid + along * c1;
                        float h0 = hole.Height(c0), h1 = hole.Height(c1);
                        Vector3 n0 = hole.Inward(c0, along), n1 = hole.Inward(c1, along);
                        b.Quad(Y(o0, h0), Y(o1, h1), Y(i1, h1), Y(i0, h0), n0, n1, n1, n0,
                               new Vector2(c0 / Tile, 0f), new Vector2(c1 / Tile, 0f), new Vector2(c1 / Tile, thick / Tile), new Vector2(c0 / Tile, thick / Tile), false);
                        b.Quad(Y(o0, hole.Sill), Y(o1, hole.Sill), Y(i1, hole.Sill), Y(i0, hole.Sill), Vector3.up, Vector3.up, Vector3.up, Vector3.up,
                               new Vector2(c0 / Tile, 0f), new Vector2(c1 / Tile, 0f), new Vector2(c1 / Tile, thick / Tile), new Vector2(c0 / Tile, thick / Tile), false);
                    }
                    foreach (float side in new[] { -1f, 1f })
                    {
                        Vector3 o = outerMid + along * (side * hole.Radius), n = innerMid + along * (side * hole.Radius);
                        Vector3 facing = -along * side;
                        b.Quad(Y(o, hole.Sill), Y(n, hole.Sill), Y(n, hole.Spring), Y(o, hole.Spring), facing, facing, facing, facing,
                               new Vector2(0f, hole.Sill / Tile), new Vector2(thick / Tile, hole.Sill / Tile), new Vector2(thick / Tile, hole.Spring / Tile), new Vector2(0f, hole.Spring / Tile), false);
                    }
                    surrounds?.Add((outerMid, dm, hole));
                }

                // The top, paved over, and the room's floor
                float r = half / cos, ri = inner / cos;
                b.Triangle(Y(centre, y1), Y(centre + d0 * r, y1), Y(centre + d1 * r, y1), Vector3.up,
                           new Vector2(centre.x / Tile, centre.z / Tile), new Vector2((centre.x + d0.x * r) / Tile, (centre.z + d0.z * r) / Tile),
                           new Vector2((centre.x + d1.x * r) / Tile, (centre.z + d1.z * r) / Tile), true);
                b.Triangle(Y(centre, floorY), Y(centre + d0 * ri, floorY), Y(centre + d1 * ri, floorY), Vector3.up,
                           new Vector2(centre.x / Tile, centre.z / Tile), new Vector2((centre.x + d0.x * ri) / Tile, (centre.z + d0.z * ri) / Tile),
                           new Vector2((centre.x + d1.x * ri) / Tile, (centre.z + d1.z * ri) / Tile), true);
            }
        }

        // Where an opening's sides and head are cut: its edges, and closely across it so the round head is round
        private static List<float> ArchCuts(Arch hole)
        {
            var cuts = new List<float>();
            int steps = Mathf.Max(6, Mathf.CeilToInt(hole.Radius * 2f / 0.15f));
            for (int k = 0; k <= steps; k++) cuts.Add(Mathf.Lerp(-hole.Radius, hole.Radius, k / (float)steps));
            return cuts;
        }

        // One side of a tower (its middle 'mid', running 'along', 'length' long, from 'y0' to 'y1'), facing out (or in,
        // for the room's side), with its openings left open: stone below each, between them, and above
        private static void SideWithHoles(Shape b, Vector3 mid, Vector3 along, float length, float y0, float y1, Vector3 dm, Vector3 d0, Vector3 d1,
                                          bool smooth, bool inside, List<Arch> holes, float u)
        {
            var cuts = new List<float> { -length * 0.5f, length * 0.5f };
            foreach (Arch hole in holes) cuts.AddRange(ArchCuts(hole));
            cuts.Sort();
            for (int k = cuts.Count - 1; k > 0; k--) if (cuts[k] - cuts[k - 1] < 0.005f) cuts.RemoveAt(k);
            Vector3 Normal(float c)
            {
                Vector3 n = smooth ? Vector3.Lerp(d0, d1, Mathf.InverseLerp(-length * 0.5f, length * 0.5f, c)).normalized : dm;
                return inside ? -n : n;
            }
            for (int k = 0; k < cuts.Count - 1; k++)
            {
                float c0 = cuts[k], c1 = cuts[k + 1];
                Vector3 p0 = mid + along * c0, p1 = mid + along * c1;
                Vector3 n0 = Normal(c0), n1 = Normal(c1);
                float uu0 = (u + c0 + length * 0.5f) / Tile, uu1 = (u + c1 + length * 0.5f) / Tile;
                var over = new List<Arch>();
                foreach (Arch hole in holes) if (c0 >= hole.Start - 0.001f && c1 <= hole.End + 0.001f) over.Add(hole);
                over.Sort((a, b2) => a.Sill.CompareTo(b2.Sill));
                float from0 = y0, from1 = y0;
                foreach (Arch hole in over)
                {
                    if (hole.Sill > from0 + 0.001f)
                    {
                        b.Quad(Y(p0, from0), Y(p1, from1), Y(p1, hole.Sill), Y(p0, hole.Sill), n0, n1, n1, n0,
                               new Vector2(uu0, from0 / Tile), new Vector2(uu1, from1 / Tile), new Vector2(uu1, hole.Sill / Tile), new Vector2(uu0, hole.Sill / Tile), false);
                    }
                    from0 = hole.Height(c0);
                    from1 = hole.Height(c1);
                }
                b.Quad(Y(p0, from0), Y(p1, from1), Y(p1, y1), Y(p0, y1), n0, n1, n1, n0,
                       new Vector2(uu0, from0 / Tile), new Vector2(uu1, from1 / Tile), new Vector2(uu1, y1 / Tile), new Vector2(uu0, y1 / Tile), false);
            }
        }

        // The sides of a prism (a polygon in plan, its faces 'from' metres out at the bottom and 'to' at the top: a taper),
        // its corners turned 'angle' degrees, from 'y0' to 'y1', capped with paving on top if asked. Many sides: smooth.
        private static void Prism(Shape b, Vector3 centre, int sides, float angle, float from, float to, float y0, float y1, bool cap)
        {
            float slice = Mathf.PI / sides, cos = Mathf.Cos(slice);
            float r0 = from / cos, r1 = to / cos; // (out to the corners)
            bool smooth = sides >= 12;
            float perimeter = 2f * sides * from * Mathf.Tan(slice);
            float lean = Mathf.Atan2(from - to, Mathf.Max(0.01f, y1 - y0)); // (a taper tips the faces up)
            for (int i = 0; i < sides; i++)
            {
                float a0 = angle * Mathf.Deg2Rad + i * 2f * slice, a1 = a0 + 2f * slice, am = a0 + slice;
                var d0 = new Vector3(Mathf.Sin(a0), 0f, Mathf.Cos(a0));
                var d1 = new Vector3(Mathf.Sin(a1), 0f, Mathf.Cos(a1));
                var dm = new Vector3(Mathf.Sin(am), 0f, Mathf.Cos(am));
                Vector3 Tip(Vector3 d) => (d * Mathf.Cos(lean) + Vector3.up * Mathf.Sin(lean)).normalized;
                Vector3 n0 = Tip(smooth ? d0 : dm), n1 = Tip(smooth ? d1 : dm);
                float u0 = perimeter * i / sides / Tile, u1 = perimeter * (i + 1) / sides / Tile;
                b.Quad(Y(centre + d0 * r0, y0), Y(centre + d1 * r0, y0), Y(centre + d1 * r1, y1), Y(centre + d0 * r1, y1), n0, n1, n1, n0,
                       new Vector2(u0, y0 / Tile), new Vector2(u1, y0 / Tile), new Vector2(u1, y1 / Tile), new Vector2(u0, y1 / Tile), false);
                if (cap)
                {
                    b.Triangle(Y(centre, y1), Y(centre + d0 * r1, y1), Y(centre + d1 * r1, y1), Vector3.up,
                               new Vector2(centre.x / Tile, centre.z / Tile), new Vector2((centre.x + d0.x * r1) / Tile, (centre.z + d0.z * r1) / Tile),
                               new Vector2((centre.x + d1.x * r1) / Tile, (centre.z + d1.z * r1) / Tile), true);
                }
            }
        }

        /// <summary>A stone pillar (older walls had these at bends; walls are smooth now, so they don't need them).</summary>
        internal static GameObject Joint(Vector3 centre, float yaw, Settings s, GroundAt ground, Transform parent)
        {
            var copy = new Settings
            {
                Storeys = s.Storeys, Thickness = s.Thickness, TowerCells = 1, TowerExtra = 0, TowerRoofs = false,
                Battlements = s.Battlements, Theme = s.Theme, RoundTowers = false
            };
            GameObject joint = Tower(centre, yaw, copy, ground, parent);
            joint.name = "Wall Bend";
            var piece = joint.GetComponent<CastlePiece>();
            piece.tower = false;
            piece.joint = true;
            return joint;
        }

        // A parapet with merlons on it, of solid stone: 'width' metres along, its outside on the line through 'at'
        // (in the parent's space, at the top of the wall), facing 'outward'
        private static void Battlement(Transform parent, Vector3 at, Vector3 outward, float width)
        {
            Vector3 along = Vector3.Cross(Vector3.up, outward);
            Vector3 line = at - outward * (ParapetDepth * 0.5f);
            Quaternion turn = Quaternion.LookRotation(outward, Vector3.up);
            Block(parent, line + Vector3.up * (ParapetHeight * 0.5f), new Vector3(width, ParapetHeight, ParapetDepth), turn);
            int count = Mathf.Max(1, Mathf.RoundToInt(width / 2f));
            float step = width / count;
            for (int m = 0; m < count; m++)
            {
                Vector3 spot = line + along * (-width * 0.5f + step * (m + 0.5f)) + Vector3.up * (ParapetHeight + MerlonHeight * 0.5f);
                Block(parent, spot, new Vector3(Mathf.Min(1.1f, step * 0.55f), MerlonHeight, ParapetDepth), turn);
            }
        }

        internal static void Finish(GameObject built, Settings s)
        {
            foreach (Transform t in built.GetComponentsInChildren<Transform>(true))
            {
                GameObjectUtility.SetStaticEditorFlags(t.gameObject, StaticEditorFlags.BatchingStatic | StaticEditorFlags.OccluderStatic |
                                                                     StaticEditorFlags.OccludeeStatic | StaticEditorFlags.ContributeGI |
                                                                     StaticEditorFlags.ReflectionProbeStatic);
            }
            if (!string.IsNullOrEmpty(s.Theme)) BuildingThemes.Apply(built, s.Theme, false);
        }

        private static Vector3 Flat(Vector3 v) => new Vector3(v.x, 0f, v.z);
        private static Vector3 Y(Vector3 v, float y) => new Vector3(v.x, y, v.z);

        // ================================================================ Details

        private static int Seed(List<Vector3> path)
        {
            unchecked
            {
                int seed = 17;
                foreach (Vector3 p in path) seed = seed * 31 + Mathf.RoundToInt(p.x * 10f) * 7 + Mathf.RoundToInt(p.z * 10f);
                return seed;
            }
        }

        // The details along a wall: 'at', 'way' and 'top' give its middle, its direction and its walkway's height a
        // distance along it; it runs 'from' to 'to'; its faces are 'half' out, its walkway 'walk'; 'inner' is the inside
        private static void WallDetails(Transform wall, Vector3 origin, System.Func<float, Vector3> at, System.Func<float, Vector3> way, System.Func<float, float> top,
                                        float from, float to, float half, float walk, float height, List<Arch> gates, int inner, Settings s, GroundAt ground, List<Arch> windows, int seed)
        {
            Details k = s.Detail;
            if (k.Ivy) EnsureVines();
            var random = new System.Random(seed);
            float Chance() => (float)random.NextDouble();
            Vector3 Across(float d) => Vector3.Cross(way(d), Vector3.down).normalized;
            float Ground(Vector3 p) => ground(p) ?? 0f;
            Vector3 Local(Vector3 world) => world - origin;
            // (not on a gateway, or too near the ends)
            bool Clear(float d, float room)
            {
                if (d < from + room || d > to - room) return false;
                foreach (Arch a in gates) if (Mathf.Abs(d - a.Centre) < a.Radius + room) return false;
                foreach (Arch a in windows) if (Mathf.Abs(d - a.Centre) < a.Radius + Mathf.Min(room, 0.6f)) return false;
                return true;
            }
            Quaternion Facing(Vector3 outward) => Quaternion.LookRotation(outward, Vector3.up);

            // A broader course of stone along the foot of both faces
            if (k.Plinth)
            {
                for (float d = from; d < to - 0.2f; d += 2f)
                {
                    float len = Mathf.Min(2f, to - d), mid = d + len * 0.5f;
                    bool overGate = false;
                    foreach (Arch a in gates) if (d < a.End + 0.05f && d + len > a.Start - 0.05f) overGate = true;
                    if (overGate) continue;
                    Vector3 p = at(mid), across = Across(mid);
                    foreach (float face in new[] { 1f, -1f })
                    {
                        Vector3 spot = p + across * (face * (half + 0.15f));
                        float g = Ground(spot);
                        Block(wall, Local(Y(spot, g + 0.35f)), new Vector3(len + 0.02f, 1.3f, 0.32f), Facing(across * face));
                    }
                }
            }

            // A carved band along both faces under the battlements, with corbels beneath it
            if (k.Corbels)
            {
                for (float d = from; d < to - 0.2f; d += 2f)
                {
                    float len = Mathf.Min(2f, to - d), mid = d + len * 0.5f;
                    Vector3 p = at(mid), across = Across(mid);
                    float y = top(mid);
                    foreach (float face in new[] { 1f, -1f })
                    {
                        Block(wall, Local(Y(p + across * (face * (half + 0.1f)), y - 0.15f)), new Vector3(len + 0.02f, 0.3f, 0.2f), Facing(across * face));
                        for (int c = 0; c < 2; c++)
                        {
                            float cd = d + len * (c + 0.5f) / 2f;
                            Vector3 cp = at(cd), ca = Across(cd);
                            Block(wall, Local(Y(cp + ca * (face * (half + 0.12f)), top(cd) - 0.6f)), new Vector3(0.35f, 0.6f, 0.24f), Facing(ca * face));
                        }
                    }
                }
            }

            // Stone round each opening cut through the wall, on both faces: a window's sill, jambs and arched head (and
            // now and then open shutters outside), or a slit's sill and lintel
            foreach (Arch hole in windows)
            {
                Vector3 p = at(hole.Centre), across = Across(hole.Centre);
                foreach (float face in new[] { 1f, -1f })
                {
                    Vector3 spot = p + across * (face * half);
                    if (hole.Slit)
                    {
                        Quaternion facing = Facing(across * face);
                        Vector3 front = spot + across * (face * 0.06f);
                        Block(wall, Local(Y(front, hole.Sill - 0.08f)), new Vector3(0.5f, 0.16f, 0.14f), facing);
                        Block(wall, Local(Y(front, hole.Spring + hole.Radius + 0.08f)), new Vector3(0.5f, 0.16f, 0.14f), facing);
                        continue;
                    }
                    WindowSurround(wall, Local(spot), across * face, hole, k.Shutters && face == -inner && random.NextDouble() < 0.45);
                }
            }

            // Buttresses: stepped stone piers against the outside face
            if (k.Buttresses && k.ButtressEvery > 1f)
            {
                for (float d = from + k.ButtressEvery * 0.5f; d < to; d += k.ButtressEvery)
                {
                    if (!Clear(d, 2f)) continue;
                    Vector3 p = at(d), across = Across(d) * -inner;
                    float g = Ground(p + across * half), tall = top(d) - g;
                    float[] depth = { 1.3f, 0.95f, 0.6f };
                    for (int tier = 0; tier < 3; tier++)
                    {
                        float y0 = g - 0.6f + tall * tier / 3f, y1 = g + tall * (tier + 1) / 3f - (tier == 2 ? 0.8f : 0f);
                        Vector3 spot = p + across * (half + depth[tier] * 0.5f - 0.05f);
                        Block(wall, Local(Y(spot, (y0 + y1) * 0.5f)), new Vector3(1.3f, y1 - y0, depth[tier]), Facing(across));
                    }
                }
            }

            // Gateways: a portcullis half raised on the outside, doors swung open against the inside, torches either side
            foreach (Arch a in gates)
            {
                Vector3 p = at(a.Centre), across = Across(a.Centre);
                float archTop = a.Spring + a.Radius;
                if (k.GatePassage)
                {
                    Material dark = DetailMaterial("Castle Dark", new Color(0.05f, 0.045f, 0.04f), 0f, 0f);
                    Vector3 w = way(a.Centre).normalized;
                    // A ring of wedge stones round the arch on each face, standing a little proud, with quoins up its sides
                    foreach (float face in new[] { 1f, -1f })
                    {
                        Vector3 rim = p + across * (face * (half + 0.06f));
                        ArchRing(wall, Local(Y(rim, a.Spring)), w, across * face, a.Radius + 0.22f, 0.44f, 0.3f, Mathf.Max(7, Mathf.RoundToInt(Mathf.PI * a.Radius / 0.55f) | 1));
                        foreach (float side in new[] { -1f, 1f })
                        {
                            int course = 0;
                            for (float y = a.Floor; y < a.Spring - 0.1f; y += 0.5f, course++)
                            {
                                float wide = course % 2 == 0 ? 0.62f : 0.42f;
                                float high = Mathf.Min(0.46f, a.Spring - y - 0.02f);
                                Block(wall, Local(Y(rim + w * (side * (a.Radius + wide * 0.5f - 0.02f)), y + high * 0.5f)), new Vector3(wide, high, 0.3f), Facing(across * face));
                            }
                        }
                        // a kerb across the threshold
                        Block(wall, Local(Y(p + across * (face * (half - 0.25f)), a.Floor + 0.08f)), new Vector3(a.Radius * 2f, 0.16f, 0.5f), Facing(across));
                    }
                    // Ribs of stone across the passage's vault, between them murder holes in the roof
                    float inside = half - 0.5f;
                    int ribs = half > 2f ? 3 : 2;
                    for (int r = 0; r < ribs; r++)
                    {
                        float off = Mathf.Lerp(-inside, inside, ribs == 1 ? 0.5f : r / (float)(ribs - 1)) * 0.75f;
                        ArchRing(wall, Local(Y(p + across * off, a.Spring)), w, across, a.Radius - 0.1f, 0.3f, 0.45f, Mathf.Max(7, Mathf.RoundToInt(Mathf.PI * a.Radius / 0.5f) | 1));
                        if (r < ribs - 1)
                        {
                            float next = Mathf.Lerp(-inside, inside, (r + 1) / (float)(ribs - 1)) * 0.75f;
                            Block(wall, Local(Y(p + across * ((off + next) * 0.5f), archTop - 0.02f)), new Vector3(0.42f, 0.06f, 0.42f), Facing(across), dark);
                        }
                    }
                    // The portcullis's grooves down both sides of the passage
                    if (k.Portcullis)
                    {
                        foreach (float side in new[] { -1f, 1f })
                        {
                            Vector3 groove = p + w * (side * (a.Radius - 0.015f)) + across * (-inner * (half - 0.35f));
                            Block(wall, Local(Y(groove, (a.Floor + a.Spring) * 0.5f)), new Vector3(0.04f, a.Spring - a.Floor, 0.2f), Facing(across), dark);
                        }
                    }
                    // Torches on the passage's walls, halfway through
                    if (k.Torches)
                    {
                        foreach (float side in new[] { -1f, 1f })
                        {
                            Torch(wall, Local(p + w * (side * a.Radius)), -w * side, Mathf.Min(a.Floor + 2.4f, a.Spring - 0.3f), k.TorchLights);
                        }
                    }
                }
                if (k.Portcullis)
                {
                    Material iron = DetailMaterial("Castle Iron", new Color(0.12f, 0.12f, 0.13f), 0.7f, 0.35f);
                    Vector3 plane = p + across * (-inner * (half - 0.35f));
                    float raised = a.Floor + (archTop - a.Floor) * 0.45f;
                    for (float x = -a.Radius + 0.25f; x < a.Radius - 0.1f; x += 0.38f)
                    {
                        float h = a.Height(a.Centre + x) - 0.05f;
                        if (h <= raised + 0.1f) continue;
                        Vector3 bar = at(a.Centre + x) + across * (-inner * (half - 0.35f));
                        Block(wall, Local(Y(bar, (raised + h) * 0.5f)), new Vector3(0.08f, h - raised, 0.08f), Facing(across), iron);
                        Block(wall, Local(Y(bar, raised - 0.18f)), new Vector3(0.06f, 0.36f, 0.06f), Facing(across), iron); // (its spike)
                    }
                    for (float y = raised + 0.3f; y < archTop - 0.2f; y += 0.55f)
                    {
                        // (the cross bars, as wide as the arch is at that height)
                        float spread = y <= a.Spring ? a.Radius : Mathf.Sqrt(Mathf.Max(0f, a.Radius * a.Radius - (y - a.Spring) * (y - a.Spring)));
                        if (spread < 0.3f) continue;
                        Block(wall, Local(Y(plane, y)), new Vector3(spread * 2f - 0.1f, 0.08f, 0.08f), Facing(across), iron);
                    }
                }
                if (k.GateDoors)
                {
                    Material wood = DetailMaterial("Castle Wood", new Color(0.33f, 0.21f, 0.12f), 0f, 0.15f);
                    Material iron = DetailMaterial("Castle Iron", new Color(0.12f, 0.12f, 0.13f), 0.7f, 0.35f);
                    float doorHigh = a.Spring - a.Floor + a.Radius * 0.7f;
                    foreach (float side in new[] { -1f, 1f })
                    {
                        float d = a.Centre + side * (a.Radius * 1.5f + (k.GatePassage ? 0.72f : 0.05f)); // (clear of the quoins)
                        Vector3 dp = at(d), da = Across(d);
                        Vector3 spot = dp + da * (inner * (half + 0.1f));
                        Block(wall, Local(Y(spot, a.Floor + doorHigh * 0.5f)), new Vector3(a.Radius, doorHigh, 0.16f), Facing(da * inner), wood);
                        foreach (float band in new[] { 0.25f, 0.75f })
                        {
                            Block(wall, Local(Y(spot + da * (inner * 0.09f), a.Floor + doorHigh * band)), new Vector3(a.Radius + 0.02f, 0.14f, 0.04f), Facing(da * inner), iron);
                        }
                    }
                }
                if (k.Torches)
                {
                    foreach (float side in new[] { -1f, 1f })
                    {
                        foreach (float face in new[] { 1f, -1f })
                        {
                            float d = a.Centre + side * (a.Radius + 0.7f);
                            Torch(wall, Local(at(d) + Across(d) * (face * half)), Across(d) * face, a.Floor + 2.6f, k.TorchLights);
                        }
                    }
                }
                if (k.Clutter)
                {
                    // Crates (and now and then a cart) just inside
                    foreach (float side in new[] { -1f, 1f })
                    {
                        if (Chance() < 0.3f) continue;
                        float d = a.Centre + side * (a.Radius + 2.2f + Chance() * 1.5f);
                        Vector3 spot = at(d) + Across(d) * (inner * (half + 1.6f + Chance()));
                        float g = Ground(spot);
                        int stack = 1 + random.Next(3);
                        for (int c = 0; c < stack; c++)
                        {
                            Vector3 offset = c == 2 ? Vector3.zero : new Vector3((float)random.NextDouble() - 0.5f, 0f, (float)random.NextDouble() - 0.5f) * 1.4f;
                            GameObject crate = MedievalHouseBuilder.Piece("Prop_Crate", wall, Local(Y(spot + offset, g + (c == 2 ? 1.06f : 0f))), Quaternion.Euler(0f, random.Next(360), 0f) * Vector3.forward,
                                                                          MedievalHouseBuilder.Turn.None, Vector3.one);
                            if (crate == null) break;
                        }
                    }
                    if (Chance() < 0.4f)
                    {
                        float d = a.Centre + (Chance() < 0.5f ? -1f : 1f) * (a.Radius + 4.5f);
                        Vector3 spot = at(d) + Across(d) * (inner * (half + 3.5f));
                        MedievalHouseBuilder.Piece("Prop_Wagon", wall, Local(Y(spot, Ground(spot))), way(d) * (Chance() < 0.5f ? 1f : -1f), MedievalHouseBuilder.Turn.None, Vector3.one);
                    }
                }
            }

            // Torches along the inside face
            if (k.Torches && k.TorchEvery > 1f)
            {
                for (float d = from + k.TorchEvery * 0.5f; d < to; d += k.TorchEvery)
                {
                    if (!Clear(d, 2f)) continue;
                    Vector3 across = Across(d) * inner;
                    Vector3 face = at(d) + across * half;
                    Torch(wall, Local(face), across, Ground(face) + 2.8f, k.TorchLights);
                }
            }

            // Stairs up to the walkway on the inside, near the start (if there's room for them)
            if (k.Stairs)
            {
                float start = from + 2.5f;
                float g0 = Ground(at(start) + Across(start) * (inner * (half + 0.7f)));
                int steps = Mathf.CeilToInt((top(start) - g0) / 0.45f);
                float run = steps * 0.5f;
                bool room = start + run < to - 2f;
                foreach (Arch a in gates) if (a.Start < start + run + 1f && a.End > start - 1f) room = false;
                if (room && steps > 2)
                {
                    // One solid flight against the wall: each step's top and front, and the flight's open side and end,
                    // the stone its proper size on every face
                    float rise = (top(start + run) - g0) / steps;
                    var flight = new Shape(at(start));
                    float wallFace = half, outer = half + 1.4f;
                    float Low(float d, float off) => Ground(at(d) + Across(d) * (inner * off)) - 0.4f;
                    for (int i = 0; i < steps; i++)
                    {
                        float d0 = start + i * 0.5f, d1 = d0 + 0.5f;
                        float stepTop = g0 + rise * (i + 1), below = g0 + rise * i;
                        Vector3 across0 = Across(d0) * inner, across1 = Across(d1) * inner;
                        Vector3 in0 = at(d0) + across0 * wallFace, in1 = at(d1) + across1 * wallFace;
                        Vector3 out0 = at(d0) + across0 * outer, out1 = at(d1) + across1 * outer;
                        Vector3 forward = way(d0);
                        if (inner < 0) forward = -forward;
                        // the tread
                        flight.Quad(Y(in0, stepTop), Y(in1, stepTop), Y(out1, stepTop), Y(out0, stepTop), Vector3.up, Vector3.up, Vector3.up, Vector3.up,
                                    new Vector2(d0 / Tile, 0f), new Vector2(d1 / Tile, 0f), new Vector2(d1 / Tile, 1.4f / Tile), new Vector2(d0 / Tile, 1.4f / Tile), true);
                        // the riser (facing back down the flight)
                        Vector3 back = -way(d0);
                        flight.Quad(Y(in0, below), Y(out0, below), Y(out0, stepTop), Y(in0, stepTop), back, back, back, back,
                                    new Vector2(0f, below / Tile), new Vector2(1.4f / Tile, below / Tile), new Vector2(1.4f / Tile, stepTop / Tile), new Vector2(0f, stepTop / Tile), false);
                        // the open side, down to the ground
                        flight.Quad(Y(out0, Low(d0, outer)), Y(out1, Low(d1, outer)), Y(out1, stepTop), Y(out0, stepTop), across0, across1, across1, across0,
                                    new Vector2(d0 / Tile, Low(d0, outer) / Tile), new Vector2(d1 / Tile, Low(d1, outer) / Tile), new Vector2(d1 / Tile, stepTop / Tile), new Vector2(d0 / Tile, stepTop / Tile), false);
                    }
                    // the high end (facing on along the wall), from the walkway down to the ground
                    float dEnd = start + run;
                    Vector3 acrossEnd = Across(dEnd) * inner, onward = way(dEnd);
                    Vector3 inEnd = at(dEnd) + acrossEnd * wallFace, outEnd = at(dEnd) + acrossEnd * outer;
                    float topEnd = g0 + rise * steps;
                    flight.Quad(Y(inEnd, Low(dEnd, wallFace)), Y(outEnd, Low(dEnd, outer)), Y(outEnd, topEnd), Y(inEnd, topEnd), onward, onward, onward, onward,
                                new Vector2(0f, Low(dEnd, wallFace) / Tile), new Vector2(1.4f / Tile, Low(dEnd, outer) / Tile), new Vector2(1.4f / Tile, topEnd / Tile), new Vector2(0f, topEnd / Tile), false);
                    GameObject stairs = flight.Build("Stairs", wall);
                    stairs.transform.SetParent(wall, true);
                }
            }

            // Ivy hanging down the faces here and there
            if (k.Ivy && k.IvyAmount > 0f)
            {
                string[] vines = { "Prop_Vine1", "Prop_Vine2", "Prop_Vine4" };
                for (float d = from + 1.5f; d < to - 1f; d += 3f)
                {
                    foreach (float face in new[] { 1f, -1f })
                    {
                        if (Chance() > k.IvyAmount) continue;
                        float dd = d + (Chance() - 0.5f) * 1.5f;
                        if (!Clear(dd, 1.2f)) continue;
                        Vector3 across = Across(dd) * face;
                        float scale = 1.6f + Chance() * 1.6f;
                        MedievalHouseBuilder.Piece(vines[random.Next(vines.Length)], wall, Local(Y(at(dd) + across * (half + 0.03f), top(dd) - 0.1f)), across,
                                                   MedievalHouseBuilder.Turn.Wall, new Vector3(scale * 0.8f, scale, 1f));
                    }
                }
            }

            // Fallen stones along the foot of the walls
            if (k.Rubble && k.RubbleAmount > 0f)
            {
                string[] stones = { "Prop_Brick1", "Prop_Brick2", "Prop_Brick3", "Prop_Brick4" };
                for (float d = from + 1f; d < to - 0.5f; d += 2f)
                {
                    foreach (float face in new[] { 1f, -1f })
                    {
                        if (Chance() > k.RubbleAmount) continue;
                        int count = 1 + random.Next(3);
                        for (int c = 0; c < count; c++)
                        {
                            float dd = d + (Chance() - 0.5f) * 2f;
                            if (!Clear(dd, 0.5f)) continue;
                            Vector3 spot = at(dd) + Across(dd) * (face * (half + 0.4f + Chance() * 1.4f));
                            float scale = 2f + Chance() * 2.5f;
                            GameObject stone = MedievalHouseBuilder.Piece(stones[random.Next(stones.Length)], wall, Local(Y(spot, Ground(spot) + 0.1f * scale)),
                                                                          Quaternion.Euler(0f, random.Next(360), 0f) * Vector3.forward, MedievalHouseBuilder.Turn.None, Vector3.one * scale);
                            if (stone != null) stone.transform.localRotation *= Quaternion.Euler(random.Next(-25, 25), 0f, random.Next(-25, 25));
                        }
                    }
                }
            }
        }

        // The details on a tower: banners, a flag, arrow slits, ivy and fallen stones
        private static void TowerDetails(Transform tower, Vector3 origin, float half, float top, float ground0, float yaw, Settings s, GroundAt ground, int seed,
                                         List<(Vector3 face, Vector3 outward, Arch hole)> openings, float flagAt = -1f)
        {
            Details k = s.Detail;
            var random = new System.Random(seed);
            float Chance() => (float)random.NextDouble();
            Quaternion turn = Quaternion.Euler(0f, yaw, 0f);
            // The ways it faces: four sides (square), or eight round
            var faces = new List<Vector3>();
            if (IsRound(s.Style)) for (int i = 0; i < 8; i++) faces.Add(turn * (Quaternion.Euler(0f, i * 45f + 22.5f, 0f) * Vector3.forward));
            else foreach (Vector3 v in new[] { Vector3.right, Vector3.left, Vector3.forward, Vector3.back }) faces.Add(turn * v);
            float FaceOut(Vector3 outward) => s.RoundTowers ? half : half;

            // Stone round each opening cut through the tower's wall (on the outside): a window's sill, jambs and arched
            // head (and now and then open shutters), or a slit's sill and lintel
            foreach (var (face, outward, hole) in openings)
            {
                Vector3 at = face - origin;
                if (hole.Slit)
                {
                    Quaternion facing = Quaternion.LookRotation(outward, Vector3.up);
                    Vector3 front = at + outward * 0.06f;
                    Block(tower, Y(front, hole.Sill - 0.08f), new Vector3(0.5f, 0.16f, 0.14f), facing);
                    Block(tower, Y(front, hole.Spring + hole.Radius + 0.08f), new Vector3(0.5f, 0.16f, 0.14f), facing);
                    continue;
                }
                WindowSurround(tower, at, outward, hole, k.Shutters && random.NextDouble() < 0.35);
            }
            if (k.Banners)
            {
                // Two banners hung from the top, across from each other (side to side across the walls)
                Material cloth = DetailMaterial("Banner " + ColorUtility.ToHtmlStringRGB(k.BannerColour), k.BannerColour, 0f, 0.1f);
                Material trim = DetailMaterial("Banner " + ColorUtility.ToHtmlStringRGB(k.BannerTrim), k.BannerTrim, 0.3f, 0.3f);
                Material wood = DetailMaterial("Castle Wood", new Color(0.33f, 0.21f, 0.12f), 0f, 0.15f);
                foreach (Vector3 outward in new[] { turn * Vector3.right, turn * Vector3.left })
                {
                    Vector3 face = outward * (half + 0.06f);
                    float hang = top - 0.6f, length = Mathf.Min(4f, (top - ground0) * 0.4f);
                    Block(tower, face + Vector3.up * (hang - length * 0.5f), new Vector3(1.5f, length, 0.04f), Quaternion.LookRotation(outward), cloth);
                    Block(tower, face + outward * 0.01f + Vector3.up * (hang - length + 0.25f), new Vector3(1.52f, 0.22f, 0.04f), Quaternion.LookRotation(outward), trim);
                    Block(tower, face + outward * 0.01f + Vector3.up * (hang - length * 0.45f), new Vector3(0.5f, 0.5f, 0.04f), Quaternion.LookRotation(outward) * Quaternion.Euler(0f, 0f, 45f), trim);
                    Block(tower, face + outward * 0.04f + Vector3.up * (hang + 0.05f), new Vector3(1.9f, 0.1f, 0.1f), Quaternion.LookRotation(outward), wood);
                }
            }
            if (k.Flags)
            {
                Material cloth = DetailMaterial("Banner " + ColorUtility.ToHtmlStringRGB(k.BannerColour), k.BannerColour, 0f, 0.1f);
                Material wood = DetailMaterial("Castle Wood", new Color(0.33f, 0.21f, 0.12f), 0f, 0.15f);
                float baseY = top + (s.Battlements || !s.TowerRoofs ? 0f : 0f);
                if (s.TowerRoofs)
                {
                    Vector3 size = MedievalHouseBuilder.PieceSize(TowerRoof);
                    float fit = size.x > 0.01f ? (half * 2f + 0.8f) / Mathf.Max(size.x, size.z) : 1f;
                    baseY = top + size.y * fit * 0.92f;
                }
                if (flagAt >= 0f) baseY = flagAt;
                const float pole = 3f;
                Block(tower, Vector3.up * (baseY + pole * 0.5f), new Vector3(0.1f, pole, 0.1f), Quaternion.identity, wood);
                Vector3 fly = turn * Vector3.right;
                Block(tower, Vector3.up * (baseY + pole - 0.5f) + fly * 0.8f, new Vector3(1.5f, 0.9f, 0.04f), Quaternion.LookRotation(Vector3.Cross(fly, Vector3.up)), cloth);
            }
            if (k.Ivy) EnsureVines();
            if (k.Ivy && Chance() < k.IvyAmount * 2f)
            {
                Vector3 outward = faces[random.Next(faces.Count)];
                MedievalHouseBuilder.Piece(Chance() < 0.5f ? "Prop_Vine1" : "Prop_Vine2", tower, outward * (half + 0.03f) + Vector3.up * (top - 0.5f), outward,
                                           MedievalHouseBuilder.Turn.Wall, new Vector3(1.6f, 2.6f, 1f));
            }
            if (k.Rubble && k.RubbleAmount > 0f)
            {
                string[] stones = { "Prop_Brick1", "Prop_Brick2", "Prop_Brick3", "Prop_Brick4" };
                int count = Mathf.RoundToInt(k.RubbleAmount * 8f);
                for (int c = 0; c < count; c++)
                {
                    Vector3 outward = Quaternion.Euler(0f, random.Next(360), 0f) * Vector3.forward;
                    Vector3 local = outward * (half + 0.4f + Chance() * 1.2f);
                    float g = ground(origin + local) ?? ground0;
                    float scale = 2f + Chance() * 2.5f;
                    MedievalHouseBuilder.Piece(stones[random.Next(stones.Length)], tower, Y(local, g + 0.1f * scale), outward, MedievalHouseBuilder.Turn.None, Vector3.one * scale);
                }
            }
        }

        // The kit's vines need their own material (set up with the rest of the kit, if it was set up before they had one)
        private static void EnsureVines()
        {
            if (AssetDatabase.LoadAssetAtPath<Material>(MedievalVillageSetup.KitFolder + "/Materials/MI_Vine.mat") == null) MedievalVillageSetup.SetUp();
        }

        // Which a row of openings is (row 0 the highest): windows, slits, or windows on top and slits below
        private static bool OpeningIsWindow(Details k, int row) => k.Openings == 1 || (k.Openings == 2 && row == 0);

        // An opening in a face ('face' on its surface, in the parent's space; 'centre' how high its middle is):
        // a glazed window in a stone surround (sill, jambs and an arched head), sometimes shuttered, or an arrow slit
        private static void Opening(Transform parent, Vector3 face, Vector3 outward, float centre, bool window, bool shutters, System.Random random)
        {
            outward.y = 0f;
            outward.Normalize();
            Vector3 along = Vector3.Cross(Vector3.up, outward);
            Quaternion facing = Quaternion.LookRotation(outward, Vector3.up);
            var at = new Vector3(face.x, centre, face.z);
            if (!window)
            {
                Material dark = DetailMaterial("Castle Dark", new Color(0.05f, 0.045f, 0.04f), 0f, 0f);
                Block(parent, at + outward * 0.02f, new Vector3(0.18f, 1.4f, 0.06f), facing, dark);
                Block(parent, at + outward * 0.06f + Vector3.down * 0.78f, new Vector3(0.5f, 0.16f, 0.14f), facing);
                Block(parent, at + outward * 0.06f + Vector3.up * 0.78f, new Vector3(0.5f, 0.16f, 0.14f), facing);
                return;
            }

            bool wide = random.NextDouble() < 0.35;
            float halfWidth = wide ? 0.68f : 0.44f;
            float top = wide ? 2.74f : 2.62f;
            // The kit's window stands 1.02 m to 'top' above where it's put, and reaches 0.25 m back: put so its back is
            // on the wall's face and its middle at 'centre'
            float bottom = centre - (1.02f + top) * 0.5f;
            Vector3 spot = new Vector3(face.x, bottom, face.z) + outward * 0.24f;
            Material glass = DetailMaterial("Castle Window Dark", new Color(0.06f, 0.07f, 0.09f), 0.2f, 0.85f);
            float glassHigh = top - 1.02f;
            // (a dark recess behind the glass, so it reads as an opening into the wall)
            Block(parent, at + outward * 0.03f, new Vector3(halfWidth * 2f - 0.1f, glassHigh - 0.1f, 0.06f), facing, glass);
            MedievalHouseBuilder.Piece(wide ? "Window_Wide_Round1" : "Window_Thin_Round1", parent, spot, outward, MedievalHouseBuilder.Turn.Wall, Vector3.one);
            if (shutters && random.NextDouble() < 0.4)
            {
                string kind = wide ? "WindowShutters_Wide_Round" : "WindowShutters_Thin_Round";
                MedievalHouseBuilder.Piece(kind + (random.NextDouble() < 0.7 ? "_Open" : "_Closed"), parent, spot, outward, MedievalHouseBuilder.Turn.Wall, Vector3.one);
            }

            // The stone surround: a sill, a jamb each side, and a round head of wedge stones with a keystone
            float sillY = bottom + 1.02f - 0.1f, springY = bottom + top - halfWidth;
            Vector3 front = new Vector3(face.x, 0f, face.z) + outward * 0.1f;
            Block(parent, Y(front + outward * 0.06f, sillY), new Vector3(halfWidth * 2f + 0.55f, 0.2f, 0.38f), facing);
            foreach (float side in new[] { -1f, 1f })
            {
                Block(parent, Y(front + along * (side * (halfWidth + 0.14f)), (sillY + springY) * 0.5f + 0.05f), new Vector3(0.28f, springY - sillY - 0.1f, 0.24f), facing);
            }
            ArchRing(parent, Y(front, springY), along, outward, halfWidth + 0.14f, 0.28f, 0.24f, 7);
        }

        // The stone round a window cut through the wall, on one face ('face' on its surface, in the parent's space): a sill
        // standing proud, a jamb each side, and an arched head of wedge stones; open wooden shutters if asked
        private static void WindowSurround(Transform parent, Vector3 face, Vector3 outward, Arch hole, bool shutters)
        {
            outward.y = 0f;
            outward.Normalize();
            Vector3 along = Vector3.Cross(Vector3.up, outward);
            Quaternion facing = Quaternion.LookRotation(outward, Vector3.up);
            Vector3 front = new Vector3(face.x, 0f, face.z) + outward * 0.08f;
            float r = hole.Radius;
            Block(parent, Y(front + outward * 0.06f, hole.Sill - 0.1f), new Vector3(r * 2f + 0.55f, 0.2f, 0.38f), facing);
            foreach (float side in new[] { -1f, 1f })
            {
                Block(parent, Y(front + along * (side * (r + 0.14f)), (hole.Sill + hole.Spring) * 0.5f), new Vector3(0.28f, hole.Spring - hole.Sill, 0.22f), facing);
            }
            ArchRing(parent, Y(front, hole.Spring), along, outward, r + 0.14f, 0.28f, 0.22f, 7);
            if (shutters)
            {
                // (the kit's shutters stand 1.09 m up from where they're put, flush on a wall's face 0.09 m out)
                string kind = r > 0.6f ? "WindowShutters_Wide_Round_Open" : "WindowShutters_Thin_Round_Open";
                Vector3 spot = new Vector3(face.x, hole.Sill - 1.05f, face.z) + outward * 0.02f;
                MedievalHouseBuilder.Piece(kind, parent, spot, outward, MedievalHouseBuilder.Turn.Wall, Vector3.one);
            }
        }

        // A half-round ring of wedge stones (an arch's head): its middle 'centre' at its springing (in the parent's
        // space), spanning along 'along', 'radius' to the middle of its stones, each 'thick' deep (radially) and 'depth'
        // through, 'count' of them, the top one a bigger keystone
        private static void ArchRing(Transform parent, Vector3 centre, Vector3 along, Vector3 through, float radius, float thick, float depth, int count)
        {
            float gap = Mathf.PI * radius / count;
            for (int i = 0; i < count; i++)
            {
                float angle = Mathf.PI * (i + 0.5f) / count;
                Vector3 radial = along * Mathf.Cos(angle) + Vector3.up * Mathf.Sin(angle);
                bool key = i == count / 2 && count % 2 == 1;
                Block(parent, centre + radial * (radius + (key ? 0.04f : 0f)), new Vector3(gap * 0.92f, thick * (key ? 1.35f : 1f), depth * (key ? 1.15f : 1f)),
                      Quaternion.LookRotation(through, radial));
            }
        }

        // A torch on a wall: an iron bracket sticking out, a burning head (and a light, if asked)
        private static void Torch(Transform parent, Vector3 face, Vector3 outward, float height, bool light)
        {
            Material iron = DetailMaterial("Castle Iron", new Color(0.12f, 0.12f, 0.13f), 0.7f, 0.35f);
            Material wood = DetailMaterial("Castle Wood", new Color(0.33f, 0.21f, 0.12f), 0f, 0.15f);
            Material flame = DetailMaterial("Castle Flame", new Color(1f, 0.55f, 0.15f), 0f, 0f, new Color(1f, 0.45f, 0.1f) * 4f);
            Vector3 spot = new Vector3(face.x, height, face.z) + outward * 0.18f;
            Quaternion lean = Quaternion.LookRotation(outward) * Quaternion.Euler(-25f, 0f, 0f);
            Block(parent, new Vector3(face.x, height - 0.1f, face.z) + outward * 0.05f, new Vector3(0.1f, 0.3f, 0.1f), Quaternion.LookRotation(outward), iron);
            Block(parent, spot, new Vector3(0.08f, 0.55f, 0.08f), lean, wood);
            Vector3 head = spot + lean * Vector3.up * 0.32f;
            Block(parent, head, new Vector3(0.16f, 0.22f, 0.16f), lean, flame);
            if (!light) return;
            var go = new GameObject("Torch Light");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = head + outward * 0.15f + Vector3.up * 0.15f;
            var l = go.AddComponent<Light>();
            l.type = LightType.Point;
            l.color = new Color(1f, 0.6f, 0.3f);
            l.range = 7f;
            l.intensity = 1.6f;
            l.shadows = LightShadows.None;
        }

        // A plain material for the details (made the first time): a colour, how metal and how smooth, maybe glowing
        internal static Material DetailMaterial(string name, Color colour, float metallic, float smoothness, Color? glow = null)
        {
            string folder = MedievalVillageSetup.KitFolder + "/Generated";
            string path = $"{folder}/{name}.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material != null) return material;
            if (!AssetDatabase.IsValidFolder(folder)) AssetDatabase.CreateFolder(MedievalVillageSetup.KitFolder, "Generated");
            material = new Material(MedievalVillageSetup.WetsInRain(name) ? MedievalVillageSetup.BuildingShader : Shader.Find("Universal Render Pipeline/Lit")) { name = name, enableInstancing = true };
            material.SetColor("_BaseColor", colour);
            material.SetFloat("_Metallic", metallic);
            material.SetFloat("_Smoothness", smoothness);
            if (glow.HasValue)
            {
                material.EnableKeyword("_EMISSION");
                material.SetColor("_EmissionColor", glow.Value);
                material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            }
            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        // ================================================================ Making the stone

        // A mesh being made, in the world (around 'origin', which becomes its object's middle): the stone, and the paving
        private sealed class Shape
        {
            private readonly Vector3 origin;
            private readonly List<Vector3> vertices = new List<Vector3>();
            private readonly List<Vector3> normals = new List<Vector3>();
            private readonly List<Vector2> uvs = new List<Vector2>();
            private readonly List<int> stone = new List<int>(), paving = new List<int>();

            public Shape(Vector3 origin) => this.origin = new Vector3(origin.x, 0f, origin.z);

            // A four-cornered face (corners in order round it), facing the way its normals do
            public void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 na, Vector3 nb, Vector3 nc, Vector3 nd,
                             Vector2 ua, Vector2 ub, Vector2 uc, Vector2 ud, bool pave)
            {
                int i = vertices.Count;
                vertices.Add(a - origin); vertices.Add(b - origin); vertices.Add(c - origin); vertices.Add(d - origin);
                normals.Add(na.normalized); normals.Add(nb.normalized); normals.Add(nc.normalized); normals.Add(nd.normalized);
                uvs.Add(ua); uvs.Add(ub); uvs.Add(uc); uvs.Add(ud);
                List<int> into = pave ? paving : stone;
                // (wound so it faces the way its normals do: Unity draws a triangle's clockwise side)
                if (Vector3.Dot(Vector3.Cross(b - a, c - a), na + nb + nc + nd) >= 0f) into.AddRange(new[] { i, i + 1, i + 2, i, i + 2, i + 3 });
                else into.AddRange(new[] { i, i + 2, i + 1, i, i + 3, i + 2 });
            }

            public void Triangle(Vector3 a, Vector3 b, Vector3 c, Vector3 normal, Vector2 ua, Vector2 ub, Vector2 uc, bool pave)
            {
                int i = vertices.Count;
                vertices.Add(a - origin); vertices.Add(b - origin); vertices.Add(c - origin);
                normals.Add(normal); normals.Add(normal); normals.Add(normal);
                uvs.Add(ua); uvs.Add(ub); uvs.Add(uc);
                List<int> into = pave ? paving : stone;
                if (Vector3.Dot(Vector3.Cross(b - a, c - a), normal) >= 0f) into.AddRange(new[] { i, i + 1, i + 2 });
                else into.AddRange(new[] { i, i + 2, i + 1 });
            }

            // The object: the mesh (stone and paving), drawn in the kit's materials, solid to walk on
            public GameObject Build(string name, Transform parent)
            {
                var mesh = new Mesh { name = name };
                if (vertices.Count > 65000) mesh.indexFormat = IndexFormat.UInt32;
                mesh.SetVertices(vertices);
                mesh.SetNormals(normals);
                mesh.SetUVs(0, uvs);
                mesh.subMeshCount = 2;
                mesh.SetTriangles(stone, 0);
                mesh.SetTriangles(paving, 1);
                mesh.RecalculateTangents();
                MeshUtility.Optimize(mesh);   // (vertices reordered for the GPU)
                mesh.RecalculateBounds();

                var go = new GameObject(name);
                go.transform.SetParent(parent, false);
                go.transform.position = origin;
                go.AddComponent<MeshFilter>().sharedMesh = mesh;
                go.AddComponent<MeshRenderer>().sharedMaterials = new[]
                {
                    AssetDatabase.LoadAssetAtPath<Material>(StonePath), AssetDatabase.LoadAssetAtPath<Material>(PavingPath)
                };
                go.AddComponent<MeshCollider>().sharedMesh = mesh;
                return go;
            }
        }

        // ---------------------------------------------------------------- Stone blocks

        private static string BlockPath => MedievalVillageSetup.KitFolder + "/Generated/Stone Block.prefab";
        private static string BlockMeshPath => MedievalVillageSetup.KitFolder + "/Generated/Stone Block.asset";
        private const float BlockPiece = 1.5f; // (long blocks are made of shorter ones, so the stone isn't stretched)

        /// <summary>
        /// A solid block of the kit's stone, 'size' metres (x, z turned by 'turn'), at 'centre' in the parent's space. Long
        /// ones are made of shorter blocks, so the stone's texture stays about the size it is on the walls.
        /// </summary>
        private static void Block(Transform parent, Vector3 centre, Vector3 size, Quaternion? turn = null, Material material = null)
        {
            GameObject prefab = BlockPrefab();
            if (prefab == null) return;
            Quaternion r = turn ?? Quaternion.identity;
            int nx = Mathf.Max(1, Mathf.CeilToInt(size.x / BlockPiece - 0.05f));
            int ny = Mathf.Max(1, Mathf.CeilToInt(size.y / BlockPiece - 0.05f));
            int nz = Mathf.Max(1, Mathf.CeilToInt(size.z / BlockPiece - 0.05f));
            var each = new Vector3(size.x / nx, size.y / ny, size.z / nz);
            for (int x = 0; x < nx; x++)
            {
                for (int y = 0; y < ny; y++)
                {
                    for (int z = 0; z < nz; z++)
                    {
                        var offset = new Vector3((x + 0.5f) * each.x - size.x * 0.5f, (y + 0.5f) * each.y - size.y * 0.5f, (z + 0.5f) * each.z - size.z * 0.5f);
                        var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
                        go.transform.localPosition = centre + r * offset;
                        go.transform.localRotation = r;
                        go.transform.localScale = each + Vector3.one * 0.002f; // (a hair over, so the joins don't flicker)
                        if (material != null) go.GetComponent<MeshRenderer>().sharedMaterial = material;
                    }
                }
            }
        }

        // The stone block (made the first time): a 1 m cube in the kit's uneven stone, a little under half its texture
        // across each face (so it matches the walls)
        private static GameObject BlockPrefab()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(BlockPath);
            if (prefab != null)
            {
                // (made with the stone a different size: its texture put to the size it is on the walls now, in place, so
                // the blocks already built match too)
                Mesh made = prefab.GetComponent<MeshFilter>() != null ? prefab.GetComponent<MeshFilter>().sharedMesh : null;
                if (made != null)
                {
                    var current = new List<Vector2>();
                    made.GetUVs(0, current);
                    float most = 0f;
                    foreach (Vector2 uv in current) most = Mathf.Max(most, uv.x);
                    if (Mathf.Abs(most - 1f / Tile) > 0.001f && most > 0f)
                    {
                        for (int i = 0; i < current.Count; i++) current[i] *= (1f / Tile) / most;
                        made.SetUVs(0, current);
                        EditorUtility.SetDirty(made);
                        AssetDatabase.SaveAssetIfDirty(made);
                    }
                }
                return prefab;
            }
            var stone = AssetDatabase.LoadAssetAtPath<Material>(StonePath);
            if (stone == null) return null;
            if (!AssetDatabase.IsValidFolder(MedievalVillageSetup.KitFolder + "/Generated")) AssetDatabase.CreateFolder(MedievalVillageSetup.KitFolder, "Generated");

            var mesh = new Mesh { name = "Stone Block" };
            var vertices = new List<Vector3>();
            var normals = new List<Vector3>();
            var uvs = new List<Vector2>();
            var triangles = new List<int>();
            foreach (Vector3 n in new[] { Vector3.right, Vector3.left, Vector3.up, Vector3.down, Vector3.forward, Vector3.back })
            {
                Vector3 u = Mathf.Abs(n.y) > 0.5f ? Vector3.right : Vector3.Cross(Vector3.up, n);
                Vector3 v = Vector3.Cross(n, u);
                int start = vertices.Count;
                foreach (Vector2 c in new[] { new Vector2(-1, -1), new Vector2(1, -1), new Vector2(1, 1), new Vector2(-1, 1) })
                {
                    vertices.Add(n * 0.5f + u * (c.x * 0.5f) + v * (c.y * 0.5f));
                    normals.Add(n);
                    uvs.Add(new Vector2((c.x + 1f) * 0.5f / Tile, (c.y + 1f) * 0.5f / Tile));
                }
                triangles.AddRange(new[] { start, start + 1, start + 2, start, start + 2, start + 3 }); // (clockwise, seen from outside)
            }
            mesh.SetVertices(vertices);
            mesh.SetNormals(normals);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateTangents();
            mesh.RecalculateBounds();
            AssetDatabase.CreateAsset(mesh, BlockMeshPath);

            var go = new GameObject("Stone Block");
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>().sharedMaterial = stone;
            go.AddComponent<BoxCollider>();
            prefab = PrefabUtility.SaveAsPrefabAsset(go, BlockPath);
            Object.DestroyImmediate(go);
            return prefab;
        }
    }
}
#endif
