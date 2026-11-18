#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace YaraJ.CastleVillageBuilder
{
    // The Details tab: props brushed, placed one at a time, or fences drawn along a line
    internal sealed partial class CastleVillageBuilderWindow
    {
        private enum DetailTool { Brush, Single, Line }

        private DetailTool detailTool = DetailTool.Brush;
        private readonly bool[] detailOn = new bool[CastleFeatures.Details.Length];
        private int detailCurrent;                 // One at a Time: the one being placed
        private float detailBrush = 8f, detailSpacing = 0.4f, detailScaleMin = 0.9f, detailScaleMax = 1.1f, detailYaw;
        private int detailPerDab = 3;
        private bool detailFollowSlope = true, detailRandomTurn = true, detailLights;
        private CastleFeatures.Fence fenceKind = CastleFeatures.Fence.Wooden;
        private bool detailDragging;
        private Vector3 detailLastDab;
        private readonly List<Vector3> detailLine = new List<Vector3>();
        private int detailUndo;
        private readonly System.Random detailRandom = new System.Random();

        // ================================================================ Window

        private void DrawPropsTab()
        {
            EditorGUI.BeginChangeCheck();
            detailTool = (DetailTool)ToolPicker("Place", (int)detailTool, new[]
            {
                new GUIContent("Brush", "Scatter the ticked props under the brush (drag to keep going)."),
                new GUIContent("One at a Time", "Place one prop where you click: [ and ] turn it, Space for the next ticked one."),
                new GUIContent("Fence Line", "Draw a line: a fence, low wall, hedge or railing goes along it.")
            });
            if (EditorGUI.EndChangeCheck()) { detailDragging = false; Changed(); }
            HowTo(detailTool == DetailTool.Brush ? "Click or drag to scatter the ticked props. Shift: rub out props and fences. [ ]: brush size. Esc: stop."
               : detailTool == DetailTool.Single ? "Click to place. [ and ]: turn it. Space: the next ticked prop. Shift+click: rub out. Esc: stop."
               : "Draw along the ground and let go: the fence follows the line, post to post. Shift: rub out spans.");

            EditorGUI.BeginChangeCheck();
            if (detailTool == DetailTool.Line)
            {
                if (Section("DetailFence", "Fence", true))
                    fenceKind = (CastleFeatures.Fence)GUILayout.SelectionGrid((int)fenceKind, CastleFeatures.FenceNames, 3, GUILayout.Height(44));
            }
            else
            {
                if (Section("DetailProps", "Props", true)) DrawDetailPicker();
                if (Section("DetailPlacing", "Placing", true))
                {
                    if (detailTool == DetailTool.Brush)
                    {
                        detailBrush = Slider("Brush size", "How far across the brush reaches (m). [ and ] change it.", detailBrush, 2f, 60f);
                        detailPerDab = EditorGUILayout.IntSlider(new GUIContent("Props per dab", "How many props each click (and each brush-width of dragging) tries to put down."), detailPerDab, 1, 20);
                        detailSpacing = Slider("Gap between", "The least room left between props (m, past their own size).", detailSpacing, 0f, 5f);
                        detailRandomTurn = EditorGUILayout.Toggle(new GUIContent("Random turn", "Turn each one a random way (or all the same way)."), detailRandomTurn);
                    }
                    EditorGUILayout.BeginHorizontal();
                    EditorGUILayout.PrefixLabel(new GUIContent("Size", "A random size between these (1: as made)."));
                    detailScaleMin = EditorGUILayout.FloatField(detailScaleMin, GUILayout.Width(40));
                    EditorGUILayout.MinMaxSlider(ref detailScaleMin, ref detailScaleMax, 0.5f, 2f);
                    detailScaleMax = EditorGUILayout.FloatField(detailScaleMax, GUILayout.Width(40));
                    EditorGUILayout.EndHorizontal();
                    detailFollowSlope = EditorGUILayout.Toggle(new GUIContent("Lean with slopes", "Tilt a little with sloping ground (or stand straight up)."), detailFollowSlope);
                    detailLights = EditorGUILayout.Toggle(new GUIContent("Real lights", "A point light in each lamp and brazier (lovely at night, but each costs: use on a few)."), detailLights);
                }
            }
            if (EditorGUI.EndChangeCheck()) Changed();
            if (Section("DetailBuilt", "In the scene", true)) DrawDetailList();
        }

        private void DrawDetailPicker()
        {
            string group = null;
            int perRow = Mathf.Max(1, Mathf.FloorToInt((position.width - 20f) / 120f));
            int col = 0;
            for (int i = 0; i < CastleFeatures.Details.Length; i++)
            {
                CastleFeatures.DetailInfo info = CastleFeatures.Details[i];
                if (info.Group != group)
                {
                    if (col != 0) { EditorGUILayout.EndHorizontal(); col = 0; }
                    group = info.Group;
                    GUILayout.Label(group, EditorStyles.miniBoldLabel);
                }
                if (col == 0) EditorGUILayout.BeginHorizontal();
                bool current = detailTool == DetailTool.Single && i == detailCurrent;
                Color was = GUI.backgroundColor;
                if (current) GUI.backgroundColor = new Color(1f, 0.85f, 0.4f);
                bool now = GUILayout.Toggle(detailOn[i], new GUIContent(info.Name, info.Tip), EditorStyles.miniButton, GUILayout.Height(22), GUILayout.MinWidth(80));
                GUI.backgroundColor = was;
                if (now != detailOn[i])
                {
                    detailOn[i] = now;
                    if (now) detailCurrent = i;
                }
                if (++col == perRow) { EditorGUILayout.EndHorizontal(); col = 0; }
            }
            if (col != 0) EditorGUILayout.EndHorizontal();
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("All", EditorStyles.miniButtonLeft)) for (int i = 0; i < detailOn.Length; i++) detailOn[i] = true;
            if (GUILayout.Button("None", EditorStyles.miniButtonMid)) for (int i = 0; i < detailOn.Length; i++) detailOn[i] = false;
            if (GUILayout.Button(new GUIContent("Market", "Barrels, sacks, crates, stalls, hay, tables."), EditorStyles.miniButtonMid)) Pick(CastleFeatures.Detail.Barrel, CastleFeatures.Detail.BarrelStack, CastleFeatures.Detail.Sacks, CastleFeatures.Detail.Crates, CastleFeatures.Detail.MarketStall, CastleFeatures.Detail.HayBale, CastleFeatures.Detail.TableAndStools);
            if (GUILayout.Button(new GUIContent("Courtyard", "Lamps, braziers, banners, benches, planters, cypresses, a well."), EditorStyles.miniButtonMid)) Pick(CastleFeatures.Detail.LampPost, CastleFeatures.Detail.Brazier, CastleFeatures.Detail.BannerPole, CastleFeatures.Detail.Bench, CastleFeatures.Detail.Planter, CastleFeatures.Detail.Cypress);
            if (GUILayout.Button(new GUIContent("Barracks", "Weapon racks, targets, barrels, braziers."), EditorStyles.miniButtonRight)) Pick(CastleFeatures.Detail.WeaponRack, CastleFeatures.Detail.ArcheryTarget, CastleFeatures.Detail.Barrel, CastleFeatures.Detail.Brazier, CastleFeatures.Detail.Crates);
            EditorGUILayout.EndHorizontal();
        }

        private void Pick(params CastleFeatures.Detail[] kinds)
        {
            for (int i = 0; i < detailOn.Length; i++) detailOn[i] = false;
            foreach (var k in kinds) detailOn[(int)k] = true;
            detailCurrent = (int)kinds[0];
            Changed();
        }

        private void DrawDetailList()
        {
            Transform container = DetailContainer(false);
            int props = 0, spans = 0;
            if (container != null)
                foreach (Transform child in container)
                {
                    if (child.name.StartsWith("Fence ")) spans += child.childCount;
                    else props++;
                }
            Hint(container == null ? "Nothing yet. Props go under \"" + CastleFeatures.DetailsContainer + "\" in the Hierarchy."
                                   : $"{props} prop{(props == 1 ? "" : "s")} and {spans} fence span{(spans == 1 ? "" : "s")}, under \"{CastleFeatures.DetailsContainer}\".");
            using (new EditorGUI.DisabledScope(container == null))
            {
                if (GUILayout.Button("Remove All Details") && EditorUtility.DisplayDialog("Remove all details?", "Remove every prop and fence? (Undo brings them back.)", "Remove", "Cancel"))
                    Undo.DestroyObjectImmediate(container.gameObject);
            }
        }

        private static Transform DetailContainer(bool make)
        {
            foreach (GameObject root in UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects())
                if (root.name == CastleFeatures.DetailsContainer) return root.transform;
            if (!make) return null;
            var go = new GameObject(CastleFeatures.DetailsContainer);
            GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.BatchingStatic);
            Undo.RegisterCreatedObjectUndo(go, "Details");
            return go.transform;
        }

        // ================================================================ Scene view

        private void DetailsSceneGUI(SceneView view, Event e, int id, bool erasing)
        {
            switch (e.type)
            {
                case EventType.Layout:
                    HandleUtility.AddDefaultControl(id);
                    break;

                case EventType.KeyDown:
                    if (e.keyCode == KeyCode.Escape) { if (detailDragging) detailDragging = false; else StopPainting(); Repaint(); e.Use(); }
                    else if (e.keyCode == KeyCode.LeftBracket || e.keyCode == KeyCode.RightBracket)
                    {
                        float sign = e.keyCode == KeyCode.RightBracket ? 1f : -1f;
                        if (detailTool == DetailTool.Brush) detailBrush = Mathf.Clamp(detailBrush * (sign > 0f ? 1.25f : 0.8f), 2f, 60f);
                        else detailYaw = Mathf.Repeat(detailYaw + sign * 15f, 360f);
                        Changed(); Repaint(); e.Use();
                    }
                    else if (e.keyCode == KeyCode.Space && detailTool == DetailTool.Single) { NextDetail(); Repaint(); view.Repaint(); e.Use(); }
                    break;

                case EventType.MouseMove:
                    Aim(e.mousePosition, view);
                    view.Repaint();
                    break;

                case EventType.MouseDown:
                    if (e.button != 0 || e.alt) break;
                    Aim(e.mousePosition, view);
                    if (!aimed) break;
                    GUIUtility.hotControl = id;
                    Undo.IncrementCurrentGroup();
                    Undo.SetCurrentGroupName(erasing ? "Rub Out Details" : "Details");
                    detailUndo = Undo.GetCurrentGroup();
                    groundIsLand = Ground.Refresh();
                    detailDragging = true;
                    detailLastDab = aim;
                    if (erasing) RubOutDetails(aim, DetailEraseRadius);
                    else if (detailTool == DetailTool.Brush) DetailDab(aim);
                    else if (detailTool == DetailTool.Single) { PlaceDetail((CastleFeatures.Detail)detailCurrent, aim, aimNormal, detailYaw); }
                    else { detailLine.Clear(); detailLine.Add(aim); }
                    e.Use();
                    break;

                case EventType.MouseDrag:
                    if (GUIUtility.hotControl != id) break;
                    Aim(e.mousePosition, view);
                    if (aimed && detailDragging)
                    {
                        float step = Flat(aim - detailLastDab).magnitude;
                        if (erasing && step >= DetailEraseRadius * 0.5f) { RubOutDetails(aim, DetailEraseRadius); detailLastDab = aim; }
                        else if (!erasing && detailTool == DetailTool.Brush && step >= detailBrush * 0.5f) { DetailDab(aim); detailLastDab = aim; }
                        else if (!erasing && detailTool == DetailTool.Line && Flat(aim - detailLine[detailLine.Count - 1]).magnitude >= 0.75f) detailLine.Add(aim);
                    }
                    view.Repaint();
                    e.Use();
                    break;

                case EventType.MouseUp:
                    if (GUIUtility.hotControl != id) break;
                    GUIUtility.hotControl = 0;
                    if (detailDragging && !erasing && detailTool == DetailTool.Line)
                    {
                        if (aimed) detailLine.Add(aim);
                        Transform container = DetailContainer(true);
                        int n = 1;
                        while (container.Find($"Fence {n}") != null) n++;
                        var group = new GameObject($"Fence {n}");
                        group.transform.SetParent(container, false);
                        Undo.RegisterCreatedObjectUndo(group, "Fence");
                        List<GameObject> spans = CastleFeatures.BuildFence(new List<Vector3>(detailLine), fenceKind, FeatureSurface, group.transform, castleTheme);
                        foreach (GameObject s in spans) Undo.RegisterCreatedObjectUndo(s, "Fence");
                        if (spans.Count == 0) { Undo.DestroyObjectImmediate(group); status = "Draw a longer line."; }
                    }
                    detailDragging = false;
                    Undo.CollapseUndoOperations(detailUndo);
                    e.Use();
                    break;

                case EventType.Repaint:
                    DrawDetailPreview(erasing);
                    break;
            }
        }

        private float DetailEraseRadius => detailTool == DetailTool.Brush ? detailBrush * 0.5f : 1.5f;

        private void NextDetail()
        {
            for (int k = 1; k <= detailOn.Length; k++)
            {
                int i = (detailCurrent + k) % detailOn.Length;
                if (detailOn[i]) { detailCurrent = i; return; }
            }
        }

        // A dab of the brush: a few of the ticked props at random spots under it, clear of each other
        private void DetailDab(Vector3 centre)
        {
            var kinds = new List<int>();
            for (int i = 0; i < detailOn.Length; i++) if (detailOn[i]) kinds.Add(i);
            if (kinds.Count == 0) { status = "Tick some props to scatter."; return; }
            for (int n = 0; n < detailPerDab; n++)
            {
                for (int attempt = 0; attempt < 8; attempt++)
                {
                    Vector2 r = Random.insideUnitCircle * detailBrush * 0.5f;
                    Vector3 p = centre + new Vector3(r.x, 0f, r.y);
                    if (!Ground.At(p, out Vector3 ground, out Vector3 normal)) continue;
                    if (Ground.UnderWater(ground, 0.05f) || Vector3.Angle(normal, Vector3.up) > 40f) continue;
                    var kind = (CastleFeatures.Detail)kinds[Random.Range(0, kinds.Count)];
                    if (DetailCrowded(ground, CastleFeatures.Details[(int)kind].Radius)) continue;
                    PlaceDetail(kind, ground, normal, detailRandomTurn ? Random.Range(0f, 360f) : detailYaw);
                    break;
                }
            }
        }

        private bool DetailCrowded(Vector3 at, float radius)
        {
            Transform container = DetailContainer(false);
            if (container != null)
            {
                foreach (Transform child in container)
                {
                    if (child.name.StartsWith("Fence ")) continue;
                    float theirs = 0.6f;
                    for (int i = 0; i < CastleFeatures.Details.Length; i++)
                        if (child.name.StartsWith(CastleFeatures.Details[i].Name)) { theirs = CastleFeatures.Details[i].Radius; break; }
                    if (Flat(child.position - at).magnitude < radius + theirs + detailSpacing) return true;
                }
            }
            // (not inside houses, castle walls or towers)
            return OverBuilding(at);
        }

        private static bool OverBuilding(Vector3 at)
        {
            foreach (Collider c in Physics.OverlapSphere(at + Vector3.up * 1.2f, 0.6f, ~0, QueryTriggerInteraction.Ignore))
            {
                if (c.GetComponentInParent<ScatteredBuilding>() != null || c.GetComponentInParent<CastlePiece>() != null) return true;
                CastleFeature f = c.GetComponentInParent<CastleFeature>();
                if (f != null && !f.walkable) return true;
            }
            return false;
        }

        private GameObject PlaceDetail(CastleFeatures.Detail kind, Vector3 at, Vector3 normal, float yaw)
        {
            CastleFeatures.DetailInfo info = CastleFeatures.Details[(int)kind];
            GameObject prefab = CastleFeatures.DetailPrefab(kind, detailRandom.Next(CastleFeatures.VariantCount(kind)));
            if (prefab == null) return null;
            Transform container = DetailContainer(true);
            GameObject go;
            if (kind == CastleFeatures.Detail.Crates && Random.value < 0.5f)
            {
                // Crates: sometimes two or three, stacked
                go = new GameObject(info.Name);
                go.transform.SetParent(container, false);
                var first = (GameObject)PrefabUtility.InstantiatePrefab(prefab, go.transform);
                Bounds b = Bounds(first);
                var second = (GameObject)PrefabUtility.InstantiatePrefab(prefab, go.transform);
                second.transform.localPosition = new Vector3(b.size.x * 1.02f, 0f, 0f);
                second.transform.localRotation = Quaternion.Euler(0f, Random.Range(-10f, 10f), 0f);
                if (Random.value < 0.6f)
                {
                    var top = (GameObject)PrefabUtility.InstantiatePrefab(prefab, go.transform);
                    top.transform.localPosition = new Vector3(b.size.x * 0.5f, b.size.y, 0f);
                    top.transform.localRotation = Quaternion.Euler(0f, Random.Range(-20f, 20f), 0f);
                }
            }
            else
            {
                go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, container);
                go.name = info.Name;
            }
            Quaternion tilt = detailFollowSlope ? Quaternion.FromToRotation(Vector3.up, Vector3.Slerp(Vector3.up, normal, 0.5f)) : Quaternion.identity;
            go.transform.SetPositionAndRotation(at, tilt * Quaternion.Euler(0f, yaw, 0f) * prefab.transform.localRotation);
            go.transform.localScale *= Random.Range(detailScaleMin, Mathf.Max(detailScaleMin, detailScaleMax));
            if (kind == CastleFeatures.Detail.BannerPole) TintBanner(go);
            if (detailLights && info.LightAt.HasValue)
            {
                var light = new GameObject("Light").AddComponent<Light>();
                light.transform.SetParent(go.transform, false);
                light.transform.localPosition = Vector3.up * info.LightAt.Value;
                light.type = LightType.Point;
                light.color = kind == CastleFeatures.Detail.Brazier ? new Color(1f, 0.55f, 0.25f) : new Color(1f, 0.68f, 0.38f);
                light.range = kind == CastleFeatures.Detail.Brazier ? 9f : 8f;
                light.intensity = 1.8f;
                light.shadows = LightShadows.None;
            }
            MeshMerger.OptimizeProp(go, false);
            if (!string.IsNullOrEmpty(castleTheme)) BuildingThemes.Apply(go, castleTheme, false);
            Undo.RegisterCreatedObjectUndo(go, "Details");
            return go;
        }

        private static Bounds Bounds(GameObject go)
        {
            var b = new Bounds(go.transform.position, Vector3.zero);
            foreach (Renderer r in go.GetComponentsInChildren<Renderer>()) b.Encapsulate(r.bounds);
            return b;
        }

        // Banners in the castle's colours (Castle Walls > Details)
        private void TintBanner(GameObject go)
        {
            Material cloth = CastleWallBuilder.DetailMaterial("Banner " + ColorUtility.ToHtmlStringRGB(castle.Detail.BannerColour), castle.Detail.BannerColour, 0f, 0.1f);
            Material trim = CastleWallBuilder.DetailMaterial("Banner Trim " + ColorUtility.ToHtmlStringRGB(castle.Detail.BannerTrim), castle.Detail.BannerTrim, 0.4f, 0.4f);
            foreach (Renderer r in go.GetComponentsInChildren<Renderer>())
            {
                Material[] mats = r.sharedMaterials;
                for (int i = 0; i < mats.Length; i++)
                {
                    if (mats[i] == null) continue;
                    if (mats[i].name.StartsWith("Banner Trim")) mats[i] = trim;
                    else if (mats[i].name.StartsWith("Banner")) mats[i] = cloth;
                }
                r.sharedMaterials = mats;
            }
        }

        private void RubOutDetails(Vector3 centre, float radius)
        {
            Transform container = DetailContainer(false);
            if (container == null) return;
            var doomed = new List<GameObject>();
            foreach (Transform child in container)
            {
                if (child.name.StartsWith("Fence "))
                {
                    foreach (Transform span in child)
                        if (Near(span.gameObject, centre, radius)) doomed.Add(span.gameObject);
                }
                else if (Near(child.gameObject, centre, radius)) doomed.Add(child.gameObject);
            }
            foreach (GameObject go in doomed)
            {
                Transform parent = go.transform.parent;
                Undo.DestroyObjectImmediate(go);
                if (parent != null && parent != container && parent.childCount == 0) Undo.DestroyObjectImmediate(parent.gameObject);
            }
        }

        private static bool Near(GameObject go, Vector3 centre, float radius)
        {
            foreach (Renderer r in go.GetComponentsInChildren<Renderer>())
            {
                Vector3 p = r.bounds.ClosestPoint(new Vector3(centre.x, r.bounds.center.y, centre.z));
                if (Flat(p - centre).sqrMagnitude <= radius * radius) return true;
            }
            return false;
        }

        private void DrawDetailPreview(bool erasing)
        {
            if (!aimed) return;
            Vector3 lift = Vector3.up * 0.1f;
            if (erasing)
            {
                Handles.color = BadColour;
                Handles.DrawWireDisc(aim + lift, Vector3.up, DetailEraseRadius, 2f);
                Handles.Label(aim + Vector3.up * 1.5f, "Rubbing out details");
                return;
            }
            Handles.color = PlaceColour;
            switch (detailTool)
            {
                case DetailTool.Brush:
                    Handles.DrawWireDisc(aim + lift, Vector3.up, detailBrush * 0.5f, 2f);
                    break;
                case DetailTool.Single:
                {
                    CastleFeatures.DetailInfo info = CastleFeatures.Details[detailCurrent];
                    Handles.DrawWireDisc(aim + lift, Vector3.up, info.Radius, 2f);
                    Handles.DrawLine(aim + lift, aim + lift + Quaternion.Euler(0f, detailYaw, 0f) * Vector3.forward * (info.Radius + 0.8f), 2f);
                    Handles.Label(aim + Vector3.up * 1.5f, info.Name);
                    break;
                }
                case DetailTool.Line:
                    if (detailDragging && detailLine.Count > 0)
                    {
                        var points = new List<Vector3>(detailLine) { aim };
                        Handles.DrawAAPolyLine(3f, points.ConvertAll(p => p + lift).ToArray());
                    }
                    else Handles.DrawWireDisc(aim + lift, Vector3.up, 0.5f, 2f);
                    Handles.Label(aim + Vector3.up * 1.5f, CastleFeatures.FenceNames[(int)fenceKind]);
                    break;
            }
        }

        // ================================================================ Prefs

        private void LoadDetailPrefs()
        {
            detailTool = (DetailTool)EditorPrefs.GetInt(Prefs + "Detail.Tool", 0);
            int mask = EditorPrefs.GetInt(Prefs + "Detail.On", (1 << (int)CastleFeatures.Detail.Barrel) | (1 << (int)CastleFeatures.Detail.Sacks) | (1 << (int)CastleFeatures.Detail.Crates) | (1 << (int)CastleFeatures.Detail.HayBale));
            for (int i = 0; i < detailOn.Length; i++) detailOn[i] = (mask & (1 << i)) != 0;
            detailCurrent = Mathf.Clamp(EditorPrefs.GetInt(Prefs + "Detail.Current", 0), 0, detailOn.Length - 1);
            detailBrush = EditorPrefs.GetFloat(Prefs + "Detail.Brush", 8f);
            detailPerDab = EditorPrefs.GetInt(Prefs + "Detail.PerDab", 3);
            detailSpacing = EditorPrefs.GetFloat(Prefs + "Detail.Spacing", 0.4f);
            detailScaleMin = EditorPrefs.GetFloat(Prefs + "Detail.ScaleMin", 0.9f);
            detailScaleMax = EditorPrefs.GetFloat(Prefs + "Detail.ScaleMax", 1.1f);
            detailFollowSlope = EditorPrefs.GetBool(Prefs + "Detail.FollowSlope", true);
            detailRandomTurn = EditorPrefs.GetBool(Prefs + "Detail.RandomTurn", true);
            detailLights = EditorPrefs.GetBool(Prefs + "Detail.Lights", false);
            fenceKind = (CastleFeatures.Fence)EditorPrefs.GetInt(Prefs + "Detail.Fence", 0);
        }

        private void SaveDetailPrefs()
        {
            EditorPrefs.SetInt(Prefs + "Detail.Tool", (int)detailTool);
            int mask = 0;
            for (int i = 0; i < detailOn.Length; i++) if (detailOn[i]) mask |= 1 << i;
            EditorPrefs.SetInt(Prefs + "Detail.On", mask);
            EditorPrefs.SetInt(Prefs + "Detail.Current", detailCurrent);
            EditorPrefs.SetFloat(Prefs + "Detail.Brush", detailBrush);
            EditorPrefs.SetInt(Prefs + "Detail.PerDab", detailPerDab);
            EditorPrefs.SetFloat(Prefs + "Detail.Spacing", detailSpacing);
            EditorPrefs.SetFloat(Prefs + "Detail.ScaleMin", detailScaleMin);
            EditorPrefs.SetFloat(Prefs + "Detail.ScaleMax", detailScaleMax);
            EditorPrefs.SetBool(Prefs + "Detail.FollowSlope", detailFollowSlope);
            EditorPrefs.SetBool(Prefs + "Detail.RandomTurn", detailRandomTurn);
            EditorPrefs.SetBool(Prefs + "Detail.Lights", detailLights);
            EditorPrefs.SetInt(Prefs + "Detail.Fence", (int)fenceKind);
        }
    }
}
#endif
