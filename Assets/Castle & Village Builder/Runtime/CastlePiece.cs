using System.Collections.Generic;
using UnityEngine;

namespace YaraJ.CastleVillageBuilder
{
    /// <summary>
    /// A tower or a wall put down by Tools > Castle & Village Builder > Castle Walls: the ground it covers (so
    /// houses keep off it, and the tool can join new walls onto its towers or rub it out), and for a wall the line it
    /// runs along (so it can be made again, say with a gate).
    /// </summary>
    [DisallowMultipleComponent, ExecuteAlways]
    public class CastlePiece : MonoBehaviour
    {
        [Tooltip("A tower (walls join onto it), or a wall.")]
        public bool tower;
        [Tooltip("A bend in a wall (a pillar, from older walls): walls join onto it too.")]
        public bool joint;
        [Tooltip("A round tower (walls meet its curve).")]
        public bool round;
        [Tooltip("The ground it covers, roughly (metres along x and z).")]
        public Vector2 footprint = new Vector2(6f, 6f);
        [Tooltip("A tower: how wide it is. A wall: how thick.")]
        public float width;

        [Header("Walls")]
        [Tooltip("The line the wall runs along (in the world, from end to end; from tower middle to tower middle).")]
        public List<Vector3> path = new List<Vector3>();
        [Tooltip("It goes right round (back to its start).")]
        public bool closed;
        [Tooltip("How far its ends are cut back from the ends of its line (into the towers there).")]
        public float fromHalf, toHalf;
        [Tooltip("It has a gateway through it.")]
        public bool gate;
        [Tooltip("Where its gateways are: their middles, in metres along its line from the start.")]
        public List<float> gates = new List<float>();
        [Tooltip("Which side of its line is the inside of the castle (1: to the right going along it, -1: the left).")]
        public int inner = 1;
        [HideInInspector] public Vector3 from, to; // (walls from before the line was kept: their ends)

        [Tooltip("A wall: how wide its gateways are (m).")]
        public float gateWidth = 3.4f;

        // ---------------------------------------------------------------- Keeping grass out of the gateways

        private static readonly HashSet<CastlePiece> gated = new HashSet<CastlePiece>();
        private List<(Vector3 centre, Vector3 along, Vector3 across, Vector2 half)> clearings;

        private void OnEnable()
        {
            clearings = null;
            if (gates != null && gates.Count > 0) gated.Add(this);
        }

        private void OnDisable()
        {
            gated.Remove(this);
            GrassChanged();
        }

        /// <summary>True if a point is in one of the castle's gateways (or within 'margin' of one): no grass grows there.</summary>
        public static bool KeepsGrassOff(Vector3 point, float margin)
        {
            foreach (CastlePiece wall in gated)
            {
                if (wall == null) continue;
                foreach (var c in wall.Clearings())
                {
                    Vector3 d = point - c.centre;
                    if (Mathf.Abs(Vector3.Dot(d, c.along)) <= c.half.x + margin && Mathf.Abs(Vector3.Dot(d, c.across)) <= c.half.y + margin) return true;
                }
            }
            return false;
        }

        /// <summary>After a wall's gateways are made (or it goes): the grass round them is worked out again.</summary>
        public void GrassChanged()
        {
            if (gates == null || gates.Count == 0) return;
            if (isActiveAndEnabled) gated.Add(this);
            clearings = null;
            foreach (var c in Clearings())
            {
                float reach = c.half.magnitude + 1f;
                GrassAreaChanged(new Rect(c.centre.x - reach, c.centre.z - reach, reach * 2f, reach * 2f));
            }
        }

        // Each gateway's way through: across the wall's whole thickness and a metre and a half out from each face
        private List<(Vector3 centre, Vector3 along, Vector3 across, Vector2 half)> Clearings()
        {
            if (clearings != null) return clearings;
            clearings = new List<(Vector3 centre, Vector3 along, Vector3 across, Vector2 half)>();
            if (path == null || path.Count < 2 || gates == null) return clearings;
            foreach (float g in gates)
            {
                float left = g;
                int count = path.Count, segments = closed ? count : count - 1;
                for (int i = 0; i < segments; i++)
                {
                    Vector3 a = path[i], b = path[(i + 1) % count];
                    a.y = b.y = 0f;
                    float length = Vector3.Distance(a, b);
                    if (left > length && i < segments - 1) { left -= length; continue; }
                    Vector3 along = length > 0.001f ? (b - a) / length : Vector3.forward;
                    Vector3 across = Vector3.Cross(along, Vector3.down).normalized;
                    Vector3 centre = a + along * Mathf.Clamp(left, 0f, length);
                    clearings.Add((centre, along, across, new Vector2(Mathf.Max(gateWidth, 2f) * 0.5f + 0.15f, Mathf.Max(width, 2f) * 0.5f + 1.5f)));
                    break;
                }
            }
            return clearings;
        }

        // ---------------------------------------------------------------- Paint Land's grass (if it's in the project)

        private static readonly string[] GrassTypes = { "YaraJ.PaintLand.GrassField", "PlayerMovement.GrassField" };
        private static List<System.Reflection.MethodInfo> areaChanged;

#if UNITY_EDITOR
        [UnityEditor.InitializeOnLoadMethod]
#endif
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void LinkGrass()
        {
            areaChanged = new List<System.Reflection.MethodInfo>();
            foreach (System.Type field in GrassFields())
            {
                // Its grass asks the castle whether a spot's in a gateway before growing there
                const System.Reflection.BindingFlags any = System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic;
                if (field.GetField("GrassBlockers", any)?.GetValue(null) is List<System.Func<Vector3, float, bool>> blockers)
                {
                    System.Func<Vector3, float, bool> keepOff = KeepsGrassOff;
                    if (!blockers.Contains(keepOff)) blockers.Add(keepOff);
                }
                System.Reflection.MethodInfo changed = field.GetMethod("WorldAreaChanged", any, null, new[] { typeof(Rect) }, null);
                if (changed != null) areaChanged.Add(changed);
            }
        }

        private static IEnumerable<System.Type> GrassFields()
        {
            foreach (System.Reflection.Assembly assembly in System.AppDomain.CurrentDomain.GetAssemblies())
            {
                foreach (string name in GrassTypes)
                {
                    System.Type t = assembly.GetType(name, false);
                    if (t != null) yield return t;
                }
            }
        }

        private static void GrassAreaChanged(Rect world)
        {
            if (areaChanged == null) LinkGrass();
            foreach (System.Reflection.MethodInfo m in areaChanged) m.Invoke(null, new object[] { world });
        }

        /// <summary>Half a tower's width, for walls to stop at (its footprint for older ones).</summary>
        public float Radius => width > 0f ? width * 0.5f : footprint.x * 0.5f;

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0.8f, 0.8f, 1f, 0.8f);
            if (path != null && path.Count >= 2)
            {
                for (int i = 0; i < path.Count - 1; i++) Gizmos.DrawLine(path[i] + Vector3.up * 0.2f, path[i + 1] + Vector3.up * 0.2f);
                if (closed) Gizmos.DrawLine(path[path.Count - 1] + Vector3.up * 0.2f, path[0] + Vector3.up * 0.2f);
                return;
            }
            Gizmos.matrix = Matrix4x4.TRS(transform.position, Quaternion.Euler(0f, transform.eulerAngles.y, 0f), Vector3.one);
            Gizmos.DrawWireCube(new Vector3(0f, 0.05f, 0f), new Vector3(footprint.x, 0.1f, footprint.y));
        }
    }
}
