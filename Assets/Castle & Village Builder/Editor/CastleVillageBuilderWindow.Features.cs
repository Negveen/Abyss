#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace YaraJ.CastleVillageBuilder
{
    // The Castle Features tab: upper areas, grand stairs, bridges, dressed streets, plazas and keeps
    internal sealed partial class CastleVillageBuilderWindow
    {
        private CastleFeatures.Tool featureTool = CastleFeatures.Tool.UpperArea;
        private readonly CastleFeatures.Settings features = new CastleFeatures.Settings();
        private bool featureDragging;
        private Vector3 featureStart;
        private readonly List<Vector3> featureLine = new List<Vector3>();
        private readonly List<bool> featureLocked = new List<bool>();
        private bool aimLocked;
        private float featureYaw;

        // ================================================================ Window

        private void DrawFeatures()
        {
            EditorGUI.BeginChangeCheck();
            var tools = new[]
            {
                new GUIContent("Upper Area", "A raised paved terrace on stone retaining walls, with grand stairs up to it. Drag out a rectangle ([ and ] turn it). Terraces can go on terraces."),
                new GUIContent("Stairs", "Grand stairs: drag from the bottom up to where they should arrive (a terrace, a wall walk...)."),
                new GUIContent("Bridge", "Drag from one end to the other: a stone arch bridge on piers, or a wooden trestle bridge."),
                new GUIContent("Street", "Draw a line: lamp posts, banners, hedges, cypresses and benches along both sides of it."),
                new GUIContent("Plaza", "Click the middle and drag out: a round paved plaza on ring steps, with a fountain."),
                new GUIContent("Keep", "Click: a great keep, much bigger than the watch towers (walls can join onto it)."),
            };
            featureTool = (CastleFeatures.Tool)ToolPicker("Build", (int)featureTool, tools, 3);
            if (EditorGUI.EndChangeCheck()) { featureDragging = false; Changed(); }
            HowTo(FeatureHint());

            EditorGUI.BeginChangeCheck();
            var s = features;
            if (Section("FeatureOptions", "Options", true))
            {
                switch (featureTool)
                {
                    case CastleFeatures.Tool.UpperArea:
                        s.Freehand = GUILayout.Toolbar(s.Freehand ? 1 : 0, new[]
                        {
                            new GUIContent("Rectangle", "Drag out a rectangle ([ and ] turn it)."),
                            new GUIContent("Freehand", "Draw its outline, any shape: let go to close it.")
                        }) == 1;
                        if (s.Freehand)
                        {
                            s.LockToWalls = EditorGUILayout.Toggle(new GUIContent("Lock to castle walls", "Near a castle wall or tower, the outline locks onto its face, so the area fits snugly against it."), s.LockToWalls);
                            using (new EditorGUI.DisabledScope(!s.LockToWalls))
                                s.LockReach = Slider("Lock reach", "How near the mouse has to come to a wall's face (m) for the outline to lock onto it.", s.LockReach, 0.5f, 10f);
                        }
                        s.TerraceHeight = Slider("Height", "How high it stands above the highest ground under it (m).", s.TerraceHeight, 1f, 15f);
                        s.TerraceEdge = (CastleFeatures.Edge)EditorGUILayout.EnumPopup(new GUIContent("Edge", "What runs round its edge."), s.TerraceEdge);
                        s.TerraceStairs = (CastleFeatures.StairSides)EditorGUILayout.EnumPopup(new GUIContent("Stairs", "Which sides have grand stairs up (the front: the side facing you as you draw it)."), s.TerraceStairs);
                        DrawStairOptions(s);
                        s.TerraceLamps = EditorGUILayout.Toggle(new GUIContent("Lamps at the corners", "Iron lamp posts at its corners."), s.TerraceLamps);
                        s.TerracePlanters = EditorGUILayout.Toggle(new GUIContent("Planters", "Stone planters with clipped shrubs along its edges."), s.TerracePlanters);
                        break;
                    case CastleFeatures.Tool.Stairs:
                        s.Stairs = (CastleFeatures.StairShape)GUILayout.Toolbar((int)s.Stairs, new[]
                        {
                            new GUIContent("Straight", "Drag from the bottom to the top: a straight flight."),
                            new GUIContent("Curved", "Drag from the bottom to the top: one flight sweeping round as it climbs."),
                            new GUIContent("Double", "Drag from the bottom to the top: two mirrored flights sweeping out either way and meeting at the top (a horseshoe)."),
                            new GUIContent("Freehand", "Draw their path: they follow it as they climb, from its lower end to its higher one.")
                        });
                        if (s.Stairs == CastleFeatures.StairShape.Curved || s.Stairs == CastleFeatures.StairShape.DoubleCurved)
                        {
                            if (s.Stairs == CastleFeatures.StairShape.Curved) s.StairTurn = Slider("Turn", "How far round they sweep as they climb (degrees).", s.StairTurn, 20f, 220f);
                            else s.DoubleTurn = Slider("Turn", "How far round each flight sweeps as it climbs (degrees): wide sweeps make a horseshoe.", s.DoubleTurn, 20f, 200f);
                            if (s.Stairs == CastleFeatures.StairShape.DoubleCurved)
                                s.DoubleInward = GUILayout.Toolbar(s.DoubleInward ? 1 : 0, new[] { new GUIContent("Curve outward", "Going down, the flights sweep away from each other: a horseshoe (their tops side by side)."), new GUIContent("Curve inward", "Going down, the flights curve towards each other: their feet meet side by side, their tops arrive apart (up to 120 degrees).") }) == 1;
                            if (s.Stairs == CastleFeatures.StairShape.Curved)
                                s.CurveRight = GUILayout.Toolbar(s.CurveRight ? 1 : 0, new[] { new GUIContent("Sweep left", "Going down, they curve away to the left."), new GUIContent("Sweep right", "Going down, they curve away to the right.") }) == 1;
                        }
                        s.ConnectStairs = EditorGUILayout.Toggle(new GUIContent("Connect to upper areas", "Ending near an upper area's edge, the stairs snap onto it: square to it, clear of its corners, at its height, and its railing opens there."), s.ConnectStairs);
                        DrawStairOptions(s);
                        break;
                    case CastleFeatures.Tool.Bridge:
                        s.Bridge = (CastleFeatures.BridgeStyle)EditorGUILayout.EnumPopup(new GUIContent("Style", "Stone arches on piers, or a wooden trestle."), s.Bridge);
                        s.BridgeWidth = Slider("Width", "How wide its deck is (m).", s.BridgeWidth, 1.5f, 10f);
                        if (s.Bridge == CastleFeatures.BridgeStyle.StoneArch) s.ArchSpan = Slider("Arch span", "About how wide each arch is (m): long bridges get piers between them.", s.ArchSpan, 4f, 30f);
                        s.BridgeRail = (CastleFeatures.Rail)EditorGUILayout.EnumPopup(new GUIContent("Sides", "Parapet walls, balustrades, or open (a wooden bridge gets wooden rails)."), s.BridgeRail);
                        s.BridgeLamps = EditorGUILayout.Toggle(new GUIContent("Lanterns", "Lamp posts at both ends (and along long bridges)."), s.BridgeLamps);
                        break;
                    case CastleFeatures.Tool.Street:
                        s.StreetHalf = Slider("Street width", "How far either side of the line the props stand (m).", s.StreetHalf * 2f, 2f, 20f) * 0.5f;
                        s.Spacing = Slider("Spacing", "How far apart the lamp posts are (m).", s.Spacing, 3f, 30f);
                        s.BothSides = EditorGUILayout.Toggle(new GUIContent("Both sides", "Props on both sides of the line (or just its right)."), s.BothSides);
                        s.Lamps = EditorGUILayout.Toggle(new GUIContent("Lamp posts", "Iron lamp posts with lanterns."), s.Lamps);
                        s.Banners = EditorGUILayout.Toggle(new GUIContent("Banners", "Banner poles in the castle's colours."), s.Banners);
                        s.Hedges = EditorGUILayout.Toggle(new GUIContent("Hedges", "Clipped box hedges along the edge."), s.Hedges);
                        s.Trees = EditorGUILayout.Toggle(new GUIContent("Cypresses", "Tall slim trees."), s.Trees);
                        s.Benches = EditorGUILayout.Toggle(new GUIContent("Benches", "Wooden benches on stone legs, facing the street."), s.Benches);
                        break;
                    case CastleFeatures.Tool.Plaza:
                        s.PlazaFreehand = GUILayout.Toolbar(s.PlazaFreehand ? 1 : 0, new[]
                        {
                            new GUIContent("Round", "Click the middle and drag out."),
                            new GUIContent("Freehand", "Draw its outline, any shape: smoothed into flowing curves, its steps following it.")
                        }) == 1;
                        s.Fountain =EditorGUILayout.Toggle(new GUIContent("Fountain", "A fountain in the middle: a basin, a column and a bowl."), s.Fountain);
                        s.PlazaSteps = EditorGUILayout.IntSlider(new GUIContent("Steps round it", "How many ring steps it's raised on."), s.PlazaSteps, 0, 5);
                        s.PlazaLamps = EditorGUILayout.Toggle(new GUIContent("Lamps round it", "Lamp posts round its edge."), s.PlazaLamps);
                        break;
                    case CastleFeatures.Tool.Keep:
                        s.KeepCells = EditorGUILayout.IntSlider(new GUIContent("Width (2 m cells)", "How broad it is."), s.KeepCells, 3, 9);
                        s.KeepStoreys = EditorGUILayout.IntSlider(new GUIContent("Height (storeys)", "How tall it is, in 3 m storeys."), s.KeepStoreys, 3, 14);
                        s.KeepStyle = (CastleWallBuilder.TowerStyle)EditorGUILayout.EnumPopup(new GUIContent("Style", "Which tower style it's built in (its details are the Castle Walls tab's)."), s.KeepStyle);
                        break;
                }
                if (featureTool != CastleFeatures.Tool.Keep)
                    s.Lights = EditorGUILayout.Toggle(new GUIContent("Real lights in lanterns", "A point light in each lantern (lovely at night, but each one costs: use them on a few)."), s.Lights);
                if (featureTool == CastleFeatures.Tool.Street)
                {
                    s.BannerColour = EditorGUILayout.ColorField(new GUIContent("Banner colour"), s.BannerColour, true, false, false);
                    s.BannerTrim = EditorGUILayout.ColorField(new GUIContent("Banner trim"), s.BannerTrim, true, false, false);
                }
                Hint("The stone takes the castle's colour theme (Castle Walls > Look).");
            }
            if (EditorGUI.EndChangeCheck()) Changed();

            if (Section("FeatureList", "Built", true)) DrawFeatureList();
        }

        private void DrawStairOptions(CastleFeatures.Settings s)
        {
            s.StairWidth = Slider("Stair width", "How wide the stairs are (m).", s.StairWidth, 1.5f, 14f);
            s.StairRail = (CastleFeatures.Rail)EditorGUILayout.EnumPopup(new GUIContent("Stair sides", "Balustrades on low walls, solid side walls, or open."), s.StairRail);
            s.StairFoot = (CastleFeatures.Foot)EditorGUILayout.EnumPopup(new GUIContent("Start", "How the stairs start at the bottom: straight, a fan of round steps, or flared (each bottom step wider, its front bowed and its ends swept round)."), s.StairFoot);
            s.Landings = EditorGUILayout.Toggle(new GUIContent("Landings", "A flat landing every 12 steps on long flights."), s.Landings);
            s.StairLamps = EditorGUILayout.Toggle(new GUIContent("Lamps at the foot", "A lamp post either side of the bottom step."), s.StairLamps);
        }

        private string FeatureHint()
        {
            switch (featureTool)
            {
                case CastleFeatures.Tool.UpperArea:
                    return features.Freehand
                        ? "Draw its outline and let go to close it. Near a castle wall the outline locks onto it (gold). Shift+click: rub out a feature. Esc: stop."
                        : "Drag out a rectangle. [ and ]: turn it. Shift+click: rub out a feature. Esc: stop.";
                case CastleFeatures.Tool.Stairs:
                    return features.Stairs == CastleFeatures.StairShape.Freehand
                        ? "Draw the stairs' path from one end to the other (they climb from the lower end to the higher). End near an upper area's edge to connect to it."
                        : "Drag from the bottom to the top (an upper area's edge, a wall walk, a hill...). Near an upper area's edge they connect to it square.";
                case CastleFeatures.Tool.Bridge: return "Drag from one end to the other: its deck meets the ground (or walls, or terraces) at both.";
                case CastleFeatures.Tool.Street: return "Draw along a street: props go down both sides.";
                case CastleFeatures.Tool.Plaza:
                    return features.PlazaFreehand ? "Draw its outline and let go to close it (it's smoothed into flowing curves)." : "Click the middle and drag out to the size.";
                default: return "Click to put the keep down. [ and ]: turn it.";
            }
        }

        private void DrawFeatureList()
        {
            Transform container = FeaturesContainer(false);
            if (container == null || container.childCount == 0)
            {
                Hint("Nothing yet. Each feature you build goes under \"" + CastleFeatures.ContainerName + "\" in the Hierarchy.");
                return;
            }
            Transform doomed = null;
            foreach (Transform child in container)
            {
                EditorGUILayout.BeginHorizontal();
                GUILayout.Label(child.name, GUILayout.MinWidth(80));
                if (GUILayout.Button("Select", EditorStyles.miniButtonLeft, GUILayout.Width(50))) { Selection.activeGameObject = child.gameObject; EditorGUIUtility.PingObject(child.gameObject); }
                if (GUILayout.Button("✕", EditorStyles.miniButtonRight, GUILayout.Width(24))) doomed = child;
                EditorGUILayout.EndHorizontal();
            }
            if (doomed != null) Undo.DestroyObjectImmediate(doomed.gameObject);
        }

        private static Transform FeaturesContainer(bool make)
        {
            foreach (GameObject root in UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects())
            {
                if (root.name == CastleFeatures.ContainerName) return root.transform;
            }
            if (!make) return null;
            var go = new GameObject(CastleFeatures.ContainerName);
            Undo.RegisterCreatedObjectUndo(go, "Castle Feature");
            return go.transform;
        }

        // ================================================================ Scene view

        private void FeaturesSceneGUI(SceneView view, Event e, int id, bool erasing)
        {
            switch (e.type)
            {
                case EventType.Layout:
                    HandleUtility.AddDefaultControl(id);
                    break;

                case EventType.KeyDown:
                    if (e.keyCode == KeyCode.Escape && featureDragging) { featureDragging = false; view.Repaint(); e.Use(); }
                    else if (e.keyCode == KeyCode.Escape) { StopPainting(); Repaint(); e.Use(); }
                    else if (e.keyCode == KeyCode.LeftBracket || e.keyCode == KeyCode.RightBracket)
                    {
                        featureYaw = Mathf.Repeat(featureYaw + (e.keyCode == KeyCode.RightBracket ? 15f : -15f), 360f);
                        view.Repaint(); e.Use();
                    }
                    break;

                case EventType.MouseMove:
                    FeatureAim(e.mousePosition, view);
                    LockAim();
                    view.Repaint();
                    break;

                case EventType.MouseDown:
                    if (e.button != 0 || e.alt) break;
                    FeatureAim(e.mousePosition, view);
                    LockAim();
                    if (!aimed) break;
                    GUIUtility.hotControl = id;
                    if (erasing) { RubOutFeature(e.mousePosition); e.Use(); break; }
                    if (featureTool == CastleFeatures.Tool.Keep) { BuildFeature(aim, aim, view); e.Use(); break; }
                    featureDragging = true;
                    featureStart = aim;
                    featureLine.Clear();
                    featureLocked.Clear();
                    featureLine.Add(aim);
                    featureLocked.Add(aimLocked);
                    e.Use();
                    break;

                case EventType.MouseDrag:
                    if (GUIUtility.hotControl != id) break;
                    FeatureAim(e.mousePosition, view);
                    LockAim();
                    if (featureDragging && Outlining && aimed && Flat(aim - featureLine[featureLine.Count - 1]).magnitude >= 0.5f) { featureLine.Add(aim); featureLocked.Add(aimLocked); }
                    if (featureDragging && featureTool == CastleFeatures.Tool.Street && aimed && Flat(aim - featureLine[featureLine.Count - 1]).magnitude >= 1f) featureLine.Add(aim);
                    view.Repaint();
                    e.Use();
                    break;

                case EventType.MouseUp:
                    if (GUIUtility.hotControl != id) break;
                    GUIUtility.hotControl = 0;
                    if (featureDragging && aimed)
                    {
                        featureDragging = false;
                        BuildFeature(featureStart, aim, view);
                    }
                    featureDragging = false;
                    e.Use();
                    break;

                case EventType.Repaint:
                    DrawFeaturePreview(erasing);
                    break;
            }
        }

        // The mouse's spot: the ground, a walkable feature, or the top of a castle wall (for stairs and bridges)
        private void FeatureAim(Vector2 mouse, SceneView view)
        {
            Ray ray = HandleUtility.GUIPointToWorldRay(mouse);
            aimed = Ground.Raycast(ray, out aim, out aimNormal);
            RaycastHit[] hits = Physics.RaycastAll(ray, 5000f, ~0, QueryTriggerInteraction.Ignore);
            System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
            foreach (RaycastHit h in hits)
            {
                if (h.collider.GetComponentInParent<CastlePiece>() == null) continue;
                if (aimed && Vector3.Distance(ray.origin, aim) <= h.distance) break;
                aim = h.point;
                aimNormal = h.normal;
                aimed = true;
                break;
            }
        }

        private float? FeatureSurface(Vector3 world) => Ground.At(world, out float h) ? h : (float?)null;

        // Where dragged stairs go: from the lower end up to the higher, climbing towards it; or, connecting to an upper
        // area's edge near their top, square to that edge (their foot as far out from it as dragged)
        private void StairPlan(Vector3 a, Vector3 b, out Vector3 low, out Vector3 top, out Vector3 climb, out bool connected)
        {
            low = a.y <= b.y ? a : b;
            top = a.y <= b.y ? b : a;
            climb = Flat(top - low).normalized;
            connected = false;
            if (!features.ConnectStairs) return;
            float width = features.Stairs == CastleFeatures.StairShape.DoubleCurved ? CastleFeatures.DoubleWidth(features, top.y - low.y) : features.StairWidth;
            if (!CastleFeatures.SnapToEdge(top, width, 3f, out Vector3 onEdge, out Vector3 outward)) return;
            connected = true;
            float reach = Mathf.Max(4f, Vector3.Dot(Flat(low - onEdge), outward));
            Vector3 foot = Flat(onEdge) + outward * reach;
            low = Y(foot, FeatureSurface(foot) ?? low.y);
            top = onEdge;
            climb = -outward;
        }

        // Drawing an upper area's outline freehand
        private bool Outlining => (featureTool == CastleFeatures.Tool.UpperArea && features.Freehand) || (featureTool == CastleFeatures.Tool.Stairs && features.Stairs == CastleFeatures.StairShape.Freehand)
                                  || (featureTool == CastleFeatures.Tool.Plaza && features.PlazaFreehand);
        private bool LockingToWalls => featureTool == CastleFeatures.Tool.UpperArea && features.Freehand && features.LockToWalls;

        // Near a castle wall or tower (within the lock reach of its face, on the mouse's side of it), the aim locks onto
        // its face: a little into the stone, so the area built there tucks into the wall with no gap
        private void LockAim()
        {
            aimLocked = false;
            if (!aimed || !LockingToWalls) return;
            Transform walls = CastleContainer(false);
            if (walls == null) return;
            const float Tuck = 0.2f;
            float best = features.LockReach;
            Vector3 lockedAt = aim;
            Vector3 p = Flat(aim);
            foreach (CastlePiece piece in walls.GetComponentsInChildren<CastlePiece>())
            {
                if (piece.tower || piece.joint)
                {
                    Vector3 c = Flat(piece.transform.position);
                    Vector3 off = p - c;
                    float r = piece.Radius;
                    float fromFace = off.magnitude - r;
                    if (off.sqrMagnitude < 0.01f || Mathf.Abs(fromFace) > best) continue;
                    best = Mathf.Abs(fromFace);
                    lockedAt = c + off.normalized * (r - Tuck);
                    continue;
                }
                List<Vector3> path = piece.path;
                if (path == null || path.Count < 2) continue;
                float half = piece.width * 0.5f;
                int segments = piece.closed ? path.Count : path.Count - 1;
                for (int i = 0; i < segments; i++)
                {
                    Vector3 a = Flat(path[i]), b = Flat(path[(i + 1) % path.Count]);
                    Vector3 ab = b - a;
                    float t = ab.sqrMagnitude > 1e-6f ? Mathf.Clamp01(Vector3.Dot(p - a, ab) / ab.sqrMagnitude) : 0f;
                    Vector3 on = a + ab * t;
                    Vector3 off = p - on;
                    float fromFace = Mathf.Abs(off.magnitude - half);
                    if (fromFace > best) continue;
                    // (which face: the mouse's side of the wall; right on the line, the castle's inside)
                    Vector3 across = off.sqrMagnitude > 0.01f ? off.normalized : Vector3.Cross(ab.normalized, Vector3.down) * (piece.inner != 0 ? piece.inner : 1);
                    best = fromFace;
                    lockedAt = on + across * (half - Tuck);
                }
            }
            if (best >= features.LockReach) return;
            aimLocked = true;
            aim = Y(lockedAt, FeatureSurface(lockedAt) ?? aim.y);
        }

        private void BuildFeature(Vector3 a, Vector3 b, SceneView view)
        {
            PrepareCastle();
            features.Theme = castleTheme;
            features.BannerColour = castle.Detail.BannerColour;
            features.BannerTrim = castle.Detail.BannerTrim;
            Undo.IncrementCurrentGroup();
            Undo.SetCurrentGroupName("Castle Feature");
            int group = Undo.GetCurrentGroup();
            Transform parent = FeaturesContainer(true);
            GameObject built = null;
            string why = null;
            switch (featureTool)
            {
                case CastleFeatures.Tool.UpperArea:
                    if (features.Freehand)
                    {
                        built = CastleFeatures.UpperAreaOutline(new List<Vector3>(featureLine), new List<bool>(featureLocked), view.camera.transform.position, features, FeatureSurface, parent);
                        why = "Draw a bigger outline, all the way round.";
                    }
                    else
                    {
                        built = CastleFeatures.UpperArea(a, b, featureYaw, view.camera.transform.position, features, FeatureSurface, parent);
                        why = "Drag out a bigger area (at least 4 m each way).";
                    }
                    break;
                case CastleFeatures.Tool.Stairs:
                {
                    var stairLines = new List<List<Vector3>>();
                    float stairTop;
                    if (features.Stairs == CastleFeatures.StairShape.Freehand)
                    {
                        var path = new List<Vector3>(featureLine);
                        if (Flat(b - path[path.Count - 1]).magnitude > 0.2f) path.Add(b);
                        if (features.ConnectStairs) path = CastleFeatures.ConnectPath(path, features.StairWidth);
                        built = CastleFeatures.StairsAlong(path, features, FeatureSurface, parent, out why, out List<Vector3> line, out stairTop);
                        stairLines.Add(line);
                    }
                    else
                    {
                        StairPlan(a, b, out Vector3 low, out Vector3 top, out Vector3 climb, out _);
                        stairTop = top.y;
                        if (features.Stairs == CastleFeatures.StairShape.Straight)
                        {
                            built = CastleFeatures.Stairs(low, top, features, FeatureSurface, parent, out List<Vector3> line);
                            stairLines.Add(line);
                            why = "Stairs need to climb: drag from lower down to higher up.";
                        }
                        else
                        {
                            built = CastleFeatures.CurvedStairs(top, climb, low.y, features.Stairs == CastleFeatures.StairShape.DoubleCurved, features, FeatureSurface, parent,
                                                                out List<List<Vector3>> lines, out why);
                            stairLines.AddRange(lines);
                        }
                    }
                    // (arriving at an upper area: its railing opens where they come in)
                    if (built != null)
                    {
                        int opened = 0;
                        foreach (List<Vector3> line in stairLines) opened += CastleFeatures.OpenEdgesFor(line, stairTop, features.StairWidth);
                        if (opened == 0 && features.ConnectStairs && stairLines.Count > 0 && stairLines[0] != null && stairLines[0].Count > 0
                            && CastleFeatures.OnOlderUpperArea(Y(stairLines[0][stairLines[0].Count - 1], stairTop)))
                            ShowNotification(new GUIContent("That upper area was built before stairs could connect to it.\nRebuild it (delete and draw it again) to open its railing for stairs."));
                    }
                    break;
                }
                case CastleFeatures.Tool.Bridge:
                    built = CastleFeatures.Bridge(a, b, features, FeatureSurface, parent);
                    why = "Drag a longer bridge.";
                    break;
                case CastleFeatures.Tool.Street:
                    featureLine.Add(b);
                    built = CastleFeatures.Street(new List<Vector3>(featureLine), features, FeatureSurface, parent);
                    why = "Draw a longer street.";
                    break;
                case CastleFeatures.Tool.Plaza:
                    if (features.PlazaFreehand)
                    {
                        built = CastleFeatures.PlazaOutline(new List<Vector3>(featureLine), features, FeatureSurface, parent);
                        why = "Draw a bigger outline, all the way round.";
                    }
                    else
                    {
                        built = CastleFeatures.Plaza(a, Flat(b - a).magnitude, features, FeatureSurface, parent);
                        why = "Drag out a bigger plaza.";
                    }
                    break;
                case CastleFeatures.Tool.Keep:
                    built = CastleFeatures.Keep(a, featureYaw, features, castle, CastleGround, parent);
                    break;
            }
            if (built != null)
            {
                built.name = $"{built.name} {parent.childCount}";
                Undo.RegisterCreatedObjectUndo(built, "Castle Feature");
                status = null;
            }
            else status = why;
            Undo.CollapseUndoOperations(group);
            Repaint();
        }

        private void RubOutFeature(Vector2 mouse)
        {
            Ray ray = HandleUtility.GUIPointToWorldRay(mouse);
            if (!Physics.Raycast(ray, out RaycastHit hit, 5000f, ~0, QueryTriggerInteraction.Ignore)) return;
            CastleFeature feature = hit.collider.GetComponentInParent<CastleFeature>();
            if (feature == null) return;
            Undo.DestroyObjectImmediate(feature.gameObject);
        }

        /// <summary>Whether something built in Castle Features (that isn't walkable) stands at a point: houses keep off.</summary>
        private static bool FeatureInTheWay(Vector3 world)
        {
            var ray = new Ray(new Vector3(world.x, world.y + 300f, world.z), Vector3.down);
            foreach (RaycastHit h in Physics.RaycastAll(ray, 600f, ~0, QueryTriggerInteraction.Ignore))
            {
                CastleFeature f = h.collider.GetComponentInParent<CastleFeature>();
                if (f != null && !f.walkable) return true;
            }
            return false;
        }

        private void DrawFeaturePreview(bool erasing)
        {
            if (!aimed) return;
            Color colour = erasing ? BadColour : PlaceColour;
            Handles.color = colour;
            Vector3 lift = Vector3.up * 0.15f;
            Color locked = new Color(1f, 0.82f, 0.2f);
            if (Outlining && !erasing)
            {
                // The outline so far: gold where it's locked to a wall, and the way back to its start
                if (featureDragging && featureLine.Count > 0)
                {
                    for (int i = 1; i < featureLine.Count; i++)
                    {
                        Handles.color = featureLocked[i] && featureLocked[i - 1] ? locked : colour;
                        Handles.DrawAAPolyLine(featureLocked[i] && featureLocked[i - 1] ? 6f : 3f, featureLine[i - 1] + lift, featureLine[i] + lift);
                    }
                    Handles.color = colour;
                    Handles.DrawAAPolyLine(3f, featureLine[featureLine.Count - 1] + lift, aim + lift);
                    if (featureTool == CastleFeatures.Tool.Stairs)
                    {
                        // (stairs: how far they climb and how long their path is, against how long they need to be)
                        float length = Flat(aim - featureLine[featureLine.Count - 1]).magnitude;
                        for (int i = 1; i < featureLine.Count; i++) length += Flat(featureLine[i] - featureLine[i - 1]).magnitude;
                        float rise = Mathf.Abs(aim.y - featureLine[0].y);
                        float need = Mathf.Max(0f, Mathf.Ceil(rise / 0.17f) - 3f) * 0.27f;
                        Handles.Label(aim + Vector3.up * 1.5f, $"{rise:F1} m up, path {length:F0} m" + (length < need ? $" (needs about {need:F0} m)" : ""));
                    }
                    else
                    {
                        Handles.color = new Color(colour.r, colour.g, colour.b, 0.45f);
                        Handles.DrawDottedLine(aim + lift, featureLine[0] + lift, 4f);
                    }
                }
                Handles.color = aimLocked ? locked : colour;
                Handles.DrawWireDisc(aim + lift, Vector3.up, aimLocked ? 0.9f : 0.6f, aimLocked ? 4f : 2f);
                if (aimLocked) Handles.Label(aim + Vector3.up * 1.2f, "Locked to wall");
                return;
            }
            if (!featureDragging)
            {
                Handles.DrawWireDisc(aim + lift, Vector3.up, featureTool == CastleFeatures.Tool.Keep ? features.KeepCells : 0.6f);
                if (featureTool == CastleFeatures.Tool.UpperArea || featureTool == CastleFeatures.Tool.Keep)
                    Handles.DrawLine(aim + lift, aim + lift + Quaternion.Euler(0f, featureYaw, 0f) * Vector3.forward * 3f, 2f);
                return;
            }
            switch (featureTool)
            {
                case CastleFeatures.Tool.UpperArea:
                {
                    Quaternion turn = Quaternion.Euler(0f, featureYaw, 0f);
                    Vector3 d = Quaternion.Inverse(turn) * Flat(aim - featureStart);
                    Vector3 a = featureStart + lift, b = a + turn * new Vector3(d.x, 0f, 0f), c = a + turn * new Vector3(d.x, 0f, d.z), dd = a + turn * new Vector3(0f, 0f, d.z);
                    float top = Mathf.Max(featureStart.y, aim.y) + features.TerraceHeight;
                    Handles.DrawAAPolyLine(3f, a, b, c, dd, a);
                    Handles.color = new Color(colour.r, colour.g, colour.b, 0.5f);
                    Handles.DrawAAPolyLine(2f, Y(a, top), Y(b, top), Y(c, top), Y(dd, top), Y(a, top));
                    break;
                }
                case CastleFeatures.Tool.Street:
                    if (featureLine.Count > 0)
                    {
                        var points = new List<Vector3>(featureLine) { aim };
                        Handles.DrawAAPolyLine(3f, points.ConvertAll(p => p + lift).ToArray());
                    }
                    break;
                case CastleFeatures.Tool.Plaza:
                    Handles.DrawWireDisc(featureStart + lift, Vector3.up, Flat(aim - featureStart).magnitude, 3f);
                    break;
                default:
                {
                    if (featureTool == CastleFeatures.Tool.Stairs)
                    {
                        // Stairs: where they'll arrive (gold when they connect to an upper area), and which way they climb
                        StairPlan(featureStart, aim, out Vector3 low, out Vector3 top, out Vector3 climb, out bool connected);
                        Vector3 across = Vector3.Cross(Vector3.up, climb) * (features.StairWidth * 0.5f);
                        Handles.color = connected ? locked : colour;
                        Handles.DrawAAPolyLine(connected ? 6f : 3f, top - across + lift, top + across + lift);
                        Handles.DrawAAPolyLine(3f, low + lift, top + lift);
                        Handles.ConeHandleCap(0, top + lift - climb * 0.6f, Quaternion.LookRotation(climb), 0.6f, EventType.Repaint);
                        string shape = features.Stairs == CastleFeatures.StairShape.Straight ? "" : features.Stairs == CastleFeatures.StairShape.Curved ? " (curved)" : " (double curved)";
                        string note = connected ? "  -  connects to the upper area"
                                    : features.ConnectStairs && CastleFeatures.OnOlderUpperArea(top) ? "  -  this upper area was built before stairs could connect: rebuild it (delete it and draw it again)" : "";
                        Handles.Label(top + Vector3.up * 1.5f, $"{top.y - low.y:F1} m up{shape}{note}");
                        break;
                    }
                    // Bridges: a line between the ends, and how wide
                    float width = featureTool == CastleFeatures.Tool.Bridge ? features.BridgeWidth : features.StairWidth;
                    Vector3 side = Vector3.Cross(Vector3.up, Flat(aim - featureStart).normalized) * (width * 0.5f);
                    Handles.DrawAAPolyLine(3f, featureStart + side + lift, aim + side + lift, aim - side + lift, featureStart - side + lift, featureStart + side + lift);
                    Handles.Label(aim + Vector3.up * 1.5f, $"{Mathf.Abs(aim.y - featureStart.y):F1} m up, {Flat(aim - featureStart).magnitude:F1} m across");
                    break;
                }
            }
        }

        private static Vector3 Y(Vector3 v, float y) => new Vector3(v.x, y, v.z);

        // ================================================================ Stone colour (Castle Walls > Look)

        // The castle's stone colours as swatches with their names (the kit's own, the palette, a custom colour), and the
        // house themes' stone below
        private void DrawStonePicker()
        {
            BuildingThemes themes = BuildingThemes.Get();
            EditorGUILayout.LabelField(new GUIContent("Stone colour", "The colour of the castle's stone: walls, towers, castle features and props built from now on (Rebuild a section to recolour it)."), EditorStyles.miniBoldLabel);
            var entries = new List<(string theme, string label, Color colour)> { ("", "Kit's Own", new Color(0.72f, 0.64f, 0.54f)) };
            foreach (var (name, colour) in BuildingThemes.StonePalette) entries.Add((name, name, colour));
            BuildingThemes.Theme custom = themes.Find(BuildingThemes.CustomStone);
            if (custom != null) entries.Add((BuildingThemes.CustomStone, "Custom", BuildingThemes.StoneSwatch(custom.stone)));

            int perRow = Mathf.Max(1, Mathf.FloorToInt((position.width - 24f) / 118f));
            for (int i = 0; i < entries.Count; i += perRow)
            {
                EditorGUILayout.BeginHorizontal();
                for (int j = i; j < Mathf.Min(i + perRow, entries.Count); j++)
                {
                    var (theme, label, colour) = entries[j];
                    bool chosen = castleTheme == theme;
                    Rect r = GUILayoutUtility.GetRect(new GUIContent(label), EditorStyles.miniButton, GUILayout.Height(24), GUILayout.MinWidth(100));
                    if (GUI.Button(r, GUIContent.none, EditorStyles.miniButton)) { castleTheme = theme; Changed(); }
                    Rect sw = new Rect(r.x + 4f, r.y + 4f, 16f, r.height - 8f);
                    if (chosen) EditorGUI.DrawRect(new Rect(sw.x - 2f, sw.y - 2f, sw.width + 4f, sw.height + 4f), new Color(1f, 0.82f, 0.25f));
                    EditorGUI.DrawRect(sw, colour);
                    var style = new GUIStyle(EditorStyles.miniLabel) { fontStyle = chosen ? FontStyle.Bold : FontStyle.Normal, clipping = TextClipping.Clip };
                    GUI.Label(new Rect(sw.xMax + 5f, r.y, r.width - sw.width - 12f, r.height), label, style);
                }
                EditorGUILayout.EndHorizontal();
            }

            // Custom: any colour
            if (custom != null)
            {
                EditorGUI.BeginChangeCheck();
                Color picked = EditorGUILayout.ColorField(new GUIContent("Custom colour", "Your own stone colour (pick Custom above to use it)."), BuildingThemes.StoneSwatch(custom.stone), true, false, false);
                if (EditorGUI.EndChangeCheck())
                {
                    Undo.RecordObject(themes, "Custom Stone");
                    custom.stone = BuildingThemes.StoneTint(picked);
                    EditorUtility.SetDirty(themes);
                    themes.Refresh(custom);
                    castleTheme = BuildingThemes.CustomStone;
                    Changed();
                }
            }

            // Or the stone of one of the house themes (their roofs and wood too)
            var houseThemes = new List<string> { "—" };
            foreach (BuildingThemes.Theme t in themes.themes) if (t != null && !t.original && !t.stoneOnly) houseThemes.Add(t.name);
            int at = Mathf.Max(0, houseThemes.IndexOf(castleTheme));
            int now = EditorGUILayout.Popup(new GUIContent("Or like the houses", "Use a house colour theme instead (its stone, roofs and wood)."), at, houseThemes.Select(n => n == "—" ? "—" : $"Like the {n} houses").ToArray());
            if (now != at && now > 0) { castleTheme = houseThemes[now]; Changed(); }
        }

        // ================================================================ Weathering (Castle Walls > Weathering)

        private void DrawWeathering()
        {
            List<Material> stone = MedievalVillageSetup.WeatheredMaterials();
            if (stone.Count == 0) { Hint("The kit's stone materials aren't set up yet."); return; }
            List<Material> walls = stone.FindAll(m => !MedievalVillageSetup.IsPaving(m)), floors = stone.FindAll(MedievalVillageSetup.IsPaving);
            Material first = walls.Count > 0 ? walls[0] : stone[0];
            bool on = first.IsKeywordEnabled("_WEATHERED");
            float grime = first.GetFloat("_ClearCoatMask"), variation = first.GetFloat("_DetailAlbedoMapScale");
            Color mossColour = first.GetColor("_SpecColor");
            mossColour.a = 1f;

            EditorGUI.BeginChangeCheck();
            on = EditorGUILayout.Toggle(new GUIContent("Weathered stone", "Age the stone: patches of colour (so it doesn't look tiled), grime in the joints, rain streaks down the walls, and moss."), on);
            using (new EditorGUI.DisabledScope(!on))
            {
                grime = Slider("Grime and streaks", "Dirt in the joints and dark streaks the rain has run down the walls.", grime, 0f, 1f);
                variation = Slider("Colour variation", "Big soft patches of lighter, darker and warmer stone.", variation, 0f, 1f);
                mossColour = EditorGUILayout.ColorField(new GUIContent("Moss colour"), mossColour, true, false, false);
            }
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObjects(stone.ToArray(), "Castle Weathering");
                foreach (Material m in stone) MedievalVillageSetup.SetWeathering(m, on, grime, variation, mossColour);
            }

            using (new EditorGUI.DisabledScope(!on))
            {
                DrawMoss("Moss on walls and towers", "The stone walls, towers, battlements, steps and trim.", walls);
                DrawMoss("Moss on floors and paving", "Paved floors: upper areas, wall walks, plazas, bridges and stair treads.", floors);
            }

            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            if (GUILayout.Button(new GUIContent("Fresh", "Clean, new stone, no moss."), EditorStyles.miniButtonLeft, GUILayout.Width(70))) Preset(stone, 0.15f, 0f, 0f, 0.4f, mossColour);
            if (GUILayout.Button(new GUIContent("Weathered", "Lived-in stone, a little moss (the default)."), EditorStyles.miniButtonMid, GUILayout.Width(80))) Preset(stone, 0.55f, 0.35f, 0.15f, 0.6f, mossColour);
            if (GUILayout.Button(new GUIContent("Ancient", "Old, mossy, rain-streaked stone."), EditorStyles.miniButtonRight, GUILayout.Width(70))) Preset(stone, 0.9f, 0.75f, 0.5f, 0.8f, mossColour);
            EditorGUILayout.EndHorizontal();
            Hint("On the kit's stone, brick and rock trim (every colour theme too): castle walls, castle features and the houses' stonework.");
        }

        private static void Preset(List<Material> stone, float grime, float wallMoss, float floorMoss, float variation, Color mossColour)
        {
            Undo.RecordObjects(stone.ToArray(), "Castle Weathering");
            foreach (Material m in stone) MedievalVillageSetup.Weather(m, grime, wallMoss, floorMoss, variation, mossColour);
        }

        // One set of surfaces' moss: on top of things, in the joints, how big its patches are
        private void DrawMoss(string title, string tooltip, List<Material> materials)
        {
            if (materials.Count == 0) return;
            Material first = materials[0];
            float tops = first.GetFloat("_ClearCoatSmoothness"), joints = first.GetColor("_SpecColor").a, size = first.GetFloat("_DetailNormalMapScale");
            GUILayout.Space(2);
            EditorGUILayout.LabelField(new GUIContent(title, tooltip), EditorStyles.miniBoldLabel);
            EditorGUI.BeginChangeCheck();
            EditorGUI.indentLevel++;
            tops = Slider("On tops", "Patches of moss on faces that look up (the tops of walls, floors, steps).", tops, 0f, 1f);
            joints = Slider("In the joints", "Moss growing in the gaps and cracks between the stones.", joints, 0f, 1f);
            size = Slider("Patch size", "Small scattered tufts (low) up to big drifts (high).", size, 0.2f, 4f);
            EditorGUI.indentLevel--;
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObjects(materials.ToArray(), "Castle Moss");
                foreach (Material m in materials) MedievalVillageSetup.SetMoss(m, tops, joints, size);
            }
        }


        // ================================================================ Prefs

        private void LoadFeaturePrefs()
        {
            var s = features;
            featureTool = (CastleFeatures.Tool)EditorPrefs.GetInt(Prefs + "Feature.Tool", 0);
            s.TerraceHeight = EditorPrefs.GetFloat(Prefs + "Feature.TerraceHeight", 4f);
            s.TerraceEdge = (CastleFeatures.Edge)EditorPrefs.GetInt(Prefs + "Feature.TerraceEdge", 0);
            s.TerraceStairs = (CastleFeatures.StairSides)EditorPrefs.GetInt(Prefs + "Feature.TerraceStairs", 1);
            s.TerraceLamps = EditorPrefs.GetBool(Prefs + "Feature.TerraceLamps", true);
            s.TerracePlanters = EditorPrefs.GetBool(Prefs + "Feature.TerracePlanters", true);
            s.Freehand = EditorPrefs.GetBool(Prefs + "Feature.Freehand", true);
            s.LockToWalls = EditorPrefs.GetBool(Prefs + "Feature.LockToWalls", true);
            s.LockReach = EditorPrefs.GetFloat(Prefs + "Feature.LockReach", 3f);
            s.StairWidth = EditorPrefs.GetFloat(Prefs + "Feature.StairWidth", 4f);
            s.StairRail = (CastleFeatures.Rail)EditorPrefs.GetInt(Prefs + "Feature.StairRail", 0);
            s.StairFoot = (CastleFeatures.Foot)EditorPrefs.GetInt(Prefs + "Feature.StairFoot", (int)CastleFeatures.Foot.Round);
            s.Stairs = (CastleFeatures.StairShape)EditorPrefs.GetInt(Prefs + "Feature.StairShape", 0);
            s.StairTurn = EditorPrefs.GetFloat(Prefs + "Feature.StairTurn", 90f);
            s.DoubleTurn = EditorPrefs.GetFloat(Prefs + "Feature.DoubleTurn", 140f);
            s.DoubleInward = EditorPrefs.GetBool(Prefs + "Feature.DoubleInward", false);
            s.CurveRight = EditorPrefs.GetBool(Prefs + "Feature.CurveRight", true);
            s.ConnectStairs = EditorPrefs.GetBool(Prefs + "Feature.ConnectStairs", true);
            s.PlazaFreehand = EditorPrefs.GetBool(Prefs + "Feature.PlazaFreehand", false);
            s.Landings = EditorPrefs.GetBool(Prefs + "Feature.Landings", true);
            s.StairLamps = EditorPrefs.GetBool(Prefs + "Feature.StairLamps", true);
            s.Bridge = (CastleFeatures.BridgeStyle)EditorPrefs.GetInt(Prefs + "Feature.Bridge", 0);
            s.BridgeWidth = EditorPrefs.GetFloat(Prefs + "Feature.BridgeWidth", 4f);
            s.BridgeRail = (CastleFeatures.Rail)EditorPrefs.GetInt(Prefs + "Feature.BridgeRail", 1);
            s.ArchSpan = EditorPrefs.GetFloat(Prefs + "Feature.ArchSpan", 12f);
            s.BridgeLamps = EditorPrefs.GetBool(Prefs + "Feature.BridgeLamps", true);
            s.StreetHalf = EditorPrefs.GetFloat(Prefs + "Feature.StreetHalf", 3f);
            s.Spacing = EditorPrefs.GetFloat(Prefs + "Feature.Spacing", 9f);
            s.BothSides = EditorPrefs.GetBool(Prefs + "Feature.BothSides", true);
            s.Lamps = EditorPrefs.GetBool(Prefs + "Feature.Lamps", true);
            s.Banners = EditorPrefs.GetBool(Prefs + "Feature.Banners", true);
            s.Hedges = EditorPrefs.GetBool(Prefs + "Feature.Hedges", true);
            s.Trees = EditorPrefs.GetBool(Prefs + "Feature.Trees", true);
            s.Benches = EditorPrefs.GetBool(Prefs + "Feature.Benches", true);
            s.Fountain = EditorPrefs.GetBool(Prefs + "Feature.Fountain", true);
            s.PlazaSteps = EditorPrefs.GetInt(Prefs + "Feature.PlazaSteps", 2);
            s.PlazaLamps = EditorPrefs.GetBool(Prefs + "Feature.PlazaLamps", true);
            s.KeepCells = EditorPrefs.GetInt(Prefs + "Feature.KeepCells", 6);
            s.KeepStoreys = EditorPrefs.GetInt(Prefs + "Feature.KeepStoreys", 7);
            s.KeepStyle = (CastleWallBuilder.TowerStyle)EditorPrefs.GetInt(Prefs + "Feature.KeepStyle", (int)CastleWallBuilder.TowerStyle.Turreted);
            s.Lights = EditorPrefs.GetBool(Prefs + "Feature.Lights", false);
        }

        private void SaveFeaturePrefs()
        {
            var s = features;
            EditorPrefs.SetInt(Prefs + "Feature.Tool", (int)featureTool);
            EditorPrefs.SetFloat(Prefs + "Feature.TerraceHeight", s.TerraceHeight);
            EditorPrefs.SetInt(Prefs + "Feature.TerraceEdge", (int)s.TerraceEdge);
            EditorPrefs.SetInt(Prefs + "Feature.TerraceStairs", (int)s.TerraceStairs);
            EditorPrefs.SetBool(Prefs + "Feature.TerraceLamps", s.TerraceLamps);
            EditorPrefs.SetBool(Prefs + "Feature.TerracePlanters", s.TerracePlanters);
            EditorPrefs.SetBool(Prefs + "Feature.Freehand", s.Freehand);
            EditorPrefs.SetBool(Prefs + "Feature.LockToWalls", s.LockToWalls);
            EditorPrefs.SetFloat(Prefs + "Feature.LockReach", s.LockReach);
            EditorPrefs.SetFloat(Prefs + "Feature.StairWidth", s.StairWidth);
            EditorPrefs.SetInt(Prefs + "Feature.StairRail", (int)s.StairRail);
            EditorPrefs.SetInt(Prefs + "Feature.StairFoot", (int)s.StairFoot);
            EditorPrefs.SetInt(Prefs + "Feature.StairShape", (int)s.Stairs);
            EditorPrefs.SetFloat(Prefs + "Feature.StairTurn", s.StairTurn);
            EditorPrefs.SetFloat(Prefs + "Feature.DoubleTurn", s.DoubleTurn);
            EditorPrefs.SetBool(Prefs + "Feature.DoubleInward", s.DoubleInward);
            EditorPrefs.SetBool(Prefs + "Feature.CurveRight", s.CurveRight);
            EditorPrefs.SetBool(Prefs + "Feature.ConnectStairs", s.ConnectStairs);
            EditorPrefs.SetBool(Prefs + "Feature.PlazaFreehand", s.PlazaFreehand);
            EditorPrefs.SetBool(Prefs + "Feature.Landings", s.Landings);
            EditorPrefs.SetBool(Prefs + "Feature.StairLamps", s.StairLamps);
            EditorPrefs.SetInt(Prefs + "Feature.Bridge", (int)s.Bridge);
            EditorPrefs.SetFloat(Prefs + "Feature.BridgeWidth", s.BridgeWidth);
            EditorPrefs.SetInt(Prefs + "Feature.BridgeRail", (int)s.BridgeRail);
            EditorPrefs.SetFloat(Prefs + "Feature.ArchSpan", s.ArchSpan);
            EditorPrefs.SetBool(Prefs + "Feature.BridgeLamps", s.BridgeLamps);
            EditorPrefs.SetFloat(Prefs + "Feature.StreetHalf", s.StreetHalf);
            EditorPrefs.SetFloat(Prefs + "Feature.Spacing", s.Spacing);
            EditorPrefs.SetBool(Prefs + "Feature.BothSides", s.BothSides);
            EditorPrefs.SetBool(Prefs + "Feature.Lamps", s.Lamps);
            EditorPrefs.SetBool(Prefs + "Feature.Banners", s.Banners);
            EditorPrefs.SetBool(Prefs + "Feature.Hedges", s.Hedges);
            EditorPrefs.SetBool(Prefs + "Feature.Trees", s.Trees);
            EditorPrefs.SetBool(Prefs + "Feature.Benches", s.Benches);
            EditorPrefs.SetBool(Prefs + "Feature.Fountain", s.Fountain);
            EditorPrefs.SetInt(Prefs + "Feature.PlazaSteps", s.PlazaSteps);
            EditorPrefs.SetBool(Prefs + "Feature.PlazaLamps", s.PlazaLamps);
            EditorPrefs.SetInt(Prefs + "Feature.KeepCells", s.KeepCells);
            EditorPrefs.SetInt(Prefs + "Feature.KeepStoreys", s.KeepStoreys);
            EditorPrefs.SetInt(Prefs + "Feature.KeepStyle", (int)s.KeepStyle);
            EditorPrefs.SetBool(Prefs + "Feature.Lights", s.Lights);
        }
    }
}
#endif
