using System.Collections.Generic;
using UnityEngine;

namespace YaraJ.CastleVillageBuilder
{
    /// <summary>
    /// One side of an upper area's edge (its balustrade, battlements or hedge, and its planters), kept as its own piece
    /// so that stairs arriving there can open a gap in it.
    /// </summary>
    [DisallowMultipleComponent]
    public class CastleFeatureEdge : MonoBehaviour
    {
        [Tooltip("Where the edge runs, from and to (in the world, at the top of the upper area).")]
        public Vector3 from, to;
        [Tooltip("Which way is out, over the side.")]
        public Vector3 outward;
        [Tooltip("What it is: 0 balustrade, 1 battlements, 2 hedge, 3 plain.")]
        public int style;
        [Tooltip("Planters along it.")]
        public bool planters;
        [Tooltip("The openings in it (where stairs arrive): from and to, in metres along it.")]
        public List<Vector2> gaps = new List<Vector2>();
        [Tooltip("The colour theme it's in.")]
        public string theme;
    }
}
