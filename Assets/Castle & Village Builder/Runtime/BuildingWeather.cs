using UnityEngine;

namespace YaraJ.CastleVillageBuilder
{
    /// <summary>
    /// How wet the buildings look (the Building Lit shader's rain): darker, richer colours and a sharp shine on wet
    /// stone, wood and tiles. Put one in the scene and set Wetness, or drive it from your own weather (set Wetness from
    /// your script, or have your weather set the shader globals _Wetness, _RainNow and _WetOvercast itself and leave
    /// this out). With "Dry Out" on, the buildings dry slowly when Raining is turned off.
    /// </summary>
    [ExecuteAlways, AddComponentMenu("Castle & Village Builder/Building Weather")]
    public class BuildingWeather : MonoBehaviour
    {
        [Tooltip("How wet the buildings are (0: dry, 1: soaked).")]
        [Range(0f, 1f)] public float wetness;
        [Tooltip("It's raining now: the buildings get wetter (at Wet Speed), up to soaked.")]
        public bool raining;
        [Tooltip("How hard it's raining (0-1).")]
        [Range(0f, 1f)] public float rainStrength = 0.7f;
        [Tooltip("How overcast it is (0-1): a hidden sun hardly glints on anything wet.")]
        [Range(0f, 1f)] public float overcast = 0.6f;
        [Tooltip("How fast they get wet in the rain (wetness per second).")]
        public float wetSpeed = 0.05f;
        [Tooltip("When it isn't raining, they dry out slowly (at Dry Speed).")]
        public bool dryOut = true;
        [Tooltip("How fast they dry (wetness per second).")]
        public float drySpeed = 0.01f;

        private static readonly int WetnessId = Shader.PropertyToID("_Wetness");
        private static readonly int RainNowId = Shader.PropertyToID("_RainNow");
        private static readonly int OvercastId = Shader.PropertyToID("_WetOvercast");

        private void Update()
        {
            if (Application.isPlaying)
            {
                if (raining) wetness = Mathf.MoveTowards(wetness, 1f, wetSpeed * Time.deltaTime);
                else if (dryOut) wetness = Mathf.MoveTowards(wetness, 0f, drySpeed * Time.deltaTime);
            }
            Apply();
        }

        private void OnValidate() => Apply();

        private void OnDisable()
        {
            Shader.SetGlobalFloat(WetnessId, 0f);
            Shader.SetGlobalFloat(RainNowId, 0f);
            Shader.SetGlobalFloat(OvercastId, 0f);
        }

        private void Apply()
        {
            if (!isActiveAndEnabled) return;
            Shader.SetGlobalFloat(WetnessId, wetness);
            Shader.SetGlobalFloat(RainNowId, raining ? rainStrength : 0f);
            Shader.SetGlobalFloat(OvercastId, raining ? Mathf.Max(overcast, rainStrength * 0.8f) : overcast * wetness);
        }
    }
}
