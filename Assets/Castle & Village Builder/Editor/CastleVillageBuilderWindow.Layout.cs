#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace YaraJ.CastleVillageBuilder
{
    // How the window looks: a title bar, the tabs, the start button, then each tab's tool and its settings in panels
    internal sealed partial class CastleVillageBuilderWindow
    {
        private static readonly Color Accent = new Color(0.95f, 0.68f, 0.25f);
        private static GUIStyle titleStyle, sectionTitle, sectionNote, panel, howTo, toolCaption;
        private bool sectionOpen;

        private static Color Shade(float dark, float light) => EditorGUIUtility.isProSkin ? new Color(dark, dark, dark) : new Color(light, light, light);

        private static void Styles()
        {
            if (panel != null) return;
            titleStyle = new GUIStyle(EditorStyles.boldLabel) { fontSize = 14, alignment = TextAnchor.MiddleLeft };
            sectionTitle = new GUIStyle(EditorStyles.foldout) { fontStyle = FontStyle.Bold, fontSize = 12 };
            sectionNote = new GUIStyle(EditorStyles.miniLabel) { alignment = TextAnchor.MiddleRight };
            panel = new GUIStyle(EditorStyles.helpBox) { padding = new RectOffset(10, 8, 7, 8), margin = new RectOffset(2, 2, 0, 2) };
            howTo = new GUIStyle(EditorStyles.helpBox) { fontSize = 11, wordWrap = true, richText = true, padding = new RectOffset(8, 8, 6, 6) };
            toolCaption = new GUIStyle(EditorStyles.miniBoldLabel);
        }

        // The window's title, and where it's building (Paint Land's land or any ground)
        private void DrawTitle()
        {
            Rect r = GUILayoutUtility.GetRect(1f, 30f, GUILayout.ExpandWidth(true));
            EditorGUI.DrawRect(r, Shade(0.16f, 0.74f));
            EditorGUI.DrawRect(new Rect(r.x, r.yMax - 2f, r.width, 2f), Accent);
            GUI.Label(new Rect(r.x + 10f, r.y, r.width - 20f, r.height - 2f), "Castle & Village Builder", titleStyle);
            string ground = groundIsLand ? "Building on Paint Land's land" : "Building on any ground";
            GUI.Label(new Rect(r.x + 10f, r.y, r.width - 20f, r.height - 2f), ground, sectionNote);
            GUILayout.Space(6);
        }

        // The tabs: a row, or two rows when the window's narrow
        private void DrawTabs()
        {
            var tabs = new[]
            {
                new GUIContent("Houses", "Scatter houses, with their colours, plants and paths."),
                new GUIContent("Castle Walls", "Build giant castle walls with towers and gateways."),
                new GUIContent("Castle Features", "Upper areas, grand stairs, bridges, dressed streets, plazas with fountains, and a keep."),
                new GUIContent("Details", "Props: barrels, sacks, hay, market stalls, wells, lamps, braziers, benches, weapon racks... and fences.")
            };
            int columns = position.width < 430f ? 2 : 4;
            var style = new GUIStyle(EditorStyles.toolbarButton) { fixedHeight = 0, fontSize = 12, fontStyle = FontStyle.Bold };
            EditorGUI.BeginChangeCheck();
            tab = (Tab)GUILayout.SelectionGrid((int)tab, tabs, columns, style, GUILayout.Height(columns == 2 ? 52f : 28f));
            if (EditorGUI.EndChangeCheck()) { EndWallChain(); FlushPaths(); Changed(); }
            GUILayout.Space(6);
        }

        // A tab's tool picker, with a caption
        private int ToolPicker(string caption, int value, GUIContent[] tools, int columns = 0)
        {
            GUILayout.Space(4);
            GUILayout.Label(caption, toolCaption);
            if (columns <= 0) columns = tools.Length;
            if (position.width < 430f && tools.Length > 3) columns = Mathf.Min(columns, 3);
            int rows = Mathf.CeilToInt(tools.Length / (float)columns);
            return GUILayout.SelectionGrid(value, tools, columns, GUILayout.Height(26f * rows));
        }

        // How to use the tool picked: one box, under the tool picker
        private static void HowTo(string text)
        {
            Styles();
            GUILayout.Label("<b>How to</b>  " + text, howTo);
        }

        /// <summary>
        /// A panel: a header bar (click to fold it), then its settings in a box (closed by the next panel, or the end of
        /// the tab). 'note' shows on the right of the header.
        /// </summary>
        private bool Section(string key, string title, bool startOpen, string note = null)
        {
            Styles();
            CloseSection();
            bool open = EditorPrefs.GetBool(Prefs + "Open." + key, startOpen);
            GUILayout.Space(5);
            Rect r = GUILayoutUtility.GetRect(1f, 22f, GUILayout.ExpandWidth(true));
            EditorGUI.DrawRect(r, Shade(0.2f, 0.8f));
            EditorGUI.DrawRect(new Rect(r.x, r.y, 3f, r.height), Accent);
            bool now = EditorGUI.Foldout(new Rect(r.x + 16f, r.y + 2f, r.width - 20f, r.height - 4f), open, title, true, sectionTitle);
            if (!string.IsNullOrEmpty(note)) GUI.Label(new Rect(r.x, r.y, r.width - 8f, r.height), note, sectionNote);
            if (now != open) EditorPrefs.SetBool(Prefs + "Open." + key, now);
            if (now)
            {
                EditorGUILayout.BeginVertical(panel);
                sectionOpen = true;
            }
            return now;
        }

        private void CloseSection()
        {
            if (!sectionOpen) return;
            sectionOpen = false;
            EditorGUILayout.EndVertical();
        }

        // A small heading inside a panel
        private static void SubHeading(string text)
        {
            GUILayout.Space(3);
            GUILayout.Label(text, EditorStyles.miniBoldLabel);
        }
    }
}
#endif
