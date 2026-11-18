using UnityEngine;

namespace YaraJ.CastleVillageBuilder
{
    /// <summary>
    /// A building put down by Tools > Castle & Village Builder: how much ground it takes up (so the tool keeps
    /// others off it and can rub it out) and how it was made (so it can be made again).
    /// </summary>
    [DisallowMultipleComponent]
    public class ScatteredBuilding : MonoBehaviour
    {
        [Tooltip("The ground it covers, roof overhang included (metres across, along its own x and z).")]
        public Vector2 footprint = new Vector2(6f, 8f);
        [Tooltip("The random seed it was made from.")]
        public int seed;
        [Tooltip("Width and length in 2 m cells, and how many storeys.")]
        public Vector3Int cells = new Vector3Int(3, 4, 1);
        [Tooltip("Its colour theme (Castle & Village Builder > Themes).")]
        public string theme;
        [Tooltip("The plants and rocks put round it (they're with the land's scatter; they go when it's rubbed out).")]
        public System.Collections.Generic.List<GameObject> decorations = new System.Collections.Generic.List<GameObject>();

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(1f, 0.8f, 0.3f, 0.8f);
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.DrawWireCube(new Vector3(0f, 0.05f, 0f), new Vector3(footprint.x, 0.1f, footprint.y));
        }
    }
}
