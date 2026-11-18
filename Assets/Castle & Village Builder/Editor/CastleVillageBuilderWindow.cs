#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace YaraJ.CastleVillageBuilder
{
    /// <summary>
    /// Editor only. Tools > Castle & Village Builder: brushes whole medieval houses onto the land, put together
    /// from the Medieval Village kit's modules (MedievalHouseBuilder), each one different. Brush mode scatters a few at a
    /// time under the brush; One at a time shows the next house where the mouse is, to turn and put down exactly. Houses
    /// keep clear of each other and of water, sit level on a stone footing (so slopes are fine, up to a limit), and can
    /// clear the Paint Land scatter (trees, rocks, grass) from under themselves. Shift (or Erase) rubs them out.
    /// They go under the "Buildings" object in the scene, separate from Paint Land's.
    /// </summary>
    internal sealed partial class CastleVillageBuilderWindow : EditorWindow
    {
        internal const string ContainerName = "Buildings";
        private const string Prefs = "CastleVillageBuilder.";

        private enum Mode { Brush, Line, Single }
        private enum Turning { Any, Quarter, FaceStroke }

        // Settings (kept in EditorPrefs)
        private Mode mode = Mode.Brush;
        private float brush = 20f;
        private int perDab = 2;
        private float gap = 3f;
        private float maxStep = 2f;
        private Turning turning = Turning.Quarter;
        private bool clearScatter = true;
        private bool eraser;
        private readonly MedievalHouseBuilder.Ranges ranges = new MedievalHouseBuilder.Ranges();

        // Nature round the houses: how many clumps of each Paint Land scatter kind (by name), how far out, and whether the
        // way to the front door is kept clear
        private bool decorate = true;
        private float natureSpread = 5f;
        private bool keepDoorClear = true;
        private readonly Dictionary<string, int> natureAmounts = new Dictionary<string, int>();
        private bool scatterChanged;

        // Which tab: houses, or castle walls
        private enum Tab { Houses, Castle, Features, Details }
        private Tab tab;

        // Auto paths: painted between new houses once they're settled (after they're turned to face the mouse, if they are)
        private bool autoPaths = true;
        private readonly BuildingPaths.Style pathStyle = new BuildingPaths.Style();
        private bool pathsPending;

        // Castle walls: the settings, and the wall being drawn (from its last tower, or the point clicked to start it)
        private readonly CastleWallBuilder.Settings castle = new CastleWallBuilder.Settings();
        private static CastleWallBuilder.Settings castleNow = new CastleWallBuilder.Settings(); // (the open window's, for the static helpers)
        private string castleTheme = "";
        private bool chainActive;
        private Vector3 chainPoint;
        private CastlePiece chainTower, chainFirst;
        private Transform chainGroup; // (the section the wall being drawn goes into)
        private bool gateNext;

        // How walls are drawn: tower to tower (clicks), freehand (drag a line), a circle (drag out from its middle), one
        // tower on its own, or gates put into walls (clicked)
        private enum WallDraw { Towers, Freehand, Circle, Tower, Gate }
        private WallDraw wallDraw = WallDraw.Towers;
        private readonly List<Vector3> freehand = new List<Vector3>();
        private bool drawingWall;
        private Vector3 circleMiddle;

        // State
        private bool painting;
        private Vector2 scroll;
        private bool aimed;
        private Vector3 aim, aimNormal;
        private Vector2 lastMouse;
        private Matrix4x4 lastCamera;
        private bool stroking;
        private Vector3 lastDab;
        private int undoGroup;
        private MedievalHouseBuilder.Design next;
        private float nextYaw;
        private string status;
        private int placedThisStroke, removedThisStroke;
        private readonly System.Random random = new System.Random();
        private bool groundIsLand;

        // The houses the last stroke put down: right-click turns their doors to face the mouse, right-click again keeps it
        private readonly List<ScatteredBuilding> lastPlaced = new List<ScatteredBuilding>();
        private readonly List<(ScatteredBuilding house, Vector3 position, Quaternion rotation)> beforeFacing =
            new List<(ScatteredBuilding, Vector3, Quaternion)>();
        private bool facing;
        private int facingGroup;
        private bool eatContextClick;

        // Along a line: where the last house went, how far it reaches along the line, and the next house
        private Vector3 lineFrom;
        private float lineReach;
        private bool lineStarted;
        private MedievalHouseBuilder.Design lineNext;

        [MenuItem("Tools/Castle & Village Builder/Open Castle & Village Builder", priority = 1)]
        public static void Open()
        {
            var window = GetWindow<CastleVillageBuilderWindow>();
            window.titleContent = new GUIContent("Castle & Village Builder", EditorGUIUtility.IconContent("d_PreMatCube").image);
            window.minSize = new Vector2(270f, 400f);
            window.Show();
            if (!MedievalVillageSetup.IsSetUp) MedievalVillageSetup.SetUp();
            window.StartPainting();
        }

        private void OnEnable()
        {
            LoadPrefs();
            castleNow = castle;
            groundIsLand = Ground.Refresh();
            SceneView.duringSceneGui += OnSceneGUI;
            EditorApplication.playModeStateChanged += OnPlayMode;
            Undo.undoRedoPerformed += Repaint;
        }

        private void OnDisable()
        {
            SceneView.duringSceneGui -= OnSceneGUI;
            EditorApplication.playModeStateChanged -= OnPlayMode;
            Undo.undoRedoPerformed -= Repaint;
            StopPainting();
            SavePrefs();
            BuildingThemes themes = AssetDatabase.LoadAssetAtPath<BuildingThemes>(BuildingThemes.AssetPath);
            if (themes != null) AssetDatabase.SaveAssetIfDirty(themes);
        }

        private void OnPlayMode(PlayModeStateChange change)
        {
            if (change == PlayModeStateChange.ExitingEditMode) StopPainting();
        }

        private void StartPainting()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) { ShowNotification(new GUIContent("Not in Play mode")); return; }
            groundIsLand = Ground.Refresh();
            painting = true;
            Tools.hidden = true;
            Roll();
            SceneView.RepaintAll();
        }

        private void StopPainting()
        {
            if (!painting) return;
            if (stroking) EndStroke();
            if (facing) ConfirmFacing();
            FlushPaths();
            EndWallChain();
            painting = false;
            Tools.hidden = false;
            SceneView.RepaintAll();
        }

        // ================================================================ Window

        private void OnGUI()
        {
            Styles();
            float labelWas = EditorGUIUtility.labelWidth;
            EditorGUIUtility.labelWidth = Mathf.Clamp(position.width * 0.4f, 130f, 210f);
            sectionOpen = false;
            scroll = EditorGUILayout.BeginScrollView(scroll);
            try
            {
                DrawTitle();
                if (!MedievalHouseBuilder.KitReady)
                {
                    EditorGUILayout.HelpBox($"The Medieval Village kit isn't in {MedievalVillageSetup.KitFolder} (or hasn't imported yet).", MessageType.Warning);
                    if (GUILayout.Button("Set Up the Kit")) MedievalVillageSetup.SetUp();
                }
                DrawTabs();
                DrawStartStop();
                switch (tab)
                {
                    case Tab.Castle: DrawCastle(); break;
                    case Tab.Features: DrawFeatures(); break;
                    case Tab.Details: DrawPropsTab(); break;
                    default: DrawHousesTab(); break;
                }
                CloseSection();
                GUILayout.Space(8);
            }
            finally
            {
                EditorGUILayout.EndScrollView();
                EditorGUIUtility.labelWidth = labelWas;
            }
        }

        private void DrawHousesTab()
        {
            EditorGUI.BeginChangeCheck();
            mode = (Mode)ToolPicker("Place houses", (int)mode, new[]
            {
                new GUIContent("Brush", "Scatter a few houses at a time under the brush (drag to keep going)."),
                new GUIContent("Along a Line", "Drag a line: houses go down one after another along it. Then right-click and move the mouse to turn their doors to face it; right-click again to keep that."),
                new GUIContent("One at a Time", "See the next house at the mouse: turn it with [ and ] (R: a quarter turn), Space for another, click to put it down.")
            });
            if (EditorGUI.EndChangeCheck()) Changed();
            HowTo(mode == Mode.Brush
                ? "Click or drag to scatter houses. Right-click after: turn them to face the mouse (right-click again to keep). <b>Shift</b>: rub out houses and paths. <b>[ ]</b>: brush size. <b>Esc</b>: stop."
                : mode == Mode.Line
                    ? "1. Drag a line: houses go down along it. 2. Right-click, then move the mouse: their doors turn to face it. 3. Right-click again to keep (Esc puts them back). <b>Shift</b>: rub out."
                    : "Click: put the house down. <b>[ ]</b>: turn. <b>R</b>: quarter turn. <b>Space</b>: another house. <b>Shift</b>: rub out. <b>Esc</b>: stop.");
            if (!groundIsLand && painting) Hint("(No Paint Land land found: houses go on any ground with a collider.)");

            if (Section("Brush", mode == Mode.Brush ? "Brush" : "Placing", true))
            {
                EditorGUI.BeginChangeCheck();
                if (mode == Mode.Brush)
                {
                    SubHeading("Brush");
                    brush = Slider("Brush size", "How far across the brush reaches (m). [ and ] change it.", brush, 5f, 150f);
                    perDab = EditorGUILayout.IntSlider(new GUIContent("Houses per dab", "How many houses each click (and each brush-width of dragging) tries to put down."), perDab, 1, 8);
                    turning = (Turning)EditorGUILayout.EnumPopup(new GUIContent("Facing", "Any way at all, a quarter turn of each other (tidy, like a village), or doors facing the way you drag (along a street)."), turning);
                }
                SubHeading("Spacing & ground");
                gap = Slider("Gap between", "The least room left between houses (m, past their roofs).", gap, 0f, 20f);
                maxStep = Slider("Steepest ground", "How much the ground can rise across a house (m) before it won't go there. Its stone footing hides up to 3 m.", maxStep, 0.25f, 3f);
                SubHeading("Options");
                clearScatter = EditorGUILayout.Toggle(new GUIContent("Clear scatter under", "Rub out Paint Land's scatter (trees, rocks, grass...) under each new house."), clearScatter);
                eraser = EditorGUILayout.Toggle(new GUIContent("Erase (or hold Shift)", "Rub out the houses under the brush instead."), eraser);
                if (EditorGUI.EndChangeCheck()) Changed();
            }

            if (Section("Houses", "Houses", true))
            {
                EditorGUI.BeginChangeCheck();
                SubHeading("Size");
                ranges.Width = IntRange("Width (2 m cells)", "How wide (the roof's span): 2, 3 or 4 cells (4, 6 or 8 m).", ranges.Width, 2, 4);
                ranges.Length = IntRange("Length (2 m cells)", "How long (along the roof's ridge), as far as the kit's roofs go for that width.", ranges.Length, 2, 7);
                ranges.Storeys = IntRange("Storeys", "How many floors.", ranges.Storeys, 1, 3);
                SubHeading("Look");
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.PrefixLabel(new GUIContent("Styles", "Which kinds of house: stone ground floor, timber framed, or plain plaster."));
                ranges.Stone = GUILayout.Toggle(ranges.Stone, "Stone", EditorStyles.miniButtonLeft);
                ranges.Timber = GUILayout.Toggle(ranges.Timber, "Timber", EditorStyles.miniButtonMid);
                ranges.Plaster = GUILayout.Toggle(ranges.Plaster, "Plaster", EditorStyles.miniButtonRight);
                EditorGUILayout.EndHorizontal();
                ranges.WindowChance = Slider("Windows", "How many of the walls have windows.", ranges.WindowChance, 0f, 1f);
                ranges.ShutterChance = Slider("Shutters", "How many of the windows have shutters.", ranges.ShutterChance, 0f, 1f);
                ranges.ChimneyChance = Slider("Chimneys", "How many houses have a chimney.", ranges.ChimneyChance, 0f, 1f);
                SubHeading("Build");
                ranges.Footing = EditorGUILayout.Toggle(new GUIContent("Stone footing", "A stone base under the ground floor, so houses on sloping ground don't float."), ranges.Footing);
                ranges.HideInterior = EditorGUILayout.Toggle(new GUIContent("Hide interior", "No rooms inside: a dark box just behind the walls, so windows and the door look dark rather than into an empty shell (and fewer pieces to draw)."), ranges.HideInterior);
                if (EditorGUI.EndChangeCheck()) { Changed(); Roll(); }
            }

            if (Section("Themes", "Colour Themes", true)) DrawThemes();
            if (Section("Nature", "Nature Around Houses", true)) DrawNature();
            if (Section("Paths", "Auto Paths", true)) DrawPaths();

            if (Section("HouseSections", "Groups", true)) DrawHouseSections();

            if (Section("More", "Scene & Tools", false))
            {
                Transform container = Container(false);
                int count = container != null ? container.GetComponentsInChildren<ScatteredBuilding>(true).Length : 0;
                Hint($"{count} house{(count == 1 ? "" : "s")} in the scene, under \"{ContainerName}\".");
                using (new EditorGUI.DisabledScope(container == null))
                {
                    if (GUILayout.Button("Select Them") && container != null) Selection.activeTransform = container;
                    if (GUILayout.Button("Remove All Houses") && container != null &&
                        EditorUtility.DisplayDialog("Remove all houses?", $"Remove all {count} houses under \"{ContainerName}\"? (Undo brings them back.)", "Remove", "Cancel"))
                    {
                        Undo.DestroyObjectImmediate(container.gameObject);
                    }
                }
                if (GUILayout.Button("Set Up the Kit Again")) MedievalVillageSetup.SetUp();
            }

        }

        private void DrawThemes()
        {
            BuildingThemes all = BuildingThemes.Get();
            Hint("New houses get one of the ticked themes at random. Changing a theme's colours changes every house wearing it.");
            BuildingThemes.Theme remove = null;
            foreach (BuildingThemes.Theme theme in all.themes)
            {
                if (theme == null || theme.stoneOnly) continue;   // (the castle's stone colours: Castle Walls > Stone colour)
                using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                {
                    EditorGUI.BeginChangeCheck();
                    EditorGUILayout.BeginHorizontal();
                    bool on = EditorGUILayout.Toggle(theme.on, GUILayout.Width(16));
                    string name = EditorGUILayout.DelayedTextField(theme.name);
                    bool gone = GUILayout.Button(new GUIContent("✕", "Remove this theme (houses wearing it keep its colours until they're given another)."),
                                                 EditorStyles.miniButton, GUILayout.Width(22));
                    EditorGUILayout.EndHorizontal();
                    Color roof = theme.roof, walls = theme.walls, wood = theme.wood, stone = theme.stone;
                    if (theme.original)
                    {
                        Hint("The kit's own colours.");
                    }
                    else
                    {
                        EditorGUILayout.BeginHorizontal();
                        roof = Swatch("Roof", "The roof tiles.", roof);
                        walls = Swatch("Walls", "The plaster walls.", walls);
                        wood = Swatch("Wood", "The timber: beams, doors, shutters, floors.", wood);
                        stone = Swatch("Stone", "The stone: ground floors, footings, chimneys.", stone);
                        EditorGUILayout.EndHorizontal();
                    }
                    if (gone) remove = theme;
                    if (EditorGUI.EndChangeCheck() && !gone)
                    {
                        Undo.RecordObject(all, "Building Theme");
                        bool recolour = roof != theme.roof || walls != theme.walls || wood != theme.wood || stone != theme.stone;
                        if (name != theme.name && !string.IsNullOrWhiteSpace(name))
                        {
                            RenameTheme(theme.name, name);
                            BuildingThemes.ForgetMaterials(theme.name);
                            theme.name = name;
                            recolour = true;
                        }
                        theme.on = on;
                        theme.roof = roof; theme.walls = walls; theme.wood = wood; theme.stone = stone;
                        EditorUtility.SetDirty(all);
                        if (recolour) all.Refresh(theme);
                    }
                }
            }
            if (remove != null && all.themes.Count > 1)
            {
                Undo.RecordObject(all, "Remove Building Theme");
                all.themes.Remove(remove);
                EditorUtility.SetDirty(all);
            }

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Add Theme"))
            {
                Undo.RecordObject(all, "Add Building Theme");
                var made = new BuildingThemes.Theme
                {
                    name = $"Theme {all.themes.Count + 1}",
                    roof = Color.HSVToRGB(Random.value, 0.45f, 0.55f), walls = Color.HSVToRGB(Random.value, 0.15f, 0.9f),
                    wood = Color.HSVToRGB(Random.Range(0.03f, 0.1f), 0.5f, 0.4f), stone = Color.HSVToRGB(Random.value, 0.08f, 0.6f)
                };
                all.themes.Add(made);
                EditorUtility.SetDirty(all);
                all.Refresh(made);
            }
            List<ScatteredBuilding> selected = SelectedHouses();
            using (new EditorGUI.DisabledScope(selected.Count == 0))
            {
                if (GUILayout.Button(new GUIContent($"Theme Selected ({selected.Count}) ▾", "Give the selected houses a theme.")))
                {
                    var menu = new GenericMenu();
                    menu.AddItem(new GUIContent("Random (from the ticked)"), false, () => ThemeHouses(selected, null));
                    foreach (BuildingThemes.Theme theme in all.themes)
                    {
                        if (theme.stoneOnly) continue;
                        string themeName = theme.name;
                        menu.AddItem(new GUIContent(themeName), false, () => ThemeHouses(selected, themeName));
                    }
                    menu.ShowAsContext();
                }
            }
            EditorGUILayout.EndHorizontal();
        }

        private static Color Swatch(string label, string tooltip, Color colour)
        {
            EditorGUILayout.BeginVertical();
            GUILayout.Label(new GUIContent(label, tooltip), EditorStyles.centeredGreyMiniLabel);
            colour = EditorGUILayout.ColorField(GUIContent.none, colour, false, false, false, GUILayout.MinWidth(40));
            EditorGUILayout.EndVertical();
            return colour;
        }

        private void ThemeHouses(List<ScatteredBuilding> houses, string themeName)
        {
            Undo.IncrementCurrentGroup();
            Undo.SetCurrentGroupName("Theme Houses");
            int group = Undo.GetCurrentGroup();
            foreach (ScatteredBuilding house in houses)
            {
                if (house == null) continue;
                string name = themeName ?? BuildingThemes.Get().Pick(random)?.name;
                BuildingThemes.Apply(house.gameObject, name, true);
            }
            Undo.CollapseUndoOperations(group);
        }

        // Houses wearing a renamed theme go on wearing it (by its new name)
        private static void RenameTheme(string from, string to)
        {
            Transform container = Container(false);
            if (container == null) return;
            foreach (ScatteredBuilding house in container.GetComponentsInChildren<ScatteredBuilding>(true))
            {
                if (house.theme != from) continue;
                house.theme = to;
                BuildingThemes.Apply(house.gameObject, to, true);
            }
        }

        private void DrawNature()
        {
            EditorGUI.BeginChangeCheck();
            decorate = EditorGUILayout.Toggle(new GUIContent("Plant round new houses", "Put plants and rocks round each new house."), decorate);
            using (new EditorGUI.DisabledScope(!decorate))
            {
                List<NatureKind> kinds = Nature.Kinds(groundIsLand);
                if (kinds.Count == 0)
                {
                    Hint(PaintLandLink.Installed
                        ? "No kinds yet: add Paint Land scatter kinds (Land Settings), or your own in Builder Settings."
                        : "No kinds yet: add your plant and rock prefabs in Builder Settings.");
                }
                else
                {
                    Hint("How many clumps of each kind go round each house:");
                    foreach (NatureKind kind in kinds)
                    {
                        int amount = EditorGUILayout.IntSlider(new GUIContent(kind.Name, $"Clumps of {kind.Name} round each house."),
                                                               NatureAmount(kind.Name), 0, 12);
                        natureAmounts[kind.Name] = amount;
                    }
                }
                if (GUILayout.Button(new GUIContent("Edit Builder Settings", "Your own plant and rock prefabs to put round houses (and how mesh paths look).")))
                    Selection.activeObject = BuilderSettings.Get();
                natureSpread = Slider("Spread", "How far out from the roof's edge they go (m).", natureSpread, 0.5f, 15f);
                keepDoorClear = EditorGUILayout.Toggle(new GUIContent("Keep doorway clear", "Nothing on the way up to the front door."), keepDoorClear);
            }
            if (EditorGUI.EndChangeCheck()) Changed();

            List<ScatteredBuilding> selected = SelectedHouses();
            EditorGUILayout.BeginHorizontal();
            using (new EditorGUI.DisabledScope(selected.Count == 0))
            {
                if (GUILayout.Button(new GUIContent($"Replant Selected ({selected.Count})", "Take the plants from round the selected houses and put new ones.")))
                {
                    groundIsLand = Ground.Refresh();
                    Undo.IncrementCurrentGroup();
                    Undo.SetCurrentGroupName("Replant Houses");
                    int group = Undo.GetCurrentGroup();
                    bool was = decorate;
                    decorate = true;
                    foreach (ScatteredBuilding house in selected) { Undecorate(house); Decorate(house); }
                    decorate = was;
                    Undo.CollapseUndoOperations(group);
                    RefreshScatter();
                }
                if (GUILayout.Button(new GUIContent("Clear", "Take the plants from round the selected houses.")))
                {
                    Undo.IncrementCurrentGroup();
                    Undo.SetCurrentGroupName("Clear House Plants");
                    int group = Undo.GetCurrentGroup();
                    foreach (ScatteredBuilding house in selected) Undecorate(house);
                    Undo.CollapseUndoOperations(group);
                    RefreshScatter();
                }
            }
            EditorGUILayout.EndHorizontal();
        }

        private void DrawStartStop()
        {
            Color was = GUI.backgroundColor;
            GUI.backgroundColor = painting ? new Color(0.45f, 0.95f, 0.5f) : new Color(1f, 0.82f, 0.45f);
            string what = tab == Tab.Details ? "Placing Details" : tab == Tab.Features ? "Building Castle Features" : tab == Tab.Castle ? "Painting Castle Walls" : "Placing Houses";
            string label = painting ? $"■  {what}   (click or Esc to stop)" : $"▶  Start {what}";
            var style = new GUIStyle(GUI.skin.button) { fontSize = 13, fontStyle = FontStyle.Bold };
            if (GUILayout.Button(label, style, GUILayout.Height(36)))
            {
                if (painting) StopPainting(); else StartPainting();
            }
            GUI.backgroundColor = was;
            if (!string.IsNullOrEmpty(status) && painting) EditorGUILayout.HelpBox(status, MessageType.None);
        }

        private static void Hint(string text)
        {
            var style = new GUIStyle(EditorStyles.miniLabel) { wordWrap = true };
            Color was = GUI.color;
            GUI.color = new Color(was.r, was.g, was.b, 0.7f);
            GUILayout.Label(text, style);
            GUI.color = was;
        }

        private static float Slider(string label, string tooltip, float value, float min, float max) =>
            EditorGUILayout.Slider(new GUIContent(label, tooltip), value, min, max);

        private static Vector2Int IntRange(string label, string tooltip, Vector2Int range, int min, int max)
        {
            float from = range.x, to = range.y;
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.PrefixLabel(new GUIContent(label, tooltip));
            from = EditorGUILayout.IntField(Mathf.RoundToInt(from), GUILayout.Width(28));
            EditorGUILayout.MinMaxSlider(ref from, ref to, min, max);
            to = EditorGUILayout.IntField(Mathf.RoundToInt(to), GUILayout.Width(28));
            EditorGUILayout.EndHorizontal();
            int a = Mathf.Clamp(Mathf.RoundToInt(from), min, max), b = Mathf.Clamp(Mathf.RoundToInt(to), min, max);
            return new Vector2Int(Mathf.Min(a, b), Mathf.Max(a, b));
        }

        private void Changed()
        {
            SavePrefs();
            SceneView.RepaintAll();
        }

        // ================================================================ Scene view

        private static readonly int ControlHint = "CastleVillageBuilderWindow".GetHashCode();

        private void OnSceneGUI(SceneView view)
        {
            if (!painting) return;
            Event e = Event.current;
            int id = GUIUtility.GetControlID(ControlHint, FocusType.Passive);
            bool erasing = eraser || e.shift;
            if (tab == Tab.Castle)
            {
                CastleSceneGUI(view, e, id, erasing);
                return;
            }
            if (tab == Tab.Features)
            {
                FeaturesSceneGUI(view, e, id, erasing);
                return;
            }
            if (tab == Tab.Details)
            {
                DetailsSceneGUI(view, e, id, erasing);
                return;
            }

            switch (e.type)
            {
                case EventType.Layout:
                    HandleUtility.AddDefaultControl(id);
                    break;

                case EventType.KeyDown:
                    if (e.keyCode == KeyCode.Escape && facing) { CancelFacing(); view.Repaint(); e.Use(); }
                    else if (e.keyCode == KeyCode.Escape) { StopPainting(); Repaint(); e.Use(); }
                    else if (e.keyCode == KeyCode.LeftBracket || e.keyCode == KeyCode.RightBracket)
                    {
                        float sign = e.keyCode == KeyCode.RightBracket ? 1f : -1f;
                        if (mode == Mode.Brush) brush = Mathf.Clamp(brush * (sign > 0f ? 1.25f : 0.8f), 5f, 150f);
                        else nextYaw = Mathf.Repeat(nextYaw + sign * 15f, 360f);
                        Changed(); Repaint(); e.Use();
                    }
                    else if (mode == Mode.Single && e.keyCode == KeyCode.R) { nextYaw = Mathf.Repeat(nextYaw + 90f, 360f); view.Repaint(); e.Use(); }
                    else if (mode == Mode.Single && e.keyCode == KeyCode.Space) { Roll(); view.Repaint(); e.Use(); }
                    break;

                case EventType.MouseMove:
                    Aim(e.mousePosition, view);
                    if (facing && aimed) FaceAt(aim);
                    view.Repaint();
                    break;

                case EventType.ContextClick:
                    if (eatContextClick) { eatContextClick = false; e.Use(); }
                    break;

                case EventType.MouseDown:
                    // Right-click: turn the last stroke's houses to face the mouse, then again to keep it
                    if (e.button == 1 && !e.alt && (facing || HasLastPlaced()))
                    {
                        Aim(e.mousePosition, view);
                        if (facing) ConfirmFacing();
                        else StartFacing();
                        eatContextClick = true;
                        view.Repaint();
                        e.Use();
                        break;
                    }
                    if (e.button != 0 || e.alt) break;
                    if (facing) { ConfirmFacing(); e.Use(); break; }
                    Aim(e.mousePosition, view);
                    if (!aimed) break;
                    BeginStroke();
                    GUIUtility.hotControl = id;
                    if (erasing) RubOut(aim, mode == Mode.Brush ? brush * 0.5f : 1f);
                    else if (mode == Mode.Single) PlaceNext();
                    else if (mode == Mode.Line) StartLine(aim);
                    else Dab(aim, aim);
                    lastDab = aim;
                    e.Use();
                    break;

                case EventType.MouseDrag:
                    if (GUIUtility.hotControl != id) break;
                    Aim(e.mousePosition, view);
                    if (aimed && mode == Mode.Line && !erasing) ContinueLine(aim);
                    else if (aimed && mode == Mode.Brush)
                    {
                        float spacing = Mathf.Max(brush * 0.6f, 6f);
                        if (Vector3.Distance(aim, lastDab) >= spacing)
                        {
                            if (erasing) RubOut(aim, brush * 0.5f);
                            else Dab(aim, lastDab);
                            lastDab = aim;
                        }
                    }
                    else if (aimed && erasing) RubOut(aim, 1f);
                    e.Use();
                    break;

                case EventType.MouseUp:
                    if (GUIUtility.hotControl == id)
                    {
                        GUIUtility.hotControl = 0;
                        if (mode == Mode.Line && !erasing) EndLine();
                        EndStroke();
                        e.Use();
                    }
                    break;

                case EventType.Repaint:
                    Draw(erasing);
                    break;
            }
            if (e.rawType == EventType.MouseUp && stroking) EndStroke();
        }

        private void Aim(Vector2 mouse, SceneView view)
        {
            Matrix4x4 camera = view.camera.worldToCameraMatrix;
            if (mouse == lastMouse && camera == lastCamera && aimed) return;
            lastMouse = mouse;
            lastCamera = camera;
            Ray ray = HandleUtility.GUIPointToWorldRay(mouse);
            aimed = CastGround(ray, out aim, out aimNormal);
        }

        // ---------------------------------------------------------------- Ground

        private bool CastGround(Ray ray, out Vector3 point, out Vector3 normal) => Ground.Raycast(ray, out point, out normal);

        private bool GroundAt(Vector3 world, out float height) => Ground.At(world, out height);

        /// <summary>
        /// For Paint Land's shaping (LandShape's brushes and Rebuild Land), so houses move with the ground: if this is a
        /// house put down by this tool, where the ground it stands on is read. That's its front door, since it's kept level
        /// with the ground there.
        /// </summary>
        internal static bool IsHouse(Transform t, out Vector3 groundSpot)
        {
            groundSpot = default;
            if (t == null || !t.TryGetComponent(out ScatteredBuilding house)) return false;
            var shape = new MedievalHouseBuilder.Design { Width = Mathf.Max(1, house.cells.x), Length = Mathf.Max(1, house.cells.y) };
            groundSpot = t.position + t.rotation * shape.DoorSpot;
            return true;
        }

        // ---------------------------------------------------------------- Putting houses down

        private void Roll()
        {
            next = MedievalHouseBuilder.Roll(ranges, random.Next());
        }

        private void BeginStroke()
        {
            FlushPaths(); // (the last stroke's houses are settled now: their paths go down)
            lastPlaced.Clear();
            strokeGroup = null; // (this stroke's houses get a group of their own)
            stroking = true;
            placedThisStroke = removedThisStroke = 0;
            Undo.IncrementCurrentGroup();
            Undo.SetCurrentGroupName("Castle & Village Builder");
            undoGroup = Undo.GetCurrentGroup();
        }

        private void EndStroke()
        {
            if (!stroking) return;
            stroking = false;
            Undo.CollapseUndoOperations(undoGroup);
            RefreshScatter();
            if (placedThisStroke > 0 || removedThisStroke > 0) Repaint();
            // New houses get their paths once they're settled: straight away one at a time, otherwise when the next
            // stroke starts (or they've been turned to face the mouse, or the tool stops)
            if (placedThisStroke > 0 && autoPaths)
            {
                pathsPending = true;
                if (mode == Mode.Single) FlushPaths();
            }
        }

        private void FlushPaths()
        {
            if (!pathsPending) return;
            pathsPending = false;
            if (!autoPaths || !HasLastPlaced()) return;
            string trouble = BuildingPaths.Connect(lastPlaced, AllHouses(), pathStyle);
            if (trouble != null) status = "Paths: " + trouble;
        }

        private static List<ScatteredBuilding> AllHouses()
        {
            Transform container = Container(false);
            return container != null ? new List<ScatteredBuilding>(container.GetComponentsInChildren<ScatteredBuilding>()) : new List<ScatteredBuilding>();
        }

        private void Dab(Vector3 centre, Vector3 from)
        {
            int placed = 0;
            float radius = brush * 0.5f;
            Vector3 stroke = centre - from;
            stroke.y = 0f;
            for (int attempt = 0; attempt < perDab * 10 && placed < perDab; attempt++)
            {
                Vector2 offset = Random.insideUnitCircle * radius;
                Vector3 spot = centre + new Vector3(offset.x, 0f, offset.y);
                MedievalHouseBuilder.Design design = MedievalHouseBuilder.Roll(ranges, random.Next());
                float yaw = Yaw(stroke);
                if (TryPlace(design, spot, yaw, out _)) placed++;
            }
            status = placed > 0 ? null : "No room here (too steep, in water, off the land, or too near other houses).";
        }

        private float Yaw(Vector3 stroke)
        {
            switch (turning)
            {
                case Turning.Any: return Random.Range(0f, 360f);
                case Turning.FaceStroke:
                    if (stroke.sqrMagnitude > 0.01f)
                    {
                        // The door (the house's +x) faces across the way the brush goes, to one side or the other
                        float along = Mathf.Atan2(stroke.x, stroke.z) * Mathf.Rad2Deg;
                        return along + (Random.value < 0.5f ? 0f : 180f);
                    }
                    return Random.Range(0, 4) * 90f;
                default: return Random.Range(0, 4) * 90f;
            }
        }

        /// <summary>Puts a new random house down at a point (as one stroke: one undo), as the tool's settings say.</summary>
        internal bool PlaceAt(Vector3 spot, float yaw, out string why)
        {
            groundIsLand = Ground.Refresh();
            BeginStroke();
            bool placed = TryPlace(MedievalHouseBuilder.Roll(ranges, random.Next()), spot, yaw, out why);
            EndStroke();
            return placed;
        }

        private void PlaceNext()
        {
            if (TryPlace(next, aim, nextYaw, out string why)) { status = null; Roll(); }
            else status = why;
        }

        // Where a house would go, and why it can't (null if it can)
        private string Check(MedievalHouseBuilder.Design design, Vector3 spot, float yaw, out Vector3 at)
        {
            at = spot;
            Quaternion turn = Quaternion.Euler(0f, yaw, 0f);
            float halfW = design.Width * MedievalHouseBuilder.Cell * 0.5f, halfL = design.Length * MedievalHouseBuilder.Cell * 0.5f;

            // The ground under its walls: the corners, the middles of its sides, its middle and its door
            float low = float.MaxValue, high = float.MinValue;
            foreach (Vector2 p in new[]
                     {
                         new Vector2(-1f, -1f), new Vector2(1f, -1f), new Vector2(-1f, 1f), new Vector2(1f, 1f),
                         new Vector2(0f, -1f), new Vector2(0f, 1f), new Vector2(-1f, 0f), new Vector2(1f, 0f), Vector2.zero
                     })
            {
                Vector3 world = spot + turn * new Vector3(p.x * halfW, 0f, p.y * halfL);
                if (!GroundAt(world, out float h)) return "Off the land.";
                low = Mathf.Min(low, h);
                high = Mathf.Max(high, h);
                if (Ground.UnderWater(new Vector3(world.x, h, world.z), 0.1f)) return "In the water.";
                if (FeatureInTheWay(new Vector3(world.x, h, world.z))) return "In the way of a castle feature.";
            }
            if (high - low > maxStep) return $"Too steep here ({high - low:F1} m across it).";

            // Level with the ground at the door, so it's always walked into straight off the ground
            Vector3 door = spot + turn * design.DoorSpot;
            if (!GroundAt(door, out float doorGround)) doorGround = low;
            at = new Vector3(spot.x, Mathf.Clamp(doorGround, low, high) + 0.04f, spot.z);

            if (Overlaps(design.Footprint, at, yaw)) return "Too near another house.";
            return null;
        }

        private bool TryPlace(MedievalHouseBuilder.Design design, Vector3 spot, float yaw, out string why)
        {
            why = Check(design, spot, yaw, out Vector3 at);
            if (why != null) return false;

            Transform container = StrokeGroup();
            GameObject house = MedievalHouseBuilder.Build(design, null);
            house.transform.SetPositionAndRotation(at, Quaternion.Euler(0f, yaw, 0f));
            house.transform.SetParent(container, true);
            Undo.RegisterCreatedObjectUndo(house, "Castle & Village Builder");
            placedThisStroke++;
            lastPlaced.Add(house.GetComponent<ScatteredBuilding>());
            if (clearScatter) ClearScatterUnder(design.Footprint, at, yaw);
            Decorate(house.GetComponent<ScatteredBuilding>());
            return true;
        }

        // ---------------------------------------------------------------- Along a line

        private void StartLine(Vector3 at)
        {
            lineFrom = at;
            lineReach = 0f;
            lineStarted = false;
            lineNext = MedievalHouseBuilder.Roll(ranges, random.Next());
        }

        // Houses one after another along the drag, each its length along the line (doors to one side), with the gap
        // between them; a house that can't go somewhere is skipped and the line carries on past it
        private void ContinueLine(Vector3 at)
        {
            for (int tries = 0; tries < 24; tries++)
            {
                Vector3 d = at - lineFrom;
                d.y = 0f;
                float distance = d.magnitude;
                if (!lineStarted)
                {
                    if (distance < 1.5f) return; // which way the line goes isn't known yet
                    lineStarted = true;
                    if (TryPlace(lineNext, lineFrom, LineYaw(d), out string why)) lineReach = lineNext.Footprint.y * 0.5f;
                    else status = why;
                    lineNext = MedievalHouseBuilder.Roll(ranges, random.Next());
                    continue;
                }
                float need = lineReach + gap + lineNext.Footprint.y * 0.5f;
                if (distance < need) return;
                Vector3 direction = d / distance;
                Vector3 spot = lineFrom + direction * need;
                if (TryPlace(lineNext, spot, LineYaw(direction), out string reason))
                {
                    lineFrom = spot;
                    lineReach = lineNext.Footprint.y * 0.5f;
                    lineNext = MedievalHouseBuilder.Roll(ranges, random.Next());
                    status = null;
                }
                else
                {
                    lineFrom += direction;
                    lineReach = Mathf.Max(lineReach - 1f, -gap);
                    status = reason;
                }
            }
        }

        private void EndLine()
        {
            // Just a click: one house there
            if (!lineStarted && lineNext != null) TryPlace(lineNext, lineFrom, Yaw(Vector3.zero), out status);
            lineStarted = false;
        }

        // Along the line: the house's length runs the way the line goes, so its door (its +x) faces to one side
        private static float LineYaw(Vector3 direction) => Mathf.Atan2(direction.x, direction.z) * Mathf.Rad2Deg;

        // ---------------------------------------------------------------- Facing the mouse (right-click)

        private bool HasLastPlaced()
        {
            lastPlaced.RemoveAll(h => h == null);
            return lastPlaced.Count > 0;
        }

        private void StartFacing()
        {
            if (!HasLastPlaced()) return;
            Undo.IncrementCurrentGroup();
            Undo.SetCurrentGroupName("Face Houses");
            facingGroup = Undo.GetCurrentGroup();
            facing = true;
            // (Their plants come off while they turn, and go back round them once they're facing the right way)
            foreach (ScatteredBuilding house in lastPlaced) Undecorate(house);
            if (aimed) FaceAt(aim);
        }

        // Each house turned on the spot so its front door looks at 'target', and set level with the ground at its door
        private void FaceAt(Vector3 target)
        {
            foreach (ScatteredBuilding house in lastPlaced)
            {
                if (house == null) continue;
                Vector3 d = target - house.transform.position;
                d.y = 0f;
                if (d.sqrMagnitude < 0.25f) continue;
                float yaw = Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg - 90f; // the door is the house's +x
                Undo.RecordObject(house.transform, "Face Houses");
                house.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
                var shape = new MedievalHouseBuilder.Design { Width = house.cells.x, Length = house.cells.y };
                Vector3 door = house.transform.position + house.transform.rotation * shape.DoorSpot;
                if (GroundAt(door, out float ground))
                {
                    Vector3 p = house.transform.position;
                    // (Not so far that the footing shows a gap, or the walls sink into the ground)
                    if (GroundAt(p, out float middle)) ground = Mathf.Clamp(ground, middle - maxStep, middle + maxStep);
                    house.transform.position = new Vector3(p.x, ground + 0.04f, p.z);
                }
            }
        }

        private void ConfirmFacing()
        {
            if (!facing) return;
            facing = false;
            if (clearScatter)
            {
                foreach (ScatteredBuilding house in lastPlaced)
                {
                    if (house != null) ClearScatterUnder(house.footprint, house.transform.position, house.transform.eulerAngles.y);
                }
            }
            foreach (ScatteredBuilding house in lastPlaced) Decorate(house);
            RefreshScatter();
            Undo.CollapseUndoOperations(facingGroup);
            status = null;
            FlushPaths(); // (now they face the right way, their paths can go to their doors)
            lastPlaced.Clear();
        }

        private void CancelFacing()
        {
            if (!facing) return;
            facing = false;
            Undo.RevertAllDownToGroup(facingGroup);
        }

        // ---------------------------------------------------------------- Nature round the houses

        // The default clumps of each of Paint Land's kinds round a house (by name; others: none)
        private static int DefaultAmount(string kind)
        {
            string k = kind.ToLowerInvariant();
            if (k.Contains("flower")) return 4;
            if (k.Contains("bush")) return 3;
            if (k.Contains("rock")) return 2;
            if (k.Contains("tree")) return 1;
            if (k.Contains("mushroom") || k.Contains("stump") || k.Contains("log")) return 1;
            if (k.Contains("crate") || k.Contains("brick")) return 1;
            return 0;
        }

        private int NatureAmount(string kind) =>
            natureAmounts.TryGetValue(kind, out int amount) ? amount : (natureAmounts[kind] = EditorPrefs.GetInt(Prefs + "Nature." + kind, DefaultAmount(kind)));

        /// <summary>Puts plants and rocks (Paint Land's scatter kinds, and the builder's own) round a house, outside its roof and off its doorstep.</summary>
        private void Decorate(ScatteredBuilding house)
        {
            if (!decorate || house == null) return;
            List<NatureKind> kinds = Nature.Kinds(groundIsLand);
            if (kinds.Count == 0) return;

            Undo.RecordObject(house, "Castle & Village Builder");
            house.decorations.RemoveAll(d => d == null);
            Vector2 half = house.footprint * 0.5f;
            foreach (NatureKind kind in kinds)
            {
                int amount = NatureAmount(kind.Name);
                float clearance = Clearance(kind);
                for (int n = 0; n < amount; n++)
                {
                    for (int attempt = 0; attempt < 14; attempt++)
                    {
                        // A point round the edge of its roof, pushed out a way (big things, like trees, further out)
                        Vector2 local = AroundRect(half, clearance + Random.value * natureSpread);
                        if (keepDoorClear && InDoorway(house, local)) continue;
                        Vector3 world = house.transform.TransformPoint(new Vector3(local.x, 0f, local.y));
                        if (!Ground.At(world, out Vector3 ground, out Vector3 normal)) continue;
                        if (Vector3.Angle(normal, Vector3.up) > kind.MaxSlope) continue;
                        if (Ground.UnderWater(ground, 0.1f)) continue;
                        if (InsideAnyHouse(ground, clearance * 0.5f)) continue;

                        foreach (GameObject thing in Nature.PlaceClump(kind, ground, normal))
                        {
                            Vector3 p = thing.transform.position;
                            Vector3 there = house.transform.InverseTransformPoint(p);
                            // (The rest of a clump can wander: not into a house or onto the doorstep)
                            if (InsideAnyHouse(p, 0.25f) || (keepDoorClear && InDoorway(house, new Vector2(there.x, there.z))))
                            {
                                Undo.DestroyObjectImmediate(thing);
                                continue;
                            }
                            house.decorations.Add(thing);
                        }
                        scatterChanged = true;
                        break;
                    }
                }
            }
        }

        private void Undecorate(ScatteredBuilding house)
        {
            if (house == null || house.decorations == null || house.decorations.Count == 0) return;
            Undo.RecordObject(house, "Castle & Village Builder");
            foreach (GameObject thing in house.decorations)
            {
                if (thing != null) Undo.DestroyObjectImmediate(thing);
            }
            house.decorations.Clear();
            scatterChanged = true;
        }

        // How far past the roof's edge a kind starts: about half the width of its biggest prefab
        private static float Clearance(NatureKind kind)
        {
            float widest = 0f;
            foreach (GameObject prefab in kind.Prefabs)
            {
                if (prefab == null) continue;
                foreach (MeshFilter filter in prefab.GetComponentsInChildren<MeshFilter>(true))
                {
                    if (filter.sharedMesh == null) continue;
                    Vector3 size = Vector3.Scale(filter.sharedMesh.bounds.size, filter.transform.lossyScale);
                    widest = Mathf.Max(widest, Mathf.Max(size.x, size.z));
                }
            }
            float biggest = Mathf.Max(kind.Size.x, kind.Size.y, 0.1f);
            return Mathf.Clamp(widest * biggest * 0.45f, 0.2f, 5f);
        }

        // A point 'away' metres out from a rectangle's edge (its half size), anywhere round it
        private static Vector2 AroundRect(Vector2 half, float away)
        {
            float w = half.x * 2f, l = half.y * 2f;
            float t = Random.value * 2f * (w + l);
            if (t < w) return new Vector2(-half.x + t, half.y + away);
            t -= w;
            if (t < w) return new Vector2(-half.x + t, -half.y - away);
            t -= w;
            if (t < l) return new Vector2(half.x + away, -half.y + t);
            t -= l;
            return new Vector2(-half.x - away, -half.y + t);
        }

        // The way up to the front door (its +x side): a strip out from the door
        private static bool InDoorway(ScatteredBuilding house, Vector2 local)
        {
            var shape = new MedievalHouseBuilder.Design { Width = Mathf.Max(1, house.cells.x), Length = Mathf.Max(1, house.cells.y) };
            Vector3 door = shape.DoorSpot;
            return local.x > door.x - 0.5f && local.x < door.x + 6f && Mathf.Abs(local.y - door.z) < 1.8f;
        }

        private static bool InsideAnyHouse(Vector3 point, float margin)
        {
            Transform container = Container(false);
            if (container != null)
            {
                foreach (ScatteredBuilding house in container.GetComponentsInChildren<ScatteredBuilding>())
                {
                    if (Inside(point, house.transform.position, house.transform.eulerAngles.y, house.footprint * 0.5f + Vector2.one * margin)) return true;
                }
            }
            return CastlePieceAt(point, margin) != null;
        }

        // Land Scatter's renderer re-reads what's under it (after plants are put down or taken away)
        private void RefreshScatter()
        {
            if (!scatterChanged) return;
            scatterChanged = false;
            PaintLandLink.RefreshScatter();
        }

        private static List<ScatteredBuilding> SelectedHouses()
        {
            var houses = new List<ScatteredBuilding>();
            foreach (GameObject go in Selection.gameObjects)
            {
                ScatteredBuilding house = go.GetComponentInParent<ScatteredBuilding>();
                if (house != null && !houses.Contains(house)) houses.Add(house);
                foreach (ScatteredBuilding inside in go.GetComponentsInChildren<ScatteredBuilding>())
                {
                    if (!houses.Contains(inside)) houses.Add(inside);
                }
            }
            return houses;
        }

        // ---------------------------------------------------------------- Castle walls

        // Tower to tower with watch towers off: the wall being drawn is one piece along all the points clicked so far
        private readonly List<Vector3> chainPath = new List<Vector3>();
        private CastlePiece chainRun, chainStartTower;

        private void CastleSceneGUI(SceneView view, Event e, int id, bool erasing)
        {
            switch (e.type)
            {
                case EventType.Layout:
                    HandleUtility.AddDefaultControl(id);
                    break;

                case EventType.KeyDown:
                    if (e.keyCode == KeyCode.Escape && (chainActive || drawingWall)) { EndWallChain(); view.Repaint(); e.Use(); }
                    else if (e.keyCode == KeyCode.Escape) { StopPainting(); Repaint(); e.Use(); }
                    else if (e.keyCode == KeyCode.Return || e.keyCode == KeyCode.KeypadEnter) { EndWallChain(); view.Repaint(); e.Use(); }
                    else if (e.keyCode == KeyCode.G) { gateNext = !gateNext; Repaint(); view.Repaint(); e.Use(); }
                    break;

                case EventType.MouseMove:
                    Aim(e.mousePosition, view);
                    view.Repaint();
                    break;

                case EventType.ContextClick:
                    if (eatContextClick) { eatContextClick = false; e.Use(); }
                    break;

                case EventType.MouseDown:
                    if (e.button == 1 && !e.alt && chainActive)
                    {
                        EndWallChain();
                        eatContextClick = true;
                        view.Repaint();
                        e.Use();
                        break;
                    }
                    if (e.button != 0 || e.alt) break;
                    Aim(e.mousePosition, view);
                    if (!aimed) break;
                    GUIUtility.hotControl = id;
                    if (erasing) RubOutCastle(aim);
                    else
                    {
                        switch (wallDraw)
                        {
                            case WallDraw.Towers: CastleClick(aim); break;
                            case WallDraw.Freehand:
                                freehand.Clear();
                                freehand.Add(aim);
                                drawingWall = true;
                                break;
                            case WallDraw.Circle:
                                circleMiddle = aim;
                                drawingWall = true;
                                break;
                            case WallDraw.Tower: LoneTower(aim); break;
                            case WallDraw.Gate: PaintGate(aim); break;
                        }
                    }
                    e.Use();
                    break;

                case EventType.MouseDrag:
                    if (GUIUtility.hotControl != id) break;
                    Aim(e.mousePosition, view);
                    if (drawingWall && wallDraw == WallDraw.Freehand && aimed && Flat(aim - freehand[freehand.Count - 1]).magnitude >= 1f) freehand.Add(aim);
                    view.Repaint();
                    e.Use();
                    break;

                case EventType.MouseUp:
                    if (GUIUtility.hotControl == id)
                    {
                        GUIUtility.hotControl = 0;
                        if (drawingWall)
                        {
                            drawingWall = false;
                            if (wallDraw == WallDraw.Freehand) FinishFreehand();
                            else if (wallDraw == WallDraw.Circle && aimed) FinishCircle(circleMiddle, Flat(aim - circleMiddle).magnitude);
                        }
                        e.Use();
                    }
                    break;

                case EventType.Repaint:
                    DrawCastlePreview(erasing);
                    break;
            }
        }

        private static Vector3 Flat(Vector3 v) => new Vector3(v.x, 0f, v.z);

        private float? CastleGround(Vector3 world) => GroundAt(world, out float h) ? h : (float?)null;

        private void PrepareCastle()
        {
            groundIsLand = Ground.Refresh();
            castle.Theme = castleTheme;
        }

        // How far a wall's end is cut back into a tower: to where its faces meet the tower (round towers curve away, so a
        // thick wall meets one nearer its middle than its line does), a little into it so there's no gap
        private static float Inset(CastlePiece tower)
        {
            if (tower == null) return 0f;
            float r = tower.Radius;
            if (!tower.round) return Mathf.Max(0f, r - 0.3f);
            float half = Mathf.Min(CastleWallBuilder.WallThickness(castleNow) * 0.5f, r * 0.95f);
            return Mathf.Max(0f, Mathf.Sqrt(r * r - half * half) - 0.3f);
        }

        // Tower to tower: the first click starts a wall (at a tower, or a new one); each after builds the wall to there,
        // with a watch tower at its end (and more along it if it's long); clicking the first one closes the ring. With
        // watch towers off, the clicks are the bends of one smooth wall.
        private void CastleClick(Vector3 point)
        {
            PrepareCastle();
            if (!castle.WatchTowers)
            {
                PathClick(point);
                return;
            }
            CastlePiece snap = TowerNear(point);
            Vector3 target = snap != null ? snap.transform.position : point;
            if (!chainActive)
            {
                chainActive = true;
                chainPoint = target;
                chainTower = chainFirst = snap;
                status = null;
                return;
            }

            Vector3 from = chainTower != null ? chainTower.transform.position : chainPoint;
            float distance = Flat(target - from).magnitude;
            float fromHalf = chainTower != null ? chainTower.Radius : CastleWallBuilder.TowerHalf(castle);
            float toHalf = snap != null ? snap.Radius : CastleWallBuilder.TowerHalf(castle);
            if (distance < fromHalf + toHalf + 2f)
            {
                status = "Too close to the last tower for a wall between them.";
                return;
            }

            var points = new List<Vector3> { from };
            int spans = castle.TowerEvery > 0f ? Mathf.Max(1, Mathf.CeilToInt(distance / castle.TowerEvery)) : 1;
            for (int i = 1; i <= spans; i++) points.Add(Vector3.Lerp(from, target, i / (float)spans));
            var towers = new List<bool>();
            for (int i = 0; i < points.Count; i++) towers.Add(true);

            Undo.IncrementCurrentGroup();
            Undo.SetCurrentGroupName("Castle Wall");
            int group = Undo.GetCurrentGroup();
            if (chainGroup == null) chainGroup = NewSection("Wall");
            List<CastlePiece> made = BuildChain(points, towers, false, chainTower, snap, chainGroup);
            if (chainFirst == null && made.Count > 0) chainFirst = made[0];
            if (made.Count > 0) chainTower = made[made.Count - 1];
            chainPoint = target;
            Undo.CollapseUndoOperations(group);
            RefreshScatter();
            status = null;
            if (snap != null && snap == chainFirst) EndWallChain(); // (the ring is closed)
            Repaint();
        }

        // Watch towers off: each click a bend of one wall, made again each time (one smooth piece); clicking its start
        // closes it round, clicking a tower ends it there
        private void PathClick(Vector3 point)
        {
            CastlePiece snap = TowerNear(point);
            Vector3 target = snap != null ? snap.transform.position : point;
            if (!chainActive)
            {
                chainActive = true;
                chainPath.Clear();
                chainPath.Add(target);
                chainStartTower = snap;
                chainRun = null;
                status = null;
                return;
            }
            bool close = chainStartTower == null && chainPath.Count >= 3 && Flat(point - chainPath[0]).magnitude < Mathf.Max(3f, CastleWallBuilder.WallThickness(castle));
            if (!close)
            {
                if (Flat(target - chainPath[chainPath.Count - 1]).magnitude < 1.5f) return;
                chainPath.Add(target);
            }

            Undo.IncrementCurrentGroup();
            Undo.SetCurrentGroupName("Castle Wall");
            int group = Undo.GetCurrentGroup();
            if (chainGroup == null) chainGroup = NewSection(close ? "Wall Ring" : "Wall");
            var gates = chainRun != null ? new List<float>(chainRun.gates) : new List<float>();
            if (chainRun != null) Undo.DestroyObjectImmediate(chainRun.gameObject);
            if (gateNext)
            {
                float total = CastleWallBuilder.Length(chainPath, close);
                gates.Add(close ? total * 0.5f : (Inset(chainStartTower) + total - Inset(snap)) * 0.5f);
                gateNext = false;
            }
            chainRun = MakeRun(new List<Vector3>(chainPath), Inset(chainStartTower), Inset(snap), close, gates, chainGroup);
            Undo.CollapseUndoOperations(group);
            RefreshScatter();
            if (close || snap != null) EndWallChain();
            Repaint();
        }

        // Freehand: the line drawn, smoothed, with watch towers along it (and at its ends); if it ends where it started,
        // it closes into a ring
        private void FinishFreehand()
        {
            if (freehand.Count < 2) return;
            PrepareCastle();
            var smooth = new List<Vector3>(freehand);
            for (int pass = 0; pass < 2; pass++) smooth = Chaikin(smooth);
            float length = 0f;
            for (int i = 1; i < smooth.Count; i++) length += Flat(smooth[i] - smooth[i - 1]).magnitude;
            if (length < 6f) { status = "Draw a longer wall."; return; }
            bool closed = length > 24f && Flat(smooth[0] - smooth[smooth.Count - 1]).magnitude < Mathf.Max(castle.Bend, 6f);

            int stretches = Mathf.Max(2, Mathf.RoundToInt(length / Mathf.Max(1f, castle.Bend * 0.5f)));
            var points = new List<Vector3>();
            for (int i = 0; i <= stretches; i++) points.Add(Along(smooth, length * i / stretches));
            if (closed) points.RemoveAt(points.Count - 1); // (the ring goes back round to the first)
            BuildDrawn(points, closed, length, closed ? "Wall Ring" : "Wall");
        }

        // A circle: points round it close enough to look round, watch towers spaced among them
        private void FinishCircle(Vector3 middle, float radius)
        {
            if (radius < 6f) { status = "Drag out further for a circle."; return; }
            PrepareCastle();
            float round = 2f * Mathf.PI * radius;
            int count = CirclePoints(round);
            var points = new List<Vector3>();
            for (int i = 0; i < count; i++)
            {
                float a = i * Mathf.PI * 2f / count;
                points.Add(middle + new Vector3(Mathf.Sin(a), 0f, Mathf.Cos(a)) * radius);
            }
            BuildDrawn(points, true, round, "Round Wall");
        }

        // How many points round a circle: about every half 'Bend' (smooth), a whole number between each pair of towers
        private int CirclePoints(float round)
        {
            int count = Mathf.Max(16, Mathf.RoundToInt(round / Mathf.Max(1f, castle.Bend * 0.5f)));
            if (castle.WatchTowers && castle.TowerEvery > 0f)
            {
                int towers = Mathf.Max(3, Mathf.RoundToInt(round / castle.TowerEvery));
                count = towers * Mathf.Max(1, Mathf.RoundToInt(count / (float)towers));
            }
            return count;
        }

        // A drawn wall (freehand or a circle): which of its points get watch towers, then built (one undo)
        private void BuildDrawn(List<Vector3> points, bool closed, float length, string kind)
        {
            // Watch towers: at the ends (of a wall that doesn't go round), then about every 'TowerEvery' along it, but never
            // so close to another that they'd stand side by side
            var towers = new List<bool>();
            for (int i = 0; i < points.Count; i++) towers.Add(false);
            if (castle.WatchTowers)
            {
                float apart = CastleWallBuilder.TowerHalf(castle) * 2f + 6f;
                float every = castle.TowerEvery > 0f ? Mathf.Max(castle.TowerEvery, apart) : float.MaxValue;
                var distance = new List<float> { 0f };
                for (int i = 1; i < points.Count; i++) distance.Add(distance[i - 1] + Flat(points[i] - points[i - 1]).magnitude);
                float total = closed ? distance[distance.Count - 1] + Flat(points[0] - points[points.Count - 1]).magnitude : distance[distance.Count - 1];
                towers[0] = true;
                float lastTower = 0f;
                for (int i = 1; i < points.Count; i++)
                {
                    bool end = !closed && i == points.Count - 1;
                    if (end) { towers[i] = true; continue; }
                    // (far enough from the last, and from the end ahead: the first again, round a ring)
                    if (distance[i] - lastTower >= every && total - distance[i] >= apart) { towers[i] = true; lastTower = distance[i]; }
                }
                // (an open wall's last tower before its end: not crowding the end one)
                if (!closed)
                {
                    for (int i = points.Count - 2; i > 0; i--)
                    {
                        if (!towers[i]) continue;
                        if (total - distance[i] < apart) towers[i] = false;
                        break;
                    }
                }
            }
            Undo.IncrementCurrentGroup();
            Undo.SetCurrentGroupName("Castle Wall");
            int group = Undo.GetCurrentGroup();
            // (ending at a tower already standing, or starting from one: joined onto it, not a new one beside it)
            CastlePiece first = castle.WatchTowers ? TowerNear(points[0]) : null;
            CastlePiece last = castle.WatchTowers && !closed ? TowerNear(points[points.Count - 1]) : null;
            if (first != null) points[0] = first.transform.position;
            if (last != null && last != first) points[points.Count - 1] = last.transform.position;
            else last = null;
            BuildChain(points, towers, closed, first, last, NewSection(kind));
            Undo.CollapseUndoOperations(group);
            RefreshScatter();
            status = null;
            Repaint();
        }

        /// <summary>
        /// Builds a wall through points: a watch tower at those 'towers' says (the first and last can be ones already
        /// standing), and between them one smooth wall each, round to the start if it's closed. A gate (if asked) goes in
        /// the middle of the wall nearest the middle. Returns the towers, in order.
        /// </summary>
        private List<CastlePiece> BuildChain(List<Vector3> points, List<bool> towers, bool closed, CastlePiece first, CastlePiece last, Transform section)
        {
            int count = points.Count;
            var at = new CastlePiece[count];
            var made = new List<CastlePiece>();
            for (int i = 0; i < count; i++)
            {
                if (i == 0 && first != null) { at[i] = first; made.Add(first); continue; }
                if (i == count - 1 && last != null) { at[i] = last; made.Add(last); continue; }
                if (!towers[i]) continue;
                // (turned along the wall: the way from the point before to the point after)
                Vector3 before = i > 0 ? points[i - 1] : closed ? points[count - 1] : points[i];
                Vector3 after = i < count - 1 ? points[i + 1] : closed ? points[0] : points[i];
                Vector3 along = Flat(after - before);
                float yaw = along.sqrMagnitude > 0.0001f ? Mathf.Atan2(along.x, along.z) * Mathf.Rad2Deg : 0f;
                at[i] = MakeTower(points[i], yaw, section);
                made.Add(at[i]);
            }

            // The walls: from each tower to the next (or the ends), each one smooth piece along the points between
            var runs = new List<(int start, int end)>();
            var stops = new List<int>();
            for (int i = 0; i < count; i++) if (at[i] != null) stops.Add(i);
            if (closed)
            {
                if (stops.Count == 0) runs.Add((0, -1)); // (all the way round, no towers)
                else for (int k = 0; k < stops.Count; k++) runs.Add((stops[k], stops[(k + 1) % stops.Count] + (k == stops.Count - 1 ? count : 0)));
            }
            else
            {
                int start = 0;
                foreach (int stop in stops)
                {
                    if (stop > start) runs.Add((start, stop));
                    start = stop;
                }
                if (start < count - 1) runs.Add((start, count - 1));
            }
            Vector3 middleOfAll = Vector3.zero;
            foreach (Vector3 p in points) middleOfAll += p;
            middleOfAll /= Mathf.Max(1, count);
            int gateRun = gateNext ? runs.Count / 2 : -1;
            for (int r = 0; r < runs.Count; r++)
            {
                (int a, int b) = runs[r];
                var path = new List<Vector3>();
                bool ring = b < 0;
                if (ring) path.AddRange(points);
                else for (int i = a; i <= b; i++) path.Add(points[i % count]);
                CastlePiece fromTower = ring ? null : at[a % count], toTower = ring ? null : at[b % count];
                float startInset = Inset(fromTower), endInset = Inset(toTower);
                List<float> gates = null;
                if (r == gateRun)
                {
                    float total = CastleWallBuilder.Length(path, ring);
                    gates = new List<float> { ring ? total * 0.5f : (startInset + total - endInset) * 0.5f };
                }
                MakeRun(path, startInset, endInset, ring, gates, section, middleOfAll);
            }
            gateNext = false;
            return made;
        }

        private static List<Vector3> Chaikin(List<Vector3> points)
        {
            if (points.Count < 3) return points;
            var smooth = new List<Vector3> { points[0] };
            for (int i = 0; i < points.Count - 1; i++)
            {
                smooth.Add(Vector3.Lerp(points[i], points[i + 1], 0.25f));
                smooth.Add(Vector3.Lerp(points[i], points[i + 1], 0.75f));
            }
            smooth.Add(points[points.Count - 1]);
            return smooth;
        }

        // The point 'distance' along a line of points (from above)
        private static Vector3 Along(List<Vector3> line, float distance)
        {
            for (int i = 1; i < line.Count; i++)
            {
                float d = Flat(line[i] - line[i - 1]).magnitude;
                if (distance <= d || i == line.Count - 1) return Vector3.Lerp(line[i - 1], line[i], d > 0f ? Mathf.Clamp01(distance / d) : 0f);
                distance -= d;
            }
            return line[line.Count - 1];
        }

        private void LoneTower(Vector3 at)
        {
            PrepareCastle();
            Undo.IncrementCurrentGroup();
            Undo.SetCurrentGroupName("Watch Tower");
            int group = Undo.GetCurrentGroup();
            MakeTower(at, 0f, NewSection("Watch Tower"));
            Undo.CollapseUndoOperations(group);
            RefreshScatter();
        }

        // Gate: a click on a wall cuts an arched gateway through it there (or walls one clicked back up)
        private void PaintGate(Vector3 point)
        {
            CastlePiece wall = WallAt(point);
            if (wall == null) { status = "Click on a wall to put a gateway there (or on one to wall it up)."; return; }
            PrepareCastle();
            List<Vector3> path = wall.path;
            bool closed = wall.closed;
            float startInset = wall.fromHalf, endInset = wall.toHalf;
            if (path == null || path.Count < 2)
            {
                // (a wall from before its line was kept: its two ends, cut back into the towers there)
                path = new List<Vector3> { wall.from, wall.to };
                startInset = wall.fromHalf - 0.3f;
                endInset = wall.toHalf - 0.3f;
                closed = false;
            }
            float centre = CastleWallBuilder.DistanceAlong(path, closed, point, out _);
            var gates = new List<float>(wall.gates ?? new List<float>());
            int hit = gates.FindIndex(g => Mathf.Abs(g - centre) < castle.GateWidth * 0.5f + 0.5f);
            if (hit >= 0) gates.RemoveAt(hit);
            else gates.Add(centre);

            Undo.IncrementCurrentGroup();
            Undo.SetCurrentGroupName(hit >= 0 ? "Wall Up Gateway" : "Castle Gateway");
            int group = Undo.GetCurrentGroup();
            Transform parent = wall.transform.parent;
            int index = wall.transform.GetSiblingIndex();
            if (wall == chainRun) chainRun = null;
            Undo.DestroyObjectImmediate(wall.gameObject);
            CastlePiece made = MakeRun(path, startInset, endInset, closed, gates, parent, null, wall.inner != 0 ? wall.inner : 1);
            if (made != null)
            {
                made.transform.SetSiblingIndex(index);
                status = hit < 0 && made.gates.Count < gates.Count ? "No room for a gateway there." : null;
            }
            Undo.CollapseUndoOperations(group);
        }

        private CastlePiece MakeTower(Vector3 at, float yaw, Transform section)
        {
            GameObject tower = CastleWallBuilder.Tower(at, yaw, castle, CastleGround, section);
            Undo.RegisterCreatedObjectUndo(tower, "Castle Wall");
            var piece = tower.GetComponent<CastlePiece>();
            if (clearScatter) ClearScatterUnder(new Vector2(piece.width, piece.width), tower.transform.position, yaw);
            return piece;
        }

        private CastlePiece MakeRun(List<Vector3> path, float startInset, float endInset, bool closed, List<float> gates, Transform section, Vector3? inside = null, int innerSide = 0)
        {
            GameObject wall = CastleWallBuilder.Run(path, startInset, endInset, closed, gates, castle, CastleGround, section, inside, innerSide);
            if (wall == null) return null;
            Undo.RegisterCreatedObjectUndo(wall, "Castle Wall");
            var piece = wall.GetComponent<CastlePiece>();
            if (clearScatter)
            {
                // (along its line, a stretch at a time)
                float total = CastleWallBuilder.Length(path, closed);
                for (float d = 0f; d < total; d += 2f)
                {
                    Vector3 p = CastleWallBuilder.PointAlong(path, closed, d + 1f, out Vector3 along);
                    ClearScatterUnder(new Vector2(2.4f, piece.width + 1f), p, Mathf.Atan2(along.x, along.z) * Mathf.Rad2Deg);
                }
            }
            ClearGateGrass(piece);
            return piece;
        }

        // No grass growing up through a gateway: the grass field keeps off each gateway's way through (exactly, whatever
        // the grass mask's size) and is redone round it
        private void ClearGateGrass(CastlePiece wall)
        {
            if (wall != null) wall.GrassChanged();
        }

        // Every gateway already built: the grass round them redone without it
        internal int ClearAllGateGrass()
        {
            Transform container = CastleContainer(false);
            if (container == null) return 0;
            int cleared = 0;
            foreach (CastlePiece wall in container.GetComponentsInChildren<CastlePiece>())
            {
                if (wall.tower || wall.joint || wall.gates == null || wall.gates.Count == 0) continue;
                wall.GrassChanged();
                cleared += wall.gates.Count;
            }
            return cleared;
        }

        private void EndWallChain()
        {
            chainActive = false;
            chainTower = chainFirst = null;
            chainGroup = null;
            chainRun = chainStartTower = null;
            chainPath.Clear();
            drawingWall = false;
            freehand.Clear();
        }

        // A tower (or an older bend) near a point, to join a wall onto, or none
        private static CastlePiece TowerNear(Vector3 point)
        {
            Transform container = CastleContainer(false);
            if (container == null) return null;
            CastlePiece best = null;
            float nearest = float.MaxValue;
            foreach (CastlePiece piece in container.GetComponentsInChildren<CastlePiece>())
            {
                if (!piece.tower && !piece.joint) continue;
                float d = Flat(point - piece.transform.position).magnitude;
                // (near enough that a new tower here would crowd it: join onto it instead)
                float reach = piece.Radius + Mathf.Max(2f, CastleWallBuilder.TowerHalf(castleNow) + 1.5f);
                if (d <= reach && d < nearest) { nearest = d; best = piece; }
            }
            return best;
        }

        // How far a point is from a wall (from its line, less half its thickness; 0 or less: on it)
        private static float FromWall(CastlePiece wall, Vector3 point)
        {
            if (wall.path != null && wall.path.Count >= 2)
            {
                CastleWallBuilder.DistanceAlong(wall.path, wall.closed, point, out float away);
                return away - wall.width * 0.5f;
            }
            Vector3 local = Quaternion.Euler(0f, -wall.transform.eulerAngles.y, 0f) * (point - wall.transform.position);
            float dx = Mathf.Max(Mathf.Abs(local.x) - wall.footprint.x * 0.5f, 0f), dz = Mathf.Max(Mathf.Abs(local.z) - wall.footprint.y * 0.5f, 0f);
            return Mathf.Sqrt(dx * dx + dz * dz);
        }

        private static CastlePiece WallAt(Vector3 point)
        {
            Transform container = CastleContainer(false);
            if (container == null) return null;
            CastlePiece best = null;
            float nearest = 0.6f;
            foreach (CastlePiece piece in container.GetComponentsInChildren<CastlePiece>())
            {
                if (piece.tower || piece.joint) continue;
                float d = FromWall(piece, point);
                if (d < nearest) { nearest = d; best = piece; }
            }
            return best;
        }

        // The castle piece at a point: a tower (or bend) before the wall running into it
        private static CastlePiece CastlePieceAt(Vector3 point, float margin)
        {
            Transform container = CastleContainer(false);
            if (container == null) return null;
            CastlePiece wall = null;
            foreach (CastlePiece piece in container.GetComponentsInChildren<CastlePiece>())
            {
                if (piece.tower || piece.joint)
                {
                    if (Flat(point - piece.transform.position).magnitude <= piece.Radius + margin) return piece;
                }
                else if (wall == null && FromWall(piece, point) <= margin) wall = piece;
            }
            return wall;
        }

        // Is a house's footprint (turned 'yaw', 'half' its size, plus 'margin') on any castle wall or tower?
        private static bool OnCastle(Vector3 at, float yaw, Vector2 half, float margin)
        {
            Transform container = CastleContainer(false);
            if (container == null) return false;
            float reach = half.magnitude + margin;
            foreach (CastlePiece piece in container.GetComponentsInChildren<CastlePiece>())
            {
                if (piece.tower || piece.joint)
                {
                    if (RectsTouch(at, yaw, half + Vector2.one * margin, piece.transform.position, 0f, Vector2.one * piece.Radius)) return true;
                    continue;
                }
                if (piece.path == null || piece.path.Count < 2)
                {
                    if (RectsTouch(at, yaw, half + Vector2.one * margin, piece.transform.position, piece.transform.eulerAngles.y, piece.footprint * 0.5f)) return true;
                    continue;
                }
                // (its line, a metre at a time: any bit of it in the footprint)
                float total = CastleWallBuilder.Length(piece.path, piece.closed);
                if (FromWall(piece, at) > reach) continue;
                for (float d = 0f; d <= total; d += 1f)
                {
                    Vector3 p = CastleWallBuilder.PointAlong(piece.path, piece.closed, d, out _);
                    if (Inside(p, at, yaw, half + Vector2.one * (margin + piece.width * 0.5f))) return true;
                }
            }
            return false;
        }

        private void RubOutCastle(Vector3 point)
        {
            CastlePiece piece = CastlePieceAt(point, 0.5f);
            if (piece == null) return;
            if (piece == chainTower || piece == chainFirst || piece == chainRun) EndWallChain();
            Transform section = piece.transform.parent;
            Undo.DestroyObjectImmediate(piece.gameObject);
            // (a section with nothing left in it goes too)
            if (section != null && section != CastleContainer(false) && section.GetComponentsInChildren<CastlePiece>().Length == 0)
            {
                Undo.DestroyObjectImmediate(section.gameObject);
            }
        }

        // A new group under Castle Walls for one wall (each wall drawn, ring or lone tower its own, to delete together)
        private static Transform NewSection(string kind)
        {
            Transform container = CastleContainer(true);
            int number = 1;
            while (container.Find($"{kind} {number}") != null) number++;
            var go = new GameObject($"{kind} {number}");
            go.transform.SetParent(container, false);
            GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.BatchingStatic);
            Undo.RegisterCreatedObjectUndo(go, "Castle Wall");
            return go.transform;
        }

        // The pieces sitting straight under Castle Walls (built before sections), grouped by what joins up: each wall
        // with the towers and bends at its ends
        private static int GroupLoosePieces()
        {
            Transform container = CastleContainer(false);
            if (container == null) return 0;
            var loose = new List<CastlePiece>();
            foreach (Transform child in container)
            {
                if (child.TryGetComponent(out CastlePiece piece)) loose.Add(piece);
            }
            if (loose.Count == 0) return 0;

            var group = new Dictionary<CastlePiece, int>();
            for (int i = 0; i < loose.Count; i++) group[loose[i]] = i;
            int Root(int i) { while (group[loose[i]] != i) i = group[loose[i]]; return i; }
            void Join(int a, int b) { a = Root(a); b = Root(b); if (a != b) group[loose[b]] = a; }
            for (int i = 0; i < loose.Count; i++)
            {
                CastlePiece wall = loose[i];
                if (wall.tower || wall.joint) continue;
                Vector3 start = wall.path != null && wall.path.Count >= 2 ? wall.path[0] : wall.from;
                Vector3 end = wall.path != null && wall.path.Count >= 2 ? wall.path[wall.path.Count - 1] : wall.to;
                for (int j = 0; j < loose.Count; j++)
                {
                    CastlePiece node = loose[j];
                    if (!node.tower && !node.joint) continue;
                    if (Flat(node.transform.position - start).magnitude < 0.6f || Flat(node.transform.position - end).magnitude < 0.6f) Join(i, j);
                }
            }

            Undo.IncrementCurrentGroup();
            Undo.SetCurrentGroupName("Group Castle Walls");
            int undo = Undo.GetCurrentGroup();
            var sections = new Dictionary<int, Transform>();
            int groups = 0;
            for (int i = 0; i < loose.Count; i++)
            {
                int root = Root(i);
                if (!sections.TryGetValue(root, out Transform section))
                {
                    bool lone = true;
                    for (int j = 0; j < loose.Count; j++) if (j != i && Root(j) == root) { lone = false; break; }
                    section = NewSection(lone && loose[i].tower ? "Watch Tower" : "Wall");
                    sections[root] = section;
                    groups++;
                }
                Undo.SetTransformParent(loose[i].transform, section, "Group Castle Walls");
            }
            Undo.CollapseUndoOperations(undo);
            return groups;
        }

        private static Transform CastleContainer(bool make)
        {
            foreach (GameObject root in UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects())
            {
                if (root.name == CastleWallBuilder.ContainerName) return root.transform;
            }
            if (!make) return null;
            var go = new GameObject(CastleWallBuilder.ContainerName);
            GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.BatchingStatic);
            Undo.RegisterCreatedObjectUndo(go, "Castle Wall");
            return go.transform;
        }

        // ---------------------------------------------------------------- Castle walls (drawing)

        private static readonly Color WallColour = new Color(0.75f, 0.8f, 1f);

        // A tower's outline on the ground: square, or round
        private void DrawTowerOutline(Vector3 at, float yaw, Color colour, bool fill)
        {
            float half = CastleWallBuilder.TowerHalf(castle);
            if (CastleWallBuilder.IsRound(castle.Style)) DrawCircle(at, half, colour);
            else DrawRect(at, yaw, Vector2.one * half, colour, fill);
        }

        // A piece standing: a tower's outline, or a wall's line
        private void DrawPiece(CastlePiece piece, Color colour)
        {
            if (piece.tower || piece.joint)
            {
                DrawCircle(piece.transform.position, piece.Radius, colour);
                return;
            }
            if (piece.path != null && piece.path.Count >= 2)
            {
                var line = new List<Vector3>();
                float total = CastleWallBuilder.Length(piece.path, piece.closed);
                for (float d = 0f; d <= total + 0.01f; d += 1f)
                {
                    Vector3 p = CastleWallBuilder.PointAlong(piece.path, piece.closed, Mathf.Min(d, total), out _);
                    line.Add(GroundAt(p, out float h) ? new Vector3(p.x, h + 0.3f, p.z) : p);
                }
                Outline(line.ToArray(), colour);
                return;
            }
            DrawRect(piece.transform.position, piece.transform.eulerAngles.y, piece.footprint * 0.5f, colour, true);
        }

        private void DrawCastlePreview(bool erasing)
        {
            if (!aimed) { DrawStatus("Point at the land."); return; }
            CompareFunction was = Handles.zTest;
            Handles.zTest = CompareFunction.Always;
            try
            {
                if (erasing)
                {
                    CastlePiece piece = CastlePieceAt(aim, 0.5f);
                    DrawCircle(aim, 1f, BadColour);
                    if (piece != null) DrawPiece(piece, BadColour);
                    DrawStatus(piece != null ? (piece.tower ? "Click to rub out this tower" : piece.joint ? "Click to rub out this bend" : "Click to rub out this wall") : "Rubbing out castle walls");
                    return;
                }
                switch (wallDraw)
                {
                    case WallDraw.Towers:
                        if (castle.WatchTowers) DrawTowerToTower();
                        else DrawPathPreview();
                        break;
                    case WallDraw.Freehand:
                        if (drawingWall && freehand.Count > 0)
                        {
                            var line = new List<Vector3>(freehand) { aim };
                            Outline(line.ConvertAll(p => p + Vector3.up * 0.2f).ToArray(), gateNext ? PlaceColour : WallColour);
                            bool closing = freehand.Count > 8 && Flat(aim - freehand[0]).magnitude < Mathf.Max(castle.Bend, 6f);
                            DrawStatus(closing ? "Let go here to close the ring" : "Drawing a wall: let go to build it");
                        }
                        else
                        {
                            DrawCircle(aim, 0.8f, WallColour);
                            DrawStatus(status ?? $"Drag to draw a wall (any shape){(gateNext ? ", with a gate" : "")}.  End where you started to close it.  G: gate");
                        }
                        break;
                    case WallDraw.Circle:
                        if (drawingWall)
                        {
                            float radius = Flat(aim - circleMiddle).magnitude;
                            DrawCircle(circleMiddle, Mathf.Max(radius, 0.5f), radius >= 6f ? WallColour : BadColour);
                            if (radius >= 6f && castle.WatchTowers && castle.TowerEvery > 0f)
                            {
                                int towers = Mathf.Max(3, Mathf.RoundToInt(2f * Mathf.PI * radius / castle.TowerEvery));
                                for (int i = 0; i < towers; i++)
                                {
                                    float a = i * Mathf.PI * 2f / towers;
                                    DrawTowerOutline(circleMiddle + new Vector3(Mathf.Sin(a), 0f, Mathf.Cos(a)) * radius, a * Mathf.Rad2Deg, WallColour, false);
                                }
                            }
                            DrawStatus(radius >= 6f ? $"Let go to build a round wall, {radius * 2f:F0} m across{(gateNext ? ", with a gate" : "")}" : "Drag out further");
                        }
                        else
                        {
                            DrawCircle(aim, 0.8f, WallColour);
                            DrawStatus(status ?? "Click the middle and drag out to build a round wall.  G: gate");
                        }
                        break;
                    case WallDraw.Tower:
                        DrawTowerOutline(aim, 0f, WallColour, true);
                        DrawStatus(status ?? "Click to put a watch tower here");
                        break;
                    case WallDraw.Gate:
                        DrawGatePreview();
                        break;
                }
            }
            finally
            {
                Handles.zTest = was;
            }
        }

        // Gate: the arch, drawn on the wall where it'd go (or the gateway there, to wall up)
        private void DrawGatePreview()
        {
            CastlePiece wall = WallAt(aim);
            if (wall == null)
            {
                DrawCircle(aim, 0.8f, PlaceColour);
                DrawStatus(status ?? "Point at a wall: click to cut a gateway there");
                return;
            }
            List<Vector3> path = wall.path != null && wall.path.Count >= 2 ? wall.path : new List<Vector3> { wall.from, wall.to };
            bool closed = wall.path != null && wall.path.Count >= 2 && wall.closed;
            float centre = CastleWallBuilder.DistanceAlong(path, closed, aim, out _);
            bool existing = wall.gates != null && wall.gates.Exists(g => Mathf.Abs(g - centre) < castle.GateWidth * 0.5f + 0.5f);
            if (existing) centre = wall.gates.Find(g => Mathf.Abs(g - centre) < castle.GateWidth * 0.5f + 0.5f);
            Color colour = existing ? BadColour : PlaceColour;
            // (on both faces, so it shows whichever side you're looking from)
            foreach (float face in new[] { 1f, -1f })
            {
                List<Vector3> arch = CastleWallBuilder.ArchOutline(path, closed, centre, face * (wall.width * 0.5f + 0.05f), castle, CastleGround);
                Outline(arch.ToArray(), colour);
            }
            DrawStatus(existing ? "Click to wall this gateway up" : $"Click to cut a gateway here ({castle.GateWidth:F1} m wide, {castle.GateHeight:F1} m high)");
        }

        // Tower to tower, watch towers off: the wall so far, and the next bit to the mouse
        private void DrawPathPreview()
        {
            CastlePiece snap = TowerNear(aim);
            Vector3 target = snap != null ? snap.transform.position : aim;
            if (!chainActive)
            {
                if (snap != null) DrawPiece(snap, PlaceColour);
                DrawCircle(target, 0.8f, WallColour);
                DrawStatus(status ?? (snap != null ? "Click to start a wall from this tower" : "Click to start a wall (each click a bend; watch towers are off)"));
                return;
            }
            var line = new List<Vector3>(chainPath) { target };
            Outline(line.ConvertAll(p => (GroundAt(p, out float h) ? new Vector3(p.x, h, p.z) : p) + Vector3.up * 0.3f).ToArray(), gateNext ? PlaceColour : WallColour);
            bool closing = chainStartTower == null && chainPath.Count >= 3 && Flat(aim - chainPath[0]).magnitude < Mathf.Max(3f, CastleWallBuilder.WallThickness(castle));
            if (snap != null) DrawPiece(snap, PlaceColour);
            DrawStatus(status ?? (closing ? "Click to close the ring" : snap != null ? "Click to end the wall at this tower" : "Click: the wall bends here.  Right-click / Enter: finish.  G: gate"));
        }

        private void DrawTowerToTower()
        {
            CastlePiece snap = TowerNear(aim);
            Vector3 target = snap != null ? snap.transform.position : aim;
            if (!chainActive)
            {
                if (snap != null) DrawPiece(snap, PlaceColour);
                else DrawTowerOutline(target, 0f, WallColour, true);
                DrawStatus(status ?? (snap != null ? "Click to start a wall from this tower" : "Click to start a wall"));
                return;
            }
            Vector3 from = chainTower != null ? chainTower.transform.position : chainPoint;
            Vector3 direction = Flat(target - from);
            float distance = direction.magnitude;
            if (distance <= 0.01f) return;
            direction /= distance;
            float towerYaw = Mathf.Atan2(direction.x, direction.z) * Mathf.Rad2Deg;
            float wallYaw = Mathf.Atan2(-direction.z, direction.x) * Mathf.Rad2Deg;
            DrawRect((from + target) * 0.5f, wallYaw, new Vector2(distance * 0.5f, CastleWallBuilder.WallThickness(castle) * 0.5f), gateNext ? PlaceColour : WallColour, true);
            if (chainTower == null) DrawTowerOutline(from, towerYaw, WallColour, false);
            int spans = castle.TowerEvery > 0f ? Mathf.Max(1, Mathf.CeilToInt(distance / castle.TowerEvery)) : 1;
            for (int i = 1; i < spans; i++) DrawTowerOutline(Vector3.Lerp(from, target, i / (float)spans), towerYaw, WallColour, false);
            if (snap != null) DrawPiece(snap, PlaceColour);
            else DrawTowerOutline(target, towerYaw, WallColour, true);
            string closing = snap != null && snap == chainFirst ? "  (closes the ring)" : "";
            DrawStatus(status ?? $"Click: wall to here, {distance:F0} m{(spans > 1 ? $", {spans - 1} more tower{(spans > 2 ? "s" : "")}" : "")}{(gateNext ? ", with a gate" : "")}{closing}.  G: gate.  Right-click / Enter: finish");
        }

        private void DrawCastle()
        {
            EditorGUI.BeginChangeCheck();
            wallDraw = (WallDraw)ToolPicker("Draw", (int)wallDraw, new[]
            {
                new GUIContent("Tower to Tower", "Click from point to point: a wall between each."),
                new GUIContent("Freehand", "Drag to draw a wall any shape (curvy too). End where you started to close it."),
                new GUIContent("Circle", "Click the middle and drag out: a round wall."),
                new GUIContent("Tower", "Click to put a watch tower on its own."),
                new GUIContent("Gateway", "Click on a wall to cut an arched gateway through it there (or on one to wall it up).")
            }, 3);
            if (EditorGUI.EndChangeCheck()) EndWallChain();
            HowTo((wallDraw == WallDraw.Towers ? "Click on the land to start, then click again and again: a wall to each point from the last. Click the first one to close the ring; right-click or Enter to finish."
               : wallDraw == WallDraw.Freehand ? "Drag a line on the land: the wall follows it, smooth. End near where you started to close it into a ring."
               : wallDraw == WallDraw.Circle ? "Click where the middle goes and drag out to the size you want."
               : wallDraw == WallDraw.Tower ? "Click to put a watch tower down on its own (walls drawn from it join onto it)."
               : "Point at a wall to see where the arch would go, then click to cut a gateway there. Click a gateway to wall it up again.")
               + "\n<b>G</b> gate in the next wall   <b>Right-click / Enter</b> finish   <b>Shift+click</b> rub out   <b>Esc</b> stop");

            EditorGUI.BeginChangeCheck();
            if (Section("CastleWalls", "Walls", true))
            {
                castle.Storeys = EditorGUILayout.IntSlider(new GUIContent("Height (storeys)", "How high the walls stand, in 3 m storeys."), castle.Storeys, 2, 6);
                castle.Thickness = EditorGUILayout.IntSlider(new GUIContent("Walkway (2 m cells)", "How wide the walkway along the top is."), castle.Thickness, 1, 2);
                castle.Battlements = EditorGUILayout.Toggle(new GUIContent("Battlements", "A parapet with merlons along both sides of the walkway."), castle.Battlements);
                castle.Bend = Slider("Curve smoothness", "Freehand walls and circles bend about every half this many metres: smaller is smoother.", castle.Bend, 2f, 12f);
            }

            if (Section("CastleGates", "Gateways", true))
            {
                castle.GateWidth = Slider("Width", "How wide gateways are (m).", castle.GateWidth, 2f, 8f);
                castle.GateHeight = Slider("Height", "How high gateways are, to the top of the arch (m).", castle.GateHeight, castle.GateWidth * 0.5f + 1f, Mathf.Max(castle.GateWidth * 0.5f + 1.1f, castle.Storeys * 3f - 1.2f));
                gateNext = EditorGUILayout.Toggle(new GUIContent("Gate in next wall (G)", "Put a gateway through the middle of the next wall you build."), gateNext);
            }

            if (Section("CastleTowers", "Watch Towers", true))
            {
                castle.WatchTowers = EditorGUILayout.Toggle(new GUIContent("Watch towers", "Towers along the walls (off: one smooth wall, bending where you click)."), castle.WatchTowers);
                using (new EditorGUI.DisabledScope(!castle.WatchTowers && wallDraw != WallDraw.Tower))
                {
                    castle.TowerEvery = Slider("Every", "How far apart the watch towers are along the walls (m). 0: only where you click.", castle.TowerEvery, 0f, 100f);
                    GUILayout.Label(new GUIContent("Style", "What the watch towers look like."), EditorStyles.miniLabel);
                    castle.Style = (CastleWallBuilder.TowerStyle)GUILayout.SelectionGrid((int)castle.Style, new[]
                    {
                        new GUIContent("Square", "A plain square tower."),
                        new GUIContent("Round", "A round tower."),
                        new GUIContent("Octagonal", "Eight-sided."),
                        new GUIContent("Tapered", "Round, flaring out wide at its foot."),
                        new GUIContent("Turreted", "Square, with a little round turret on each corner."),
                        new GUIContent("Stepped", "A broad lower tier and a narrower upper one."),
                        new GUIContent("Hoarded", "Round, with a wooden gallery overhanging its top."),
                        new GUIContent("Mixed", "A different style for each tower.")
                    }, 4, GUILayout.Height(44));
                    castle.TowerCells = EditorGUILayout.IntSlider(new GUIContent("Width (2 m cells)", "How wide the towers are."), castle.TowerCells, Mathf.Max(2, castle.Thickness + 1), 4);
                    castle.TowerExtra = EditorGUILayout.IntSlider(new GUIContent("Above the walls", "How many storeys the towers stand above the walls."), castle.TowerExtra, 0, 4);
                    EditorGUILayout.BeginHorizontal();
                    EditorGUILayout.PrefixLabel(new GUIContent("Tops", "A pointed tiled roof, or battlements."));
                    if (GUILayout.Toggle(castle.TowerRoofs, "Pointed roof", EditorStyles.miniButtonLeft)) castle.TowerRoofs = true;
                    if (GUILayout.Toggle(!castle.TowerRoofs, "Battlements", EditorStyles.miniButtonRight)) castle.TowerRoofs = false;
                    EditorGUILayout.EndHorizontal();
                }
            }

            if (Section("CastleLook", "Stone Colour", true)) DrawStonePicker();
            if (Section("CastleOptions", "Options", true))
            {
                clearScatter = EditorGUILayout.Toggle(new GUIContent("Clear scatter under", "Rub out Paint Land's scatter (trees, rocks, grass...) under new walls and towers."), clearScatter);
                eraser = EditorGUILayout.Toggle(new GUIContent("Erase (or hold Shift)", "Click a tower or wall to rub it out."), eraser);
            }
            if (EditorGUI.EndChangeCheck()) Changed();

            if (Section("CastleWeathering", "Weathering", true)) DrawWeathering();
            if (Section("CastleDetails", "Wall Details", true)) DrawDetails();
            if (Section("CastleSections", "Sections", true)) DrawSections();
            if (Section("CastleMore", "Scene & Tools", false))
            {
                Transform container = CastleContainer(false);
                using (new EditorGUI.DisabledScope(container == null))
                {
                    if (GUILayout.Button("Select All") && container != null) Selection.activeTransform = container;
                    if (GUILayout.Button(new GUIContent("Clear Grass In Gateways", "Take Paint Land's grass off the way through every gateway already built.")) && container != null)
                    {
                        int cleared = ClearAllGateGrass();
                        ShowNotification(new GUIContent(cleared > 0 ? $"Grass cleared from {cleared} gateway{(cleared == 1 ? "" : "s")}" : "No gateways (or no grass field) to clear"));
                    }
                    if (GUILayout.Button("Remove All Castle Walls") && container != null &&
                        EditorUtility.DisplayDialog("Remove all castle walls?", "Remove every tower and wall? (Undo brings them back.)", "Remove", "Cancel"))
                    {
                        EndWallChain();
                        Undo.DestroyObjectImmediate(container.gameObject);
                    }
                }
            }
        }

        // The little things on new walls and towers (Look > Details)
        private void DrawDetails()
        {
            CastleWallBuilder.Details k = castle.Detail;
            Hint("For walls and towers built from now on (rebuild a section to give it new details).");
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button(new GUIContent("All On", "Every detail."), EditorStyles.miniButtonLeft)) { SetAllDetails(true); Changed(); }
            if (GUILayout.Button(new GUIContent("Defensive", "Arrow slits, buttresses, corbels, plinth, portcullis."), EditorStyles.miniButtonMid)) { SetAllDetails(false); k.ArrowSlits = k.Buttresses = k.Corbels = k.Plinth = k.Portcullis = k.Torches = true; Changed(); }
            if (GUILayout.Button(new GUIContent("Ruined", "Ivy and rubble everywhere, no banners or torches."), EditorStyles.miniButtonMid)) { SetAllDetails(false); k.Ivy = k.Rubble = k.ArrowSlits = true; k.IvyAmount = 0.75f; k.RubbleAmount = 0.8f; Changed(); }
            if (GUILayout.Button(new GUIContent("Bare Stone", "No details at all."), EditorStyles.miniButtonRight)) { SetAllDetails(false); Changed(); }
            EditorGUILayout.EndHorizontal();

            EditorGUI.BeginChangeCheck();
            SubHeading("Defences");
            k.ArrowSlits = DetailToggle("Windows and slits", "Openings in the walls and towers: glazed windows in stone surrounds, arrow slits, or both.", k.ArrowSlits);
            if (k.ArrowSlits)
            {
                k.Openings = EditorGUILayout.Popup(new GUIContent("    kind", "Arrow slits only, windows only, or windows on the top row with slits below."), k.Openings, new[] { "Arrow slits", "Windows", "Windows over slits" });
                if (k.Openings > 0) k.Shutters = EditorGUILayout.Toggle(new GUIContent("    shutters", "Wooden shutters (open or closed) on some windows."), k.Shutters);
            }
            if (k.ArrowSlits) k.SlitEvery = Slider("    every", "How far apart (m).", k.SlitEvery, 2f, 20f);
            k.Buttresses = DetailToggle("Buttresses", "Stepped stone piers against the outside of the walls.", k.Buttresses);
            if (k.Buttresses) k.ButtressEvery = Slider("    every", "How far apart (m).", k.ButtressEvery, 5f, 40f);
            k.Corbels = DetailToggle("Corbels", "A carved band with corbels under the battlements.", k.Corbels);
            k.Plinth = DetailToggle("Plinth", "A broader course of stone along the foot of the walls.", k.Plinth);
            k.Portcullis = DetailToggle("Portcullis", "An iron grate, half raised, in each gateway.", k.Portcullis);

            SubHeading("Gateways");
            k.GatePassage = DetailToggle("Gate passage", "Inside the gateways: a ring of arch stones and quoins on each face, stone ribs across the vault, murder holes, portcullis grooves, a threshold kerb and torches.", k.GatePassage);
            k.GateDoors = DetailToggle("Gate doors", "Heavy wooden doors with iron bands, swung open against the inside.", k.GateDoors);
            k.Clutter = DetailToggle("Crates and carts", "Crates (some stacked) and now and then a cart, just inside the gates.", k.Clutter);

            SubHeading("Life");
            k.Banners = DetailToggle("Banners", "Banners hung from the towers, with a trim and an emblem.", k.Banners);
            k.Flags = DetailToggle("Flags", "A flag flying from each tower (on the spire, or the battlements).", k.Flags);
            if (k.Banners || k.Flags)
            {
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.PrefixLabel(new GUIContent("    colours", "The banners' and flags' colour, and their trim."));
                k.BannerColour = EditorGUILayout.ColorField(GUIContent.none, k.BannerColour, false, false, false, GUILayout.MinWidth(40));
                k.BannerTrim = EditorGUILayout.ColorField(GUIContent.none, k.BannerTrim, false, false, false, GUILayout.MinWidth(40));
                EditorGUILayout.EndHorizontal();
            }
            k.Torches = DetailToggle("Torches", "Burning torches on brackets: beside the gateways and along the inside.", k.Torches);
            if (k.Torches)
            {
                k.TorchEvery = Slider("    every", "How far apart along the inside (m).", k.TorchEvery, 6f, 50f);
                k.TorchLights = EditorGUILayout.Toggle(new GUIContent("    real light", "Each torch casts light (looks lovely at night, but many lights cost frames)."), k.TorchLights);
            }
            k.Stairs = DetailToggle("Stairs", "Stone stairs up to the walkway, on the inside of each wall.", k.Stairs);

            SubHeading("Nature");
            k.Ivy = DetailToggle("Ivy", "Ivy hanging down the walls and towers.", k.Ivy);
            if (k.Ivy) k.IvyAmount = Slider("    amount", "How much of the walls it covers.", k.IvyAmount, 0.05f, 1f);
            k.Rubble = DetailToggle("Rubble", "Fallen stones along the foot of the walls and towers.", k.Rubble);
            if (k.Rubble) k.RubbleAmount = Slider("    amount", "How much.", k.RubbleAmount, 0.05f, 1f);
            if (EditorGUI.EndChangeCheck()) Changed();
        }

        private static bool DetailToggle(string label, string tooltip, bool on) => EditorGUILayout.ToggleLeft(new GUIContent(label, tooltip), on);

        private void SetAllDetails(bool on)
        {
            CastleWallBuilder.Details k = castle.Detail;
            k.ArrowSlits = k.Buttresses = k.Corbels = k.Plinth = k.Portcullis = on;
            k.GatePassage = k.Shutters = on;
            k.GateDoors = k.Clutter = k.Banners = k.Flags = k.Torches = k.Stairs = on;
            k.Ivy = k.Rubble = on;
            if (on) { k.IvyAmount = Mathf.Max(k.IvyAmount, 0.25f); k.RubbleAmount = Mathf.Max(k.RubbleAmount, 0.35f); }
        }

        private void LoadDetails()
        {
            CastleWallBuilder.Details k = castle.Detail;
            string p = Prefs + "Detail.";
            k.ArrowSlits = EditorPrefs.GetBool(p + "Slits", true); k.SlitEvery = EditorPrefs.GetFloat(p + "SlitEvery", 6f);
            k.Openings = EditorPrefs.GetInt(p + "Openings", 2); k.Shutters = EditorPrefs.GetBool(p + "Shutters", true);
            k.GatePassage = EditorPrefs.GetBool(p + "GatePassage", true);
            k.Buttresses = EditorPrefs.GetBool(p + "Buttresses", false); k.ButtressEvery = EditorPrefs.GetFloat(p + "ButtressEvery", 14f);
            k.Corbels = EditorPrefs.GetBool(p + "Corbels", true);
            k.Plinth = EditorPrefs.GetBool(p + "Plinth", true);
            k.Portcullis = EditorPrefs.GetBool(p + "Portcullis", true);
            k.GateDoors = EditorPrefs.GetBool(p + "GateDoors", true);
            k.Clutter = EditorPrefs.GetBool(p + "Clutter", true);
            k.Banners = EditorPrefs.GetBool(p + "Banners", true);
            k.Flags = EditorPrefs.GetBool(p + "Flags", true);
            k.BannerColour = ColorPref("Detail.BannerColour", new Color(0.55f, 0.08f, 0.1f));
            k.BannerTrim = ColorPref("Detail.BannerTrim", new Color(0.9f, 0.72f, 0.25f));
            k.Torches = EditorPrefs.GetBool(p + "Torches", true); k.TorchEvery = EditorPrefs.GetFloat(p + "TorchEvery", 16f);
            k.TorchLights = EditorPrefs.GetBool(p + "TorchLights", false);
            k.Stairs = EditorPrefs.GetBool(p + "Stairs", true);
            k.Ivy = EditorPrefs.GetBool(p + "Ivy", true); k.IvyAmount = EditorPrefs.GetFloat(p + "IvyAmount", 0.25f);
            k.Rubble = EditorPrefs.GetBool(p + "Rubble", true); k.RubbleAmount = EditorPrefs.GetFloat(p + "RubbleAmount", 0.35f);
        }

        private void SaveDetails()
        {
            CastleWallBuilder.Details k = castle.Detail;
            string p = Prefs + "Detail.";
            EditorPrefs.SetBool(p + "Slits", k.ArrowSlits); EditorPrefs.SetFloat(p + "SlitEvery", k.SlitEvery);
            EditorPrefs.SetInt(p + "Openings", k.Openings); EditorPrefs.SetBool(p + "Shutters", k.Shutters);
            EditorPrefs.SetBool(p + "GatePassage", k.GatePassage);
            EditorPrefs.SetBool(p + "Buttresses", k.Buttresses); EditorPrefs.SetFloat(p + "ButtressEvery", k.ButtressEvery);
            EditorPrefs.SetBool(p + "Corbels", k.Corbels);
            EditorPrefs.SetBool(p + "Plinth", k.Plinth);
            EditorPrefs.SetBool(p + "Portcullis", k.Portcullis);
            EditorPrefs.SetBool(p + "GateDoors", k.GateDoors);
            EditorPrefs.SetBool(p + "Clutter", k.Clutter);
            EditorPrefs.SetBool(p + "Banners", k.Banners);
            EditorPrefs.SetBool(p + "Flags", k.Flags);
            EditorPrefs.SetString(p + "BannerColour", "#" + ColorUtility.ToHtmlStringRGB(k.BannerColour));
            EditorPrefs.SetString(p + "BannerTrim", "#" + ColorUtility.ToHtmlStringRGB(k.BannerTrim));
            EditorPrefs.SetBool(p + "Torches", k.Torches); EditorPrefs.SetFloat(p + "TorchEvery", k.TorchEvery);
            EditorPrefs.SetBool(p + "TorchLights", k.TorchLights);
            EditorPrefs.SetBool(p + "Stairs", k.Stairs);
            EditorPrefs.SetBool(p + "Ivy", k.Ivy); EditorPrefs.SetFloat(p + "IvyAmount", k.IvyAmount);
            EditorPrefs.SetBool(p + "Rubble", k.Rubble); EditorPrefs.SetFloat(p + "RubbleAmount", k.RubbleAmount);
        }

        // Each wall drawn is a section (a group under Castle Walls): select it, or delete it whole
        private void DrawSections()
        {
            Transform container = CastleContainer(false);
            if (container == null)
            {
                Hint("No castle walls yet. Each wall you draw becomes a section here (and a group in the Hierarchy).");
                return;
            }
            int loose = 0;
            Transform remove = null;
            foreach (Transform child in container)
            {
                if (child.GetComponent<CastlePiece>() != null) { loose++; continue; }
                CastlePiece[] pieces = child.GetComponentsInChildren<CastlePiece>();
                int towers = 0, walls = 0, gates = 0;
                foreach (CastlePiece p in pieces)
                {
                    if (p.tower) towers++;
                    else if (!p.joint) { walls++; if (p.gates != null) gates += p.gates.Count; }
                }
                EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);
                GUILayout.Label(new GUIContent(child.name, $"{walls} wall{(walls == 1 ? "" : "s")}, {towers} tower{(towers == 1 ? "" : "s")}, {gates} gateway{(gates == 1 ? "" : "s")}"), GUILayout.MinWidth(80));
                GUILayout.Label($"{towers} tower{(towers == 1 ? "" : "s")}", EditorStyles.miniLabel, GUILayout.Width(52));
                if (GUILayout.Button(new GUIContent("Select", "Select this section (and show it in the Scene view)."), EditorStyles.miniButton, GUILayout.Width(50)))
                {
                    Selection.activeTransform = child;
                    SceneView.lastActiveSceneView?.FrameSelected();
                }
                if (GUILayout.Button(new GUIContent("✕", "Delete this whole section (Undo brings it back)."), EditorStyles.miniButton, GUILayout.Width(22))) remove = child;
                EditorGUILayout.EndHorizontal();
            }
            if (remove != null)
            {
                if (chainGroup == remove) EndWallChain();
                Undo.DestroyObjectImmediate(remove.gameObject);
            }
            if (loose > 0)
            {
                EditorGUILayout.HelpBox($"{loose} piece{(loose == 1 ? " was" : "s were")} built before sections. Group them to delete them in sections too.", MessageType.None);
                if (GUILayout.Button("Group Them Into Sections"))
                {
                    int made = GroupLoosePieces();
                    ShowNotification(new GUIContent($"{made} section{(made == 1 ? "" : "s")} made"));
                }
            }
        }

        // ---------------------------------------------------------------- Auto paths (window)

        private void DrawPaths()
        {
            EditorGUI.BeginChangeCheck();
            autoPaths = EditorGUILayout.Toggle(new GUIContent("Paths to new houses", "Make a path from each new house's front door to the nearest house (so the village is joined up)."), autoPaths);
            if (groundIsLand)
            {
                pathStyle.Look = (BuildingPaths.Look)GUILayout.Toolbar((int)pathStyle.Look, new[]
                {
                    new GUIContent("Colour", "Painted on Paint Land's land in a colour (its paths)."),
                    new GUIContent("Texture", "Painted on Paint Land's land with one of its textures (cobbles, gravel...)."),
                    new GUIContent("Mesh", "A strip of mesh laid on the ground, in a colour or your own material (Builder Settings).")
                });
            }
            else
            {
                Hint("Paths are strips of mesh laid on the ground (with Paint Land's land in the scene they can be painted on it).");
            }
            BuildingPaths.Look look = groundIsLand ? pathStyle.Look : BuildingPaths.Look.Mesh;
            if (look != BuildingPaths.Look.Texture)
            {
                // Paint Land's colours to pick from, then any colour
                List<(string name, Color colour)> palette = groundIsLand ? PaintLandLink.Palette() : new List<(string, Color)>();
                if (palette.Count > 0)
                {
                    int perRow = Mathf.Max(1, Mathf.FloorToInt((position.width - 30f) / 64f));
                    for (int i = 0; i < palette.Count; i += perRow)
                    {
                        EditorGUILayout.BeginHorizontal();
                        for (int j = i; j < Mathf.Min(i + perRow, palette.Count); j++)
                        {
                            Color was = GUI.backgroundColor;
                            GUI.backgroundColor = palette[j].colour;
                            bool picked = (Vector4)palette[j].colour == (Vector4)pathStyle.Colour;
                            if (GUILayout.Button(new GUIContent(picked ? "✓" : "", palette[j].name), GUILayout.Height(20), GUILayout.MinWidth(30))) pathStyle.Colour = palette[j].colour;
                            GUI.backgroundColor = was;
                        }
                        EditorGUILayout.EndHorizontal();
                    }
                }
                pathStyle.Colour = EditorGUILayout.ColorField(new GUIContent("Colour", "The path's colour."), pathStyle.Colour, true, false, false);
                if (look == BuildingPaths.Look.Mesh && BuilderSettings.Get().pathMaterial != null) Hint("(Builder Settings has a path material: it's used instead of the colour.)");
            }
            else
            {
                Material land = PaintLandLink.TextureMaterial;
                if (!PaintLandLink.TexturesSupported(land))
                {
                    Hint("The land's material has no painted textures (Paint Land > Textures).");
                }
                else
                {
                    var names = new string[8];
                    for (int i = 0; i < 8; i++) names[i] = $"{i + 1}. {PaintLandLink.TextureName(land, i + 1)}";
                    EditorGUILayout.BeginHorizontal();
                    Texture preview = PaintLandLink.TextureOf(land, pathStyle.Layer);
                    GUILayout.Label(preview != null ? preview : Texture2D.grayTexture, GUILayout.Width(40), GUILayout.Height(40));
                    EditorGUILayout.BeginVertical();
                    pathStyle.Layer = EditorGUILayout.Popup("Texture", Mathf.Clamp(pathStyle.Layer, 1, 8) - 1, names) + 1;
                    Hint("Paint Land's textures (change them, and their tint, in its Textures tab).");
                    EditorGUILayout.EndVertical();
                    EditorGUILayout.EndHorizontal();
                }
            }
            pathStyle.Width = Slider("Width", "How wide the paths are (m).", pathStyle.Width, 0.8f, 6f);
            pathStyle.Wobble = Slider("Wander", "How much the paths wander, rather than run smooth.", pathStyle.Wobble, 0f, 1f);
            pathStyle.Longest = Slider("Longest path", "Houses further apart than this (m) aren't joined.", pathStyle.Longest, 10f, 200f);
            pathStyle.ClearUnder = EditorGUILayout.Toggle(new GUIContent("Clear under", "Rub out what's set to clear under paths (Paint Land's scatter kinds, as its Land Settings say), and a Unity Terrain's trees (Builder Settings)."), pathStyle.ClearUnder);
            if (EditorGUI.EndChangeCheck()) Changed();

            List<ScatteredBuilding> selected = SelectedHouses();
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button(new GUIContent("Join All Houses", "Make paths joining every house in the scene, by the shortest ways.")))
            {
                List<ScatteredBuilding> all = AllHouses();
                string trouble = BuildingPaths.Connect(all, all, pathStyle);
                if (trouble != null) ShowNotification(new GUIContent(trouble));
            }
            using (new EditorGUI.DisabledScope(selected.Count == 0))
            {
                if (GUILayout.Button(new GUIContent($"Join Selected ({selected.Count})", "Make paths from the selected houses to their nearest neighbours.")))
                {
                    string trouble = BuildingPaths.Connect(selected, AllHouses(), pathStyle);
                    if (trouble != null) ShowNotification(new GUIContent(trouble));
                }
            }
            EditorGUILayout.EndHorizontal();
            Hint(look == BuildingPaths.Look.Mesh
                ? "Hold Shift (or tick Erase) and brush over a path to cut it away. Mesh paths go under the \"Village Paths\" object."
                : "Paths are painted on the land like Paint Land's: rub them out with its Paths (or Textures) tab.");
        }

        // ---------------------------------------------------------------- Keeping clear

        private bool Overlaps(Vector2 footprint, Vector3 at, float yaw)
        {
            Transform container = Container(false);
            if (container != null)
            {
                foreach (ScatteredBuilding other in container.GetComponentsInChildren<ScatteredBuilding>())
                {
                    Vector2 size = other.footprint * Mathf.Max(other.transform.lossyScale.x, 0.01f);
                    if (RectsTouch(at, yaw, footprint * 0.5f + Vector2.one * gap * 0.5f,
                                   other.transform.position, other.transform.eulerAngles.y, size * 0.5f + Vector2.one * gap * 0.5f)) return true;
                }
            }
            // (and off the castle walls and towers)
            return OnCastle(at, yaw, footprint * 0.5f, gap * 0.5f);
        }

        // Two turned rectangles on the ground (separating axes)
        private static bool RectsTouch(Vector3 aAt, float aYaw, Vector2 aHalf, Vector3 bAt, float bYaw, Vector2 bHalf)
        {
            Vector2 aX = Axis(aYaw, true), aZ = Axis(aYaw, false), bX = Axis(bYaw, true), bZ = Axis(bYaw, false);
            Vector2 d = new Vector2(bAt.x - aAt.x, bAt.z - aAt.z);
            foreach (Vector2 axis in new[] { aX, aZ, bX, bZ })
            {
                float a = aHalf.x * Mathf.Abs(Vector2.Dot(aX, axis)) + aHalf.y * Mathf.Abs(Vector2.Dot(aZ, axis));
                float b = bHalf.x * Mathf.Abs(Vector2.Dot(bX, axis)) + bHalf.y * Mathf.Abs(Vector2.Dot(bZ, axis));
                if (Mathf.Abs(Vector2.Dot(d, axis)) > a + b) return false;
            }
            return true;
        }

        private static Vector2 Axis(float yaw, bool x)
        {
            Vector3 v = Quaternion.Euler(0f, yaw, 0f) * (x ? Vector3.right : Vector3.forward);
            return new Vector2(v.x, v.z);
        }

        private static bool Inside(Vector3 point, Vector3 at, float yaw, Vector2 half)
        {
            Vector3 local = Quaternion.Euler(0f, -yaw, 0f) * (point - at);
            return Mathf.Abs(local.x) <= half.x && Mathf.Abs(local.z) <= half.y;
        }

        private void ClearScatterUnder(Vector2 footprint, Vector3 at, float yaw)
        {
            Vector2 half = footprint * 0.5f + Vector2.one * 0.5f;
            var doomed = new List<GameObject>();
            GameObject scatter = PaintLandLink.ScatterRoot();
            if (scatter != null)
            {
                foreach (Transform kind in scatter.transform)
                {
                    foreach (Transform thing in kind)
                    {
                        if (Inside(thing.position, at, yaw, half)) doomed.Add(thing.gameObject);
                    }
                }
            }
            foreach (GameObject root in UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects())
            {
                if (root.name != Nature.ContainerName) continue;
                foreach (Transform thing in root.transform) if (Inside(thing.position, at, yaw, half)) doomed.Add(thing.gameObject);
            }
            foreach (GameObject thing in doomed) Undo.DestroyObjectImmediate(thing);
            if (doomed.Count > 0) PaintLandLink.RefreshScatter();
            TerrainTrees.Clear(p => Inside(p, at, yaw, half), at, half.magnitude);
        }

        private void RubOut(Vector3 centre, float radius)
        {
            Transform container = Container(false);
            if (container == null) return;
            var doomed = new List<GameObject>();
            foreach (ScatteredBuilding house in container.GetComponentsInChildren<ScatteredBuilding>())
            {
                Vector3 local = Quaternion.Euler(0f, -house.transform.eulerAngles.y, 0f) * (centre - house.transform.position);
                Vector2 half = house.footprint * 0.5f;
                // The nearest point of its footprint to the brush's middle
                float dx = Mathf.Max(Mathf.Abs(local.x) - half.x, 0f), dz = Mathf.Max(Mathf.Abs(local.z) - half.y, 0f);
                if (dx * dx + dz * dz <= radius * radius) doomed.Add(house.gameObject);
            }
            foreach (GameObject house in doomed)
            {
                Undecorate(house.GetComponent<ScatteredBuilding>());
                Transform group = house.transform.parent;
                Undo.DestroyObjectImmediate(house);
                // (a group with nothing left in it goes too)
                if (group != null && group != container && group.GetComponentsInChildren<ScatteredBuilding>().Length == 0) Undo.DestroyObjectImmediate(group.gameObject);
            }
            removedThisStroke += doomed.Count;
            BuildingPaths.CutPaths(centre, Mathf.Max(radius, 1.2f));   // (and the mesh paths under the brush)
        }

        // The group this stroke's houses go in (made with its first house): "Houses 1", "Houses 2"... under Buildings
        private Transform strokeGroup;

        private Transform StrokeGroup()
        {
            if (strokeGroup != null) return strokeGroup;
            Transform container = Container(true);
            int number = 1;
            while (container.Find($"Houses {number}") != null) number++;
            var go = new GameObject($"Houses {number}");
            go.transform.SetParent(container, false);
            GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.BatchingStatic);
            Undo.RegisterCreatedObjectUndo(go, "Castle & Village Builder");
            strokeGroup = go.transform;
            return strokeGroup;
        }

        // Each lot of houses put down together is a group under Buildings: select it, or delete it whole (its plants too)
        private void DrawHouseSections()
        {
            Transform container = Container(false);
            if (container == null)
            {
                Hint("No houses yet. Each lot you put down becomes a group here (and in the Hierarchy).");
                return;
            }
            int loose = 0;
            Transform remove = null;
            foreach (Transform child in container)
            {
                if (child.GetComponent<ScatteredBuilding>() != null) { loose++; continue; }
                int count = child.GetComponentsInChildren<ScatteredBuilding>().Length;
                EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);
                GUILayout.Label(child.name, GUILayout.MinWidth(80));
                GUILayout.Label($"{count} house{(count == 1 ? "" : "s")}", EditorStyles.miniLabel, GUILayout.Width(56));
                if (GUILayout.Button(new GUIContent("Select", "Select this group (and show it in the Scene view)."), EditorStyles.miniButton, GUILayout.Width(50)))
                {
                    Selection.activeTransform = child;
                    SceneView.lastActiveSceneView?.FrameSelected();
                }
                if (GUILayout.Button(new GUIContent("✕", "Delete this group's houses and their plants (Undo brings them back)."), EditorStyles.miniButton, GUILayout.Width(22))) remove = child;
                EditorGUILayout.EndHorizontal();
            }
            if (remove != null)
            {
                Undo.IncrementCurrentGroup();
                Undo.SetCurrentGroupName("Delete Houses");
                int group = Undo.GetCurrentGroup();
                foreach (ScatteredBuilding house in remove.GetComponentsInChildren<ScatteredBuilding>()) Undecorate(house);
                if (strokeGroup == remove) strokeGroup = null;
                Undo.DestroyObjectImmediate(remove.gameObject);
                Undo.CollapseUndoOperations(group);
                RefreshScatter();
            }
            if (loose > 0)
            {
                EditorGUILayout.HelpBox($"{loose} house{(loose == 1 ? " was" : "s were")} put down before groups. Group them (houses near each other together) to delete them in groups too.", MessageType.None);
                if (GUILayout.Button("Group Them By Neighbourhood"))
                {
                    int made = GroupLooseHouses();
                    ShowNotification(new GUIContent($"{made} group{(made == 1 ? "" : "s")} made"));
                }
            }
        }

        // The houses straight under Buildings, gathered into groups of neighbours (each within 30 m of another in it)
        private static int GroupLooseHouses()
        {
            Transform container = Container(false);
            if (container == null) return 0;
            var loose = new List<ScatteredBuilding>();
            foreach (Transform child in container) if (child.TryGetComponent(out ScatteredBuilding house)) loose.Add(house);
            if (loose.Count == 0) return 0;

            Undo.IncrementCurrentGroup();
            Undo.SetCurrentGroupName("Group Houses");
            int undo = Undo.GetCurrentGroup();
            var left = new List<ScatteredBuilding>(loose);
            int groups = 0;
            while (left.Count > 0)
            {
                var cluster = new List<ScatteredBuilding> { left[0] };
                left.RemoveAt(0);
                for (int i = 0; i < cluster.Count; i++)
                {
                    for (int j = left.Count - 1; j >= 0; j--)
                    {
                        if (Vector3.Distance(cluster[i].transform.position, left[j].transform.position) > 30f) continue;
                        cluster.Add(left[j]);
                        left.RemoveAt(j);
                    }
                }
                int number = 1;
                while (container.Find($"Houses {number}") != null) number++;
                var go = new GameObject($"Houses {number}");
                Undo.RegisterCreatedObjectUndo(go, "Group Houses");
                go.transform.SetParent(container, false);
                foreach (ScatteredBuilding house in cluster) Undo.SetTransformParent(house.transform, go.transform, "Group Houses");
                groups++;
            }
            Undo.CollapseUndoOperations(undo);
            return groups;
        }

        private static Transform Container(bool make)
        {
            foreach (GameObject root in UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects())
            {
                if (root.name == ContainerName) return root.transform;
            }
            if (!make) return null;
            var go = new GameObject(ContainerName);
            GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.BatchingStatic);
            Undo.RegisterCreatedObjectUndo(go, "Castle & Village Builder");
            return go.transform;
        }

        // ---------------------------------------------------------------- Drawing

        private static readonly Color PlaceColour = new Color(1f, 0.78f, 0.35f);
        private static readonly Color BadColour = new Color(1f, 0.35f, 0.3f);

        private void Draw(bool erasing)
        {
            if (!aimed) { DrawStatus("Point at the land."); return; }
            CompareFunction was = Handles.zTest;
            Handles.zTest = CompareFunction.Always;

            if (facing)
            {
                // Each house's door, with a dashed line to the mouse it's turning to face
                Handles.color = PlaceColour;
                foreach (ScatteredBuilding house in lastPlaced)
                {
                    if (house == null) continue;
                    var shape = new MedievalHouseBuilder.Design { Width = house.cells.x, Length = house.cells.y };
                    Vector3 door = house.transform.position + house.transform.rotation * shape.DoorSpot + Vector3.up * 0.1f;
                    DrawRect(house.transform.position, house.transform.eulerAngles.y, house.footprint * 0.5f, PlaceColour, false);
                    Handles.color = PlaceColour;
                    Handles.DrawDottedLine(door, aim + Vector3.up * 0.1f, 4f);
                }
                DrawCircle(aim, 0.6f, PlaceColour);
                DrawStatus($"Turning {lastPlaced.Count} house{(lastPlaced.Count == 1 ? "" : "s")} to face the mouse: right-click to keep, Esc to undo");
            }
            else if (erasing)
            {
                DrawCircle(aim, mode == Mode.Brush ? brush * 0.5f : 1f, BadColour);
                Transform container = Container(false);
                if (container != null)
                {
                    foreach (ScatteredBuilding house in container.GetComponentsInChildren<ScatteredBuilding>())
                    {
                        if (Vector3.Distance(house.transform.position, aim) < brush * 0.5f + house.footprint.magnitude * 0.5f)
                            DrawRect(house.transform.position, house.transform.eulerAngles.y, house.footprint * 0.5f, new Color(1f, 0.35f, 0.3f, 0.6f), false);
                    }
                }
                DrawStatus("Rubbing out houses and paths");
            }
            else if (mode == Mode.Brush)
            {
                DrawCircle(aim, brush * 0.5f, PlaceColour);
                DrawStatus(status ?? (HasLastPlaced() && !stroking ? "Scattering houses (right-click: turn the last ones to face the mouse)" : "Scattering houses"));
            }
            else if (mode == Mode.Line)
            {
                DrawCircle(aim, 0.8f, PlaceColour);
                if (stroking && lineNext != null)
                {
                    Handles.color = new Color(PlaceColour.r, PlaceColour.g, PlaceColour.b, 0.8f);
                    Handles.DrawDottedLine(lineFrom + Vector3.up * 0.1f, aim + Vector3.up * 0.1f, 4f);
                }
                DrawStatus(status ?? (stroking ? "Laying houses along the line"
                                     : HasLastPlaced() ? "Right-click to turn the new houses to face the mouse (or drag another line)"
                                     : "Drag a line of houses"));
            }
            else if (next != null)
            {
                string why = Check(next, aim, nextYaw, out Vector3 at);
                Color colour = why == null ? PlaceColour : BadColour;
                DrawRect(at, nextYaw, next.Footprint * 0.5f, colour, true);
                // The front door
                Vector3 door = at + Quaternion.Euler(0f, nextYaw, 0f) * (next.DoorSpot + Vector3.right * 1.5f);
                Handles.color = colour;
                Handles.DrawAAPolyLine(4f, at + Quaternion.Euler(0f, nextYaw, 0f) * next.DoorSpot + Vector3.up * 0.1f, door + Vector3.up * 0.1f);
                DrawStatus(why ?? $"{next.Style} house, {next.Width * 2}x{next.Length * 2} m, {next.Storeys} storey{(next.Storeys > 1 ? "s" : "")}");
            }
            Handles.zTest = was;
        }

        private void DrawCircle(Vector3 centre, float radius, Color colour)
        {
            const int segments = 48;
            var points = new Vector3[segments + 1];
            for (int i = 0; i <= segments; i++)
            {
                float a = i * Mathf.PI * 2f / segments;
                Vector3 p = centre + new Vector3(Mathf.Cos(a) * radius, 0f, Mathf.Sin(a) * radius);
                points[i] = (GroundAt(p, out float h) ? new Vector3(p.x, h, p.z) : p) + Vector3.up * 0.05f;
            }
            Outline(points, colour);
            Handles.color = new Color(colour.r, colour.g, colour.b, 0.12f);
            for (int i = 0; i < segments; i++) Handles.DrawAAConvexPolygon(centre + Vector3.up * 0.05f, points[i], points[i + 1]);
        }

        private void DrawRect(Vector3 at, float yaw, Vector2 half, Color colour, bool fill)
        {
            Quaternion turn = Quaternion.Euler(0f, yaw, 0f);
            Vector3[] corners =
            {
                new Vector3(-half.x, 0f, -half.y), new Vector3(half.x, 0f, -half.y), new Vector3(half.x, 0f, half.y), new Vector3(-half.x, 0f, half.y)
            };
            var points = new List<Vector3>();
            for (int c = 0; c < 4; c++)
            {
                Vector3 a = corners[c], b = corners[(c + 1) % 4];
                for (int s = 0; s < 8; s++)
                {
                    Vector3 p = at + turn * Vector3.Lerp(a, b, s / 8f);
                    points.Add((GroundAt(p, out float h) ? new Vector3(p.x, Mathf.Max(h, at.y - 3f), p.z) : p) + Vector3.up * 0.06f);
                }
            }
            points.Add(points[0]);
            Outline(points.ToArray(), colour);
            if (fill)
            {
                Handles.color = new Color(colour.r, colour.g, colour.b, 0.12f);
                Vector3 lift = Vector3.up * 0.06f;
                Handles.DrawAAConvexPolygon(at + turn * corners[0] + lift, at + turn * corners[1] + lift, at + turn * corners[2] + lift, at + turn * corners[3] + lift);
            }
        }

        private static void Outline(Vector3[] points, Color colour)
        {
            Handles.color = new Color(0f, 0f, 0f, 0.6f);
            Handles.DrawAAPolyLine(5f, points);
            Handles.color = colour;
            Handles.DrawAAPolyLine(2.5f, points);
        }

        private static void DrawStatus(string text)
        {
            Handles.BeginGUI();
            var style = new GUIStyle(EditorStyles.helpBox) { alignment = TextAnchor.MiddleCenter, fontSize = 12 };
            var content = new GUIContent("Castle & Village Builder: " + text);
            Vector2 size = style.CalcSize(content) + new Vector2(16f, 6f);
            Rect view = SceneView.currentDrawingSceneView != null ? SceneView.currentDrawingSceneView.position : new Rect(0, 0, 600, 400);
            GUI.Label(new Rect((view.width - size.x) * 0.5f, 8f, size.x, size.y), content, style);
            Handles.EndGUI();
        }

        // ================================================================ Prefs

        private static Color ColorPref(string key, Color fallback) =>
            ColorUtility.TryParseHtmlString(EditorPrefs.GetString(Prefs + key, ""), out Color c) ? c : fallback;

        private void LoadPrefs()
        {
            mode = (Mode)EditorPrefs.GetInt(Prefs + "Mode", (int)Mode.Brush);
            brush = EditorPrefs.GetFloat(Prefs + "Brush", 20f);
            perDab = EditorPrefs.GetInt(Prefs + "PerDab", 2);
            gap = EditorPrefs.GetFloat(Prefs + "Gap", 3f);
            maxStep = EditorPrefs.GetFloat(Prefs + "MaxStep", 2f);
            turning = (Turning)EditorPrefs.GetInt(Prefs + "Turning", (int)Turning.Quarter);
            clearScatter = EditorPrefs.GetBool(Prefs + "ClearScatter", true);
            eraser = EditorPrefs.GetBool(Prefs + "Eraser", false);
            ranges.Width = new Vector2Int(EditorPrefs.GetInt(Prefs + "WidthMin", 2), EditorPrefs.GetInt(Prefs + "WidthMax", 4));
            ranges.Length = new Vector2Int(EditorPrefs.GetInt(Prefs + "LengthMin", 2), EditorPrefs.GetInt(Prefs + "LengthMax", 6));
            ranges.Storeys = new Vector2Int(EditorPrefs.GetInt(Prefs + "StoreysMin", 1), EditorPrefs.GetInt(Prefs + "StoreysMax", 2));
            ranges.Stone = EditorPrefs.GetBool(Prefs + "Stone", true);
            ranges.Timber = EditorPrefs.GetBool(Prefs + "Timber", true);
            ranges.Plaster = EditorPrefs.GetBool(Prefs + "Plaster", true);
            ranges.WindowChance = EditorPrefs.GetFloat(Prefs + "Windows", 0.55f);
            ranges.ShutterChance = EditorPrefs.GetFloat(Prefs + "Shutters", 0.4f);
            ranges.ChimneyChance = EditorPrefs.GetFloat(Prefs + "Chimneys", 0.6f);
            ranges.Footing = EditorPrefs.GetBool(Prefs + "Footing", true);
            ranges.HideInterior = EditorPrefs.GetBool(Prefs + "HideInterior", true);
            decorate = EditorPrefs.GetBool(Prefs + "Decorate", true);
            natureSpread = EditorPrefs.GetFloat(Prefs + "NatureSpread", 5f);
            keepDoorClear = EditorPrefs.GetBool(Prefs + "KeepDoorClear", true);
            natureAmounts.Clear(); // (read as they're needed)
            tab = (Tab)EditorPrefs.GetInt(Prefs + "Tab", (int)Tab.Houses);
            autoPaths = EditorPrefs.GetBool(Prefs + "AutoPaths", true);
            pathStyle.Look = (BuildingPaths.Look)EditorPrefs.GetInt(Prefs + "PathLook", 0);
            pathStyle.Colour = ColorPref("PathColour", new Color(0.62f, 0.42f, 0.27f));
            pathStyle.Layer = EditorPrefs.GetInt(Prefs + "PathLayer", 1);
            pathStyle.Width = EditorPrefs.GetFloat(Prefs + "PathWidth", 2.2f);
            pathStyle.Wobble = EditorPrefs.GetFloat(Prefs + "PathWobble", 0.35f);
            pathStyle.Longest = EditorPrefs.GetFloat(Prefs + "PathLongest", 60f);
            pathStyle.ClearUnder = EditorPrefs.GetBool(Prefs + "PathClear", true);
            castle.Storeys = EditorPrefs.GetInt(Prefs + "WallStoreys", 4);
            castle.Thickness = EditorPrefs.GetInt(Prefs + "WallThickness", 1);
            castle.TowerCells = EditorPrefs.GetInt(Prefs + "TowerCells", 3);
            castle.TowerExtra = EditorPrefs.GetInt(Prefs + "TowerExtra", 2);
            castle.TowerRoofs = EditorPrefs.GetBool(Prefs + "TowerRoofs", true);
            castle.Battlements = EditorPrefs.GetBool(Prefs + "Battlements", true);
            castle.TowerEvery = EditorPrefs.GetFloat(Prefs + "TowerEvery", 40f);
            castleTheme = EditorPrefs.GetString(Prefs + "CastleTheme", "");
            wallDraw = (WallDraw)EditorPrefs.GetInt(Prefs + "WallDraw", 0);
            castle.WatchTowers = EditorPrefs.GetBool(Prefs + "WatchTowers", true);
            castle.Style = (CastleWallBuilder.TowerStyle)EditorPrefs.GetInt(Prefs + "TowerStyle", EditorPrefs.GetBool(Prefs + "RoundTowers", false) ? 1 : 0);
            castle.Bend = EditorPrefs.GetFloat(Prefs + "WallBend", 6f);
            LoadDetails();
            LoadFeaturePrefs();
            LoadDetailPrefs();
        }

        private void SavePrefs()
        {
            EditorPrefs.SetInt(Prefs + "Mode", (int)mode);
            EditorPrefs.SetFloat(Prefs + "Brush", brush);
            EditorPrefs.SetInt(Prefs + "PerDab", perDab);
            EditorPrefs.SetFloat(Prefs + "Gap", gap);
            EditorPrefs.SetFloat(Prefs + "MaxStep", maxStep);
            EditorPrefs.SetInt(Prefs + "Turning", (int)turning);
            EditorPrefs.SetBool(Prefs + "ClearScatter", clearScatter);
            EditorPrefs.SetBool(Prefs + "Eraser", eraser);
            EditorPrefs.SetInt(Prefs + "WidthMin", ranges.Width.x);
            EditorPrefs.SetInt(Prefs + "WidthMax", ranges.Width.y);
            EditorPrefs.SetInt(Prefs + "LengthMin", ranges.Length.x);
            EditorPrefs.SetInt(Prefs + "LengthMax", ranges.Length.y);
            EditorPrefs.SetInt(Prefs + "StoreysMin", ranges.Storeys.x);
            EditorPrefs.SetInt(Prefs + "StoreysMax", ranges.Storeys.y);
            EditorPrefs.SetBool(Prefs + "Stone", ranges.Stone);
            EditorPrefs.SetBool(Prefs + "Timber", ranges.Timber);
            EditorPrefs.SetBool(Prefs + "Plaster", ranges.Plaster);
            EditorPrefs.SetFloat(Prefs + "Windows", ranges.WindowChance);
            EditorPrefs.SetFloat(Prefs + "Shutters", ranges.ShutterChance);
            EditorPrefs.SetFloat(Prefs + "Chimneys", ranges.ChimneyChance);
            EditorPrefs.SetBool(Prefs + "Footing", ranges.Footing);
            EditorPrefs.SetBool(Prefs + "HideInterior", ranges.HideInterior);
            EditorPrefs.SetBool(Prefs + "Decorate", decorate);
            EditorPrefs.SetFloat(Prefs + "NatureSpread", natureSpread);
            EditorPrefs.SetBool(Prefs + "KeepDoorClear", keepDoorClear);
            foreach (var pair in natureAmounts) EditorPrefs.SetInt(Prefs + "Nature." + pair.Key, pair.Value);
            EditorPrefs.SetInt(Prefs + "Tab", (int)tab);
            EditorPrefs.SetBool(Prefs + "AutoPaths", autoPaths);
            EditorPrefs.SetInt(Prefs + "PathLook", (int)pathStyle.Look);
            EditorPrefs.SetString(Prefs + "PathColour", "#" + ColorUtility.ToHtmlStringRGB(pathStyle.Colour));
            EditorPrefs.SetInt(Prefs + "PathLayer", pathStyle.Layer);
            EditorPrefs.SetFloat(Prefs + "PathWidth", pathStyle.Width);
            EditorPrefs.SetFloat(Prefs + "PathWobble", pathStyle.Wobble);
            EditorPrefs.SetFloat(Prefs + "PathLongest", pathStyle.Longest);
            EditorPrefs.SetBool(Prefs + "PathClear", pathStyle.ClearUnder);
            EditorPrefs.SetInt(Prefs + "WallStoreys", castle.Storeys);
            EditorPrefs.SetInt(Prefs + "WallThickness", castle.Thickness);
            EditorPrefs.SetInt(Prefs + "TowerCells", castle.TowerCells);
            EditorPrefs.SetInt(Prefs + "TowerExtra", castle.TowerExtra);
            EditorPrefs.SetBool(Prefs + "TowerRoofs", castle.TowerRoofs);
            EditorPrefs.SetBool(Prefs + "Battlements", castle.Battlements);
            EditorPrefs.SetFloat(Prefs + "TowerEvery", castle.TowerEvery);
            EditorPrefs.SetString(Prefs + "CastleTheme", castleTheme ?? "");
            EditorPrefs.SetInt(Prefs + "WallDraw", (int)wallDraw);
            EditorPrefs.SetBool(Prefs + "WatchTowers", castle.WatchTowers);
            EditorPrefs.SetInt(Prefs + "TowerStyle", (int)castle.Style);
            EditorPrefs.SetFloat(Prefs + "WallBend", castle.Bend);
            SaveDetails();
            SaveFeaturePrefs();
            SaveDetailPrefs();
        }
    }
}
#endif
