using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace YaraJ.CastleVillageBuilder
{
    /// <summary>
    /// Editor only. Castle & Village Builder works on any ground with a collider (a Unity Terrain, or a mesh). If Paint
    /// Land is in the project too, this finds it (without needing it to build) and lets the builder use it: its land's
    /// shape for the ground, its water, its scatter kinds for the plants round houses, its painted paths and textures for
    /// the paths between them, and its hooks so houses move when the land's reshaped and grass keeps out of gateways.
    /// </summary>
    [InitializeOnLoad]
    internal static class PaintLandLink
    {
        private const BindingFlags Any = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
        private static readonly string[] Spaces = { "YaraJ.PaintLand.", "PlayerMovement." };
        private static string space; // the Paint Land in use (the one whose land is in the scene)

        static PaintLandLink()
        {
            // Houses move with Paint Land's land when it's reshaped: every Paint Land in the project is told about them
            foreach (string sp in Spaces)
            {
                Type builder = TypeOf(sp, "LandBuilder");
                FieldInfo followers = builder?.GetField("GroundFollowers", Any);
                if (followers?.GetValue(null) is List<Func<Transform, Vector3?>> list && !list.Contains(HouseGround)) list.Add(HouseGround);
            }
        }

        // Where the ground under a house is read (at its front door, which it's kept level with), or null if it isn't one
        private static Vector3? HouseGround(Transform t)
        {
            if (t == null || !t.TryGetComponent(out ScatteredBuilding house)) return null;
            Vector3 step = BuildingPaths.Doorstep(house, out Vector3 outward);
            return step - outward * 0.8f;
        }

        // ---------------------------------------------------------------- Finding it

        private static readonly Dictionary<string, Type> types = new Dictionary<string, Type>();

        private static Type TypeOf(string sp, string name)
        {
            string full = sp + name;
            if (types.TryGetValue(full, out Type found)) return found;
            foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                found = assembly.GetType(full, false);
                if (found != null) break;
            }
            types[full] = found;
            return found;
        }

        private static Type T(string name) => space != null ? TypeOf(space, name) : null;

        /// <summary>Is Paint Land in the project?</summary>
        internal static bool Installed
        {
            get
            {
                foreach (string sp in Spaces) if (TypeOf(sp, "LandPaint") != null) return true;
                return false;
            }
        }

        /// <summary>Is there a Paint Land land in the scene, ready to work on (found and loaded)?</summary>
        internal static bool Available()
        {
            if (!BuilderSettings.Get().usePaintLand) { space = null; return false; }
            if (space != null && Call<string>(TypeOf(space, "LandPaint"), "Load") == null) return true;
            space = null;
            foreach (string sp in Spaces)
            {
                Type paint = TypeOf(sp, "LandPaint");
                if (paint == null) continue;
                if (Call<string>(paint, "Load") != null) continue;
                space = sp;
                return true;
            }
            return false;
        }

        private static TResult Call<TResult>(Type type, string name, params object[] args)
        {
            MethodInfo m = null;
            if (type != null)
            {
                foreach (MethodInfo candidate in type.GetMethods(Any))
                {
                    if (candidate.Name != name || candidate.GetParameters().Length != args.Length) continue;
                    m = candidate;
                    break;
                }
            }
            if (m == null) return default;
            object result = m.Invoke(null, args);
            return result is TResult r ? r : default;
        }

        private static object Get(Type type, string member)
        {
            if (type == null) return null;
            PropertyInfo p = type.GetProperty(member, Any);
            if (p != null) return p.GetValue(null);
            FieldInfo f = type.GetField(member, Any);
            return f?.GetValue(null);
        }

        private static object Field(object obj, string name) => obj?.GetType().GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)?.GetValue(obj);

        // ---------------------------------------------------------------- The ground

        internal static GameObject Land => Get(T("LandPaint"), "Land") as GameObject;

        internal static bool Raycast(Ray ray, out Vector3 point, out Vector3 normal)
        {
            point = default;
            normal = Vector3.up;
            Type paint = T("LandPaint");
            MethodInfo m = null;
            if (paint != null) foreach (MethodInfo c in paint.GetMethods(Any)) if (c.Name == "Raycast" && c.GetParameters().Length == 2) m = c;
            if (m == null) return false;
            var args = new object[] { ray, null };
            if (!(m.Invoke(null, args) is bool hit) || !hit) return false;
            point = (Vector3)Field(args[1], "Point");
            normal = (Vector3)Field(args[1], "Normal");
            return true;
        }

        internal static bool GroundAt(Vector3 world, out Vector3 ground, out Vector3 normal)
        {
            ground = world;
            normal = Vector3.up;
            Type paint = T("LandPaint");
            if (paint == null) return false;
            MethodInfo m = paint.GetMethod("GroundAt", Any, null, new[] { typeof(Vector3), typeof(Vector3).MakeByRefType(), typeof(Vector3).MakeByRefType() }, null);
            if (m == null) return false;
            var args = new object[] { world, null, null };
            if (!(m.Invoke(null, args) is bool found) || !found) return false;
            ground = (Vector3)args[1];
            normal = (Vector3)args[2];
            return true;
        }

        internal static bool UnderWater(Vector3 point, float margin)
        {
            Type water = T("LandWater");
            MethodInfo m = water?.GetMethod("UnderWater", Any, null, new[] { typeof(Vector3), typeof(float) }, null);
            return m != null && m.Invoke(null, new object[] { point, margin }) is bool under && under;
        }

        // ---------------------------------------------------------------- Its scatter (plants, rocks...)

        internal static string ScatterRootName => (Get(T("LandScatter"), "ContainerName") as string) ?? "Land Scatter";

        internal static bool IsScattered(Transform t)
        {
            if (space == null) return false;
            return Call<bool>(T("LandScatter"), "IsScattered", t);
        }

        private static object Settings => space == null ? null : Call<object>(T("LandBuilder"), "GetSettings");

        /// <summary>Paint Land's scatter kinds (its Land Settings), as nature to put round houses.</summary>
        internal static List<NatureKind> Kinds()
        {
            var kinds = new List<NatureKind>();
            if (!(Field(Settings, "scatter") is IList groups)) return kinds;
            foreach (object group in groups)
            {
                if (group == null) continue;
                var kind = new NatureKind
                {
                    Name = Field(group, "name") as string ?? "Kind",
                    MaxSlope = Field(group, "maxSlope") is float slope ? slope : 90f,
                    Size = Field(group, "size") is Vector2 size ? size : Vector2.one,
                    Source = group
                };
                if (Field(group, "entries") is IList entries)
                {
                    foreach (object entry in entries)
                    {
                        if (Field(entry, "prefab") is GameObject prefab && (!(Field(entry, "on") is bool on) || on)) kind.Prefabs.Add(prefab);
                    }
                }
                if (kind.Prefabs.Count > 0) kinds.Add(kind);
            }
            return kinds;
        }

        /// <summary>A clump of one of Paint Land's kinds, as its Scatter brush would put it (null if it can't here).</summary>
        internal static List<GameObject> PlaceClump(NatureKind kind, Vector3 ground, Vector3 normal, float plantRadius)
        {
            if (kind?.Source == null) return null;
            MethodInfo m = null;
            Type scatter = T("LandScatter");
            if (scatter != null) foreach (MethodInfo c in scatter.GetMethods(Any)) if (c.Name == "PlaceClumpAt" && c.GetParameters().Length == 4) m = c;
            if (m == null) return null;
            return m.Invoke(null, new[] { kind.Source, ground, normal, (object)plantRadius }) as List<GameObject>;
        }

        /// <summary>Its scatter's renderer reads what's under it again (after things are put down or taken away).</summary>
        internal static void RefreshScatter()
        {
            GameObject root = ScatterRoot();
            if (root == null) return;
            foreach (Component c in root.GetComponents<Component>())
            {
                if (c != null && c.GetType().Name == "ScatterRenderer") c.GetType().GetMethod("Refresh", BindingFlags.Instance | BindingFlags.Public)?.Invoke(c, null);
            }
        }

        internal static GameObject ScatterRoot()
        {
            string name = ScatterRootName;
            foreach (GameObject root in UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects())
            {
                if (root.name == name) return root;
            }
            return null;
        }

        // ---------------------------------------------------------------- Its paths and textures

        /// <summary>Its colours to pick from (Land Settings > palette).</summary>
        internal static List<(string name, Color colour)> Palette()
        {
            var palette = new List<(string, Color)>();
            if (!(Field(Settings, "palette") is IList colours)) return palette;
            foreach (object c in colours) if (c != null && Field(c, "colour") is Color colour) palette.Add((Field(c, "name") as string ?? "", colour));
            return palette;
        }

        internal static string LoadPaths() => space == null ? "Paint Land isn't ready." : Call<string>(T("LandPaint"), "LoadPaths");
        internal static string LoadTextures() => space == null ? "Paint Land isn't ready." : Call<string>(T("LandTextures"), "Load");

        internal static void StampPath(Vector2 at, float radius, Color32 colour) => Call<object>(T("LandPaint"), "StampPath", at, radius, colour, false);
        internal static void StampTexture(Vector2 at, float radius, int layer) => Call<object>(T("LandTextures"), "Stamp", at, radius, layer, 1f, 0.35f);

        internal static void BeginPaint(string name)
        {
            Call<object>(T("LandPaint"), "OpenUndoGroup", name);
            T("LandPaint")?.GetField("Busy", Any)?.SetValue(null, true);
        }

        internal static void EndPaint(string name, bool textures)
        {
            if (textures) Call<object>(T("LandTextures"), "EndStroke", name);
            Call<object>(T("LandPaint"), "EndStroke");
            Call<object>(T("LandPaint"), "CloseUndoGroup");
        }

        internal static bool BeginClearing()
        {
            object settings = Settings;
            return settings != null && Call<bool>(T("LandScatter"), "BeginClearing", settings, false);
        }

        internal static void ClearAt(Vector3 centre, float radius)
        {
            Type scatter = T("LandScatter");
            scatter?.GetMethod("ClearAt", Any, null, new[] { typeof(Vector3), typeof(float) }, null)?.Invoke(null, new object[] { centre, radius });
        }

        internal static void EndClearing() => Call<object>(T("LandScatter"), "EndClearing");

        internal static Material TextureMaterial => Get(T("LandTextures"), "Material") as Material;
        internal static bool TexturesSupported(Material m) => m != null && Call<bool>(T("LandTextures"), "Supported", m);
        internal static string TextureName(Material m, int layer) => Call<string>(T("LandTextures"), "Name", Settings, m, layer) ?? $"Texture {layer}";
        internal static Texture TextureOf(Material m, int layer) => Call<Texture>(T("LandTextures"), "LayerTexture", m, layer);

        /// <summary>The land's space (paths and textures are painted in it).</summary>
        internal static Vector2 ToLand(Vector3 world)
        {
            GameObject land = Land;
            Vector3 local = land != null ? land.transform.InverseTransformPoint(world) : world;
            return new Vector2(local.x, local.z);
        }
    }

    /// <summary>A kind of nature to put round houses: Paint Land's scatter kinds, or the builder's own list.</summary>
    internal sealed class NatureKind
    {
        public string Name;
        public float MaxSlope = 90f;
        public Vector2 Size = Vector2.one;
        public readonly List<GameObject> Prefabs = new List<GameObject>();
        public object Source; // (Paint Land's own kind, if it's one of its)
    }
}
