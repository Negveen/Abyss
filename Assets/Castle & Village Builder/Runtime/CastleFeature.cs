using UnityEngine;

namespace YaraJ.CastleVillageBuilder
{
    /// <summary>
    /// Something built with Tools > Castle & Village Builder > Castle Features: an upper area (a raised terrace), grand
    /// stairs, a bridge, a dressed street, a plaza with a fountain, or a keep. Walkable ones (terraces, bridges, plazas)
    /// count as ground: houses, props and paths can go on them. The rest keep houses off.
    /// </summary>
    [DisallowMultipleComponent]
    public class CastleFeature : MonoBehaviour
    {
        public enum Kind { UpperArea, Stairs, Bridge, Street, Plaza, Keep }

        [Tooltip("What it is.")]
        public Kind kind;
        [Tooltip("It's ground: houses, props and paths can go on top of it.")]
        public bool walkable;
        [Tooltip("Where it was drawn from and to (in the world).")]
        public Vector3 from, to;
        [Tooltip("Which way it was turned (degrees).")]
        public float yaw;

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(1f, 0.85f, 0.45f, 0.8f);
            Gizmos.DrawLine(from + Vector3.up * 0.3f, to + Vector3.up * 0.3f);
        }
    }
}
